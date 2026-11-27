using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Aimmy2.AILogic;

internal sealed class StickyAimSelector
{
    private const float VelocityTau = .025f, MissDecayTau = .080f, MaxSpeed = 2500f;
    internal const int DefaultGraceMilliseconds = 150;
    internal const float DefaultMaxMatchCost = 2f;
    internal const float BlockedCost = 1e4f;

    private struct AxisKalman(float value, float initialVelocityVar)
    {
        internal float Value = value;
        internal float Velocity;
        private float _pp = 4f, _pv, _vv = initialVelocityVar;

        internal void Predict(float dt, float processNoise)
        {
            Value += Velocity * dt;
            // F P F' + Q for constant velocity driven by white acceleration.
            float dt2 = dt * dt;
            _pp += dt * (2 * _pv + dt * _vv) + processNoise * dt2 * dt / 3;
            _pv += dt * _vv + processNoise * dt2 / 2;
            _vv += processNoise * dt;
        }

        internal void Observe(float measurement, float measurementVar, float maxVelocity)
        {
            float innovation = measurement - Value;
            float inverse = 1 / (_pp + measurementVar);
            float positionGain = _pp * inverse, velocityGain = _pv * inverse;
            Value += positionGain * innovation;
            Velocity = Math.Clamp(Velocity + velocityGain * innovation, -maxVelocity, maxVelocity);
            _vv -= velocityGain * _pv;
            _pv = velocityGain * measurementVar;
            _pp = positionGain * measurementVar;
        }

        internal void Coast(float retention)
        {
            Velocity *= retention;
            _pv *= retention;
            _vv *= retention * retention;
        }
    }

    private struct Track(int id, Prediction p, long timestamp)
    {
        internal readonly int Id = id, ClassId = p.ClassId;
        internal int Hits = 1;
        internal long SeenAt = timestamp;
        internal Prediction? Measurement = p;
        // Decoded Prediction instances belong to the current frame.
        internal RectangleF LastBounds = p.ScreenBounds;
        private AxisKalman _x = new(p.ScreenCenterX, 160000), _y = new(p.ScreenCenterY, 160000);
        private AxisKalman _w = new(p.Rectangle.Width, 1600), _h = new(p.Rectangle.Height, 1600);

        internal readonly Vector2 Center => new(_x.Value, _y.Value);
        internal readonly Vector2 Size => new(_w.Value, _h.Value);

        internal void Predict(float dt)
        {
            Measurement = null;
            _x.Predict(dt, 240000);
            _y.Predict(dt, 240000);
            _w.Predict(dt, 2400);
            _h.Predict(dt, 2400);
            _w.Value = Math.Max(1e-3f, _w.Value);
            _h.Value = Math.Max(1e-3f, _h.Value);
        }

        internal void Coast(float retention)
        {
            _x.Coast(retention);
            _y.Coast(retention);
            _w.Coast(retention);
            _h.Coast(retention);
        }

        internal void Observe(Prediction detection, long time, bool sticky)
        {
            if (sticky)
            {
                _x.Observe(detection.ScreenCenterX, 2, MaxSpeed);
                _y.Observe(detection.ScreenCenterY, 2, MaxSpeed);
                _w.Observe(detection.Rectangle.Width, 9, 400);
                _h.Observe(detection.Rectangle.Height, 9, 400);
            }
            else
            {
                // Preserve the non-sticky identity transitions that reset mouse motion.
                float dt = Math.Max(.001f, (float)Stopwatch.GetElapsedTime(SeenAt, time).TotalSeconds);
                var position = new Vector2(detection.ScreenCenterX, detection.ScreenCenterY);
                var velocity = Vector2.Lerp(new(_x.Velocity, _y.Velocity),
                    (position - BoxCenter(LastBounds)) / dt, 1 - MathF.Exp(-dt / VelocityTau));
                if (velocity.LengthSquared() > MaxSpeed * MaxSpeed)
                    velocity *= MaxSpeed / velocity.Length();
                (_x.Velocity, _y.Velocity) = (velocity.X, velocity.Y);
                var estimate = Vector2.Lerp(Center, position, .85f);
                (_x.Value, _y.Value) = (estimate.X, estimate.Y);
                var size = Vector2.Lerp(Size, new(detection.Rectangle.Width, detection.Rectangle.Height), .8f);
                (_w.Value, _h.Value) = (size.X, size.Y);
            }
            Hits++;
            Measurement = detection;
            LastBounds = detection.ScreenBounds;
            SeenAt = time;
        }
    }

