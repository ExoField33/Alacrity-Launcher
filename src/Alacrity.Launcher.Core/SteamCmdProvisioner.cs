using System.IO.Compression;
using System.Net.Http;

namespace Alacrity.Launcher.Core;

public sealed class SteamCmdProvisioner
{
    private static readonly Uri SteamCmdArchiveUri = new Uri("https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip");

    private readonly LauncherPaths paths;
    private readonly HttpClient httpClient;

    public SteamCmdProvisioner(LauncherPaths paths, HttpClient httpClient)
    {
        this.paths = paths;
        this.httpClient = httpClient;
    }

    public async Task<string> EnsureAvailableAsync(CancellationToken cancellationToken)
    {
        paths.EnsureDirectories();
        string executablePath = Path.Combine(paths.SteamCmdDirectory, "steamcmd.exe");
        if (File.Exists(executablePath)) {
            return executablePath;
        }

        string stagingDirectory = paths.SteamCmdDirectory + ".staging-" + Guid.NewGuid().ToString("N");
        string archivePath = stagingDirectory + ".zip";
        Directory.CreateDirectory(stagingDirectory);

        try {
            await DownloadArchiveAsync(archivePath, cancellationToken).ConfigureAwait(false);
            ZipFile.ExtractToDirectory(archivePath, stagingDirectory);
            string stagedExecutablePath = Path.Combine(stagingDirectory, "steamcmd.exe");
            if (!File.Exists(stagedExecutablePath)) {
                throw new InvalidDataException("The SteamCMD download did not contain steamcmd.exe.");
            }

            if (Directory.Exists(paths.SteamCmdDirectory)) {
                Directory.Delete(paths.SteamCmdDirectory, recursive: true);
            }

            Directory.Move(stagingDirectory, paths.SteamCmdDirectory);
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
        using HttpResponseMessage response = await httpClient.GetAsync(SteamCmdArchiveUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var destination = new FileStream(archivePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await source.CopyToAsync(destination, 65536, cancellationToken).ConfigureAwait(false);
        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
