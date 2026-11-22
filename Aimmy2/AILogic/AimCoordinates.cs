using System.Drawing;
using System.Numerics;

namespace Aimmy2.AILogic
{
    internal static class AimCoordinates
    {
        internal static Point Error(RectangleF box, int cropSize, bool percentX, double xPercent,
            double xOffset, bool percentY, double yPercent, double yOffset, string alignment)
        {
            var point = AimPoint(box, percentX, xPercent, xOffset, percentY, yPercent, yOffset, alignment);
            return Point.Round(new PointF(point.X - cropSize / 2, point.Y - cropSize / 2));
        }

        internal static Vector2 AimPoint(RectangleF box, bool percentX, double xPercent,
            double xOffset, bool percentY, double yPercent, double yOffset, string alignment)
        {
            double x = box.X + (percentX ? box.Width * xPercent / 100 : box.Width / 2 + xOffset);
            double yFraction = percentY ? 1 - yPercent / 100 : alignment switch
            {
                "Center" => 0.5,
                "Bottom" => 1,
                _ => 0
            };
            return new((float)x, (float)(box.Y + box.Height * yFraction + yOffset));
        }
    }
}
