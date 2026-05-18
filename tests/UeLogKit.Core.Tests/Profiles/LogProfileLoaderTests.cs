using UeLogKit.Core.Profiles;

namespace UeLogKit.Core.Tests.Profiles;

public sealed class LogProfileLoaderTests
{
    [Fact]
    public void Loads_builtin_default_profile()
    {
        var profile = LogProfileLoader.Load("ue-default");

        Assert.Equal("ue-default", profile.Name);
        Assert.Contains("LogSlate", profile.NoiseCategories);
        Assert.Contains("LogOnline", profile.ImportantCategories);
        Assert.Contains("NetworkFailure", profile.ImportantPatterns);
    }

    [Fact]
    public void Loads_custom_yaml_profile_file()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, """
            name: synthetic-custom
            version: "1"
            noise_categories:
              - LogSyntheticNoise
            important_categories:
              - LogSyntheticImportant
            important_patterns:
              - SyntheticFailure
            output_preferences:
              format: compact
            """);

        var profile = LogProfileLoader.Load(path);

        Assert.Equal("synthetic-custom", profile.Name);
        Assert.Equal("1", profile.Version);
        Assert.Contains("LogSyntheticNoise", profile.NoiseCategories);
        Assert.Contains("LogSyntheticImportant", profile.ImportantCategories);
        Assert.Contains("SyntheticFailure", profile.ImportantPatterns);
        Assert.Equal("compact", profile.OutputPreferences["format"]);
    }
}
