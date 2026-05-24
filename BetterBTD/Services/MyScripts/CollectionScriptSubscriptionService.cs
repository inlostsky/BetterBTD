using System.IO;
using System.IO.Compression;
using System.Text.Json;
using BetterBTD.Models.AutoTasks;
using BetterBTD.Models.MyScripts;

namespace BetterBTD.Services.MyScripts;

public sealed class CollectionScriptSubscriptionService
{
    private const string ManifestEntryName = "collection-subscription.json";
    private const string ScriptsDirectoryName = "scripts";

    private static readonly Lazy<CollectionScriptSubscriptionService> InstanceHolder =
        new(() => new CollectionScriptSubscriptionService(
            ManagedScriptLibraryService.Instance,
            ManagedScriptSlotCatalogService.Instance));

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly ManagedScriptLibraryService _managedScriptLibraryService;
    private readonly ManagedScriptSlotCatalogService _slotCatalogService;

    internal CollectionScriptSubscriptionService(
        ManagedScriptLibraryService managedScriptLibraryService,
        ManagedScriptSlotCatalogService slotCatalogService)
    {
        _managedScriptLibraryService = managedScriptLibraryService ?? throw new ArgumentNullException(nameof(managedScriptLibraryService));
        _slotCatalogService = slotCatalogService ?? throw new ArgumentNullException(nameof(slotCatalogService));
    }

    public static CollectionScriptSubscriptionService Instance => InstanceHolder.Value;

    public void Export(string targetFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetFilePath);

        var snapshot = _managedScriptLibraryService.GetSnapshot();
        var collectionSlots = snapshot.Slots
            .Where(slot => slot.Definition.TaskKind == AutoTaskKind.Collection && slot.BoundScript is not null)
            .ToList();
        if (collectionSlots.Count == 0)
        {
            throw new InvalidOperationException("No collection slot bindings were found.");
        }

        var uniqueScripts = collectionSlots
            .Select(slot => slot.BoundScript!)
            .GroupBy(script => script.CanonicalScriptId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(script => script.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var fileNamesByCanonicalId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var directory = Path.GetDirectoryName(targetFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var archive = ZipFile.Open(targetFilePath, ZipArchiveMode.Create);
        foreach (var script in uniqueScripts)
        {
            var safeFileName = SanitizeFileName(script.DisplayName);
            var entryFileName = $"{safeFileName}-{script.CanonicalScriptId}.btd";
            var archiveEntryName = $"{ScriptsDirectoryName}/{entryFileName}";
            archive.CreateEntryFromFile(script.StoredFilePath, archiveEntryName);
            fileNamesByCanonicalId[script.CanonicalScriptId] = entryFileName;
        }

        var manifest = new CollectionScriptSubscriptionDocument
        {
            Scripts = uniqueScripts
                .Select(script => new CollectionScriptSubscriptionScriptDocument
                {
                    CanonicalScriptId = script.CanonicalScriptId,
                    DisplayName = script.DisplayName,
                    FileName = fileNamesByCanonicalId[script.CanonicalScriptId]
                })
                .ToList(),
            Bindings = collectionSlots.ToDictionary(
                slot => slot.Definition.SlotId,
                slot => slot.BoundScript!.CanonicalScriptId,
                StringComparer.OrdinalIgnoreCase)
        };

        var manifestEntry = archive.CreateEntry(ManifestEntryName);
        using var writer = new StreamWriter(manifestEntry.Open());
        writer.Write(JsonSerializer.Serialize(manifest, JsonOptions));
    }

    public void Import(string sourceFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFilePath);

        if (!File.Exists(sourceFilePath))
        {
            throw new FileNotFoundException("Collection subscription package was not found.", sourceFilePath);
        }

        using var archive = ZipFile.OpenRead(sourceFilePath);
        var manifestEntry = archive.GetEntry(ManifestEntryName)
            ?? throw new InvalidDataException("Collection subscription manifest was not found.");
        CollectionScriptSubscriptionDocument manifest;
        using (var reader = new StreamReader(manifestEntry.Open()))
        {
            manifest = JsonSerializer.Deserialize<CollectionScriptSubscriptionDocument>(reader.ReadToEnd(), JsonOptions)
                       ?? throw new InvalidDataException("Collection subscription manifest is invalid.");
        }

        ValidateManifest(manifest);

        var importedScriptIdsByCanonicalId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var tempRoot = Path.Combine(Path.GetTempPath(), $"betterbtd-collection-subscription-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            foreach (var script in manifest.Scripts)
            {
                var archiveEntry = archive.GetEntry($"{ScriptsDirectoryName}/{script.FileName}")
                    ?? throw new InvalidDataException($"Script payload '{script.FileName}' was not found.");
                var extractedFilePath = Path.Combine(tempRoot, script.FileName);
                archiveEntry.ExtractToFile(extractedFilePath, overwrite: true);

                var imported = _managedScriptLibraryService.ImportScript(extractedFilePath);
                if (!string.Equals(imported.CanonicalScriptId, script.CanonicalScriptId, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"Script '{script.FileName}' canonical ID does not match manifest.");
                }

                importedScriptIdsByCanonicalId[script.CanonicalScriptId] = imported.ScriptId;
            }

            foreach (var binding in manifest.Bindings)
            {
                if (!_slotCatalogService.TryGetById(binding.Key, out var slot) || slot.TaskKind != AutoTaskKind.Collection)
                {
                    continue;
                }

                if (!importedScriptIdsByCanonicalId.TryGetValue(binding.Value, out var localScriptId))
                {
                    throw new InvalidDataException($"Binding target canonical script ID '{binding.Value}' was not imported.");
                }

                _managedScriptLibraryService.SetBinding(binding.Key, localScriptId);
            }
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static void ValidateManifest(CollectionScriptSubscriptionDocument manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        if (!string.Equals(manifest.Kind, "better-btd/collection-subscription", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Unsupported collection subscription kind '{manifest.Kind}'.");
        }

        manifest.Scripts ??= [];
        manifest.Bindings ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var script in manifest.Scripts)
        {
            if (string.IsNullOrWhiteSpace(script.CanonicalScriptId) || string.IsNullOrWhiteSpace(script.FileName))
            {
                throw new InvalidDataException("Collection subscription contains an invalid script entry.");
            }
        }
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string((value ?? string.Empty)
            .Trim()
            .Select(ch => invalidChars.Contains(ch) ? '_' : ch)
            .ToArray());

        return string.IsNullOrWhiteSpace(sanitized) ? "script" : sanitized;
    }
}
