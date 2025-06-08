using AILogic;
using Aimmy2.Class;
using Class;
using InputLogic;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Other;
using Supercluster.KDTree;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using Visuality;

namespace Aimmy2.AILogic
{
    internal class AIManager : IDisposable
    {
        #region Variables

        private const int IMAGE_SIZE = 640;
        private const int NUM_DETECTIONS = 8400; // Standard for OnnxV8 model (Shape: 1x5x8400)
        private const int SAVE_FRAME_COOLDOWN_MS = 500;

        private DateTime lastSavedTime = DateTime.MinValue;
        private List<string>? _outputNames;
        private RectangleF LastDetectionBox;
        private KalmanPrediction kalmanPrediction;
        private WiseTheFoxPrediction wtfpredictionManager;

        //private Bitmap? _screenCaptureBitmap;
        private byte[]? _bitmapBuffer; // Reusable buffer for bitmap operations

        // Screen capture
        //private Device _dxDevice;
        //private OutputDuplication _deskDuplication;
        //private Texture2DDescription _texDesc;
        //private Texture2D _stagingTex;

        // Display-aware properties
        private int ScreenWidth => DisplayManager.ScreenWidth;
        private int ScreenHeight => DisplayManager.ScreenHeight;
        private int ScreenLeft => DisplayManager.ScreenLeft;
        private int ScreenTop => DisplayManager.ScreenTop;

        private readonly RunOptions? _modeloptions;
        private InferenceSession? _onnxModel;

        private Thread? _aiLoopThread;
        private volatile bool _isAiLoopRunning;

        // For Auto-Labelling Data System
        private bool PlayerFound = false;

        private double CenterXTranslated = 0;
        private double CenterYTranslated = 0;

        // For Shall0e's Prediction Method
        private int PrevX = 0;
        private int PrevY = 0;

        // Benchmarking
        private int iterationCount = 0;
        private long totalTime = 0;

        private int detectedX { get; set; }
        private int detectedY { get; set; }

        public double AIConf = 0;
        private static int targetX, targetY;

        // Pre-calculated values - now dynamic
        private float _scaleX => ScreenWidth / 640f;
        private float _scaleY => ScreenHeight / 640f;

        // Tensor reuse (model inference)
        private DenseTensor<float>? _reusableTensor;
        private float[]? _reusableInputArray;
        private List<NamedOnnxValue>? _reusableInputs;

        // Benchmarking
        private readonly Dictionary<string, BenchmarkData> _benchmarks = new();
        private readonly object _benchmarkLock = new();


        private readonly CaptureManager _captureManager = new();
        #endregion Variables

        #region Benchmarking

        private class BenchmarkData
        {
            public long TotalTime { get; set; }
            public int CallCount { get; set; }
            public long MinTime { get; set; } = long.MaxValue;
            public long MaxTime { get; set; }
            public double AverageTime => CallCount > 0 ? (double)TotalTime / CallCount : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private IDisposable Benchmark(string name)
        {
            return new BenchmarkScope(this, name);
        }

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
                Debug.WriteLine("=== AIManager Performance Benchmarks ===");
                foreach (var kvp in _benchmarks.OrderBy(x => x.Key))
                {
                    var data = kvp.Value;
                    Debug.WriteLine($"{kvp.Key}: Avg={data.AverageTime:F2}ms, Min={data.MinTime}ms, Max={data.MaxTime}ms, Count={data.CallCount}");
                }
                Debug.WriteLine($"Overall FPS: {(iterationCount > 0 ? 1000.0 / (totalTime / (double)iterationCount) : 0):F2}");
            }
        }

        #endregion Benchmarking