    private const int InitialCapacity = 64;
    private readonly List<Track> _tracks = new(InitialCapacity);
    private readonly List<(float Cost, int Track, int Detection)> _pairs = new(InitialCapacity * InitialCapacity);
    private int[] _trackIndices = new int[InitialCapacity], _detectionIndices = new int[InitialCapacity];
    private bool[] _used = new bool[InitialCapacity];
    private Candidate[] _candidates = new Candidate[InitialCapacity];
    private float[,] _costs = new float[InitialCapacity, InitialCapacity];
    private LinearAssignment.Node[] _largeScratch = [];
    private int _nextId;
    private int? _lockedId;
    private bool _sticky;
    private long? _lastObservation;

    internal bool HasLock => _lockedId.HasValue;
    internal bool Acquired { get; private set; }
    internal int TrackCount => _tracks.Count;

    internal Prediction? ForDisplay(Prediction? selected, IReadOnlyList<Prediction> detections,
        Vector2 crosshair, Func<RectangleF, Vector2> aimPoint, float fovSize, float minConfidence = 0)
    {
        if (_sticky && HasLock) return selected;
        return selected ?? NearestToCenter(detections, crosshair, aimPoint, fovSize, minConfidence);
    }

    private static Vector2 BoxCenter(RectangleF box) => new(box.X + box.Width / 2, box.Y + box.Height / 2);

    private static bool ImprovesAcquisition(Prediction prediction, Vector2 crosshair,
        Func<RectangleF, Vector2> aimPoint, float radiusSquared, ref float best)
    {
        float aimDistance = Vector2.DistanceSquared(crosshair, aimPoint(prediction.ScreenBounds));
        if (!(aimDistance <= radiusSquared && aimDistance < best)) return false;
        best = aimDistance;
        return true;
    }

    internal static Prediction? NearestToCenter(IReadOnlyList<Prediction> detections,
        Vector2 crosshair, Func<RectangleF, Vector2> aimPoint, float fovSize, float minConfidence = 0)
    {
        Prediction? nearest = null;
        float radius = fovSize / 2;
        float best = float.PositiveInfinity;
        for (int i = 0; i < detections.Count; i++)
        {
            var p = detections[i];
            if (p.Confidence >= minConfidence && ImprovesAcquisition(p, crosshair, aimPoint, radius * radius, ref best))
                nearest = p;
        }
        return nearest;
    }

