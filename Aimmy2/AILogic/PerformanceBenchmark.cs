using System.Diagnostics;
using System.Globalization;
using System.Management;
using System.Text.RegularExpressions;

namespace Aimmy2.AILogic
{
    internal sealed record PerformanceBenchmarkProgress(
        int StepIndex,
        int TotalSteps,
        int ImageSize,
        string Status,
        double StepProgress = 0);

    internal sealed record PerformanceBenchmarkSizeResult(
        int ImageSize,
        double AverageFps,
        double MaxFps,
        double AverageCpuPercent,
        double PeakCpuPercent,
        double AverageGpuPercent,
        double PeakGpuPercent,
        bool GpuAvailable,
        int FrameCount)
    {
        public int SuggestedFpsLimit => PerformanceRecommendationBuilder.BuildFpsCap(AverageFps);
    }

    internal enum PerformanceRecommendationMode
    {
        FastestDetections,
        Quality,
        Balanced,
        HighSpeed,
        LowestResources
    }

    internal enum PerformanceGoal
    {
        FastestDetections,
        Balanced,
        LowestResources
    }

    internal sealed record PerformanceRecommendation(
        int SuggestedImageSize,
        int SuggestedFpsLimit,
        bool CanChangeImageSize,
        string Summary,
        string Label = "Balanced",
        PerformanceRecommendationMode Mode = PerformanceRecommendationMode.Balanced);

    internal sealed record PerformanceRecommendationSet(
        PerformanceRecommendation Quality,
        PerformanceRecommendation Balanced,
        PerformanceRecommendation HighSpeed,
        PerformanceRecommendation FastestDetections,
        PerformanceRecommendation LowestResources,
        PerformanceRecommendation Primary,
        bool HasLimitedHardwareWarning,
        string HardwareWarning);

    internal sealed record PerformanceBenchmarkReport(
        IReadOnlyList<PerformanceBenchmarkSizeResult> Results,
        PerformanceRecommendation Recommendation,
        bool IsFixedSizeModel,
        PerformanceRecommendationSet Recommendations,
        PerformanceGoal Goal = PerformanceGoal.Balanced);

    internal static class PerformanceRecommendationBuilder
    {
        private const double MinimumRelativeFpsForTradeoff = 0.70;
        private const double MinimumRelativeFpsForBalancedEfficiency = 0.85;
        private const double MeaningfulLoadDropPercent = 10;
        private const double MeaningfulLoadDropRatio = 0.25;
        private const double HighSpeedPlayableFps = 60;
        private const double LimitedHardwareAverageFps = 45;
        private const double LowResourceTargetGpuPercent = 15;
        private const double LowResourceTargetCpuPercent = 20;

