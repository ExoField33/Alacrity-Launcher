using Alacrity.Launcher.Core;
using System.IO;
using System.Net.Http;
using System.Windows;

namespace Alacrity.Launcher;

public partial class App : Application
{
    private readonly HttpClient httpClient = new HttpClient {
        Timeout = TimeSpan.FromSeconds(12)
    };

    public App()
    {
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Alacrity-Launcher/0.1.0");
    }

    protected override void OnStartup(StartupEventArgs arguments)
    {
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
                new SteamCmdProvisioner(paths, httpClient),
                new SteamAccountNameLocator(),
                new SteamCmdDepotDownloader(),
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
        base.OnExit(eventArgs);
    }
}
