using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using BetterBTD.Helpers;
using BetterBTD.Models;
using BetterBTD.Models.GameElements;
using BetterBTD.Models.ScriptEditor;
using BetterBTD.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GongSolutions.Wpf.DragDrop;
using Wpf.Ui.Violeta.Controls;

namespace BetterBTD.ViewModels;

public sealed class ScriptEditorPageViewModel : ObservableObject, IDropTarget
{
    private readonly LocalizationService _localizationService;
    private readonly List<ScriptInstructionInstance> _clipboardSequenceInstructions = [];
    private readonly Stack<List<ScriptInstructionInstance>> _undoHistory = [];
    private readonly Stack<List<ScriptInstructionInstance>> _redoHistory = [];

    private string _scriptText = string.Empty;
    private GameMapType _selectedMap = GameMapType.MonkeyMeadow;
    private LanguageOption? _selectedDifficultyOption;
    private LanguageOption? _selectedModeOption;
    private LanguageOption? _selectedHeroOption;
    private ScriptInstructionTemplate? _selectedLibraryInstruction;
    private ScriptInstructionInstance? _selectedSequenceInstruction;
    private bool _isRestoringHistory;
    private bool _suppressHistoryTracking;
    private bool _isUpdatingSequenceInternals;
    private List<ScriptInstructionInstance> _sequenceSnapshot = [];

    public ScriptEditorPageViewModel(LocalizationService localizationService)
    {
        _localizationService = localizationService;
        _localizationService.LanguageChanged += (_, _) =>
        {
            BuildMetadataOptions();
            BuildScriptParameterOptions();
            UpdateInstructionLocalization();
            RebuildMonkeyObjectOptions();
            RaiseLocalizedProperties();
        };

        InstructionSequence.CollectionChanged += OnInstructionSequenceChanged;

        AddInstructionToSequenceCommand = new RelayCommand<ScriptInstructionTemplate?>(AddInstructionToSequence);
        DeleteSelectedSequenceInstructionsCommand = new RelayCommand<IList?>(DeleteSelectedSequenceInstructions, CanDeleteSelectedSequenceInstructions);
        CopySelectedSequenceInstructionsCommand = new RelayCommand<IList?>(CopySelectedSequenceInstructions, CanCopySelectedSequenceInstructions);
        PasteSequenceInstructionsCommand = new RelayCommand<IList?>(PasteSequenceInstructions, CanPasteSequenceInstructions);
        UndoSequenceCommand = new RelayCommand(UndoSequence, CanUndoSequence);
        RedoSequenceCommand = new RelayCommand(RedoSequence, CanRedoSequence);

        BuildMetadataOptions();
        BuildInstructionLibrary();
        BuildScriptParameterOptions();
        UpdateInstructionLocalization();
        RebuildMonkeyObjectOptions();
        _sequenceSnapshot = CaptureSequenceSnapshot();
        RefreshHistoryCommandState();
    }

    public int ScriptCount { get; } = 34;
    public int SharedCount { get; } = 12;

    public IRelayCommand<ScriptInstructionTemplate?> AddInstructionToSequenceCommand { get; }
    public IRelayCommand<IList?> DeleteSelectedSequenceInstructionsCommand { get; }
    public IRelayCommand<IList?> CopySelectedSequenceInstructionsCommand { get; }
    public IRelayCommand<IList?> PasteSequenceInstructionsCommand { get; }
    public IRelayCommand UndoSequenceCommand { get; }
    public IRelayCommand RedoSequenceCommand { get; }

    public ObservableCollection<LanguageOption> DifficultyOptions { get; } = [];
    public ObservableCollection<LanguageOption> ModeOptions { get; } = [];
    public ObservableCollection<LanguageOption> HeroOptions { get; } = [];
    public ObservableCollection<LanguageOption> MonkeyObjectOptions { get; } = [];
    public ObservableCollection<LanguageOption> UpgradePathOptions { get; } = [];
    public ObservableCollection<LanguageOption> SwitchDirectionOptions { get; } = [];
    public ObservableCollection<LanguageOption> MonkeyAbilityOptions { get; } = [];
    public ObservableCollection<LanguageOption> InventoryOptions { get; } = [];
    public ObservableCollection<LanguageOption> ActivatedAbilityOptions { get; } = [];
    public ObservableCollection<LanguageOption> NextRoundActionOptions { get; } = [];

    public ObservableCollection<ScriptInstructionTemplate> InstructionLibrary { get; } = [];
    public ObservableCollection<ScriptInstructionInstance> InstructionSequence { get; } = [];

    public IReadOnlyList<ICascadingItem> MapItems => GameElementCascadingItems.MapItems;
    public IReadOnlyList<ICascadingItem> MonkeyTowerItems => GameElementCascadingItems.MonkeyTowerItems;

    public string WorkspaceTitle => _localizationService.T("Editor.Workspace");
    public string WorkspaceDescription => _localizationService.T("Editor.Description");
    public string PreviewHint => _localizationService.T("Editor.Preview");

    public string ScriptText
    {
        get => _scriptText;
        set => SetProperty(ref _scriptText, value);
    }

    public GameMapType SelectedMap
    {
        get => _selectedMap;
        set => SetProperty(ref _selectedMap, value);
    }

    public LanguageOption? SelectedDifficultyOption
    {
        get => _selectedDifficultyOption;
        set => SetProperty(ref _selectedDifficultyOption, value);
    }

    public LanguageOption? SelectedModeOption
    {
        get => _selectedModeOption;
        set => SetProperty(ref _selectedModeOption, value);
    }

    public LanguageOption? SelectedHeroOption
    {
        get => _selectedHeroOption;
        set => SetProperty(ref _selectedHeroOption, value);
    }

    public ScriptInstructionTemplate? SelectedLibraryInstruction
    {
        get => _selectedLibraryInstruction;
        set => SetProperty(ref _selectedLibraryInstruction, value);
    }

