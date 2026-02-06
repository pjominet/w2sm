using System.IO;
using System.Text;
using UtfUnknown;

namespace W2ScriptMerger.Extensions;

// ReSharper disable InconsistentNaming
internal static class EncodingExtensions
{
    extension(Encoding)
    {
        public static Encoding ANSI1250 => Encoding.GetEncoding(1250);
    }

    public static Encoding DetectEncoding(byte[] bytes)
    {
        if (bytes.Length == 0)
            return Encoding.UTF8;

        // 1. Check for BOMs manually for speed and reliability
        switch (bytes)
        {
            case [0xEF, 0xBB, 0xBF, ..]:
                return Encoding.UTF8;
            case [0xFF, 0xFE, ..]:
                return Encoding.Unicode;
            case [0xFE, 0xFF, ..]:
                return Encoding.BigEndianUnicode;
        }

        // 2. Strong heuristic for UTF-16 without BOM: look for a high ratio of null bytes
        var sample = Math.Min(bytes.Length, 2048);
        var nulls = 0;
        for (var i = 0; i < sample; i++)
        {
            if (bytes[i] == 0) nulls++;
        }
        if (nulls > sample * 0.30)
        {
            // Guess endianness by observing which byte in each pair tends to be zero
            var leScore = 0; var beScore = 0;
            for (var i = 0; i + 1 < sample; i += 2)
            {
                if (bytes[i] != 0 && bytes[i + 1] == 0) leScore++;
                if (bytes[i] == 0 && bytes[i + 1] != 0) beScore++;
            }
            return leScore >= beScore ? Encoding.Unicode : Encoding.BigEndianUnicode;
        }

        // 3. Use UtfUnknown for heuristic detection across many encodings
        var result = CharsetDetector.DetectFromBytes(bytes);
        if (result?.Detected?.Encoding is not null && result.Detected.Confidence > 0.5)
            return result.Detected.Encoding;

        // 4. Fallback to CP1250
        return Encoding.ANSI1250;
    }

    public static string ReadFileWithEncodingFromBytes(byte[] bytes)
    {
        var encoding = DetectEncoding(bytes);

        // Skip BOM if present
        var offset = 0;
        if (encoding.Equals(Encoding.UTF8) && bytes is [0xEF, 0xBB, 0xBF, ..])
            offset = 3;
        else if (encoding.Equals(Encoding.Unicode) && bytes is [0xFF, 0xFE, ..] || encoding.Equals(Encoding.BigEndianUnicode) && bytes is [0xFE, 0xFF, ..])
            offset = 2;

        return encoding.GetString(bytes, offset, bytes.Length - offset);
    }

    internal static string ReadFileWithEncoding(string path)
    {
        var bytes = File.ReadAllBytes(path);
        return ReadFileWithEncodingFromBytes(bytes);
    }
}
