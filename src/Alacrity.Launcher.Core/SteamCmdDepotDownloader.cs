using System.Diagnostics;

namespace Alacrity.Launcher.Core;

public sealed class SteamCmdDepotDownloader
{
    public async Task<string> DownloadAsync(DepotDownloadRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        string steamCmdDirectory = Path.GetDirectoryName(request.SteamCmdPath) ?? throw new InvalidOperationException("SteamCMD must have a containing directory.");
        string logPath = Path.Combine(steamCmdDirectory, "logs", "console_log.txt");
        long consoleLogLength = GetFileLength(logPath);
        int exitCode;
        using (Process process = StartSteamCmd(request)) {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            exitCode = process.ExitCode;
        }

        string depotDirectory = Path.Combine(steamCmdDirectory, "steamapps", "content", "app_105600", "depot_105601");
        bool producedTerraria = File.Exists(Path.Combine(depotDirectory, "Terraria.exe"));
        bool completedRequestedManifest = exitCode != 0 && ReportsCompletedManifest(ReadAppendedLog(logPath, consoleLogLength), request.ManifestId);
        if (producedTerraria && (exitCode == 0 || completedRequestedManifest)) {
            return depotDirectory;
        }

        if (exitCode != 0) {
            throw new InvalidOperationException($"SteamCMD exited with code {exitCode} while downloading Terraria {request.Version}. See {logPath} for Steam's diagnostic.");
        }

        throw new InvalidDataException("SteamCMD completed without producing Terraria.exe. Steam may have rejected this historical manifest or requested interactive sign-in.");
    }

    private static Process StartSteamCmd(DepotDownloadRequest request)
    {
        var process = new Process {
            StartInfo = new ProcessStartInfo {
                FileName = request.SteamCmdPath,
                Arguments = CreateArguments(request),
                WorkingDirectory = Path.GetDirectoryName(request.SteamCmdPath),
                UseShellExecute = true
            }
        };

        if (!process.Start()) {
            throw new InvalidOperationException("SteamCMD did not start.");
        }

        return process;
    }

    internal static string CreateArguments(DepotDownloadRequest request)
    {
        return "+@ShutdownOnFailedCommand 1"
            + " +@NoPromptForPassword 0"
            + " +login " + Quote(request.SteamAccountName)
            + " +download_depot 105600 105601 " + request.ManifestId
            + " +quit";
    }

    internal static bool ReportsCompletedManifest(string consoleLog, string manifestId)
    {
        if (string.IsNullOrWhiteSpace(consoleLog) || string.IsNullOrWhiteSpace(manifestId)) {
            return false;
        }

        return consoleLog.IndexOf("Depot download complete", StringComparison.OrdinalIgnoreCase) >= 0
            && consoleLog.IndexOf("(manifest " + manifestId + ")", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static long GetFileLength(string path)
    {
        try {
            return new FileInfo(path).Length;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
            return 0;
        }
    }

    private static string ReadAppendedLog(string path, long previousLength)
    {
        try {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan);
            stream.Position = stream.Length >= previousLength ? previousLength : 0;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
            return string.Empty;
        }
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", string.Empty, StringComparison.Ordinal) + "\"";
    }
}

public sealed class DepotDownloadRequest
{
    public required string Version { get; init; }

    public required string ManifestId { get; init; }

    public required string SteamCmdPath { get; init; }

    public required string SteamAccountName { get; init; }

    public void Validate()
    {
        if (!TerrariaVersionNumber.TryParse(Version, out _)) {
            throw new ArgumentException("A valid Terraria version is required.", nameof(Version));
        }

        if (string.IsNullOrWhiteSpace(ManifestId) || !ManifestId.All(char.IsAsciiDigit)) {
            throw new ArgumentException("A numeric Steam manifest id is required.", nameof(ManifestId));
        }

        if (!File.Exists(SteamCmdPath)) {
            throw new FileNotFoundException("SteamCMD was not found.", SteamCmdPath);
        }

        if (string.IsNullOrWhiteSpace(SteamAccountName)) {
            throw new ArgumentException("A Steam account name is required.", nameof(SteamAccountName));
        }

    }
}
