namespace UeLogKit.Core.Tests.Contract;

public static class ParserContractTestHarness
{
    public static async Task RunFixtureAsync(
        ILogEventSource source,
        string fixturePath,
        CancellationToken cancellationToken = default)
    {
        var fixture = ParserContractFixture.LoadFromFile(fixturePath);

        var actual = new List<LogEvent>();
        await foreach (var item in source.ReadEventsAsync(fixture.Input, fixture.Options, cancellationToken))
        {
            actual.Add(item);
        }

        Assert.Equal(fixture.ExpectedEvents.Count, actual.Count);

        for (var i = 0; i < fixture.ExpectedEvents.Count; i++)
        {
            AssertEventsEqual(fixture.ExpectedEvents[i], actual[i]);
        }
    }

    private static void AssertEventsEqual(LogEvent expected, LogEvent actual)
    {
        Assert.Equal(expected.SchemaVersion, actual.SchemaVersion);
        Assert.Equal(expected.SourceId, actual.SourceId);
        Assert.Equal(expected.SourcePath, actual.SourcePath);
        Assert.Equal(expected.LineNumber, actual.LineNumber);
        Assert.Equal(expected.Timestamp, actual.Timestamp);
        Assert.Equal(expected.RelativeTime, actual.RelativeTime);
        Assert.Equal(expected.Frame, actual.Frame);
        Assert.Equal(expected.Category, actual.Category);
        Assert.Equal(expected.Verbosity, actual.Verbosity);
        Assert.Equal(expected.Message, actual.Message);
        Assert.Equal(expected.RawTextHash, actual.RawTextHash);

        Assert.Equal(expected.ContinuationLines, actual.ContinuationLines);
        Assert.Equal(expected.Fields.OrderBy(kv => kv.Key), actual.Fields.OrderBy(kv => kv.Key));
    }
}
