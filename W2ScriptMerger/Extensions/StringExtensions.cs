namespace W2ScriptMerger.Extensions;

public static class StringExtensions
{
    extension(string? @string)
    {
        public bool HasValue(bool checkWhitespace = true) => checkWhitespace ? !string.IsNullOrWhiteSpace(@string) : !string.IsNullOrEmpty(@string);
        public string NormalizePath() => @string.HasValue() ? @string!.Replace('\\', '/').ToLowerInvariant().TrimStart('/') : string.Empty;
    }
}
