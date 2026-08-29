using Alacrity.Launcher.Core;
using Xunit;

namespace Alacrity.Launcher.Core.Tests;

public sealed class SteamManifestReaderTests
{
    [Fact]
    public void ReadsTerrariaWindowsDepotManifestFromAppManifest()
    {
        string root = Path.Combine(Path.GetTempPath(), "alacrity-launcher-tests", Guid.NewGuid().ToString("N"));
        try {
            Directory.CreateDirectory(root);
            string appManifestPath = Path.Combine(root, "appmanifest_105600.acf");
            File.WriteAllText(appManifestPath, "\"AppState\"\n{\n  \"InstalledDepots\"\n  {\n    \"105601\"\n    {\n      \"manifest\" \"2046220459945595868\"\n    }\n  }\n}");

            var installation = new SteamTerrariaInstallation(root, Path.Combine(root, "Terraria.exe"), null, appManifestPath);

            Assert.Equal("2046220459945595868", new SteamManifestReader().TryReadTerrariaManifestId(installation));
        }
        finally {
            if (Directory.Exists(root)) {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
