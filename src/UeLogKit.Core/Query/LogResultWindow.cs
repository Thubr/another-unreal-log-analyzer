namespace UeLogKit.Core.Query;

public sealed record LogResultWindow(int Offset, int Limit, int TotalMatches, IReadOnlyList<LogEvent> Events);
