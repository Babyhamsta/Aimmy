using System.ComponentModel;
using Aimmy2.AILogic.Contracts;
using Aimmy2.Config;
using Aimmy2.InputLogic;
using InputLogic;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using Nextended.Core;

namespace Aimmy2.AILogic.Actions;

public class AutoTriggerAction: BaseAction
{
    private Prediction? _lastPrediction;
    private CancellationTokenSource? _autoTriggerCts;

    public AutoTriggerAction()
    {
        AppConfig.Current.ToggleState.PropertyChanged += CheckChange;
    }

    private void CheckChange(object? sender, PropertyChangedEventArgs e)
    {
        if(e.PropertyName is nameof(AppConfig.Current.ToggleState.AutoTrigger) or nameof(AppConfig.Current.ToggleState.AutoTriggerCharged))
        {
            CancelCharge();
        }            
    }

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

    public override Task OnPause()
    {
        CancelCharge();
        return base.OnPause();
    }

    private void CancelCharge()
    {
        CancelTriggerChargeIf();
        _autoTriggerCts = null;
        
        if (MouseManager.IsLeftDown)
        {
            MouseManager.LeftUp();
        }
    }


    private async Task AutoTrigger(Prediction prediction)
    {
        if (AppConfig.Current.ToggleState.AutoTrigger)
        {
            var delay = TimeSpan.FromSeconds(AppConfig.Current.SliderSettings.AutoTriggerDelay);
            if (AppConfig.Current.ToggleState.AutoTriggerCharged && TriggerKeyUnsetOrHold())
            {
                if (!MouseManager.IsLeftDown && _autoTriggerCts == null)
                {
                    _autoTriggerCts = new CancellationTokenSource();
                    _autoTriggerCts.Token.Register(() => _autoTriggerCts = null);
                    _ = MouseManager.LeftDownUntil(() => Task.FromResult(TriggerKeyUnsetOrHold() && PredictionIsIntersecting(AppConfig.Current.DropdownState.TriggerCheck)), delay, _autoTriggerCts.Token).ContinueWith(_ => CancelTriggerChargeIf());
                }
                return;
            }

            if (InputBindingManager.IsValidKey(AppConfig.Current.BindingSettings.TriggerAdditionalSend) && TriggerCommandKeyUnsetOrHold() && PredictionIsIntersecting(AppConfig.Current.DropdownState.TriggerAdditionalCommandCheck, prediction))
            {
                InputBindingManager.SendKey(AppConfig.Current.BindingSettings.TriggerAdditionalSend);
            }

            if (TriggerKeyUnsetOrHold())
            {
                if (PredictionIsIntersecting(AppConfig.Current.DropdownState.TriggerCheck, prediction))
                {
                    await Task.Delay(delay);
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

    private bool TriggerCommandKeyUnsetOrHold()
    {
        var triggerKey = AppConfig.Current.BindingSettings.TriggerAdditionalCommandKey;
        return string.IsNullOrEmpty(triggerKey) || triggerKey == "None" || InputBindingManager.IsHoldingBinding(nameof(AppConfig.Current.BindingSettings.TriggerAdditionalCommandKey));
    }

    private bool PredictionIsIntersecting(TriggerCheck check, Prediction? prediction = null)
    {
        prediction ??= _lastPrediction;
        if (prediction == null)
        {
            return false;
        }
        return check == TriggerCheck.None
               || (check == TriggerCheck.HeadIntersectingCenter && prediction.IntersectsWithCenterOfHeadRelativeRect)
               || (check == TriggerCheck.IntersectingCenter && prediction.InteractsWithCenterOfFov);
    }
}