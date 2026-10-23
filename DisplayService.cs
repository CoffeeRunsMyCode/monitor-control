using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Win32;

namespace MonitorControl
{
    internal static class DisplayService
    {
        // Made-up monitors for screenshots (--render-demo), so published images show no real model names or serials.
        internal static List<MonitorInfo> Demo;
        public static List<MonitorInfo> Scan()
        {
            if (Demo != null) return new List<MonitorInfo>(Demo);
            var routes = DisplayTopology.Read();
            var result = new List<MonitorInfo>();
            var usedSources = new HashSet<string>(routes.Where(r => r.Active).Select(DisplayTopology.SourceKey));
            foreach (var group in routes.Where(r => r.Available && !String.IsNullOrEmpty(r.InterfacePath)).GroupBy(r => r.InterfacePath, StringComparer.OrdinalIgnoreCase).OrderByDescending(g => g.Any(r => r.Active)))
            {
                var route = group.FirstOrDefault(r => r.Active) ?? group.FirstOrDefault(r => !usedSources.Contains(DisplayTopology.SourceKey(r))) ?? group.First();
                usedSources.Add(DisplayTopology.SourceKey(route));
                var mode = Native.Mode.Create();
                bool active = route.Active;
                bool found = Native.EnumDisplaySettingsEx(route.Device, active ? -1 : -2, ref mode, 0);
                if (active && !found) throw new InvalidOperationException("Cannot read the current mode for " + route.Name);
                var info = Identify(route.InterfacePath, route.Name);
                info.Device = route.Device;
                info.Ambiguous = active && routes.Count(r => r.Active && DisplayTopology.SourceKey(r) == DisplayTopology.SourceKey(route)) > 1;
                info.Current = FromMode(info, mode);
                info.Current.Enabled = active;
                info.Current.Primary = active && mode.X == 0 && mode.Y == 0;
                if (active)
                {
                    info.Current.Rotation = (int)route.Path.Target.Rotation - 1;
                    info.Current.RefreshNumerator = route.Path.Target.Numerator;
                    info.Current.RefreshDenominator = route.Path.Target.Denominator;
                }
                for (int k = 0; k < 2000; k++)
                {
                    var available = Native.Mode.Create();
                    if (!Native.EnumDisplaySettingsEx(route.Device, k, ref available, 0)) break;
                    if (available.BitsPerPixel != 32) continue;
                    var value = FromMode(info, available);
                    if (!info.Modes.Any(d => d.Width == value.Width && d.Height == value.Height && d.Hertz == value.Hertz && d.Rotation == value.Rotation)) info.Modes.Add(value);
                }
                if (!active && (mode.Width == 0 || mode.Height == 0 || mode.Frequency == 0))
                {
                    var preferred = info.Modes.Where(d => d.Width == info.PreferredWidth && d.Height == info.PreferredHeight).OrderBy(d => Math.Abs(d.Hertz - 60)).FirstOrDefault()
                        ?? info.Modes.OrderByDescending(d => (long)d.Width * d.Height).ThenBy(d => Math.Abs(d.Hertz - 60)).FirstOrDefault();
                    if (preferred != null) info.Current = preferred.Copy();
                    if (route.HasTargetMode && route.TargetMode.Signal.VSyncDenominator > 0)
                    {
                        var signal = route.TargetMode.Signal;
                        info.Current.Width = (int)signal.ActiveWidth; info.Current.Height = (int)signal.ActiveHeight;
                        info.Current.Hertz = (int)Math.Round((double)signal.VSyncNumerator / signal.VSyncDenominator);
                        info.Current.RefreshNumerator = signal.VSyncNumerator; info.Current.RefreshDenominator = signal.VSyncDenominator;
                    }
                    info.Current.Enabled = false; info.Current.Primary = false;
                }
                result.Add(info);
            }
            int right = result.Where(m => m.Current.Enabled).Select(m => m.Current.X + m.Current.Width).DefaultIfEmpty(0).Max();
            foreach (var m in result.Where(m => !m.Current.Enabled)) { m.Current.X = right; m.Current.Y = 0; right += m.Current.Width; }
            foreach (var group in result.GroupBy(m => m.Id).Where(g => g.Count() > 1))
                foreach (var m in group) m.Ambiguous = true;
            return result.OrderBy(m => m.Current.X).ThenBy(m => m.Current.Y).ToList();
        }
        private static DisplaySetting FromMode(MonitorInfo info, Native.Mode mode)
        {
            return new DisplaySetting { Id = info.Id, Label = info.Model, X = mode.X, Y = mode.Y, Width = (int)mode.Width, Height = (int)mode.Height, Hertz = (int)mode.Frequency, Rotation = (int)mode.Orientation };
        }
        private static MonitorInfo Identify(string path, string fallback)
        {
            var info = new MonitorInfo { InterfacePath = path, Model = fallback, Serial = "", IdentityKind = "Connection identity (keep the same port)", Id = "port:" + (path ?? "").ToUpperInvariant() };
            try
            {
                string[] parts = path.Split('#');
                if (parts.Length < 3) return info;
                using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\DISPLAY\" + parts[1] + @"\" + parts[2] + @"\Device Parameters"))
                {
                    var edid = key == null ? null : key.GetValue("EDID") as byte[];
                    if (edid == null || edid.Length < 128) return info;
                    int manufacturer = (edid[8] << 8) | edid[9];
                    string maker = new string(new[] { (char)(64 + ((manufacturer >> 10) & 31)), (char)(64 + ((manufacturer >> 5) & 31)), (char)(64 + (manufacturer & 31)) });
                    int product = edid[10] | edid[11] << 8;
                    uint number = BitConverter.ToUInt32(edid, 12);
                    if (edid[54] != 0 || edid[55] != 0)
                    {
                        info.PreferredWidth = edid[56] + ((edid[58] & 0xf0) << 4);
                        info.PreferredHeight = edid[59] + ((edid[61] & 0xf0) << 4);
                    }
                    string serial = "";
                    for (int o = 54; o <= 108; o += 18)
                    {
                        if (edid[o] != 0 || edid[o + 1] != 0 || edid[o + 2] != 0) continue;
                        string text = Encoding.ASCII.GetString(edid, o + 5, 13).Trim('\0', '\n', '\r', ' ');
                        if (edid[o + 3] == 0xfc && text.Length > 0) info.Model = text;
                        if (edid[o + 3] == 0xff) serial = text;
                    }
                    if (String.IsNullOrWhiteSpace(serial) || serial.All(c => c == '0') || serial == "123456789" || serial == "Unknown")
                        serial = number != 0 && number != UInt32.MaxValue ? number.ToString("X8") : "";
                    info.Serial = serial;
                    if (serial.Length > 0)
                    {
                        info.Id = "edid:" + maker + ":" + product.ToString("X4") + ":" + serial.ToUpperInvariant();
                        info.IdentityKind = "Hardware serial (independent of Windows display number)";
                    }
                }
            }
            catch (System.Security.SecurityException) { }
            catch (UnauthorizedAccessException) { }
            return info;
        }
        public static List<MonitorInfo> DemoMonitors()
        {
            return new List<MonitorInfo> {
                DemoMonitor("A", "Generic 24in", -1920, 1920, 1080, 60, true, false),
                DemoMonitor("B", "Generic 27in", 0, 2560, 1440, 144, true, true),
                DemoMonitor("C", "Generic TV", 2560, 3840, 2160, 60, false, false)
            };
        }
        // Names and presets that match DemoMonitors, so the screenshot looks like a set-up app.
        public static Preferences DemoPreferences()
        {
            var monitors = DemoMonitors(); var prefs = new Preferences();
            prefs.Labels[monitors[0].Id] = "Left"; prefs.Labels[monitors[1].Id] = "Center"; prefs.Labels[monitors[2].Id] = "TV";
            prefs.Presets.Add(new Preset { Name = "Desk", Displays = monitors.Select(m => m.Current.Copy()).ToList() });
            prefs.Presets.Add(new Preset { Name = "Movie night", Displays = monitors.Select(m => m.Current.Copy()).ToList() });
            return prefs;
        }
        private static MonitorInfo DemoMonitor(string letter, string model, int x, int width, int height, int hertz, bool on, bool primary)
        {
            var info = new MonitorInfo { Id = "edid:GEN:0000:DEMO-" + letter, Model = model, Serial = "DEMO-" + letter, Device = @"\\.\DISPLAY0", InterfacePath = "demo", IdentityKind = "Hardware serial (independent of Windows display number)" };
            info.Current = new DisplaySetting { Id = info.Id, Label = model, X = x, Y = 0, Width = width, Height = height, Hertz = hertz, Enabled = on, Primary = primary };
            foreach (var size in new[] { new[] { width, height, hertz }, new[] { width, height, 60 }, new[] { 1920, 1080, 60 }, new[] { 1280, 720, 60 } })
                if (!info.Modes.Any(d => d.Width == size[0] && d.Height == size[1] && d.Hertz == size[2]))
                    info.Modes.Add(new DisplaySetting { Id = info.Id, Width = size[0], Height = size[1], Hertz = size[2] });
            return info;
        }
        public static void Apply(List<DisplaySetting> requested)
        {
            if (Demo != null) throw new InvalidOperationException("Demo monitors cannot be applied.");
            var desired = LayoutRules.Normalize(requested);
            var live = Scan();
            LayoutRules.CheckTargets(desired, live);
            var plan = DisplayTopology.Build(desired, live, DisplayTopology.Read());
            DisplayTopology.Execute(plan, true);
            DisplayTopology.Execute(plan, false);
            Verify(desired, Scan());
            Store.Log("Applied layout with " + desired.Count(d => d.Enabled) + " enabled displays.");
        }
        public static void Validate(List<DisplaySetting> requested)
        {
            var desired = LayoutRules.Normalize(requested); var live = Scan(); LayoutRules.CheckTargets(desired, live);
            DisplayTopology.Execute(DisplayTopology.Build(desired, live, DisplayTopology.Read()), true);
        }
        private static void Verify(List<DisplaySetting> desired, List<MonitorInfo> actual)
        {
            foreach (var d in desired)
            {
                var found = actual.SingleOrDefault(m => m.Id == d.Id);
                if (!d.Enabled && (found == null || !found.Current.Enabled)) continue;
                if (found == null || found.Current.Enabled != d.Enabled) throw new InvalidOperationException("Windows did not apply the requested connection state for " + d.Label + ".");
                var a = found.Current;
                if (d.Enabled && (a.Primary != d.Primary || a.Width != d.Width || a.Height != d.Height || a.X != d.X || a.Y != d.Y || a.Rotation != d.Rotation || Math.Abs(a.Hertz - d.Hertz) > 1))
                    throw new InvalidOperationException("The driver changed the requested mode or position for " + d.Label + ". Restoring the previous layout.");
            }
        }
    }
}
