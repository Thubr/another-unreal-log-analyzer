namespace UeLogKit.Core;

public sealed record LogEvent(
    string SchemaVersion,
    string SourceId,
    string SourcePath,
    int LineNumber,
    DateTimeOffset? Timestamp,
    TimeSpan? RelativeTime,
    int? Frame,
    string Category,
    string Verbosity,
    string Message,
    IReadOnlyList<string> ContinuationLines,
    IReadOnlyDictionary<string, string> Fields,
    string RawTextHash
);
