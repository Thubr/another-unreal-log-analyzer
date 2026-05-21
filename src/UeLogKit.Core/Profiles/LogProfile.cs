namespace UeLogKit.Core.Profiles;

public sealed record LogProfile(
    string Name,
    string? Version,
    IReadOnlyList<string> NoiseCategories,
    IReadOnlyList<string> ImportantCategories,
    IReadOnlyList<string> ImportantPatterns,
    IReadOnlyDictionary<string, string> OutputPreferences);
