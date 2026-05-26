using BetterBTD.Models.GameElements;

namespace BetterBTD.Models.MyScripts;

public sealed class BlackBorderScriptSubscriptionDocument
{
    public string Kind { get; set; } = "better-btd/blackborder-subscription";

    public int Version { get; set; } = 1;

    public string SubscriptionType { get; set; } = string.Empty;

    public string MapCategory { get; set; } = string.Empty;

    public string Map { get; set; } = string.Empty;

    public List<CollectionScriptSubscriptionScriptDocument> Scripts { get; set; } = [];

    public Dictionary<string, string> Bindings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class BlackBorderSubscriptionDescriptor
{
    public required BlackBorderSubscriptionExportType ExportType { get; init; }

    public BlackBorderMapCategory? Category { get; init; }

    public GameMapType? Map { get; init; }
}