    internal Prediction? SelectTarget(bool stickyAimEnabled, float stickyThreshold,
        IReadOnlyList<Prediction> predictions, long observationTime, Vector2 crosshair,
        Func<RectangleF, Vector2> aimPoint, double graceMilliseconds = DefaultGraceMilliseconds,
        Rectangle? captureBounds = null, float fovSize = float.PositiveInfinity,
        float minConfidence = 0, float maxMatchCost = DefaultMaxMatchCost)
    {
        if (_sticky != stickyAimEnabled) Reset();
        _sticky = stickyAimEnabled;
        Acquired = false;
        if (_lastObservation is long previous && observationTime <= previous) return null;
        float frameDt = (float)Stopwatch.GetElapsedTime(_lastObservation ?? observationTime, observationTime).TotalSeconds;
        _lastObservation = observationTime;
        maxMatchCost = float.IsNaN(maxMatchCost) ? DefaultMaxMatchCost : Math.Clamp(maxMatchCost, 1.2f, 3.5f);
        double grace = Math.Max(0, graceMilliseconds) / 1000;
        var tracks = CollectionsMarshal.AsSpan(_tracks);
        for (int i = tracks.Length - 1; i >= 0; i--)
        {
            ref var track = ref tracks[i];
            double expiry = track.Measurement == null ? grace : Math.Max(.15, grace);
            if (Stopwatch.GetElapsedTime(track.SeenAt, observationTime).TotalSeconds > expiry)
                _tracks.RemoveAt(i);
            else
                track.Predict(frameDt);
        }

        Associate(predictions, observationTime, stickyThreshold, minConfidence, maxMatchCost);
        float retention = MathF.Exp(-frameDt / MissDecayTau);
        tracks = CollectionsMarshal.AsSpan(_tracks);
        for (int i = tracks.Length - 1; i >= 0; i--)
        {
            ref var track = ref tracks[i];
            if (track.Measurement != null) continue;
            var lastCenter = BoxCenter(track.LastBounds);
            if (!_sticky || Stopwatch.GetElapsedTime(track.SeenAt, observationTime).TotalSeconds > grace ||
                (captureBounds is Rectangle frame && !((RectangleF)frame).Contains(lastCenter.X, lastCenter.Y)))
                _tracks.RemoveAt(i);
            else
                track.Coast(retention);
        }

        float radius = fovSize / 2;
        float best = float.PositiveInfinity;
        int? nearestId = null;
        Prediction? nearest = null;
        foreach (ref readonly var track in CollectionsMarshal.AsSpan(_tracks))
        {
            if (_sticky && track.Id == _lockedId) return track.Measurement;
            if (track.Measurement is Prediction p && p.Confidence >= minConfidence &&
                ImprovesAcquisition(p, crosshair, aimPoint, radius * radius, ref best))
            {
                nearestId = track.Id;
                nearest = p;
            }
        }
        Acquired = nearestId.HasValue && nearestId != _lockedId;
        _lockedId = nearestId;
        return nearest;
    }

    private void Associate(IReadOnlyList<Prediction> detections, long time, float radius,
        float minConfidence, float maxCost)
    {
        if (detections.Count == 0) return;
        EnsureCapacity(ref _used, detections.Count);
        _used.AsSpan(0, detections.Count).Clear();
        if (_tracks.Count != 0)
        {
            EnsureCapacity(ref _candidates, detections.Count);
            bool hasLowConfidence = false;
            for (int j = 0; j < detections.Count; j++)
            {
                var detection = detections[j];
                _candidates[j] = new Candidate(detection);
                hasLowConfidence |= detection.Confidence < minConfidence;
            }
            if (_sticky)
            {
                EnsureCapacity(ref _trackIndices, _tracks.Count);
                EnsureCapacity(ref _detectionIndices, detections.Count);
                int matched = MatchPass(detections, time, radius, minConfidence, maxCost, Pass.Confirmed);
                if (matched < _tracks.Count && matched < detections.Count)
                    matched += MatchPass(detections, time, radius, minConfidence, maxCost, Pass.Tentative);
                if (HasLock && hasLowConfidence && matched < _tracks.Count && matched < detections.Count)
                    MatchPass(detections, time, radius, minConfidence, maxCost * .75f, Pass.Rescue);
            }
            else
            {
                MatchNonSticky(detections, time, radius, minConfidence);
            }
        }
        for (int j = 0; j < detections.Count; j++)
            if (!_used[j] && detections[j].Confidence >= minConfidence)
                _tracks.Add(new Track(_nextId++, detections[j], time));
    }

    private enum Pass { Confirmed, Tentative, Rescue }

