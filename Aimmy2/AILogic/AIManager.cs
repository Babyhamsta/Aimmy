using AILogic;
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
using System.Windows;
using Aimmy2.Types;
using Visuality;
using Aimmy2.Config;

namespace Aimmy2.AILogic
{
    internal class AIManager : IDisposable
    {
        #region Variables

        public bool IsModelLoaded { get; private set; }
        public bool Paused { get; set; }

        private const int IMAGE_SIZE = 640;
        private const int NUM_DETECTIONS = 8400; // Standard for OnnxV8 model (Shape: 1x5x8400)

        private DateTime lastSavedTime = DateTime.MinValue;
        private List<string>? _outputNames;
        private RectangleF LastDetectionBox;
        private KalmanPrediction kalmanPrediction;
        private WiseTheFoxPrediction wtfpredictionManager;
        private Bitmap? _screenCaptureBitmap;

        private readonly int ScreenWidth = WinAPICaller.ScreenWidth;
        private readonly int ScreenHeight = WinAPICaller.ScreenHeight;

        private readonly RunOptions? _modeloptions;
        private InferenceSession? _onnxModel;

        private Thread? _aiLoopThread;
        private bool _isAiLoopRunning;

        // For Auto-Labelling Data System
        private bool PlayerFound = false;

        private double CenterXTranslated = 0;
        private double CenterYTranslated = 0;

        // For Shall0e's Prediction Method
        private int PrevX = 0;
        private int PrevY = 0;
        private int IndependentMousePress = 0;
        private int iterationCount = 0;
        private long totalTime = 0;

        private int detectedX { get; set; }
        private int detectedY { get; set; }

        public double AIConf = 0;
        private static int targetX, targetY;

        private Graphics? _graphics;

        internal RelativeRect HeadRelativeRect = RelativeRect.Default;

        #endregion Variables

        public AIManager(string modelPath)
        {
            kalmanPrediction = new KalmanPrediction();
            wtfpredictionManager = new WiseTheFoxPrediction();
            _modeloptions = new RunOptions();

            var sessionOptions = new SessionOptions
            {
                EnableCpuMemArena = true,
                EnableMemoryPattern = true,
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                ExecutionMode = ExecutionMode.ORT_PARALLEL
            };

            // Attempt to load via DirectML (else fallback to CPU)
            Application.Current.Dispatcher.BeginInvoke(() => InitializeModel(sessionOptions, modelPath));
            Console.WriteLine($"AIManager Initialized with {modelPath}");
        }

        #region Models

        private async Task InitializeModel(SessionOptions sessionOptions, string modelPath)
        {
            try
            {
                await LoadModelAsync(sessionOptions, modelPath, useDirectML: true);
            }
            catch (Exception ex)
            {
                await Application.Current.Dispatcher.BeginInvoke(new Action(() => new NoticeBar($"Error starting the model via DirectML: {ex.Message}\n\nFalling back to CPU, performance may be poor.", 5000).Show()));
                try
                {
                    await LoadModelAsync(sessionOptions, modelPath, useDirectML: false);
                }
                catch (Exception e)
                {
                    await Application.Current.Dispatcher.BeginInvoke(new Action(() => new NoticeBar($"Error starting the model via CPU: {e.Message}, you won't be able to aim assist at all.", 5000).Show()));
                }
            }

            FileManager.CurrentlyLoadingModel = false;
        }

