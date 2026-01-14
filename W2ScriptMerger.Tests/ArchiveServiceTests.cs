using System.IO;
using System.Text.Json;
using SharpSevenZip;
using W2ScriptMerger.Models;
using W2ScriptMerger.Services;
using W2ScriptMerger.Tests.Infrastructure;

namespace W2ScriptMerger.Tests;

public class ArchiveServiceTests : IDisposable
{
    private readonly string _testDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData");
    private readonly TestArtifactScope _scope;
    private readonly ArchiveService _archiveService;

    public ArchiveServiceTests()
    {
        _scope = TestArtifactScope.Create(nameof(ArchiveServiceTests));
        var runtimePath = _scope.CreateSubdirectory("runtime");

        var options = new JsonSerializerOptions { WriteIndented = true };
        var configService = new ConfigService(options)
        {
            RuntimeDataPath = runtimePath
        };
        SharpSevenZipBase.SetLibraryPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Dependencies", "7z.dll"));
        _archiveService = new ArchiveService(configService);
    }

    public void Dispose() => _scope.Dispose();

    [Fact]
    public async Task LoadModArchive_WithDzipFile_ExtractsAndIdentifiesFiles()
    {
        var archivePath = Path.Combine(_testDataPath, "test_mod-89-1-6g.rar");
        if (!File.Exists(archivePath))
            return;

        var result = await _archiveService.LoadModArchive(archivePath);

        Assert.True(result.IsLoaded, result.Error);
        Assert.NotEmpty(result.Files);
        Assert.Contains(result.Files, f => f.Type is FileType.Dzip);
    }

    [Fact]
    public async Task LoadModArchive_WithBaseScriptsDzip_IdentifiesAsConflictCandidate()
    {
        var archivePath = Path.Combine(_testDataPath, "base_scripts-847-1-00.zip");
        if (!File.Exists(archivePath))
            return;

        var result = await _archiveService.LoadModArchive(archivePath);

        Assert.True(result.IsLoaded, result.Error);
        Assert.Contains(result.Files, f =>
            f.Name.Equals("base_scripts.dzip", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task LoadModArchive_SetsCorrectDisplayName()
    {
        var archivePath = Path.Combine(_testDataPath, "test_mod-89-1-6g.rar");
        if (!File.Exists(archivePath))
            return;

        var result = await _archiveService.LoadModArchive(archivePath);

        Assert.Equal("test_mod", result.DisplayName);
        Assert.Equal("-89-1-6g", result.NexusId);
    }

    [Fact]
    public async Task LoadModArchive_IgnoresTxtFiles()
    {
        var archivePath = Path.Combine(_testDataPath, "test_mod-89-1-6g.rar");
        if (!File.Exists(archivePath))
            return;

        var result = await _archiveService.LoadModArchive(archivePath);

        Assert.DoesNotContain(result.Files, f =>
            f.RelativePath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase));
    }
}
