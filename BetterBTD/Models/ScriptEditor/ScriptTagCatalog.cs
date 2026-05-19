using System.Text;

namespace BetterBTD.Models.ScriptEditor;

public sealed record ScriptTagDefinition(
    string Key,
    string DisplayName,
    IReadOnlyList<string> Aliases);

public static class ScriptTagCatalog
{
    public static IReadOnlyList<ScriptTagDefinition> Definitions { get; } =
    [
        new(
            "black-border",
            "黑框 / Black Border",
            ["black-border", "black border", "blackborder", "bb", "黑框"]),
        new(
            "race",
            "竞速 / Race",
            ["race", "竞速"]),
        new(
            "boss",
            "BOSS / Boss",
            ["boss", "首领", "boss活动"]),
        new(
            "collection",
            "收集 / Collection",
            ["collection", "collect", "收集", "刷收集"]),
        new(
            "gold-bloon",
            "金气球 / Gold Bloon",
            ["gold-bloon", "gold bloon", "goldbloon", "金气球"]),
        new(
            "odyssey",
            "征程 / Odyssey",
            ["odyssey", "征程"]),
        new(
            "br",
            "BR / BOSS冲锋",
            ["br", "boss rush", "BOSS冲锋"])
    ];

    private static readonly Lazy<Dictionary<string, ScriptTagDefinition>> DefinitionsByKeyHolder =
        new(() => Definitions.ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase));

    private static readonly Lazy<Dictionary<string, string>> AliasMapHolder = new(CreateAliasMap);

    public static bool TryGetDefinition(string? key, out ScriptTagDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            definition = null!;
            return false;
        }

        return DefinitionsByKeyHolder.Value.TryGetValue(key.Trim(), out definition!);
    }

    public static bool TryResolveBuiltInKey(string? value, out string key)
    {
        var normalized = NormalizeQuery(value);
        if (normalized.Length == 0)
        {
            key = string.Empty;
            return false;
        }

        return AliasMapHolder.Value.TryGetValue(normalized, out key!);
    }

    public static string ResolveStoredValue(string? value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        return TryResolveBuiltInKey(trimmed, out var key)
            ? key
            : trimmed;
    }

    public static IReadOnlyList<string> NormalizeStoredTags(IEnumerable<string>? values)
    {
        var normalizedTags = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var value in values ?? [])
        {
            var normalized = ResolveStoredValue(value);
            if (normalized.Length == 0 || !seen.Add(normalized))
            {
                continue;
            }

            normalizedTags.Add(normalized);
        }

        return normalizedTags;
    }

    public static string GetDisplayName(string? storedValue)
    {
        var trimmed = storedValue?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        return TryGetDefinition(trimmed, out var definition)
            ? definition.DisplayName
            : trimmed;
    }

    public static string NormalizeQuery(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsWhiteSpace(character) ||
                char.IsPunctuation(character) ||
                char.IsSeparator(character) ||
                character is '-' or '_' or '/')
            {
                continue;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    private static Dictionary<string, string> CreateAliasMap()
    {
        var aliasMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in Definitions)
        {
            RegisterAlias(aliasMap, definition.Key, definition.Key);
            RegisterAlias(aliasMap, definition.DisplayName, definition.Key);

            foreach (var alias in definition.Aliases)
            {
                RegisterAlias(aliasMap, alias, definition.Key);
            }
        }

        return aliasMap;
    }

    private static void RegisterAlias(Dictionary<string, string> aliasMap, string alias, string key)
    {
        var normalized = NormalizeQuery(alias);
        if (normalized.Length > 0)
        {
            aliasMap[normalized] = key;
        }
    }
}
