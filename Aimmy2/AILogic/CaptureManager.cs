using Aimmy2.Class;
using Aimmy2.Resources;
using Other;
using SharpGen.Runtime;
using System.Drawing;
using System.Drawing.Imaging;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using LogLevel = Other.LogManager.LogLevel;

namespace AILogic
{
    internal class CaptureManager
    {
        #region Variables
        private string _currentCaptureMethod = ""; // Track current method
#pragma warning disable CS0414
        private bool _directXFailedPermanently = false; // Track if DirectX failed with unsupported error
        private bool _notificationShown = false; // Prevent spam notifications
#pragma warning restore CS0414

        // Capturing
        public Bitmap? screenCaptureBitmap { get; private set; }
        public Bitmap? directXBitmap { get; private set; }
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

        // stride matching
#pragma warning disable CS0414
        private bool _lastStrideMatch = true;
        private int _lastSrcStride = 0;
        private int _lastDstStride = 0;
#pragma warning restore CS0414

        #endregion
        #region Handlers
        public CaptureManager()
        {
            // Subscribe to display changes FIRST
            DisplayManager.DisplayChanged += OnDisplayChanged;
        }

        private void OnDisplayChanged(object? sender, DisplayChangedEventArgs e)
        {
            lock (_displayLock)
            {
                _displayChangesPending = true;
                _consecutiveFailures = 0;
                DisposeDxgiResources();
            }
            LogManager.Log(LogLevel.Info, LocalizationManager.GetString("Msg_Capture_DisplayChanged"));
        }

        public void HandlePendingDisplayChanges()
        {
            lock (_displayLock)
            {
                if (!_displayChangesPending) return;

                try
                {
                    InitializeDxgiDuplication();
                    _displayChangesPending = false;
                }
                catch
                {
                }
            }
        }

        #endregion
        #region DirectX
        public void InitializeDxgiDuplication()
        {
            DisposeDxgiResources();
            var currentDisplay = DisplayManager.CurrentDisplay;
            try
            {
                if (currentDisplay == null)
                {
                    LogManager.Log(LogLevel.Error, LocalizationManager.GetString("Msg_Capture_NoDisplays"));
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
                    LogManager.Log(LogLevel.Info, LocalizationManager.GetString("Msg_Capture_AdapterDetected", adapter.Description.Description.TrimEnd('\0')));

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
                            LogManager.Log(LogLevel.Info, LocalizationManager.GetString("Msg_Capture_OutputDetected", outputDesc.DeviceName.TrimEnd('\0'), outputBounds.Width, outputBounds.Height));

                            // Try different matching strategies
                            bool nameMatch = currentDisplay?.DeviceName != null && outputDesc.DeviceName.TrimEnd('\0') == currentDisplay.DeviceName.TrimEnd('\0');
                            bool boundsMatch = currentDisplay?.Bounds != null && outputBounds.Equals(currentDisplay.Bounds);

                            if (nameMatch || boundsMatch)
                            {
                                targetOutput1 = output1;
                                targetAdapter = adapter;
                                foundTarget = true;
                                break;
                            }
                            output1.Dispose();
                        }
                    }

                    if (foundTarget) break;
                }

                // Fallback to specific display index if not found
                if (!foundTarget)
                {
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
                                LogManager.Log(LogLevel.Warning, LocalizationManager.GetString("Msg_Capture_NoMatchingDisplay"));
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
                    LogManager.Log(LogLevel.Error, LocalizationManager.GetString("Msg_Capture_NoSuitableOutput"), true, 6000);
                    throw new Exception("No suitable display output found");
                }

                FeatureLevel[] featureLevels = {
                    FeatureLevel.Level_12_2, // 50 series support
                    FeatureLevel.Level_12_1,
                    FeatureLevel.Level_12_0,
                    FeatureLevel.Level_11_1,
                    FeatureLevel.Level_11_0,
                    FeatureLevel.Level_10_1,
                    FeatureLevel.Level_10_0,
                    FeatureLevel.Level_9_3,
                    FeatureLevel.Level_9_2,
                    FeatureLevel.Level_9_1
                };

                // Create D3D11 device
                var result = D3D11.D3D11CreateDevice(
                    targetAdapter,
                    DriverType.Unknown,
                    DeviceCreationFlags.None,
                    featureLevels,
                    out _dxDevice);

                if (result.Failure || _dxDevice == null)
                {
                    result = D3D11.D3D11CreateDevice(
                      targetAdapter,
                      DriverType.Unknown,
                      DeviceCreationFlags.None,
                      null,
                      out _dxDevice);

                    if (result.Failure || _dxDevice == null)
                    {
                        LogManager.Log(LogLevel.Error, LocalizationManager.GetString("Msg_Capture_DXDeviceFailed", currentDisplay?.ToString() ?? "Unknown"), true, 6000);
                        throw new Exception($"Failed to create D3D11 device: {result}");
                    }
                }

