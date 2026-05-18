using UeLogKit.Core;
using UeLogKit.Core.Dedupe;
using UeLogKit.Core.Normalization;
using UeLogKit.Core.Parser;
using UeLogKit.Core.Profiles;

namespace UeLogKit.Cli;

public static class CliApp
{
    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        if (args.Length < 2)
        {
            await error.WriteLineAsync("Usage: uelog <parse|summarize|filter|clean> <logPath> [options]");
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
        var warnings = events.Count(e => string.Equals(e.Verbosity, "Warning", StringComparison.OrdinalIgnoreCase));
        var errors = events.Count(e => string.Equals(e.Verbosity, "Error", StringComparison.OrdinalIgnoreCase) || string.Equals(e.Verbosity, "Fatal", StringComparison.OrdinalIgnoreCase));

        await output.WriteLineAsync($"Total events: {events.Count}");
        await output.WriteLineAsync($"Warnings: {warnings}");
        await output.WriteLineAsync($"Errors: {errors}");
        if (profile is not null)
        {
            await output.WriteLineAsync($"Important events: {events.Count(e => IsImportant(e, profile))}");
        }

        return 0;
    }

    private static async Task<int> RunFilterAsync(ILogEventSource parser, LogInput input, string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var category = args.FirstOrDefault(a => a.StartsWith("--category=", StringComparison.OrdinalIgnoreCase))?.Split('=')[1];
        var minLevel = args.FirstOrDefault(a => a.StartsWith("--min-level=", StringComparison.OrdinalIgnoreCase))?.Split('=')[1];
        var contains = args.FirstOrDefault(a => a.StartsWith("--contains=", StringComparison.OrdinalIgnoreCase))?.Split('=')[1];
        var since = ParseTimeSpanOption(args, "--since=");
        var until = ParseTimeSpanOption(args, "--until=");
        var normalize = args.Any(a => string.Equals(a, "--normalize", StringComparison.OrdinalIgnoreCase));
        if (!TryLoadProfile(args, error, out var profile))
        {
            return 1;
        }

        var events = await ToListAsync(parser.ReadEventsAsync(input, new ParserOptions(), cancellationToken), cancellationToken);
        var filtered = events.Where(e => category is null || string.Equals(e.Category, category, StringComparison.OrdinalIgnoreCase));
        if (profile is not null)
        {
            filtered = filtered.Where(e => !ContainsIgnoreCase(profile.NoiseCategories, e.Category));
        }
        if (minLevel is not null)
        {
            filtered = filtered.Where(e => SeverityRank(e.Verbosity) >= SeverityRank(minLevel));
        }
        if (!string.IsNullOrWhiteSpace(contains))
        {
            filtered = filtered.Where(e => e.Message.Contains(contains, StringComparison.OrdinalIgnoreCase));
        }
        if (since is not null || until is not null)
        {
            var baseTime = events.Where(e => e.Timestamp is not null).Select(e => e.Timestamp!.Value).DefaultIfEmpty().Min();
            if (baseTime != default)
            {
                filtered = filtered.Where(e =>
                {
                    if (e.Timestamp is null)
                    {
                        return false;
                    }

                    var offset = e.Timestamp.Value - baseTime;
                    var matchesSince = since is null || offset >= since.Value;
                    var matchesUntil = until is null || offset <= until.Value;
                    return matchesSince && matchesUntil;
                });
            }
        }

        var normalizer = normalize ? new LogEventNormalizer() : null;
        foreach (var e in filtered)
        {
            var outputEvent = normalizer is null ? e : normalizer.Normalize(e);
            await output.WriteLineAsync($"{outputEvent.LineNumber}: [{outputEvent.Category}] {outputEvent.Verbosity}: {outputEvent.Message}");
        }

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

    private static int SeverityRank(string verbosity) => verbosity.ToLowerInvariant() switch
    {
        "fatal" => 5,
        "error" => 4,
        "warning" => 3,
        "display" => 2,
        _ => 1
    };

    private static TimeSpan? ParseTimeSpanOption(string[] args, string prefix)
    {
        var raw = args.FirstOrDefault(a => a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))?.Split('=')[1];
        if (raw is null)
        {
            return null;
        }

        return TimeSpan.TryParse(raw, out var parsed) ? parsed : null;
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
