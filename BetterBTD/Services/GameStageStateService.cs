using BetterBTD.Core.ScriptExecution.Runtime;
using BetterBTD.Models.ScriptExecution;
using OpenCvSharp;
using OpenCvRect = OpenCvSharp.Rect;
using OpenCvSize = OpenCvSharp.Size;

namespace BetterBTD.Services;

public sealed class GameStageStateService : IGameStageStateService
{
    private static readonly Lazy<GameStageStateService> InstanceHolder = new(() => new GameStageStateService());
    private static readonly OpenCvRect GoldReferenceRect = new(360, 20, 180, 50);
    private static readonly OpenCvRect RoundReferenceRect = new(1370, 25, 195, 70);
    private static readonly OpenCvSize Reference1080p = new(1920, 1080);

    private readonly GameCaptureService _gameCaptureService;
    private readonly GameTargetOcrService _gameTargetOcrService;

    private GameStageStateService()
    {
        _gameCaptureService = GameCaptureService.Instance;
        _gameTargetOcrService = GameTargetOcrService.Instance;
    }

    public static GameStageStateService Instance => InstanceHolder.Value;

    public bool IsAvailable => _gameTargetOcrService.IsAvailable;

    public (OpenCvRect GoldRegion, OpenCvRect RoundRegion) GetCaptureRegions(int frameWidth, int frameHeight)
    {
        if (frameWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frameWidth));
        }

        if (frameHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frameHeight));
        }

        return (
            ScaleReferenceRect(GoldReferenceRect, frameWidth, frameHeight),
            ScaleReferenceRect(RoundReferenceRect, frameWidth, frameHeight));
    }

    public Task<GameStageStateSnapshot?> CaptureSnapshotAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(TryCaptureSnapshot(out var snapshot, out _) ? snapshot : null);
    }

    public bool TryCaptureSnapshot(out GameStageStateSnapshot snapshot)
    {
        return TryCaptureSnapshot(out snapshot, out _);
    }

    public bool TryCaptureSnapshot(out GameStageStateSnapshot snapshot, out string failureMessage)
    {
        snapshot = new GameStageStateSnapshot();
        failureMessage = "Capture frame unavailable.";

        if (!_gameCaptureService.TryCaptureFrame(out var frame))
        {
            return false;
        }

        using (frame)
        {
            return TryCaptureSnapshot(frame, out snapshot, out failureMessage);
        }
    }

    public bool TryCaptureSnapshot(Mat frame, out GameStageStateSnapshot snapshot)
    {
        return TryCaptureSnapshot(frame, out snapshot, out _);
    }

    public bool TryCaptureSnapshot(Mat frame, out GameStageStateSnapshot snapshot, out string failureMessage)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (frame.Empty())
        {
            snapshot = new GameStageStateSnapshot();
            failureMessage = "Source frame is empty.";
            return false;
        }

        var captureRegions = GetCaptureRegions(frame.Width, frame.Height);

        using var goldCaptureRegion = new Mat(frame, captureRegions.GoldRegion);
        using var roundCaptureRegion = new Mat(frame, captureRegions.RoundRegion);

        var hasGold = _gameTargetOcrService.TryReadGold(goldCaptureRegion, frame.Width, frame.Height, out var gold);
        var hasRound = _gameTargetOcrService.TryReadRound(roundCaptureRegion, frame.Width, frame.Height, out var round);

        snapshot = new GameStageStateSnapshot
        {
            IsInLevel = DetectIsInLevel(frame),
            Gold = hasGold ? gold : null,
            Round = hasRound ? round : null,
            RightUpgradePanel = ReadRightUpgradePanelState(frame),
            LeftUpgradePanel = ReadLeftUpgradePanelState(frame),
            IsPlacingMonkey = DetectIsPlacingMonkey(frame),
            CanPlaceHero = DetectCanPlaceHero(frame),
            StageTarget = string.Empty
        };

        failureMessage = hasGold || hasRound
            ? string.Empty
            : "Gold/Round OCR failed.";

        return hasGold || hasRound;
    }

    private static bool? DetectIsInLevel(Mat frame)
    {
        _ = frame;
        return null;
    }

    private static GameStageUpgradePanelState ReadRightUpgradePanelState(Mat frame)
    {
        _ = frame;
        return GameStageUpgradePanelState.Empty;
    }

    private static GameStageUpgradePanelState ReadLeftUpgradePanelState(Mat frame)
    {
        _ = frame;
        return GameStageUpgradePanelState.Empty;
    }

    private static bool? DetectIsPlacingMonkey(Mat frame)
    {
        _ = frame;
        return null;
    }

    private static bool? DetectCanPlaceHero(Mat frame)
    {
        _ = frame;
        return null;
    }

    private static OpenCvRect ScaleReferenceRect(OpenCvRect referenceRect, int actualWidth, int actualHeight)
    {
        var x = (int)Math.Round(referenceRect.X / (double)Reference1080p.Width * actualWidth);
        var y = (int)Math.Round(referenceRect.Y / (double)Reference1080p.Height * actualHeight);
        var right = (int)Math.Round(referenceRect.Right / (double)Reference1080p.Width * actualWidth);
        var bottom = (int)Math.Round(referenceRect.Bottom / (double)Reference1080p.Height * actualHeight);

        x = Math.Clamp(x, 0, Math.Max(0, actualWidth - 1));
        y = Math.Clamp(y, 0, Math.Max(0, actualHeight - 1));
        right = Math.Clamp(right, x + 1, actualWidth);
        bottom = Math.Clamp(bottom, y + 1, actualHeight);

        return new OpenCvRect(x, y, right - x, bottom - y);
    }
}