        internal static PerformanceRecommendationSet BuildChoices(
            IReadOnlyList<PerformanceBenchmarkSizeResult> results,
            int currentImageSize,
            bool isFixedSizeModel,
            PerformanceGoal goal = PerformanceGoal.Balanced)
        {
            var validResults = results
                .Where(result => result.FrameCount > 0 && result.AverageFps > 0)
                .OrderBy(result => result.ImageSize)
                .ToList();

            if (validResults.Count == 0)
            {
                var noStable = new PerformanceRecommendation(
                    currentImageSize,
                    60,
                    false,
                    "No stable sample captured. Try again after the model settles.",
                    "Balanced",
                    PerformanceRecommendationMode.Balanced);

                return new PerformanceRecommendationSet(
                    noStable,
                    noStable,
                    noStable,
                    noStable,
                    noStable,
                    noStable,
                    true,
                    "No stable AI-loop sample was captured. Try again after the model settles.");
            }

            PerformanceBenchmarkSizeResult bestThroughput = FindBestThroughput(validResults);
            PerformanceBenchmarkSizeResult fastestResult = bestThroughput;
            PerformanceBenchmarkSizeResult qualityResult = ChooseQuality(validResults, bestThroughput);
            PerformanceBenchmarkSizeResult highSpeedResult = ChooseHighSpeed(validResults, bestThroughput);
            PerformanceBenchmarkSizeResult lowestResourceResult = ChooseLowestResources(validResults);
            PerformanceBenchmarkSizeResult balancedResult = ChooseBalanced(
                validResults,
                bestThroughput,
                qualityResult,
                highSpeedResult);

            if (isFixedSizeModel)
            {
                var fixedResult = validResults.FirstOrDefault(result => result.ImageSize == currentImageSize) ?? qualityResult;
                qualityResult = fixedResult;
                balancedResult = fixedResult;
                highSpeedResult = fixedResult;
                fastestResult = fixedResult;
                lowestResourceResult = fixedResult;
            }

            string hardwareWarning = BuildHardwareWarning(validResults, bestThroughput);
            bool hasLimitedHardwareWarning = !string.IsNullOrWhiteSpace(hardwareWarning);

            var fastest = CreateRecommendation(
                fastestResult,
                currentImageSize,
                isFixedSizeModel,
                "Fastest Detections",
                PerformanceRecommendationMode.FastestDetections,
                DescribeFastest(fastestResult, isFixedSizeModel),
                PerformanceGoal.FastestDetections);

            var quality = CreateRecommendation(
                qualityResult,
                currentImageSize,
                isFixedSizeModel,
                "Quality",
                PerformanceRecommendationMode.Quality,
                DescribeQuality(qualityResult, currentImageSize, isFixedSizeModel));

            var balanced = CreateRecommendation(
                balancedResult,
                currentImageSize,
                isFixedSizeModel,
                "Balanced",
                PerformanceRecommendationMode.Balanced,
                DescribeBalanced(balancedResult, qualityResult, currentImageSize, isFixedSizeModel));

            var highSpeed = CreateRecommendation(
                highSpeedResult,
                currentImageSize,
                isFixedSizeModel,
                "High Speed",
                PerformanceRecommendationMode.HighSpeed,
                DescribeHighSpeed(highSpeedResult, qualityResult, isFixedSizeModel));

            var lowestResources = CreateRecommendation(
                lowestResourceResult,
                currentImageSize,
                isFixedSizeModel,
                "Lowest Resources",
                PerformanceRecommendationMode.LowestResources,
                DescribeLowestResources(lowestResourceResult, isFixedSizeModel),
                PerformanceGoal.LowestResources);

            var primary = goal switch
            {
                PerformanceGoal.FastestDetections => fastest,
                PerformanceGoal.LowestResources => lowestResources,
                _ => hasLimitedHardwareWarning ? lowestResources : balanced
            };

            return new PerformanceRecommendationSet(
                quality,
                balanced,
                highSpeed,
                fastest,
                lowestResources,
                primary,
                hasLimitedHardwareWarning,
                hardwareWarning);
        }

        private static PerformanceBenchmarkSizeResult FindBestThroughput(
            IReadOnlyList<PerformanceBenchmarkSizeResult> validResults)
        {
            return validResults
                .OrderByDescending(result => result.AverageFps)
                .ThenByDescending(result => result.ImageSize)
                .First();
        }

        private static PerformanceBenchmarkSizeResult ChooseQuality(
            IReadOnlyList<PerformanceBenchmarkSizeResult> validResults,
            PerformanceBenchmarkSizeResult bestThroughput)
        {
            double minimumUsefulFps = bestThroughput.AverageFps * MinimumRelativeFpsForTradeoff;
            var candidate = validResults
                .OrderByDescending(result => result.ImageSize)
                .FirstOrDefault(result => result.AverageFps >= minimumUsefulFps && !HasHighLoad(result)) ??
                validResults
                    .OrderByDescending(result => result.ImageSize)
                    .FirstOrDefault(result => result.AverageFps >= minimumUsefulFps) ??
                bestThroughput;

            if (HasHighLoad(candidate))
            {
                var lower = validResults
                    .Where(result =>
                        result.ImageSize < candidate.ImageSize &&
                        result.AverageFps >= minimumUsefulFps &&
                        !HasHighLoad(result))
                    .OrderByDescending(result => result.ImageSize)
                    .FirstOrDefault();
                if (lower != null)
                {
                    candidate = lower;
                }
            }

            return candidate;
        }

