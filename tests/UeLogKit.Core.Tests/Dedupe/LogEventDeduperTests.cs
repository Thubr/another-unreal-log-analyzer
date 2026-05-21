using UeLogKit.Core.Dedupe;

namespace UeLogKit.Core.Tests.Dedupe;

public sealed class LogEventDeduperTests
{
    [Fact]
    public void Exact_mode_collapses_matching_events()
    {
        var events = new[]
        {
            CreateEvent("LogNet", "Warning", "Polling state", ["Detail A"]),
            CreateEvent("LogNet", "Warning", "Polling state", ["Detail A"]),
            CreateEvent("LogOnline", "Warning", "Join failed")
        };

        var results = new LogEventDeduper().Deduplicate(events, DedupeMode.Exact);

        Assert.Equal(2, results.Count);
        Assert.Equal(2, results[0].Count);
        Assert.Equal("Polling state", results[0].Event.Message);
        Assert.Equal(1, results[1].Count);
    }

    [Fact]
    public void Exact_mode_keeps_events_with_different_continuations_separate()
    {
        var events = new[]
        {
            CreateEvent("LogNet", "Warning", "Polling state", ["Detail A"]),
            CreateEvent("LogNet", "Warning", "Polling state", ["Detail B"])
        };

        var results = new LogEventDeduper().Deduplicate(events, DedupeMode.Exact);

        Assert.Equal(2, results.Count);
        Assert.All(results, result => Assert.Equal(1, result.Count));
    }

    [Fact]
    public void Normalized_mode_collapses_events_that_only_differ_by_supported_ids()
    {
        var events = new[]
        {
            CreateEvent("LogOnline", "Warning", "Session=Session-ABC123 Ticket=Ticket-1"),
            CreateEvent("LogOnline", "Warning", "Session=Session-XYZ789 Ticket=Ticket-2")
        };

        var results = new LogEventDeduper().Deduplicate(events, DedupeMode.Normalized);

        var result = Assert.Single(results);
        Assert.Equal(2, result.Count);
        Assert.Equal("Session=<session_id> Ticket=<ticket_id>", result.Event.Message);
    }

    [Fact]
    public void None_mode_keeps_all_events_unmodified()
    {
        var events = new[]
        {
            CreateEvent("LogNet", "Warning", "Polling state"),
            CreateEvent("LogNet", "Warning", "Polling state")
        };

        var results = new LogEventDeduper().Deduplicate(events, DedupeMode.None);

        Assert.Equal(2, results.Count);
        Assert.All(results, result => Assert.Equal(1, result.Count));
    }

    [Fact]
    public void Burst_mode_collapses_matching_timestamped_events_within_window()
    {
        var first = CreateEvent("LogNet", "Display", "Polling state") with
        {
            Timestamp = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero)
        };
        var second = first with
        {
            LineNumber = 2,
            Timestamp = new DateTimeOffset(2026, 1, 1, 12, 0, 3, TimeSpan.Zero)
        };
        var outsideWindow = first with
        {
            LineNumber = 3,
            Timestamp = new DateTimeOffset(2026, 1, 1, 12, 0, 20, TimeSpan.Zero)
        };

        var results = new LogEventDeduper().Deduplicate([first, second, outsideWindow], DedupeMode.Burst);

        Assert.Equal(2, results.Count);
        Assert.Equal(2, results[0].Count);
        Assert.Equal(1, results[1].Count);
    }

    [Fact]
    public void Burst_mode_skips_untimestamped_events()
    {
        var events = new[]
        {
            CreateEvent("LogNet", "Display", "Polling state"),
            CreateEvent("LogNet", "Display", "Polling state")
        };

        var results = new LogEventDeduper().Deduplicate(events, DedupeMode.Burst);

        Assert.Equal(2, results.Count);
        Assert.All(results, result => Assert.Equal(1, result.Count));
    }

    private static LogEvent CreateEvent(
        string category,
        string verbosity,
        string message,
        IReadOnlyList<string>? continuationLines = null)
    {
        return new LogEvent(
            SchemaVersion: LogEventSchemaVersion.V1,
            SourceId: "synthetic",
            SourcePath: "synthetic.log",
            LineNumber: 1,
            Timestamp: null,
            RelativeTime: null,
            Frame: null,
            Category: category,
            Verbosity: verbosity,
            Message: message,
            ContinuationLines: continuationLines ?? [],
            Fields: new Dictionary<string, string>(),
            RawTextHash: "sha256:synthetic");
    }
}
