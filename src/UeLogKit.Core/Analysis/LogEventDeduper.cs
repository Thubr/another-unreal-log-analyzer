using System.Text.RegularExpressions;
using UeLogKit.Core.Profiles;

namespace UeLogKit.Core.Analysis;

public static class LogEventDeduper
{
    public static IReadOnlyList<LogEvent> Apply(IReadOnlyList<LogEvent> events, DedupeMode mode, AnalysisProfile profile)
    {
        return mode switch
        {
            DedupeMode.Exact => Exact(events),
            DedupeMode.Normalized => Normalized(events, profile),
            DedupeMode.Burst => Burst(events, profile),
            _ => events
        };
    }

    private static IReadOnlyList<LogEvent> Exact(IReadOnlyList<LogEvent> events)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<LogEvent>();
        foreach (var e in events)
        {
            var key = $"{e.Category}|{e.Verbosity}|{e.Message}";
            if (seen.Add(key)) result.Add(e);
        }
        return result;
    }

    private static IReadOnlyList<LogEvent> Normalized(IReadOnlyList<LogEvent> events, AnalysisProfile profile)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<LogEvent>();
        foreach (var e in events)
        {
            var key = $"{e.Category}|{e.Verbosity}|{NormalizeMessage(e.Message, profile)}";
            if (seen.Add(key)) result.Add(e);
        }
        return result;
    }

    private static IReadOnlyList<LogEvent> Burst(IReadOnlyList<LogEvent> events, AnalysisProfile profile)
    {
        var result = new List<LogEvent>();
        string? last = null;
        foreach (var e in events)
        {
            var key = $"{e.Category}|{e.Verbosity}|{NormalizeMessage(e.Message, profile)}";
            if (!string.Equals(last, key, StringComparison.Ordinal)) result.Add(e);
            last = key;
        }
        return result;
    }

    private static string NormalizeMessage(string message, AnalysisProfile profile)
    {
        var normalized = Regex.Replace(message, "\\d+", "#");
        return normalized.Trim();
    }
}
