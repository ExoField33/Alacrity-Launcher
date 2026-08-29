using System.Text.RegularExpressions;

namespace Alacrity.Launcher.Core;

public sealed class SteamManifestReader
{
    private static readonly Regex TerrariaDepotManifestPattern = new Regex(
        @"""105601""\s*\{(?:(?!\}).)*?""manifest""\s+""(?<id>\d+)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    public string? TryReadTerrariaManifestId(SteamTerrariaInstallation? installation)
    {
        if (installation is null || string.IsNullOrWhiteSpace(installation.AppManifestPath) || !File.Exists(installation.AppManifestPath)) {
            return null;
        }

        try {
            Match match = TerrariaDepotManifestPattern.Match(File.ReadAllText(installation.AppManifestPath));
            return match.Success ? match.Groups["id"].Value : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
            return null;
        }
    }
}
