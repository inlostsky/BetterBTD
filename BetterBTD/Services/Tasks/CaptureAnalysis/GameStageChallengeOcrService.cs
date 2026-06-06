using System.Globalization;
using System.IO;
using System.Diagnostics;
using System.Text;
using BetterBTD.Core.ScriptExecution;
using BetterBTD.Models;
using BetterBTD.Models.ScriptExecution;
using OpenCvSharp;
using OpenCvRect = OpenCvSharp.Rect;

namespace BetterBTD.Services.Tasks.CaptureAnalysis;

public sealed class GameStageChallengeOcrService
{
    private static readonly Lazy<GameStageChallengeOcrService> InstanceHolder = new(() => new GameStageChallengeOcrService());
    private static readonly double[] GoldThresholds = [0.90d, 0.84d, 0.78d];
    private static readonly double[] RoundThresholds = [0.90d, 0.84d, 0.78d];
    private static readonly TemplateMatchOptions DigitMatchOptions = TemplateMatchOptions.SqDiffNormedMasked;
    private const double GoldOneScoreDelta = 0.04d;
    private const int GoldOneTopTolerance = 2;
    private const double NmsIouThreshold = 0.30d;
    private const double SameGlyphCenterDistanceRatio = 0.35d;

    private readonly object _syncRoot = new();
    private readonly TemplateMatchService _templateMatchService;

    private DigitTemplateRepository? _digitRepository;

    private GameStageChallengeOcrService()
    {
        _templateMatchService = TemplateMatchService.Instance;
    }

    public static GameStageChallengeOcrService Instance => InstanceHolder.Value;

    public bool IsAvailable => TryEnsureDigitRepository(out _);

