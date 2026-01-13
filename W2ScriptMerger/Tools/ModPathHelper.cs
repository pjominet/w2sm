using System;
using System.IO;
using W2ScriptMerger.Models;
using W2ScriptMerger.Extensions;

namespace W2ScriptMerger.Tools;

internal static class ModPathHelper
{
    private const string CookedPcSegment = "CookedPC";
    private const string CookedPcPrefix = CookedPcSegment + "/";
    private const string UserContentSegment = "UserContent";
    private const string UserContentPrefix = UserContentSegment + "/";

    /// <summary>
    /// Normalizes a path from an archive entry and determines which Witcher 2 install root it belongs to.
    /// This is used while staging extracted files: it ensures the relative path includes either the CookedPC or UserContent root
    /// so the mod can later be deployed back to the appropriate location.
    /// </summary>
    /// <param name="rawPath">Path exactly as it was stored inside the archive.</param>
    /// <returns>
    /// The inferred <see cref="InstallLocation"/> along with a relative path that starts at the detected root.
    /// Unknown paths are staged under CookedPC by default so they can still be surfaced to the user.
    /// </returns>
    internal static (InstallLocation Location, string RelativePathWithRoot) ResolveStagingPath(string? rawPath)
    {
        var normalizedPath = rawPath.NormalizePath();
        if (normalizedPath.Length == 0)
            return (InstallLocation.Unknown, normalizedPath);

        if (normalizedPath.StartsWith(CookedPcPrefix, StringComparison.OrdinalIgnoreCase))
            return (InstallLocation.CookedPC, normalizedPath);

        if (normalizedPath.StartsWith(UserContentPrefix, StringComparison.OrdinalIgnoreCase))
            return (InstallLocation.UserContent, normalizedPath);

        return (InstallLocation.Unknown, string.Concat(CookedPcPrefix, normalizedPath));
    }

    /// <summary>
    /// Converts a staged file path into the relative path that should be applied during deployment.
    /// Any CookedPC/UserContent prefixes are trimmed and the result is normalized to OS-specific separators for <see cref="Path.Combine"/>.
    /// </summary>
    /// <param name="filePath">Path recorded in the mod archive metadata.</param>
    internal static string GetDeployRelativePath(string filePath)
    {
        var normalizedPath = filePath.NormalizePath();

        if (normalizedPath.StartsWith(CookedPcPrefix, StringComparison.OrdinalIgnoreCase))
            normalizedPath = normalizedPath[CookedPcPrefix.Length..];
        else if (normalizedPath.StartsWith(UserContentPrefix, StringComparison.OrdinalIgnoreCase))
            normalizedPath = normalizedPath[UserContentPrefix.Length..];

        return normalizedPath.ToSystemPath();
    }

    /// <summary>
    /// Determines the relative path for a mod file by trimming the archive path to start from the first valid root directory
    /// ("CookedPc" or "UserContent", case-insensitive) encountered when traversing upwards from the file.
    /// This prevents nesting issues where archives contain multiple root directories, ensuring only the deepest valid root is used
    /// (as per game folder structure expectations). If no valid root is found, returns the original normalized path.
    /// </summary>
    /// <param name="archivePath">The raw archive entry path to the file.</param>
    /// <returns>The relative path starting from the appropriate root, or the original normalized path if no root found.</returns>
    internal static string DetermineRelativeModFilePath(string? archivePath)
    {
        if (!archivePath.HasValue())
            return string.Empty;

        var normalizedPath = archivePath.NormalizePath();
        if (normalizedPath.Length == 0)
            return string.Empty;

        // Work with spans for better performance than strings/arrays when scanning for the root segment
        var span = normalizedPath.AsSpan();
        var segmentEndExclusive = span.Length;

        // Walk backwards, checking each segment between '/' characters until we find the deepest CookedPC/UserContent folder
        for (var i = span.Length - 1; i >= -1; i--)
        {
            if (i >= 0 && span[i] != '/')
                continue;

            var segmentStart = i + 1;
            var segment = span.Slice(segmentStart, segmentEndExclusive - segmentStart);
            if (segment.Length == 0)
            {
                segmentEndExclusive = i;
                continue;
            }

            if (IsRootSegment(segment))
                // Return the suffix starting at the detected root
                return normalizedPath[segmentStart..];

            segmentEndExclusive = i;
        }

        return archivePath!;
    }

    private static bool IsRootSegment(ReadOnlySpan<char> segment) =>
        segment.Equals(CookedPcSegment, StringComparison.OrdinalIgnoreCase) ||
        segment.Equals(UserContentSegment, StringComparison.OrdinalIgnoreCase);
}
