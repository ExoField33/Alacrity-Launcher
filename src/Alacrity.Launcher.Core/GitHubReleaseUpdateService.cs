using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace Alacrity.Launcher.Core;

public sealed class GitHubReleaseUpdateService
{
    private const string LatestReleaseEndpoint = "https://api.github.com/repos/ExoField33/Alacrity-Launcher/releases/latest";
    private const string ReleaseAssetName = "Alacrity-Launcher-win-x64.zip";
    private const string LauncherExecutableName = "Alacrity Launcher.exe";
    private const string VersionTemplateRelativePath = "data/versions.template.json";

    private readonly HttpClient httpClient;

    public GitHubReleaseUpdateService(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<LauncherUpdateInfo?> CheckAsync(Version currentVersion, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseEndpoint);
        using HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) {
            return null;
        }

        response.EnsureSuccessStatusCode();
        string document = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        LauncherUpdateInfo? release = ParseLatestRelease(document);
        return release is not null && release.Version.CompareTo(currentVersion) > 0 ? release : null;
    }

    public async Task<LauncherUpdatePayload> DownloadAsync(LauncherUpdateInfo update, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);
        string stagingDirectory = Path.Combine(Path.GetTempPath(), "AlacrityLauncherUpdate-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingDirectory);

        try {
            string archivePath = Path.Combine(stagingDirectory, "update.zip");
            using HttpResponseMessage response = await httpClient.GetAsync(update.DownloadUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using (Stream source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false)) {
                await using var destination = new FileStream(archivePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
                await source.CopyToAsync(destination, 65536, cancellationToken).ConfigureAwait(false);
            }

            string payloadDirectory = Path.Combine(stagingDirectory, "payload");
            Directory.CreateDirectory(payloadDirectory);
            using ZipArchive archive = ZipFile.OpenRead(archivePath);
            ExtractRequiredEntry(archive, LauncherExecutableName, Path.Combine(payloadDirectory, LauncherExecutableName));
            ExtractRequiredEntry(archive, VersionTemplateRelativePath, Path.Combine(payloadDirectory, "data", "versions.template.json"));
            File.Delete(archivePath);
            return new LauncherUpdatePayload(payloadDirectory, update.Version);
        }
        catch {
            TryDeleteDirectory(stagingDirectory);
            throw;
        }
    }

    public void ScheduleApplyAfterExit(LauncherUpdatePayload payload, string launcherDirectory, int processId)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(launcherDirectory);
        if (!File.Exists(Path.Combine(payload.PayloadDirectory, LauncherExecutableName))) {
            throw new FileNotFoundException("The downloaded update does not contain the launcher executable.");
        }

        string versionTemplatePath = Path.Combine(payload.PayloadDirectory, "data", "versions.template.json");
        if (!File.Exists(versionTemplatePath)) {
            throw new FileNotFoundException("The downloaded update does not contain the version catalog template.");
        }

        string scriptPath = Path.Combine(Path.GetDirectoryName(payload.PayloadDirectory)!, "apply-update.cmd");
        File.WriteAllText(scriptPath, CreateApplyScript(launcherDirectory, payload.PayloadDirectory, processId), Encoding.ASCII);
        var process = new Process {
            StartInfo = new ProcessStartInfo {
                FileName = scriptPath,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            }
        };

        if (!process.Start()) {
            process.Dispose();
            throw new InvalidOperationException("The launcher update process did not start.");
        }

        process.Dispose();
    }

    internal static LauncherUpdateInfo? ParseLatestRelease(string document)
    {
        using JsonDocument json = JsonDocument.Parse(document);
        JsonElement root = json.RootElement;
        if (!root.TryGetProperty("tag_name", out JsonElement tagElement) || tagElement.GetString() is not string tagName) {
            return null;
        }

        string versionText = tagName.Trim().TrimStart('v', 'V');
        if (!Version.TryParse(versionText, out Version? version)) {
            return null;
        }

        if (!root.TryGetProperty("assets", out JsonElement assets) || assets.ValueKind != JsonValueKind.Array) {
            return null;
        }

        foreach (JsonElement asset in assets.EnumerateArray()) {
            if (!asset.TryGetProperty("name", out JsonElement nameElement)
                || !string.Equals(nameElement.GetString(), ReleaseAssetName, StringComparison.Ordinal)
                || !asset.TryGetProperty("browser_download_url", out JsonElement downloadElement)
                || !Uri.TryCreate(downloadElement.GetString(), UriKind.Absolute, out Uri? downloadUri)
                || downloadUri.Scheme != Uri.UriSchemeHttps) {
                continue;
            }

            return new LauncherUpdateInfo(version, downloadUri);
        }

        return null;
    }

    private static void ExtractRequiredEntry(ZipArchive archive, string entryPath, string destinationPath)
    {
        ZipArchiveEntry? entry = archive.GetEntry(entryPath);
        if (entry is null || entry.Length == 0) {
            throw new InvalidDataException($"The update archive does not contain {entryPath}.");
        }

        string? directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(directory)) {
            Directory.CreateDirectory(directory);
        }

        entry.ExtractToFile(destinationPath, overwrite: false);
    }

    private static string CreateApplyScript(string launcherDirectory, string payloadDirectory, int processId)
    {
        return "@echo off\r\n"
            + "setlocal\r\n"
            + "set \"PID=" + processId + "\"\r\n"
            + "set \"TARGET=" + EscapeBatchValue(launcherDirectory) + "\"\r\n"
            + "set \"PAYLOAD=" + EscapeBatchValue(payloadDirectory) + "\"\r\n"
            + ":wait\r\n"
            + "tasklist /FI \"PID eq %PID%\" /NH | find \"%PID%\" >nul\r\n"
            + "if not errorlevel 1 (\r\n"
            + "  timeout /t 1 /nobreak >nul\r\n"
            + "  goto wait\r\n"
            + ")\r\n"
            + "for /L %%I in (1,1,10) do (\r\n"
            + "  copy /y \"%PAYLOAD%\\Alacrity Launcher.exe\" \"%TARGET%\\Alacrity Launcher.exe\" >nul && goto copied\r\n"
            + "  timeout /t 1 /nobreak >nul\r\n"
            + ")\r\n"
            + "exit /b 1\r\n"
            + ":copied\r\n"
            + "if not exist \"%TARGET%\\data\" mkdir \"%TARGET%\\data\"\r\n"
            + "copy /y \"%PAYLOAD%\\data\\versions.template.json\" \"%TARGET%\\data\\versions.template.json\" >nul\r\n"
            + "start \"\" \"%TARGET%\\Alacrity Launcher.exe\"\r\n"
            + "rmdir /s /q \"%PAYLOAD%\"\r\n"
            + "del \"%~f0\"\r\n";
    }

    private static string EscapeBatchValue(string value)
    {
        return value.Replace("\"", string.Empty, StringComparison.Ordinal);
    }

    private static void TryDeleteDirectory(string path)
    {
        try {
            if (Directory.Exists(path)) {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception) {
        }
    }
}

public sealed record LauncherUpdateInfo(Version Version, Uri DownloadUri);

public sealed record LauncherUpdatePayload(string PayloadDirectory, Version Version);