    private int MatchPass(IReadOnlyList<Prediction> detections, long time, float radius,
        float minConfidence, float maxCost, Pass pass)
    {
        var tracks = CollectionsMarshal.AsSpan(_tracks);
        int trackCount = 0, detectionCount = 0;
        for (int i = 0; i < tracks.Length; i++)
        {
            ref readonly var track = ref tracks[i];
            bool confirmed = track.Hits >= 2 || track.Id == _lockedId;
            if (track.Measurement == null && (pass == Pass.Rescue || confirmed == (pass == Pass.Confirmed)))
                _trackIndices[trackCount++] = i;
        }
        if (trackCount == 0) return 0;
        for (int j = 0; j < detections.Count; j++)
            if (!_used[j] && (detections[j].Confidence < minConfidence) == (pass == Pass.Rescue))
                _detectionIndices[detectionCount++] = j;
        if (detectionCount == 0) return 0;

        // Put the smaller partition on the augmenting side of the rectangular problem.
        bool transpose = trackCount > detectionCount;
        int rows = Math.Min(trackCount, detectionCount), columns = Math.Max(trackCount, detectionCount);
        if (rows > _costs.GetLength(0) || columns > _costs.GetLength(1))
            _costs = new float[GrownCapacity(_costs.GetLength(0), rows), GrownCapacity(_costs.GetLength(1), columns)];

        bool hasEdge = false;
        for (int i = 0; i < trackCount; i++)
        {
            var box = new MatchBox(in tracks[_trackIndices[i]]);
            for (int j = 0; j < detectionCount; j++)
            {
                float cost = box.Cost(in _candidates[_detectionIndices[j]], radius, true, pass == Pass.Rescue);
                bool allowed = cost <= maxCost;
                hasEdge |= allowed;
                _costs[transpose ? j : i, transpose ? i : j] = allowed ? cost : BlockedCost;
            }
        }
        if (!hasEdge) return 0;

        int length = columns + 1;
        if (length > 129) EnsureCapacity(ref _largeScratch, length);
        Span<LinearAssignment.Node> scratch = length <= 129 ? stackalloc LinearAssignment.Node[length] : _largeScratch.AsSpan(0, length);
        LinearAssignment.Solve(_costs, rows, columns, scratch);
        int matched = 0;
        for (int j = 0; j < columns; j++)
        {
            int i = scratch[j + 1].Row - 1;
            if (i < 0 || _costs[i, j] == BlockedCost) continue;
            int detection = _detectionIndices[transpose ? i : j];
            tracks[_trackIndices[transpose ? j : i]].Observe(detections[detection], time, true);
            _used[detection] = true;
            matched++;
        }
        return matched;
    }

    private void MatchNonSticky(IReadOnlyList<Prediction> detections, long time, float radius, float minConfidence)
    {
        var tracks = CollectionsMarshal.AsSpan(_tracks);
        _pairs.Clear();
        for (int i = 0; i < tracks.Length; i++)
        {
            var box = new MatchBox(in tracks[i]);
            for (int j = 0; j < detections.Count; j++)
            {
                if (detections[j].Confidence < minConfidence) continue;
                float cost = box.Cost(in _candidates[j], radius, false, false);
                if (cost < BlockedCost) _pairs.Add((cost, i, j));
            }
        }
        _pairs.Sort();
        foreach (var (_, i, j) in _pairs)
        {
            if (_used[j] || tracks[i].Measurement != null) continue;
            tracks[i].Observe(detections[j], time, false);
            _used[j] = true;
        }
    }

    private readonly struct Candidate(Prediction p)
    {
        internal readonly int ClassId = p.ClassId;
        internal readonly RectangleF Bounds = p.ScreenBounds;
        internal readonly Vector2 Center = new(p.ScreenCenterX, p.ScreenCenterY);
        internal readonly Vector2 LogSize = new(MathF.Log(p.Rectangle.Width), MathF.Log(p.Rectangle.Height));
    }

    private readonly struct MatchBox(in Track track)
    {
        private readonly int _classId = track.ClassId;
        private readonly Vector2 _center = track.Center;
        private readonly RectangleF _bounds = new(track.Center.X - track.Size.X / 2,
            track.Center.Y - track.Size.Y / 2, track.Size.X, track.Size.Y);
        private readonly RectangleF _last = track.LastBounds;
        private readonly float _inverseScale = 1 / Math.Max(1, track.Size.Length());
        private readonly Vector2 _lastCenter = BoxCenter(track.LastBounds);
        private readonly Vector2 _logSize = new(MathF.Log(track.Size.X), MathF.Log(track.Size.Y));

        internal float Cost(in Candidate p, float radius, bool sticky, bool rescue)
        {
            if (_classId != p.ClassId) return BlockedCost;
            float distance = Vector2.Distance(_center, p.Center);
            RectangleF bounds = _bounds;
            if (distance > radius)
            {
                distance = Vector2.Distance(_lastCenter, p.Center);
                if (distance > radius) return BlockedCost;
                bounds = _last;
            }
            float iou = PredictionFilter.IntersectionOverUnion(bounds, p.Bounds);
            if (rescue && iou < .25f) return BlockedCost;
            var change = p.LogSize - _logSize;
            float growth = (change.X + change.Y) / 2;
            float sizePenalty = (sticky && growth > 0 ? 1.25f : .35f) * MathF.Abs(growth)
                + 1.25f * MathF.Abs(change.X - change.Y);
            return 1 - iou + distance * _inverseScale + sizePenalty;
        }
    }

