using System;
namespace VoltHUD
{
    internal static class DeviceKind
    {
        public static string Resolve(string name, string reported)
        {
            if (!String.IsNullOrEmpty(reported) && reported != "device") return reported;
            name = (name ?? "").ToLowerInvariant();
            if (name.Contains("headset") || name.Contains("headphone") || name.Contains("earbud") || name.Contains("airpods")) return "headset";
            if (name.Contains("keyboard") || name.Contains("mx keys")) return "keyboard";
            if (name.Contains("mousepad") || name.Contains("powerplay")) return "mousepad";
            if (name.Contains("mouse") || name.Contains("trackball")) return "mouse";
            if (name.Contains("speaker") || name.Contains("soundbar")) return "speaker";
            if (name.Contains("controller") || name.Contains("gamepad") || name.Contains("dualsense") || name.Contains("xbox")) return "controller";
            if (name.Contains("microphone")) return "microphone";
            if (name.Contains("webcam") || name.Contains("camera")) return "webcam";
            if (name.Contains("pedal")) return "pedals";
            if (name.Contains("wheel")) return "wheel";
            if (name.Contains("stream deck") || name.Contains("keypad")) return "keypad";
            if (name.Contains("dock")) return "dock";
            if (name.Contains("receiver") || name.Contains("dongle")) return "receiver";
            return "device";
        }
    }
}
