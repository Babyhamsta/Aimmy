using System.Drawing;
using AILogic;
using Aimmy2.AILogic.Contracts;
using Aimmy2.Config;
using Class;
using InputLogic;

namespace Aimmy2.AILogic.Actions;

public class AimingAction : BaseAction
{
    private int PrevX = 0;
    private int PrevY = 0;
    private int detectedX { get; set; }
    private int detectedY { get; set; }

    private KalmanPrediction kalmanPrediction = new();
    private WiseTheFoxPrediction wtfpredictionManager = new();

    public override Task ExecuteAsync(Prediction[] predictions)
    {
        var closestPrediction = predictions.MinBy(p => p.Confidence);
        if (closestPrediction != null)
        {
            HandleAim(closestPrediction);
        }
        return Task.CompletedTask;
    }

    private void CalculateCoordinates(Prediction closestPrediction, float scaleX, float scaleY)
    {
        double YOffset = AppConfig.Current.SliderSettings.YOffset;
        double XOffset = AppConfig.Current.SliderSettings.XOffset;

        double YOffsetPercentage = AppConfig.Current.SliderSettings.YOffsetPercentage;
        double XOffsetPercentage = AppConfig.Current.SliderSettings.XOffsetPercentage;

        var rect = closestPrediction.Rectangle;

        if (AppConfig.Current.ToggleState.XAxisPercentageAdjustment)
        {
            detectedX = (int)((rect.X + (rect.Width * (XOffsetPercentage / 100))) * scaleX);
        }
        else
        {
            detectedX = (int)((rect.X + rect.Width / 2) * scaleX + XOffset);
        }

        if (AppConfig.Current.ToggleState.YAxisPercentageAdjustment)
        {
            detectedY = (int)((rect.Y + rect.Height - (rect.Height * (YOffsetPercentage / 100))) * scaleY + YOffset);
        }
        else
        {
            detectedY = CalculateDetectedY(scaleY, YOffset, closestPrediction);
        }
    }

    private static int CalculateDetectedY(float scaleY, double YOffset, Prediction closestPrediction)
    {
        var rect = closestPrediction.Rectangle;
        float yBase = rect.Y;
        float yAdjustment = 0;

        switch (AppConfig.Current.DropdownState.AimingBoundariesAlignment)
        {
            case AimingBoundariesAlignment.Center:
                yAdjustment = rect.Height / 2;
                break;

            case AimingBoundariesAlignment.Top:
                // yBase is already at the top
                break;

            case AimingBoundariesAlignment.Bottom:
                yAdjustment = rect.Height;
                break;
        }

        return (int)((yBase + yAdjustment) * scaleY + YOffset);
    }

    private void HandleAim(Prediction closestPrediction)
    {
        if (AppConfig.Current.ToggleState.AimAssist && (AppConfig.Current.ToggleState.ConstantAITracking
                                                        || AppConfig.Current.ToggleState.AimAssist && InputBindingManager.IsHoldingBinding(nameof(AppConfig.Current.BindingSettings.AimKeybind))
                                                        || AppConfig.Current.ToggleState.AimAssist && InputBindingManager.IsHoldingBinding(nameof(AppConfig.Current.BindingSettings.SecondAimKeybind))))
        {
            var area = ImageCapture.GetCaptureArea();
            float scaleX = area.Width / 640f;
            float scaleY = area.Height / 640f;

            CalculateCoordinates(closestPrediction, scaleX, scaleY);
            if (AppConfig.Current.ToggleState.Predictions)
            {
                HandlePredictions(kalmanPrediction, closestPrediction, area);
            }
            else
            {
                MouseManager.MoveCrosshair(detectedX, detectedY, area);
            }
        }
    }

    private void HandlePredictions(KalmanPrediction kalmanPrediction, Prediction closestPrediction, Rectangle area )
    {
        var predictionMethod = AppConfig.Current.DropdownState.PredictionMethod;
        switch (predictionMethod)
        {
            case PredictionMethod.KalmanFilter:
                KalmanPrediction.Detection detection = new()
                {
                    X = detectedX,
                    Y = detectedY,
                    Timestamp = DateTime.UtcNow
                };

                kalmanPrediction.UpdateKalmanFilter(detection);
                var predictedPosition = kalmanPrediction.GetKalmanPosition();

                MouseManager.MoveCrosshair(predictedPosition.X, predictedPosition.Y, area);
                break;

            case PredictionMethod.Shall0:
                ShalloePredictionV2.xValues.Add(detectedX - PrevX);
                ShalloePredictionV2.yValues.Add(detectedY - PrevY);

                ShalloePredictionV2.xValues = ShalloePredictionV2.xValues.TakeLast(5).ToList();
                ShalloePredictionV2.yValues = ShalloePredictionV2.yValues.TakeLast(5).ToList();

                MouseManager.MoveCrosshair(ShalloePredictionV2.GetSPX(), detectedY, area);

                PrevX = detectedX;
                PrevY = detectedY;
                break;

            case PredictionMethod.WiseThef0x:
                WiseTheFoxPrediction.WTFDetection wtfdetection = new()
                {
                    X = detectedX,
                    Y = detectedY,
                    Timestamp = DateTime.UtcNow
                };

                wtfpredictionManager.UpdateDetection(wtfdetection);
                var wtfpredictedPosition = wtfpredictionManager.GetEstimatedPosition();

                MouseManager.MoveCrosshair(wtfpredictedPosition.X, detectedY, area);
                break;
        }
    }
}
