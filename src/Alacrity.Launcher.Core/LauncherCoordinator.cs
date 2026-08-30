using System.Text.Json;

namespace Alacrity.Launcher.Core;

public sealed class LauncherCoordinator
{
    private readonly LauncherPaths paths;
    private readonly TerrariaVersionCatalogStore catalogStore;
    private readonly LauncherSettingsStore settingsStore;
    private readonly SteamTerrariaInstallationLocator installationLocator;
    private readonly ChangelogReader changelogReader;
    private readonly LatestTerrariaVersionDiscovery latestVersionDiscovery;
    private readonly SteamManifestReader manifestReader;
    private readonly TerrariaVersionInstaller versionInstaller;
    private readonly DepotDownloaderProvisioner depotDownloaderProvisioner;
    private readonly SteamAccountNameLocator steamAccountNameLocator;
    private readonly DepotDownloaderManifestDownloader depotDownloader;
    private readonly ArchiveVersionDownloader archiveDownloader;
    private readonly SteamClientLauncher steamClientLauncher;
    private readonly TerrariaLaunchService launchService;

    public LauncherCoordinator(
        LauncherPaths paths,
        TerrariaVersionCatalogStore catalogStore,
        LauncherSettingsStore settingsStore,
        SteamTerrariaInstallationLocator installationLocator,
        ChangelogReader changelogReader,
        LatestTerrariaVersionDiscovery latestVersionDiscovery,
        SteamManifestReader manifestReader,
        TerrariaVersionInstaller versionInstaller,
        DepotDownloaderProvisioner depotDownloaderProvisioner,
        SteamAccountNameLocator steamAccountNameLocator,
        DepotDownloaderManifestDownloader depotDownloader,
        ArchiveVersionDownloader archiveDownloader,
        SteamClientLauncher steamClientLauncher,
        TerrariaLaunchService launchService)
    {
        this.paths = paths;
        this.catalogStore = catalogStore;
        this.settingsStore = settingsStore;
        this.installationLocator = installationLocator;
        this.changelogReader = changelogReader;
        this.latestVersionDiscovery = latestVersionDiscovery;
        this.manifestReader = manifestReader;
        this.versionInstaller = versionInstaller;
        this.depotDownloaderProvisioner = depotDownloaderProvisioner;
        this.steamAccountNameLocator = steamAccountNameLocator;
        this.depotDownloader = depotDownloader;
        this.archiveDownloader = archiveDownloader;
        this.steamClientLauncher = steamClientLauncher;
        this.launchService = launchService;
    }

    public async Task<LauncherStartupState> InitializeAsync(CancellationToken cancellationToken)
    {
        paths.EnsureDirectories();
        await launchService.RecoverAsync(cancellationToken).ConfigureAwait(false);

        LauncherSettings settings = await settingsStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        TerrariaVersionCatalog catalog = await catalogStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        SteamTerrariaInstallation? installation = installationLocator.Locate(settings.TerrariaDirectory);
        string? installedSteamVersion = TryReadInstalledVersion(installation);

        if (installedSteamVersion is not null && catalog.Find(installedSteamVersion) is null) {
            catalog.Upsert(new TerrariaVersionEntry {
                Version = installedSteamVersion,
                ManifestId = manifestReader.TryReadTerrariaManifestId(installation),
                IsAutomaticallyDiscovered = true
            });
            await catalogStore.SaveAsync(catalog, cancellationToken).ConfigureAwait(false);
        }

        LatestTerrariaVersion? latest = null;
        string? installedManifestId = manifestReader.TryReadTerrariaManifestId(installation);
        try {
            latest = await latestVersionDiscovery.TryDiscoverAsync(cancellationToken).ConfigureAwait(false);
            if (latest is not null) {
                TerrariaVersionEntry? existing = catalog.Find(latest.Version);
                catalog.Upsert(new TerrariaVersionEntry {
                    Version = latest.Version,
                    ManifestId = string.Equals(installedSteamVersion, latest.Version, StringComparison.OrdinalIgnoreCase) ? installedManifestId ?? existing?.ManifestId : existing?.ManifestId,
                    Url = existing?.Url,
                    IsAutomaticallyDiscovered = true
                });
                await catalogStore.SaveAsync(catalog, cancellationToken).ConfigureAwait(false);

            }
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidDataException) {
            return CreateStartupState(catalog, settings, installation, installedSteamVersion, "Version refresh failed: " + exception.Message);
        }

        return CreateStartupState(catalog, settings, installation, installedSteamVersion, latest is null ? "Version refresh returned no Terraria release." : $"Latest Terraria release: {latest.Version}.");
    }

