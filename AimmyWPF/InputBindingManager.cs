using Gma.System.MouseKeyHook;
using System;
using System.Diagnostics;
using System.Windows.Forms;

public class InputBindingManager
{
    private IKeyboardMouseEvents _mEvents;
    private bool isSettingBinding = false;

    public string CurrentBinding { get; private set; }

    public event Action<string> OnBindingSet;
    public event Action<string> OnBindingPressed;
    public event Action<string> OnBindingReleased;

    public void SetupDefault(string KeyCode)
    {
        CurrentBinding = KeyCode.ToString();
        OnBindingSet?.Invoke(CurrentBinding);

        _mEvents = Hook.GlobalEvents();
        _mEvents.KeyDown += GlobalHookKeyDown;
        _mEvents.MouseDown += GlobalHookMouseDown;
        _mEvents.KeyUp += GlobalHookKeyUp;
        _mEvents.MouseUp += GlobalHookMouseUp;
    }

    public void StartListeningForBinding()
    {
        isSettingBinding = true;
        if (_mEvents == null)
        {
            _mEvents = Hook.GlobalEvents();
            _mEvents.KeyDown += GlobalHookKeyDown;
            _mEvents.MouseDown += GlobalHookMouseDown;
            _mEvents.KeyUp += GlobalHookKeyUp;
            _mEvents.MouseUp += GlobalHookMouseUp;
        }
    }

    private void GlobalHookKeyDown(object sender, KeyEventArgs e)
    {
        if (isSettingBinding)
        {
            CurrentBinding = e.KeyCode.ToString();
            OnBindingSet?.Invoke(CurrentBinding);
            isSettingBinding = false;
        }
        else if (CurrentBinding == e.KeyCode.ToString())
        {
            OnBindingPressed?.Invoke(CurrentBinding);
        }
    }

    private void GlobalHookMouseDown(object sender, MouseEventArgs e)
    {
        if (isSettingBinding)
        {
            CurrentBinding = e.Button.ToString();
            OnBindingSet?.Invoke(CurrentBinding);
            isSettingBinding = false;
        }
        else if (CurrentBinding == e.Button.ToString())
        {
            OnBindingPressed?.Invoke(CurrentBinding);
        }
    }

    private void GlobalHookKeyUp(object sender, KeyEventArgs e)
    {
        if (CurrentBinding == e.KeyCode.ToString())
        {
            OnBindingReleased?.Invoke(CurrentBinding);
        }
    }

    private void GlobalHookMouseUp(object sender, MouseEventArgs e)
    {
        if (CurrentBinding == e.Button.ToString())
        {
            OnBindingReleased?.Invoke(CurrentBinding);
        }
    }

    public void StopListening()
    {
        if (_mEvents == null) return;

        _mEvents.KeyDown -= GlobalHookKeyDown;
        _mEvents.MouseDown -= GlobalHookMouseDown;
        _mEvents.KeyUp -= GlobalHookKeyUp;
        _mEvents.MouseUp -= GlobalHookMouseUp;
        _mEvents.Dispose();
        _mEvents = null;
    }
}
