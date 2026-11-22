using System.Diagnostics;

namespace InputLogic
{
    // Owned by one inference loop. Always release using the backend that sent button-down.
    internal sealed class TriggerButton(Func<(Action down, Action up)> getActions)
    {
        private Action? _release;
        private long? _lastClick;

        internal void Release()
        {
            _release?.Invoke();
            _release = null;
        }

        internal Task UpdateAsync(bool eligible, bool spray, int delayMs, CancellationToken cancellation)
        {
            if (!eligible || cancellation.IsCancellationRequested)
            {
                Release();
                return Task.CompletedTask;
            }

            if (spray)
            {
                if (_release == null) Press();
                return Task.CompletedTask;
            }

            Release();
            if (_lastClick is long last && Stopwatch.GetElapsedTime(last).TotalMilliseconds < delayMs)
                return Task.CompletedTask;

            return ClickAsync(cancellation);
        }

        private async Task ClickAsync(CancellationToken cancellation)
        {
            try
            {
                Press();
                await Task.Delay(20, cancellation);
            }
            finally
            {
                Release();
                _lastClick = Stopwatch.GetTimestamp();
            }
        }

        private void Press()
        {
            var (down, up) = getActions();
            _release = up;
            down();
        }
    }
}