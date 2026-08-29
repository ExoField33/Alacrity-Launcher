using Alacrity.Launcher.Core;
using Xunit;

namespace Alacrity.Launcher.Core.Tests;

public sealed class SteamAccountNameLocatorTests
{
    [Fact]
    public void ReadsTheMostRecentSteamAccountName()
    {
        const string loginUsers = "\"users\"\n{\n\t\"111\"\n\t{\n\t\t\"AccountName\"\t\t\"older\"\n\t}\n\t\"222\"\n\t{\n\t\t\"AccountName\"\t\t\"current\"\n\t\t\"MostRecent\"\t\t\"1\"\n\t}\n}";

        string? accountName = SteamAccountNameLocator.TryReadMostRecentAccountName(loginUsers);

        Assert.Equal("current", accountName);
    }
}
