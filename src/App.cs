using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Interop;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

[assembly: AssemblyTitle("VoltHUD")]
[assembly: AssemblyDescription("A quiet battery widget for connected peripherals")]
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
   <Setter Property='Foreground' Value='{DynamicResource C9098A6}'/><Setter Property='Background' Value='Transparent'/><Setter Property='BorderThickness' Value='0'/><Setter Property='Cursor' Value='Hand'/><Setter Property='Focusable' Value='True'/>
   <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Border x:Name='Hover' Background='{TemplateBinding Background}' CornerRadius='7' Padding='6,2'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Hover' Property='Background' Value='{DynamicResource C29303B}'/><Setter Property='Foreground' Value='{DynamicResource CFFFFFF}'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Hover' Property='Background' Value='{DynamicResource C29303B}'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
  </Style>
 </Grid.Resources>
 <Border Background='{DynamicResource C171B23}' BorderBrush='{DynamicResource C343B47}' BorderThickness='1' CornerRadius='18'>
  <Border.Effect><DropShadowEffect BlurRadius='15' ShadowDepth='3' Opacity='0.28'/></Border.Effect>
  <Grid Margin='22,17,22,15'>
   <Grid.RowDefinitions><RowDefinition Height='28'/><RowDefinition Height='Auto'/><RowDefinition Height='25'/><RowDefinition Height='22'/></Grid.RowDefinitions>
   <Grid x:Name='Header' Background='Transparent'>
    <StackPanel Orientation='Horizontal' VerticalAlignment='Top'><Ellipse Width='7' Height='7' Fill='{DynamicResource C00B8FC}' Margin='0,6,8,0' VerticalAlignment='Top'/><TextBlock x:Name='BrandCaption' Text='VOLTHUD' Foreground='{DynamicResource C9FA8B7}' FontSize='10' FontWeight='SemiBold' VerticalAlignment='Top' Margin='0,2,0,0'/></StackPanel>
    <StackPanel Orientation='Horizontal' HorizontalAlignment='Right' Margin='0,-5,-8,0' VerticalAlignment='Top'>
     <Button x:Name='MenuButton' Content='&#x22EF;' Width='28' Height='25' FontSize='19' ToolTip='Options' AutomationProperties.Name='Options'/>
     <Button x:Name='HideButton' Content='&#x2212;' Width='28' Height='25' FontSize='16' ToolTip='Hide to tray' AutomationProperties.Name='Hide to tray'/>
    </StackPanel>
   </Grid>
   <StackPanel x:Name='DeviceRows' Grid.Row='1'/>
   <Grid Grid.Row='2' VerticalAlignment='Bottom'>
    <TextBlock x:Name='Summary' Text='Checking devices' Foreground='{DynamicResource CA0AABA}' FontSize='10'/>
    <Button x:Name='RefreshButton' Content='Refresh' Foreground='{DynamicResource C778395}' FontSize='10' HorizontalAlignment='Right' Padding='0' Margin='0,0,-6,-4' ToolTip='Check battery now' AutomationProperties.Name='Refresh battery'/>
   </Grid>
   <TextBlock Grid.Row='3' Text='Battery % updates every 5 minutes' Foreground='{DynamicResource C929EAE}' FontSize='10' HorizontalAlignment='Center' VerticalAlignment='Bottom'/>
  </Grid>
 </Border>
