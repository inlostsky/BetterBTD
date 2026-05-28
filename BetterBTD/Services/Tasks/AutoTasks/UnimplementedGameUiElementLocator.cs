using BetterBTD.Core.AutoTasks.Runtime;
using BetterBTD.Models.AutoTasks;
using WpfPoint = System.Windows.Point;

namespace BetterBTD.Services.Tasks.AutoTasks;

internal sealed class UnimplementedGameUiElementLocator : IGameUiElementLocator
{
    private static readonly Lazy<UnimplementedGameUiElementLocator> InstanceHolder =
        new(() => new UnimplementedGameUiElementLocator());

    private UnimplementedGameUiElementLocator()
    {
    }

    public static UnimplementedGameUiElementLocator Instance => InstanceHolder.Value;

    public bool TryLocateScriptPoint(
        GameUiActionKind actionKind,
        StageEntryTarget target,
        GameUiSnapshot snapshot,
        out WpfPoint scriptPoint,
        out string failureMessage)
    {
        scriptPoint = default;
        failureMessage = $"Locator for UI action '{actionKind}' is not implemented yet.";
        return false;
    }
}