    public ScriptInstructionInstance? SelectedSequenceInstruction
    {
        get => _selectedSequenceInstruction;
        set => SetProperty(ref _selectedSequenceInstruction, value);
    }

    public string StatsTitle => _localizationService.T("Editor.Stats");
    public string TotalScriptsText => string.Format(_localizationService.T("Editor.TotalScripts"), ScriptCount);
    public string SharedScriptsText => string.Format(_localizationService.T("Editor.SharedScripts"), SharedCount);
    public string StatusTitle => _localizationService.T("Editor.State");
    public string DraftState => _localizationService.T("Editor.DraftState");

    public string FileOpenText => _localizationService.T("Editor.File.Open");
    public string FileSaveText => _localizationService.T("Editor.File.Save");
    public string FileSaveAsText => _localizationService.T("Editor.File.SaveAs");
    public string FileNewText => _localizationService.T("Editor.File.New");
    public string FileCloseText => _localizationService.T("Editor.File.Close");
    public string CurrentFileText => _localizationService.T("Editor.File.Current");

    public string MetadataMapText => _localizationService.T("Editor.Metadata.Map");
    public string MetadataDifficultyText => _localizationService.T("Editor.Metadata.Difficulty");
    public string MetadataModeText => _localizationService.T("Editor.Metadata.Mode");
    public string MetadataHeroText => _localizationService.T("Editor.Metadata.Hero");
    public string MetadataMapPlaceholderText => _localizationService.T("Editor.Metadata.Map.Placeholder");

    public string ScriptCategoryAllText => _localizationService.T("Editor.Category.All");
    public string ScriptCategoryCollectionText => _localizationService.T("Editor.Category.Collection");
    public string ScriptCategoryBlackBorderText => _localizationService.T("Editor.Category.BlackBorder");
    public string ScriptCategoryRaceText => _localizationService.T("Editor.Category.Race");

    public string DebugRunText => _localizationService.T("Editor.Debug.Run");
    public string DebugStepText => _localizationService.T("Editor.Debug.Step");
    public string DebugStopText => _localizationService.T("Editor.Debug.Stop");
    public string DebugValidateText => _localizationService.T("Editor.Debug.Validate");

    public string LibraryTitle => _localizationService.T("Editor.Panel.Library.Title");
    public string SequenceTitle => _localizationService.T("Editor.Panel.Sequence.Title");
    public string PropertiesTitle => _localizationService.T("Editor.Panel.Properties.Title");
    public string SequenceEmptyText => _localizationService.T("Editor.Panel.Sequence.Empty");
    public string PropertiesEmptyText => _localizationService.T("Editor.Panel.Properties.Empty");
    public string PropertyMonkeyTowerText => _localizationService.T("Editor.Property.MonkeyTower");
    public string PropertyCoordinateText => _localizationService.T("Editor.Property.Coordinate");
    public string PropertyTargetMonkeyText => _localizationService.T("Editor.Property.TargetMonkey");
    public string PropertyUpgradePathText => _localizationService.T("Editor.Property.UpgradePath");
    public string PropertyUpgradeCountText => _localizationService.T("Editor.Property.UpgradeCount");
    public string PropertySwitchDirectionText => _localizationService.T("Editor.Property.SwitchDirection");
    public string PropertySwitchCountText => _localizationService.T("Editor.Property.SwitchCount");
    public string PropertyAbilityText => _localizationService.T("Editor.Property.Ability");
    public string PropertyNeedAbilityCoordinateText => _localizationService.T("Editor.Property.NeedAbilityCoordinate");
    public string PropertyInventoryText => _localizationService.T("Editor.Property.Inventory");
    public string PropertyActivatedAbilityText => _localizationService.T("Editor.Property.ActivatedAbility");
    public string PropertyNeedCoordinateText => _localizationService.T("Editor.Property.NeedCoordinate");
    public string PropertyNextRoundActionText => _localizationService.T("Editor.Property.NextRoundAction");
    public string PropertyNextRoundSendCountText => _localizationService.T("Editor.Property.NextRound.SendCount");
    public string PropertyNotesText => _localizationService.T("Editor.Property.Notes");
    public string DeleteSelectedInstructionText => _localizationService.T("Editor.Command.DeleteSelected");
    public string CopySelectedInstructionText => _localizationService.T("Editor.Command.CopySelected");
    public string PasteInstructionText => _localizationService.T("Editor.Command.Paste");

    public void DragOver(IDropInfo dropInfo)
    {
        if (dropInfo.TargetCollection != InstructionSequence)
        {
            dropInfo.Effects = System.Windows.DragDropEffects.None;
            return;
        }

        if (TryGetTemplates(dropInfo.Data).Count > 0)
        {
            dropInfo.Effects = System.Windows.DragDropEffects.Copy;
            dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
            return;
        }

        if (TryGetInstructionInstances(dropInfo.Data).Count > 0)
        {
            dropInfo.Effects = System.Windows.DragDropEffects.Move;
            dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
            return;
        }

        dropInfo.Effects = System.Windows.DragDropEffects.None;
    }

