using Microsoft.Win32;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;

namespace Alacrity.Launcher.Core;

public sealed class SteamTerrariaInstallationLocator
{
    private static readonly Regex LibraryPathPattern = new Regex(
        @"""path""\s+""(?<path>[^""]+)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex InstallDirectoryPattern = new Regex(
        @"""installdir""\s+""(?<directory>[^""]+)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public SteamTerrariaInstallation? Locate(string? manuallySelectedTerrariaDirectory = null)
    {
        if (TryCreateInstallation(manuallySelectedTerrariaDirectory, steamDirectory: null, out SteamTerrariaInstallation? manualInstallation)) {
            return manualInstallation;
        }

        foreach (string steamDirectory in FindSteamDirectories()) {
            foreach (string libraryDirectory in FindLibraryDirectories(steamDirectory)) {
                SteamTerrariaInstallation? installation = TryFindTerrariaInLibrary(libraryDirectory, steamDirectory);
                if (installation is not null) {
                    return installation;
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> FindSteamDirectories()
    {
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (OperatingSystem.IsWindows()) {
            AddWindowsRegistrySteamDirectories(directories);
        }

        string? programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrWhiteSpace(programFilesX86)) {
            directories.Add(Path.Combine(programFilesX86, "Steam"));
        }

        return directories.Where(Directory.Exists);
    }

    [SupportedOSPlatform("windows")]
    private static void AddWindowsRegistrySteamDirectories(HashSet<string> directories)
    {
        AddRegistrySteamDirectory(directories, Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"));
        AddRegistrySteamDirectory(directories, Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam"));
    }

    [SupportedOSPlatform("windows")]
    private static void AddRegistrySteamDirectory(HashSet<string> directories, RegistryKey? key)
    {
        using (key) {
            if (key?.GetValue("SteamPath") is string path && !string.IsNullOrWhiteSpace(path)) {
                directories.Add(path);
            }
        }
    }

    private static IEnumerable<string> FindLibraryDirectories(string steamDirectory)
    {
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            steamDirectory
        };

        string libraryFoldersPath = Path.Combine(steamDirectory, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(libraryFoldersPath)) {
            return directories;
        }

        foreach (Match match in LibraryPathPattern.Matches(File.ReadAllText(libraryFoldersPath))) {
            string path = match.Groups["path"].Value.Replace("\\\\", "\\", StringComparison.Ordinal);
            if (Directory.Exists(path)) {
                directories.Add(path);
            }
        }

        return directories;
    }

    private static SteamTerrariaInstallation? TryFindTerrariaInLibrary(string libraryDirectory, string steamDirectory)
    {
        string manifestPath = Path.Combine(libraryDirectory, "steamapps", "appmanifest_105600.acf");
        if (!File.Exists(manifestPath)) {
            return null;
        }

        Match match = InstallDirectoryPattern.Match(File.ReadAllText(manifestPath));
        if (!match.Success) {
            return null;
        }

        string terrariaDirectory = Path.Combine(libraryDirectory, "steamapps", "common", match.Groups["directory"].Value);
        return TryCreateInstallation(terrariaDirectory, steamDirectory, out SteamTerrariaInstallation? installation) ? installation : null;
    }

    private static bool TryCreateInstallation(string? terrariaDirectory, string? steamDirectory, out SteamTerrariaInstallation? installation)
    {
        installation = null;
        if (string.IsNullOrWhiteSpace(terrariaDirectory)) {
            return false;
        }

        string fullDirectory;
        try {
            fullDirectory = Path.GetFullPath(terrariaDirectory);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException) {
            return false;
        }

        string executablePath = Path.Combine(fullDirectory, "Terraria.exe");
        if (!File.Exists(executablePath)) {
            return false;
        }

        steamDirectory ??= FindSteamDirectoryForTerrariaDirectory(fullDirectory);
        string? appManifestPath = FindAppManifestPath(fullDirectory);
        installation = new SteamTerrariaInstallation(fullDirectory, executablePath, steamDirectory is null ? null : Path.Combine(steamDirectory, "Steam.exe"), appManifestPath);
        return true;
    }

    private static string? FindAppManifestPath(string terrariaDirectory)
    {
        DirectoryInfo? commonDirectory = Directory.GetParent(terrariaDirectory);
        DirectoryInfo? steamAppsDirectory = commonDirectory?.Parent;
        string? appManifestPath = steamAppsDirectory is null ? null : Path.Combine(steamAppsDirectory.FullName, "appmanifest_105600.acf");
        return appManifestPath is not null && File.Exists(appManifestPath) ? appManifestPath : null;
    }

    private static string? FindSteamDirectoryForTerrariaDirectory(string terrariaDirectory)
    {
        DirectoryInfo? current = new DirectoryInfo(terrariaDirectory);
        while (current?.Parent is not null) {
            if (File.Exists(Path.Combine(current.FullName, "Steam.exe"))) {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }
}

public sealed record SteamTerrariaInstallation(string TerrariaDirectory, string TerrariaExecutablePath, string? SteamExecutablePath, string? AppManifestPath);
