using Accord.Statistics.Running;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;

namespace AILogic
{
    internal class KalmanPrediction
    {
        public struct Detection
        {
            public int X;
            public int Y;
            public DateTime Timestamp;
        }

        private readonly KalmanFilter2D kalmanFilter = new KalmanFilter2D();
        private DateTime lastFilterUpdateTime = DateTime.UtcNow;

        public void UpdateKalmanFilter(Detection detection)
        {
            kalmanFilter.Push(detection.X, detection.Y);
            lastFilterUpdateTime = DateTime.UtcNow;
        }

        public Detection GetKalmanPosition()
        {
            double timeStep = (DateTime.UtcNow - lastFilterUpdateTime).TotalSeconds;

            double predictedX = kalmanFilter.X + kalmanFilter.XAxisVelocity * timeStep;
            double predictedY = kalmanFilter.Y + kalmanFilter.YAxisVelocity * timeStep;

            return new Detection { X = (int)predictedX, Y = (int)predictedY };
        }
    }

    internal class WiseTheFoxPrediction
    {
        /// <summary>
        /// Proof of Concept Prediction as written by @wisethef0x
        /// "Exponential Moving Average"
        /// </summary>
        public struct WTFDetection
        {
            public int X;
            public int Y;
            public DateTime Timestamp;
        }

        private DateTime lastUpdateTime;
        private const double alpha = 0.5; // Smoothing factor, adjust as necessary

        private double emaX;
        private double emaY;

        public WiseTheFoxPrediction()
        {
            lastUpdateTime = DateTime.UtcNow;
        }

        public void UpdateDetection(WTFDetection detection)
        {
            double newX = lastUpdateTime == DateTime.MinValue ? detection.X : alpha * detection.X + (1 - alpha) * emaX;
            double newY = lastUpdateTime == DateTime.MinValue ? detection.Y : alpha * detection.Y + (1 - alpha) * emaY;

            emaX = newX;
            emaY = newY;

            lastUpdateTime = DateTime.UtcNow;
        }

        public WTFDetection GetEstimatedPosition()
        {
            return new WTFDetection { X = (int)emaX, Y = (int)emaY };
        }
    }

    internal class ShalloePrediction
    {
        private const int ScreenResolution = 640 * 640;

        //var ScreenResolution = WinAPICaller.ScreenWidth * WinAPICaller.ScreenHeight;
        private const int BulletSpeedX = 10;

        private const int BulletSpeedY = 1;

        public static int GetShalloePredictionX(int CurrentX, int PrevX, int EnemyWidth, int EnemyHeight)
        {
            var xVelocity = CurrentX - PrevX;
            var EnemySize = EnemyWidth * EnemyHeight;
            var EnemyDistance = 1 - (EnemySize / ScreenResolution);

            return WinAPICaller.GetCursorPosition().X + (xVelocity * (EnemyDistance * BulletSpeedX));
        }

        public static int GetShalloePredictionY(int CurrentY, int PrevY, int EnemyWidth, int EnemyHeight)
        {
            var yVelocity = CurrentY - PrevY;
            var EnemySize = EnemyWidth * EnemyHeight;
            var EnemyDistance = (1 - (EnemySize / ScreenResolution));

            return WinAPICaller.GetCursorPosition().Y + (yVelocity * (EnemyDistance * BulletSpeedY));
        }
    }

    internal class ShalloePredictionV2
    {
        public static List<int> xValues = new List<int>();
        public static List<int> yValues = new List<int>();

        public static int AmountCount = 2;

        public static int GetSPX()
        {
            //Debug.WriteLine((((int)Queryable.Average(xValues.AsQueryable()) * AmountCount) + WinAPICaller.GetCursorPosition().X) * (1 - Dictionary.sliderSettings["Mouse Sensitivity (+/-)"]));
            return (int)(((Queryable.Average(xValues.AsQueryable()) * AmountCount) + WinAPICaller.GetCursorPosition().X));
        }

        public static int GetSPY()
        {
            //Debug.WriteLine((int)Queryable.Average(yValues.AsQueryable()));
            return (int)(((Queryable.Average(yValues.AsQueryable()) * AmountCount) + WinAPICaller.GetCursorPosition().Y));
        }
    }

    internal class HoodPredict
    {
        public static List<int> xValues = new List<int>();
        public static List<int> yValues = new List<int>();

        public static int AmountCount = 2;

        public static int GetHPX(int CurrentX, int PrevX)
        {
            int CurrentTime = DateTime.Now.Millisecond;
            return 1;
        }

        public static int GetHPY()
        {
            //Debug.WriteLine((int)Queryable.Average(yValues.AsQueryable()));
            return (int)(((Queryable.Average(yValues.AsQueryable()) * AmountCount) + WinAPICaller.GetCursorPosition().Y));
        }
    }

    internal class ApraxoPrediction
    {
        private const double MaxSensitivity = 1.0;  // Maximum sensitivity factor
        private const double MinSensitivity = 0.4;  // Minimum sensitivity factor

        private Point detectionCenter;  // Center of detection area (e.g., screen center)

        public ApraxoPrediction(Point center)
        {
            detectionCenter = center;
        }

        public int PredictXPosition(int targetX)
        {
            double distanceToCenter = Math.Abs(targetX - detectionCenter.X);
            double sensitivityFactor = CalculateSensitivityFactor(distanceToCenter);

            // Example prediction logic based on sensitivity factor
            return (int)(targetX + sensitivityFactor * (targetX - detectionCenter.X));
        }

        public int PredictYPosition(int targetY)
        {
            double distanceToCenter = Math.Abs(targetY - detectionCenter.Y);
            double sensitivityFactor = CalculateSensitivityFactor(distanceToCenter);

            // Example prediction logic based on sensitivity factor
            return (int)(targetY + sensitivityFactor * (targetY - detectionCenter.Y));
        }

        private double CalculateSensitivityFactor(double distanceToCenter)
        {
            // Adjust sensitivity based on distance to center
            if (distanceToCenter > 100)  // Example threshold distance
            {
                // Scale sensitivity from MinSensitivity to MaxSensitivity as distance increases
                return MinSensitivity + (distanceToCenter / 100) * (MaxSensitivity - MinSensitivity);
            }
            else
            {
                return MinSensitivity;  // Minimum sensitivity when close to center
            }
        }
    }
}
