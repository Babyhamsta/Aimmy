using System.Windows;
using Aimmy2.AILogic.Contracts;
using Aimmy2.Config;
using Aimmy2.Extensions;
using Aimmy2.Other;
using Class;
using Visuality;


namespace Aimmy2.AILogic.Actions;

public class OverlayAction : BaseAction
{
    private readonly DetectedPlayerWindow _playerOverlay = new();
    

    public override Task ExecuteAsync(Prediction[] predictions)
    {
        if (AppConfig.Current.ToggleState.FOV)
        {
            FOV.Instance.Area = ImageCapture.GetCaptureArea();
            _ = FOV.Instance.UpdateStrictEnclosure();
        }
        if (Active)
        {
            switch (AppConfig.Current.DropdownState.OverlayDrawingMethod)
            {
                case OverlayDrawingMethod.DesktopDC:
                    _= Task.Run(DisableOverlay);
                    PredictionDrawer.DrawPredictions(predictions, ImageCapture.GetCaptureArea());
                    break;
                case OverlayDrawingMethod.WpfWindow:
                    DrawWithWpf(predictions);
                    break;
            }
        }

        return Task.CompletedTask;
    }

    public override Task OnResume()
    {
        FOV.Instance?.MoveToScreenCenter(ImageCapture.Screen);
        _playerOverlay?.MoveToScreenCenter(ImageCapture.Screen);
        return base.OnResume();
    }

    public override Task OnPause()
    {
        DisableOverlay();
        return base.OnPause();
    }

    protected override bool Active => base.Active && AppConfig.Current.ToggleState.ShowDetectedPlayer;

    private void DrawWithWpf(Prediction[] predictions)
    {
        var prediction = predictions.MinBy(p => p.Confidence);
        if (prediction == null)
        {
            DisableOverlay();
        }
        else
        {
            var lastDetectionBox = prediction.TranslatedRectangle;
            var captureArea = ImageCapture.GetCaptureArea();

            Application.Current.Dispatcher.Invoke(() =>
            {
                var scalingFactorX = WinAPICaller.scalingFactorX;
                var scalingFactorY = WinAPICaller.scalingFactorY;
                var centerX = Convert.ToInt16((lastDetectionBox.X + captureArea.Left) / scalingFactorX) + (lastDetectionBox.Width / 2.0);
                var centerY = Convert.ToInt16((lastDetectionBox.Y + captureArea.Top) / scalingFactorY);

                if (AppConfig.Current.ToggleState.ShowAIConfidence)
                {
                    _playerOverlay.DetectedPlayerConfidence.Opacity = 1;
                    _playerOverlay.DetectedPlayerConfidence.Content = $"{Math.Round((prediction.Confidence * 100), 2)}%";

                    var labelEstimatedHalfWidth = _playerOverlay.DetectedPlayerConfidence.ActualWidth / 2.0;
                    _playerOverlay.DetectedPlayerConfidence.Margin = new Thickness(centerX - labelEstimatedHalfWidth, centerY - _playerOverlay.DetectedPlayerConfidence.ActualHeight - 2, 0, 0);
                }

                var showTracers = AppConfig.Current.ToggleState.ShowTracers;
                _playerOverlay.DetectedTracers.Opacity = showTracers ? 1 : 0;
                if (showTracers)
                {
                    _playerOverlay.DetectedTracers.X1 = captureArea.GetBottomCenter().X;
                    _playerOverlay.DetectedTracers.Y1 = captureArea.GetBottomCenter().Y;
                    _playerOverlay.DetectedTracers.X2 = centerX;
                    _playerOverlay.DetectedTracers.Y2 = centerY + lastDetectionBox.Height;
                }

                _playerOverlay.Opacity = AppConfig.Current.SliderSettings.Opacity;

                _playerOverlay.DetectedPlayerFocus.Opacity = 1;
                _playerOverlay.DetectedPlayerFocus.Margin = new Thickness(centerX - (lastDetectionBox.Width / 2.0), centerY, 0, 0);
                _playerOverlay.DetectedPlayerFocus.Width = lastDetectionBox.Width;
                _playerOverlay.DetectedPlayerFocus.Height = lastDetectionBox.Height;

                _playerOverlay.SetHeadRelativeArea(AppConfig.Current.ToggleState.ShowTriggerHeadArea ? prediction.HeadRelativeRect : null);
            });
        }
    }


    private void DisableOverlay()
    {
        try
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _playerOverlay.Opacity = 0;
            });
        }
        catch (Exception e)
        {}
    }
}