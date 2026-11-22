using AILogic;
using Aimmy2.Class;
using Class;
using InputLogic;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Newtonsoft.Json.Linq;
using Other;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Windows;
using Visuality;
using static AILogic.MathUtil;
using static Other.LogManager;

namespace Aimmy2.AILogic
{
    internal class AIManager : IDisposable
    {
        private static readonly TimeSpan BenchmarkWarmupMinimumDuration = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan BenchmarkWarmupMaximumDuration = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan BenchmarkSampleDuration = TimeSpan.FromSeconds(10);
        private const int BenchmarkWarmupMinimumInferences = 30;
        private const int BenchmarkStabilityWindow = 8;
        private const int DetectionHideDebounceMilliseconds = 20;

        #region Variables

        private int _currentImageSize;
        private readonly object _sizeLock = new object();
        private volatile bool _sizeChangePending = false;

        public void RequestSizeChange(int newSize)
        {
            lock (_sizeLock)
            {
                _sizeChangePending = true;
            }
        }

        // Dynamic properties instead of constants
        public int IMAGE_SIZE => _currentImageSize;

        private int NUM_DETECTIONS { get; set; } = 8400; // Will be set dynamically for dynamic models
        private bool IsDynamicModel { get; set; } = false;

        // Public static property to check if current loaded model is dynamic
        public static bool CurrentModelIsDynamic { get; private set; } = false;

        private int ModelFixedSize { get; set; } = 640; // Store the fixed size for non-dynamic models
        private int NUM_CLASSES { get; set; } = 1;

        private Dictionary<int, string> _modelClasses = new Dictionary<int, string>
        {
            { 0, "enemy" }
        };

        public Dictionary<int, string> ModelClasses => _modelClasses; // apparently this is better than making _modelClasses public

        public static event Action<Dictionary<int, string>>? ClassesUpdated;

        public static event Action<int>? ImageSizeUpdated;

        public static event Action<bool>? DynamicModelStatusChanged;

        private const int SAVE_FRAME_COOLDOWN_MS = 500;

        private Task _pendingSave = Task.CompletedTask;
        private DateTime lastSavedTime = DateTime.MinValue;
        private List<string>? _outputNames;

        private readonly RunOptions? _modeloptions;
        private InferenceSession? _onnxModel;
        private readonly string _modelPath;
        private bool _usingDirectML;
        private DirectMlGpuContext? _gpuContext;
        private bool _gpuCaptureDisabled;

        private Task? _aiLoopTask;
        private CancellationTokenSource? _aiLoopCancellation;
        private volatile bool _isAiLoopRunning;
        private volatile bool _benchmarkMode;
        private volatile bool _suppressOutputActions;
        private volatile bool _lastPredictionRanInference;
        private readonly SemaphoreSlim _inferenceGate = new(1, 1);
        private volatile bool _disposed;

        // Benchmarking
        private int _liveInferences;
        private int _zeroCopyGpuFrames;

        private int _liveSelections;
        private int _framesWithCandidates;
        private int _framesAwaitingMatch;
        private double _liveSeconds;
        private DetectedPlayerWindow? _visibleOverlay;
        private long _lastDisplayTimestamp;
        private bool _displayMissPending;

        public double AIConf = 0;

        // Tensor reuse (model inference)
        private FixedModelBuffers? _fixedModelBuffers;

        private float[]? _reusableFloatInput;
        private Float16[]? _reusableHalfInput;
        private bool _halfOutput;
        private List<NamedOnnxValue>? _reusableInputs;

        // Benchmarking
        private readonly Dictionary<string, BenchmarkData> _benchmarks = new();

        private readonly object _benchmarkLock = new();

        private readonly CaptureManager _captureManager = new();
        private bool _trackingActive;
        private bool _observing;
        private int _releaseVersion;
        private readonly KalmanPrediction kalmanPrediction = new();
        private readonly ShalloePredictionV2 shalloePrediction = new();
        private readonly WiseTheFoxPrediction wtfpredictionManager = new();
        private string? _motionMethod;

        private readonly TriggerButton _trigger = new(MouseManager.GetMouseActions);

        private void ResetMotion()
        {
            MouseManager.ResetMovement();
            kalmanPrediction.Reset();
            wtfpredictionManager.Reset();
            shalloePrediction.Reset();
        }

        private readonly StickyAimSelector _stickyAimSelector = new();
        public Task<bool> InitializationTask { get; }
        public bool IsLoaded => _onnxModel != null && _outputNames != null;

        #endregion Variables

        #region Benchmarking

        private class BenchmarkData
        {
            public double TotalTime { get; set; }
            public int CallCount { get; set; }
            public double MinTime { get; set; } = double.MaxValue;
            public double MaxTime { get; set; }
            public long OverFrameBudget { get; set; }
            public double AverageTime => CallCount > 0 ? (double)TotalTime / CallCount : 0;
        }

        private BenchmarkScope Benchmark(string name) => new(this, name);

        private readonly struct BenchmarkScope(AIManager manager, string name) : IDisposable
        {
            private readonly long _start = Stopwatch.GetTimestamp();

            public void Dispose() => manager.RecordBenchmark(name, Stopwatch.GetElapsedTime(_start).TotalMilliseconds);
        }

        private void RecordBenchmark(string name, double elapsedMs)
        {
            lock (_benchmarkLock)
            {
                if (!_benchmarks.TryGetValue(name, out var data))
                {
                    data = new BenchmarkData();
                    _benchmarks[name] = data;
                }

                data.TotalTime += elapsedMs;
                data.CallCount++;
                if (elapsedMs > 1000.0 / 60) data.OverFrameBudget++;
                data.MinTime = Math.Min(data.MinTime, elapsedMs);
                data.MaxTime = Math.Max(data.MaxTime, elapsedMs);
            }
        }

        private void PrintBenchmarks()
        {
            lock (_benchmarkLock)
            {
                var lines = new List<string>
                {
                    "=== AIManager Performance Benchmarks ==="
                };

                foreach (var kvp in _benchmarks.OrderBy(x => x.Key))
                {
                    var data = kvp.Value;
                    lines.Add($"{kvp.Key}: Avg={data.AverageTime:F2}ms, Min={data.MinTime:F2}ms, Max={data.MaxTime:F2}ms, Count={data.CallCount}, >16.7ms={data.OverFrameBudget}");
                }

                if (_captureManager.SlowestReadback is { } readback) lines.Add(readback);
                lines.Add(_captureManager.ReadbackCounts);
                lines.Add($"Zero-copy GPU frames: {_zeroCopyGpuFrames}/{_liveInferences}");
                lines.Add($"Frames with eligible detections: {_framesWithCandidates}/{_liveInferences}, with detections but no sticky match: {_framesAwaitingMatch}");
                lines.Add($"Active inference FPS: {(_liveSeconds > 0 ? _liveInferences / _liveSeconds : 0):F2}, selected frames: {_liveSelections}/{_liveInferences}");

                Log(LogLevel.Info, string.Join(Environment.NewLine, lines));
            }
        }

        #endregion Benchmarking

