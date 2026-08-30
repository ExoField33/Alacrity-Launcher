using Alacrity.Launcher.Core;
using Xunit;

namespace Alacrity.Launcher.Core.Tests;

public sealed class TerrariaVersionCatalogStoreTests
{
    [Fact]
    public void UrlSourceCanDownloadWithoutASteamManifest()
    {
        var entry = new TerrariaVersionEntry {
            Version = "1.0.1",
            Url = "https://github.com/RussDev7/LostTerrariaArchive/blob/main/Terraria-v1.0.1/Terraria-v1.0.1.zip"
        };

        entry.Validate();
        Assert.True(entry.CanDownload);
    }

    [Fact]
    public void VersionUrlMustUseHttps()
    {
        var entry = new TerrariaVersionEntry {
            Version = "1.0.1",
            Url = "http://example.test/Terraria-v1.0.1.zip"
        };

        Assert.Throws<InvalidDataException>(entry.Validate);
    }

    [Fact]
    public void TemplateMergeAddsMissingVersionsWithoutOverwritingTheLiveCatalog()
    {
        var catalog = new TerrariaVersionCatalog {
            Versions = new List<TerrariaVersionEntry> {
                new TerrariaVersionEntry {
                    Version = "1.4.5.8",
                    ManifestId = "111",
                    IsAutomaticallyDiscovered = true
                }
            }
        };
        var template = new TerrariaVersionCatalog {
            Versions = new List<TerrariaVersionEntry> {
                new TerrariaVersionEntry {
                    Version = "1.4.5.8",
                    ManifestId = "222",
                    Url = "https://example.test/Terraria-v1.4.5.8.zip",
                    IsAutomaticallyDiscovered = false
                },
                new TerrariaVersionEntry {
                    Version = "1.4.5.9",
                    ManifestId = "333",
                    IsAutomaticallyDiscovered = false
                }
            }
        };

        Assert.True(TerrariaVersionCatalogStore.MergeMissingTemplateEntries(catalog, template));
        Assert.Equal("111", catalog.Find("1.4.5.8")?.ManifestId);
        Assert.Equal("https://example.test/Terraria-v1.4.5.8.zip", catalog.Find("1.4.5.8")?.Url);
        Assert.NotNull(catalog.Find("1.4.5.9"));
        Assert.Equal("333", catalog.Find("1.4.5.9")?.ManifestId);
    }
}
