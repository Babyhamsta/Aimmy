using System.Runtime.InteropServices;
using System.Windows;

namespace Aimmy2.Class
{
    /// <summary>
    /// Central manager for handling multi-display functionality
    /// </summary>
    public static class DisplayManager
    {
        private static DisplayInfo? _currentDisplay;
        private static int _currentDisplayIndex = 0;
        private static List<DisplayInfo> _displays = new List<DisplayInfo>();
        private static readonly object _lockObject = new object();
        private static bool _initialized = false;

        public static event EventHandler<DisplayChangedEventArgs>? DisplayChanged;

        // Properties for current display
        public static int CurrentDisplayIndex
        {
            get
            {
                lock (_lockObject)
                {
                    return _currentDisplayIndex;
                }
            }
        }

        public static DisplayInfo? CurrentDisplay
        {
            get
            {
                lock (_lockObject)
                {
                    return _currentDisplay;
                }
            }
        }

        // Screen properties for current display - with safety checks
        public static int ScreenWidth => (int)(CurrentDisplay?.Bounds.Width ?? SystemParameters.PrimaryScreenWidth);
        public static int ScreenHeight => (int)(CurrentDisplay?.Bounds.Height ?? SystemParameters.PrimaryScreenHeight);
        public static int ScreenLeft => (int)(CurrentDisplay?.Bounds.Left ?? 0);
        public static int ScreenTop => (int)(CurrentDisplay?.Bounds.Top ?? 0);

        // Working area (excludes taskbar)
        public static Rect WorkingArea => CurrentDisplay?.WorkingArea ?? SystemParameters.WorkArea;

        public static void Initialize()
        {
            DisplayChangedEventArgs? change;
            lock (_lockObject)
            {
                if (_initialized) return;
                EnumerateDisplays();
                SelectSavedDisplay();
                change = Snapshot();
                Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
                _initialized = true;
            }
            Publish(change);
        }

        private static int _refreshVersion;

        private static async void OnDisplaySettingsChanged(object? sender, EventArgs e)
        {
            int version = Interlocked.Increment(ref _refreshVersion);
            await Task.Delay(1000).ConfigureAwait(false);
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted) return;
            _ = dispatcher.BeginInvoke(new Action(() =>
            {
                if (Volatile.Read(ref _refreshVersion) == version) ForceRefresh();
            }));
        }

        public static void ForceRefresh()
        {
            DisplayChangedEventArgs? change;
            lock (_lockObject)
            {
                EnumerateDisplays();
                SelectSavedDisplay();
                change = Snapshot();
            }
            Publish(change);
        }

        public static void RefreshDisplays()
        {
            DisplayChangedEventArgs? change = null;
            lock (_lockObject)
            {
                var previous = _currentDisplay;
                int count = _displays.Count;
                EnumerateDisplays();
                if (count != _displays.Count || previous?.Index != _currentDisplay?.Index ||
                    previous?.DeviceName != _currentDisplay?.DeviceName ||
                    previous?.Bounds != _currentDisplay?.Bounds || previous?.WorkingArea != _currentDisplay?.WorkingArea)
                    change = Snapshot();
            }
            Publish(change);
        }

        private static void EnumerateDisplays()
        {
            _displays = MonitorHelper.GetMonitors();
            if (_currentDisplayIndex < 0 || _currentDisplayIndex >= _displays.Count)
                _currentDisplayIndex = DefaultIndex();
            _currentDisplay = _currentDisplayIndex < 0 ? null : _displays[_currentDisplayIndex];
        }

        private static int DefaultIndex()
        {
            int primary = _displays.FindIndex(display => display.IsPrimary);
            return primary >= 0 || _displays.Count == 0 ? primary : 0;
        }

        public static bool SetDisplay(int displayIndex)
        {
            DisplayChangedEventArgs? change;
            lock (_lockObject)
            {
                if (displayIndex < 0 || displayIndex >= _displays.Count) return false;
                _currentDisplayIndex = displayIndex;
                _currentDisplay = _displays[displayIndex];
                Dictionary.sliderSettings["SelectedDisplay"] = displayIndex;
                change = Snapshot();
            }
            Publish(change);
            return true;
        }

        private static DisplayChangedEventArgs? Snapshot() => _currentDisplay == null ? null : new()
        {
            DisplayIndex = _currentDisplayIndex,
            DisplayInfo = _currentDisplay,
            Bounds = _currentDisplay.Bounds,
            WorkingArea = _currentDisplay.WorkingArea
        };

        // Subscribers can dispatch to WPF and read display state on a different thread.
        private static void Publish(DisplayChangedEventArgs? change)
        {
            if (change != null) DisplayChanged?.Invoke(null, change);
        }

        /// <summary>
        /// Get display info by index
        /// </summary>
        public static DisplayInfo? GetDisplay(int index)
        {
            lock (_lockObject)
            {
                return index >= 0 && index < _displays.Count ? _displays[index] : null;
            }
        }