        public AIManager(string modelPath)
        {
            _modelPath = modelPath;

            // Initialize the cached image size
            _currentImageSize = AimSettings.ImageSize;

            _modeloptions = new RunOptions();

            // Attempt to load via DirectML (else fallback to CPU)
            InitializationTask = Task.Run(() => InitializeModel(modelPath));
        }

        #region Models

        private async Task<bool> InitializeModel(string modelPath)
        {
            using (Benchmark("ModelInitialization"))
            {
                try
                {
                    return await LoadModelAsync(modelPath, useDirectML: true);
                }
                catch (Exception ex)
                {
                    Log(LogLevel.Error, $"Error starting the model via DirectML: {ex.Message}\n\nFalling back to CPU, performance may be poor.", true);

                    try
                    {
                        return await LoadModelAsync(modelPath, useDirectML: false);
                    }
                    catch (Exception e)
                    {
                        Log(LogLevel.Error, $"Error starting the model via CPU: {e.Message}, you won't be able to aim assist at all.", true);
                        return false;
                    }
                }
            }
        }

        private Task<bool> LoadModelAsync(string modelPath, bool useDirectML)
        {
            try
            {
                if (useDirectML && AimSettings.ScreenCaptureMethod == "DirectX")
                {
                    try { _gpuContext = DirectMlGpuContext.Create(); }
                    catch (Exception ex) { DisableGpuCapture(ex.Message); }
                }
                OnnxModelLoadResult loadedModel;
                try { loadedModel = OnnxModelSessionFactory.Load(modelPath, useDirectML, _gpuContext); }
                catch (Exception ex) when (_gpuContext != null)
                {
                    DisableGpuCapture(ex.Message);
                    _gpuContext.Dispose();
                    _gpuContext = null;
                    loadedModel = OnnxModelSessionFactory.Load(modelPath, useDirectML);
                }
                _onnxModel = loadedModel.Session;
                _usingDirectML = useDirectML;
                _outputNames = loadedModel.OutputNames;

                // Validate the onnx model output shape (ensure model is OnnxV8)
                if (!ValidateOnnxShape())
                {
                    DisposeLoadedModel();
                    return Task.FromResult(false);
                }
                EnsureInferenceBuffers(_onnxModel);
            }
            catch
            {
                DisposeLoadedModel();
                throw;
            }

            // This method runs on the model-loading worker, before the live loop owns these resources.
            try
            {
                using (Benchmark("ModelWarmup"))
                {
                    for (int i = 0; i < 2; i++)
                    {
                        if (_fixedModelBuffers != null)
                            _fixedModelBuffers.Run(_onnxModel, _modeloptions!);
                        else
                        {
                            using var outputs = _onnxModel.Run(_reusableInputs, _outputNames, _modeloptions);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log(LogLevel.Warning, $"Model warmup failed: {ex.Message}");
            }

            try
            {
                using (Benchmark("CapturePreparation"))
                {
                    if (_fixedModelBuffers?.HasGpuInput == true && AimSettings.ScreenCaptureMethod == "DirectX")
                        _captureManager.InitializeDxgiDuplication();
                    else _captureManager.PrepareDirectX(IMAGE_SIZE);
                }
            }
            catch (Exception ex)
            {
                Log(LogLevel.Warning, $"DirectX preparation deferred to first capture: {ex.Message}");
            }

            if (_disposed) return Task.FromResult(false);
            _isAiLoopRunning = true;
            _aiLoopCancellation?.Dispose();
            _aiLoopCancellation = new CancellationTokenSource();
            CancellationToken loopCancellation = _aiLoopCancellation.Token;
            _aiLoopTask = Task.Factory.StartNew(
                () => AiLoopAsync(loopCancellation),
                loopCancellation,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default).Unwrap();
            return Task.FromResult(true);
        }

        private void DisposeLoadedModel()
        {
            try { _gpuContext?.Drain(); }
            finally
            {
                ReleaseInferenceBuffers();
                _onnxModel?.Dispose();
                _onnxModel = null;
                _gpuContext?.Dispose();
                _gpuContext = null;
                _outputNames = null;
            }
        }

        private void DisableGpuCapture(string reason)
        {
            if (_gpuCaptureDisabled) return;
            _gpuCaptureDisabled = true;
            Log(LogLevel.Warning, $"Zero-copy capture unavailable. Using CPU staging for this session: {reason}");
        }

        private bool ValidateOnnxShape()
        {
            if (_onnxModel != null)
            {
                var inputMetadata = _onnxModel.InputMetadata;
                var outputMetadata = _onnxModel.OutputMetadata;

                Log(LogLevel.Info, "=== Model Metadata ===");
                Log(LogLevel.Info, "Input Metadata:");

                bool isDynamic = false;
                int fixedInputSize = 0;

                foreach (var kvp in inputMetadata)
                {
                    string dimensionsStr = string.Join("x", kvp.Value.Dimensions);
                    Log(LogLevel.Info, $"  Name: {kvp.Key}, Dimensions: {dimensionsStr}");

                    // Check if model is dynamic (dimensions are -1)
                    if (kvp.Value.Dimensions.Any(d => d == -1))
                    {
                        isDynamic = true;
                    }
                    else if (kvp.Value.Dimensions.Length == 4)
                    {
                        // For fixed models, check if it's the expected format (1x3xHxW)
                        fixedInputSize = kvp.Value.Dimensions[2]; // Height should equal Width for square models
                    }
                }

                Log(LogLevel.Info, "Output Metadata:");
                foreach (var kvp in outputMetadata)
                {
                    string dimensionsStr = string.Join("x", kvp.Value.Dimensions);
                    Log(LogLevel.Info, $"  Name: {kvp.Key}, Dimensions: {dimensionsStr}");
                }

                IsDynamicModel = isDynamic;
                CurrentModelIsDynamic = isDynamic;

                if (IsDynamicModel)
                {
                    // For dynamic models, calculate NUM_DETECTIONS based on selected image size
                    NUM_DETECTIONS = CalculateNumDetections(IMAGE_SIZE);
                    LoadClasses();
                    ImageSizeUpdated?.Invoke(IMAGE_SIZE);
                    Log(LogLevel.Info, $"Loaded dynamic model - using selected image size {IMAGE_SIZE}x{IMAGE_SIZE} with {NUM_DETECTIONS} detections", true, 3000);
                }
                else
                {
                    // For fixed models, auto-adjust image size if needed
                    ModelFixedSize = fixedInputSize;

                    // List of supported sizes
                    var supportedSizes = new[] { "640", "512", "416", "320", "256", "160" };
                    var fixedSizeStr = fixedInputSize.ToString();

                    if (!supportedSizes.Contains(fixedSizeStr))
                    {
                        Log(LogLevel.Error,
                            $"Model requires unsupported size {fixedInputSize}x{fixedInputSize}. Supported sizes are: {string.Join(", ", supportedSizes)}",
                            true, 10000);
                        return false;
                    }

                    // Always calculate NUM_DETECTIONS based on the model's fixed size
                    NUM_DETECTIONS = CalculateNumDetections(fixedInputSize);
                    _currentImageSize = fixedInputSize;

                    if (fixedInputSize != AimSettings.ImageSize)
                    {
                        // Auto-adjust the image size to match the model
                        Log(LogLevel.Warning,
                            $"Fixed-size model expects {fixedInputSize}x{fixedInputSize}. Automatically adjusting Image Size setting.",
                            true, 3000);

                        AimSettings.ImageSize = fixedInputSize;

                        // Update the UI dropdown if it exists
                        Application.Current?.Dispatcher.BeginInvoke(() =>
                        {
                            try
                            {
                                // Find the MainWindow and update the dropdown
                                var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                                if (mainWindow?.SettingsMenuControlInstance != null)
                                {
                                    mainWindow.SettingsMenuControlInstance.UpdateImageSizeDropdown(fixedSizeStr);
                                }
                            }
                            catch { }
                        });
                    }

                    ImageSizeUpdated?.Invoke(fixedInputSize);
                    LoadClasses();

                    // For static models, validate the expected shape
                    var expectedShape = new int[] { 1, 4 + NUM_CLASSES, NUM_DETECTIONS };
                    if (!outputMetadata.Values.All(metadata => metadata.Dimensions.SequenceEqual(expectedShape)))
                    {
                        Log(LogLevel.Error,
                            $"Output shape does not match the expected shape of {string.Join("x", expectedShape)}.\nThis model will not work with Aimmy, please use an YOLOv8 model converted to ONNXv8.",
                            true, 10000);
                        return false;
                    }

                    Log(LogLevel.Info, $"Loaded fixed-size model: {fixedInputSize}x{fixedInputSize}", true, 2000);
                }

                // Notify UI about dynamic model status
                DynamicModelStatusChanged?.Invoke(IsDynamicModel);

                return true;
            }

            return false;
        }

        private void LoadClasses()
        {
            if (_onnxModel == null) return;
            _modelClasses.Clear();

            try
            {
                var metadata = _onnxModel.ModelMetadata;

                if (metadata != null &&
                    metadata.CustomMetadataMap.TryGetValue("names", out string? value) &&
                    !string.IsNullOrEmpty(value))
                {
                    JObject data = JObject.Parse(value);
                    if (data != null && data.Type == JTokenType.Object)
                    {
                        //int maxClassId = -1;
                        foreach (var item in data)
                        {
                            if (int.TryParse(item.Key, out int classId) && item.Value.Type == JTokenType.String)
                            {
                                _modelClasses[classId] = item.Value.ToString();
                            }
                        }
                        NUM_CLASSES = _modelClasses.Count > 0 ? _modelClasses.Keys.Max() + 1 : 1;
                        Log(LogLevel.Info, $"Loaded {_modelClasses.Count} class(es) from model metadata: {data.ToString(Newtonsoft.Json.Formatting.None)}", false);
                    }
                    else
                    {
                        Log(LogLevel.Error, "Model metadata 'names' field is not a valid JSON object.", true);
                    }
                }
                else
                {
                    Log(LogLevel.Error, "Model metadata does not contain 'names' field for classes.", true);
                }
                ClassesUpdated?.Invoke(new Dictionary<int, string>(_modelClasses));
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Error loading classes: {ex.Message}", true);
            }
        }

        #endregion Models

        #region AI

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ShouldPredict() =>
            AimProcessingDecisions.ShouldRunPrediction(
                AimSettings.ShowDetectedPlayer,
                AimSettings.ConstantAiTracking,
                InputBindingManager.IsHoldingBinding("Aim Keybind"),
                InputBindingManager.IsHoldingBinding("Second Aim Keybind"));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ShouldProcess() =>
            AimProcessingDecisions.ShouldProcessFrame(
                AimSettings.AimAssist,
                AimSettings.ShowDetectedPlayer,
                AimSettings.AutoTrigger);

        private async Task AiLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;
            }
            catch
            {
            }

            Stopwatch stopwatch = new();

            try
            {
                while (_isAiLoopRunning && !cancellationToken.IsCancellationRequested)
                {
                    bool sizeChangePending;
                    lock (_sizeLock)
                    {
                        sizeChangePending = _sizeChangePending;
                    }

                    if (_benchmarkMode)
                    {
                        _observing = false;
                        _trigger.Release();
                        HideDetection();
                        await Task.Delay(10, cancellationToken);
                        continue;
                    }

                    stopwatch.Restart();
                    long activeStart = 0;

                    try
                    {
                        if (sizeChangePending)
                        {
                            _trigger.Release();
                            HideDetection();
                            await Task.Delay(1, cancellationToken);
                            continue;
                        }

                        bool active = AimSettings.ConstantAiTracking ||
                            InputBindingManager.IsHoldingBinding("Aim Keybind") ||
                            InputBindingManager.IsHoldingBinding("Second Aim Keybind");
                        int release = InputBindingManager.AimReleaseVersion;
                        bool observing = ShouldProcess() && ShouldPredict();
                        if (active != _trackingActive || release != _releaseVersion || observing != _observing)
                        {
                            _trigger.Release();
                            _stickyAimSelector.Reset();
                            ResetMotion();
                            if (!active || !observing || release != _releaseVersion)
                                HideDetection();
                        }
                        _observing = observing;
                        _trackingActive = active;
                        _releaseVersion = release;

                        using (Benchmark("AILoopIteration"))
                        {
                            if (observing)
                            {
                                activeStart = Stopwatch.GetTimestamp();
                                Prediction? closestPrediction;
                                Prediction? displayDetection;
                                System.Drawing.Point? measured;
                                using (Benchmark("CaptureToSelection"))
                                {
                                    // The performance helper can own the gate for an entire benchmark.
                                    // Release input before yielding a live frame to it.
                                    if (!_inferenceGate.Wait(0))
                                    {
                                        _trigger.Release();
                                        HideDetection();
                                        continue;
                                    }
                                    try
                                    {
                                        if (_benchmarkMode || _suppressOutputActions)
                                        {
                                            _trigger.Release();
                                            continue;
                                        }
                                        closestPrediction = DetectAndSelect(out displayDetection, out measured);
                                        if (_lastPredictionRanInference) _liveInferences++;
                                    }
                                    finally
                                    {
                                        _inferenceGate.Release();
                                    }
                                }

                                if (_suppressOutputActions || release != InputBindingManager.AimReleaseVersion)
                                {
                                    _trigger.Release();
                                    HideDetection();
                                    continue;
                                }

                                if (measured is System.Drawing.Point aim)
                                {
                                    using (Benchmark("HandleAim"))
                                    {
                                        HandleAim(aim);
                                    }
                                }

                                using (Benchmark("AutoTrigger"))
                                {
                                    await UpdateTriggerAsync(closestPrediction, cancellationToken);
                                }
                                if (displayDetection != null)
                                    UpdateDetectionOverlay(displayDetection, cancellationToken);
                                else if (_visibleOverlay != null)
                                {
                                    // A capture without a new frame is different from an inference miss.
                                    _displayMissPending |= _lastPredictionRanInference;
                                    if (Stopwatch.GetElapsedTime(_lastDisplayTimestamp).TotalMilliseconds >= DetectionHideDebounceMilliseconds &&
                                        (_displayMissPending || Stopwatch.GetElapsedTime(_captureManager.LastCaptureTimestamp).TotalMilliseconds > 150))
                                        HideDetection();
                                }

                                if (closestPrediction == null)
                                {
                                    if (_lastPredictionRanInference && AimSettings.StickyAim && _stickyAimSelector.HasLock)
                                        MouseManager.PauseMovement(_captureManager.LastCaptureTimestamp);
                                    continue;
                                }

                                AIConf = closestPrediction.Confidence;
                                _liveSelections++;
                            }
                            else
                            {
                                _trigger.Release();
                                HideDetection();
                                await Task.Delay(1, cancellationToken);
                            }
                        }
                    }
                    finally
                    {
                        stopwatch.Stop();
                        await ApplyFpsLimitAsync(stopwatch, cancellationToken);
                        if (activeStart != 0) _liveSeconds += Stopwatch.GetElapsedTime(activeStart).TotalSeconds;
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _trigger.Release();
                Log(LogLevel.Error, $"AI loop stopped: {ex.Message}", true, 5000);
            }
            finally
            {
                _trigger.Release();
                HideDetection();
            }
        }

        private static async Task ApplyFpsLimitAsync(Stopwatch iterationStopwatch, CancellationToken cancellationToken)
        {
            int fpsLimit = AimSettings.AiFpsLimit;
            if (fpsLimit <= 0)
                return;

            double targetMilliseconds = 1000.0 / fpsLimit;
            double remainingMilliseconds = targetMilliseconds - iterationStopwatch.Elapsed.TotalMilliseconds;
            if (remainingMilliseconds > 1)
            {
                await Task.Delay((int)Math.Floor(remainingMilliseconds), cancellationToken);
            }
        }

        #region AI Loop Functions

        private Task UpdateTriggerAsync(Prediction? selected, CancellationToken token)
        {
            bool eligible = selected != null && !_suppressOutputActions && _releaseVersion == InputBindingManager.AimReleaseVersion &&
                AimProcessingDecisions.ShouldAttemptAutoTrigger(AimSettings.AutoTrigger,
                    InputBindingManager.IsHoldingBinding("Aim Keybind"),
                    InputBindingManager.IsHoldingBinding("Second Aim Keybind"), AimSettings.ConstantAiTracking);
            if (eligible && AimSettings.CursorCheck)
            {
                var cursor = WinAPICaller.GetCursorPosition();
                eligible = selected!.ScreenBounds.Contains(cursor.X, cursor.Y) &&
                    DisplayManager.IsPointInCurrentDisplay(new System.Windows.Point(cursor.X, cursor.Y));
            }

            return _trigger.UpdateAsync(eligible, eligible && AimSettings.SprayMode,
                eligible ? AimSettings.AutoTriggerDelayMilliseconds : 0, token);
        }

        private int _overlayUpdateInFlight;
        private int _overlayGeneration;

        private void HideDetection()
        {
            Interlocked.Increment(ref _overlayGeneration);
            _displayMissPending = false;
            var overlay = _visibleOverlay;
            if (overlay == null) return;
            _visibleOverlay = null;
            overlay.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Send, () =>
            {
                overlay.DetectedPlayerFocus.Opacity = 0;
                overlay.DetectedPlayerConfidence.Opacity = 0;
                overlay.DetectedTracers.Opacity = 0;
            });
        }

        private void UpdateOverlay(DetectedPlayerWindow overlay, Prediction closestPrediction, CancellationToken cancellationToken)
        {
            int expectedGeneration = Volatile.Read(ref _overlayGeneration);
            if (Interlocked.CompareExchange(ref _overlayUpdateInFlight, 1, 0) != 0) return;
            var bounds = closestPrediction.ScreenBounds;
            overlay.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, () =>
            {
                try
                {
                    if (Volatile.Read(ref _overlayGeneration) != expectedGeneration || cancellationToken.IsCancellationRequested)
                        return;
                    var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(overlay);

                    var width = bounds.Width / dpi.DpiScaleX;
                    var height = bounds.Height / dpi.DpiScaleY;
                    var centerX = (bounds.X - DisplayManager.ScreenLeft) / dpi.DpiScaleX + width / 2;
                    var centerY = (bounds.Y - DisplayManager.ScreenTop) / dpi.DpiScaleY;

                    bool showConfidence = AimSettings.ShowAiConfidence;
                    overlay.DetectedPlayerConfidence.Opacity = showConfidence ? 1 : 0;
                    if (showConfidence)
                    {
                        overlay.DetectedPlayerConfidence.Content = $"{closestPrediction.ClassName}: {Math.Round(closestPrediction.Confidence * 100, 2)}%";

                        var labelEstimatedHalfWidth = overlay.DetectedPlayerConfidence.ActualWidth / 2.0;
                        overlay.DetectedPlayerConfidence.Margin = new Thickness(
                            centerX - labelEstimatedHalfWidth,
                            centerY - overlay.DetectedPlayerConfidence.ActualHeight - 2, 0, 0);
                    }
                    var showTracers = AimSettings.ShowTracers;
                    overlay.DetectedTracers.Opacity = showTracers ? 1 : 0;
                    if (showTracers)
                    {
                        var tracerPosition = AimSettings.TracerPosition;
                        overlay.DetectedTracers.X1 = DisplayManager.ScreenWidth / (2.0 * dpi.DpiScaleX);
                        overlay.DetectedTracers.Y1 = tracerPosition switch
                        {
                            "Top" => 0,
                            "Middle" => DisplayManager.ScreenHeight / (2.0 * dpi.DpiScaleY),
                            _ => DisplayManager.ScreenHeight / dpi.DpiScaleY
                        };

                        var boxTop = centerY;
                        var boxBottom = centerY + height;
                        var boxHorizontalCenter = centerX;
                        var boxVerticalCenter = centerY + (height / 2.0);
                        var boxLeft = centerX - (width / 2.0);
                        var boxRight = centerX + (width / 2.0);

                        switch (tracerPosition)
                        {
                            case "Top":
                                overlay.DetectedTracers.X2 = boxHorizontalCenter;
                                overlay.DetectedTracers.Y2 = boxTop;
                                break;

                            case "Bottom":
                                overlay.DetectedTracers.X2 = boxHorizontalCenter;
                                overlay.DetectedTracers.Y2 = boxBottom;
                                break;

                            case "Middle":
                                var screenHorizontalCenter = DisplayManager.ScreenWidth / (2.0 * dpi.DpiScaleX);
                                if (boxHorizontalCenter < screenHorizontalCenter)
                                {
                                    // if the box is on the left half of the screen, aim for the right-middle of the box
                                    overlay.DetectedTracers.X2 = boxRight;
                                    overlay.DetectedTracers.Y2 = boxVerticalCenter;
                                }
                                else
                                {
                                    // if the box is on the right half, aim for the left-middle
                                    overlay.DetectedTracers.X2 = boxLeft;
                                    overlay.DetectedTracers.Y2 = boxVerticalCenter;
                                }
                                break;

                            default:
                                // default to the bottom-center if the setting is unrecognized
                                overlay.DetectedTracers.X2 = boxHorizontalCenter;
                                overlay.DetectedTracers.Y2 = boxBottom;
                                break;
                        }
                    }

                    overlay.Opacity = AimSettings.OverlayOpacity;

                    overlay.DetectedPlayerFocus.Opacity = 1;
                    overlay.DetectedPlayerFocus.Margin = new Thickness(
                        centerX - (width / 2.0), centerY, 0, 0);
                    overlay.DetectedPlayerFocus.Width = width;
                    overlay.DetectedPlayerFocus.Height = height;
                }
                finally
                {
                    Interlocked.Exchange(ref _overlayUpdateInFlight, 0);
                }
            });
        }

        private void UpdateDetectionOverlay(Prediction closestPrediction, CancellationToken cancellationToken)
        {
            var overlay = AimSettings.ShowDetectedPlayer ? Dictionary.DetectedPlayerOverlay : null;
            if (overlay != _visibleOverlay) HideDetection();
            if (overlay != null)
            {
                using (Benchmark("UpdateOverlay"))
                {
                    UpdateOverlay(overlay, closestPrediction, cancellationToken);
                    _visibleOverlay = overlay;
                    _lastDisplayTimestamp = Stopwatch.GetTimestamp();
                    _displayMissPending = false;
                }
            }
        }

        private void HandleAim(System.Drawing.Point measured)
        {
            if (_suppressOutputActions || _releaseVersion != InputBindingManager.AimReleaseVersion)
                return;

            if (AimSettings.AimAssist &&
                (AimSettings.ConstantAiTracking ||
                 AimSettings.AimAssist && InputBindingManager.IsHoldingBinding("Aim Keybind") ||
                 AimSettings.AimAssist && InputBindingManager.IsHoldingBinding("Second Aim Keybind")))
            {
                long time = _captureManager.LastCaptureTimestamp;
                string method = AimSettings.Predictions ? AimSettings.PredictionMethod : "";
                if (_motionMethod != method)
                    ResetMotion();
                _motionMethod = method;
                var correction = measured;
                if (method.Length != 0)
                {
                    using var predictionTiming = Benchmark(method);
                    correction = method switch
                    {
                        "Kalman Filter" => kalmanPrediction.Update(measured, time, AimSettings.KalmanLeadTime),
                        "Shall0e's Prediction" => shalloePrediction.Update(measured, time, AimSettings.ShalloeLeadMultiplier),
                        "wisethef0x's EMA Prediction" => wtfpredictionManager.Update(measured, time, AimSettings.WiseTheFoxLeadTime),
                        _ => measured
                    };
                }
                MouseManager.MoveCrosshair(correction.X, correction.Y, measured.X, measured.Y, time);
            }
        }

        private Prediction? DetectAndSelect(out Prediction? displayDetection, out System.Drawing.Point? measured, bool useMousePosition = true)
        {
            displayDetection = null;
            measured = null;
            if (_captureManager.HandlePendingDisplayChanges())
            {
                _stickyAimSelector.Reset();
                ResetMotion();
            }
            _lastPredictionRanInference = false;
            var detectionBox = CreateDetectionBox(useMousePosition);

            if (_onnxModel == null) return null;
            EnsureInferenceBuffers(_onnxModel);

            Bitmap? frame = null;
            Rectangle capturedBox = detectionBox;
            bool gpuFrame = false;
            using (Benchmark("ScreenGrab"))
            {
                if (AimSettings.ScreenCaptureMethod == "DirectX" && !_benchmarkMode && !AimSettings.CollectDataWhilePlaying)
                {
                    if (!_gpuCaptureDisabled && _gpuContext != null && _fixedModelBuffers?.HasGpuInput == true)
                    {
                        gpuFrame = _captureManager.TryCaptureToGpu(detectionBox, IMAGE_SIZE, _gpuContext,
                            _reusableHalfInput != null, out capturedBox);
                        if (!gpuFrame)
                        {
                            if (_captureManager.GpuCaptureFailure is not { } reason) return null;
                            DisableGpuCapture(reason);
                        }
                    }
                    if (!gpuFrame)
                    {
                        bool captured = _reusableHalfInput != null
                            ? _captureManager.TryCaptureToTensor(detectionBox, _reusableHalfInput, IMAGE_SIZE, out capturedBox)
                            : _captureManager.TryCaptureToTensor(detectionBox, _reusableFloatInput!, IMAGE_SIZE, out capturedBox);
                        if (!captured) return null;
                    }
                }
                else
                {
                    frame = _captureManager.BorrowFrame(detectionBox, out capturedBox, allowStaleCache: _benchmarkMode);
                    if (frame == null) return null;
                }
            }
            detectionBox = capturedBox;

            IDisposableReadOnlyCollection<DisposableNamedOnnxValue>? results = null;
            TensorBase? outputTensor = null;

            try
            {
                if (frame != null)
                {
                    using var timing = Benchmark(_reusableHalfInput != null ? "BitmapToHalfArray" : "BitmapToFloatArray");
                    if (_reusableHalfInput != null) BitmapToHalfArrayInPlace(frame, _reusableHalfInput, IMAGE_SIZE);
                    else BitmapToFloatArrayInPlace(frame, _reusableFloatInput!, IMAGE_SIZE);
                }

                using (Benchmark("ModelInference"))
                {
                    if (_fixedModelBuffers != null)
                    {
                        try { outputTensor = _fixedModelBuffers.Run(_onnxModel, _modeloptions!, gpuFrame); }
                        catch (Exception ex) when (gpuFrame)
                        {
                            DisableGpuCapture(ex.Message);
                            return null;
                        }
                        if (gpuFrame) _zeroCopyGpuFrames++;
                    }
                    else
                    {
                        results = _onnxModel.Run(_reusableInputs, _outputNames, _modeloptions);
                        outputTensor = _halfOutput ? results[0].AsTensor<Float16>() : results[0].AsTensor<float>();
                    }
                    _lastPredictionRanInference = true;
                }

                if (outputTensor == null)
                {
                    Log(LogLevel.Error, "Model inference returned null output tensor.", true, 2000);
                    return null;
                }

                bool sticky = AimSettings.StickyAim && _trackingActive;
                float fovSize = Math.Clamp((float)AimSettings.FovSize, 0, IMAGE_SIZE);
                float stickyThreshold = (float)AimSettings.StickyAimThreshold;
                bool hadLock = _stickyAimSelector.HasLock;

                //redundant, but i might need later not sure
                //float inset = sticky && hadLock ? 0 : (IMAGE_SIZE - fovSize) / 2;
                float inset = 0;
                var crosshair = new Vector2(detectionBox.Left + detectionBox.Width / 2,
                    detectionBox.Top + detectionBox.Height / 2);

                bool percentX = AimSettings.UseXAxisPercentageAdjustment;
                bool percentY = AimSettings.UseYAxisPercentageAdjustment;

                double xPercent = AimSettings.XOffsetPercent;
                double yPercent = AimSettings.YOffsetPercent;

                double xOffset = AimSettings.XOffset;
                double yOffset = AimSettings.YOffset;
                string alignment = AimSettings.AimingBoundariesAlignment;
                Func<RectangleF, Vector2> aimPoint = bounds => AimCoordinates.AimPoint(bounds,
                    percentX, xPercent, xOffset, percentY, yPercent, yOffset, alignment);

                List<Prediction> predictions;
                using (Benchmark("DecodeDetections"))
                {
                    float minConfidence = AimSettings.MinimumConfidence;
                    string selectedClass = AimSettings.TargetClass;

                    predictions = PredictionFilter.CreatePredictions(
                        outputTensor,
                        detectionBox,
                        IMAGE_SIZE,
                        NUM_DETECTIONS,
                        NUM_CLASSES,
                        _modelClasses,
                        minConfidence,
                        selectedClass,
                        inset,
                        IMAGE_SIZE - inset,
                        inset,
                        IMAGE_SIZE - inset,
                        capturedPixels: _captureManager.CapturedPixels);
                }

                if (_benchmarkMode)
                {
                    return StickyAimSelector.NearestToCenter(predictions, crosshair, aimPoint, fovSize);
                }

                Prediction? finalTarget;
                using (Benchmark("SelectTarget"))
                {
                    finalTarget = _stickyAimSelector.SelectTarget(
                        sticky, stickyThreshold, predictions,
                        _captureManager.LastCaptureTimestamp, crosshair, aimPoint,
                        AimSettings.StickyAimGraceMs, detectionBox, fovSize);
                    displayDetection = AimSettings.ShowDetectedPlayer
                        ? _stickyAimSelector.ForDisplay(finalTarget, predictions, crosshair, aimPoint, fovSize) : null;
                }
                if (predictions.Count != 0)
                {
                    _framesWithCandidates++;
                    if (finalTarget == null) _framesAwaitingMatch++;
                }
                if (frame != null) SaveFrame(frame, outputTensor, detectionBox);
                if (_stickyAimSelector.Acquired || (hadLock && !_stickyAimSelector.HasLock)) ResetMotion();
                if (finalTarget != null && AimSettings.AimAssist)
                {
                    using var timing = Benchmark("CalculateCoordinates");
                    var error = aimPoint(finalTarget.ScreenBounds) - crosshair;
                    measured = System.Drawing.Point.Round(new PointF(error.X, error.Y));
                }
                return finalTarget;
            }
            finally
            {
                results?.Dispose();
            }
        }

        private Rectangle CreateDetectionBox(bool useMousePosition = true)
        {
            string detectionAreaType = AimSettings.DetectionAreaType;
            System.Drawing.Point mousePosition = default;
            bool mouseOnCurrentDisplay = false;

            if (useMousePosition && detectionAreaType == "Closest to Mouse")
            {
                mousePosition = WinAPICaller.GetCursorPosition();
                mouseOnCurrentDisplay = DisplayManager.IsPointInCurrentDisplay(new System.Windows.Point(mousePosition.X, mousePosition.Y));
            }

            var displayBounds = new Rectangle(
                DisplayManager.ScreenLeft,
                DisplayManager.ScreenTop,
                DisplayManager.ScreenWidth,
                DisplayManager.ScreenHeight);

            return CaptureTargetSelector.SelectDetectionBox(
                detectionAreaType,
                IMAGE_SIZE,
                displayBounds,
                mousePosition,
                mouseOnCurrentDisplay);
        }

        public async Task<PerformanceBenchmarkReport> RunPerformanceBenchmarkAsync(
            IProgress<PerformanceBenchmarkProgress>? progress = null,
            CancellationToken cancellationToken = default,
            PerformanceGoal goal = PerformanceGoal.Balanced)
        {
            CancellationTokenSource benchmarkCancellation;
            bool originalSizeChangePending;
            lock (_sizeLock)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (!IsLoaded || _aiLoopCancellation == null)
                    throw new InvalidOperationException("Load a model before running the performance helper.");
                if (_benchmarkMode) throw new InvalidOperationException("A performance benchmark is already running.");
                benchmarkCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _aiLoopCancellation.Token);
                originalSizeChangePending = _sizeChangePending;
                _sizeChangePending = false;
                _benchmarkMode = true;
                _suppressOutputActions = true;
            }
            using var ownedCancellation = benchmarkCancellation;
            cancellationToken = benchmarkCancellation.Token;

