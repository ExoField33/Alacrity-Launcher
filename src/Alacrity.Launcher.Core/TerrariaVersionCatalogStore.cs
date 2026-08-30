using System.Text.Json;

namespace Alacrity.Launcher.Core;

public sealed class TerrariaVersionCatalogStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly LauncherPaths paths;

    public TerrariaVersionCatalogStore(LauncherPaths paths)
    {
        this.paths = paths;
    }

    public async Task<TerrariaVersionCatalog> LoadAsync(CancellationToken cancellationToken)
    {
        paths.EnsureDirectories();

        if (!File.Exists(paths.VersionCatalogPath)) {
            string templatePath = Path.Combine(paths.DataDirectory, "versions.template.json");
            if (File.Exists(templatePath)) {
                File.Copy(templatePath, paths.VersionCatalogPath);
            }

            if (File.Exists(paths.VersionCatalogPath)) {
                return await LoadAsync(cancellationToken).ConfigureAwait(false);
            }

            TerrariaVersionCatalog emptyCatalog = CreateTemplate();
            await SaveAsync(emptyCatalog, cancellationToken).ConfigureAwait(false);
            return emptyCatalog;
        }

        TerrariaVersionCatalog catalog = await LoadCatalogAsync(paths.VersionCatalogPath, "versions.json", cancellationToken).ConfigureAwait(false);
        TerrariaVersionCatalog? template = await TryLoadTemplateAsync(cancellationToken).ConfigureAwait(false);
        if (template is not null && MergeMissingTemplateEntries(catalog, template)) {
            await SaveAsync(catalog, cancellationToken).ConfigureAwait(false);
        }

        Validate(catalog);
        catalog.Versions.Sort(TerrariaVersionEntryComparer.Instance);
        return catalog;
    }

    public async Task SaveAsync(TerrariaVersionCatalog catalog, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        Validate(catalog);
        paths.EnsureDirectories();

        string temporaryPath = paths.VersionCatalogPath + ".tmp";
        await using (FileStream stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough)) {
            await JsonSerializer.SerializeAsync(stream, catalog, SerializerOptions, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        File.Move(temporaryPath, paths.VersionCatalogPath, true);
    }

    private static TerrariaVersionCatalog CreateTemplate()
    {
        return new TerrariaVersionCatalog();
    }

    internal static bool MergeMissingTemplateEntries(TerrariaVersionCatalog catalog, TerrariaVersionCatalog template)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(template);
        Validate(catalog);
        Validate(template);

        bool changed = false;
        foreach (TerrariaVersionEntry templateEntry in template.Versions) {
            TerrariaVersionEntry? existingEntry = catalog.Find(templateEntry.Version);
            if (existingEntry is not null) {
                if (!string.IsNullOrWhiteSpace(existingEntry.Url) || string.IsNullOrWhiteSpace(templateEntry.Url)) {
                    continue;
                }

                catalog.Upsert(new TerrariaVersionEntry {
                    Version = existingEntry.Version,
                    ManifestId = existingEntry.ManifestId,
                    Url = templateEntry.Url,
                    IsAutomaticallyDiscovered = existingEntry.IsAutomaticallyDiscovered
                });
                changed = true;
                continue;
            }

            catalog.Upsert(new TerrariaVersionEntry {
                Version = templateEntry.Version,
                ManifestId = templateEntry.ManifestId,
                Url = templateEntry.Url,
                IsAutomaticallyDiscovered = templateEntry.IsAutomaticallyDiscovered
            });
            changed = true;
        }

        return changed;
    }

    private async Task<TerrariaVersionCatalog?> TryLoadTemplateAsync(CancellationToken cancellationToken)
    {
        string templatePath = Path.Combine(paths.DataDirectory, "versions.template.json");
        return File.Exists(templatePath)
            ? await LoadCatalogAsync(templatePath, "versions.template.json", cancellationToken).ConfigureAwait(false)
            : null;
    }

    private static async Task<TerrariaVersionCatalog> LoadCatalogAsync(string path, string displayName, CancellationToken cancellationToken)
    {
        await using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        TerrariaVersionCatalog? catalog = await JsonSerializer.DeserializeAsync<TerrariaVersionCatalog>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
        if (catalog is null) {
            throw new InvalidDataException($"{displayName} did not contain a Terraria version catalog.");
        }

        Validate(catalog);
        return catalog;
    }

    private static void Validate(TerrariaVersionCatalog catalog)
    {
        if (catalog.SteamAppId != 105600 || catalog.WindowsDepotId != 105601) {
            throw new InvalidDataException("The launcher supports Terraria app 105600 and Windows depot 105601 only.");
        }

        var seenVersions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (TerrariaVersionEntry entry in catalog.Versions) {
            entry.Validate();
            if (!seenVersions.Add(entry.Version)) {
                throw new InvalidDataException($"versions.json contains Terraria {entry.Version} more than once.");
            }
        }
    }
}
