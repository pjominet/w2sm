using System.IO;
using W2ScriptMerger.Services;

namespace W2ScriptMerger.Tests;

public class DzipServiceTests
{
    private readonly string _testDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData");
    private readonly string _tempPath = Path.Combine(Path.GetTempPath(), "W2ScriptMerger_Tests");

    public DzipServiceTests()
    {
        if (Directory.Exists(_tempPath))
            Directory.Delete(_tempPath, true);
        Directory.CreateDirectory(_tempPath);
    }

    [Fact]
    public void ListEntries_ReturnsEntriesFromDzip()
    {
        var dzipPath = Path.Combine(_testDataPath, "test_scripts.dzip");
        if (!File.Exists(dzipPath))
            return;

        var entries = DzipService.ListEntries(dzipPath);

        Assert.NotEmpty(entries);
        Assert.All(entries, e => Assert.False(string.IsNullOrEmpty(e.Name)));
    }

    [Fact]
    public void UnpackDzipTo_ExtractsFilesToDirectory()
    {
        var dzipPath = Path.Combine(_testDataPath, "test_scripts.dzip");
        if (!File.Exists(dzipPath))
            return;

        var outputPath = Path.Combine(_tempPath, "extracted");

        DzipService.UnpackDzipTo(dzipPath, outputPath);

        Assert.True(Directory.Exists(outputPath));
        var files = Directory.GetFiles(outputPath, "*", SearchOption.AllDirectories);
        Assert.NotEmpty(files);
    }

    [Fact]
    public void PackDzip_CreatesValidDzipArchive()
    {
        var sourceDir = Path.Combine(_tempPath, "pack_source");
        Directory.CreateDirectory(sourceDir);

        var testFile = Path.Combine(sourceDir, "test.ws");
        File.WriteAllText(testFile, """
                                    // Test WitcherScript file
                                    class TestClass {}
                                    """);

        var outputDzip = Path.Combine(_tempPath, "output.dzip");

        DzipService.PackDzip(outputDzip, sourceDir);

        Assert.True(File.Exists(outputDzip));

        var entries = DzipService.ListEntries(outputDzip);
        Assert.Single(entries);
        Assert.Equal("test.ws", entries[0].Name);
    }

    [Fact]
    public void PackAndUnpack_RoundTrip_PreservesContent()
    {
        var sourceDir = Path.Combine(_tempPath, "roundtrip_source");
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

        var dzipPath = Path.Combine(_tempPath, "roundtrip.dzip");
        DzipService.PackDzip(dzipPath, sourceDir);

        var extractDir = Path.Combine(_tempPath, "roundtrip_extracted");
        DzipService.UnpackDzipTo(dzipPath, extractDir);

        var extractedFile = Path.Combine(extractDir, "scripts", "test.ws");
        Assert.True(File.Exists(extractedFile));

        var extractedContent = File.ReadAllText(extractedFile);
        Assert.Equal(originalContent, extractedContent);
    }
}
