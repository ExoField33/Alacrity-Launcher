using System.Diagnostics;

namespace Alacrity.Launcher.Core;

public sealed class DepotDownloaderManifestDownloader
{
    public async Task<string> DownloadAsync(DepotDownloadRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        Directory.CreateDirectory(request.OutputDirectory);
        using Process process = StartDepotDownloader(request);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        if (process.ExitCode != 0) {
            throw new InvalidOperationException($"DepotDownloader exited with code {process.ExitCode} while downloading Terraria {request.Version}.");
        }

        if (!File.Exists(Path.Combine(request.OutputDirectory, "Terraria.exe"))) {
            throw new InvalidDataException("DepotDownloader completed without producing Terraria.exe. Steam may have rejected this historical manifest or requested interactive sign-in.");
        }

        return request.OutputDirectory;
    }

    private static Process StartDepotDownloader(DepotDownloadRequest request)
    {
        var process = new Process {
            StartInfo = new ProcessStartInfo {
                FileName = request.DepotDownloaderPath,
                Arguments = CreateArguments(request),
                WorkingDirectory = Path.GetDirectoryName(request.DepotDownloaderPath),
                UseShellExecute = true
            }
        };

        if (!process.Start()) {
            throw new InvalidOperationException("DepotDownloader did not start.");
        }

        return process;
    }

    internal static string CreateArguments(DepotDownloadRequest request)
    {
        return "-app 105600"
            + " -depot 105601"
            + " -manifest " + request.ManifestId
            + " -username " + Quote(request.SteamAccountName)
            + " -remember-password"
            + " -loginid 105600"
            + " -dir " + Quote(request.OutputDirectory);
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

    public required string DepotDownloaderPath { get; init; }

    public required string OutputDirectory { get; init; }

    public required string SteamAccountName { get; init; }

    public void Validate()
    {
        if (!TerrariaVersionNumber.TryParse(Version, out _)) {
            throw new ArgumentException("A valid Terraria version is required.", nameof(Version));
        }

        if (string.IsNullOrWhiteSpace(ManifestId) || !ManifestId.All(char.IsAsciiDigit)) {
            throw new ArgumentException("A numeric Steam manifest id is required.", nameof(ManifestId));
        }

        if (!File.Exists(DepotDownloaderPath)) {
            throw new FileNotFoundException("DepotDownloader was not found.", DepotDownloaderPath);
        }

        if (string.IsNullOrWhiteSpace(OutputDirectory)) {
            throw new ArgumentException("A depot output directory is required.", nameof(OutputDirectory));
        }

        if (string.IsNullOrWhiteSpace(SteamAccountName)) {
            throw new ArgumentException("A Steam account name is required.", nameof(SteamAccountName));
        }
    }
}
