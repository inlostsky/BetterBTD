using System.Collections.ObjectModel;
using BetterBTD.Helpers;
using BetterBTD.Models;
using BetterBTD.Models.AutoTasks;
using BetterBTD.Models.GameElements;
using BetterBTD.Models.MyScripts;
using BetterBTD.Models.ScriptEditor;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Wpf.Ui.Violeta.Controls;

namespace BetterBTD.ViewModels;

public sealed class MyScriptsPageViewModel : ObservableObject
{
    private readonly LocalizationService _localizationService;
    private readonly AppDialogService _appDialogService;
    private readonly ManagedScriptLibraryService _managedScriptLibraryService;

    private List<ManagedScriptListItemViewModel> _allScripts = [];
    private Dictionary<string, ManagedScriptSlotEntry> _blackBorderSlotsById = new(StringComparer.OrdinalIgnoreCase);
    private bool _isUpdatingFilters;
    private string _scriptSearchText = string.Empty;
    private GameMapType _selectedMap = GameMapType.MonkeyMeadow;
    private LanguageOption? _selectedDifficultyOption;
    private LanguageOption? _selectedModeOption;
    private ManagedScriptListItemViewModel? _selectedScript;
    private string _selectedScriptName = string.Empty;
    private string _selectedScriptDescription = string.Empty;
    private string _selectedScriptHero = string.Empty;
    private string _selectedScriptMap = string.Empty;
    private string _selectedScriptDifficulty = string.Empty;
    private string _selectedScriptMode = string.Empty;
    private string _selectedScriptTags = string.Empty;
    private string _selectedScriptState = string.Empty;
    private string _selectedBlackBorderTarget = string.Empty;
    private string _selectedBlackBorderBindingState = string.Empty;

    public MyScriptsPageViewModel(LocalizationService localizationService)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _appDialogService = AppDialogService.Instance;
        _managedScriptLibraryService = ManagedScriptLibraryService.Instance;

        RefreshCommand = new RelayCommand(Refresh);
        ImportScriptCommand = new RelayCommand(ImportScript);
        ExportSelectedScriptCommand = new RelayCommand(ExportSelectedScript, CanExportSelectedScript);
        RemoveSelectedScriptCommand = new RelayCommand(RemoveSelectedScript, CanRemoveSelectedScript);
        BindSelectedScriptToBlackBorderCommand = new RelayCommand(BindSelectedScriptToBlackBorder, CanBindSelectedScriptToBlackBorder);
        ClearSelectedBlackBorderBindingCommand = new RelayCommand(ClearSelectedBlackBorderBinding, CanClearSelectedBlackBorderBinding);

        _localizationService.LanguageChanged += (_, _) => RefreshLocalizedContent();

