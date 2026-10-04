using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Management;
using Aimmy2.Class;
using Other;
using LogLevel = Other.LogManager.LogLevel;

namespace AILogic
{
    /// <summary>
    /// EZCAP RAW 380 PCIe Capture Card Integration
    /// Supports: 4K@30fps, 1440p@120fps, 1080p@120fps
    /// Formats: MJPG, NV12, I420, YUY2, RGB24
    /// Interface: PCIe Gen3 x1
    /// </summary>
    internal class EzcapCaptureCardManager : IDisposable
    {
        #region Win32 API Declarations

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateFileA(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadFile(
            IntPtr hFile,
            byte[] lpBuffer,
            uint nNumberOfBytesToRead,
            out uint lpNumberOfBytesRead,
            IntPtr lpOverlapped);

        private const uint GENERIC_READ = 0x80000000;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint OPEN_EXISTING = 3;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;

        #endregion

        #region Variables

        private IntPtr _deviceHandle = IntPtr.Zero;
        private byte[]? _frameBuffer;
        private Bitmap? _captureBuffer;
        private string? _devicePath;
        private int _captureWidth = 1920;
        private int _captureHeight = 1080;
        private bool _isInitialized = false;
        private bool _disposed = false;
        private readonly object _lockObject = new();

        #endregion

        #region Initialization

        public bool Initialize()
        {
            lock (_lockObject)
            {
                try
                {
                    LogManager.Log(LogLevel.Info, "Initializing EZCAP RAW 380 capture card...");

                    _devicePath = FindEzcapDevice();
                    if (string.IsNullOrEmpty(_devicePath))
                    {
                        LogManager.Log(LogLevel.Error, 
                            "EZCAP RAW 380 not found. Verify:\n" +
                            "1. Card is installed in PCIe slot\n" +
                            "2. Drivers installed from manufacturer\n" +
                            "3. Device appears in Device Manager", 
                            true, 8000);
                        return false;
                    }

                    LogManager.Log(LogLevel.Info, $"Found EZCAP device: {_devicePath}");

                    _deviceHandle = CreateFileA(_devicePath, GENERIC_READ, FILE_SHARE_READ,
                        IntPtr.Zero, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, IntPtr.Zero);

                    if (_deviceHandle == IntPtr.Zero || _deviceHandle.ToInt64() == -1)
                    {
                        LogManager.Log(LogLevel.Error, 
                            "Failed to open EZCAP device. Ensure drivers are installed and app has admin privileges.", 
                            true, 6000);
                        return false;
                    }

                    _frameBuffer = new byte[_captureWidth * _captureHeight * 3];
                    _captureBuffer = new Bitmap(_captureWidth, _captureHeight, PixelFormat.Format24bppRgb);

                    _isInitialized = true;
                    LogManager.Log(LogLevel.Info, 
                        $"EZCAP initialized: {_captureWidth}x{_captureHeight} @ RGB24", 
                        true, 3000);

                    return true;
                }
                catch (Exception ex)
                {
                    LogManager.Log(LogLevel.Error, $"EZCAP initialization failed: {ex.Message}", true, 6000);
                    Cleanup();
                    return false;
                }
            }
        }

        private string? FindEzcapDevice()
        {
            try
            {
                var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%ezcap%' OR Name LIKE '%EZCAP%'");

                foreach (ManagementObject device in searcher.Get())
                {
                    string? name = device["Name"]?.ToString();
                    string? deviceId = device["DeviceID"]?.ToString();
                    
                    LogManager.Log(LogLevel.Info, $"Found device: {name}");

                    if (!string.IsNullOrEmpty(deviceId) && 
                        (name?.Contains("ezcap", StringComparison.OrdinalIgnoreCase) ?? false))
                    {
                        return $"\\\\.\\{deviceId}";
                    }
                }

                string[] commonPaths = 
                {
                    "\\\\.\\ezcap0",
                    "\\\\.\\Video0",
                    "\\\\.\\capture0"
                };

                foreach (string path in commonPaths)
                {
                    IntPtr handle = CreateFileA(path, GENERIC_READ, FILE_SHARE_READ,
                        IntPtr.Zero, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, IntPtr.Zero);
                    
                    if (handle != IntPtr.Zero && handle.ToInt64() != -1)
                    {
                        CloseHandle(handle);
                        LogManager.Log(LogLevel.Info, $"Found device at fallback path: {path}");
                        return path;
                    }
                }

                LogManager.Log(LogLevel.Warning, "No EZCAP device found in device tree or common paths");
                return null;
            }
            catch (Exception ex)
            {
                LogManager.Log(LogLevel.Error, $"Error searching for EZCAP device: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Capture

        public Bitmap? CaptureFrame(Rectangle detectionBox)
        {
            if (!_isInitialized || _disposed || _frameBuffer == null || _captureBuffer == null)
                return null;

            lock (_lockObject)
            {
                try
                {
                    if (_captureBuffer.Width != detectionBox.Width || 
                        _captureBuffer.Height != detectionBox.Height)
                    {
                        _captureBuffer.Dispose();
                        _captureBuffer = new Bitmap(detectionBox.Width, detectionBox.Height, 
                            PixelFormat.Format24bppRgb);
                    }

                    if (!ReadFrame())
                    {
                        return null;
                    }

                    BitmapData bmpData = _captureBuffer.LockBits(
                        new Rectangle(0, 0, _captureBuffer.Width, _captureBuffer.Height),
                        ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

                    try
                    {
                        Marshal.Copy(_frameBuffer, 0, bmpData.Scan0, 
                            Math.Min(_frameBuffer.Length, bmpData.Stride * _captureBuffer.Height));
                    }
                    finally
                    {
                        _captureBuffer.UnlockBits(bmpData);
                    }

                    return (Bitmap)_captureBuffer.Clone();
                }
                catch (Exception ex)
                {
                    LogManager.Log(LogLevel.Error, $"Frame capture error: {ex.Message}");
                    return null;
                }
            }
        }

        private bool ReadFrame()
        {
            if (_deviceHandle == IntPtr.Zero || _deviceHandle.ToInt64() == -1 || _frameBuffer == null)
                return false;

            try
            {
                uint bytesRead = 0;
                bool success = ReadFile(_deviceHandle, _frameBuffer, 
                    (uint)_frameBuffer.Length, out bytesRead, IntPtr.Zero);

                if (!success)
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    LogManager.Log(LogLevel.Warning, 
                        $"ReadFile failed with error code: {errorCode}");
                    return false;
                }

                if (bytesRead == 0)
                {
                    LogManager.Log(LogLevel.Warning, "No data read from capture device");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                LogManager.Log(LogLevel.Error, $"Error reading frame: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Cleanup

        private void Cleanup()
        {
            try
            {
                if (_deviceHandle != IntPtr.Zero && _deviceHandle.ToInt64() != -1)
                {
                    CloseHandle(_deviceHandle);
                    _deviceHandle = IntPtr.Zero;
                }

                _captureBuffer?.Dispose();
                _isInitialized = false;
            }
            catch (Exception ex)
            {
                LogManager.Log(LogLevel.Error, $"Cleanup error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            lock (_lockObject)
            {
                Cleanup();
                _disposed = true;
            }
        }

        #endregion
    }
}