        private static PerformanceBenchmarkSizeResult ChooseBalanced(
            IReadOnlyList<PerformanceBenchmarkSizeResult> validResults,
            PerformanceBenchmarkSizeResult bestThroughput,
            PerformanceBenchmarkSizeResult quality,
            PerformanceBenchmarkSizeResult highSpeed)
        {
            double minimumUsefulFps = bestThroughput.AverageFps * MinimumRelativeFpsForTradeoff;
            bool hasDistinctHighSpeed = highSpeed.ImageSize < quality.ImageSize;

            if (HasHighLoad(quality))
            {
                var lowerLoad = validResults
                    .Where(result =>
                        result.ImageSize < quality.ImageSize &&
                        (!hasDistinctHighSpeed || result.ImageSize > highSpeed.ImageSize) &&
                        result.AverageFps >= minimumUsefulFps &&
                        !HasHighLoad(result))
                    .OrderByDescending(result => result.ImageSize)
                    .FirstOrDefault();

                if (lowerLoad != null)
                    return lowerLoad;
            }

            double minimumEfficiencyFps = quality.AverageFps * MinimumRelativeFpsForBalancedEfficiency;
            foreach (var lower in validResults
                         .Where(result => result.ImageSize < quality.ImageSize)
                         .Where(result => !hasDistinctHighSpeed || result.ImageSize > highSpeed.ImageSize)
                         .OrderByDescending(result => result.ImageSize))
            {
                if (lower.AverageFps < minimumUsefulFps ||
                    lower.AverageFps < minimumEfficiencyFps ||
                    HasHighLoad(lower))
                {
                    continue;
                }

                if (HasMeaningfulEfficiencyGain(quality, lower))
                    return lower;
            }

            if (hasDistinctHighSpeed &&
                highSpeed.AverageFps >= minimumUsefulFps &&
                highSpeed.AverageFps >= minimumEfficiencyFps &&
                HasMeaningfulEfficiencyGain(quality, highSpeed))
            {
                var middle = ChooseMiddleFallback(validResults, quality, highSpeed, minimumUsefulFps);
                if (middle != null)
                    return middle;
            }

            return quality;
        }

        private static PerformanceBenchmarkSizeResult? ChooseMiddleFallback(
            IReadOnlyList<PerformanceBenchmarkSizeResult> validResults,
            PerformanceBenchmarkSizeResult quality,
            PerformanceBenchmarkSizeResult highSpeed,
            double minimumUsefulFps)
        {
            double midpoint = Math.Sqrt(quality.ImageSize * highSpeed.ImageSize);
            return validResults
                .Where(result =>
                    result.ImageSize > highSpeed.ImageSize &&
                    result.ImageSize < quality.ImageSize &&
                    result.AverageFps >= minimumUsefulFps &&
                    !HasHighLoad(result))
                .OrderBy(result => Math.Abs(result.ImageSize - midpoint))
                .ThenByDescending(result => result.AverageFps)
                .FirstOrDefault();
        }

        private static PerformanceBenchmarkSizeResult ChooseHighSpeed(
            IReadOnlyList<PerformanceBenchmarkSizeResult> validResults,
            PerformanceBenchmarkSizeResult bestThroughput)
        {
            var preferred = validResults.Where(result => !HasHighLoad(result)).ToList();
            var source = preferred.Count > 0 ? preferred : validResults.ToList();

            var playable = source
                .Where(result => result.AverageFps >= HighSpeedPlayableFps && result.SuggestedFpsLimit >= 30)
                .ToList();

            if (playable.Count > 0)
            {
                return playable
                    .OrderBy(result => result.ImageSize)
                    .ThenByDescending(result => result.AverageFps)
                    .First();
            }

            return source
                .Where(result => result.AverageFps >= bestThroughput.AverageFps * MinimumRelativeFpsForTradeoff)
                .OrderBy(result => result.ImageSize)
                .ThenByDescending(result => result.AverageFps)
                .FirstOrDefault() ??
                source
                    .OrderByDescending(result => result.AverageFps)
                    .ThenBy(result => result.ImageSize)
                    .First();
        }

