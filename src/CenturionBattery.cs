using System;
using System.Diagnostics;

namespace VoltHUD
{
    // Centurion devices use a separate frame format, not HID++ report 0x11.
    internal static class CenturionBattery
    {
        static byte[] Exchange(HidChannel channel, int bridge, byte feature, byte function, params byte[] arguments)
        {
            byte fn = (byte)(function | 0x0B);
            int start = bridge < 0 ? 3 : 8;
            byte[] packet = new byte[channel.OutputLength];
            packet[0] = 0x51; packet[1] = (byte)(start - 2 + 2 + arguments.Length);
            if (bridge >= 0)
            {
                packet[3] = (byte)bridge; packet[4] = 0x1B;
                packet[5] = 0; packet[6] = (byte)(3 + arguments.Length); packet[7] = 0;
            }
            packet[start] = feature; packet[start + 1] = fn;
            Array.Copy(arguments, 0, packet, start + 2, arguments.Length);
            if (!channel.Transfer(packet, true, 400)) return null;
            var clock = Stopwatch.StartNew();
            while (clock.ElapsedMilliseconds < 1200)
            {
                byte[] reply = new byte[channel.InputLength];
                if (!channel.Transfer(reply, false, Math.Max(1, 1200 - (int)clock.ElapsedMilliseconds))) return null;
                if (reply[0] != 0x51 || reply[1] < 3 || reply[1] + 2 > reply.Length) continue;
                int offset = bridge < 0 ? 3 : 8;
                if (bridge >= 0 && (reply[3] != bridge || reply[4] != 0x10 || reply[7] != 0)) continue;
                if (reply[1] + 2 < offset + 2) continue;
                if (reply[offset] == 0xFF) return null;
                if (reply[offset] != feature || reply[offset + 1] != fn) continue;
                int size = reply[1] + 2 - offset - 2;
                byte[] data = new byte[size]; Array.Copy(reply, offset + 2, data, 0, size); return data;
            }
            return null;
        }
        static int Locate(HidChannel channel, int bridge, int id)
        {
            byte[] set = Exchange(channel, bridge, 0, 0, 0, 1);
            if (set == null || set.Length == 0 || set[0] == 0) return -1;
            byte[] count = Exchange(channel, bridge, set[0], 0);
            if (count == null || count.Length == 0 || count[0] > 100) return -1;
            for (byte index = 0; index < count[0]; index++)
            {
                var data = Exchange(channel, bridge, set[0], 0x10, index);
                if (data != null && data.Length >= 3 && ((data[1] << 8) | data[2]) == id) return index;
            }
            return -1;
        }
        internal static BatteryReading Read(HidChannel channel)
        {
            if (channel.OutputLength < 20 || channel.InputLength < 20) return null;
            int battery = Locate(channel, -1, 0x0104);
            byte[] data = battery >= 0 ? Exchange(channel, -1, (byte)battery, 0) : null;
            if (data == null)
            {
                int bridge = Locate(channel, -1, 3);
                if (bridge < 0) return null;
                battery = Locate(channel, bridge, 0x0104);
                if (battery < 0) return null;
                data = Exchange(channel, bridge, (byte)battery, 0);
            }
            var reading = Decode(data);
            if (reading != null) { reading.Name = BatteryMonitor.DeviceName(channel.Product); reading.Kind = "headset"; }
            return reading;
        }
        public static BatteryReading Decode(byte[] data)
        {
            if (data == null || data.Length < 3 || data[0] > 100) return null;
            return new BatteryReading { Percent = data[0], Charging = data[2] == 1 || data[2] == 2, State = data[2] == 3 ? "Fully charged" : data[2] == 1 || data[2] == 2 ? "Charging" : data[2] == 0 ? "On battery" : "Battery status unavailable" };
        }
    }
}
