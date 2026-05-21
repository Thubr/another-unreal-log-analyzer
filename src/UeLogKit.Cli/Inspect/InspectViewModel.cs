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

    public string DefaultFilterProfilePath()
    {
        var fullPath = Path.GetFullPath(LogPath);
        var directory = Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory;
        var name = Path.GetFileNameWithoutExtension(fullPath);
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "uelog-filter";
        }

        return Path.Combine(directory, $"{name}.uelog-filter.yaml");
    }

    public void SaveFilterProfile(string path)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, BuildFilterProfileYaml());
    }

    public string BuildFilterProfileYaml()
    {
        var name = Path.GetFileNameWithoutExtension(LogPath);
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "uelog-filter";
        }

        var lines = new List<string>
        {
            $"name: {YamlScalar(name)}",
            "version: \"1\"",
            "filters:",
            "  included_categories:"
        };
        AppendList(lines, Query.IncludedCategories, indent: "    ");
        lines.Add("  excluded_categories:");
        AppendList(lines, Query.ExcludedCategories, indent: "    ");
        lines.Add($"  min_verbosity: {YamlNullable(Query.MinVerbosity)}");
        lines.Add($"  contains_text: {YamlNullable(Query.ContainsText)}");
        lines.Add($"  since: {YamlNullable(Query.Since?.ToString("c", CultureInfo.InvariantCulture))}");
        lines.Add($"  until: {YamlNullable(Query.Until?.ToString("c", CultureInfo.InvariantCulture))}");
        lines.Add("  normalize_on_export: true");
        lines.Add(string.Empty);
        return string.Join(Environment.NewLine, lines);
    }

    public int ClampCategorySelection(int previousIndex)
    {
        if (Facets.CategoryCounts.Count == 0)
        {
            return -1;
        }

        return Math.Clamp(previousIndex, 0, Facets.CategoryCounts.Count - 1);
    }

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
            "analyze",
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

    private static void AppendList(List<string> lines, IReadOnlyList<string> values, string indent)
    {
        if (values.Count == 0)
        {
            lines.Add($"{indent}[]");
            return;
        }

        lines.AddRange(values.Select(value => $"{indent}- {YamlScalar(value)}"));
    }

    private static string YamlNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "null" : YamlScalar(value);
    }

    private static string YamlScalar(string value)
    {
        return value.All(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.')
            ? value
            : $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
    }
}
