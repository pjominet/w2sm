using System.IO;
using System.Text;
using DiffPlex;
using W2ScriptMerger.Extensions;
using W2ScriptMerger.Models;
using EncodingExtensions = W2ScriptMerger.Extensions.EncodingExtensions;

namespace W2ScriptMerger.Services;

public class ScriptMergeService(ScriptExtractionService extractionService)
{
    private readonly Differ _differ = new();

    public MergeSessionResult StartMergeSession(List<DzipConflict> conflicts)
    {
        var result = new MergeSessionResult();

        foreach (var dzipConflict in conflicts)
        {
            foreach (var scriptConflict in dzipConflict.ScriptConflicts)
            {
                if (scriptConflict.Status is ConflictStatus.AutoResolved or ConflictStatus.ManuallyResolved)
                    continue;

                var mergeResult = AttemptAutoMerge(scriptConflict);

                if (mergeResult.Success)
                {
                    scriptConflict.MergedContent = mergeResult.MergedContent;
                    scriptConflict.Status = ConflictStatus.AutoResolved;
                    result.AutoMergedCount++;

                    extractionService.SaveMergedScript(dzipConflict, scriptConflict);
                }
                else
                {
                    scriptConflict.Status = ConflictStatus.NeedsManualResolution;
                    result.NeedsManualCount++;
                    result.FirstUnresolvedConflict ??= (dzipConflict, scriptConflict);
                }
            }
        }

        result.IsComplete = result.NeedsManualCount == 0;
        return result;
    }

    public void ApplyManualMerge(DzipConflict dzipConflict, ScriptFileConflict scriptConflict, byte[] mergedContent)
    {
        scriptConflict.MergedContent = mergedContent;
        scriptConflict.Status = ConflictStatus.ManuallyResolved;
        extractionService.SaveMergedScript(dzipConflict, scriptConflict);
    }

    public ScriptMergeAttemptResult AttemptAutoMerge(ScriptFileConflict conflict)
    {
        var baseContent = File.ReadAllBytes(conflict.CurrentMergeBasePath ?? conflict.VanillaScriptPath);
        var currentMerge = baseContent;

        foreach (var modVersion in conflict.ModVersions)
        {
            var modContent = File.ReadAllBytes(modVersion.ScriptPath);
            var mergeResult = ThreeWayMerge(currentMerge, modContent);

            if (!mergeResult.Success)
            {
                return new ScriptMergeAttemptResult
                {
                    Success = false,
                    FailedAtMod = modVersion.ModName
                };
            }

            currentMerge = mergeResult.MergedContent!;
        }

        return new ScriptMergeAttemptResult
        {
            Success = true,
            MergedContent = currentMerge
        };
    }

    private ScriptMergeAttemptResult ThreeWayMerge(byte[] baseContent, byte[] modContent)
    {
        var baseText = EncodingExtensions.ReadFileWithEncodingFromBytes(baseContent);
        var modText = EncodingExtensions.ReadFileWithEncodingFromBytes(modContent);

        var baseLines = baseText.Split(["\r\n", "\n"], StringSplitOptions.None);
        var modLines = modText.Split(["\r\n", "\n"], StringSplitOptions.None);

        var diff = _differ.CreateLineDiffs(baseText, modText, ignoreWhitespace: false);

        var baseChanged = GetChangedLineNumbers(diff);

        if (baseChanged.Count == 0)
        {
            return new ScriptMergeAttemptResult
            {
                Success = true,
                MergedContent = modContent
            };
        }

        var operations = new List<MergeOperation>();

        foreach (var block in diff.DiffBlocks)
        {
            if (block.DeleteCountA > 0)
            {
                operations.Add(new MergeOperation
                {
                    Position = block.DeleteStartA,
                    DeleteCount = block.DeleteCountA,
                    InsertLines = null
                });
            }

            if (block.InsertCountB > 0)
            {
                var lines = modLines.Skip(block.InsertStartB).Take(block.InsertCountB).ToList();
                operations.Add(new MergeOperation
                {
                    Position = block.DeleteStartA,
                    DeleteCount = 0,
                    InsertLines = lines
                });
            }
        }

        operations = operations.OrderByDescending(o => o.Position).ToList();

        var mergedLines = baseLines.ToList();

        foreach (var op in operations)
        {
            if (op.DeleteCount > 0)
            {
                var endIndex = Math.Min(op.Position + op.DeleteCount, mergedLines.Count);
                var startIndex = Math.Min(op.Position, mergedLines.Count);
                if (startIndex < endIndex)
                    mergedLines.RemoveRange(startIndex, endIndex - startIndex);
            }

            if (op.InsertLines is { Count: > 0 })
            {
                var insertIndex = Math.Min(op.Position, mergedLines.Count);

                // Check if these lines are already present at or near this position to avoid duplicates
                // This handles cases where multiple mods add the same property/logic
                var existingAtPos = mergedLines.Skip(insertIndex).Take(op.InsertLines.Count).ToList();
                if (!existingAtPos.SequenceEqual(op.InsertLines))
                    mergedLines.InsertRange(insertIndex, op.InsertLines);
            }
        }

        var mergedText = string.Join("\r\n", mergedLines);
        return new ScriptMergeAttemptResult
        {
            Success = true,
            MergedContent = Encoding.ANSI1250.GetBytes(mergedText)
        };
    }

    private static HashSet<int> GetChangedLineNumbers(DiffPlex.Model.DiffResult diff)
    {
        var changed = new HashSet<int>();

        foreach (var block in diff.DiffBlocks)
        {
            for (var i = block.DeleteStartA; i < block.DeleteStartA + block.DeleteCountA; i++)
                changed.Add(i);

            if (block.InsertCountB > 0)
                changed.Add(block.DeleteStartA);
        }

        return changed;
    }

    private class MergeOperation
    {
        public int Position { get; init; }
        public int DeleteCount { get; init; }
        public List<string>? InsertLines { get; init; }
    }
}

public class MergeSessionResult
{
    public int AutoMergedCount { get; set; }
    public int NeedsManualCount { get; set; }
    public bool IsComplete { get; set; }
    public (DzipConflict Dzip, ScriptFileConflict Script)? FirstUnresolvedConflict { get; set; }
}

public class ScriptMergeAttemptResult
{
    public bool Success { get; init; }
    public byte[]? MergedContent { get; init; }
    public string? FailedAtMod { get; init; }
}
