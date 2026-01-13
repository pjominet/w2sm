using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using W2ScriptMerger.Models;
using W2ScriptMerger.Tools;

namespace W2ScriptMerger.Services;

internal class DeploymentManifest
{
    public DateTime DeployedAt { get; set; }
    public List<string> ManagedFiles { get; init; } = [];

    [JsonIgnore]
    public HashSet<string> ManagedFilesIndex { get; } = new(StringComparer.OrdinalIgnoreCase);

    public DeploymentManifest()
    {
        SyncIndex();
    }

    // Rebuild the runtime index after deserialization so manifest files from disk inherit the fast lookups
    public void SyncIndex()
    {
        ManagedFilesIndex.Clear();
        foreach (var file in ManagedFiles)
            ManagedFilesIndex.Add(file);
    }

    // Adds a file path while guarding against duplicates across concurrent deploy operations
    public bool TryAdd(string relativePath)
    {
        if (!ManagedFilesIndex.Add(relativePath))
            return false;

        ManagedFiles.Add(relativePath);
        return true;
    }

    // Removes a file path (used during uninstall/cleanup) and keeps both structures in sync
    public bool Remove(string relativePath)
    {
        if (!ManagedFilesIndex.Remove(relativePath))
            return false;

        ManagedFiles.RemoveAll(f => string.Equals(f, relativePath, StringComparison.OrdinalIgnoreCase));
        return true;
    }
}

internal class DeploymentService(ConfigService configService, ScriptExtractionService extractionService)
{
    public void DeployMod(ModArchive mod, HashSet<string> mergedDzipNames, CancellationToken ctx = default)
    {
        var targetBasePath = GetTargetPath(mod.ModInstallLocation);
        var manifest = LoadOrCreateManifest(targetBasePath);

        var lockObj = new object();

        Parallel.ForEach(mod.Files, new ParallelOptions { CancellationToken = ctx }, file =>
        {
            if (file.Type == ModFileType.Dzip && mergedDzipNames.Contains(file.Name))
                return;

            var relativePath = ModPathHelper.GetDeployRelativePath(file.RelativePath);
            var targetPath = Path.Combine(targetBasePath, relativePath);

            BackupIfExists(targetPath);

            var dir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var sourcePath = Path.Combine(mod.StagingPath, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(sourcePath))
            {
                File.Copy(sourcePath, targetPath, overwrite: true);

                lock (lockObj)
                {
                    manifest.TryAdd(relativePath);
                }
            }
        });

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

    public void RemoveMod(ModArchive mod)
    {
        if (!mod.IsDeployed)
            return;

        var targetBasePath = GetTargetPath(mod.ModInstallLocation);
        var manifest = LoadOrCreateManifest(targetBasePath);

        foreach (var file in mod.Files)
        {
            var relativePath = ModPathHelper.GetDeployRelativePath(file.RelativePath);
            var targetPath = Path.Combine(targetBasePath, relativePath);

            RestoreBackup(targetPath, targetBasePath);
            manifest.Remove(relativePath);
        }

        SaveManifest(targetBasePath, manifest);
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
            RestoreBackup(filePath, targetBasePath);
        }

        var manifestPath = Path.Combine(targetBasePath, Constants.DEPLOY_MANIFEST_FILENAME);
        if (File.Exists(manifestPath))
            File.Delete(manifestPath);
    }

    public void PurgeDeployedMods(IEnumerable<ModArchive> mods)
    {
        foreach (var mod in mods.Where(m => m.IsDeployed))
            RemoveMod(mod);

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
            var manifest = JsonSerializer.Deserialize<DeploymentManifest>(json) ?? new DeploymentManifest();
            manifest.SyncIndex();
            return manifest;
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

    private static void RestoreBackup(string filePath, string? stopAtDirectory = null)
    {
        var backupPath = filePath + Constants.BACKUP_FILE_EXTENSION;

        if (File.Exists(backupPath))
        {
            // Restore from backup (file existed before mod deployment)
            if (File.Exists(filePath))
                File.Delete(filePath);
            File.Move(backupPath, filePath);
        }
        else if (File.Exists(filePath))
        {
            // No backup means this file was added by the mod - delete it
            File.Delete(filePath);

            // Clean up empty parent directories up to the stop directory
            if (stopAtDirectory != null)
                DeleteEmptyParentDirectories(Path.GetDirectoryName(filePath), stopAtDirectory);
        }
    }

    private static void DeleteEmptyParentDirectories(string? directory, string stopAtDirectory)
    {
        while (!string.IsNullOrEmpty(directory) &&
               !string.Equals(directory, stopAtDirectory, StringComparison.OrdinalIgnoreCase) &&
               Directory.Exists(directory))
        {
            if (Directory.EnumerateFileSystemEntries(directory).Any())
                break; // Directory is not empty

            Directory.Delete(directory);
            directory = Path.GetDirectoryName(directory);
        }
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
