namespace Alacrity.Launcher.Core;

public sealed class LauncherPaths
{
    public LauncherPaths(string rootDirectory)
    {
        RootDirectory = Path.GetFullPath(rootDirectory);
    }

    public string RootDirectory { get; }

    public string DataDirectory => Path.Combine(RootDirectory, "data");

    public string VersionsDirectory => Path.Combine(RootDirectory, "Versions");

    public string RecoveryDirectory => Path.Combine(RootDirectory, "Recovery");

    public string LogsDirectory => Path.Combine(RootDirectory, "Logs");

    public string ToolsDirectory => Path.Combine(RootDirectory, "Tools");

    public string DepotDownloaderDirectory => Path.Combine(ToolsDirectory, "DepotDownloader");

    public string LegacySteamCmdDirectory => Path.Combine(ToolsDirectory, "SteamCMD");

    public string VersionCatalogPath => Path.Combine(DataDirectory, "versions.json");

    public string SettingsPath => Path.Combine(DataDirectory, "launcher-settings.json");

    public string RecoveryJournalPath => Path.Combine(RecoveryDirectory, "active-launch.json");

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(VersionsDirectory);
        Directory.CreateDirectory(RecoveryDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(ToolsDirectory);
    }

    public string GetVersionDirectory(string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        foreach (char character in Path.GetInvalidFileNameChars()) {
            if (version.IndexOf(character) >= 0) {
                throw new ArgumentException("The Terraria version contains an invalid path character.", nameof(version));
            }
        }

        return Path.Combine(VersionsDirectory, version);
    }
}
