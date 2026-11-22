using System.Diagnostics;
using System.Drawing;
using System.Numerics;

namespace Aimmy2.AILogic;

internal sealed class StickyAimSelector
{
    private const float VelocityTau = .025f, MissDecayTau = .080f, MaxSpeed = 2500f;
    internal const int DefaultGraceMilliseconds = 150;

    private sealed class Track(int id, Prediction detection, long timestamp)
    {
        internal readonly int Id = id;
        internal readonly int ClassId = detection.ClassId;
        private Vector2 _estimate = Position(detection);
        private Vector2 _velocity;
        internal Vector2 Size = new(detection.Rectangle.Width, detection.Rectangle.Height);
        internal long SeenAt = timestamp;
        internal Prediction? Measurement = detection;
        internal Prediction LastDetection = detection;

        internal Vector2 Center => Position(LastDetection);
        internal float Elapsed(long time) => (float)Stopwatch.GetElapsedTime(SeenAt, time).TotalSeconds;
        internal Vector2 Predict(float dt) => _estimate + _velocity * dt;

        internal void Coast(float retention)
        {
            // Integrate the decaying velocity once over this capture interval.
            _estimate += _velocity * (MissDecayTau * (1 - retention));
            _velocity *= retention;
        }

        internal void Observe(Prediction p, long time, float frameDt)
        {
            float dt = Math.Max(.001f, Elapsed(time));
            var forecast = Predict(frameDt);
            var position = Position(p);
            var measuredVelocity = (position - Center) / dt;
            _velocity = Vector2.Lerp(_velocity, measuredVelocity, 1 - MathF.Exp(-dt / VelocityTau));
            float speedSquared = _velocity.LengthSquared();
            if (speedSquared > MaxSpeed * MaxSpeed)
                _velocity *= MaxSpeed / MathF.Sqrt(speedSquared);
            _estimate = Vector2.Lerp(forecast, position, .85f);
            Size = Vector2.Lerp(Size, new(p.Rectangle.Width, p.Rectangle.Height), .8f);
            LastDetection = Measurement = p;
            SeenAt = time;
        }
    }

    private readonly List<Track> _tracks = new();
    private readonly List<(float Cost, int Track, int Detection)> _pairs = new();
    private bool[] _used = [];
    private int _nextId;
    private int? _lockedId;
    private bool _sticky;
    private long? _lastObservation;

    internal bool HasLock => _lockedId.HasValue;
    internal bool Acquired { get; private set; }

    internal Prediction? ForDisplay(Prediction? selected, IReadOnlyList<Prediction> detections,
        Vector2 crosshair, Func<RectangleF, Vector2> aimPoint, float fovSize)
    {
        if (_sticky && HasLock) return selected;
        return selected ?? NearestToCenter(detections, crosshair, aimPoint, fovSize);
    }

    private static Vector2 Position(Prediction p) => new(p.ScreenCenterX, p.ScreenCenterY);

    private static bool ImprovesAcquisition(Prediction prediction, Vector2 crosshair,
        Func<RectangleF, Vector2> aimPoint, float radiusSquared, ref float best)
    {
        float aimDistance = Vector2.DistanceSquared(crosshair, aimPoint(prediction.ScreenBounds));
        if (!(aimDistance <= radiusSquared && aimDistance < best)) return false;

        best = aimDistance;
        return true;
    }

    internal static Prediction? NearestToCenter(IReadOnlyList<Prediction> detections,
        Vector2 crosshair, Func<RectangleF, Vector2> aimPoint, float fovSize)
    {
        Prediction? nearest = null;
        float radius = fovSize / 2;
        float radiusSquared = radius * radius;
        float best = float.PositiveInfinity;
        foreach (var p in detections)
            if (ImprovesAcquisition(p, crosshair, aimPoint, radiusSquared, ref best)) nearest = p;
        return nearest;
    }