                // Create desktop duplication
                _deskDuplication = targetOutput1.DuplicateOutput(_dxDevice);
                _consecutiveFailures = 0; //reset on success

                LogManager.Log(LogLevel.Info, LocalizationManager.GetString("Msg_Capture_DuplicationInit", currentDisplay?.ToString() ?? "Unknown"));
            }
            catch (SharpGenException ex) when (ex.ResultCode == Vortice.DXGI.ResultCode.Unsupported || ex.HResult == unchecked((int)0x887A0004))
            {
                LogManager.Log(LogLevel.Error, LocalizationManager.GetString("Msg_Capture_DuplicationUnsupported", currentDisplay?.ToString() ?? "Unknown"), true, 6000);
                _directXFailedPermanently = true;
                DisposeDxgiResources();

                Dictionary.dropdownState["Screen Capture Method"] = "GDI+";
                _currentCaptureMethod = "GDI+";

                LogManager.Log(LogLevel.Error, LocalizationManager.GetString("Msg_Capture_DuplicationUnsupported", currentDisplay?.ToString() ?? "Unknown"), true, 6000);
            }
            catch (Exception)
            {
                LogManager.Log(LogLevel.Error, LocalizationManager.GetString("Msg_Capture_DuplicationInitFailed", currentDisplay?.ToString() ?? "Unknown"), true, 6000);
                DisposeDxgiResources();
                throw;
            }
        }
        private Bitmap? DirectX(Rectangle detectionBox)
        {
            int w = detectionBox.Width;
            int h = detectionBox.Height;
            bool frameAcquired = false;
            IDXGIResource? desktopResource = null;


            Bitmap? resultBitmap = null;

            try
            {

                lock (_displayLock)
                {
                    if (_displayChangesPending)
                    {
                        InitializeDxgiDuplication();
                        _displayChangesPending = false;
                    }
                }

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

                if (directXBitmap == null || directXBitmap.Width != w || directXBitmap.Height != h)
                {
                    directXBitmap?.Dispose();
                    directXBitmap = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                }

                // Check if we need new staging texture - always match requested size
                if (_stagingTex == null ||
                    _stagingTex.Description.Width != w ||
                    _stagingTex.Description.Height != h)
                {
                    _stagingTex?.Dispose();
                    _stagingTex = _dxDevice.CreateTexture2D(new Texture2DDescription
                    {
                        Width = (uint)w,
                        Height = (uint)h,
                        MipLevels = 1,
                        ArraySize = 1,
                        Format = Format.B8G8R8A8_UNorm,
                        SampleDescription = new(1, 0),
                        Usage = ResourceUsage.Staging,
                        CPUAccessFlags = CpuAccessFlags.Read,
                        BindFlags = BindFlags.None
                    });
                }

                int timeout = _consecutiveFailures > 0 ? 5 : 1;
                var result = _deskDuplication!.AcquireNextFrame((uint)timeout, out var frameInfo, out desktopResource);

                if (result == Vortice.DXGI.ResultCode.WaitTimeout)
                {
                    // No new frame available - this is normal
                    _consecutiveFailures = 0; // Reset failure counter
                    return GetCachedFrame(detectionBox);
                }
                else if (result == Vortice.DXGI.ResultCode.DeviceRemoved || result == Vortice.DXGI.ResultCode.AccessLost)
                { // Device lost - need to reinitialize
                    _consecutiveFailures++;

                    if (_consecutiveFailures >= MAX_CONSECUTIVE_FAILURES)
                        lock (_displayLock) { _displayChangesPending = true; }

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

                using (var screenTexture = desktopResource.QueryInterface<ID3D11Texture2D>())
                {
                    #region Display Bounds
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

                    // Only copy if there's a visible region
                    if (srcRight > srcLeft && srcBottom > srcTop)
                    {
                        var box = new Box(srcLeft, srcTop, 0, srcRight, srcBottom, 1);

                        _dxDevice.ImmediateContext.CopySubresourceRegion(
                               _stagingTex, 0,
                               (uint)(srcLeft - relativeDetectionLeft),
                               (uint)(srcTop - relativeDetectionTop),
                               0,
                               screenTexture, 0, box);
                    }
                    else
                    {
                        LogManager.Log(LogLevel.Warning, LocalizationManager.GetString("Msg_Capture_NoVisibleArea", DisplayManager.CurrentDisplay?.ToString() ?? "Unknown"), true, 3000);
                        return GetCachedFrame(detectionBox);
                    }

                    #endregion

                    #region Bitmap
                    var map = _dxDevice.ImmediateContext.Map(_stagingTex, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
                    var boundsRect = new Rectangle(0, 0, w, h);
                    BitmapData? mapDest = directXBitmap.LockBits(boundsRect, ImageLockMode.WriteOnly, directXBitmap.PixelFormat);

                    try
                    {
                        unsafe
                        {
                            byte* src = (byte*)map.DataPointer;
                            byte* dst = (byte*)mapDest.Scan0;
                            int srcStride = (int)map.RowPitch;
                            int dstStride = mapDest.Stride;

                            int copyBytesPerRow = Math.Min(srcStride, dstStride);
                            for (int y = 0; y < h; y++)
                            {
                                Buffer.MemoryCopy(src, dst, dstStride, copyBytesPerRow);
                                src += srcStride;
                                dst += dstStride;
                            }

                            if (Dictionary.toggleState["Third Person Support"]) // a mask basically
                            {
                                int width = w / 2;
                                int height = h / 2;
                                int startY = h - height;

                                byte* basePtr = (byte*)mapDest.Scan0;
                                for (int y = startY; y < h; y++)
                                {
                                    byte* rowPtr = basePtr + (y * dstStride);
                                    for (int x = 0; x < width; x++)
                                    {
                                        int pixelOffset = x * 4;
                                        // Pixel layout: [B, G, R, A]
                                        rowPtr[pixelOffset + 0] = 0;   // Blue -> 0
                                        rowPtr[pixelOffset + 1] = 0;   // Green -> 0
                                        rowPtr[pixelOffset + 2] = 0;   // Red -> 0
                                        rowPtr[pixelOffset + 3] = 255; // Alpha -> 255 (opaque)
                                    }
                                }
                            }
                        }
                        #endregion
                    }
                    finally
                    {
                        directXBitmap.UnlockBits(mapDest);
                        _dxDevice.ImmediateContext.Unmap(_stagingTex, 0);
                    }


                    resultBitmap = (Bitmap)directXBitmap.Clone();
                    UpdateCache(resultBitmap, detectionBox);
                    return resultBitmap;
                }
            }
            catch (Exception e)
            {
                LogManager.Log(LogLevel.Error, LocalizationManager.GetString("Msg_Capture_DXError", e.Message));

                if (++_consecutiveFailures >= MAX_CONSECUTIVE_FAILURES)
                    lock (_displayLock) { _displayChangesPending = true; }

                return GetCachedFrame(detectionBox);
            }
            finally
            {
                desktopResource?.Dispose();
                try
                {
                    if (frameAcquired && _deskDuplication != null)
                    {
                        _deskDuplication.ReleaseFrame();
                    }
                }
                catch { }

            }
        }
        #region Frame Caching


        private void UpdateCache(Bitmap frame, Rectangle bounds)
        {
            if (_cachedFrame == null ||
                !_cachedFrameBounds.Equals(bounds) ||
                DateTime.Now - _lastFrameTime > _frameCacheTimeout)
            {
                _cachedFrame?.Dispose();
                _cachedFrame = (Bitmap)frame.Clone();
                _cachedFrameBounds = bounds;
            }
            _lastFrameTime = DateTime.Now;
        }


        private Bitmap? GetCachedFrame(Rectangle detectionBox)
        {
            if (_cachedFrame != null &&
                _cachedFrameBounds.Equals(detectionBox) &&
                DateTime.Now - _lastFrameTime <= _frameCacheTimeout)
            {
                return (Bitmap)_cachedFrame.Clone();
            }
            return null;
        }
        #endregion
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
                    g.CopyFromScreen(
                        detectionBox.Left,
                        detectionBox.Top,
                        0, 0,
                        detectionBox.Size,
                        CopyPixelOperation.SourceCopy
                    );

                    if (Dictionary.toggleState["Third Person Support"])
                    {
                        int width = screenCaptureBitmap.Width / 2;
                        int height = screenCaptureBitmap.Height / 2;
                        int startY = screenCaptureBitmap.Height - height;

                        using var brush = new SolidBrush(System.Drawing.Color.Black);
                        g.FillRectangle(brush, 0, startY, width, height);
                    }
                }

                // Clone the bitmap to avoid race conditions with Sticky Aim / SaveFrame
                // The source bitmap is reused, so returning it directly can cause crashes
                // if the caller is still using it when the next frame capture starts
                return (Bitmap)screenCaptureBitmap.Clone();
            }
            catch (Exception ex)
            {
                LogManager.Log(LogLevel.Error, LocalizationManager.GetString("Msg_Capture_GDIFailed", ex.Message));
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

                directXBitmap?.Dispose();
                directXBitmap = null;

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

        #region dispose
        public void DisposeDxgiResources()
        {
            lock (_displayLock)
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
                    directXBitmap?.Dispose();

                    _deskDuplication = null;
                    _stagingTex = null;
                    _dxDevice = null;
                    _cachedFrame = null;

                    // Small delay to ensure resources are fully released
                    //System.Threading.Thread.Sleep(50);
                }
                catch (Exception ex)
                {
                    LogManager.Log(LogLevel.Error, LocalizationManager.GetString("Msg_Capture_DXGIReleaseFailed", ex.Message));
                }
            }
        }
        public void Dispose()
        {
            DisplayManager.DisplayChanged -= OnDisplayChanged;
            DisposeDxgiResources();
            screenCaptureBitmap?.Dispose();
        }
        #endregion
    }
}