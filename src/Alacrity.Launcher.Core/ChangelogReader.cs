using System.Text.RegularExpressions;

namespace Alacrity.Launcher.Core;

public sealed class ChangelogReader
{
    private static readonly Regex VersionHeadingPattern = new Regex(
        @"^Version\s+(?<version>\d+(?:\.\d+){1,4})\s+changes\s*(?:-+\s*)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Multiline);

    public IReadOnlyDictionary<string, string> Read(string changelogPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(changelogPath);

        string text = File.ReadAllText(changelogPath);
        MatchCollection headings = VersionHeadingPattern.Matches(text);
        var entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < headings.Count; index++) {
            Match heading = headings[index];
            int contentStart = heading.Index + heading.Length;
            int contentEnd = index + 1 < headings.Count ? headings[index + 1].Index : text.Length;
            string content = text.Substring(contentStart, contentEnd - contentStart).Trim();

            while (content.StartsWith("---", StringComparison.Ordinal)) {
                content = content.Substring(3).TrimStart();
            }

            entries[heading.Groups["version"].Value] = content;
        }

        return entries;
    }

    public bool TryReadLatestVersion(string changelogPath, out string version)
    {
        version = string.Empty;
        IReadOnlyDictionary<string, string> entries = Read(changelogPath);
        if (entries.Count == 0) {
            return false;
        }

        TerrariaVersionNumber? latest = null;
        foreach (string candidate in entries.Keys) {
            if (!TerrariaVersionNumber.TryParse(candidate, out TerrariaVersionNumber parsed)) {
                continue;
            }

            if (!latest.HasValue || parsed.CompareTo(latest.Value) > 0) {
                latest = parsed;
            }
        }

        if (!latest.HasValue) {
            return false;
        }

        version = latest.Value.ToString();
        return true;
    }
}
