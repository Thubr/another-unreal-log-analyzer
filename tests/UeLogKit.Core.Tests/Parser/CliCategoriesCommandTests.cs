using System.Text.Json;
using UeLogKit.Cli;

namespace UeLogKit.Core.Tests.Parser;

public sealed class CliCategoriesCommandTests
{
    [Fact]
    public async Task Categories_text_lists_categories_with_counts_descending()
    {
        var path = WriteSyntheticLog();
        var stdout = new StringWriter();

        var code = await CliApp.RunAsync(["categories", path], stdout, new StringWriter(), default);

        Assert.Equal(0, code);
        var lines = stdout.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Equal("LogOnline\t2", lines[0]);
        Assert.Equal("LogNet\t1", lines[1]);
        Assert.Equal("LogSlate\t1", lines[2]);
    }

    [Fact]
    public async Task Categories_json_outputs_machine_readable_facets()
    {
        var path = WriteSyntheticLog();
        var stdout = new StringWriter();

        var code = await CliApp.RunAsync(["categories", path, "--format=json"], stdout, new StringWriter(), default);

        Assert.Equal(0, code);
        using var doc = JsonDocument.Parse(stdout.ToString());
        Assert.Equal(4, doc.RootElement.GetProperty("totalEvents").GetInt32());
        Assert.Equal("LogOnline", doc.RootElement.GetProperty("categoryCounts")[0].GetProperty("name").GetString());
        Assert.Equal(2, doc.RootElement.GetProperty("categoryCounts")[0].GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task Categories_profile_excludes_noise_when_requested()
    {
        var path = WriteSyntheticLog();
        var stdout = new StringWriter();

        var code = await CliApp.RunAsync(["categories", path, "--profile=ue-default"], stdout, new StringWriter(), default);

        Assert.Equal(0, code);
        Assert.DoesNotContain("LogSlate", stdout.ToString());
    }

    private static string WriteSyntheticLog()
    {
        var path = Path.GetTempFileName();
        File.WriteAllLines(path,
        [
            "LogOnline: Warning: JoinSession failed",
            "LogOnline: Error: JoinSession failed again",
            "LogNet: Display: Connected",
            "LogSlate: Warning: Synthetic UI noise"
        ]);
        return path;
    }
}
