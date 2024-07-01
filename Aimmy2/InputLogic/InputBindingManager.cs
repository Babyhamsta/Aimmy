namespace InputLogic
{
    internal class InputBindingManager
    {
        // Existing code ...

        private readonly Dictionary<string, string> actionBindings = new();

        public void SetupDefaultAction(string actionId, string keyCode, string action)
        {
            bindings[actionId] = keyCode;
            actionBindings[actionId] = action;
            isHolding[actionId] = false;
            OnBindingSet?.Invoke(actionId, keyCode);
            EnsureHookEvents();
        }

        public void StartListeningForActionBinding(string actionId)
        {
            settingBindingId = actionId;
            EnsureHookEvents();
        }

        private void GlobalHookKeyDown(object sender, KeyEventArgs e)
        {
            if (settingBindingId != null)
            {
                bindings[settingBindingId] = e.KeyCode.ToString();
                actionBindings[settingBindingId] = "SinglePress"; // Default action
                OnBindingSet?.Invoke(settingBindingId, e.KeyCode.ToString());
                settingBindingId = null;
            }
            else
            {
                foreach (var binding in bindings)
                {
                    if (binding.Value == e.KeyCode.ToString())
                    {
                        isHolding[binding.Key] = true;
                        OnBindingPressed?.Invoke(binding.Key);

                        if (binding.Key == "AutoTriggerToggle")
                        {
                            MouseManager.AutoTriggerEnabled = !MouseManager.AutoTriggerEnabled;
                        }
                        else
                        {
                            MouseManager.PerformKeyAction(binding.Value, actionBindings[binding.Key]);
                        }
                    }
                }
            }
        }

        private void GlobalHookMouseDown(object sender, MouseEventArgs e)
        {
            if (settingBindingId != null)
            {
                bindings[settingBindingId] = e.Button.ToString();
                actionBindings[settingBindingId] = "SinglePress"; // Default action
                OnBindingSet?.Invoke(settingBindingId, e.Button.ToString());
                settingBindingId = null;
            }
            else
            {
                foreach (var binding in bindings)
                {
                    if (binding.Value == e.Button.ToString())
                    {
                        isHolding[binding.Key] = true;
                        OnBindingPressed?.Invoke(binding.Key);

                        if (binding.Key == "AutoTriggerToggle")
                        {
                            MouseManager.AutoTriggerEnabled = !MouseManager.AutoTriggerEnabled;
                        }
                        else
                        {
                            MouseManager.PerformKeyAction(binding.Value, actionBindings[binding.Key]);
                        }
                    }
                }
            }
        }

        // Existing code for KeyUp and MouseUp handlers ...
    }
}
