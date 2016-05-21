using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;

namespace TouhouCV
{
    public class Win32
    {
        public static IntPtr GetProcessHandle(Process p)
        {
            return OpenProcess(ProcessAccessFlags.All, false, p.Id);
        }

        public static bool ReadMemoryInt32(IntPtr hndl, IntPtr addr, out int val)
        {
            int bytesRead = 0;
            byte[] buff = new byte[4];
            bool stat = ReadProcessMemory(hndl, addr, buff, 4, ref bytesRead);
            val = buff[3] << 24 | buff[2] << 16 | buff[1] << 8 | buff[0];
            return stat;
        }
        public static bool ReadMemoryFloat(IntPtr hndl, IntPtr addr, out float val)
        {
            int bytesRead = 0;
            byte[] buff = new byte[4];
            bool stat = ReadProcessMemory(hndl, addr, buff, 4, ref bytesRead);
            val = BitConverter.ToSingle(buff, 0);
            return stat;
        }

        public static bool ReadMemoryInt64(IntPtr hndl, IntPtr addr, out long val)
        {
            int bytesRead = 0;
            byte[] buff = new byte[8];
            bool stat = ReadProcessMemory(hndl, addr, buff, 8, ref bytesRead);
            val = buff[7] << 56 | buff[6] << 48 | buff[5] << 40 | buff[4] << 32
                | buff[3] << 24 | buff[2] << 16 | buff[1] << 8 | buff[0];
            return stat;
        }

        public static bool ReadMemoryByte(IntPtr hndl, IntPtr addr, out byte val)
        {
            int bytesRead = 0;
            byte[] buff = new byte[1];
            bool stat = ReadProcessMemory(hndl, addr, buff, 1, ref bytesRead);
            val = buff[0];
            return stat;
        }

        public static bool WriteMemoryByte(IntPtr hndl, IntPtr addr, byte val)
        {
            int bytesWritten = 0;
            bool stat = WriteProcessMemory(hndl, addr, new[] { val }, 1, out bytesWritten);
            return stat;
        }

        [DllImport("kernel32.dll")]
        public static extern void GetSystemInfo(out SYSTEM_INFO lpSystemInfo);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

        public struct MEMORY_BASIC_INFORMATION
        {
            public int BaseAddress;
            public int AllocationBase;
            public int AllocationProtect;
            public int RegionSize;
            public int State;
            public int Protect;
            public int lType;
        }

        public struct SYSTEM_INFO
        {
            public ushort processorArchitecture;
            ushort reserved;
            public uint pageSize;
            public IntPtr minimumApplicationAddress;
            public IntPtr maximumApplicationAddress;
            public IntPtr activeProcessorMask;
            public uint numberOfProcessors;
            public uint processorType;
            public uint allocationGranularity;
            public ushort processorLevel;
            public ushort processorRevision;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;        // x position of upper-left corner
            public int Top;         // y position of upper-left corner
            public int Right;       // x position of lower-right corner
            public int Bottom;      // y position of lower-right corner

            public int Width => Right - Left;
            public int Height => Bottom - Top;

            public RECT(int left, int right, int top, int bottom)
            {
                this.Left = left;
                this.Right = right;
                this.Top = top;
                this.Bottom = bottom;
            }

            public RECT(POINT a, POINT b)
            {
                Left = a.X;
                Right = b.X;
                Top = a.Y;
                Bottom = b.Y;
            }

            public override string ToString()
            {
                return $"<{Left}, {Top}, {Width}, {Height}>";
            }

        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;

            public POINT(int x, int y)
            {
                this.X = x;
                this.Y = y;
            }

            public override string ToString()
            {
                return $"<{X}, {Y}>";
            }
        }

        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(ProcessAccessFlags dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out int lpNumberOfBytesWritten);

        [DllImport("kernel32.dll")]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);

        [DllImport("kernel32.dll")]
        private static extern int CloseHandle(IntPtr hProcess);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, int nFlags);

        [DllImport(("user32.dll"))]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport(("user32.dll"))]
        public static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [Flags]
        private enum ProcessAccessFlags : uint
        {
            All = 0x001F0FFF,
            Terminate = 0x00000001,
            CreateThread = 0x00000002,
            VMOperation = 0x00000008,
            VMRead = 0x00000010,
            VMWrite = 0x00000020,
            DupHandle = 0x00000040,
            SetInformation = 0x00000200,
            QueryInformation = 0x00000400,
            Synchronize = 0x00100000
        }
    }
}
