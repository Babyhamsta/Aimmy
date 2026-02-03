using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace MouseMovementLibraries.DRIVER
{
    // Make it static so you can call: DriverMouse.Move(...)
    internal static class DriverMouse
    {
        // From your Driver.hpp
        private const string DevicePath = @"\\.\cla300";
        private const uint IO_SEND_MOUSE_EVENT = 0x23FACC00;

        private static readonly object _lock = new object();
        private static SafeFileHandle _hDriver;

        [Flags]
        internal enum MouseFlags : int
        {
            None = 0,
            LeftButtonDown = 1,
            LeftButtonUp = 2,
            RightButtonDown = 4,
            RightButtonUp = 8,
            MiddleButtonDown = 16,
            MiddleButtonUp = 32,
            XButton1Down = 64,
            XButton1Up = 128,
            XButton2Down = 256,
            XButton2Up = 512,
            MouseWheel = 1024,
            MouseHorizontalWheel = 2048
        }

        /// <summary>
        /// Sends a mouse packet to the CLA300 driver.
        /// Your switch uses: Move(1,0,0,0) etc.
        /// buttonFlags goes into the driver struct as a SHORT.
        /// </summary>
        internal static bool Move(int buttonFlags, int dx, int dy, int wheel)
        {
            lock (_lock)
            {
                if (!EnsureOpen())
                    return false;

                // Movement + button flags
                var req = new NF_MOUSE_REQUEST
                {
                    x = dx,
                    y = dy,
                    ButtonFlags = unchecked((short)buttonFlags),
                    _padding = 0
                };

                bool ok = DeviceIoControl(
                    _hDriver,
                    IO_SEND_MOUSE_EVENT,
                    ref req,
                    Marshal.SizeOf<NF_MOUSE_REQUEST>(),
                    IntPtr.Zero,
                    0,
                    out _,
                    IntPtr.Zero
                );

                if (!ok) return false;

                // Optional wheel support (best-effort, depends on your driver implementation)
                if (wheel != 0)
                {
                    var wheelReq = new NF_MOUSE_REQUEST
                    {
                        x = 0,
                        y = wheel,
                        ButtonFlags = unchecked((short)MouseFlags.MouseWheel),
                        _padding = 0
                    };

                    ok = DeviceIoControl(
                        _hDriver,
                        IO_SEND_MOUSE_EVENT,
                        ref wheelReq,
                        Marshal.SizeOf<NF_MOUSE_REQUEST>(),
                        IntPtr.Zero,
                        0,
                        out _,
                        IntPtr.Zero
                    );
                }

                return ok;
            }
        }

        internal static bool Initialize()
        {
            lock (_lock) { return EnsureOpen(); }
        }

        internal static void Shutdown()
        {
            lock (_lock)
            {
                _hDriver?.Dispose();
                _hDriver = null;
            }
        }

        internal static bool IsConnected
        {
            get
            {
                lock (_lock)
                    return _hDriver != null && !_hDriver.IsInvalid && !_hDriver.IsClosed;
            }
        }

        private static bool EnsureOpen()
        {
            if (_hDriver != null && !_hDriver.IsInvalid && !_hDriver.IsClosed)
                return true;

            _hDriver?.Dispose();

            _hDriver = CreateFileW(
                DevicePath,
                GENERIC_READ | GENERIC_WRITE,
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero,
                OPEN_EXISTING,
                0,
                IntPtr.Zero
            );

            return _hDriver != null && !_hDriver.IsInvalid && !_hDriver.IsClosed;
        }

        // C++: struct { int x; int y; short ButtonFlags; };
        // Usually padded to 12 bytes; we make padding explicit to avoid layout mismatches.
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct NF_MOUSE_REQUEST
        {
            public int x;
            public int y;
            public short ButtonFlags;
            public short _padding;
        }

        // Win32
        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint OPEN_EXISTING = 3;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern SafeFileHandle CreateFileW(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeviceIoControl(
            SafeFileHandle hDevice,
            uint dwIoControlCode,
            ref NF_MOUSE_REQUEST lpInBuffer,
            int nInBufferSize,
            IntPtr lpOutBuffer,
            int nOutBufferSize,
            out int lpBytesReturned,
            IntPtr lpOverlapped
        );
    }
}
