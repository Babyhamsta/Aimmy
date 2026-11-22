using Microsoft.ML.OnnxRuntime;
using Aimmy2.Class;
using Aimmy2.AILogic;
using Other;
using SharpGen.Runtime;
using System.Diagnostics;
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
        private bool _directXFailedPermanently = false; // Track if DirectX failed with unsupported error

        // Capturing
        private Bitmap? screenCaptureBitmap;

        private Bitmap? directXBitmap;
        private ID3D11Device? _dxDevice;
        private readonly AutoResetEvent _copyReady = new(false);
        private IDXGIDevice2? _eventDevice;
        private IDXGIOutputDuplication? _deskDuplication;
        private ID3D11Texture2D? _stagingTex;

        // Frame caching for DirectX
        private Rectangle _cachedFrameBounds;

        private long _slowestReadbackTicks;
        internal string? SlowestReadback { get; private set; }
        internal Rectangle CapturedPixels { get; private set; }
        internal long LastCaptureTimestamp { get; private set; }

        // Display change handling
        private readonly object _displayLock = new();

        private int _displayChangesPending;

        // Performance tracking
        private int _consecutiveFailures = 0;

        private const int MAX_CONSECUTIVE_FAILURES = 5;

        #endregion Variables

        #region Handlers

        public CaptureManager()
        {
            // Subscribe to display changes FIRST
            DisplayManager.DisplayChanged += OnDisplayChanged;
        }

        private void OnDisplayChanged(object? sender, DisplayChangedEventArgs e)
        {
            Interlocked.Exchange(ref _displayChangesPending, 1);
            LogManager.Log(LogLevel.Info, "Display change detected. DirectX resources will be reinitialized.");
        }

        public bool HandlePendingDisplayChanges()
        {
            if (Interlocked.Exchange(ref _displayChangesPending, 0) == 0) return false;
            _consecutiveFailures = 0;
            DisposeDxgiResources();
            return true;
        }

        #endregion Handlers

        #region DirectX

        public void InitializeDxgiDuplication()
        {
            DisposeDxgiResources();
            try
            {
                var currentDisplay = DisplayManager.CurrentDisplay;
                if (currentDisplay == null)
                {
                    LogManager.Log(LogLevel.Error, "No current display available. DisplayManager may not be initialized.");
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
                    LogManager.Log(LogLevel.Info, $"Checking Adapter {adapterIndex}: {adapter.Description.Description.TrimEnd('\0')}");

                    for (uint outputIndex = 0;
                        adapter.EnumOutputs(outputIndex, out var output).Success;
                        outputIndex++)
                    {
                        using (output)
                        {
                            var output1 = output.QueryInterface<IDXGIOutput1>();
                            var outputDesc = output1.Description;
                            var outputBounds = new System.Windows.Rect(
                                outputDesc.DesktopCoordinates.Left,
                                outputDesc.DesktopCoordinates.Top,
                                outputDesc.DesktopCoordinates.Right - outputDesc.DesktopCoordinates.Left,
                                outputDesc.DesktopCoordinates.Bottom - outputDesc.DesktopCoordinates.Top);
                            LogManager.Log(LogLevel.Info, $"Found Output {outputIndex}: DeviceName = '{outputDesc.DeviceName.TrimEnd('\0')}', Bounds = {outputBounds}");

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
                    adapter.Dispose();
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
                                LogManager.Log(LogLevel.Warning, $"Could not match display by name or bounds. Found a fallback index, {targetIndex}.");
                                targetOutput1 = output.QueryInterface<IDXGIOutput1>();
                                output.Dispose();
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
                    LogManager.Log(LogLevel.Error, "No suitable display output found for DirectX capture.", true, 6000);
                    throw new Exception("No suitable display output found");
                }

                using var ownedAdapter = targetAdapter;
                using var ownedOutput = targetOutput1;
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
                        LogManager.Log(LogLevel.Error, $"Failed to create D3D11 device: {result}", true, 6000);
                        throw new Exception($"Failed to create D3D11 device: {result}");
                    }
                }

                // Create desktop duplication
                _eventDevice = _dxDevice.QueryInterface<IDXGIDevice2>();
                _deskDuplication = targetOutput1.DuplicateOutput(_dxDevice);
                _consecutiveFailures = 0; //reset on success

                LogManager.Log(LogLevel.Info, "DirectX Desktop Duplication initialized successfully.");
            }
            catch (SharpGenException ex) when (ex.ResultCode == Vortice.DXGI.ResultCode.Unsupported || ex.HResult == unchecked((int)0x887A0004))
            {
                LogManager.Log(LogLevel.Error, $"DirectX Desktop Duplication not supported on this system: {ex.Message}", true, 6000);
                _directXFailedPermanently = true;
                DisposeDxgiResources();

                AimSettings.ScreenCaptureMethod = "GDI+";
                _currentCaptureMethod = "GDI+";

                LogManager.Log(LogLevel.Error, "DirectX Desktop Duplication not supported on this system. Switched to GDI+ capture.", true, 6000);
            }
            catch (Exception ex)
            {
                LogManager.Log(LogLevel.Error, $"Failed to initialize DirectX Desktop Duplication: {ex.Message}", true, 6000);
                DisposeDxgiResources();
                throw;
            }
        }

        internal void PrepareDirectX(int size)
        {
            if (AimSettings.ScreenCaptureMethod != "DirectX" || _directXFailedPermanently) return;
            InitializeDxgiDuplication();
            if (_deskDuplication == null) return;
            EnsureReadbackBuffers(size, size);
            _currentCaptureMethod = "DirectX";
        }

        [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(directXBitmap), nameof(_stagingTex))]
        private void EnsureReadbackBuffers(int width, int height)
        {
            if (directXBitmap != null && directXBitmap.Width == width && directXBitmap.Height == height && _stagingTex != null) return;
            var texture = _dxDevice!.CreateTexture2D(new Texture2DDescription
            {
                Width = (uint)width,
                Height = (uint)height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new(1, 0),
                Usage = ResourceUsage.Staging,
                CPUAccessFlags = CpuAccessFlags.Read,
                BindFlags = BindFlags.None
            });
            Bitmap bitmap;
            try { bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb); }
            catch { texture.Dispose(); throw; }

            _stagingTex?.Dispose();
            directXBitmap?.Dispose();
            _stagingTex = texture;
            directXBitmap = bitmap;
            _cachedFrameBounds = Rectangle.Empty;
        }

        private bool DirectX(Rectangle detectionBox, Array? inputBuffer, bool allowStaleCache, out Rectangle capturedBox)
        {
            capturedBox = detectionBox;
            int w = detectionBox.Width;
            int h = detectionBox.Height;
            bool frameAcquired = false;
            IDXGIResource? desktopResource = null;

            try
            {
                // Check if we need to reinitialize
                if (_dxDevice == null || _dxDevice.ImmediateContext == null || _deskDuplication == null)
                {
                    InitializeDxgiDuplication();
                    if (_dxDevice == null || _dxDevice.ImmediateContext == null || _deskDuplication == null)
                    {
                        Interlocked.Exchange(ref _displayChangesPending, 1);
                        return HasCachedFrame(detectionBox, allowStaleCache);
                    }
                }

                if (_pendingReadback.HasValue) return FinishReadback(detectionBox, inputBuffer, out capturedBox);
                EnsureReadbackBuffers(w, h);

                long acquireStart = Stopwatch.GetTimestamp();
                int timeout = _consecutiveFailures > 0 ? 5 : 1;
                var result = _deskDuplication!.AcquireNextFrame((uint)timeout, out var frameInfo, out desktopResource);

                if (result == Vortice.DXGI.ResultCode.WaitTimeout)
                {
                    // No new frame available - this is normal
                    _consecutiveFailures = 0; // Reset failure counter
                    return HasCachedFrame(detectionBox, allowStaleCache);
                }
                else if (result == Vortice.DXGI.ResultCode.DeviceRemoved || result == Vortice.DXGI.ResultCode.AccessLost)
                { // Device lost - need to reinitialize
                    _consecutiveFailures++;

                    if (_consecutiveFailures >= MAX_CONSECUTIVE_FAILURES)
                        Interlocked.Exchange(ref _displayChangesPending, 1);

                    return HasCachedFrame(detectionBox, allowStaleCache);
                }
                else if (result != Result.Ok)
                {
                    // Other error
                    _consecutiveFailures++;
                    return HasCachedFrame(detectionBox, allowStaleCache);
                }

                long capturedAt = Stopwatch.GetTimestamp();
                frameAcquired = true;

                using (var screenTexture = desktopResource.QueryInterface<ID3D11Texture2D>())
                {
                    #region Display Bounds

                    var display = DisplayBounds();
                    var (visible, destination) = CaptureTargetSelector.VisibleRegion(detectionBox, display);
                    if (visible.Width > 0 && visible.Height > 0)
                    {
                        var box = new Box(visible.Left - display.Left, visible.Top - display.Top, 0,
                            visible.Right - display.Left, visible.Bottom - display.Top, 1);

                        _dxDevice.ImmediateContext.CopySubresourceRegion(
                               _stagingTex, 0,
                               (uint)destination.X,
                               (uint)destination.Y,
                               0,
                               screenTexture, 0, box);
                    }
                    else
                    {
                        LogManager.Log(LogLevel.Warning, "No visible region to copy from DirectX capture.", true, 3000);
                        return HasCachedFrame(detectionBox, allowStaleCache);
                    }

                    #endregion Display Bounds

                    _dxDevice.ImmediateContext.Flush();
                    _pendingReadback = new(detectionBox, visible, destination, acquireStart, capturedAt);
                    _mapTicks = _submitTicks = _waitTicks = 0;
                }
                desktopResource.Dispose();
                desktopResource = null;
                frameAcquired = false;
                _deskDuplication.ReleaseFrame();
                return FinishReadback(detectionBox, inputBuffer, out capturedBox);
            }
            catch (Exception e)
            {
                LogManager.Log(LogLevel.Error, $"DirectX capture error: {e.Message}");

                if (++_consecutiveFailures >= MAX_CONSECUTIVE_FAILURES)
                    Interlocked.Exchange(ref _displayChangesPending, 1);

                return HasCachedFrame(detectionBox, allowStaleCache);
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

        private readonly record struct PendingReadback(Rectangle Crop, Rectangle Visible, System.Drawing.Point Destination,
            long AcquireStart, long CapturedAt);
        private PendingReadback? _pendingReadback;
        private bool _waitingForCopy;
        private long _mapTicks, _submitTicks, _waitTicks;
        private int _deferredReads, _movedCrops, _expiredReads;
        internal string ReadbackCounts => $"Deferred GPU map calls: {_deferredReads}, discarded for changed crop: {_movedCrops}, discarded for age: {_expiredReads}";

        private bool DeferReadback(long capturedAt)
        {
            if (Stopwatch.GetElapsedTime(capturedAt).TotalSeconds >= 1)
                Interlocked.Exchange(ref _displayChangesPending, 1);
            _deferredReads++;
            return false;
        }

        private unsafe bool FinishReadback(Rectangle requestedCrop, Array? inputBuffer, out Rectangle capturedBox)
        {
            var (detectionBox, visible, destination, acquireStart, capturedAt) = _pendingReadback!.Value;
            capturedBox = detectionBox;
            var context = _dxDevice!.ImmediateContext;
            var texture = _stagingTex!;
            // Some drivers begin system-memory transfer on the first Map. Probe before
            // queuing the completion event so that transfer is covered by the wait.
            long attemptStart = Stopwatch.GetTimestamp();
            MappedSubresource map;
            Result result;
            while (true)
            {
                long phaseStart = Stopwatch.GetTimestamp();
                result = context.Map(texture, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.DoNotWait, out map);
                _mapTicks += Stopwatch.GetTimestamp() - phaseStart;
                if (result != Vortice.DXGI.ResultCode.WasStillDrawing) break;

                // A completion event can precede CPU visibility of the staging texture.
                // Retry within the original budget instead of restarting the entire AI loop.
                if (Stopwatch.GetElapsedTime(attemptStart).TotalMilliseconds >= 4)
                    return DeferReadback(capturedAt);

                if (!_waitingForCopy)
                {
                    phaseStart = Stopwatch.GetTimestamp();
                    _copyReady.Reset();
                    _eventDevice!.EnqueueSetEvent(_copyReady.SafeWaitHandle.DangerousGetHandle());
                    context.Flush();
                    _submitTicks += Stopwatch.GetTimestamp() - phaseStart;
                    _waitingForCopy = true;
                }

                phaseStart = Stopwatch.GetTimestamp();
                int remainingMs = 4 - (int)Math.Ceiling(Stopwatch.GetElapsedTime(attemptStart, phaseStart).TotalMilliseconds);
                if (remainingMs <= 0) return DeferReadback(capturedAt);
                bool ready = _copyReady.WaitOne(remainingMs);
                _waitTicks += Stopwatch.GetTimestamp() - phaseStart;
                if (!ready) return DeferReadback(capturedAt);
                _waitingForCopy = false;
            }
            _waitingForCopy = false;
            _pendingReadback = null;
            result.CheckError();

            if (requestedCrop.Size != detectionBox.Size || Stopwatch.GetElapsedTime(capturedAt).TotalMilliseconds > 50)
            {
                context.Unmap(texture, 0);
                if (requestedCrop.Size != detectionBox.Size) _movedCrops++;
                else _expiredReads++;
                return false;
            }
            long mappedAt = Stopwatch.GetTimestamp();
            _cachedFrameBounds = Rectangle.Empty;
            try
            {
                if (inputBuffer is Float16[] halves)
                    MathUtil.BgraToHalfArrayInPlace((byte*)map.DataPointer, (int)map.RowPitch, visible,
                        destination, AimSettings.ThirdPersonSupport, halves, detectionBox.Width);
                else if (inputBuffer is float[] floats)
                    MathUtil.BgraToFloatArrayInPlace((byte*)map.DataPointer, (int)map.RowPitch, visible,
                        destination, AimSettings.ThirdPersonSupport, floats, detectionBox.Width);
                else
                    CopyToBitmap(map, detectionBox, visible, destination);
            }
            finally
            {
                context.Unmap(texture, 0);
            }
            if (inputBuffer == null) _cachedFrameBounds = detectionBox;
            _consecutiveFailures = 0;
            long end = Stopwatch.GetTimestamp();
            if (end - acquireStart > _slowestReadbackTicks)
            {
                _slowestReadbackTicks = end - acquireStart;
                SlowestReadback = $"Slowest successful DirectX readback: acquire {Stopwatch.GetElapsedTime(acquireStart, capturedAt).TotalMilliseconds:F2} ms, " +
                    $"submit to ready (including deferred time) {Stopwatch.GetElapsedTime(capturedAt, mappedAt).TotalMilliseconds:F2} ms " +
                    $"(Map {Stopwatch.GetElapsedTime(0, _mapTicks).TotalMilliseconds:F2} ms, " +
                    $"submit {Stopwatch.GetElapsedTime(0, _submitTicks).TotalMilliseconds:F2} ms, " +
                    $"event wait {Stopwatch.GetElapsedTime(0, _waitTicks).TotalMilliseconds:F2} ms), " +
                    $"pixel processing and unmap {Stopwatch.GetElapsedTime(mappedAt, end).TotalMilliseconds:F2} ms";
            }
            CapturedPixels = new Rectangle(destination, visible.Size);
            LastCaptureTimestamp = capturedAt;
            return true;
        }

        private void CopyToBitmap(MappedSubresource map, Rectangle detectionBox, Rectangle visible, System.Drawing.Point destination)
        {
            int w = detectionBox.Width, h = detectionBox.Height;
            var boundsRect = new Rectangle(0, 0, w, h);
            BitmapData? mapDest = null;

            try
            {
                mapDest = directXBitmap!.LockBits(boundsRect, ImageLockMode.WriteOnly, directXBitmap!.PixelFormat);
                unsafe
                {
                    byte* src = (byte*)map.DataPointer;
                    byte* dst = (byte*)mapDest.Scan0;
                    int srcStride = (int)map.RowPitch;
                    int dstStride = mapDest.Stride;

                    int left = destination.X;
                    int top = destination.Y;
                    int bytes = visible.Width * 4;
                    bool padded = left != 0 || top != 0 || bytes != w * 4 || visible.Height != h;
                    if (!padded && srcStride == bytes && dstStride == bytes)
                    {
                        long length = (long)bytes * h;
                        Buffer.MemoryCopy(src, dst, length, length);
                    }
                    else
                    {
                        for (int y = 0; y < h; y++)
                        {
                            if (padded) new Span<byte>(dst, w * 4).Clear();
                            if (y >= top && y < top + visible.Height)
                                Buffer.MemoryCopy(src + left * 4, dst + left * 4, bytes, bytes);
                            src += srcStride;
                            dst += dstStride;
                        }
                    }

                    if (AimSettings.ThirdPersonSupport) // a mask basically
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
            }
            finally
            {
                if (mapDest != null) directXBitmap!.UnlockBits(mapDest);
            }
        }

        #region Frame Caching

        private bool HasCachedFrame(Rectangle detectionBox, bool allowStaleCache)
        {
            // Only the performance helper may repeat a frame to measure inference throughput.
            return allowStaleCache && directXBitmap != null && detectionBox == _cachedFrameBounds;
        }

        #endregion Frame Caching

        #endregion DirectX

        #region GDI

        private static Rectangle DisplayBounds() => new(DisplayManager.ScreenLeft, DisplayManager.ScreenTop,
            DisplayManager.ScreenWidth, DisplayManager.ScreenHeight);

        private Bitmap GDIScreen(Rectangle detectionBox)
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
                    var (visible, destination) = CaptureTargetSelector.VisibleRegion(detectionBox, DisplayBounds());
                    CapturedPixels = new Rectangle(destination, visible.Size);
                    if (visible.Size != detectionBox.Size) g.Clear(System.Drawing.Color.Black);
                    if (visible.Width > 0 && visible.Height > 0)
                        g.CopyFromScreen(visible.Location, destination, visible.Size, CopyPixelOperation.SourceCopy);

                    if (AimSettings.ThirdPersonSupport)
                    {
                        int width = screenCaptureBitmap.Width / 2;
                        int height = screenCaptureBitmap.Height / 2;
                        int startY = screenCaptureBitmap.Height - height;

                        using var brush = new SolidBrush(System.Drawing.Color.Black);
                        g.FillRectangle(brush, 0, startY, width, height);
                    }
                }

                LastCaptureTimestamp = Stopwatch.GetTimestamp();
                return screenCaptureBitmap;
            }
            catch (Exception ex)
            {
                LogManager.Log(LogLevel.Error, $"GDI+ screen capture failed: {ex.Message}");
                throw;
            }
        }

        #endregion GDI

        // The inference gate serializes borrowers. Export takes its own copy before queuing work.
        internal Bitmap? BorrowFrame(Rectangle detectionBox, out Rectangle capturedBox, bool allowStaleCache = false)
        {
            capturedBox = detectionBox;
            if (PrepareCaptureMethod() == "DirectX")
                return DirectX(detectionBox, null, allowStaleCache, out capturedBox) ? directXBitmap : null;
            return GDIScreen(detectionBox);
        }

        internal bool TryCaptureToTensor(Rectangle detectionBox, float[] inputBuffer, int imageSize, out Rectangle capturedBox) =>
            TryCaptureToTensorCore(detectionBox, inputBuffer, imageSize, out capturedBox);

        internal bool TryCaptureToTensor(Rectangle detectionBox, Float16[] inputBuffer, int imageSize, out Rectangle capturedBox) =>
            TryCaptureToTensorCore(detectionBox, inputBuffer, imageSize, out capturedBox);

        private bool TryCaptureToTensorCore(Rectangle detectionBox, Array inputBuffer, int imageSize, out Rectangle capturedBox)
        {
            capturedBox = detectionBox;
            if (detectionBox.Width != imageSize || detectionBox.Height != imageSize ||
                inputBuffer.Length != checked(3 * imageSize * imageSize))
                throw new ArgumentException("Capture bounds and input tensor dimensions must match.");
            return PrepareCaptureMethod() == "DirectX" && DirectX(detectionBox, inputBuffer, false, out capturedBox);
        }

        private string PrepareCaptureMethod()
        {
            string selectedMethod = AimSettings.ScreenCaptureMethod;

            // If DirectX failed permanently, force GDI+
            if (_directXFailedPermanently && selectedMethod == "DirectX")
            {
                AimSettings.ScreenCaptureMethod = "GDI+";
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
                _cachedFrameBounds = Rectangle.Empty;

                _currentCaptureMethod = selectedMethod;

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

            return _currentCaptureMethod;
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
                    _eventDevice?.Dispose();
                    _eventDevice = null;
                    _dxDevice?.Dispose();
                    directXBitmap?.Dispose();

                    _waitingForCopy = false;
                    _pendingReadback = null;
                    _deskDuplication = null;
                    _stagingTex = null;
                    _dxDevice = null;
                    directXBitmap = null;
                    _cachedFrameBounds = Rectangle.Empty;

                    // Small delay to ensure resources are fully released
                    //System.Threading.Thread.Sleep(50);
                }
                catch (Exception ex)
                {
                    LogManager.Log(LogLevel.Error, $"Error disposing DXGI resources: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            DisplayManager.DisplayChanged -= OnDisplayChanged;
            DisposeDxgiResources();
            _copyReady.Dispose();
            screenCaptureBitmap?.Dispose();
        }

        #endregion dispose
    }
}