</Grid>";
        readonly Grid root;
        readonly Grid surface;
        readonly StackPanel deviceRows;
        readonly TextBlock state;
        readonly Forms.NotifyIcon tray;
        readonly DispatcherTimer timer;
        DispatcherTimer activationTimer;
        readonly string settingsPath;
        readonly string alertHistoryPath;
        bool trayDisposed;
        Border settingsPanel;
        Popup settingsPopup;
        List<BatteryReading> readings = new List<BatteryReading>();
        bool busy, exiting, monitoringStarted;
        bool lowBatteryNotifications;
        readonly LowBatteryAlerts lowBatteryAlerts = new LowBatteryAlerts();
        public bool StartMinimized { get; private set; }
        double widgetScale = 1.0;
        readonly bool demoMode;
        string themeMode = "Dark";
        UserPreferenceChangedEventHandler themeChanged;
        DateTime settingsButtonDismissedAt = DateTime.MinValue;
        DispatcherTimer discoveryTimer;
        int discoveryAttempts;
        HwndSource deviceSource;
        int themeRefreshPending;
        public Widget(bool demo = false)
        {
            demoMode = demo;
            Title = "VoltHUD"; Width = 346; Height = 222;
            using (var iconStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("VoltHUD.AppIcon.ico"))
            {
                var decoder = BitmapDecoder.Create(iconStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                Icon = decoder.Frames[decoder.Frames.Count - 1]; Icon.Freeze();
            }
            ResizeMode = ResizeMode.NoResize; WindowStyle = WindowStyle.None;
            AllowsTransparency = true; Background = Brushes.Transparent;
            ShowInTaskbar = false; FontFamily = new FontFamily("Segoe UI");
            root = (Grid)XamlReader.Parse(Layout);
            surface = new Grid { Width = 346, Height = 222 };
            surface.Children.Add(root);
            Content = new Viewbox { Child = surface, Stretch = Stretch.Uniform };
            if (demoMode) ((TextBlock)root.FindName("BrandCaption")).Text = "VOLTHUD · SIMULATION";
            deviceRows = (StackPanel)root.FindName("DeviceRows");
            state = (TextBlock)root.FindName("Summary");
            settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VoltHUD", "settings.txt");
            alertHistoryPath = Path.Combine(Path.GetDirectoryName(settingsPath), "low-battery-alerts.txt");
            if (!demoMode)
            {
                try { lowBatteryAlerts.Restore(File.ReadLines(alertHistoryPath)); }
                catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
            Left = SystemParameters.WorkArea.Right - Width - 24; Top = SystemParameters.WorkArea.Bottom - Height - 24;
            if (!demoMode) LoadSettings();
            deviceRows.Children.Add(new TextBlock { Text = "Detecting connected devices…", Height = 88, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, Foreground = Brush("#A0AABA"), FontSize = 12 }); UpdateWidgetSize();
            ((Button)root.FindName("MenuButton")).Click += delegate { ToggleSettings(); };
            root.MouseRightButtonUp += delegate(object sender, MouseButtonEventArgs e) { OpenSettings(); e.Handled = true; };
            ((Button)root.FindName("HideButton")).Click += delegate { if (settingsPopup != null) settingsPopup.IsOpen = false; SaveSettings(); Hide(); };
            ((Button)root.FindName("RefreshButton")).Click += delegate { Refresh(); };
            ((Grid)root.FindName("Header")).MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) { if (e.OriginalSource is TextBlock || e.OriginalSource is Grid || e.OriginalSource is System.Windows.Shapes.Ellipse) { DragMove(); ClampPosition(); SaveSettings(); } };

            tray = new Forms.NotifyIcon { Text = "VoltHUD — checking battery", Icon = MakeIcon(), Visible = true };
            tray.MouseUp += delegate(object sender, Forms.MouseEventArgs e) { if (e.Button == Forms.MouseButtons.Right) Dispatcher.BeginInvoke(new Action(delegate { ShowWidget(); OpenSettings(); })); };
            tray.DoubleClick += delegate { Dispatcher.BeginInvoke(new Action(ShowWidget)); };
            tray.BalloonTipClicked += delegate { Dispatcher.BeginInvoke(new Action(ShowWidget)); };
            timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) }; timer.Tick += delegate { Refresh(); };
            discoveryTimer = new DispatcherTimer();
            discoveryTimer.Tick += delegate { discoveryTimer.Stop(); if (busy) { ScheduleDiscovery(1); return; } Refresh(); };
            SourceInitialized += delegate
            {
                deviceSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
                if (deviceSource != null) deviceSource.AddHook(DeviceMessages);
            };
            themeChanged = delegate { QueueWindowsThemeRefresh(); };
            SystemEvents.UserPreferenceChanged += themeChanged;
            ApplyTheme();
            Loaded += delegate { StartMonitoring(); };
            Closing += delegate { exiting = true; if (settingsPopup != null) settingsPopup.IsOpen = false; SaveSettings(); SaveAlertHistory(); timer.Stop(); discoveryTimer.Stop(); SystemEvents.UserPreferenceChanged -= themeChanged; if (deviceSource != null) deviceSource.RemoveHook(DeviceMessages); if (activationTimer != null) activationTimer.Stop(); DisposeTray(); };
            KeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) Hide(); if (e.Key == Key.F5) Refresh(); };
        }
        void EnsureSettings()
        {
            if (settingsPanel != null) return;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("VoltHUD.Settings.xaml")) settingsPanel = (Border)XamlReader.Load(stream);
            foreach (string mode in new[] { "Dark", "Light", "System" })
            {
                var choice = (RadioButton)settingsPanel.FindName("Theme" + mode);
                choice.IsChecked = themeMode == mode;
                choice.Checked += delegate { themeMode = mode; ApplyTheme(); SaveSettings(); };
            }
            var optionsButton = (Button)root.FindName("MenuButton");
            settingsPopup = new Popup { Child = settingsPanel, PlacementTarget = optionsButton, Placement = PlacementMode.Bottom, AllowsTransparency = true, StaysOpen = false, HorizontalOffset = -250, VerticalOffset = 4 };
            settingsPopup.Opened += delegate { ((RadioButton)settingsPanel.FindName("Theme" + themeMode)).Focus(); };
            var opacitySlider = (Slider)settingsPanel.FindName("OpacitySlider");
            var opacityValue = (TextBlock)settingsPanel.FindName("OpacityValue");
            opacitySlider.Value = Opacity * 100; opacityValue.Text = Math.Round(opacitySlider.Value) + "%";
            opacitySlider.ValueChanged += delegate { Opacity = opacitySlider.Value / 100.0; opacityValue.Text = Math.Round(opacitySlider.Value) + "%"; SaveSettings(); };
            var sizeSlider = (Slider)settingsPanel.FindName("SizeSlider");
            var sizeValue = (TextBlock)settingsPanel.FindName("SizeValue");
            sizeSlider.Value = widgetScale * 100; sizeValue.Text = Math.Round(sizeSlider.Value) + "%";
            sizeSlider.ValueChanged += delegate { widgetScale = sizeSlider.Value / 100.0; UpdateWidgetSize(); sizeValue.Text = Math.Round(sizeSlider.Value) + "%"; SaveSettings(); };
            var pin = (CheckBox)settingsPanel.FindName("AlwaysOnTop");
            pin.IsChecked = Topmost; pin.Click += delegate { Topmost = pin.IsChecked == true; SaveSettings(); };
            var startup = (CheckBox)settingsPanel.FindName("StartWithWindows");
            try { startup.IsChecked = StartupRegistration.Enabled; } catch { startup.IsEnabled = false; }
            if (demoMode) startup.IsEnabled = false;
            startup.Click += delegate
            {
                try { StartupRegistration.SetEnabled(startup.IsChecked == true); }
                catch { startup.IsChecked = !startup.IsChecked; ShowSettingsMessage("Windows couldn't change this startup setting. Your other settings are saved."); }
            };
            var minimized = (CheckBox)settingsPanel.FindName("StartInTray");
            minimized.IsChecked = StartMinimized; minimized.Click += delegate { StartMinimized = minimized.IsChecked == true; SaveSettings(); };
            var notifications = (CheckBox)settingsPanel.FindName("LowBatteryNotifications");
            notifications.IsChecked = lowBatteryNotifications;
            notifications.IsEnabled = !demoMode;
            notifications.Click += delegate { lowBatteryNotifications = notifications.IsChecked == true; SaveSettings(); NotifyLowBattery(); };
            ((Button)settingsPanel.FindName("CloseSettings")).Click += delegate { settingsPopup.IsOpen = false; optionsButton.Focus(); };
            ((Button)settingsPanel.FindName("RefreshNow")).Click += delegate { Refresh(); };
            ((Button)settingsPanel.FindName("AboutButton")).Click += delegate { ShowSettingsMessage("VoltHUD 0.1.1 preview\nAll your connected devices, together.\nBattery levels refresh every 5 minutes.\nNo internet access or telemetry.\nIndependent project."); };
            ((Button)settingsPanel.FindName("QuitButton")).Click += delegate { settingsPopup.IsOpen = false; Exit(); };
            settingsPanel.PreviewKeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) { settingsPopup.IsOpen = false; optionsButton.Focus(); e.Handled = true; } };
            settingsPopup.Closed += delegate(object sender, EventArgs e)
            {
                var point = Mouse.GetPosition(optionsButton);
                if (Mouse.LeftButton == MouseButtonState.Pressed && point.X >= 0 && point.Y >= 0 && point.X <= optionsButton.ActualWidth && point.Y <= optionsButton.ActualHeight) settingsButtonDismissedAt = DateTime.UtcNow;
                // Popup controls and their backing window are needed only while settings is open.
                var closed = (Popup)sender; closed.Child = null;
                if (Object.ReferenceEquals(settingsPopup, closed)) { settingsPopup = null; settingsPanel = null; }
            };
            ThemePalette.Apply(settingsPanel);

        }
        void OpenSettings() { EnsureSettings(); settingsPopup.IsOpen = true; }
        void ToggleSettings()
        {
            bool justDismissed = DateTime.UtcNow - settingsButtonDismissedAt < TimeSpan.FromMilliseconds(500);
            settingsButtonDismissedAt = DateTime.MinValue;
            if (justDismissed) return;
            EnsureSettings(); settingsPopup.IsOpen = !settingsPopup.IsOpen;
        }
        void ShowSettingsMessage(string text) { var message = (TextBlock)settingsPanel.FindName("SettingsMessage"); message.Text = text; message.Visibility = Visibility.Visible; }
        void ApplyTheme()
        {
            ThemePalette.Configure(themeMode);
            ThemePalette.Apply(root); if (settingsPanel != null) ThemePalette.Apply(settingsPanel);
            if (readings.Count == 0 && (!monitoringStarted || busy))
            {
                deviceRows.Children.Clear();
                deviceRows.Children.Add(new TextBlock { Text = "Detecting connected devices…", Height = 88, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, Foreground = Brush("#A0AABA"), FontSize = 12 });
            }
            else BuildRows(deviceRows, readings);
        }
        void QueueWindowsThemeRefresh()
        {
            if (exiting || themeMode != "System" || Dispatcher.HasShutdownStarted || Interlocked.Exchange(ref themeRefreshPending,1) != 0) return;
            Dispatcher.BeginInvoke(new Action(delegate { Interlocked.Exchange(ref themeRefreshPending,0); if (!exiting && themeMode == "System") ApplyTheme(); }));
        }
        void ScheduleDiscovery(int seconds)
        {
            if (demoMode || exiting) return;
            discoveryTimer.Stop(); discoveryTimer.Interval = TimeSpan.FromSeconds(seconds); discoveryTimer.Start();
        }
        IntPtr DeviceMessages(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message == 0x0219) { discoveryAttempts = 0; ScheduleDiscovery(2); }
            if (message == 0x0320 || message == 0x001A) QueueWindowsThemeRefresh();
            return IntPtr.Zero;
        }
        void StartMonitoring()
        {
            if (monitoringStarted) return;
            monitoringStarted = true; new WindowInteropHelper(this).EnsureHandle(); ClampPosition(); timer.Start(); Refresh();
        }
        public void StartInTray() { StartMonitoring(); }
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
                var latest = demoMode ? SampleDevices() : await Task.Run(() => BatteryMonitor.Scan());
                if (exiting) return;
                bool changed = !SameVisibleReadings(readings, latest);
                readings = latest;
                NotifyLowBattery();
                if (changed || state.Text == "Checking devices") Display();
                bool incomplete = latest.Count == 0 || latest.Exists(r => r.NeedsIdentity);
                if (incomplete && discoveryAttempts < 5) { int[] delays = { 2, 5, 10, 20, 30 }; ScheduleDiscovery(delays[discoveryAttempts++]); }
                else if (!incomplete) { discoveryAttempts = 0; discoveryTimer.Stop(); }
            }
            catch { state.Text = "Unable to read battery · try Refresh"; }
            finally { busy = false; refresh.IsEnabled = true; refresh.Content = "Refresh"; }
        }
        void NotifyLowBattery()
        {
            if (demoMode || exiting) return;
            var alerts = lowBatteryAlerts.Evaluate(readings, lowBatteryNotifications);
            SaveAlertHistory();
            if (alerts.Count == 0) return;
            string message = "";
            foreach (var reading in alerts)
            {
                string line = reading.Name + " is at " + reading.Percent.Value + "%.";
                if (message.Length + line.Length > 210) { message += "\nMore devices have low battery. Open VoltHUD to view them."; break; }
                message += (message.Length == 0 ? "" : "\n") + line;
            }
            ShowLowBatteryNotification(message);
        }
        bool ShowLowBatteryNotification(string message)
        {
            if (exiting || trayDisposed) return false;
            try { tray.ShowBalloonTip(10000, "VoltHUD · Low battery", message, Forms.ToolTipIcon.Warning); return true; }
            catch (System.ComponentModel.Win32Exception) { return false; }
            catch (InvalidOperationException) { return false; }
        }
        void Display()
        {
            BuildRows(deviceRows, readings);
            UpdateWidgetSize();
            state.Text = readings.Count == 0 ? "No connected devices found" : readings.Count + (demoMode ? " sample devices" : readings.Count == 1 ? " connected device" : " connected devices");
            string text = (demoMode ? "VoltHUD simulation · " : "VoltHUD · ") + readings.Count + " devices";
            if (readings.Count == 1) text = "VoltHUD · " + readings[0].Name + " · " + (readings[0].Percent.HasValue ? readings[0].Percent + "%" : "unknown");
            tray.Text = text.Length > 63 ? text.Substring(0, 63) : text; Title = text;
        }
        void SaveAlertHistory()
        {
            if (demoMode || !lowBatteryAlerts.Changed) return;
            string temporary = alertHistoryPath + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(alertHistoryPath));
                File.WriteAllLines(temporary, lowBatteryAlerts.Export());
                if (File.Exists(alertHistoryPath)) File.Replace(temporary, alertHistoryPath, null);
                else File.Move(temporary, alertHistoryPath);
                lowBatteryAlerts.MarkSaved();
            }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        void DisposeTray()
        {
            if (trayDisposed) return;
            trayDisposed = true;
            tray.Visible = false;
            var icon = tray.Icon;
            tray.Dispose();
            if (icon != null) icon.Dispose();
        }
        static bool SameVisibleReadings(List<BatteryReading> left, List<BatteryReading> right)
        {
            if (left.Count != right.Count) return false;
            for (int i = 0; i < left.Count; i++)
            {
                var a = left[i]; var b = right[i];
                if (a.Key != b.Key || a.Name != b.Name || a.Kind != b.Kind || a.Percent != b.Percent || a.State != b.State || a.Detail != b.Detail) return false;
            }
            return true;
        }
        void UpdateWidgetSize()
        {
            double naturalHeight = 134 + 88 * Math.Max(1, readings.Count);
            surface.Height = naturalHeight;
            double scale = Math.Min(widgetScale, Math.Max(0.2, (SystemParameters.WorkArea.Height - 36) / naturalHeight));
            Width = 346 * scale; Height = naturalHeight * scale;
            if (Top + Height > SystemParameters.WorkArea.Bottom - 12) Top = Math.Max(SystemParameters.WorkArea.Top, SystemParameters.WorkArea.Bottom - Height - 12);
        }
        static void BuildRows(StackPanel panel, List<BatteryReading> items)
        {
            panel.Children.Clear();
            if (items.Count == 0)
            {
                panel.Children.Add(CreateDeviceRow(new BatteryReading { Name = "No devices connected", State = "Wake your device or connect its receiver", Kind = "device" }));
                return;
            }
            foreach (var reading in items) panel.Children.Add(CreateDeviceRow(reading));
        }
        static Border CreateDeviceRow(BatteryReading reading)
        {
            var row = new Grid { Margin = new Thickness(0, 10, 0, 10), VerticalAlignment = VerticalAlignment.Center };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.RowDefinitions.Add(new RowDefinition { Height = new GridLength(25) });
            row.RowDefinitions.Add(new RowDefinition { Height = new GridLength(19) });
            row.RowDefinitions.Add(new RowDefinition { Height = new GridLength(13) });
            var image = new Image { Source = DeviceIcons.Get(DeviceKind.Resolve(reading.Name, reading.Kind)), Width = 46, Height = 54, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Left };
            Grid.SetRowSpan(image, 3); row.Children.Add(image);
            var name = new TextBlock { Text = reading.Name, FontSize = 12.5, FontWeight = FontWeights.Medium, Foreground = Brush("#E8ECF2"), Margin = new Thickness(0, 3, 54, 0), TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = reading.Name };
            Grid.SetColumn(name, 1); row.Children.Add(name);
            var color = reading.Percent.HasValue && reading.Percent.Value <= 15 ? Brush("#FFAA7A") : Brush("#00B8FC");
            var percent = new TextBlock { Text = reading.Percent.HasValue ? reading.Percent + "%" : "—", FontSize = 20, FontWeight = FontWeights.SemiBold, Foreground = reading.Percent.HasValue ? Brush("#F4F7FC") : Brush("#8791A2"), HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetColumn(percent, 1); row.Children.Add(percent);
            var status = new TextBlock { Text = reading.State, FontSize = 10.5, Foreground = Brush("#A0AABA"), TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = reading.Detail ?? reading.State };
            Grid.SetColumn(status, 1); Grid.SetRow(status, 1); row.Children.Add(status);
            var track = new Grid { Height = 4, VerticalAlignment = VerticalAlignment.Center, ClipToBounds = true };
            track.Children.Add(new Border { Background = Brush("#303743"), CornerRadius = new CornerRadius(2) });
            track.Children.Add(new Border { Background = color, CornerRadius = new CornerRadius(2), Width = reading.Percent.HasValue ? 220 * reading.Percent.Value / 100.0 : 0, HorizontalAlignment = HorizontalAlignment.Left });
            Grid.SetColumn(track, 1); Grid.SetRow(track, 2); row.Children.Add(track);
            var border = new Border { Height = 88, BorderBrush = Brush("#2C333E"), BorderThickness = new Thickness(0, 0, 0, 1), Child = row };
            System.Windows.Automation.AutomationProperties.SetName(border, reading.Name + ", " + (reading.Percent.HasValue ? reading.Percent + " percent" : "battery unavailable") + ", " + reading.State);
            return border;
        }
        static SolidColorBrush Brush(string value) { return ThemePalette.Brush(value); }
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
                double savedOpacity;
                if (lines.Length >= 4 && Double.TryParse(lines[3], NumberStyles.Float, CultureInfo.InvariantCulture, out savedOpacity) && !Double.IsNaN(savedOpacity) && !Double.IsInfinity(savedOpacity)) Opacity = Math.Max(0.2, Math.Min(1.0, savedOpacity));
                double savedScale;
                if (lines.Length >= 5 && Double.TryParse(lines[4], NumberStyles.Float, CultureInfo.InvariantCulture, out savedScale) && !Double.IsNaN(savedScale) && !Double.IsInfinity(savedScale))
                {
                    widgetScale = Math.Max(0.75, Math.Min(2.0, savedScale));
                    Width = 346 * widgetScale; Height = 222 * widgetScale;
                }
                if (lines.Length >= 6) StartMinimized = lines[5] == "True";
                if (lines.Length >= 7 && (lines[6] == "Dark" || lines[6] == "Light" || lines[6] == "System")) themeMode = lines[6];
                  if (lines.Length >= 8) lowBatteryNotifications = lines[7] == "True";
            }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        void SaveSettings()
        {
            if (demoMode) return;
            try { Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)); File.WriteAllLines(settingsPath, new[] { Left.ToString(CultureInfo.InvariantCulture), Top.ToString(CultureInfo.InvariantCulture), Topmost.ToString(), Opacity.ToString(CultureInfo.InvariantCulture), widgetScale.ToString(CultureInfo.InvariantCulture), StartMinimized.ToString(), themeMode, lowBatteryNotifications.ToString() }); }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        public static List<BatteryReading> SampleDevices()
        {
            return new List<BatteryReading> {
                new BatteryReading { Key = "demo-mouse", Name = "Wireless mouse", Kind = "mouse", Percent = 77, State = "On battery", Updated = DateTime.Now },
                new BatteryReading { Key = "demo-keyboard", Name = "Wireless keyboard", Kind = "keyboard", Percent = 64, State = "On battery", Updated = DateTime.Now },
                new BatteryReading { Key = "demo-headset", Name = "Wireless gaming headset", Kind = "headset", Percent = 43, State = "Charging", Charging = true, Updated = DateTime.Now },
                new BatteryReading { Key = "demo-controller", Name = "Wireless controller", Kind = "controller", Percent = 28, State = "Bluetooth · battery level", Updated = DateTime.Now },
                new BatteryReading { Key = "demo-speaker", Name = "Bluetooth speaker", Kind = "speaker", Percent = 12, State = "Bluetooth · battery level", Updated = DateTime.Now },
                new BatteryReading { Key = "demo-mic", Name = "Wireless microphone", Kind = "microphone", Percent = 91, State = "On battery", Updated = DateTime.Now },
                new BatteryReading { Key = "demo-camera", Name = "Bluetooth camera", Kind = "webcam", Percent = null, State = "Battery unavailable", Detail = "This sample device does not report a battery level.", Updated = DateTime.Now },
                new BatteryReading { Key = "demo-unknown", Name = "Unknown Bluetooth device", Kind = "device", Percent = null, State = "Battery unavailable", Updated = DateTime.Now }
            };
        }
        public void RenderPreview(string file)
        {
            var preview = (Grid)XamlReader.Parse(Layout);
            ThemePalette.Apply(preview);
            preview.SetValue(System.Windows.Documents.TextElement.FontFamilyProperty, new FontFamily("Segoe UI"));
            var samples = SampleDevices();
            BuildRows((StackPanel)preview.FindName("DeviceRows"), samples);
            ((TextBlock)preview.FindName("BrandCaption")).Text = "VOLTHUD · SIMULATION";
            ((TextBlock)preview.FindName("Summary")).Text = samples.Count + " sample devices";
            double height = 134 + 88 * samples.Count;
            preview.Measure(new Size(346, height)); preview.Arrange(new Rect(0, 0, 346, height)); preview.UpdateLayout();
            var bitmap = new RenderTargetBitmap(692, (int)height * 2, 192, 192, PixelFormats.Pbgra32); bitmap.Render(preview);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(file)) encoder.Save(stream);
            DisposeTray();
        }
        public void RenderSettingsPreview(string file, string previewTheme = null)
        {
            ThemePalette.Configure(previewTheme ?? themeMode);
            Border preview;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("VoltHUD.Settings.xaml")) preview = (Border)XamlReader.Load(stream);
            ThemePalette.Apply(preview);
            ((RadioButton)preview.FindName("Theme" + (previewTheme ?? themeMode))).IsChecked = true;
            ((Slider)preview.FindName("OpacitySlider")).Value = 80;
            ((TextBlock)preview.FindName("OpacityValue")).Text = "80%";
            ((Slider)preview.FindName("SizeSlider")).Value = 100;
            ((CheckBox)preview.FindName("AlwaysOnTop")).IsChecked = true;
            preview.Measure(new Size(320, Double.PositiveInfinity));
            var size = preview.DesiredSize;
            preview.Arrange(new Rect(0, 0, size.Width, size.Height)); preview.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(size.Width * 2), (int)Math.Ceiling(size.Height * 2), 192, 192, PixelFormats.Pbgra32); bitmap.Render(preview);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(file)) encoder.Save(stream);
            DisposeTray();
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
                bool demo = Array.IndexOf(args, "--demo") >= 0;
                bool created;
                using (var mutex = new Mutex(true, demo ? "Local\\VoltHUD.Demo.Instance" : "Local\\VoltHUD.Instance", out created))
                using (var activate = new EventWaitHandle(false, EventResetMode.AutoReset, demo ? "Local\\VoltHUD.Demo.Show" : "Local\\VoltHUD.Show"))
                {
                    if (!created) { activate.Set(); return; }
                    var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
                    var widget = new Widget(demo);
                    if (args.Length == 2 && args[0] == "--preview") { widget.RenderPreview(args[1]); return; }
                    if (args.Length == 2 && args[0] == "--settings-preview") { widget.RenderSettingsPreview(args[1]); return; }
                    if (args.Length == 2 && args[0] == "--settings-preview-light") { widget.RenderSettingsPreview(args[1], "Light"); return; }
                    if (args.Length == 2 && args[0] == "--settings-preview-windows") { widget.RenderSettingsPreview(args[1], "System"); return; }
                    if (demo || Array.IndexOf(args, "--windowed") >= 0) widget.ShowInTaskbar = true;
                    widget.ListenForActivation(activate);
                    app.MainWindow = widget;
                    bool startHidden = !demo && (Array.IndexOf(args, "--tray") >= 0 || widget.StartMinimized && Array.IndexOf(args, "--show") < 0);
                    if (startHidden) { widget.StartInTray(); app.Run(); }
                    else app.Run(widget);
                }
            }
            catch (Exception ex) { MessageBox.Show("VoltHUD couldn't start.\n\n" + ex.Message, "VoltHUD", MessageBoxButton.OK, MessageBoxImage.Error); }
        }
    }
}
