using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
namespace VoltHUD
{
    internal static class ThemePalette
    {
        public static bool Dark = true;
        static bool followWindows;
        static Color accent = Color.FromRgb(0,184,252);
        static readonly Dictionary<string, SolidColorBrush> Brushes = new Dictionary<string, SolidColorBrush>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string,string> Light = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) {
            {"#171B23","#F7F9FC"},{"#343B47","#CCD4DF"},{"#29303B","#E4EAF2"},{"#29313D","#E4EAF2"},
            {"#303743","#DDE4ED"},{"#2C333E","#DCE3EC"},{"#343D4A","#BEC9D7"},
            {"#E8ECF2","#263142"},{"#DDE4EF","#263142"},{"#F4F7FC","#1D293A"},
            {"#9098A6","#5A687A"},{"#9FA8B7","#5A687A"},{"#A0AABA","#59687B"},
            {"#929EAE","#59687B"},{"#778395","#59687B"},{"#8791A2","#63748A"},{"#8795A9","#59687B"},
            {"#B0BAC8","#46576B"},{"#BAC5D4","#FFFFFF"},{"#00B8FC","#007AAF"},
            {"#FFAA7A","#B44817"},{"#F2A7A7","#B33F49"},{"#FFFFFF","#263142"},
            {"#89DEFF","#72D3F7"},{"#79DBFF","#2B9ECC"},{"#C2ECFF","#007AAF"}
        };
        public static bool ResolveDark(string mode)
        {
            if (mode == "Dark") return true;
            if (mode == "Light") return false;
            try { using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")) return key != null && Convert.ToInt32(key.GetValue("AppsUseLightTheme", 1)) == 0; }
            catch { return true; }
        }
        public static void Configure(string mode)
        {
            bool dark = ResolveDark(mode), follow = mode == "System";
            Color selected = follow ? ReadWindowsAccent() : Color.FromRgb(0,184,252);
            if (dark != Dark || follow != followWindows || selected != accent) Brushes.Clear();
            Dark = dark; followWindows = follow; accent = selected;
        }
        public static Color ReadWindowsAccent()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent"))
                {
                    object selected = key == null ? null : key.GetValue("AccentColorMenu");
                    if (selected != null)
                    {
                        // Explorer stores the chosen accent as ABGR, not ARGB.
                        uint value = unchecked((uint)Convert.ToInt64(selected));
                        return Color.FromRgb((byte)value, (byte)(value >> 8), (byte)(value >> 16));
                    }
                }
            }
            catch { }
            try
            {
                uint value; bool opaque;
                if (DwmGetColorizationColor(out value, out opaque) >= 0) return Color.FromRgb((byte)(value >> 16), (byte)(value >> 8), (byte)value);
            }
            catch (DllNotFoundException) { } catch (EntryPointNotFoundException) { }
            return Color.FromRgb(0,184,252);
        }
        static Color Blend(Color first, Color second, double fraction)
        {
            return Color.FromRgb((byte)Math.Round(first.R+(second.R-first.R)*fraction), (byte)Math.Round(first.G+(second.G-first.G)*fraction), (byte)Math.Round(first.B+(second.B-first.B)*fraction));
        }
        static string Hex(Color color) { return "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2"); }
        static double Channel(byte value) { double s = value/255.0; return s <= 0.04045 ? s/12.92 : Math.Pow((s+0.055)/1.055,2.4); }
        static double Luminance(Color c) { return .2126*Channel(c.R)+.7152*Channel(c.G)+.0722*Channel(c.B); }
        static double Contrast(Color a, Color b) { double x=Luminance(a), y=Luminance(b); return (Math.Max(x,y)+.05)/(Math.Min(x,y)+.05); }
        public static SolidColorBrush Brush(string hex)
        {
            string value;
            if (followWindows && hex.Equals("#00B8FC", StringComparison.OrdinalIgnoreCase)) hex = Hex(accent);
            else if (followWindows && (hex.Equals("#89DEFF",StringComparison.OrdinalIgnoreCase) || hex.Equals("#79DBFF",StringComparison.OrdinalIgnoreCase) || hex.Equals("#C2ECFF",StringComparison.OrdinalIgnoreCase))) hex = Hex(Blend(accent, Dark ? Colors.White : Colors.Black, .35));
            else if (!Dark && Light.TryGetValue(hex, out value)) hex = value;
            SolidColorBrush brush;
            if (Brushes.TryGetValue(hex, out brush)) return brush;
            brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); brush.Freeze(); Brushes[hex] = brush; return brush;
        }
        public static void Apply(FrameworkElement element)
        {
            foreach (var item in Light) element.Resources["C" + item.Key.Substring(1).ToUpperInvariant()] = Brush(item.Key);
            Color text = Brush("#00B8FC").Color, background = Brush("#171B23").Color;
            for (int i=1; i<=20 && Contrast(text,background)<4.5; i++) text = Blend(text, Dark ? Colors.White : Colors.Black, .10);
            var textBrush = new SolidColorBrush(text); textBrush.Freeze(); element.Resources["AccentText"] = textBrush;
        }
        [DllImport("dwmapi.dll")] static extern int DwmGetColorizationColor(out uint color, out bool opaque);
    }
}
