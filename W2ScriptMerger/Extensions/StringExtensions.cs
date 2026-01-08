namespace W2ScriptMerger.Extensions;

internal static class StringExtensions
{
    extension(string? @string)
    {
        internal bool HasValue(bool checkWhitespace = true) => checkWhitespace ? !string.IsNullOrWhiteSpace(@string) : !string.IsNullOrEmpty(@string);
        internal string NormalizePath() => @string.HasValue() ? @string!.Replace('\\', '/').ToLowerInvariant().TrimStart('/') : string.Empty;
    }
}
