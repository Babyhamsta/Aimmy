using System;
using System.Collections.Generic;
using System.Text;
using Accord.Math;
using Accord.Statistics.Filters;
using Accord.Statistics.Running;
using static Accord.Math.FourierTransform;

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

        public void InitializeKalmanFilter()
        {
            kalmanFilter = new KalmanFilter2D();
        }

        public void UpdateKalmanFilter(double detectedX, double detectedY)
        {
            kalmanFilter.Push(detectedX, detectedY);
        }

        public Detection GetEstimatedPosition()
        {
            // Current estimated position
            double currentX = kalmanFilter.X;
            double currentY = kalmanFilter.Y;

            // Current velocity
            double velocityX = kalmanFilter.XAxisVelocity;
            double velocityY = kalmanFilter.YAxisVelocity;

            // Predict next position based on current position and velocity
            double timeStep = 0.01;
            double predictedX = currentX + velocityX * timeStep;
            double predictedY = currentY + velocityY * timeStep;

            return new Detection { X = (int)predictedX, Y = (int)predictedY };
        }
    }
}