        private static PerformanceBenchmarkSizeResult ChooseLowestResources(
            IReadOnlyList<PerformanceBenchmarkSizeResult> validResults)
        {
            var preferred = validResults.Where(result => !HasHighLoad(result)).ToList();
            var source = preferred.Count > 0 ? preferred : validResults.ToList();

            var withCaps = source
                .Select(result => new
                {
                    Result = result,
                    Cap = BuildFpsCap(result, PerformanceGoal.LowestResources)
                })
                .ToList();

            var candidates = withCaps.Any(item => item.Cap >= 60)
                ? withCaps.Where(item => item.Cap >= 60)
                : withCaps.Any(item => item.Cap >= 30)
                    ? withCaps.Where(item => item.Cap >= 30)
                    : withCaps;

            return candidates
                .OrderBy(item => EstimateResourceScoreAtCap(item.Result, item.Cap))
                .ThenBy(item => item.Result.ImageSize)
                .ThenByDescending(item => item.Cap)
                .First()
                .Result;
        }

        internal static bool HasHighLoad(PerformanceBenchmarkSizeResult result)
        {
            bool cpuHigh = result.AverageCpuPercent >= 70 || result.PeakCpuPercent >= 90;
            bool gpuHigh = result.GpuAvailable && (result.AverageGpuPercent >= 85 || result.PeakGpuPercent >= 95);
            return cpuHigh || gpuHigh;
        }

        private static bool HasMeaningfulEfficiencyGain(
            PerformanceBenchmarkSizeResult quality,
            PerformanceBenchmarkSizeResult candidate)
        {
            bool fpsClose = candidate.AverageFps >= quality.AverageFps * MinimumRelativeFpsForBalancedEfficiency ||
                            candidate.SuggestedFpsLimit >= quality.SuggestedFpsLimit - 15;

            if (!fpsClose)
                return false;

            bool gpuDrop = quality.GpuAvailable &&
                           candidate.GpuAvailable &&
                           HasMeaningfulLoadDrop(quality.AverageGpuPercent, candidate.AverageGpuPercent);
            bool cpuDrop = HasMeaningfulLoadDrop(quality.AverageCpuPercent, candidate.AverageCpuPercent);

            return gpuDrop || cpuDrop;
        }

        private static double EstimateResourceScoreAtCap(PerformanceBenchmarkSizeResult result, int cap)
        {
            if (result.AverageFps <= 0)
                return double.MaxValue;

            double scale = Math.Clamp(cap / result.AverageFps, 0, 1);
            double estimatedCpu = result.AverageCpuPercent * scale;
            if (!result.GpuAvailable)
                return estimatedCpu;

            double estimatedGpu = result.AverageGpuPercent * scale;
            return estimatedGpu * 2 + estimatedCpu;
        }

        private static bool HasMeaningfulLoadDrop(double baseline, double candidate)
        {
            if (baseline < MeaningfulLoadDropPercent || candidate >= baseline)
                return false;

            double absoluteDrop = baseline - candidate;
            double relativeDrop = absoluteDrop / baseline;
            return absoluteDrop >= MeaningfulLoadDropPercent || relativeDrop >= MeaningfulLoadDropRatio;
        }

        private static PerformanceRecommendation CreateRecommendation(
            PerformanceBenchmarkSizeResult result,
            int currentImageSize,
            bool isFixedSizeModel,
            string label,
            PerformanceRecommendationMode mode,
            string summary,
            PerformanceGoal capGoal = PerformanceGoal.Balanced)
        {
            return new PerformanceRecommendation(
                result.ImageSize,
                BuildFpsCap(result, capGoal),
                !isFixedSizeModel && result.ImageSize != currentImageSize,
                summary,
                label,
                mode);
        }

