namespace UeLogKit.Core.Profiles;

public static class ProfileCatalog
{
    private static readonly string BuiltInProfilesDir = Path.Combine(AppContext.BaseDirectory, "Profiles", "BuiltIn");

    public static AnalysisProfile Get(string? name, string? profilesDirectory = null)
    {
        var requested = string.IsNullOrWhiteSpace(name) ? "ue-default" : name.Trim();
        var loader = new ProfileLoader(profilesDirectory ?? BuiltInProfilesDir);
        return loader.Load(requested);
    }
}
