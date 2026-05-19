using Terminal.Gui;
using UeLogKit.Core;

namespace UeLogKit.Cli.Inspect;

public sealed class InspectTerminalUi
{
    private readonly string[] _levels = ["Any", "Display", "Warning", "Error", "Fatal"];

    public void Run(InspectViewModel model)
    {
        Application.Init();
        try
        {
            var top = Application.Top;
            top.StatusBar = new StatusBar(
            [
                new StatusItem(Key.CtrlMask | Key.Q, "~^Q~ Quit", () => Application.RequestStop()),
                new StatusItem(Key.CtrlMask | Key.E, "~^E~ Export", () => ShowExportCommand(model))
            ]);

            var window = new Window($"uelog inspect - {model.LogPath}")
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            var status = new Label()
            {
                X = 1,
                Y = 0,
                Width = Dim.Fill(),
                Height = 1
            };

            var searchLabel = new Label("Search:")
            {
                X = 1,
                Y = 2
            };
            var search = new TextField("")
            {
                X = Pos.Right(searchLabel) + 1,
                Y = 2,
                Width = Dim.Percent(35)
            };

            var categories = new ListView()
            {
                X = 1,
                Y = 4,
                Width = Dim.Percent(28),
                Height = Dim.Fill(1),
                AllowsMarking = false
            };

            var levels = new ListView(_levels)
            {
                X = Pos.Right(categories) + 1,
                Y = 4,
                Width = 14,
                Height = 7,
                AllowsMarking = false
            };

            var results = new ListView()
            {
                X = Pos.Right(levels) + 1,
                Y = 4,
                Width = Dim.Percent(42),
                Height = Dim.Fill(1),
                AllowsMarking = false
            };

            var details = new TextView()
            {
                X = Pos.Right(results) + 1,
                Y = 4,
                Width = Dim.Fill(1),
                Height = Dim.Fill(1),
                ReadOnly = true,
                WordWrap = true
            };

            void Refresh()
            {
                status.Text = BuildStatus(model);
                categories.SetSource(BuildCategoryRows(model));
                results.SetSource(BuildEventRows(model.Results));
                UpdateDetails(model, results.SelectedItem, details);
                window.SetNeedsDisplay();
            }

            search.TextChanged += _ =>
            {
                model.SetContainsText(search.Text.ToString());
                Refresh();
            };

            categories.OpenSelectedItem += _ =>
            {
                var category = CategoryAt(model, categories.SelectedItem);
                if (category is not null)
                {
                    model.ToggleCategory(category);
                    Refresh();
                }
            };

            levels.OpenSelectedItem += _ =>
            {
                var selected = levels.SelectedItem >= 0 && levels.SelectedItem < _levels.Length
                    ? _levels[levels.SelectedItem]
                    : "Any";
                model.SetMinVerbosity(selected == "Any" ? null : selected);
                Refresh();
            };

            results.SelectedItemChanged += _ => UpdateDetails(model, results.SelectedItem, details);
            results.OpenSelectedItem += _ => UpdateDetails(model, results.SelectedItem, details);

            window.Add(status, searchLabel, search, categories, levels, results, details);
            top.Add(window);
            Refresh();

            Application.Run();
        }
        finally
        {
            Application.Shutdown();
        }
    }

    private static void ShowExportCommand(InspectViewModel model)
    {
        MessageBox.Query("Export normalized command", model.ExportFilterCommand(), "OK");
    }

    private static string BuildStatus(InspectViewModel model)
    {
        var facets = model.Facets;
        return $"Events {facets.TotalEvents} | Matches {model.Results.Count} | Warnings {facets.WarningCount} | Errors {facets.ErrorCount}";
    }

    private static List<string> BuildCategoryRows(InspectViewModel model)
    {
        return model.Facets.CategoryCounts
            .Select(count =>
            {
                var selected = model.Query.IncludedCategories.Any(category => string.Equals(category, count.Name, StringComparison.OrdinalIgnoreCase));
                return $"{(selected ? "[x]" : "[ ]")} {count.Name} {count.Count}";
            })
            .ToList();
    }

    private static List<string> BuildEventRows(IReadOnlyList<LogEvent> events)
    {
        return events
            .Take(500)
            .Select(e => $"{e.LineNumber,6} {Short(e.Category, 18),-18} {Short(e.Verbosity, 7),-7} {Short(e.Message, 100)}")
            .ToList();
    }

    private static string? CategoryAt(InspectViewModel model, int index)
    {
        return index >= 0 && index < model.Facets.CategoryCounts.Count
            ? model.Facets.CategoryCounts[index].Name
            : null;
    }

    private static void UpdateDetails(InspectViewModel model, int index, TextView details)
    {
        if (index < 0 || index >= model.Results.Count)
        {
            details.Text = "No event selected.";
            return;
        }

        var e = model.Results[index];
        details.Text = string.Join(Environment.NewLine,
        [
            $"Line: {e.LineNumber}",
            $"Timestamp: {e.Timestamp?.ToString("O") ?? "<none>"}",
            $"Category: {e.Category}",
            $"Verbosity: {e.Verbosity}",
            string.Empty,
            e.Message,
            string.Empty,
            "Continuation:",
            e.ContinuationLines.Count == 0 ? "<none>" : string.Join(Environment.NewLine, e.ContinuationLines)
        ]);
    }

    private static string Short(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..Math.Max(0, maxLength - 1)] + "...";
    }
}
