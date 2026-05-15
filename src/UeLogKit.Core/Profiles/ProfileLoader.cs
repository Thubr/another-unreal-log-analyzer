namespace UeLogKit.Core.Profiles;

internal sealed class ProfileLoader
{
    private readonly string _root;
    private readonly Dictionary<string, ParsedProfile> _parsed = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AnalysisProfile> _resolved = new(StringComparer.OrdinalIgnoreCase);

    public ProfileLoader(string root)
    {
        _root = root;
    }

    public AnalysisProfile Load(string name)
    {
        if (_resolved.TryGetValue(name, out var cached)) return cached;

        var parsed = ReadParsed(name);
        AnalysisProfile result;
        if (string.IsNullOrWhiteSpace(parsed.Extends))
        {
            result = ToAnalysisProfile(parsed);
        }
        else
        {
            var parent = Load(parsed.Extends!);
            result = Merge(parent, parsed);
        }

        _resolved[name] = result;
        return result;
    }

    private ParsedProfile ReadParsed(string name)
    {
        if (_parsed.TryGetValue(name, out var p)) return p;

        var path = Path.Combine(_root, $"{name}.yaml");
        if (!File.Exists(path))
        {
            if (!string.Equals(name, "ue-default", StringComparison.OrdinalIgnoreCase))
            {
                return ReadParsed("ue-default");
            }

            throw new FileNotFoundException($"Profile '{name}' was not found in '{_root}'.", path);
        }

        var parsed = ParseSimpleYaml(File.ReadAllLines(path));
        if (string.IsNullOrWhiteSpace(parsed.Name)) parsed = parsed with { Name = name };
        _parsed[name] = parsed;
        return parsed;
    }

    private static AnalysisProfile ToAnalysisProfile(ParsedProfile parsed)
        => new(parsed.Name, ToSet(parsed.ImportantCategories), ToSet(parsed.ErrorVerbosities), parsed.NormalizationTokens);

    private static AnalysisProfile Merge(AnalysisProfile parent, ParsedProfile child)
    {
        var important = child.ImportantCategories.Count == 0 ? parent.ImportantCategories : ToSet(child.ImportantCategories);
        var errors = child.ErrorVerbosities.Count == 0 ? parent.ErrorVerbosities : ToSet(child.ErrorVerbosities);
        var tokens = child.NormalizationTokens.Count == 0 ? parent.NormalizationTokens : child.NormalizationTokens;

        return new AnalysisProfile(child.Name, important, errors, tokens);
    }

    private static HashSet<string> ToSet(IReadOnlyList<string> values) => new(values, StringComparer.OrdinalIgnoreCase);

    private static ParsedProfile ParseSimpleYaml(IEnumerable<string> lines)
    {
        var profile = new ParsedProfile();
        string? currentList = null;

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#')) continue;

            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("- "))
            {
                var item = trimmed[2..].Trim().Trim('"');
                if (currentList is not null) AddListItem(profile, currentList, item);
                continue;
            }

            var idx = line.IndexOf(':');
            if (idx <= 0) continue;

            var key = line[..idx].Trim();
            var value = line[(idx + 1)..].Trim().Trim('"');
            if (value.Length == 0)
            {
                currentList = key;
                continue;
            }

            currentList = null;
            profile = key switch
            {
                "name" => profile with { Name = value },
                "extends" => profile with { Extends = value },
                _ => profile
            };
        }

        return profile;
    }

    private static void AddListItem(ParsedProfile profile, string listName, string value)
    {
        switch (listName)
        {
            case "importantCategories":
                profile.ImportantCategories.Add(value);
                break;
            case "errorVerbosities":
                profile.ErrorVerbosities.Add(value);
                break;
            case "normalizationTokens":
                profile.NormalizationTokens.Add(value);
                break;
        }
    }

    private sealed record ParsedProfile
    {
        public string Name { get; init; } = string.Empty;
        public string? Extends { get; init; }
        public List<string> ImportantCategories { get; } = [];
        public List<string> ErrorVerbosities { get; } = [];
        public List<string> NormalizationTokens { get; } = [];
    }
}
