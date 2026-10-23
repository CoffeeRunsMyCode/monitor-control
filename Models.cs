using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace MonitorControl
{
    public class DisplaySetting
    {
        public string Id, Label;
        public bool Enabled, Primary;
        public int X, Y, Width, Height, Hertz, Rotation;
        public uint RefreshNumerator, RefreshDenominator;
        public DisplaySetting Copy() { return (DisplaySetting)MemberwiseClone(); }
    }
    public class Preset
    {
        public string Name;
        public int Shortcut;
        public List<DisplaySetting> Displays = new List<DisplaySetting>();
        public override string ToString() { return Name + (Shortcut > 0 ? "    Ctrl + Alt + " + Shortcut : ""); }
    }
    public class Preferences
    {
        public Dictionary<string, string> Labels = new Dictionary<string, string>();
        public List<Preset> Presets = new List<Preset>();
        // Off by default, so X exits unless the user opts in. Older settings files without these fields load as false.
        public bool CloseToTray;
        public bool TrayNoticeShown;
    }
    public class Recovery
    {
        public DateTime DeadlineUtc;
        public List<DisplaySetting> Displays;
    }
    public class MonitorInfo
    {
        public string Id, Device, Model, Serial, InterfacePath, IdentityKind;
        public DisplaySetting Current;
        public List<DisplaySetting> Modes = new List<DisplaySetting>();
        public bool Ambiguous;
        public int PreferredWidth, PreferredHeight;
    }
    internal static class Store
    {
        public static string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MonitorControl");
        public static string ConfigPath { get { return Path.Combine(Root, "settings.json"); } }
        public static T Read<T>(string path) { return new JavaScriptSerializer().Deserialize<T>(File.ReadAllText(path)); }
        public static void Write<T>(string path, T value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temp, new JavaScriptSerializer().Serialize(value), new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temp, path, path + ".bak"); else File.Move(temp, path);
        }
        public static Preferences Load()
        {
            if (!File.Exists(ConfigPath)) return new Preferences();
            var result = Read<Preferences>(ConfigPath);
            if (result == null || result.Labels == null || result.Presets == null || result.Presets.Any(p => p == null || p.Displays == null))
                throw new InvalidDataException("Settings are invalid. Your file has been preserved: " + ConfigPath);
            return result;
        }
        public static void Log(string message)
        {
            try { Directory.CreateDirectory(Root); File.AppendAllText(Path.Combine(Root, "activity.log"), DateTime.Now.ToString("s") + " " + message + Environment.NewLine); } catch { }
        }
    }
    internal static class LayoutRules
    {
        public static List<DisplaySetting> Normalize(IEnumerable<DisplaySetting> source)
        {
            var items = source.Select(d => d.Copy()).ToList();
            if (items.Count == 0) throw new InvalidOperationException("No monitors were found.");
            if (items.Any(d => String.IsNullOrWhiteSpace(d.Id)) || items.GroupBy(d => d.Id).Any(g => g.Count() > 1))
                throw new InvalidOperationException("Monitor identities are missing or ambiguous. Refresh and inspect the hardware details.");
            var enabled = items.Where(d => d.Enabled).ToList();
            if (enabled.Count == 0) throw new InvalidOperationException("Keep at least one monitor enabled.");
            if (items.Any(d => d.Primary && !d.Enabled) || enabled.Count(d => d.Primary) != 1)
                throw new InvalidOperationException("Choose exactly one enabled monitor as primary.");
            foreach (var d in enabled)
                if (d.Width < 320 || d.Height < 200 || d.Width > 32768 || d.Height > 32768 || d.Hertz < 1 || d.Hertz > 1000 || d.Rotation < 0 || d.Rotation > 3 || Math.Abs((long)d.X) > 100000 || Math.Abs((long)d.Y) > 100000)
                    throw new InvalidOperationException("Invalid display size, refresh rate, rotation, or position for " + d.Label + ".");
            var primary = enabled.Single(d => d.Primary);
            int ox = primary.X, oy = primary.Y;
            foreach (var d in items) { d.X -= ox; d.Y -= oy; }
            for (int i = 0; i < enabled.Count; i++)
                for (int j = i + 1; j < enabled.Count; j++)
                {
                    var a = enabled[i]; var b = enabled[j];
                    if (Math.Max(a.X, b.X) < Math.Min(a.X + a.Width, b.X + b.Width) && Math.Max(a.Y, b.Y) < Math.Min(a.Y + a.Height, b.Y + b.Height))
                        throw new InvalidOperationException("Displays overlap. Use Arrange in a row or edit their positions. Mirroring is not supported in this version.");
                }
            var connected = new HashSet<string> { primary.Id };
            bool added;
            do
            {
                added = false;
                foreach (var b in enabled.Where(d => !connected.Contains(d.Id)))
                    if (enabled.Any(a => connected.Contains(a.Id) && Touch(a, b))) { connected.Add(b.Id); added = true; }
            } while (added);
            if (connected.Count != enabled.Count) throw new InvalidOperationException("Enabled displays must touch along an edge. Use Arrange in a row to close gaps.");
            return items;
        }
        private static bool Touch(DisplaySetting a, DisplaySetting b)
        {
            return ((a.X + a.Width == b.X || b.X + b.Width == a.X) && Math.Max(a.Y, b.Y) < Math.Min(a.Y + a.Height, b.Y + b.Height)) ||
                   ((a.Y + a.Height == b.Y || b.Y + b.Height == a.Y) && Math.Max(a.X, b.X) < Math.Min(a.X + a.Width, b.X + b.Width));
        }
        public static List<DisplaySetting> Row(IEnumerable<DisplaySetting> source)
        {
            var result = source.Select(d => d.Copy()).ToList();
            int x = 0;
            foreach (var d in result.Where(d => d.Enabled).OrderBy(d => d.X)) { d.X = x; d.Y = 0; x += d.Width; }
            return Normalize(result);
        }
        public static void CheckTargets(List<DisplaySetting> desired, List<MonitorInfo> live)
        {
            foreach (var d in desired)
            {
                var matches = live.Where(m => m.Id == d.Id).ToList();
                if (matches.Count > 1 || matches.Any(m => m.Ambiguous)) throw new InvalidOperationException("Ambiguous hardware identity for " + d.Label + ". No changes were made.");
                if (d.Enabled && matches.Count == 0) throw new InvalidOperationException(d.Label + " is not available to this PC. Check its cable/input and refresh. No changes were made.");
            }
            if (live.Any(m => m.Current.Enabled && !desired.Any(d => d.Id == m.Id)))
                throw new InvalidOperationException("An additional active monitor is missing from this preset. Refresh and save an updated layout first.");
        }
    }
}
