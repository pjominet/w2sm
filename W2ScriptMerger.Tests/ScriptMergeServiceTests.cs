using System.IO;
using System.Text;
using System.Text.Json;
using W2ScriptMerger.Models;
using W2ScriptMerger.Services;
using W2ScriptMerger.Tests.Infrastructure;

namespace W2ScriptMerger.Tests;

public class ScriptMergeServiceTests : IDisposable
{
    private readonly TestArtifactScope _scope;
    private readonly string _workspace;

    static ScriptMergeServiceTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public ScriptMergeServiceTests()
    {
        _scope = TestArtifactScope.Create(nameof(ScriptMergeServiceTests));
        _workspace = _scope.CreateSubdirectory("workspace");
    }

    public void Dispose() => _scope.Dispose();

    [Fact]
    public void AttemptAutoMerge_WithNonConflictingChanges_Succeeds()
    {
        const string vanillaContent = """
                                      // Vanilla script
                                      class TestClass {
                                          function Original() {
                                              // Original code
                                          }
                                      }
                                      """;

        const string modContent = """
                                  // Vanilla script
                                  class TestClass {
                                      function Original() {
                                          // Original code
                                      }

                                      function NewModFunction() {
                                          // Added by mod
                                      }
                                  }
                                  """;

        var vanillaPath = Path.Combine(_workspace, "vanilla.ws");
        var modPath = Path.Combine(_workspace, "mod.ws");

        File.WriteAllText(vanillaPath, vanillaContent, Encoding.GetEncoding(1250));
        File.WriteAllText(modPath, modContent, Encoding.GetEncoding(1250));

        var conflict = new ScriptFileConflict
        {
            ScriptRelativePath = "test.ws",
            VanillaScriptPath = vanillaPath,
            CurrentMergeBasePath = vanillaPath
        };
        conflict.ModVersions.Add(new ModScriptVersion
        {
            ModName = "TestMod",
            ScriptPath = modPath
        });

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        var configService = new ConfigService(options)
        {
            RuntimeDataPath = _workspace
        };
        var vanillaIndexService = new IndexerService(configService);
        var extractionService = new ScriptExtractionService(configService, vanillaIndexService);
        var mergeService = new ScriptMergeService(extractionService);

        var result = mergeService.AttemptAutoMerge(conflict);

        Assert.True(result.Success);
        Assert.NotNull(result.MergedContent);

        var mergedText = Encoding.GetEncoding(1250).GetString(result.MergedContent);
        Assert.Contains("NewModFunction", mergedText);
        Assert.Contains("Original", mergedText);
    }

    [Fact]
    public void AttemptAutoMerge_WithSingleMod_AppliesModChanges()
    {
        const string vanillaContent = """
                                      class TestClass {
                                          function Original() {}
                                      }
                                      """;

        const string modContent = """
                                  class TestClass {
                                      function Original() {}
                                      function ModAdded() {}
                                  }
                                  """;

        var vanillaPath = Path.Combine(_workspace, "vanilla2.ws");
        var modPath = Path.Combine(_workspace, "mod.ws");

        File.WriteAllText(vanillaPath, vanillaContent, Encoding.GetEncoding(1250));
        File.WriteAllText(modPath, modContent, Encoding.GetEncoding(1250));

        var conflict = new ScriptFileConflict
        {
            ScriptRelativePath = "test.ws",
            VanillaScriptPath = vanillaPath,
            CurrentMergeBasePath = vanillaPath
        };
        conflict.ModVersions.Add(new ModScriptVersion { ModName = "TestMod", ScriptPath = modPath });

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        var configService = new ConfigService(options)
        {
            RuntimeDataPath = _workspace
        };
        var vanillaIndexService = new IndexerService(configService);
        var extractionService = new ScriptExtractionService(configService, vanillaIndexService);
        var mergeService = new ScriptMergeService(extractionService);

        var result = mergeService.AttemptAutoMerge(conflict);

        Assert.True(result.Success);
        var mergedText = Encoding.GetEncoding(1250).GetString(result.MergedContent!);
        Assert.Contains("ModAdded", mergedText);
        Assert.Contains("Original", mergedText);
    }
}
