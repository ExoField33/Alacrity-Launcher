using Alacrity.Launcher.Core;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Alacrity.Launcher;

public partial class MainWindow : Window
{
    private readonly LauncherViewModel viewModel;
    private readonly ChangelogSearchSession changelogSearch = new ChangelogSearchSession();

    public MainWindow(LauncherViewModel viewModel)
    {
        this.viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs eventArgs)
    {
        await RunOperationAsync(viewModel.InitializeAsync);
    }

    private async void Refresh_Click(object sender, RoutedEventArgs eventArgs)
    {
        await RunOperationAsync(viewModel.InitializeAsync);
    }

    private async void Download_Click(object sender, RoutedEventArgs eventArgs)
    {
        try {
            await viewModel.DownloadSelectedAsync(CancellationToken.None);
        }
        catch (SteamAccountNameRequiredException exception) {
            string? accountName = SteamAccountPromptWindow.Show(this, exception.Message);
            if (string.IsNullOrWhiteSpace(accountName)) {
                return;
            }

            await RunOperationAsync(async cancellationToken => {
                await viewModel.SetSteamAccountNameAsync(accountName, cancellationToken);
                await viewModel.DownloadSelectedAsync(cancellationToken);
            });
        }
        catch (Exception exception) {
            ReportException(exception);
        }
    }

    private async void Launch_Click(object sender, RoutedEventArgs eventArgs)
    {
        bool isolateLegacyProfile = false;
        if (viewModel.SelectedVersion is { IsLegacy: true }) {
            bool? selection = LegacyProfilePromptWindow.Show(this);
            if (!selection.HasValue) {
                return;
            }

            isolateLegacyProfile = selection.Value;
        }

        await RunOperationAsync(cancellationToken => viewModel.LaunchSelectedAsync(isolateLegacyProfile, cancellationToken));
    }

    private async Task RunOperationAsync(Func<CancellationToken, Task> operation)
    {
        try {
            await operation(CancellationToken.None);
        }
        catch (Exception exception) {
            ReportException(exception);
        }
    }

    private void ReportException(Exception exception)
    {
        viewModel.Status = exception.Message;
        MessageBox.Show(exception.Message, "Alacrity Launcher", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control) {
            ChangelogSearchPanel.Visibility = Visibility.Visible;
            ChangelogSearchBox.Focus();
            ChangelogSearchBox.SelectAll();
            eventArgs.Handled = true;
            return;
        }

        if (eventArgs.Key == Key.Escape && ChangelogSearchPanel.Visibility == Visibility.Visible) {
            CloseChangelogSearch();
            eventArgs.Handled = true;
        }
    }

    private void ChangelogSearchBox_TextChanged(object sender, TextChangedEventArgs eventArgs)
    {
        ShowChangelogSearchMatch(changelogSearch.UpdateQuery(ChangelogTextBox.Text, ChangelogSearchBox.Text));
    }

    private void ChangelogSearchBox_KeyDown(object sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key == Key.Enter) {
            bool previous = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            ShowChangelogSearchMatch(changelogSearch.Move(ChangelogTextBox.Text, previous));
            eventArgs.Handled = true;
        }
    }

    private void ChangelogSearchNext_Click(object sender, RoutedEventArgs eventArgs)
    {
        ShowChangelogSearchMatch(changelogSearch.Move(ChangelogTextBox.Text, previous: false));
    }

    private void ChangelogTextBox_TextChanged(object sender, TextChangedEventArgs eventArgs)
    {
        if (ChangelogSearchPanel.Visibility != Visibility.Visible) {
            return;
        }

        ShowChangelogSearchMatch(changelogSearch.UpdateQuery(ChangelogTextBox.Text, ChangelogSearchBox.Text));
    }

    private void ShowChangelogSearchMatch(ChangelogSearchMatch match)
    {
        if (!match.IsFound) {
            ChangelogSearchStatus.Text = string.IsNullOrEmpty(ChangelogSearchBox.Text) ? string.Empty : "Not found";
            return;
        }

        ChangelogSearchStatus.Text = match.Current + "/" + match.Total;
        ChangelogTextBox.Select(match.Start, match.Length);
        int line = ChangelogTextBox.GetLineIndexFromCharacterIndex(match.Start);
        if (line >= 0) {
            ChangelogTextBox.ScrollToLine(line);
        }
    }

    private void CloseChangelogSearch()
    {
        changelogSearch.Reset();
        ChangelogSearchBox.Clear();
        ChangelogSearchStatus.Text = string.Empty;
        ChangelogSearchPanel.Visibility = Visibility.Collapsed;
        ChangelogTextBox.Focus();
    }

}

public sealed class LauncherViewModel : INotifyPropertyChanged
{
    private readonly LauncherCoordinator coordinator;
    private readonly LauncherSettingsStore settingsStore;
    private readonly SemaphoreSlim operationGate = new SemaphoreSlim(1, 1);
    private LauncherSettings settings = new LauncherSettings();
    private LauncherVersionView? selectedVersion;
    private string status = "Starting launcher...";
    private bool isIdle = true;
    private string terrariaDirectory = string.Empty;

