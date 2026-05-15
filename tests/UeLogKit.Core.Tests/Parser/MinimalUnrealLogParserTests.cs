using UeLogKit.Core.Parser;

namespace UeLogKit.Core.Tests.Parser;

public sealed class MinimalUnrealLogParserTests
{
    [Fact]
    public async Task Parses_core_lines_and_plain_fallback_in_input_order()
    {
        var parser = LogEventSourceFactory.CreateDefault();
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "synthetic_minimal_unreal.log");
        var input = new LogInput("fixture:minimal", path);
        var options = new ParserOptions();

        var events = await ReadAllAsync(parser, input, options);

        Assert.Equal(3, events.Count);
        Assert.Equal(1, events[0].LineNumber);
        Assert.Equal("LogInit", events[0].Category);
        Assert.Equal("Display", events[0].Verbosity);
        Assert.Equal("Synthetic startup complete", events[0].Message);

        Assert.Equal(2, events[1].LineNumber);
        Assert.Equal("LogNet", events[1].Category);
        Assert.Equal("Warning", events[1].Verbosity);
        Assert.Single(events[1].ContinuationLines);

        Assert.Equal(4, events[2].LineNumber);
        Assert.Equal("Unknown", events[2].Category);
        Assert.Equal("Display", events[2].Verbosity);
    }

    [Fact]
    public async Task Empty_or_newline_only_input_is_safe()
    {
        var file = Path.GetTempFileName();
        await File.WriteAllTextAsync(file, "\n\n");

        var parser = new MinimalUnrealLogParser();
        var events = await ReadAllAsync(parser, new LogInput("fixture:empty", file), new ParserOptions());

        Assert.Empty(events);
    }

    [Fact]
    public async Task Mixed_quality_input_does_not_throw()
    {
        var file = Path.GetTempFileName();
        await File.WriteAllTextAsync(file, "NotARecord\n[bad][x]Log: Warning: Message\nLogA: Error: Still parsed\n");

        var parser = new MinimalUnrealLogParser();
        var events = await ReadAllAsync(parser, new LogInput("fixture:mixed", file), new ParserOptions());

        Assert.Equal(3, events.Count);
    }

    private static async Task<List<LogEvent>> ReadAllAsync(ILogEventSource parser, LogInput input, ParserOptions options)
    {
        var list = new List<LogEvent>();
        await foreach (var e in parser.ReadEventsAsync(input, options))
        {
            list.Add(e);
        }

        return list;
    }
}
