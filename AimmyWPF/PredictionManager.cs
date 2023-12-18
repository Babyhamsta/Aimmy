using System;
using Accord.Statistics.Running;

namespace AimmyWPF
{
    internal class PredictionManager
    {
        public struct Detection
        {
            public int X;
            public int Y;
            public DateTime Timestamp;
        }

        KalmanFilter2D kalmanFilter;
        private DateTime lastUpdateTime;

        public PredictionManager()
        {
            kalmanFilter = new KalmanFilter2D();
            lastUpdateTime = DateTime.UtcNow;
        }

        public void UpdateKalmanFilter(Detection detection)
        {
            var currentTime = DateTime.UtcNow;

            kalmanFilter.Push(detection.X, detection.Y);
            lastUpdateTime = currentTime;
        }

        public Detection GetEstimatedPosition()
        {
            // Current estimated position
            double currentX = kalmanFilter.X;
            double currentY = kalmanFilter.Y;

            // Current velocity
            double velocityX = kalmanFilter.XAxisVelocity;
            double velocityY = kalmanFilter.YAxisVelocity;

            // Calculate time since last update
            double timeStep = (DateTime.UtcNow - lastUpdateTime).TotalSeconds;

            // Predict next position based on current position and velocity
            double predictedX = currentX + velocityX * timeStep;
            double predictedY = currentY + velocityY * timeStep;

            return new Detection { X = (int)predictedX, Y = (int)predictedY };
        }
    }
}
