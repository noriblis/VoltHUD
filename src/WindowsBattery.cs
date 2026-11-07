using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace VoltHUD
{
    // Reads the battery property already provided by the Windows Bluetooth driver.
    // It does not pair devices, request permissions, or infer a charging state.
    internal static class WindowsBattery
    {
        [StructLayout(LayoutKind.Sequential)] struct DeviceInfo { public int Size; public Guid Class; public uint DevInst; public IntPtr Reserved; }
        [StructLayout(LayoutKind.Sequential)] struct PropertyKey { public Guid Format; public uint Id; public PropertyKey(string format, uint id) { Format = new Guid(format); Id = id; } }
        [StructLayout(LayoutKind.Sequential)] struct RadioSearch { public int Size; }
        static PropertyKey Battery = new PropertyKey("104ea319-6ee2-4701-bd47-8ddbf425bbe5", 2);
        static PropertyKey Manufacturer = new PropertyKey("a45c254e-df1c-4efd-8020-67d146a850e0", 13);
        static PropertyKey FriendlyName = new PropertyKey("a45c254e-df1c-4efd-8020-67d146a850e0", 14);
        static PropertyKey HardwareIds = new PropertyKey("a45c254e-df1c-4efd-8020-67d146a850e0", 3);
        static PropertyKey Description = new PropertyKey("a45c254e-df1c-4efd-8020-67d146a850e0", 2);
        static PropertyKey Container = new PropertyKey("8c7ed206-3f8a-4827-b3ab-ae9e1faefc6c", 2);
        static byte[] Property(IntPtr set, ref DeviceInfo info, PropertyKey key, byte[] buffer)
        {
            uint type, needed;
            return SetupDiGetDeviceProperty(set, ref info, ref key, out type, buffer, buffer.Length, out needed, 0) ? Copy(buffer, (int)needed) : null;
        }
        static byte[] Copy(byte[] data, int count) { var value = new byte[count]; Array.Copy(data, value, count); return value; }
        static string Text(byte[] value) { return value == null ? "" : Encoding.Unicode.GetString(value).TrimEnd('\0'); }
        public static List<BatteryReading> Scan()
        {
            var result = new List<BatteryReading>(); var seen = new HashSet<string>();
            // Avoid loading Bluetooth/WinRT services when no enabled radio exists.
            // A device-change notification triggers another scan if a radio is enabled later.
            if (!HasBluetoothRadio()) return result;
            var buffer = new byte[4096];
            IntPtr set = SetupDiGetClassDevs(IntPtr.Zero, null, IntPtr.Zero, 6);
            if (set == new IntPtr(-1)) return result;
            try
            {
                for (uint index = 0; ; index++)
                {
                    var info = new DeviceInfo { Size = Marshal.SizeOf(typeof(DeviceInfo)) };
                    if (!SetupDiEnumDeviceInfo(set, index, ref info)) break;
                    byte[] battery = Property(set, ref info, Battery, buffer);
                    if (battery == null || battery.Length != 1 || battery[0] > 100) continue;
                    string vendor = Text(Property(set, ref info, Manufacturer, buffer));
                    string ids = Text(Property(set, ref info, HardwareIds, buffer));
                    string name = Text(Property(set, ref info, FriendlyName, buffer));
                    if (name.Length == 0) name = Text(Property(set, ref info, Description, buffer));
                    byte[] container = Property(set, ref info, Container, buffer);
                    string key = container != null && container.Length == 16 ? new Guid(container).ToString() : name;
                    if (!seen.Add(key)) continue;
                    bool logitech = vendor.IndexOf("logitech", StringComparison.OrdinalIgnoreCase) >= 0 || ids.IndexOf("046d", StringComparison.OrdinalIgnoreCase) >= 0;
                    result.Add(new BatteryReading { Name = logitech ? BatteryMonitor.DeviceName(name) : name, Percent = battery[0], State = "Windows reported", Detail = "Windows supplies this battery value; it may be cached.", Key = key, Updated = DateTime.Now });
                }
            }
            finally { SetupDiDestroyDeviceInfoList(set); }
            return BluetoothBattery.Connected(result);
        }
        static bool HasBluetoothRadio()
        {
            try
            {
                var search = new RadioSearch { Size = Marshal.SizeOf(typeof(RadioSearch)) };
                IntPtr radio;
                IntPtr find = BluetoothFindFirstRadio(ref search, out radio);
                if (find == IntPtr.Zero) return false;
                try { return radio != IntPtr.Zero; }
                finally { if (radio != IntPtr.Zero) CloseHandle(radio); BluetoothFindRadioClose(find); }
            }
            catch (DllNotFoundException) { return true; }
            catch (EntryPointNotFoundException) { return true; }
        }
        [DllImport("bthprops.cpl", SetLastError = true)] static extern IntPtr BluetoothFindFirstRadio(ref RadioSearch search, out IntPtr radio);
        [DllImport("bthprops.cpl")] static extern bool BluetoothFindRadioClose(IntPtr find);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode)] static extern IntPtr SetupDiGetClassDevs(IntPtr guid, string enumerator, IntPtr parent, uint flags);
        [DllImport("setupapi.dll")] static extern bool SetupDiEnumDeviceInfo(IntPtr set, uint index, ref DeviceInfo info);
        [DllImport("setupapi.dll", EntryPoint = "SetupDiGetDevicePropertyW", CharSet = CharSet.Unicode)] static extern bool SetupDiGetDeviceProperty(IntPtr set, ref DeviceInfo info, ref PropertyKey key, out uint type, byte[] buffer, int size, out uint needed, uint flags);
        [DllImport("setupapi.dll")] static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
    }
}
