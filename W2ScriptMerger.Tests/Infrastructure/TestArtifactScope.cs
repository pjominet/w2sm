using System.IO;

namespace W2ScriptMerger.Tests.Infrastructure;

internal sealed class TestArtifactScope : IDisposable
{
    private bool _disposed;
    private string RootPath { get; }

    private TestArtifactScope(string rootPath) => RootPath = rootPath;

    public static TestArtifactScope Create(string scenarioName)
    {
        var artifactsRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestArtifacts");
        Directory.CreateDirectory(artifactsRoot);

        var safeName = string.Concat(scenarioName.Select(c => char.IsLetterOrDigit(c) ? c : '_'));
        var scopePath = Path.Combine(artifactsRoot, $"{safeName}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(scopePath);

        return new TestArtifactScope(scopePath);
    }

    public string CreateSubdirectory(string name)
    {
        var path = Path.Combine(RootPath, name);
        Directory.CreateDirectory(path);
        return path;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            if (Directory.Exists(RootPath))
                Directory.Delete(RootPath, true);
        }
        catch
        {
            // Swallow cleanup exceptions to avoid interfering with test results.
        }

        _disposed = true;
    }
}