        private static string DescribeFastest(
            PerformanceBenchmarkSizeResult result,
            bool isFixedSizeModel)
        {
            if (isFixedSizeModel)
                return "Fixed model locks image size. Higher FPS cap keeps detections as fresh as this model allows.";

            return "Highest measured AI-loop speed. Uses more CPU/GPU than lower resource picks.";
        }

        private static string DescribeQuality(
            PerformanceBenchmarkSizeResult result,
            int currentImageSize,
            bool isFixedSizeModel)
        {
            if (isFixedSizeModel)
                return "Fixed model locks image size. FPS cap cuts heat without changing detection detail.";

            return result.ImageSize == currentImageSize
                ? "Best detail-first pair from measured FPS and load."
                : "Higher image size keeps more detection detail, but costs more CPU/GPU.";
        }

        private static string DescribeBalanced(
            PerformanceBenchmarkSizeResult balanced,
            PerformanceBenchmarkSizeResult quality,
            int currentImageSize,
            bool isFixedSizeModel)
        {
            if (isFixedSizeModel)
                return "Fixed model locks image size. FPS cap cuts heat without changing detection detail.";

            if (balanced.ImageSize < quality.ImageSize)
                return "Keeps near-quality FPS with lower CPU/GPU load. Small or far targets can lose detail.";

            return balanced.ImageSize < currentImageSize
                ? "Lower image size cuts load while keeping measured FPS close."
                : "Best all-around pair from measured FPS, image size, and load.";
        }

        private static string DescribeHighSpeed(
            PerformanceBenchmarkSizeResult highSpeed,
            PerformanceBenchmarkSizeResult quality,
            bool isFixedSizeModel)
        {
            if (isFixedSizeModel)
                return "Fixed model locks image size. FPS cap still cuts heat.";

            return highSpeed.ImageSize < quality.ImageSize
                ? "Fastest/coolest practical pair. Detection detail can drop on small targets."
                : "Lower sizes were slower here. Using the best measured speed pair.";
        }

        private static string DescribeLowestResources(
            PerformanceBenchmarkSizeResult lowestResources,
            bool isFixedSizeModel)
        {
            if (isFixedSizeModel)
                return "Fixed model locks image size. Lower FPS cap gives the game more GPU/CPU room.";

            return BuildFpsCap(lowestResources, PerformanceGoal.LowestResources) >= 60
                ? "Targets lower GPU/CPU use while keeping detections at 60 FPS or better."
                : "Hardware could not hold 60 FPS cheaply, so this keeps detections above the poor range when possible.";
        }

        private static string BuildHardwareWarning(
            IReadOnlyList<PerformanceBenchmarkSizeResult> validResults,
            PerformanceBenchmarkSizeResult bestThroughput)
        {
            if (bestThroughput.AverageFps < LimitedHardwareAverageFps || BuildFpsCap(bestThroughput.AverageFps) < 30)
            {
                return "Aimmy may feel inconsistent on this hardware. Try Lowest Resources, lower image size, or a lighter model.";
            }

            if (bestThroughput.AverageFps < 90 && validResults.All(HasHighLoad))
            {
                return "CPU/GPU load is high across every tested size. Use Lowest Resources or a lighter model.";
            }

            var smallest = validResults.OrderBy(result => result.ImageSize).First();
            if (bestThroughput.AverageFps < 120 && HasHighLoad(smallest))
            {
                return "CPU/GPU load stayed high even at the smallest tested size. Use Lowest Resources or a lighter model.";
            }

            return string.Empty;
        }

        internal static int BuildFpsCap(double measuredFps)
        {
            if (measuredFps <= 0)
                return 60;

            int floor = measuredFps >= 40 ? 30 : 5;
            int target = Clamp(RoundToNearestFive(measuredFps * 0.75), floor, 240);
            int measuredCeiling = Math.Max(5, RoundDownToNearestFive(measuredFps));
            return Math.Min(target, measuredCeiling);
        }

