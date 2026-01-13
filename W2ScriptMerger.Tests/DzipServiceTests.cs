using System.IO;
using W2ScriptMerger.Services;
using W2ScriptMerger.Tests.Infrastructure;

namespace W2ScriptMerger.Tests;

public class DzipServiceTests : IDisposable
{
    private readonly TestArtifactScope _scope;
    private readonly string _workRoot;
    private readonly string _sampleDzip;

    public DzipServiceTests()
    {
        _scope = TestArtifactScope.Create(nameof(DzipServiceTests));
        _workRoot = _scope.CreateSubdirectory("work");
        _sampleDzip = PrepareSampleDzip();
    }

    public void Dispose() => _scope.Dispose();

    private string PrepareSampleDzip()
    {
        var sourceDir = _scope.CreateSubdirectory("sample_source");
        var scriptPath = Path.Combine(sourceDir, "scripts", "mock.ws");
        Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);
        File.WriteAllText(scriptPath, "// mock script");

        var dzipPath = Path.Combine(_workRoot, "mock_scripts.dzip");
        DzipService.PackDzip(dzipPath, sourceDir);
        return dzipPath;
    }

    [Fact]
    public void ListEntries_ReturnsEntriesFromDzip()
    {
        var entries = DzipService.ListEntries(_sampleDzip);

        Assert.NotEmpty(entries);
        Assert.All(entries, e => Assert.False(string.IsNullOrEmpty(e.Name)));
    }

    [Fact]
    public void UnpackDzipTo_ExtractsFilesToDirectory()
    {
        var outputPath = _scope.CreateSubdirectory("extracted");

        DzipService.UnpackDzipTo(_sampleDzip, outputPath);

        Assert.True(Directory.Exists(outputPath));
        var files = Directory.GetFiles(outputPath, "*", SearchOption.AllDirectories);
        Assert.NotEmpty(files);
    }

    [Fact]
    public void PackDzip_CreatesValidDzipArchive()
    {
        var sourceDir = _scope.CreateSubdirectory("pack_source");
        Directory.CreateDirectory(sourceDir);

        var testFile = Path.Combine(sourceDir, "test.ws");
        File.WriteAllText(testFile, """
                                    // Test WitcherScript file
                                    class TestClass {}
                                    """);

        var outputDzip = Path.Combine(_workRoot, "output.dzip");

        DzipService.PackDzip(outputDzip, sourceDir);

        Assert.True(File.Exists(outputDzip));

        var entries = DzipService.ListEntries(outputDzip);
        Assert.Single(entries);
        Assert.Equal("test.ws", entries[0].Name);
    }

    [Fact]
    public void PackAndUnpack_RoundTrip_PreservesContent()
    {
        var sourceDir = _scope.CreateSubdirectory("roundtrip_source");
        Directory.CreateDirectory(sourceDir);

        const string originalContent = """
                                       // Test script
                                       class MyClass {
                                           function DoSomething() {}
                                       }
                                       """;
        var testFile = Path.Combine(sourceDir, "scripts", "test.ws");
        Directory.CreateDirectory(Path.GetDirectoryName(testFile)!);
        File.WriteAllText(testFile, originalContent);

        var dzipPath = Path.Combine(_workRoot, "roundtrip.dzip");
        DzipService.PackDzip(dzipPath, sourceDir);

        var extractDir = _scope.CreateSubdirectory("roundtrip_extracted");
        DzipService.UnpackDzipTo(dzipPath, extractDir);

        var extractedFile = Path.Combine(extractDir, "scripts", "test.ws");
        Assert.True(File.Exists(extractedFile));

        var extractedContent = File.ReadAllText(extractedFile);
        Assert.Equal(originalContent, extractedContent);
    }
}
