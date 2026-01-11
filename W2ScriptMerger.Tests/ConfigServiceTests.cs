using System.IO;
using System.Text.Json;
using W2ScriptMerger.Services;

namespace W2ScriptMerger.Tests;

public class ConfigServiceTests : IDisposable
{
    private const string StagingFolder = "mod_staging";
    private const string VanillaScriptsFolder = "vanilla_scripts";
    private const string ModScriptsFolder = "mod_scripts";

    private readonly string _sourcePath;
    private readonly string _destPath;
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    public ConfigServiceTests()
    {
        var basePath = Path.Combine(Path.GetTempPath(), "W2ScriptMerger_ConfigTests");
        _sourcePath = Path.Combine(basePath, "source");
        _destPath = Path.Combine(basePath, "dest");

        // Clean up any previous test runs
        if (Directory.Exists(basePath))
            Directory.Delete(basePath, true);

        Directory.CreateDirectory(_sourcePath);
        Directory.CreateDirectory(_destPath);
    }

    public void Dispose()
    {
        var basePath = Path.GetDirectoryName(_sourcePath)!;
        if (Directory.Exists(basePath))
            Directory.Delete(basePath, true);
    }

    [Fact]
    public void MigrateRuntimeData_CopiesFoldersToNewLocation()
    {
        // Arrange
        var configService = new ConfigService(_options) { RuntimeDataPath = _sourcePath };

        // Create test folders with files
        var stagingFolder = Path.Combine(_sourcePath, StagingFolder);
        var vanillaFolder = Path.Combine(_sourcePath, VanillaScriptsFolder);
        var modFolder = Path.Combine(_sourcePath, ModScriptsFolder);

        Directory.CreateDirectory(stagingFolder);
        Directory.CreateDirectory(vanillaFolder);
        Directory.CreateDirectory(modFolder);

        File.WriteAllText(Path.Combine(stagingFolder, "test_mod.txt"), "mod content");
        File.WriteAllText(Path.Combine(vanillaFolder, "vanilla.ws"), "vanilla script");
        File.WriteAllText(Path.Combine(modFolder, "mod.ws"), "mod script");

        // Act
        configService.MigrateRuntimeData(_destPath);

        // Assert - files exist in new location
        Assert.True(File.Exists(Path.Combine(_destPath, StagingFolder, "test_mod.txt")));
        Assert.True(File.Exists(Path.Combine(_destPath, VanillaScriptsFolder, "vanilla.ws")));
        Assert.True(File.Exists(Path.Combine(_destPath, ModScriptsFolder, "mod.ws")));

        // Assert - files removed from old location
        Assert.False(Directory.Exists(stagingFolder));
        Assert.False(Directory.Exists(vanillaFolder));
        Assert.False(Directory.Exists(modFolder));

        // Assert - RuntimeDataPath updated
        Assert.Equal(_destPath, configService.RuntimeDataPath);
    }

    [Fact]
    public void MigrateRuntimeData_PreservesNestedFolderStructure()
    {
        // Arrange
        var configService = new ConfigService(_options) { RuntimeDataPath = _sourcePath };

        var nestedPath = Path.Combine(_sourcePath, StagingFolder, "mod1", "subfolder");
        Directory.CreateDirectory(nestedPath);
        File.WriteAllText(Path.Combine(nestedPath, "nested_file.txt"), "nested content");

        // Act
        configService.MigrateRuntimeData(_destPath);

        // Assert
        var destNestedFile = Path.Combine(_destPath, StagingFolder, "mod1", "subfolder", "nested_file.txt");
        Assert.True(File.Exists(destNestedFile));
        Assert.Equal("nested content", File.ReadAllText(destNestedFile));
    }

    [Fact]
    public void MigrateRuntimeData_SkipsMissingFolders()
    {
        // Arrange
        var configService = new ConfigService(_options) { RuntimeDataPath = _sourcePath };

        // Only create staging folder, not others
        var stagingFolder = Path.Combine(_sourcePath, StagingFolder);
        Directory.CreateDirectory(stagingFolder);
        File.WriteAllText(Path.Combine(stagingFolder, "test.txt"), "content");

        // Act - should not throw
        configService.MigrateRuntimeData(_destPath);

        // Assert
        Assert.True(File.Exists(Path.Combine(_destPath, StagingFolder, "test.txt")));
        Assert.Equal(_destPath, configService.RuntimeDataPath);
    }

    [Fact]
    public void MigrateRuntimeData_SamePathDoesNothing()
    {
        // Arrange
        var configService = new ConfigService(_options) { RuntimeDataPath = _sourcePath };

        var stagingFolder = Path.Combine(_sourcePath, StagingFolder);
        Directory.CreateDirectory(stagingFolder);
        File.WriteAllText(Path.Combine(stagingFolder, "test.txt"), "content");

        // Act
        configService.MigrateRuntimeData(_sourcePath);

        // Assert - files still in original location
        Assert.True(File.Exists(Path.Combine(_sourcePath, StagingFolder, "test.txt")));
    }

    [Fact]
    public void MigrateRuntimeData_OverwritesExistingDestinationFolders()
    {
        // Arrange
        var configService = new ConfigService(_options) { RuntimeDataPath = _sourcePath };

        // Create source folder with file
        var sourceStaging = Path.Combine(_sourcePath, StagingFolder);
        Directory.CreateDirectory(sourceStaging);
        File.WriteAllText(Path.Combine(sourceStaging, "new_file.txt"), "new content");

        // Create destination folder with different file
        var destStaging = Path.Combine(_destPath, StagingFolder);
        Directory.CreateDirectory(destStaging);
        File.WriteAllText(Path.Combine(destStaging, "old_file.txt"), "old content");

        // Act
        configService.MigrateRuntimeData(_destPath);

        // Assert - new file exists, old file removed (folder was replaced)
        Assert.True(File.Exists(Path.Combine(_destPath, StagingFolder, "new_file.txt")));
        Assert.False(File.Exists(Path.Combine(_destPath, StagingFolder, "old_file.txt")));
    }
}
