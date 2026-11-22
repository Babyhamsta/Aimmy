using System.Diagnostics;
using System.Drawing;

namespace InputLogic
{
    internal sealed class PointerMotion
    {
        private const double ReferenceFramesPerSecond = 60;

        private long? _previousTime;
        private Axis _x, _y;
        private bool _paused;

        internal void Pause(long timestamp)
        {
            _previousTime = timestamp;
            // Retain the settled EMA and fractional pixels. Step validates the
            // next observation's direction and caps the resumed interval.
            _paused = true;
        }

        internal void Reset()
        {
            _previousTime = null;
            _x = _y = default;
            _paused = false;
        }

        internal Point Step(PointF command, Point observedError, long timestamp, double smoothing, int jitterX, int jitterY)
        {
            double frames = _previousTime is long previous
                ? Math.Clamp(ReferenceFramesPerSecond * Stopwatch.GetElapsedTime(previous, timestamp).TotalSeconds, 0, 4) : 1;
            if (_paused) frames = Math.Min(1, frames);
            _paused = false;
            _previousTime = timestamp;
            return new Point(_x.Step(command.X, observedError.X, jitterX, smoothing, frames),
                _y.Step(command.Y, observedError.Y, jitterY, smoothing, frames));
        }

        private struct Axis
        {
            private const double MaxStepPerFrame = 150;

            private double _filtered, _remainder;
            private int _direction;

            public int Step(double command, int error, int jitter, double smoothing, double frames)
            {
                int direction = Math.Sign(error);
                if (direction == 0)
                {
                    this = default;
                    return 0;
                }
                double magnitude = Math.Abs((double)error);
                if (direction != _direction)
                {
                    this = default;
                    _direction = direction;
                }
                if (frames == 0) return 0;

                // Smooth speed. The live error sets direction, even if lead or path noise crosses zero.
                double speed = Math.Clamp(command * direction, 0, magnitude);
                if (_filtered == 0 || speed == 0 || smoothing >= 1)
                    _filtered = speed;
                else
                    _filtered += (1 - Math.Pow(1 - Math.Clamp(smoothing, 0, 1), frames)) * (speed - _filtered);
                // Preserve fractional corrections. Jitter may vary a larger step by at most half.
                int jitterLimit = (int)(Math.Abs(_filtered) / 2);
                jitter = Math.Clamp(jitter, -jitterLimit, jitterLimit);
                double adjusted = Math.Clamp(_filtered + direction * jitter, 0, magnitude);
                double delta = frames == 1 ? adjusted : magnitude * (1 - Math.Pow(1 - adjusted / magnitude, frames));
                double desired = Math.Min(Math.Min(magnitude, MaxStepPerFrame * frames), delta + _remainder);
                int step = (int)desired;
                _remainder = desired - step;
                return direction * step;
            }
        }
    }
}
