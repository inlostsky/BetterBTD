using System;
using System.Collections.Generic;

namespace BetterBTD.Services.Shell.Localization;

public sealed partial class LocalizationService
{
    private static Dictionary<string, string> BuildZhCnToolsResources() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["Tools.PageTitle"] = "小工具",
        ["Tools.PageDescription"] = "先提供回合、英雄等级和模范度三类计算器的卡片式 UI，后续再接入具体公式。",
        ["Tools.Section.Parameters"] = "参数",
        ["Tools.Section.ParametersDescription"] = "折叠查看与编辑当前计算所需参数。",
        ["Tools.Section.Result"] = "计算结果",
        ["Tools.Section.ResultDescription"] = "结果区域已预留，当前展示 UI 占位信息。",
        ["Tools.Action.Calculate"] = "计算",

        ["Tools.Round.Title"] = "回合计算器",
        ["Tools.Round.Description"] = "输入起始回合和结束回合，后续用于计算区间收益、耗时或事件跨度。",
        ["Tools.Round.StartRound"] = "起始回合",
        ["Tools.Round.StartRoundDescription"] = "计算开始的回合编号。",
        ["Tools.Round.EndRound"] = "结束回合",
        ["Tools.Round.EndRoundDescription"] = "计算结束的回合编号。",
        ["Tools.Round.ResultPlaceholder"] = "已记录回合区间：{0} 到 {1}。具体公式待接入。",

        ["Tools.Hero.Title"] = "英雄等级计算器",
        ["Tools.Hero.Description"] = "根据英雄与放置回合，推算目标回合或目标等级中的另一项。",
        ["Tools.Hero.Hero"] = "英雄",
        ["Tools.Hero.HeroDescription"] = "选择需要计算经验曲线的英雄。",
        ["Tools.Hero.PlacementRound"] = "放置回合",
        ["Tools.Hero.PlacementRoundDescription"] = "英雄实际落地的回合。",
        ["Tools.Hero.TargetRound"] = "目标回合",
        ["Tools.Hero.TargetRoundDescription"] = "填写后，后续可反推该回合能达到的等级。",
        ["Tools.Hero.TargetLevel"] = "目标等级",
        ["Tools.Hero.TargetLevelDescription"] = "填写后，后续可反推到达该等级所需回合。",
        ["Tools.Hero.Hint"] = "目标回合与目标等级任选其一填写，当前先保留双输入 UI。",
        ["Tools.Hero.Result.NoTarget"] = "请选择英雄，并填写目标回合或目标等级中的任意一项。",
        ["Tools.Hero.Result.TargetRound"] = "已记录 {0}，放置回合 {1}，目标回合 {2}。后续将计算对应等级。",
        ["Tools.Hero.Result.TargetLevel"] = "已记录 {0}，放置回合 {1}，目标等级 {2}。后续将计算所需回合。",
        ["Tools.Hero.Result.BothTargets"] = "已同时填写目标回合与目标等级。接入公式后会校验优先级或冲突。",

