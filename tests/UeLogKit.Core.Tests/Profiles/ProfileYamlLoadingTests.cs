using UeLogKit.Core.Profiles;

namespace UeLogKit.Core.Tests.Profiles;

public sealed class ProfileYamlLoadingTests
{
    [Fact]
    public void Builtin_profile_loads_from_yaml()
    {
        var profile = ProfileCatalog.Get("ue-online");

        Assert.Equal("ue-online", profile.Name);
        Assert.Contains("LogOnline", profile.ImportantCategories);
        Assert.Contains("Warning", profile.ErrorVerbosities);
    }

    [Fact]
    public void Inheritance_from_parent_works_for_missing_fields()
    {
        var temp = Directory.CreateTempSubdirectory();
        File.WriteAllText(Path.Combine(temp.FullName, "ue-default.yaml"), """
name: ue-default
importantCategories:
  - BaseCat
errorVerbosities:
  - Error
normalizationTokens:
  - "1"
""");
        File.WriteAllText(Path.Combine(temp.FullName, "child.yaml"), """
name: child
extends: ue-default
importantCategories:
  - ChildCat
""");

        var profile = ProfileCatalog.Get("child", temp.FullName);

        Assert.Equal("child", profile.Name);
        Assert.Contains("ChildCat", profile.ImportantCategories);
        Assert.DoesNotContain("BaseCat", profile.ImportantCategories);
        Assert.Contains("Error", profile.ErrorVerbosities); // inherited
        Assert.Contains("1", profile.NormalizationTokens); // inherited
    }
}
