using Aimmy2.Class;
using SharpGen.Runtime;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Threading;
using Visuality;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace AILogic
{
    internal class CaptureManager
    {
        #region Variables
        private string _currentCaptureMethod = ""; // Track current method
        private bool _directXFailedPermanently = false; // Track if DirectX failed with unsupported error
        private bool _notificationShown = false; // Prevent spam notifications

        private const int IMAGE_SIZE = 640;
        public Bitmap? screenCaptureBitmap { get; private set; }
        private ID3D11Device? _dxDevice;
        private IDXGIOutputDuplication? _deskDuplication;
        private ID3D11Texture2D? _stagingTex;

        // Frame caching for DirectX
        private Bitmap? _cachedFrame;
        private Rectangle _cachedFrameBounds;
        private DateTime _lastFrameTime = DateTime.MinValue;
        private readonly TimeSpan _frameCacheTimeout = TimeSpan.FromMilliseconds(15); // Adjust as needed

        // Display change handling
        public readonly object _displayLock = new();
        public bool _displayChangesPending { get; set; } = false;

        // Performance tracking
        private int _consecutiveFailures = 0;
        private const int MAX_CONSECUTIVE_FAILURES = 5;
        #endregion

        #region Helper Methods
        private void ShowNoticeOnUIThread(string message, int duration)
        {
            if (_notificationShown) return; // Prevent spam
            _notificationShown = true;

            // Check if we're already on the UI thread
            if (Application.Current?.Dispatcher?.CheckAccess() == true)
            {
                new NoticeBar(message, duration).Show();
            }
            else
            {
                // Dispatch to UI thread
                Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    new NoticeBar(message, duration).Show();
                }), DispatcherPriority.Normal);
            }
        }
        #endregion

        #region DirectX
        public void InitializeDxgiDuplication()
        {
            DisposeDxgiResources();
            try
            {
                var currentDisplay = DisplayManager.CurrentDisplay;

                if (currentDisplay == null)
                {
                    throw new InvalidOperationException("No current display available. DisplayManager may not be initialized.");
                }

                using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
                IDXGIOutput1? targetOutput1 = null;
                IDXGIAdapter1? targetAdapter = null;
                bool foundTarget = false;

                for (uint adapterIndex = 0;
                    factory.EnumAdapters1(adapterIndex, out var adapter).Success;
                    adapterIndex++)
                {
                    Debug.WriteLine($"\nAdapter {adapterIndex}:");

                    for (uint outputIndex = 0;
                        adapter.EnumOutputs(outputIndex, out var output).Success;
                        outputIndex++)
                    {
                        using (output)
                        {
                            var output1 = output.QueryInterface<IDXGIOutput1>();
                            var outputDesc = output1.Description;
                            var outputBounds = new Vortice.Mathematics.Rect(
                                outputDesc.DesktopCoordinates.Left,
                                outputDesc.DesktopCoordinates.Top,
                                outputDesc.DesktopCoordinates.Right - outputDesc.DesktopCoordinates.Left,
                                outputDesc.DesktopCoordinates.Bottom - outputDesc.DesktopCoordinates.Top);

                            // Try different matching strategies
                            bool nameMatch = currentDisplay?.DeviceName != null && outputDesc.DeviceName.TrimEnd('\0') == currentDisplay.DeviceName.TrimEnd('\0');
                            bool boundsMatch = currentDisplay?.Bounds != null && outputBounds.Equals(currentDisplay.Bounds);

                            // Try matching by bounds only as a fallback
                            if (boundsMatch)
                            {
                                targetOutput1 = output1;
                                targetAdapter = adapter;
                                foundTarget = true;
                                break;
                            }
                            output1.Dispose();
                        }
                    }

                    if (foundTarget)
                        break;
                    adapter.Dispose();
                }

                // Fallback to specific display index if not found
                if (targetOutput1 == null || targetAdapter == null)
                {
                    // Try to find by index
                    int targetIndex = currentDisplay?.Index ?? 0;
                    int currentIndex = 0;

                    for (uint adapterIndex = 0;
                        factory.EnumAdapters1(adapterIndex, out var adapter).Success;
                        adapterIndex++)
                    {
                        for (uint outputIndex = 0;
                            adapter.EnumOutputs(outputIndex, out var output).Success;
                            outputIndex++)
                        {
                            if (currentIndex == targetIndex)
                            {
                                Debug.WriteLine($"Found display at index {targetIndex}");
                                targetOutput1 = output.QueryInterface<IDXGIOutput1>();
                                targetAdapter = adapter;
                                foundTarget = true;
                                break;
                            }
                            currentIndex++;
                            output.Dispose();
                        }

                        if (foundTarget)
                            break;
                        adapter.Dispose();
                    }
                }

                if (targetAdapter == null || targetOutput1 == null)
                {
                    throw new Exception("No suitable display output found");
                }

                // Create D3D11 device
                var result = D3D11.D3D11CreateDevice(
                    targetAdapter,
                    DriverType.Unknown,
                    DeviceCreationFlags.None,
                    null,
                    out _dxDevice);

                if (result.Failure || _dxDevice == null)
                {
                    throw new Exception($"Failed to create D3D11 device: {result}");
                }

                // Create desktop duplication
                _deskDuplication = targetOutput1.DuplicateOutput(_dxDevice);

                // Reset failure counter on successful init
                _consecutiveFailures = 0;

                // Cleanup
                targetAdapter.Dispose();
                targetOutput1.Dispose();
            }
            catch (SharpGenException ex) when (ex.ResultCode == Vortice.DXGI.ResultCode.Unsupported ||
                                                ex.HResult == unchecked((int)0x887A0004))
            {
                // DirectX Desktop Duplication not supported
                Debug.WriteLine($"DirectX Desktop Duplication not supported: {ex.Message}");
                _directXFailedPermanently = true;
                DisposeDxgiResources();

                // Force switch to GDI+
                Dictionary.dropdownState["Screen Capture Method"] = "GDI+";
                _currentCaptureMethod = "GDI+";

                ShowNoticeOnUIThread("DirectX Desktop Duplication not supported on this system. Switched to GDI+ capture.", 6000);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"InitializeDxgiDuplication failed: {ex.Message}");
                DisposeDxgiResources();
                throw;
            }
        }

        private Bitmap? DirectX(Rectangle detectionBox)
        {
            int w = detectionBox.Width;
            int h = detectionBox.Height;

            try
            {
                // Check if we need to reinitialize
                if (_dxDevice == null || _dxDevice.ImmediateContext == null || _deskDuplication == null)
                {
                    InitializeDxgiDuplication();
                    if (_dxDevice == null || _dxDevice.ImmediateContext == null || _deskDuplication == null)
                    {
                        lock (_displayLock) { _displayChangesPending = true; }
                        return GetCachedFrame(detectionBox);
                    }
                }

                // Check if we need new staging texture - always match requested size
                bool requiresNewResources = _stagingTex == null ||
                    _stagingTex.Description.Width != detectionBox.Width ||
                    _stagingTex.Description.Height != detectionBox.Height;

                if (requiresNewResources)
                {
                    _stagingTex?.Dispose();

                    var desc = new Texture2DDescription
                    {
                        Width = (uint)w,
                        Height = (uint)h,
                        MipLevels = 1,
                        ArraySize = 1,
                        Format = Format.B8G8R8A8_UNorm,
                        SampleDescription = new SampleDescription(1, 0),
                        Usage = ResourceUsage.Staging,
                        CPUAccessFlags = CpuAccessFlags.Read,
                        BindFlags = BindFlags.None
                    };

                    _stagingTex = _dxDevice.CreateTexture2D(desc);
                }

                bool frameAcquired = false;
                IDXGIResource? desktopResource = null;

                try
                {
                    // Try to acquire next frame with a reasonable timeout
                    var result = _deskDuplication!.AcquireNextFrame(0, out var frameInfo, out desktopResource);

                    if (result == Vortice.DXGI.ResultCode.WaitTimeout)
                    {
                        // No new frame available - this is normal
                        _consecutiveFailures = 0; // Reset failure counter
                        return GetCachedFrame(detectionBox);
                    }
                    else if (result == Vortice.DXGI.ResultCode.DeviceRemoved || result == Vortice.DXGI.ResultCode.AccessLost)
                    {
                        // Device lost - need to reinitialize
                        _consecutiveFailures++;
                        if (_consecutiveFailures >= MAX_CONSECUTIVE_FAILURES)
                        {
                            lock (_displayLock) { _displayChangesPending = true; }
                        }
                        return GetCachedFrame(detectionBox);
                    }
                    else if (result != Result.Ok)
                    {
                        // Other error
                        _consecutiveFailures++;
                        return GetCachedFrame(detectionBox);
                    }

                    frameAcquired = true;
                    _consecutiveFailures = 0; // Reset on successful acquisition

                    using var screenTexture = desktopResource.QueryInterface<ID3D11Texture2D>();

                    var displayBounds = new Rectangle(DisplayManager.ScreenLeft,
                                                      DisplayManager.ScreenTop,
                                                      DisplayManager.ScreenWidth,
                                                      DisplayManager.ScreenHeight);

                    // IMPORTANT: Convert absolute screen coordinates to display-relative coordinates
                    // The duplicated output starts at (0,0), not at its screen position
                    int relativeDetectionLeft = detectionBox.Left - DisplayManager.ScreenLeft;
                    int relativeDetectionTop = detectionBox.Top - DisplayManager.ScreenTop;
                    int relativeDetectionRight = relativeDetectionLeft + detectionBox.Width;
                    int relativeDetectionBottom = relativeDetectionTop + detectionBox.Height;

                    // Calculate the visible portion in display-relative coordinates
                    int srcLeft = Math.Max(relativeDetectionLeft, 0);
                    int srcTop = Math.Max(relativeDetectionTop, 0);
                    int srcRight = Math.Min(relativeDetectionRight, DisplayManager.ScreenWidth);
                    int srcBottom = Math.Min(relativeDetectionBottom, DisplayManager.ScreenHeight);

                    // Calculate where to place this in the destination bitmap
                    int dstX = srcLeft - relativeDetectionLeft;
                    int dstY = srcTop - relativeDetectionTop;

                    // Only copy if there's a visible region
                    if (srcRight > srcLeft && srcBottom > srcTop)
                    {
                        var box = new Box
                        {
                            Left = srcLeft,
                            Top = srcTop,
                            Front = 0,
                            Right = srcRight,
                            Bottom = srcBottom,
                            Back = 1
                        };

                        // Copy to the correct position in the staging texture
                        // Cast to uint as required by the API
                        _dxDevice.ImmediateContext!.CopySubresourceRegion(_stagingTex, 0, (uint)dstX, (uint)dstY, 0, screenTexture, 0, box);
                    }

                    var map = _dxDevice.ImmediateContext.Map(_stagingTex, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
                    var bitmap = new Bitmap(w, h, PixelFormat.Format32bppArgb);

                    // Clear bitmap to black to match GDI behavior for out-of-bounds areas
                    // Use fully qualified name to avoid ambiguity
                    using (var g = Graphics.FromImage(bitmap))
                    {
                        g.Clear(System.Drawing.Color.Black);
                    }

                    var boundsRect = new Rectangle(0, 0, detectionBox.Width, detectionBox.Height);
                    BitmapData? mapDest = null;

                    try
                    {
                        mapDest = bitmap.LockBits(boundsRect, ImageLockMode.WriteOnly, bitmap.PixelFormat);

                        unsafe
                        {
                            // Use the minimum of the two strides to avoid buffer overrun
                            int srcStride = (int)map.RowPitch;
                            int dstStride = mapDest.Stride;
                            int copyStride = Math.Min(srcStride, dstStride);

                            byte* src = (byte*)map.DataPointer;
                            byte* dst = (byte*)mapDest.Scan0;

                            for (int y = 0; y < h; y++)
                            {
                                Buffer.MemoryCopy(src, dst, copyStride, copyStride);
                                src += srcStride;
                                dst += dstStride;
                            }
                        }

                        // Update cache
                        UpdateCache(bitmap, detectionBox);
                        return bitmap;
                    }
                    finally
                    {
                        if (mapDest != null)
                            bitmap.UnlockBits(mapDest);

                        _dxDevice.ImmediateContext.Unmap(_stagingTex, 0);
                    }
                }
                finally
                {
                    if (frameAcquired && _deskDuplication != null)
                    {
                        try
                        {
                            _deskDuplication.ReleaseFrame();
                        }
                        catch { }
                    }
                    desktopResource?.Dispose();
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"DirectX capture error: {e.Message}");
                _consecutiveFailures++;

                if (_consecutiveFailures >= MAX_CONSECUTIVE_FAILURES)
                {
                    lock (_displayLock) { _displayChangesPending = true; }
                }

                return GetCachedFrame(detectionBox);
            }
        }

        private void UpdateCache(Bitmap frame, Rectangle bounds)
        {
            // Dispose old cached frame if bounds changed
            if (_cachedFrame != null && !_cachedFrameBounds.Equals(bounds))
            {
                _cachedFrame.Dispose();
                _cachedFrame = null;
            }

            // Clone the frame for cache
            _cachedFrame?.Dispose();
            _cachedFrame = (Bitmap)frame.Clone();
            _cachedFrameBounds = bounds;
            _lastFrameTime = DateTime.Now;
        }

        private Bitmap? GetCachedFrame(Rectangle detectionBox)
        {
            // Check if we have a valid cached frame
            if (_cachedFrame == null || !_cachedFrameBounds.Equals(detectionBox))
                return null;

            // Check if cache is too old
            if (DateTime.Now - _lastFrameTime > _frameCacheTimeout)
                return null;

            // Return a clone of the cached frame
            return (Bitmap)_cachedFrame.Clone();
        }

        public void DisposeDxgiResources()
        {
            try
            {
                // Try to release any pending frame
                if (_deskDuplication != null)
                {
                    try
                    {
                        _deskDuplication.ReleaseFrame();
                    }
                    catch { }
                }

                _deskDuplication?.Dispose();
                _stagingTex?.Dispose();
                _dxDevice?.Dispose();
                _cachedFrame?.Dispose();

                _deskDuplication = null;
                _stagingTex = null;
                _dxDevice = null;
                _cachedFrame = null;

                // Small delay to ensure resources are fully released
                System.Threading.Thread.Sleep(50);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error disposing DXGI resources: {ex.Message}");
            }
        }
        #endregion

        #region GDI
        public Bitmap GDIScreen(Rectangle detectionBox)
        {
            if (_dxDevice != null || _deskDuplication != null)
            {
                DisposeDxgiResources();
            }

            if (screenCaptureBitmap == null || screenCaptureBitmap.Width != detectionBox.Width || screenCaptureBitmap.Height != detectionBox.Height)
            {
                screenCaptureBitmap?.Dispose();
                screenCaptureBitmap = new Bitmap(detectionBox.Width, detectionBox.Height, PixelFormat.Format32bppArgb);
            }

            try
            {
                using (var g = Graphics.FromImage(screenCaptureBitmap))
                {
                    g.CopyFromScreen(detectionBox.Left, detectionBox.Top, 0, 0, detectionBox.Size);
                }
                return screenCaptureBitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to capture screen: {ex.Message}");
                throw;
            }
        }
        #endregion

        public Bitmap? ScreenGrab(Rectangle detectionBox)
        {
            string selectedMethod = Dictionary.dropdownState["Screen Capture Method"];

            // If DirectX failed permanently, force GDI+
            if (_directXFailedPermanently && selectedMethod == "DirectX")
            {
                Dictionary.dropdownState["Screen Capture Method"] = "GDI+";
                selectedMethod = "GDI+";
                _currentCaptureMethod = "GDI+";
            }

            // Handle method switch
            if (selectedMethod != _currentCaptureMethod)
            {
                // Dispose bitmap when switching methods
                screenCaptureBitmap?.Dispose();
                screenCaptureBitmap = null;
                _currentCaptureMethod = selectedMethod;
                _notificationShown = false; // Reset notification flag on method change

                // Dispose DX resources when switching to GDI
                if (selectedMethod == "GDI+")
                {
                    DisposeDxgiResources();
                }
                else
                {
                    InitializeDxgiDuplication();
                }
            }

            if (selectedMethod == "DirectX" && !_directXFailedPermanently)
            {
                return DirectX(detectionBox);
            }
            else
            {
                return GDIScreen(detectionBox);
            }
        }
    }
}