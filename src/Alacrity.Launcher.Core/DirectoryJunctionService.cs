using System.Diagnostics;

namespace Alacrity.Launcher.Core;

public sealed class DirectoryJunctionService
{
    public async Task CreateAsync(string junctionPath, string targetPath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(junctionPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        if (!Directory.Exists(targetPath)) {
            throw new DirectoryNotFoundException($"The Terraria version directory '{targetPath}' does not exist.");
        }

        if (Directory.Exists(junctionPath) || File.Exists(junctionPath)) {
            throw new IOException($"The junction path '{junctionPath}' already exists.");
        }

        int exitCode = await RunCommandAsync($"mklink /J {Quote(junctionPath)} {Quote(targetPath)}", cancellationToken).ConfigureAwait(false);
        if (exitCode != 0 || !Directory.Exists(junctionPath)) {
            throw new IOException("Windows could not create the Terraria directory junction. Run the launcher with permission to modify the Steam library folder.");
        }
    }

    public async Task RemoveAsync(string junctionPath, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(junctionPath)) {
            return;
        }

        int exitCode = await RunCommandAsync($"rmdir {Quote(junctionPath)}", cancellationToken).ConfigureAwait(false);
        if (exitCode != 0 || Directory.Exists(junctionPath)) {
            throw new IOException("Windows could not remove the temporary Terraria directory junction.");
        }
    }

    private static async Task<int> RunCommandAsync(string command, CancellationToken cancellationToken)
    {
        using var process = new Process {
            StartInfo = new ProcessStartInfo {
                FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
                Arguments = "/d /s /c " + command,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return process.ExitCode;
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }
}
