using System.Globalization;
using UeLogKit.Core;
using UeLogKit.Core.Profiles;
using UeLogKit.Core.Query;

namespace UeLogKit.Cli.Inspect;

public sealed class InspectViewModel
{
    private readonly IReadOnlyList<LogEvent> _events;
    private readonly LogProfile? _profile;

    public InspectViewModel(string logPath, IReadOnlyList<LogEvent> events, LogProfile? profile)
    {
        LogPath = logPath;
        _events = events;
        _profile = profile;
        Query = LogQuery.Empty;
        Refresh();
    }

    public string LogPath { get; }
    public LogQuery Query { get; private set; }
    public LogFacetSummary Facets { get; private set; } = LogQueryService.BuildFacetSummary([]);
    public IReadOnlyList<LogEvent> Results { get; private set; } = [];

    public void ToggleCategory(string category)
    {
        var categories = Query.IncludedCategories.ToList();
        var existing = categories.FindIndex(value => string.Equals(value, category, StringComparison.OrdinalIgnoreCase));
        if (existing >= 0)
        {
            categories.RemoveAt(existing);
        }
        else
        {
            categories.Add(category);
        }

        Query = Query with { IncludedCategories = categories };
        Refresh();
    }

    public void SetMinVerbosity(string? minVerbosity)
    {
        Query = Query with { MinVerbosity = string.IsNullOrWhiteSpace(minVerbosity) ? null : minVerbosity };
        Refresh();
    }

    public void SetContainsText(string? containsText)
    {
        Query = Query with { ContainsText = string.IsNullOrWhiteSpace(containsText) ? null : containsText };
        Refresh();
    }

    public void SetExcludeProfileNoise(bool excludeProfileNoise)
    {
        Query = Query with { ExcludeProfileNoise = excludeProfileNoise };
        Refresh();
    }

    public void SetTimeWindow(TimeSpan? since, TimeSpan? until)
    {
        Query = Query with { Since = since, Until = until };
        Refresh();
    }

    public string ExportFilterCommand()
    {
        var parts = new List<string>
        {
            "uelog",
            "filter",
            Quote(LogPath)
        };

        if (Query.IncludedCategories.Count > 0)
        {
            parts.Add($"--category={string.Join(',', Query.IncludedCategories)}");
        }

        if (!string.IsNullOrWhiteSpace(Query.MinVerbosity))
        {
            parts.Add($"--min-level={Query.MinVerbosity}");
        }

        if (!string.IsNullOrWhiteSpace(Query.ContainsText))
        {
            parts.Add($"--contains={Quote(Query.ContainsText)}");
        }

        if (Query.Since is not null)
        {
            parts.Add($"--since={Query.Since.Value.ToString("c", CultureInfo.InvariantCulture)}");
        }

        if (Query.Until is not null)
        {
            parts.Add($"--until={Query.Until.Value.ToString("c", CultureInfo.InvariantCulture)}");
        }

        parts.Add("--normalize");
        return string.Join(' ', parts);
    }

    private void Refresh()
    {
        Results = LogQueryService.Apply(_events, Query, _profile);
        Facets = LogQueryService.BuildFacetSummary(Query.ExcludeProfileNoise && _profile is not null
            ? LogQueryService.Apply(_events, LogQuery.Empty with { ExcludeProfileNoise = true }, _profile)
            : _events);
    }

    private static string Quote(string value)
    {
        return $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }
}
