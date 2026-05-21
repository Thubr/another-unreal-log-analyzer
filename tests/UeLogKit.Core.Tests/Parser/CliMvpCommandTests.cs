using UeLogKit.Cli;
using System.Text;

namespace UeLogKit.Core.Tests.Parser;

public sealed class CliMvpCommandTests
{
    [Fact]
    public async Task Parse_json_outputs_array()
    {
        var path = WriteSyntheticLog();
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(["parse", path, "--format=json"], stdout, stderr, default);

        Assert.Equal(0, code);
        Assert.Contains("LogInit", stdout.ToString());
    }

    [Fact]
    public async Task Parse_json_normalize_redacts_identifier_values_when_requested()
    {
        var path = WriteSyntheticIdentifierLog();
        var stdout = new StringWriter();

        var code = await CliApp.RunAsync(["parse", path, "--format=json", "--normalize"], stdout, new StringWriter(), default);

        Assert.Equal(0, code);
        var text = stdout.ToString();
        Assert.Contains("Session=<session_id>", text);
        Assert.Contains("\"Session\": \"<session_id>\"", text);
        Assert.Contains("\"Ticket\": \"<ticket_id>\"", text);
        Assert.DoesNotContain("Session-ABC123", text);
        Assert.DoesNotContain("Ticket-98765", text);
    }

    [Fact]
    public async Task Parse_ndjson_normalize_redacts_identifier_values_when_requested()
    {
        var path = WriteSyntheticIdentifierLog();
        var stdout = new StringWriter();

        var code = await CliApp.RunAsync(["parse", path, "--format=ndjson", "--normalize"], stdout, new StringWriter(), default);

        Assert.Equal(0, code);
        var text = stdout.ToString();
        Assert.Contains("Session=<session_id>", text);
        Assert.Contains("\"Session\":\"<session_id>\"", text);
        Assert.Contains("\"Ticket\":\"<ticket_id>\"", text);
        Assert.DoesNotContain("Session-ABC123", text);
        Assert.DoesNotContain("Ticket-98765", text);
    }

    [Fact]
    public async Task Parse_without_normalize_preserves_observed_identifier_values()
    {
        var path = WriteSyntheticIdentifierLog();
        var stdout = new StringWriter();

        var code = await CliApp.RunAsync(["parse", path, "--format=json"], stdout, new StringWriter(), default);

        Assert.Equal(0, code);
        var text = stdout.ToString();
        Assert.Contains("Session-ABC123", text);
        Assert.Contains("Ticket-98765", text);
    }

    [Fact]
    public async Task Summarize_reports_warning_and_error_counts()
    {
        var path = WriteSyntheticLog();
        var stdout = new StringWriter();

        var code = await CliApp.RunAsync(["summarize", path], stdout, new StringWriter(), default);

        Assert.Equal(0, code);
        var text = stdout.ToString();
        Assert.Contains("Total events: 3", text);
        Assert.Contains("Warnings: 1", text);
        Assert.Contains("Errors: 1", text);
    }

    [Fact]
    public async Task Summarize_profile_reports_important_event_count()
    {
        var path = WriteSyntheticProfileLog();
        var stdout = new StringWriter();

        var code = await CliApp.RunAsync(["summarize", path, "--profile=ue-default"], stdout, new StringWriter(), default);

        Assert.Equal(0, code);
        Assert.Contains("Important events: 2", stdout.ToString());
    }

