using UeLogKit.Core.Profiles;

namespace UeLogKit.Core.Analysis;

public static class LogSummarizer
{
    public static LogSummary Summarize(IReadOnlyList<LogEvent> events, AnalysisProfile profile)
    {
        var warnings = events.Count(e => e.Verbosity.Equals("Warning", StringComparison.OrdinalIgnoreCase));
        var errors = events.Count(e => profile.ErrorVerbosities.Contains(e.Verbosity));
        var timeline = events
            .Where(e => profile.ImportantCategories.Contains(e.Category) || profile.ErrorVerbosities.Contains(e.Verbosity))
            .Take(5)
            .Select(e => $"L{e.LineNumber} {e.Category} {e.Verbosity}: {e.Message}")
            .ToArray();

        return new LogSummary(events.Count, warnings, errors, timeline);
    }
}
