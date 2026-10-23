using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Win32;

namespace MonitorControl
{
    internal static class RecoveryService
    {
        private const string RunOnceKey = @"Software\Microsoft\Windows\CurrentVersion\RunOnce";
        // Self-tests use their own names so they never touch the real entry or the real displays.
        internal static string RunOnceName = "MonitorControl.Recovery";
        internal static Action<List<DisplaySetting>> ApplyDisplays = DisplayService.Apply;

        public static string Arm(Recovery recovery)
        {
            Directory.CreateDirectory(Store.Root);
            string path = Path.Combine(Store.Root, "pending-" + Guid.NewGuid().ToString("N") + ".json");
            Store.Write(path, recovery);
            try
            {
                RestoreAtSignIn();
                var process = Process.Start(new ProcessStartInfo {
                    FileName = System.Windows.Forms.Application.ExecutablePath,
                    Arguments = "--watch \"" + path + "\"", UseShellExecute = false, CreateNoWindow = true
                });
                var watch = Stopwatch.StartNew();
                while (!File.Exists(path + ".ready") && watch.ElapsedMilliseconds < 5000 && !process.HasExited) Thread.Sleep(50);
                if (!File.Exists(path + ".ready")) throw new InvalidOperationException("The independent recovery process did not start. No display changes were made.");
                process.Dispose();
                return path;
            }
            catch { File.Delete(path); CancelSignInRestore(); throw; }
        }
        public static bool Keep(string path)
        {
            try { File.Move(path, path + ".kept"); File.Delete(path + ".kept"); CancelSignInRestore(); return true; }
            catch (FileNotFoundException) { return false; }
            catch (IOException) { return false; }
        }
        public static void Restore(string path)
        {
            string claimed = path + ".restoring";
            try { File.Move(path, claimed); }
            catch (FileNotFoundException) { return; }
            catch (IOException) { if (!File.Exists(path)) return; throw; }
            try
            {
                using (var mutex = new Mutex(false, @"Local\MonitorControl.DisplayChange"))
                {
                    Acquire(mutex);
                    try { ApplyDisplays(Store.Read<Recovery>(claimed).Displays); }
                    finally { mutex.ReleaseMutex(); }
                }
                File.Delete(claimed);
                CancelSignInRestore();
                Store.Log("Previous display layout restored.");
            }
            catch (DesktopUnavailableException)
            {
                // Windows refused before changing anything. Hand the change back so it can be retried.
                File.Move(claimed, path);
                throw;
            }
            catch (Exception ex)
            {
                File.Move(claimed, path + ".failed");
                CancelSignInRestore();
                Store.Log("RESTORE FAILED: " + ex.Message);
                throw new InvalidOperationException("Automatic recovery could not fully restore the previous layout. " + ex.Message + " Recovery data: " + path + ".failed", ex);
            }
        }
        public static void Watch(string path)
        {
            try
            {
                var deadline = Store.Read<Recovery>(path).DeadlineUtc;
                File.WriteAllText(path + ".ready", "ready");
                RestoreWhenDue(path, deadline);
            }
            finally { File.Delete(path + ".ready"); }
        }
        // Started by Windows at the next sign-in when a change was still pending at shutdown or sign-out.
        public static void RecoverAtSignIn()
        {
            if (!Directory.Exists(Store.Root)) return;
            foreach (string path in Directory.GetFiles(Store.Root, "pending-*.json").Where(p => p.EndsWith(".json")))
            {
                Store.Log("Restoring the layout from before Windows restarted or signed out.");
                RestoreWhenDue(path, DateTime.MinValue);
            }
        }
        // Restores once the deadline passes, unless the change is kept or restored elsewhere first.
        // Windows refuses display changes while the desktop is locked, switching users, signing out or shutting down;
        // keep retrying until it accepts. A shutdown ends this process, and the sign-in entry takes over.
        private static void RestoreWhenDue(string path, DateTime deadlineUtc)
        {
            bool postponed = false; var claimedSince = DateTime.MaxValue;
            while (true)
            {
                if (!File.Exists(path))
                {
                    // ".restoring" means another process is restoring right now and may hand the change back.
                    if (!File.Exists(path + ".restoring")) return;
                    if (claimedSince == DateTime.MaxValue) claimedSince = DateTime.UtcNow;
                    else if (DateTime.UtcNow - claimedSince > TimeSpan.FromMinutes(1)) return;
                }
                else if (DateTime.UtcNow >= deadlineUtc)
                {
                    claimedSince = DateTime.MaxValue;
                    try { Restore(path); return; }
                    catch (DesktopUnavailableException)
                    {
                        if (!postponed) Store.Log("Restore postponed: Windows is not accepting display changes right now. Retrying.");
                        postponed = true; Thread.Sleep(2000); continue;
                    }
                }
                Thread.Sleep(200);
            }
        }
        private static void RestoreAtSignIn()
        {
            // Windows deletes a RunOnce entry before running it, so it fires at most once.
            try { using (var key = Registry.CurrentUser.CreateSubKey(RunOnceKey)) key.SetValue(RunOnceName, "\"" + System.Windows.Forms.Application.ExecutablePath + "\" --recover"); }
            catch (Exception ex) { Store.Log("Could not schedule recovery after a restart: " + ex.Message); }
        }
        internal static void CancelSignInRestore()
        {
            try { using (var key = Registry.CurrentUser.OpenSubKey(RunOnceKey, true)) if (key != null) key.DeleteValue(RunOnceName, false); }
            catch { }
        }
        internal static bool SignInRestoreScheduled()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunOnceKey)) return key != null && key.GetValue(RunOnceName) != null;
        }
        public static void Acquire(Mutex mutex)
        {
            try { if (!mutex.WaitOne(TimeSpan.FromSeconds(15))) throw new InvalidOperationException("Another display change is still running."); }
            catch (AbandonedMutexException) { /* The crashed owner released the lock to us. */ }
        }
    }
}
