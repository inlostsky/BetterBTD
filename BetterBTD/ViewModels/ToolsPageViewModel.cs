using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BetterBTD.Models;
using BetterBTD.Models.GameElements;
using BetterBTD.Services;

namespace BetterBTD.ViewModels;

public sealed class ToolsPageViewModel : ObservableObject
{
    private readonly LocalizationService _localizationService;

    private int _startRound = 1;
    private int _endRound = 100;
    private int _heroPlacementRound = 1;
    private string _heroTargetRound = string.Empty;
    private string _heroTargetLevel = string.Empty;
    private double _paragonTotalPops;
    private int _paragonUpgradeCount;
    private double _paragonExtraCash;
    private LanguageOption? _selectedHero;
    private LanguageOption? _selectedParagonMonkey;
    private string _roundResultText = string.Empty;
    private string _heroResultText = string.Empty;
    private string _paragonResultText = string.Empty;

    public ToolsPageViewModel()
        : this(LocalizationService.Instance)
    {
    }

    internal ToolsPageViewModel(LocalizationService localizationService)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));

        HeroOptions = [];
        ParagonMonkeyOptions = [];

        CalculateRoundCommand = new RelayCommand(UpdateRoundResult);
        CalculateHeroCommand = new RelayCommand(UpdateHeroResult);
        CalculateParagonCommand = new RelayCommand(UpdateParagonResult);

        _localizationService.LanguageChanged += (_, _) => RefreshLocalizedContent();
        RefreshLocalizedContent();
    }

    public ObservableCollection<LanguageOption> HeroOptions { get; }

    public ObservableCollection<LanguageOption> ParagonMonkeyOptions { get; }

    public IRelayCommand CalculateRoundCommand { get; }

    public IRelayCommand CalculateHeroCommand { get; }

    public IRelayCommand CalculateParagonCommand { get; }

    public string PageTitle => _localizationService.T("Tools.PageTitle");

    public string PageDescription => _localizationService.T("Tools.PageDescription");

    public string ParametersSectionTitle => _localizationService.T("Tools.Section.Parameters");

    public string ParametersSectionDescription => _localizationService.T("Tools.Section.ParametersDescription");

    public string ResultSectionTitle => _localizationService.T("Tools.Section.Result");

    public string ResultSectionDescription => _localizationService.T("Tools.Section.ResultDescription");

    public string CalculateButtonText => _localizationService.T("Tools.Action.Calculate");

    public string RoundCardTitle => _localizationService.T("Tools.Round.Title");

    public string RoundCardDescription => _localizationService.T("Tools.Round.Description");

    public string StartRoundLabel => _localizationService.T("Tools.Round.StartRound");

    public string StartRoundDescription => _localizationService.T("Tools.Round.StartRoundDescription");

    public string EndRoundLabel => _localizationService.T("Tools.Round.EndRound");

    public string EndRoundDescription => _localizationService.T("Tools.Round.EndRoundDescription");

    public string HeroCardTitle => _localizationService.T("Tools.Hero.Title");

    public string HeroCardDescription => _localizationService.T("Tools.Hero.Description");

    public string HeroLabel => _localizationService.T("Tools.Hero.Hero");

    public string HeroDescription => _localizationService.T("Tools.Hero.HeroDescription");

    public string HeroPlacementRoundLabel => _localizationService.T("Tools.Hero.PlacementRound");

    public string HeroPlacementRoundDescription => _localizationService.T("Tools.Hero.PlacementRoundDescription");

    public string HeroTargetRoundLabel => _localizationService.T("Tools.Hero.TargetRound");

    public string HeroTargetRoundDescription => _localizationService.T("Tools.Hero.TargetRoundDescription");

    public string HeroTargetLevelLabel => _localizationService.T("Tools.Hero.TargetLevel");

    public string HeroTargetLevelDescription => _localizationService.T("Tools.Hero.TargetLevelDescription");

    public string HeroHintText => _localizationService.T("Tools.Hero.Hint");

    public string ParagonCardTitle => _localizationService.T("Tools.Paragon.Title");

    public string ParagonCardDescription => _localizationService.T("Tools.Paragon.Description");

    public string ParagonMonkeyLabel => _localizationService.T("Tools.Paragon.Monkey");

    public string ParagonMonkeyDescription => _localizationService.T("Tools.Paragon.MonkeyDescription");

    public string ParagonTotalPopsLabel => _localizationService.T("Tools.Paragon.TotalPops");

    public string ParagonTotalPopsDescription => _localizationService.T("Tools.Paragon.TotalPopsDescription");

    public string ParagonUpgradeCountLabel => _localizationService.T("Tools.Paragon.UpgradeCount");

    public string ParagonUpgradeCountDescription => _localizationService.T("Tools.Paragon.UpgradeCountDescription");

    public string ParagonExtraCashLabel => _localizationService.T("Tools.Paragon.ExtraCash");

    public string ParagonExtraCashDescription => _localizationService.T("Tools.Paragon.ExtraCashDescription");

    public int StartRound
    {
        get => _startRound;
        set
        {
            if (SetProperty(ref _startRound, value))
            {
                UpdateRoundResult();
            }
        }
    }

    public int EndRound
    {
        get => _endRound;
        set
        {
            if (SetProperty(ref _endRound, value))
            {
                UpdateRoundResult();
            }
        }
    }

    public LanguageOption? SelectedHero
    {
        get => _selectedHero;
        set
        {
            if (SetProperty(ref _selectedHero, value))
            {
                UpdateHeroResult();
            }
        }
    }

    public int HeroPlacementRound
    {
        get => _heroPlacementRound;
        set
        {
            if (SetProperty(ref _heroPlacementRound, value))
            {
                UpdateHeroResult();
            }
        }
    }

    public string HeroTargetRound
    {
        get => _heroTargetRound;
        set
        {
            if (SetProperty(ref _heroTargetRound, value))
            {
                UpdateHeroResult();
            }
        }
    }

    public string HeroTargetLevel
    {
        get => _heroTargetLevel;
        set
        {
            if (SetProperty(ref _heroTargetLevel, value))
            {
                UpdateHeroResult();
            }
        }
    }

    public LanguageOption? SelectedParagonMonkey
    {
        get => _selectedParagonMonkey;
        set
        {
            if (SetProperty(ref _selectedParagonMonkey, value))
            {
                UpdateParagonResult();
            }
        }
    }

    public double ParagonTotalPops
    {
        get => _paragonTotalPops;
        set
        {
            if (SetProperty(ref _paragonTotalPops, value))
            {
                UpdateParagonResult();
            }
        }
    }

    public int ParagonUpgradeCount
    {
        get => _paragonUpgradeCount;
        set
        {
            if (SetProperty(ref _paragonUpgradeCount, value))
            {
                UpdateParagonResult();
            }
        }
    }

    public double ParagonExtraCash
    {
        get => _paragonExtraCash;
        set
        {
            if (SetProperty(ref _paragonExtraCash, value))
            {
                UpdateParagonResult();
            }
        }
    }

    public string RoundResultText
    {
        get => _roundResultText;
        private set => SetProperty(ref _roundResultText, value);
    }

    public string HeroResultText
    {
        get => _heroResultText;
        private set => SetProperty(ref _heroResultText, value);
    }

    public string ParagonResultText
    {
        get => _paragonResultText;
        private set => SetProperty(ref _paragonResultText, value);
    }

    private void RefreshLocalizedContent()
    {
        var selectedHeroCode = SelectedHero?.Code;
        var selectedParagonCode = SelectedParagonMonkey?.Code;

        RefreshHeroOptions(selectedHeroCode);
        RefreshParagonMonkeyOptions(selectedParagonCode);

        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(PageDescription));
        OnPropertyChanged(nameof(ParametersSectionTitle));
        OnPropertyChanged(nameof(ParametersSectionDescription));
        OnPropertyChanged(nameof(ResultSectionTitle));
        OnPropertyChanged(nameof(ResultSectionDescription));
        OnPropertyChanged(nameof(CalculateButtonText));
        OnPropertyChanged(nameof(RoundCardTitle));
        OnPropertyChanged(nameof(RoundCardDescription));
        OnPropertyChanged(nameof(StartRoundLabel));
        OnPropertyChanged(nameof(StartRoundDescription));
        OnPropertyChanged(nameof(EndRoundLabel));
        OnPropertyChanged(nameof(EndRoundDescription));
        OnPropertyChanged(nameof(HeroCardTitle));
        OnPropertyChanged(nameof(HeroCardDescription));
        OnPropertyChanged(nameof(HeroLabel));
        OnPropertyChanged(nameof(HeroDescription));
        OnPropertyChanged(nameof(HeroPlacementRoundLabel));
        OnPropertyChanged(nameof(HeroPlacementRoundDescription));
        OnPropertyChanged(nameof(HeroTargetRoundLabel));
        OnPropertyChanged(nameof(HeroTargetRoundDescription));
        OnPropertyChanged(nameof(HeroTargetLevelLabel));
        OnPropertyChanged(nameof(HeroTargetLevelDescription));
        OnPropertyChanged(nameof(HeroHintText));
        OnPropertyChanged(nameof(ParagonCardTitle));
        OnPropertyChanged(nameof(ParagonCardDescription));
        OnPropertyChanged(nameof(ParagonMonkeyLabel));
        OnPropertyChanged(nameof(ParagonMonkeyDescription));
        OnPropertyChanged(nameof(ParagonTotalPopsLabel));
        OnPropertyChanged(nameof(ParagonTotalPopsDescription));
        OnPropertyChanged(nameof(ParagonUpgradeCountLabel));
        OnPropertyChanged(nameof(ParagonUpgradeCountDescription));
        OnPropertyChanged(nameof(ParagonExtraCashLabel));
        OnPropertyChanged(nameof(ParagonExtraCashDescription));

        UpdateRoundResult();
        UpdateHeroResult();
        UpdateParagonResult();
    }

    private void RefreshHeroOptions(string? selectedCode)
    {
        HeroOptions.Clear();
        foreach (var hero in GameElementCatalog.Heroes)
        {
            HeroOptions.Add(new LanguageOption
            {
                Code = hero.Type.ToString(),
                DisplayName = GameElementCatalog.GetHeroDisplayName(hero.Type)
            });
        }

        SelectedHero = SelectOption(HeroOptions, selectedCode) ?? HeroOptions.FirstOrDefault();
    }

    private void RefreshParagonMonkeyOptions(string? selectedCode)
    {
        ParagonMonkeyOptions.Clear();
        foreach (var monkey in GameElementCatalog.MonkeyTowers)
        {
            ParagonMonkeyOptions.Add(new LanguageOption
            {
                Code = monkey.Type.ToString(),
                DisplayName = GameElementCatalog.GetMonkeyTowerDisplayName(monkey.Type)
            });
        }

        SelectedParagonMonkey = SelectOption(ParagonMonkeyOptions, selectedCode) ?? ParagonMonkeyOptions.FirstOrDefault();
    }

    private void UpdateRoundResult()
    {
        RoundResultText = string.Format(
            _localizationService.T("Tools.Round.ResultPlaceholder"),
            StartRound,
            EndRound);
    }

    private void UpdateHeroResult()
    {
        var heroName = SelectedHero?.DisplayName ?? _localizationService.T("Tools.Hero.Hero");
        var hasTargetRound = !string.IsNullOrWhiteSpace(HeroTargetRound);
        var hasTargetLevel = !string.IsNullOrWhiteSpace(HeroTargetLevel);

        if (hasTargetRound && hasTargetLevel)
        {
            HeroResultText = _localizationService.T("Tools.Hero.Result.BothTargets");
            return;
        }

        if (hasTargetRound)
        {
            HeroResultText = string.Format(
                _localizationService.T("Tools.Hero.Result.TargetRound"),
                heroName,
                HeroPlacementRound,
                HeroTargetRound.Trim());
            return;
        }

        if (hasTargetLevel)
        {
            HeroResultText = string.Format(
                _localizationService.T("Tools.Hero.Result.TargetLevel"),
                heroName,
                HeroPlacementRound,
                HeroTargetLevel.Trim());
            return;
        }

        HeroResultText = _localizationService.T("Tools.Hero.Result.NoTarget");
    }

    private void UpdateParagonResult()
    {
        var monkeyName = SelectedParagonMonkey?.DisplayName ?? _localizationService.T("Tools.Paragon.Monkey");
        ParagonResultText = string.Format(
            _localizationService.T("Tools.Paragon.ResultPlaceholder"),
            monkeyName,
            FormatWholeNumber(ParagonTotalPops),
            ParagonUpgradeCount,
            FormatWholeNumber(ParagonExtraCash));
    }

    private static LanguageOption? SelectOption(IEnumerable<LanguageOption> options, string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        return options.FirstOrDefault(option => string.Equals(option.Code, code, StringComparison.OrdinalIgnoreCase));
    }

    private static string FormatWholeNumber(double value)
    {
        return Math.Round(value).ToString("0");
    }
}
