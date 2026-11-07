using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Media.Imaging;

namespace VoltHUD
{
    internal static class DeviceIcons
    {
        static readonly Dictionary<string, BitmapSource> Cache = new Dictionary<string, BitmapSource>();
        static BitmapImage sheet;
        public static BitmapSource Get(string kind)
        {
            if (String.IsNullOrEmpty(kind)) kind = "device";
            BitmapSource icon;
            if (Cache.TryGetValue(kind, out icon)) return icon;
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
                case "mouse": bounds = new Int32Rect(85, 124, 200, 280); break;
                case "keyboard": bounds = new Int32Rect(310, 177, 290, 170); break;
                case "headset": bounds = new Int32Rect(598, 122, 266, 282); break;
                case "speaker": bounds = new Int32Rect(898, 133, 194, 267); break;
                case "webcam": bounds = new Int32Rect(1137, 158, 278, 209); break;
                case "microphone": bounds = new Int32Rect(92, 418, 163, 297); break;
                case "controller": bounds = new Int32Rect(291, 462, 292, 211); break;
                case "wheel": bounds = new Int32Rect(588, 426, 273, 282); break;
                case "pedals": bounds = new Int32Rect(850, 464, 286, 215); break;
                case "keypad": bounds = new Int32Rect(1137, 459, 288, 216); break;
                case "mousepad": bounds = new Int32Rect(15, 748, 326, 209); break;
                case "usb": bounds = new Int32Rect(357, 741, 213, 219); break;
                case "dock": bounds = new Int32Rect(581, 734, 267, 229); break;
                case "receiver": bounds = new Int32Rect(1198, 728, 168, 243); break;
                default: bounds = new Int32Rect(872, 755, 271, 190); break;
            }
            icon = new CroppedBitmap(sheet, bounds); icon.Freeze(); Cache[kind] = icon; return icon;
        }
    }
}