    private static void EnsureCapacity<T>(ref T[] buffer, int count)
    {
        if (count > buffer.Length) buffer = new T[GrownCapacity(buffer.Length, count)];
    }

    private static int GrownCapacity(int current, int needed) =>
        needed <= current ? current : Math.Max(needed, current * 2);

    internal void Reset()
    {
        _tracks.Clear();
        _lockedId = null;
        _lastObservation = null;
        _nextId = 0;
        Acquired = false;
    }
}

// Shortest augmenting paths with row/column dual potentials. Column zero is the root.
internal static class LinearAssignment
{
    internal struct Node
    {
        internal int Row, Parent, Remaining;
        internal double RowPotential, ColumnPotential, Distance;
        internal bool InitiallyMatched;
    }

    internal static void Solve(float[,] costs, int rows, int columns, Span<Node> nodes)
    {
        nodes.Clear();
        // Row minima are feasible dual potentials. Any distinct minimum edges
        // are already tight, so only the remaining rows need an augmenting path.
        for (int row = 1; row <= rows; row++)
        {
            var edges = MemoryMarshal.CreateReadOnlySpan(ref costs[row - 1, 0], columns);
            int best = 1;
            float minimum = edges[0];
            for (int j = 2; j <= columns; j++)
            {
                float cost = edges[j - 1];
                if (cost < minimum || (cost == minimum && nodes[best].Row != 0 && nodes[j].Row == 0))
                {
                    minimum = cost;
                    best = j;
                }
            }
            nodes[row].RowPotential = minimum;
            if (nodes[best].Row != 0) continue;
            nodes[best].Row = row;
            nodes[row].InitiallyMatched = true;
        }

        for (int root = 1; root <= rows; root++)
        {
            if (nodes[root].InitiallyMatched) continue;
            nodes[0].Row = root;
            for (int j = 1; j <= columns; j++)
            {
                nodes[j].Distance = double.PositiveInfinity;
                nodes[j].Remaining = j;
            }

            int remaining = columns, column = 0;
            double distance = 0;
            do
            {
                int row = nodes[column].Row;
                var edges = MemoryMarshal.CreateReadOnlySpan(ref costs[row - 1, 0], columns);
                double offset = distance - nodes[row].RowPotential;
                double shortest = double.PositiveInfinity;
                int nextIndex = 0;
                for (int k = 1; k <= remaining; k++)
                {
                    int j = nodes[k].Remaining;
                    ref var node = ref nodes[j];
                    double candidate = offset + edges[j - 1] - node.ColumnPotential;
                    if (candidate < node.Distance)
                    {
                        node.Distance = candidate;
                        node.Parent = column;
                    }
                    if (node.Distance < shortest || (node.Distance == shortest && node.Row == 0))
                    {
                        shortest = node.Distance;
                        nextIndex = k;
                    }
                }
                column = nodes[nextIndex].Remaining;
                nodes[nextIndex].Remaining = nodes[remaining].Remaining;
                nodes[remaining--].Remaining = column;
                distance = shortest;
            } while (nodes[column].Row != 0);

            // Apply the path's dual updates once. Settled columns occupy the suffix.
            nodes[root].RowPotential += distance;
            for (int k = remaining + 1; k <= columns; k++)
            {
                ref var node = ref nodes[nodes[k].Remaining];
                if (node.Row == 0) continue;
                double adjustment = distance - node.Distance;
                nodes[node.Row].RowPotential += adjustment;
                node.ColumnPotential -= adjustment;
            }
            do
            {
                int parent = nodes[column].Parent;
                nodes[column].Row = nodes[parent].Row;
                column = parent;
            } while (column != 0);
        }
    }
}