        ["Tools.Paragon.Title"] = "模范等级计算器",
        ["Tools.Paragon.Description"] = "根据模范猴子、击破总数、升级总数和额外金币，后续计算模范度。",
        ["Tools.Paragon.Monkey"] = "模范猴子",
        ["Tools.Paragon.MonkeyDescription"] = "选择要估算模范度的猴子。",
        ["Tools.Paragon.TotalPops"] = "击破总数",
        ["Tools.Paragon.TotalPopsDescription"] = "用于模范度计算的累计击破数。",
        ["Tools.Paragon.UpgradeCount"] = "升级总数",
        ["Tools.Paragon.UpgradeCountDescription"] = "用于模范度计算的总升级次数。",
        ["Tools.Paragon.ExtraCash"] = "额外金币",
        ["Tools.Paragon.ExtraCashDescription"] = "用于模范度计算的额外投入金币。",
        ["Tools.Paragon.ResultPlaceholder"] = "已记录 {0} 参数：击破 {1}，升级 {2}，额外金币 {3}。具体 degree 公式待接入。"
    };

    private static Dictionary<string, string> BuildEnUsToolsResources() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["Tools.PageTitle"] = "Tools",
        ["Tools.PageDescription"] = "Card-based UI for round, hero level, and paragon calculators is in place. Exact formulas will be wired later.",
        ["Tools.Section.Parameters"] = "Parameters",
        ["Tools.Section.ParametersDescription"] = "Expand to inspect and edit the current calculator inputs.",
        ["Tools.Section.Result"] = "Result",
        ["Tools.Section.ResultDescription"] = "Result space is reserved. It currently shows placeholder output.",
        ["Tools.Action.Calculate"] = "Calculate",

        ["Tools.Round.Title"] = "Round Calculator",
        ["Tools.Round.Description"] = "Enter a start and end round. This will later drive interval reward, timing, or event calculations.",
        ["Tools.Round.StartRound"] = "Start Round",
        ["Tools.Round.StartRoundDescription"] = "Round number where the calculation begins.",
        ["Tools.Round.EndRound"] = "End Round",
        ["Tools.Round.EndRoundDescription"] = "Round number where the calculation ends.",
        ["Tools.Round.ResultPlaceholder"] = "Recorded round span: {0} to {1}. Formula pending.",

        ["Tools.Hero.Title"] = "Hero Level Calculator",
        ["Tools.Hero.Description"] = "Use hero and placement round to infer either target round or target level from the other field.",
        ["Tools.Hero.Hero"] = "Hero",
        ["Tools.Hero.HeroDescription"] = "Select the hero whose XP curve you want to inspect.",
        ["Tools.Hero.PlacementRound"] = "Placement Round",
        ["Tools.Hero.PlacementRoundDescription"] = "Round where the hero is placed.",
        ["Tools.Hero.TargetRound"] = "Target Round",
        ["Tools.Hero.TargetRoundDescription"] = "Fill this to later infer the reached level at that round.",
        ["Tools.Hero.TargetLevel"] = "Target Level",
        ["Tools.Hero.TargetLevelDescription"] = "Fill this to later infer the round required to reach that level.",
        ["Tools.Hero.Hint"] = "Fill either target round or target level. Both inputs stay visible for now.",
        ["Tools.Hero.Result.NoTarget"] = "Select a hero and fill either target round or target level.",
        ["Tools.Hero.Result.TargetRound"] = "Recorded {0}, placement round {1}, target round {2}. The matching level will be computed later.",
        ["Tools.Hero.Result.TargetLevel"] = "Recorded {0}, placement round {1}, target level {2}. The required round will be computed later.",
        ["Tools.Hero.Result.BothTargets"] = "Both target round and target level are filled. Formula integration will resolve priority or conflicts.",

        ["Tools.Paragon.Title"] = "Paragon Degree Calculator",
        ["Tools.Paragon.Description"] = "Use paragon monkey, total pops, upgrade count, and extra cash to calculate degree later.",
        ["Tools.Paragon.Monkey"] = "Paragon Monkey",
        ["Tools.Paragon.MonkeyDescription"] = "Select the monkey to estimate paragon degree for.",
        ["Tools.Paragon.TotalPops"] = "Total Pops",
        ["Tools.Paragon.TotalPopsDescription"] = "Accumulated pop count used by the degree calculation.",
        ["Tools.Paragon.UpgradeCount"] = "Upgrade Count",
        ["Tools.Paragon.UpgradeCountDescription"] = "Total number of upgrades used by the degree calculation.",
        ["Tools.Paragon.ExtraCash"] = "Extra Cash",
        ["Tools.Paragon.ExtraCashDescription"] = "Additional injected cash used by the degree calculation.",
        ["Tools.Paragon.ResultPlaceholder"] = "Recorded {0} inputs: pops {1}, upgrades {2}, extra cash {3}. Degree formula pending."
    };
}
