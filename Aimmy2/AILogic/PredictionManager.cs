using System.Diagnostics;
using System.Drawing;

namespace AILogic
{
    internal abstract class MotionPredictor
    {
        private long? _last;
        private long _changedX, _changedY;
        private Point _observed, _prediction;

        internal void Reset() => _last = null;

        internal Point Update(Point point, long timestamp, double lead)
        {
            if (_last is not long previous)
            {
                Initialize(point, timestamp);
                _changedX = _changedY = timestamp;
                _prediction = point;
            }
            else
            {
                if (timestamp <= previous) return _prediction;
                var estimate = Estimate(point, timestamp, Stopwatch.GetElapsedTime(previous, timestamp).TotalSeconds, lead);
                _prediction = new Point(LimitLead(point.X, _observed.X, estimate.X, timestamp, ref _changedX),
                    LimitLead(point.Y, _observed.Y, estimate.Y, timestamp, ref _changedY));
            }

            _last = timestamp;
            _observed = point;
            return _prediction;
        }

        private static int LimitLead(int current, int previous, int estimate, long time, ref long lastChange)
        {
            if (current != previous)
            {
                lastChange = time;
                if ((current > previous && estimate < current) || (current < previous && estimate > current))
                    return current;
            }
            else if (Stopwatch.GetElapsedTime(lastChange, time).TotalMilliseconds >= 33)
                return current;
            return estimate;
        }

        protected abstract void Initialize(Point point, long timestamp);
        protected abstract Point Estimate(Point point, long timestamp, double dt, double lead);
    }

    internal sealed class KalmanPrediction : MotionPredictor
    {
        private double _x, _y, _vx, _vy;
        // Both axes use the same position/velocity covariance and measurement variance.
        private double _pp, _pv, _vv;

        protected override void Initialize(Point point, long timestamp)
        {
            _x = point.X;
            _y = point.Y;
            _vx = _vy = _pv = 0;
            _pp = 1;
            _vv = 10000;
        }

        protected override Point Estimate(Point point, long timestamp, double dt, double lead)
        {
            // Constant velocity model with continuous acceleration noise.
            const double noise = 24000;
            double pp = _pp + dt * (2 * _pv + dt * _vv) + noise * dt * dt * dt / 3;
            double pv = _pv + dt * _vv + noise * dt * dt / 2;
            double positionGain = pp / (pp + 0.5);
            double velocityGain = pv / (pp + 0.5);
            _x += _vx * dt;
            _y += _vy * dt;
            double dx = point.X - _x, dy = point.Y - _y;
            _x += positionGain * dx;
            _y += positionGain * dy;
            _vx = Math.Clamp(_vx + velocityGain * dx, -5000, 5000);
            _vy = Math.Clamp(_vy + velocityGain * dy, -5000, 5000);
            _pp = (1 - positionGain) * pp;
            _pv = (1 - positionGain) * pv;
            _vv += noise * dt - velocityGain * pv;
            return new Point((int)Math.Round(_x + _vx * lead), (int)Math.Round(_y + _vy * lead));
        }
    }

    internal sealed class WiseTheFoxPrediction : MotionPredictor
    {
        private double _x, _y, _vx, _vy;

        protected override void Initialize(Point point, long timestamp)
        {
            _x = point.X;
            _y = point.Y;
            _vx = _vy = 0;
        }

        protected override Point Estimate(Point point, long timestamp, double dt, double lead)
        {
            double alpha = 1 - Math.Pow(0.5, 60 * dt);
            double dx = alpha * (point.X - _x), dy = alpha * (point.Y - _y);
            _vx += alpha * (dx / dt - _vx);
            _vy += alpha * (dy / dt - _vy);
            _x += dx;
            _y += dy;
            return new Point((int)Math.Round(_x + _vx * lead), (int)Math.Round(_y + _vy * lead));
        }
    }

    internal sealed class ShalloePredictionV2 : MotionPredictor
    {
        private readonly Queue<(long Time, Point Position)> _history = new();
        protected override void Initialize(Point point, long timestamp)
        {
            _history.Clear();
            _history.Enqueue((timestamp, point));
        }

        protected override Point Estimate(Point point, long timestamp, double dt, double multiplier)
        {
            // Preserve the old five-frame window and lead units at the 60 Hz reference rate.
            while (_history.Count > 1 && Stopwatch.GetElapsedTime(_history.Peek().Time, timestamp).TotalSeconds > 5.0 / 60)
                _history.Dequeue();
            var oldest = _history.Peek();
            _history.Enqueue((timestamp, point));
            double window = Stopwatch.GetElapsedTime(oldest.Time, timestamp).TotalSeconds;
            double factor = multiplier / (60 * window);
            return new Point(point.X + (int)Math.Round((point.X - oldest.Position.X) * factor),
                point.Y + (int)Math.Round((point.Y - oldest.Position.Y) * factor));
        }
    }
}
