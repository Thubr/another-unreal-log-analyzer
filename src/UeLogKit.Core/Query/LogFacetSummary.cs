namespace UeLogKit.Core.Query;

public sealed record LogFacetSummary(
    int TotalEvents,
    int WarningCount,
    int ErrorCount,
    DateTimeOffset? FirstTimestamp,
    DateTimeOffset? LastTimestamp,
    IReadOnlyList<LogFacetCount> CategoryCounts,
    IReadOnlyList<LogFacetCount> VerbosityCounts);
