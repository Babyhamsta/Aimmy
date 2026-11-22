using Microsoft.ML.OnnxRuntime;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Drawing;
using System.Drawing.Imaging;

namespace AILogic
{
    public static class MathUtil
    {
        private static readonly ushort[] ByteToHalfBits = CreateByteToHalfTable();

        private static ushort[] CreateByteToHalfTable()
        {
            var table = new ushort[256];
            for (int i = 0; i < table.Length; i++)
                table[i] = BitConverter.HalfToUInt16Bits((Half)(i / 255f));
            return table;
        }

        public static int CalculateNumDetections(int imageSize)
        {
            // YOLOv8 detection calculation: (size/8)² + (size/16)² + (size/32)²
            int stride8 = imageSize / 8;
            int stride16 = imageSize / 16;
            int stride32 = imageSize / 32;

            return (stride8 * stride8) + (stride16 * stride16) + (stride32 * stride32);
        }

        public static unsafe void BitmapToFloatArrayInPlace(Bitmap image, float[] result, int IMAGE_SIZE)
        {
            ArgumentNullException.ThrowIfNull(image);
            ArgumentNullException.ThrowIfNull(result);
            int size = IMAGE_SIZE;
            int pixels = checked(size * size);
            if (result.Length != checked(3 * pixels))
                throw new ArgumentException("Expected three planar color channels.", nameof(result));

            var bits = image.LockBits(new Rectangle(0, 0, size, size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                BgraToFloatArrayInPlace((byte*)bits.Scan0, bits.Stride, new Rectangle(0, 0, size, size),
                    Point.Empty, false, result, size);
            }
            finally
            {
                image.UnlockBits(bits);
            }
        }

        internal static unsafe void BgraToFloatArrayInPlace(byte* scan0, int stride, Rectangle visible,
            Point destination, bool maskThirdPerson, float[] result, int imageSize)
        {
            int pixels = checked(imageSize * imageSize);
            // Windows BGRA pixels are little-endian. Convert whole SIMD vectors into planar RGB.
            var mask = new Vector<uint>(255);
            const float scale = 1f / 255f;
            var multiplier = new Vector<float>(scale);
            for (int y = 0; y < imageSize; y++)
            {
                int offset = y * imageSize;
                bool visibleRow = y >= destination.Y && y < destination.Y + visible.Height;
                int right = visibleRow ? destination.X + visible.Width : 0;
                int left = visibleRow ? destination.X : 0;
                if (maskThirdPerson && y >= imageSize - imageSize / 2)
                    left = Math.Min(right, Math.Max(left, imageSize / 2));

                if (left != 0 || right != imageSize)
                {
                    for (int plane = 0; plane < 3; plane++)
                    {
                        int start = plane * pixels + offset;
                        result.AsSpan(start, left).Clear();
                        result.AsSpan(start + right, imageSize - right).Clear();
                    }
                }
                if (left == right) continue;
                var row = new ReadOnlySpan<uint>(scan0 + (long)y * stride, right);
                int x = left;
                if (Vector.IsHardwareAccelerated)
                {
                    for (; x <= right - Vector<uint>.Count; x += Vector<uint>.Count)
                    {
                        var colors = new Vector<uint>(row.Slice(x));
                        (Vector.ConvertToSingle((colors >> 16) & mask) * multiplier).CopyTo(result, offset + x);
                        (Vector.ConvertToSingle((colors >> 8) & mask) * multiplier).CopyTo(result, pixels + offset + x);
                        (Vector.ConvertToSingle(colors & mask) * multiplier).CopyTo(result, 2 * pixels + offset + x);
                    }
                }
                for (; x < right; x++)
                {
                    uint color = row[x];
                    result[offset + x] = ((color >> 16) & 255) * scale;
                    result[pixels + offset + x] = ((color >> 8) & 255) * scale;
                    result[2 * pixels + offset + x] = (color & 255) * scale;
                }
            }
        }

        public static unsafe void BitmapToHalfArrayInPlace(Bitmap image, Float16[] result, int IMAGE_SIZE)
        {
            ArgumentNullException.ThrowIfNull(image);
            ArgumentNullException.ThrowIfNull(result);
            int size = IMAGE_SIZE;
            int pixels = checked(size * size);
            if (result.Length != checked(3 * pixels))
                throw new ArgumentException("Expected three planar color channels.", nameof(result));

            var bits = image.LockBits(new Rectangle(0, 0, size, size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                BgraToHalfArrayInPlace((byte*)bits.Scan0, bits.Stride, new Rectangle(0, 0, size, size),
                    Point.Empty, false, result, size);
            }
            finally
            {
                image.UnlockBits(bits);
            }
        }

        internal static unsafe void BgraToHalfArrayInPlace(byte* scan0, int stride, Rectangle visible,
            Point destination, bool maskThirdPerson, Float16[] result, int imageSize)
        {
            int pixels = checked(imageSize * imageSize);
            Span<ushort> bits = MemoryMarshal.Cast<Float16, ushort>(result);
            ushort[] lut = ByteToHalfBits;
            for (int y = 0; y < imageSize; y++)
            {
                int offset = y * imageSize;
                bool visibleRow = y >= destination.Y && y < destination.Y + visible.Height;
                int right = visibleRow ? destination.X + visible.Width : 0;
                int left = visibleRow ? destination.X : 0;
                if (maskThirdPerson && y >= imageSize - imageSize / 2)
                    left = Math.Min(right, Math.Max(left, imageSize / 2));

                if (left != 0 || right != imageSize)
                {
                    for (int plane = 0; plane < 3; plane++)
                    {
                        int start = plane * pixels + offset;
                        result.AsSpan(start, left).Clear();
                        result.AsSpan(start + right, imageSize - right).Clear();
                    }
                }
                if (left == right) continue;
                byte* pixel = scan0 + (long)y * stride + left * 4;
                var red = bits.Slice(offset, right);
                var green = bits.Slice(pixels + offset, right);
                var blue = bits.Slice(2 * pixels + offset, right);
                int x = left;
                if (Avx2.IsSupported)
                {
                    var channelMask = Vector256.Create(255u);
                    fixed (ushort* redRow = red)
                    {
                        for (; x <= right - 8; x += 8, pixel += 32)
                        {
                            var colors = Vector256.Load((uint*)pixel);
                            var r = Avx2.And(Avx2.ShiftRightLogical(colors, 16), channelMask);
                            var g = Avx2.And(Avx2.ShiftRightLogical(colors, 8), channelMask);
                            var b = Avx2.And(colors, channelMask);
                            ushort* output = redRow + x;
                            NormalizedByteToHalfBits(r).Store(output);
                            NormalizedByteToHalfBits(g).Store(output + pixels);
                            NormalizedByteToHalfBits(b).Store(output + 2 * pixels);
                        }
                    }
                }
                for (; x <= right - 4; x += 4, pixel += 16)
                {
                    red[x] = lut[pixel[2]];
                    green[x] = lut[pixel[1]];
                    blue[x] = lut[pixel[0]];
                    red[x + 1] = lut[pixel[6]];
                    green[x + 1] = lut[pixel[5]];
                    blue[x + 1] = lut[pixel[4]];
                    red[x + 2] = lut[pixel[10]];
                    green[x + 2] = lut[pixel[9]];
                    blue[x + 2] = lut[pixel[8]];
                    red[x + 3] = lut[pixel[14]];
                    green[x + 3] = lut[pixel[13]];
                    blue[x + 3] = lut[pixel[12]];
                }
                for (; x < right; x++, pixel += 4)
                {
                    red[x] = lut[pixel[2]];
                    green[x] = lut[pixel[1]];
                    blue[x] = lut[pixel[0]];
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<ushort> NormalizedByteToHalfBits(Vector256<uint> channel)
        {
            var normalized = Avx.Multiply(Avx.ConvertToVector256Single(channel.AsInt32()), Vector256.Create(1f / 255f));
            // Every nonzero byte / 255 is a normal half and none is a rounding midpoint.
            // Rebase the exponent and round away the low 13 mantissa bits. The pack clamps zero's negative result to zero.
            const int exponentBias = (127 - 15) << 23;
            const int roundingBias = 1 << 12;
            var rounded = Avx2.Add(normalized.AsInt32(), Vector256.Create(roundingBias - exponentBias));
            var halfBits = Avx2.ShiftRightArithmetic(rounded, 13);
            return Sse41.PackUnsignedSaturate(halfBits.GetLower(), halfBits.GetUpper());
        }
    }
}
