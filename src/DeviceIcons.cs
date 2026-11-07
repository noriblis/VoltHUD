using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Media.Imaging;
namespace VoltHUD
{
    internal static class DeviceIcons
    {
        static readonly HashSet<string> Known = new HashSet<string>(new[] { "mouse", "keyboard", "headset", "speaker", "webcam", "microphone", "controller", "wheel", "pedals", "keypad", "mousepad", "usb", "dock", "receiver", "battery", "device" });
        static readonly Dictionary<string, BitmapSource> Cache = new Dictionary<string, BitmapSource>();
        public static BitmapSource Get(string kind)
        {
            if (String.IsNullOrEmpty(kind) || !Known.Contains(kind)) kind = "device";
            BitmapSource cached;
            if (Cache.TryGetValue(kind, out cached)) return cached;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("VoltHUD.Icons." + kind + ".png"))
            {
                var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.StreamSource = stream; image.EndInit(); image.Freeze();
                Cache[kind] = image; return image;
            }
        }
    }
}