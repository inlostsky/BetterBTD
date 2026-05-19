using BetterBTD.Core.AutoTasks.Runtime;
using BetterBTD.Core.AutoTasks.Strategies;
using BetterBTD.Models.AutoTasks;

namespace BetterBTD.Core.AutoTasks;

public sealed class AutoTaskStrategyRegistry : IAutoTaskStrategyRegistry
{
    private static readonly Lazy<AutoTaskStrategyRegistry> InstanceHolder = new(() => new AutoTaskStrategyRegistry());

    private readonly IReadOnlyDictionary<AutoTaskKind, IAutoTaskStrategy> _strategies;

    private AutoTaskStrategyRegistry()
        : this(
            [
                new CustomAutoTaskStrategy(),
                new CollectionAutoTaskStrategy(),
                new BlackBorderAutoTaskStrategy(),
                new RaceAutoTaskStrategy()
            ])
    {
    }

    internal AutoTaskStrategyRegistry(IEnumerable<IAutoTaskStrategy> strategies)
    {
        ArgumentNullException.ThrowIfNull(strategies);

        _strategies = strategies.ToDictionary(x => x.Kind);
    }

    public static AutoTaskStrategyRegistry Instance => InstanceHolder.Value;

    public IAutoTaskStrategy GetRequiredStrategy(AutoTaskKind kind)
    {
        if (_strategies.TryGetValue(kind, out var strategy))
        {
            return strategy;
        }

        throw new InvalidOperationException($"No auto-task strategy is registered for '{kind}'.");
    }
}
