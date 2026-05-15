namespace UeLogKit.Core.Parser;

public static class LogEventSourceFactory
{
    public static ILogEventSource CreateDefault() => new MinimalUnrealLogParser();
}
