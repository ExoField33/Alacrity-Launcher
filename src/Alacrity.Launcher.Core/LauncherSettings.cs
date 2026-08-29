using System.Text.Json;

namespace Alacrity.Launcher.Core;

public sealed class LauncherSettings
{
    public string? TerrariaDirectory { get; init; }

    public string? SteamAccountName { get; init; }
}

public sealed class LauncherSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly LauncherPaths paths;

    public LauncherSettingsStore(LauncherPaths paths)
    {
        this.paths = paths;
    }

    public async Task<LauncherSettings> LoadAsync(CancellationToken cancellationToken)
    {
        paths.EnsureDirectories();
        if (!File.Exists(paths.SettingsPath)) {
            return new LauncherSettings();
        }

        await using FileStream stream = new FileStream(paths.SettingsPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return await JsonSerializer.DeserializeAsync<LauncherSettings>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false) ?? new LauncherSettings();
    }

    public async Task SaveAsync(LauncherSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        paths.EnsureDirectories();

        string temporaryPath = paths.SettingsPath + ".tmp";
        await using (FileStream stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough)) {
            await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        File.Move(temporaryPath, paths.SettingsPath, true);
    }
}
