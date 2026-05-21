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
            await error.WriteLineAsync("Usage: uelog <analyze|inspect|parse|summarize|filter|clean|categories> <logPath> [options]");
            return 1;
        }

        var command = args[0].ToLowerInvariant();
        var logPath = args[1];
        var parser = LogEventSourceFactory.CreateDefault();

        var input = new LogInput(SourceId: "cli", SourcePath: logPath);
        return command switch
        {
            "analyze" => await RunAnalyzeAsync(parser, input, args.Skip(2).ToArray(), output, error, cancellationToken),
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

    private static async Task<int> RunAnalyzeAsync(ILogEventSource parser, LogInput input, string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        if (!TryParseAnalyzeOptions(args, error, out var options)
            || !TryLoadProfile(args, error, out var profile))
        {
            return 1;
        }

        var destination = output;
        StreamWriter? fileWriter = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(options.OutputPath))
            {
                fileWriter = new StreamWriter(options.OutputPath);
                destination = fileWriter;
            }

            var events = await ToListAsync(parser.ReadEventsAsync(input, new ParserOptions(), cancellationToken), cancellationToken);
            if (options.Normalize)
            {
                var normalizer = new LogEventNormalizer();
                events = events.Select(normalizer.Normalize).ToList();
            }

            var deduped = new LogEventDeduper().Deduplicate(events, options.DedupeMode);
            var filteredEvents = LogQueryService.Apply(deduped.Select(d => d.Event).ToArray(), options.Query, profile);
            var filteredLineNumbers = filteredEvents.Select(e => e.LineNumber).ToHashSet();
            var rows = deduped.Where(d => filteredLineNumbers.Contains(d.Event.LineNumber)).ToArray();
            if (options.Limit is not null)
            {
                rows = rows.Take(options.Limit.Value).ToArray();
            }

            if (options.Explain)
            {
                await destination.WriteLineAsync($"Pipeline: {DescribePipeline(options)}");
            }

            if (options.Summary || options.Facets)
            {
                await WriteAnalyzeSummaryAsync(rows.Select(row => row.Event).ToArray(), options, destination);
            }

            if (options.Events)
            {
                await WriteAnalyzeEventsAsync(rows, options, destination, cancellationToken);
            }

            return 0;
        }
        finally
        {
            if (fileWriter is not null)
            {
                await fileWriter.DisposeAsync();
            }
        }
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

    private static async Task WriteAnalyzeSummaryAsync(IReadOnlyList<LogEvent> events, AnalyzeOptions options, TextWriter output)
    {
        var summary = LogQueryService.BuildFacetSummary(events);
        await output.WriteLineAsync($"Total events: {summary.TotalEvents}");
        await output.WriteLineAsync($"Warnings: {summary.WarningCount}");
        await output.WriteLineAsync($"Errors: {summary.ErrorCount}");

        if (!options.Facets)
        {
            return;
        }

        await output.WriteLineAsync("Categories:");
        foreach (var count in summary.CategoryCounts)
        {
            await output.WriteLineAsync($"{count.Name}\t{count.Count}");
        }

        await output.WriteLineAsync("Verbosity:");
        foreach (var count in summary.VerbosityCounts)
        {
            await output.WriteLineAsync($"{count.Name}\t{count.Count}");
        }
    }

    private static async Task WriteAnalyzeEventsAsync(
        IReadOnlyList<DedupedLogEvent> rows,
        AnalyzeOptions options,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        if (string.Equals(options.Format, "json", StringComparison.OrdinalIgnoreCase))
        {
            var writer = new LogEventJsonWriter();
            await writer.WriteJsonArrayAsync(rows.Select(row => row.Event).ToArray(), output, cancellationToken);
            await output.WriteLineAsync();
            return;
        }

        if (string.Equals(options.Format, "ndjson", StringComparison.OrdinalIgnoreCase))
        {
            var writer = new LogEventJsonWriter();
            await writer.WriteNdjsonAsync(ToAsyncEnumerable(rows.Select(row => row.Event)), output, cancellationToken);
            return;
        }

        foreach (var row in rows)
        {
            var prefix = row.Count > 1 ? $"[{row.Count}x] " : string.Empty;
            if (options.CleanOnly)
            {
                await output.WriteLineAsync($"{prefix}{row.Event.Category}: {row.Event.Verbosity}: {row.Event.Message}");
            }
            else
            {
                await output.WriteLineAsync($"{prefix}{row.Event.LineNumber}: [{row.Event.Category}] {row.Event.Verbosity}: {row.Event.Message}");
            }
        }
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

    private static bool TryParseAnalyzeOptions(string[] args, TextWriter error, out AnalyzeOptions options)
    {
        options = AnalyzeOptions.Default;
        var explicitCategory = TryGetOption(args, "--category=", out var category);
        var explicitMinLevel = TryGetOption(args, "--min-level=", out var minLevel);
        var explicitDedupe = TryGetOption(args, "--dedupe=", out var dedupe);
        var explicitFormat = TryGetOption(args, "--format=", out var format);

        if (TryGetOption(args, "--preset=", out var preset) && !ApplyPreset(preset, ref options, error))
        {
            return false;
        }

        var cleanOnly = HasFlag(args, "--clean-only") || options.CleanOnly;
        var normalize = HasFlag(args, "--normalize") || cleanOnly || options.Normalize;
        var summary = HasFlag(args, "--summary") || options.Summary;
        var facets = HasFlag(args, "--facets") || options.Facets;
        var events = !HasFlag(args, "--no-events");
        var explain = HasFlag(args, "--explain");
        var outputPath = TryGetOption(args, "--out=", out var rawOut) ? rawOut : null;

        var dedupeMode = options.DedupeMode;
        if (explicitDedupe && !TryParseDedupeModeValue(dedupe, out dedupeMode))
        {
            error.WriteLine($"Invalid cleanup option '--dedupe={dedupe}'. Expected one of: none, exact, normalized, burst.");
            error.WriteLine("Example: uelog analyze Game.log --dedupe=normalized");
            return false;
        }

        if (explicitFormat && !IsValidFormat(format))
        {
            error.WriteLine($"Invalid output option '--format={format}'. Expected one of: text, json, ndjson.");
            error.WriteLine("Example: uelog analyze Game.log --format=ndjson");
            return false;
        }

        var resolvedFormat = explicitFormat ? format : options.Format;
        var resolvedMinLevel = explicitMinLevel ? minLevel : options.Query.MinVerbosity;
        if (!string.IsNullOrWhiteSpace(resolvedMinLevel) && !IsValidVerbosity(resolvedMinLevel))
        {
            error.WriteLine($"Invalid filter option '--min-level={resolvedMinLevel}'. Expected one of: Fatal, Error, Warning, Display, Log, Verbose, VeryVerbose.");
            error.WriteLine("Example: uelog analyze Game.log --min-level=Warning");
            return false;
        }

        if (!TryParseLimit(args, error, out var limit))
        {
            return false;
        }

        var includedCategories = explicitCategory ? SplitCsv(category) : options.Query.IncludedCategories;
        var excludedCategory = TryGetOption(args, "--exclude-category=", out var rawExcluded) ? rawExcluded : null;
        var contains = TryGetOption(args, "--contains=", out var rawContains)
            ? rawContains
            : TryGetOption(args, "--filter=", out var rawFilter)
                ? rawFilter
                : options.Query.ContainsText;

        options = new AnalyzeOptions(
            CleanOnly: cleanOnly,
            Normalize: normalize,
            DedupeMode: dedupeMode,
            Summary: summary,
            Facets: facets,
            Events: events,
            Format: resolvedFormat,
            OutputPath: outputPath,
            Limit: limit,
            Explain: explain,
            Query: new LogQuery(
                IncludedCategories: includedCategories,
                ExcludedCategories: SplitCsv(excludedCategory),
                MinVerbosity: resolvedMinLevel,
                ContainsText: contains,
                Since: ParseTimeSpanOption(args, "--since="),
                Until: ParseTimeSpanOption(args, "--until="),
                ExcludeProfileNoise: args.Any(a => a.StartsWith("--profile=", StringComparison.OrdinalIgnoreCase))));

        return true;
    }

    private static bool ApplyPreset(string preset, ref AnalyzeOptions options, TextWriter error)
    {
        var normalized = preset.ToLowerInvariant();
        options = normalized switch
        {
            "triage" => options with { Summary = true, Facets = true, Query = options.Query with { MinVerbosity = "Warning" } },
            "clean" => options with { CleanOnly = true, Normalize = true, DedupeMode = DedupeMode.Normalized },
            "errors" => options with { Query = options.Query with { MinVerbosity = "Error" } },
            "online" => options with { Query = options.Query with { IncludedCategories = ["LogOnline"] } },
            _ => options
        };

        if (normalized is "triage" or "clean" or "errors" or "online")
        {
            return true;
        }

        error.WriteLine($"Invalid workflow option '--preset={preset}'. Expected one of: triage, clean, errors, online.");
        error.WriteLine("Example: uelog analyze Game.log --preset=triage");
        return false;
    }

    private static bool TryParseLimit(string[] args, TextWriter error, out int? limit)
    {
        limit = null;
        if (!TryGetOption(args, "--limit=", out var raw))
        {
            return true;
        }

        if (int.TryParse(raw, out var parsed) && parsed >= 0)
        {
            limit = parsed;
            return true;
        }

        error.WriteLine($"Invalid output option '--limit={raw}'. Expected a non-negative integer.");
        error.WriteLine("Example: uelog analyze Game.log --limit=100");
        return false;
    }

    private static bool TryParseDedupeModeValue(string raw, out DedupeMode mode)
    {
        return Enum.TryParse(raw, ignoreCase: true, out mode) && Enum.IsDefined(mode);
    }

    private static bool IsValidFormat(string value)
    {
        return string.Equals(value, "text", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "ndjson", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValidVerbosity(string value)
    {
        return value.Equals("Fatal", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Error", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Warning", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Display", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Log", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Verbose", StringComparison.OrdinalIgnoreCase)
            || value.Equals("VeryVerbose", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasFlag(string[] args, string flag)
    {
        return args.Any(arg => string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryGetOption(string[] args, string prefix, out string value)
    {
        var raw = args.FirstOrDefault(a => a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        value = raw?.Split('=', 2)[1] ?? string.Empty;
        return raw is not null;
    }

    private static string DescribePipeline(AnalyzeOptions options)
    {
        var parts = new List<string> { "parse" };
        if (options.Normalize)
        {
            parts.Add("normalize");
        }

        if (options.DedupeMode != DedupeMode.None)
        {
            parts.Add($"dedupe({options.DedupeMode.ToString().ToLowerInvariant()})");
        }

        parts.Add($"filter({DescribeFilter(options.Query)})");
        parts.Add(options.Format.ToLowerInvariant());
        return string.Join(" -> ", parts);
    }

    private static string DescribeFilter(LogQuery query)
    {
        var parts = new List<string>();
        if (query.IncludedCategories.Count > 0)
        {
            parts.Add($"category={string.Join(',', query.IncludedCategories)}");
        }

        if (!string.IsNullOrWhiteSpace(query.MinVerbosity))
        {
            parts.Add($"min={query.MinVerbosity}");
        }

        if (!string.IsNullOrWhiteSpace(query.ContainsText))
        {
            parts.Add($"contains={query.ContainsText}");
        }

        if (query.Since is not null)
        {
            parts.Add($"since={query.Since.Value:c}");
        }

        if (query.Until is not null)
        {
            parts.Add($"until={query.Until.Value:c}");
        }

        return parts.Count == 0 ? "all" : string.Join(',', parts);
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

    private static async IAsyncEnumerable<LogEvent> ToAsyncEnumerable(IEnumerable<LogEvent> events)
    {
        foreach (var e in events)
        {
            yield return e;
            await Task.Yield();
        }
    }

    private sealed record AnalyzeOptions(
        bool CleanOnly,
        bool Normalize,
        DedupeMode DedupeMode,
        bool Summary,
        bool Facets,
        bool Events,
        string Format,
        string? OutputPath,
        int? Limit,
        bool Explain,
        LogQuery Query)
    {
        public static AnalyzeOptions Default { get; } = new(
            CleanOnly: false,
            Normalize: false,
            DedupeMode: DedupeMode.None,
            Summary: false,
            Facets: false,
            Events: true,
            Format: "text",
            OutputPath: null,
            Limit: null,
            Explain: false,
            Query: LogQuery.Empty);
    }
}
