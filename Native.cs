using System;
using System.Runtime.InteropServices;

namespace MonitorControl
{
    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct Device
        {
            public int Size;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
            public uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Id;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Key;
            public static Device Create() { return new Device { Size = Marshal.SizeOf(typeof(Device)) }; }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct Mode
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
            public ushort SpecVersion, DriverVersion, Size, DriverExtra;
            public uint Fields;
            public int X, Y;
            public uint Orientation, FixedOutput;
            public short Color, Duplex, YResolution, TTOption, Collate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string FormName;
            public ushort LogPixels;
            public uint BitsPerPixel, Width, Height, DisplayFlags, Frequency;
            public uint IcmMethod, IcmIntent, MediaType, DitherType, Reserved1, Reserved2, PanningWidth, PanningHeight;
            public static Mode Create() { return new Mode { Size = (ushort)Marshal.SizeOf(typeof(Mode)) }; }
        }
        internal const uint Position = 0x20, Rotation = 0x80, Bits = 0x40000, Width = 0x80000, Height = 0x100000, Frequency = 0x400000;
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EnumDisplayDevices(string device, uint index, ref Device data, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EnumDisplaySettingsEx(string device, int modeIndex, ref Mode mode, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int ChangeDisplaySettingsEx(string device, ref Mode mode, IntPtr hwnd, uint flags, IntPtr param);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "ChangeDisplaySettingsExW")]
        internal static extern int Commit(string device, IntPtr mode, IntPtr hwnd, uint flags, IntPtr param);
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnregisterHotKey(IntPtr hwnd, int id);
        [DllImport("user32.dll")]
        internal static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DestroyIcon(IntPtr handle);
    }
}
