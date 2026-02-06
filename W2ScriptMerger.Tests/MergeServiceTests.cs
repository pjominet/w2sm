using System.Text;
using System.IO;
using System.Text.Json;
using W2ScriptMerger.Extensions;
using W2ScriptMerger.Services;
using W2ScriptMerger.Models;
using W2ScriptMerger.Tests.Infrastructure;

namespace W2ScriptMerger.Tests;

public class MergeServiceTests : IDisposable
{
    private readonly TestArtifactScope _scope;
    private readonly ScriptMergeService _mergeService;

    public MergeServiceTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        _scope = TestArtifactScope.Create(nameof(MergeServiceTests));
        var configService = new ConfigService(new JsonSerializerOptions());
        var extractionService = new ScriptExtractionService(configService);
        _mergeService = new ScriptMergeService(extractionService);
    }

    public void Dispose() => _scope.Dispose();

    [Fact]
    public void ThreeWayMerge_WhenTwoModsAddSameProperty_ShouldNotDuplicate()
    {
        // Arrange
        var baseText = "class MyClass\r\n{\r\n    void MyMethod() {}\r\n}";
        var mod1Text = "class MyClass\r\n{\r\n    var isTeleporting : bool;\r\n    void MyMethod() {}\r\n}";
        var mod2Text = "class MyClass\r\n{\r\n    var isTeleporting : bool;\r\n    void MyMethod() { Log(\"teleporting\"); }\r\n}";

        // Act
        var result = _mergeService.AttemptAutoMerge(new ScriptFileConflict
        {
            ScriptRelativePath = "test.ws",
            VanillaScriptPath = CreateFile("vanilla.ws", baseText),
            ModVersions =
            {
                new ModScriptVersion { ModName = "Mod1", ScriptPath = CreateFile("mod1.ws", mod1Text) },
                new ModScriptVersion { ModName = "Mod2", ScriptPath = CreateFile("mod2.ws", mod2Text) }
            }
        });

        // Assert
        Assert.True(result.Success);
        // We use Windows-1250 as the default output encoding now
        var mergedText = Encoding.ANSI1250.GetString(result.MergedContent!);

        Assert.Equal(1, CountOccurrences(mergedText, "var isTeleporting : bool;"));
        Assert.Contains("Log(\"teleporting\");", mergedText);
    }

    [Fact]
    public void ThreeWayMerge_ShouldUseCP1250ForOutput()
    {
        // Arrange
        var baseText = "class MyClass { void Original() {} }";
        var mod1Text = "class MyClass { void Original() {} /* ą */ }";
        var mod2Text = "class MyClass { void Original() {} /* ą */ void Mod2() {} }";

        // Act
        var result = _mergeService.AttemptAutoMerge(new ScriptFileConflict
        {
            ScriptRelativePath = "test.ws",
            VanillaScriptPath = CreateFile("vanilla.ws", baseText),
            ModVersions =
            {
                new ModScriptVersion { ModName = "Mod1", ScriptPath = CreateFile("mod1.ws", mod1Text) },
                new ModScriptVersion { ModName = "Mod2", ScriptPath = CreateFile("mod2.ws", mod2Text) }
            }
        });

        // Assert
        Assert.True(result.Success);

        // In CP1250, 'ą' (U+0105) is encoded as 0xB9 (185).
        var bytes = result.MergedContent!;
        Assert.Contains((byte)185, bytes);

        // Verify it can be decoded back to the correct character using CP1250
        var mergedText = Encoding.GetEncoding(1250).GetString(bytes);
        Assert.Contains("ą", mergedText);
    }

    private string CreateFile(string fileName, string content)
    {
        var path = Path.Combine(_scope.CreateSubdirectory("files"), fileName);
        File.WriteAllText(path, content, Encoding.GetEncoding(1250));
        return path;
    }

    private static int CountOccurrences(string text, string pattern)
    {
        var count = 0;
        var i = 0;
        while ((i = text.IndexOf(pattern, i, StringComparison.Ordinal)) != -1)
        {
            i += pattern.Length;
            count++;
        }
        return count;
    }
}

