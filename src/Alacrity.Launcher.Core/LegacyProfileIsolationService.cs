namespace Alacrity.Launcher.Core;

public sealed class LegacyProfileIsolationService
{
    private static readonly string[] VersionSettingsFileNames = {
        "config.json",
        "favorites.json",
        "input profiles.json"
    };

    private static readonly string[] DirectoryNames = { "Players", "Worlds" };
    // config.dat belongs to older Terraria releases. Leaving it at the shared
    // root lets a newer release import its incompatible settings on next launch.
    private static readonly string[] LegacyFileNames = {
        "achievements.dat",
        "config.dat",
        "servers.dat"
    };

    public LegacyProfileSwapState CreateState(string currentVersion, string legacyVersion, string? terrariaDocumentsDirectory = null)
    {
        return CreateState(currentVersion, legacyVersion, includeLegacyProfileData: true, terrariaDocumentsDirectory: terrariaDocumentsDirectory);
    }

    public LegacyProfileSwapState CreateState(string currentVersion, string targetVersion, bool includeLegacyProfileData, string? terrariaDocumentsDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetVersion);

        string documentsDirectory = terrariaDocumentsDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Terraria");
        var state = new LegacyProfileSwapState {
            CurrentVersion = currentVersion,
            LegacyVersion = targetVersion,
            TerrariaDocumentsDirectory = documentsDirectory
        };

        foreach (string fileName in VersionSettingsFileNames) {
            AddPath(state, fileName);
        }

        if (includeLegacyProfileData) {
            foreach (string directoryName in DirectoryNames) {
                AddPath(state, directoryName);
            }

            foreach (string fileName in LegacyFileNames) {
                AddPath(state, fileName);
            }
        }

        return state;
    }

    public void Activate(LegacyProfileSwapState state, Action? checkpoint = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.IsActivated) {
            return;
        }

        Directory.CreateDirectory(state.TerrariaDocumentsDirectory);
        state.IsActivationInProgress = true;
        checkpoint?.Invoke();

        foreach (LegacyProfilePathState path in state.Paths) {
            if (!path.IsActivationStarted) {
                path.CurrentProfileExistedAtActivation = PathExists(path.DefaultPath);
                path.IsActivationStarted = true;
                checkpoint?.Invoke();
            }

            if (path.CurrentProfileExistedAtActivation
                && !PathExists(path.CurrentVersionPath)
                && PathExists(path.DefaultPath)) {
                Move(path.DefaultPath, path.CurrentVersionPath);
                checkpoint?.Invoke();
            }

            if (!path.LegacyProfileMovedToDefault && PathExists(path.LegacyVersionPath)) {
                if (PathExists(path.DefaultPath)) {
                    throw new IOException($"'{path.DefaultPath}' already exists while activating the legacy Terraria profile.");
                }

                Move(path.LegacyVersionPath, path.DefaultPath);
                checkpoint?.Invoke();
            }

            path.LegacyProfileMovedToDefault = true;
            checkpoint?.Invoke();
        }

        state.IsActivated = true;
        state.IsActivationInProgress = false;
        checkpoint?.Invoke();
    }

    public void Restore(LegacyProfileSwapState state, Action? checkpoint = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!state.IsActivated && !state.IsActivationInProgress) {
            return;
        }

        foreach (LegacyProfilePathState path in state.Paths) {
            if (!path.IsActivationStarted) {
                continue;
            }

            if (path.CurrentProfileExistedAtActivation && PathExists(path.CurrentVersionPath)) {
                if (PathExists(path.DefaultPath) && !PathExists(path.LegacyVersionPath)) {
                    Move(path.DefaultPath, path.LegacyVersionPath);
                    checkpoint?.Invoke();
                }

                if (PathExists(path.DefaultPath)) {
                    throw new IOException($"'{path.DefaultPath}' could not be restored because the versioned legacy profile still occupies that path.");
                }

                Move(path.CurrentVersionPath, path.DefaultPath);
                checkpoint?.Invoke();
            }
            else if (!path.CurrentProfileExistedAtActivation
                && PathExists(path.DefaultPath)
                && !PathExists(path.LegacyVersionPath)) {
                Move(path.DefaultPath, path.LegacyVersionPath);
                checkpoint?.Invoke();
            }
        }

        state.IsActivated = false;
        state.IsActivationInProgress = false;
        checkpoint?.Invoke();
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
