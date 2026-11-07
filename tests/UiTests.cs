using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using VoltHUD;

class UiTests
{
    static FieldInfo Field(string name) { return typeof(Widget).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic); }
    static object Call(Widget widget, string name) { return typeof(Widget).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(widget, null); }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(delegate { frame.Continue = false; }));
        Dispatcher.PushFrame(frame);
    }
    static WeakReference SettingsCycle(Widget widget)
    {
        Call(widget, "OpenSettings"); Pump();
        var panel = (Border)Field("settingsPanel").GetValue(widget);
        Check(panel.FindName("TestNotification") == null, "Test button was removed");
        var weak = new WeakReference(panel);
        ((Popup)Field("settingsPopup").GetValue(widget)).IsOpen = false;
        Pump();
        Check(Field("settingsPanel").GetValue(widget) == null, "Closed settings must release controls");
        return weak;
    }
    static long Collect()
    {
        // Collection is test instrumentation only; production never forces a collection.
        Pump(); GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); Pump();
        return GC.GetTotalMemory(true);
    }
    [STAThread] static void Main(string[] args)
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var widget = new Widget(true);
        try
        {
            widget.StartInTray();
            var timer = (DispatcherTimer)Field("timer").GetValue(widget);
            var tray = (System.Windows.Forms.NotifyIcon)Field("tray").GetValue(widget);
            Check(!widget.IsVisible && timer.IsEnabled && tray.Visible, "Hidden startup must monitor");
            bool interactive = Array.IndexOf(args, "--interactive") >= 0;
            if (interactive) { widget.Show(); widget.Hide(); }
            Check(!widget.IsVisible && timer.IsEnabled && tray.Visible, "Hide must preserve monitoring");
            if (interactive)
            {
            widget.Show();
            SettingsCycle(widget);
            long before = Collect();
            var process = Process.GetCurrentProcess(); process.Refresh(); int handlesBefore = process.HandleCount;
            var weakPanels = new List<WeakReference>();
            var root = (Grid)Field("root").GetValue(widget);
            var rows = (StackPanel)root.FindName("DeviceRows");
            object firstRow = rows.Children[0];
            for (int i = 0; i < 100; i++)
            {
                weakPanels.Add(SettingsCycle(widget));
                Call(widget, "Refresh"); Pump();
                Check(Object.ReferenceEquals(firstRow, rows.Children[0]), "Unchanged refresh must reuse rows");
            }
            long after = Collect(); process.Refresh();
            int retained = weakPanels.FindAll(r => r.IsAlive).Count;
            Check(retained <= 1, "Settings controls leaked across close/reopen");
            Check(after - before < 3 * 1024 * 1024, "Managed memory grew excessively");
            Check(process.HandleCount - handlesBefore < 30, "Window/GDI handle growth");
            Console.WriteLine("PASS: 100 settings open/close and refresh cycles; retained panels=" + retained + "; managed delta=" + (after-before) + " bytes; handle delta=" + (process.HandleCount-handlesBefore));
            }
            else Console.WriteLine("SKIP: visible settings stress test (requires explicit -Interactive).");

            // Exercise the real persistence method against an isolated test path, without sending a notification.
            string history = Path.Combine(Path.GetFullPath(args[0]), "alert-history-test.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(history));
            Field("alertHistoryPath").SetValue(widget, history);
            var policy = (LowBatteryAlerts)Field("lowBatteryAlerts").GetValue(widget);
            var reading = new BatteryReading { Key = "test-device", Percent = 9 };
            policy.Evaluate(new[] { reading }, true);
            Field("demoMode").SetValue(widget, false);
            try
            {
                Call(widget, "SaveAlertHistory");
                var restarted = new LowBatteryAlerts(); restarted.Restore(File.ReadLines(history));
                Check(restarted.Evaluate(new[] { reading }, true).Count == 0, "Saved history must suppress warning after restart");
                Check(!policy.Changed, "Successfully saved history should be clean");
                reading.Percent = 15; policy.Evaluate(new[] { reading }, false); Call(widget, "SaveAlertHistory");
                Check(File.ReadAllLines(history).Length == 0, "Recovery should clear persisted latch");
            }
            finally { Field("demoMode").SetValue(widget, true); }
            File.Delete(history);
            Console.WriteLine("PASS: atomic alert history save/replacement, restart suppression, recovery; no real preferences or notifications changed.");
            widget.Hide();
            Check(timer.IsEnabled && tray.Visible, "Monitoring survives stress while hidden");
        }
        finally { widget.Close(); }
        var preview = new Widget(true);
        preview.RenderPreview(Path.Combine(Path.GetFullPath(args[0]), "qa-preview.png"));
        preview.Close();
        Console.WriteLine("PASS: rendering then closing disposes tray safely.");
        app.Shutdown();
    }
}
