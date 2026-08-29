using Alacrity.Launcher.Core;
using Xunit;

namespace Alacrity.Launcher.Core.Tests;

public sealed class VersionDiscoveryTests
{
    [Theory]
    [InlineData("terraria-server-1458.zip", "1.4.5.8")]
    [InlineData("terraria-server-14510.zip", "1.4.5.10")]
    [InlineData("terraria-server-1353.zip", "1.3.5.3")]
    public void DedicatedServerArchiveParsesTerrariaVersion(string archiveName, string expectedVersion)
    {
        Assert.True(LatestTerrariaVersionDiscovery.TryParseServerArchive(archiveName, out string version));
        Assert.Equal(expectedVersion, version);
    }

    [Theory]
    [InlineData("server-1458.zip")]
    [InlineData("terraria-server-invalid.zip")]
    [InlineData("terraria-server-258.zip")]
    public void InvalidDedicatedServerArchiveIsRejected(string archiveName)
    {
        Assert.False(LatestTerrariaVersionDiscovery.TryParseServerArchive(archiveName, out _));
    }

    [Fact]
    public void ChangelogReaderSplitsVersionSections()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".txt");
        try {
            File.WriteAllText(path, "Version 1.4.5.8 Changes -------------------------------------------------------------------------------------------\n\nNewest\n\nVersion 1.4.5.7 changes\n---\nOlder\n");
            var reader = new ChangelogReader();

            IReadOnlyDictionary<string, string> entries = reader.Read(path);

            Assert.Equal("Newest", entries["1.4.5.8"]);
            Assert.Equal("Older", entries["1.4.5.7"]);
            Assert.True(reader.TryReadLatestVersion(path, out string version));
            Assert.Equal("1.4.5.8", version);
        }
        finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void ChangelogReaderAcceptsHistoricalHeadingsWithoutASpaceBeforeTheDashes()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".txt");
        try {
            File.WriteAllText(path, "Version 1.4.0.1 Changes --------------------------------\nRecent\n\nVersion 1.3.5.3 Changes--------------------------------\nHistorical\n\nVersion 1.2 Changes--------------------------------\nOldest\n");
            IReadOnlyDictionary<string, string> entries = new ChangelogReader().Read(path);

            Assert.Equal("Historical", entries["1.3.5.3"]);
            Assert.Equal("Oldest", entries["1.2"]);
        }
        finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void FivePartTerrariaVersionsAreAcceptedAndSorted()
    {
        Assert.True(TerrariaVersionNumber.TryParse("1.4.5.8.1", out TerrariaVersionNumber hotfix));
        Assert.True(TerrariaVersionNumber.TryParse("1.4.5.8", out TerrariaVersionNumber baseVersion));
        Assert.True(hotfix.CompareTo(baseVersion) > 0);

        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".txt");
        try {
            File.WriteAllText(path, "Version 1.4.5.8 Changes\nBase\n\nVersion 1.4.5.8.1 Changes\nHotfix\n");
            Assert.True(new ChangelogReader().TryReadLatestVersion(path, out string latest));
            Assert.Equal("1.4.5.8.1", latest);
        }
        finally {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("1.2", true)]
    [InlineData("1.2.4.1", true)]
    [InlineData("1.3", false)]
    [InlineData("1.3.0.1", false)]
    [InlineData("1.4.5.8.1", false)]
    public void VersionsBeforeOnePointThreeUseSteamLaunch(string version, bool expected)
    {
        Assert.Equal(expected, TerrariaLaunchService.RequiresSteamLaunch(version));
    }
}
