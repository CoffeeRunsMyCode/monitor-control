using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.IO;
using System.Threading;
using System.Drawing;

namespace MonitorControl
{
    internal static class SelfTests
    {
        public static string Run()
        {
            var log = new StringBuilder(); int passed = 0;
            Action<string, Action> test = delegate(string name, Action body) { body(); log.AppendLine("PASS " + name); passed++; };
            test("Win32 structure sizes match Unicode Windows ABI", delegate { Assert(Marshal.SizeOf(typeof(Native.Mode)) == 220); Assert(Marshal.SizeOf(typeof(Native.Device)) == 840); });
            test("CCD structures match the native Windows ABI", delegate {
                Assert(Marshal.SizeOf(typeof(DisplayTopology.PathInfo)) == 72); Assert(Marshal.SizeOf(typeof(DisplayTopology.ModeInfo)) == 64);
                Assert(Marshal.SizeOf(typeof(DisplayTopology.TargetName)) == 420); Assert(Marshal.SizeOf(typeof(DisplayTopology.SourceName)) == 84);
            });
            test("Built-in guides load, their links and images resolve, and web links use https", delegate {
                var guides = HelpDocs.Files.Select((f, i) => HelpDocs.Load(f, HelpDocs.Names[i])).ToList();
                foreach (var guide in guides)
                {
                    Assert(guide.Blocks.Count > 10);
                    foreach (var b in guide.Blocks.Where(b => b.Kind == 'i')) Assert(HelpDocs.Picture(b.Text) != null);
                    // The app opens https links only, so any other web link in a guide would do nothing when clicked.
                    foreach (string link in HelpDocs.Links(guide).Where(HelpDocs.IsWebLink))
                        if (!link.StartsWith("https://")) throw new InvalidOperationException("Help link must use https in " + guide.File + ": " + link);
                    foreach (string link in HelpDocs.Links(guide).Where(l => !HelpDocs.IsWebLink(l)))
                    {
                        string file = link.Split('#')[0], anchor = link.Contains("#") ? link.Split('#')[1] : "";
                        if (file == "../LICENSE" || file == "../README.md") continue; // opened on GitHub
                        var target = file.Length == 0 ? guide : guides.FirstOrDefault(g => g.File == file);
                        if (target == null || (anchor.Length > 0 && !HelpDocs.Anchors(target).Contains(anchor))) throw new InvalidOperationException("Broken help link in " + guide.File + ": " + link);
                    }
                }
            });
            test("Demo monitors form a valid layout with no real hardware", delegate {
                var demo = DisplayService.DemoMonitors(); LayoutRules.Normalize(demo.Select(m => m.Current));
                Assert(demo.All(m => m.Id.Contains("DEMO") && m.Model.StartsWith("Generic")));
            });
            test("Topology assigns distinct sources when routes compete", delegate {
                var desired = Sample(); var live = new List<MonitorInfo> { new MonitorInfo { Id = "a", InterfacePath = "panel-a" }, new MonitorInfo { Id = "b", InterfacePath = "panel-b" } };
                var routes = new List<DisplayTopology.Route> { Route("panel-a", 0, 10), Route("panel-a", 1, 10), Route("panel-b", 0, 20) };
                var plan = DisplayTopology.Build(desired, live, routes);
                Assert(plan.Paths[0].Source.Id == 1 && plan.Paths[1].Source.Id == 0);
                Assert(plan.Paths[0].Target.Id == 10 && plan.Paths[1].Target.Id == 20);
                Assert(plan.Modes[1].X == 1920);
            });
            test("Primary change translates all display coordinates", delegate {
                var items = Sample(); items[0].Primary = false; items[1].Primary = true;
                var result = LayoutRules.Normalize(items); Assert(result[1].X == 0); Assert(result[0].X == -1920); Assert(items[1].X == 1920);
            });
            test("Last enabled monitor cannot be disconnected", delegate { var items = Sample(); foreach (var d in items) d.Enabled = false; Reject(delegate { LayoutRules.Normalize(items); }); });
            test("Disabled primary and multiple primaries rejected", delegate {
                var items = Sample(); items[0].Enabled = false; Reject(delegate { LayoutRules.Normalize(items); });
                items = Sample(); items[1].Primary = true; Reject(delegate { LayoutRules.Normalize(items); });
            });
            test("Overlaps and disconnected islands rejected", delegate {
                var items = Sample(); items[1].X = 100; Reject(delegate { LayoutRules.Normalize(items); });
                items[1].X = 4000; Reject(delegate { LayoutRules.Normalize(items); });
            });
            test("Arrange row closes gap after widescreen is disabled", delegate {
                var items = Sample(); items.Add(new DisplaySetting { Id = "c", Label = "Third", Enabled = true, X = 3840, Width = 1280, Height = 720, Hertz = 60 });
                items[1].Enabled = false; var result = LayoutRules.Row(items); Assert(result[2].X == 1920); Assert(!result[1].Enabled);
            });
            test("Hardware identity survives changed Windows numbering", delegate {
                var desired = Sample(); var live = desired.Select(d => new MonitorInfo { Id = d.Id, Device = d.Id == "a" ? "DISPLAY7" : "DISPLAY2", Current = d.Copy() }).ToList();
                LayoutRules.CheckTargets(desired, live);
            });
            test("Unavailable enabled monitor rejected", delegate { var desired = Sample(); Reject(delegate { LayoutRules.CheckTargets(desired, new List<MonitorInfo>()); }); });
            test("Unavailable disabled monitor allowed", delegate {
                var desired = Sample(); desired[1].Enabled = false;
                LayoutRules.CheckTargets(desired, new List<MonitorInfo> { new MonitorInfo { Id = "a", Current = desired[0].Copy() } });
            });
            test("Ambiguous serials and unknown active monitors rejected", delegate {
                var desired = Sample(); var live = desired.Select(d => new MonitorInfo { Id = d.Id, Current = d.Copy() }).ToList(); live[0].Ambiguous = true;
                Reject(delegate { LayoutRules.CheckTargets(desired, live); }); live[0].Ambiguous = false;
                live.Add(new MonitorInfo { Id = "unexpected", Current = new DisplaySetting { Enabled = true } }); Reject(delegate { LayoutRules.CheckTargets(desired, live); });
            });
            test("Duplicate saved identities and invalid dimensions rejected", delegate {
                var desired = Sample(); desired[1].Id = "a"; Reject(delegate { LayoutRules.Normalize(desired); });
                desired = Sample(); desired[0].Width = -1; Reject(delegate { LayoutRules.Normalize(desired); });
            });
            test("Vertical layouts are supported", delegate { var desired = Sample(); desired[1].X = 0; desired[1].Y = -1080; LayoutRules.Normalize(desired); });
            test("Drag snaps to exact edges and top alignment", delegate {
                var displays = Sample(); Point result;
                Assert(LayoutPlacement.TrySnap(displays, "b", new Point(1932, 14), 30, false, out result));
                Assert(result.X == 1920 && result.Y == 0);
                Assert(displays[1].X == 1920 && displays[1].Y == 0);
            });
            test("Dragging left or above supports negative coordinates", delegate {
                var displays = Sample(); Point result;
                Assert(LayoutPlacement.TrySnap(displays, "b", new Point(-1930, 10), 30, false, out result)); Assert(result.X == -1920 && result.Y == 0);
                Assert(LayoutPlacement.TrySnap(displays, "b", new Point(8, -1090), 30, false, out result)); Assert(result.X == 0 && result.Y == -1080);
            });
            test("Free drag uses threshold; release docks to a valid edge", delegate {
                var displays = Sample(); Point result;
                Assert(!LayoutPlacement.TrySnap(displays, "b", new Point(8000, 300), 30, false, out result));
                Assert(result == new Point(8000, 300));
                Assert(LayoutPlacement.TrySnap(displays, "b", new Point(8000, 300), 30, true, out result));
                displays[1].X = result.X; displays[1].Y = result.Y; LayoutRules.Normalize(displays);
            });
            test("Dropping over another display resolves overlap", delegate {
                var displays = Sample(); Point result;
                Assert(LayoutPlacement.TrySnap(displays, "b", new Point(400, 100), 30, true, out result));
                displays[1].X = result.X; displays[1].Y = result.Y; LayoutRules.Normalize(displays);
            });
            test("Moving a bridge monitor keeps the entire group connected", delegate {
                var displays = Sample(); displays.Add(new DisplaySetting { Id = "c", Enabled = true, Width = 1920, Height = 1080, X = 3840, Hertz = 60 }); Point result;
                Assert(LayoutPlacement.TrySnap(displays, "b", new Point(-1920, 0), 30, true, out result));
                displays[1].X = result.X; displays[1].Y = result.Y; LayoutRules.Normalize(displays);
            });
            test("Different sizes align bottom edges and ignore disabled displays", delegate {
                var displays = Sample(); displays[1].Width = 1280; displays[1].Height = 720;
                displays.Add(new DisplaySetting { Id = "off", X = -1280, Width = 1280, Height = 720, Enabled = false }); Point result;
                Assert(LayoutPlacement.TrySnap(displays, "b", new Point(-1290, 350), 25, false, out result));
                Assert(result.X == -1280 && result.Y == 360);
            });
            test("Preset storage round-trips identities and creates a backup", delegate {
                string dir = Path.Combine(Path.GetTempPath(), "MonitorControl-test-" + Guid.NewGuid().ToString("N"));
                string path = Path.Combine(dir, "settings.json");
                try
                {
                    var p = new Preferences(); p.Presets.Add(new Preset { Name = "Work laptop", Shortcut = 1, Displays = Sample() });
                    Store.Write(path, p); Store.Write(path, p);
                    var read = Store.Read<Preferences>(path);
                    Assert(read.Presets[0].Displays[1].Id == "b" && read.Presets[0].Shortcut == 1 && File.Exists(path + ".bak"));
                }
                finally { File.Delete(path); File.Delete(path + ".bak"); if (Directory.Exists(dir)) Directory.Delete(dir); }
            });
            test("Independent recovery process starts and stops after Keep without applying", delegate {
                string originalRoot = Store.Root;
                string dir = Path.Combine(Path.GetTempPath(), "MonitorControl-watch-test-" + Guid.NewGuid().ToString("N"));
                string path = null; string originalName = RecoveryService.RunOnceName;
                try
                {
                    Store.Root = dir; RecoveryService.RunOnceName = "MonitorControl.SelfTest-" + Guid.NewGuid().ToString("N");
                    // Empty snapshot guarantees an accidental recovery attempt cannot change any monitor.
                    path = RecoveryService.Arm(new Recovery { DeadlineUtc = DateTime.UtcNow.AddSeconds(20), Displays = new List<DisplaySetting>() });
                    Assert(File.Exists(path + ".ready")); Assert(RecoveryService.SignInRestoreScheduled());
                    Assert(RecoveryService.Keep(path)); Assert(!RecoveryService.Keep(path)); Assert(!RecoveryService.SignInRestoreScheduled());
                    var watch = System.Diagnostics.Stopwatch.StartNew(); while (File.Exists(path + ".ready") && watch.ElapsedMilliseconds < 5000) Thread.Sleep(50);
                    Assert(!File.Exists(path + ".ready") && !File.Exists(path + ".restoring") && !File.Exists(path + ".failed"));
                }
                finally { RecoveryService.CancelSignInRestore(); RecoveryService.RunOnceName = originalName; Store.Root = originalRoot; if (path != null) File.Delete(path); if (Directory.Exists(dir) && Directory.GetFileSystemEntries(dir).Length == 0) Directory.Delete(dir); }
            });
            test("Restore refused by Windows stays pending, then succeeds at sign-in", delegate {
                string originalRoot = Store.Root, originalName = RecoveryService.RunOnceName; var originalApply = RecoveryService.ApplyDisplays;
                string dir = Path.Combine(Path.GetTempPath(), "MonitorControl-signin-test-" + Guid.NewGuid().ToString("N"));
                try
                {
                    Store.Root = dir; RecoveryService.RunOnceName = "MonitorControl.SelfTest-" + Guid.NewGuid().ToString("N");
                    string path = Path.Combine(dir, "pending-test.json");
                    Store.Write(path, new Recovery { DeadlineUtc = DateTime.UtcNow, Displays = new List<DisplaySetting>() });
                    // Simulate the shutdown: Windows answers Access denied (5).
                    RecoveryService.ApplyDisplays = delegate { throw new DesktopUnavailableException("Access is denied (5)."); };
                    try { RecoveryService.Restore(path); throw new Exception("Expected the restore to be postponed."); } catch (DesktopUnavailableException) { }
                    Assert(File.Exists(path) && !File.Exists(path + ".restoring") && !File.Exists(path + ".failed"));
                    // Next sign-in: Windows accepts the change.
                    int applied = 0; RecoveryService.ApplyDisplays = delegate { applied++; };
                    RecoveryService.RecoverAtSignIn();
                    Assert(applied == 1 && !File.Exists(path) && !File.Exists(path + ".restoring") && !File.Exists(path + ".failed"));
                }
                finally
                {
                    RecoveryService.ApplyDisplays = originalApply; RecoveryService.CancelSignInRestore(); RecoveryService.RunOnceName = originalName; Store.Root = originalRoot;
                    if (Directory.Exists(dir)) Directory.Delete(dir, true);
                }
            });
            log.AppendLine(passed + " tests passed. No display changes were performed."); return log.ToString();
        }
        private static List<DisplaySetting> Sample()
        {
            return new List<DisplaySetting> {
                new DisplaySetting { Id = "a", Label = "Left", Enabled = true, Primary = true, Width = 1920, Height = 1080, Hertz = 60 },
                new DisplaySetting { Id = "b", Label = "Dell", Enabled = true, X = 1920, Width = 1920, Height = 1080, Hertz = 60 }
            };
        }
        private static void Assert(bool value) { if (!value) throw new Exception("Assertion failed."); }
        private static DisplayTopology.Route Route(string path, uint source, uint target)
        {
            return new DisplayTopology.Route { InterfacePath = path, SourceId = source, TargetId = target, Available = true, Path = new DisplayTopology.PathInfo { Source = new DisplayTopology.Source { Id = source }, Target = new DisplayTopology.Target { Id = target } } };
        }
        private static void Reject(Action action) { try { action(); } catch (InvalidOperationException) { return; } throw new Exception("Expected unsafe layout to be rejected."); }
    }
}
