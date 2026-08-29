using Alacrity.Launcher.Core;
using Xunit;

namespace Alacrity.Launcher.Core.Tests;

public sealed class GitHubReleaseUpdateServiceTests
{
    [Fact]
    public void ParsesTheExpectedReleaseAsset()
    {
        const string document = """
            {
              "tag_name": "v0.1.1",
              "assets": [
                {
                  "name": "Alacrity-Launcher-win-x64.zip",
                  "browser_download_url": "https://github.com/ExoField33/Alacrity-Launcher/releases/download/v0.1.1/Alacrity-Launcher-win-x64.zip"
                }
              ]
            }
            """;

        LauncherUpdateInfo? update = GitHubReleaseUpdateService.ParseLatestRelease(document);

        Assert.NotNull(update);
        Assert.Equal(new Version(0, 1, 1), update.Version);
        Assert.Equal("Alacrity-Launcher-win-x64.zip", Path.GetFileName(update.DownloadUri.AbsolutePath));
    }

    [Fact]
    public void RejectsAReleaseWithoutTheExpectedAsset()
    {
        const string document = """
            {
              "tag_name": "v0.1.1",
              "assets": [
                {
                  "name": "source.zip",
                  "browser_download_url": "https://github.com/ExoField33/Alacrity-Launcher/archive/v0.1.1.zip"
                }
              ]
            }
            """;

        Assert.Null(GitHubReleaseUpdateService.ParseLatestRelease(document));
    }
}
