using System.Numerics;
using System.Drawing;
using System.Drawing.Imaging;

namespace AILogic
{
    public static class MathUtil
    {
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
                // Windows BGRA pixels are little-endian. Convert whole SIMD vectors into planar RGB.
                var mask = new Vector<uint>(255);
                var divisor = new Vector<float>(255);
                for (int y = 0; y < size; y++)
                {
                    var row = new ReadOnlySpan<uint>((byte*)bits.Scan0 + (long)y * bits.Stride, size);
                    int offset = y * size;
                    int x = 0;
                    if (Vector.IsHardwareAccelerated)
                    {
                        for (; x <= size - Vector<uint>.Count; x += Vector<uint>.Count)
                        {
                            var colors = new Vector<uint>(row.Slice(x));
                            (Vector.ConvertToSingle((colors >> 16) & mask) / divisor).CopyTo(result, offset + x);
                            (Vector.ConvertToSingle((colors >> 8) & mask) / divisor).CopyTo(result, pixels + offset + x);
                            (Vector.ConvertToSingle(colors & mask) / divisor).CopyTo(result, 2 * pixels + offset + x);
                        }
                    }
                    for (; x < size; x++)
                    {
                        uint color = row[x];
                        result[offset + x] = ((color >> 16) & 255) / 255f;
                        result[pixels + offset + x] = ((color >> 8) & 255) / 255f;
                        result[2 * pixels + offset + x] = (color & 255) / 255f;
                    }
                }
            }
            finally
            {
                image.UnlockBits(bits);
            }
        }
    }
}