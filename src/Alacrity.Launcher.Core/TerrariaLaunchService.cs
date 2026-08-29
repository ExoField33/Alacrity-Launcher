using System.Diagnostics;

namespace Alacrity.Launcher.Core;

public sealed class TerrariaLaunchService
{
    private static readonly TerrariaVersionNumber SteamLaunchBoundary = ParseVersion("1.3");

    private readonly LaunchRecoveryJournal journal;
    private readonly DirectoryJunctionService junctions;
    private readonly LegacyProfileIsolationService legacyProfiles;

    public TerrariaLaunchService(LaunchRecoveryJournal journal, DirectoryJunctionService junctions, LegacyProfileIsolationService legacyProfiles)
    {
        this.journal = journal;
        this.junctions = junctions;
        this.legacyProfiles = legacyProfiles;
    }

    public async Task RecoverAsync(CancellationToken cancellationToken)
    {
        LaunchRecoveryState? state = journal.Read();
        if (state is null) {
            return;
        }

        if (IsGameStillRunning(state.TerrariaProcessId)) {
            throw new InvalidOperationException("Terraria is still running from an interrupted launcher session. Close it before recovery.");
        }

        await RestoreAsync(state, cancellationToken).ConfigureAwait(false);
    }

    public async Task LaunchAsync(TerrariaLaunchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);
        await RecoverAsync(cancellationToken).ConfigureAwait(false);
        EnsureTerrariaIsNotAlreadyRunning();

        string backupDirectory = request.TerrariaInstallation.TerrariaDirectory + ".alacrity-launcher-backup";
        if (Directory.Exists(backupDirectory)) {
            throw new IOException($"The stale Terraria backup directory '{backupDirectory}' must be recovered before launch.");
        }

        var state = new LaunchRecoveryState {
            SelectedVersion = request.Version,
            TerrariaDirectory = request.TerrariaInstallation.TerrariaDirectory,
            BackupTerrariaDirectory = backupDirectory,
            VersionDirectory = request.VersionDirectory,
            LegacyProfileSwap = request.IsolateLegacyProfile ? legacyProfiles.CreateState(request.CurrentVersion, request.Version) : null
        };

