using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace VoltHUD
{
    // Bounded, persistable latches survive reconnects, preference changes and restarts.
    public sealed class LowBatteryAlerts
    {
        readonly List<string> warned = new List<string>();
        public bool Changed { get; private set; }
        public string[] Export() { return warned.ToArray(); }
        public void MarkSaved() { Changed = false; }
        public void Restore(IEnumerable<string> values)
        {
            warned.Clear();
            foreach (string value in values)
            {
                if (value == null || value.Length != 64 || !Array.TrueForAll(value.ToCharArray(), c => c >= '0' && c <= '9' || c >= 'A' && c <= 'F')) continue;
                if (!warned.Contains(value)) { if (warned.Count == 256) warned.RemoveAt(0); warned.Add(value); }
            }
            Changed = false;
        }
        static string Identity(string key)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(key.ToUpperInvariant()))).Replace("-", "");
        }

        public List<BatteryReading> Evaluate(IEnumerable<BatteryReading> readings, bool enabled)
        {
            var alerts = new List<BatteryReading>();
            foreach (var reading in readings)
            {
                string key = reading.Key;
                if (string.IsNullOrEmpty(key) || !reading.Percent.HasValue || reading.NeedsIdentity) continue;
                int percent = reading.Percent.Value;
                if (percent < 0 || percent > 100) continue;
                key = Identity(key);
                if (percent >= 15) { if (warned.Remove(key)) Changed = true; continue; }
                if (!enabled || percent >= 10 || reading.Charging || warned.Contains(key)) continue;
                if (warned.Count == 256) warned.RemoveAt(0);
                warned.Add(key);
                Changed = true;
                alerts.Add(reading);
            }
            return alerts;
        }
    }
}
