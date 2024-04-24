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
using System.Windows;
using Visuality;

namespace Aimmy2.AILogic
{
    internal class AIManager : IDisposable
    {
        #region Variables

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

        private async void AiLoop()
        {
            Stopwatch stopwatch = new();
            DetectedPlayerWindow? DetectedPlayerOverlay = Dictionary.DetectedPlayerOverlay;

            float scaleX = ScreenWidth / 640f;
            float scaleY = ScreenHeight / 640f;

            while (_isAiLoopRunning)
            {
                stopwatch.Restart();

                if (Dictionary.dropdownState["Detection Area Type"] == "Closest to Mouse" && Dictionary.toggleState["FOV"])
                {
                    var mousePosition = WinAPICaller.GetCursorPosition();
                    await Application.Current.Dispatcher.BeginInvoke(() => Dictionary.FOVWindow.FOVStrictEnclosure.Margin = new Thickness(Convert.ToInt16(mousePosition.X / WinAPICaller.scalingFactorX) - 320, Convert.ToInt16(mousePosition.Y / WinAPICaller.scalingFactorY) - 320, 0, 0));
                }

                if (Dictionary.toggleState["Aim Assist"] || Dictionary.toggleState["Show Detected Player"] || Dictionary.toggleState["Auto Trigger"])
                {
                    if (Dictionary.toggleState["Show Detected Player"] || InputBindingManager.IsHoldingBinding("Aim Keybind") || Dictionary.toggleState["Constant AI Tracking"] || InputBindingManager.IsHoldingBinding("Second Aim Keybind"))
                    {
                        var closestPrediction = await GetClosestPrediction();

                        if (closestPrediction == null)
                        {
                            if (Dictionary.toggleState["Show Detected Player"] && Dictionary.DetectedPlayerOverlay != null)
                            {
                                DisableOverlay(DetectedPlayerOverlay!);
                            }
                            continue;
                        }

                        if (Dictionary.toggleState["Auto Trigger"] && (InputBindingManager.IsHoldingBinding("Aim Keybind") || Dictionary.toggleState["Constant AI Tracking"]))
                        {
                            await MouseManager.DoTriggerClick();
                            if (!Dictionary.toggleState["Aim Assist"] && !Dictionary.toggleState["Show Detected Player"]) continue;
                        }

                        AIConf = closestPrediction.Confidence;

                        if (Dictionary.toggleState["Show Detected Player"] && Dictionary.DetectedPlayerOverlay != null)
                        {
                            UpdateOverlay(DetectedPlayerOverlay!);
                            if (!Dictionary.toggleState["Aim Assist"]) continue;
                        }

                        double YOffset = Dictionary.sliderSettings["Y Offset (Up/Down)"];
                        double XOffset = Dictionary.sliderSettings["X Offset (Left/Right)"];

                        double YOffsetPercentage = Dictionary.sliderSettings["Y Offset (%)"];
                        double XOffsetPercentage = Dictionary.sliderSettings["X Offset (%)"];

                        if (Dictionary.toggleState["X Axis Percentage Adjustment"])
                            detectedX = (int)((closestPrediction.Rectangle.X + (closestPrediction.Rectangle.Width * (XOffsetPercentage / 100))) * scaleX);
                        else
                            detectedX = (int)((closestPrediction.Rectangle.X + closestPrediction.Rectangle.Width / 2) * scaleX + XOffset);

                        if (Dictionary.toggleState["Y Axis Percentage Adjustment"])
                            detectedY = (int)(((closestPrediction.Rectangle.Y + closestPrediction.Rectangle.Height) - (closestPrediction.Rectangle.Height * (YOffsetPercentage / 100))) * scaleY + YOffset);
                        else
                            detectedY = CalculateDetectedY(scaleY, YOffset, closestPrediction);

                        if (Dictionary.dropdownState["Prediction Method"] == "Shall0e's Prediction")
                        {
                            UpdateShalloePrediction();
                        }
                        
                        if (Dictionary.toggleState["Aim Assist"] && (Dictionary.toggleState["Constant AI Tracking"] || InputBindingManager.IsHoldingBinding("Aim Keybind") || InputBindingManager.IsHoldingBinding("Second Aim Keybind")))
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
                        totalTime += stopwatch.ElapsedMilliseconds;
                        iterationCount++;
                    }

                    stopwatch.Stop();

                    if (iterationCount == 1000)
                    {
                        double averageTime = totalTime / 1000.0;
                        Debug.WriteLine($"Average loop iteration time: {averageTime} ms");
                        //MessageBox.Show($"Average loop iteration time: {averageTime} ms (per 1000 loops)");
                    }
                }

                await Task.Delay(1); // Add a small delay to avoid high CPU usage
            }
        }

