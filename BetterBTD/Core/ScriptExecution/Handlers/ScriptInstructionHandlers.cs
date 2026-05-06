using BetterBTD.Models.ScriptEditor;
using BetterBTD.Models.ScriptExecution;

namespace BetterBTD.Core.ScriptExecution.Handlers;

public interface IScriptInstructionHandler
{
    ScriptCommandType CommandType { get; }

    Task HandleAsync(ScriptInstructionExecutionContext context, CancellationToken cancellationToken);
}

public abstract class ScriptInstructionHandlerBase : IScriptInstructionHandler
{
    public abstract ScriptCommandType CommandType { get; }

    public abstract Task HandleAsync(ScriptInstructionExecutionContext context, CancellationToken cancellationToken);
}

public sealed class ScriptInstructionHandlerRegistry
{
    private static readonly Lazy<ScriptInstructionHandlerRegistry> InstanceHolder = new(() => new ScriptInstructionHandlerRegistry());

    private readonly Dictionary<ScriptCommandType, IScriptInstructionHandler> _handlers = [];

    private ScriptInstructionHandlerRegistry()
    {
        Register(new PlaceMonkeyInstructionHandler());
        Register(new UpgradeMonkeyInstructionHandler());
        Register(new SwitchMonkeyTargetInstructionHandler());
        Register(new SetMonkeyAbilityInstructionHandler());
        Register(new SellMonkeyInstructionHandler());
        Register(new PlaceHeroInventoryInstructionHandler());
        Register(new ActivateAbilityInstructionHandler());
        Register(new NextRoundInstructionHandler());
        Register(new WaitInstructionHandler());
        Register(new ModifyMonkeyCoordinateInstructionHandler());
        Register(new CommentInstructionHandler());
    }

    public static ScriptInstructionHandlerRegistry Instance => InstanceHolder.Value;

    public void Register(IScriptInstructionHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _handlers[handler.CommandType] = handler;
    }

    public IScriptInstructionHandler GetRequiredHandler(ScriptCommandType commandType)
    {
        if (_handlers.TryGetValue(commandType, out var handler))
        {
            return handler;
        }

        throw new InvalidOperationException($"No instruction handler was registered for '{commandType}'.");
    }
}

public sealed class PlaceMonkeyInstructionHandler : ScriptInstructionHandlerBase
{
    public override ScriptCommandType CommandType => ScriptCommandType.PlaceMonkey;

    public override Task HandleAsync(ScriptInstructionExecutionContext context, CancellationToken cancellationToken)
    {
        // Placeholder for tower selection, placement, and runtime monkey state sync.
        return Task.CompletedTask;
    }
}

public sealed class UpgradeMonkeyInstructionHandler : ScriptInstructionHandlerBase
{
    public override ScriptCommandType CommandType => ScriptCommandType.UpgradeMonkey;

    public override Task HandleAsync(ScriptInstructionExecutionContext context, CancellationToken cancellationToken)
    {
        // Placeholder for selecting a placed monkey and sending upgrade inputs.
        return Task.CompletedTask;
    }
}

public sealed class SwitchMonkeyTargetInstructionHandler : ScriptInstructionHandlerBase
{
    public override ScriptCommandType CommandType => ScriptCommandType.SwitchMonkeyTarget;

    public override Task HandleAsync(ScriptInstructionExecutionContext context, CancellationToken cancellationToken)
    {
        // Placeholder for rotating targeting priorities on an existing monkey.
        return Task.CompletedTask;
    }
}

public sealed class SetMonkeyAbilityInstructionHandler : ScriptInstructionHandlerBase
{
    public override ScriptCommandType CommandType => ScriptCommandType.SetMonkeyAbility;

    public override Task HandleAsync(ScriptInstructionExecutionContext context, CancellationToken cancellationToken)
    {
        // Placeholder for selecting a monkey ability and optionally targeting a coordinate.
        return Task.CompletedTask;
    }
}

public sealed class SellMonkeyInstructionHandler : ScriptInstructionHandlerBase
{
    public override ScriptCommandType CommandType => ScriptCommandType.SellMonkey;

    public override Task HandleAsync(ScriptInstructionExecutionContext context, CancellationToken cancellationToken)
    {
        // Placeholder for selecting a monkey and sending the sell action.
        return Task.CompletedTask;
    }
}

public sealed class PlaceHeroInventoryInstructionHandler : ScriptInstructionHandlerBase
{
    public override ScriptCommandType CommandType => ScriptCommandType.PlaceHeroInventory;

    public override Task HandleAsync(ScriptInstructionExecutionContext context, CancellationToken cancellationToken)
    {
        // Placeholder for selecting hero inventory and placing the item at a target coordinate.
        return Task.CompletedTask;
    }
}

public sealed class ActivateAbilityInstructionHandler : ScriptInstructionHandlerBase
{
    public override ScriptCommandType CommandType => ScriptCommandType.ActivateAbility;

    public override Task HandleAsync(ScriptInstructionExecutionContext context, CancellationToken cancellationToken)
    {
        // Placeholder for triggering a global activated ability and optional target click.
        return Task.CompletedTask;
    }
}

public sealed class NextRoundInstructionHandler : ScriptInstructionHandlerBase
{
    public override ScriptCommandType CommandType => ScriptCommandType.NextRound;

    public override Task HandleAsync(ScriptInstructionExecutionContext context, CancellationToken cancellationToken)
    {
        // Placeholder for fast-forward / next-round actions.
        return Task.CompletedTask;
    }
}

public sealed class WaitInstructionHandler : ScriptInstructionHandlerBase
{
    public override ScriptCommandType CommandType => ScriptCommandType.Wait;

    public override Task HandleAsync(ScriptInstructionExecutionContext context, CancellationToken cancellationToken)
    {
        // Placeholder for wait conditions backed by OCR and capture services.
        return Task.CompletedTask;
    }
}

public sealed class ModifyMonkeyCoordinateInstructionHandler : ScriptInstructionHandlerBase
{
    public override ScriptCommandType CommandType => ScriptCommandType.ModifyMonkeyCoordinate;

    public override Task HandleAsync(ScriptInstructionExecutionContext context, CancellationToken cancellationToken)
    {
        // Placeholder for updating runtime coordinates of an existing monkey binding.
        return Task.CompletedTask;
    }
}

public sealed class CommentInstructionHandler : ScriptInstructionHandlerBase
{
    public override ScriptCommandType CommandType => ScriptCommandType.Comment;

    public override Task HandleAsync(ScriptInstructionExecutionContext context, CancellationToken cancellationToken)
    {
        // Comments do not change runtime state and are intentionally ignored.
        return Task.CompletedTask;
    }
}
