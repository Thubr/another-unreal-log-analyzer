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

    [Fact]
    public async Task Extracts_basic_key_value_fields_from_message()
    {
        var file = Path.GetTempFileName();
        await File.WriteAllTextAsync(
            file,
            "[2026.01.01-12.00.00:000][42]LogOnline: Warning: Event=Session.Join Result=Failed Session=<session_id> User=<user_id>\n");

        var parser = new MinimalUnrealLogParser();
        var events = await ReadAllAsync(parser, new LogInput("fixture:fields", file), new ParserOptions());

        var logEvent = Assert.Single(events);
        Assert.Equal(42, logEvent.Frame);
        Assert.Equal("Session.Join", logEvent.Fields["Event"]);
        Assert.Equal("Failed", logEvent.Fields["Result"]);
        Assert.Equal("<session_id>", logEvent.Fields["Session"]);
        Assert.Equal("<user_id>", logEvent.Fields["User"]);
    }

    [Fact]
    public async Task Extracts_quoted_key_value_fields_without_changing_message()
    {
        var file = Path.GetTempFileName();
        await File.WriteAllTextAsync(
            file,
            "LogOnline: Warning: Event=Session.Join Reason=\"Synthetic timeout\" Session=<session_id>\n");

        var parser = new MinimalUnrealLogParser();
        var events = await ReadAllAsync(parser, new LogInput("fixture:quoted-fields", file), new ParserOptions());

        var logEvent = Assert.Single(events);
        Assert.Equal("Event=Session.Join Reason=\"Synthetic timeout\" Session=<session_id>", logEvent.Message);
        Assert.Equal("Session.Join", logEvent.Fields["Event"]);
        Assert.Equal("Synthetic timeout", logEvent.Fields["Reason"]);
        Assert.Equal("<session_id>", logEvent.Fields["Session"]);
    }

    [Fact]
    public async Task Attaches_indented_json_like_payload_as_continuation()
    {
        var file = Path.GetTempFileName();
        await File.WriteAllTextAsync(
            file,
            "LogHttp: Warning: Synthetic request failed\n" +
            "  {\n" +
            "    \"status\": \"timeout\",\n" +
            "    \"requestId\": \"<ticket_id>\"\n" +
            "  }\n" +
            "LogInit: Display: Continued after payload\n");

        var parser = new MinimalUnrealLogParser();
        var events = await ReadAllAsync(parser, new LogInput("fixture:json-continuation", file), new ParserOptions());

        Assert.Equal(2, events.Count);
        Assert.Equal(4, events[0].ContinuationLines.Count);
        Assert.Contains("\"requestId\": \"<ticket_id>\"", events[0].ContinuationLines);
        Assert.Equal("LogInit", events[1].Category);
    }

    [Fact]
    public async Task Keeps_continuation_lines_empty_when_option_is_disabled()
    {
        var file = Path.GetTempFileName();
        await File.WriteAllTextAsync(
            file,
            "LogNet: Error: Synthetic disconnect\n" +
            "  Synthetic callstack line\n");

        var parser = new MinimalUnrealLogParser();
        var events = await ReadAllAsync(
            parser,
            new LogInput("fixture:no-continuation", file),
            new ParserOptions(IncludeContinuationLines: false));

        var logEvent = Assert.Single(events);
        Assert.Empty(logEvent.ContinuationLines);
    }

    [Fact]
    public async Task Parses_indented_timestamped_log_lines_as_events_not_continuations()
    {
        var file = Path.GetTempFileName();
        await File.WriteAllTextAsync(
            file,
            "[2026.01.01-12.00.00:000][1]LogInit: Display: Start\n" +
            "  [2026.01.01-12.00.01:000][2]LogSyntheticCustom: Warning: Indented event\n" +
            "    Continuation payload\n");

        var parser = new MinimalUnrealLogParser();
        var events = await ReadAllAsync(parser, new LogInput("fixture:indented-event", file), new ParserOptions());

        Assert.Equal(2, events.Count);
        Assert.Equal("LogSyntheticCustom", events[1].Category);
        Assert.Equal("Warning", events[1].Verbosity);
        Assert.Equal("Indented event", events[1].Message);
        Assert.Single(events[1].ContinuationLines);
    }

    [Fact]
    public async Task Parses_indented_log_category_lines_as_events_not_continuations()
    {
        var file = Path.GetTempFileName();
        await File.WriteAllTextAsync(
            file,
            "LogInit: Display: Start\n" +
            "  LogSyntheticRuntime: Error: Indented category event\n");

        var parser = new MinimalUnrealLogParser();
        var events = await ReadAllAsync(parser, new LogInput("fixture:indented-category", file), new ParserOptions());

        Assert.Equal(2, events.Count);
        Assert.Equal("LogSyntheticRuntime", events[1].Category);
        Assert.Equal("Error", events[1].Verbosity);
        Assert.Equal("Indented category event", events[1].Message);
    }

    [Fact]
    public async Task Parses_log_category_lines_without_explicit_verbosity_as_display()
    {
        var file = Path.GetTempFileName();
        await File.WriteAllTextAsync(
            file,
            "LogInit:  - supports SSL with OpenSSL/1.1.1t\n" +
            "[2026.05.20-17.21.24:300][  0]LogNvidiaAftermath: Aftermath initialized\n");

        var parser = new MinimalUnrealLogParser();
        var events = await ReadAllAsync(parser, new LogInput("fixture:category-message", file), new ParserOptions());

        Assert.Equal(2, events.Count);
        Assert.Equal("LogInit", events[0].Category);
        Assert.Equal("Display", events[0].Verbosity);
        Assert.Equal("- supports SSL with OpenSSL/1.1.1t", events[0].Message);
        Assert.Equal("LogNvidiaAftermath", events[1].Category);
        Assert.Equal("Display", events[1].Verbosity);
        Assert.Equal("Aftermath initialized", events[1].Message);
        Assert.Equal(0, events[1].Frame);
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
