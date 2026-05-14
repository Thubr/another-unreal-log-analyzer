namespace UeLogKit.Core.Tests.Contract;

internal sealed class StubLogEventSource : ILogEventSource
{
    private readonly IReadOnlyList<LogEvent> _events;

    public StubLogEventSource(IReadOnlyList<LogEvent> events)
    {
        _events = events;
    }

    public async IAsyncEnumerable<LogEvent> ReadEventsAsync(
        LogInput input,
        ParserOptions options,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var logEvent in _events)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return logEvent;
        }
    }
}
