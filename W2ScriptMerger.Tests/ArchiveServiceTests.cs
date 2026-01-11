using System.IO;
using System.Text.Json;
using W2ScriptMerger.Models;
using W2ScriptMerger.Services;

namespace W2ScriptMerger.Tests;

public class ArchiveServiceTests
{
    private readonly string _testDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData");
    private readonly string _tempPath = Path.Combine(Path.GetTempPath(), "W2ScriptMerger_ArchiveTests");
    private readonly ArchiveService _archiveService;

    public ArchiveServiceTests()
    {
        if (Directory.Exists(_tempPath))
            Directory.Delete(_tempPath, true);
        Directory.CreateDirectory(_tempPath);

        var options = new JsonSerializerOptions { WriteIndented = true };
        var configService = new ConfigService(options)
        {
            RuntimeDataPath = _tempPath
        };
        _archiveService = new ArchiveService(configService);
    }

    [Fact]
    public async Task LoadModArchive_WithDzipFile_ExtractsAndIdentifiesFiles()
    {
        var archivePath = Path.Combine(_testDataPath, "CEO - dzip-89-1-6g.rar");
        if (!File.Exists(archivePath))
            return;

        var result = await _archiveService.LoadModArchive(archivePath);

        Assert.True(result.IsLoaded);
        Assert.NotEmpty(result.Files);
        Assert.Contains(result.Files, f => f.Type == ModFileType.Dzip);
    }

    [Fact]
    public async Task LoadModArchive_WithBaseScriptsDzip_IdentifiesAsConflictCandidate()
    {
        var archivePath = Path.Combine(_testDataPath, "base_scripts.zip-847-1-00.zip");
        if (!File.Exists(archivePath))
            return;

        var result = await _archiveService.LoadModArchive(archivePath);

        Assert.True(result.IsLoaded);
        Assert.Contains(result.Files, f =>
            f.Name.Equals("base_scripts.dzip", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task LoadModArchive_SetsCorrectDisplayName()
    {
        var archivePath = Path.Combine(_testDataPath, "CEO - dzip-89-1-6g.rar");
        if (!File.Exists(archivePath))
            return;

        var result = await _archiveService.LoadModArchive(archivePath);

        Assert.Equal("CEO - dzip", result.DisplayName);
        Assert.Equal("-89-1-6g", result.NexusId);
    }

    [Fact]
    public async Task LoadModArchive_IgnoresTxtFiles()
    {
        var archivePath = Path.Combine(_testDataPath, "CEO - dzip-89-1-6g.rar");
        if (!File.Exists(archivePath))
            return;

        var result = await _archiveService.LoadModArchive(archivePath);

        Assert.DoesNotContain(result.Files, f =>
            f.RelativePath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase));
    }
}
