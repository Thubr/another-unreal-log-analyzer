using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace UeLogKit.Core.Profiles;

public static class LogProfileLoader
{
    private static readonly IReadOnlyDictionary<string, string> BuiltInProfiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["ue-default"] = """
            name: ue-default
            version: "1"
            noise_categories:
              - LogSlate
              - LogRHI
              - LogRenderer
              - LogShaderCompilers
            important_categories:
              - LogOnline
              - LogNet
              - LogTravel
              - LogOutputDevice
            important_patterns:
              - NetworkFailure
              - TravelFailure
              - JoinSession
              - Fatal error
            output_preferences:
              summary: compact
            """,
        ["ue-online"] = """
            name: ue-online
            version: "1"
            noise_categories:
              - LogSlate
              - LogRHI
            important_categories:
              - LogOnline
              - LogNet
              - LogNetTraffic
              - LogTravel
            important_patterns:
              - JoinSession
              - NetworkFailure
              - Login failed
              - PendingConnectionFailure
            output_preferences:
              summary: online
            """
    };

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static LogProfile Load(string nameOrPath)
    {
        var yaml = File.Exists(nameOrPath)
            ? File.ReadAllText(nameOrPath)
            : BuiltInProfiles.TryGetValue(nameOrPath, out var builtIn)
                ? builtIn
                : throw new InvalidOperationException($"Unknown profile '{nameOrPath}'.");

        var dto = Deserializer.Deserialize<ProfileDto>(yaml)
            ?? throw new InvalidOperationException($"Failed to load profile '{nameOrPath}'.");

        return new LogProfile(
            Name: dto.Name ?? throw new InvalidOperationException($"Profile '{nameOrPath}' is missing a name."),
            Version: dto.Version,
            NoiseCategories: dto.NoiseCategories ?? [],
            ImportantCategories: dto.ImportantCategories ?? [],
            ImportantPatterns: dto.ImportantPatterns ?? [],
            OutputPreferences: dto.OutputPreferences ?? new Dictionary<string, string>());
    }

    private sealed class ProfileDto
    {
        public string? Name { get; set; }
        public string? Version { get; set; }
        public List<string>? NoiseCategories { get; set; }
        public List<string>? ImportantCategories { get; set; }
        public List<string>? ImportantPatterns { get; set; }
        public Dictionary<string, string>? OutputPreferences { get; set; }
    }
}
