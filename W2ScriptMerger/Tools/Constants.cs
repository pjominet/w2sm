namespace W2ScriptMerger.Tools;

// ReSharper disable InconsistentNaming
internal static class Constants
{
    // files names
    internal const string MERGE_SUMMARY_FILENAME = "merge_summary.md";
    internal const string STAGING_LIST_FILENAME = "loaded_mods.json";
    internal const string CONFIG_FILENAME = "config.json";
    internal const string VANILLA_FILES_INDEX_FILENAME = "vanilla_files.json";
    internal const string DEPLOY_MANIFEST_FILENAME = "w2sm_deploy.json";

    // folders
    internal const string VANILLA_SCRIPTS_FOLDER = "vanilla_scripts";
    internal const string MOD_SCRIPTS_FOLDER = "mod_scripts";
    internal const string MERGED_SCRIPTS_FOLDER = "merged_scripts";
    internal const string STAGING_FOLDER = "mod_staging";

    // file extensions
    internal const string BACKUP_FILE_EXTENSION = ".smbk";
}
