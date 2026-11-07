using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace VoltHUD
{
    // Enumerate connected classic + LE Bluetooth endpoints, across all brands.
    // No discovery inquiry, pairing, device settings, or radio settings are changed.
    internal static class BluetoothBattery
    {
        static readonly Guid Service = new Guid("0000180f-0000-1000-8000-00805f9b34fb");
        static readonly Guid Level = new Guid("00002a19-0000-1000-8000-00805f9b34fb");
        static T Finish<T>(IAsyncOperation<T> operation, int timeout) where T : class
        {
            var clock = Stopwatch.StartNew();
            while (operation.Status == AsyncStatus.Started && clock.ElapsedMilliseconds < timeout) Thread.Sleep(15);
            if (operation.Status == AsyncStatus.Completed)
            {
                try { return operation.GetResults(); }
                finally { operation.Close(); }
            }
            if (operation.Status == AsyncStatus.Started) operation.Cancel();
            if (operation.Status != AsyncStatus.Started) operation.Close();
            return null;
        }
        public static List<BatteryReading> Connected(List<BatteryReading> windowsValues)
        {
            var result = new List<BatteryReading>(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string[] selectors = {
                BluetoothDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected),
                BluetoothLEDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected)
            };
            for (int transport = 0; transport < selectors.Length; transport++)
            {
                try
                {
                    var endpoints = Finish(DeviceInformation.FindAllAsync(selectors[transport], new[] { "System.Devices.Aep.IsConnected", "System.Devices.Aep.ContainerId" }, DeviceInformationKind.AssociationEndpoint), 4000);
                    if (endpoints == null) { BatteryMonitor.Log("Bluetooth enumeration timed out or was unavailable."); continue; }
                    foreach (var device in endpoints)
                    {
                        object connected;
                        if (device.Properties.TryGetValue("System.Devices.Aep.IsConnected", out connected) && connected is bool && !(bool)connected) continue;
                        object container;
                        string key = device.Properties.TryGetValue("System.Devices.Aep.ContainerId", out container) && container != null && container.ToString() != Guid.Empty.ToString() ? container.ToString() : device.Id;
                        // Container IDs merge Bluetooth audio/HID services into one physical device.
                        if (!seen.Add(key)) continue;
                        BatteryReading known = windowsValues.Find(r => String.Equals(r.Key, key, StringComparison.OrdinalIgnoreCase));
                        var reading = new BatteryReading { Name = String.IsNullOrWhiteSpace(device.Name) ? "Bluetooth device" : device.Name, Key = "bluetooth:" + key, Kind = "device", State = "Battery unavailable", Detail = "Connected by Bluetooth. This device does not currently expose a readable battery level.", Updated = DateTime.Now };
                        if (known != null) { reading.Name = known.Name; reading.Percent = known.Percent; reading.State = "Bluetooth · Windows reported"; reading.Detail = known.Detail; }
                        if (transport == 1 && !reading.Percent.HasValue)
                        {
                            int? percent = ReadGatt(device.Id);
                            if (percent.HasValue) { reading.Percent = percent; reading.State = "Bluetooth · battery level"; reading.Detail = "Read from the standard Bluetooth Battery Service. Charging state is not provided."; }
                        }
                        if (reading.Name.IndexOf("logitech", StringComparison.OrdinalIgnoreCase) >= 0) reading.Name = BatteryMonitor.DeviceName(reading.Name);
                        result.Add(reading);
                        BatteryMonitor.Log("Bluetooth device: " + reading.ToString());
                    }
                }
                catch (Exception ex) { BatteryMonitor.Log("Bluetooth reader unavailable: " + ex.Message); }
            }
            return result;
        }
        static int? ReadGatt(string id)
        {
            try
            {
                using (var device = Finish(BluetoothLEDevice.FromIdAsync(id), 2000))
                {
                    if (device == null || device.ConnectionStatus != BluetoothConnectionStatus.Connected) return null;
                    var services = Finish(device.GetGattServicesForUuidAsync(Service, BluetoothCacheMode.Cached), 2000);
                    if (services == null || services.Status != GattCommunicationStatus.Success) return null;
                    try
                    {
                        foreach (var service in services.Services)
                        {
                            var characteristics = Finish(service.GetCharacteristicsForUuidAsync(Level, BluetoothCacheMode.Cached), 2000);
                            if (characteristics == null || characteristics.Status != GattCommunicationStatus.Success) continue;
                            foreach (var characteristic in characteristics.Characteristics)
                            {
                                var value = Finish(characteristic.ReadValueAsync(BluetoothCacheMode.Uncached), 2000);
                                if (value == null || value.Status != GattCommunicationStatus.Success || value.Value == null || value.Value.Length != 1) continue;
                                using (var reader = DataReader.FromBuffer(value.Value)) { byte level = reader.ReadByte(); return level <= 100 ? (int?)level : null; }
                            }
                        }
                    }
                    finally { foreach (var service in services.Services) service.Dispose(); }
                }
            }
            catch { /* Denied access or a device without Battery Service stays unavailable. */ }
            return null;
        }
    }
}