    public void Drop(IDropInfo dropInfo)
    {
        if (dropInfo.TargetCollection is not IList targetCollection || targetCollection != InstructionSequence)
        {
            return;
        }

        var insertIndex = dropInfo.InsertIndex;
        var templates = TryGetTemplates(dropInfo.Data);
        if (templates.Count > 0)
        {
            ExecuteTrackedSequenceMutation(() =>
            {
                ScriptInstructionInstance? firstAdded = null;
                foreach (var template in templates)
                {
                    var instance = CreateInstructionInstance(template);
                    targetCollection.Insert(insertIndex, instance);
                    firstAdded ??= instance;
                    insertIndex++;
                }

                if (firstAdded is not null && templates.Count == 1)
                {
                    SelectedSequenceInstruction = firstAdded;
                }
            });

            return;
        }

        var instancesToMove = TryGetInstructionInstances(dropInfo.Data)
            .Where(InstructionSequence.Contains)
            .Distinct()
            .ToList();

        if (instancesToMove.Count == 0)
        {
            return;
        }

        var oldIndexes = instancesToMove
            .Select(InstructionSequence.IndexOf)
            .OrderBy(index => index)
            .ToList();

        if (oldIndexes.Count == 0)
        {
            return;
        }

        var orderedInstances = oldIndexes.Select(index => InstructionSequence[index]).ToList();
        var removedBeforeInsert = oldIndexes.Count(index => index < insertIndex);
        insertIndex -= removedBeforeInsert;

        var noPositionChange = oldIndexes
            .Select((index, offset) => index == insertIndex + offset)
            .All(matches => matches);

        if (noPositionChange)
        {
            return;
        }

        ExecuteTrackedSequenceMutation(() =>
        {
            for (var index = orderedInstances.Count - 1; index >= 0; index--)
            {
                InstructionSequence.Remove(orderedInstances[index]);
            }

            foreach (var instance in orderedInstances)
            {
                InstructionSequence.Insert(insertIndex, instance);
                insertIndex++;
            }

            if (orderedInstances.Count == 1)
            {
                SelectedSequenceInstruction = orderedInstances[0];
            }
        });
    }

