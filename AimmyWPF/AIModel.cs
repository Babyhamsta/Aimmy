using KdTree.Math;
using KdTree;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.OnnxRuntime;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace AimmyAimbot
{
    public class AIModel
    {
        private const int IMAGE_SIZE = 640;
        private const int NUM_DETECTIONS = 8400;

        private readonly RunOptions _modeloptions;
        private readonly InferenceSession _onnxModel;

        public float ConfidenceThreshold = 0.6f;
        public bool CollectData = false;
        public int FovSize = 640;


        private DateTime lastSavedTime = DateTime.MinValue;
        private readonly List<string> _outputNames;

        private readonly MemoryStream _captureStream = new MemoryStream(IMAGE_SIZE * IMAGE_SIZE * 4);
        private readonly float[] _imageArray = new float[3 * IMAGE_SIZE * IMAGE_SIZE];

        public AIModel(string modelPath)
        {
            _modeloptions = new RunOptions();

            var sessionOptions = new SessionOptions
            {
                EnableCpuMemArena = true,
                EnableMemoryPattern = true,
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                ExecutionMode = ExecutionMode.ORT_PARALLEL
            };

            try
            {
                sessionOptions.AppendExecutionProvider_DML();
                _onnxModel = new InferenceSession(modelPath, sessionOptions);
                _outputNames = _onnxModel.OutputMetadata.Keys.ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"There was an error starting the OnnxModel via DirectML: {ex}\n\nProgram will attempt to use CPU only, performance may be poor.", "Model Error");
                try
                {
                    sessionOptions.AppendExecutionProvider_CPU();
                    _onnxModel = new InferenceSession(modelPath, sessionOptions);
                    _outputNames = _onnxModel.OutputMetadata.Keys.ToList();
                }
                catch (Exception innerEx)
                {
                    MessageBox.Show($"There was an error starting the model via CPU: {innerEx}", "Model Error");
                    System.Windows.Application.Current.Shutdown();
                }
            }

            // Checking output shape
            foreach (var output in _onnxModel.OutputMetadata)
            {
                var shape = _onnxModel.OutputMetadata[output.Key].Dimensions;
                if (shape.Length != 3 || shape[0] != 1 || shape[1] != 5 || shape[2] != 8400)
                {
                    MessageBox.Show($"Output shape {string.Join("x", shape)} does not match the expected shape of 1x5x8400.\n\nThis model will not work with Aimmy, please use an ONNX V8 model.", "Model Error");
                }
            }
        }

        public class Prediction
        {
            public RectangleF Rectangle { get; set; }
            public float Confidence { get; set; }
        }

        public static Bitmap ScreenGrab(Rectangle detectionBox)
        {
            Bitmap bmp = new Bitmap(detectionBox.Width, detectionBox.Height);
            Graphics g = Graphics.FromImage(bmp);
            g.CopyFromScreen(detectionBox.Left, detectionBox.Top, 0, 0, detectionBox.Size);
            return bmp;
        }

        public static float[] BitmapToFloatArray(Bitmap image)
        {
            int height = image.Height;
            int width = image.Width;
            float[] result = new float[3 * height * width];
            Rectangle rect = new Rectangle(0, 0, width, height);
            BitmapData bmpData = image.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

            IntPtr ptr = bmpData.Scan0;
            int bytes = Math.Abs(bmpData.Stride) * height;
            byte[] rgbValues = new byte[bytes];

            Marshal.Copy(ptr, rgbValues, 0, bytes);
            Parallel.For(0, rgbValues.Length / 3, i =>
            {
                int index = i * 3;
                int counter = i;
                result[counter] = rgbValues[index + 2] / 255.0f; // R
                result[height * width + counter] = rgbValues[index + 1] / 255.0f; // G
                result[2 * height * width + counter] = rgbValues[index] / 255.0f; // B
            });

            image.UnlockBits(bmpData);

            return result;
        }

        public async Task<Prediction> GetClosestPredictionToCenterAsync()
        {

            // Define the detection box
            int halfScreenWidth = Screen.PrimaryScreen.Bounds.Width / 2;
            int halfScreenHeight = Screen.PrimaryScreen.Bounds.Height / 2;
            int detectionBoxSize = 640;
            Rectangle detectionBox = new Rectangle(halfScreenWidth - detectionBoxSize / 2,
                                                   halfScreenHeight - detectionBoxSize / 2,
                                                   detectionBoxSize,
                                                   detectionBoxSize);

            // Capture a screenshot
            Bitmap frame = ScreenGrab(detectionBox);

            // Save frame asynchronously if the option is turned on
            if (CollectData)
            {
                DateTime currentTime = DateTime.Now;
                if ((currentTime - lastSavedTime).TotalSeconds >= 0.5)
                {
                    lastSavedTime = currentTime;
                    string uuid = Guid.NewGuid().ToString();
                    await Task.Run(() => frame.Save($"bin/images/{uuid}.jpg"));
                }
            }

            // Convert the Bitmap to float array and normalize
            float[] inputArray = BitmapToFloatArray(frame);
            if (inputArray == null) { return null; }

            Tensor<float> inputTensor = new DenseTensor<float>(inputArray, new int[] { 1, 3, frame.Height, frame.Width });
            var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("images", inputTensor) };
            var results = _onnxModel.Run(inputs, _outputNames, _modeloptions);

            var outputTensor = results[0].AsTensor<float>();

            // Calculate the FOV boundaries
            float fovMinX = (IMAGE_SIZE - FovSize) / 2.0f;
            float fovMaxX = (IMAGE_SIZE + FovSize) / 2.0f;
            float fovMinY = (IMAGE_SIZE - FovSize) / 2.0f;
            float fovMaxY = (IMAGE_SIZE + FovSize) / 2.0f;

            var tree = new KdTree<float, Prediction>(2, new FloatMath());

            var filteredIndices = Enumerable.Range(0, NUM_DETECTIONS)
                                    .AsParallel()
                                    .Where(i => outputTensor[0, 4, i] >= ConfidenceThreshold)
                                    .ToList();

            object treeLock = new object();

            Parallel.ForEach(filteredIndices, i =>
            {
                float objectness = outputTensor[0, 4, i];

                float x_center = outputTensor[0, 0, i];
                float y_center = outputTensor[0, 1, i];
                float width = outputTensor[0, 2, i];
                float height = outputTensor[0, 3, i];

                float x_min = x_center - width / 2;
                float y_min = y_center - height / 2;
                float x_max = x_center + width / 2;
                float y_max = y_center + height / 2;

                if (x_min >= fovMinX && x_max <= fovMaxX && y_min >= fovMinY && y_max <= fovMaxY)
                {
                    var prediction = new Prediction
                    {
                        Rectangle = new RectangleF(x_min, y_min, x_max - x_min, y_max - y_min),
                        Confidence = objectness
                    };

                    var centerX = (x_min + x_max) / 2.0f;
                    var centerY = (y_min + y_max) / 2.0f;

                    lock (treeLock)
                    {
                        tree.Add(new[] { centerX, centerY }, prediction);
                    }
                }
            });

            // Querying the KDTree for the closest prediction to the center.
            var nodes = tree.GetNearestNeighbours(new[] { IMAGE_SIZE / 2.0f, IMAGE_SIZE / 2.0f }, 1);

            return nodes.Length > 0 ? nodes[0].Value : (Prediction?)null;
        }

        public void Dispose()
        {
            _onnxModel?.Dispose();
            _captureStream?.Dispose();
        }
    }
}
