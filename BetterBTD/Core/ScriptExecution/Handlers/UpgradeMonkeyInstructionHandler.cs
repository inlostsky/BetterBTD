using BetterBTD.Models.ScriptEditor;
using BetterBTD.Models.ScriptExecution;
using WpfPoint = System.Windows.Point;

namespace BetterBTD.Core.ScriptExecution.Handlers;

public sealed class UpgradeMonkeyInstructionHandler : ScriptInstructionHandlerBase
{
    public override ScriptCommandType CommandType => ScriptCommandType.UpgradeMonkey;

    public override async Task HandleAsync(ScriptInstructionExecutionContext context, CancellationToken cancellationToken)
    {
        var instruction = context.Step.Instruction;
        if (string.IsNullOrWhiteSpace(instruction.TargetMonkeyBindingId))
        {
            throw ScriptInstructionHandlerSupport.CreateExecutionException(
                context,
                "UpgradeMonkeyTarget",
                "Upgrade instruction is missing the target monkey binding ID.");
        }

        if (!context.State.TryGetMonkeyState(instruction.TargetMonkeyBindingId, out var monkeyState))
        {
            throw ScriptInstructionHandlerSupport.CreateExecutionException(
                context,
                "UpgradeMonkeyTarget",
                $"Target monkey binding '{instruction.TargetMonkeyBindingId}' does not exist in runtime state.");
        }

        if (ScriptInstructionHandlerSupport.IsHeroObjectKey(monkeyState.ObjectId) ||
            ScriptInstructionHandlerSupport.IsHeroObjectKey(instruction.TargetMonkeyObjectId))
        {
            throw ScriptInstructionHandlerSupport.CreateExecutionException(
                context,
                "UpgradeMonkeyHeroUnsupported",
                "Hero upgrades are not yet supported because the runtime cannot verify hero upgrade success.");
        }

        if (monkeyState.LastKnownCoordinate is null)
        {
            throw ScriptInstructionHandlerSupport.CreateExecutionException(
                context,
                "UpgradeMonkeyCoordinate",
                $"Target monkey '{monkeyState.ObjectId}' does not have a known runtime coordinate.");
        }

        if (!Enum.TryParse<UpgradePathType>(instruction.UpgradePath, true, out var upgradePath))
        {
            throw ScriptInstructionHandlerSupport.CreateExecutionException(
                context,
                "UpgradeMonkeyPath",
                $"Unsupported upgrade path '{instruction.UpgradePath}'.");
        }

        var targetCoordinate = monkeyState.LastKnownCoordinate.Value;
        var upgradeHotkey = ScriptExecutionKeyBindingResolver.ResolveUpgradeHotkey(upgradePath);
        var upgradeCount = Math.Max(1, instruction.UpgradeCount);

        for (var upgradeIndex = 1; upgradeIndex <= upgradeCount; upgradeIndex++)
        {
            var panelSnapshot = await ScriptInstructionHandlerSupport
                .EnsureUpgradePanelVisibleAsync(context, targetCoordinate, cancellationToken)
                .ConfigureAwait(false);
            var panelSide = ScriptInstructionHandlerSupport.ResolveVisibleUpgradePanelSide(panelSnapshot);
            if (!panelSide.HasValue)
            {
                throw ScriptInstructionHandlerSupport.CreateExecutionException(
                    context,
                    "UpgradeMonkeyPanel",
                    "Failed to detect the upgrade panel for the selected monkey.");
            }

            var currentLevel = ScriptInstructionHandlerSupport.GetUpgradeLevel(
                panelSnapshot,
                panelSide.Value,
                upgradePath);
            if (!currentLevel.HasValue)
            {
                throw ScriptInstructionHandlerSupport.CreateExecutionException(
                    context,
                    "UpgradeMonkeyPanel",
                    $"Failed to read the current '{instruction.UpgradePath}' path level.");
            }

            if (currentLevel.Value >= 5)
            {
                throw ScriptInstructionHandlerSupport.CreateExecutionException(
                    context,
                    "UpgradeMonkeyLevelCap",
                    $"The '{instruction.UpgradePath}' path is already at level 5.");
            }

            var expectedLevel = currentLevel.Value + 1;

            await ScriptExecutionOperations.RetryAsync(
                context,
                new ScriptRetryOptions
                {
                    MaxAttempts = 3,
                    DelayBetweenAttemptsMilliseconds = 150,
                    Description = $"Upgrade '{monkeyState.ObjectId}' {instruction.UpgradePath} to level {expectedLevel}"
                },
                async (attempt, token) =>
                {
                    var beforePressSnapshot = await ScriptInstructionHandlerSupport
                        .EnsureUpgradePanelVisibleAsync(context, targetCoordinate, token)
                        .ConfigureAwait(false);
                    var visiblePanelSide = ScriptInstructionHandlerSupport.ResolveVisibleUpgradePanelSide(beforePressSnapshot);
                    if (!visiblePanelSide.HasValue)
                    {
                        throw ScriptInstructionHandlerSupport.CreateExecutionException(
                            context,
                            "UpgradeMonkeyPanel",
                            "Failed to restore the upgrade panel before sending the upgrade hotkey.",
                            attempt);
                    }

                    var beforePressLevel = ScriptInstructionHandlerSupport.GetUpgradeLevel(
                        beforePressSnapshot,
                        visiblePanelSide.Value,
                        upgradePath);
                    if (!beforePressLevel.HasValue)
                    {
                        throw ScriptInstructionHandlerSupport.CreateExecutionException(
                            context,
                            "UpgradeMonkeyPanel",
                            $"Failed to read the '{instruction.UpgradePath}' path level before upgrading.",
                            attempt);
                    }

                    if (beforePressLevel.Value >= expectedLevel)
                    {
                        return true;
                    }

                    await ScriptExecutionOperations.CheckpointAsync(
                        context,
                        "UpgradeMonkeyPress",
                        $"Upgrade {upgradeIndex}/{upgradeCount}, attempt {attempt}: sending '{instruction.UpgradePath}' upgrade hotkey '{upgradeHotkey.DisplayName}'.",
                        token).ConfigureAwait(false);

                    context.RuntimeServices.Input.PressHotkey(upgradeHotkey);

                    await ScriptExecutionOperations.WaitUntilAsync(
                        context,
                        new ScriptWaitOptions
                        {
                            TimeoutMilliseconds = 900,
                            PollIntervalMilliseconds = 100,
                            Description = $"upgrade path reach level {expectedLevel}"
                        },
                        async innerToken =>
                        {
                            var afterPressSnapshot = await context.RuntimeServices.GameStageState
                                .CaptureSnapshotAsync(innerToken)
                                .ConfigureAwait(false);
                            if (afterPressSnapshot is null)
                            {
                                return false;
                            }

                            var afterPressPanelSide = ScriptInstructionHandlerSupport.ResolveVisibleUpgradePanelSide(afterPressSnapshot);
                            if (!afterPressPanelSide.HasValue)
                            {
                                return false;
                            }

                            var afterPressLevel = ScriptInstructionHandlerSupport.GetUpgradeLevel(
                                afterPressSnapshot,
                                afterPressPanelSide.Value,
                                upgradePath);
                            return afterPressLevel.HasValue && afterPressLevel.Value >= expectedLevel;
                        },
                        token).ConfigureAwait(false);

                    await ScriptExecutionOperations.CheckpointAsync(
                        context,
                        "UpgradeMonkeySucceeded",
                        $"Upgrade {upgradeIndex}/{upgradeCount}: '{instruction.UpgradePath}' reached level {expectedLevel}.",
                        token).ConfigureAwait(false);

                    return true;
                },
                static success => success,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
