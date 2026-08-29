using Alacrity.Launcher.Core;
using Xunit;

namespace Alacrity.Launcher.Core.Tests;

public sealed class LegacyProfileIsolationTests
{
    [Fact]
    public void FirstLegacyLaunchStoresItsProfileAndRestoresTheCurrentProfile()
    {
        string root = Path.Combine(Path.GetTempPath(), "alacrity-launcher-tests", Guid.NewGuid().ToString("N"));
        try {
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root, "Players"));
            Directory.CreateDirectory(Path.Combine(root, "Worlds"));
            File.WriteAllText(Path.Combine(root, "Players", "current.plr"), "current-player");
            File.WriteAllText(Path.Combine(root, "Worlds", "current.wld"), "current-world");
            File.WriteAllText(Path.Combine(root, "achievements.dat"), "current-achievements");
            File.WriteAllText(Path.Combine(root, "config.dat"), "current-legacy-config");
            File.WriteAllText(Path.Combine(root, "config.json"), "current-config");
            File.WriteAllText(Path.Combine(root, "favorites.json"), "current-favorites");
            File.WriteAllText(Path.Combine(root, "input profiles.json"), "current-input-profiles");
            File.WriteAllText(Path.Combine(root, "servers.dat"), "current-servers");

            var service = new LegacyProfileIsolationService();
            LegacyProfileSwapState state = service.CreateState("1.4.5.8", "1.3.5.3", root);

            service.Activate(state);

            Directory.CreateDirectory(Path.Combine(root, "Players"));
            Directory.CreateDirectory(Path.Combine(root, "Worlds"));
            File.WriteAllText(Path.Combine(root, "Players", "legacy.plr"), "legacy-player");
            File.WriteAllText(Path.Combine(root, "Worlds", "legacy.wld"), "legacy-world");
            File.WriteAllText(Path.Combine(root, "achievements.dat"), "legacy-achievements");
            File.WriteAllText(Path.Combine(root, "config.dat"), "legacy-legacy-config");
            File.WriteAllText(Path.Combine(root, "config.json"), "legacy-config");
            File.WriteAllText(Path.Combine(root, "favorites.json"), "legacy-favorites");
            File.WriteAllText(Path.Combine(root, "input profiles.json"), "legacy-input-profiles");
            File.WriteAllText(Path.Combine(root, "servers.dat"), "legacy-servers");

            service.Restore(state);

            Assert.Equal("current-player", File.ReadAllText(Path.Combine(root, "Players", "current.plr")));
            Assert.Equal("current-world", File.ReadAllText(Path.Combine(root, "Worlds", "current.wld")));
            Assert.Equal("current-achievements", File.ReadAllText(Path.Combine(root, "achievements.dat")));
            Assert.Equal("current-legacy-config", File.ReadAllText(Path.Combine(root, "config.dat")));
            Assert.Equal("current-config", File.ReadAllText(Path.Combine(root, "config.json")));
            Assert.Equal("current-favorites", File.ReadAllText(Path.Combine(root, "favorites.json")));
            Assert.Equal("current-input-profiles", File.ReadAllText(Path.Combine(root, "input profiles.json")));
            Assert.Equal("current-servers", File.ReadAllText(Path.Combine(root, "servers.dat")));
            Assert.Equal("legacy-player", File.ReadAllText(Path.Combine(root, "Players 1.3.5.3", "legacy.plr")));
            Assert.Equal("legacy-world", File.ReadAllText(Path.Combine(root, "Worlds 1.3.5.3", "legacy.wld")));
            Assert.Equal("legacy-achievements", File.ReadAllText(Path.Combine(root, "achievements.dat 1.3.5.3")));
            Assert.Equal("legacy-legacy-config", File.ReadAllText(Path.Combine(root, "config.dat 1.3.5.3")));
            Assert.Equal("legacy-config", File.ReadAllText(Path.Combine(root, "config.json 1.3.5.3")));
            Assert.Equal("legacy-favorites", File.ReadAllText(Path.Combine(root, "favorites.json 1.3.5.3")));
            Assert.Equal("legacy-input-profiles", File.ReadAllText(Path.Combine(root, "input profiles.json 1.3.5.3")));
            Assert.Equal("legacy-servers", File.ReadAllText(Path.Combine(root, "servers.dat 1.3.5.3")));
        }
        finally {
            if (Directory.Exists(root)) {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void ExistingLegacyProfileIsActivatedAndCurrentProfileIsRestored()
    {
        string root = Path.Combine(Path.GetTempPath(), "alacrity-launcher-tests", Guid.NewGuid().ToString("N"));
        try {
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root, "Players"));
            File.WriteAllText(Path.Combine(root, "Players", "current.txt"), "current");
            Directory.CreateDirectory(Path.Combine(root, "Players 1.3.5.3"));
            File.WriteAllText(Path.Combine(root, "Players 1.3.5.3", "legacy.txt"), "legacy");

            var service = new LegacyProfileIsolationService();
            LegacyProfileSwapState state = service.CreateState("1.4.5.8", "1.3.5.3", root);

            service.Activate(state);

            Assert.True(File.Exists(Path.Combine(root, "Players", "legacy.txt")));
            Assert.True(File.Exists(Path.Combine(root, "Players 1.4.5.8", "current.txt")));
            File.WriteAllText(Path.Combine(root, "Players", "created-by-legacy.txt"), "legacy-result");

            service.Restore(state);

            Assert.True(File.Exists(Path.Combine(root, "Players", "current.txt")));
            Assert.True(File.Exists(Path.Combine(root, "Players 1.3.5.3", "created-by-legacy.txt")));
        }
        finally {
            if (Directory.Exists(root)) {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void RestoringPartiallyActivatedProfileKeepsCurrentFiles()
    {
        string root = Path.Combine(Path.GetTempPath(), "alacrity-launcher-tests", Guid.NewGuid().ToString("N"));
        try {
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root, "Players"));
            File.WriteAllText(Path.Combine(root, "Players", "current.txt"), "current");

            var service = new LegacyProfileIsolationService();
            LegacyProfileSwapState state = service.CreateState("1.4.5.8", "1.3.5.3", root);
            service.Activate(state);

            service.Restore(state);

            Assert.True(File.Exists(Path.Combine(root, "Players", "current.txt")));
            Assert.False(Directory.Exists(Path.Combine(root, "Players 1.4.5.8")));
        }
        finally {
            if (Directory.Exists(root)) {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
