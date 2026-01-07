using System.IO;
using System.Text;
using DiffPlex;
using W2ScriptMerger.Models;

namespace W2ScriptMerger.Services;

public class MergeService(GameFileService gameFileService)
{
    private readonly Differ _differ = new();

    public List<ModConflict> DetectConflicts(List<ModArchive> newModArchives)
    {
        var conflicts = new Dictionary<string, ModConflict>(StringComparer.OrdinalIgnoreCase);

        foreach (var modArchive in newModArchives)
        {
            foreach (var file in modArchive.Files)
            {
                if (!gameFileService.DzipIsIndexed(file.Name))
                    continue;

                var conflict = new ModConflict
                {
                    OriginalFile = gameFileService.GetDzipReference(file.Name).GetCurrentScriptPath()
                };
                conflict.ConflictingFiles.Add($"{modArchive.ModName}/{file.RelativePath}");
                conflicts[file.Name] = conflict;
            }
        }

        return conflicts.Values.ToList();
    }

    public void AttemptAutoMerge(ModConflict conflict)
    {
        // Unpack the base (vanilla) dzip to access its script files for comparison and merging
        var baseFilePath = DzipService.UnpackDzip(conflict.OriginalFile, $"vanilla_{conflict.OriginalFileName}");
        var scriptConflictsToResolve = new List<ScriptConflict>();
        var modCount = 1;

        // Iterate through each conflicting mod file to unpack and collect script conflicts
        foreach (var conflictingFile in conflict.ConflictingFiles)
        {
            // Unpack the mod dzip to access its script files
            var modFilePath = DzipService.UnpackDzip(conflictingFile, $"mod{modCount++}_{Path.GetFileName(conflictingFile)}");
            var modScripts = Directory.GetFiles(modFilePath, "*.ws", SearchOption.AllDirectories);
            foreach (var modScript in modScripts)
            {
                // Calculate the relative path of the script within the dzip for matching with base scripts
                var relativeScriptPath = Path.GetRelativePath(modFilePath, modScript);
                var baseScript = Path.Combine(baseFilePath, relativeScriptPath);
                if (File.Exists(baseScript)) // sanity check: only create a conflict for scripts that exist in the vanilla dzip
                {
                    // Start with the base content for the first merge
                    scriptConflictsToResolve.Add(new ScriptConflict
                    {
                        DzipSource = Path.GetFileName(conflict.OriginalFile),
                        RelativeScriptPath = relativeScriptPath,
                        BaseScriptContent = File.ReadAllBytes(baseScript),
                        ConflictScriptContent = File.ReadAllBytes(modScript)
                    });
                }
            }
        }

        byte[] currentMerge = [];
        // Group conflicts by script path to handle multiple mods modifying the same script
        var groupedConflicts = scriptConflictsToResolve.GroupBy(sc => sc.RelativeScriptPath).ToList();
        if (groupedConflicts.Count == 0)
            return; // no conflicts to resolve

        foreach (var group in groupedConflicts)
        {
            // Process all mod conflicts for this script
            var conflictsForScript = group.ToList();
            currentMerge = conflictsForScript[0].BaseScriptContent;
            foreach (var scriptConflict in conflictsForScript)
            {
                // Attempt to merge the current merge with this mod's changes
                var merged = AttemptAutoMerge(currentMerge, scriptConflict.ConflictScriptContent);
                if (merged is null)
                {
                    // If merge failed due to auto unresolvable conflicts, flag the conflict as needing manual resolution and stop merge
                    conflict.Status = ConflictStatus.NeedsManualResolution;
                    return;
                }

                // Update current content with the merged result for the next mod in sequence
                currentMerge = merged;
            }

            // Populate diff viewer content from the first script group only
            if (group.Key != groupedConflicts[0].Key)
                continue;

            conflict.VanillaContent = conflictsForScript[0].BaseScriptContent;
            foreach (var sc in conflictsForScript)
            {
                conflict.ModVersions.Add(new ModVersion
                {
                    SourceArchive = sc.DzipSource,
                    Content = sc.ConflictScriptContent
                });
            }
        }

        conflict.Status = ConflictStatus.AutoResolved;
        conflict.MergeContent = currentMerge;
    }

