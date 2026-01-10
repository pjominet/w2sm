using System.IO;
using System.Text.Json;
using W2ScriptMerger.Models;
using W2ScriptMerger.Tools;

namespace W2ScriptMerger.Services;

internal class DeploymentService(ConfigService configService, ScriptExtractionService extractionService)
{
    private class DeploymentManifest
    {
        public DateTime DeployedAt { get; set; }
        public List<string> ManagedFiles { get; init; } = [];
    }

    public void DeployMod(ModArchive mod, HashSet<string> mergedDzipNames)
    {
        var targetBasePath = GetTargetPath(mod.ModInstallLocation);
        var manifest = LoadOrCreateManifest(targetBasePath);

        foreach (var file in mod.Files)
        {
            if (file.Type == ModFileType.Dzip && mergedDzipNames.Contains(file.Name))
                continue;

            var relativePath = GetDeployRelativePath(file.RelativePath);
            var targetPath = Path.Combine(targetBasePath, relativePath);

            BackupIfExists(targetPath);

            var dir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var sourcePath = Path.Combine(mod.StagingPath, file.RelativePath);
            if (File.Exists(sourcePath))
            {
                File.Copy(sourcePath, targetPath, overwrite: true);

                if (!manifest.ManagedFiles.Contains(relativePath))
                    manifest.ManagedFiles.Add(relativePath);
            }
        }

        manifest.DeployedAt = DateTime.Now;
        SaveManifest(targetBasePath, manifest);
        mod.IsDeployed = true;
    }

    public void DeployMergedDzips(List<DzipConflict> conflicts)
    {
        var targetBasePath = configService.GameCookedPCPath;
        if (string.IsNullOrEmpty(targetBasePath))
            throw new InvalidOperationException("Game path not set");

        var manifest = LoadOrCreateManifest(targetBasePath);

        foreach (var conflict in conflicts.Where(c => c.IsFullyMerged))
        {
            var packedDzipPath = extractionService.PackMergedDzip(conflict.DzipName);
            if (packedDzipPath is null)
                continue;

            var targetPath = Path.Combine(targetBasePath, conflict.DzipName);

            BackupIfExists(targetPath);
            File.Copy(packedDzipPath, targetPath, overwrite: true);

            if (!manifest.ManagedFiles.Contains(conflict.DzipName))
                manifest.ManagedFiles.Add(conflict.DzipName);
        }

        manifest.DeployedAt = DateTime.Now;
        SaveManifest(targetBasePath, manifest);
    }

    public void UndeployMod(ModArchive mod)
    {
        if (!mod.IsDeployed)
            return;

        var targetBasePath = GetTargetPath(mod.ModInstallLocation);

        foreach (var file in mod.Files)
        {
            var relativePath = GetDeployRelativePath(file.RelativePath);
            var targetPath = Path.Combine(targetBasePath, relativePath);

            RestoreBackup(targetPath);
        }

        mod.IsDeployed = false;
    }

    public void RestoreAllBackups()
    {
        var cookedPcPath = configService.GameCookedPCPath;
        if (!string.IsNullOrEmpty(cookedPcPath))
        {
            RestoreBackupsFromManifest(cookedPcPath);
            RestoreBackupsInDirectory(cookedPcPath);
        }

        var userContentPath = configService.UserContentPath;
        if (string.IsNullOrEmpty(userContentPath) || !Directory.Exists(userContentPath))
            return;

        RestoreBackupsFromManifest(userContentPath);
        RestoreBackupsInDirectory(userContentPath);
    }

    private static void RestoreBackupsFromManifest(string targetBasePath)
    {
        var manifest = LoadOrCreateManifest(targetBasePath);

        foreach (var managedFile in manifest.ManagedFiles)
        {
            var filePath = Path.Combine(targetBasePath, managedFile);
            var backupPath = filePath + Constants.BACKUP_FILE_EXTENSION;

            // If no backup exists, leave the file alone as it's either:
            // 1. A vanilla file that was never backed up (shouldn't happen but safe)
            // 2. A file that was already restored
            // Do not delete files without backups as that could delete vanilla game files
            if (!File.Exists(backupPath))
                continue;

            // Restore from backup
            if (File.Exists(filePath))
                File.Delete(filePath);
            File.Move(backupPath, filePath);
        }

        var manifestPath = Path.Combine(targetBasePath, Constants.DEPLOY_MANIFEST_FILENAME);
        if (File.Exists(manifestPath))
            File.Delete(manifestPath);
    }

    public void PurgeDeployedMods(IEnumerable<ModArchive> mods)
    {
        foreach (var mod in mods.Where(m => m.IsDeployed))
            UndeployMod(mod);

        RestoreAllBackups();
    }

    private string GetTargetPath(InstallLocation location)
    {
        return location switch
        {
            InstallLocation.UserContent => configService.UserContentPath,
            _ => configService.GameCookedPCPath
                ?? throw new InvalidOperationException("Game path not set")
        };
    }

    private static string GetDeployRelativePath(string filePath)
    {
        var normalized = filePath.Replace('\\', '/');

        if (normalized.StartsWith("CookedPC/", StringComparison.OrdinalIgnoreCase))
            return normalized["CookedPC/".Length..];

        if (normalized.StartsWith("UserContent/", StringComparison.OrdinalIgnoreCase))
            return normalized["UserContent/".Length..];

        return normalized;
    }

    private static bool BackupIfExists(string filePath)
    {
        if (!File.Exists(filePath))
            return false;

        var backupPath = filePath + Constants.BACKUP_FILE_EXTENSION;

        if (File.Exists(backupPath))
            return false;

        File.Copy(filePath, backupPath, overwrite: false);
        return true;
    }

    private static DeploymentManifest LoadOrCreateManifest(string targetBasePath)
    {
        var manifestPath = Path.Combine(targetBasePath, Constants.DEPLOY_MANIFEST_FILENAME);
        if (!File.Exists(manifestPath))
            return new DeploymentManifest();

        try
        {
            var json = File.ReadAllText(manifestPath);
            return JsonSerializer.Deserialize<DeploymentManifest>(json) ?? new DeploymentManifest();
        }
        catch
        {
            return new DeploymentManifest();
        }
    }

    private void SaveManifest(string targetBasePath, DeploymentManifest manifest)
    {
        var manifestPath = Path.Combine(targetBasePath, Constants.DEPLOY_MANIFEST_FILENAME);
        var json = JsonSerializer.Serialize(manifest, configService.JsonSerializerOptions);
        File.WriteAllText(manifestPath, json);
    }

    private static void RestoreBackup(string filePath)
    {
        var backupPath = filePath + Constants.BACKUP_FILE_EXTENSION;

        if (!File.Exists(backupPath))
            return;

        if (File.Exists(filePath))
            File.Delete(filePath);

        File.Move(backupPath, filePath);
    }

    private static void RestoreBackupsInDirectory(string? directoryPath)
    {
        if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
            return;

        var backupFiles = Directory.GetFiles(directoryPath, $"*{Constants.BACKUP_FILE_EXTENSION}", SearchOption.AllDirectories);

        foreach (var backupPath in backupFiles)
        {
            var originalPath = backupPath[..^Constants.BACKUP_FILE_EXTENSION.Length];

            if (File.Exists(originalPath))
                File.Delete(originalPath);

            File.Move(backupPath, originalPath);
        }
    }
}
