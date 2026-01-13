using W2ScriptMerger.Models;

namespace W2ScriptMerger.Tests;

public class ModArchiveTests
{
    [Theory]
    [InlineData("ModWithSimpleNexusId-757-1-0.zip", "ModWithSimpleNexusId", "-757-1-0")]
    [InlineData("ModWithAlphanumericalNexusId-89-1-6g.rar", "ModWithAlphanumericalNexusId", "-89-1-6g")]
    [InlineData("ModWithLongNexusId-981-1-3-1697658222.zip", "ModWithLongNexusId", "-981-1-3-1697658222")]
    [InlineData("ModWithTimestampNexusId-942-1-2-1699036429.rar", "ModWithTimestampNexusId", "-942-1-2-1699036429")]
    [InlineData("ModWithoutNexusId.zip", "ModWithoutNexusId", null)]
    public void DisplayName_RemovesNexusIdSuffix(string fileName, string expectedDisplayName, string? expectedNexusId)
    {
        var archive = new ModArchive { SourcePath = $@"C:\Mods\{fileName}" };

        Assert.Equal(expectedDisplayName, archive.DisplayName);
        Assert.Equal(expectedNexusId, archive.NexusId);
    }

    [Fact]
    public void ModName_ReturnsFileNameWithoutExtension()
    {
        var archive = new ModArchive { SourcePath = @"C:\Mods\GenericTestMod-123-1-0.zip" };

        Assert.Equal("GenericTestMod-123-1-0", archive.ModName);
    }

    [Fact]
    public void IsDeployed_DefaultsFalse()
    {
        var archive = new ModArchive { SourcePath = @"C:\Mods\GenericTestMod.zip" };

        Assert.False(archive.IsDeployed);
    }
}
