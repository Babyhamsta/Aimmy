using Aimmy2.AILogic.Contracts;
using Aimmy2.Config;
using InputLogic;

namespace Aimmy2.AILogic.Actions;

public class AutoTriggerAction: BaseAction
{
    private Prediction? _lastPrediction;
    private CancellationTokenSource? _autoTriggerCts;

    private void CancelTriggerChargeIf()
    {
        if (_autoTriggerCts is { IsCancellationRequested: false })
            _autoTriggerCts.Cancel();
    }

    public override Task ExecuteAsync(Prediction[] predictions)
    {
        var closestPrediction = predictions.MinBy(p => p.Confidence);
        if (closestPrediction != null)
        {
            _lastPrediction = closestPrediction;
            return AutoTrigger(closestPrediction);
        }
        return Task.CompletedTask;
    }

    private async Task AutoTrigger(Prediction prediction)
    {
        if (AppConfig.Current.ToggleState.AutoTrigger)
        {
            var delay = TimeSpan.FromSeconds(AppConfig.Current.SliderSettings.AutoTriggerDelay);
            if (AppConfig.Current.ToggleState.AutoTriggerCharged)
            {
                // JUST FOR TESTING
                if (!MouseManager.IsLeftDown && _autoTriggerCts == null)
                {
                    _autoTriggerCts = new CancellationTokenSource();
                    _autoTriggerCts.Token.Register(() => _autoTriggerCts = null);
                    _ = MouseManager.LeftDownUntil(() => Task.FromResult(TriggerKeyUnsetOrHold() && PredictionIsIntersecting()), delay, _autoTriggerCts.Token).ContinueWith(_ => CancelTriggerChargeIf());
                }
                return;
            }
            if (TriggerKeyUnsetOrHold())
            {
                //await MouseManager.LeftDownUntil(() => PredictionIsIntersecting());
                //return;
                if (PredictionIsIntersecting(prediction))
                {
                    await Task.Delay(delay);
                    if (InputBindingManager.IsValidKey(AppConfig.Current.BindingSettings.TriggerAdditionalSend))
                    {
                        InputBindingManager.SendKey(AppConfig.Current.BindingSettings.TriggerAdditionalSend);
                    }
                    await MouseManager.DoTriggerClick();
                }
            }
        }
    }

    private bool TriggerKeyUnsetOrHold()
    {
        var triggerKey = AppConfig.Current.BindingSettings.TriggerKey;
        return string.IsNullOrEmpty(triggerKey) || triggerKey == "None" || InputBindingManager.IsHoldingBindingFor(nameof(AppConfig.Current.BindingSettings.TriggerKey), TimeSpan.FromSeconds(AppConfig.Current.SliderSettings.TriggerKeyMin));
    }


    private bool PredictionIsIntersecting(Prediction? prediction = null)
    {
        prediction ??= _lastPrediction;
        if (prediction == null)
        {
            return false;
        }
        return AppConfig.Current.DropdownState.TriggerCheck == TriggerCheck.None
               || (AppConfig.Current.DropdownState.TriggerCheck == TriggerCheck.HeadIntersectingCenter && prediction.IntersectsWithCenterOfHeadRelativeRect)
               || (AppConfig.Current.DropdownState.TriggerCheck == TriggerCheck.IntersectingCenter && prediction.InteractsWithCenterOfFov);
    }
}