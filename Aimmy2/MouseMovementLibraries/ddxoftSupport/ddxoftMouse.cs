using System.Runtime.InteropServices;

namespace MouseMovementLibraries.ddxoftSupport
{
    // Imported from: https://github.com/ddxoft/master/tree/master/Example/App_csharp
    // Nori
    public enum KeyModifiers
    {
        None = 0,
        Alt = 1,
        Control = 2,
        Shift = 4,
        Windows = 8
    }

    internal class ddxoftMouse
    {
        [DllImport("Kernel32")]
        private static extern System.IntPtr LoadLibrary(string dllfile);

        [DllImport("Kernel32")]
        private static extern System.IntPtr GetProcAddress(System.IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll")]
        public static extern bool FreeLibrary(IntPtr hModule);

        public delegate int pDD_btn(int btn);

        public delegate int pDD_whl(int whl);

        public delegate int pDD_key(int ddcode, int flag);

        public delegate int pDD_mov(int x, int y);

        public delegate int pDD_movR(int dx, int dy);

        public delegate int pDD_str(string str);

        public delegate int pDD_todc(int vkcode);

        public pDD_btn btn;         //Mouse button
        public pDD_whl whl;         //Mouse wheel
        public pDD_mov mov;      //Mouse move abs.
        public pDD_movR movR;  //Mouse move rel.
        public pDD_key key;         //Keyboard
        public pDD_str str;            //Input visible char
        public pDD_todc todc;      //VK to ddcode

        private IntPtr m_hinst;

        ~ddxoftMouse()
        {
            if (!m_hinst.Equals(IntPtr.Zero))
            {
                FreeLibrary(m_hinst);
            }
        }

        public int Load(string dllfile)
        {
            m_hinst = LoadLibrary(dllfile);
            if (m_hinst.Equals(IntPtr.Zero))
            {
                return -2;
            }
            else
            {
                return GetDDfunAddress(m_hinst);
            }
        }

        private int GetDDfunAddress(IntPtr hinst)
        {
            IntPtr ptr;

            ptr = GetProcAddress(hinst, "DD_btn");
            if (ptr.Equals(IntPtr.Zero)) { return -1; }
            btn = Marshal.GetDelegateForFunctionPointer<pDD_btn>(ptr);

            ptr = GetProcAddress(hinst, "DD_whl");
            if (ptr.Equals(IntPtr.Zero)) { return -1; }
            whl = Marshal.GetDelegateForFunctionPointer<pDD_whl>(ptr);

            ptr = GetProcAddress(hinst, "DD_mov");
            if (ptr.Equals(IntPtr.Zero)) { return -1; }
            mov = Marshal.GetDelegateForFunctionPointer<pDD_mov>(ptr);

            ptr = GetProcAddress(hinst, "DD_key");
            if (ptr.Equals(IntPtr.Zero)) { return -1; }
            key = Marshal.GetDelegateForFunctionPointer<pDD_key>(ptr);

            ptr = GetProcAddress(hinst, "DD_movR");
            if (ptr.Equals(IntPtr.Zero)) { return -1; }
            movR = Marshal.GetDelegateForFunctionPointer<pDD_movR>(ptr);

            ptr = GetProcAddress(hinst, "DD_str");
            if (ptr.Equals(IntPtr.Zero)) { return -1; }
            str = Marshal.GetDelegateForFunctionPointer<pDD_str>(ptr);

            ptr = GetProcAddress(hinst, "DD_todc");
            if (ptr.Equals(IntPtr.Zero)) { return -1; }
            todc = Marshal.GetDelegateForFunctionPointer<pDD_todc>(ptr);

            return 1;
        }
    }
}