using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace VoltHUD
{
    internal static class PrepareIcons
    {
        [STAThread] static void Main(string[] args)
        {
            Directory.CreateDirectory(args[0]);
            foreach (string kind in new[] { "mouse", "keyboard", "headset", "speaker", "webcam", "microphone", "controller", "wheel", "pedals", "keypad", "mousepad", "usb", "dock", "receiver", "battery", "device" })
            {
                BitmapSource icon = IconSources.Get(kind);
                double scale = Math.Min(1.0, 256.0 / Math.Max(icon.PixelWidth, icon.PixelHeight));
                if (scale < 1) icon = new TransformedBitmap(icon, new ScaleTransform(scale, scale));
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(icon));
                using (var stream = File.Create(Path.Combine(args[0], kind + ".png"))) encoder.Save(stream);
            }
        }
    }
}