    private void OnInstructionSequenceChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems.OfType<ScriptInstructionInstance>())
            {
                item.PropertyChanged -= OnInstructionPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems.OfType<ScriptInstructionInstance>())
            {
                item.PropertyChanged += OnInstructionPropertyChanged;
            }
        }

        if (!_isRestoringHistory && !_suppressHistoryTracking)
        {
            PushUndoSnapshot();
            _sequenceSnapshot = CaptureSequenceSnapshot();
        }

        RebuildMonkeyObjectOptions();
        RefreshHistoryCommandState();
    }

    private void OnInstructionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ScriptInstructionInstance instruction)
        {
            return;
        }

        if (_isUpdatingSequenceInternals)
        {
            return;
        }

        if (instruction.Type == ScriptCommandType.PlaceMonkey &&
            string.Equals(e.PropertyName, nameof(ScriptInstructionInstance.SelectedMonkeyTower), StringComparison.Ordinal))
        {
            if (!_isRestoringHistory && !_suppressHistoryTracking)
            {
                PushUndoSnapshot();
                _sequenceSnapshot = CaptureSequenceSnapshot();
            }

            RebuildMonkeyObjectOptions();
            RefreshHistoryCommandState();
            return;
        }

        if (string.Equals(e.PropertyName, nameof(ScriptInstructionInstance.DisplayName), StringComparison.Ordinal) ||
            string.Equals(e.PropertyName, nameof(ScriptInstructionInstance.Description), StringComparison.Ordinal) ||
            string.Equals(e.PropertyName, nameof(ScriptInstructionInstance.MonkeyObjectId), StringComparison.Ordinal) ||
            string.Equals(e.PropertyName, nameof(ScriptInstructionInstance.ShowNextRoundSendCount), StringComparison.Ordinal))
        {
            return;
        }

        if (!_isRestoringHistory && !_suppressHistoryTracking)
        {
            PushUndoSnapshot();
            _sequenceSnapshot = CaptureSequenceSnapshot();
            RefreshHistoryCommandState();
        }

        UpdateInstructionDisplayName(instruction);
    }

    private void RebuildMonkeyObjectOptions()
    {
        _isUpdatingSequenceInternals = true;
        try
        {
            var counters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var options = new List<LanguageOption>();

            foreach (var instruction in InstructionSequence)
            {
                if (instruction.Type != ScriptCommandType.PlaceMonkey)
                {
                    continue;
                }

                var selectionCode = NormalizePlaceSelectionCode(instruction.SelectedMonkeyTower);

                if (TryParseHeroSelection(selectionCode, out var heroType))
                {
                    var heroKey = BuildHeroObjectKey(heroType);
                    instruction.MonkeyObjectId = heroKey;

                    if (options.All(x => !string.Equals(x.Code, heroKey, StringComparison.OrdinalIgnoreCase)))
                    {
                        options.Add(new LanguageOption
                        {
                            Code = heroKey,
                            DisplayName = BuildHeroObjectDisplayName(heroType)
                        });
                    }

                    continue;
                }

                var towerType = TryParseTowerSelection(selectionCode, out var parsedTowerType)
                    ? parsedTowerType
                    : MonkeyTowerType.DartMonkey;
                var towerCode = BuildTowerSelectionCode(towerType);

                counters.TryGetValue(towerCode, out var count);
                count++;
                counters[towerCode] = count;

                var key = BuildMonkeyObjectKey(towerType, count);
                instruction.MonkeyObjectId = key;

                options.Add(new LanguageOption
                {
                    Code = key,
                    DisplayName = BuildMonkeyObjectDisplayName(towerType, count)
                });
            }

            MonkeyObjectOptions.Clear();
            foreach (var option in options)
            {
                MonkeyObjectOptions.Add(option);
            }

            var availableKeys = options.Select(x => x.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var fallbackKey = options.FirstOrDefault()?.Code ?? string.Empty;

            foreach (var instruction in InstructionSequence.Where(RequiresMonkeyObjectTarget))
            {
                if (string.IsNullOrWhiteSpace(instruction.TargetMonkeyObjectId) ||
                    !availableKeys.Contains(instruction.TargetMonkeyObjectId))
                {
                    instruction.TargetMonkeyObjectId = fallbackKey;
                }
            }
        }
        finally
        {
            _isUpdatingSequenceInternals = false;
        }

        UpdateAllInstructionDisplayNames();
    }

    private static string BuildMonkeyObjectKey(MonkeyTowerType towerType, int index)
    {
        return $"{towerType}:{index}";
    }

    private static string BuildHeroObjectKey(HeroType heroType)
    {
        return $"Hero:{heroType}";
    }

    private static string BuildTowerSelectionCode(MonkeyTowerType towerType)
    {
        return $"Tower:{towerType}";
    }

    private static bool IsInventorySelection(string value)
    {
        return Enum.TryParse<InventoryType>(value, out _);
    }

    private static bool IsActivatedAbilitySelection(string value)
    {
        return Enum.TryParse<ActivatedAbilityType>(value, out _);
    }

    private static string NormalizePlaceSelectionCode(string? selectionCode)
    {
        if (string.IsNullOrWhiteSpace(selectionCode))
        {
            return BuildTowerSelectionCode(MonkeyTowerType.DartMonkey);
        }

        if (selectionCode.StartsWith("Tower:", StringComparison.OrdinalIgnoreCase) ||
            selectionCode.StartsWith("Hero:", StringComparison.OrdinalIgnoreCase))
        {
            return selectionCode;
        }

        return $"Tower:{selectionCode}";
    }

    private static bool TryParseTowerSelection(string selectionCode, out MonkeyTowerType towerType)
    {
        towerType = default;
        if (!selectionCode.StartsWith("Tower:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var raw = selectionCode.Substring("Tower:".Length);
        return Enum.TryParse(raw, out towerType);
    }

    private static bool TryParseHeroSelection(string selectionCode, out HeroType heroType)
    {
        heroType = default;
        if (!selectionCode.StartsWith("Hero:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var raw = selectionCode.Substring("Hero:".Length);
        return Enum.TryParse(raw, out heroType);
    }

    private string BuildMonkeyObjectDisplayName(MonkeyTowerType towerType, int index)
    {
        var tower = GameElementCatalog.MonkeyTowers.FirstOrDefault(x => x.Type == towerType);
        var towerName = tower is null ? towerType.ToString() : _localizationService.T(tower.NameKey);
        return $"{towerName}{index}";
    }

    private string BuildHeroObjectDisplayName(HeroType heroType)
    {
        var hero = GameElementCatalog.Heroes.FirstOrDefault(x => x.Type == heroType);
        return hero is null ? heroType.ToString() : _localizationService.T(hero.NameKey);
    }

    private static bool RequiresMonkeyObjectTarget(ScriptInstructionInstance instruction)
    {
        return instruction.Type is ScriptCommandType.UpgradeMonkey or ScriptCommandType.SwitchMonkeyTarget or ScriptCommandType.SetMonkeyAbility or ScriptCommandType.SellMonkey;
    }

    private void DeleteSelectedSequenceInstructions(IList? selectedItems)
    {
        var selectedInstructions = selectedItems?.OfType<ScriptInstructionInstance>().Distinct().ToList() ?? [];
        if (selectedInstructions.Count == 0)
        {
            return;
        }

        ExecuteTrackedSequenceMutation(() =>
        {
            foreach (var instruction in selectedInstructions)
            {
                InstructionSequence.Remove(instruction);
            }

            SelectedSequenceInstruction = InstructionSequence.FirstOrDefault();
        });
    }

    private static bool CanDeleteSelectedSequenceInstructions(IList? selectedItems)
    {
        return selectedItems?.OfType<ScriptInstructionInstance>().Any() == true;
    }

    private void AddInstructionToSequence(ScriptInstructionTemplate? template)
    {
        if (template is null)
        {
            return;
        }

        ExecuteTrackedSequenceMutation(() =>
        {
            var instance = CreateInstructionInstance(template);
            InstructionSequence.Add(instance);
            SelectedSequenceInstruction = instance;
        });
    }

    private ScriptInstructionInstance CreateInstructionInstance(ScriptInstructionTemplate template)
    {
        var instruction = new ScriptInstructionInstance(template.Type, template.NameKey, template.DescriptionKey)
        {
            DisplayName = template.DisplayName,
            Description = template.Description
        };

        if (RequiresMonkeyObjectTarget(instruction))
        {
            instruction.TargetMonkeyObjectId = MonkeyObjectOptions.FirstOrDefault()?.Code ?? string.Empty;
        }

        if (instruction.Type == ScriptCommandType.PlaceHeroInventory)
        {
            instruction.SelectedInventoryItem = InventoryOptions.FirstOrDefault()?.Code ?? string.Empty;
        }

        if (instruction.Type == ScriptCommandType.ActivateAbility)
        {
            instruction.SelectedActivatedAbility = ActivatedAbilityOptions.FirstOrDefault()?.Code ?? string.Empty;
        }

        if (instruction.Type == ScriptCommandType.NextRound)
        {
            instruction.NextRoundAction = "PlayFastForward";
            instruction.NextRoundSendCount = 1;
        }

        UpdateInstructionDisplayName(instruction);
        return instruction;
    }

    private static IReadOnlyList<ScriptInstructionTemplate> TryGetTemplates(object? data)
    {
        if (data is ScriptInstructionTemplate template)
        {
            return [template];
        }

        return data is IEnumerable enumerable
            ? enumerable.OfType<ScriptInstructionTemplate>().ToList()
            : [];
    }

    private static IReadOnlyList<ScriptInstructionInstance> TryGetInstructionInstances(object? data)
    {
        if (data is ScriptInstructionInstance instance)
        {
            return [instance];
        }

        return data is IEnumerable enumerable
            ? enumerable.OfType<ScriptInstructionInstance>().ToList()
            : [];
    }

    private void CopySelectedSequenceInstructions(IList? selectedItems)
    {
        var selectedInstructions = selectedItems?.OfType<ScriptInstructionInstance>().Distinct().ToList() ?? [];
        if (selectedInstructions.Count == 0)
        {
            return;
        }

        _clipboardSequenceInstructions.Clear();
        foreach (var instruction in selectedInstructions)
        {
            _clipboardSequenceInstructions.Add(CloneInstructionInstance(instruction));
        }

        PasteSequenceInstructionsCommand.NotifyCanExecuteChanged();
    }

    private static bool CanCopySelectedSequenceInstructions(IList? selectedItems)
    {
        return selectedItems?.OfType<ScriptInstructionInstance>().Any() == true;
    }

    private void PasteSequenceInstructions(IList? selectedItems)
    {
        if (_clipboardSequenceInstructions.Count == 0)
        {
            return;
        }

        ExecuteTrackedSequenceMutation(() =>
        {
            var insertIndex = GetPasteInsertIndex(selectedItems);
            ScriptInstructionInstance? firstPasted = null;

            foreach (var copiedInstruction in _clipboardSequenceInstructions)
            {
                var pastedInstruction = CloneInstructionInstance(copiedInstruction);
                InstructionSequence.Insert(insertIndex, pastedInstruction);
                firstPasted ??= pastedInstruction;
                insertIndex++;
            }

            if (firstPasted is not null)
            {
                SelectedSequenceInstruction = firstPasted;
            }
        });
    }

    private bool CanPasteSequenceInstructions(IList? _)
    {
        return _clipboardSequenceInstructions.Count > 0;
    }

    private void ExecuteTrackedSequenceMutation(Action mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);

        if (_isRestoringHistory)
        {
            mutation();
            return;
        }

        PushUndoSnapshot();
        _suppressHistoryTracking = true;
        try
        {
            mutation();
        }
        finally
        {
            _suppressHistoryTracking = false;
            _sequenceSnapshot = CaptureSequenceSnapshot();
            RefreshHistoryCommandState();
        }
    }

    private void PushUndoSnapshot()
    {
        _undoHistory.Push(CloneSnapshot(_sequenceSnapshot));
        _redoHistory.Clear();
    }

    private static List<ScriptInstructionInstance> CloneSnapshot(IEnumerable<ScriptInstructionInstance> source)
    {
        return source.Select(CloneInstructionInstance).ToList();
    }

    private List<ScriptInstructionInstance> CaptureSequenceSnapshot()
    {
        return InstructionSequence.Select(CloneInstructionInstance).ToList();
    }

    private void UndoSequence()
    {
        if (_undoHistory.Count == 0)
        {
            return;
        }

        _isRestoringHistory = true;
        try
        {
            _redoHistory.Push(CaptureSequenceSnapshot());
            var snapshot = _undoHistory.Pop();
            RestoreSequenceSnapshot(snapshot);
            _sequenceSnapshot = CaptureSequenceSnapshot();
        }
        finally
        {
            _isRestoringHistory = false;
            RefreshHistoryCommandState();
        }
    }

    private void RedoSequence()
    {
        if (_redoHistory.Count == 0)
        {
            return;
        }

        _isRestoringHistory = true;
        try
        {
            _undoHistory.Push(CaptureSequenceSnapshot());
            var snapshot = _redoHistory.Pop();
            RestoreSequenceSnapshot(snapshot);
            _sequenceSnapshot = CaptureSequenceSnapshot();
        }
        finally
        {
            _isRestoringHistory = false;
            RefreshHistoryCommandState();
        }
    }

    private void RestoreSequenceSnapshot(IEnumerable<ScriptInstructionInstance> snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        _suppressHistoryTracking = true;
        try
        {
            InstructionSequence.Clear();
            foreach (var instruction in snapshot.Select(CloneInstructionInstance))
            {
                InstructionSequence.Add(instruction);
            }

            SelectedSequenceInstruction = InstructionSequence.FirstOrDefault();
        }
        finally
        {
            _suppressHistoryTracking = false;
        }
    }

    private bool CanUndoSequence()
    {
        return _undoHistory.Count > 0;
    }

    private bool CanRedoSequence()
    {
        return _redoHistory.Count > 0;
    }

    private void RefreshHistoryCommandState()
    {
        UndoSequenceCommand.NotifyCanExecuteChanged();
        RedoSequenceCommand.NotifyCanExecuteChanged();
    }

    private int GetPasteInsertIndex(IList? selectedItems)
    {
        var selectedInstructions = selectedItems?.OfType<ScriptInstructionInstance>()
            .Where(InstructionSequence.Contains)
            .Distinct()
            .ToList() ?? [];

        if (selectedInstructions.Count == 0)
        {
            return InstructionSequence.Count;
        }

        return selectedInstructions.Max(InstructionSequence.IndexOf) + 1;
    }

    private static ScriptInstructionInstance CloneInstructionInstance(ScriptInstructionInstance source)
    {
        return new ScriptInstructionInstance(source.Type, source.NameKey, source.DescriptionKey)
        {
            DisplayName = source.DisplayName,
            Description = source.Description,
            SelectedMonkeyTower = NormalizePlaceSelectionCode(source.SelectedMonkeyTower),
            MonkeyObjectId = source.MonkeyObjectId,
            TargetMonkeyObjectId = source.TargetMonkeyObjectId,
            SelectedInventoryItem = source.SelectedInventoryItem,
            SelectedActivatedAbility = source.SelectedActivatedAbility,
            NextRoundAction = source.NextRoundAction,
            NextRoundSendCount = source.NextRoundSendCount,
            PositionX = source.PositionX,
            PositionY = source.PositionY,
            UpgradePath = source.UpgradePath,
            UpgradeCount = source.UpgradeCount,
            SwitchDirection = source.SwitchDirection,
            SwitchCount = source.SwitchCount,
            SelectedAbility = source.SelectedAbility,
            RequiresAbilityCoordinate = source.RequiresAbilityCoordinate,
            AbilityCoordinateX = source.AbilityCoordinateX,
            AbilityCoordinateY = source.AbilityCoordinateY,
            Notes = source.Notes
        };
    }

    private void BuildInstructionLibrary()
    {
        InstructionLibrary.Clear();
        InstructionLibrary.Add(new ScriptInstructionTemplate
        {
            Type = ScriptCommandType.PlaceMonkey,
            NameKey = "Editor.Command.PlaceMonkey.Title",
            DescriptionKey = "Editor.Command.PlaceMonkey.Description"
        });
        InstructionLibrary.Add(new ScriptInstructionTemplate
        {
            Type = ScriptCommandType.UpgradeMonkey,
            NameKey = "Editor.Command.UpgradeMonkey.Title",
            DescriptionKey = "Editor.Command.UpgradeMonkey.Description"
        });
        InstructionLibrary.Add(new ScriptInstructionTemplate
        {
            Type = ScriptCommandType.SwitchMonkeyTarget,
            NameKey = "Editor.Command.SwitchMonkeyTarget.Title",
            DescriptionKey = "Editor.Command.SwitchMonkeyTarget.Description"
        });
        InstructionLibrary.Add(new ScriptInstructionTemplate
        {
            Type = ScriptCommandType.SetMonkeyAbility,
            NameKey = "Editor.Command.SetMonkeyAbility.Title",
            DescriptionKey = "Editor.Command.SetMonkeyAbility.Description"
        });
        InstructionLibrary.Add(new ScriptInstructionTemplate
        {
            Type = ScriptCommandType.SellMonkey,
            NameKey = "Editor.Command.SellMonkey.Title",
            DescriptionKey = "Editor.Command.SellMonkey.Description"
        });
        InstructionLibrary.Add(new ScriptInstructionTemplate
        {
            Type = ScriptCommandType.PlaceHeroInventory,
            NameKey = "Editor.Command.PlaceHeroInventory.Title",
            DescriptionKey = "Editor.Command.PlaceHeroInventory.Description"
        });
        InstructionLibrary.Add(new ScriptInstructionTemplate
        {
            Type = ScriptCommandType.ActivateAbility,
            NameKey = "Editor.Command.ActivateAbility.Title",
            DescriptionKey = "Editor.Command.ActivateAbility.Description"
        });
        InstructionLibrary.Add(new ScriptInstructionTemplate
        {
            Type = ScriptCommandType.NextRound,
            NameKey = "Editor.Command.NextRound.Title",
            DescriptionKey = "Editor.Command.NextRound.Description"
        });
    }

    private void BuildScriptParameterOptions()
    {
        UpgradePathOptions.Clear();
        UpgradePathOptions.Add(new LanguageOption { Code = UpgradePathType.Top.ToString(), DisplayName = _localizationService.T("Editor.Property.UpgradePath.Top") });
        UpgradePathOptions.Add(new LanguageOption { Code = UpgradePathType.Middle.ToString(), DisplayName = _localizationService.T("Editor.Property.UpgradePath.Middle") });
        UpgradePathOptions.Add(new LanguageOption { Code = UpgradePathType.Bottom.ToString(), DisplayName = _localizationService.T("Editor.Property.UpgradePath.Bottom") });

        SwitchDirectionOptions.Clear();
        SwitchDirectionOptions.Add(new LanguageOption { Code = SwitchDirectionType.Right.ToString(), DisplayName = _localizationService.T("Editor.Property.SwitchDirection.Right") });
        SwitchDirectionOptions.Add(new LanguageOption { Code = SwitchDirectionType.Left.ToString(), DisplayName = _localizationService.T("Editor.Property.SwitchDirection.Left") });

        MonkeyAbilityOptions.Clear();
        MonkeyAbilityOptions.Add(new LanguageOption { Code = MonkeyAbilityType.Ability1.ToString(), DisplayName = _localizationService.T("Editor.Property.Ability1") });
        MonkeyAbilityOptions.Add(new LanguageOption { Code = MonkeyAbilityType.Ability2.ToString(), DisplayName = _localizationService.T("Editor.Property.Ability2") });

        InventoryOptions.Clear();
        foreach (var inventory in GameElementCatalog.InventoryItems)
        {
            InventoryOptions.Add(new LanguageOption { Code = inventory.Type.ToString(), DisplayName = _localizationService.T(inventory.NameKey) });
        }

        ActivatedAbilityOptions.Clear();
        foreach (var ability in GameElementCatalog.ActivatedAbilities)
        {
            ActivatedAbilityOptions.Add(new LanguageOption { Code = ability.Type.ToString(), DisplayName = _localizationService.T(ability.NameKey) });
        }

        NextRoundActionOptions.Clear();
        foreach (var action in GameElementCatalog.NextRoundActions)
        {
            NextRoundActionOptions.Add(new LanguageOption
            {
                Code = action,
                DisplayName = GameElementCatalog.GetNextRoundActionDisplayName(action)
            });
        }
    }

    private void BuildMetadataOptions()
    {
        var selectedDifficultyCode = SelectedDifficultyOption?.Code ?? StageDifficulty.Medium.ToString();
        var selectedModeCode = SelectedModeOption?.Code ?? StageMode.Standard.ToString();
        var selectedHeroCode = SelectedHeroOption?.Code ?? HeroType.Quincy.ToString();

        DifficultyOptions.Clear();
        DifficultyOptions.Add(new LanguageOption { Code = StageDifficulty.Easy.ToString(), DisplayName = _localizationService.T("GameElements.StageDifficulty.Easy") });
        DifficultyOptions.Add(new LanguageOption { Code = StageDifficulty.Medium.ToString(), DisplayName = _localizationService.T("GameElements.StageDifficulty.Medium") });
        DifficultyOptions.Add(new LanguageOption { Code = StageDifficulty.Hard.ToString(), DisplayName = _localizationService.T("GameElements.StageDifficulty.Hard") });

        ModeOptions.Clear();
        foreach (var mode in new[]
                 {
                     StageMode.Standard,
                     StageMode.PrimaryOnly,
                     StageMode.Deflation,
                     StageMode.MilitaryOnly,
                     StageMode.Apopalypse,
                     StageMode.Reverse,
                     StageMode.MagicOnly,
                     StageMode.DoubleHpMoabs,
                     StageMode.HalfCash,
                     StageMode.AlternateBloonsRounds,
                     StageMode.Impoppable,
                     StageMode.CHIMPS
                 })
        {
            ModeOptions.Add(new LanguageOption
            {
                Code = mode.ToString(),
                DisplayName = _localizationService.T($"GameElements.StageMode.{mode}")
            });
        }

        HeroOptions.Clear();
        foreach (var hero in GameElementCatalog.Heroes)
        {
            HeroOptions.Add(new LanguageOption
            {
                Code = hero.Type.ToString(),
                DisplayName = _localizationService.T(hero.NameKey)
            });
        }

        SelectedDifficultyOption = DifficultyOptions.FirstOrDefault(x => x.Code == selectedDifficultyCode) ?? DifficultyOptions.FirstOrDefault();
        SelectedModeOption = ModeOptions.FirstOrDefault(x => x.Code == selectedModeCode) ?? ModeOptions.FirstOrDefault();
        SelectedHeroOption = HeroOptions.FirstOrDefault(x => x.Code == selectedHeroCode) ?? HeroOptions.FirstOrDefault();
    }

    private void UpdateInstructionLocalization()
    {
        foreach (var template in InstructionLibrary)
        {
            template.DisplayName = _localizationService.T(template.NameKey);
            template.Description = _localizationService.T(template.DescriptionKey);
        }

        foreach (var instance in InstructionSequence)
        {
            instance.Description = _localizationService.T(instance.DescriptionKey);
        }

        UpdateAllInstructionDisplayNames();
    }

    private void UpdateAllInstructionDisplayNames()
    {
        foreach (var instruction in InstructionSequence)
        {
            UpdateInstructionDisplayName(instruction);
        }
    }

    private void UpdateInstructionDisplayName(ScriptInstructionInstance instruction)
    {
        var text = instruction.Type switch
        {
            ScriptCommandType.PlaceMonkey => string.Format(
                _localizationService.T("Editor.Display.PlaceMonkey"),
                string.IsNullOrWhiteSpace(instruction.MonkeyObjectId)
                    ? GetPlaceSelectionDisplayName(instruction.SelectedMonkeyTower)
                    : GetMonkeyObjectDisplayName(instruction.MonkeyObjectId)),
            ScriptCommandType.UpgradeMonkey => IsHeroObjectKey(instruction.TargetMonkeyObjectId)
                ? string.Format(
                    _localizationService.T("Editor.Display.UpgradeMonkey.Hero"),
                    GetMonkeyObjectDisplayName(instruction.TargetMonkeyObjectId),
                    instruction.UpgradeCount)
                : string.Format(
                    _localizationService.T("Editor.Display.UpgradeMonkey"),
                    GetMonkeyObjectDisplayName(instruction.TargetMonkeyObjectId),
                    GetUpgradePathDisplayName(instruction.UpgradePath),
                    instruction.UpgradeCount),
            ScriptCommandType.SwitchMonkeyTarget => string.Format(
                _localizationService.T("Editor.Display.SwitchMonkeyTarget"),
                GetMonkeyObjectDisplayName(instruction.TargetMonkeyObjectId),
                GetSwitchDirectionDisplayName(instruction.SwitchDirection),
                instruction.SwitchCount),
            ScriptCommandType.SetMonkeyAbility => string.Format(
                _localizationService.T("Editor.Display.SetMonkeyAbility"),
                GetMonkeyObjectDisplayName(instruction.TargetMonkeyObjectId),
                GetAbilityDisplayNumber(instruction.SelectedAbility),
                instruction.RequiresAbilityCoordinate
                    ? string.Format(
                        _localizationService.T("Editor.Display.SetMonkeyAbility.WithCoordinateSuffix"),
                        FormatCoordinate(instruction.AbilityCoordinateX),
                        FormatCoordinate(instruction.AbilityCoordinateY))
                    : string.Empty),
            ScriptCommandType.SellMonkey => string.Format(
                _localizationService.T("Editor.Display.SellMonkey"),
                GetMonkeyObjectDisplayName(instruction.TargetMonkeyObjectId)),
            ScriptCommandType.PlaceHeroInventory => string.Format(
                _localizationService.T("Editor.Display.PlaceHeroInventory"),
                GetInventoryDisplayName(instruction.SelectedInventoryItem),
                instruction.RequiresAbilityCoordinate
                    ? string.Format(
                        _localizationService.T("Editor.Display.PlaceHeroInventory.WithCoordinateSuffix"),
                        FormatCoordinate(instruction.PositionX),
                        FormatCoordinate(instruction.PositionY))
                    : string.Empty),
            ScriptCommandType.ActivateAbility => string.Format(
                _localizationService.T("Editor.Display.ActivateAbility"),
                GetActivatedAbilityDisplayName(instruction.SelectedActivatedAbility),
                instruction.RequiresAbilityCoordinate
                    ? string.Format(
                        _localizationService.T("Editor.Display.ActivateAbility.WithCoordinateSuffix"),
                        FormatCoordinate(instruction.AbilityCoordinateX),
                        FormatCoordinate(instruction.AbilityCoordinateY))
                    : string.Empty),
            ScriptCommandType.NextRound => GetNextRoundActionDisplayName(instruction),
            _ => _localizationService.T(instruction.NameKey)
        };

        instruction.DisplayName = text;
    }

    private static bool IsHeroObjectKey(string objectKey)
    {
        return objectKey.StartsWith("Hero:", StringComparison.OrdinalIgnoreCase);
    }

    private string GetTowerDisplayName(MonkeyTowerType towerType)
    {
        var tower = GameElementCatalog.MonkeyTowers.FirstOrDefault(x => x.Type == towerType);
        return tower is null ? towerType.ToString() : _localizationService.T(tower.NameKey);
    }

    private string GetPlaceSelectionDisplayName(string selectionCode)
    {
        var normalized = NormalizePlaceSelectionCode(selectionCode);
        if (TryParseTowerSelection(normalized, out var towerType))
        {
            return GetTowerDisplayName(towerType);
        }

        if (TryParseHeroSelection(normalized, out var heroType))
        {
            return BuildHeroObjectDisplayName(heroType);
        }

        return GetTowerDisplayName(MonkeyTowerType.DartMonkey);
    }

    private string GetMonkeyObjectDisplayName(string objectKey)
    {
        return MonkeyObjectOptions.FirstOrDefault(x => string.Equals(x.Code, objectKey, StringComparison.OrdinalIgnoreCase))?.DisplayName
               ?? _localizationService.T("Editor.Property.TargetMonkey");
    }

    private string GetUpgradePathDisplayName(UpgradePathType path)
    {
        return path switch
        {
            UpgradePathType.Top => _localizationService.T("Editor.Display.Path.Top"),
            UpgradePathType.Middle => _localizationService.T("Editor.Display.Path.Middle"),
            UpgradePathType.Bottom => _localizationService.T("Editor.Display.Path.Bottom"),
            _ => _localizationService.T("Editor.Display.Path.Top")
        };
    }

    private string GetSwitchDirectionDisplayName(SwitchDirectionType direction)
    {
        return direction switch
        {
            SwitchDirectionType.Right => _localizationService.T("Editor.Display.Direction.Right"),
            SwitchDirectionType.Left => _localizationService.T("Editor.Display.Direction.Left"),
            _ => _localizationService.T("Editor.Display.Direction.Right")
        };
    }

    private static int GetAbilityDisplayNumber(MonkeyAbilityType ability)
    {
        return ability == MonkeyAbilityType.Ability2 ? 2 : 1;
    }

    private string GetInventoryDisplayName(string inventoryCode)
    {
        return Enum.TryParse<InventoryType>(inventoryCode, out var inventoryType)
            ? GameElementCatalog.GetInventoryDisplayName(inventoryType)
            : _localizationService.T("Editor.Property.Inventory");
    }

    private string GetActivatedAbilityDisplayName(string activatedAbilityCode)
    {
        return Enum.TryParse<ActivatedAbilityType>(activatedAbilityCode, out var abilityType)
            ? GameElementCatalog.GetActivatedAbilityDisplayName(abilityType)
            : _localizationService.T("Editor.Property.ActivatedAbility");
    }

    private string GetNextRoundActionDisplayName(ScriptInstructionInstance instruction)
    {
        return instruction.NextRoundAction switch
        {
            "SendNextRound" => string.Format(
                _localizationService.T("Editor.Display.NextRound.SendNextRound"),
                instruction.NextRoundSendCount),
            _ => _localizationService.T("Editor.Display.NextRound.PlayFastForward")
        };
    }

    private static string FormatCoordinate(double value)
    {
        return value.ToString("0.##");
    }

    private void RaiseLocalizedProperties()
    {
        OnPropertyChanged(nameof(MapItems));
        OnPropertyChanged(nameof(MonkeyTowerItems));
        OnPropertyChanged(nameof(WorkspaceTitle));
        OnPropertyChanged(nameof(WorkspaceDescription));
        OnPropertyChanged(nameof(PreviewHint));
        OnPropertyChanged(nameof(ScriptText));
        OnPropertyChanged(nameof(StatsTitle));
        OnPropertyChanged(nameof(TotalScriptsText));
        OnPropertyChanged(nameof(SharedScriptsText));
        OnPropertyChanged(nameof(StatusTitle));
        OnPropertyChanged(nameof(DraftState));
        OnPropertyChanged(nameof(FileOpenText));
        OnPropertyChanged(nameof(FileSaveText));
        OnPropertyChanged(nameof(FileSaveAsText));
        OnPropertyChanged(nameof(FileNewText));
        OnPropertyChanged(nameof(FileCloseText));
        OnPropertyChanged(nameof(CurrentFileText));
        OnPropertyChanged(nameof(MetadataMapText));
        OnPropertyChanged(nameof(MetadataDifficultyText));
        OnPropertyChanged(nameof(MetadataModeText));
        OnPropertyChanged(nameof(MetadataHeroText));
        OnPropertyChanged(nameof(MetadataMapPlaceholderText));
        OnPropertyChanged(nameof(ScriptCategoryAllText));
        OnPropertyChanged(nameof(ScriptCategoryCollectionText));
        OnPropertyChanged(nameof(ScriptCategoryBlackBorderText));
        OnPropertyChanged(nameof(ScriptCategoryRaceText));
        OnPropertyChanged(nameof(DebugRunText));
        OnPropertyChanged(nameof(DebugStepText));
        OnPropertyChanged(nameof(DebugStopText));
        OnPropertyChanged(nameof(DebugValidateText));
        OnPropertyChanged(nameof(LibraryTitle));
        OnPropertyChanged(nameof(SequenceTitle));
        OnPropertyChanged(nameof(PropertiesTitle));
        OnPropertyChanged(nameof(SequenceEmptyText));
        OnPropertyChanged(nameof(PropertiesEmptyText));
        OnPropertyChanged(nameof(PropertyMonkeyTowerText));
        OnPropertyChanged(nameof(PropertyCoordinateText));
        OnPropertyChanged(nameof(PropertyTargetMonkeyText));
        OnPropertyChanged(nameof(PropertyUpgradePathText));
        OnPropertyChanged(nameof(PropertyUpgradeCountText));
        OnPropertyChanged(nameof(PropertySwitchDirectionText));
        OnPropertyChanged(nameof(PropertySwitchCountText));
        OnPropertyChanged(nameof(PropertyAbilityText));
        OnPropertyChanged(nameof(PropertyNeedAbilityCoordinateText));
        OnPropertyChanged(nameof(PropertyInventoryText));
        OnPropertyChanged(nameof(PropertyActivatedAbilityText));
        OnPropertyChanged(nameof(PropertyNeedCoordinateText));
        OnPropertyChanged(nameof(PropertyNextRoundActionText));
        OnPropertyChanged(nameof(PropertyNextRoundSendCountText));
        OnPropertyChanged(nameof(PropertyNotesText));
        OnPropertyChanged(nameof(DeleteSelectedInstructionText));
        OnPropertyChanged(nameof(CopySelectedInstructionText));
        OnPropertyChanged(nameof(PasteInstructionText));
    }
}