    public async Task DownloadVersionAsync(TerrariaVersionEntry entry, LauncherSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(settings);
        string destinationDirectory = paths.GetVersionDirectory(entry.Version);
        if (versionInstaller.IsInstalled(entry.Version)) {
            return;
        }

        if (!string.IsNullOrWhiteSpace(entry.Url)) {
            await DownloadArchiveVersionAsync(entry, destinationDirectory, cancellationToken).ConfigureAwait(false);
            return;
        }

        SteamTerrariaInstallation? installation = installationLocator.Locate(settings.TerrariaDirectory);
        if (installation is not null && string.Equals(TryReadInstalledVersion(installation), entry.Version, StringComparison.OrdinalIgnoreCase)) {
            await versionInstaller.CopyCurrentSteamInstallationAsync(installation, entry.Version, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (string.IsNullOrWhiteSpace(entry.ManifestId)) {
            throw new InvalidOperationException($"Terraria {entry.Version} is not installed and does not have a download source in versions.json.");
        }

        string? steamAccountName = settings.SteamAccountName ?? steamAccountNameLocator.TryLocate(installation);
        if (string.IsNullOrWhiteSpace(steamAccountName)) {
            throw new SteamAccountNameRequiredException();
        }

        string depotDownloaderPath = await depotDownloaderProvisioner.EnsureAvailableAsync(cancellationToken).ConfigureAwait(false);
        string stagingDirectory = destinationDirectory + ".staging-" + Guid.NewGuid().ToString("N");
        try {
            string downloadedDirectory = await depotDownloader.DownloadAsync(new DepotDownloadRequest {
                Version = entry.Version,
                ManifestId = entry.ManifestId!,
                DepotDownloaderPath = depotDownloaderPath,
                OutputDirectory = stagingDirectory,
                SteamAccountName = steamAccountName
            }, cancellationToken).ConfigureAwait(false);

            versionInstaller.FinalizeStagedVersion(downloadedDirectory, destinationDirectory, entry.Version, "depotdownloader-depot");
        }
        catch {
            TryDeleteDirectory(stagingDirectory);
            throw;
        }
    }

    public async Task LaunchAsync(TerrariaVersionEntry entry, LauncherSettings settings, bool isolateLegacyProfile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(settings);
        SteamTerrariaInstallation? installation = installationLocator.Locate(settings.TerrariaDirectory);
        bool requiresSteamLaunch = TerrariaLaunchService.RequiresSteamLaunch(entry.Version);
        bool canLaunchWithoutSteam = TerrariaLaunchService.CanLaunchWithoutSteam(entry.Version);
        if (requiresSteamLaunch && installation is null) {
            throw new DirectoryNotFoundException("Terraria could not be found in a Steam library. Choose its installed folder in launcher settings.");
        }

        string? currentVersion = TryReadInstalledVersion(installation);
        if (requiresSteamLaunch && currentVersion is null) {
            throw new InvalidDataException("The installed Steam Terraria changelog does not identify its version.");
        }

        if (!canLaunchWithoutSteam) {
            if (installation is null) {
                throw new DirectoryNotFoundException("Terraria could not be found in a Steam library. Choose its installed folder in launcher settings.");
            }

            await steamClientLauncher.EnsureRunningAsync(installation, cancellationToken).ConfigureAwait(false);
        }

        await launchService.LaunchAsync(new TerrariaLaunchRequest {
            TerrariaInstallation = installation,
            Version = entry.Version,
            CurrentVersion = currentVersion,
            VersionDirectory = paths.GetVersionDirectory(entry.Version),
            IsolateLegacyProfile = isolateLegacyProfile && IsLegacyVersion(entry.Version)
        }, cancellationToken).ConfigureAwait(false);
    }

    public static bool IsLegacyVersion(string version)
    {
        return TerrariaVersionNumber.TryParse(version, out TerrariaVersionNumber parsed)
            && TerrariaVersionNumber.TryParse("1.3.5.3", out TerrariaVersionNumber legacyBoundary)
            && parsed.CompareTo(legacyBoundary) <= 0;
    }

    private LauncherStartupState CreateStartupState(TerrariaVersionCatalog catalog, LauncherSettings settings, SteamTerrariaInstallation? installation, string? installedSteamVersion, string status)
    {
        IReadOnlyDictionary<string, string> changelogs = installation is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : TryReadChangelogs(installation.TerrariaDirectory);

        var versions = new List<LauncherVersionPresentation>(catalog.Versions.Count);
        foreach (TerrariaVersionEntry entry in catalog.Versions) {
            bool canCopyCurrentSteamInstall = installation is not null && string.Equals(installedSteamVersion, entry.Version, StringComparison.OrdinalIgnoreCase);
            versions.Add(new LauncherVersionPresentation(entry, versionInstaller.IsInstalled(entry.Version), canCopyCurrentSteamInstall, changelogs.TryGetValue(entry.Version, out string? changelog) ? changelog : null));
        }

        return new LauncherStartupState(settings, installation, installedSteamVersion, versions, status);
    }

    private string? TryReadInstalledVersion(SteamTerrariaInstallation? installation)
    {
        if (installation is null) {
            return null;
        }

        string changelogPath = Path.Combine(installation.TerrariaDirectory, "changelog.txt");
        return File.Exists(changelogPath) && changelogReader.TryReadLatestVersion(changelogPath, out string version) ? version : null;
    }

    private IReadOnlyDictionary<string, string> TryReadChangelogs(string terrariaDirectory)
    {
        string changelogPath = Path.Combine(terrariaDirectory, "changelog.txt");
        return File.Exists(changelogPath) ? changelogReader.Read(changelogPath) : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private async Task DownloadArchiveVersionAsync(TerrariaVersionEntry entry, string destinationDirectory, CancellationToken cancellationToken)
    {
        string stagingDirectory = destinationDirectory + ".staging-" + Guid.NewGuid().ToString("N");
        try {
            string contentDirectory = await archiveDownloader.DownloadAndExtractAsync(entry.Version, entry.Url!, stagingDirectory, cancellationToken).ConfigureAwait(false);
            versionInstaller.FinalizeStagedVersion(contentDirectory, destinationDirectory, entry.Version, "archive-url");
        }
        catch {
            TryDeleteDirectory(stagingDirectory);
            throw;
        }

        TryDeleteDirectory(stagingDirectory);
    }

    private static void TryDeleteDirectory(string directory)
    {
        try {
            if (Directory.Exists(directory)) {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (Exception) {
        }
    }
}

public sealed record LauncherStartupState(
    LauncherSettings Settings,
    SteamTerrariaInstallation? TerrariaInstallation,
    string? InstalledSteamVersion,
    IReadOnlyList<LauncherVersionPresentation> Versions,
    string Status);

public sealed record LauncherVersionPresentation(TerrariaVersionEntry Entry, bool IsInstalled, bool CanCopyCurrentSteamInstall, string? Changelog)
{
    public bool CanPrepare => Entry.CanDownload || CanCopyCurrentSteamInstall;
}
