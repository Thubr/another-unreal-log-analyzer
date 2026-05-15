namespace UeLogKit.Core.Parser;

public interface ILogEventWriter
{
    Task WriteJsonArrayAsync(IReadOnlyList<LogEvent> events, TextWriter writer, CancellationToken cancellationToken = default);
    Task WriteNdjsonAsync(IAsyncEnumerable<LogEvent> events, TextWriter writer, CancellationToken cancellationToken = default);
}
