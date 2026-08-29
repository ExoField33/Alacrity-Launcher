using Alacrity.Launcher.Core;
using Xunit;

namespace Alacrity.Launcher.Core.Tests;

public sealed class LaunchRecoveryIdentityTests
{
    [Fact]
    public void RecordedProcessRequiresBothPidAndStartTimeToMatch()
    {
        DateTime startedUtc = new DateTime(2026, 8, 29, 12, 30, 0, DateTimeKind.Utc);
        var state = new LaunchRecoveryState {
            SelectedVersion = "1.2",
            TerrariaDirectory = @"C:\Steam\Terraria",
            BackupTerrariaDirectory = @"C:\Steam\Terraria.alacrity-launcher-backup",
            VersionDirectory = @"C:\Launcher\Versions\1.2",
            TerrariaLaunchInProgress = true,
            TerrariaLaunchStartedUtc = startedUtc.AddSeconds(-1),
            TerrariaProcessId = 1234,
            TerrariaProcessStartedUtc = startedUtc
        };

        Assert.True(TerrariaLaunchService.MatchesRecordedTerrariaProcess(state, 1234, startedUtc));
        Assert.False(TerrariaLaunchService.MatchesRecordedTerrariaProcess(state, 1234, startedUtc.AddSeconds(1)));
        Assert.False(TerrariaLaunchService.MatchesRecordedTerrariaProcess(state, 4321, startedUtc));
    }
}
