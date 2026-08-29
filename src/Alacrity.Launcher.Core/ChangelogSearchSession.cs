namespace Alacrity.Launcher.Core;

public sealed class ChangelogSearchSession
{
    private string query = string.Empty;
    private int currentMatchStart = -1;
    private int currentMatchOrdinal;
    private int totalMatches;

    public ChangelogSearchMatch UpdateQuery(string text, string? newQuery)
    {
        query = newQuery ?? string.Empty;
        currentMatchStart = -1;
        currentMatchOrdinal = 0;
        totalMatches = CountMatches(text, query);

        if (totalMatches == 0) {
            return ChangelogSearchMatch.NotFound;
        }

        currentMatchStart = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        currentMatchOrdinal = 1;
        return new ChangelogSearchMatch(currentMatchStart, query.Length, currentMatchOrdinal, totalMatches);
    }

    public ChangelogSearchMatch Move(string text, bool previous)
    {
        if (string.IsNullOrEmpty(query)) {
            return ChangelogSearchMatch.NotFound;
        }

        totalMatches = CountMatches(text, query);
        if (totalMatches == 0) {
            currentMatchStart = -1;
            currentMatchOrdinal = 0;
            return ChangelogSearchMatch.NotFound;
        }

        if (currentMatchStart < 0 || currentMatchStart >= text.Length) {
            return UpdateQuery(text, query);
        }

        if (previous) {
            int previousSearchStart = currentMatchStart - 1;
            int previousMatch = previousSearchStart >= 0
                ? text.LastIndexOf(query, previousSearchStart, StringComparison.OrdinalIgnoreCase)
                : -1;
            if (previousMatch < 0) {
                previousMatch = text.LastIndexOf(query, StringComparison.OrdinalIgnoreCase);
                currentMatchOrdinal = totalMatches;
            }
            else {
                currentMatchOrdinal--;
            }

            currentMatchStart = previousMatch;
        }
        else {
            int nextStart = currentMatchStart + query.Length;
            int nextMatch = nextStart < text.Length
                ? text.IndexOf(query, nextStart, StringComparison.OrdinalIgnoreCase)
                : -1;
            if (nextMatch < 0) {
                nextMatch = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
                currentMatchOrdinal = 1;
            }
            else {
                currentMatchOrdinal++;
            }

            currentMatchStart = nextMatch;
        }

        return new ChangelogSearchMatch(currentMatchStart, query.Length, currentMatchOrdinal, totalMatches);
    }

    public void Reset()
    {
        query = string.Empty;
        currentMatchStart = -1;
        currentMatchOrdinal = 0;
        totalMatches = 0;
    }

    private static int CountMatches(string text, string query)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(query)) {
            return 0;
        }

        int count = 0;
        int searchStart = 0;
        while (searchStart < text.Length) {
            int match = text.IndexOf(query, searchStart, StringComparison.OrdinalIgnoreCase);
            if (match < 0) {
                break;
            }

            count++;
            searchStart = match + query.Length;
        }

        return count;
    }
}

public readonly record struct ChangelogSearchMatch(int Start, int Length, int Current, int Total)
{
    public static ChangelogSearchMatch NotFound { get; } = new ChangelogSearchMatch(-1, 0, 0, 0);

    public bool IsFound => Start >= 0;
}
