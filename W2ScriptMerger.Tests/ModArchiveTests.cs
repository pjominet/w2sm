using W2ScriptMerger.Models;

namespace W2ScriptMerger.Tests;

public class ModArchiveTests
{
    [Theory]
    [InlineData("CEO - dzip-89-1-6g.rar", "CEO - dzip", "-89-1-6g")]
    [InlineData("base_scripts.zip-847-1-00.zip", "base_scripts.zip", "-847-1-00")]
    [InlineData("Talent Reset Mod-757-1-0.zip", "Talent Reset Mod", "-757-1-0")]
    [InlineData("Project Mersey - The Witcher 2 Fix Pack-981-1-3-1697658222.zip", "Project Mersey - The Witcher 2 Fix Pack", "-981-1-3-1697658222")]
    [InlineData("Yet Another Mod Compilation 1.2-942-1-2-1699036429.rar", "Yet Another Mod Compilation 1.2", "-942-1-2-1699036429")]
    [InlineData("Geralt's Improved Quality of Life-871-7-1552654769.zip", "Geralt's Improved Quality of Life", "-871-7-1552654769")]
    [InlineData("Simple Mod Without ID.zip", "Simple Mod Without ID", null)]
    public void DisplayName_RemovesNexusIdSuffix(string fileName, string expectedDisplayName, string? expectedNexusId)
    {
        var archive = new ModArchive { SourcePath = $@"C:\Mods\{fileName}" };
        
        Assert.Equal(expectedDisplayName, archive.DisplayName);
        Assert.Equal(expectedNexusId, archive.NexusId);
    }

    [Fact]
    public void ModName_ReturnsFileNameWithoutExtension()
    {
        var archive = new ModArchive { SourcePath = @"C:\Mods\Test Mod-123-1-0.zip" };
        
        Assert.Equal("Test Mod-123-1-0", archive.ModName);
    }

    [Fact]
    public void IsDeployed_DefaultsFalse()
    {
        var archive = new ModArchive { SourcePath = @"C:\Mods\Test.zip" };
        
        Assert.False(archive.IsDeployed);
    }
}
