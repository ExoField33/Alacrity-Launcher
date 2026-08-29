using System.Text.Json;

namespace Alacrity.Launcher.Core;

public sealed class TerrariaVersionInstaller
{
    private readonly LauncherPaths paths;
    private readonly ChangelogReader changelogReader;

    public TerrariaVersionInstaller(LauncherPaths paths, ChangelogReader changelogReader)
    {
        this.paths = paths;
        this.changelogReader = changelogReader;
    }

    public bool IsInstalled(string version)
    {
        string versionDirectory = paths.GetVersionDirectory(version);
        return File.Exists(Path.Combine(versionDirectory, "Terraria.exe"));
    }

    public async Task<string> CopyCurrentSteamInstallationAsync(SteamTerrariaInstallation installation, string version, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(installation);
        EnsureSourceMatchesVersion(installation.TerrariaDirectory, version);

        string destination = paths.GetVersionDirectory(version);
        if (File.Exists(Path.Combine(destination, "Terraria.exe"))) {
            return destination;
        }

        string stagingDirectory = destination + ".staging-" + Guid.NewGuid().ToString("N");
        try {
            await CopyDirectoryAsync(installation.TerrariaDirectory, stagingDirectory, cancellationToken).ConfigureAwait(false);
            FinalizeStagedVersion(stagingDirectory, destination, version, "steam-copy");
            return destination;
        }
        catch {
            TryDeleteDirectory(stagingDirectory);
            throw;
        }
    }

    public void FinalizeStagedVersion(string stagingDirectory, string destinationDirectory, string version, string source)
    {
        if (!File.Exists(Path.Combine(stagingDirectory, "Terraria.exe"))) {
            throw new InvalidDataException("Steam did not produce a Terraria.exe in the staged version directory.");
        }

        string changelogPath = Path.Combine(stagingDirectory, "changelog.txt");
        if (File.Exists(changelogPath) && changelogReader.TryReadLatestVersion(changelogPath, out string detectedVersion) && !string.Equals(detectedVersion, version, StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidDataException($"The staged Terraria files identify as {detectedVersion}, not requested version {version}.");
        }

        if (Directory.Exists(destinationDirectory)) {
            throw new IOException($"The Terraria {version} destination already exists.");
        }

        Directory.Move(stagingDirectory, destinationDirectory);
        var metadata = new InstalledVersionMetadata {
            Version = version,
            Source = source,
            InstalledUtc = DateTimeOffset.UtcNow
        };

        string metadataPath = Path.Combine(destinationDirectory, "alacrity-launcher-version.json");
        File.WriteAllText(metadataPath, JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void EnsureSourceMatchesVersion(string installationDirectory, string version)
    {
        string changelogPath = Path.Combine(installationDirectory, "changelog.txt");
        if (!File.Exists(changelogPath)) {
            throw new InvalidDataException("The installed Terraria directory does not include changelog.txt, so the launcher cannot verify the current version.");
        }

        if (!changelogReader.TryReadLatestVersion(changelogPath, out string detectedVersion) || !string.Equals(detectedVersion, version, StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidDataException($"The installed Terraria changelog does not identify this copy as Terraria {version}.");
        }
    }

    private static async Task CopyDirectoryAsync(string sourceDirectory, string destinationDirectory, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (string directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories)) {
            cancellationToken.ThrowIfCancellationRequested();
            string relativePath = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(destinationDirectory, relativePath));
        }

        foreach (string sourcePath in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories)) {
            cancellationToken.ThrowIfCancellationRequested();
            string relativePath = Path.GetRelativePath(sourceDirectory, sourcePath);
            string destinationPath = Path.Combine(destinationDirectory, relativePath);

            await using FileStream source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using FileStream destination = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous | FileOptions.WriteThrough);
            await source.CopyToAsync(destination, 65536, cancellationToken).ConfigureAwait(false);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try {
            if (Directory.Exists(path)) {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception) {
        }
    }
}

public sealed class InstalledVersionMetadata
{
    public required string Version { get; init; }

    public required string Source { get; init; }

    public DateTimeOffset InstalledUtc { get; init; }
}
