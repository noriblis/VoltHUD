using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Media.Imaging;

namespace VoltHUD
{
    internal static class IconSources
    {
        static readonly Dictionary<string, BitmapSource> Cache = new Dictionary<string, BitmapSource>();
        static BitmapImage sheet;
        public static BitmapSource Get(string kind)
        {
            if (String.IsNullOrEmpty(kind)) kind = "device";
            BitmapSource icon;
            if (Cache.TryGetValue(kind, out icon)) return icon;
            string[] known = { "mouse", "keyboard", "headset", "speaker", "webcam", "microphone", "controller", "wheel", "pedals", "keypad", "mousepad", "usb", "dock", "receiver", "battery" };
            if (Array.IndexOf(known, kind) < 0)
            {
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("VoltHUD.UnknownDevice.png"))
                {
                    var unknown = new BitmapImage(); unknown.BeginInit(); unknown.CacheOption = BitmapCacheOption.OnLoad; unknown.StreamSource = stream; unknown.EndInit(); unknown.Freeze();
                    // Discard transparent margins in the displayed view; preserve the supplied file.
                    byte[] pixels = new byte[unknown.PixelWidth * unknown.PixelHeight * 4];
                    var bgra = new FormatConvertedBitmap(unknown, System.Windows.Media.PixelFormats.Bgra32, null, 0);
                    bgra.CopyPixels(pixels, unknown.PixelWidth * 4, 0);
                    int left = unknown.PixelWidth, top = unknown.PixelHeight, right = 0, bottom = 0;
                    for (int y = 0; y < unknown.PixelHeight; y++) for (int x = 0; x < unknown.PixelWidth; x++) if (pixels[(y * unknown.PixelWidth + x) * 4 + 3] > 20) { left = Math.Min(left, x); top = Math.Min(top, y); right = Math.Max(right, x); bottom = Math.Max(bottom, y); }
                    icon = left <= right && top <= bottom ? (BitmapSource)new CroppedBitmap(unknown, new Int32Rect(left, top, right - left + 1, bottom - top + 1)) : unknown;
                    icon.Freeze(); Cache[kind] = icon; return icon;
                }
            }
            if (sheet == null)
            {
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("VoltHUD.DeviceIcons.png"))
                {
                    sheet = new BitmapImage(); sheet.BeginInit(); sheet.CacheOption = BitmapCacheOption.OnLoad; sheet.StreamSource = stream; sheet.EndInit(); sheet.Freeze();
                }
            }
            // The supplied transparent sheet is embedded unchanged. Only the view is cropped.
            Int32Rect bounds;
            switch (kind)
            {
                case "mouse": bounds = new Int32Rect(66, 120, 200, 284); break;
                case "keyboard": bounds = new Int32Rect(295, 178, 284, 172); break;
                case "headset": bounds = new Int32Rect(585, 117, 268, 286); break;
                case "speaker": bounds = new Int32Rect(885, 130, 201, 268); break;
                case "webcam": bounds = new Int32Rect(1125, 155, 286, 207); break;
                case "microphone": bounds = new Int32Rect(78, 413, 170, 301); break;
                case "controller": bounds = new Int32Rect(280, 453, 297, 219); break;
                case "wheel": bounds = new Int32Rect(574, 418, 283, 288); break;
                case "pedals": bounds = new Int32Rect(845, 460, 283, 209); break;
                case "keypad": bounds = new Int32Rect(1124, 452, 303, 220); break;
                case "mousepad": bounds = new Int32Rect(0, 742, 337, 219); break;
                case "usb": bounds = new Int32Rect(350, 742, 213, 217); break;
                case "dock": bounds = new Int32Rect(573, 729, 281, 229); break;
                case "receiver": bounds = new Int32Rect(1198, 716, 176, 253); break;
                default: bounds = new Int32Rect(874, 758, 274, 174); break;
            }
            icon = new CroppedBitmap(sheet, bounds); icon.Freeze(); Cache[kind] = icon; return icon;
        }
    }
}
