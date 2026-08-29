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

            TerrariaVersionCatalog template = CreateTemplate();
            await SaveAsync(template, cancellationToken).ConfigureAwait(false);
            return template;
        }

        await using FileStream stream = new FileStream(paths.VersionCatalogPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        TerrariaVersionCatalog? catalog = await JsonSerializer.DeserializeAsync<TerrariaVersionCatalog>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
        if (catalog is null) {
            throw new InvalidDataException("versions.json did not contain a Terraria version catalog.");
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
