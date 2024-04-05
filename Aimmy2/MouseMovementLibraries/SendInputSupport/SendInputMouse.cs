using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace MouseMovementLibraries.SendInputSupport
{
    class SendInputMouse
    {
        // Admittedly written by ChatGPT, I accidentially had ChatGPT cook this up while asking it to rewrite some
        // python script someone sent me over "Raw Input Manipulation" (never heard of it) and it came up with a SendInput Class
        // I know I know, it's similar to Mouse Event, but I decided to add it anyways :shrug:

        // Nori

        [DllImport("user32.dll")]
        static extern void SendInput(int nInputs, INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        struct INPUT
        {
            public int type;
            public InputUnion U;
        }

        [StructLayout(LayoutKind.Explicit)]
        struct InputUnion
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        public static void SendMouseCommand(uint MouseCommand, int x = 0, int y = 0)
        {
            INPUT input = new INPUT
            {
                type = 0, 
                U = new InputUnion
                {
                    mi = new MOUSEINPUT
                    {
                        dx = x,
                        dy = y,
                        dwFlags = MouseCommand
                    }
                }
            };

            SendInput(1, [input], Marshal.SizeOf(typeof(INPUT)));
        }
    }
}
