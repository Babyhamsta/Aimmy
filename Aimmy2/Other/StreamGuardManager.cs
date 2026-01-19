using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media;

namespace Aimmy2.Other
{
    // Manages StreamGuard protection for application windows and controls
    // Prevents screen capture of protected content
    // by setting window display affinity and adjusting window styles
    // Also manages system tray icons for user feedback
    // Usage:
    // StreamGuardManager.ApplyStreamGuardToAllWindows(true); // Enable protection
    // StreamGuardManager.ApplyStreamGuardToAllWindows(false); // Disable protection
    // StreamGuardManager.ForceProtectAllContent(); // Force protection on all current content
    // StreamGuardManager.ProtectComboBoxPopups(); // Specifically protect ComboBox popups
    // StreamGuardManager.ProtectWindow(window); // Specifically protect a given window
    // StreamGuardManager.ProtectUserControl(userControl); // Specifically protect a given UserControl
    // Note: This implementation assumes a WPF application context
    // and uses P/Invoke to interact with Windows API for window management.
    // Ensure appropriate error handling and testing in your specific application context.
    // ReSharper disable once InconsistentNaming
    // ReSharper disable once IdentifierTypo
    // ETC.
    // I also added comments to explain the code better. And to make it easier to read.
    public static class StreamGuardManager
    {
        #region Constants
        // Window Display Affinity constants
        const uint WDA_NONE = 0;
        // ReSharper disable once InconsistentNaming
        const uint WDA_EXCLUDEFROMCAPTURE = 0x11;
        // Window styles
        const int GWL_EXSTYLE = -20;
        // ReSharper disable once InconsistentNaming
        const int WS_EX_TOOLWINDOW = 0x00000080;
        // ReSharper disable once InconsistentNaming
        const int WS_EX_APPWINDOW = 0x00040000;
        // Ancestor flags
        const uint GA_ROOT = 2;
        #endregion

        #region Private Fields
        // State management
        private static bool _isEnabled = false;
        // Protected windows tracking
        private static HashSet<nint> _protectedWindows = new();
        // Event attachment tracking
        private static bool _eventsAttached = false;
        // Popup monitoring timer
        private static System.Windows.Threading.DispatcherTimer _popupMonitorTimer;
        // Tray icon management fields - can be omitted if not needed (i recommend keeping it tho)
        private static bool _trayIconCreated = false;
        private static nint _mainApplicationHandle = nint.Zero;
        #endregion

        #region P/Invoke Declarations
        // Delegate for EnumWindows
        private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

        // Set window display affinity
        [DllImport("user32.dll")]
        private static extern bool SetWindowDisplayAffinity(nint hWnd, uint dwAffinity);

        // Get window long
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(nint hWnd, int nIndex);

        // Set window long
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(nint hWnd, int nIndex, int dwNewLong);

        // Get parent window
        [DllImport("user32.dll")]
        private static extern nint GetParent(nint hWnd);

        // Get ancestor window
        [DllImport("user32.dll")]
        private static extern nint GetAncestor(nint hWnd, uint gaFlags);

        // Enumerate all top-level windows
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, nint lParam);

        // Get window process ID
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

        // Get window class name
        [DllImport("user32.dll")]
        private static extern int GetClassName(nint hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        // Check if window is visible
        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(nint hWnd);

        // Destroy icon
        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(nint hIcon);

        // Load icon
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

        // Shell_NotifyIcon function
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        // ReSharper disable once InconsistentNaming
        private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);
        // Constants for NotifyIcon
        private const uint NIM_ADD = 0x00000000;
        private const uint NIM_MODIFY = 0x00000001;
        private const uint NIM_DELETE = 0x00000002;
        private const uint NIF_MESSAGE = 0x00000001;
        private const uint NIF_ICON = 0x00000002;
        private const uint NIF_TIP = 0x00000004;
        private const uint NIF_INFO = 0x00000010;
        private const uint WM_USER = 0x0400;
        private const uint WM_LBUTTONDOWN = 0x0201;
        private const uint WM_RBUTTONDOWN = 0x0204;
        // NOTIFYICONDATA structure
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        // ReSharper disable once InconsistentNaming
        private struct NOTIFYICONDATA
        {
            public uint cbSize;
            public nint hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public nint hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public uint dwState;
            public uint dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public uint uVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public uint dwInfoFlags;
            public Guid guidItem;
            public nint hBalloonIcon;
        }
        #endregion

