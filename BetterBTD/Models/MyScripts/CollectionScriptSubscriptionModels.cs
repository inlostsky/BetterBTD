namespace BetterBTD.Models.MyScripts;

public sealed class CollectionScriptSubscriptionDocument
{
    public string Kind { get; set; } = "better-btd/collection-subscription";

    public int Version { get; set; } = 1;

    public List<CollectionScriptSubscriptionScriptDocument> Scripts { get; set; } = [];

    public Dictionary<string, string> Bindings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class CollectionScriptSubscriptionScriptDocument
{
    public string CanonicalScriptId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;
}
