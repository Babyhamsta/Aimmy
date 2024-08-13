using Gma.System.MouseKeyHook;
using System.Windows.Forms;
using Aimmy2.InputLogic;
using Aimmy2.InputLogic.Contracts;
using Aimmy2.InputLogic.Gamepad.Interaction;

namespace InputLogic
{
    internal class InputBindingManager
    {
        private IKeyboardMouseEvents? _mEvents;
        private bool _gamepadListen;
        private readonly Dictionary<string, string> bindings = new();
        private static readonly Dictionary<string, (bool IsHolding, DateTime StartTime)> isHolding = new();
        private string? settingBindingId = null;

        public event Action<string, string>? OnBindingSet;

        public event Action<string>? OnBindingPressed;

        public event Action<string>? OnBindingReleased;

        public static bool IsValidKey(string key)
        {
            return !string.IsNullOrWhiteSpace(key) && key.ToLower() != "none";
        }

        public static bool IsHoldingBinding(string bindingId)
        {
            return isHolding.TryGetValue(bindingId, out var holding) && holding.IsHolding;
        }

        public static TimeSpan GetHoldingTime(string bindingId)
        {
            if (isHolding.TryGetValue(bindingId, out var holding) && holding.IsHolding)
            {
                return DateTime.Now - holding.StartTime;
            }
            return TimeSpan.Zero;
        }

        public static bool IsHoldingBindingFor(string bindingId, TimeSpan? duration)
        {
            if(duration == null || duration <= TimeSpan.Zero)
                return IsHoldingBinding(bindingId);
            return GetHoldingTime(bindingId) >= duration;
        }

        public void SetupDefault(string bindingId, string keyCode)
        {
            bindings[bindingId] = keyCode;
            isHolding[bindingId] = (false, DateTime.MinValue);
            OnBindingSet?.Invoke(bindingId, keyCode);
            EnsureHookEvents();
        }

        public void StartListeningForBinding(string bindingId)
        {
            settingBindingId = bindingId;
            EnsureHookEvents();
        }

        private void EnsureHookEvents()
        {
            if (_mEvents == null)
            {
                _mEvents = Hook.GlobalEvents();
                _mEvents.KeyDown += GlobalHookKeyDown!;
                _mEvents.MouseDown += GlobalHookMouseDown!;
                _mEvents.KeyUp += GlobalHookKeyUp!;
                _mEvents.MouseUp += GlobalHookMouseUp!;
            }

            if (!_gamepadListen && GamepadManager.CanRead)
            {
                _gamepadListen = true;
                GamepadManager.GamepadReader.ButtonEvent += GamepadReader_ButtonEvent;
            }
        }

        private void GamepadReader_ButtonEvent(object? sender, GamepadEventArgs e)
        {
            if (!e.IsStickEvent)
            {
                var pressed = e.IsPressed == true;
                if (settingBindingId != null)
                {
                    bindings[settingBindingId] = e.Code;
                    OnBindingSet?.Invoke(settingBindingId, e.Code);
                    settingBindingId = null;
                }
                else
                {
                    foreach (var binding in bindings)
                    {
                        if (binding.Value == e.Code)
                        {
                            if (pressed)
                            {
                                isHolding[binding.Key] = (true, DateTime.Now);
                                OnBindingPressed?.Invoke(binding.Key);
                            }
                            else
                            {
                                isHolding[binding.Key] = (false, DateTime.MinValue);
                                OnBindingReleased?.Invoke(binding.Key);
                            }
                        }
                    }
                }
            }
        }

        private void GlobalHookKeyDown(object sender, KeyEventArgs e)
        {
            if (settingBindingId != null)
            {
                bindings[settingBindingId] = e.KeyCode.ToString();
                OnBindingSet?.Invoke(settingBindingId, e.KeyCode.ToString());
                settingBindingId = null;
            }
            else
            {
                foreach (var binding in bindings)
                {
                    if (binding.Value == e.KeyCode.ToString())
                    {
                        isHolding[binding.Key] = (true, DateTime.Now);
                        OnBindingPressed?.Invoke(binding.Key);
                    }
                }
            }
        }

        private void GlobalHookMouseDown(object sender, MouseEventArgs e)
        {
            if (settingBindingId != null)
            {
                bindings[settingBindingId] = e.Button.ToString();
                OnBindingSet?.Invoke(settingBindingId, e.Button.ToString());
                settingBindingId = null;
            }
            else
            {
                foreach (var binding in bindings)
                {
                    if (binding.Value == e.Button.ToString())
                    {
                        isHolding[binding.Key] = (true, DateTime.Now);
                        OnBindingPressed?.Invoke(binding.Key);
                    }
                }
            }
        }

        private void GlobalHookKeyUp(object sender, KeyEventArgs e)
        {
            foreach (var binding in bindings)
            {
                if (binding.Value == e.KeyCode.ToString())
                {
                    isHolding[binding.Key] = (false, DateTime.MinValue);
                    OnBindingReleased?.Invoke(binding.Key);
                }
            }
        }

        private void GlobalHookMouseUp(object sender, MouseEventArgs e)
        {
            foreach (var binding in bindings)
            {
                if (binding.Value == e.Button.ToString())
                {
                    isHolding[binding.Key] = (false, DateTime.MinValue);
                    OnBindingReleased?.Invoke(binding.Key);
                }
            }
        }

        public void StopListening()
        {
            if (_gamepadListen)
            {
                GamepadManager.GamepadReader.ButtonEvent -= GamepadReader_ButtonEvent;
                _gamepadListen = false;
            }
            if (_mEvents != null)
            {
                _mEvents.KeyDown -= GlobalHookKeyDown!;
                _mEvents.MouseDown -= GlobalHookMouseDown!;
                _mEvents.KeyUp -= GlobalHookKeyUp!;
                _mEvents.MouseUp -= GlobalHookMouseUp!;
                _mEvents.Dispose();
                _mEvents = null;
            }
        }

        public static void SendKey(dynamic key)
        {
            InputSender.SendKey(key);
        }
    }
}
