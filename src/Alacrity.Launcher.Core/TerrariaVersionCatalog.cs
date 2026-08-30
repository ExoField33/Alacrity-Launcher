using System.Text.Json.Serialization;

namespace Alacrity.Launcher.Core;

public sealed class TerrariaVersionCatalog
{
    public int SteamAppId { get; init; } = 105600;

    public int WindowsDepotId { get; init; } = 105601;

    public List<TerrariaVersionEntry> Versions { get; init; } = new List<TerrariaVersionEntry>();

    public TerrariaVersionEntry? Find(string version)
    {
        return Versions.FirstOrDefault(entry => string.Equals(entry.Version, version, StringComparison.OrdinalIgnoreCase));
    }

    public void Upsert(TerrariaVersionEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        entry.Validate();

        int index = Versions.FindIndex(existing => string.Equals(existing.Version, entry.Version, StringComparison.OrdinalIgnoreCase));
        if (index >= 0) {
            Versions[index] = entry;
        }
        else {
            Versions.Add(entry);
        }

        Versions.Sort(TerrariaVersionEntryComparer.Instance);
    }
}

public sealed class TerrariaVersionEntry
{
    public required string Version { get; init; }

    public string? ManifestId { get; init; }

    /// <summary>
    /// Optional HTTPS ZIP source used when this historical version is not fetched from Steam.
    /// URL sources take precedence over depot manifests when both are present.
    /// </summary>
    public string? Url { get; init; }

    public bool IsAutomaticallyDiscovered { get; init; }

    [JsonIgnore]
    public bool CanDownload => !string.IsNullOrWhiteSpace(Url) || !string.IsNullOrWhiteSpace(ManifestId);

    public void Validate()
    {
        if (!TerrariaVersionNumber.TryParse(Version, out _)) {
            throw new InvalidDataException($"'{Version}' is not a valid Terraria version.");
        }

        if (!string.IsNullOrWhiteSpace(ManifestId) && !ManifestId.All(char.IsAsciiDigit)) {
            throw new InvalidDataException($"The manifest id for Terraria {Version} must contain digits only.");
        }

        if (!string.IsNullOrWhiteSpace(Url)
            && (!Uri.TryCreate(Url, UriKind.Absolute, out Uri? archiveUri)
                || !string.Equals(archiveUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))) {
            throw new InvalidDataException($"The URL for Terraria {Version} must be an absolute HTTPS URL.");
        }
    }
}

internal sealed class TerrariaVersionEntryComparer : IComparer<TerrariaVersionEntry>
{
    public static readonly TerrariaVersionEntryComparer Instance = new TerrariaVersionEntryComparer();

    public int Compare(TerrariaVersionEntry? left, TerrariaVersionEntry? right)
    {
        if (ReferenceEquals(left, right)) {
            return 0;
        }

        if (left is null) {
            return -1;
        }

        if (right is null) {
            return 1;
        }

        _ = TerrariaVersionNumber.TryParse(left.Version, out TerrariaVersionNumber leftVersion);
        _ = TerrariaVersionNumber.TryParse(right.Version, out TerrariaVersionNumber rightVersion);
        return rightVersion.CompareTo(leftVersion);
    }
}