    private byte[]? AttemptAutoMerge(byte[] baseScriptContent, byte[] conflictScriptContent)
    {
        var baseText = Encoding.GetEncoding(1250).GetString(baseScriptContent);
        var modText = Encoding.GetEncoding(1250).GetString(conflictScriptContent);

        // Try three-way merge
        var currentMerged = baseText;

        var mergeResult = ThreeWayMerge(baseText, currentMerged, modText);
        if (mergeResult.HasConflicts)
            return null;

        currentMerged = mergeResult.MergedText;

        return Encoding.GetEncoding(1250).GetBytes(currentMerged);
    }

    private MergeResult ThreeWayMerge(string baseText, string leftText, string rightText)
    {
        // Split texts into lines for line-based merging
        var baseLines = baseText.Split('\n');
        var leftLines = leftText.Split('\n');
        var rightLines = rightText.Split('\n');

        // Create diffs between base and each mod version
        var leftDiff = _differ.CreateLineDiffs(baseText, leftText, ignoreWhitespace: false);
        var rightDiff = _differ.CreateLineDiffs(baseText, rightText, ignoreWhitespace: false);

        // Get changed line numbers to check for conflicts
        var leftChanged = GetChangedLineNumbers(leftDiff);
        var rightChanged = GetChangedLineNumbers(rightDiff);

        // If any line is changed by both mods, it's a conflict
        var hasConflicts = leftChanged.Intersect(rightChanged).Any();
        if (hasConflicts)
            return new MergeResult { HasConflicts = true, MergedText = string.Empty };

        // No conflicts, so merge the changes
        // Collect all operations (deletions and insertions) from both diffs
        var operations = new List<(int Position, int Count, bool IsDeletion, object Lines)>();

        foreach (var block in leftDiff.DiffBlocks)
        {
            if (block.DeleteCountA > 0)
                operations.Add((block.DeleteStartA, block.DeleteCountA, true, null)!);
            if (block.InsertCountB <= 0)
                continue;

            var lines = leftLines.Skip(block.InsertStartB).Take(block.InsertCountB).ToList();
            operations.Add((block.DeleteStartA, 0, false, lines));
        }

        foreach (var block in rightDiff.DiffBlocks)
        {
            if (block.DeleteCountA > 0)
                operations.Add((block.DeleteStartA, block.DeleteCountA, true, null)!);
            if (block.InsertCountB <= 0)
                continue;

            var lines = rightLines.Skip(block.InsertStartB).Take(block.InsertCountB).ToList();
            operations.Add((block.DeleteStartA, 0, false, lines));
        }

        // Sort operations by position descending to apply from end to beginning, avoiding index shifts
        operations = operations.OrderByDescending(o => o.Position).ToList();

        // Start with baselines
        var mergedLines = baseLines.ToList();

        // Apply each operation
        foreach (var op in operations)
        {
            if (op.IsDeletion)
            {
                mergedLines.RemoveRange(op.Position, op.Count);
            }
            else if (op.Lines is List<string> lines)
            {
                mergedLines.InsertRange(op.Position, lines);
            }
        }

        // Join the merged lines back into text
        var mergedText = string.Join('\n', mergedLines);
        return new MergeResult { HasConflicts = false, MergedText = mergedText };
    }

    private static HashSet<int> GetChangedLineNumbers(DiffPlex.Model.DiffResult diff)
    {
        var changed = new HashSet<int>();

        foreach (var block in diff.DiffBlocks)
        {
            for (var i = block.DeleteStartA; i < block.DeleteStartA + block.DeleteCountA; i++)
                changed.Add(i);

            // Include insertion positions as changed lines to detect conflicts when both mods insert at the same location
            if (block.InsertCountB > 0)
                changed.Add(block.DeleteStartA);
        }

        return changed;
    }
}

public class MergeResult
{
    public bool HasConflicts { get; init; }
    public string MergedText { get; init; } = string.Empty;
}
