namespace UeLogKit.Core.Dedupe;

public sealed record DedupedLogEvent(LogEvent Event, int Count);
