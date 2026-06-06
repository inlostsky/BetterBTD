using System.IO;
using System.Threading;
using BetterBTD.Models.ScriptExecution;
using BetterBTD.Services.Diagnostics;

namespace BetterBTD.Core.ScriptExecution;

public sealed class ScriptExecutionRuntimeLogger : IDisposable
{
    private readonly object _syncRoot = new();
    private readonly DiagnosticsFileLogWriter _writer;
    private bool _disposed;

    public ScriptExecutionRuntimeLogger(string sourceFilePath)
    {
        FilePath = DiagnosticsLogFilePathFactory.CreateDatedLogFilePath("ScriptExecution", ResolveSessionName(sourceFilePath));
        _writer = new DiagnosticsFileLogWriter(FilePath);
    }

    public event EventHandler<ScriptExecutionRuntimeLogEntry>? EntryAdded;

    public string FilePath { get; }

    public void Trace(
        ScriptExecutionRuntimeLogCategory category,
        string message,
        string? aggregationKey = null,
        bool replaceExisting = false)
    {
        Log(ScriptExecutionRuntimeLogLevel.Trace, category, message, aggregationKey, replaceExisting);
    }

    public void Info(
        ScriptExecutionRuntimeLogCategory category,
        string message,
        string? aggregationKey = null,
        bool replaceExisting = false)
    {
        Log(ScriptExecutionRuntimeLogLevel.Info, category, message, aggregationKey, replaceExisting);
    }

    public void Warning(
        ScriptExecutionRuntimeLogCategory category,
        string message,
        string? aggregationKey = null,
        bool replaceExisting = false)
    {
        Log(ScriptExecutionRuntimeLogLevel.Warning, category, message, aggregationKey, replaceExisting);
    }

    public void Error(
        ScriptExecutionRuntimeLogCategory category,
        string message,
        string? aggregationKey = null,
        bool replaceExisting = false)
    {
        Log(ScriptExecutionRuntimeLogLevel.Error, category, message, aggregationKey, replaceExisting);
    }

    public ScriptExecutionPollingLogScope CreatePollingScope(
        string operationKey,
        string description,
        int timeoutMilliseconds,
        int pollIntervalMilliseconds,
        int stableSuccessCount)
    {
        return new ScriptExecutionPollingLogScope(
            this,
            operationKey,
            description,
            timeoutMilliseconds,
            pollIntervalMilliseconds,
            stableSuccessCount);
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _writer.Dispose();
        }
    }

    internal void Log(
        ScriptExecutionRuntimeLogLevel level,
        ScriptExecutionRuntimeLogCategory category,
        string message,
        string? aggregationKey,
        bool replaceExisting)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        ScriptExecutionRuntimeLogEntry entry;
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            entry = new ScriptExecutionRuntimeLogEntry
            {
                Timestamp = DateTimeOffset.Now,
                Level = level,
                Category = category,
                Message = message.Trim(),
                AggregationKey = aggregationKey?.Trim() ?? string.Empty,
                ReplaceExisting = replaceExisting
            };

            _writer.Write(level.ToString(), category.ToString(), entry.Message);
        }

        EntryAdded?.Invoke(this, entry);
    }

    private static string ResolveSessionName(string sourceFilePath)
    {
        return string.IsNullOrWhiteSpace(sourceFilePath)
            ? "UnsavedScript"
            : Path.GetFileNameWithoutExtension(sourceFilePath);
    }
}

public sealed class ScriptExecutionPollingLogScope
{
    private readonly ScriptExecutionRuntimeLogger _logger;
    private readonly string _aggregationKey;
    private readonly string _description;
    private readonly int _timeoutMilliseconds;
    private readonly int _pollIntervalMilliseconds;
    private readonly int _stableSuccessCount;
    private readonly DateTimeOffset _startedAt;

    public ScriptExecutionPollingLogScope(
        ScriptExecutionRuntimeLogger logger,
        string operationKey,
        string description,
        int timeoutMilliseconds,
        int pollIntervalMilliseconds,
        int stableSuccessCount)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _aggregationKey = $"poll:{operationKey}";
        _description = string.IsNullOrWhiteSpace(description) ? "condition" : description.Trim();
        _timeoutMilliseconds = Math.Max(0, timeoutMilliseconds);
        _pollIntervalMilliseconds = Math.Max(0, pollIntervalMilliseconds);
        _stableSuccessCount = Math.Max(1, stableSuccessCount);
        _startedAt = DateTimeOffset.UtcNow;

