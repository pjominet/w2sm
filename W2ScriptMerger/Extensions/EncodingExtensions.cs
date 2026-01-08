using System.Text;

namespace W2ScriptMerger.Extensions;

// ReSharper disable InconsistentNaming
internal static class EncodingExtensions
{
    extension(Encoding)
    {
        internal static Encoding ANSI1250 => Encoding.GetEncoding(1250);
        internal static Encoding ANSI1252 => Encoding.GetEncoding(1252);
    }
}
