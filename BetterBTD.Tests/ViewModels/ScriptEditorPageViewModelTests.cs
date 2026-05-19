using BetterBTD.Models.ScriptEditor;
using BetterBTD.Services;
using BetterBTD.ViewModels;

namespace BetterBTD.Tests.ViewModels;

public sealed class ScriptEditorPageViewModelTests
{
    [Fact]
    public void SaveScriptDocument_RefreshesInstructionSequenceWithOptimizedInstructions()
    {
        var viewModel = new ScriptEditorPageViewModel(LocalizationService.Instance);
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.btd");

        try
        {
            viewModel.ImportScriptDocument(new ScriptDocument
            {
                Metadata = new ScriptMetadataDocument
                {
                    Tags = ["collection", "自定义标签"]
                },
                Instructions =
                [
                    new ScriptInstructionDocument
                    {
                        CommandType = ScriptCommandType.UpgradeMonkey.ToString(),
                        TargetMonkeyBindingId = "dart-bind",
                        TargetMonkeyObjectId = "DartMonkey:1",
                        UpgradePath = UpgradePathType.Top.ToString(),
                        UpgradeCount = 1,
                        IntervalToNextInstructionMs = 0
                    },
                    new ScriptInstructionDocument
                    {
                        CommandType = ScriptCommandType.UpgradeMonkey.ToString(),
                        TargetMonkeyBindingId = "dart-bind",
                        TargetMonkeyObjectId = "DartMonkey:1",
                        UpgradePath = UpgradePathType.Top.ToString(),
                        UpgradeCount = 2,
                        IntervalToNextInstructionMs = 0
                    },
                    new ScriptInstructionDocument
                    {
                        CommandType = ScriptCommandType.NextRound.ToString(),
                        NextRoundAction = "SendNextRound",
                        NextRoundSendCount = 1,
                        IntervalToNextInstructionMs = 0
                    },
                    new ScriptInstructionDocument
                    {
                        CommandType = ScriptCommandType.NextRound.ToString(),
                        NextRoundAction = "SendNextRound",
                        NextRoundSendCount = 2,
                        IntervalToNextInstructionMs = 0
                    }
                ]
            });

            Assert.Equal(4, viewModel.InstructionSequence.Count);

            viewModel.SaveScriptDocument(filePath);
            var saved = ScriptDocumentService.Instance.Load(filePath);

            Assert.Equal(filePath, viewModel.CurrentScriptFilePath);
            Assert.Equal(2, viewModel.InstructionSequence.Count);
            Assert.Equal(3, viewModel.InstructionSequence[0].UpgradeCount);
            Assert.Equal(3, viewModel.InstructionSequence[1].NextRoundSendCount);
            Assert.Equal(["collection", "自定义标签"], saved.Metadata.Tags);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public void AddScriptTagCommand_ResolvesBuiltInAliasAndKeepsCustomTags()
    {
        var viewModel = new ScriptEditorPageViewModel(LocalizationService.Instance);

        viewModel.PendingTagInput = "黑框";
        viewModel.AddScriptTagCommand.Execute(null);

        viewModel.PendingTagInput = "custom-run";
        viewModel.AddScriptTagCommand.Execute(null);

        viewModel.PendingTagInput = "Black Border";
        viewModel.AddScriptTagCommand.Execute(null);

        var exported = viewModel.ExportScriptDocument();

        Assert.Equal(["black-border", "custom-run"], exported.Metadata.Tags);
        Assert.Collection(
            viewModel.SelectedTagOptions,
            first =>
            {
                Assert.Equal("black-border", first.Code);
                Assert.Equal("黑框 / Black Border", first.DisplayName);
            },
            second =>
            {
                Assert.Equal("custom-run", second.Code);
                Assert.Equal("custom-run", second.DisplayName);
            });
    }
}
