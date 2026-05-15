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
    public async Task Summarize_reports_warning_and_error_counts()
    {
        var path = WriteSyntheticLog();
        var stdout = new StringWriter();

        var code = await CliApp.RunAsync(["summarize", path, "--profile=ue-online"], stdout, new StringWriter(), default);

        Assert.Equal(0, code);
        var text = stdout.ToString();
        Assert.Contains("Profile: ue-online", text);
        Assert.Contains("Total events: 3", text);
        Assert.Contains("Warnings: 1", text);
        Assert.Contains("Errors: 2", text);
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
    public async Task Clean_outputs_simplified_lines()
    {
        var path = WriteSyntheticLog();
        var stdout = new StringWriter();

        var code = await CliApp.RunAsync(["clean", path, "--dedupe=normalized"], stdout, new StringWriter(), default);

        Assert.Equal(0, code);
        Assert.Contains("LogInit: Display:", stdout.ToString());
        Assert.Equal(3, stdout.ToString().Split("\n", StringSplitOptions.RemoveEmptyEntries).Length);
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
}
