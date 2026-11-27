using Microsoft.ML.OnnxRuntime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Buffers;
using System.Drawing;
using System.Numerics;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Aimmy2.AILogic
{
    internal static class PredictionFilter
    {
        internal const float DefaultNmsIou = .55f;

        // Sized when binding the model. Results are borrowed until the next decode.
        internal sealed class FrameBuffer
        {
            internal readonly List<Prediction> Results;
            internal readonly Prediction[] Boxes;
            internal readonly string[] ClassNames;
            internal readonly int[] Order;

            internal FrameBuffer(int detections, int classes, IReadOnlyDictionary<int, string> names)
            {
                Results = new(detections);
                Boxes = new Prediction[detections];
                for (int i = 0; i < Boxes.Length; i++) Boxes[i] = new Prediction();
                Order = new int[detections];
                ClassNames = new string[classes];
                for (int i = 0; i < classes; i++)
                    ClassNames[i] = names.TryGetValue(i, out var name) ? name : $"Class_{i}";
            }
        }

        internal static List<Prediction> CreatePredictions(
            TensorBase outputTensor,
            Rectangle detectionBox,
            int imageSize,
            int numDetections,
            int numClasses,
            IReadOnlyDictionary<int, string> modelClasses,
            float minConfidence,
            string selectedClass,
            float fovMinX,
            float fovMaxX,
            float fovMinY,
            float fovMaxY,
            bool clipToBounds = false,
            Rectangle? capturedPixels = null,
            float nmsIouThreshold = DefaultNmsIou,
            FrameBuffer? buffer = null)
        {
            return outputTensor switch
            {
                Tensor<float> floats => Decode(floats),
                Tensor<Float16> halves => Decode(halves),
                _ => throw new NotSupportedException("Expected Float32 or Float16 detections.")
            };

            List<Prediction> Decode<T>(Tensor<T> tensor) where T : unmanaged =>
                DecodePredictions(tensor, detectionBox, imageSize, numDetections, numClasses, modelClasses,
                    minConfidence, selectedClass, fovMinX, fovMaxX, fovMinY, fovMaxY, clipToBounds, capturedPixels,
                    float.IsNaN(nmsIouThreshold) ? DefaultNmsIou : Math.Clamp(nmsIouThreshold, .35f, .75f), buffer);
        }

        private static List<Prediction> DecodePredictions<T>(Tensor<T> outputTensor, Rectangle detectionBox,
            int imageSize, int numDetections, int numClasses, IReadOnlyDictionary<int, string> modelClasses,
            float minConfidence, string selectedClass, float fovMinX, float fovMaxX, float fovMinY,
            float fovMaxY, bool clipToBounds, Rectangle? capturedPixels, float nmsIouThreshold,
            FrameBuffer? buffer) where T : unmanaged
        {
            var predictions = buffer?.Results ?? new List<Prediction>();
            predictions.Clear();
            if (capturedPixels is Rectangle visible)
            {
                fovMinX = Math.Max(fovMinX, visible.Left);
                fovMaxX = Math.Min(fovMaxX, visible.Right);
                fovMinY = Math.Max(fovMinY, visible.Top);
                fovMaxY = Math.Min(fovMaxY, visible.Bottom);
            }
            if (fovMinX > fovMaxX || fovMinY > fovMaxY) return predictions;

            int selectedClassId = ResolveSelectedClassId(modelClasses, selectedClass);

            var dense = outputTensor as DenseTensor<T>;
            int channelStride = outputTensor.Strides[1];
            int boxStride = outputTensor.Strides[2];
            float Read(int channel, int box)
            {
                int index = channel * channelStride + box * boxStride;
                T value = dense != null ? dense.Buffer.Span[index] : outputTensor.GetValue(index);
                return typeof(T) == typeof(float) ? Unsafe.As<T, float>(ref value) : (float)Unsafe.As<T, Half>(ref value);
            }

            // A single-class YOLO head stores confidences contiguously on the usual dense layout.
            ReadOnlySpan<float> scores = typeof(T) == typeof(float) && dense != null && numClasses == 1 && boxStride == 1
                ? MemoryMarshal.Cast<T, float>(dense.Buffer.Span.Slice(4 * channelStride, numDetections)) : default;
            ReadOnlySpan<short> halfScores = typeof(T) == typeof(Float16) && dense != null && numClasses == 1 && boxStride == 1
                ? MemoryMarshal.Cast<T, short>(dense.Buffer.Span.Slice(4 * channelStride, numDetections)) : default;
            // Signed half bits place negative scores below zero and preserve positive score order.
            var halfThreshold = Vector<short>.Zero;
            if (!halfScores.IsEmpty && !float.IsNaN(minConfidence))
            {
                ushort minScoreBits = BitConverter.HalfToUInt16Bits((Half)Math.Max(0f, minConfidence));
                halfThreshold = new Vector<short>((short)minScoreBits);
            }
            var threshold = new Vector<float>(minConfidence);
            for (int i = 0; i < numDetections; i++)
            {
                if (typeof(T) == typeof(float) && Vector.IsHardwareAccelerated && i % Vector<float>.Count == 0 &&
                    i <= scores.Length - Vector<float>.Count &&
                    Vector.LessThanAll(new Vector<float>(scores.Slice(i)), threshold))
                {
                    i += Vector<float>.Count - 1;
                    continue;
                }

                if (typeof(T) == typeof(Float16) && Vector.IsHardwareAccelerated && i % Vector<short>.Count == 0 &&
                    i <= halfScores.Length - Vector<short>.Count &&
                    Vector.LessThanAll(new Vector<short>(halfScores.Slice(i)), halfThreshold))
                {
                    i += Vector<short>.Count - 1;
                    continue;
                }

                int bestClassId = 0;
                float bestConfidence = 0f;

                if (numClasses == 1)
                {
                    bestConfidence = Read(4, i);
                }
                else
                {
                    if (selectedClassId == -1)
                    {
                        for (int classId = 0; classId < numClasses; classId++)
                        {
                            float classConfidence = Read(4 + classId, i);
                            if (float.IsFinite(classConfidence) && classConfidence > bestConfidence)
                            {
                                bestConfidence = classConfidence;
                                bestClassId = classId;
                            }
                        }
                    }
                    else
                    {
                        bestConfidence = Read(4 + selectedClassId, i);
                        bestClassId = selectedClassId;
                    }
                }

                if (!float.IsFinite(bestConfidence) || bestConfidence <= 0 || bestConfidence < minConfidence) continue;

                float xCenter = Read(0, i);
                float yCenter = Read(1, i);
                float width = Read(2, i);
                float height = Read(3, i);
                if (!float.IsFinite(xCenter) || !float.IsFinite(yCenter) || !float.IsFinite(width) ||
                    !float.IsFinite(height) || width <= 0 || height <= 0) continue;

                float xMin = xCenter - width / 2;
                float yMin = yCenter - height / 2;
                float xMax = xCenter + width / 2;
                float yMax = yCenter + height / 2;

                if (clipToBounds)
                {
                    xMin = Math.Max(xMin, fovMinX);
                    yMin = Math.Max(yMin, fovMinY);
                    xMax = Math.Min(xMax, fovMaxX);
                    yMax = Math.Min(yMax, fovMaxY);
                    width = xMax - xMin;
                    height = yMax - yMin;
                    if (width <= 0 || height <= 0) continue;
                    xCenter = xMin + width / 2;
                    yCenter = yMin + height / 2;
                }
                else if (xCenter < fovMinX || xCenter > fovMaxX || yCenter < fovMinY || yCenter > fovMaxY) continue;

                var rect = new RectangleF(xMin, yMin, width, height);
                var prediction = buffer == null ? new Prediction() : buffer.Boxes[predictions.Count];
                prediction.Rectangle = rect;
                prediction.Confidence = bestConfidence;
                prediction.ClassId = bestClassId;
                prediction.ClassName = buffer?.ClassNames[bestClassId] ??
                    (modelClasses.TryGetValue(bestClassId, out var name) ? name : $"Class_{bestClassId}");
                prediction.CenterXTranslated = xCenter / imageSize;
                prediction.CenterYTranslated = yCenter / imageSize;
                prediction.ScreenCenterX = detectionBox.Left + xCenter;
                prediction.ScreenCenterY = detectionBox.Top + yCenter;
                predictions.Add(prediction);
            }

            if (predictions.Count < 2) return predictions;

            int count = predictions.Count;
            int[]? rented = null;
            Span<int> order = buffer != null ? buffer.Order.AsSpan(0, count) : count <= 128 ? stackalloc int[count] :
                (rented = ArrayPool<int>.Shared.Rent(count)).AsSpan(0, count);
            try
            {
                // Equal scores retain their original detector order.
                for (int i = 0; i < count; i++)
                {
                    int j = i;
                    float score = predictions[i].Confidence;
                    while (j > 0 && predictions[order[j - 1]].Confidence < score)
                    {
                        order[j] = order[j - 1];
                        j--;
                    }
                    order[j] = i;
                }

                int keptCount = 0;
                foreach (int index in order)
                {
                    var candidate = predictions[index];
                    bool suppressed = false;
                    foreach (int winner in order[..keptCount])
                    {
                        var other = predictions[winner];
                        if (other.ClassId == candidate.ClassId && IntersectionOverUnion(other.Rectangle, candidate.Rectangle) >= nmsIouThreshold)
                        {
                            suppressed = true;
                            break;
                        }
                    }
                    if (!suppressed) order[keptCount++] = index;
                }
                order[..keptCount].Sort();
                for (int i = 0; i < keptCount; i++) predictions[i] = predictions[order[i]];
                predictions.RemoveRange(keptCount, count - keptCount);
            }
            finally
            {
                if (rented != null) ArrayPool<int>.Shared.Return(rented);
            }
            return predictions;
        }

        internal static float IntersectionOverUnion(RectangleF a, RectangleF b)
        {
            float width = Math.Min(a.Right, b.Right) - Math.Max(a.Left, b.Left);
            float height = Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Top, b.Top);
            if (width <= 0 || height <= 0) return 0;
            float intersection = width * height;
            return intersection / (a.Width * a.Height + b.Width * b.Height - intersection);
        }

        internal static int ResolveSelectedClassId(IReadOnlyDictionary<int, string> modelClasses, string selectedClass)
        {
            if (selectedClass == "Best Confidence")
            {
                return -1;
            }

            if (modelClasses is Dictionary<int, string> names)
            {
                foreach (var entry in names)
                    if (entry.Value == selectedClass) return entry.Key;
                return -1;
            }

            foreach (var (classId, className) in modelClasses)
            {
                if (className == selectedClass)
                {
                    return classId;
                }
            }

            return -1;
        }
    }
}