        private async Task LoadModelAsync(SessionOptions sessionOptions, string modelPath, bool useDirectML)
        {
            try
            {
                if (useDirectML) sessionOptions.AppendExecutionProvider_DML();
                else sessionOptions.AppendExecutionProvider_CPU();

                _onnxModel = new InferenceSession(modelPath, sessionOptions);
                _outputNames = new List<string>(_onnxModel.OutputMetadata.Keys);

                // Validate the onnx model output shape (ensure model is OnnxV8)
                ValidateOnnxShape();
            }
            catch (Exception ex)
            {
                await Application.Current.Dispatcher.BeginInvoke(new Action(() => new NoticeBar($"Error starting the model: {ex.Message}", 5000).Show()));
                _onnxModel?.Dispose();
            }

            IsModelLoaded = true;

            var w = Application.Current.MainWindow as MainWindow;
            w.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (AppConfig.Current.ToggleState.GlobalActive)
                    w.SetActive(true);
                w.CallPropertyChanged(nameof(w.IsModelLoaded));
            }));
            // Begin the loop
            _isAiLoopRunning = true;
            _aiLoopThread = new Thread(AiLoop);
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

        private static bool ShouldPredict() => AppConfig.Current.ToggleState.ShowDetectedPlayer
                                               || AppConfig.Current.ToggleState.ConstantAITracking
                                               || InputBindingManager.IsHoldingBinding(nameof(AppConfig.Current.BindingSettings.AimKeybind))
                                               || InputBindingManager.IsHoldingBinding(nameof(AppConfig.Current.BindingSettings.SecondAimKeybind))
                                               || AppConfig.Current.ToggleState.AutoTrigger;

        private static bool ShouldProcess() => AppConfig.Current.ToggleState.AimAssist
                                               || AppConfig.Current.ToggleState.ShowDetectedPlayer
                                               || AppConfig.Current.ToggleState.AutoTrigger;

        private async void AiLoop()
        {
            AppConfig.Current.ToggleState.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName is nameof(AppConfig.Current.ToggleState.GlobalActive) or nameof(AppConfig.Current.ToggleState.AutoTriggerCharged))
                {
                    if (!AppConfig.Current.ToggleState.GlobalActive || !AppConfig.Current.ToggleState.AutoTriggerCharged)
                        CancelTriggerChargeIf();
                }
            };

            Stopwatch stopwatch = new();
            DetectedPlayerWindow? DetectedPlayerOverlay = AppConfig.Current.DetectedPlayerOverlay;

            float scaleX = ScreenWidth / 640f;
            float scaleY = ScreenHeight / 640f;

            while (_isAiLoopRunning)
            {
                if (AppConfig.Current.ToggleState.GlobalActive)
                {
                    stopwatch.Restart();

                    UpdateFOV(); // Organization/Simplification of AILoop inspired/helped by @.harlans or @apraxo on github.

                    if (ShouldProcess())
                    {
                        if (ShouldPredict())
                        {
                            var closestPrediction = await GetClosestPrediction();

                            if (closestPrediction == null)
                            {
                                DisableOverlay(DetectedPlayerOverlay!);

                                continue;
                            }

                            await AutoTrigger(closestPrediction);

                            CalculateCoordinates(DetectedPlayerOverlay, closestPrediction, scaleX, scaleY);

                            HandleAim(closestPrediction);

                            totalTime += stopwatch.ElapsedMilliseconds;
                            iterationCount++;
                        }

                        stopwatch.Stop();
                    }
                }
                else
                {
                    DisableOverlay(DetectedPlayerOverlay!);
                }

                await Task.Delay(1); // Add a small delay to avoid high CPU usage
            }
        }

        #region AI Loop Functions

        private bool TriggerKeyUnsetOrHold()
        {
            var triggerKey = AppConfig.Current.BindingSettings.TriggerKey;
            return string.IsNullOrEmpty(triggerKey) || triggerKey == "None" || InputBindingManager.IsHoldingBindingFor(nameof(AppConfig.Current.BindingSettings.TriggerKey), TimeSpan.FromSeconds(AppConfig.Current.SliderSettings.TriggerKeyMin));
        }

        private async Task<bool> PredictionIsIntersecting(Prediction? prediction = null)
        {
            prediction ??= await GetClosestPrediction();
            if (prediction == null)
                return false;

            return AppConfig.Current.DropdownState.TriggerCheck == TriggerCheck.None
                   || (AppConfig.Current.DropdownState.TriggerCheck == TriggerCheck.HeadIntersectingCenter && prediction.IsUpperMiddleIntersectingCenter)
                   || (AppConfig.Current.DropdownState.TriggerCheck == TriggerCheck.IntersectingCenter && prediction.InteractsWithCenterOfFov);
        }

        private CancellationTokenSource? _autoTriggerCts;

        private void CancelTriggerChargeIf()
        {
            if (_autoTriggerCts is { IsCancellationRequested: false })
                _autoTriggerCts.Cancel();
        }
        private static object _lock = new();
        private async Task AutoTrigger(Prediction prediction)
        {
            if (AppConfig.Current.ToggleState.AutoTrigger)
            {
                var delay = TimeSpan.FromSeconds(AppConfig.Current.SliderSettings.AutoTriggerDelay);
                if (AppConfig.Current.ToggleState.AutoTriggerCharged)
                {
                    // JUST FOR TESTING
                    if (!MouseManager.IsLeftDown && _autoTriggerCts == null)
                    {
                        _autoTriggerCts = new CancellationTokenSource();
                        _autoTriggerCts.Token.Register(() => _autoTriggerCts = null);
                        _ = MouseManager.LeftDownUntil(async () => TriggerKeyUnsetOrHold() && await PredictionIsIntersecting(), delay, _autoTriggerCts.Token).ContinueWith(_ => CancelTriggerChargeIf());
                    }
                    return;
                }
                if (TriggerKeyUnsetOrHold())
                {
                    //await MouseManager.LeftDownUntil(() => PredictionIsIntersecting());
                    //return;
                    if (await PredictionIsIntersecting(prediction))
                    {
                        await Task.Delay(delay);
                        if (InputBindingManager.IsValidKey(AppConfig.Current.BindingSettings.TriggerAdditionalSend))
                        {
                            InputBindingManager.SendKey(AppConfig.Current.BindingSettings.TriggerAdditionalSend);
                        }
                        await MouseManager.DoTriggerClick();
                    }
                }
            }
        }


        private async void UpdateFOV()
        {
            if (AppConfig.Current.DropdownState.DetectionAreaType == DetectionAreaType.ClosestToMouse && AppConfig.Current.ToggleState.FOV)
            {
                var mousePosition = WinAPICaller.GetCursorPosition();
                await Application.Current.Dispatcher.BeginInvoke(() => AppConfig.Current.FOVWindow.FOVStrictEnclosure.Margin = new Thickness(Convert.ToInt16(mousePosition.X / WinAPICaller.scalingFactorX) - 320, Convert.ToInt16(mousePosition.Y / WinAPICaller.scalingFactorY) - 320, 0, 0));
            }
        }

        private void DisableOverlay(DetectedPlayerWindow? playerOverlay)
        {
            if (playerOverlay == null || !_isAiLoopRunning)
                return;

            if (AppConfig.Current.ToggleState.ShowDetectedPlayer && AppConfig.Current.DetectedPlayerOverlay != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (AppConfig.Current.ToggleState.ShowAIConfidence)
                    {
                        playerOverlay.DetectedPlayerConfidence.Opacity = 0;
                    }

                    if (AppConfig.Current.ToggleState.ShowTracers)
                    {
                        playerOverlay!.DetectedTracers.Opacity = 0;
                    }

                    playerOverlay!.DetectedPlayerFocus.Opacity = 0;
                });
            }
        }

        private void UpdateOverlay(DetectedPlayerWindow? playerOverlay)
        {
            if (playerOverlay == null || !_isAiLoopRunning)
                return;
            Application.Current.Dispatcher.Invoke(() =>
            {
                var scalingFactorX = WinAPICaller.scalingFactorX;
                var scalingFactorY = WinAPICaller.scalingFactorY;
                var centerX = Convert.ToInt16(LastDetectionBox.X / scalingFactorX) + (LastDetectionBox.Width / 2.0);
                var centerY = Convert.ToInt16(LastDetectionBox.Y / scalingFactorY);

                if (AppConfig.Current.ToggleState.ShowAIConfidence)
                {
                    playerOverlay.DetectedPlayerConfidence.Opacity = 1;
                    playerOverlay.DetectedPlayerConfidence.Content = $"{Math.Round((AIConf * 100), 2)}%";

                    var labelEstimatedHalfWidth = playerOverlay.DetectedPlayerConfidence.ActualWidth / 2.0;
                    playerOverlay.DetectedPlayerConfidence.Margin = new Thickness(centerX - labelEstimatedHalfWidth, centerY - playerOverlay.DetectedPlayerConfidence.ActualHeight - 2, 0, 0);
                }

                var showTracers = AppConfig.Current.ToggleState.ShowTracers;
                playerOverlay.DetectedTracers.Opacity = showTracers ? 1 : 0;
                if (showTracers)
                {
                    playerOverlay.DetectedTracers.X2 = centerX;
                    playerOverlay.DetectedTracers.Y2 = centerY + LastDetectionBox.Height;
                }

                playerOverlay.Opacity = AppConfig.Current.SliderSettings.Opacity;

                playerOverlay.DetectedPlayerFocus.Opacity = 1;
                playerOverlay.DetectedPlayerFocus.Margin = new Thickness(centerX - (LastDetectionBox.Width / 2.0), centerY, 0, 0);
                playerOverlay.DetectedPlayerFocus.Width = LastDetectionBox.Width;
                playerOverlay.DetectedPlayerFocus.Height = LastDetectionBox.Height;

                playerOverlay.SetHeadRelativeArea(AppConfig.Current.ToggleState.ShowTriggerHeadArea ? HeadRelativeRect : null);
            });
        }

        private void CalculateCoordinates(DetectedPlayerWindow DetectedPlayerOverlay, Prediction closestPrediction, float scaleX, float scaleY)
        {
            AIConf = closestPrediction.Confidence;

            if (AppConfig.Current.ToggleState.ShowDetectedPlayer && AppConfig.Current.DetectedPlayerOverlay != null)
            {
                UpdateOverlay(DetectedPlayerOverlay!);
                if (!AppConfig.Current.ToggleState.AimAssist) return;
            }

            double YOffset = AppConfig.Current.SliderSettings.YOffset;
            double XOffset = AppConfig.Current.SliderSettings.XOffset;

            double YOffsetPercentage = AppConfig.Current.SliderSettings.YOffsetPercentage;
            double XOffsetPercentage = AppConfig.Current.SliderSettings.XOffsetPercentage;

            var rect = closestPrediction.Rectangle;

            if (AppConfig.Current.ToggleState.XAxisPercentageAdjustment)
            {
                detectedX = (int)((rect.X + (rect.Width * (XOffsetPercentage / 100))) * scaleX);
            }
            else
            {
                detectedX = (int)((rect.X + rect.Width / 2) * scaleX + XOffset);
            }

            if (AppConfig.Current.ToggleState.YAxisPercentageAdjustment)
            {
                detectedY = (int)((rect.Y + rect.Height - (rect.Height * (YOffsetPercentage / 100))) * scaleY + YOffset);
            }
            else
            {
                detectedY = CalculateDetectedY(scaleY, YOffset, closestPrediction);
            }
        }

        private static int CalculateDetectedY(float scaleY, double YOffset, Prediction closestPrediction)
        {
            var rect = closestPrediction.Rectangle;
            float yBase = rect.Y;
            float yAdjustment = 0;

            switch (AppConfig.Current.DropdownState.AimingBoundariesAlignment)
            {
                case AimingBoundariesAlignment.Center:
                    yAdjustment = rect.Height / 2;
                    break;

                case AimingBoundariesAlignment.Top:
                    // yBase is already at the top
                    break;

                case AimingBoundariesAlignment.Bottom:
                    yAdjustment = rect.Height;
                    break;
            }

            return (int)((yBase + yAdjustment) * scaleY + YOffset);
        }

        private void HandleAim(Prediction closestPrediction)
        {
            if (AppConfig.Current.ToggleState.AimAssist && (AppConfig.Current.ToggleState.ConstantAITracking
                || AppConfig.Current.ToggleState.AimAssist && InputBindingManager.IsHoldingBinding(nameof(AppConfig.Current.BindingSettings.AimKeybind))
                || AppConfig.Current.ToggleState.AimAssist && InputBindingManager.IsHoldingBinding(nameof(AppConfig.Current.BindingSettings.SecondAimKeybind))))
            {
                if (AppConfig.Current.ToggleState.Predictions)
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
            var predictionMethod = AppConfig.Current.DropdownState.PredictionMethod;
            switch (predictionMethod)
            {
                case PredictionMethod.KalmanFilter:
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

                case PredictionMethod.Shall0:
                    ShalloePredictionV2.xValues.Add(detectedX - PrevX);
                    ShalloePredictionV2.yValues.Add(detectedY - PrevY);

                    ShalloePredictionV2.xValues = ShalloePredictionV2.xValues.TakeLast(5).ToList();
                    ShalloePredictionV2.yValues = ShalloePredictionV2.yValues.TakeLast(5).ToList();

                    MouseManager.MoveCrosshair(ShalloePredictionV2.GetSPX(), detectedY);

                    PrevX = detectedX;
                    PrevY = detectedY;
                    break;

                case PredictionMethod.WiseThef0x:
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

        private async Task<Prediction?> GetClosestPrediction()
        {
            lock (_lock)
            {
                targetX = AppConfig.Current.DropdownState.DetectionAreaType == DetectionAreaType.ClosestToMouse ? WinAPICaller.GetCursorPosition().X : ScreenWidth / 2;
                targetY = AppConfig.Current.DropdownState.DetectionAreaType == DetectionAreaType.ClosestToMouse ? WinAPICaller.GetCursorPosition().Y : ScreenHeight / 2;

                Rectangle detectionBox = new(targetX - IMAGE_SIZE / 2, targetY - IMAGE_SIZE / 2, IMAGE_SIZE, IMAGE_SIZE);

                Bitmap? frame = ScreenGrab(detectionBox);
                if (frame == null) return null;

                float[] inputArray = BitmapToFloatArray(frame);
                if (inputArray == null) return null;

                Tensor<float> inputTensor = new DenseTensor<float>(inputArray, new int[] { 1, 3, frame.Height, frame.Width });
                var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("images", inputTensor) };
                if (_onnxModel == null) return null;
                var results = _onnxModel.Run(inputs, _outputNames, _modeloptions);

                var outputTensor = results[0].AsTensor<float>();

                // Calculate the FOV boundaries
                float FovSize = (float)AppConfig.Current.SliderSettings.FOVSize;
                float fovMinX = (IMAGE_SIZE - FovSize) / 2.0f;
                float fovMaxX = (IMAGE_SIZE + FovSize) / 2.0f;
                float fovMinY = (IMAGE_SIZE - FovSize) / 2.0f;
                float fovMaxY = (IMAGE_SIZE + FovSize) / 2.0f;

                var (KDpoints, KDPredictions) = PrepareKDTreeData(outputTensor, detectionBox, fovMinX, fovMaxX, fovMinY, fovMaxY);

                if (KDpoints.Count == 0 || KDPredictions.Count == 0)
                {
                    return null;
                }

                var tree = new KDTree<double, Prediction>(2, KDpoints.ToArray(), KDPredictions.ToArray(), L2Norm_Squared_Double);

                var nearest = tree.NearestNeighbors(new double[] { IMAGE_SIZE / 2.0, IMAGE_SIZE / 2.0 }, 1);

                if (nearest != null && nearest.Length > 0)
                {
                    // Translate coordinates
                    float translatedXMin = nearest[0].Item2.Rectangle.X + detectionBox.Left;
                    float translatedYMin = nearest[0].Item2.Rectangle.Y + detectionBox.Top;
                    LastDetectionBox = new RectangleF(translatedXMin, translatedYMin, nearest[0].Item2.Rectangle.Width, nearest[0].Item2.Rectangle.Height);

                    CenterXTranslated = nearest[0].Item2.CenterXTranslated;
                    CenterYTranslated = nearest[0].Item2.CenterYTranslated;


                    RectangleF predictionRect = nearest[0].Item2.Rectangle;
                    nearest[0].Item2.InteractsWithCenterOfFov = IsIntersectingCenter(predictionRect);


                    // Check if the upper middle part of the object intersects the center of the FOV
                    nearest[0].Item2.IsUpperMiddleIntersectingCenter = IsUpperMiddleIntersectingCenter(predictionRect, HeadRelativeRect);


                    // Moved SaveFrameAsync over here to get accurate Prediction Labelling
                    _ = SaveFrameAsync(frame, nearest[0].Item2);

                    return nearest[0].Item2;
                }

                if (AppConfig.Current.ToggleState.CollectDataWhilePlaying && !AppConfig.Current.ToggleState.ConstantAITracking && !AppConfig.Current.ToggleState.AutoLabelData)
                {
                    _ = SaveFrameAsync(frame, null); // Save the frame without a prediction for the people without pre-existing models. Since people complained about this...
                }

                return null;
            }
        }

        private bool IsUpperMiddleIntersectingCenter(RectangleF rect, RelativeRect relativeRect)
        {
            float centerX = IMAGE_SIZE / 2.0f;
            float centerY = IMAGE_SIZE / 2.0f;

            // Calculate the size and position of the relative rectangle
            float relativeWidth = rect.Width * relativeRect.WidthPercentage;
            float relativeHeight = rect.Height * relativeRect.HeightPercentage;
            float leftMargin = rect.Width * relativeRect.LeftMarginPercentage;
            float topMargin = rect.Height * relativeRect.TopMarginPercentage;

            float relativeX = rect.X + leftMargin;
            float relativeY = rect.Y + topMargin;

            RectangleF relativeRectF = new RectangleF(relativeX, relativeY, relativeWidth, relativeHeight);

            return relativeRectF.Left <= centerX && relativeRectF.Right >= centerX &&
                   relativeRectF.Top <= centerY && relativeRectF.Bottom >= centerY;
        }

        private bool IsIntersectingCenter(RectangleF rect)
        {
            float centerX = IMAGE_SIZE / 2.0f;
            float centerY = IMAGE_SIZE / 2.0f;

            return rect.Left <= centerX && rect.Right >= centerX &&
                   rect.Top <= centerY && rect.Bottom >= centerY;
        }

        private (List<double[]>, List<Prediction>) PrepareKDTreeData(Tensor<float> outputTensor, Rectangle detectionBox, float fovMinX, float fovMaxX, float fovMinY, float fovMaxY)
        {
            float minConfidence = (float)AppConfig.Current.SliderSettings.AIMinimumConfidence / 100.0f; // Pre-compute minimum confidence

            var KDpoints = new List<double[]>();
            var KDpredictions = new List<Prediction>();

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

        private async Task SaveFrameAsync(Bitmap frame, Prediction? DoLabel)
        {
            if (AppConfig.Current.ToggleState.CollectDataWhilePlaying && !AppConfig.Current.ToggleState.ConstantAITracking)
            {
                if ((DateTime.Now - lastSavedTime).TotalMilliseconds >= 500)
                {
                    lastSavedTime = DateTime.Now;
                    string uuid = Guid.NewGuid().ToString();

                    try
                    {
                        await Task.Run(() =>
                        {
                            frame.Save(Path.Combine("bin", "images", $"{uuid}.jpg"));

                            if (AppConfig.Current.ToggleState.AutoLabelData && DoLabel != null)
                            {
                                var labelPath = Path.Combine("bin", "labels", $"{uuid}.txt");

                                float x = (DoLabel!.Rectangle.X + DoLabel.Rectangle.Width / 2) / frame.Width;
                                float y = (DoLabel!.Rectangle.Y + DoLabel.Rectangle.Height / 2) / frame.Height;
                                float width = DoLabel.Rectangle.Width / frame.Width;
                                float height = DoLabel.Rectangle.Height / frame.Height;

                                File.WriteAllText(labelPath, $"0 {x} {y} {width} {height}");
                            }
                        });
                    }
                    catch (Exception e)
                    {
                        new NoticeBar($"Collect Data isn't working, try again later. {e.Message}", 6000).Show();
                    }
                }
            }
        }

        public Bitmap? ScreenGrab(Rectangle detectionBox)
        {
            if (_graphics == null || _screenCaptureBitmap == null || _screenCaptureBitmap.Width != detectionBox.Width || _screenCaptureBitmap.Height != detectionBox.Height)
            {
                _screenCaptureBitmap?.Dispose();
                _screenCaptureBitmap = new Bitmap(detectionBox.Width, detectionBox.Height);

                _graphics?.Dispose();
                _graphics = Graphics.FromImage(_screenCaptureBitmap);
            }

            _graphics.CopyFromScreen(detectionBox.Left, detectionBox.Top, 0, 0, detectionBox.Size);

            return _screenCaptureBitmap;
        }

        #endregion Screen Capture

        #region complicated math

        public static Func<double[], double[], double> L2Norm_Squared_Double = (x, y) =>
        {
            double dist = 0f;
            for (int i = 0; i < x.Length; i++)
            {
                dist += (x[i] - y[i]) * (x[i] - y[i]);
            }

            return dist;
        };

        public static float[] BitmapToFloatArray(Bitmap image)
        {
            int height = image.Height;
            int width = image.Width;
            float[] result = new float[3 * height * width];
            float multiplier = 1.0f / 255.0f;

            Rectangle rect = new(0, 0, width, height);
            BitmapData bmpData = image.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

            int stride = bmpData.Stride;
            int offset = stride - width * 3;

            try
            {
                unsafe
                {
                    byte* ptr = (byte*)bmpData.Scan0.ToPointer();
                    int baseIndex = 0;
                    for (int i = 0; i < height; i++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            result[baseIndex] = ptr[2] * multiplier; // R
                            result[height * width + baseIndex] = ptr[1] * multiplier; // G
                            result[2 * height * width + baseIndex] = ptr[0] * multiplier; // B
                            ptr += 3;
                            baseIndex++;
                        }
                        ptr += offset;
                    }
                }
            }
            finally
            {
                image.UnlockBits(bmpData);
            }

            return result;
        }

        #endregion complicated math

        public void Dispose()
        {
            // Stop the loop
            _isAiLoopRunning = false;
            if (_aiLoopThread != null && _aiLoopThread.IsAlive)
            {
                if (!_aiLoopThread.Join(TimeSpan.FromSeconds(1)))
                {
                    Debug.WriteLine("AIManager: Thread didn't join in 1 second...");
                    _aiLoopThread.Interrupt(); // Force join the thread (may error..)
                }
            }

            _screenCaptureBitmap?.Dispose();
            _graphics?.Dispose();
            _onnxModel?.Dispose();
            _modeloptions?.Dispose();
        }

        public class Prediction
        {
            public bool IsUpperMiddleIntersectingCenter { get; set; }
            public bool InteractsWithCenterOfFov { get; set; }
            public RectangleF Rectangle { get; set; }
            public float Confidence { get; set; }
            public float CenterXTranslated { get; set; }
            public float CenterYTranslated { get; set; }
        }

    }
}
