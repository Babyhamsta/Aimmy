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

        static DisplayManager()
        {
            // Don't auto-initialize here, let the application control initialization
        }

        /// <summary>
        /// Initialize the DisplayManager - should be called early in application startup
        /// </summary>
        public static void Initialize()
        {
            lock (_lockObject)
            {
                if (_initialized) return;


                // Refresh displays first
                RefreshDisplays();

                // Load saved display preference
                LoadSavedDisplay();

                // Set up monitor change detection
                SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

                _initialized = true;

            }
        }

        /// <summary>
        /// Handle system display settings changes
        /// </summary>
        private static void OnDisplaySettingsChanged(object? sender, EventArgs e)
        {
            // Add delay to ensure system has updated display info
            Task.Delay(1000).ContinueWith(t =>
            {
                ForceRefresh();
            });
        }

        public static void ForceRefresh()
        {
            lock (_lockObject)
            {
                RefreshDisplays();
                LoadSavedDisplay();
                ForceUpdateWindows();
            }
        }

        /// <summary>
        /// Refresh the list of available displays
        /// </summary>
        public static void RefreshDisplays()
        {
            lock (_lockObject)
            {
                var oldDisplayCount = _displays.Count;
                var oldCurrentIndex = _currentDisplayIndex;

                _displays = MonitorHelper.GetMonitors();

                for (int i = 0; i < _displays.Count; i++)
                {
                    var d = _displays[i];
                }

                // Handle case where displays were removed
                if (_currentDisplayIndex >= _displays.Count)
                {
                    _currentDisplayIndex = _displays.FindIndex(d => d.IsPrimary);
                    if (_currentDisplayIndex == -1 && _displays.Count > 0)
                        _currentDisplayIndex = 0;
                }

                // Update current display reference
                _currentDisplay = _currentDisplayIndex >= 0 && _currentDisplayIndex < _displays.Count
                    ? _displays[_currentDisplayIndex]
                    : null;

                // Always notify if displays changed or current display index changed
                if (_displays.Count != oldDisplayCount || _currentDisplayIndex != oldCurrentIndex)
                {
                    NotifyDisplayChanged();
                }
            }
        }

        /// <summary>
        /// Set the active display by index
        /// </summary>
        public static bool SetDisplay(int displayIndex)
        {
            lock (_lockObject)
            {
                if (displayIndex < 0 || displayIndex >= _displays.Count)
                {
                    return false;
                }

                var oldIndex = _currentDisplayIndex;
                _currentDisplayIndex = displayIndex;
                _currentDisplay = _displays[displayIndex];


                // Save selection
                Dictionary.sliderSettings["SelectedDisplay"] = displayIndex;

                // Always notify about display change, even if same index
                // This ensures any new windows get positioned correctly
                NotifyDisplayChanged();

                return true;
            }
        }

        /// <summary>
        /// Notify subscribers of display change
        /// </summary>
        private static void NotifyDisplayChanged()
        {
            if (_currentDisplay != null)
            {
                DisplayChanged?.Invoke(null, new DisplayChangedEventArgs
                {
                    DisplayIndex = _currentDisplayIndex,
                    DisplayInfo = _currentDisplay,
                    Bounds = _currentDisplay.Bounds,
                    WorkingArea = _currentDisplay.WorkingArea
                });
            }
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
            lock (_lockObject)
            {
                // Ensure displays are detected
                if (_displays.Count == 0)
                {
                    RefreshDisplays();
                }

                if (Dictionary.sliderSettings.TryGetValue("SelectedDisplay", out var saved))
                {
                    var savedIndex = (int)saved;
                    if (savedIndex >= 0 && savedIndex < _displays.Count)
                    {
                        _currentDisplayIndex = savedIndex;
                        _currentDisplay = _displays[savedIndex];
                        return;
                    }
                }

                // Default to primary display
                var primaryIndex = _displays.FindIndex(d => d.IsPrimary);
                if (primaryIndex >= 0)
                {
                    _currentDisplayIndex = primaryIndex;
                    _currentDisplay = _displays[primaryIndex];
                }
                else if (_displays.Count > 0)
                {
                    _currentDisplayIndex = 0;
                    _currentDisplay = _displays[0];
                }
            }
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
            catch
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
            catch
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

                SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
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

    // System event handling
    internal static class SystemEvents
    {
        public static event EventHandler? DisplaySettingsChanged;

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        private const uint EVENT_SYSTEM_DISPLAYCHANGE = 0x001E;
        private const uint WINEVENT_OUTOFCONTEXT = 0;

        private static IntPtr _hook;
        private static WinEventDelegate _procDelegate = new WinEventDelegate(WinEventProc);

        static SystemEvents()
        {
            _hook = SetWinEventHook(EVENT_SYSTEM_DISPLAYCHANGE, EVENT_SYSTEM_DISPLAYCHANGE, IntPtr.Zero, _procDelegate, 0, 0, WINEVENT_OUTOFCONTEXT);
        }

        private static void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (eventType == EVENT_SYSTEM_DISPLAYCHANGE)
            {
                DisplaySettingsChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        public static void Dispose()
        {
            if (_hook != IntPtr.Zero)
            {
                UnhookWinEvent(_hook);
                _hook = IntPtr.Zero;
            }
        }
    }
}