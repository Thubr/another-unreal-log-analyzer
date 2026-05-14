namespace UeLogKit.Core;

public interface ILogEventSource
{
    IAsyncEnumerable<LogEvent> ReadEventsAsync(
        LogInput input,
        ParserOptions options,
        CancellationToken cancellationToken = default);
}