    public bool TryReadGold(Mat captureRegion, int frameWidth, int frameHeight, out int gold)
    {
        ArgumentNullException.ThrowIfNull(captureRegion);
        gold = 0;
        var startedAt = DateTimeOffset.UtcNow;

        if (captureRegion.Empty())
        {
            ScriptExecutionRuntimeDiagnostics.Warning(
                ScriptExecutionRuntimeLogCategory.Ocr,
                "Gold OCR skipped because capture region is empty.",
                aggregationKey: "ocr:gold",
                replaceExisting: true);
            return false;
        }

        GameOcrSupport.ValidateFrameSize(frameWidth, frameHeight);

        if (!TryEnsureDigitRepository(out var repository))
        {
            ScriptExecutionRuntimeDiagnostics.Warning(
                ScriptExecutionRuntimeLogCategory.Ocr,
                "Gold OCR skipped because digit templates are unavailable.",
                aggregationKey: "ocr:gold",
                replaceExisting: true);
            return false;
        }

        var templates = repository.GetDigitTemplates(OcrValueType.Gold, frameWidth, frameHeight);

        if (!TryRecognizeDigits(captureRegion, templates, GoldThresholds, out var text))
        {
            ScriptExecutionRuntimeDiagnostics.Warning(
                ScriptExecutionRuntimeLogCategory.Ocr,
                $"Gold OCR failed | size={captureRegion.Width}x{captureRegion.Height} | elapsed={(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds:F0} ms.",
                aggregationKey: "ocr:gold",
                replaceExisting: true);
            return false;
        }

        var parsed = int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out gold);
        ScriptExecutionRuntimeDiagnostics.Info(
            ScriptExecutionRuntimeLogCategory.Ocr,
            parsed
                ? $"Gold OCR succeeded | value={gold} | text='{text}' | elapsed={(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds:F0} ms."
                : $"Gold OCR parsed invalid text '{text}' | elapsed={(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds:F0} ms.",
            aggregationKey: "ocr:gold",
            replaceExisting: true);
        return parsed;
    }

    public bool TryReadRound(Mat captureRegion, int frameWidth, int frameHeight, out int round)
    {
        ArgumentNullException.ThrowIfNull(captureRegion);
        round = 0;
        var startedAt = DateTimeOffset.UtcNow;

        if (captureRegion.Empty())
        {
            ScriptExecutionRuntimeDiagnostics.Warning(
                ScriptExecutionRuntimeLogCategory.Ocr,
                "Round OCR skipped because capture region is empty.",
                aggregationKey: "ocr:round",
                replaceExisting: true);
            return false;
        }

        GameOcrSupport.ValidateFrameSize(frameWidth, frameHeight);

        if (!TryEnsureDigitRepository(out var repository))
        {
            ScriptExecutionRuntimeDiagnostics.Warning(
                ScriptExecutionRuntimeLogCategory.Ocr,
                "Round OCR skipped because digit templates are unavailable.",
                aggregationKey: "ocr:round",
                replaceExisting: true);
            return false;
        }

        var digitTemplates = repository.GetDigitTemplates(OcrValueType.Round, frameWidth, frameHeight);
        var slashTemplate = repository.GetSlashTemplate(frameWidth, frameHeight);

        if (!TryRecognizeRoundDigits(captureRegion, digitTemplates, slashTemplate, out var text))
        {
            ScriptExecutionRuntimeDiagnostics.Warning(
                ScriptExecutionRuntimeLogCategory.Ocr,
                $"Round OCR failed | size={captureRegion.Width}x{captureRegion.Height} | elapsed={(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds:F0} ms.",
                aggregationKey: "ocr:round",
                replaceExisting: true);
            return false;
        }

        var parsed = int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out round);
        ScriptExecutionRuntimeDiagnostics.Info(
            ScriptExecutionRuntimeLogCategory.Ocr,
            parsed
                ? $"Round OCR succeeded | value={round} | text='{text}' | elapsed={(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds:F0} ms."
                : $"Round OCR parsed invalid text '{text}' | elapsed={(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds:F0} ms.",
            aggregationKey: "ocr:round",
            replaceExisting: true);
        return parsed;
    }

    private bool TryRecognizeDigits(
        Mat captureRegion,
        IReadOnlyList<PreparedTemplate> templates,
        IReadOnlyList<double> thresholds,
        out string text)
    {
        text = string.Empty;
        ThresholdRecognitionResult? bestResult = null;

        foreach (var threshold in thresholds)
        {
            var candidates = FilterGoldCandidates(CollectCandidates(captureRegion, templates, threshold), threshold);
            var recognizedText = candidates.Count == 0 ? string.Empty : BuildDigitText(candidates);
            var result = new ThresholdRecognitionResult(threshold, candidates, recognizedText);
            //DebugWriteThresholdResult("[GameStageOCR] gold", result);

            if (string.IsNullOrEmpty(recognizedText))
            {
                continue;
            }

            if (bestResult is null || result.IsBetterThan(bestResult))
            {
                bestResult = result;
            }
        }

        if (bestResult is null)
        {
            return false;
        }

        text = bestResult.Text;
        //Debug.WriteLine($"[GameStageOCR] gold selected threshold={bestResult.Threshold:F3} text={text}");
        return true;
    }

    private bool TryRecognizeRoundDigits(
        Mat captureRegion,
        IReadOnlyList<PreparedTemplate> digitTemplates,
        PreparedTemplate slashTemplate,
        out string text)
    {
        text = string.Empty;
        ThresholdRecognitionResult? bestResult = null;

        foreach (var threshold in RoundThresholds)
        {
            var digitCandidates = CollectCandidates(captureRegion, digitTemplates, threshold);
            if (digitCandidates.Count == 0)
            {
                //DebugWriteThresholdResult("[GameStageOCR] round", new ThresholdRecognitionResult(threshold, [], string.Empty));
                continue;
            }

            double? slashCenterX = TryFindSlashCandidate(captureRegion, slashTemplate, threshold, out var detectedSlashCandidate)
                ? detectedSlashCandidate!.CenterX
                : null;

            var filteredCandidates = FilterRoundCandidates(digitCandidates, slashCenterX);
            var recognizedText = BuildDigitText(filteredCandidates);
            var result = new ThresholdRecognitionResult(threshold, filteredCandidates, recognizedText);
            //DebugWriteThresholdResult("[GameStageOCR] round", result);

            if (string.IsNullOrEmpty(recognizedText))
            {
                continue;
            }

            if (bestResult is null || result.IsBetterThan(bestResult))
            {
                bestResult = result;
            }
        }

        if (bestResult is null)
        {
            return false;
        }

        text = bestResult.Text;
        //Debug.WriteLine($"[GameStageOCR] round selected threshold={bestResult.Threshold:F3} text={text}");
        return true;
    }

    private List<OcrCandidate> CollectCandidates(Mat captureRegion, IReadOnlyList<PreparedTemplate> templates, double threshold)
    {
        var candidates = new List<OcrCandidate>();
        //Debug.WriteLine(
        //    $"[GameStageOCR] CollectCandidates threshold={threshold:F3} capture={captureRegion.Width}x{captureRegion.Height} templates={templates.Count}");

        foreach (var template in templates)
        {
            if (captureRegion.Width < template.Width || captureRegion.Height < template.Height)
            {
                continue;
            }

            using var matchResult = _templateMatchService.CreateMatchResult(
                captureRegion,
                template.Image,
                template.Mask,
                DigitMatchOptions);
            var templateCandidates = FindLocalMaximaCandidates(matchResult, template.Symbol, template.Width, template.Height, threshold);
            //DebugWriteCandidates($"[GameStageOCR] template={template.Symbol} raw", templateCandidates);
            candidates.AddRange(templateCandidates);
        }

        //DebugWriteCandidates("[GameStageOCR] raw combined", candidates);
        var suppressedCandidates = ApplyNonMaximumSuppression(candidates);
        //DebugWriteCandidates("[GameStageOCR] nms", suppressedCandidates);
        return suppressedCandidates;
    }

    private bool TryFindSlashCandidate(Mat captureRegion, PreparedTemplate slashTemplate, double threshold, out OcrCandidate? bestMatch)
    {
        bestMatch = null;

        var candidates = CollectCandidates(captureRegion, [slashTemplate], threshold);
        if (candidates.Count == 0)
        {
            return false;
        }

        bestMatch = candidates.OrderByDescending(x => x.Score).First();
        return true;
    }

    private static List<OcrCandidate> FilterGoldCandidates(IReadOnlyList<OcrCandidate> candidates, double threshold)
    {
        var ordered = OrderCandidates(candidates);
        if (ordered.Count == 0)
        {
            return ordered;
        }

        var nonOneCandidates = ordered
            .Where(x => !string.Equals(x.Symbol, "1", StringComparison.Ordinal))
            .ToList();

        if (nonOneCandidates.Count == 0)
        {
            return ordered;
        }

        var oneScoreThreshold = Math.Max(
            threshold,
            nonOneCandidates.Average(x => x.Score) - GoldOneScoreDelta);

        var topYValues = nonOneCandidates
            .Select(x => x.Bounds.Y)
            .OrderBy(x => x)
            .ToArray();
        var dominantTopY = topYValues[topYValues.Length / 2];

        return ordered
            .Where(candidate =>
                !string.Equals(candidate.Symbol, "1", StringComparison.Ordinal) ||
                (candidate.Score >= oneScoreThreshold &&
                 Math.Abs(candidate.Bounds.Y - dominantTopY) <= GoldOneTopTolerance))
            .ToList();
    }

    private static List<OcrCandidate> FilterRoundCandidates(IReadOnlyList<OcrCandidate> candidates, double? slashCenterX)
    {
        var ordered = OrderCandidates(candidates);
        if (ordered.Count == 0)
        {
            return ordered;
        }

        if (slashCenterX.HasValue)
        {
            var averageWidth = ordered.Average(x => x.Bounds.Width);
            if (slashCenterX.Value > ordered[0].CenterX && slashCenterX.Value < ordered[^1].CenterX + averageWidth)
            {
                return ordered
                    .Where(x => x.CenterX < slashCenterX.Value)
                    .ToList();
            }
        }

        if (ordered.Count <= 1)
        {
            return ordered;
        }

        var widestGap = double.MinValue;
        var splitIndex = -1;
        var meanWidth = ordered.Average(x => x.Bounds.Width);
        for (var index = 0; index < ordered.Count - 1; index++)
        {
            var gap = ordered[index + 1].Bounds.X - ordered[index].Bounds.Right;
            if (gap > widestGap)
            {
                widestGap = gap;
                splitIndex = index;
            }
        }

        if (widestGap >= Math.Max(2d, meanWidth * 0.55d) && splitIndex >= 0)
        {
            return ordered.Take(splitIndex + 1).ToList();
        }

        return ordered;
    }

    private static string BuildDigitText(IReadOnlyList<OcrCandidate> candidates)
    {
        var ordered = OrderCandidates(candidates);
        if (ordered.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        OcrCandidate? previous = null;
        foreach (var candidate in ordered)
        {
            builder.Append(candidate.Symbol);
            previous = candidate;
        }

        //DebugWriteCandidates("[GameStageOCR] ordered", ordered);
        //Debug.WriteLine($"[GameStageOCR] text={builder}");
        return builder.ToString();
    }

    private static List<OcrCandidate> OrderCandidates(IReadOnlyList<OcrCandidate> candidates)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        var centerYValues = candidates
            .Select(x => x.CenterY)
            .OrderBy(x => x)
            .ToArray();

        var medianCenterY = centerYValues[centerYValues.Length / 2];
        var averageHeight = candidates.Average(x => x.Bounds.Height);
        var verticalTolerance = Math.Max(4d, averageHeight * 0.45d);

        return candidates
            .Where(x => Math.Abs(x.CenterY - medianCenterY) <= verticalTolerance)
            .OrderBy(x => x.Bounds.X)
            .ThenByDescending(x => x.Score)
            .ToList();
    }

    private static List<OcrCandidate> ApplyNonMaximumSuppression(IReadOnlyList<OcrCandidate> candidates)
    {
        var kept = new List<OcrCandidate>();

        foreach (var candidate in candidates.OrderByDescending(x => x.Score))
        {
            if (kept.Any(existing => ShouldSuppress(existing, candidate)))
            {
                continue;
            }

            kept.Add(candidate);
        }

        return kept
            .OrderBy(x => x.Bounds.X)
            .ThenByDescending(x => x.Score)
            .ToList();
    }

    private static bool ShouldSuppress(OcrCandidate left, OcrCandidate right)
    {
        var overlap = GetIntersection(left.Bounds, right.Bounds);
        if (overlap.Width > 0 && overlap.Height > 0)
        {
            var overlapArea = overlap.Width * overlap.Height;
            var leftArea = left.Bounds.Width * left.Bounds.Height;
            var rightArea = right.Bounds.Width * right.Bounds.Height;
            var unionArea = leftArea + rightArea - overlapArea;
            var overlapRatio = unionArea <= 0 ? 0d : overlapArea / (double)unionArea;
            if (overlapRatio >= NmsIouThreshold)
            {
                return true;
            }
        }

        var deltaX = Math.Abs(left.CenterX - right.CenterX);
        var deltaY = Math.Abs(left.CenterY - right.CenterY);
        return deltaX <= Math.Min(left.Bounds.Width, right.Bounds.Width) * SameGlyphCenterDistanceRatio &&
               deltaY <= Math.Min(left.Bounds.Height, right.Bounds.Height) * SameGlyphCenterDistanceRatio;
    }

    private static OpenCvRect GetIntersection(OpenCvRect left, OpenCvRect right)
    {
        var x = Math.Max(left.X, right.X);
        var y = Math.Max(left.Y, right.Y);
        var rightEdge = Math.Min(left.Right, right.Right);
        var bottomEdge = Math.Min(left.Bottom, right.Bottom);
        var width = Math.Max(0, rightEdge - x);
        var height = Math.Max(0, bottomEdge - y);
        return new OpenCvRect(x, y, width, height);
    }

    private static List<OcrCandidate> FindLocalMaximaCandidates(
        Mat matchResult,
        string symbol,
        int templateWidth,
        int templateHeight,
        double threshold)
    {
        var candidates = new List<OcrCandidate>();
        for (var y = 0; y < matchResult.Rows; y++)
        {
            for (var x = 0; x < matchResult.Cols; x++)
            {
                var score = matchResult.At<float>(y, x);
                if (!double.IsFinite(score) || score < threshold)
                {
                    continue;
                }

                if (!IsLocalMaximum(matchResult, x, y, score))
                {
                    continue;
                }

                candidates.Add(new OcrCandidate(symbol, new OpenCvRect(x, y, templateWidth, templateHeight), score));
            }
        }

        return candidates;
    }

    private static bool IsLocalMaximum(Mat matchResult, int x, int y, float score)
    {
        for (var offsetY = -1; offsetY <= 1; offsetY++)
        {
            var neighborY = y + offsetY;
            if (neighborY < 0 || neighborY >= matchResult.Rows)
            {
                continue;
            }

            for (var offsetX = -1; offsetX <= 1; offsetX++)
            {
                if (offsetX == 0 && offsetY == 0)
                {
                    continue;
                }

                var neighborX = x + offsetX;
                if (neighborX < 0 || neighborX >= matchResult.Cols)
                {
                    continue;
                }

                var neighborScore = matchResult.At<float>(neighborY, neighborX);
                if (neighborScore > score)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static void DebugWriteCandidates(string header, IReadOnlyList<OcrCandidate> candidates)
    {
        Debug.WriteLine($"{header} count={candidates.Count}");
        foreach (var candidate in candidates.OrderBy(x => x.Bounds.X).ThenByDescending(x => x.Score))
        {
            Debug.WriteLine(
                $"{header} {candidate.Symbol} x={candidate.Bounds.X} y={candidate.Bounds.Y} w={candidate.Bounds.Width} h={candidate.Bounds.Height} score={candidate.Score:F4}");
        }
    }

    private static void DebugWriteThresholdResult(string header, ThresholdRecognitionResult result)
    {
        Debug.WriteLine(
            $"{header} threshold={result.Threshold:F3} count={result.Candidates.Count} text={result.Text}");
    }

    private bool TryEnsureDigitRepository(out DigitTemplateRepository repository)
    {
        lock (_syncRoot)
        {
            if (_digitRepository is not null)
            {
                repository = _digitRepository;
                return true;
            }

            var assetRootPath = GameOcrSupport.BuildDigitAssetRootPath();
            if (!Directory.Exists(assetRootPath))
            {
                repository = null!;
                return false;
            }

            try
            {
                _digitRepository = new DigitTemplateRepository(assetRootPath);
                repository = _digitRepository;
                return true;
            }
            catch
            {
                repository = null!;
                return false;
            }
        }
    }

    private sealed record OcrCandidate(string Symbol, OpenCvRect Bounds, double Score)
    {
        public double CenterX => Bounds.X + Bounds.Width / 2d;

        public double CenterY => Bounds.Y + Bounds.Height / 2d;
    }

    private sealed record ThresholdRecognitionResult(double Threshold, IReadOnlyList<OcrCandidate> Candidates, string Text)
    {
        public bool IsBetterThan(ThresholdRecognitionResult other)
        {
            if (Text.Length != other.Text.Length)
            {
                return Text.Length > other.Text.Length;
            }

            var averageScore = Candidates.Count == 0 ? double.NegativeInfinity : Candidates.Average(x => x.Score);
            var otherAverageScore = other.Candidates.Count == 0 ? double.NegativeInfinity : other.Candidates.Average(x => x.Score);
            if (!double.Equals(averageScore, otherAverageScore))
            {
                return averageScore > otherAverageScore;
            }

            return Threshold < other.Threshold;
        }
    }
}
