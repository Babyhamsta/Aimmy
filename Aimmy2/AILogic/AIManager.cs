using AILogic;
using Aimmy2.Class;
using Class;
using InputLogic;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Newtonsoft.Json.Linq;
using Other;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Visuality;
using static AILogic.MathUtil;
using static Other.LogManager;
using Device = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;

namespace Aimmy2.AILogic
{
    #region Core Data Structures
    
    /// <summary>
    /// Represents a single captured frame passed from the capture thread to the processing thread.
    /// </summary>
    public readonly struct CapturedFrame
    {
        public readonly byte FrameData;
        public readonly Rectangle CaptureRegion;
        public readonly int Width;
        public readonly int Height;

        public CapturedFrame(byte frameData, Rectangle captureRegion, int width, int height)
        {
            FrameData = frameData;
            CaptureRegion = captureRegion;
            Width = width;
            Height = height;
        }
    }

    /// <summary>
    /// Represents a single detection after model inference.
    /// </summary>
    public class Detection
    {
        public RectangleF Box { get; set; }
        public float Confidence { get; set; }
        public int ClassId { get; set; }
        public string ClassName { get; set; }
    }

    /// <summary>
    /// Represents a tracked object with a persistent ID.
    /// </summary>
    public class Track
    {
        public int Id { get; }
        public RectangleF Box { get; private set; }
        public int ClassId { get; }
        public string ClassName { get; }
        public int Misses { get; set; }
        public int Age { get; private set; }

        // Placeholder for a Kalman Filter instance
        // public KalmanFilter KalmanFilter { get; }

        private static int _nextId = 1;

        public Track(Detection detection)
        {
            Id = _nextId++;
            ClassId = detection.ClassId;
            ClassName = detection.ClassName;
            Update(detection);
            // KalmanFilter = new KalmanFilter();
            // KalmanFilter.Initialize(detection.Box);
        }

        public void Update(Detection detection)
        {
            Box = detection.Box;
            Age++;
            Misses = 0;
            // KalmanFilter.Correct(detection.Box);
        }

        public void Predict()
        {
            // Box = KalmanFilter.Predict();
            Age++;
            Misses++;
        }
    }

    #endregion

    /// <summary>
    /// Manages the entire AI perception pipeline, from screen capture to aim mechanics.
    /// This class is architected using a high-performance, asynchronous producer-consumer pattern.
    /// </summary>
    internal class AIManager : IDisposable
    {
        #region Variables

        // Pipeline Control
        private CancellationTokenSource _cancellationTokenSource;
        private readonly BlockingCollection<CapturedFrame> _frameQueue = new BlockingCollection<CapturedFrame>(2);

        // ONNX Model & Inference
        private InferenceSession _onnxModel;
        private readonly RunOptions _modelOptions;
        private List<string> _outputNames;
        private int _imageSize;
        private int _numClasses = 1;
        private int _numDetections = 8400; // Default for 640x640, will be updated
        private Dictionary<int, string> _modelClasses = new Dictionary<int, string> { { 0, "enemy" } };
        public Dictionary<int, string> ModelClasses => _modelClasses;
        public static event Action<Dictionary<int, string>> ClassesUpdated;

        // Screen Capture
        private DxgiCaptureService _captureService;

        // Tracking & Targeting
        private readonly SortTracker _tracker = new SortTracker();
        private readonly TargetingSystem _targetingSystem = new TargetingSystem();
        private Track _currentTarget;

        // Benchmarking
        private readonly Dictionary<string, BenchmarkData> _benchmarks = new();
        private readonly object _benchmarkLock = new object();

        #endregion

        public AIManager(string modelPath)
        {
            _imageSize = int.Parse(Dictionary.dropdownState);
            _modelOptions = new RunOptions();

            // Asynchronously initialize the model and capture service
            InitializeServices(modelPath);
        }

        private async void InitializeServices(string modelPath)
        {
            try
            {
                await InitializeModelAsync(modelPath);
                _captureService = new DxgiCaptureService();
                Start();
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Failed to initialize AI Manager: {ex.Message}", true);
            }
        }

        #region Model Initialization

