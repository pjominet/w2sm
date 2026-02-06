using System.IO;
using System.Text;
using System.Text.Json;
using W2ScriptMerger.Models;
using W2ScriptMerger.Services;
using W2ScriptMerger.Tests.Infrastructure;

namespace W2ScriptMerger.Tests;

public class ExtractionServiceTests : IDisposable
{
    private readonly TestArtifactScope _scope = TestArtifactScope.Create(nameof(ExtractionServiceTests));
    private readonly ScriptExtractionService _extractionService;
    private readonly string _gameScriptsPath;
    private readonly string _mergedScriptsPath;
    private readonly string _modScriptsPath;

    public ExtractionServiceTests()
    {
        var runtimePath = _scope.CreateSubdirectory("runtime");
        var options = new JsonSerializerOptions { WriteIndented = true };
        var configService = new ConfigService(options)
        {
            RuntimeDataPath = runtimePath
        };
        _extractionService = new ScriptExtractionService(configService);
        _gameScriptsPath = configService.GameScriptsPath;
        _mergedScriptsPath = _extractionService.MergedScriptsPath;
        _modScriptsPath = configService.ModScriptsPath;
    }

    public void Dispose() => _scope.Dispose();

    [Fact]
    public void PackMergedDzip_ShouldIncludeNonConflictingModFiles()
    {
        // Arrange
        var dzipName = "base_scripts.dzip";

        // 1. Setup Vanilla
        var vanillaDir = Path.Combine(_gameScriptsPath, dzipName);
        Directory.CreateDirectory(vanillaDir);
        File.WriteAllText(Path.Combine(vanillaDir, "vanilla_only.ws"), "vanilla content");
        File.WriteAllText(Path.Combine(vanillaDir, "conflict.ws"), "vanilla conflict content");

        // 2. Setup Mod1 (contains a new file and a conflict)
        var mod1Name = "Mod1";
        var mod1Dir = Path.Combine(_modScriptsPath, mod1Name, dzipName);
        Directory.CreateDirectory(mod1Dir);
        File.WriteAllText(Path.Combine(mod1Dir, "mod1_new_file.ws"), "mod1 new content");
        File.WriteAllText(Path.Combine(mod1Dir, "conflict.ws"), "mod1 conflict content");

        // 3. Setup Merged
        var mergedDir = Path.Combine(_mergedScriptsPath, dzipName);
        Directory.CreateDirectory(mergedDir);
        File.WriteAllText(Path.Combine(mergedDir, "conflict.ws"), "merged conflict content");

        var conflict = new DzipConflict
        {
            DzipName = dzipName,
            BaseDzipPath = "dummy",
            ModSources =
            {
                new ModDzipSource
                {
                    ModName = mod1Name,
                    DzipPath = "dummy",
                    ExtractedPath = mod1Dir
                }
            }
        };

        // Act
        var resultPath = _extractionService.PackMergedDzip(conflict);

        // Assert
        Assert.NotNull(resultPath);
        Assert.True(File.Exists(resultPath));

        var unpackDir = _scope.CreateSubdirectory("final_unpack");
        DzipService.UnpackDzipTo(resultPath!, unpackDir);

        Assert.True(File.Exists(Path.Combine(unpackDir, "vanilla_only.ws")), "Vanilla file missing");
        Assert.True(File.Exists(Path.Combine(unpackDir, "mod1_new_file.ws")), "Mod added file missing");
        Assert.Equal("merged conflict content", File.ReadAllText(Path.Combine(unpackDir, "conflict.ws")));
    }
}
