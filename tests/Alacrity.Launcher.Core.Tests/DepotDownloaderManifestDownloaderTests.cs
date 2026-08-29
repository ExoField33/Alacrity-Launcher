using Alacrity.Launcher.Core;
using Xunit;

namespace Alacrity.Launcher.Core.Tests;

public sealed class DepotDownloaderManifestDownloaderTests
{
    [Fact]
    public void UsesInteractiveSteamLoginAndWritesDirectlyToTheStagingDirectory()
    {
        var request = new DepotDownloadRequest {
            Version = "1.4.5.8.1",
            ManifestId = "2046220459945595868",
            DepotDownloaderPath = @"C:\launcher\Tools\DepotDownloader\DepotDownloader.exe",
            OutputDirectory = @"C:\launcher\Versions\1.4.5.8.1.staging-test",
            SteamAccountName = "example-user"
        };

        string arguments = DepotDownloaderManifestDownloader.CreateArguments(request);

        Assert.Contains("-app 105600", arguments, StringComparison.Ordinal);
        Assert.Contains("-depot 105601", arguments, StringComparison.Ordinal);
        Assert.Contains("-manifest 2046220459945595868", arguments, StringComparison.Ordinal);
        Assert.Contains("-username \"example-user\"", arguments, StringComparison.Ordinal);
        Assert.Contains("-remember-password", arguments, StringComparison.Ordinal);
        Assert.Contains("-dir \"C:\\launcher\\Versions\\1.4.5.8.1.staging-test\"", arguments, StringComparison.Ordinal);
        Assert.DoesNotContain(
            arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries),
            argument => string.Equals(argument, "-password", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("steamcmd", arguments, StringComparison.OrdinalIgnoreCase);
    }
}
