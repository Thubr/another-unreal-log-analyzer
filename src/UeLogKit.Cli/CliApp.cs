using UeLogKit.Core;
using UeLogKit.Core.Dedupe;
using UeLogKit.Cli.Inspect;
using UeLogKit.Core.Normalization;
using UeLogKit.Core.Parser;
using UeLogKit.Core.Profiles;
using UeLogKit.Core.Query;

namespace UeLogKit.Cli;

public static class CliApp
{
    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        if (args.Length < 2)
        {
            await error.WriteLineAsync("Usage: uelog <parse|summarize|filter|clean|categories|inspect> <logPath> [options]");
            return 1;
        }

        var command = args[0].ToLowerInvariant();
        var logPath = args[1];
        var parser = LogEventSourceFactory.CreateDefault();

        var input = new LogInput(SourceId: "cli", SourcePath: logPath);
        return command switch
        {
            "parse" => await RunParseAsync(parser, input, args.Skip(2).ToArray(), output, cancellationToken),
            "summarize" => await RunSummarizeAsync(parser, input, args.Skip(2).ToArray(), output, error, cancellationToken),
            "filter" => await RunFilterAsync(parser, input, args.Skip(2).ToArray(), output, error, cancellationToken),
            "clean" => await RunCleanAsync(parser, input, args.Skip(2).ToArray(), output, error, cancellationToken),
            "categories" => await RunCategoriesAsync(parser, input, args.Skip(2).ToArray(), output, error, cancellationToken),
            "inspect" => await RunInspectAsync(parser, input, args.Skip(2).ToArray(), error, cancellationToken),
            _ => await UnknownCommandAsync(command, error)
        };
    }

    private static async Task<int> UnknownCommandAsync(string command, TextWriter error)
    {
        await error.WriteLineAsync($"Unknown command '{command}'.");
        return 1;
    }

    private static async Task<int> RunParseAsync(ILogEventSource parser, LogInput input, string[] args, TextWriter output, CancellationToken cancellationToken)
    {
        var format = args.FirstOrDefault(a => a.StartsWith("--format=", StringComparison.OrdinalIgnoreCase))?.Split('=')[1] ?? "json";
        var normalize = args.Any(a => string.Equals(a, "--normalize", StringComparison.OrdinalIgnoreCase));
        var writer = new LogEventJsonWriter();
        var events = parser.ReadEventsAsync(input, new ParserOptions(), cancellationToken);
        if (normalize)
        {
            events = new LogEventNormalizer().ProcessAsync(events, cancellationToken);
        }

        if (string.Equals(format, "ndjson", StringComparison.OrdinalIgnoreCase))
        {
            await writer.WriteNdjsonAsync(events, output, cancellationToken);
            return 0;
        }

        var eventList = await ToListAsync(events, cancellationToken);
        await writer.WriteJsonArrayAsync(eventList, output, cancellationToken);
        await output.WriteLineAsync();
        return 0;
    }

    private static async Task<int> RunSummarizeAsync(ILogEventSource parser, LogInput input, string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        if (!TryLoadProfile(args, error, out var profile))
        {
            return 1;
        }

        var events = await ToListAsync(parser.ReadEventsAsync(input, new ParserOptions(), cancellationToken), cancellationToken);
        var summary = LogQueryService.BuildFacetSummary(events);

        await output.WriteLineAsync($"Total events: {summary.TotalEvents}");
        await output.WriteLineAsync($"Warnings: {summary.WarningCount}");
        await output.WriteLineAsync($"Errors: {summary.ErrorCount}");
        if (profile is not null)
        {
            await output.WriteLineAsync($"Important events: {events.Count(e => IsImportant(e, profile))}");
        }

        return 0;
    }

    private static async Task<int> RunFilterAsync(ILogEventSource parser, LogInput input, string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var normalize = args.Any(a => string.Equals(a, "--normalize", StringComparison.OrdinalIgnoreCase));
        if (!TryLoadProfile(args, error, out var profile))
        {
            return 1;
        }

        var events = await ToListAsync(parser.ReadEventsAsync(input, new ParserOptions(), cancellationToken), cancellationToken);
        var query = BuildQuery(args, excludeProfileNoise: profile is not null);
        var filtered = LogQueryService.Apply(events, query, profile);

        var normalizer = normalize ? new LogEventNormalizer() : null;
        foreach (var e in filtered)
        {
            var outputEvent = normalizer is null ? e : normalizer.Normalize(e);
            await output.WriteLineAsync($"{outputEvent.LineNumber}: [{outputEvent.Category}] {outputEvent.Verbosity}: {outputEvent.Message}");
        }

        return 0;
    }

    private static async Task<int> RunCategoriesAsync(ILogEventSource parser, LogInput input, string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        if (!TryLoadProfile(args, error, out var profile))
        {
            return 1;
        }

        var format = args.FirstOrDefault(a => a.StartsWith("--format=", StringComparison.OrdinalIgnoreCase))?.Split('=')[1] ?? "text";
        var events = await ToListAsync(parser.ReadEventsAsync(input, new ParserOptions(), cancellationToken), cancellationToken);
        var facetEvents = profile is null
            ? events
            : LogQueryService.Apply(events, LogQuery.Empty with { ExcludeProfileNoise = true }, profile);
        var summary = LogQueryService.BuildFacetSummary(facetEvents);

        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            var json = System.Text.Json.JsonSerializer.Serialize(
                summary,
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                });
            await output.WriteAsync(json.AsMemory(), cancellationToken);
            await output.WriteLineAsync();
            return 0;
        }

        foreach (var count in summary.CategoryCounts)
        {
            await output.WriteLineAsync($"{count.Name}\t{count.Count}");
        }

        return 0;
    }

    private static async Task<int> RunInspectAsync(ILogEventSource parser, LogInput input, string[] args, TextWriter error, CancellationToken cancellationToken)
    {
        if (!TryLoadProfile(args, error, out var profile))
        {
            return 1;
        }

        var events = await ToListAsync(parser.ReadEventsAsync(input, new ParserOptions(), cancellationToken), cancellationToken);
        var model = new InspectViewModel(input.SourcePath, events, profile);
        new InspectTerminalUi().Run(model);
        return 0;
    }

    private static async Task<int> RunCleanAsync(ILogEventSource parser, LogInput input, string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        if (!TryParseDedupeMode(args, error, out var dedupeMode))
        {
            return 1;
        }

        var normalizer = new LogEventNormalizer();
        var events = await ToListAsync(parser.ReadEventsAsync(input, new ParserOptions(), cancellationToken), cancellationToken);
        var dedupedEvents = new LogEventDeduper().Deduplicate(events, dedupeMode);
        foreach (var deduped in dedupedEvents)
        {
            var normalized = normalizer.Normalize(deduped.Event);
            var prefix = deduped.Count > 1 ? $"[{deduped.Count}x] " : string.Empty;
            await output.WriteLineAsync($"{prefix}{normalized.Category}: {normalized.Verbosity}: {normalized.Message}");
        }

        return 0;
    }

    private static TimeSpan? ParseTimeSpanOption(string[] args, string prefix)
    {
        var raw = args.FirstOrDefault(a => a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))?.Split('=')[1];
        if (raw is null)
        {
            return null;
        }

        return TimeSpan.TryParse(raw, out var parsed) ? parsed : null;
    }

    private static LogQuery BuildQuery(string[] args, bool excludeProfileNoise)
    {
        var category = args.FirstOrDefault(a => a.StartsWith("--category=", StringComparison.OrdinalIgnoreCase))?.Split('=')[1];
        var excludedCategory = args.FirstOrDefault(a => a.StartsWith("--exclude-category=", StringComparison.OrdinalIgnoreCase))?.Split('=')[1];
        var minLevel = args.FirstOrDefault(a => a.StartsWith("--min-level=", StringComparison.OrdinalIgnoreCase))?.Split('=')[1];
        var contains = args.FirstOrDefault(a => a.StartsWith("--contains=", StringComparison.OrdinalIgnoreCase))?.Split('=')[1];

        return new LogQuery(
            IncludedCategories: SplitCsv(category),
            ExcludedCategories: SplitCsv(excludedCategory),
            MinVerbosity: minLevel,
            ContainsText: contains,
            Since: ParseTimeSpanOption(args, "--since="),
            Until: ParseTimeSpanOption(args, "--until="),
            ExcludeProfileNoise: excludeProfileNoise);
    }

    private static IReadOnlyList<string> SplitCsv(string? raw)
    {
        return string.IsNullOrWhiteSpace(raw)
            ? []
            : raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static bool TryParseDedupeMode(string[] args, TextWriter error, out DedupeMode mode)
    {
        var raw = args.FirstOrDefault(a => a.StartsWith("--dedupe=", StringComparison.OrdinalIgnoreCase))?.Split('=')[1] ?? "none";
        if (Enum.TryParse(raw, ignoreCase: true, out mode) && Enum.IsDefined(mode))
        {
            return true;
        }

        error.WriteLine($"Invalid dedupe mode '{raw}'. Expected one of: none, exact, normalized, burst.");
        mode = DedupeMode.None;
        return false;
    }

    private static bool TryLoadProfile(string[] args, TextWriter error, out LogProfile? profile)
    {
        profile = null;
        var raw = args.FirstOrDefault(a => a.StartsWith("--profile=", StringComparison.OrdinalIgnoreCase))?.Split('=')[1];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        try
        {
            profile = LogProfileLoader.Load(raw);
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            error.WriteLine(ex.Message);
            return false;
        }
    }

    private static bool IsImportant(LogEvent logEvent, LogProfile profile)
    {
        return ContainsIgnoreCase(profile.ImportantCategories, logEvent.Category)
            || profile.ImportantPatterns.Any(pattern => logEvent.Message.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsIgnoreCase(IEnumerable<string> values, string candidate)
    {
        return values.Any(value => string.Equals(value, candidate, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<List<LogEvent>> ToListAsync(IAsyncEnumerable<LogEvent> events, CancellationToken cancellationToken)
    {
        var list = new List<LogEvent>();
        await foreach (var e in events.WithCancellation(cancellationToken))
        {
            list.Add(e);
        }

        return list;
    }
}
