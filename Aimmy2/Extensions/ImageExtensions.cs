using System.Drawing.Imaging;
using System.Drawing;

namespace Aimmy2.Extensions;

public static class ImageExtensions
{
    public static float[] ToFloatArray(this Bitmap image)
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
}