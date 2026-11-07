using System;
using VoltHUD;
class BatteryTests
{
    static int checks;
    static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    static byte[] Packet(byte a, byte b, byte c) { return new byte[] {0x11,1,4,0x1B,a,b,c}; }
    static void Main()
    {
        Check(BatteryMonitor.Decode(0x1004, Packet(77,4,0)).Percent == 77, "Unified percentage");
        Check(BatteryMonitor.Decode(0x1004, Packet(0,0,0)).Percent == 0, "A real empty battery is zero");
        Check(BatteryMonitor.Decode(0x1004, Packet(0,4,0)).Percent == null, "Coarse level is not a made-up percentage");
        Check(BatteryMonitor.Decode(0x1004, Packet(0,4,0)).State.Contains("Good"), "Coarse level label");
        Check(BatteryMonitor.Decode(0x1000, Packet(0,0,0)).Percent == null, "Legacy feature zero means unknown");
        Check(BatteryMonitor.Decode(0x1004, Packet(80,4,1)).Charging, "Charging");
        Check(BatteryMonitor.Decode(0x1004, Packet(80,4,4)).Charging, "Slow charging");
        Check(BatteryMonitor.Decode(0x1004, Packet(100,8,3)).State == "Fully charged", "Charge complete");
        Check(BatteryMonitor.Decode(0x1004, Packet(255,0,0)) == null, "Reject impossible percentage");
        Check(BatteryMonitor.Decode(0x1004, new byte[3]) == null, "Reject truncated HID++ report");
        Check(BatteryMonitor.Decode(0x1004, null) == null, "Reject absent report");
        Check(BatteryMonitor.Decode(42, Packet(50,0,0)) == null, "Reject unknown feature");
        Check(BatteryMonitor.Decode(0x1001, Packet(0x0E,0xE2,0)).Percent == 50, "3810mV estimate");
        Check(BatteryMonitor.Decode(0x1001, Packet(0x0E,0xE2,0)).State.Contains("estimated"), "Estimate disclosed");
        Check(BatteryMonitor.Decode(0x1001, Packet(0,0,0)) == null, "Reject zero voltage");
        Check(BatteryMonitor.Decode(0x1F20, Packet(0x0F,0xA0,0)) == null, "Disconnected ADC headset");
        Check(BatteryMonitor.Decode(0x1F20, Packet(0x0F,0xA0,3)).Charging, "ADC headset charging");
        Check(BatteryMonitor.DecodeRegister(0x0D, Packet(64,0,0x50)).Charging, "Old percentage register charging");
        Check(BatteryMonitor.DecodeRegister(7, Packet(5,0,0)).Percent == null, "Old discrete battery remains coarse");
        Check(BatteryMonitor.DecodeRegister(7, Packet(2,0,0)) == null, "Reject invalid old level");
        Check(CenturionBattery.Decode(new byte[] {71,0,1}).Percent == 71, "Centurion battery");
        Check(CenturionBattery.Decode(new byte[] {71,0,2}).Charging, "Centurion USB charging");
        Check(CenturionBattery.Decode(new byte[] {255,0,0}) == null, "Reject invalid Centurion level");
        Check(CenturionBattery.Decode(new byte[] {71}) == null, "Reject truncated Centurion reply");
        Check(BatteryMonitor.DeviceName("PRO X 2") == "Logitech PRO X 2", "Brand prefix");
        Check(BatteryMonitor.DeviceName("Logitech MX Master") == "Logitech MX Master", "No duplicate brand");
        Check(DeviceKind.Resolve("Xbox Wireless Controller", "device") == "controller", "Controller icon");
        Check(DeviceKind.Resolve("AirPods Pro", "device") == "headset", "Bluetooth headphone icon");
        Check(DeviceKind.Resolve("PRO X 2", "mouse") == "mouse", "Hardware type takes precedence");
        Check(DeviceKind.Resolve("Unknown peripheral", "device") == "device", "Unknown device uses generic battery icon");
        int previous = 0;
        Check(BatteryMonitor.DecodeDeviceName(System.Text.Encoding.UTF8.GetBytes("PRO X 2")) == "PRO X 2", "Valid identity name");
        Check(BatteryMonitor.DecodeDeviceName(new byte[] { 77, 8 }) == null, "Reject control characters in corrupted name");
        Check(BatteryMonitor.DecodeDeviceName(new byte[] { 0xC3 }) == null, "Reject truncated UTF-8 name");
        Check(BatteryMonitor.DecodeDeviceName(new byte[] { 0, 0 }) == null, "Reject empty name");
        var warnings = new LowBatteryAlerts();
        var mouse = new BatteryReading { Key = "mouse", Name = "Mouse", Percent = 10 };
        var devices = new[] { mouse };
        Check(warnings.Evaluate(devices, true).Count == 0, "10% is not below 10%");
        mouse.Percent = 9;
        Check(warnings.Evaluate(devices, false).Count == 0, "Notifications are optional");
        Check(warnings.Evaluate(devices, true).Count == 1, "First low battery warning");
        mouse.Percent = 5;
        Check(warnings.Evaluate(devices, true).Count == 0, "No repeated warning");
        mouse.Percent = 10; warnings.Evaluate(devices, true); mouse.Percent = 9;
        Check(warnings.Evaluate(devices, true).Count == 0, "Threshold fluctuation does not spam");
        mouse.Percent = 15; warnings.Evaluate(devices, true); mouse.Percent = 9;
        Check(warnings.Evaluate(devices, true).Count == 1, "Recovery rearms warning");
        warnings.Evaluate(devices, false); mouse.Charging = true;
        mouse.Charging = false;
        Check(warnings.Evaluate(devices, true).Count == 0, "Changing preference must not repeat alert");
        var restored = new LowBatteryAlerts(); restored.Restore(warnings.Export());
        Check(restored.Evaluate(devices, true).Count == 0, "Restart must not repeat alert");
        Check(!restored.Changed, "Unchanged history must not rewrite disk");
        mouse.Percent = 15; restored.Evaluate(devices, false); mouse.Percent = 9;
        Check(restored.Evaluate(devices, true).Count == 1, "Recovery while disabled rearms notification");
        mouse.Key = "MOUSE";
        Check(restored.Evaluate(devices, true).Count == 0, "Identity casing must not repeat alert");
        mouse.Key = "mouse";
        warnings = new LowBatteryAlerts(); mouse.Charging = true;
        Check(warnings.Evaluate(devices, true).Count == 0, "Charging devices do not warn");
        mouse.Charging = false; mouse.Percent = null;
        Check(warnings.Evaluate(devices, true).Count == 0, "Unknown battery does not warn");
        mouse.Percent = 0; mouse.NeedsIdentity = true;
        Check(warnings.Evaluate(devices, true).Count == 0, "Wait for device identity");
        mouse.NeedsIdentity = false;
        Check(warnings.Evaluate(new[] { mouse, new BatteryReading { Key = "headset", Percent = 8 } }, true).Count == 2, "Multiple devices and empty batteries warn");
        Check(warnings.Evaluate(new BatteryReading[0], true).Count == 0, "Disconnect does not reset warning");
        Check(warnings.Evaluate(devices, true).Count == 0, "Reconnect does not spam");
        mouse.Percent = 255;
        Check(warnings.Evaluate(devices, true).Count == 0, "Invalid high values cannot rearm");
        mouse.Percent = 0;
        Check(warnings.Evaluate(devices, true).Count == 0, "Invalid values preserve alert history");
        var bounded = new LowBatteryAlerts();
        bounded.Restore(new[] { "bad", "", new string('Z', 64) });
        Check(bounded.Export().Length == 0, "Malformed alert history is ignored");
        for (int i = 0; i < 300; i++) bounded.Evaluate(new[] { new BatteryReading { Key = "device-" + i, Percent = 5 } }, true);
        Check(bounded.Export().Length == 256, "Alert memory and saved history are bounded");
        Check(Array.TrueForAll(bounded.Export(), key => key.Length == 64 && !key.Contains("device")), "Persist hashes instead of device identifiers");
        bounded.MarkSaved(); Check(!bounded.Changed, "Saved history is clean");
        for (int mv = 2500; mv <= 5000; mv++) { int p = BatteryMonitor.VoltagePercent(mv); Check(p >= previous && p >= 0 && p <= 100, "Monotonic bounded voltage estimate"); previous = p; }
        Console.WriteLine("PASS: " + checks + " checks (battery formats, malformed replies, charging, voltage bounds).");
    }
}
