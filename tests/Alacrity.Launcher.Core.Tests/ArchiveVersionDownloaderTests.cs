using Alacrity.Launcher.Core;
using Xunit;

namespace Alacrity.Launcher.Core.Tests;

public sealed class ArchiveVersionDownloaderTests
{
    [Fact]
    public void GitHubBlobUrlUsesTheRawDownloadRoute()
    {
        Uri uri = ArchiveVersionDownloader.NormalizeDownloadUri("https://github.com/RussDev7/LostTerrariaArchive/blob/main/Terraria-v1.0.1/Terraria-v1.0.1.zip");

        Assert.Equal("https://github.com/RussDev7/LostTerrariaArchive/raw/main/Terraria-v1.0.1/Terraria-v1.0.1.zip", uri.AbsoluteUri);
    }

    [Fact]
    public void NestedArchiveRootIsFlattenedToTheTerrariaDirectory()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string contentDirectory = Path.Combine(root, "Terraria-v1.0.1");
        try {
            Directory.CreateDirectory(contentDirectory);
            File.WriteAllText(Path.Combine(contentDirectory, "Terraria.exe"), "test");

            Assert.Equal(contentDirectory, ArchiveVersionDownloader.FindTerrariaContentDirectory(root));
        }
        finally {
            if (Directory.Exists(root)) {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