        _logger.Info(
            ScriptExecutionRuntimeLogCategory.Polling,
            $"Started polling '{_description}' (timeout={_timeoutMilliseconds} ms, interval={_pollIntervalMilliseconds} ms, stableSuccess={_stableSuccessCount}).",
            _aggregationKey,
            replaceExisting: true);
    }

    public void ReportAttempt(int attempt, int currentSuccessCount, string? detail = null)
    {
        var elapsedMilliseconds = (DateTimeOffset.UtcNow - _startedAt).TotalMilliseconds;
        var detailText = string.IsNullOrWhiteSpace(detail) ? string.Empty : $" | {detail.Trim()}";
        _logger.Trace(
            ScriptExecutionRuntimeLogCategory.Polling,
            $"Polling '{_description}' | attempt={attempt} | stable={currentSuccessCount}/{_stableSuccessCount} | elapsed={elapsedMilliseconds:F0} ms{detailText}",
            _aggregationKey,
            replaceExisting: true);
    }

    public void Complete(int attempt, int currentSuccessCount, string? detail = null)
    {
        var elapsedMilliseconds = (DateTimeOffset.UtcNow - _startedAt).TotalMilliseconds;
        var detailText = string.IsNullOrWhiteSpace(detail) ? string.Empty : $" | {detail.Trim()}";
        _logger.Info(
            ScriptExecutionRuntimeLogCategory.Polling,
            $"Polling '{_description}' satisfied | attempts={attempt} | stable={currentSuccessCount}/{_stableSuccessCount} | elapsed={elapsedMilliseconds:F0} ms{detailText}",
            _aggregationKey,
            replaceExisting: true);
    }

    public void Timeout(int attempt, int currentSuccessCount, string? detail = null)
    {
        var elapsedMilliseconds = (DateTimeOffset.UtcNow - _startedAt).TotalMilliseconds;
        var detailText = string.IsNullOrWhiteSpace(detail) ? string.Empty : $" | {detail.Trim()}";
        _logger.Warning(
            ScriptExecutionRuntimeLogCategory.Polling,
            $"Polling '{_description}' timed out | attempts={attempt} | stable={currentSuccessCount}/{_stableSuccessCount} | elapsed={elapsedMilliseconds:F0} ms{detailText}",
            _aggregationKey,
            replaceExisting: true);
    }
}

public static class ScriptExecutionRuntimeDiagnostics
{
    private static readonly AsyncLocal<ScriptExecutionRuntimeLogger?> CurrentLoggerHolder = new();

    public static ScriptExecutionRuntimeLogger? Current => CurrentLoggerHolder.Value;

    public static IDisposable PushLogger(ScriptExecutionRuntimeLogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        var previousLogger = CurrentLoggerHolder.Value;
        CurrentLoggerHolder.Value = logger;
        return new PopWhenDisposed(previousLogger);
    }

    public static void Trace(
        ScriptExecutionRuntimeLogCategory category,
        string message,
        string? aggregationKey = null,
        bool replaceExisting = false)
    {
        Current?.Trace(category, message, aggregationKey, replaceExisting);
    }

    public static void Info(
        ScriptExecutionRuntimeLogCategory category,
        string message,
        string? aggregationKey = null,
        bool replaceExisting = false)
    {
        Current?.Info(category, message, aggregationKey, replaceExisting);
    }

    public static void Warning(
        ScriptExecutionRuntimeLogCategory category,
        string message,
        string? aggregationKey = null,
        bool replaceExisting = false)
    {
        Current?.Warning(category, message, aggregationKey, replaceExisting);
    }

    public static void Error(
        ScriptExecutionRuntimeLogCategory category,
        string message,
        string? aggregationKey = null,
        bool replaceExisting = false)
    {
        Current?.Error(category, message, aggregationKey, replaceExisting);
    }

    private sealed class PopWhenDisposed : IDisposable
    {
        private readonly ScriptExecutionRuntimeLogger? _previousLogger;
        private bool _disposed;

        public PopWhenDisposed(ScriptExecutionRuntimeLogger? previousLogger)
        {
            _previousLogger = previousLogger;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            CurrentLoggerHolder.Value = _previousLogger;
        }
    }
}
