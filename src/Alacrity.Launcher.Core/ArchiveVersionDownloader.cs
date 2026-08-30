using System.IO.Compression;
using System.Net.Http;

namespace Alacrity.Launcher.Core;

public sealed class ArchiveVersionDownloader
{
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(30);

    private readonly HttpClient httpClient;

    public ArchiveVersionDownloader(HttpClient httpClient)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<string> DownloadAndExtractAsync(string version, string url, string stagingDirectory, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingDirectory);

        Uri archiveUri = NormalizeDownloadUri(url);
        Directory.CreateDirectory(stagingDirectory);

        string archivePath = Path.Combine(stagingDirectory, "download.zip");
        string extractionDirectory = Path.Combine(stagingDirectory, "content");
        try {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(DownloadTimeout);
            await DownloadAsync(archiveUri, archivePath, timeout.Token).ConfigureAwait(false);
            await ExtractAsync(archivePath, extractionDirectory, timeout.Token).ConfigureAwait(false);
            return FindTerrariaContentDirectory(extractionDirectory);
        }
        finally {
            TryDeleteFile(archivePath);
        }
    }

    internal static Uri NormalizeDownloadUri(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? source)
            || !string.Equals(source.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidDataException("A version URL must be an absolute HTTPS URL.");
        }

        if (!string.Equals(source.Host, "github.com", StringComparison.OrdinalIgnoreCase)) {
            return source;
        }

        const string blobMarker = "/blob/";
        string path = source.AbsolutePath;
        int markerIndex = path.IndexOf(blobMarker, StringComparison.Ordinal);
        if (markerIndex <= 0) {
            return source;
        }

        var builder = new UriBuilder(source) {
            Path = path.Substring(0, markerIndex) + "/raw/" + path.Substring(markerIndex + blobMarker.Length)
        };
        return builder.Uri;
    }

    internal static string FindTerrariaContentDirectory(string extractionDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extractionDirectory);

        string? contentDirectory = null;
        foreach (string candidate in Directory.EnumerateFiles(extractionDirectory, "*", SearchOption.AllDirectories)) {
            if (!string.Equals(Path.GetFileName(candidate), "Terraria.exe", StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            if (contentDirectory is not null) {
                throw new InvalidDataException("The downloaded archive contains more than one Terraria.exe.");
            }

            contentDirectory = Path.GetDirectoryName(candidate);
        }

        return contentDirectory ?? throw new InvalidDataException("The downloaded archive did not contain Terraria.exe.");
    }

    private async Task DownloadAsync(Uri archiveUri, string archivePath, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await httpClient.GetAsync(archiveUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var destination = new FileStream(archivePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await source.CopyToAsync(destination, 65536, cancellationToken).ConfigureAwait(false);
        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task ExtractAsync(string archivePath, string extractionDirectory, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(extractionDirectory);
        string extractionRoot = Path.GetFullPath(extractionDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        await using var archiveFile = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var archive = new ZipArchive(archiveFile, ZipArchiveMode.Read, leaveOpen: false);
        foreach (ZipArchiveEntry entry in archive.Entries) {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(entry.FullName)) {
                continue;
            }

            if (Path.IsPathRooted(entry.FullName)) {
                throw new InvalidDataException("The downloaded archive contains an unsafe absolute path.");
            }

            string destinationPath = Path.GetFullPath(Path.Combine(extractionRoot, entry.FullName));
            if (!destinationPath.StartsWith(extractionRoot, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidDataException("The downloaded archive contains a path outside its extraction directory.");
            }

            if (string.IsNullOrEmpty(entry.Name)) {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            string? parentDirectory = Path.GetDirectoryName(destinationPath);
            if (parentDirectory is null) {
                throw new InvalidDataException("The downloaded archive contains an invalid file path.");
            }

            Directory.CreateDirectory(parentDirectory);
            await using Stream source = entry.Open();
            await using var destination = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
            await source.CopyToAsync(destination, 65536, cancellationToken).ConfigureAwait(false);
            await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static void TryDeleteFile(string path)
    {
        try {
            if (File.Exists(path)) {
                File.Delete(path);
            }
        }
        catch (Exception) {
        }
    }
}
