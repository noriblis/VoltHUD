using System;
using System.Reflection;
using Microsoft.Win32;
namespace VoltHUD
{
    internal static class StartupRegistration
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public static string Command { get { return "\"" + Assembly.GetExecutingAssembly().Location + "\" --startup"; } }
        public static bool Enabled
        {
            get { using (var key = Registry.CurrentUser.OpenSubKey(RunKey)) return key != null && String.Equals(key.GetValue("VoltHUD") as string, Command, StringComparison.OrdinalIgnoreCase); }
        }
        public static void SetEnabled(bool enabled)
        {
            // Change only this user's VoltHUD entry, following an explicit toggle.
            using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (enabled) key.SetValue("VoltHUD", Command, RegistryValueKind.String);
                else key.DeleteValue("VoltHUD", false);
            }
        }
    }
}