        private async Task InitializeModelAsync(string modelPath)
        {
            await Task.Run(() =>
            {
                var sessionOptions = new SessionOptions
                {
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                    EnableMemoryPattern = true,
                    ExecutionMode = ExecutionMode.ORT_SEQUENTIAL
                };

                // Prioritize GPU Execution Providers, fallback to CPU
                try
                {
                    sessionOptions.AppendExecutionProvider_CUDA(0);
                    Log(LogLevel.Info, "ONNX Runtime: Using CUDA Execution Provider.");
                }
                catch
                {
                    try
                    {
                        sessionOptions.AppendExecutionProvider_DML(0);
                        Log(LogLevel.Info, "ONNX Runtime: Using DirectML Execution Provider.");
                    }
                    catch
                    {
                        sessionOptions.AppendExecutionProvider_CPU();
                        Log(LogLevel.Warning, "ONNX Runtime: No GPU provider found. Falling back to CPU.");
                    }
                }

                _onnxModel = new InferenceSession(modelPath, sessionOptions);
                _outputNames = _onnxModel.OutputMetadata.Keys.ToList();

                ValidateOnnxShape();
                LoadClasses();
            });
        }

        private void ValidateOnnxShape()
        {
            // Simplified validation logic
            var outputMeta = _onnxModel.OutputMetadata.Values.First();
            _numDetections = outputMeta.Dimensions[1];
            _numClasses = outputMeta.Dimensions[2] - 4; // 4 for box coords
            Log(LogLevel.Info, $"Model loaded. Detections: {_numDetections}, Classes: {_numClasses}");
        }

        private void LoadClasses()
        {
            if (_onnxModel.ModelMetadata.CustomMetadataMap.TryGetValue("names", out var namesJson))
            {
                var parsedClasses = JObject.Parse(namesJson);
                _modelClasses.Clear();
                foreach (var prop in parsedClasses.Properties())
                {
                    if (int.TryParse(prop.Name, out int classId))
                    {
                        _modelClasses[classId] = prop.Value.ToString();
                    }
                }
                Log(LogLevel.Info, $"Loaded {_modelClasses.Count} classes from model metadata.");
                ClassesUpdated?.Invoke(new Dictionary<int, string>(_modelClasses));
            }
            else
            {
                Log(LogLevel.Warning, "Model metadata does not contain 'names' field. Using default classes.");
            }
        }

        #endregion

        #region Pipeline Start/Stop

        public void Start()
        {
            if (_cancellationTokenSource!= null) return;

            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            Task.Run(() => CaptureLoop(token), token);
            Task.Run(() => ProcessingLoop(token), token);

            Log(LogLevel.Info, "AI pipeline started.");
        }

        public async Task StopAsync()
        {
            if (_cancellationTokenSource == null) return;

            _cancellationTokenSource.Cancel();
            // Allow time for loops to finish gracefully
            await Task.Delay(100);
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;

            Log(LogLevel.Info, "AI pipeline stopped.");
        }

        #endregion

        #region Producer-Consumer Pipeline