    internal Prediction? SelectTarget(bool stickyAimEnabled, float stickyThreshold,
        IReadOnlyList<Prediction> predictions, long observationTime, Vector2 crosshair,
        Func<RectangleF, Vector2> aimPoint, double graceMilliseconds = DefaultGraceMilliseconds,
        Rectangle? captureBounds = null, float fovSize = float.PositiveInfinity)
    {
        if (_sticky != stickyAimEnabled) Reset();
        _sticky = stickyAimEnabled;
        Acquired = false;
        if (_lastObservation is long previous && observationTime <= previous) return null;
        float frameDt = (float)Stopwatch.GetElapsedTime(_lastObservation ?? observationTime, observationTime).TotalSeconds;
        _lastObservation = observationTime;

        double grace = Math.Max(0, graceMilliseconds) / 1000;
        // A continuous stream may run slower than a short (or zero) miss grace.
        // After an actual miss, the last sighting sets the strict expiry time.
        for (int i = _tracks.Count - 1; i >= 0; i--)
        {
            var track = _tracks[i];
            double expiry = track.Measurement == null ? grace : Math.Max(.15, grace);
            if (Stopwatch.GetElapsedTime(track.SeenAt, observationTime).TotalSeconds > expiry)
                _tracks.RemoveAt(i);
        }

        Associate(predictions, observationTime, frameDt, stickyThreshold);
        float retention = MathF.Exp(-frameDt / MissDecayTau);
        for (int i = _tracks.Count - 1; i >= 0; i--)
        {
            var track = _tracks[i];
            if (track.Measurement != null) continue;
            track.Coast(retention);
            if (!_sticky || Stopwatch.GetElapsedTime(track.SeenAt, observationTime).TotalSeconds > grace ||
                (captureBounds is Rectangle frame && !((RectangleF)frame).Contains(track.LastDetection.ScreenCenterX,
                    track.LastDetection.ScreenCenterY)))
                _tracks.RemoveAt(i);
        }

        float radius = fovSize / 2;
        float radiusSquared = radius * radius;
        float best = float.PositiveInfinity;
        Track? nearest = null;
        foreach (var track in _tracks)
        {
            if (_sticky && track.Id == _lockedId) return track.Measurement;
            if (track.Measurement is Prediction p &&
                ImprovesAcquisition(p, crosshair, aimPoint, radiusSquared, ref best))
                nearest = track;
        }
        Acquired = nearest != null && nearest.Id != _lockedId;
        _lockedId = nearest?.Id;
        return nearest?.Measurement;
    }

    private void Associate(IReadOnlyList<Prediction> detections, long time, float frameDt, float radius)
    {
        if (_used.Length < detections.Count) _used = new bool[detections.Count];
        else Array.Clear(_used, 0, detections.Count);
        _pairs.Clear();
        for (int i = 0; i < _tracks.Count; i++)
        {
            var track = _tracks[i];
            track.Measurement = null;
            var center = track.Predict(frameDt);
            var observedCenter = track.Center;
            var size = track.Size;
            var bounds = new RectangleF(center.X - size.X / 2, center.Y - size.Y / 2, size.X, size.Y);
            float scale = Math.Max(1, size.Length());
            var observedBounds = track.LastDetection.ScreenBounds;
            for (int j = 0; j < detections.Count; j++)
            {
                var p = detections[j];
                if (p.ClassId != track.ClassId) continue;
                var position = Position(p);
                var candidate = p.ScreenBounds;
                float distance = Vector2.Distance(center, position);
                var matchBounds = bounds;
                if (distance > radius)
                {
                    distance = Vector2.Distance(observedCenter, position);
                    if (distance > radius) continue;
                    matchBounds = observedBounds;
                }
                float iou = PredictionFilter.IntersectionOverUnion(matchBounds, candidate);
                float widthChange = MathF.Log(p.Rectangle.Width / size.X);
                float heightChange = MathF.Log(p.Rectangle.Height / size.Y);
                // Uniform shrinkage is common with distant players. A change in
                // aspect ratio offers stronger evidence when two centers cross.
                float sizePenalty = .35f * MathF.Abs((widthChange + heightChange) / 2)
                    + 1.25f * MathF.Abs(widthChange - heightChange);
                float cost = 1 - iou + distance / scale + sizePenalty;
                _pairs.Add((cost, i, j));
            }
        }

        // Lexicographic order resolves equal costs by track creation order, then detector order.
        _pairs.Sort();
        foreach (var (_, i, j) in _pairs)
        {
            if (_used[j] || _tracks[i].Measurement != null) continue;
            _tracks[i].Observe(detections[j], time, frameDt);
            _used[j] = true;
        }
        for (int j = 0; j < detections.Count; j++)
            if (!_used[j]) _tracks.Add(new Track(_nextId++, detections[j], time));
    }

    internal void Reset()
    {
        _tracks.Clear();
        _lockedId = null;
        _lastObservation = null;
        _nextId = 0;
        Acquired = false;
    }
}
