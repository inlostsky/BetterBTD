using BetterBTD.Models.AutoTasks;
using BetterBTD.Models.GameElements;
using BetterBTD.Models.MyScripts;
using BetterBTD.Models.ScriptEditor;
using System.Text.Json;

namespace BetterBTD.Tests.Services;

public sealed class ManagedScriptLibraryServiceTests
{
    [Fact]
    public void ImportBindExportFlow_StoresManagedScriptAndResolvesSlotBinding()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), $"betterbtd-library-{Guid.NewGuid():N}");
        var sourceFilePath = Path.Combine(rootDirectory, "source", "sample-script.btd");
        var exportFilePath = Path.Combine(rootDirectory, "export", "sample-script-copy.btd");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(sourceFilePath)!);
            ScriptDocumentService.Instance.Save(sourceFilePath, CreateDocument(
                GameMapType.MonkeyMeadow,
                StageDifficulty.Easy,
                StageMode.Standard,
                ["black-border", "custom-tag"]));

            var service = new ManagedScriptLibraryService(
                Path.Combine(rootDirectory, "managed"),
                ScriptDocumentService.Instance,
                ManagedScriptSlotCatalogService.Instance);

            var imported = service.ImportScript(sourceFilePath);
            var blackBorderSlotId = ManagedScriptSlotIdFactory.CreateBlackBorderSlotId(
                GameMapType.MonkeyMeadow,
                StageDifficulty.Easy,
                StageMode.Standard);

            service.SetBinding(blackBorderSlotId, imported.ScriptId);
            service.ExportScript(imported.ScriptId, exportFilePath);

            var snapshot = service.GetSnapshot();
            var script = Assert.Single(snapshot.Scripts);
            var slot = snapshot.Slots.First(x => x.Definition.SlotId == blackBorderSlotId);

            Assert.Equal("sample-script", script.DisplayName);
            Assert.Equal(GameMapType.MonkeyMeadow, script.Map);
            Assert.Equal(StageDifficulty.Easy, script.Difficulty);
            Assert.Equal(StageMode.Standard, script.Mode);
            Assert.Equal(1, script.BindingCount);
            Assert.False(script.HasMissingFile);
            Assert.Equal(imported.ScriptId, slot.BoundScriptId);
            Assert.NotNull(slot.BoundScript);
            Assert.True(File.Exists(exportFilePath));
        }
        finally
        {
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(rootDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void RemoveScript_ClearsExistingBindings()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), $"betterbtd-library-{Guid.NewGuid():N}");
        var sourceFilePath = Path.Combine(rootDirectory, "source", "custom-script.btd");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(sourceFilePath)!);
            ScriptDocumentService.Instance.Save(sourceFilePath, CreateDocument(
                GameMapType.DarkCastle,
                StageDifficulty.Hard,
                StageMode.CHIMPS,
                ["black-border"]));

            var service = new ManagedScriptLibraryService(
                Path.Combine(rootDirectory, "managed"),
                ScriptDocumentService.Instance,
                ManagedScriptSlotCatalogService.Instance);

            var imported = service.ImportScript(sourceFilePath);
            var slotId = ManagedScriptSlotIdFactory.CreateCustomDefaultSlotId();
            service.SetBinding(slotId, imported.ScriptId);

            var removed = service.RemoveScript(imported.ScriptId);
            var snapshot = service.GetSnapshot();
            var slot = snapshot.Slots.First(x => x.Definition.SlotId == slotId);

            Assert.True(removed);
            Assert.Empty(snapshot.Scripts);
            Assert.False(slot.HasBinding);
        }
        finally
        {
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(rootDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void ImportLegacyScriptCollection_ConvertsAndImportsEachLegacyScript()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), $"betterbtd-library-{Guid.NewGuid():N}");
        var packageFilePath = Path.Combine(rootDirectory, "source", "legacy-package.btd6s");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(packageFilePath)!);
            File.WriteAllText(packageFilePath, JsonSerializer.Serialize(new[]
            {
                CreateLegacyDocument("legacy-standard", GameMapType.MonkeyMeadow, StageDifficulty.Easy, StageMode.Standard),
                CreateLegacyDocument("legacy-chimps", GameMapType.DarkCastle, StageDifficulty.Hard, StageMode.CHIMPS)
            }));

            var service = new ManagedScriptLibraryService(
                Path.Combine(rootDirectory, "managed"),
                ScriptDocumentService.Instance,
                ManagedScriptSlotCatalogService.Instance);

            var imported = service.ImportLegacyScriptCollection(packageFilePath);
            var snapshot = service.GetSnapshot();

            Assert.Equal(2, imported.Count);
            Assert.Equal(2, snapshot.Scripts.Count);
            Assert.All(snapshot.Scripts, script => Assert.Equal("legacy-package.btd6s", script.SourceFileName));
            Assert.Contains(snapshot.Scripts, script => script.DisplayName == "legacy-standard" &&
                                                        script.Map == GameMapType.MonkeyMeadow &&
                                                        script.Difficulty == StageDifficulty.Easy &&
                                                        script.Mode == StageMode.Standard);
            Assert.Contains(snapshot.Scripts, script => script.DisplayName == "legacy-chimps" &&
                                                        script.Map == GameMapType.DarkCastle &&
                                                        script.Difficulty == StageDifficulty.Hard &&
                                                        script.Mode == StageMode.CHIMPS);
        }
        finally
        {
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(rootDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void ImportScript_SkipsDuplicateWhenFingerprintMatches()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), $"betterbtd-library-{Guid.NewGuid():N}");
        var firstFilePath = Path.Combine(rootDirectory, "source", "first-script.btd");
        var secondFilePath = Path.Combine(rootDirectory, "source", "second-script.btd");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(firstFilePath)!);
            var document = CreateDocument(
                GameMapType.MonkeyMeadow,
                StageDifficulty.Easy,
                StageMode.Standard,
                ["black-border"]);
            ScriptDocumentService.Instance.Save(firstFilePath, document);
            ScriptDocumentService.Instance.Save(secondFilePath, document);

            var service = new ManagedScriptLibraryService(
                Path.Combine(rootDirectory, "managed"),
                ScriptDocumentService.Instance,
                ManagedScriptSlotCatalogService.Instance);

            var firstImported = service.ImportScript(firstFilePath);
            var secondImported = service.ImportScript(secondFilePath);
            var snapshot = service.GetSnapshot();

            Assert.Single(snapshot.Scripts);
            Assert.Equal(firstImported.ScriptId, secondImported.ScriptId);
        }
        finally
        {
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(rootDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void ImportLegacyScriptCollection_SkipsDuplicateWhenFingerprintMatches()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), $"betterbtd-library-{Guid.NewGuid():N}");
        var packageFilePath = Path.Combine(rootDirectory, "source", "legacy-package.btd6s");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(packageFilePath)!);
            var duplicateScript = CreateLegacyDocument("legacy-standard", GameMapType.MonkeyMeadow, StageDifficulty.Easy, StageMode.Standard);
            File.WriteAllText(packageFilePath, JsonSerializer.Serialize(new[]
            {
                duplicateScript,
                duplicateScript
            }));

            var service = new ManagedScriptLibraryService(
                Path.Combine(rootDirectory, "managed"),
                ScriptDocumentService.Instance,
                ManagedScriptSlotCatalogService.Instance);

            var imported = service.ImportLegacyScriptCollection(packageFilePath);
            var snapshot = service.GetSnapshot();

            Assert.Single(imported);
            Assert.Single(snapshot.Scripts);
        }
        finally
        {
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(rootDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void ImportScript_BackfillsMissingFingerprintAndStillSkipsDuplicate()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), $"betterbtd-library-{Guid.NewGuid():N}");
        var firstFilePath = Path.Combine(rootDirectory, "source", "first-script.btd");
        var secondFilePath = Path.Combine(rootDirectory, "source", "second-script.btd");
        var managedRootDirectory = Path.Combine(rootDirectory, "managed");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(firstFilePath)!);
            var document = CreateDocument(
                GameMapType.MonkeyMeadow,
                StageDifficulty.Easy,
                StageMode.Standard,
                ["black-border"]);
            ScriptDocumentService.Instance.Save(firstFilePath, document);
            ScriptDocumentService.Instance.Save(secondFilePath, document);

            var service = new ManagedScriptLibraryService(
                managedRootDirectory,
                ScriptDocumentService.Instance,
                ManagedScriptSlotCatalogService.Instance);

            _ = service.ImportScript(firstFilePath);

            var manifestFilePath = Path.Combine(managedRootDirectory, "library.json");
            var manifest = JsonSerializer.Deserialize<ManagedScriptLibraryDocument>(File.ReadAllText(manifestFilePath));
            Assert.NotNull(manifest);
            Assert.Single(manifest.Scripts);
            manifest.Scripts[0].Fingerprint = string.Empty;
            File.WriteAllText(manifestFilePath, JsonSerializer.Serialize(manifest));

            var duplicateImported = service.ImportScript(secondFilePath);
            var snapshot = service.GetSnapshot();
            var updatedManifest = JsonSerializer.Deserialize<ManagedScriptLibraryDocument>(File.ReadAllText(manifestFilePath));

            Assert.Single(snapshot.Scripts);
            Assert.NotNull(updatedManifest);
            Assert.Single(updatedManifest.Scripts);
            Assert.False(string.IsNullOrWhiteSpace(updatedManifest.Scripts[0].Fingerprint));
            Assert.Equal(snapshot.Scripts[0].ScriptId, duplicateImported.ScriptId);
        }
        finally
        {
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(rootDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void SlotCatalog_ContainsExpectedFrameworkSlots()
    {
        var catalog = ManagedScriptSlotCatalogService.Instance;
        var slots = catalog.GetAll();

        var blackBorderCount = GameElementCatalog.Maps.Count * 14;
        var collectionCount = 3 * 13;
        var expectedCount = blackBorderCount + collectionCount + 2;

        Assert.Equal(expectedCount, slots.Count);
        Assert.Contains(slots, x => x.SlotId == ManagedScriptSlotIdFactory.CreateCustomDefaultSlotId());
        Assert.Contains(slots, x => x.SlotId == ManagedScriptSlotIdFactory.CreateRaceCurrentSlotId());
        Assert.Contains(slots, x => x.SlotId == ManagedScriptSlotIdFactory.CreateBlackBorderSlotId(
            GameMapType.MonkeyMeadow,
            StageDifficulty.Easy,
            StageMode.Standard));
    }

    private static ScriptDocument CreateDocument(
        GameMapType map,
        StageDifficulty difficulty,
        StageMode mode,
        IReadOnlyList<string> tags)
    {
        return new ScriptDocument
        {
            Metadata = new ScriptMetadataDocument
            {
                Map = map.ToString(),
                Difficulty = difficulty.ToString(),
                Mode = mode.ToString(),
                Hero = HeroType.Quincy.ToString(),
                Tags = [.. tags]
            }
        };
    }

    private static LegacyScriptModel CreateLegacyDocument(
        string scriptName,
        GameMapType map,
        StageDifficulty difficulty,
        StageMode mode)
    {
        return new LegacyScriptModel
        {
            Metadata = new LegacyScriptMetadata
            {
                Version = "1.1",
                ScriptName = scriptName,
                SelectedMap = (int)Enum.Parse<LegacyMapType>(map.ToString()),
                SelectedDifficulty = (int)Enum.Parse<LegacyLevelDifficulty>(difficulty.ToString()),
                SelectedMode = mode == StageMode.MagicOnly
                    ? (int)LegacyLevelMode.MagicMonkeysOnly
                    : (int)Enum.Parse<LegacyLevelMode>(mode.ToString()),
                SelectedHero = (int)LegacyHeroType.Quincy
            },
            InstructionsList = [],
            MonkeyCounts = [],
            MonkeyIds = []
        };
    }
}