        public AIManager(string modelPath)
        {
            // Subscribe to display changes FIRST
            DisplayManager.DisplayChanged += OnDisplayChanged;

            // Initialize DXGI capture for current display
            if (Dictionary.dropdownState["Screen Capture Method"] == "DirectX")
            {
                _captureManager.InitializeDxgiDuplication();
            }

            kalmanPrediction = new KalmanPrediction();
            wtfpredictionManager = new WiseTheFoxPrediction();

            _modeloptions = new RunOptions();

            var sessionOptions = new SessionOptions
            {
                EnableCpuMemArena = true,
                EnableMemoryPattern = false,
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                ExecutionMode = ExecutionMode.ORT_PARALLEL,
                InterOpNumThreads = Environment.ProcessorCount,
                IntraOpNumThreads = Environment.ProcessorCount
            };

            // Attempt to load via DirectML (else fallback to CPU)
            Task.Run(() => InitializeModel(sessionOptions, modelPath));
        }

        private void OnDisplayChanged(object? sender, DisplayChangedEventArgs e)
        {
            lock (_captureManager._displayLock)
            {
                _captureManager._displayChangesPending = true;
            }
        }

        private void HandlePendingDisplayChanges()
        {
            lock (_captureManager._displayLock)
            {
                if (!_captureManager._displayChangesPending) return;

                try
                {
                    _captureManager.InitializeDxgiDuplication();
                    _captureManager._displayChangesPending = false;
                }
                catch (Exception ex)
                {
                    // Will retry on next iteration
                }
            }
        }

        #region Models