    [Fact]
    public async Task Filter_applies_category_and_min_level()
    {
        var path = WriteSyntheticLog();
        var stdout = new StringWriter();

        var code = await CliApp.RunAsync(["filter", path, "--category=LogNet", "--min-level=Warning"], stdout, new StringWriter(), default);

        Assert.Equal(0, code);
        var lines = stdout.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines);
        Assert.Contains("LogNet", lines[0]);
    }

    [Fact]
    public async Task Filter_applies_contains_text_match()
    {
        var path = WriteSyntheticLog();
        var stdout = new StringWriter();

        var code = await CliApp.RunAsync(["filter", path, "--contains=join"], stdout, new StringWriter(), default);

        Assert.Equal(0, code);
        var lines = stdout.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines);
        Assert.Contains("Join failed", lines[0]);
    }

    [Fact]
    public async Task Filter_applies_since_and_until_relative_window()
    {
        var path = WriteSyntheticLog();
        var stdout = new StringWriter();

        var code = await CliApp.RunAsync(["filter", path, "--since=00:00:00.050", "--until=00:00:00.150"], stdout, new StringWriter(), default);

        Assert.Equal(0, code);
        var lines = stdout.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines);
        Assert.Contains("Packet loss", lines[0]);
    }

    [Fact]
    public async Task Filter_normalize_redacts_identifier_values_when_requested()
    {
        var path = WriteSyntheticIdentifierLog();
        var stdout = new StringWriter();

        var code = await CliApp.RunAsync(["filter", path, "--category=LogOnline", "--normalize"], stdout, new StringWriter(), default);

        Assert.Equal(0, code);
        var text = stdout.ToString();
        Assert.Contains("Session=<session_id>", text);
        Assert.Contains("Ticket=<ticket_id>", text);
        Assert.DoesNotContain("Session-ABC123", text);
        Assert.DoesNotContain("Ticket-98765", text);
    }

    [Fact]
    public async Task Filter_without_normalize_preserves_observed_identifier_values()
    {
        var path = WriteSyntheticIdentifierLog();
        var stdout = new StringWriter();

        var code = await CliApp.RunAsync(["filter", path, "--category=LogOnline"], stdout, new StringWriter(), default);

        Assert.Equal(0, code);
        var text = stdout.ToString();
        Assert.Contains("Session-ABC123", text);
        Assert.Contains("Ticket-98765", text);
    }

    [Fact]
    public async Task Filter_profile_excludes_profile_noise_categories()
    {
        var path = WriteSyntheticProfileLog();
        var stdout = new StringWriter();

        var code = await CliApp.RunAsync(["filter", path, "--profile=ue-default"], stdout, new StringWriter(), default);

        Assert.Equal(0, code);
        var text = stdout.ToString();
        Assert.Contains("LogOnline", text);
        Assert.DoesNotContain("LogSlate", text);
    }

    [Fact]
    public async Task Analyze_default_outputs_readable_event_rows()
    {
        var path = WriteSyntheticLog();
        var stdout = new StringWriter();

        var code = await CliApp.RunAsync(["analyze", path], stdout, new StringWriter(), default);

        Assert.Equal(0, code);
        var lines = stdout.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Equal(3, lines.Length);
        Assert.Contains("1: [LogInit] Display: Start", lines);
        Assert.Contains("2: [LogNet] Warning: Packet loss", lines);
    }

    [Fact]
    public async Task Analyze_dedupes_before_filtering()
    {
        var path = WriteSyntheticDedupeLog();
        var stdout = new StringWriter();

        var code = await CliApp.RunAsync(["analyze", path, "--dedupe=normalized", "--category=LogOnline"], stdout, new StringWriter(), default);

        Assert.Equal(0, code);
        var lines = stdout.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var line = Assert.Single(lines);
        Assert.Equal("[2x] 3: [LogOnline] Warning: Session=<session_id> Ticket=<ticket_id>", line);
    }

    [Fact]
    public async Task Analyze_clean_only_normalizes_and_dedupes_without_filters()
    {
        var path = WriteSyntheticDedupeLog();
        var stdout = new StringWriter();

        var code = await CliApp.RunAsync(["analyze", path, "--clean-only", "--dedupe=normalized"], stdout, new StringWriter(), default);

        Assert.Equal(0, code);
        var lines = stdout.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Contains("[2x] LogNet: Warning: Polling state", lines);
        Assert.Contains("[2x] LogOnline: Warning: Session=<session_id> Ticket=<ticket_id>", lines);
    }

    [Fact]
    public async Task Analyze_summary_and_facets_print_counts()
    {
        var path = WriteSyntheticLog();
        var stdout = new StringWriter();

        var code = await CliApp.RunAsync(["analyze", path, "--summary", "--facets"], stdout, new StringWriter(), default);

        Assert.Equal(0, code);
        var text = stdout.ToString();
        Assert.Contains("Total events: 3", text);
        Assert.Contains("Warnings: 1", text);
        Assert.Contains("Errors: 1", text);
        Assert.Contains("Categories:", text);
        Assert.Contains("LogInit\t1", text);
        Assert.Contains("Verbosity:", text);
        Assert.Contains("Warning\t1", text);
    }

    [Fact]
    public async Task Analyze_json_outputs_structured_events()
    {
        var path = WriteSyntheticIdentifierLog();
        var stdout = new StringWriter();

        var code = await CliApp.RunAsync(["analyze", path, "--normalize", "--format=json"], stdout, new StringWriter(), default);

        Assert.Equal(0, code);
        var text = stdout.ToString();
        Assert.StartsWith("[", text.TrimStart());
        Assert.Contains("\"Category\": \"LogOnline\"", text);
        Assert.Contains("Session=<session_id>", text);
        Assert.DoesNotContain("Session-ABC123", text);
    }

    [Fact]
    public async Task Analyze_ndjson_outputs_one_structured_event_per_line()
    {
        var path = WriteSyntheticLog();
        var stdout = new StringWriter();

        var code = await CliApp.RunAsync(["analyze", path, "--format=ndjson", "--limit=2"], stdout, new StringWriter(), default);

        Assert.Equal(0, code);
        var lines = stdout.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Equal(2, lines.Length);
        Assert.All(lines, line => Assert.StartsWith("{", line));
    }

    [Fact]
    public async Task Analyze_explain_prints_resolved_pipeline()
    {
        var path = WriteSyntheticLog();
        var stdout = new StringWriter();

        var code = await CliApp.RunAsync(["analyze", path, "--normalize", "--dedupe=normalized", "--category=LogNet", "--min-level=Warning", "--explain"], stdout, new StringWriter(), default);

        Assert.Equal(0, code);
        Assert.Contains("Pipeline: parse -> normalize -> dedupe(normalized) -> filter(category=LogNet,min=Warning) -> text", stdout.ToString());
    }

    [Theory]
    [InlineData("--dedupe=surprise", "Invalid cleanup option '--dedupe=surprise'. Expected one of: none, exact, normalized, burst.")]
    [InlineData("--format=xml", "Invalid output option '--format=xml'. Expected one of: text, json, ndjson.")]
    [InlineData("--min-level=Loud", "Invalid filter option '--min-level=Loud'. Expected one of: Fatal, Error, Warning, Display, Log, Verbose, VeryVerbose.")]
    public async Task Analyze_rejects_invalid_pipeline_options(string option, string expectedError)
    {
        var path = WriteSyntheticLog();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(["analyze", path, option], new StringWriter(), stderr, default);

        Assert.Equal(1, code);
        Assert.Contains(expectedError, stderr.ToString());
    }

    [Fact]
    public async Task Clean_outputs_simplified_lines()
    {
        var path = WriteSyntheticLog();
        var stdout = new StringWriter();

        var code = await CliApp.RunAsync(["clean", path], stdout, new StringWriter(), default);

        Assert.Equal(0, code);
        Assert.Contains("LogInit: Display:", stdout.ToString());
    }

    [Fact]
    public async Task Clean_outputs_normalized_identifier_placeholders()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, "LogOnline: Warning: Event=Session.Join Session=Session-ABC123 Ticket=\"Ticket-98765\" UserId=User-42\n");
        var stdout = new StringWriter();

        var code = await CliApp.RunAsync(["clean", path], stdout, new StringWriter(), default);

        Assert.Equal(0, code);
        var text = stdout.ToString();
        Assert.Contains("Session=<session_id>", text);
        Assert.Contains("Ticket=\"<ticket_id>\"", text);
        Assert.Contains("UserId=<user_id>", text);
        Assert.DoesNotContain("Session-ABC123", text);
        Assert.DoesNotContain("Ticket-98765", text);
        Assert.DoesNotContain("User-42", text);
    }

    [Fact]
    public async Task Clean_exact_dedupe_collapses_identical_lines()
    {
        var path = WriteSyntheticDedupeLog();
        var stdout = new StringWriter();

        var code = await CliApp.RunAsync(["clean", path, "--dedupe=exact"], stdout, new StringWriter(), default);

        Assert.Equal(0, code);
        var lines = stdout.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Contains("[2x] LogNet: Warning: Polling state", lines);
        Assert.Equal(2, lines.Count(line => line == "LogOnline: Warning: Session=<session_id> Ticket=<ticket_id>"));
    }

    [Fact]
    public async Task Clean_normalized_dedupe_collapses_identifier_variants()
    {
        var path = WriteSyntheticDedupeLog();
        var stdout = new StringWriter();

        var code = await CliApp.RunAsync(["clean", path, "--dedupe=normalized"], stdout, new StringWriter(), default);

        Assert.Equal(0, code);
        var lines = stdout.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Contains("[2x] LogNet: Warning: Polling state", lines);
        Assert.Contains("[2x] LogOnline: Warning: Session=<session_id> Ticket=<ticket_id>", lines);
    }

    [Fact]
    public async Task Clean_normalized_dedupe_matches_golden_output()
    {
        var path = WriteSyntheticDedupeLog();
        var stdout = new StringWriter();
        var expectedPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "expected_clean_normalized_dedupe.txt");

        var code = await CliApp.RunAsync(["clean", path, "--dedupe=normalized"], stdout, new StringWriter(), default);

        Assert.Equal(0, code);
        Assert.Equal(
            File.ReadAllText(expectedPath).ReplaceLineEndings().TrimEnd(),
            stdout.ToString().ReplaceLineEndings().TrimEnd());
    }

    [Fact]
    public async Task Clean_rejects_invalid_dedupe_mode()
    {
        var path = WriteSyntheticLog();
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(["clean", path, "--dedupe=surprise"], stdout, stderr, default);

        Assert.Equal(1, code);
        Assert.Contains("Invalid dedupe mode 'surprise'.", stderr.ToString());
    }

    [Fact]
    public async Task Clean_rejects_numeric_dedupe_mode()
    {
        var path = WriteSyntheticLog();
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(["clean", path, "--dedupe=42"], stdout, stderr, default);

        Assert.Equal(1, code);
        Assert.Contains("Invalid dedupe mode '42'.", stderr.ToString());
    }

    private static string WriteSyntheticLog()
    {
        var path = Path.GetTempFileName();
        var content = new StringBuilder()
            .AppendLine("[2026.01.01-12.00.00:000][1]LogInit: Display: Start")
            .AppendLine("[2026.01.01-12.00.00:100][2]LogNet: Warning: Packet loss")
            .AppendLine("LogOnline: Error: Join failed")
            .ToString();
        File.WriteAllText(path, content);
        return path;
    }

    private static string WriteSyntheticIdentifierLog()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, "LogOnline: Warning: Event=Session.Join Session=Session-ABC123 Ticket=Ticket-98765\n");
        return path;
    }

    private static string WriteSyntheticDedupeLog()
    {
        var path = Path.GetTempFileName();
        var content = new StringBuilder()
            .AppendLine("LogNet: Warning: Polling state")
            .AppendLine("LogNet: Warning: Polling state")
            .AppendLine("LogOnline: Warning: Session=Session-ABC123 Ticket=Ticket-1")
            .AppendLine("LogOnline: Warning: Session=Session-XYZ789 Ticket=Ticket-2")
            .ToString();
        File.WriteAllText(path, content);
        return path;
    }

    private static string WriteSyntheticProfileLog()
    {
        var path = Path.GetTempFileName();
        var content = new StringBuilder()
            .AppendLine("LogSlate: Display: Synthetic UI noise")
            .AppendLine("LogOnline: Warning: JoinSession failed")
            .AppendLine("LogNet: Warning: NetworkFailure: PendingConnectionFailure")
            .ToString();
        File.WriteAllText(path, content);
        return path;
    }
}