            int originalImageSize = IMAGE_SIZE;
            int originalDetections = NUM_DETECTIONS;
            InferenceSession? originalSession = _onnxModel;
            List<string>? originalOutputNames = _outputNames;
            bool originalUsingDirectML = _usingDirectML;

            int[] supportedSizes = [640, 512, 416, 320, 256, 160];
            int[] sizes = IsDynamicModel
                ? supportedSizes
                    .OrderBy(size => size == originalImageSize ? 0 : 1)
                    .ThenByDescending(size => size)
                    .ToArray()
                : [ModelFixedSize];

            var results = new List<PerformanceBenchmarkSizeResult>(sizes.Length);
            bool gateEntered = false;
            Exception? benchmarkException = null;
            Exception? restoreException = null;

            try
            {
                await Task.Delay(80, cancellationToken).ConfigureAwait(false);
                await _inferenceGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                gateEntered = true;
                _stickyAimSelector.Reset();

                for (int i = 0; i < sizes.Length; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int size = sizes[i];

                    progress?.Report(new PerformanceBenchmarkProgress(
                        i + 1,
                        sizes.Length,
                        size,
                        $"Preparing {size}px model"));

                    ConfigureBenchmarkImageSize(size);
                    if (i > 0 || size != originalImageSize)
                    {
                        ReloadBenchmarkModelSession(originalSession);
                    }

                    progress?.Report(new PerformanceBenchmarkProgress(
                        i + 1,
                        sizes.Length,
                        size,
                        $"Warming up {size}px"));

                    await RunBenchmarkWarmupAsync(
                        cancellationToken,
                        progress,
                        i + 1,
                        sizes.Length,
                        size);

                    progress?.Report(new PerformanceBenchmarkProgress(
                        i + 1,
                        sizes.Length,
                        size,
                        $"Testing {size}px"));

                    PerformanceBenchmarkSizeResult result = await RunBenchmarkSampleAsync(
                        BenchmarkSampleDuration,
                        true,
                        cancellationToken,
                        progress,
                        i + 1,
                        sizes.Length,
                        size,
                        "Testing");

                    results.Add(result);
                }
            }
            catch (Exception ex)
            {
                benchmarkException = ex;
            }
            finally
            {
                try
                {
                    if (gateEntered)
                    {
                        RestoreBenchmarkState(
                            originalImageSize,
                            originalDetections,
                            originalSession,
                            originalOutputNames,
                            originalUsingDirectML);
                    }
                }
                catch (Exception ex)
                {
                    restoreException = ex;
                    Log(LogLevel.Error, $"Performance helper could not restore the model session: {ex.Message}", true, 5000);
                }
                finally
                {
                    if (gateEntered)
                    {
                        _stickyAimSelector.Reset();
                        _inferenceGate.Release();
                    }

                    lock (_sizeLock)
                    {
                        _sizeChangePending = _sizeChangePending || originalSizeChangePending;
                        _suppressOutputActions = false;
                        _benchmarkMode = false;
                    }
                }
            }

