using UeLogKit.Core.Query;
using UeLogKit.Core.Profiles;

namespace UeLogKit.Core.Tests.Query;

public sealed class LogQueryServiceTests
{
    [Fact]
    public void BuildFacetSummary_counts_categories_levels_and_time_bounds()
    {
        var events = SyntheticEvents();
        var summary = LogQueryService.BuildFacetSummary(events);

        Assert.Equal(5, summary.TotalEvents);
        Assert.Equal(2, summary.WarningCount);
        Assert.Equal(2, summary.ErrorCount);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero), summary.FirstTimestamp);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 12, 0, 4, TimeSpan.Zero), summary.LastTimestamp);
        Assert.Equal(["LogOnline", "LogInit", "LogNet", "LogSlate"], summary.CategoryCounts.Select(c => c.Name).ToArray());
        Assert.Equal([2, 1, 1, 1], summary.CategoryCounts.Select(c => c.Count).ToArray());
    }

    [Fact]
    public void Apply_filters_by_facets_text_time_and_profile_noise()
    {
        var events = SyntheticEvents();
        var profile = new LogProfile(
            Name: "synthetic",
            Version: "1",
            NoiseCategories: ["LogSlate"],
            ImportantCategories: [],
            ImportantPatterns: [],
            OutputPreferences: new Dictionary<string, string>());
        var query = new LogQuery(
            IncludedCategories: ["LogOnline", "LogSlate"],
            ExcludedCategories: [],
            MinVerbosity: "Warning",
            ContainsText: "join",
            Since: TimeSpan.FromSeconds(1),
            Until: TimeSpan.FromSeconds(3),
            ExcludeProfileNoise: true);

        var matches = LogQueryService.Apply(events, query, profile);

        var match = Assert.Single(matches);
        Assert.Equal(2, match.LineNumber);
        Assert.Equal("LogOnline", match.Category);
        Assert.Equal("JoinSession failed", match.Message);
    }

    [Fact]
    public void GetContextWindow_returns_bounded_events_around_selected_line()
    {
        var events = SyntheticEvents();

        var context = LogQueryService.GetContextWindow(events, selectedLineNumber: 3, before: 1, after: 2);

        Assert.Equal([2, 3, 4, 5], context.Select(e => e.LineNumber).ToArray());
    }

    private static IReadOnlyList<LogEvent> SyntheticEvents()
    {
        var start = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        return
        [
            Event(1, start, "LogInit", "Display", "Start"),
            Event(2, start.AddSeconds(1), "LogOnline", "Warning", "JoinSession failed"),
            Event(3, start.AddSeconds(2), "LogNet", "Error", "NetworkFailure: PendingConnectionFailure"),
            Event(4, start.AddSeconds(3), "LogSlate", "Warning", "Slate tick JoinSession noise"),
            Event(5, start.AddSeconds(4), "LogOnline", "Error", "JoinSession failed again")
        ];
    }

    private static LogEvent Event(int line, DateTimeOffset timestamp, string category, string verbosity, string message)
    {
        return new LogEvent(
            SchemaVersion: LogEventSchemaVersion.V1,
            SourceId: "test",
            SourcePath: "synthetic.log",
            LineNumber: line,
            Timestamp: timestamp,
            RelativeTime: null,
            Frame: null,
            Category: category,
            Verbosity: verbosity,
            Message: message,
            ContinuationLines: [],
            Fields: new Dictionary<string, string>(),
            RawTextHash: $"sha256:{line}");
    }
}
