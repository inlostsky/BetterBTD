namespace BetterBTD.Models.MyScripts;

public sealed class GoldBalloonScriptSubscriptionDocument
{
    public string Kind { get; set; } = "better-btd/goldballoon-subscription";

    public int Version { get; set; } = 1;

    public List<CollectionScriptSubscriptionScriptDocument> Scripts { get; set; } = [];

    public Dictionary<string, string> Bindings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
