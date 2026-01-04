using System.Text;
using DiffPlex;
using W2ScriptMerger.Extensions;
using W2ScriptMerger.Models;

namespace W2ScriptMerger.Services;

public class MergeService(GameFileService gameFileService)
{
    private readonly Differ _differ = new();

    public List<ScriptConflict> DetectConflicts(List<ModArchive> archives)
    {
        var conflicts = new Dictionary<string, ScriptConflict>(StringComparer.OrdinalIgnoreCase);

        foreach (var archive in archives)
        {
            foreach (var file in archive.Files)
            {
                var key = file.RelativePath.NormalizePath();

                if (!conflicts.TryGetValue(key, out var conflict))
                {
                    conflict = new ScriptConflict
                    {
                        RelativePath = file.RelativePath,
                        VanillaContent = gameFileService.GetVanillaScriptContent(file.RelativePath)
                    };
                    conflicts[key] = conflict;
                }

                conflict.ModVersions.Add(new ModFileVersion
                {
                    SourceArchive = archive.ModName,
                    Content = file.Content
                });

                // Mark as requiring merge if multiple mods touch this file
                if (conflict.ModVersions.Count > 1 || conflict.VanillaContent is not null)
                {
                }
            }
        }

        // Only return conflicts where multiple mods modify the same file
        // or where a mod modifies a vanilla file
        return conflicts.Values
            .Where(c => c.ModVersions.Count > 1 || c.VanillaContent is not null)
            .ToList();
    }

    public bool TryAutoMerge(ScriptConflict conflict)
    {
        switch (conflict.ModVersions.Count)
        {
            case 0:
                return false;
            // If only one mod and no vanilla, just use that mod's version
            case 1 when conflict.VanillaContent is null:
                conflict.MergedContent = conflict.ModVersions[0].Content;
                conflict.Status = ConflictStatus.AutoMerged;
                return true;
        }

        var baseContent = conflict.VanillaContent ?? [];
        var baseText = Encoding.UTF8.GetString(baseContent);

        // Try three-way merge for each mod version
        var currentMerged = baseText;

        foreach (var modVersion in conflict.ModVersions)
        {
            var modText = Encoding.UTF8.GetString(modVersion.Content);

            var mergeResult = ThreeWayMerge(baseText, currentMerged, modText);
            if (mergeResult.HasConflicts)
            {
                conflict.Status = ConflictStatus.Pending;
                return false;
            }

            currentMerged = mergeResult.MergedText;
        }

        conflict.MergedContent = Encoding.UTF8.GetBytes(currentMerged);
        conflict.Status = ConflictStatus.AutoMerged;
        return true;
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

        // Start with base lines
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
