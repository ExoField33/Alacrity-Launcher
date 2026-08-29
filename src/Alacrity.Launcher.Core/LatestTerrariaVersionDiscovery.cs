using System.Text.Json;
using System.Text.RegularExpressions;

namespace Alacrity.Launcher.Core;

public sealed class LatestTerrariaVersionDiscovery
{
    public const string DedicatedServerNamesEndpoint = "https://terraria.org/api/get/dedicated-servers-names";

    private static readonly Regex ArchivePattern = new Regex(
        @"^terraria-server-(?<digits>\d+)\.zip$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly HttpClient httpClient;

    public LatestTerrariaVersionDiscovery(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<LatestTerrariaVersion?> TryDiscoverAsync(CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await httpClient.GetAsync(DedicatedServerNamesEndpoint, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        string[]? names = await JsonSerializer.DeserializeAsync<string[]>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (names is null || names.Length == 0) {
            return null;
        }

        return SelectLatest(names);
    }

    internal static LatestTerrariaVersion? SelectLatest(IEnumerable<string> archiveNames)
    {
        ArgumentNullException.ThrowIfNull(archiveNames);

        LatestTerrariaVersion? latest = null;
        TerrariaVersionNumber latestVersion = default;
        foreach (string archiveName in archiveNames) {
            if (string.IsNullOrWhiteSpace(archiveName)
                || !TryParseServerArchive(archiveName, out string version)
                || !TerrariaVersionNumber.TryParse(version, out TerrariaVersionNumber parsed)) {
                continue;
            }

            if (latest is null || parsed.CompareTo(latestVersion) > 0) {
                latest = new LatestTerrariaVersion(version, archiveName);
                latestVersion = parsed;
            }
        }

        return latest;
    }

    public static bool TryParseServerArchive(string archiveName, out string version)
    {
        version = string.Empty;
        if (string.IsNullOrWhiteSpace(archiveName)) {
            return false;
        }

        Match match = ArchivePattern.Match(archiveName);
        if (!match.Success) {
            return false;
        }

        string digits = match.Groups["digits"].Value;
        // Terraria currently publishes an unseparated 1.<minor>.<patch>[.<build>]
        // form. The first three components are single digits; only the optional
        // build suffix may contain two digits. Longer compact forms are ambiguous
        // without separators, so discovery deliberately ignores them.
        if (digits.Length < 3 || digits.Length > 5 || digits[0] != '1') {
            return false;
        }

        if (digits.Length == 3) {
            version = $"1.{digits[1]}.{digits[2]}";
            return true;
        }

        version = $"1.{digits[1]}.{digits[2]}.{digits.Substring(3)}";
        return TerrariaVersionNumber.TryParse(version, out _);
    }
}

public sealed record LatestTerrariaVersion(string Version, string ServerArchiveName);
