using Alacrity.Launcher.Core;
using Xunit;

namespace Alacrity.Launcher.Core.Tests;

public sealed class LegacyProfileIsolationTests
{
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
