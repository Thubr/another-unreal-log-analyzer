using UeLogKit.Core.Profiles;

namespace UeLogKit.Core.Query;

public static class LogQueryService
{
    public static IReadOnlyList<LogEvent> Apply(IReadOnlyList<LogEvent> events, LogQuery query, LogProfile? profile = null)
    {
        var baseTimestamp = GetBaseTimestamp(events);
        var hasTimeFilter = query.Since is not null || query.Until is not null;
        var filtered = events.Where(e => Matches(e, query, profile, baseTimestamp, hasTimeFilter));
        return filtered.ToArray();
    }

    public static LogFacetSummary BuildFacetSummary(IReadOnlyList<LogEvent> events)
    {
        var timestamps = events.Where(e => e.Timestamp is not null).Select(e => e.Timestamp!.Value).ToArray();
        return new LogFacetSummary(
            TotalEvents: events.Count,
            WarningCount: events.Count(e => string.Equals(e.Verbosity, "Warning", StringComparison.OrdinalIgnoreCase)),
            ErrorCount: events.Count(e => IsError(e.Verbosity)),
            FirstTimestamp: timestamps.Length == 0 ? null : timestamps.Min(),
            LastTimestamp: timestamps.Length == 0 ? null : timestamps.Max(),
            CategoryCounts: CountFacet(events.Select(e => e.Category)),
            VerbosityCounts: CountFacet(events.Select(e => e.Verbosity)));
    }

    public static LogResultWindow GetWindow(IReadOnlyList<LogEvent> events, LogQuery query, int offset, int limit, LogProfile? profile = null)
    {
        var matches = Apply(events, query, profile);
        var safeOffset = Math.Max(0, offset);
        var safeLimit = Math.Max(0, limit);
        return new LogResultWindow(
            Offset: safeOffset,
            Limit: safeLimit,
            TotalMatches: matches.Count,
            Events: matches.Skip(safeOffset).Take(safeLimit).ToArray());
    }

    public static IReadOnlyList<LogEvent> GetContextWindow(IReadOnlyList<LogEvent> events, int selectedLineNumber, int before, int after)
    {
        var selectedIndex = events.ToList().FindIndex(e => e.LineNumber == selectedLineNumber);
        if (selectedIndex < 0)
        {
            return [];
        }

        var start = Math.Max(0, selectedIndex - Math.Max(0, before));
        var count = Math.Min(events.Count - start, Math.Max(0, before) + 1 + Math.Max(0, after));
        return events.Skip(start).Take(count).ToArray();
    }

    public static int SeverityRank(string verbosity) => verbosity.ToLowerInvariant() switch
    {
        "fatal" => 5,
        "error" => 4,
        "warning" => 3,
        "display" => 2,
        _ => 1
    };

    private static bool Matches(LogEvent logEvent, LogQuery query, LogProfile? profile, DateTimeOffset? baseTimestamp, bool hasTimeFilter)
    {
        if (query.IncludedCategories.Count > 0 && !ContainsIgnoreCase(query.IncludedCategories, logEvent.Category))
        {
            return false;
        }

        if (ContainsIgnoreCase(query.ExcludedCategories, logEvent.Category))
        {
            return false;
        }

        if (query.ExcludeProfileNoise && profile is not null && ContainsIgnoreCase(profile.NoiseCategories, logEvent.Category))
        {
            return false;
        }

        if (query.MinVerbosity is not null && SeverityRank(logEvent.Verbosity) < SeverityRank(query.MinVerbosity))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.ContainsText)
            && !logEvent.Message.Contains(query.ContainsText, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!MatchesTimeWindow(logEvent, query, baseTimestamp, hasTimeFilter))
        {
            return false;
        }

        return true;
    }

    private static bool MatchesTimeWindow(LogEvent logEvent, LogQuery query, DateTimeOffset? baseTimestamp, bool hasTimeFilter)
    {
        if (!hasTimeFilter || baseTimestamp is null)
        {
            return true;
        }

        if (logEvent.Timestamp is null)
        {
            return false;
        }

        var offset = logEvent.Timestamp.Value - baseTimestamp.Value;
        return (query.Since is null || offset >= query.Since.Value)
            && (query.Until is null || offset <= query.Until.Value);
    }

    private static DateTimeOffset? GetBaseTimestamp(IReadOnlyList<LogEvent> events)
    {
        var timestamps = events.Where(e => e.Timestamp is not null).Select(e => e.Timestamp!.Value).ToArray();
        return timestamps.Length == 0 ? null : timestamps.Min();
    }

    private static IReadOnlyList<LogFacetCount> CountFacet(IEnumerable<string> values)
    {
        return values
            .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Select(group => new LogFacetCount(group.Key, group.Count()))
            .OrderByDescending(count => count.Count)
            .ThenBy(count => count.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsError(string verbosity)
    {
        return string.Equals(verbosity, "Error", StringComparison.OrdinalIgnoreCase)
            || string.Equals(verbosity, "Fatal", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsIgnoreCase(IEnumerable<string> values, string candidate)
    {
        return values.Any(value => string.Equals(value, candidate, StringComparison.OrdinalIgnoreCase));
    }
}
