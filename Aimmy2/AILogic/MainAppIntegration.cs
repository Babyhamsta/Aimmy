class Program
{
    static void Main()
    {
        AimAlignmentSystem aimAlignmentSystem = new AimAlignmentSystem();
        KalmanPrediction kalmanPrediction = new KalmanPrediction();
        
        // Assume this is a method to get all current detections
        List<Target> detections = GetCurrentDetections();

        foreach (var detection in detections)
        {
            kalmanPrediction.UpdateKalmanFilter(detection);
        }

        aimAlignmentSystem.UpdateDetections(detections);
        aimAlignmentSystem.AlignAim();
    }

    static List<Target> GetCurrentDetections()
    {
        // This should return a list of current detections
        return new List<Target>
        {
            new Target(1, 100, 150),
            new Target(2, 200, 250),
            // Add more detections as necessary
        };
    }
}
