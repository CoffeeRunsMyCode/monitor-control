using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace MonitorControl
{
    internal static class DisplayTopology
    {
        [StructLayout(LayoutKind.Sequential)] internal struct Luid { public uint Low; public int High; }
        [StructLayout(LayoutKind.Sequential)] internal struct Source { public Luid Adapter; public uint Id, ModeIndex, Flags; }
        [StructLayout(LayoutKind.Sequential)] internal struct Target
        {
            public Luid Adapter; public uint Id, ModeIndex, Technology, Rotation, Scaling, Numerator, Denominator, ScanLine;
            public int Available; public uint Flags;
        }
        [StructLayout(LayoutKind.Sequential)] internal struct PathInfo { public Source Source; public Target Target; public uint Flags; }
        [StructLayout(LayoutKind.Sequential)] internal struct Signal
        {
            public ulong PixelRate;
            public uint HSyncNumerator, HSyncDenominator, VSyncNumerator, VSyncDenominator;
            public uint ActiveWidth, ActiveHeight, TotalWidth, TotalHeight, Standard, ScanLine;
        }
        [StructLayout(LayoutKind.Explicit, Size = 64)] internal struct ModeInfo
        {
            [FieldOffset(0)] public uint Type;
            [FieldOffset(4)] public uint Id;
            [FieldOffset(8)] public Luid Adapter;
            [FieldOffset(16)] public uint Width;
            [FieldOffset(20)] public uint Height;
            [FieldOffset(24)] public uint PixelFormat;
            [FieldOffset(28)] public int X;
            [FieldOffset(32)] public int Y;
            [FieldOffset(16)] public Signal Signal;
        }
        [StructLayout(LayoutKind.Sequential)] internal struct Header { public uint Type, Size; public Luid Adapter; public uint Id; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct TargetName
        {
            public Header Header; public uint Flags, Technology; public ushort Manufacturer, Product; public uint Connector;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string Name;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DevicePath;
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct SourceName
        {
            public Header Header;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        }
        [StructLayout(LayoutKind.Explicit, Size = 80)] internal struct PreferredMode
        {
            [FieldOffset(0)] public Header Header;
            [FieldOffset(20)] public uint Width;
            [FieldOffset(24)] public uint Height;
            [FieldOffset(32)] public Signal Signal;
        }
        [DllImport("user32.dll")] private static extern int GetDisplayConfigBufferSizes(uint flags, out uint paths, out uint modes);
        [DllImport("user32.dll")] private static extern int QueryDisplayConfig(uint flags, ref uint pathsCount, [Out] PathInfo[] paths, ref uint modesCount, [Out] ModeInfo[] modes, IntPtr topology);
        [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")] private static extern int GetTargetName(ref TargetName name);
        [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")] private static extern int GetSourceName(ref SourceName name);
        [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")] private static extern int GetPreferredMode(ref PreferredMode mode);
        [DllImport("user32.dll")] private static extern int SetDisplayConfig(uint pathsCount, [In] PathInfo[] paths, uint modesCount, [In] ModeInfo[] modes, uint flags);
        internal class Route
        {
            public string Device, InterfacePath, Name;
            public bool Active, Available;
            public uint SourceId, TargetId;
            internal PathInfo Path;
            internal ModeInfo TargetMode;
            internal bool HasTargetMode;
        }
        public static List<Route> Read()
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                uint pathsCount, modesCount;
                int code = GetDisplayConfigBufferSizes(1, out pathsCount, out modesCount);
                if (code == 5) throw new DesktopUnavailableException("Cannot inspect the Windows display topology (" + code + "). Run on the local desktop.");
                if (code != 0) throw new InvalidOperationException("Cannot inspect the Windows display topology (" + code + "). Run on the local desktop.");
                var paths = new PathInfo[pathsCount]; var modes = new ModeInfo[modesCount];
                code = QueryDisplayConfig(1, ref pathsCount, paths, ref modesCount, modes, IntPtr.Zero);
                if (code == 122) continue;
                if (code != 0) throw new InvalidOperationException("Cannot read the Windows display topology (" + code + ").");
                var result = new List<Route>();
                for (int i = 0; i < pathsCount; i++)
                {
                    var path = paths[i];
                    var target = new TargetName { Header = new Header { Type = 2, Size = (uint)Marshal.SizeOf(typeof(TargetName)), Adapter = path.Target.Adapter, Id = path.Target.Id } };
                    var source = new SourceName { Header = new Header { Type = 1, Size = (uint)Marshal.SizeOf(typeof(SourceName)), Adapter = path.Source.Adapter, Id = path.Source.Id } };
                    if (GetTargetName(ref target) != 0 || GetSourceName(ref source) != 0) continue;
                    bool hasTarget = path.Target.ModeIndex < modesCount;
                    var targetMode = hasTarget ? modes[path.Target.ModeIndex] : new ModeInfo();
                    if (!hasTarget && path.Target.Available != 0)
                    {
                        var preferred = new PreferredMode { Header = new Header { Type = 3, Size = 80, Adapter = path.Target.Adapter, Id = path.Target.Id } };
                        if (GetPreferredMode(ref preferred) == 0)
                        {
                            targetMode = new ModeInfo { Type = 2, Id = path.Target.Id, Adapter = path.Target.Adapter, Signal = preferred.Signal }; hasTarget = true;
                        }
                    }
                    result.Add(new Route { Device = source.DeviceName, InterfacePath = target.DevicePath, Name = target.Name, Active = (path.Flags & 1) != 0, Available = path.Target.Available != 0, SourceId = path.Source.Id, TargetId = path.Target.Id, Path = path, HasTargetMode = hasTarget, TargetMode = targetMode });
                }
                return result;
            }
            throw new InvalidOperationException("Displays are still changing. Wait a moment and refresh.");
        }
        internal static string SourceKey(Route route) { return route.Path.Source.Adapter.High + ":" + route.Path.Source.Adapter.Low + ":" + route.SourceId; }
        internal class Plan { public PathInfo[] Paths; public ModeInfo[] Modes; }
        internal static Plan Build(List<DisplaySetting> desired, List<MonitorInfo> live, List<Route> routes)
        {
            var enabled = desired.Where(d => d.Enabled).OrderByDescending(d => d.Primary).ToList();
            var choices = enabled.Select(d => routes.Where(r => r.Available && String.Equals(r.InterfacePath, live.Single(m => m.Id == d.Id).InterfacePath, StringComparison.OrdinalIgnoreCase)).OrderByDescending(r => r.Active).ToList()).ToList();
            var selected = new Route[enabled.Count];
            if (!Assign(choices, selected, new HashSet<string>(), 0)) throw new InvalidOperationException("Windows has no distinct display routes for this layout.");
            var paths = new PathInfo[enabled.Count]; var modes = new List<ModeInfo>();
            for (int i = 0; i < enabled.Count; i++)
            {
                var d = enabled[i]; var path = selected[i].Path;
                path.Flags = 1; path.Source.ModeIndex = (uint)modes.Count; path.Source.Flags = 0;
                path.Target.ModeIndex = UInt32.MaxValue; path.Target.Flags = 0;
                path.Target.Rotation = (uint)d.Rotation + 1; path.Target.Scaling = 128;
                path.Target.Numerator = d.RefreshDenominator > 0 ? d.RefreshNumerator : (uint)d.Hertz;
                path.Target.Denominator = d.RefreshDenominator > 0 ? d.RefreshDenominator : 1;
                path.Target.ScanLine = selected[i].Active && selected[i].Path.Target.ScanLine != 0 ? selected[i].Path.Target.ScanLine : 1;
                modes.Add(new ModeInfo { Type = 1, Id = path.Source.Id, Adapter = path.Source.Adapter, Width = (uint)d.Width, Height = (uint)d.Height, PixelFormat = 4, X = d.X, Y = d.Y });
                var current = live.Single(m => m.Id == d.Id).Current;
                // Keep the physical signal timing when resizing/rotating the desktop at the same refresh rate.
                bool unchanged = selected[i].Active && current != null && current.Hertz == d.Hertz;
                var signal = selected[i].TargetMode.Signal;
                bool preferred = !selected[i].Active && d.Rotation == 0 && signal.ActiveWidth == d.Width && signal.ActiveHeight == d.Height && signal.VSyncDenominator > 0 && Math.Abs((double)signal.VSyncNumerator / signal.VSyncDenominator - d.Hertz) < 1;
                if (selected[i].HasTargetMode && (unchanged || preferred))
                {
                    path.Target.ModeIndex = (uint)modes.Count;
                    modes.Add(selected[i].TargetMode);
                    path.Target.Scaling = unchanged && current.Width == d.Width && current.Height == d.Height && current.Rotation == d.Rotation ? selected[i].Path.Target.Scaling : 128;
                }
                paths[i] = path;
            }
            return new Plan { Paths = paths, Modes = modes.ToArray() };
        }
        private static bool Assign(List<List<Route>> choices, Route[] selected, HashSet<string> used, int index)
        {
            if (index == choices.Count) return true;
            foreach (var route in choices[index])
            {
                string key = SourceKey(route); if (!used.Add(key)) continue;
                selected[index] = route; if (Assign(choices, selected, used, index + 1)) return true; used.Remove(key);
            }
            return false;
        }
        public static void Execute(Plan plan, bool validateOnly)
        {
            uint flags = 0x20u | (validateOnly ? 0x40u : 0x80u | 0x200u);
            int code = SetDisplayConfig((uint)plan.Paths.Length, plan.Paths, (uint)plan.Modes.Length, plan.Modes, flags);
            if (code == 1610) code = SetDisplayConfig((uint)plan.Paths.Length, plan.Paths, (uint)plan.Modes.Length, plan.Modes, flags | 0x400u);
            if (code == 0) return;
            string message = (validateOnly ? "Windows cannot validate this layout: " : "Windows could not apply this layout: ") + new System.ComponentModel.Win32Exception(code).Message + " (" + code + ").";
            if (code == 5) throw new DesktopUnavailableException(message);
            throw new InvalidOperationException(message);
        }
    }
    // Access denied (5): this process is not on the active desktop, e.g. the PC is locked, switching users, or shutting down. Nothing was changed.
    internal class DesktopUnavailableException : InvalidOperationException
    {
        public DesktopUnavailableException(string message) : base(message) { }
    }
}
