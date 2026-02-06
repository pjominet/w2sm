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
}
