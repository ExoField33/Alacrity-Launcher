namespace Alacrity.Launcher.Core;

public sealed class LegacyProfileIsolationService
{
    private static readonly string[] DirectoryNames = { "Players", "Worlds" };
    private static readonly string[] FileNames = { "achievements.dat", "config.json", "favorites.json" };

    public LegacyProfileSwapState CreateState(string currentVersion, string legacyVersion, string? terrariaDocumentsDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(legacyVersion);

        string documentsDirectory = terrariaDocumentsDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Terraria");
        var state = new LegacyProfileSwapState {
            CurrentVersion = currentVersion,
            LegacyVersion = legacyVersion,
            TerrariaDocumentsDirectory = documentsDirectory
        };

        foreach (string directoryName in DirectoryNames) {
            AddPath(state, directoryName);
        }

        foreach (string fileName in FileNames) {
            AddPath(state, fileName);
        }

        return state;
    }

    public void Activate(LegacyProfileSwapState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.IsActivated) {
            return;
        }

        Directory.CreateDirectory(state.TerrariaDocumentsDirectory);

        state.IsActivated = true;

        foreach (LegacyProfilePathState path in state.Paths) {
            EnsureNoInterruptedPathConflict(path);

            if (PathExists(path.DefaultPath)) {
                Move(path.DefaultPath, path.CurrentVersionPath);
            }

            if (PathExists(path.LegacyVersionPath)) {
                Move(path.LegacyVersionPath, path.DefaultPath);
            }
        }

    }

    public void Restore(LegacyProfileSwapState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!state.IsActivated) {
            return;
        }

        foreach (LegacyProfilePathState path in state.Paths) {
            if (PathExists(path.CurrentVersionPath)) {
                if (PathExists(path.DefaultPath) && !PathExists(path.LegacyVersionPath)) {
                    Move(path.DefaultPath, path.LegacyVersionPath);
                }

                Move(path.CurrentVersionPath, path.DefaultPath);
            }
        }

        state.IsActivated = false;
    }

    private static void AddPath(LegacyProfileSwapState state, string fileName)
    {
        string defaultPath = Path.Combine(state.TerrariaDocumentsDirectory, fileName);
        state.Paths.Add(new LegacyProfilePathState {
            DefaultPath = defaultPath,
            CurrentVersionPath = defaultPath + " " + state.CurrentVersion,
            LegacyVersionPath = defaultPath + " " + state.LegacyVersion
        });
    }

    private static void EnsureNoInterruptedPathConflict(LegacyProfilePathState path)
    {
        if (PathExists(path.CurrentVersionPath)) {
            throw new IOException($"'{path.CurrentVersionPath}' already exists. Run launcher recovery before starting another legacy Terraria version.");
        }
    }

    private static bool PathExists(string path)
    {
        return File.Exists(path) || Directory.Exists(path);
    }

    private static void Move(string sourcePath, string destinationPath)
    {
        if (Directory.Exists(sourcePath)) {
            Directory.Move(sourcePath, destinationPath);
        }
        else {
            File.Move(sourcePath, destinationPath);
        }
    }
}
