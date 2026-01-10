using System.IO;
using System.Text.Json;
using W2ScriptMerger.Models;

namespace W2ScriptMerger.Services;

internal class DeploymentService(ConfigService configService, ScriptExtractionService extractionService)
{
    private const string BackupExtension = ".smbk";
    private const string ManifestFileName = "w2scriptmerger_deployment.json";

    private class DeploymentManifest
    {
        public DateTime DeployedAt { get; set; }
        public List<string> BackedUpFiles { get; init; } = [];
        public List<string> DeployedFiles { get; init; } = [];
    }

    public void DeployMod(ModArchive mod, HashSet<string> mergedDzipNames)
    {
        var targetBasePath = GetTargetPath(mod.ModInstallLocation);

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
                File.Copy(sourcePath, targetPath, overwrite: true);
        }

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
            extractionService.PackMergedDzip(conflict.DzipName);

            var mergedDzipPath = Path.Combine(extractionService.MergedScriptsPath, conflict.DzipName);
            if (!File.Exists(mergedDzipPath))
                continue;

            var targetPath = Path.Combine(targetBasePath, conflict.DzipName);

            if (BackupIfExists(targetPath))
                manifest.BackedUpFiles.Add(conflict.DzipName);

            File.Copy(mergedDzipPath, targetPath, overwrite: true);

            if (!manifest.DeployedFiles.Contains(conflict.DzipName))
                manifest.DeployedFiles.Add(conflict.DzipName);
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

        RestoreBackupsInDirectory(configService.UserContentPath);
    }

    private static void RestoreBackupsFromManifest(string targetBasePath)
    {
        var manifest = LoadOrCreateManifest(targetBasePath);

        foreach (var deployedFile in manifest.DeployedFiles)
        {
            var filePath = Path.Combine(targetBasePath, deployedFile);
            RestoreBackup(filePath);
        }

        var manifestPath = Path.Combine(targetBasePath, ManifestFileName);
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

        var backupPath = filePath + BackupExtension;

        if (File.Exists(backupPath))
            return false;

        File.Copy(filePath, backupPath, overwrite: false);
        return true;
    }

    private static DeploymentManifest LoadOrCreateManifest(string targetBasePath)
    {
        var manifestPath = Path.Combine(targetBasePath, ManifestFileName);
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
        var manifestPath = Path.Combine(targetBasePath, ManifestFileName);
        var json = JsonSerializer.Serialize(manifest, configService.JsonSerializerOptions);
        File.WriteAllText(manifestPath, json);
    }

    private static void RestoreBackup(string filePath)
    {
        var backupPath = filePath + BackupExtension;

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

        var backupFiles = Directory.GetFiles(directoryPath, "*" + BackupExtension, SearchOption.AllDirectories);

        foreach (var backupPath in backupFiles)
        {
            var originalPath = backupPath[..^BackupExtension.Length];

            if (File.Exists(originalPath))
                File.Delete(originalPath);

            File.Move(backupPath, originalPath);
        }
    }
}