            ThrowIfBenchmarkFailed(benchmarkException, restoreException);

            var recommendations = PerformanceRecommendationBuilder.BuildChoices(
                results,
                originalImageSize,
                !IsDynamicModel,
                goal);
            var recommendation = recommendations.Primary;

            progress?.Report(new PerformanceBenchmarkProgress(
                sizes.Length,
                sizes.Length,
                recommendation.SuggestedImageSize,
                "Complete"));

            return new PerformanceBenchmarkReport(results, recommendation, !IsDynamicModel, recommendations, goal);
        }

        private static void ThrowIfBenchmarkFailed(Exception? benchmarkException, Exception? restoreException)
        {
            if (restoreException != null)
            {
                throw new InvalidOperationException(
                    "Performance helper could not restore the model session. Reload the model before applying settings.",
                    restoreException);
            }

            if (benchmarkException != null)
            {
                ExceptionDispatchInfo.Capture(benchmarkException).Throw();
            }
        }

        private async Task RunBenchmarkWarmupAsync(
            CancellationToken cancellationToken,
            IProgress<PerformanceBenchmarkProgress>? progress,
            int stepIndex,
            int totalSteps,
            int imageSize)
        {
            var warmupStopwatch = Stopwatch.StartNew();
            var progressStopwatch = Stopwatch.StartNew();
            var recentFrameTimes = new Queue<double>(BenchmarkStabilityWindow);
            int inferenceCount = 0;

            while (warmupStopwatch.Elapsed < BenchmarkWarmupMaximumDuration)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var frameStopwatch = Stopwatch.StartNew();
                DetectAndSelect(out _, out _, useMousePosition: false);
                frameStopwatch.Stop();

                if (_lastPredictionRanInference)
                {
                    inferenceCount++;
                    recentFrameTimes.Enqueue(frameStopwatch.Elapsed.TotalMilliseconds);
                    if (recentFrameTimes.Count > BenchmarkStabilityWindow)
                    {
                        recentFrameTimes.Dequeue();
                    }
                }

                if (progress != null && progressStopwatch.ElapsedMilliseconds >= 250)
                {
                    double requiredProgress = Math.Min(
                        warmupStopwatch.Elapsed.TotalMilliseconds / BenchmarkWarmupMinimumDuration.TotalMilliseconds,
                        inferenceCount / (double)BenchmarkWarmupMinimumInferences);
                    double maxProgress = warmupStopwatch.Elapsed.TotalMilliseconds / BenchmarkWarmupMaximumDuration.TotalMilliseconds;
                    int secondsLeft = Math.Max(0, (int)Math.Ceiling((BenchmarkWarmupMinimumDuration - warmupStopwatch.Elapsed).TotalSeconds));

                    progress.Report(new PerformanceBenchmarkProgress(
                        stepIndex,
                        totalSteps,
                        imageSize,
                        $"Warming up {imageSize}px ({secondsLeft}s minimum, {inferenceCount} frames)",
                        Math.Clamp(Math.Max(requiredProgress, maxProgress), 0, 1)));
                    progressStopwatch.Restart();
                }

                if (warmupStopwatch.Elapsed >= BenchmarkWarmupMinimumDuration &&
                    inferenceCount >= BenchmarkWarmupMinimumInferences &&
                    IsBenchmarkWarmupStable(recentFrameTimes))
                {
                    break;
                }
            }
        }

        private static bool IsBenchmarkWarmupStable(IReadOnlyCollection<double> recentFrameTimes)
        {
            if (recentFrameTimes.Count < BenchmarkStabilityWindow)
                return false;

            double average = recentFrameTimes.Average();
            double max = recentFrameTimes.Max();
            return max <= Math.Max(100, average * 2.5);
        }

        private void ConfigureBenchmarkImageSize(int imageSize)
        {
            _currentImageSize = imageSize;
            NUM_DETECTIONS = CalculateNumDetections(imageSize);
            ReleaseInferenceBuffers();
        }

        private void EnsureInferenceBuffers(InferenceSession session)
        {
            int length = checked(3 * IMAGE_SIZE * IMAGE_SIZE);
            if ((_reusableFloatInput?.Length ?? _reusableHalfInput?.Length) == length) return;

            ReleaseInferenceBuffers();
            var input = session.InputMetadata.First();
            Type inputType = input.Value.ElementType;
            Type outputType = session.OutputMetadata.First().Value.ElementType;
            _halfOutput = outputType == typeof(Float16);
            if (!_halfOutput && outputType != typeof(float))
                throw new NotSupportedException($"Unsupported model output type {outputType.Name}.");

            if (inputType == typeof(Float16)) _reusableHalfInput = Bind(new Float16[length]);
            else if (inputType == typeof(float)) _reusableFloatInput = Bind(new float[length]);
            else throw new NotSupportedException($"Unsupported model input type {inputType.Name}.");

            Log(LogLevel.Info, $"Model precision: Input={(_reusableHalfInput != null ? "Float16" : "Float32")}, Output={(_halfOutput ? "Float16" : "Float32")}");

            T[] Bind<T>(T[] buffer) where T : unmanaged
            {
                if (_usingDirectML)
                    _fixedModelBuffers = FixedModelBuffers.TryCreate(session, IMAGE_SIZE, buffer);
                if (_fixedModelBuffers != null && _gpuContext != null && !_gpuCaptureDisabled && !_benchmarkMode)
                {
                    try { _fixedModelBuffers.BindGpuInput(_gpuContext.GetInputTensor(IMAGE_SIZE, typeof(T) == typeof(Float16))); }
                    catch (Exception ex) { DisableGpuCapture(ex.Message); }
                }
                if (_fixedModelBuffers == null)
                {
                    var tensor = new DenseTensor<T>(buffer, new[] { 1, 3, IMAGE_SIZE, IMAGE_SIZE });
                    _reusableInputs = [NamedOnnxValue.CreateFromTensor(input.Key, tensor)];
                }
                return buffer;
            }
        }

        private void ReleaseInferenceBuffers()
        {
            _fixedModelBuffers?.Dispose();
            _fixedModelBuffers = null;
            _reusableFloatInput = null;
            _reusableHalfInput = null;
            _reusableInputs = null;
        }

        private void RestoreBenchmarkState(
            int originalImageSize,
            int originalDetections,
            InferenceSession? originalSession,
            List<string>? originalOutputNames,
            bool originalUsingDirectML)
        {
            InferenceSession? benchmarkSession = _onnxModel;
            ConfigureBenchmarkImageSize(originalImageSize);
            _onnxModel = originalSession;
            _outputNames = originalOutputNames;
            _usingDirectML = originalUsingDirectML;
            NUM_DETECTIONS = originalDetections;

            if (!ReferenceEquals(benchmarkSession, originalSession))
            {
                benchmarkSession?.Dispose();
            }

            if (_onnxModel == null || _outputNames == null)
            {
                throw new InvalidOperationException("Original model session was unavailable after benchmark.");
            }
        }

        private void ReloadBenchmarkModelSession(InferenceSession? preservedSession)
        {
            InferenceSession? previousSession = _onnxModel;
            OnnxModelLoadResult loadedModel = OnnxModelSessionFactory.Load(_modelPath, _usingDirectML, _gpuContext);
            _onnxModel = loadedModel.Session;
            _outputNames = loadedModel.OutputNames;

            if (!ReferenceEquals(previousSession, preservedSession))
            {
                previousSession?.Dispose();
            }
        }

        private async Task<PerformanceBenchmarkSizeResult> RunBenchmarkSampleAsync(
            TimeSpan duration,
            bool collectMetrics,
            CancellationToken cancellationToken,
            IProgress<PerformanceBenchmarkProgress>? progress = null,
            int stepIndex = 0,
            int totalSteps = 0,
            int imageSize = 0,
            string phase = "")
        {
            using var sampler = new ResourceUsageSampler();
            var sampleStopwatch = Stopwatch.StartNew();
            var peakWindowStopwatch = Stopwatch.StartNew();
            var progressStopwatch = Stopwatch.StartNew();
            int frameCount = 0;
            int peakWindowFrameCount = 0;
            double maxFps = 0;

            if (collectMetrics)
            {
                sampler.Start();
            }

            while (sampleStopwatch.Elapsed < duration)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var frameStopwatch = Stopwatch.StartNew();
                DetectAndSelect(out _, out _, useMousePosition: false);
                frameStopwatch.Stop();
                bool ranInference = _lastPredictionRanInference;

                if (progress != null &&
                    progressStopwatch.ElapsedMilliseconds >= 250 &&
                    totalSteps > 0)
                {
                    double stepProgress = Math.Clamp(sampleStopwatch.Elapsed.TotalMilliseconds / duration.TotalMilliseconds, 0, 1);
                    int secondsLeft = Math.Max(0, (int)Math.Ceiling((duration - sampleStopwatch.Elapsed).TotalSeconds));
                    progress.Report(new PerformanceBenchmarkProgress(
                        stepIndex,
                        totalSteps,
                        imageSize,
                        $"{phase} {imageSize}px ({secondsLeft}s left)",
                        stepProgress));
                    progressStopwatch.Restart();
                }

                if (!collectMetrics)
                    continue;

                if (!ranInference)
                    continue;

                frameCount++;
                peakWindowFrameCount++;

                if (peakWindowStopwatch.ElapsedMilliseconds >= 500)
                {
                    maxFps = Math.Max(maxFps, peakWindowFrameCount / peakWindowStopwatch.Elapsed.TotalSeconds);
                    peakWindowFrameCount = 0;
                    peakWindowStopwatch.Restart();
                }
            }

            if (!collectMetrics)
            {
                return new PerformanceBenchmarkSizeResult(
                    IMAGE_SIZE,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    false,
                    0);
            }

            TimeSpan sampleElapsed = sampleStopwatch.Elapsed;
            TimeSpan peakWindowElapsed = peakWindowStopwatch.Elapsed;
            sampleStopwatch.Stop();
            peakWindowStopwatch.Stop();

            ResourceUsageSummary resourceUsage = await sampler.FinishAsync();
            double averageFps = CalculateBenchmarkAverageFps(frameCount, sampleElapsed);
            if (peakWindowFrameCount > 0 && peakWindowElapsed.TotalSeconds > 0)
            {
                maxFps = Math.Max(maxFps, peakWindowFrameCount / peakWindowElapsed.TotalSeconds);
            }

            return new PerformanceBenchmarkSizeResult(
                IMAGE_SIZE,
                averageFps,
                Math.Max(maxFps, averageFps),
                resourceUsage.AverageCpuPercent,
                resourceUsage.PeakCpuPercent,
                resourceUsage.AverageGpuPercent,
                resourceUsage.PeakGpuPercent,
                resourceUsage.GpuAvailable,
                frameCount);
        }

        internal static double CalculateBenchmarkAverageFps(int frameCount, TimeSpan sampleElapsed)
        {
            return sampleElapsed.TotalSeconds > 0
                ? frameCount / sampleElapsed.TotalSeconds
                : 0;
        }

        #endregion AI Loop Functions

        #endregion AI

        #region Screen Capture

        private void SaveFrame(Bitmap frame, TensorBase output, Rectangle crop)
        {
            bool label = AimSettings.AutoLabelData;
            if (_suppressOutputActions || !AimSettings.CollectDataWhilePlaying ||
                (AimSettings.ConstantAiTracking && !label) || !_pendingSave.IsCompleted ||
                (DateTime.UtcNow - lastSavedTime).TotalMilliseconds < SAVE_FRAME_COOLDOWN_MS) return;

            var boxes = label ? PredictionFilter.CreatePredictions(output, crop, IMAGE_SIZE,
                NUM_DETECTIONS, NUM_CLASSES, _modelClasses, AimSettings.MinimumConfidence,
                "Best Confidence", 0, IMAGE_SIZE, 0, IMAGE_SIZE, clipToBounds: true, capturedPixels: _captureManager.CapturedPixels) : null;
            var ownedFrame = (Bitmap)frame.Clone();
            lastSavedTime = DateTime.UtcNow;
            _pendingSave = Task.Run(() =>
            {
                using (ownedFrame)
                {
                    string id = Guid.NewGuid().ToString();
                    string imagePath = Path.Combine("bin", "images", id + ".jpg");
                    string labelPath = Path.Combine("bin", "labels", id + ".txt");
                    try
                    {
                        if (boxes != null)
                            TrainingLabels.Write(labelPath, boxes, ownedFrame.Width, ownedFrame.Height);
                        ownedFrame.Save(imagePath, ImageFormat.Jpeg);
                    }
                    catch (Exception ex)
                    {
                        Log(LogLevel.Error, $"Frame export {id} failed: {ex.Message}");
                        try { File.Delete(imagePath); File.Delete(labelPath); }
                        catch (Exception cleanup) { Log(LogLevel.Warning, cleanup.Message); }
                    }
                }
            });
        }

        #endregion Screen Capture

        private void StopAiLoop()
        {
            _isAiLoopRunning = false;
            _aiLoopCancellation?.Cancel();

            try
            {
                Task? loopTask = _aiLoopTask;
                if (loopTask != null && !loopTask.Wait(TimeSpan.FromSeconds(5)))
                {
                    Log(LogLevel.Warning, "AI loop is still stopping; waiting before disposing model resources.");
                    loopTask.Wait();
                }
            }
            catch (AggregateException ex) when (ex.InnerExceptions.All(inner => inner is OperationCanceledException))
            {
            }
            catch (Exception ex)
            {
                Log(LogLevel.Warning, $"AI loop did not stop cleanly: {ex.Message}");
            }
            finally
            {
                _aiLoopCancellation?.Dispose();
                _aiLoopCancellation = null;
                _aiLoopTask = null;
            }
        }

        public void Dispose()
        {
            lock (_sizeLock)
            {
                if (_disposed) return;
                _disposed = true;
                _sizeChangePending = true;
            }

            // Loading can still be notifying WPF synchronously. Let that worker finish
            // before tearing down resources, without blocking its dispatcher callbacks.
            if (!InitializationTask.IsCompleted)
            {
                _ = InitializationTask.ContinueWith(_ => DisposeResources(), CancellationToken.None,
                    TaskContinuationOptions.None, TaskScheduler.Default);
                return;
            }
            DisposeResources();
        }

        private void DisposeResources()
        {
            StopAiLoop();
            _pendingSave.GetAwaiter().GetResult();
            // A canceled benchmark restores its original session before releasing this gate.
            _inferenceGate.Wait();
            try
            {
                PrintBenchmarks();
                _captureManager.Dispose();
                DisposeLoadedModel();
            }
            finally
            {
                _modeloptions?.Dispose();
                _inferenceGate.Dispose();
            }
        }
    }
}
