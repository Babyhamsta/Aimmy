using Aimmy2.MouseMovementLibraries.GHubSupport.dist;
using System.Runtime.InteropServices;

namespace Aimmy2.MouseMovementLibraries.GHubSupport
{
    internal class LGMouse
    {
        private static nint Input = nint.Zero;
        private static Properties.IO_STATUS_BLOCK io = new();

        private const int FILE_SYNCHRONOUS_IO_NONALERT = 0x00000020;
        private const int FILE_NON_DIRECTORY_FILE = 0x00000040;
        private const int FILE_ATTRIBUTE_NORMAL = 0x00000080;
        private const int SYNCHRONIZE = 0x00100000;
        private const int GENERIC_WRITE = 0x40000000;

        public static int Initialize(string name)
        {
            Properties.OBJECT_ATTRIBUTES Attributes = new(name, 0);
            int Status = WinAPI.NtCreateFile(out Input, GENERIC_WRITE | SYNCHRONIZE, ref Attributes, ref io, nint.Zero, FILE_ATTRIBUTE_NORMAL, 0, 3, FILE_NON_DIRECTORY_FILE | FILE_SYNCHRONOUS_IO_NONALERT, nint.Zero, 0);
            Attributes.Dispose();
            return Status;
        }

        public static bool Open()
        {
            if (Input != nint.Zero)
            {
                return true;
            }

            for (int num = 9; num >= 0; num--)
            {
                int Status = Initialize("\\??\\ROOT#SYSTEM#000" + num + "#{1abc05c0-c378-41b9-9cef-df1aba82b015}");
                if (Status >= 0) break;
            }

            return false;
        }

        public static void Close()
        {
            if (Input != nint.Zero)
            {
                _ = WinAPI.ZwClose(Input);
                Input = nint.Zero;
            }
        }

        public static bool Call(Struct.MOUSE_IO buffer)
        {
            Properties.IO_STATUS_BLOCK block = new();
            return 0 == WinAPI.NtDeviceIoControlFile(Input, nint.Zero, nint.Zero, nint.Zero, ref block, 0x2a2010, ref buffer, Marshal.SizeOf(typeof(Struct.MOUSE_IO)), nint.Zero, 0);
        }

        public static void Move(int button, int x, int y, int wheel)
        {
            if (Input == nint.Zero && !Open())
            {
                return;
            }

            var io = new Struct.MOUSE_IO
            {
                Unk1 = 0,
                Button = (byte)button,
                X = (byte)x,
                Y = (byte)y,
                Wheel = (byte)wheel
            };

            if (Call(io)) return;

            Close();
            if (!Open())
            {
                throw new InvalidOperationException("Failed to open the device.");
            }
        }
    }
}