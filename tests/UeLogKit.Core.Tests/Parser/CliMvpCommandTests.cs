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
}
