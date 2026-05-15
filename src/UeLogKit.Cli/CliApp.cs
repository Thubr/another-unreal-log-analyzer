using UeLogKit.Core;
using UeLogKit.Core.Parser;
using UeLogKit.Core.Analysis;
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
        var profileName = args.FirstOrDefault(a => a.StartsWith("--profile=", StringComparison.OrdinalIgnoreCase))?.Split('=')[1];
        var profile = ProfileCatalog.Get(profileName);

        var input = new LogInput(SourceId: "cli", SourcePath: logPath);
        return command switch
        {
            "parse" => await RunParseAsync(parser, input, args.Skip(2).ToArray(), output, cancellationToken),
            "summarize" => await RunSummarizeAsync(parser, input, profile, output, cancellationToken),
            "filter" => await RunFilterAsync(parser, input, args.Skip(2).ToArray(), output, cancellationToken),
            "clean" => await RunCleanAsync(parser, input, profile, args.Skip(2).ToArray(), output, cancellationToken),
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
        var writer = new LogEventJsonWriter();

        if (string.Equals(format, "ndjson", StringComparison.OrdinalIgnoreCase))
        {
            await writer.WriteNdjsonAsync(parser.ReadEventsAsync(input, new ParserOptions(), cancellationToken), output, cancellationToken);
            return 0;
        }

        var events = await ToListAsync(parser.ReadEventsAsync(input, new ParserOptions(), cancellationToken), cancellationToken);
        await writer.WriteJsonArrayAsync(events, output, cancellationToken);
        await output.WriteLineAsync();
        return 0;
    }

    private static async Task<int> RunSummarizeAsync(ILogEventSource parser, LogInput input, AnalysisProfile profile, TextWriter output, CancellationToken cancellationToken)
    {
        var events = await ToListAsync(parser.ReadEventsAsync(input, new ParserOptions(), cancellationToken), cancellationToken);
        var summary = LogSummarizer.Summarize(events, profile);

        await output.WriteLineAsync($"Profile: {profile.Name}");
        await output.WriteLineAsync($"Total events: {summary.Total}");
        await output.WriteLineAsync($"Warnings: {summary.Warnings}");
        await output.WriteLineAsync($"Errors: {summary.Errors}");
        await output.WriteLineAsync("Important timeline:");
        foreach (var line in summary.ImportantTimeline)
        {
            await output.WriteLineAsync($"- {line}");
        }
        return 0;
    }

    private static async Task<int> RunFilterAsync(ILogEventSource parser, LogInput input, string[] args, TextWriter output, CancellationToken cancellationToken)
    {
        var category = args.FirstOrDefault(a => a.StartsWith("--category=", StringComparison.OrdinalIgnoreCase))?.Split('=')[1];
        var minLevel = args.FirstOrDefault(a => a.StartsWith("--min-level=", StringComparison.OrdinalIgnoreCase))?.Split('=')[1];

        var events = await ToListAsync(parser.ReadEventsAsync(input, new ParserOptions(), cancellationToken), cancellationToken);
        var filtered = events.Where(e => category is null || string.Equals(e.Category, category, StringComparison.OrdinalIgnoreCase));
        if (minLevel is not null)
        {
            filtered = filtered.Where(e => SeverityRank(e.Verbosity) >= SeverityRank(minLevel));
        }

        foreach (var e in filtered)
        {
            await output.WriteLineAsync($"{e.LineNumber}: [{e.Category}] {e.Verbosity}: {e.Message}");
        }

        return 0;
    }

    private static async Task<int> RunCleanAsync(ILogEventSource parser, LogInput input, AnalysisProfile profile, string[] args, TextWriter output, CancellationToken cancellationToken)
    {
        var modeArg = args.FirstOrDefault(a => a.StartsWith("--dedupe=", StringComparison.OrdinalIgnoreCase))?.Split('=')[1];
        var mode = ParseDedupeMode(modeArg);
        var allEvents = await ToListAsync(parser.ReadEventsAsync(input, new ParserOptions(), cancellationToken), cancellationToken);
        var events = LogEventDeduper.Apply(allEvents, mode, profile);

        foreach (var e in events)
        {
            await output.WriteLineAsync($"{e.Category}: {e.Verbosity}: {e.Message}");
        }

        return 0;
    }

    private static DedupeMode ParseDedupeMode(string? value) => value?.ToLowerInvariant() switch
    {
        "exact" => DedupeMode.Exact,
        "normalized" => DedupeMode.Normalized,
        "burst" => DedupeMode.Burst,
        _ => DedupeMode.None
    };

    private static int SeverityRank(string verbosity) => verbosity.ToLowerInvariant() switch
    {
        "fatal" => 5,
        "error" => 4,
        "warning" => 3,
        "display" => 2,
        _ => 1
    };

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
