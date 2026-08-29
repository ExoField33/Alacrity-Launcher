using System.Text.Json;

namespace Alacrity.Launcher.Core;

public sealed class LaunchRecoveryJournal
{
    private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly LauncherPaths paths;

    public LaunchRecoveryJournal(LauncherPaths paths)
    {
        this.paths = paths;
    }

    public LaunchRecoveryState? Read()
    {
        if (!File.Exists(paths.RecoveryJournalPath)) {
            return null;
        }

        using FileStream stream = new FileStream(paths.RecoveryJournalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return JsonSerializer.Deserialize<LaunchRecoveryState>(stream, SerializerOptions);
    }

    public void Write(LaunchRecoveryState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        paths.EnsureDirectories();

        string temporaryPath = paths.RecoveryJournalPath + ".tmp";
        using (FileStream stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough)) {
            JsonSerializer.Serialize(stream, state, SerializerOptions);
            stream.Flush(flushToDisk: true);
        }

        File.Move(temporaryPath, paths.RecoveryJournalPath, true);
    }

    public void Delete()
    {
        if (File.Exists(paths.RecoveryJournalPath)) {
            File.Delete(paths.RecoveryJournalPath);
        }
    }
}

public sealed class LaunchRecoveryState
{
    public required string SelectedVersion { get; init; }

    public required string TerrariaDirectory { get; init; }

    public required string BackupTerrariaDirectory { get; init; }

    public required string VersionDirectory { get; init; }

    public bool TerrariaDirectoryMoved { get; set; }

    public bool JunctionCreated { get; set; }

    public int? TerrariaProcessId { get; set; }

    public LegacyProfileSwapState? LegacyProfileSwap { get; set; }
}

public sealed class LegacyProfileSwapState
{
    public required string CurrentVersion { get; init; }

    public required string LegacyVersion { get; init; }

    public required string TerrariaDocumentsDirectory { get; init; }

    public bool IsActivated { get; set; }

    public List<LegacyProfilePathState> Paths { get; init; } = new List<LegacyProfilePathState>();
}

public sealed class LegacyProfilePathState
{
    public required string DefaultPath { get; init; }

    public required string CurrentVersionPath { get; init; }

    public required string LegacyVersionPath { get; init; }

}
