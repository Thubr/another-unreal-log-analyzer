namespace UeLogKit.Core.Query;

public sealed record LogQuery(
    IReadOnlyList<string> IncludedCategories,
    IReadOnlyList<string> ExcludedCategories,
    string? MinVerbosity,
    string? ContainsText,
    TimeSpan? Since,
    TimeSpan? Until,
    bool ExcludeProfileNoise)
{
    public static LogQuery Empty { get; } = new(
        IncludedCategories: [],
        ExcludedCategories: [],
        MinVerbosity: null,
        ContainsText: null,
        Since: null,
        Until: null,
        ExcludeProfileNoise: false);
}