        internal static int BuildFpsCap(PerformanceBenchmarkSizeResult result, PerformanceGoal goal)
        {
            return goal switch
            {
                PerformanceGoal.FastestDetections => BuildFastestFpsCap(result.AverageFps),
                PerformanceGoal.LowestResources => BuildLowestResourceFpsCap(result),
                _ => BuildFpsCap(result.AverageFps)
            };
        }

        private static int BuildFastestFpsCap(double measuredFps)
        {
            if (measuredFps <= 0)
                return 60;

            int floor = measuredFps >= 80 ? 60 : measuredFps >= 40 ? 30 : 5;
            int target = Clamp(RoundToNearestFive(measuredFps * 0.90), floor, 240);
            int measuredCeiling = Math.Max(5, RoundDownToNearestFive(measuredFps));
            return Math.Min(target, measuredCeiling);
        }

        private static int BuildLowestResourceFpsCap(PerformanceBenchmarkSizeResult result)
        {
            double measuredFps = result.AverageFps;
            if (measuredFps <= 0)
                return 30;

            int measuredCeiling = Math.Max(5, RoundDownToNearestFive(measuredFps));
            int floor = measuredFps >= 30 ? 30 : 5;
            int target = measuredFps >= 60
                ? 60
                : measuredFps >= 30
                    ? 30
                    : measuredCeiling;

            if (result.GpuAvailable && result.AverageGpuPercent > LowResourceTargetGpuPercent)
            {
                target = Math.Min(target, RoundDownToNearestFive(measuredFps * LowResourceTargetGpuPercent / result.AverageGpuPercent));
            }

            if (result.AverageCpuPercent > LowResourceTargetCpuPercent)
            {
                target = Math.Min(target, RoundDownToNearestFive(measuredFps * LowResourceTargetCpuPercent / result.AverageCpuPercent));
            }

            return Math.Min(Clamp(target, floor, 60), measuredCeiling);
        }

        private static int RoundToNearestFive(double value) => (int)(Math.Round(value / 5.0) * 5);
        private static int RoundDownToNearestFive(double value) => (int)(Math.Floor(value / 5.0) * 5);

        private static int Clamp(int value, int min, int max) => Math.Min(max, Math.Max(min, value));
    }

    internal sealed class ResourceUsageSampler : IDisposable
    {
        private static readonly Regex ProcessIdRegex = new(@"pid_(\d+)_", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly TimeSpan SampleInterval = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan MinimumCpuSampleInterval = TimeSpan.FromMilliseconds(100);

        private readonly Process _process;
        private readonly int _processorCount;
        private readonly Stopwatch _wallClock = Stopwatch.StartNew();
        private readonly List<double> _cpuSamples = new();
        private readonly List<double> _gpuSamples = new();
        private readonly object _sampleLock = new();
        private readonly TimeSpan _startCpuTime;
        private readonly long _startWallTicks;
        private TimeSpan _lastCpuTime;
        private long _lastWallTicks;
        private bool _gpuAvailable = true;
        private bool _gpuEngineRowsSeen;
        private CancellationTokenSource? _samplingCancellation;
        private Task? _samplingTask;

        internal ResourceUsageSampler()
        {
            _process = Process.GetCurrentProcess();
            _processorCount = Math.Max(1, Environment.ProcessorCount);
            _startCpuTime = _process.TotalProcessorTime;
            _startWallTicks = _wallClock.ElapsedTicks;
            _lastCpuTime = _startCpuTime;
            _lastWallTicks = _startWallTicks;
        }

        internal void Start()
        {
            if (_samplingTask != null)
                return;

            _samplingCancellation = new CancellationTokenSource();
            _samplingTask = Task.Run(() => SampleLoopAsync(_samplingCancellation.Token));
        }

        internal async Task<ResourceUsageSummary> FinishAsync()
        {
            if (_samplingCancellation != null)
            {
                _samplingCancellation.Cancel();
            }

            if (_samplingTask != null)
            {
                try
                {
                    await _samplingTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }

            SampleCpuInterval();
            return Finish();
        }

        private async Task SampleLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                SampleCpuInterval();
                SampleGpu();
                await Task.Delay(SampleInterval, cancellationToken).ConfigureAwait(false);
            }
        }

