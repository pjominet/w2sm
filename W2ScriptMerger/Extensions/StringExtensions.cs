using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace W2ScriptMerger.Extensions;

internal static class StringExtensions
{
    extension([NotNullWhen(true)] string? @string)
    {
        internal bool HasValue(bool checkWhitespace = true) => checkWhitespace
            ? !string.IsNullOrWhiteSpace(@string)
            : !string.IsNullOrEmpty(@string);

        internal string NormalizePath(bool trimLeadingSlash = true) => @string.HasValue()
            ? !trimLeadingSlash
                ? @string.Replace('\\', '/')
                : @string.Replace('\\', '/').TrimStart('/')
            : string.Empty;

        internal string ToSystemPath() => @string.HasValue()
            ? @string.NormalizePath().Replace('/', Path.DirectorySeparatorChar)
            : string.Empty;
    }
}
