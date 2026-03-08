using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Other;

namespace Aimmy2.Other
{
    /// <summary>
    /// 高级身份欺骗器 - 使用底层Windows API绕过.NET限制
    /// </summary>
    public static class AdvancedIdentitySpoofer
    {
        #region Native API Definitions

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern int NtQueryInformationProcess(
            IntPtr processHandle,
            int processInformationClass,
            ref PROCESS_BASIC_INFORMATION processInformation,
            int processInformationLength,
            out int returnLength);

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern int NtWriteVirtualMemory(
            IntPtr processHandle,
            IntPtr baseAddress,
            byte[] buffer,
            int numberOfBytesToWrite,
            out int numberOfBytesWritten);

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern int NtReadVirtualMemory(
            IntPtr processHandle,
            IntPtr baseAddress,
            byte[] buffer,
            int numberOfBytesToRead,
            out int numberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualProtect(
            IntPtr lpAddress,
            UIntPtr dwSize,
            uint flNewProtect,
            out uint lpflOldProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(
            IntPtr hProcess,
            IntPtr lpBaseAddress,
            byte[] lpBuffer,
            int nSize,
            out int lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(
            IntPtr hProcess,
            IntPtr lpBaseAddress,
            byte[] lpBuffer,
            int nSize,
            out int lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(
            uint processAccess,
            bool bInheritHandle,
            int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetCurrentThread();

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern int NtSetInformationThread(
            IntPtr threadHandle,
            int threadInformationClass,
            IntPtr threadInformation,
            int threadInformationLength);

        private const int ProcessBasicInformation = 0;
        private const uint PAGE_EXECUTE_READWRITE = 0x40;
        private const uint PAGE_READWRITE = 0x04;
        private const uint PROCESS_ALL_ACCESS = 0x1F0FFF;
        private const int ThreadHideFromDebugger = 0x11;

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_BASIC_INFORMATION
        {
            public IntPtr Reserved1;
            public IntPtr PebAddress;
            public IntPtr Reserved2;
            public IntPtr Reserved3;
            public IntPtr UniquePid;
            public IntPtr MoreReserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct UNICODE_STRING
        {
            public ushort Length;
            public ushort MaximumLength;
            public IntPtr Buffer;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RTL_USER_PROCESS_PARAMETERS
        {
            public uint MaximumLength;
            public uint Length;
            public uint Flags;
            public uint DebugFlags;
            public IntPtr ConsoleHandle;
            public uint ConsoleFlags;
            public IntPtr StandardInput;
            public IntPtr StandardOutput;
            public IntPtr StandardError;
            public UNICODE_STRING CurrentDirectory;
            public IntPtr CurrentDirectoryHandle;
            public UNICODE_STRING DllPath;
            public UNICODE_STRING ImagePathName;
            public UNICODE_STRING CommandLine;
        }

        #endregion

        /// <summary>
        /// 修改PEB中的进程映像名称 - 绕过任务管理器检测
        /// </summary>
        public static bool SpoofProcessImageName(string newImageName)
        {
            try
            {
                LogManager.Log(LogManager.LogLevel.Info, $"[AdvancedIdentitySpoofer] 开始修改PEB映像名称为: {newImageName}");

                IntPtr hProcess = GetCurrentProcess();
                
                // 获取PEB地址
                PROCESS_BASIC_INFORMATION pbi = new();
                int status = NtQueryInformationProcess(
                    hProcess,
                    ProcessBasicInformation,
                    ref pbi,
                    Marshal.SizeOf(typeof(PROCESS_BASIC_INFORMATION)),
                    out _);

                if (status != 0)
                {
                    LogManager.Log(LogManager.LogLevel.Error, $"[AdvancedIdentitySpoofer] NtQueryInformationProcess失败: 0x{status:X8}");
                    return false;
                }

                LogManager.Log(LogManager.LogLevel.Info, $"[AdvancedIdentitySpoofer] PEB地址: 0x{pbi.PebAddress.ToInt64():X16}");

                // 读取PEB中的ProcessParameters指针
                // PEB偏移0x20处是ProcessParameters指针 (x64)
                IntPtr processParamsOffset = IntPtr.Add(pbi.PebAddress, IntPtr.Size == 8 ? 0x20 : 0x10);
                byte[] processParamsBytes = new byte[IntPtr.Size];
                
                if (!ReadProcessMemory(hProcess, processParamsOffset, processParamsBytes, IntPtr.Size, out _))
                {
                    LogManager.Log(LogManager.LogLevel.Error, "[AdvancedIdentitySpoofer] 读取ProcessParameters失败");
                    return false;
                }

                IntPtr processParamsAddress = IntPtr.Size == 8 
                    ? (IntPtr)BitConverter.ToInt64(processParamsBytes, 0)
                    : (IntPtr)BitConverter.ToInt32(processParamsBytes, 0);

                LogManager.Log(LogManager.LogLevel.Info, $"[AdvancedIdentitySpoofer] ProcessParameters地址: 0x{processParamsAddress.ToInt64():X16}");

                // 修改ImagePathName
                // RTL_USER_PROCESS_PARAMETERS偏移0x60是ImagePathName (x64)
                int imagePathOffset = IntPtr.Size == 8 ? 0x60 : 0x38;
                IntPtr imagePathAddress = IntPtr.Add(processParamsAddress, imagePathOffset);

                // 准备新的UNICODE_STRING
                byte[] newNameBytes = Encoding.Unicode.GetBytes(newImageName);
                ushort newLength = (ushort)newNameBytes.Length;
                ushort newMaxLength = (ushort)(newNameBytes.Length + 2);

                // 分配新内存存储字符串
                IntPtr newStringBuffer = Marshal.AllocHGlobal(newMaxLength);
                Marshal.Copy(newNameBytes, 0, newStringBuffer, newNameBytes.Length);
                Marshal.WriteByte(IntPtr.Add(newStringBuffer, newNameBytes.Length), 0);
                Marshal.WriteByte(IntPtr.Add(newStringBuffer, newNameBytes.Length + 1), 0);

                // 修改内存保护
                uint oldProtect;
                if (!VirtualProtect(imagePathAddress, (UIntPtr)16, PAGE_READWRITE, out oldProtect))
                {
                    LogManager.Log(LogManager.LogLevel.Error, "[AdvancedIdentitySpoofer] VirtualProtect失败");
                    Marshal.FreeHGlobal(newStringBuffer);
                    return false;
                }

                // 写入新的UNICODE_STRING
                byte[] unicodeStringBytes = new byte[16]; // sizeof(UNICODE_STRING) on x64
                BitConverter.GetBytes(newLength).CopyTo(unicodeStringBytes, 0);
                BitConverter.GetBytes(newMaxLength).CopyTo(unicodeStringBytes, 2);
                BitConverter.GetBytes(newStringBuffer.ToInt64()).CopyTo(unicodeStringBytes, 8);

                if (!WriteProcessMemory(hProcess, imagePathAddress, unicodeStringBytes, unicodeStringBytes.Length, out _))
                {
                    LogManager.Log(LogManager.LogLevel.Error, "[AdvancedIdentitySpoofer] 写入ImagePathName失败");
                    VirtualProtect(imagePathAddress, (UIntPtr)16, oldProtect, out _);
                    Marshal.FreeHGlobal(newStringBuffer);
                    return false;
                }

                // 恢复内存保护
                VirtualProtect(imagePathAddress, (UIntPtr)16, oldProtect, out _);

                LogManager.Log(LogManager.LogLevel.Info, "[AdvancedIdentitySpoofer] PEB映像名称修改成功");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"[AdvancedIdentitySpoofer] SpoofProcessImageName异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 修改命令行参数
        /// </summary>
        public static bool SpoofCommandLine(string newCommandLine)
        {
            try
            {
                LogManager.Log(LogManager.LogLevel.Info, $"[AdvancedIdentitySpoofer] 开始修改命令行参数");

                IntPtr hProcess = GetCurrentProcess();
                
                PROCESS_BASIC_INFORMATION pbi = new();
                int status = NtQueryInformationProcess(
                    hProcess,
                    ProcessBasicInformation,
                    ref pbi,
                    Marshal.SizeOf(typeof(PROCESS_BASIC_INFORMATION)),
                    out _);

                if (status != 0)
                {
                    return false;
                }

                IntPtr processParamsOffset = IntPtr.Add(pbi.PebAddress, IntPtr.Size == 8 ? 0x20 : 0x10);
                byte[] processParamsBytes = new byte[IntPtr.Size];
                
                if (!ReadProcessMemory(hProcess, processParamsOffset, processParamsBytes, IntPtr.Size, out _))
                {
                    return false;
                }

                IntPtr processParamsAddress = IntPtr.Size == 8 
                    ? (IntPtr)BitConverter.ToInt64(processParamsBytes, 0)
                    : (IntPtr)BitConverter.ToInt32(processParamsBytes, 0);

                // CommandLine在ImagePathName之后
                int commandLineOffset = IntPtr.Size == 8 ? 0x70 : 0x40;
                IntPtr commandLineAddress = IntPtr.Add(processParamsAddress, commandLineOffset);

                byte[] newCmdBytes = Encoding.Unicode.GetBytes(newCommandLine);
                ushort newLength = (ushort)newCmdBytes.Length;
                ushort newMaxLength = (ushort)(newCmdBytes.Length + 2);

                IntPtr newStringBuffer = Marshal.AllocHGlobal(newMaxLength);
                Marshal.Copy(newCmdBytes, 0, newStringBuffer, newCmdBytes.Length);
                Marshal.WriteByte(IntPtr.Add(newStringBuffer, newCmdBytes.Length), 0);
                Marshal.WriteByte(IntPtr.Add(newStringBuffer, newCmdBytes.Length + 1), 0);

                uint oldProtect;
                if (!VirtualProtect(commandLineAddress, (UIntPtr)16, PAGE_READWRITE, out oldProtect))
                {
                    Marshal.FreeHGlobal(newStringBuffer);
                    return false;
                }

                byte[] unicodeStringBytes = new byte[16];
                BitConverter.GetBytes(newLength).CopyTo(unicodeStringBytes, 0);
                BitConverter.GetBytes(newMaxLength).CopyTo(unicodeStringBytes, 2);
                BitConverter.GetBytes(newStringBuffer.ToInt64()).CopyTo(unicodeStringBytes, 8);

                bool success = WriteProcessMemory(hProcess, commandLineAddress, unicodeStringBytes, unicodeStringBytes.Length, out _);
                VirtualProtect(commandLineAddress, (UIntPtr)16, oldProtect, out _);

                if (success)
                {
                    LogManager.Log(LogManager.LogLevel.Info, "[AdvancedIdentitySpoofer] 命令行参数修改成功");
                }

                return success;
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"[AdvancedIdentitySpoofer] SpoofCommandLine异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 隐藏线程来自调试器
        /// </summary>
        public static bool HideThreadFromDebugger()
        {
            try
            {
                IntPtr hThread = GetCurrentThread();
                int status = NtSetInformationThread(hThread, ThreadHideFromDebugger, IntPtr.Zero, 0);
                
                if (status == 0)
                {
                    LogManager.Log(LogManager.LogLevel.Info, "[AdvancedIdentitySpoofer] 线程已隐藏来自调试器");
                    return true;
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 执行完整的身份欺骗
        /// </summary>
        public static bool PerformFullSpoof(string processName, string commandLine)
        {
            bool result = true;

            // 修改PEB映像名称
            if (!SpoofProcessImageName(processName))
            {
                result = false;
            }

            // 修改命令行
            if (!SpoofCommandLine(commandLine))
            {
                result = false;
            }

            // 隐藏线程
            HideThreadFromDebugger();

            return result;
        }
    }
}
