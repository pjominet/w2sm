using System.IO;
using W2ScriptMerger.Models;

namespace W2ScriptMerger.Services;

public class InstallService
{
    private readonly ConfigService _configService;
    private readonly DzipService _dzipService;

    public InstallService(ConfigService configService, DzipService dzipService)
    {
        _configService = configService;
        _dzipService = dzipService;
    }

    public string GetInstallPath(InstallLocation location)
    {
        return location switch
        {
            InstallLocation.UserContent => _configService.UserContentPath,
            InstallLocation.CookedPC => _configService.CookedPCPath ?? throw new InvalidOperationException("Game path not set"),
            _ => throw new ArgumentOutOfRangeException(nameof(location))
        };
    }

    public void InstallFile(ModFile file, InstallLocation location)
    {
        var basePath = GetInstallPath(location);
        var targetPath = Path.Combine(basePath, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        
        var dir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllBytes(targetPath, file.Content);
    }

    public void InstallMergedScript(ScriptConflict conflict, InstallLocation location)
    {
        if (conflict.MergedContent is null)
            throw new InvalidOperationException("No merged content available");

        var basePath = GetInstallPath(location);
        var targetPath = Path.Combine(basePath, conflict.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        
        var dir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllBytes(targetPath, conflict.MergedContent);
    }

    public void InstallNonConflictingFiles(ModArchive archive, InstallLocation location, HashSet<string> conflictPaths)
    {
        var basePath = GetInstallPath(location);

        foreach (var file in archive.Files)
        {
            var normalizedPath = file.RelativePath.Replace('\\', '/').ToLowerInvariant();
            if (conflictPaths.Contains(normalizedPath))
                continue;

            var targetPath = Path.Combine(basePath, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            
            var dir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllBytes(targetPath, file.Content);
        }
    }

    public void BackupFile(string relativePath, InstallLocation location)
    {
        var basePath = GetInstallPath(location);
        var sourcePath = Path.Combine(basePath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        
        if (!File.Exists(sourcePath))
            return;

        var backupDir = Path.Combine(basePath, ".w2merger_backup");
        Directory.CreateDirectory(backupDir);

        var backupPath = Path.Combine(backupDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var backupFileDir = Path.GetDirectoryName(backupPath);
        if (!string.IsNullOrEmpty(backupFileDir))
            Directory.CreateDirectory(backupFileDir);

        File.Copy(sourcePath, backupPath, overwrite: true);
    }

    public void RestoreBackup(string relativePath, InstallLocation location)
    {
        var basePath = GetInstallPath(location);
        var backupDir = Path.Combine(basePath, ".w2merger_backup");
        var backupPath = Path.Combine(backupDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
        
        if (!File.Exists(backupPath))
            return;

        var targetPath = Path.Combine(basePath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        File.Copy(backupPath, targetPath, overwrite: true);
    }

    public void CreateDzipFromMergedScripts(List<ScriptConflict> mergedConflicts, string outputPath)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "W2ScriptMerger_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            foreach (var conflict in mergedConflicts.Where(c => c.MergedContent is not null))
            {
                var filePath = Path.Combine(tempDir, conflict.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllBytes(filePath, conflict.MergedContent!);
            }

            _dzipService.CreateDzip(outputPath, tempDir);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }
}
