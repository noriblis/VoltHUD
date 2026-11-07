using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

[assembly: AssemblyTitle("VoltHUD")]
[assembly: AssemblyDescription("A quiet battery widget for Logitech and Bluetooth peripherals")]
[assembly: AssemblyCompany("VoltHUD contributors")]
[assembly: AssemblyProduct("VoltHUD")]
[assembly: AssemblyVersion("0.1.1.0")]
[assembly: AssemblyFileVersion("0.1.1.0")]
[assembly: AssemblyCopyright("Copyright 2026 VoltHUD contributors")]

namespace VoltHUD
{
    public sealed class Widget : Window
    {
        const string Layout = @"
<Grid xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' Margin='12'>
 <Grid.Resources>
  <Style TargetType='Button'>
   <Setter Property='Foreground' Value='#9098A6'/><Setter Property='Background' Value='Transparent'/><Setter Property='BorderThickness' Value='0'/><Setter Property='Cursor' Value='Hand'/><Setter Property='Focusable' Value='True'/>
   <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Border x:Name='Hover' Background='{TemplateBinding Background}' CornerRadius='7' Padding='6,2'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Hover' Property='Background' Value='#29303B'/><Setter Property='Foreground' Value='White'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Hover' Property='Background' Value='#29303B'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
  </Style>
 </Grid.Resources>
 <Border Background='#171B23' BorderBrush='#343B47' BorderThickness='1' CornerRadius='18'>
  <Border.Effect><DropShadowEffect BlurRadius='15' ShadowDepth='3' Opacity='0.28'/></Border.Effect>
  <Grid Margin='22,17,22,15'>
   <Grid.RowDefinitions><RowDefinition Height='28'/><RowDefinition Height='30'/><RowDefinition Height='66'/><RowDefinition Height='17'/><RowDefinition Height='29'/></Grid.RowDefinitions>
   <Grid x:Name='Header' Background='Transparent'>
    <StackPanel Orientation='Horizontal' VerticalAlignment='Top'><Ellipse Width='7' Height='7' Fill='#00B8FC' Margin='0,6,8,0' VerticalAlignment='Top'/><TextBlock Text='VOLTHUD' Foreground='#9FA8B7' FontSize='10' FontWeight='SemiBold' VerticalAlignment='Top' Margin='0,2,0,0'/></StackPanel>
    <StackPanel Orientation='Horizontal' HorizontalAlignment='Right' Margin='0,-5,-8,0' VerticalAlignment='Top'>
     <Button x:Name='MenuButton' Content='&#x22EF;' Width='28' Height='25' FontSize='19' ToolTip='Options' AutomationProperties.Name='Options'/>
     <Button x:Name='HideButton' Content='&#x2212;' Width='28' Height='25' FontSize='16' ToolTip='Hide to tray' AutomationProperties.Name='Hide to tray'/>
    </StackPanel>
   </Grid>
   <TextBlock x:Name='DeviceName' Grid.Row='1' Text='Looking for your mouse' Foreground='#E8ECF2' FontSize='15' FontWeight='Medium' TextTrimming='CharacterEllipsis' VerticalAlignment='Center'/>
   <Grid Grid.Row='2'>
    <StackPanel Orientation='Horizontal' VerticalAlignment='Center'><TextBlock x:Name='Level' Text='—' Foreground='#F4F7FC' FontSize='51' FontWeight='SemiBold'/><TextBlock x:Name='Percent' Text='%' Foreground='#8791A2' FontSize='22' Margin='4,0,0,11' VerticalAlignment='Bottom'/></StackPanel>
    <Canvas x:Name='DeviceIcon' HorizontalAlignment='Right' VerticalAlignment='Center' Width='72' Height='68' Margin='0,2,-4,0'>
     <Path Stroke='#677385' StrokeThickness='1.6' Data='M 26,2 C 12,2 7,12 7,25 L 7,39 C 7,53 15,60 26,60 C 38,60 46,53 46,39 L 46,25 C 46,12 41,2 26,2 Z'/>
     <Path Stroke='#677385' StrokeThickness='1.3' Data='M 26,3 L 26,27 M 8,27 L 45,27'/>
     <Border Width='4' Height='12' CornerRadius='2' Background='#00B8FC' Canvas.Left='24' Canvas.Top='10'/>
    </Canvas>
   </Grid>
   <Grid Grid.Row='3' VerticalAlignment='Center' Height='5'><Border Background='#303743' CornerRadius='2.5'/><Border x:Name='BatteryBar' Background='#00B8FC' CornerRadius='2.5' HorizontalAlignment='Left' Width='0'/></Grid>
   <Grid Grid.Row='4' VerticalAlignment='Bottom'>
    <StackPanel Orientation='Horizontal'><Ellipse x:Name='StateDot' Fill='#788391' Height='5' Width='5' Margin='0,0,6,0'/><TextBlock x:Name='State' Text='Checking connection' Foreground='#A0AABA' FontSize='11'/></StackPanel>
    <Button x:Name='RefreshButton' Content='Refresh' Foreground='#778395' FontSize='10' HorizontalAlignment='Right' Padding='0' Margin='0,0,-6,-4' ToolTip='Check battery now' AutomationProperties.Name='Refresh battery'/>
   </Grid>
  </Grid>
 </Border>
</Grid>";
        readonly Grid root;
        readonly TextBlock deviceName, level, state;
        readonly Border bar;
        readonly System.Windows.Shapes.Ellipse dot;
        readonly Forms.NotifyIcon tray;
        readonly DispatcherTimer timer;
        DispatcherTimer activationTimer;
        readonly string settingsPath;
        readonly MenuItem pin;
        readonly MenuItem devices;
        List<BatteryReading> readings = new List<BatteryReading>();
        bool busy, exiting;
        int selected;
        public Widget()
        {
            Title = "VoltHUD"; Width = 346; Height = 248;
            using (var iconStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("VoltHUD.AppIcon.ico"))
            {
                var decoder = BitmapDecoder.Create(iconStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                Icon = decoder.Frames[decoder.Frames.Count - 1]; Icon.Freeze();
            }
            ResizeMode = ResizeMode.NoResize; WindowStyle = WindowStyle.None;
            AllowsTransparency = true; Background = Brushes.Transparent;
            ShowInTaskbar = false; FontFamily = new FontFamily("Segoe UI");
            root = (Grid)XamlReader.Parse(Layout); Content = root;
            deviceName = (TextBlock)root.FindName("DeviceName"); level = (TextBlock)root.FindName("Level");
            state = (TextBlock)root.FindName("State"); bar = (Border)root.FindName("BatteryBar");
            dot = (System.Windows.Shapes.Ellipse)root.FindName("StateDot");
            settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VoltHUD", "settings.txt");
            Left = SystemParameters.WorkArea.Right - Width - 24; Top = SystemParameters.WorkArea.Bottom - Height - 24;
            LoadSettings();
            var menu = new ContextMenu();
            menu.Items.Add(Item("Refresh battery", delegate { Refresh(); }));
            devices = new MenuItem { Header = "Devices", IsEnabled = false }; menu.Items.Add(devices);
            pin = Item("Always on top", delegate { Topmost = pin.IsChecked; SaveSettings(); });
            pin.IsCheckable = true; pin.IsChecked = Topmost; menu.Items.Add(pin);
            menu.Items.Add(new Separator());
            menu.Items.Add(Item("About VoltHUD", delegate { MessageBox.Show(this, "VoltHUD 0.1.1 preview\n\nBattery levels for Logitech and Bluetooth peripherals.\nNo internet access or telemetry.\n\nDrag the header to move. Hide to keep it in the tray.\nOptions → Devices switches between peripherals.\nBattery refreshes every 60 seconds.\n\nIndependent project; not affiliated with Logitech.", "About VoltHUD", MessageBoxButton.OK, MessageBoxImage.Information); }));
            menu.Items.Add(Item("Quit", delegate { Exit(); }));
            root.ContextMenu = menu;
            ((Button)root.FindName("MenuButton")).Click += delegate { menu.PlacementTarget = (Button)root.FindName("MenuButton"); menu.IsOpen = true; };
            ((Button)root.FindName("HideButton")).Click += delegate { SaveSettings(); Hide(); };
            ((Button)root.FindName("RefreshButton")).Click += delegate { Refresh(); };
            ((Grid)root.FindName("Header")).MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) { if (e.OriginalSource is TextBlock || e.OriginalSource is Grid || e.OriginalSource is System.Windows.Shapes.Ellipse) { DragMove(); ClampPosition(); SaveSettings(); } };
            tray = new Forms.NotifyIcon { Text = "VoltHUD — checking battery", Icon = MakeIcon(), Visible = true };
            var trayMenu = new Forms.ContextMenuStrip();
            trayMenu.Items.Add("Show widget", null, delegate { ShowWidget(); });
            trayMenu.Items.Add("Refresh battery", null, delegate { Dispatcher.BeginInvoke(new Action(Refresh)); });
            trayMenu.Items.Add("Quit", null, delegate { Dispatcher.BeginInvoke(new Action(Exit)); });
            tray.ContextMenuStrip = trayMenu;
            tray.DoubleClick += delegate { Dispatcher.BeginInvoke(new Action(ShowWidget)); };
            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) }; timer.Tick += delegate { Refresh(); };
            Loaded += delegate { ClampPosition(); timer.Start(); Refresh(); };
            Closing += delegate { if (!exiting) { SaveSettings(); } timer.Stop(); if (activationTimer != null) activationTimer.Stop(); tray.Visible = false; tray.Dispose(); };
            KeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) Hide(); if (e.Key == Key.F5) Refresh(); };
        }
        static MenuItem Item(string name, Action action) { var item = new MenuItem { Header = name }; item.Click += delegate { action(); }; return item; }
        public void ListenForActivation(EventWaitHandle signal)
        {
            activationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            activationTimer.Tick += delegate { if (signal.WaitOne(0)) ShowWidget(); };
            activationTimer.Start();
        }
        void ShowWidget() { Show(); WindowState = WindowState.Normal; Activate(); }
        void Exit() { exiting = true; SaveSettings(); Close(); Application.Current.Shutdown(); }
        async void Refresh()
        {
            if (busy) return;
            busy = true;
            var refresh = (Button)root.FindName("RefreshButton"); refresh.IsEnabled = false; refresh.Content = "Checking…";
            try
            {
                var latest = await Task.Run(() => BatteryMonitor.Scan());
                if (exiting) return;
                string previous = readings.Count > selected ? readings[selected].Key : null;
                readings = latest; int same = readings.FindIndex(r => r.Key == previous);
                selected = same >= 0 ? same : 0;
                devices.Items.Clear(); devices.IsEnabled = readings.Count > 0;
                for (int i = 0; i < readings.Count; i++)
                {
                    int index = i; var r = readings[i];
                    devices.Items.Add(Item(r.Name + " · " + (r.Percent.HasValue ? r.Percent + "%" : r.Detail ?? "unknown"), delegate { selected = index; Display(); }));
                }
                Display();
            }
            catch { state.Text = "Unable to read battery · try Refresh"; }
            finally { busy = false; refresh.IsEnabled = true; refresh.Content = "Refresh"; }
        }
        void Display()
        {
            if (readings.Count == 0)
            {
                deviceName.Text = "No battery reading"; level.Text = "—"; bar.Width = 0;
                state.Text = "Wake your device or connect its receiver";
                state.FontSize = 9; dot.Fill = Brush("#788391"); tray.Text = "VoltHUD — no battery reading";
                ((TextBlock)root.FindName("Percent")).Visibility = Visibility.Collapsed;
                return;
            }
            var r = readings[selected];
            deviceName.Text = r.Name; deviceName.ToolTip = readings.Count > 1 ? "Use Options → Devices to switch peripherals" : r.Name;
            level.Text = r.Percent.HasValue ? r.Percent.Value.ToString(CultureInfo.InvariantCulture) : "—";
            ((TextBlock)root.FindName("Percent")).Visibility = r.Percent.HasValue ? Visibility.Visible : Visibility.Collapsed;
            var color = r.Percent.HasValue && r.Percent <= 15 ? Brush("#FFAA7A") : Brush("#00B8FC");
            bar.Background = color; bar.Width = r.Percent.HasValue ? 278.0 * r.Percent.Value / 100.0 : 0;
            dot.Fill = r.Percent.HasValue ? color : Brush("#788391"); state.FontSize = 11; state.Text = r.State;
            state.ToolTip = "Updated " + r.Updated.ToString("T") + (r.Detail == null ? "" : "\n" + r.Detail);
            DrawDevice(DeviceKind.Resolve(r.Name, r.Kind));
            string text = "VoltHUD · " + r.Name + " · " + (r.Percent.HasValue ? r.Percent + "%" : "unknown");
            tray.Text = text.Length > 63 ? text.Substring(0, 63) : text;
            Title = text;
            AutomationPropertiesSet(r);
        }
        void DrawDevice(string kind)
        {
            var canvas = (Canvas)root.FindName("DeviceIcon"); canvas.Children.Clear();
            canvas.Children.Add(new Image { Source = DeviceIcons.Get(kind), Width = 72, Height = 68, Stretch = Stretch.Uniform });
        }
        void AutomationPropertiesSet(BatteryReading r)
        {
            System.Windows.Automation.AutomationProperties.SetName(level, r.Percent.HasValue ? r.Percent + " percent battery" : "Battery level unknown");
        }
        static SolidColorBrush Brush(string value) { return (SolidColorBrush)new BrushConverter().ConvertFromString(value); }
        void ClampPosition()
        {
            var rect = new System.Drawing.Rectangle((int)Left, (int)Top, (int)Width, (int)Height);
            bool visible = false;
            foreach (var screen in Forms.Screen.AllScreens) if (screen.WorkingArea.IntersectsWith(rect)) visible = true;
            if (!visible) { Left = SystemParameters.WorkArea.Right - Width - 24; Top = SystemParameters.WorkArea.Bottom - Height - 24; }
        }
        void LoadSettings()
        {
            try
            {
                var lines = File.ReadAllLines(settingsPath); double x, y;
                if (lines.Length >= 3 && Double.TryParse(lines[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x) && Double.TryParse(lines[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y) && !Double.IsNaN(x) && !Double.IsNaN(y) && !Double.IsInfinity(x) && !Double.IsInfinity(y)) { Left = x; Top = y; Topmost = lines[2] == "True"; }
            }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        void SaveSettings()
        {
            try { Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)); File.WriteAllLines(settingsPath, new[] { Left.ToString(CultureInfo.InvariantCulture), Top.ToString(CultureInfo.InvariantCulture), Topmost.ToString() }); }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        public void RenderPreview(string file)
        {
            readings.Add(new BatteryReading { Name = "Logitech PRO X 2", Percent = 77, State = "On battery", Kind = "mouse", Updated = DateTime.Now }); Display();
            root.Measure(new Size(Width, Height)); root.Arrange(new Rect(0, 0, Width, Height)); root.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)Width * 2, (int)Height * 2, 192, 192, PixelFormats.Pbgra32); bitmap.Render(root);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(file)) encoder.Save(stream);
            tray.Dispose();
        }
        static System.Drawing.Icon MakeIcon()
        {
            using (var bitmap = new System.Drawing.Bitmap(32, 32))
            using (var g = System.Drawing.Graphics.FromImage(bitmap))
            using (var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(0, 184, 252), 3))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(System.Drawing.Color.Transparent); g.DrawRectangle(pen, 3, 8, 23, 16); g.FillRectangle(System.Drawing.Brushes.White, 28, 12, 2, 8);
                using (var fill = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(0, 184, 252))) g.FillRectangle(fill, 7, 12, 15, 8);
                IntPtr native = bitmap.GetHicon();
                try { return (System.Drawing.Icon)System.Drawing.Icon.FromHandle(native).Clone(); }
                finally { DestroyIcon(native); }
            }
        }
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr handle);
    }
    public static class Program
    {
        [STAThread] public static void Main(string[] args)
        {
            try
            {
                if (args.Length == 2 && args[0] == "--diagnose")
                {
                    var lines = new List<string> { "VoltHUD 0.1.1 battery diagnostic", "No serial numbers or device paths collected." };
                    BatteryMonitor.Log = lines.Add; var result = BatteryMonitor.Scan();
                    lines.Add("Peripherals found: " + result.Count); File.WriteAllLines(args[1], lines); return;
                }
                bool created;
                using (var mutex = new Mutex(true, "Local\\VoltHUD.Instance", out created))
                using (var activate = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\VoltHUD.Show"))
                {
                    if (!created) { activate.Set(); return; }
                    var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
                    var widget = new Widget();
                    if (args.Length == 2 && args[0] == "--preview") { widget.RenderPreview(args[1]); return; }
                    if (Array.IndexOf(args, "--windowed") >= 0) widget.ShowInTaskbar = true;
                    widget.ListenForActivation(activate);
                    app.Run(widget);
                }
            }
            catch (Exception ex) { MessageBox.Show("VoltHUD couldn't start.\n\n" + ex.Message, "VoltHUD", MessageBoxButton.OK, MessageBoxImage.Error); }
        }
    }
}