        RefreshLocalizedContent();
    }

    public ObservableCollection<LanguageOption> DifficultyOptions { get; } = [];

    public ObservableCollection<LanguageOption> ModeOptions { get; } = [];

    public ObservableCollection<ManagedScriptListItemViewModel> Scripts { get; } = [];

    public IReadOnlyList<ICascadingItem> MapItems => GameElementCascadingItems.MapItems;

    public IRelayCommand RefreshCommand { get; }

    public IRelayCommand ImportScriptCommand { get; }

    public IRelayCommand ExportSelectedScriptCommand { get; }

    public IRelayCommand RemoveSelectedScriptCommand { get; }

    public IRelayCommand BindSelectedScriptToBlackBorderCommand { get; }

    public IRelayCommand ClearSelectedBlackBorderBindingCommand { get; }

    public string ImportText => _localizationService.T("Library.Action.Import");

    public string ExportText => _localizationService.T("Library.Action.Export");

    public string RemoveText => _localizationService.T("Library.Action.Remove");

    public string RefreshText => _localizationService.T("Library.Action.Refresh");

    public string ScriptSearchLabel => _localizationService.T("Library.Filters.Search");

    public string ScriptSearchPlaceholder => _localizationService.T("Library.Filters.Search.Placeholder");

    public string MapFilterLabel => _localizationService.T("Library.Filters.Map");

    public string DifficultyFilterLabel => _localizationService.T("Library.Filters.Difficulty");

    public string ModeFilterLabel => _localizationService.T("Library.Filters.Mode");

    public string NameColumnText => _localizationService.T("Library.Column.Name");

    public string MapColumnText => _localizationService.T("Library.Column.Map");

    public string DifficultyColumnText => _localizationService.T("Library.Column.Difficulty");

    public string ModeColumnText => _localizationService.T("Library.Column.Mode");

    public string TagsColumnText => _localizationService.T("Library.Column.Tags");

    public string StateColumnText => _localizationService.T("Library.Column.State");

    public string SelectedScriptSummary => SelectedScript is null
        ? _localizationService.T("Library.Summary.None")
        : string.Format(
            _localizationService.T("Library.Summary.Script"),
            SelectedScript.SourceFileName,
            SelectedScript.ScriptId);

    public string PropertyNameText => _localizationService.T("Library.Property.Name");

    public string PropertyDescriptionText => _localizationService.T("Library.Property.Description");

    public string PropertyHeroText => _localizationService.T("Library.Property.Hero");

    public string PropertyMapText => _localizationService.T("Library.Property.Map");

    public string PropertyDifficultyText => _localizationService.T("Library.Property.Difficulty");

    public string PropertyModeText => _localizationService.T("Library.Property.Mode");

    public string PropertyTagsText => _localizationService.T("Library.Property.Tags");

    public string PropertyStateText => _localizationService.T("Library.Property.State");

    public string PropertyBlackBorderTargetText => _localizationService.T("Library.Property.BlackBorderTarget");

    public string PropertyBlackBorderBindingText => _localizationService.T("Library.Property.BlackBorderBinding");

    public string BindBlackBorderText => _localizationService.T("Library.Action.BindBlackBorder");

    public string ClearBlackBorderText => _localizationService.T("Library.Action.ClearBlackBorder");

    public string ScriptSearchText
    {
        get => _scriptSearchText;
        set
        {
            if (!SetProperty(ref _scriptSearchText, value))
            {
                return;
            }

            RefreshFilteredScripts();
        }
    }

    public GameMapType SelectedMap
    {
        get => _selectedMap;
        set
        {
            if (!SetProperty(ref _selectedMap, value))
            {
                return;
            }

            RefreshFilteredScripts();
        }
    }

    public LanguageOption? SelectedDifficultyOption
    {
        get => _selectedDifficultyOption;
        set
        {
            if (!SetProperty(ref _selectedDifficultyOption, value))
            {
                return;
            }

            if (_isUpdatingFilters)
            {
                return;
            }

            RefreshFilteredScripts();
        }
    }

    public LanguageOption? SelectedModeOption
    {
        get => _selectedModeOption;
        set
        {
            if (!SetProperty(ref _selectedModeOption, value))
            {
                return;
            }

            if (_isUpdatingFilters)
            {
                return;
            }

            RefreshFilteredScripts();
        }
    }

    public ManagedScriptListItemViewModel? SelectedScript
    {
        get => _selectedScript;
        set
        {
            if (!SetProperty(ref _selectedScript, value))
            {
                return;
            }

            UpdateSelectedScriptDetails();
            OnPropertyChanged(nameof(SelectedScriptSummary));
            ExportSelectedScriptCommand.NotifyCanExecuteChanged();
            RemoveSelectedScriptCommand.NotifyCanExecuteChanged();
            BindSelectedScriptToBlackBorderCommand.NotifyCanExecuteChanged();
            ClearSelectedBlackBorderBindingCommand.NotifyCanExecuteChanged();
        }
    }

    public string SelectedScriptName
    {
        get => _selectedScriptName;
        private set => SetProperty(ref _selectedScriptName, value);
    }

    public string SelectedScriptDescription
    {
        get => _selectedScriptDescription;
        private set => SetProperty(ref _selectedScriptDescription, value);
    }

    public string SelectedScriptHero
    {
        get => _selectedScriptHero;
        private set => SetProperty(ref _selectedScriptHero, value);
    }

    public string SelectedScriptMap
    {
        get => _selectedScriptMap;
        private set => SetProperty(ref _selectedScriptMap, value);
    }

    public string SelectedScriptDifficulty
    {
        get => _selectedScriptDifficulty;
        private set => SetProperty(ref _selectedScriptDifficulty, value);
    }

    public string SelectedScriptMode
    {
        get => _selectedScriptMode;
        private set => SetProperty(ref _selectedScriptMode, value);
    }

    public string SelectedScriptTags
    {
        get => _selectedScriptTags;
        private set => SetProperty(ref _selectedScriptTags, value);
    }

    public string SelectedScriptState
    {
        get => _selectedScriptState;
        private set => SetProperty(ref _selectedScriptState, value);
    }

    public string SelectedBlackBorderTarget
    {
        get => _selectedBlackBorderTarget;
        private set => SetProperty(ref _selectedBlackBorderTarget, value);
    }

    public string SelectedBlackBorderBindingState
    {
        get => _selectedBlackBorderBindingState;
        private set => SetProperty(ref _selectedBlackBorderBindingState, value);
    }

    private void Refresh()
    {
        var snapshot = _managedScriptLibraryService.GetSnapshot();
        _blackBorderSlotsById = snapshot.Slots
            .Where(x => x.Definition.TaskKind == AutoTaskKind.BlackBorder)
            .ToDictionary(x => x.Definition.SlotId, StringComparer.OrdinalIgnoreCase);
        _allScripts = snapshot.Scripts.Select(CreateScriptItem).ToList();
        BuildFilterOptions();
        RefreshFilteredScripts();
    }

    private void ImportScript()
    {
        var dialog = new OpenFileDialog
        {
            Filter = _localizationService.T("Library.File.ImportFilter"),
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            _managedScriptLibraryService.ImportScript(dialog.FileName);
            Refresh();
        }
        catch (Exception ex)
        {
            ShowError("Library.Dialog.ImportError.Title", ex.Message);
        }
    }

    private bool CanExportSelectedScript()
    {
        return SelectedScript is not null;
    }

    private void ExportSelectedScript()
    {
        if (SelectedScript is null)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = _localizationService.T("Library.File.ExportFilter"),
            FileName = $"{SelectedScript.DisplayName}.btd"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            _managedScriptLibraryService.ExportScript(SelectedScript.ScriptId, dialog.FileName);
        }
        catch (Exception ex)
        {
            ShowError("Library.Dialog.ExportError.Title", ex.Message);
        }
    }

    private bool CanRemoveSelectedScript()
    {
        return SelectedScript is not null;
    }

    private void RemoveSelectedScript()
    {
        if (SelectedScript is null)
        {
            return;
        }

        var result = _appDialogService.Show(new AppDialogRequest
        {
            Title = _localizationService.T("Library.Dialog.Remove.Title"),
            Message = _localizationService.T("Library.Dialog.Remove.Message"),
            PrimaryButtonText = _localizationService.T("Library.Dialog.Primary"),
            SecondaryButtonText = _localizationService.T("Library.Dialog.Cancel")
        });

        if (result != AppDialogResult.Primary)
        {
            return;
        }

        try
        {
            _managedScriptLibraryService.RemoveScript(SelectedScript.ScriptId);
            Refresh();
        }
        catch (Exception ex)
        {
            ShowError("Library.Dialog.RemoveError.Title", ex.Message);
        }
    }

    private bool CanBindSelectedScriptToBlackBorder()
    {
        return SelectedScript?.CanBindToBlackBorder == true;
    }

    private void BindSelectedScriptToBlackBorder()
    {
        if (SelectedScript is null || string.IsNullOrWhiteSpace(SelectedScript.BlackBorderSlotId))
        {
            return;
        }

        try
        {
            _managedScriptLibraryService.SetBinding(SelectedScript.BlackBorderSlotId, SelectedScript.ScriptId);
            Refresh();
            RestoreSelection(SelectedScript.ScriptId);
        }
        catch (Exception ex)
        {
            ShowError("Library.Dialog.BindingError.Title", ex.Message);
        }
    }

    private bool CanClearSelectedBlackBorderBinding()
    {
        return SelectedScript?.IsBoundToBlackBorder == true;
    }

    private void ClearSelectedBlackBorderBinding()
    {
        if (SelectedScript is null || string.IsNullOrWhiteSpace(SelectedScript.BlackBorderSlotId))
        {
            return;
        }

        try
        {
            _managedScriptLibraryService.SetBinding(SelectedScript.BlackBorderSlotId, null);
            Refresh();
            RestoreSelection(SelectedScript.ScriptId);
        }
        catch (Exception ex)
        {
            ShowError("Library.Dialog.BindingError.Title", ex.Message);
        }
    }

    private void RefreshFilteredScripts()
    {
        var selectedScriptId = SelectedScript?.ScriptId;

        var filteredScripts = _allScripts
            .Where(MatchesScriptFilters)
            .OrderByDescending(x => x.UpdatedAt)
            .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        ReplaceCollection(Scripts, filteredScripts);
        SelectedScript = Scripts.FirstOrDefault(x => x.ScriptId == selectedScriptId) ?? Scripts.FirstOrDefault();
    }

    private bool MatchesScriptFilters(ManagedScriptListItemViewModel script)
    {
        if (script.MapType != SelectedMap)
        {
            return false;
        }

        if (SelectedDifficultyOption?.Code.Length > 0 &&
            !string.Equals(script.DifficultyCode, SelectedDifficultyOption.Code, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (SelectedModeOption?.Code.Length > 0 &&
            !string.Equals(script.ModeCode, SelectedModeOption.Code, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(ScriptSearchText))
        {
            return true;
        }

        var query = ScriptSearchText.Trim();
        return script.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               script.MapDisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               script.TagsText.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               script.HeroDisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               script.Description.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private ManagedScriptListItemViewModel CreateScriptItem(ManagedScriptAssetEntry entry)
    {
        var blackBorderSlotId = ManagedScriptSlotIdFactory.CreateBlackBorderSlotId(entry.Map, entry.Difficulty, entry.Mode);
        _blackBorderSlotsById.TryGetValue(blackBorderSlotId, out var blackBorderSlot);
        var isBoundToBlackBorder = blackBorderSlot?.BoundScriptId.Equals(entry.ScriptId, StringComparison.OrdinalIgnoreCase) == true;

        return new ManagedScriptListItemViewModel
        {
            ScriptId = entry.ScriptId,
            DisplayName = entry.DisplayName,
            Description = entry.Description,
            SourceFileName = entry.SourceFileName,
            MapType = entry.Map,
            DifficultyCode = entry.Difficulty.ToString(),
            ModeCode = entry.Mode.ToString(),
            MapDisplayName = GameElementCatalog.GetMapDisplayName(entry.Map),
            DifficultyDisplayName = GameElementCatalog.GetStageDifficultyDisplayName(entry.Difficulty),
            ModeDisplayName = GameElementCatalog.GetStageModeDisplayName(entry.Mode),
            HeroDisplayName = GameElementCatalog.GetHeroDisplayName(entry.Hero),
            TagsText = entry.Tags.Count == 0
                ? string.Empty
                : string.Join(", ", entry.Tags.Select(ScriptTagCatalog.GetDisplayName)),
            StateText = ResolveScriptStateText(entry),
            UpdatedAt = entry.UpdatedAt,
            BlackBorderSlotId = blackBorderSlotId,
            BlackBorderTargetText = $"{GameElementCatalog.GetMapDisplayName(entry.Map)} / {GameElementCatalog.GetStageDifficultyDisplayName(entry.Difficulty)} / {GameElementCatalog.GetStageModeDisplayName(entry.Mode)}",
            BlackBorderBindingStateText = ResolveBlackBorderBindingStateText(entry, blackBorderSlot),
            IsBoundToBlackBorder = isBoundToBlackBorder,
            CanBindToBlackBorder = !entry.HasMissingFile && !entry.HasMetadataIssue
        };
    }

    private string ResolveScriptStateText(ManagedScriptAssetEntry entry)
    {
        if (entry.HasMissingFile)
        {
            return _localizationService.T("Library.State.MissingFile");
        }

        if (entry.HasMetadataIssue)
        {
            return _localizationService.T("Library.State.MetadataIssue");
        }

        return _localizationService.T("Library.State.Ready");
    }

    private string ResolveBlackBorderBindingStateText(
        ManagedScriptAssetEntry entry,
        ManagedScriptSlotEntry? blackBorderSlot)
    {
        if (entry.HasMetadataIssue)
        {
            return _localizationService.T("Library.State.MetadataIssue");
        }

        if (blackBorderSlot is null)
        {
            return _localizationService.T("Library.State.Unbound");
        }

        if (!blackBorderSlot.HasBinding)
        {
            return _localizationService.T("Library.BlackBorder.Unbound");
        }

        if (blackBorderSlot.IsBrokenBinding)
        {
            return _localizationService.T("Library.BlackBorder.Broken");
        }

        if (string.Equals(blackBorderSlot.BoundScriptId, entry.ScriptId, StringComparison.OrdinalIgnoreCase))
        {
            return _localizationService.T("Library.BlackBorder.BoundToCurrent");
        }

        return string.Format(
            _localizationService.T("Library.BlackBorder.BoundToOther"),
            blackBorderSlot.BoundScript?.DisplayName ?? blackBorderSlot.BoundScriptId);
    }

    private void BuildFilterOptions()
    {
        var allText = _localizationService.T("Library.Filters.All");
        var previousDifficulty = SelectedDifficultyOption?.Code ?? string.Empty;
        var previousMode = SelectedModeOption?.Code ?? string.Empty;

        _isUpdatingFilters = true;

        try
        {
            ReplaceCollection(
                DifficultyOptions,
                new[]
                {
                    new LanguageOption { Code = string.Empty, DisplayName = allText },
                    new LanguageOption { Code = StageDifficulty.Easy.ToString(), DisplayName = GameElementCatalog.GetStageDifficultyDisplayName(StageDifficulty.Easy) },
                    new LanguageOption { Code = StageDifficulty.Medium.ToString(), DisplayName = GameElementCatalog.GetStageDifficultyDisplayName(StageDifficulty.Medium) },
                    new LanguageOption { Code = StageDifficulty.Hard.ToString(), DisplayName = GameElementCatalog.GetStageDifficultyDisplayName(StageDifficulty.Hard) }
                });

            ReplaceCollection(
                ModeOptions,
                new[]
                {
                    new LanguageOption { Code = string.Empty, DisplayName = allText }
                }.Concat(Enum.GetValues<StageMode>().Select(mode => new LanguageOption
                    {
                        Code = mode.ToString(),
                        DisplayName = GameElementCatalog.GetStageModeDisplayName(mode)
                    })));

            SelectedDifficultyOption = DifficultyOptions.FirstOrDefault(x => x.Code == previousDifficulty) ?? DifficultyOptions.FirstOrDefault();
            SelectedModeOption = ModeOptions.FirstOrDefault(x => x.Code == previousMode) ?? ModeOptions.FirstOrDefault();
        }
        finally
        {
            _isUpdatingFilters = false;
        }
    }

    private void RestoreSelection(string? scriptId)
    {
        SelectedScript = Scripts.FirstOrDefault(x => string.Equals(x.ScriptId, scriptId, StringComparison.OrdinalIgnoreCase))
                         ?? Scripts.FirstOrDefault();
    }

    private void UpdateSelectedScriptDetails()
    {
        if (SelectedScript is null)
        {
            SelectedScriptName = string.Empty;
            SelectedScriptDescription = string.Empty;
            SelectedScriptHero = string.Empty;
            SelectedScriptMap = string.Empty;
            SelectedScriptDifficulty = string.Empty;
            SelectedScriptMode = string.Empty;
            SelectedScriptTags = string.Empty;
            SelectedScriptState = string.Empty;
            SelectedBlackBorderTarget = string.Empty;
            SelectedBlackBorderBindingState = string.Empty;
            return;
        }

        SelectedScriptName = SelectedScript.DisplayName;
        SelectedScriptDescription = SelectedScript.Description;
        SelectedScriptHero = SelectedScript.HeroDisplayName;
        SelectedScriptMap = SelectedScript.MapDisplayName;
        SelectedScriptDifficulty = SelectedScript.DifficultyDisplayName;
        SelectedScriptMode = SelectedScript.ModeDisplayName;
        SelectedScriptTags = SelectedScript.TagsText;
        SelectedScriptState = SelectedScript.StateText;
        SelectedBlackBorderTarget = SelectedScript.BlackBorderTargetText;
        SelectedBlackBorderBindingState = SelectedScript.BlackBorderBindingStateText;
    }

    private void RefreshLocalizedContent()
    {
        Refresh();

        OnPropertyChanged(nameof(ImportText));
        OnPropertyChanged(nameof(ExportText));
        OnPropertyChanged(nameof(RemoveText));
        OnPropertyChanged(nameof(RefreshText));
        OnPropertyChanged(nameof(ScriptSearchLabel));
        OnPropertyChanged(nameof(ScriptSearchPlaceholder));
        OnPropertyChanged(nameof(MapItems));
        OnPropertyChanged(nameof(MapFilterLabel));
        OnPropertyChanged(nameof(DifficultyFilterLabel));
        OnPropertyChanged(nameof(ModeFilterLabel));
        OnPropertyChanged(nameof(NameColumnText));
        OnPropertyChanged(nameof(MapColumnText));
        OnPropertyChanged(nameof(DifficultyColumnText));
        OnPropertyChanged(nameof(ModeColumnText));
        OnPropertyChanged(nameof(TagsColumnText));
        OnPropertyChanged(nameof(StateColumnText));
        OnPropertyChanged(nameof(SelectedScriptSummary));
        OnPropertyChanged(nameof(PropertyNameText));
        OnPropertyChanged(nameof(PropertyDescriptionText));
        OnPropertyChanged(nameof(PropertyHeroText));
        OnPropertyChanged(nameof(PropertyMapText));
        OnPropertyChanged(nameof(PropertyDifficultyText));
        OnPropertyChanged(nameof(PropertyModeText));
        OnPropertyChanged(nameof(PropertyTagsText));
        OnPropertyChanged(nameof(PropertyStateText));
        OnPropertyChanged(nameof(PropertyBlackBorderTargetText));
        OnPropertyChanged(nameof(PropertyBlackBorderBindingText));
        OnPropertyChanged(nameof(BindBlackBorderText));
        OnPropertyChanged(nameof(ClearBlackBorderText));
    }

    private void ShowError(string titleKey, string message)
    {
        _appDialogService.Show(new AppDialogRequest
        {
            Title = _localizationService.T(titleKey),
            Message = message,
            PrimaryButtonText = _localizationService.T("Library.Dialog.Primary")
        });
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }
}

public sealed class ManagedScriptListItemViewModel
{
    public required string ScriptId { get; init; }

    public required string DisplayName { get; init; }

    public required string Description { get; init; }

    public required string SourceFileName { get; init; }

    public required GameMapType MapType { get; init; }

    public required string DifficultyCode { get; init; }

    public required string ModeCode { get; init; }

    public required string MapDisplayName { get; init; }

    public required string DifficultyDisplayName { get; init; }

    public required string ModeDisplayName { get; init; }

    public required string HeroDisplayName { get; init; }

    public required string TagsText { get; init; }

    public required string StateText { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    public required string BlackBorderSlotId { get; init; }

    public required string BlackBorderTargetText { get; init; }

    public required string BlackBorderBindingStateText { get; init; }

    public bool IsBoundToBlackBorder { get; init; }

    public bool CanBindToBlackBorder { get; init; }
}
