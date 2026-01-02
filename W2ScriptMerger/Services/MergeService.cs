using System.Text;
using DiffPlex;
using DiffPlex.DiffBuilder;
using W2ScriptMerger.Models;

namespace W2ScriptMerger.Services;

public class MergeService
{
    private readonly Differ _differ = new();

    public static List<ScriptConflict> DetectConflicts(List<ModArchive> archives, GameFileService gameFileService)
    {
        var conflicts = new Dictionary<string, ScriptConflict>(StringComparer.OrdinalIgnoreCase);

        foreach (var archive in archives)
        {
            foreach (var file in archive.Files.Where(f => f.FileType == ModFileType.Script))
            {
                var key = NormalizePath(file.RelativePath);

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
                    SourceArchive = archive.FileName,
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
        var baseLines = baseText.Split('\n');
        var leftLines = leftText.Split('\n');
        var rightLines = rightText.Split('\n');

        var leftDiff = _differ.CreateLineDiffs(baseText, leftText, ignoreWhitespace: false);
        var rightDiff = _differ.CreateLineDiffs(baseText, rightText, ignoreWhitespace: false);

        var leftChanges = GetChangedLineNumbers(leftDiff);
        var rightChanges = GetChangedLineNumbers(rightDiff);

        // Check for overlapping changes
        var hasConflicts = leftChanges.Intersect(rightChanges).Any();

        if (hasConflicts)
            return new MergeResult
            {
                HasConflicts = true,
                MergedText = string.Empty
            };

        // Apply non-conflicting changes
        var result = new List<string>();
        var leftBuilder = InlineDiffBuilder.Diff(baseText, leftText);
        var rightBuilder = InlineDiffBuilder.Diff(baseText, rightText);

        // Simple merge: prefer left changes, then right changes, then base
        var leftLineIndex = 0;
        var rightLineIndex = 0;
        var baseLineIndex = 0;

        while (baseLineIndex < baseLines.Length || leftLineIndex < leftLines.Length || rightLineIndex < rightLines.Length)
        {
            var leftHasChange = leftChanges.Contains(baseLineIndex);
            var rightHasChange = rightChanges.Contains(baseLineIndex);

            if (leftHasChange && !rightHasChange)
            {
                // Use left version
                if (leftLineIndex < leftLines.Length)
                    result.Add(leftLines[leftLineIndex]);
            }
            else if (rightHasChange && !leftHasChange)
            {
                // Use right version
                if (rightLineIndex < rightLines.Length)
                    result.Add(rightLines[rightLineIndex]);
            }
            else
            {
                // No change or both same, use base/left
                if (baseLineIndex < baseLines.Length)
                    result.Add(baseLines[baseLineIndex]);
            }

            leftLineIndex++;
            rightLineIndex++;
            baseLineIndex++;
        }

        return new MergeResult
        {
            HasConflicts = false,
            MergedText = string.Join('\n', result)
        };
    }

    private static HashSet<int> GetChangedLineNumbers(DiffPlex.Model.DiffResult diff)
    {
        var changed = new HashSet<int>();

        foreach (var block in diff.DiffBlocks)
        {
            for (var i = block.DeleteStartA; i < block.DeleteStartA + block.DeleteCountA; i++)
            {
                changed.Add(i);
            }
        }

        return changed;
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/').ToLowerInvariant().TrimStart('/');
}

public class MergeResult
{
    public bool HasConflicts { get; init; }
    public string MergedText { get; init; } = string.Empty;
}
