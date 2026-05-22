using System.IO;
using System.Text.Json;
using BetterBTD.Helpers.Security;
using BetterBTD.Models.AutoTasks;
using BetterBTD.Models.GameElements;
using BetterBTD.Models.MyScripts;
using BetterBTD.Models.ScriptEditor;

namespace BetterBTD.Services.MyScripts;

public sealed class ManagedScriptLibraryService
{
    private static readonly Lazy<ManagedScriptLibraryService> InstanceHolder =
        new(() => new ManagedScriptLibraryService());

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _rootDirectory;
    private readonly string _assetsDirectory;
    private readonly string _bindingsDirectory;
    private readonly string _manifestFilePath;
    private readonly string _blackBorderBindingsFilePath;
    private readonly string _collectionBindingsFilePath;
    private readonly ScriptDocumentService _scriptDocumentService;
    private readonly ManagedScriptSlotCatalogService _slotCatalogService;
    private readonly object _syncRoot = new();

    private ManagedScriptLibraryService()
        : this(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BetterBTD",
                "MyScripts"),
            ScriptDocumentService.Instance,
            ManagedScriptSlotCatalogService.Instance)
    {
    }

    internal ManagedScriptLibraryService(
        string rootDirectory,
        ScriptDocumentService scriptDocumentService,
        ManagedScriptSlotCatalogService slotCatalogService)
    {
        _rootDirectory = rootDirectory ?? throw new ArgumentNullException(nameof(rootDirectory));
        _assetsDirectory = Path.Combine(_rootDirectory, "Assets");
        _bindingsDirectory = Path.Combine(_rootDirectory, "Bindings");
        _manifestFilePath = Path.Combine(_rootDirectory, "library.json");
        _blackBorderBindingsFilePath = Path.Combine(_bindingsDirectory, "blackborder.json");
        _collectionBindingsFilePath = Path.Combine(_bindingsDirectory, "collection.json");
        _scriptDocumentService = scriptDocumentService ?? throw new ArgumentNullException(nameof(scriptDocumentService));
        _slotCatalogService = slotCatalogService ?? throw new ArgumentNullException(nameof(slotCatalogService));
    }

    public static ManagedScriptLibraryService Instance => InstanceHolder.Value;

    public ManagedScriptLibrarySnapshot GetSnapshot()
    {
        lock (_syncRoot)
        {
            var document = LoadManifest();
            MigrateDedicatedBindings(document);
            RefreshCachedMetadata(document);
            SaveManifest(document);
            return BuildSnapshot(document);
        }
    }

    public ManagedScriptAssetEntry ImportScript(string sourceFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFilePath);

        lock (_syncRoot)
        {
            EnsureStorage();

            if (!File.Exists(sourceFilePath))
            {
                throw new FileNotFoundException("Script file was not found.", sourceFilePath);
            }

            var loadResult = _scriptDocumentService.LoadCompatible(sourceFilePath);
            var document = LoadManifest();
            MigrateDedicatedBindings(document);
            var currentBindings = LoadCurrentBindings(document);
            var targetFingerprint = BuildDocumentFingerprint(loadResult.Document);
            var recordsByFingerprint = BuildFingerprintIndex(document);
            if (recordsByFingerprint.TryGetValue(targetFingerprint, out var existingRecord))
            {
                SaveManifest(document);
                return BuildAssetEntry(existingRecord, currentBindings);
            }

            var record = CreateManagedScriptRecord(
                loadResult.Document,
                Path.GetFileNameWithoutExtension(sourceFilePath),
                Path.GetFileName(sourceFilePath),
                targetFingerprint);
            document.Scripts.Add(record);
            SaveManifest(document);

            return BuildAssetEntry(record, currentBindings);
        }
    }

    public IReadOnlyList<ManagedScriptAssetEntry> ImportLegacyScriptCollection(
        string sourceFilePath,
        IProgress<int>? progress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFilePath);

        lock (_syncRoot)
        {
            EnsureStorage();

            if (!File.Exists(sourceFilePath))
            {
                throw new FileNotFoundException("Legacy script package was not found.", sourceFilePath);
            }

            var document = LoadManifest();
            MigrateDedicatedBindings(document);
            var currentBindings = LoadCurrentBindings(document);
            var recordsByFingerprint = BuildFingerprintIndex(document);
            var importedEntries = new List<ManagedScriptAssetEntry>();
            using var stream = File.OpenRead(sourceFilePath);
            var enumerator = JsonSerializer.DeserializeAsyncEnumerable<JsonElement>(stream).GetAsyncEnumerator();
            var sourceFileName = Path.GetFileName(sourceFilePath);
            var packageDisplayName = Path.GetFileNameWithoutExtension(sourceFilePath);
            var index = 0;
            var processedCount = 0;
            progress?.Report(processedCount);

            try
            {
                while (enumerator.MoveNextAsync().AsTask().GetAwaiter().GetResult())
                {
                    var element = enumerator.Current;
                    if (element.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }

                    if (element.ValueKind != JsonValueKind.Object)
                    {
                        throw new InvalidDataException($"Legacy script package item at index {index} must be a JSON object.");
                    }

                    var legacyDocument = LegacyScriptDocumentService.Instance.LoadFromJson(element.GetRawText());
                    var conversionResult = LegacyScriptConversionService.Instance.Convert(legacyDocument);
                    var displayName = ResolveLegacyPackageScriptDisplayName(
                        legacyDocument,
                        packageDisplayName,
                        index);
                    var targetFingerprint = BuildDocumentFingerprint(conversionResult.Document);
                    processedCount++;
                    progress?.Report(processedCount);
                    if (recordsByFingerprint.ContainsKey(targetFingerprint))
                    {
                        index++;
                        continue;
                    }

                    var record = CreateManagedScriptRecord(
                        conversionResult.Document,
                        displayName,
                        sourceFileName,
                        targetFingerprint);
                    document.Scripts.Add(record);
                    recordsByFingerprint[targetFingerprint] = record;
                    importedEntries.Add(BuildAssetEntry(record, currentBindings));
                    index++;
                }
            }
            finally
            {
                enumerator.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }

            if (processedCount == 0)
            {
                throw new InvalidDataException("Legacy script package does not contain any importable scripts.");
            }

            SaveManifest(document);
            return importedEntries;
        }
    }

    public ManagedScriptAssetEntry UpsertScript(
        string sourceFilePath,
        string? scriptId = null,
        string? displayName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFilePath);

        lock (_syncRoot)
        {
            EnsureStorage();

            if (!File.Exists(sourceFilePath))
            {
                throw new FileNotFoundException("Script file was not found.", sourceFilePath);
            }

            var loadResult = _scriptDocumentService.LoadCompatible(sourceFilePath);
            var scriptDocument = loadResult.Document;
            var scriptFingerprint = BuildDocumentFingerprint(scriptDocument);
            var document = LoadManifest();
            MigrateDedicatedBindings(document);
            var currentBindings = LoadCurrentBindings(document);
            var record = FindRecordById(document, scriptId) ?? FindRecordByStoredFilePath(document, sourceFilePath);
            var now = DateTimeOffset.UtcNow;

            if (record is null)
            {
                var newScriptId = Guid.NewGuid().ToString("N");
                var storedFileName = $"{newScriptId}.btd";
                var storedFilePath = Path.Combine(_assetsDirectory, storedFileName);
                _scriptDocumentService.Save(storedFilePath, scriptDocument);

                record = new ManagedScriptAssetRecord
                {
                    ScriptId = newScriptId,
                    DisplayName = ResolveDisplayName(displayName, sourceFilePath),
                    SourceFileName = Path.GetFileName(sourceFilePath),
                    StoredFileName = storedFileName,
                    Fingerprint = scriptFingerprint,
                    Description = scriptDocument.Metadata.Description,
                    Map = scriptDocument.Metadata.Map,
                    Difficulty = scriptDocument.Metadata.Difficulty,
                    Mode = scriptDocument.Metadata.Mode,
                    Hero = scriptDocument.Metadata.Hero,
                    Tags = [.. ScriptTagCatalog.NormalizeStoredTags(scriptDocument.Metadata.Tags)],
                    ImportedAt = now,
                    UpdatedAt = now
                };

                document.Scripts.Add(record);
            }
            else
            {
                var storedFilePath = GetStoredFilePath(record);
                var isManagedSourcePath = AreSameFilePath(sourceFilePath, storedFilePath);

                if (!isManagedSourcePath)
                {
                    _scriptDocumentService.Save(storedFilePath, scriptDocument);
                    record.SourceFileName = Path.GetFileName(sourceFilePath);
                }

                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    record.DisplayName = displayName.Trim();
                }
                else if (string.IsNullOrWhiteSpace(record.DisplayName))
                {
                    record.DisplayName = ResolveDisplayName(displayName, sourceFilePath);
                }

                record.Description = scriptDocument.Metadata.Description;
                record.Map = scriptDocument.Metadata.Map;
                record.Difficulty = scriptDocument.Metadata.Difficulty;
                record.Mode = scriptDocument.Metadata.Mode;
                record.Hero = scriptDocument.Metadata.Hero;
                record.Tags = [.. ScriptTagCatalog.NormalizeStoredTags(scriptDocument.Metadata.Tags)];
                record.Fingerprint = scriptFingerprint;
                record.UpdatedAt = now;
            }

            SaveManifest(document);
            return BuildAssetEntry(record, currentBindings);
        }
    }

    public void ExportScript(string scriptId, string targetFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetFilePath);

        lock (_syncRoot)
        {
            var document = LoadManifest();
            MigrateDedicatedBindings(document);
            var record = document.Scripts.FirstOrDefault(x => string.Equals(x.ScriptId, scriptId, StringComparison.OrdinalIgnoreCase))
                         ?? throw new InvalidOperationException("Managed script asset was not found.");
            var sourceFilePath = GetStoredFilePath(record);
            if (!File.Exists(sourceFilePath))
            {
                throw new FileNotFoundException("Managed script asset file was not found.", sourceFilePath);
            }

            var directory = Path.GetDirectoryName(targetFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.Copy(sourceFilePath, targetFilePath, overwrite: true);
        }
    }

    public bool RemoveScript(string scriptId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptId);

        lock (_syncRoot)
        {
            var document = LoadManifest();
            MigrateDedicatedBindings(document);
            var record = document.Scripts.FirstOrDefault(x => string.Equals(x.ScriptId, scriptId, StringComparison.OrdinalIgnoreCase));
            if (record is null)
            {
                return false;
            }

            document.Scripts.Remove(record);
            document.Bindings.RemoveAll(x => string.Equals(x.ScriptId, scriptId, StringComparison.OrdinalIgnoreCase));
            RemoveScriptBindingsFromDedicatedFiles(scriptId);

            var storedFilePath = GetStoredFilePath(record);
            if (File.Exists(storedFilePath))
            {
                File.Delete(storedFilePath);
            }

            SaveManifest(document);
            return true;
        }
    }

    public void SetBinding(string slotId, string? scriptId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);

        lock (_syncRoot)
        {
            var document = LoadManifest();
            MigrateDedicatedBindings(document);

            if (!_slotCatalogService.TryGetById(slotId, out var slot))
            {
                throw new InvalidOperationException("Managed script slot was not found.");
            }

            var trimmedSlotId = slotId.Trim();

            if (string.IsNullOrWhiteSpace(scriptId))
            {
                if (TryGetDedicatedBindingFilePath(slot.TaskKind, out var dedicatedBindingFilePath))
                {
                    var dedicatedBindings = LoadTaskBindingDocument(dedicatedBindingFilePath);
                    if (dedicatedBindings.Bindings.Remove(trimmedSlotId))
                    {
                        SaveTaskBindingDocument(dedicatedBindingFilePath, dedicatedBindings);
                    }

                    return;
                }

                var binding = document.Bindings.FirstOrDefault(x => string.Equals(x.SlotId, trimmedSlotId, StringComparison.OrdinalIgnoreCase));
                if (binding is not null)
                {
                    document.Bindings.Remove(binding);
                    SaveManifest(document);
                }

                return;
            }

            if (document.Scripts.All(x => !string.Equals(x.ScriptId, scriptId, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("Managed script asset was not found.");
            }

            var trimmedScriptId = scriptId.Trim();

            if (TryGetDedicatedBindingFilePath(slot.TaskKind, out var bindingFilePath))
            {
                var dedicatedBindings = LoadTaskBindingDocument(bindingFilePath);
                dedicatedBindings.Bindings[trimmedSlotId] = trimmedScriptId;
                SaveTaskBindingDocument(bindingFilePath, dedicatedBindings);
            }
            else
            {
                var binding = document.Bindings.FirstOrDefault(x => string.Equals(x.SlotId, trimmedSlotId, StringComparison.OrdinalIgnoreCase));
                if (binding is null)
                {
                    document.Bindings.Add(new ManagedScriptSlotBindingRecord
                    {
                        SlotId = trimmedSlotId,
                        ScriptId = trimmedScriptId,
                        UpdatedAt = DateTimeOffset.UtcNow
                    });
                }
                else
                {
                    binding.ScriptId = trimmedScriptId;
                    binding.UpdatedAt = DateTimeOffset.UtcNow;
                }

                SaveManifest(document);
            }
        }
    }

    public bool TryGetManagedScriptFilePath(string scriptId, out string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptId);

        lock (_syncRoot)
        {
            var document = LoadManifest();
            var record = document.Scripts.FirstOrDefault(x => string.Equals(x.ScriptId, scriptId, StringComparison.OrdinalIgnoreCase));
            if (record is null)
            {
                filePath = string.Empty;
                return false;
            }

            filePath = GetStoredFilePath(record);
            return File.Exists(filePath);
        }
    }

    public bool TryGetManagedScriptByStoredFilePath(string storedFilePath, out ManagedScriptAssetEntry entry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storedFilePath);

        lock (_syncRoot)
        {
            var document = LoadManifest();
            MigrateDedicatedBindings(document);
            var record = FindRecordByStoredFilePath(document, storedFilePath);
            if (record is null)
            {
                entry = null!;
                return false;
            }

            entry = BuildAssetEntry(record, LoadCurrentBindings(document));
            return true;
        }
    }

    public bool TryResolveSlotBinding(string slotId, out string scriptId, out string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);

        lock (_syncRoot)
        {
            var document = LoadManifest();
            MigrateDedicatedBindings(document);
            var bindings = LoadCurrentBindings(document);
            var binding = bindings.FirstOrDefault(x => string.Equals(x.SlotId, slotId, StringComparison.OrdinalIgnoreCase));
            if (binding is null || string.IsNullOrWhiteSpace(binding.ScriptId))
            {
                scriptId = string.Empty;
                filePath = string.Empty;
                return false;
            }

            var record = document.Scripts.FirstOrDefault(x => string.Equals(x.ScriptId, binding.ScriptId, StringComparison.OrdinalIgnoreCase));
            if (record is null)
            {
                scriptId = binding.ScriptId;
                filePath = string.Empty;
                return false;
            }

            scriptId = record.ScriptId;
            filePath = GetStoredFilePath(record);
            return File.Exists(filePath);
        }
    }

    private ManagedScriptLibraryDocument LoadManifest()
    {
        EnsureStorage();

        if (!File.Exists(_manifestFilePath))
        {
            return new ManagedScriptLibraryDocument();
        }

        var json = File.ReadAllText(_manifestFilePath);
        var document = JsonSerializer.Deserialize<ManagedScriptLibraryDocument>(json, JsonOptions)
                       ?? new ManagedScriptLibraryDocument();
        document.Scripts ??= [];
        document.Bindings ??= [];

        foreach (var script in document.Scripts)
        {
            NormalizeRecord(script);
        }

        foreach (var binding in document.Bindings)
        {
            binding.SlotId = binding.SlotId?.Trim() ?? string.Empty;
            binding.ScriptId = binding.ScriptId?.Trim() ?? string.Empty;
        }

        document.Bindings = document.Bindings
            .Where(x => x.SlotId.Length > 0)
            .GroupBy(x => x.SlotId, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.OrderByDescending(item => item.UpdatedAt).First())
            .ToList();

        return document;
    }

    private void SaveManifest(ManagedScriptLibraryDocument document)
    {
        EnsureStorage();
        var json = JsonSerializer.Serialize(document, JsonOptions);
        File.WriteAllText(_manifestFilePath, json);
    }

    private ManagedScriptTaskBindingDocument LoadTaskBindingDocument(string filePath)
    {
        EnsureStorage();

        if (!File.Exists(filePath))
        {
            return new ManagedScriptTaskBindingDocument();
        }

        var json = File.ReadAllText(filePath);
        var document = JsonSerializer.Deserialize<ManagedScriptTaskBindingDocument>(json, JsonOptions)
                       ?? new ManagedScriptTaskBindingDocument();
        document.Bindings ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        document.Bindings = document.Bindings
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .GroupBy(x => x.Key.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Last().Value?.Trim() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);
        return document;
    }

    private void SaveTaskBindingDocument(string filePath, ManagedScriptTaskBindingDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        EnsureStorage();
        document.Bindings ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        document.Bindings = document.Bindings
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .GroupBy(x => x.Key.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Last().Value?.Trim() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);

        var json = JsonSerializer.Serialize(document, JsonOptions);
        File.WriteAllText(filePath, json);
    }

    private void RefreshCachedMetadata(ManagedScriptLibraryDocument document)
    {
        foreach (var record in document.Scripts)
        {
            var storedFilePath = GetStoredFilePath(record);
            if (!File.Exists(storedFilePath))
            {
                continue;
            }

            var scriptDocument = _scriptDocumentService.LoadCompatible(storedFilePath).Document;
            record.Fingerprint = BuildDocumentFingerprint(scriptDocument);
            record.Description = scriptDocument.Metadata.Description;
            record.Map = scriptDocument.Metadata.Map;
            record.Difficulty = scriptDocument.Metadata.Difficulty;
            record.Mode = scriptDocument.Metadata.Mode;
            record.Hero = scriptDocument.Metadata.Hero;
            record.Tags = [.. ScriptTagCatalog.NormalizeStoredTags(scriptDocument.Metadata.Tags)];
            record.UpdatedAt = File.GetLastWriteTimeUtc(storedFilePath);
        }
    }

    private ManagedScriptLibrarySnapshot BuildSnapshot(ManagedScriptLibraryDocument document)
    {
        var currentBindings = LoadCurrentBindings(document);
        var assets = document.Scripts
            .Select(record => BuildAssetEntry(record, currentBindings))
            .OrderByDescending(x => x.UpdatedAt)
            .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var assetsById = assets.ToDictionary(x => x.ScriptId, StringComparer.OrdinalIgnoreCase);
        var bindingsBySlotId = currentBindings.ToDictionary(x => x.SlotId, StringComparer.OrdinalIgnoreCase);

        var slots = _slotCatalogService
            .GetAll()
            .Select(definition =>
            {
                bindingsBySlotId.TryGetValue(definition.SlotId, out var binding);
                ManagedScriptAssetEntry? boundScript = null;
                if (binding is not null && binding.ScriptId.Length > 0)
                {
                    assetsById.TryGetValue(binding.ScriptId, out boundScript);
                }

                return new ManagedScriptSlotEntry
                {
                    Definition = definition,
                    BoundScriptId = binding?.ScriptId ?? string.Empty,
                    BoundScript = boundScript
                };
            })
            .OrderBy(x => x.Definition.TaskKind)
            .ThenBy(x => x.Definition.GroupName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Definition.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ManagedScriptLibrarySnapshot
        {
            Scripts = assets,
            Slots = slots
        };
    }

    private ManagedScriptAssetEntry BuildAssetEntry(
        ManagedScriptAssetRecord record,
        IReadOnlyList<ManagedScriptSlotBindingRecord> bindings)
    {
        var storedFilePath = GetStoredFilePath(record);
        var map = TryParseEnum(record.Map, GameMapType.MonkeyMeadow, out var parsedMap);
        var difficulty = TryParseEnum(record.Difficulty, StageDifficulty.Medium, out var parsedDifficulty);
        var mode = TryParseEnum(record.Mode, StageMode.Standard, out var parsedMode);
        var hero = TryParseEnum(record.Hero, HeroType.Quincy, out var parsedHero);

        return new ManagedScriptAssetEntry
        {
            ScriptId = record.ScriptId,
            DisplayName = record.DisplayName,
            SourceFileName = record.SourceFileName,
            StoredFilePath = storedFilePath,
            Description = record.Description,
            Map = parsedMap,
            Difficulty = parsedDifficulty,
            Mode = parsedMode,
            Hero = parsedHero,
            Tags = [.. ScriptTagCatalog.NormalizeStoredTags(record.Tags)],
            ImportedAt = record.ImportedAt,
            UpdatedAt = record.UpdatedAt,
            BindingCount = bindings.Count(x => string.Equals(x.ScriptId, record.ScriptId, StringComparison.OrdinalIgnoreCase)),
            HasMissingFile = !File.Exists(storedFilePath),
            HasMetadataIssue = !(map && difficulty && mode && hero)
        };
    }

    private string GetStoredFilePath(ManagedScriptAssetRecord record)
    {
        return Path.Combine(_assetsDirectory, record.StoredFileName);
    }

    private ManagedScriptAssetRecord CreateManagedScriptRecord(
        ScriptDocument scriptDocument,
        string displayName,
        string sourceFileName,
        string? fingerprint = null)
    {
        ArgumentNullException.ThrowIfNull(scriptDocument);

        var scriptId = Guid.NewGuid().ToString("N");
        var storedFileName = $"{scriptId}.btd";
        var storedFilePath = Path.Combine(_assetsDirectory, storedFileName);
        _scriptDocumentService.Save(storedFilePath, scriptDocument);

        var now = DateTimeOffset.UtcNow;
        return new ManagedScriptAssetRecord
        {
            ScriptId = scriptId,
            DisplayName = displayName.Trim(),
            SourceFileName = sourceFileName.Trim(),
            StoredFileName = storedFileName,
            Fingerprint = string.IsNullOrWhiteSpace(fingerprint)
                ? BuildDocumentFingerprint(scriptDocument)
                : fingerprint.Trim(),
            Description = scriptDocument.Metadata.Description,
            Map = scriptDocument.Metadata.Map,
            Difficulty = scriptDocument.Metadata.Difficulty,
            Mode = scriptDocument.Metadata.Mode,
            Hero = scriptDocument.Metadata.Hero,
            Tags = [.. ScriptTagCatalog.NormalizeStoredTags(scriptDocument.Metadata.Tags)],
            ImportedAt = now,
            UpdatedAt = now
        };
    }

    private ManagedScriptAssetRecord? FindRecordById(ManagedScriptLibraryDocument document, string? scriptId)
    {
        if (string.IsNullOrWhiteSpace(scriptId))
        {
            return null;
        }

        return document.Scripts.FirstOrDefault(x => string.Equals(x.ScriptId, scriptId.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private ManagedScriptAssetRecord? FindRecordByStoredFilePath(ManagedScriptLibraryDocument document, string sourceFilePath)
    {
        return document.Scripts.FirstOrDefault(record => AreSameFilePath(GetStoredFilePath(record), sourceFilePath));
    }

    private Dictionary<string, ManagedScriptAssetRecord> BuildFingerprintIndex(ManagedScriptLibraryDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var index = new Dictionary<string, ManagedScriptAssetRecord>(StringComparer.Ordinal);
        foreach (var record in document.Scripts)
        {
            var fingerprint = EnsureRecordFingerprint(record);
            if (string.IsNullOrWhiteSpace(fingerprint))
            {
                continue;
            }

            _ = index.TryAdd(fingerprint, record);
        }

        return index;
    }

    private string EnsureRecordFingerprint(ManagedScriptAssetRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (!string.IsNullOrWhiteSpace(record.Fingerprint))
        {
            return record.Fingerprint;
        }

        var storedFilePath = GetStoredFilePath(record);
        if (!File.Exists(storedFilePath))
        {
            return string.Empty;
        }

        var scriptDocument = _scriptDocumentService.LoadCompatible(storedFilePath).Document;
        record.Fingerprint = BuildDocumentFingerprint(scriptDocument);
        return record.Fingerprint;
    }

    private void EnsureStorage()
    {
        Directory.CreateDirectory(_rootDirectory);
        Directory.CreateDirectory(_assetsDirectory);
        Directory.CreateDirectory(_bindingsDirectory);
    }

    private IReadOnlyList<ManagedScriptSlotBindingRecord> LoadCurrentBindings(ManagedScriptLibraryDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var bindings = document.Bindings
            .Where(binding => binding.SlotId.Length > 0 &&
                              _slotCatalogService.TryGetById(binding.SlotId, out var slot) &&
                              !TryGetDedicatedBindingFilePath(slot.TaskKind, out _))
            .Select(binding => new ManagedScriptSlotBindingRecord
            {
                SlotId = binding.SlotId,
                ScriptId = binding.ScriptId,
                UpdatedAt = binding.UpdatedAt
            })
            .ToList();

        bindings.AddRange(LoadDedicatedBindingRecords(_blackBorderBindingsFilePath));
        bindings.AddRange(LoadDedicatedBindingRecords(_collectionBindingsFilePath));
        return bindings;
    }

    private IReadOnlyList<ManagedScriptSlotBindingRecord> LoadDedicatedBindingRecords(string filePath)
    {
        return LoadTaskBindingDocument(filePath)
            .Bindings
            .Select(binding => new ManagedScriptSlotBindingRecord
            {
                SlotId = binding.Key,
                ScriptId = binding.Value,
                UpdatedAt = DateTimeOffset.UtcNow
            })
            .ToList();
    }

    private void MigrateDedicatedBindings(ManagedScriptLibraryDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var blackBorderBindings = LoadTaskBindingDocument(_blackBorderBindingsFilePath);
        var collectionBindings = LoadTaskBindingDocument(_collectionBindingsFilePath);
        var manifestBindingsToRemove = new List<ManagedScriptSlotBindingRecord>();

        foreach (var binding in document.Bindings)
        {
            if (!_slotCatalogService.TryGetById(binding.SlotId, out var slot) ||
                !TryGetDedicatedBindingFilePath(slot.TaskKind, out var filePath))
            {
                continue;
            }

            var targetDocument = string.Equals(filePath, _blackBorderBindingsFilePath, StringComparison.OrdinalIgnoreCase)
                ? blackBorderBindings
                : collectionBindings;

            if (!targetDocument.Bindings.ContainsKey(binding.SlotId))
            {
                targetDocument.Bindings[binding.SlotId] = binding.ScriptId;
            }

            manifestBindingsToRemove.Add(binding);
        }

        if (manifestBindingsToRemove.Count == 0)
        {
            return;
        }

        document.Bindings.RemoveAll(binding => manifestBindingsToRemove.Contains(binding));
        SaveTaskBindingDocument(_blackBorderBindingsFilePath, blackBorderBindings);
        SaveTaskBindingDocument(_collectionBindingsFilePath, collectionBindings);
        SaveManifest(document);
    }

    private void RemoveScriptBindingsFromDedicatedFiles(string scriptId)
    {
        RemoveScriptBindingsFromDedicatedFile(_blackBorderBindingsFilePath, scriptId);
        RemoveScriptBindingsFromDedicatedFile(_collectionBindingsFilePath, scriptId);
    }

    private void RemoveScriptBindingsFromDedicatedFile(string filePath, string scriptId)
    {
        var document = LoadTaskBindingDocument(filePath);
        var keysToRemove = document.Bindings
            .Where(binding => string.Equals(binding.Value, scriptId, StringComparison.OrdinalIgnoreCase))
            .Select(binding => binding.Key)
            .ToList();

        if (keysToRemove.Count == 0)
        {
            return;
        }

        foreach (var key in keysToRemove)
        {
            document.Bindings.Remove(key);
        }

        SaveTaskBindingDocument(filePath, document);
    }

    private bool TryGetDedicatedBindingFilePath(AutoTaskKind taskKind, out string filePath)
    {
        switch (taskKind)
        {
            case AutoTaskKind.BlackBorder:
                filePath = _blackBorderBindingsFilePath;
                return true;
            case AutoTaskKind.Collection:
                filePath = _collectionBindingsFilePath;
                return true;
            default:
                filePath = string.Empty;
                return false;
        }
    }

    private static void NormalizeRecord(ManagedScriptAssetRecord record)
    {
        record.ScriptId = string.IsNullOrWhiteSpace(record.ScriptId)
            ? Guid.NewGuid().ToString("N")
            : record.ScriptId.Trim();
        record.DisplayName = record.DisplayName?.Trim() ?? string.Empty;
        record.SourceFileName = record.SourceFileName?.Trim() ?? string.Empty;
        record.StoredFileName = record.StoredFileName?.Trim() ?? string.Empty;
        record.Fingerprint = record.Fingerprint?.Trim() ?? string.Empty;
        record.Description = record.Description?.Trim() ?? string.Empty;
        record.Map = record.Map?.Trim() ?? GameMapType.MonkeyMeadow.ToString();
        record.Difficulty = record.Difficulty?.Trim() ?? StageDifficulty.Medium.ToString();
        record.Mode = record.Mode?.Trim() ?? StageMode.Standard.ToString();
        record.Hero = record.Hero?.Trim() ?? HeroType.Quincy.ToString();
        record.Tags = [.. ScriptTagCatalog.NormalizeStoredTags(record.Tags)];
    }

    private static bool TryParseEnum<TEnum>(string? value, TEnum fallback, out TEnum result)
        where TEnum : struct, Enum
    {
        if (Enum.TryParse<TEnum>(value, ignoreCase: true, out result))
        {
            return true;
        }

        result = fallback;
        return false;
    }

    private static string ResolveDisplayName(string? displayName, string sourceFilePath)
    {
        return string.IsNullOrWhiteSpace(displayName)
            ? Path.GetFileNameWithoutExtension(sourceFilePath)
            : displayName.Trim();
    }

    private static bool AreSameFilePath(string left, string right)
    {
        return string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildDocumentFingerprint(ScriptDocument scriptDocument)
    {
        ArgumentNullException.ThrowIfNull(scriptDocument);
        return MD5Helper.ComputeMD5(JsonSerializer.Serialize(scriptDocument));
    }

    private static string ResolveLegacyPackageScriptDisplayName(
        LegacyScriptModel legacyDocument,
        string packageDisplayName,
        int index)
    {
        ArgumentNullException.ThrowIfNull(legacyDocument);

        if (!string.IsNullOrWhiteSpace(legacyDocument.Metadata.ScriptName))
        {
            return legacyDocument.Metadata.ScriptName.Trim();
        }

        return $"{packageDisplayName} #{index + 1:000}";
    }
}
