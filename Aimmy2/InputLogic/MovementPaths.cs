using System.Drawing;

namespace InputLogic
{
    internal static class MovementPaths
    {
        private static readonly int[] permutation = new int[512];

        internal static PointF GetCommand(string path, Point end, double sensitivity)
        {
            var start = Point.Empty;
            double progress = 1 - sensitivity;
            return path switch
            {
                "Cubic Bezier" => CubicBezier(start, end, new PointF(end.X / 3f, end.Y / 3f),
                    new PointF(2f * end.X / 3f, 2f * end.Y / 3f), progress),
                "Exponential" => Exponential(start, end, progress + 0.2, 3.0),
                "Perlin Noise" => PerlinNoise(start, end, progress, 20, 0.5),
                _ => Lerp(start, end, progress)
            };
        }

        private static PointF CubicBezier(Point start, Point end, PointF control1, PointF control2, double t)
        {
            double u = 1 - t;
            double tt = t * t;
            double uu = u * u;

            double x = uu * u * start.X + 3 * uu * t * control1.X + 3 * u * tt * control2.X + tt * t * end.X;
            double y = uu * u * start.Y + 3 * uu * t * control1.Y + 3 * u * tt * control2.Y + tt * t * end.Y;

            return new PointF((float)x, (float)y);
        }

        private static PointF Lerp(Point start, Point end, double t)
        {
            return new PointF((float)(start.X + (end.X - start.X) * t),
                (float)(start.Y + (end.Y - start.Y) * t));
        }

        private static PointF Exponential(Point start, Point end, double t, double exponent = 2.0)
        {
            double x = start.X + (end.X - start.X) * Math.Pow(t, exponent);
            double y = start.Y + (end.Y - start.Y) * Math.Pow(t, exponent);
            return new PointF((float)x, (float)y);
        }

        private static PointF PerlinNoise(Point start, Point end, double t, double amplitude = 10.0, double frequency = 0.1)
        {
            double baseX = start.X + (end.X - start.X) * t;
            double baseY = start.Y + (end.Y - start.Y) * t;

            double noiseX = Noise(t * frequency, 0) * amplitude;
            double noiseY = Noise(t * frequency, 100) * amplitude;

            double perpX = -(end.Y - start.Y);
            double perpY = end.X - start.X;
            double perpLength = Math.Sqrt(perpX * perpX + perpY * perpY);

            if (perpLength > 0)
            {
                perpX /= perpLength;
                perpY /= perpLength;
            }

            double finalX = baseX + perpX * noiseX + noiseY * 0.3;
            double finalY = baseY + perpY * noiseX + noiseY * 0.3;

            return new PointF((float)finalX, (float)finalY);
        }

        private static double Fade(double t)
        {
            return t * t * t * (t * (t * 6 - 15) + 10);
        }

        private static double Lerp(double a, double b, double t)
        {
            return a + t * (b - a);
        }

        private static double Grad(int hash, double x, double y)
        {
            int h = hash & 15;
            double u = h < 8 ? x : y;
            double v = h < 4 ? y : h == 12 || h == 14 ? x : 0;
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }

        private static double Noise(double x, double y)
        {
            int X = (int)Math.Floor(x) & 255;
            int Y = (int)Math.Floor(y) & 255;

            x -= Math.Floor(x);
            y -= Math.Floor(y);

            double u = Fade(x);
            double v = Fade(y);

            int A = permutation[X] + Y;
            int AA = permutation[A];
            int AB = permutation[A + 1];
            int B = permutation[X + 1] + Y;
            int BA = permutation[B];
            int BB = permutation[B + 1];

            return Lerp(Lerp(Grad(permutation[AA], x, y),
                           Grad(permutation[BA], x - 1, y), u),
                      Lerp(Grad(permutation[AB], x, y - 1),
                           Grad(permutation[BB], x - 1, y - 1), u), v);
        }
    }
}