        #region Window Protection
        // Apply protection to a specific window
        private static void ApplyToWindow(Window window, bool enable)
        {
            if (window == null) return;

            var hWnd = new WindowInteropHelper(window).Handle;
            if (hWnd == nint.Zero)
            {
                if (enable && _isEnabled)
                {
                    window.SourceInitialized += (s, e) => ApplyToWindow(window, true);
                }
                return;
            }

            if (enable)
            {
                if (_protectedWindows.Contains(hWnd)) return;
                _protectedWindows.Add(hWnd);
            }
            else
            {
                _protectedWindows.Remove(hWnd);
            }

            SetWindowDisplayAffinity(hWnd, enable ? WDA_EXCLUDEFROMCAPTURE : WDA_NONE);
            window.ShowInTaskbar = !enable;

            var extendedStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
            if (enable)
                SetWindowLong(hWnd, GWL_EXSTYLE, (extendedStyle | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW);
            else
                SetWindowLong(hWnd, GWL_EXSTYLE, (extendedStyle | WS_EX_APPWINDOW) & ~WS_EX_TOOLWINDOW);

            // Manage tray icon (optional, for user feedback) - can be omitted if not needed - I love this vs feature.
            if (enable)
            {
                AddTrayIcon(window, hWnd);
            }
            else
            {
                RemoveTrayIcon(hWnd);
            }
        }
        // Find parent window of a UserControl
        private static Window FindParentWindow(UserControl userControl)
        {
            Window parentWindow = Window.GetWindow(userControl);
            if (parentWindow != null)
            {
                return parentWindow;
            }

            DependencyObject parent = userControl;
            while (parent != null && !(parent is Window))
            {
                parent = VisualTreeHelper.GetParent(parent) ?? LogicalTreeHelper.GetParent(parent);
            }

            if (parent is Window window)
            {
                return window;
            }

            DependencyObject current = userControl;
            while (current != null)
            {
                if (current is System.Windows.Controls.Primitives.Popup popup && popup.Child != null)
                {
                    var popupRoot = popup.PlacementTarget;
                    if (popupRoot != null)
                    {
                        return Window.GetWindow(popupRoot);
                    }
                }
                current = LogicalTreeHelper.GetParent(current) ?? VisualTreeHelper.GetParent(current);
            }

            return null;
        }
        // Apply protection to a UserControl's parent window
        private static void ApplyToUserControl(UserControl userControl, bool enable)
        {
            if (userControl == null) return;

            Window parentWindow = FindParentWindow(userControl);

            if (parentWindow != null)
            {
                ApplyToWindow(parentWindow, enable);
            }
            else if (enable)
            {
                userControl.Loaded += (s, e) => {
                    Window delayedWindow = FindParentWindow(userControl);
                    if (delayedWindow != null)
                    {
                        ApplyToWindow(delayedWindow, enable);
                    }
                };
            }
        }
        #endregion

        #region Popup Window Protection
        // Protect all popup windows of the current process
        private static void ProtectAllProcessWindows()
        {
            uint currentProcessId = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;

            EnumWindows((hWnd, lParam) =>
            {
                try
                {
                    if (IsWindowVisible(hWnd))
                    {
                        GetWindowThreadProcessId(hWnd, out uint windowProcessId);

                        if (windowProcessId == currentProcessId)
                        {
                            var className = new System.Text.StringBuilder(256);
                            GetClassName(hWnd, className, className.Capacity);
                            string classNameStr = className.ToString();

                            if (classNameStr.Contains("ComboBox") ||
                                classNameStr.Contains("Popup") ||
                                classNameStr.Equals("HwndWrapper[DefaultDomain") ||
                                classNameStr.Contains("HwndWrapper") ||
                                classNameStr.Contains("DropDown") ||
                                classNameStr.Contains("MenuDropAlignment"))
                            {
                                if (_isEnabled && !_protectedWindows.Contains(hWnd))
                                {
                                    SetWindowDisplayAffinity(hWnd, WDA_EXCLUDEFROMCAPTURE);
                                    _protectedWindows.Add(hWnd);

                                    var extendedStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
                                    SetWindowLong(hWnd, GWL_EXSTYLE, (extendedStyle | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW);
                                }
                                else if (!_isEnabled && _protectedWindows.Contains(hWnd))
                                {
                                    SetWindowDisplayAffinity(hWnd, WDA_NONE);
                                    _protectedWindows.Remove(hWnd);

                                    var extendedStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
                                    SetWindowLong(hWnd, GWL_EXSTYLE, (extendedStyle | WS_EX_APPWINDOW) & ~WS_EX_TOOLWINDOW);
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Continue enumeration on error
                }
                return true;
            }, nint.Zero);
        }
        #endregion

        #region Event Monitoring
        // Attach necessary events for monitoring
        private static void AttachEvents()
        {
            if (_eventsAttached) return;

            Application.Current.Activated -= OnAppActivated;
            Application.Current.Activated += OnAppActivated;

            EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnWindowLoaded));
            EventManager.RegisterClassHandler(typeof(UserControl), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnUserControlLoaded));

            StartPopupMonitoring();
            _eventsAttached = true;
        }
        // Detach events when disabling
        private static void DetachEvents()
        {
            if (!_eventsAttached) return;

            Application.Current.Activated -= OnAppActivated;
            StopPopupMonitoring();
            _eventsAttached = false;
        }
        // Start monitoring for popup windows
        private static void StartPopupMonitoring()
        {
            if (_popupMonitorTimer != null) return;

            _popupMonitorTimer = new System.Windows.Threading.DispatcherTimer();
            _popupMonitorTimer.Interval = TimeSpan.FromMilliseconds(100);
            _popupMonitorTimer.Tick += (s, e) => ProtectAllProcessWindows();
            _popupMonitorTimer.Start();
        }
        // Stop monitoring for popup windows
        private static void StopPopupMonitoring()
        {
            if (_popupMonitorTimer != null)
            {
                _popupMonitorTimer.Stop();
                _popupMonitorTimer = null;
            }
        }
        // Application activated event handler
        private static void OnAppActivated(object? sender, EventArgs e)
        {
            if (!_isEnabled) return;
            CheckAndProtectNewWindows();
        }
        // Window loaded event handler
        private static void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            if (!_isEnabled) return;
            if (sender is Window window)
            {
                ApplyToWindow(window, true);
            }
        }
        // UserControl loaded event handler
        private static void OnUserControlLoaded(object sender, RoutedEventArgs e)
        {
            if (!_isEnabled) return;
            if (sender is UserControl userControl)
            {
                ApplyToUserControl(userControl, true);
            }
        }
        #endregion

        #region Helper Methods
        // Check all UserControls in all windows
        private static void CheckAllUserControls()
        {
            foreach (Window window in Application.Current.Windows)
            {
                CheckUserControlsInWindow(window);
            }
        }
        // Recursively check UserControls in a window
        private static void CheckUserControlsInWindow(DependencyObject parent)
        {
            if (parent == null) return;

            if (parent is UserControl userControl)
            {
                ApplyToUserControl(userControl, true);
            }

            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                CheckUserControlsInWindow(child);
            }
        }
        // Check for new windows and protect them
        private static void CheckAndProtectNewWindows()
        {
            foreach (Window window in Application.Current.Windows)
            {
                var hWnd = new WindowInteropHelper(window).Handle;
                if (hWnd != nint.Zero && !_protectedWindows.Contains(hWnd))
                {
                    ApplyToWindow(window, true);
                }
            }

            CheckAllUserControls();
            ProtectAllProcessWindows();
        }
        #endregion

        #region Public API
        // Enable or disable StreamGuard for all windows
        public static void ApplyStreamGuardToAllWindows(bool enable)
        {
            _isEnabled = enable;

            foreach (Window window in Application.Current.Windows)
                ApplyToWindow(window, enable);

            if (enable)
            {
                CheckAllUserControls();
                ProtectAllProcessWindows();
                AttachEvents();
            }
            else
            {
                // Unprotect all popup windows when disabling
                ProtectAllProcessWindows();
                DetachEvents();
                foreach (var hWnd in _protectedWindows.ToArray())
                {
                    RemoveTrayIcon(hWnd);
                }
                _protectedWindows.Clear();
            }
        }
        // Force protection on all current content
        public static void ForceProtectAllContent()
        {
            if (!_isEnabled) return;

            foreach (Window window in Application.Current.Windows)
            {
                ApplyToWindow(window, true);
                CheckUserControlsInWindow(window);
            }

            ProtectAllProcessWindows();
        }
        // Specifically protect ComboBox popups
        public static void ProtectComboBoxPopups()
        {
            if (!_isEnabled) return;
            ProtectAllProcessWindows();
        }
        // Specifically protect a given window
        public static void ProtectWindow(Window window)
        {
            if (_isEnabled)
            {
                ApplyToWindow(window, true);
            }
        }
        // Specifically protect a given UserControl
        public static void ProtectUserControl(UserControl userControl)
        {
            if (_isEnabled)
            {
                ApplyToUserControl(userControl, true);
            }
        }
        #endregion

        #region System Tray Management
        // Add tray icon for a window
        private static void AddTrayIcon(Window window, nint hWnd)
        {
            try
            {
                if (_trayIconCreated && _mainApplicationHandle != nint.Zero)
                {
                    // this is the main menu, so don't create a new icon to avoid duplicates - just return lmfao, simple right?
                    if (hWnd != _mainApplicationHandle)
                        return;
                }
                else
                {
                    _mainApplicationHandle = hWnd;
                }
                NOTIFYICONDATA iconData = new NOTIFYICONDATA();
                iconData.cbSize = (uint)Marshal.SizeOf(iconData);
                iconData.hWnd = _mainApplicationHandle;
                iconData.uID = 1; // Fixed ID for single icon - must match RemoveTrayIcon pls
                iconData.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP;
                iconData.uCallbackMessage = WM_USER + 1;
                // Use a standard Windows icon (change the number to use different icons:
                // 32512 = Application, 
                // 32513 = Hand, 
                // 32514 = Question, 
                // 32515 = Exclamation, 
                // 32516 = Asterisk .. and so on)
                iconData.hIcon = LoadIcon(IntPtr.Zero, (IntPtr)32512);
                iconData.szTip = "CouldBeAimmyV2";
                Shell_NotifyIcon(_trayIconCreated ? NIM_MODIFY : NIM_ADD, ref iconData);
                _trayIconCreated = true;
                if (!_eventsAttached)
                {
                    HwndSource source = HwndSource.FromHwnd(_mainApplicationHandle);
                    if (source != null)
                    {
                        source.AddHook(WndProc);
                        _eventsAttached = true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to add tray icon: {ex.Message}");
            }
        }

        // Remove tray icon for a window
        private static void RemoveTrayIcon(nint hWnd)
        {
            try
            {
                if (_trayIconCreated && _mainApplicationHandle == hWnd)
                {
                    NOTIFYICONDATA iconData = new NOTIFYICONDATA();
                    iconData.cbSize = (uint)Marshal.SizeOf(iconData);
                    iconData.hWnd = _mainApplicationHandle;
                    iconData.uID = 1; // fixed ID for single icon - must match AddTrayIcon pls
                    iconData.uFlags = 0;

                    Shell_NotifyIcon(NIM_DELETE, ref iconData);

                    _trayIconCreated = false;
                    _mainApplicationHandle = nint.Zero;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to remove tray icon: {ex.Message}");
            }
        }
        // Window procedure to handle tray icon messages
        private static nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
        {
            if (hwnd != _mainApplicationHandle)
                return nint.Zero;

            if (msg == (WM_USER + 1))
            {
                switch ((uint)lParam)
                {
                    case WM_LBUTTONDOWN:
                        RestoreAllWindowsFromTray();
                        handled = true;
                        break;
                    case WM_RBUTTONDOWN:
                        ShowTrayContextMenu(hwnd);
                        handled = true;
                        break;
                }
            }
            return nint.Zero;
        }
        // Restore all application windows from tray - excluding specific ones
        private static void RestoreAllWindowsFromTray()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (Window window in Application.Current.Windows)
                {
                    if (window.GetType().Name == "FOV" ||
                        window.GetType().Name.Contains("DetectedPlayerWindow") ||
                        window.Title.Contains("FOV") ||
                        window.Title.Contains("DetectedPlayerWindow") ||
                        // Exclude other specific windows as needed - Incase i missed any (like overlays)
                        window.Title.Contains("Overlay"))
                    {
                        continue;
                    }

                    if (window.WindowState == WindowState.Minimized)
                    {
                        window.WindowState = WindowState.Normal;
                    }
                    window.Show();
                    window.Activate();
                    window.Topmost = true;
                    window.Topmost = false;
                }
            });
        }
        // Show context menu for tray icon
        private static void ShowTrayContextMenu(nint hWnd)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var menu = new ContextMenu
                {
                    Background = System.Windows.Media.Brushes.Black,
                    BorderBrush = System.Windows.Media.Brushes.Gray,
                    BorderThickness = new System.Windows.Thickness(1),
                    Padding = new System.Windows.Thickness(0),
                    MinWidth = 100,
                    StaysOpen = false
                };

                var titleItem = new System.Windows.Controls.MenuItem
                {
                    Header = "StreamGuard",
                    Foreground = System.Windows.Media.Brushes.LightGray,
                    Background = System.Windows.Media.Brushes.Transparent,
                    FontSize = 10,
                    FontWeight = System.Windows.FontWeights.SemiBold,
                    Height = 24,
                    Padding = new System.Windows.Thickness(8, 4, 8, 4),
                    BorderThickness = new System.Windows.Thickness(0),
                    IsEnabled = false
                };
                menu.Items.Add(titleItem);

                var titleSeparator = new System.Windows.Controls.Separator
                {
                    Height = 1,
                    Background = System.Windows.Media.Brushes.Gray,
                    Margin = new System.Windows.Thickness(0)
                };
                menu.Items.Add(titleSeparator);

                var reopenItem = new System.Windows.Controls.MenuItem
                {
                    Header = "↻ Reopen",
                    Foreground = System.Windows.Media.Brushes.White,
                    Background = System.Windows.Media.Brushes.Transparent,
                    FontSize = 11,
                    Height = 28,
                    Padding = new System.Windows.Thickness(8, 0, 8, 0),
                    BorderThickness = new System.Windows.Thickness(0)
                };

                var exitItem = new System.Windows.Controls.MenuItem
                {
                    Header = "✕ Exit",
                    Foreground = System.Windows.Media.Brushes.White,
                    Background = System.Windows.Media.Brushes.Transparent,
                    FontSize = 11,
                    Height = 28,
                    Padding = new System.Windows.Thickness(8, 0, 8, 0),
                    BorderThickness = new System.Windows.Thickness(0)
                };

                reopenItem.MouseEnter += (s, e) =>
                {
                    reopenItem.Background = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(64, 64, 64));
                };
                reopenItem.MouseLeave += (s, e) =>
                {
                    reopenItem.Background = System.Windows.Media.Brushes.Transparent;
                };

