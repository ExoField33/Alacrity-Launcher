using Alacrity.Launcher.Core;
using Xunit;

namespace Alacrity.Launcher.Core.Tests;

public sealed class DirectoryJunctionServiceTests
{
    [Fact]
    public async Task CreateAndRemoveDoNotModifyTheTargetDirectory()
    {
        string root = Path.Combine(Path.GetTempPath(), "alacrity-launcher-tests", Guid.NewGuid().ToString("N"));
        string target = Path.Combine(root, "target");
        string junction = Path.Combine(root, "junction");
        try {
            Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(target, "marker.txt"), "target");

            var service = new DirectoryJunctionService();
            await service.CreateAsync(junction, target, CancellationToken.None);

            Assert.True(File.Exists(Path.Combine(junction, "marker.txt")));
            await service.RemoveAsync(junction, CancellationToken.None);

            Assert.False(Directory.Exists(junction));
            Assert.True(File.Exists(Path.Combine(target, "marker.txt")));
        }
        finally {
            if (Directory.Exists(root)) {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
