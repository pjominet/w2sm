using System.IO;

namespace W2ScriptMerger.Tools;

internal static class DirectoryUtils
{
    internal static void ClearDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            return;

        foreach (var file in Directory.GetFiles(directoryPath))
            File.Delete(file);

        foreach (var subDir in Directory.GetDirectories(directoryPath))
            Directory.Delete(subDir, recursive: true);
    }
}
