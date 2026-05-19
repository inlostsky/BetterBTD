using BetterBTD.Models.ScriptEditor;

namespace BetterBTD.Tests.Services;

public sealed class ScriptTagCatalogTests
{
    [Theory]
    [InlineData("黑框", "black-border")]
    [InlineData("black border", "black-border")]
    [InlineData("BB", "black-border")]
    [InlineData("竞速", "race")]
    [InlineData("Race", "race")]
    [InlineData("首领", "boss")]
    public void ResolveStoredValue_BuiltInAlias_ReturnsCanonicalKey(string input, string expected)
    {
        var actual = ScriptTagCatalog.ResolveStoredValue(input);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void NormalizeStoredTags_DeduplicatesBuiltInAliasesAndPreservesCustomTag()
    {
        var actual = ScriptTagCatalog.NormalizeStoredTags(["黑框", "black-border", " 自定义标签 ", "自定义标签"]);

        Assert.Equal(["black-border", "自定义标签"], actual);
    }

    [Fact]
    public void GetDisplayName_BuiltInKey_ReturnsBilingualLabel()
    {
        var actual = ScriptTagCatalog.GetDisplayName("gold-bloon");

        Assert.Equal("金气球 / Gold Bloon", actual);
    }
}