    public LauncherViewModel(LauncherCoordinator coordinator, LauncherSettingsStore settingsStore)
    {
        this.coordinator = coordinator;
        this.settingsStore = settingsStore;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<LauncherVersionView> Versions { get; } = new ObservableCollection<LauncherVersionView>();

    public LauncherVersionView? SelectedVersion
    {
        get => selectedVersion;
        set {
            if (Set(ref selectedVersion, value)) {
                RaisePropertyChanged(nameof(SelectedChangelog));
                RaisePropertyChanged(nameof(ChangelogHeading));
                RaisePropertyChanged(nameof(CanDownloadSelected));
                RaisePropertyChanged(nameof(CanLaunchSelected));
            }
        }
    }

    public string SelectedChangelog => SelectedVersion?.Changelog ?? "Select a version to read its changelog. Historical changelogs are taken from the installed current Terraria changelog.txt.";

    public string ChangelogHeading => SelectedVersion is null ? "Changelog" : "Changelog - " + SelectedVersion.Entry.Version;

    public string TerrariaDirectory
    {
        get => terrariaDirectory;
        set => Set(ref terrariaDirectory, value);
    }

    public string Status
    {
        get => status;
        set => Set(ref status, value);
    }

    public bool IsIdle
    {
        get => isIdle;
        private set {
            if (Set(ref isIdle, value)) {
                RaisePropertyChanged(nameof(CanDownloadSelected));
                RaisePropertyChanged(nameof(CanLaunchSelected));
            }
        }
    }

    public bool CanDownloadSelected => IsIdle && SelectedVersion is { CanDownload: true, IsInstalled: false };

    public bool CanLaunchSelected => IsIdle && SelectedVersion is { IsInstalled: true };

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await ExecuteExclusiveAsync(async () => {
            Status = "Checking Terraria and available versions...";
            LauncherStartupState state = await coordinator.InitializeAsync(cancellationToken);
            settings = state.Settings;
            TerrariaDirectory = settings.TerrariaDirectory ?? state.TerrariaInstallation?.TerrariaDirectory ?? string.Empty;

            string? previousVersion = SelectedVersion?.Entry.Version;
            Versions.Clear();
            foreach (LauncherVersionPresentation version in state.Versions) {
                Versions.Add(new LauncherVersionView(version));
            }

            SelectedVersion = Versions.FirstOrDefault(version => string.Equals(version.Entry.Version, previousVersion, StringComparison.OrdinalIgnoreCase))
                ?? Versions.FirstOrDefault();
            Status = state.Status;
        });
    }

    public async Task DownloadSelectedAsync(CancellationToken cancellationToken)
    {
        LauncherVersionView selected = SelectedVersion ?? throw new InvalidOperationException("Select a Terraria version first.");
        await ExecuteExclusiveAsync(async () => {
            Status = $"Preparing Terraria {selected.Entry.Version}...";
            LauncherSettings currentSettings = await SaveSettingsAsync(cancellationToken);
            await coordinator.DownloadVersionAsync(selected.Entry, currentSettings, cancellationToken);
            selected.IsInstalled = true;
            RaisePropertyChanged(nameof(CanDownloadSelected));
            RaisePropertyChanged(nameof(CanLaunchSelected));
            Status = $"Terraria {selected.Entry.Version} is ready to launch.";
        });
    }

    public async Task LaunchSelectedAsync(bool isolateLegacyProfile, CancellationToken cancellationToken)
    {
        LauncherVersionView selected = SelectedVersion ?? throw new InvalidOperationException("Select a Terraria version first.");
        await ExecuteExclusiveAsync(async () => {
            if (!selected.IsInstalled) {
                throw new InvalidOperationException($"Download Terraria {selected.Entry.Version} before launching it.");
            }

            LauncherSettings currentSettings = await SaveSettingsAsync(cancellationToken);
            Status = $"Running Terraria {selected.Entry.Version}...";
            await coordinator.LaunchAsync(selected.Entry, currentSettings, isolateLegacyProfile, cancellationToken);
            Status = $"Terraria {selected.Entry.Version} closed.";
        });
    }

    public async Task SetSteamAccountNameAsync(string accountName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(accountName)) {
            throw new ArgumentException("A Steam account name is required.", nameof(accountName));
        }

        settings = new LauncherSettings {
            TerrariaDirectory = string.IsNullOrWhiteSpace(TerrariaDirectory) ? null : TerrariaDirectory.Trim(),
            SteamAccountName = accountName.Trim()
        };
        await settingsStore.SaveAsync(settings, cancellationToken);
    }

    private async Task<LauncherSettings> SaveSettingsAsync(CancellationToken cancellationToken)
    {
        settings = new LauncherSettings {
            TerrariaDirectory = string.IsNullOrWhiteSpace(TerrariaDirectory) ? null : TerrariaDirectory.Trim(),
            SteamAccountName = settings.SteamAccountName
        };
        await settingsStore.SaveAsync(settings, cancellationToken);
        return settings;
    }

    private async Task ExecuteExclusiveAsync(Func<Task> operation)
    {
        await operationGate.WaitAsync();
        IsIdle = false;
        try {
            await operation();
        }
        finally {
            IsIdle = true;
            operationGate.Release();
        }
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) {
            return false;
        }

        field = value;
        RaisePropertyChanged(propertyName);
        return true;
    }

    private void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class LauncherVersionView : INotifyPropertyChanged
{
    private bool isInstalled;
    private readonly bool canPrepare;

    public LauncherVersionView(LauncherVersionPresentation presentation)
    {
        Entry = presentation.Entry;
        isInstalled = presentation.IsInstalled;
        canPrepare = presentation.CanPrepare;
        Changelog = presentation.Changelog;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public TerrariaVersionEntry Entry { get; }

    public string? Changelog { get; }

    public bool IsInstalled
    {
        get => isInstalled;
        set {
            if (isInstalled == value) {
                return;
            }

            isInstalled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsInstalled)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
        }
    }

    public bool CanDownload => canPrepare;

    public bool IsLegacy => LauncherCoordinator.IsLegacyVersion(Entry.Version);

    public string DisplayName => Entry.Version;

}