        /// <summary>
        /// The Producer: Captures frames from the screen and adds them to the queue.
        /// </summary>
        private void CaptureLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                using (Benchmark("CaptureLoopIteration"))
                {
                    var mousePos = WinAPICaller.GetCursorPosition();
                    var targetX = DisplayManager.ScreenLeft + (DisplayManager.ScreenWidth / 2);
                    var targetY = DisplayManager.ScreenTop + (DisplayManager.ScreenHeight / 2);

                    if (Dictionary.dropdownState == "Closest to Mouse" &&
                        DisplayManager.IsPointInCurrentDisplay(new Point(mousePos.X, mousePos.Y)))
                    {
                        targetX = mousePos.X;
                        targetY = mousePos.Y;
                    }

                    var captureRegion = new Rectangle(targetX - _imageSize / 2, targetY - _imageSize / 2, _imageSize, _imageSize);
                    
                    byte frameData = _captureService.CaptureFrame(captureRegion);

                    if (frameData!= null)
                    {
                        var frame = new CapturedFrame(frameData, captureRegion, _imageSize, _imageSize);
                        // TryAdd is non-blocking and will drop the frame if the queue is full.
                        if (!_frameQueue.TryAdd(frame, 5, token))
                        {
                            // Optional: Log frame drops if consumer is falling behind
                        }
                    }
                }
            }
            _frameQueue.CompleteAdding();
        }

        /// <summary>
        /// The Consumer: Processes frames from the queue, runs inference, and performs tracking/aiming.
        /// </summary>
        private void ProcessingLoop(CancellationToken token)
        {
            float inputArray = new float;
            var tensor = new DenseTensor<float>(inputArray, new { 1, 3, _imageSize, _imageSize });
            var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("images", tensor) };

            foreach (var frame in _frameQueue.GetConsumingEnumerable(token))
            {
                using (Benchmark("ProcessingLoopIteration"))
                {
                    if (!ShouldProcess())
                    {
                        // Sleep briefly to yield CPU if AI is disabled
                        Thread.Sleep(10);
                        continue;
                    }
                    
                    // 1. Pre-process frame
                    PreprocessFrame(frame, inputArray);
                    inputArray.AsSpan().CopyTo(tensor.Buffer.Span);

                    // 2. Run Inference
                    Tensor<float> outputTensor;
                    using (Benchmark("ModelInference"))
                    {
                        using var results = _onnxModel.Run(inputs, _outputNames, _modelOptions);
                        outputTensor = results.AsTensor<float>();
                    }

                    // 3. Post-process: Parse, NMS
                    var detections = ParseOutput(outputTensor);
                    var filteredDetections = VisionAlgorithms.NonMaxSuppression(detections, 0.5f, 0.4f);

                    // 4. Update Tracker
                    _tracker.Update(filteredDetections);

                    // 5. Target Selection
                    _currentTarget = _targetingSystem.SelectBestTarget(_tracker.Tracks, new Point(frame.Width / 2, frame.Height / 2));

                    // 6. Update Overlay and Aim
                    if (_currentTarget!= null)
                    {
                        // TODO: Implement overlay update logic using a frozen BitmapSource
                        // for high-performance, non-blocking UI updates.

                        if (ShouldAim())
                        {
                            HandleAim(_currentTarget, frame.CaptureRegion);
                        }
                    }
                }
            }
        }

        #endregion

        #region AI Processing Steps

        private void PreprocessFrame(CapturedFrame frame, float inputArray)
        {
            // Efficiently convert B8G8R8A8 byte array to planar float array (R, G, B) and normalize.
            // This is a performance-critical step. Using SIMD with System.Numerics.Vectors is recommended.
            int stride = frame.Width * 4;
            int size = frame.Width * frame.Height;
            Parallel.For(0, size, i =>
            {
                int baseIndex = i * 4;
                inputArray[i] = frame.FrameData[baseIndex + 2] / 255.0f;      // R
                inputArray[i + size] = frame.FrameData[baseIndex + 1] / 255.0f; // G
                inputArray[i + 2 * size] = frame.FrameData[baseIndex] / 255.0f; // B
            });
        }

        private List<Detection> ParseOutput(Tensor<float> output)
        {
            var detections = new List<Detection>();
            float minConfidence = (float)Dictionary.sliderSettings["AI Minimum Confidence"] / 100.0f;

            for (int i = 0; i < _numDetections; i++)
            {
                // Find best class score
                float bestConfidence = 0;
                int bestClassId = 0;
                for (int j = 0; j < _numClasses; j++)
                {
                    float classConfidence = output[0, 4 + j, i];
                    if (classConfidence > bestConfidence)
                    {
                        bestConfidence = classConfidence;
                        bestClassId = j;
                    }
                }

                if (bestConfidence < minConfidence) continue;

                float x_center = output[0, 0, i];
                float y_center = output[0, 1, i];
                float width = output[0, 2, i];
                float height = output[0, 3, i];

                detections.Add(new Detection
                {
                    Box = new RectangleF(x_center - width / 2, y_center - height / 2, width, height),
                    Confidence = bestConfidence,
                    ClassId = bestClassId,
                    ClassName = _modelClasses.GetValueOrDefault(bestClassId, "unknown")
                });
            }
            return detections;
        }

        private void HandleAim(Track target, Rectangle captureRegion)
        {
            // This logic remains similar, but now operates on a stable, tracked target.
            var rect = target.Box;
            var detectedX = (int)((rect.X + rect.Width / 2) + captureRegion.Left);
            var detectedY = (int)((rect.Y + rect.Height / 2) + captureRegion.Top); // Simplified Y calculation

            // TODO: Integrate prediction logic (e.g., Kalman filter's predicted state)
            MouseManager.MoveCrosshair(detectedX, detectedY);
        }

        #endregion

        #region Utility Methods

        private static bool ShouldProcess() =>
            Dictionary.toggleState["Aim Assist"] ||
            Dictionary.toggleState ||
            Dictionary.toggleState;

        private static bool ShouldAim() =>
            Dictionary.toggleState["Aim Assist"] &&
            (InputBindingManager.IsHoldingBinding("Aim Keybind") ||
             InputBindingManager.IsHoldingBinding("Second Aim Keybind"));

        #endregion

        #region Benchmarking & Disposal

        private class BenchmarkData
        {
            public long TotalTime { get; set; }
            public int CallCount { get; set; }
            public long MinTime { get; set; } = long.MaxValue;
            public long MaxTime { get; set; }
            public double AverageTime => CallCount > 0? (double)TotalTime / CallCount : 0;
        }

        private IDisposable Benchmark(string name) => new BenchmarkScope(this, name);

        private class BenchmarkScope : IDisposable
        {
            private readonly AIManager _manager;
            private readonly string _name;
            private readonly Stopwatch _sw;
            public BenchmarkScope(AIManager manager, string name)
            {
                _manager = manager;
                _name = name;
                _sw = Stopwatch.StartNew();
            }
            public void Dispose()
            {
                _sw.Stop();
                _manager.RecordBenchmark(_name, _sw.ElapsedMilliseconds);
            }
        }

        private void RecordBenchmark(string name, long elapsedMs)
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
                data.MinTime = Math.Min(data.MinTime, elapsedMs);
                data.MaxTime = Math.Max(data.MaxTime, elapsedMs);
            }
        }

        public void PrintBenchmarks()
        {
            lock (_benchmarkLock)
            {
                var lines = new List<string> { "=== AIManager Performance Benchmarks ===" };
                foreach (var kvp in _benchmarks.OrderBy(x => x.Key))
                {
                    var data = kvp.Value;
                    lines.Add($"{kvp.Key}: Avg={data.AverageTime:F2}ms, Min={data.MinTime}ms, Max={data.MaxTime}ms, Count={data.CallCount}");
                }
                Log(LogLevel.Info, string.Join(Environment.NewLine, lines));
            }
        }

        public void Dispose()
        {
            StopAsync().Wait();
            PrintBenchmarks();
            _captureService?.Dispose();
            _onnxModel?.Dispose();
            _modelOptions?.Dispose();
            _frameQueue?.Dispose();
        }

        #endregion
    }

    #region Helper Classes

    /// <summary>
    /// Provides high-performance screen capture using the DXGI Desktop Duplication API.
    /// </summary>
    public class DxgiCaptureService : IDisposable
    {
        private Device _device;
        private OutputDuplication _outputDuplication;
        private Texture2D _stagingTexture;

        public DxgiCaptureService(int adapterIndex = 0, int outputIndex = 0)
        {
            using (var factory = new Factory1())
            using (var adapter = factory.GetAdapter1(adapterIndex))
            {
                _device = new Device(adapter);
                using (var output = adapter.GetOutput(outputIndex))
                using (var output1 = output.QueryInterface<Output1>())
                {
                    _outputDuplication = output1.DuplicateOutput(_device);
                }
            }
        }

        public byte CaptureFrame(Rectangle region)
        {
            if (_stagingTexture == null |

| _stagingTexture.Description.Width!= region.Width |
| _stagingTexture.Description.Height!= region.Height)
            {
                _stagingTexture?.Dispose();
                var textureDesc = new Texture2DDescription
                {
                    CpuAccessFlags = CpuAccessFlags.Read,
                    BindFlags = BindFlags.None,
                    Format = Format.B8G8R8A8_UNorm,
                    Width = region.Width,
                    Height = region.Height,
                    OptionFlags = ResourceOptionFlags.None,
                    MipLevels = 1,
                    ArraySize = 1,
                    SampleDescription = { Count = 1, Quality = 0 },
                    Usage = ResourceUsage.Staging
                };
                _stagingTexture = new Texture2D(_device, textureDesc);
            }

            try
            {
                if (_outputDuplication.AcquireNextFrame(500, out _, out SharpDX.DXGI.Resource screenResource).Failure)
                    return null;

                using (screenResource)
                using (var screenTexture = screenResource.QueryInterface<Texture2D>())
                {
                    var sourceRegion = new ResourceRegion(region.Left, region.Top, 0, region.Right, region.Bottom, 1);
                    _device.ImmediateContext.CopySubresourceRegion(screenTexture, 0, sourceRegion, _stagingTexture, 0);
                }

                var dataBox = _device.ImmediateContext.MapSubresource(_stagingTexture, 0, MapMode.Read, MapFlags.None);
                try
                {
                    byte frameData = new byte;
                    IntPtr sourcePtr = dataBox.DataPointer;
                    int rowPitch = dataBox.RowPitch;
                    int framePitch = region.Width * 4;

                    if (rowPitch == framePitch)
                    {
                        Marshal.Copy(sourcePtr, frameData, 0, frameData.Length);
                    }
                    else
                    {
                        for (int y = 0; y < region.Height; y++)
                        {
                            Marshal.Copy(sourcePtr + y * rowPitch, frameData, y * framePitch, framePitch);
                        }
                    }
                    return frameData;
                }
                finally
                {
                    _device.ImmediateContext.UnmapSubresource(_stagingTexture, 0);
                    _outputDuplication.ReleaseFrame();
                }
            }
            catch (SharpDXException e) when (e.ResultCode.Code == SharpDX.DXGI.ResultCode.WaitTimeout.Result.Code)
            {
                return null; // Expected timeout
            }
        }

        public void Dispose()
        {
            _stagingTexture?.Dispose();
            _outputDuplication?.Dispose();
            _device?.Dispose();
        }
    }

    /// <summary>
    /// Contains computer vision algorithms for post-processing.
    /// </summary>
    public static class VisionAlgorithms
    {
        public static float CalculateIoU(RectangleF boxA, RectangleF boxB)
        {
            float xA = Math.Max(boxA.Left, boxB.Left);
            float yA = Math.Max(boxA.Top, boxB.Top);
            float xB = Math.Min(boxA.Right, boxB.Right);
            float yB = Math.Min(boxA.Bottom, boxB.Bottom);

            float intersectionArea = Math.Max(0, xB - xA) * Math.Max(0, yB - yA);
            if (intersectionArea == 0) return 0;

            float boxAArea = boxA.Width * boxA.Height;
            float boxBArea = boxB.Width * boxB.Height;
            float unionArea = boxAArea + boxBArea - intersectionArea;

            return intersectionArea / unionArea;
        }

        public static List<Detection> NonMaxSuppression(List<Detection> detections, float confidenceThreshold, float iouThreshold)
        {
            var finalDetections = new List<Detection>();
            var confidentDetections = detections.Where(d => d.Confidence >= confidenceThreshold).ToList();
            var detectionsByClass = confidentDetections.GroupBy(d => d.ClassId);

            foreach (var group in detectionsByClass)
            {
                var classDetections = group.ToList();
                classDetections.Sort((a, b) => b.Confidence.CompareTo(a.Confidence));

                while (classDetections.Count > 0)
                {
                    var bestDetection = classDetections;
                    finalDetections.Add(bestDetection);
                    classDetections.RemoveAt(0);

                    classDetections = classDetections
                       .Where(d => CalculateIoU(bestDetection.Box, d.Box) < iouThreshold)
                       .ToList();
                }
            }
            return finalDetections;
        }
    }

    /// <summary>
    /// Implements Simple Online and Realtime Tracking (SORT).
    /// </summary>
    public class SortTracker
    {
        public List<Track> Tracks { get; } = new List<Track>();
        private readonly float _iouThreshold;
        private readonly int _maxMisses;

        public SortTracker(float iouThreshold = 0.3f, int maxMisses = 5)
        {
            _iouThreshold = iouThreshold;
            _maxMisses = maxMisses;
        }

        public void Update(List<Detection> detections)
        {
            // Predict next state for all existing tracks
            foreach (var track in Tracks)
            {
                track.Predict();
            }

            // Associate detections with tracks
            var matched = new HashSet<int>();
            var unmatchedDetections = new List<Detection>(detections);

            foreach (var track in Tracks)
            {
                Detection bestMatch = null;
                float bestIoU = 0;

                foreach (var detection in unmatchedDetections)
                {
                    float iou = VisionAlgorithms.CalculateIoU(track.Box, detection.Box);
                    if (iou > bestIoU)
                    {
                        bestIoU = iou;
                        bestMatch = detection;
                    }
                }

                if (bestIoU > _iouThreshold)
                {
                    track.Update(bestMatch);
                    unmatchedDetections.Remove(bestMatch);
                    matched.Add(track.Id);
                }
            }

            // Create new tracks for unmatched detections
            foreach (var detection in unmatchedDetections)
            {
                Tracks.Add(new Track(detection));
            }

            // Remove old tracks that have been missed for too long
            Tracks.RemoveAll(t => t.Misses > _maxMisses);
        }
    }

    /// <summary>
    /// A utility-based AI for selecting the best target.
    /// </summary>
    public class TargetingSystem
    {
        public Track SelectBestTarget(List<Track> tracks, Point screenCenter)
        {
            if (tracks.Count == 0) return null;

            // Simple utility: score based on proximity to crosshair.
            // A more advanced system would use multiple weighted "Considerations".
            Track bestTarget = null;
            double bestScore = double.MinValue;

            foreach (var track in tracks)
            {
                var centerX = track.Box.X + track.Box.Width / 2;
                var centerY = track.Box.Y + track.Box.Height / 2;
                
                var dx = centerX - screenCenter.X;
                var dy = centerY - screenCenter.Y;
                var distanceSquared = dx * dx + dy * dy;

                // Inverse distance is our score (higher score is better)
                double score = 1.0 / (1.0 + distanceSquared);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = track;
                }
            }
            return bestTarget;
        }
    }

    #endregion
}
