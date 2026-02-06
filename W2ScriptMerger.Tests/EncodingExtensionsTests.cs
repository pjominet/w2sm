using System.Text;
using W2ScriptMerger.Extensions;
using EncodingExtensions = W2ScriptMerger.Extensions.EncodingExtensions;

namespace W2ScriptMerger.Tests;

public class EncodingExtensionsTests
{
    static EncodingExtensionsTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    [Fact]
    public void DetectEncoding_WithUtf8Bom_ShouldReturnUtf8()
    {
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF, 0x61, 0x62, 0x63 }; // BOM + abc
        var encoding = EncodingExtensions.DetectEncoding(bytes);
        Assert.Equal(Encoding.UTF8, encoding);
    }

    [Fact]
    public void DetectEncoding_WithUtf16LeBom_ShouldReturnUnicode()
    {
        var bytes = new byte[] { 0xFF, 0xFE, 0x61, 0x00, 0x62, 0x00 }; // BOM + ab
        var encoding = EncodingExtensions.DetectEncoding(bytes);
        Assert.Equal(Encoding.Unicode, encoding);
    }

    [Fact]
    public void DetectEncoding_WithUtf16BeBom_ShouldReturnBigEndianUnicode()
    {
        var bytes = new byte[] { 0xFE, 0xFF, 0x00, 0x61, 0x00, 0x62 }; // BOM + ab
        var encoding = EncodingExtensions.DetectEncoding(bytes);
        Assert.Equal(Encoding.BigEndianUnicode, encoding);
    }

    [Fact]
    public void DetectEncoding_WithValidUtf8NoBom_ShouldReturnUtf8()
    {
        var bytes = "ąęśćżźńół"u8.ToArray();
        var encoding = EncodingExtensions.DetectEncoding(bytes);
        Assert.Equal(Encoding.UTF8, encoding);
    }

    [Fact]
    public void DetectEncoding_WithCp1250_ShouldReturnCp1250()
    {
        var cp1250 = Encoding.ANSI1250;
        var bytes = cp1250.GetBytes("ąęśćżźńół"); // Contains bytes that are invalid in UTF-8
        var encoding = EncodingExtensions.DetectEncoding(bytes);
        Assert.Equal(cp1250, encoding);
    }

    [Fact]
    public void DetectEncoding_WithUtf16LeNoBom_ShouldReturnUnicode()
    {
        // Many nulls and non-ASCII chars should help UtfUnknown
        var bytes = Encoding.Unicode.GetBytes("This is some text with non-ASCII characters like ąęśćżźńół to help heuristic detection.");
        var encoding = EncodingExtensions.DetectEncoding(bytes);
        Assert.Equal(Encoding.Unicode, encoding);
    }

    [Fact]
    public void DetectEncoding_WithUtf16BeNoBom_ShouldReturnBigEndianUnicode()
    {
        var bytes = Encoding.BigEndianUnicode.GetBytes("This is some text with non-ASCII characters like ąęśćżźńół to help heuristic detection.");
        var encoding = EncodingExtensions.DetectEncoding(bytes);
        Assert.Equal(Encoding.BigEndianUnicode, encoding);
    }

    [Fact]
    public void ReadFileWithEncodingFromBytes_ShouldSkipBom()
    {
        var text = "Hello World";
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(text)).ToArray();

        var result = EncodingExtensions.ReadFileWithEncodingFromBytes(bytes);

        Assert.Equal(text, result);
    }
}
