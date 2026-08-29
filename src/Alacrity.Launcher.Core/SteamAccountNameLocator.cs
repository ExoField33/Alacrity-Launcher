using System.Text.RegularExpressions;

namespace Alacrity.Launcher.Core;

public sealed class SteamAccountNameLocator
{
    private static readonly Regex UserBlockPattern = new Regex(
        @"""(?<id>\d+)""\s*\{(?<contents>.*?)\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    private static readonly Regex AccountNamePattern = new Regex(
        @"""AccountName""\s*""(?<name>[^""]+)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex MostRecentPattern = new Regex(
        @"""MostRecent""\s*""1""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public string? TryLocate(SteamTerrariaInstallation? installation)
    {
        string? steamExecutablePath = installation?.SteamExecutablePath;
        if (string.IsNullOrWhiteSpace(steamExecutablePath)) {
            return null;
        }

        string? steamDirectory = Path.GetDirectoryName(steamExecutablePath);
        if (string.IsNullOrWhiteSpace(steamDirectory)) {
            return null;
        }

        string loginUsersPath = Path.Combine(steamDirectory, "config", "loginusers.vdf");
        if (!File.Exists(loginUsersPath)) {
            return null;
        }

        try {
            return TryReadMostRecentAccountName(File.ReadAllText(loginUsersPath));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
            return null;
        }
    }

    public static string? TryReadMostRecentAccountName(string contents)
    {
        ArgumentNullException.ThrowIfNull(contents);

        string? firstAccountName = null;
        foreach (Match block in UserBlockPattern.Matches(contents)) {
            Match accountName = AccountNamePattern.Match(block.Groups["contents"].Value);
            if (!accountName.Success) {
                continue;
            }

            string name = accountName.Groups["name"].Value;
            firstAccountName ??= name;
            if (MostRecentPattern.IsMatch(block.Groups["contents"].Value)) {
                return name;
            }
        }

        return firstAccountName;
    }
}
