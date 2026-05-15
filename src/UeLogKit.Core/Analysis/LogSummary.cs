namespace UeLogKit.Core.Analysis;

public sealed record LogSummary(int Total, int Warnings, int Errors, IReadOnlyList<string> ImportantTimeline);