        private ResourceUsageSummary Finish()
        {
            _process.Refresh();
            double averageCpuPercent = CalculateCpuPercent(
                (_process.TotalProcessorTime - _startCpuTime).TotalMilliseconds,
                TicksToMilliseconds(_wallClock.ElapsedTicks - _startWallTicks));

            lock (_sampleLock)
            {
                double peakCpuPercent = Math.Max(averageCpuPercent, MaxOrZero(_cpuSamples));

                return new ResourceUsageSummary(
                    averageCpuPercent,
                    peakCpuPercent,
                    AverageOrZero(_gpuSamples),
                    MaxOrZero(_gpuSamples),
                    _gpuAvailable && _gpuEngineRowsSeen);
            }
        }

        private void SampleCpuInterval()
        {
            _process.Refresh();
            TimeSpan currentCpuTime = _process.TotalProcessorTime;
            long currentWallTicks = _wallClock.ElapsedTicks;

            double wallMilliseconds = TicksToMilliseconds(currentWallTicks - _lastWallTicks);
            if (wallMilliseconds < MinimumCpuSampleInterval.TotalMilliseconds)
                return;

            double cpuMilliseconds = (currentCpuTime - _lastCpuTime).TotalMilliseconds;
            double cpuPercent = CalculateCpuPercent(cpuMilliseconds, wallMilliseconds);

            lock (_sampleLock)
            {
                _cpuSamples.Add(cpuPercent);
            }

            _lastCpuTime = currentCpuTime;
            _lastWallTicks = currentWallTicks;
        }

        private void SampleGpu()
        {
            if (!_gpuAvailable)
                return;

            try
            {
                double utilization = 0;
                bool foundCurrentProcessEngine = false;
                using var searcher = new ManagementObjectSearcher(
                    "root\\CIMV2",
                    "SELECT Name, UtilizationPercentage FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine");

                foreach (ManagementObject engine in searcher.Get().Cast<ManagementObject>())
                {
                    string name = Convert.ToString(engine["Name"], CultureInfo.InvariantCulture) ?? string.Empty;
                    Match match = ProcessIdRegex.Match(name);
                    if (!match.Success || !int.TryParse(match.Groups[1].Value, out int pid) || pid != _process.Id)
                        continue;

                    foundCurrentProcessEngine = true;
                    utilization += Convert.ToDouble(engine["UtilizationPercentage"], CultureInfo.InvariantCulture);
                }

                if (foundCurrentProcessEngine)
                {
                    lock (_sampleLock)
                    {
                        _gpuEngineRowsSeen = true;
                        _gpuSamples.Add(Math.Clamp(utilization, 0, 100));
                    }
                }
            }
            catch
            {
                lock (_sampleLock)
                {
                    _gpuAvailable = false;
                    _gpuSamples.Clear();
                }
            }
        }

        private static double TicksToMilliseconds(long ticks) =>
            ticks * 1000.0 / Stopwatch.Frequency;

        private double CalculateCpuPercent(double cpuMilliseconds, double wallMilliseconds)
        {
            if (wallMilliseconds <= 0)
                return 0;

            return Math.Clamp((cpuMilliseconds / (wallMilliseconds * _processorCount)) * 100.0, 0, 100);
        }

        private static double AverageOrZero(List<double> values) =>
            values.Count == 0 ? 0 : values.Average();

        private static double MaxOrZero(List<double> values) =>
            values.Count == 0 ? 0 : values.Max();

        public void Dispose()
        {
            _samplingCancellation?.Cancel();
            try
            {
                _samplingTask?.Wait(TimeSpan.FromMilliseconds(250));
            }
            catch
            {
            }

            _samplingCancellation?.Dispose();
            _process.Dispose();
        }
    }

    internal sealed record ResourceUsageSummary(
        double AverageCpuPercent,
        double PeakCpuPercent,
        double AverageGpuPercent,
        double PeakGpuPercent,
        bool GpuAvailable);
}