        journal.Write(state);
        try {
            if (state.LegacyProfileSwap is not null) {
                state.LegacyProfileSwap.IsActivated = true;
                journal.Write(state);
                legacyProfiles.Activate(state.LegacyProfileSwap);
                journal.Write(state);
            }

            Directory.Move(state.TerrariaDirectory, state.BackupTerrariaDirectory);
            state.TerrariaDirectoryMoved = true;
            journal.Write(state);

            await junctions.CreateAsync(state.TerrariaDirectory, state.VersionDirectory, cancellationToken).ConfigureAwait(false);
            state.JunctionCreated = true;
            journal.Write(state);

            using Process terraria = await StartTerrariaAsync(request, state.TerrariaDirectory, cancellationToken).ConfigureAwait(false);
            state.TerrariaProcessId = terraria.Id;
            journal.Write(state);

            await terraria.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            await RestoreAsync(state, CancellationToken.None).ConfigureAwait(false);
        }
        catch {
            if (!IsGameStillRunning(state.TerrariaProcessId)) {
                await RestoreAsync(state, CancellationToken.None).ConfigureAwait(false);
            }

            throw;
        }
    }

    private async Task RestoreAsync(LaunchRecoveryState state, CancellationToken cancellationToken)
    {
        bool isJunction = IsDirectoryJunction(state.TerrariaDirectory);
        if (state.JunctionCreated && Directory.Exists(state.TerrariaDirectory) && !isJunction) {
            throw new IOException("Launcher recovery found a normal Terraria directory where its temporary junction should be. It will not delete it automatically.");
        }

        if (isJunction) {
            await junctions.RemoveAsync(state.TerrariaDirectory, cancellationToken).ConfigureAwait(false);
            state.JunctionCreated = false;
            journal.Write(state);
        }

        if (Directory.Exists(state.BackupTerrariaDirectory)) {
            if (Directory.Exists(state.TerrariaDirectory)) {
                throw new IOException("Launcher recovery found both the original Terraria directory and its backup. It will not choose one automatically.");
            }

            Directory.Move(state.BackupTerrariaDirectory, state.TerrariaDirectory);
            state.TerrariaDirectoryMoved = false;
            journal.Write(state);
        }

        if (state.LegacyProfileSwap is { IsActivated: true } profileSwap) {
            legacyProfiles.Restore(profileSwap);
            journal.Write(state);
        }

        journal.Delete();
    }

    private static Process StartTerraria(string terrariaDirectory)
    {
        string executablePath = Path.Combine(terrariaDirectory, "Terraria.exe");
        var process = new Process {
            StartInfo = new ProcessStartInfo {
                FileName = executablePath,
                WorkingDirectory = terrariaDirectory,
                UseShellExecute = true
            }
        };

        if (!process.Start()) {
            throw new InvalidOperationException("Terraria did not start.");
        }

        return process;
    }

    private static async Task<Process> StartTerrariaAsync(TerrariaLaunchRequest request, string terrariaDirectory, CancellationToken cancellationToken)
    {
        if (!RequiresSteamLaunch(request.Version)) {
            return StartTerraria(terrariaDirectory);
        }

        DateTime launchStartUtc = DateTime.UtcNow;
        StartTerrariaThroughSteam(request.TerrariaInstallation);
        return await WaitForSteamLaunchedTerrariaAsync(launchStartUtc, cancellationToken).ConfigureAwait(false);
    }

    internal static bool RequiresSteamLaunch(string version)
    {
        return TerrariaVersionNumber.TryParse(version, out TerrariaVersionNumber parsed)
            && parsed.CompareTo(SteamLaunchBoundary) < 0;
    }

    private static void StartTerrariaThroughSteam(SteamTerrariaInstallation installation)
    {
        string? steamExecutablePath = installation.SteamExecutablePath;
        ProcessStartInfo startInfo;
        if (!string.IsNullOrWhiteSpace(steamExecutablePath) && File.Exists(steamExecutablePath)) {
            startInfo = new ProcessStartInfo {
                FileName = steamExecutablePath,
                Arguments = "-applaunch 105600",
                WorkingDirectory = Path.GetDirectoryName(steamExecutablePath),
                UseShellExecute = true
            };
        }
        else {
            startInfo = new ProcessStartInfo {
                FileName = "steam://rungameid/105600",
                UseShellExecute = true
            };
        }

        using Process? process = Process.Start(startInfo);
    }

    private static async Task<Process> WaitForSteamLaunchedTerrariaAsync(DateTime launchStartUtc, CancellationToken cancellationToken)
    {
        DateTime deadlineUtc = launchStartUtc.AddSeconds(60);
        while (DateTime.UtcNow < deadlineUtc) {
            Process? terraria = TryFindTerrariaStartedAfter(launchStartUtc);
            if (terraria is not null) {
                return terraria;
            }

            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException("Steam did not start Terraria within one minute. The original Steam installation was restored.");
    }

    private static void EnsureTerrariaIsNotAlreadyRunning()
    {
        Process? existing = TryFindAnyTerrariaProcess();
        if (existing is null) {
            return;
        }

        using (existing) {
            throw new InvalidOperationException("Terraria is already running. Close it before launching another version.");
        }
    }

    private static Process? TryFindTerrariaStartedAfter(DateTime startUtc)
    {
        foreach (Process candidate in Process.GetProcessesByName("Terraria")) {
            try {
                if (candidate.StartTime.ToUniversalTime() >= startUtc) {
                    return candidate;
                }
            }
            catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception) {
            }

            candidate.Dispose();
        }

        return null;
    }

    private static Process? TryFindAnyTerrariaProcess()
    {
        Process[] candidates = Process.GetProcessesByName("Terraria");
        if (candidates.Length == 0) {
            return null;
        }

        Process first = candidates[0];
        for (int index = 1; index < candidates.Length; index++) {
            candidates[index].Dispose();
        }

        return first;
    }

    private static TerrariaVersionNumber ParseVersion(string version)
    {
        if (!TerrariaVersionNumber.TryParse(version, out TerrariaVersionNumber parsed)) {
            throw new InvalidOperationException("The launcher Steam launch boundary is invalid.");
        }

        return parsed;
    }

    private static bool IsDirectoryJunction(string path)
    {
        if (!Directory.Exists(path)) {
            return false;
        }

        return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
    }

    private static bool IsGameStillRunning(int? processId)
    {
        if (!processId.HasValue) {
            return false;
        }

        try {
            using Process process = Process.GetProcessById(processId.Value);
            return !process.HasExited;
        }
        catch (ArgumentException) {
            return false;
        }
    }

    private static void ValidateRequest(TerrariaLaunchRequest request)
    {
        if (!File.Exists(Path.Combine(request.VersionDirectory, "Terraria.exe"))) {
            throw new FileNotFoundException($"Terraria {request.Version} has not been downloaded.", Path.Combine(request.VersionDirectory, "Terraria.exe"));
        }

        if (!TerrariaVersionNumber.TryParse(request.Version, out _)) {
            throw new ArgumentException("The selected Terraria version is invalid.", nameof(request));
        }

        if (!TerrariaVersionNumber.TryParse(request.CurrentVersion, out _)) {
            throw new ArgumentException("The current Terraria version is invalid.", nameof(request));
        }
    }
}

public sealed class TerrariaLaunchRequest
{
    public required SteamTerrariaInstallation TerrariaInstallation { get; init; }

    public required string Version { get; init; }

    public required string CurrentVersion { get; init; }

    public required string VersionDirectory { get; init; }

    public bool IsolateLegacyProfile { get; init; }
}
