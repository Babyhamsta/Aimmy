using Accord.Statistics.Running;
using Class;

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
            emaX = lastUpdateTime == DateTime.MinValue ? detection.X : alpha * detection.X + (1 - alpha) * emaX;
            emaY = lastUpdateTime == DateTime.MinValue ? detection.Y : alpha * detection.Y + (1 - alpha) * emaY;

            lastUpdateTime = DateTime.UtcNow;
        }

        public WTFDetection GetEstimatedPosition()
        {
            return new WTFDetection { X = (int)emaX, Y = (int)emaY };
        }
    }

    internal class ShalloePredictionV2
    {
        public static List<int> xValues = [];
        public static List<int> yValues = [];

        public static int AmountCount = 2;

        public static void AddValues(int x, int y)
        {
            if (xValues.Count >= AmountCount)
            {
                xValues.RemoveAt(0);
            }
            if (yValues.Count >= AmountCount)
            {
                yValues.RemoveAt(0);
            }
            xValues.Add(x);
            yValues.Add(y);
        }

        public static int GetSPX()
        {
            return (int)(xValues.Average() * AmountCount + WinAPICaller.GetCursorPosition().X);
        }

        public static int GetSPY()
        {
            return (int)(yValues.Average() * AmountCount + WinAPICaller.GetCursorPosition().Y);
        }
    }

    //internal class HoodPredict
    //{
    //    public static List<int> xValues = [];
    //    public static List<int> yValues = [];

    //    public static int AmountCount = 2;

    //    public static int GetHPX(int CurrentX, int PrevX)
    //    {
    //        int CurrentTime = DateTime.Now.Millisecond;
    //        return 1;
    //    }

    //    public static int GetSPY()
    //    {
    //        //Debug.WriteLine((int)Queryable.Average(yValues.AsQueryable()));
    //        return (int)(((Queryable.Average(yValues.AsQueryable()) * AmountCount) + WinAPICaller.GetCursorPosition().Y));
    //    }
    //}
}