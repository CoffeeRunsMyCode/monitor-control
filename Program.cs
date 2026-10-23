using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace MonitorControl
{
    internal static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            try
            {
                if (args.Length == 2 && args[0] == "--diagnose")
                {
                    Store.Write(args[1], new { Monitors = DisplayService.Scan(), Routes = DisplayTopology.Read() }); return 0;
                }
                if (args.Length == 2 && args[0] == "--self-test")
                {
                    File.WriteAllText(args[1], SelfTests.Run()); return 0;
                }
                if (args.Length == 2 && args[0] == "--validate-layouts")
                {
                    var detected = DisplayService.Scan();
                    var report = new System.Text.StringBuilder();
                    File.WriteAllText(args[1], "Checking current layout...\r\n");
                    DisplayService.Validate(detected.Select(m => m.Current).ToList());
                    report.AppendLine("PASS Windows validated the current layout.");
                    foreach (var primary in detected)
                    {
                        var desired = detected.Select(m => m.Current.Copy()).ToList();
                        foreach (var d in desired) { d.Enabled = true; d.Primary = d.Id == primary.Id; }
                        File.WriteAllText(args[1], report.ToString() + "Checking all displays with " + primary.Model + " primary...\r\n");
                        DisplayService.Validate(LayoutRules.Row(desired));
                        report.AppendLine("PASS Windows validated all displays enabled with " + primary.Model + " primary.");
                    }
                    foreach (var primary in detected)
                    {
                        var desired = detected.Select(m => m.Current.Copy()).ToList();
                        foreach (var d in desired) { d.Enabled = d.Id == primary.Id; d.Primary = d.Enabled; }
                        File.WriteAllText(args[1], report.ToString() + "Checking only " + primary.Model + " enabled...\r\n");
                        DisplayService.Validate(LayoutRules.Normalize(desired));
                        report.AppendLine("PASS Windows validated only " + primary.Model + " enabled.");
                    }
                    report.AppendLine("Validation only: no display changes were applied.");
                    File.WriteAllText(args[1], report.ToString()); return 0;
                }
                if (args.Length == 2 && (args[0] == "--render" || args[0] == "--render-demo"))
                {
                    Store.Root = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[1])), "qa-profile");
                    // Demo mode shows made-up generic monitors, for screenshots that are safe to publish.
                    if (args[0] == "--render-demo") { DisplayService.Demo = DisplayService.DemoMonitors(); Store.Write(Store.ConfigPath, DisplayService.DemoPreferences()); }
                    Native.SetProcessDPIAware(); Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                    using (var form = new MainForm()) { form.RenderForTest(args[1]); }
                    using (var help = new HelpForm()) { help.RenderForTest(Path.ChangeExtension(args[1], ".help.png")); }
                    return 0;
                }
                if (args.Length == 2 && args[0] == "--make-icon")
                {
                    AppIcon.WriteIco(args[1]); return 0;
                }
                if (args.Length == 2 && args[0] == "--watch")
                {
                    RecoveryService.Watch(args[1]); return 0;
                }
                if (args.Length == 1 && args[0] == "--recover")
                {
                    RecoveryService.RecoverAtSignIn(); return 0;
                }
                Native.SetProcessDPIAware();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                bool created;
                using (var single = new Mutex(true, @"Local\MonitorControl.UserInterface", out created))
                {
                    if (!created) { MessageBox.Show("Monitor Control is already running. Open it from the system tray.", "Monitor Control"); return 0; }
                    Application.Run(new MainForm());
                }
                return 0;
            }
            catch (Exception ex)
            {
                Store.Log(ex.ToString());
                if (args.Length == 2 && (args[0] == "--self-test" || args[0] == "--diagnose" || args[0] == "--validate-layouts" || args[0] == "--render" || args[0] == "--render-demo")) File.WriteAllText(args[1] + ".error.txt", "ERROR: " + ex);
                else MessageBox.Show(ex.Message, "Monitor Control", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }
    }
}