        /// <summary>
        /// Get all available displays (thread-safe copy)
        /// </summary>
        public static List<DisplayInfo> GetAllDisplays()
        {
            lock (_lockObject)
            {
                return new List<DisplayInfo>(_displays);
            }
        }

        /// <summary>
        /// Get the number of available displays
        /// </summary>
        public static int DisplayCount
        {
            get
            {
                lock (_lockObject)
                {
                    return _displays.Count;
                }
            }
        }

        /// <summary>
        /// Convert screen coordinates to the current display's coordinate system
        /// </summary>
        public static Point ScreenToDisplay(Point screenPoint)
        {
            return new Point(
                screenPoint.X - ScreenLeft,
                screenPoint.Y - ScreenTop
            );
        }

        /// <summary>
        /// Convert display coordinates to screen coordinates
        /// </summary>
        public static Point DisplayToScreen(Point displayPoint)
        {
            return new Point(
                displayPoint.X + ScreenLeft,
                displayPoint.Y + ScreenTop
            );
        }

        /// <summary>
        /// Check if a point is within the current display bounds
        /// </summary>
        public static bool IsPointInCurrentDisplay(Point screenPoint)
        {
            var current = CurrentDisplay;
            return current != null && current.Bounds.Contains(screenPoint);
        }

        /// <summary>
        /// Get the display index that contains the given point
        /// </summary>
        public static int GetDisplayIndexFromPoint(Point screenPoint)
        {
            lock (_lockObject)
            {
                for (int i = 0; i < _displays.Count; i++)
                {
                    if (_displays[i].Bounds.Contains(screenPoint))
                        return i;
                }
                return -1;
            }
        }

        /// <summary>
        /// Load saved display preference
        /// </summary>
        public static void LoadSavedDisplay()
        {
            DisplayChangedEventArgs? change;
            lock (_lockObject)
            {
                var previous = _currentDisplay;
                if (_displays.Count == 0) EnumerateDisplays();
                SelectSavedDisplay();
                change = previous == _currentDisplay ? null : Snapshot();
            }
            Publish(change);
        }

        private static void SelectSavedDisplay()
        {
            int index = Dictionary.sliderSettings.TryGetValue("SelectedDisplay", out var saved)
                ? (int)saved : -1;
            _currentDisplayIndex = index >= 0 && index < _displays.Count ? index : DefaultIndex();
            _currentDisplay = _currentDisplayIndex < 0 ? null : _displays[_currentDisplayIndex];
        }

        /// <summary>
        /// Force update positions of overlay windows if they exist
        /// </summary>
        public static void ForceUpdateWindows()
        {
            // Check if windows exist and force them to reposition
            try
            {
                if (Dictionary.FOVWindow != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Dictionary.FOVWindow.ForceReposition();
                    });
                }
            }
            catch (Exception ex)
            {
            }

            try
            {
                if (Dictionary.DetectedPlayerOverlay != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Dictionary.DetectedPlayerOverlay.ForceReposition();
                    });
                }
            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// Clean up resources
        /// </summary>
        public static void Dispose()
        {
            lock (_lockObject)
            {
                if (!_initialized) return;

                Interlocked.Increment(ref _refreshVersion);
                Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
                _initialized = false;
            }
        }
    }

    public class DisplayChangedEventArgs : EventArgs
    {
        public int DisplayIndex { get; set; }
        public DisplayInfo DisplayInfo { get; set; } = null!;
        public Rect Bounds { get; set; }
        public Rect WorkingArea { get; set; }
    }

    public class DisplayInfo
    {
        public int Index { get; set; }
        public bool IsPrimary { get; set; }
        public string DeviceName { get; set; } = "";
        public Rect Bounds { get; set; }
        public Rect WorkingArea { get; set; }
    }

    // Helper class to enumerate monitors using WPF methods
    internal static class MonitorHelper
    {
        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, EnumMonitorsDelegate lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfoEx lpmi);

        private delegate bool EnumMonitorsDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public Rect ToRect()
            {
                return new Rect(Left, Top, Right - Left, Bottom - Top);
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MonitorInfoEx
        {
            public int Size;
            public RECT Monitor;
            public RECT WorkArea;
            public uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
        }

        private const uint MONITORINFOF_PRIMARY = 1;

        public static List<DisplayInfo> GetMonitors()
        {
            var monitors = new List<DisplayInfo>();
            int index = 0;

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                delegate (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData)
                {
                    var mi = new MonitorInfoEx();
                    mi.Size = Marshal.SizeOf(mi);

                    if (GetMonitorInfo(hMonitor, ref mi))
                    {
                        monitors.Add(new DisplayInfo
                        {
                            Index = index++,
                            IsPrimary = (mi.Flags & MONITORINFOF_PRIMARY) != 0,
                            DeviceName = mi.DeviceName,
                            Bounds = mi.Monitor.ToRect(),
                            WorkingArea = mi.WorkArea.ToRect()
                        });
                    }
                    return true;
                },
                IntPtr.Zero);

            return monitors;
        }
    }

}
