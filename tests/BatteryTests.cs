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
        for (int mv = 2500; mv <= 5000; mv++) { int p = BatteryMonitor.VoltagePercent(mv); Check(p >= previous && p >= 0 && p <= 100, "Monotonic bounded voltage estimate"); previous = p; }
        Console.WriteLine("PASS: " + checks + " checks (battery formats, malformed replies, charging, voltage bounds).");
    }
}