        private static int CalculateDetectedY(float scaleY, double YOffset, Prediction closestPrediction)
        {
            switch (Dictionary.dropdownState["Aiming Boundaries Alignment"])
            {
                case "Center":
                    return (int)((closestPrediction.Rectangle.Y + closestPrediction.Rectangle.Height / 2) * scaleY + YOffset);

                case "Top":
                    return (int)(closestPrediction.Rectangle.Y * scaleY + YOffset);

                case "Bottom":
                    return (int)((closestPrediction.Rectangle.Y + closestPrediction.Rectangle.Height) * scaleY + YOffset);

                default:
                    return 0;
            }
        }

        private void UpdateShalloePrediction()
        {
            ShalloePredictionV2.xValues.Add(detectedX - PrevX);
            ShalloePredictionV2.yValues.Add(detectedY - PrevY);

            if (ShalloePredictionV2.xValues.Count > 5)
                ShalloePredictionV2.xValues.RemoveAt(0);

            if (ShalloePredictionV2.yValues.Count > 5)
                ShalloePredictionV2.yValues.RemoveAt(0);
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

        private static void DisableOverlay(DetectedPlayerWindow DetectedPlayerOverlay)
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
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
            }));
        }

        private void UpdateOverlay(DetectedPlayerWindow DetectedPlayerOverlay)
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                if (Dictionary.toggleState["Show AI Confidence"])
                {
                    DetectedPlayerOverlay.DetectedPlayerConfidence.Opacity = 1;
                    DetectedPlayerOverlay.DetectedPlayerConfidence.Content = $"{Math.Round((AIConf * 100), 2)}%";

                    double centerX = Convert.ToInt16(LastDetectionBox.X / WinAPICaller.scalingFactorX) + (LastDetectionBox.Width / 2.0);
                    double labelEstimatedHalfWidth = DetectedPlayerOverlay.DetectedPlayerConfidence.ActualWidth / 2.0;
                    DetectedPlayerOverlay.DetectedPlayerConfidence.Margin = new Thickness(centerX - labelEstimatedHalfWidth, Convert.ToInt16(LastDetectionBox.Y / WinAPICaller.scalingFactorY) - DetectedPlayerOverlay.DetectedPlayerConfidence.ActualHeight - 2, 0, 0);
                }

                if (Dictionary.toggleState["Show Tracers"])
                {
                    DetectedPlayerOverlay.DetectedTracers.Opacity = 1;
                    DetectedPlayerOverlay.DetectedTracers.X2 = Convert.ToInt16(LastDetectionBox.X / WinAPICaller.scalingFactorX) + (LastDetectionBox.Width / 2);
                    DetectedPlayerOverlay.DetectedTracers.Y2 = Convert.ToInt16(LastDetectionBox.Y / WinAPICaller.scalingFactorY) + LastDetectionBox.Height;
                }
                else if (!Dictionary.toggleState["Show Tracers"])
                {
                    DetectedPlayerOverlay.DetectedTracers.Opacity = 0;
                }

                DetectedPlayerOverlay.Opacity = Dictionary.sliderSettings["Opacity"];

                DetectedPlayerOverlay.DetectedPlayerFocus.Opacity = 1;
                DetectedPlayerOverlay.DetectedPlayerFocus.Margin = new Thickness(Convert.ToInt16(LastDetectionBox.X / WinAPICaller.scalingFactorX),
                    Convert.ToInt16(LastDetectionBox.Y / WinAPICaller.scalingFactorY),
                    0, 0);
                DetectedPlayerOverlay.DetectedPlayerFocus.Width = LastDetectionBox.Width;
                DetectedPlayerOverlay.DetectedPlayerFocus.Height = LastDetectionBox.Height;
            }));
        }

        private async Task<Prediction?> GetClosestPrediction(bool useMousePosition = true)
        {
            targetX = Dictionary.dropdownState["Detection Area Type"] == "Closest to Mouse" ? WinAPICaller.GetCursorPosition().X : ScreenWidth / 2;
            targetY = Dictionary.dropdownState["Detection Area Type"] == "Closest to Mouse" ? WinAPICaller.GetCursorPosition().Y : ScreenHeight / 2;

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
            float FovSize = (float)Dictionary.sliderSettings["FOV Size"];
            float fovMinX = (IMAGE_SIZE - FovSize) / 2.0f;
            float fovMaxX = (IMAGE_SIZE + FovSize) / 2.0f;
            float fovMinY = (IMAGE_SIZE - FovSize) / 2.0f;
            float fovMaxY = (IMAGE_SIZE + FovSize) / 2.0f;

            var (KDpoints, KDPredictions) = PrepareKDTreeData(outputTensor, detectionBox, fovMinX, fovMaxX, fovMinY, fovMaxY);

            if (KDpoints.Count == 0 || KDPredictions.Count == 0)
            {
                return null;
            }

            var tree = new KDTree<double, Prediction>(2, [.. KDpoints], [.. KDPredictions], L2Norm_Squared_Double);

            var nearest = tree.NearestNeighbors(new double[] { IMAGE_SIZE / 2.0, IMAGE_SIZE / 2.0 }, 1);

            if (nearest != null && nearest.Length > 0)
            {
                // Translate coordinates
                float translatedXMin = nearest[0].Item2.Rectangle.X + detectionBox.Left;
                float translatedYMin = nearest[0].Item2.Rectangle.Y + detectionBox.Top;
                LastDetectionBox = new RectangleF(translatedXMin, translatedYMin, nearest[0].Item2.Rectangle.Width, nearest[0].Item2.Rectangle.Height);

                CenterXTranslated = nearest[0].Item2.CenterXTranslated;
                CenterYTranslated = nearest[0].Item2.CenterYTranslated;

                // Moved SaveFrameAsync over here to get accurate Prediction Labelling
                await SaveFrameAsync(frame, nearest[0].Item2);

                return nearest[0].Item2;
            }
            else if (Dictionary.toggleState["Collect Data While Playing"] && !Dictionary.toggleState["Constant AI Tracking"] && !Dictionary.toggleState["Auto Label Data"])
            {
                await SaveFrameAsync(frame, null); // Save the frame without a prediction for the people without pre-existing models. Since people complained about this...
            }

            return null;
        }

        private (List<double[]>, List<Prediction>) PrepareKDTreeData(Tensor<float> outputTensor, Rectangle detectionBox, float fovMinX, float fovMaxX, float fovMinY, float fovMaxY)
        {
            float minConfidence = (float)Dictionary.sliderSettings["AI Minimum Confidence"] / 100.0f; // Pre-compute minimum confidence

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

                KDpoints.Add([x_center, y_center]);
                KDpredictions.Add(prediction);
            }

            return (KDpoints, KDpredictions);
        }

        #endregion AI

        #region Screen Capture

        private async Task SaveFrameAsync(Bitmap frame, Prediction? DoLabel)
        {
            if (Dictionary.toggleState["Collect Data While Playing"] && !Dictionary.toggleState["Constant AI Tracking"])
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

                            if (Dictionary.toggleState["Auto Label Data"] && DoLabel != null)
                            {
                                var labelPath = Path.Combine("bin", "labels", $"{uuid}.txt");

                                float x = (DoLabel.Rectangle.X + DoLabel.Rectangle.Width / 2) / frame.Width;
                                float y = (DoLabel.Rectangle.Y + DoLabel.Rectangle.Height / 2) / frame.Height;
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
            public RectangleF Rectangle { get; set; }
            public float Confidence { get; set; }
            public float CenterXTranslated { get; set; }
            public float CenterYTranslated { get; set; }
        }
    }
}
