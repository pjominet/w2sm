using System.Text.Json.Serialization;

namespace W2ScriptMerger.Models;

internal class DeployManifest
{
    public DateTime DeployedAt { get; set; }
    public List<string> ManagedFiles { get; init; } = [];

    [JsonIgnore]
    public HashSet<string> ManagedFilesIndex { get; } = new(StringComparer.OrdinalIgnoreCase);

    public DeployManifest()
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
