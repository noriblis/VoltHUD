using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace VoltHUD
{
    public sealed class BatteryReading
    {
        public string Name;
        public int? Percent;
        public bool Charging;
        public string State;
        public string Key;
        public string Kind = "device";
        public string Detail;
        public DateTime Updated;
        public bool NeedsIdentity;
        public override string ToString() { return Name + " | " + (Percent.HasValue ? Percent + "%" : "unknown") + " | " + State; }
    }

    // Only read-only HID++ requests are sent: feature lookup, name/type, battery.
    public static class BatteryMonitor
    {
        public static Action<string> Log = delegate { };
        static readonly Dictionary<string, Tuple<string,string>> IdentityCache = new Dictionary<string, Tuple<string,string>>();
        public static string DeviceName(string name)
        {
            if (String.IsNullOrWhiteSpace(name)) return "Wireless peripheral";
            name = name.Trim();
            return name.StartsWith("Logitech", StringComparison.OrdinalIgnoreCase) ? name : "Logitech " + name;
        }
        public static List<BatteryReading> Scan()
        {
            // HID replies carry a small client ID. Two VoltHUD processes must not
            // consume each other's feature/name replies during diagnostics or previews.
            using (var gate = new System.Threading.Mutex(false, @"Local\VoltHUD.BatteryScan"))
            {
                bool acquired = false;
                try
                {
                    try { acquired = gate.WaitOne(TimeSpan.FromSeconds(60)); }
                    catch (System.Threading.AbandonedMutexException) { acquired = true; }
                    if (!acquired) throw new TimeoutException("Another battery scan is still running.");
                    return ScanCore();
                }
                finally { if (acquired) gate.ReleaseMutex(); }
            }
        }
        public static string DecodeDeviceName(byte[] bytes)
        {
            if (bytes == null) return null;
            try
            {
                string name = new UTF8Encoding(false, true).GetString(bytes).TrimEnd('\0').Trim();
                if (String.IsNullOrWhiteSpace(name)) return null;
                foreach (char value in name) if (Char.IsControl(value)) return null;
                return name;
            }
            catch (DecoderFallbackException) { return null; }
        }
        static List<BatteryReading> ScanCore()
        {
            var readings = new List<BatteryReading>();
            var seen = new HashSet<string>();
            foreach (string path in Native.Paths())
            {
                try
                {
                    using (var hid = HidChannel.Open(path))
                    {
                        if (hid == null) continue;
                        Log("Logitech HID interface: " + hid.Product + " (" + hid.InputLength + "/" + hid.OutputLength + ")");
                        if (hid.Centurion)
                        {
                            var headset = CenturionBattery.Read(hid);
                            if (headset != null) { headset.Key = path; headset.Updated = DateTime.Now; readings.Add(headset); }
                            continue;
                        }
                        foreach (byte device in new byte[] { 1, 255, 2, 3, 4, 5, 6 })
                        {
                            string kind = "device";
                            BatteryReading legacy = null;
                            // Legacy register reads must use the short-report collection.
                            if (hid.OutputLength == 7)
                            {
                                legacy = DecodeRegister(0x0D, hid.Register(device, 0x0D));
                                if (legacy == null) legacy = DecodeRegister(7, hid.Register(device, 7));
                                if (legacy == null) continue;
                                legacy.Name = "Wireless peripheral · slot " + device;
                                legacy.Key = path + ":" + device;
                                legacy.Updated = DateTime.Now;
                                readings.Add(legacy);
                                continue;
                            }
                            var ping = hid.Request(device, 0, 0x10, 0, 0, 0x42);
                            if (ping == null && (device == 1 || device == 255)) { System.Threading.Thread.Sleep(80); ping = hid.Request(device, 0, 0x10, 0, 0, 0x42); }
                            if (ping == null) continue;
                            int feature = hid.Feature(device, 0x1004);
                            int protocol = 0x1004;
                            if (feature == 0) { feature = hid.Feature(device, 0x1000); protocol = 0x1000; }
                            if (feature == 0) { feature = hid.Feature(device, 0x1001); protocol = 0x1001; }
                            if (feature == 0) { feature = hid.Feature(device, 0x1F20); protocol = 0x1F20; }
                            if (feature == 0) continue;
                            int nameFeature = hid.Feature(device, 5);
                            string name = hid.Product;
                            bool nameRead = false, typeRead = false;
                            for (int attempt = 0; attempt < 3 && (!nameRead || !typeRead); attempt++)
                            {
                                if (attempt > 0) { System.Threading.Thread.Sleep(80); if (nameFeature == 0) nameFeature = hid.Feature(device, 5); }
                                if (nameFeature == 0) continue;
                                if (!typeRead)
                                {
                                    var type = hid.Request(device, (byte)nameFeature, 0x20);
                                    if (type != null) { typeRead = true; kind = type[4] == 3 || type[4] == 4 || type[4] == 5 ? "mouse" : type[4] == 0 || type[4] == 2 ? "keyboard" : "device"; }
                                }
                                if (nameRead) continue;
                                var count = hid.Request(device, (byte)nameFeature, 0);
                                if (count != null && count[4] > 0 && count[4] < 100)
                                {
                                    var text = new List<byte>();
                                    int length = count[4];
                                    while (text.Count < length)
                                    {
                                        var part = hid.Request(device, (byte)nameFeature, 0x10, (byte)text.Count);
                                        if (part == null) break;
                                        for (int i = 4; i < part.Length && text.Count < length; i++) text.Add(part[i]);
                                    }
                                    if (text.Count == length)
                                    {
                                        string decoded = DecodeDeviceName(text.ToArray());
                                        if (decoded != null) { name = decoded; nameRead = true; }
                                    }
                                }
                            }
                            byte[] result = hid.Request(device, (byte)feature, protocol == 0x1004 ? (byte)0x10 : (byte)0);
                            if (result == null) continue;
                            var reading = Decode(protocol, result);
                            if (reading == null) continue;
                            reading.Name = DeviceName(name);
                            reading.Kind = hid.Headset || protocol == 0x1F20 ? "headset" : kind;
                            reading.NeedsIdentity = !nameRead || !typeRead;
                            int infoFeature = hid.Feature(device, 3);
                            byte[] info = infoFeature == 0 ? null : hid.Request(device, (byte)infoFeature, 0);
                            // A unit ID distinguishes identical models and merges cable/receiver duplicates.
                            string identity = info != null && info.Length >= 9 && (info[5] | info[6] | info[7] | info[8]) != 0 ? BitConverter.ToString(info, 5, 4) : path + ":" + device;
                            bool stableIdentity = info != null && info.Length >= 9 && (info[5] | info[6] | info[7] | info[8]) != 0;
                            if (stableIdentity)
                            {
                                Tuple<string,string> cached;
                                if (reading.NeedsIdentity && IdentityCache.TryGetValue(identity, out cached))
                                {
                                    reading.Name = cached.Item1; reading.Kind = cached.Item2; reading.NeedsIdentity = false;
                                }
                                else if (!reading.NeedsIdentity)
                                {
                                    if (IdentityCache.Count >= 256 && !IdentityCache.ContainsKey(identity)) IdentityCache.Clear();
                                    IdentityCache[identity] = Tuple.Create(reading.Name, reading.Kind);
                                }
                            }
                            if (reading.NeedsIdentity) Log("Battery answered before device identity was ready; discovery will retry automatically.");
                            if (reading.NeedsIdentity)
                            {
                                if (!nameRead) reading.Name = "Identifying device…";
                                reading.Detail = "Battery received; the device name or type is still being discovered.";
                                reading.State = "Identifying device…";
                            }
                            if (!seen.Add(identity)) continue;
                            reading.Key = identity;
                            reading.Updated = DateTime.Now;
                            readings.Add(reading);
                            Log(reading.ToString());
                        }
                    }
                }
                catch (Exception ex) { Log("Interface unavailable: " + ex.Message); }
            }
            try { readings.AddRange(WindowsBattery.Scan()); } catch (Exception ex) { Log("Windows battery reader: " + ex.Message); }
            return readings;
        }

        public static BatteryReading Decode(int protocol, byte[] data)
        {
            if (data == null || data.Length < 7) return null;
            int level = data[4];
            int status = data[6];
            if (protocol == 0x1001 || protocol == 0x1F20)
            {
                int millivolts = (data[4] << 8) | data[5];
                if (millivolts < 2500 || millivolts > 5000 || (protocol == 0x1F20 && (status & 1) == 0)) return null;
                bool plugged = protocol == 0x1001 ? (status & 0x80) != 0 : (status & 2) != 0;
                return new BatteryReading { Percent = VoltagePercent(millivolts), Charging = plugged, State = plugged ? "Charging · estimated" : "On battery · estimated", Detail = millivolts + " mV · voltage-based estimate" };
            }
            if (protocol != 0x1000 && protocol != 0x1004) return null;
            if (level > 100) return null;
            bool charging = status == 1 || status == 2 || status == 4;
            string state = status == 3 ? "Fully charged" : status == 4 ? "Charging slowly" : charging ? "Charging" : status > 4 ? "Battery status unavailable" : "On battery";
            // BATTERY_LEVEL_STATUS explicitly uses 0 for unknown; unified battery has a real 0%.
            int? percent = protocol == 0x1000 && level == 0 ? (int?)null : level;
            string detail = null;
            if (protocol == 0x1004 && level == 0 && data[5] != 0)
            {
                percent = null;
                detail = data[5] == 8 ? "Full" : data[5] == 4 ? "Good" : data[5] == 2 ? "Low" : data[5] == 1 ? "Critical" : "Unknown";
                state = detail + (charging ? " · charging" : " · coarse level");
            }
            return new BatteryReading { Percent = percent, Charging = charging, State = state, Detail = detail };
        }
        public static int VoltagePercent(int millivolts)
        {
            // A generic single-cell Li-ion estimate, never presented as an exact reading.
            int[] voltage = {3500, 3600, 3670, 3720, 3780, 3810, 3860, 3920, 3990, 4070, 4200};
            int[] percent = {0, 3, 10, 20, 40, 50, 60, 70, 80, 90, 100};
            if (millivolts <= voltage[0]) return 0;
            for (int i = 1; i < voltage.Length; i++) if (millivolts <= voltage[i]) return percent[i-1] + (int)Math.Round((millivolts - voltage[i-1]) * (percent[i] - percent[i-1]) / (double)(voltage[i] - voltage[i-1]));
            return 100;
        }
        public static BatteryReading DecodeRegister(int register, byte[] data)
        {
            if (data == null || data.Length < 7) return null;
            if (register == 0x0D)
            {
                if (data[4] == 0 || data[4] > 100) return null;
                int status = data[6] & 0xF0;
                return new BatteryReading { Percent = data[4], Charging = status == 0x50, State = status == 0x90 ? "Fully charged" : status == 0x50 ? "Charging" : "On battery" };
            }
            if (register != 7) return null;
            string label = data[4] == 7 ? "Full" : data[4] == 5 ? "Good" : data[4] == 3 ? "Low" : data[4] == 1 ? "Critical" : null;
            if (label == null) return null;
            bool charging = (data[5] & 0x21) == 0x21;
            return new BatteryReading { Percent = null, Detail = label, Charging = charging, State = label + (charging ? " · charging" : " · coarse level") };
        }
    }

    internal sealed class HidChannel : IDisposable
    {
        SafeFileHandle handle;
        public int InputLength, OutputLength;
        public string Product;
        public bool Centurion, Headset;
        int softwareId = 10;
        public static HidChannel Open(string path)
        {
            using (var info = Native.CreateFile(path, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero))
            {
                if (info.IsInvalid) { BatteryMonitor.Log("Cannot inspect HID interface (Windows error " + Marshal.GetLastWin32Error() + ")"); return null; }
                IntPtr preparsed;
                if (!Native.HidD_GetPreparsedData(info, out preparsed)) { BatteryMonitor.Log("No HID capabilities"); return null; }
                byte[] caps = new byte[64];
                try { if (Native.HidP_GetCaps(preparsed, caps) != 0x110000) return null; }
                finally { Native.HidD_FreePreparsedData(preparsed); }
                int page = BitConverter.ToUInt16(caps, 2);
                int usage = BitConverter.ToUInt16(caps, 0);
                int input = BitConverter.ToUInt16(caps, 4), output = BitConverter.ToUInt16(caps, 6);
                bool centurion = page == 0xFFA0 && usage == 1;
                bool headset = page == 0xFF43 && usage == 0x0202 || page == 0x000C && usage == 1 && path.IndexOf("pid_0ac4", StringComparison.OrdinalIgnoreCase) >= 0;
                if ((!centurion && !headset && (page != 0xFF00 || usage != 1 && usage != 2)) || input < 7 || output < 7 || input > 64 || output > 64) return null;
                var product = new byte[256];
                Native.HidD_GetProductString(info, product, product.Length);
                var handle = Native.CreateFile(path, 0xC0000000, 3, IntPtr.Zero, 3, 0x40000000, IntPtr.Zero);
                if (handle.IsInvalid) { BatteryMonitor.Log("Cannot open vendor HID interface (Windows error " + Marshal.GetLastWin32Error() + ")"); handle.Dispose(); return null; }
                return new HidChannel { handle = handle, InputLength = input, OutputLength = output, Product = Encoding.Unicode.GetString(product).TrimEnd('\0'), Centurion = centurion, Headset = headset };
            }
        }
        public int Feature(byte device, int id)
        {
            var data = Request(device, 0, 0, (byte)(id >> 8), (byte)id);
            return data == null ? 0 : data[4];
        }
        public byte[] Request(byte device, byte feature, byte function, params byte[] parameters)
        {
            byte[] packet = new byte[OutputLength];
            packet[0] = OutputLength >= 20 ? (byte)0x11 : (byte)0x10;
            softwareId = softwareId == 15 ? 10 : softwareId + 1;
            packet[1] = device; packet[2] = feature; packet[3] = (byte)(function | softwareId);
            Array.Copy(parameters, 0, packet, 4, Math.Min(parameters.Length, packet.Length - 4));
            if (!Transfer(packet, true, 300)) return null;
            var clock = Stopwatch.StartNew();
            while (clock.ElapsedMilliseconds < 400)
            {
                var reply = new byte[InputLength];
                if (!Transfer(reply, false, Math.Max(1, 400 - (int)clock.ElapsedMilliseconds))) return null;
                if ((reply[0] != 0x10 && reply[0] != 0x11) || reply[1] != device) continue;
                if ((reply[2] == 0xFF || reply[2] == 0x8F) && reply[3] == feature && reply[4] == packet[3]) return null;
                if (reply[2] == feature && reply[3] == packet[3]) return reply;
            }
            return null;
        }
        public byte[] Register(byte device, byte register)
        {
            byte[] packet = new byte[] { 0x10, device, 0x81, register, 0, 0, 0 };
            if (!Transfer(packet, true, 300)) return null;
            var clock = Stopwatch.StartNew();
            while (clock.ElapsedMilliseconds < 300)
            {
                byte[] reply = new byte[InputLength];
                if (!Transfer(reply, false, Math.Max(1, 300 - (int)clock.ElapsedMilliseconds))) return null;
                if (reply[1] != device) continue;
                if (reply[2] == 0x81 && reply[3] == register) return reply;
                if ((reply[2] == 0x8F || reply[2] == 0xFF) && reply[3] == 0x81 && reply[4] == register) return null;
            }
            return null;
        }
        internal bool Transfer(byte[] bytes, bool write, int timeout)
        {
            IntPtr buffer = Marshal.AllocHGlobal(bytes.Length);
            IntPtr overlapped = Marshal.AllocHGlobal(IntPtr.Size == 8 ? 32 : 20);
            IntPtr signal = Native.CreateEvent(IntPtr.Zero, true, false, null);
            try
            {
                for (int i = 0; i < (IntPtr.Size == 8 ? 32 : 20); i++) Marshal.WriteByte(overlapped, i, 0);
                Marshal.WriteIntPtr(overlapped, IntPtr.Size == 8 ? 24 : 16, signal);
                Marshal.Copy(bytes, 0, buffer, bytes.Length);
                uint transferred;
                bool success = write ? Native.WriteFile(handle, buffer, bytes.Length, out transferred, overlapped) : Native.ReadFile(handle, buffer, bytes.Length, out transferred, overlapped);
                if (!success && Marshal.GetLastWin32Error() != 997) return false;
                if (!success && Native.WaitForSingleObject(signal, (uint)timeout) != 0)
                {
                    Native.CancelIoEx(handle, overlapped);
                    // Keep unmanaged memory alive until the cancelled I/O has completed.
                    Native.GetOverlappedResult(handle, overlapped, out transferred, true);
                    return false;
                }
                if (!Native.GetOverlappedResult(handle, overlapped, out transferred, true) || transferred < (write ? bytes.Length : 7)) return false;
                if (!write) Marshal.Copy(buffer, bytes, 0, bytes.Length);
                return true;
            }
            finally { Native.CloseHandle(signal); Marshal.FreeHGlobal(overlapped); Marshal.FreeHGlobal(buffer); }
        }
        public void Dispose() { if (handle != null) handle.Dispose(); }
    }

    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential)] struct InterfaceData { public int Size; public Guid Guid; public int Flags; public IntPtr Reserved; }
        public static IEnumerable<string> Paths()
        {
            Guid guid; HidD_GetHidGuid(out guid);
            IntPtr set = SetupDiGetClassDevs(ref guid, null, IntPtr.Zero, 0x12);
            if (set == new IntPtr(-1)) yield break;
            try
            {
                for (uint index = 0; ; index++)
                {
                    var data = new InterfaceData { Size = Marshal.SizeOf(typeof(InterfaceData)) };
                    if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref guid, index, ref data)) break;
                    int needed;
                    SetupDiGetDeviceInterfaceDetail(set, ref data, IntPtr.Zero, 0, out needed, IntPtr.Zero);
                    IntPtr detail = Marshal.AllocHGlobal(needed);
                    string path = null;
                    try
                    {
                        Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                        if (SetupDiGetDeviceInterfaceDetail(set, ref data, detail, needed, out needed, IntPtr.Zero)) path = Marshal.PtrToStringUni(IntPtr.Add(detail, 4));
                    }
                    finally { Marshal.FreeHGlobal(detail); }
                    if (path != null && path.IndexOf("vid_046d", StringComparison.OrdinalIgnoreCase) >= 0) yield return path;
                }
            }
            finally { SetupDiDestroyDeviceInfoList(set); }
        }
        [DllImport("hid.dll")] static extern void HidD_GetHidGuid(out Guid guid);
        [DllImport("hid.dll")] public static extern bool HidD_GetPreparsedData(SafeFileHandle handle, out IntPtr data);
        [DllImport("hid.dll")] public static extern bool HidD_FreePreparsedData(IntPtr data);
        [DllImport("hid.dll")] public static extern int HidP_GetCaps(IntPtr data, [Out] byte[] caps);
        [DllImport("hid.dll")] public static extern bool HidD_GetProductString(SafeFileHandle handle, byte[] buffer, int size);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern IntPtr SetupDiGetClassDevs(ref Guid guid, string enumerator, IntPtr parent, uint flags);
        [DllImport("setupapi.dll", SetLastError = true)] static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr device, ref Guid guid, uint index, ref InterfaceData data);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref InterfaceData data, IntPtr detail, int size, out int needed, IntPtr device);
        [DllImport("setupapi.dll")] static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool ReadFile(SafeFileHandle handle, IntPtr buffer, int size, out uint read, IntPtr overlapped);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool WriteFile(SafeFileHandle handle, IntPtr buffer, int size, out uint written, IntPtr overlapped);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool GetOverlappedResult(SafeFileHandle handle, IntPtr overlapped, out uint transferred, bool wait);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool CancelIoEx(SafeFileHandle handle, IntPtr overlapped);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr CreateEvent(IntPtr security, bool manual, bool initial, string name);
        [DllImport("kernel32.dll")] public static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
        [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr handle);
    }
}
