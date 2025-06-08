using Aimmy2.Class;
using SharpGen.Runtime;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
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

        private const int IMAGE_SIZE = 640;
        public Bitmap? screenCaptureBitmap { get; private set; }
        private ID3D11Device? _dxDevice;
        private IDXGIOutputDuplication? _deskDuplication;
        private ID3D11Texture2D? _stagingTex;

        // Display change handling
        public readonly object _displayLock = new();
        public bool _displayChangesPending { get; set; } = false;
        #endregion
        #region DirectX
        public void InitializeDxgiDuplication()
        {
            DisposeDxgiResources();
            try
            {
                var currentDisplay = DisplayManager.CurrentDisplay;

                using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
                IDXGIOutput1? targetOutput1 = null;
                IDXGIAdapter1? targetAdapter = null;
                //int globalOutputIndex = 0;
                bool foundTarget = false;

                for (uint adapterIndex = 0;
                    factory.EnumAdapters1(adapterIndex, out var adapter).Success;
                    adapterIndex++)
                {
                    for (uint outputIndex = 0;
                        adapter.EnumOutputs(outputIndex, out var output).Success;
                        outputIndex++)
                    {
                        using (output)
                        {
                            var output1 = output.QueryInterface<IDXGIOutput1>();
                            var outputDesc = output1.Description;
                            var outputBounds = new Rect(
                                outputDesc.DesktopCoordinates.Left,
                                outputDesc.DesktopCoordinates.Top,
                                outputDesc.DesktopCoordinates.Right - outputDesc.DesktopCoordinates.Left,
                                outputDesc.DesktopCoordinates.Bottom - outputDesc.DesktopCoordinates.Top);

                            // Match by device name and bounds
                            if (outputDesc.DeviceName.TrimEnd('\0') == currentDisplay.DeviceName.TrimEnd('\0') &&
                                outputBounds.Equals(currentDisplay.Bounds))
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
                    {
                        // Don't dispose targetAdapter - we'll use it later
                        break;
                    }
                    adapter.Dispose();
                }

                // Fallback to first adapter/output if not found
                if (targetOutput1 == null || targetAdapter == null)
                {
                    Debug.WriteLine("fell back to first adapter");
                    factory.EnumAdapters1(0, out targetAdapter);
                    if (targetAdapter != null)
                    {
                        targetAdapter.EnumOutputs(0, out var output);
                        targetOutput1 = output.QueryInterface<IDXGIOutput1>();
                        output.Dispose();
                    }
                }

                if (targetAdapter == null || targetOutput1 == null)
                {
                    throw new Exception("No suitable display output found");
                }

                // Create D3D11 device - handle potential failure
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

                // Cleanup
                targetAdapter.Dispose();
                targetOutput1.Dispose();
            }
            catch (Exception ex)
            {
                DisposeDxgiResources();
                throw;
            }
        }
        private Bitmap? DirectX(Rectangle detectionBox)
        {
            var displayBounds = new Rectangle(DisplayManager.ScreenLeft,
                                              DisplayManager.ScreenTop,
                                              DisplayManager.ScreenWidth,
                                              DisplayManager.ScreenHeight);

            if (detectionBox.Left < displayBounds.Left)
                detectionBox.X = displayBounds.Left;
            if (detectionBox.Top < displayBounds.Top)
                detectionBox.Y = displayBounds.Top;
            if (detectionBox.Right > displayBounds.Right)
                detectionBox.X = displayBounds.Right - detectionBox.Width;
            if (detectionBox.Bottom > displayBounds.Bottom)
                detectionBox.Y = displayBounds.Bottom - detectionBox.Height;


            int w = detectionBox.Width;
            int h = detectionBox.Height;

            try
            {
                if (_dxDevice == null || _dxDevice.ImmediateContext == null || _deskDuplication == null)
                {
                    //FileManager.LogError("Device, context, or textures are null, attempting to reinitialize");
                    InitializeDxgiDuplication();

                    if (_dxDevice == null || _dxDevice.ImmediateContext == null || _deskDuplication == null)
                    {
                        lock (_displayLock) { _displayChangesPending = true; }
                        throw new InvalidOperationException("Device, context, or textures are still null after reinitialization.");
                    }
                }

                bool requiresNewResources = _stagingTex == null || _stagingTex.Description.Width != detectionBox.Width || _stagingTex.Description.Height != detectionBox.Height;

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

                var result = _deskDuplication!.AcquireNextFrame(15, out var frameInfo, out var desktopResource);

                if (result != Result.Ok)
                {
                    if (result == Vortice.DXGI.ResultCode.DeviceRemoved)
                    {
                        lock (_displayLock) { _displayChangesPending = true; }
                        return null;
                    }

                    lock (_displayLock) { _displayChangesPending = true; }
                    return null;
                }

                frameAcquired = true;
                using var screenTexture = desktopResource.QueryInterface<ID3D11Texture2D>();

                var box = new Box { Left = detectionBox.Left, Top = detectionBox.Top, Front = 0, Right = detectionBox.Right, Bottom = detectionBox.Bottom, Back = 1 };

                _dxDevice.ImmediateContext!.CopySubresourceRegion(_stagingTex, 0, 0, 0, 0, screenTexture, 0, box);

                if (_stagingTex == null) return null;
                var map = _dxDevice.ImmediateContext.Map(_stagingTex, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
                var bitmap = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                var boundsRect = new Rectangle(0, 0, detectionBox.Width, detectionBox.Height);
                BitmapData? mapDest = null;

                try
                { 
                    mapDest = bitmap.LockBits(boundsRect, ImageLockMode.WriteOnly, bitmap.PixelFormat);

                    unsafe
                    {
                        Buffer.MemoryCopy((void*)map.DataPointer, (void*)mapDest.Scan0, mapDest.Stride * mapDest.Height, map.RowPitch * detectionBox.Height);
                    }
                        return bitmap;
                }
                finally
                {
                    if (mapDest != null)
                        bitmap.UnlockBits(mapDest);
                    
                    _dxDevice.ImmediateContext.Unmap(_stagingTex, 0);

                    if (frameAcquired)
                        _deskDuplication.ReleaseFrame();
                }

            }
            catch (Exception e)
            {
                lock (_displayLock) { _displayChangesPending = true; }
                return null;
            }
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
                    catch (Exception)
                    {
                        // This is expected if no frame is currently acquired
                    }
                }

                _deskDuplication?.Dispose();
                _stagingTex?.Dispose();
                _dxDevice?.Dispose();

                _deskDuplication = null;
                _stagingTex = null;
                _dxDevice = null;

                // Small delay to ensure resources are fully released
                System.Threading.Thread.Sleep(50);
            }
            catch (Exception ex)
            {
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

            // Handle method switch
            if (selectedMethod != _currentCaptureMethod)
            {
                // Dispose bitmap when switching methods
                screenCaptureBitmap?.Dispose();
                screenCaptureBitmap = null;
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

            if (selectedMethod == "DirectX")
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
