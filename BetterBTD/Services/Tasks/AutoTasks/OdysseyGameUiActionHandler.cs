using BetterBTD.Core.AutoTasks.Runtime;
using BetterBTD.Models.AutoTasks;
using BetterBTD.Services.Start.Capture;
using BetterBTD.Services.Tasks.CaptureAnalysis;
using BetterBTD.Services.Tasks.Input;
using WpfPoint = System.Windows.Point;

namespace BetterBTD.Services.Tasks.AutoTasks;

internal sealed class OdysseyGameUiActionHandler : AutoTaskGameUiActionHandlerBase
{
    public OdysseyGameUiActionHandler(
        ScriptInputSimulationService inputSimulationService,
        GameCaptureService gameCaptureService,
        GameUiNavigationOcrService navigationOcrService)
        : base(inputSimulationService, gameCaptureService, navigationOcrService)
    {
    }

    public override AutoTaskKind Kind => AutoTaskKind.Odyssey;

    public override Task<GameUiActionExecutionResult> ExecuteAsync(
        GameUiNavigationStep step,
        AutoTaskRuntimeState state,
        GameUiSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = snapshot.State switch
        {
            GameUiStateId.Unknown => Success(step, "Odyssey UI state is unknown. No action taken.", step.PostActionDelayMs),
            GameUiStateId.InLevel => Success(step, "Odyssey stage is active. Waiting for the configured script lifecycle.", step.PostActionDelayMs),
            GameUiStateId.OdysseyStart => Click(step, new WpfPoint(1760, 960), "Started the Odyssey run."),
            GameUiStateId.OdysseyCrew => Click(step, new WpfPoint(1760, 960), "Confirmed the Odyssey crew screen."),
            GameUiStateId.OdysseyLoading => Success(step, "Waiting for the Odyssey loading screen.", step.PostActionDelayMs),
            GameUiStateId.OdysseyStageVictory => Click(step, new WpfPoint(830, 790), "Confirmed the Odyssey stage victory screen."),
            GameUiStateId.OdysseySettlement => Click(step, new WpfPoint(960, 840), "Advanced past the Odyssey settlement screen."),
            GameUiStateId.OdysseyReward => Click(step, new WpfPoint(960, 850), "Collected the Odyssey reward."),
            _ => new GameUiActionExecutionResult
            {
                Succeeded = false,
                Message = $"Odyssey action executor does not handle UI state '{snapshot.State}' yet.",
                RecommendedDelayMs = step.PostActionDelayMs
            }
        };

        return Task.FromResult(result);
    }
}
