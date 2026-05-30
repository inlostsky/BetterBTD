using BetterBTD.Models.AutoTasks;
using BetterBTD.Models.GameElements;
using BetterBTD.Models.MyScripts;

namespace BetterBTD.Services.MyScripts;

public sealed class ManagedScriptSlotCatalogService
{
    private static readonly Lazy<ManagedScriptSlotCatalogService> InstanceHolder =
        new(() => new ManagedScriptSlotCatalogService());

    private readonly IReadOnlyList<ManagedScriptSlotDefinition> _slots;
    private readonly IReadOnlyDictionary<string, ManagedScriptSlotDefinition> _slotsById;

    private ManagedScriptSlotCatalogService()
    {
        var slots = BuildSlots();
        _slots = slots;
        _slotsById = slots.ToDictionary(x => x.SlotId, StringComparer.OrdinalIgnoreCase);
    }

    public static ManagedScriptSlotCatalogService Instance => InstanceHolder.Value;

    public IReadOnlyList<ManagedScriptSlotDefinition> GetAll()
    {
        return _slots;
    }

    public IReadOnlyList<ManagedScriptSlotDefinition> GetByTaskKind(AutoTaskKind kind)
    {
        return _slots.Where(x => x.TaskKind == kind).ToList();
    }

    public bool TryGetById(string? slotId, out ManagedScriptSlotDefinition slot)
    {
        if (string.IsNullOrWhiteSpace(slotId))
        {
            slot = null!;
            return false;
        }

        return _slotsById.TryGetValue(slotId.Trim(), out slot!);
    }

    public string BuildBlackBorderSlotId(StageEntryTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return ManagedScriptSlotIdFactory.CreateBlackBorderSlotId(
            target.Map,
            target.Difficulty,
            target.Mode);
    }

    private static IReadOnlyList<ManagedScriptSlotDefinition> BuildSlots()
    {
        var slots = new List<ManagedScriptSlotDefinition>();
        slots.AddRange(BuildCustomSlots());
        slots.AddRange(BuildCollectionSlots());
        slots.AddRange(BuildBlackBorderSlots());
        slots.AddRange(BuildRaceSlots());
        return slots;
    }

    private static IEnumerable<ManagedScriptSlotDefinition> BuildCustomSlots()
    {
        yield return new ManagedScriptSlotDefinition
        {
            SlotId = ManagedScriptSlotIdFactory.CreateCustomDefaultSlotId(),
            TaskKind = AutoTaskKind.Custom,
            GroupName = "Custom",
            DisplayName = "Default Script"
        };
    }

    private static IEnumerable<ManagedScriptSlotDefinition> BuildCollectionSlots()
    {
        var expertMaps = GameElementCatalog.Maps
            .Where(map => map.Tier == MapDifficultyTier.Expert)
            .ToList();

        foreach (var mode in ManagedScriptCollectionModeCatalog.Modes)
        {
            foreach (var map in expertMaps)
            {
                yield return new ManagedScriptSlotDefinition
                {
                    SlotId = ManagedScriptSlotIdFactory.CreateCollectionSlotId(mode.Key, map.Type),
                    TaskKind = AutoTaskKind.Collection,
                    GroupName = mode.DisplayName,
                    DisplayName = GameElementCatalog.GetMapDisplayName(map.Type),
                    Qualifiers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["modeKey"] = mode.Key,
                        ["map"] = map.Type.ToString()
                    },
                    SuggestedTags = ["collection"],
                    IsPlaceholder = true
                };
            }
        }
    }

    private static IEnumerable<ManagedScriptSlotDefinition> BuildBlackBorderSlots()
    {
        foreach (var map in GameElementCatalog.Maps)
        {
            foreach (var difficulty in BlackBorderTaskCatalog.Difficulties)
            {
                foreach (var mode in BlackBorderTaskCatalog.GetModesForDifficulty(difficulty))
                {
                    yield return new ManagedScriptSlotDefinition
                    {
                        SlotId = ManagedScriptSlotIdFactory.CreateBlackBorderSlotId(map.Type, difficulty, mode),
                        TaskKind = AutoTaskKind.BlackBorder,
                        GroupName = $"{GameElementCatalog.GetMapDisplayName(map.Type)} / {GameElementCatalog.GetStageDifficultyDisplayName(difficulty)}",
                        DisplayName = GameElementCatalog.GetStageModeDisplayName(mode),
                        StageTarget = new StageEntryTarget
                        {
                            Map = map.Type,
                            Difficulty = difficulty,
                            Mode = mode
                        },
                        SuggestedTags = ["black-border"]
                    };
                }
            }
        }
    }

    private static IEnumerable<ManagedScriptSlotDefinition> BuildRaceSlots()
    {
        yield return new ManagedScriptSlotDefinition
        {
            SlotId = ManagedScriptSlotIdFactory.CreateRaceCurrentSlotId(),
            TaskKind = AutoTaskKind.Race,
            GroupName = "Race",
            DisplayName = "Current Event",
            SuggestedTags = ["race"],
            IsPlaceholder = true
        };
    }
}
