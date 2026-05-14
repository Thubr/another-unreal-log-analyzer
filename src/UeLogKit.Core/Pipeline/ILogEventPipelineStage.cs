namespace UeLogKit.Core.Pipeline;

public interface ILogEventPipelineStage
{
    IAsyncEnumerable<LogEvent> ProcessAsync(
        IAsyncEnumerable<LogEvent> input,
        CancellationToken cancellationToken = default);
}
