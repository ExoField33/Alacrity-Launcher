using System.IO.Compression;
using System.Net.Http;

namespace Alacrity.Launcher.Core;

public sealed class DepotDownloaderProvisioner
{
    private static readonly Uri ArchiveUri = new Uri("https://github.com/SteamRE/DepotDownloader/releases/download/DepotDownloader_3.4.0/DepotDownloader-windows-x64.zip");

    private readonly LauncherPaths paths;
    private readonly HttpClient httpClient;

    public DepotDownloaderProvisioner(LauncherPaths paths, HttpClient httpClient)
    {
        this.paths = paths;
        this.httpClient = httpClient;
    }

    public async Task<string> EnsureAvailableAsync(CancellationToken cancellationToken)
    {
        paths.EnsureDirectories();
        string executablePath = Path.Combine(paths.DepotDownloaderDirectory, "DepotDownloader.exe");
        if (File.Exists(executablePath)) {
            return executablePath;
        }

        string stagingDirectory = paths.DepotDownloaderDirectory + ".staging-" + Guid.NewGuid().ToString("N");
        string archivePath = stagingDirectory + ".zip";
        Directory.CreateDirectory(stagingDirectory);

        try {
            await DownloadArchiveAsync(archivePath, cancellationToken).ConfigureAwait(false);
            ZipFile.ExtractToDirectory(archivePath, stagingDirectory);
            string stagedExecutablePath = Path.Combine(stagingDirectory, "DepotDownloader.exe");
            if (!File.Exists(stagedExecutablePath)) {
                throw new InvalidDataException("The official DepotDownloader archive did not contain DepotDownloader.exe.");
            }

            if (Directory.Exists(paths.DepotDownloaderDirectory)) {
                Directory.Delete(paths.DepotDownloaderDirectory, recursive: true);
            }

            Directory.Move(stagingDirectory, paths.DepotDownloaderDirectory);
            TryDeleteLegacySteamCmdDirectory();
            return executablePath;
        }
        finally {
            if (File.Exists(archivePath)) {
                File.Delete(archivePath);
            }

            if (Directory.Exists(stagingDirectory)) {
                Directory.Delete(stagingDirectory, recursive: true);
            }
        }
    }

    private async Task DownloadArchiveAsync(string archivePath, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await httpClient.GetAsync(ArchiveUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var destination = new FileStream(archivePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await source.CopyToAsync(destination, 65536, cancellationToken).ConfigureAwait(false);
        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private void TryDeleteLegacySteamCmdDirectory()
    {
        try {
            if (Directory.Exists(paths.LegacySteamCmdDirectory)) {
                Directory.Delete(paths.LegacySteamCmdDirectory, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
            // An active legacy download may still hold this cache; the next successful provision retries cleanup.
        }
    }
}
