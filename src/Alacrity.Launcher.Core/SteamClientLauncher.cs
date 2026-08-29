using System.Diagnostics;

namespace Alacrity.Launcher.Core;

public sealed class SteamClientLauncher
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(15);

    public async Task EnsureRunningAsync(SteamTerrariaInstallation installation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(installation);
        if (IsSteamRunning()) {
            return;
        }

        if (string.IsNullOrWhiteSpace(installation.SteamExecutablePath) || !File.Exists(installation.SteamExecutablePath)) {
            throw new FileNotFoundException("Steam is not running and Steam.exe could not be located. Start Steam manually, then try again.", installation.SteamExecutablePath);
        }

        using Process steam = new Process {
            StartInfo = new ProcessStartInfo {
                FileName = installation.SteamExecutablePath,
                WorkingDirectory = Path.GetDirectoryName(installation.SteamExecutablePath),
                UseShellExecute = true
            }
        };

        if (!steam.Start()) {
            throw new InvalidOperationException("Steam did not start. Start Steam manually, then try again.");
        }

        DateTime deadlineUtc = DateTime.UtcNow + StartupTimeout;
        while (DateTime.UtcNow < deadlineUtc) {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsSteamRunning()) {
                return;
            }

            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException("Steam did not become available within 15 seconds. Start Steam manually, then try again.");
    }

    private static bool IsSteamRunning()
    {
        foreach (Process process in Process.GetProcessesByName("steam")) {
            using (process) {
                try {
                    if (!process.HasExited) {
                        return true;
                    }
                }
                catch (InvalidOperationException) {
                }
            }
        }

        return false;
    }
}
