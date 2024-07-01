using System;
using System.Collections.Generic;

namespace AILogic
{
    internal class KalmanPrediction
    {
        private readonly Dictionary<int, KalmanFilter2D> kalmanFilters = new Dictionary<int, KalmanFilter2D>();

        public void UpdateKalmanFilter(Target target)
        {
            if (!kalmanFilters.ContainsKey(target.Id))
            {
                kalmanFilters[target.Id] = new KalmanFilter2D();
            }

            kalmanFilters[target.Id].Push(target.X, target.Y);
        }

        public Target GetKalmanPosition(int id)
        {
            if (!kalmanFilters.ContainsKey(id))
                throw new KeyNotFoundException("No filter found for given ID");

            double predictedX = kalmanFilters[id].X;
            double predictedY = kalmanFilters[id].Y;

            return new Target(id, (int)predictedX, (int)predictedY);
        }
    }
}
