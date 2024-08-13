using Aimmy2.AILogic.Contracts;
using Aimmy2.Config;
using Aimmy2.InputLogic;
using Aimmy2.InputLogic.Contracts;
using InputLogic;
using Nefarius.ViGEm.Client.Targets.Xbox360;


namespace Aimmy2.AILogic.Actions;

public class RapidFireAction: BaseAction
{
    protected override bool Active => base.Active && AppConfig.Current.ToggleState.RapidFire && (string.IsNullOrWhiteSpace(AppConfig.Current.BindingSettings.RapidFireKey) || InputBindingManager.IsHoldingBinding(nameof(AppConfig.Current.BindingSettings.RapidFireKey)));

    public override async Task ExecuteAsync(Prediction[] predictions)
    {
        if(Active)
        {
            await MouseManager.DoTriggerClick();
            //GamepadManager.GamepadSender.SetSliderValue(GamepadSlider.RightTrigger, 255, GamepadSyncState.Paused);
            //await Task.Delay(25);
            //GamepadManager.GamepadSender.SetSliderValue(GamepadSlider.RightTrigger, 0, GamepadSyncState.Resume);
        }
    }

}