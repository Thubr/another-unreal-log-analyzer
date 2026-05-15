namespace UeLogKit.Core.Profiles;

public sealed record AnalysisProfile(
    string Name,
    IReadOnlySet<string> ImportantCategories,
    IReadOnlySet<string> ErrorVerbosities,
    IReadOnlyList<string> NormalizationTokens
);
