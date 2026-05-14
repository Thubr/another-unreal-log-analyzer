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
            Assert.Equal(fixture.ExpectedEvents[i], actual[i]);
        }
    }
}
