using BetterBTD.Models;
using OpenCvSharp;

namespace BetterBTD.Services.Diagnostics;

public sealed class GameCaptureDiagnosticsService : IDisposable
{
    private static readonly Lazy<GameCaptureDiagnosticsService> InstanceHolder = new(() => new GameCaptureDiagnosticsService());
    private static readonly TimeSpan[] CaptureAttemptWatchdogThresholds =
    [
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(15)
    ];
    private static readonly TimeSpan[] ServedFrameAgeThresholds =
    [
        TimeSpan.FromMilliseconds(200),
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5)
    ];
    private static readonly int[] RepeatThresholds = [5, 20, 100, 500];

    private readonly object _syncRoot = new();
    private readonly Timer _watchdogTimer;

    private DiagnosticsFileLogWriter? _writer;
    private bool _sessionActive;
    private string _currentWindowTitle = string.Empty;
    private nint _currentWindowHandle;
    private long _publishedFrameCount;
    private long _servedFrameCount;
    private long _failedFrameRequestCount;
    private long _nullFrameCount;
    private long _emptyFrameCount;
    private long _captureExceptionCount;
    private long _latestPublishedSequence;
    private DateTimeOffset _latestPublishedAt = DateTimeOffset.MinValue;
    private ulong _latestPublishedFingerprint;
    private int _sameFingerprintPublishStreak;
    private long _lastServedSequence;
    private int _sameServedSequenceStreak;
    private int _lastServedAgeBucket;
    private bool _captureAttemptInFlight;
    private long _inFlightAttemptId;
    private DateTimeOffset _inFlightAttemptStartedAt = DateTimeOffset.MinValue;
    private int _lastWatchdogBucket;

    private GameCaptureDiagnosticsService()
    {
        _watchdogTimer = new Timer(OnWatchdogTimerTick, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    public static GameCaptureDiagnosticsService Instance => InstanceHolder.Value;

    public string CurrentLogFilePath
    {
        get
        {
            lock (_syncRoot)
            {
                return _writer?.FilePath ?? string.Empty;
            }
        }
    }

    public long BeginCaptureAttempt(long attemptId)
    {
        lock (_syncRoot)
        {
            if (!_sessionActive)
            {
                return attemptId;
            }

            _captureAttemptInFlight = true;
            _inFlightAttemptId = attemptId;
            _inFlightAttemptStartedAt = DateTimeOffset.UtcNow;
            _lastWatchdogBucket = 0;
            return attemptId;
        }
    }

    public void StartSession(GameWindowInfo windowInfo, GameCaptureOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var sessionName = BuildSessionName(windowInfo, options);
        var filePath = DiagnosticsLogFilePathFactory.CreateDatedLogFilePath("Capture", sessionName);
        var writer = new DiagnosticsFileLogWriter(filePath);

        lock (_syncRoot)
        {
            DisposeWriterUnderLock();
            _writer = writer;
            _sessionActive = true;
            _currentWindowTitle = windowInfo.Title ?? string.Empty;
            _currentWindowHandle = windowInfo.Handle;
            ResetSessionStateUnderLock();

            _writer.Info(
                "Session",
                $"Capture diagnostics started | window='{_currentWindowTitle}' | handle=0x{_currentWindowHandle:X} | mode={options.CaptureModeName} | interval={options.CaptureIntervalMs} ms | autoFixWin11BitBlt={options.AutoFixWin11BitBlt}.");
        }
    }

    public void StopSession(string reason)
    {
        lock (_syncRoot)
        {
            if (!_sessionActive && _writer is null)
            {
                return;
            }

            _writer?.Info(
                "Session",
                $"Capture diagnostics stopped | reason={NormalizeReason(reason)} | published={_publishedFrameCount} | served={_servedFrameCount} | failedRequests={_failedFrameRequestCount} | nullFrames={_nullFrameCount} | emptyFrames={_emptyFrameCount} | captureExceptions={_captureExceptionCount} | latestSeq={_latestPublishedSequence}.");

            _sessionActive = false;
            _captureAttemptInFlight = false;
            DisposeWriterUnderLock();
            _currentWindowTitle = string.Empty;
            _currentWindowHandle = nint.Zero;
            ResetSessionStateUnderLock();
        }
    }

    public void RecordCaptureLoopStarted()
    {
        lock (_syncRoot)
        {
            _writer?.Info("Loop", "Capture loop started.");
        }
    }

    public void RecordCaptureLoopStopped()
    {
        lock (_syncRoot)
        {
            _writer?.Info("Loop", "Capture loop stopped.");
        }
    }

    public void RecordCaptureAttemptCancelled(long attemptId, TimeSpan elapsed)
    {
        lock (_syncRoot)
        {
            CompleteCaptureAttemptUnderLock(attemptId);
            _writer?.Info("Loop", $"Capture attempt cancelled | attempt={attemptId} | elapsed={elapsed.TotalMilliseconds:F0} ms.");
        }
    }

    public void RecordCaptureAttemptDiscarded(long attemptId, TimeSpan elapsed, string reason)
    {
        lock (_syncRoot)
        {
            CompleteCaptureAttemptUnderLock(attemptId);
            _writer?.Info(
                "Loop",
                $"Capture attempt discarded | attempt={attemptId} | elapsed={elapsed.TotalMilliseconds:F0} ms | reason={NormalizeReason(reason)}.");
        }
    }

    public void RecordCaptureAttemptFaulted(long attemptId, TimeSpan elapsed, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        lock (_syncRoot)
        {
            _captureExceptionCount++;
            CompleteCaptureAttemptUnderLock(attemptId);
            _writer?.Error(
                "Loop",
                $"Capture attempt faulted | attempt={attemptId} | elapsed={elapsed.TotalMilliseconds:F0} ms | exception={exception.GetType().Name} | message='{exception.Message}'.");
        }
    }

    public void RecordCaptureAttemptReturnedNull(long attemptId, TimeSpan elapsed)
    {
        lock (_syncRoot)
        {
            _nullFrameCount++;
            CompleteCaptureAttemptUnderLock(attemptId);
            _writer?.Warning(
                "Loop",
                $"Capture attempt returned null frame | attempt={attemptId} | elapsed={elapsed.TotalMilliseconds:F0} ms.");
        }
    }

    public void RecordCaptureAttemptReturnedEmptyFrame(long attemptId, TimeSpan elapsed, int width, int height)
    {
        lock (_syncRoot)
        {
            _emptyFrameCount++;
            CompleteCaptureAttemptUnderLock(attemptId);
            _writer?.Warning(
                "Loop",
                $"Capture attempt returned empty frame | attempt={attemptId} | elapsed={elapsed.TotalMilliseconds:F0} ms | size={Math.Max(0, width)}x{Math.Max(0, height)}.");
        }
    }

    public void RecordFramePublished(
        long attemptId,
        long frameSequence,
        DateTimeOffset publishedAt,
        int width,
        int height,
        ulong fingerprint,
        TimeSpan captureElapsed,
        int captureIntervalMs)
    {
        lock (_syncRoot)
        {
            var previousSequence = _latestPublishedSequence;
            var previousFingerprint = _latestPublishedFingerprint;
            var previousStreak = _sameFingerprintPublishStreak;
            var isSameFingerprint = previousSequence > 0 &&
                                    previousFingerprint == fingerprint;

            _publishedFrameCount++;
            _latestPublishedSequence = frameSequence;
            _latestPublishedAt = publishedAt;
            _latestPublishedFingerprint = fingerprint;
            _sameFingerprintPublishStreak = isSameFingerprint ? previousStreak + 1 : 1;
            CompleteCaptureAttemptUnderLock(attemptId);

            var repeatedDurationMs = Math.Max(0, (_sameFingerprintPublishStreak - 1) * Math.Max(1, captureIntervalMs));
            if (_sameFingerprintPublishStreak > 1 && ShouldLogRepeat(_sameFingerprintPublishStreak))
            {
                _writer?.Warning(
                    "Frame",
                    $"Published identical-looking frame repeatedly | seq={frameSequence} | size={width}x{height} | fingerprint=0x{fingerprint:X16} | sameFingerprintStreak={_sameFingerprintPublishStreak} | approxDuration={repeatedDurationMs} ms | captureElapsed={captureElapsed.TotalMilliseconds:F0} ms.");
                return;
            }

            var shouldLogPublish = frameSequence == 1 ||
                                   frameSequence % 100 == 0 ||
                                   previousStreak >= 5 ||
                                   captureElapsed.TotalMilliseconds >= Math.Max(200, Math.Max(1, captureIntervalMs) * 2);
            if (!shouldLogPublish)
            {
                return;
            }

            _writer?.Trace(
                "Frame",
                $"Published frame | seq={frameSequence} | size={width}x{height} | fingerprint=0x{fingerprint:X16} | captureElapsed={captureElapsed.TotalMilliseconds:F0} ms | published={publishedAt:HH:mm:ss.fff}.");
        }
    }

    public void RecordFrameServed(
        long frameSequence,
        DateTimeOffset publishedAt,
        int width,
        int height,
        ulong fingerprint)
    {
        lock (_syncRoot)
        {
            _servedFrameCount++;

            var age = publishedAt == DateTimeOffset.MinValue
                ? TimeSpan.Zero
                : DateTimeOffset.UtcNow - publishedAt;
            var ageBucket = GetThresholdBucket(age, ServedFrameAgeThresholds);

            if (_lastServedSequence == frameSequence)
            {
                _sameServedSequenceStreak++;
            }
            else
            {
                _lastServedSequence = frameSequence;
                _sameServedSequenceStreak = 1;
                _lastServedAgeBucket = 0;
            }

            if (ageBucket > 0 &&
                (ageBucket > _lastServedAgeBucket || ShouldLogRepeat(_sameServedSequenceStreak)))
            {
                _writer?.Warning(
                    "Consumer",
                    $"Served stale frame repeatedly | seq={frameSequence} | size={width}x{height} | fingerprint=0x{fingerprint:X16} | age={age.TotalMilliseconds:F0} ms | sameSeqServeStreak={_sameServedSequenceStreak}.");
            }
            else if (_servedFrameCount == 1 || _servedFrameCount % 200 == 0)
            {
                _writer?.Trace(
                    "Consumer",
                    $"Served frame | seq={frameSequence} | size={width}x{height} | fingerprint=0x{fingerprint:X16} | age={age.TotalMilliseconds:F0} ms | servedCount={_servedFrameCount}.");
            }

            _lastServedAgeBucket = Math.Max(_lastServedAgeBucket, ageBucket);
        }
    }

    public void RecordFrameRequestFailed(bool isRunning, bool hasLatestFrame)
    {
        lock (_syncRoot)
        {
            _failedFrameRequestCount++;
            _writer?.Warning(
                "Consumer",
                $"Frame request failed | isRunning={isRunning} | hasLatestFrame={hasLatestFrame} | latestSeq={_latestPublishedSequence} | latestAge={FormatLatestAgeUnderLock()} | failedCount={_failedFrameRequestCount}.");
        }
    }

    public void RecordLatestFrameCleared(string reason)
    {
        lock (_syncRoot)
        {
            _writer?.Info(
                "Frame",
                $"Latest frame cleared | reason={NormalizeReason(reason)} | latestSeq={_latestPublishedSequence} | latestAge={FormatLatestAgeUnderLock()}.");
        }
    }

    public static ulong CalculateFingerprint(Mat frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (frame.Empty() || frame.Width <= 0 || frame.Height <= 0)
        {
            return 0;
        }

        unchecked
        {
            ulong hash = 1469598103934665603UL;
            var samplePoints = new (double X, double Y)[]
            {
                (0.10d, 0.10d),
                (0.50d, 0.10d),
                (0.90d, 0.10d),
                (0.10d, 0.50d),
                (0.50d, 0.50d),
                (0.90d, 0.50d),
                (0.10d, 0.90d),
                (0.50d, 0.90d),
                (0.90d, 0.90d)
            };

            foreach (var point in samplePoints)
            {
                var x = Math.Clamp((int)Math.Round((frame.Width - 1) * point.X), 0, Math.Max(0, frame.Width - 1));
                var y = Math.Clamp((int)Math.Round((frame.Height - 1) * point.Y), 0, Math.Max(0, frame.Height - 1));
                var pixel = frame.At<Vec3b>(y, x);
                hash ^= pixel.Item0;
                hash *= 1099511628211UL;
                hash ^= pixel.Item1;
                hash *= 1099511628211UL;
                hash ^= pixel.Item2;
                hash *= 1099511628211UL;
            }

            hash ^= (ulong)frame.Width;
            hash *= 1099511628211UL;
            hash ^= (ulong)frame.Height;
            hash *= 1099511628211UL;
            return hash;
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            _sessionActive = false;
            _captureAttemptInFlight = false;
            DisposeWriterUnderLock();
        }

        _watchdogTimer.Dispose();
    }

    private static string BuildSessionName(GameWindowInfo windowInfo, GameCaptureOptions options)
    {
        var windowTitle = string.IsNullOrWhiteSpace(windowInfo.Title) ? "UnknownWindow" : windowInfo.Title.Trim();
        var modeName = string.IsNullOrWhiteSpace(options.CaptureModeName) ? "UnknownMode" : options.CaptureModeName.Trim();
        return $"{windowTitle}_{modeName}";
    }

    private static string NormalizeReason(string reason)
    {
        return string.IsNullOrWhiteSpace(reason) ? "unspecified" : reason.Trim();
    }

    private static int GetThresholdBucket(TimeSpan elapsed, IReadOnlyList<TimeSpan> thresholds)
    {
        var bucket = 0;
        for (var index = 0; index < thresholds.Count; index++)
        {
            if (elapsed >= thresholds[index])
            {
                bucket = index + 1;
            }
        }

        return bucket;
    }

    private static bool ShouldLogRepeat(int count)
    {
        return RepeatThresholds.Contains(count) || (count > 0 && count % 1000 == 0);
    }

    private void OnWatchdogTimerTick(object? state)
    {
        lock (_syncRoot)
        {
            if (!_sessionActive || !_captureAttemptInFlight || _writer is null)
            {
                return;
            }

            var elapsed = DateTimeOffset.UtcNow - _inFlightAttemptStartedAt;
            var bucket = GetThresholdBucket(elapsed, CaptureAttemptWatchdogThresholds);
            if (bucket <= _lastWatchdogBucket)
            {
                return;
            }

            _lastWatchdogBucket = bucket;
            _writer.Warning(
                "Watchdog",
                $"capture.Capture() is still in flight | attempt={_inFlightAttemptId} | elapsed={elapsed.TotalMilliseconds:F0} ms | latestSeq={_latestPublishedSequence} | latestAge={FormatLatestAgeUnderLock()}.");
        }
    }

    private void CompleteCaptureAttemptUnderLock(long attemptId)
    {
        if (!_captureAttemptInFlight || _inFlightAttemptId != attemptId)
        {
            return;
        }

        if (_lastWatchdogBucket > 0 && _writer is not null)
        {
            var elapsed = DateTimeOffset.UtcNow - _inFlightAttemptStartedAt;
            _writer.Info(
                "Watchdog",
                $"Capture attempt recovered | attempt={attemptId} | elapsed={elapsed.TotalMilliseconds:F0} ms.");
        }

        _captureAttemptInFlight = false;
        _inFlightAttemptId = 0;
        _inFlightAttemptStartedAt = DateTimeOffset.MinValue;
        _lastWatchdogBucket = 0;
    }

    private string FormatLatestAgeUnderLock()
    {
        if (_latestPublishedAt == DateTimeOffset.MinValue)
        {
            return "n/a";
        }

        return $"{(DateTimeOffset.UtcNow - _latestPublishedAt).TotalMilliseconds:F0} ms";
    }

    private void DisposeWriterUnderLock()
    {
        _writer?.Dispose();
        _writer = null;
    }

    private void ResetSessionStateUnderLock()
    {
        _publishedFrameCount = 0;
        _servedFrameCount = 0;
        _failedFrameRequestCount = 0;
        _nullFrameCount = 0;
        _emptyFrameCount = 0;
        _captureExceptionCount = 0;
        _latestPublishedSequence = 0;
        _latestPublishedAt = DateTimeOffset.MinValue;
        _latestPublishedFingerprint = 0;
        _sameFingerprintPublishStreak = 0;
        _lastServedSequence = 0;
        _sameServedSequenceStreak = 0;
        _lastServedAgeBucket = 0;
        _captureAttemptInFlight = false;
        _inFlightAttemptId = 0;
        _inFlightAttemptStartedAt = DateTimeOffset.MinValue;
        _lastWatchdogBucket = 0;
    }
}
