using Alacrity.Launcher.Core;
using Xunit;

namespace Alacrity.Launcher.Core.Tests;

public sealed class SteamCmdDepotDownloaderTests
{
    [Fact]
    public void UsesInteractiveSteamLoginWithoutPuttingCredentialsInAFile()
    {
        var request = new DepotDownloadRequest {
            Version = "1.4.5.8.1",
            ManifestId = "2046220459945595868",
            SteamCmdPath = @"C:\launcher\Tools\SteamCMD\steamcmd.exe",
            SteamAccountName = "example-user"
        };

        string arguments = SteamCmdDepotDownloader.CreateArguments(request);

        Assert.Contains("+login \"example-user\"", arguments, StringComparison.Ordinal);
        Assert.DoesNotContain("+login \"example-user\" \"", arguments, StringComparison.Ordinal);
        Assert.DoesNotContain("runscript", arguments, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("+download_depot 105600 105601 2046220459945595868", arguments, StringComparison.Ordinal);
        Assert.DoesNotContain("force_install_dir", arguments, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RecognizesCompletionForTheRequestedManifest()
    {
        const string completed = "[2026-08-29 13:09:39] Depot download complete : \"C:\\SteamCMD\\steamapps\\content\\app_105600\\depot_105601\" (manifest 117201757688892592)";

        Assert.True(SteamCmdDepotDownloader.ReportsCompletedManifest(completed, "117201757688892592"));
        Assert.False(SteamCmdDepotDownloader.ReportsCompletedManifest(completed, "2046220459945595868"));
    }
}