                exitItem.MouseEnter += (s, e) =>
                {
                    exitItem.Background = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(64, 64, 64));
                };
                exitItem.MouseLeave += (s, e) =>
                {
                    exitItem.Background = System.Windows.Media.Brushes.Transparent;
                };

                reopenItem.Click += (s, e) =>
                {
                    menu.IsOpen = false;
                    RestoreAllWindowsFromTray();
                };

                exitItem.Click += (s, e) =>
                {
                    menu.IsOpen = false;
                    Application.Current.Dispatcher.BeginInvoke((Action)(() =>
                    {
                        Application.Current.Shutdown();
                    }), System.Windows.Threading.DispatcherPriority.Background);
                };

                menu.Items.Add(reopenItem);

                var separator = new System.Windows.Controls.Separator
                {
                    Height = 1,
                    Background = System.Windows.Media.Brushes.Gray,
                    Margin = new System.Windows.Thickness(0, 1, 0, 1)
                };
                menu.Items.Add(separator);

                menu.Items.Add(exitItem);

                bool forceClose = false;

                menu.PreviewMouseDown += (s, e) =>
                {
                    if (!menu.IsMouseOver)
                    {
                        forceClose = true;
                        menu.IsOpen = false;
                    }
                };

                menu.MouseDown += (s, e) =>
                {
                    if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
                    {
                        var pos = e.GetPosition(menu);
                        var hit = System.Windows.Media.VisualTreeHelper.HitTest(menu, pos);

                        if (hit == null || hit.VisualHit == menu)
                        {
                            forceClose = true;
                            menu.IsOpen = false;
                        }
                    }
                };

                menu.Closed += (s, e) =>
                {
                    menu.IsOpen = false;
                };

                System.Windows.Threading.DispatcherTimer clickTimer = null;

                menu.Opened += (s, e) =>
                {
                    clickTimer = new System.Windows.Threading.DispatcherTimer();
                    clickTimer.Interval = TimeSpan.FromMilliseconds(10);
                    clickTimer.Tick += (timerSender, timerE) =>
                    {
                        if (System.Windows.Input.Mouse.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
                        {
                            var mousePos = System.Windows.Input.Mouse.GetPosition(menu);
                            var hitResult = System.Windows.Media.VisualTreeHelper.HitTest(menu, mousePos);

                            if (hitResult == null)
                            {
                                forceClose = true;
                                menu.IsOpen = false;
                                clickTimer.Stop();
                            }
                        }
                    };
                    clickTimer.Start();
                };

                menu.Closed += (s, e) =>
                {
                    if (clickTimer != null)
                    {
                        clickTimer.Stop();
                        clickTimer = null;
                    }
                };
                Window mainWindow = null;
                foreach (Window window in Application.Current.Windows)
                {
                    var windowHandle = new WindowInteropHelper(window).Handle;
                    if (windowHandle == _mainApplicationHandle)
                    {
                        mainWindow = window;
                        break;
                    }
                }

                if (mainWindow != null)
                {
                    menu.PlacementTarget = mainWindow;
                    menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
                    menu.IsOpen = true;
                }
            });
        }
        #endregion
    }
}