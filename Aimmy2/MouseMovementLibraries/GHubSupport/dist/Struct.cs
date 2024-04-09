using System.Runtime.InteropServices;

namespace Aimmy2.MouseMovementLibraries.GHubSupport.dist
{
    internal class Struct
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSE_IO
        {
            public byte Button;
            public byte X;
            public byte Y;
            public byte Wheel;
            public byte Unk1;
        }
    }
}