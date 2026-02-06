using System.IO;
using System.Text;
using W2ScriptMerger.Services;
using W2ScriptMerger.Tests.Infrastructure;

namespace W2ScriptMerger.Tests;

public class DzipServiceTests : IDisposable
{
    private readonly TestArtifactScope _scope = TestArtifactScope.Create(nameof(DzipServiceTests));

    public void Dispose() => _scope.Dispose();

    [Fact]
    public void PackAndUnpack_ShouldBeIdempotent()
    {
        // Arrange
        var sourceDir = _scope.CreateSubdirectory("source_idemp");
        var testFile = Path.Combine(sourceDir, "test.ws");

        // Use a mix of content that triggers multiple blocks (64KB)
        var largeContent = new StringBuilder();
        for (var i = 0; i < 10000; i++)
            largeContent.AppendLine($"class Test_{i} {{ var x_{i} : int; }}");

        var originalContent = largeContent.ToString();
        File.WriteAllText(testFile, originalContent);

        var dzipPath = Path.Combine(_scope.CreateSubdirectory("temp"), "test.dzip");
        var unpackDir = _scope.CreateSubdirectory("unpack");

        // Act
        DzipService.PackDzip(dzipPath, sourceDir);
        DzipService.UnpackDzipTo(dzipPath, unpackDir);

        // Assert
        var unpackedFile = Path.Combine(unpackDir, "test.ws");
        Assert.True(File.Exists(unpackedFile));
        Assert.Equal(originalContent, File.ReadAllText(unpackedFile));
    }
    [Fact]
    public void PackDzip_ShouldMatchExpectedStructure()
    {
        // Arrange
        var sourceDir = _scope.CreateSubdirectory("source_struct");
        var testFile = Path.Combine(sourceDir, "test.txt");
        var content = "Hello DZIP";
        File.WriteAllText(testFile, content);

        var dzipPath = Path.Combine(_scope.CreateSubdirectory("temp_struct"), "test.dzip");

        // Act
        DzipService.PackDzip(dzipPath, sourceDir);

        // Assert
        using var stream = File.OpenRead(dzipPath);
        using var reader = new BinaryReader(stream);

        // Header (32 bytes)
        Assert.Equal(0x50495A44u, reader.ReadUInt32()); // "DZIP"
        Assert.Equal(2u, reader.ReadUInt32()); // version
        Assert.Equal(1u, reader.ReadUInt32()); // entryCount
        Assert.Equal(0x64626267u, reader.ReadUInt32()); // "gbbd"
        var entryTableOffset = reader.ReadInt64();
        var hash = reader.ReadUInt64();

        // Check if data starts at 32
        Assert.Equal(32, stream.Position);

        // For one file:
        // Offset table: 2 * 4 = 8 bytes (start of block 0, and terminal offset)
        // Block 0: [1 byte: isCompressed] [compressed data]

        var block0Start = reader.ReadUInt32();
        Assert.Equal(8u, block0Start); // relative to entry offset (which is 32)

        // Skip terminal offset
        var terminalOffsetFromTable = reader.ReadUInt32();

        var isCompressed = reader.ReadByte();
        // content "Hello DZIP" is too short to compress effectively with LZF usually, but let's see.
        // Actually, let's just check that terminalOffset matches current position - offset
        Assert.Equal((uint)(stream.Position - 1 - 32), block0Start);

        // Seek to entryTableOffset to get compressedSize
        stream.Seek(entryTableOffset, SeekOrigin.Begin);
        var nameLength = reader.ReadUInt16();
        var nameBytes = reader.ReadBytes(nameLength);
        var timeStamp = reader.ReadInt64();
        var uncompressedSize = reader.ReadInt64();
        var offset = reader.ReadInt64();
        var compressedSize = reader.ReadInt64();

        Assert.Equal(32, offset);
        Assert.Equal(content.Length, uncompressedSize);
        Assert.Equal((uint)compressedSize, terminalOffsetFromTable);
    }
}
