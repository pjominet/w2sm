using System.IO;
using System.Text;

namespace W2ScriptMerger.Extensions;

// ReSharper disable InconsistentNaming
internal static class EncodingExtensions
{
    extension(Encoding)
    {
        internal static Encoding ANSI1250 => Encoding.GetEncoding(1250);
    }

    internal static string ReadFileWithEncoding(string path)
    {
        var bytes = File.ReadAllBytes(path);

        switch (bytes)
        {
            // Check for UTF-8 BOM
            case [0xEF, 0xBB, 0xBF, ..]:
                return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            // Check for UTF-16 LE BOM
            case [0xFF, 0xFE, ..]:
                return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            // Check for UTF-16 BE BOM
            case [0xFE, 0xFF, ..]:
                return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
            default:
                try
                {
                    // Try UTF-8 first (valid UTF-8 without BOM)
                    var utf8 = new UTF8Encoding(false, true);
                    return utf8.GetString(bytes);
                }
                catch
                {
                    // Fall back to Windows-1252 (Western European ANSI - most common for game scripts)
                    return Encoding.GetEncoding(1252).GetString(bytes);
                }
        }
    }
}
