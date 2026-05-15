using UeLogKit.Core.Analysis;
using UeLogKit.Core.Profiles;

namespace UeLogKit.Core.Tests.Analysis;

public sealed class ProfileDedupeSummaryTests
{
    [Fact]
    public void Profiles_are_resolvable()
    {
        Assert.Equal("ue-default", ProfileCatalog.Get("ue-default").Name);
        Assert.Equal("ue-online", ProfileCatalog.Get("ue-online").Name);
        Assert.Equal("ue-default", ProfileCatalog.Get("missing").Name);
    }

    [Fact]
    public void Dedupe_normalized_collapses_numeric_variants()
    {
        var profile = ProfileCatalog.Get("ue-default");
        var events = new[]
        {
            E(1, "LogNet", "Warning", "Packet lost count 100"),
            E(2, "LogNet", "Warning", "Packet lost count 200"),
            E(3, "LogNet", "Warning", "Packet lost count 200")
        };

        var deduped = LogEventDeduper.Apply(events, DedupeMode.Normalized, profile);
        Assert.Single(deduped);
    }

    [Fact]
    public void Summary_uses_profile_for_error_count_and_timeline()
    {
        var profile = ProfileCatalog.Get("ue-online");
        var events = new[]
        {
            E(1, "LogInit", "Display", "Start"),
            E(2, "LogNet", "Warning", "Packet"),
            E(3, "LogOnline", "Error", "Join failed")
        };

        var summary = LogSummarizer.Summarize(events, profile);
        Assert.Equal(3, summary.Total);
        Assert.Equal(1, summary.Warnings);
        Assert.Equal(2, summary.Errors); // ue-online counts warning as error-signal class
        Assert.NotEmpty(summary.ImportantTimeline);
    }

    private static LogEvent E(int line, string cat, string sev, string msg)
        => new("1.0", "s", "p", line, null, null, null, cat, sev, msg, Array.Empty<string>(), new Dictionary<string, string>(), "h");
}