        private async Task InitializeModel(SessionOptions sessionOptions, string modelPath)
        {
            using (Benchmark("ModelInitialization"))
            {
                try
                {
                    await LoadModelAsync(sessionOptions, modelPath, useDirectML: true);
                }
                catch (Exception ex)
                {
                    await Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        new NoticeBar($"Error starting the model via DirectML: {ex.Message}\n\nFalling back to CPU, performance may be poor.", 5000).Show()));
                    try
                    {
                        await LoadModelAsync(sessionOptions, modelPath, useDirectML: false);
                    }
                    catch (Exception e)
                    {
                        await Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                            new NoticeBar($"Error starting the model via CPU: {e.Message}, you won't be able to aim assist at all.", 5000).Show()));
                    }
                }

                FileManager.CurrentlyLoadingModel = false;
            }
        }

        private async Task LoadModelAsync(SessionOptions sessionOptions, string modelPath, bool useDirectML)
        {
            try
            {
                if (useDirectML) { sessionOptions.AppendExecutionProvider_DML(); }
                else { sessionOptions.AppendExecutionProvider_CPU(); }

                _onnxModel = new InferenceSession(modelPath, sessionOptions);
                _outputNames = new List<string>(_onnxModel.OutputMetadata.Keys);

                // Validate the onnx model output shape (ensure model is OnnxV8)
                ValidateOnnxShape();

                // Pre-allocate bitmap buffer
                _bitmapBuffer = new byte[3 * IMAGE_SIZE * IMAGE_SIZE];
            }
            catch (Exception ex)
            {
                await Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    new NoticeBar($"Error starting the model: {ex.Message}", 5000).Show()));
                _onnxModel?.Dispose();
            }

            // Begin the loop
            _isAiLoopRunning = true;
            _aiLoopThread = new Thread(AiLoop)
            {
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal // Higher priority for AI thread
            };
            _aiLoopThread.Start();
        }

        private void ValidateOnnxShape()
        {
            var expectedShape = new int[] { 1, 5, NUM_DETECTIONS };
            if (_onnxModel != null)
            {
                var outputMetadata = _onnxModel.OutputMetadata;
                if (!outputMetadata.Values.All(metadata => metadata.Dimensions.SequenceEqual(expectedShape)))
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    new NoticeBar(
                        $"Output shape does not match the expected shape of {string.Join("x", expectedShape)}.\n\nThis model will not work with Aimmy, please use an YOLOv8 model converted to ONNXv8."
                        , 15000)
                    .Show()
                    ));
                }
            }
        }

        #endregion Models

        #region AI

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ShouldPredict() =>
            Dictionary.toggleState["Show Detected Player"] ||
            Dictionary.toggleState["Constant AI Tracking"] ||
            InputBindingManager.IsHoldingBinding("Aim Keybind") ||
            InputBindingManager.IsHoldingBinding("Second Aim Keybind");

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ShouldProcess() =>
            Dictionary.toggleState["Aim Assist"] ||
            Dictionary.toggleState["Show Detected Player"] ||
            Dictionary.toggleState["Auto Trigger"];

        private async void AiLoop()
        {
            Stopwatch stopwatch = new();
            DetectedPlayerWindow? DetectedPlayerOverlay = Dictionary.DetectedPlayerOverlay;

            while (_isAiLoopRunning)
            {
                stopwatch.Restart();

                // Handle any pending display changes
                HandlePendingDisplayChanges();

                using (Benchmark("AILoopIteration"))
                {
                    UpdateFOV();

                    if (ShouldProcess())
                    {
                        if (ShouldPredict())
                        {
                            Prediction? closestPrediction;
                            using (Benchmark("GetClosestPrediction"))
                            {
                                closestPrediction = await GetClosestPrediction();
                            }

                            if (closestPrediction == null)
                            {
                                DisableOverlay(DetectedPlayerOverlay!);
                                continue;
                            }

                            using (Benchmark("AutoTrigger"))
                            {
                                await AutoTrigger();
                            }

                            using (Benchmark("CalculateCoordinates"))
                            {
                                CalculateCoordinates(DetectedPlayerOverlay, closestPrediction, _scaleX, _scaleY);
                            }

                            using (Benchmark("HandleAim"))
                            {
                                HandleAim(closestPrediction);
                            }

                            totalTime += stopwatch.ElapsedMilliseconds;
                            iterationCount++;
                        }
                        else
                        {
                            // Processing so we are at the ready but not holding right/click.
                            await Task.Delay(1);
                        }
                    }
                    else
                    {
                        // No work to do—sleep briefly to free up CPU
                        await Task.Delay(1);
                    }
                }

                stopwatch.Stop();
            }
        }

        #region AI Loop Functions

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async Task AutoTrigger()
        {
            if (Dictionary.toggleState["Auto Trigger"] &&
                (InputBindingManager.IsHoldingBinding("Aim Keybind") || Dictionary.toggleState["Constant AI Tracking"]))
            {
                await MouseManager.DoTriggerClick();
                if (!Dictionary.toggleState["Aim Assist"] && !Dictionary.toggleState["Show Detected Player"]) return;
            }
        }

        private async void UpdateFOV()
        {
            if (Dictionary.dropdownState["Detection Area Type"] == "Closest to Mouse" && Dictionary.toggleState["FOV"])
            {
                var mousePosition = WinAPICaller.GetCursorPosition();

                // Check if mouse is on the current display
                if (!DisplayManager.IsPointInCurrentDisplay(new System.Windows.Point(mousePosition.X, mousePosition.Y)))
                {
                    // Mouse is on a different display - don't update FOV position
                    return;
                }

                // Translate mouse position relative to current display
                var displayRelativeX = mousePosition.X - DisplayManager.ScreenLeft;
                var displayRelativeY = mousePosition.Y - DisplayManager.ScreenTop;

                await Application.Current.Dispatcher.BeginInvoke(() =>
                    Dictionary.FOVWindow.FOVStrictEnclosure.Margin = new Thickness(
                        Convert.ToInt16(displayRelativeX / WinAPICaller.scalingFactorX) - 320,
                        Convert.ToInt16(displayRelativeY / WinAPICaller.scalingFactorY) - 320, 0, 0));
            }
        }

        private static void DisableOverlay(DetectedPlayerWindow DetectedPlayerOverlay)
        {
            if (Dictionary.toggleState["Show Detected Player"] && Dictionary.DetectedPlayerOverlay != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (Dictionary.toggleState["Show AI Confidence"])
                    {
                        DetectedPlayerOverlay!.DetectedPlayerConfidence.Opacity = 0;
                    }

                    if (Dictionary.toggleState["Show Tracers"])
                    {
                        DetectedPlayerOverlay!.DetectedTracers.Opacity = 0;
                    }

                    DetectedPlayerOverlay!.DetectedPlayerFocus.Opacity = 0;
                });
            }
        }

        private void UpdateOverlay(DetectedPlayerWindow DetectedPlayerOverlay)
        {
            var scalingFactorX = WinAPICaller.scalingFactorX;
            var scalingFactorY = WinAPICaller.scalingFactorY;

            // Convert screen coordinates to display-relative coordinates
            var displayRelativeX = LastDetectionBox.X - DisplayManager.ScreenLeft;
            var displayRelativeY = LastDetectionBox.Y - DisplayManager.ScreenTop;

            // Calculate center position in display-relative coordinates
            var centerX = Convert.ToInt16(displayRelativeX / scalingFactorX) + (LastDetectionBox.Width / 2.0);
            var centerY = Convert.ToInt16(displayRelativeY / scalingFactorY);

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (Dictionary.toggleState["Show AI Confidence"])
                {
                    DetectedPlayerOverlay.DetectedPlayerConfidence.Opacity = 1;
                    DetectedPlayerOverlay.DetectedPlayerConfidence.Content = $"{Math.Round((AIConf * 100), 2)}%";

                    var labelEstimatedHalfWidth = DetectedPlayerOverlay.DetectedPlayerConfidence.ActualWidth / 2.0;
                    DetectedPlayerOverlay.DetectedPlayerConfidence.Margin = new Thickness(
                        centerX - labelEstimatedHalfWidth,
                        centerY - DetectedPlayerOverlay.DetectedPlayerConfidence.ActualHeight - 2, 0, 0);
                }

                var showTracers = Dictionary.toggleState["Show Tracers"];
                DetectedPlayerOverlay.DetectedTracers.Opacity = showTracers ? 1 : 0;
                if (showTracers)
                {
                    DetectedPlayerOverlay.DetectedTracers.X2 = centerX;
                    DetectedPlayerOverlay.DetectedTracers.Y2 = centerY + LastDetectionBox.Height;
                }

                DetectedPlayerOverlay.Opacity = Dictionary.sliderSettings["Opacity"];

                DetectedPlayerOverlay.DetectedPlayerFocus.Opacity = 1;
                DetectedPlayerOverlay.DetectedPlayerFocus.Margin = new Thickness(
                    centerX - (LastDetectionBox.Width / 2.0), centerY, 0, 0);
                DetectedPlayerOverlay.DetectedPlayerFocus.Width = LastDetectionBox.Width;
                DetectedPlayerOverlay.DetectedPlayerFocus.Height = LastDetectionBox.Height;
            });
        }

        private void CalculateCoordinates(DetectedPlayerWindow DetectedPlayerOverlay, Prediction closestPrediction, float scaleX, float scaleY)
        {
            AIConf = closestPrediction.Confidence;

            if (Dictionary.toggleState["Show Detected Player"] && Dictionary.DetectedPlayerOverlay != null)
            {
                using (Benchmark("UpdateOverlay"))
                {
                    UpdateOverlay(DetectedPlayerOverlay!);
                }
                if (!Dictionary.toggleState["Aim Assist"]) return;
            }

            double YOffset = Dictionary.sliderSettings["Y Offset (Up/Down)"];
            double XOffset = Dictionary.sliderSettings["X Offset (Left/Right)"];

            double YOffsetPercentage = Dictionary.sliderSettings["Y Offset (%)"];
            double XOffsetPercentage = Dictionary.sliderSettings["X Offset (%)"];

            var rect = closestPrediction.Rectangle;

            if (Dictionary.toggleState["X Axis Percentage Adjustment"])
            {
                detectedX = (int)((rect.X + (rect.Width * (XOffsetPercentage / 100))) * scaleX);
            }
            else
            {
                detectedX = (int)((rect.X + rect.Width / 2) * scaleX + XOffset);
            }

            if (Dictionary.toggleState["Y Axis Percentage Adjustment"])
            {
                detectedY = (int)((rect.Y + rect.Height - (rect.Height * (YOffsetPercentage / 100))) * scaleY + YOffset);
            }
            else
            {
                detectedY = CalculateDetectedY(scaleY, YOffset, closestPrediction);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CalculateDetectedY(float scaleY, double YOffset, Prediction closestPrediction)
        {
            var rect = closestPrediction.Rectangle;
            float yBase = rect.Y;
            float yAdjustment = 0;

            switch (Dictionary.dropdownState["Aiming Boundaries Alignment"])
            {
                case "Center":
                    yAdjustment = rect.Height / 2;
                    break;

                case "Top":
                    // yBase is already at the top
                    break;

                case "Bottom":
                    yAdjustment = rect.Height;
                    break;
            }

            return (int)((yBase + yAdjustment) * scaleY + YOffset);
        }

        private void HandleAim(Prediction closestPrediction)
        {
            if (Dictionary.toggleState["Aim Assist"] &&
                (Dictionary.toggleState["Constant AI Tracking"] ||
                 Dictionary.toggleState["Aim Assist"] && InputBindingManager.IsHoldingBinding("Aim Keybind") ||
                 Dictionary.toggleState["Aim Assist"] && InputBindingManager.IsHoldingBinding("Second Aim Keybind")))
            {
                if (Dictionary.toggleState["Predictions"])
                {
                    HandlePredictions(kalmanPrediction, closestPrediction, detectedX, detectedY);
                }
                else
                {
                    MouseManager.MoveCrosshair(detectedX, detectedY);
                }
            }
        }

        private void HandlePredictions(KalmanPrediction kalmanPrediction, Prediction closestPrediction, int detectedX, int detectedY)
        {
            var predictionMethod = Dictionary.dropdownState["Prediction Method"];
            switch (predictionMethod)
            {
                case "Kalman Filter":
                    KalmanPrediction.Detection detection = new()
                    {
                        X = detectedX,
                        Y = detectedY,
                        Timestamp = DateTime.UtcNow
                    };

                    kalmanPrediction.UpdateKalmanFilter(detection);
                    var predictedPosition = kalmanPrediction.GetKalmanPosition();

                    MouseManager.MoveCrosshair(predictedPosition.X, predictedPosition.Y);
                    break;

                case "Shall0e's Prediction":
                    ShalloePredictionV2.xValues.Add(detectedX - PrevX);
                    ShalloePredictionV2.yValues.Add(detectedY - PrevY);

                    if (ShalloePredictionV2.xValues.Count > 5)
                    {
                        ShalloePredictionV2.xValues.RemoveAt(0);
                        ShalloePredictionV2.yValues.RemoveAt(0);
                    }

                    MouseManager.MoveCrosshair(ShalloePredictionV2.GetSPX(), detectedY);

                    PrevX = detectedX;
                    PrevY = detectedY;
                    break;

                case "wisethef0x's EMA Prediction":
                    WiseTheFoxPrediction.WTFDetection wtfdetection = new()
                    {
                        X = detectedX,
                        Y = detectedY,
                        Timestamp = DateTime.UtcNow
                    };

                    wtfpredictionManager.UpdateDetection(wtfdetection);
                    var wtfpredictedPosition = wtfpredictionManager.GetEstimatedPosition();

                    MouseManager.MoveCrosshair(wtfpredictedPosition.X, detectedY);
                    break;
            }
        }

        private async Task<Prediction?> GetClosestPrediction(bool useMousePosition = true)
        {
            int adjustedTargetX, adjustedTargetY;

            if (Dictionary.dropdownState["Detection Area Type"] == "Closest to Mouse")
            {
                var mousePos = WinAPICaller.GetCursorPosition();

                // Check if mouse is on the current display
                if (DisplayManager.IsPointInCurrentDisplay(new System.Windows.Point(mousePos.X, mousePos.Y)))
                {
                    // Mouse is on current display, use its position
                    targetX = mousePos.X;
                    targetY = mousePos.Y;
                }
                else
                {
                    // Mouse is on different display, use center of current display
                    targetX = DisplayManager.ScreenLeft + (DisplayManager.ScreenWidth / 2);
                    targetY = DisplayManager.ScreenTop + (DisplayManager.ScreenHeight / 2);
                }
            }
            else
            {
                // Center of current display
                targetX = DisplayManager.ScreenLeft + (DisplayManager.ScreenWidth / 2);
                targetY = DisplayManager.ScreenTop + (DisplayManager.ScreenHeight / 2);
            }

            Rectangle detectionBox = new(targetX - IMAGE_SIZE / 2, targetY - IMAGE_SIZE / 2, IMAGE_SIZE, IMAGE_SIZE); // Detection box always 640x640

            Bitmap? frame;
            using (Benchmark("ScreenGrab"))
            {
                frame = _captureManager.ScreenGrab(detectionBox);
            }
            if (frame == null) return null;

            float[] inputArray;
            using (Benchmark("BitmapToFloatArray"))
            {
                if (_reusableInputArray == null || _reusableInputArray.Length != 3 * IMAGE_SIZE * IMAGE_SIZE)
                {
                    _reusableInputArray = new float[3 * IMAGE_SIZE * IMAGE_SIZE];
                }
                inputArray = _reusableInputArray;

                // Fill the reusable array
                BitmapToFloatArrayInPlace(frame, inputArray);
            }

            // Reuse tensor and inputs
            if (_reusableTensor == null)
            {
                _reusableTensor = new DenseTensor<float>(inputArray, new int[] { 1, 3, IMAGE_SIZE, IMAGE_SIZE });
                _reusableInputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("images", _reusableTensor) };
            }
            else
            {
                // Directly copy into existing DenseTensor buffer
                inputArray.AsSpan().CopyTo(_reusableTensor.Buffer.Span);
            }

            if (_onnxModel == null) return null;

            IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results;
            using (Benchmark("ModelInference"))
            {
                results = _onnxModel.Run(_reusableInputs, _outputNames, _modeloptions);
            }

            var outputTensor = results[0].AsTensor<float>();

            // Calculate the FOV boundaries
            float FovSize = (float)Dictionary.sliderSettings["FOV Size"];
            float fovMinX = (IMAGE_SIZE - FovSize) / 2.0f;
            float fovMaxX = (IMAGE_SIZE + FovSize) / 2.0f;
            float fovMinY = (IMAGE_SIZE - FovSize) / 2.0f;
            float fovMaxY = (IMAGE_SIZE + FovSize) / 2.0f;

            List<double[]> KDpoints;
            List<Prediction> KDPredictions;
            using (Benchmark("PrepareKDTreeData"))
            {
                (KDpoints, KDPredictions) = PrepareKDTreeData(outputTensor, detectionBox, fovMinX, fovMaxX, fovMinY, fovMaxY);
            }

            if (KDpoints.Count == 0 || KDPredictions.Count == 0)
            {
                return null;
            }

            KDTree<double, Prediction> tree;
            Tuple<double[], Prediction>[]? nearest;
            using (Benchmark("KDTreeOperations"))
            {
                tree = new KDTree<double, Prediction>(2, KDpoints.ToArray(), KDPredictions.ToArray(), L2Norm_Squared_Double);
                nearest = tree.NearestNeighbors(new double[] { IMAGE_SIZE / 2.0, IMAGE_SIZE / 2.0 }, 1);
            }

            if (nearest != null && nearest.Length > 0)
            {
                // Translate coordinates
                float translatedXMin = nearest[0].Item2.Rectangle.X + detectionBox.Left;
                float translatedYMin = nearest[0].Item2.Rectangle.Y + detectionBox.Top;
                LastDetectionBox = new RectangleF(translatedXMin, translatedYMin,
                    nearest[0].Item2.Rectangle.Width, nearest[0].Item2.Rectangle.Height);

                CenterXTranslated = nearest[0].Item2.CenterXTranslated;
                CenterYTranslated = nearest[0].Item2.CenterYTranslated;

                using (Benchmark("SaveFrame"))
                {
                    SaveFrame(frame, nearest[0].Item2);
                }
                return nearest[0].Item2;
            }
            else if (Dictionary.toggleState["Collect Data While Playing"] &&
                     !Dictionary.toggleState["Constant AI Tracking"] &&
                     !Dictionary.toggleState["Auto Label Data"])
            {
                using (Benchmark("SaveFrame"))
                {
                    SaveFrame(frame);
                }
            }

            return null;
        }

        private (List<double[]>, List<Prediction>) PrepareKDTreeData(Tensor<float> outputTensor, Rectangle detectionBox,
            float fovMinX, float fovMaxX, float fovMinY, float fovMaxY)
        {
            float minConfidence = (float)Dictionary.sliderSettings["AI Minimum Confidence"] / 100.0f;

            var KDpoints = new List<double[]>(100); // Pre-allocate with estimated capacity
            var KDpredictions = new List<Prediction>(100);

            for (int i = 0; i < NUM_DETECTIONS; i++)
            {
                float objectness = outputTensor[0, 4, i];
                if (objectness < minConfidence) continue;

                float x_center = outputTensor[0, 0, i];
                float y_center = outputTensor[0, 1, i];
                float width = outputTensor[0, 2, i];
                float height = outputTensor[0, 3, i];

                float x_min = x_center - width / 2;
                float y_min = y_center - height / 2;
                float x_max = x_center + width / 2;
                float y_max = y_center + height / 2;

                if (x_min < fovMinX || x_max > fovMaxX || y_min < fovMinY || y_max > fovMaxY) continue;

                RectangleF rect = new(x_min, y_min, width, height);
                Prediction prediction = new()
                {
                    Rectangle = rect,
                    Confidence = objectness,
                    CenterXTranslated = (x_center - detectionBox.Left) / IMAGE_SIZE,
                    CenterYTranslated = (y_center - detectionBox.Top) / IMAGE_SIZE
                };

                KDpoints.Add(new double[] { x_center, y_center });
                KDpredictions.Add(prediction);
            }

            return (KDpoints, KDpredictions);
        }

        #endregion AI Loop Functions

        #endregion AI

        #region Screen Capture

        private void SaveFrame(Bitmap frame, Prediction? DoLabel = null)
        {
            // Only save frames if "Collect Data While Playing" is enabled
            if (!Dictionary.toggleState["Collect Data While Playing"]) return;

            // Skip if we're in constant tracking mode (unless auto-labeling is enabled)
            if (Dictionary.toggleState["Constant AI Tracking"] && !Dictionary.toggleState["Auto Label Data"]) return;

            // Cooldown check
            if ((DateTime.Now - lastSavedTime).TotalMilliseconds < SAVE_FRAME_COOLDOWN_MS) return;

            lastSavedTime = DateTime.Now;
            string uuid = Guid.NewGuid().ToString();

            // Clone the bitmap to avoid threading issues
            string imagePath = Path.Combine("bin", "images", $"{uuid}.jpg");

            // Save synchronously to avoid "Object is currently in use elsewhere" error
            frame.Save(imagePath, ImageFormat.Jpeg);

            if (Dictionary.toggleState["Auto Label Data"] && DoLabel != null)
            {
                var labelPath = Path.Combine("bin", "labels", $"{uuid}.txt");

                float x = (DoLabel!.Rectangle.X + DoLabel.Rectangle.Width / 2) / frame.Width;
                float y = (DoLabel!.Rectangle.Y + DoLabel.Rectangle.Height / 2) / frame.Height;
                float width = DoLabel.Rectangle.Width / frame.Width;
                float height = DoLabel.Rectangle.Height / frame.Height;

                File.WriteAllText(labelPath, $"0 {x} {y} {width} {height}");
            }
        }



        #endregion Screen Capture

        #region Optimized Math

        public static Func<double[], double[], double> L2Norm_Squared_Double = (x, y) =>
        {
            double dist = 0f;
            for (int i = 0; i < x.Length; i++)
            {
                dist += (x[i] - y[i]) * (x[i] - y[i]);
            }

            return dist;
        };

        private unsafe void BitmapToFloatArrayInPlace(Bitmap image, float[] result)
        {
            const int width = IMAGE_SIZE;
            const int height = IMAGE_SIZE;
            const int totalPixels = width * height;
            const float multiplier = 1f / 255f;

            var rect = new Rectangle(0, 0, width, height);
            var bmpData = image.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb); //3 bytes per pixel
                                                                                                    // blue green red (strict order)
            try
            {
                int stride = bmpData.Stride;
                byte* basePtr = (byte*)bmpData.Scan0;
                int redOffset = 0;
                int greenOffset = totalPixels;
                int blueOffset = 2 * totalPixels;

                //for each row in the image -> create a temporary array for red green and blue (rgb)
                Parallel.For(0, height, () => (localR: new float[width], localG: new float[width], localB: new float[width]),
                (y, state, local) =>
                {
                    byte* row = basePtr + (y * stride);

                    // process entire row in local buffers
                    for (int x = 0; x < width; x++)
                    {
                        int bufferIndex = x * 3;
                        // BGR byte order: +2 = R, +1 = G, +0 = B
                        // B = 0
                        // G = 1
                        // R = 2
                        // (bufferIndex + x)
                        local.localR[x] = row[bufferIndex + 2] * multiplier;
                        local.localG[x] = row[bufferIndex + 1] * multiplier;
                        local.localB[x] = row[bufferIndex] * multiplier;
                    }

                    // after processing the row copy the results into the final array
                    int rowStart = y * width;
                    Array.Copy(local.localR, 0, result, redOffset + rowStart, width);
                    Array.Copy(local.localG, 0, result, greenOffset + rowStart, width);
                    Array.Copy(local.localB, 0, result, blueOffset + rowStart, width);

                    return local;
                },
                _ => { });
            }
            finally
            {
                image.UnlockBits(bmpData);
            }
        }

        #endregion Optimized Math

        public void Dispose()
        {
            // Unsubscribe from display changes
            DisplayManager.DisplayChanged -= OnDisplayChanged;

            // Stop the loop
            _isAiLoopRunning = false;
            if (_aiLoopThread != null && _aiLoopThread.IsAlive)
            {
                if (!_aiLoopThread.Join(TimeSpan.FromSeconds(1)))
                {
                    try { _aiLoopThread.Interrupt(); }
                    catch { }
                }
            }

            // Print final benchmarks
            PrintBenchmarks();

            // Dispose DXGI objects
            _captureManager.DisposeDxgiResources();

            // Clean up other resources
            _reusableInputArray = null;
            _reusableInputs = null;
            _onnxModel?.Dispose();
            _modeloptions?.Dispose();
            _bitmapBuffer = null;
            _captureManager.screenCaptureBitmap?.Dispose();
        }

        public class Prediction
        {
            public RectangleF Rectangle { get; set; }
            public float Confidence { get; set; }
            public float CenterXTranslated { get; set; }
            public float CenterYTranslated { get; set; }
        }
    }
}