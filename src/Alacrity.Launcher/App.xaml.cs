using Alacrity.Launcher.Core;
using System.IO;
using System.Net.Http;
using System.Windows;

namespace Alacrity.Launcher;

public partial class App : Application
{
    private const string SingleInstanceMutexName = @"Local\ExoField33.AlacrityLauncher";

    private readonly HttpClient httpClient = new HttpClient {
        Timeout = TimeSpan.FromSeconds(12)
    };
    private Mutex? singleInstanceMutex;

    public App()
    {
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Alacrity-Launcher/0.1.1");
    }

    protected override void OnStartup(StartupEventArgs arguments)
    {
        singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out bool createdNew);
        if (!createdNew) {
            singleInstanceMutex.Dispose();
            singleInstanceMutex = null;
            MessageBox.Show("Alacrity Launcher is already running.", "Alacrity Launcher", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown(0);
            return;
        }

        base.OnStartup(arguments);
        try {
            string rootDirectory = AppContext.BaseDirectory;
            var paths = new LauncherPaths(rootDirectory);
            var changelogReader = new ChangelogReader();
            var journal = new LaunchRecoveryJournal(paths);
            var coordinator = new LauncherCoordinator(
                paths,
                new TerrariaVersionCatalogStore(paths),
                new LauncherSettingsStore(paths),
                new SteamTerrariaInstallationLocator(),
                changelogReader,
                new LatestTerrariaVersionDiscovery(httpClient),
                new SteamManifestReader(),
                new TerrariaVersionInstaller(paths, changelogReader),
                new DepotDownloaderProvisioner(paths, httpClient),
                new SteamAccountNameLocator(),
                new DepotDownloaderManifestDownloader(),
                new SteamClientLauncher(),
                new TerrariaLaunchService(journal, new DirectoryJunctionService(), new LegacyProfileIsolationService()));

            var window = new MainWindow(new LauncherViewModel(
                coordinator,
                new LauncherSettingsStore(paths)));
            MainWindow = window;
            window.Show();
        }
        catch (Exception exception) {
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "launcher-startup-error.txt"), exception.ToString());
            MessageBox.Show(exception.Message, "Alacrity Launcher", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs eventArgs)
    {
        httpClient.Dispose();
        singleInstanceMutex?.Dispose();
        base.OnExit(eventArgs);
    }
}
