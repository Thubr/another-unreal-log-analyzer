using UeLogKit.Cli.Inspect;

namespace UeLogKit.Core.Tests.Inspect;

public sealed class InspectViewModelTests
{
    [Fact]
    public void ToggleCategory_updates_results_and_export_command_defaults_to_normalized_output()
    {
        var events = new[]
        {
            Event(1, "LogOnline", "Warning", "Session=Session-ABC123 JoinSession failed"),
            Event(2, "LogNet", "Error", "NetworkFailure"),
            Event(3, "LogSlate", "Display", "Synthetic UI noise")
        };
        var model = new InspectViewModel("Game.log", events, profile: null);

        model.ToggleCategory("LogOnline");

        Assert.Equal(["LogOnline"], model.Query.IncludedCategories);
        var match = Assert.Single(model.Results);
        Assert.Equal("LogOnline", match.Category);
        Assert.Equal("uelog filter \"Game.log\" --category=LogOnline --normalize", model.ExportFilterCommand());
    }

    [Fact]
    public void Search_and_min_level_update_results()
    {
        var events = new[]
        {
            Event(1, "LogOnline", "Display", "JoinSession started"),
            Event(2, "LogOnline", "Warning", "JoinSession failed"),
            Event(3, "LogNet", "Error", "NetworkFailure")
        };
        var model = new InspectViewModel("Game.log", events, profile: null);

        model.SetContainsText("join");
        model.SetMinVerbosity("Warning");

        var match = Assert.Single(model.Results);
        Assert.Equal(2, match.LineNumber);
        Assert.Equal("uelog filter \"Game.log\" --min-level=Warning --contains=\"join\" --normalize", model.ExportFilterCommand());
    }

    private static LogEvent Event(int line, string category, string verbosity, string message)
    {
        return new LogEvent(
            SchemaVersion: LogEventSchemaVersion.V1,
            SourceId: "test",
            SourcePath: "Game.log",
            LineNumber: line,
            Timestamp: null,
            RelativeTime: null,
            Frame: null,
            Category: category,
            Verbosity: verbosity,
            Message: message,
            ContinuationLines: [],
            Fields: new Dictionary<string, string>(),
            RawTextHash: $"sha256:{line}");
    }
}
