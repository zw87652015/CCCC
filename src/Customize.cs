using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Cccc
{
    // Windows 11 style settings window (WPF). Every control writes straight into the settings and calls `changed`;
    // `loading` blocks those writes while Reload pushes values back.
    sealed class Customize
    {
        sealed class Colour
        {
            public Button Swatch, Reset;
            public TextBlock Hex;
            public UIElement Inline, Row;
            public Func<string> Get;
            public Action<string> Put;
            public Func<Theme, System.Drawing.Color> Pick;
            public bool Full;
        }

        sealed class Sound
        {
            public string Title;
            public TextBlock Hint;
            public Border Seg;
            public RadioButton BuiltIn, Custom;
            public Button Browse, Play;
            public Func<string> Get;
            public Action<string> Put;
            public bool Ok;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct Margins { public int Left, Right, Top, Bottom; }

        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);

        static readonly uint[] Presets =
        {
            0xFF30D158, 0xFF0A84FF, 0xFF5E5CE6, 0xFFBF5AF2, 0xFFFF375F, 0xFFFF453A,
            0xFFFF9F0A, 0xFFFFD60A, 0xFF32D74B, 0xFF64D2FF, 0xFFAC8E68, 0xFF8E8E93,
            0xFFFFFFFF, 0xFF000000, 0xFFFFAC1C, 0xFF2EC4B6, 0xFFF4A261, 0xFF6B6B6B,
        };

        // Set by the harness for screenshots that must not depend on Mica.
        public static bool UseMica = true;

        public event EventHandler Closed;

        readonly Settings set;
        readonly Action changed;
        readonly Action<bool> showSample, test;
        readonly Action showCaps, showWifi, showAudio;
        readonly WifiWatch wifiWatch;
        readonly Card card = new Card();
        readonly Window w;
        readonly DispatcherTimer armTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        readonly RadioButton[] navs = new RadioButton[9];
        readonly StackPanel[] pages = new StackPanel[9];
        readonly TextBlock txtTitle;
        readonly Button btnReset;
        readonly StackPanel pageHost;
        readonly ScrollViewer pageScroll;
        readonly List<string> fonts = new List<string>();
        readonly List<Button> presetButtons = new List<Button>();
        readonly Colour[] colours = new Colour[7];
        readonly Button[] tiles = new Button[5];
        readonly RadioButton[] rbDetail = new RadioButton[4];
        RadioButton[] rbLang, rbTheme, rbWhen;
        CheckBox chkPause, chkWarn, chkCaps, chkStart, chkAuto, chkSound, chkSource, chkFull, chkStay, chkSimple, chkCapsStay, chkWifi, chkWifiStay, chkAudio, chkAudioStay, chkTray;
        Picker pickMain, pickCaps;
        Slider sldSize, sldText, sldDur, sldTrans, sldCapsW, sldCapsH, sldCapsDur, sldWifiDur, sldAudioDur;
        TextBlock lblSize, lblText, lblDur, lblTrans, lblCapsW, lblCapsH, lblCapsDur, lblWifiDur, lblWifiNames, lblAudioDur;
        ComboBox cmbFont, cmbIgnore;
        StackPanel ignoreList;
        Sound soundOk, soundFail;
        Button chevron;
        FrameworkElement fullBody;
        UIElement whenPanel, detailPanel;
        StackPanel sourceBody;
        Popup popColour;
        Border popBody;
        TextBox txtHex;
        TextBlock hexHint;
        Colour popFor;
        bool loading, armed, dark, builtZh, expanded, lastFull, popLoading, switching;
        int page;

        public Customize(Settings settings, Action onChanged, Action<bool> onShowSample, Action onShowCaps, Action onShowWifi, Action onShowAudio, Action<bool> onTest, WifiWatch wifiWatch)
        {
            // Software rendering keeps WPF off the GPU drivers (~24 MB of d3d9 + vendor UMD).
            System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
            set = settings;
            changed = onChanged;
            showSample = onShowSample;
            showCaps = onShowCaps;
            showWifi = onShowWifi;
            showAudio = onShowAudio;
            this.wifiWatch = wifiWatch;
            test = onTest;

            using (var s = typeof(Customize).Assembly.GetManifestResourceStream("Cccc.Customize.xaml"))
                w = (Window)XamlReader.Load(s);
            w.Closed += delegate
            {
                SystemEvents.UserPreferenceChanged -= OnSystemChange;
                if (Closed != null) Closed(this, EventArgs.Empty);
            };
            w.SourceInitialized += delegate { ApplyMica(); };
            txtTitle = (TextBlock)w.FindName("txtTitle");
            btnReset = (Button)w.FindName("btnReset");
            pageHost = (StackPanel)w.FindName("pageHost");
            pageScroll = (ScrollViewer)w.FindName("pageScroll");
            w.Icon = ToSource(Icons.App(32));
            ((Image)w.FindName("appIcon")).Source = w.Icon;

            ApplyTheme();
            BuildNav();
            btnReset.Style = Res<Style>("Link");
            btnReset.Click += delegate { OnReset(); };
            armTimer.Tick += delegate { armTimer.Stop(); armed = false; btnReset.Content = L.ResetDefaults; };
            BuildPages();
            LocalizeChrome();
            ShowPage(0);
            Reload();
            SystemEvents.UserPreferenceChanged += OnSystemChange;
        }

        void OnSystemChange(object sender, UserPreferenceChangedEventArgs e)
        {
            w.Dispatcher.BeginInvoke(new Action(Reload));
        }

        public void Show()
        {
            System.Windows.Forms.Integration.ElementHost.EnableModelessKeyboardInterop(w);
            w.Show();
        }

        public void Activate() { w.Activate(); }

        // MARK: Sync

        public void Reload()
        {
            loading = true;
            ApplyTheme();
            bool rebuilt = builtZh != L.Zh;
            if (rebuilt)
            {
                pageHost.Children.Clear();
                BuildPages();
                LocalizeChrome();
                ShowPage(page);
            }
            bool turnedOn = set.FullPalette && !lastFull;
            lastFull = set.FullPalette;
            if (turnedOn) expanded = true;
            chkPause.IsChecked = set.Paused;
            chkWarn.IsChecked = set.FailHint;
            chkCaps.IsChecked = set.CapsHint;
            chkStart.IsChecked = Settings.AutoStart;
            chkTray.IsChecked = !set.HideTray;
            chkAuto.IsChecked = set.AutoUpdate;
            rbLang[set.Lang == "en" ? 1 : set.Lang == "zh" ? 2 : 0].IsChecked = true;
            rbTheme[set.ThemeMode].IsChecked = true;
            for (int i = 0; i < tiles.Length; i++) tiles[i].Tag = i == set.Palette ? "selected" : null;
            var theme = Theme.Resolve(set, !Theme.SystemLight());
            foreach (Colour c in colours)
            {
                string v = c.Get();
                System.Drawing.Color parsed;
                System.Drawing.Color shown = Theme.TryColor(v, out parsed) ? parsed : c.Pick(theme);
                c.Swatch.Background = Tint(shown);
                c.Hex.Text = Hex6(shown);
                c.Reset.Visibility = v != null ? Visibility.Visible : Visibility.Collapsed;
            }
            chkFull.IsChecked = set.FullPalette;
            if (rebuilt || !w.IsLoaded) SyncExpanded();
            else if (turnedOn) SyncExpanded(true);
            sldSize.Value = set.Scale;
            sldText.Value = set.TextScale;
            chkSimple.IsChecked = set.Simple;
            sldTrans.Value = set.Transparency;
            sldDur.Value = set.DurationMs;
            sldDur.IsEnabled = !set.StayUntilClicked;
            sldCapsDur.Value = set.CapsDurationMs;
            sldCapsDur.IsEnabled = !set.CapsStayUntilClicked;
            lblCapsDur.Text = (set.CapsDurationMs / 1000.0).ToString("0.0") + " s";
            chkCapsStay.IsChecked = set.CapsStayUntilClicked;
            chkWifi.IsChecked = set.WifiHint;
            sldWifiDur.Value = set.WifiDurationMs;
            sldWifiDur.IsEnabled = !set.WifiStayUntilClicked;
            lblWifiDur.Text = (set.WifiDurationMs / 1000.0).ToString("0.0") + " s";
            chkWifiStay.IsChecked = set.WifiStayUntilClicked;
            chkAudio.IsChecked = set.AudioHint;
            sldAudioDur.Value = set.AudioDurationMs;
            sldAudioDur.IsEnabled = !set.AudioStayUntilClicked;
            lblAudioDur.Text = (set.AudioDurationMs / 1000.0).ToString("0.0") + " s";
            chkAudioStay.IsChecked = set.AudioStayUntilClicked;
            lblWifiNames.Text = !wifiWatch.Available ? L.WifiNoAdapter
                : wifiWatch.NamesAllowed == null ? ""
                : wifiWatch.NamesAllowed == true ? L.WifiAllowed : L.WifiNotAllowed;
            lblSize.Text = set.Scale + "%";
            lblText.Text = set.TextScale + "%";
            lblTrans.Text = set.Transparency + "%";
            lblDur.Text = (set.DurationMs / 1000.0).ToString("0.0") + " s";
            int fi = fonts.IndexOf(set.FontFamily);
            cmbFont.SelectedIndex = fi < 0 ? 0 : fi;
            sldCapsW.Value = set.CapsWidth;
            sldCapsH.Value = set.CapsHeight;
            lblCapsW.Text = set.CapsWidth + " px";
            lblCapsH.Text = set.CapsHeight + " px";
            pickMain.Sync();
            pickCaps.Sync();
            chkStay.IsChecked = set.StayUntilClicked;
            chkSound.IsChecked = set.Sound;
            ShowSound(soundOk);
            ShowSound(soundFail);
            chkSource.IsChecked = set.ShowSource;
            rbWhen[set.SourceWhen].IsChecked = true;
            for (int i = 0; i < rbDetail.Length; i++) rbDetail[i].IsChecked = set.SourceDetail == i;
            Reveal(sourceBody, set.ShowSource, w.IsLoaded && !rebuilt);
            FillIgnore();
            ignoreList.Children.Clear();
            foreach (string exe in set.IgnoreApps)
            {
                string e = exe;
                var remove = new Button { Style = Res<Style>("Subtle"), Content = L.Remove };
                remove.Click += delegate { Edit(delegate { set.IgnoreApps.Remove(e); }); };
                ignoreList.Children.Add(Divider());
                ignoreList.Children.Add(CardGrid(Sources.NameFor(e), e + ".exe", remove));
            }
            loading = false;
            card.Apply(set);
        }

        // The copied card mirrors almost every display setting; warn colour and the fail hint only affect the
        // failed card; everything else (pause, autostart, auto-update, sound files) shows nothing.
        static string SigShared(Settings s)
        {
            return string.Join("|", new object[] { s.ThemeMode, s.Palette, s.OkColor, s.BgColor, s.BorderColor, s.TitleColor,
                s.SubColor, s.MetaColor, s.FullPalette, s.FontFamily, s.Scale, s.TextScale, s.Position, s.FreeX, s.FreeY,
                s.DurationMs, s.StayUntilClicked, s.ShowSource, s.SourceWhen, s.SourceDetail, s.Simple, s.Transparency, s.Lang });
        }

        static string SigFailed(Settings s) { return s.WarnColor + "|" + s.FailHint; }

        static string SigCaps(Settings s)
        {
            return s.CapsWidth + "|" + s.CapsHeight + "|" + s.CapsPosition + "|" + s.CapsFreeX + "|" + s.CapsFreeY
                + "|" + s.CapsDurationMs + "|" + s.CapsStayUntilClicked;
        }

        static string SigWifi(Settings s)
        {
            return s.WifiHint + "|" + s.WifiDurationMs + "|" + s.WifiStayUntilClicked;
        }

        static string SigAudio(Settings s)
        {
            return s.AudioHint + "|" + s.AudioDurationMs + "|" + s.AudioStayUntilClicked;
        }

        // Every user change: apply once through `changed`, re-sync the window, then pop the real card on screen.
        void Edit(Action a)
        {
            if (loading) return;
            string shared = SigShared(set), failed = SigFailed(set), caps = SigCaps(set), wifi = SigWifi(set), audio = SigAudio(set);
            a();
            changed();
            Reload();
            if (SigShared(set) != shared) showSample(false);
            else if (SigCaps(set) != caps) showCaps();
            else if (SigWifi(set) != wifi && set.WifiHint) showWifi();
            else if (SigAudio(set) != audio && set.AudioHint) showAudio();
            else if (SigFailed(set) != failed && set.FailHint) showSample(true);
        }

        // MARK: Theme and chrome

        void ApplyTheme()
        {
            bool sysLight = Theme.SystemLight();
            dark = set.ThemeMode == 2 || (set.ThemeMode == 0 && !sysLight);
            var t = Theme.Resolve(set, !sysLight);
            w.Resources["Accent"] = Tint(t.Ok);
            w.Resources["AccentLow"] = Tint(System.Drawing.Color.FromArgb(0x33, t.Ok));
            w.Resources["AccentHalf"] = Tint(System.Drawing.Color.FromArgb(0x80, t.Ok));
            w.Resources["AccentText"] = Tint(Theme.Glyph(t.Ok));
            w.Resources["WinBg"] = Solid(dark ? "#202020" : "#F3F3F3");
            w.Resources["Surface"] = Solid(dark ? "#2B2B2B" : "#FFFFFF");
            w.Resources["CardSurface"] = Solid(dark ? "#0DFFFFFF" : "#B3FFFFFF");
            w.Resources["CardStroke"] = Solid(dark ? "#14FFFFFF" : "#0F000000");
            w.Resources["TextBrush"] = Solid(dark ? "#FFFFFF" : "#1A1A1A");
            w.Resources["SecondaryText"] = Solid(dark ? "#A0FFFFFF" : "#5F5F5F");
            w.Resources["ControlFill"] = Solid(dark ? "#0FFFFFFF" : "#B3FFFFFF");
            w.Resources["ControlStroke"] = Solid(dark ? "#18FFFFFF" : "#14000000");
            w.Resources["NavFill"] = Solid(dark ? "#0FFFFFFF" : "#09000000");
            w.Resources["PressFill"] = Solid(dark ? "#08FFFFFF" : "#4DF9F9F9");
            w.Resources["HoverFill"] = Solid(dark ? "#15FFFFFF" : "#80F9F9F9");
            w.Resources["ThumbFill"] = Solid("#FFFFFF");
            w.Resources["ThumbStroke"] = Solid("#33000000");
            w.Resources["UiFont"] = new FontFamily(L.Zh ? "Microsoft YaHei UI" : FirstInstalled("Segoe UI Variable Text", "Segoe UI"));
            w.Resources["IconFont"] = new FontFamily(FirstInstalled("Segoe Fluent Icons", "Segoe MDL2 Assets"));
            ApplyDwm();
        }

        // The dark caption lives behind attribute 20, so it has to follow every theme change, not just Mica setup.
        void ApplyDwm()
        {
            IntPtr hwnd = new WindowInteropHelper(w).Handle;
            if (hwnd == IntPtr.Zero) return;
            int dk = dark ? 1 : 0;
            DwmSetWindowAttribute(hwnd, 20, ref dk, 4);
        }

        static string FirstInstalled(params string[] names)
        {
            foreach (string n in names)
                foreach (FontFamily f in Fonts.SystemFontFamilies)
                    if (string.Equals(f.Source, n, StringComparison.OrdinalIgnoreCase)) return n;
            return names[names.Length - 1];
        }

        void ApplyMica()
        {
            if (!UseMica) return;
            IntPtr hwnd = new WindowInteropHelper(w).Handle;
            int mica = 2;
            if (DwmSetWindowAttribute(hwnd, 38, ref mica, 4) == 0)
            {
                var m = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
                DwmExtendFrameIntoClientArea(hwnd, ref m);
                w.Background = Brushes.Transparent;
                HwndSource.FromHwnd(hwnd).CompositionTarget.BackgroundColor = Colors.Transparent;
            }
            ApplyDwm();
        }

        void LocalizeChrome()
        {
            builtZh = L.Zh;
            w.Title = L.CustomizeTitle;
            string[] labels = { L.TabGeneral, L.TabAppearance, L.TabBehavior, L.TabSound, L.TabSource, L.TabCaps, L.TabWifi, L.TabAudio, L.TabAbout };
            string[] glyphs = { "\uE713", "\uE790", "\uE7F4", "\uE767", "\uE71B", "\uE765", "\uE701", "\uE7F5", "\uE946" };
            for (int i = 0; i < 9; i++)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                row.Children.Add(new TextBlock { Text = glyphs[i], Style = Res<Style>("Glyph"), Margin = new Thickness(0, 0, 12, 0) });
                row.Children.Add(new TextBlock { Text = labels[i], VerticalAlignment = VerticalAlignment.Center });
                navs[i].Content = row;
            }
            if (!armed) btnReset.Content = L.ResetDefaults;
            txtTitle.Text = PageTitle(page);
        }

        string PageTitle(int index)
        {
            return new[] { L.TabGeneral, L.TabAppearance, L.TabBehavior, L.TabSound, L.TabSource, L.TabCaps, L.TabWifi, L.TabAudio, L.TabAbout }[index];
        }

        void ShowPage(int index)
        {
            if (switching) return;
            switching = true;
            page = index;
            for (int i = 0; i < pages.Length; i++)
            {
                if (pages[i] != null) pages[i].Visibility = i == index ? Visibility.Visible : Visibility.Collapsed;
                navs[i].IsChecked = i == index;
            }
            switching = false;
            txtTitle.Text = PageTitle(index);
        }

        void BuildNav()
        {
            string[] names = { "navGeneral", "navAppearance", "navBehavior", "navSound", "navSource", "navCaps", "navWifi", "navAudio", "navAbout" };
            for (int i = 0; i < 9; i++)
            {
                int index = i;
                var nav = (RadioButton)w.FindName(names[i]);
                nav.Style = Res<Style>("Nav");
                nav.Checked += delegate { if (!loading) ShowPage(index); };
                navs[i] = nav;
            }
        }

        // MARK: Helpers

        T Res<T>(string key) where T : class { return (T)w.FindResource(key); }

        static Brush Solid(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }

        static Color ToMedia(System.Drawing.Color c) { return Color.FromArgb(c.A, c.R, c.G, c.B); }

        static Brush Tint(System.Drawing.Color c) { return new SolidColorBrush(ToMedia(c)); }

        static string Hex6(System.Drawing.Color c) { return string.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B); }

        static System.Drawing.Color FromArgb(uint argb)
        {
            return System.Drawing.Color.FromArgb((int)(argb >> 24), (int)((argb >> 16) & 255), (int)((argb >> 8) & 255), (int)(argb & 255));
        }

        static BitmapSource ToSource(System.Drawing.Bitmap b, double dpi = 96)
        {
            var rect = new System.Drawing.Rectangle(0, 0, b.Width, b.Height);
            var data = b.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            int stride = data.Stride;
            byte[] px = new byte[stride * b.Height];
            Marshal.Copy(data.Scan0, px, 0, px.Length);
            b.UnlockBits(data);
            var src = BitmapSource.Create(b.Width, b.Height, dpi, dpi, PixelFormats.Pbgra32, null, px, stride);
            src.Freeze();
            return src;
        }

        CheckBox Toggle()
        {
            var cb = new CheckBox { Style = Res<Style>("Toggle"), VerticalAlignment = VerticalAlignment.Center, Content = L.ToggleOff };
            cb.Checked += delegate { cb.Content = L.ToggleOn; };
            cb.Unchecked += delegate { cb.Content = L.ToggleOff; };
            return cb;
        }

        // A setting row: title and hint on the left, the control on the right.
        Grid CardGrid(string title, string hint, UIElement control)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
            text.Children.Add(new TextBlock { Text = title, Style = Res<Style>("RowTitle") });
            if (hint != null) text.Children.Add(new TextBlock { Text = hint, Style = Res<Style>("RowHint") });
            g.Children.Add(text);
            Grid.SetColumn(control, 1);
            control.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            g.Children.Add(control);
            return g;
        }

        Border Box(UIElement child)
        {
            return new Border { Style = Res<Style>("CardBorder"), Child = child };
        }

        UIElement SettingCard(string title, string hint, UIElement control)
        {
            return Box(CardGrid(title, hint, control));
        }

        TextBlock Header(string text)
        {
            return new TextBlock { Text = text, Style = Res<Style>("SectionHeader") };
        }

        StackPanel PageOf(params UIElement[] items)
        {
            var sp = new StackPanel();
            foreach (UIElement e in items) sp.Children.Add(e);
            return sp;
        }

        Border Divider()
        {
            var d = new Border { Height = 1, Margin = new Thickness(0, 10, 0, 10) };
            d.SetResourceReference(Border.BackgroundProperty, "CardStroke");
            return d;
        }

        Border SegOf(string[] labels, out RadioButton[] rbs)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            rbs = new RadioButton[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                rbs[i] = new RadioButton { Content = labels[i], Style = Res<Style>("Seg") };
                sp.Children.Add(rbs[i]);
            }
            var b = new Border { CornerRadius = new CornerRadius(6), BorderThickness = new Thickness(1), Padding = new Thickness(2), Child = sp };
            b.SetResourceReference(Border.BackgroundProperty, "ControlFill");
            b.SetResourceReference(Border.BorderBrushProperty, "ControlStroke");
            return b;
        }

        void BindToggle(CheckBox cb, Action<bool> put)
        {
            cb.Checked += delegate { Edit(delegate { put(true); }); };
            cb.Unchecked += delegate { Edit(delegate { put(false); }); };
        }

        void BindRadio(RadioButton rb, Action put)
        {
            rb.Checked += delegate { Edit(put); };
        }

        Slider Slide(double lo, double hi, double step, Action<int> put)
        {
            var s = new Slider { Minimum = lo, Maximum = hi, TickFrequency = step, IsSnapToTickEnabled = true, Width = 240, Style = Res<Style>("Sld") };
            s.ValueChanged += delegate { int v = (int)Math.Round(s.Value); Edit(delegate { put(v); }); };
            return s;
        }

        // MARK: Pages

        void BuildPages()
        {
            pages[0] = BuildGeneral();
            pages[1] = BuildAppearance();
            pages[2] = BuildBehavior();
            pages[3] = BuildSound();
            pages[4] = BuildSource();
            pages[5] = BuildCaps();
            pages[6] = BuildWifi();
            pages[7] = BuildAudio();
            pages[8] = BuildAbout();
            foreach (StackPanel p in pages) pageHost.Children.Add(p);
        }

        StackPanel BuildGeneral()
        {
            chkPause = Toggle();
            chkWarn = Toggle();
            chkStart = Toggle();
            chkAuto = Toggle();
            chkTray = Toggle();
            BindToggle(chkPause, v => set.Paused = v);
            BindToggle(chkWarn, v => set.FailHint = v);
            BindToggle(chkStart, v => Settings.AutoStart = v);
            BindToggle(chkTray, v => set.HideTray = !v);
            BindToggle(chkAuto, v => set.AutoUpdate = v);
            RadioButton[] lang;
            var langSeg = SegOf(new[] { L.LangSystem, "English", "中文" }, out lang);
            rbLang = lang;
            string[] codes = { "auto", "en", "zh" };
            for (int i = 0; i < 3; i++) { int idx = i; BindRadio(rbLang[i], delegate { set.Lang = codes[idx]; }); }
            cmbIgnore = new ComboBox { Style = Res<Style>("Cmb") };
            cmbIgnore.DropDownOpened += delegate { FillIgnore(); };
            cmbIgnore.SelectionChanged += delegate { PickIgnore(); };
            ignoreList = new StackPanel();
            FillIgnore();
            return PageOf(
                SettingCard(L.MenuPause, L.HintPause, chkPause),
                SettingCard(L.MenuHint, L.HintWarn, chkWarn),
                // Ignored apps are rows inside their setting's card, not cards of their own.
                Box(PageOf(CardGrid(L.IgnoreApps, L.HintIgnore, cmbIgnore), ignoreList)),
                SettingCard(L.MenuStartup, L.HintStartup, chkStart),
                SettingCard(L.MenuTray, L.HintTray, chkTray),
                SettingCard(L.MenuAutoUpdate, L.HintAutoUpdate, chkAuto),
                SettingCard(L.MenuLanguage, L.HintLanguage, langSeg));
        }

        StackPanel BuildAppearance()
        {
            RadioButton[] theme;
            var themeSeg = SegOf(new[] { L.ThemeSystem, L.ThemeLight, L.ThemeDark }, out theme);
            rbTheme = theme;
            for (int i = 0; i < 3; i++) { int idx = i; BindRadio(rbTheme[i], delegate { set.ThemeMode = idx; }); }

            var tileRow = new WrapPanel();
            string[] tileNames = { L.PaletteDefault, L.PaletteGraphite, L.PaletteContrast, L.PalettePastel, L.PaletteGlass };
            for (int p = 0; p < tiles.Length; p++)
            {
                int idx = p;
                var b = new Button { Style = Res<Style>("PaletteTile"), Margin = new Thickness(0, 0, 10, 0), Cursor = System.Windows.Input.Cursors.Hand };
                b.Content = PaletteTile(p, tileNames[p]);
                b.Click += delegate { Edit(delegate { set.Palette = idx; }); };
                tiles[p] = b;
                tileRow.Children.Add(b);
            }

            colours[0] = MakeColour(L.OkColor, L.HintOkColor, false, () => set.OkColor, v => set.OkColor = v, t => t.Ok);
            colours[1] = MakeColour(L.WarnColor, L.HintWarnColor, false, () => set.WarnColor, v => set.WarnColor = v, t => t.Warn);
            colours[2] = MakeColour(L.ColorBg, null, true, () => set.BgColor, v => set.BgColor = v, t => t.Bg);
            colours[3] = MakeColour(L.ColorBorder, null, true, () => set.BorderColor, v => set.BorderColor = v, t => t.Border);
            colours[4] = MakeColour(L.ColorTitle, null, true, () => set.TitleColor, v => set.TitleColor = v, t => t.Title);
            colours[5] = MakeColour(L.ColorSub, null, true, () => set.SubColor, v => set.SubColor = v, t => t.Sub);
            colours[6] = MakeColour(L.ColorMeta, null, true, () => set.MetaColor, v => set.MetaColor = v, t => t.Meta);

            chkFull = Toggle();
            BindToggle(chkFull, v => set.FullPalette = v);
            chevron = new Button { Style = Res<Style>("IconBtn"), HorizontalAlignment = HorizontalAlignment.Right,
                                   VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
            chevron.Content = new TextBlock { Text = "\uE70D", Style = Res<Style>("Glyph"), FontSize = 12 };
            chevron.RenderTransformOrigin = new Point(0.5, 0.5);
            chevron.Click += delegate { expanded = !expanded; SyncExpanded(true); };
            var head = new StackPanel { Orientation = Orientation.Horizontal, Children = { chkFull, chevron } };
            var header = CardGrid(L.FullColors, L.HintFullColors, head);
            header.Background = Brushes.Transparent;
            header.Cursor = System.Windows.Input.Cursors.Hand;
            header.MouseLeftButtonUp += OnExpandHeader;
            var body = new StackPanel();
            for (int i = 2; i < 7; i++)
            {
                if (i > 2) body.Children.Add(Divider());
                body.Children.Add(colours[i].Row);
            }
            fullBody = new StackPanel { Margin = new Thickness(0, 12, 0, 0), Children = { body } };
            var fullCard = new StackPanel();
            fullCard.Children.Add(header);
            fullCard.Children.Add(fullBody);

            sldSize = Slide(75, 200, 5, v => set.Scale = v);
            lblSize = new TextBlock { Style = Res<Style>("ValueText"), Width = 56, Margin = new Thickness(10, 0, 0, 0) };
            var sizeRow = new StackPanel { Orientation = Orientation.Horizontal, Children = { sldSize, lblSize } };

            fonts.Clear();
            fonts.Add(L.FontDefault);
            var names = new List<string>();
            foreach (FontFamily f in Fonts.SystemFontFamilies) names.Add(f.Source);
            names.Sort(StringComparer.CurrentCultureIgnoreCase);
            fonts.AddRange(names);
            cmbFont = new ComboBox { Style = Res<Style>("Cmb"), ItemsSource = fonts, ItemTemplate = FontItemTemplate() };
            cmbFont.SelectionChanged += delegate
            {
                int i = cmbFont.SelectedIndex;
                if (!loading && i >= 0) Edit(delegate { set.FontFamily = i == 0 ? null : fonts[i]; });
            };

            sldText = Slide(80, 160, 5, v => set.TextScale = v);
            lblText = new TextBlock { Style = Res<Style>("ValueText"), Width = 56, Margin = new Thickness(10, 0, 0, 0) };
            var textRow = new StackPanel { Orientation = Orientation.Horizontal, Children = { sldText, lblText } };

            chkSimple = Toggle();
            BindToggle(chkSimple, v => set.Simple = v);
            sldTrans = Slide(0, 75, 5, v => set.Transparency = v);
            lblTrans = new TextBlock { Style = Res<Style>("ValueText"), Width = 56, Margin = new Thickness(10, 0, 0, 0) };
            var transRow = new StackPanel { Orientation = Orientation.Horizontal, Children = { sldTrans, lblTrans } };

            return PageOf(
                SettingCard(L.ThemeTitle, L.HintTheme, themeSeg),
                Header(L.PaletteTitle),
                tileRow,
                Header(L.ColoursTitle),
                colours[0].Row,
                colours[1].Row,
                Box(fullCard),
                Header(L.SizeTitle),
                SettingCard(L.CardSize, L.HintCardSize, sizeRow),
                SettingCard(L.FontTitle, L.HintFont, cmbFont),
                SettingCard(L.TextSize, L.HintTextSize, textRow),
                SettingCard(L.SimpleMode, L.HintSimple, chkSimple),
                SettingCard(L.Transparency, L.HintTransparency, transRow));
        }

        void FillIgnore()
        {
            cmbIgnore.Items.Clear();
            cmbIgnore.Items.Add(new ComboBoxItem { Content = L.AddApp });
            foreach (KeyValuePair<string, string> app in Sources.Seen())
                if (!set.Ignores(app.Key))
                    cmbIgnore.Items.Add(new ComboBoxItem { Content = app.Value, Tag = app.Key });
            cmbIgnore.Items.Add(new ComboBoxItem { Content = L.Browse, Tag = "browse" });
            cmbIgnore.SelectedIndex = 0;
        }

        void PickIgnore()
        {
            if (loading || cmbIgnore.SelectedIndex <= 0) return;
            var item = cmbIgnore.SelectedItem as ComboBoxItem;
            cmbIgnore.SelectedIndex = 0;
            if (item == null) return;
            if ("browse".Equals(item.Tag))
            {
                var dlg = new OpenFileDialog { Filter = L.ProgramFilter };
                if (dlg.ShowDialog(w) == true)
                {
                    string exe = System.IO.Path.GetFileNameWithoutExtension(dlg.FileName);
                    if (!set.Ignores(exe)) Edit(delegate { set.IgnoreApps.Add(exe); });
                }
                return;
            }
            string app = item.Tag as string;
            if (app != null && !set.Ignores(app)) Edit(delegate { set.IgnoreApps.Add(app); });
        }

        void OnExpandHeader(object s, System.Windows.Input.MouseButtonEventArgs e)
        {
            var src = e.Source as DependencyObject;
            while (src != null)
            {
                if (src == chkFull) return;
                src = src is Visual ? VisualTreeHelper.GetParent(src) : LogicalTreeHelper.GetParent(src);
            }
            expanded = !expanded;
            SyncExpanded(true);
        }

        void SyncExpanded() { SyncExpanded(false); }

        void SyncExpanded(bool animate)
        {
            if (fullBody == null) return;
            bool open = expanded;
            Reveal(fullBody, open, animate);
            if (!animate)
            {
                chevron.RenderTransform = new RotateTransform(open ? 180 : 0, 0.5, 0.5);
                return;
            }
            var rot = chevron.RenderTransform as RotateTransform;
            if (rot == null) { rot = new RotateTransform(open ? 180 : 0, 0.5, 0.5); chevron.RenderTransform = rot; }
            rot.BeginAnimation(RotateTransform.AngleProperty, new System.Windows.Media.Animation.DoubleAnimation(open ? 180 : 0,
                TimeSpan.FromMilliseconds(200)) { EasingFunction = new System.Windows.Media.Animation.CubicEase
                { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut } });
        }

        // Slides a section open or shut (height + fade). The target is kept in Tag so a stale
        // completion from an interrupted animation doesn't override a newer one.
        static void Reveal(FrameworkElement el, bool open, bool animate)
        {
            if (el.Tag is bool && (bool)el.Tag == open && animate) return;
            el.Tag = open;
            if (!animate)
            {
                el.BeginAnimation(FrameworkElement.HeightProperty, null);
                el.BeginAnimation(UIElement.OpacityProperty, null);
                el.Height = double.NaN;
                el.Opacity = 1;
                el.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
                return;
            }
            var ease = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };
            var span = TimeSpan.FromMilliseconds(200);
            // Start from the current on-screen height so a click mid-animation reverses smoothly.
            bool shown = el.Visibility == Visibility.Visible;
            double from = shown ? el.ActualHeight : 0, fromOp = shown ? el.Opacity : 0;
            el.BeginAnimation(FrameworkElement.HeightProperty, null);
            el.BeginAnimation(UIElement.OpacityProperty, null);
            el.Height = double.NaN;
            el.Opacity = 1;
            el.Visibility = Visibility.Visible;
            el.UpdateLayout();
            double to = open ? el.ActualHeight : 0;
            var size = new System.Windows.Media.Animation.DoubleAnimation(from, to, span)
                { EasingFunction = ease, FillBehavior = System.Windows.Media.Animation.FillBehavior.Stop };
            var fade = new System.Windows.Media.Animation.DoubleAnimation(fromOp, open ? 1 : 0, span)
                { FillBehavior = System.Windows.Media.Animation.FillBehavior.Stop };
            size.Completed += delegate
            {
                if (!(el.Tag is bool) || (bool)el.Tag != open) return;
                el.Height = double.NaN;
                el.Opacity = 1;
                el.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            };
            // Final values stay as local values so nothing flashes when the animations release.
            if (!open) { el.Height = 0; el.Opacity = 0; }
            el.BeginAnimation(FrameworkElement.HeightProperty, size);
            el.BeginAnimation(UIElement.OpacityProperty, fade);
        }

        // A font name rendered in its own face.
        DataTemplate FontItemTemplate()
        {
            var tb = new FrameworkElementFactory(typeof(TextBlock));
            tb.SetBinding(TextBlock.TextProperty, new Binding("."));
            tb.SetBinding(TextBlock.FontFamilyProperty, new Binding("."));
            tb.SetValue(TextBlock.FontSizeProperty, 14.0);
            var template = new DataTemplate { VisualTree = tb };
            template.Seal();
            return template;
        }

        UIElement PaletteTile(int p, string name)
        {
            var t = Theme.Resolve(new Settings { Palette = p, ThemeMode = dark ? 2 : 1 }, false);
            var mini = new Border { Width = 96, Height = 60, CornerRadius = new CornerRadius(6), BorderThickness = new Thickness(1),
                                    Background = Tint(t.Bg), BorderBrush = Tint(t.Border) };
            var g = new Canvas { Width = 94, Height = 58 };
            var dot = new Ellipse { Width = 12, Height = 12, Fill = Tint(t.Ok) };
            var bar1 = new Rectangle { Width = 46, Height = 5, RadiusX = 2, RadiusY = 2, Fill = Tint(t.Title) };
            var bar2 = new Rectangle { Width = 60, Height = 4, RadiusX = 2, RadiusY = 2, Fill = Tint(t.Sub) };
            g.Children.Add(dot); Canvas.SetLeft(dot, 8); Canvas.SetTop(dot, 8);
            g.Children.Add(bar1); Canvas.SetLeft(bar1, 26); Canvas.SetTop(bar1, 10);
            g.Children.Add(bar2); Canvas.SetLeft(bar2, 26); Canvas.SetTop(bar2, 22);
            mini.Child = g;
            if (t.Glass)
            {
                // A tinted pane with a bright rim over a colourful backdrop.
                mini.Background = new LinearGradientBrush(Color.FromRgb(0x4F, 0x8D, 0xFF), Color.FromRgb(0xFF, 0x9A, 0x6B), 35);
                mini.BorderBrush = Tint(t.Border);
                var rim = new LinearGradientBrush(Color.FromArgb(0xE6, 0xFF, 0xFF, 0xFF), Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF), 60);
                var pane = new Border { Margin = new Thickness(5, 7, 5, 7), CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1),
                                        Background = Tint(System.Drawing.Color.FromArgb(dark ? 150 : 165, t.Bg)), BorderBrush = rim };
                g.Width = 82; g.Height = 42;
                Canvas.SetLeft(dot, 7); Canvas.SetTop(dot, 7);
                Canvas.SetLeft(bar1, 25); Canvas.SetTop(bar1, 9);
                Canvas.SetLeft(bar2, 25); Canvas.SetTop(bar2, 21);
                bar2.Width = 48;
                mini.Child = pane;
                pane.Child = g;
            }
            var caption = new TextBlock { Text = name, HorizontalAlignment = HorizontalAlignment.Center, FontSize = 12,
                                          Margin = new Thickness(0, 6, 0, 0), Style = Res<Style>("ValueText") };
            caption.VerticalAlignment = VerticalAlignment.Top;
            return new StackPanel { Children = { mini, caption } };
        }

        Colour MakeColour(string title, string hint, bool full, Func<string> get, Action<string> put, Func<Theme, System.Drawing.Color> pick)
        {
            var c = new Colour { Get = get, Put = put, Pick = pick, Full = full };
            c.Hex = new TextBlock { Style = Res<Style>("ValueText"), Margin = new Thickness(0, 0, 10, 0) };
            c.Swatch = new Button { Style = Res<Style>("Swatch"), Cursor = System.Windows.Input.Cursors.Hand };
            c.Swatch.Click += delegate { OpenPopup(c); };
            c.Reset = new Button { Style = Res<Style>("Link"), Content = L.ResetColor, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            c.Reset.Click += delegate { Edit(delegate { put(null); }); };
            var inline = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = { c.Hex, c.Swatch, c.Reset } };
            c.Inline = inline;
            c.Row = full ? (UIElement)CardGrid(title, null, inline) : SettingCard(title, hint, inline);
            return c;
        }

        // MARK: Colour popup

        void OpenPopup(Colour c)
        {
            if (popColour == null) BuildPopup();
            popFor = c;
            popColour.PlacementTarget = c.Swatch;
            System.Drawing.Color shown;
            string v = c.Get();
            System.Drawing.Color parsed;
            shown = Theme.TryColor(v, out parsed) ? parsed : c.Pick(Theme.Resolve(set, !Theme.SystemLight()));
            popLoading = true;
            txtHex.Text = Hex6(shown);
            popLoading = false;
            hexHint.Visibility = Visibility.Collapsed;
            foreach (Button b in presetButtons)
            {
                uint p = (uint)b.Tag;
                bool same = (p & 0x00FFFFFF) == (uint)((shown.R << 16) | (shown.G << 8) | shown.B);
                b.BorderBrush = same ? Res<Brush>("Accent") : Brushes.Transparent;
            }
            popBody.Background = Res<Brush>("Surface");
            popColour.IsOpen = true;
        }

        void BuildPopup()
        {
            var grid = new UniformGrid { Rows = 3, Columns = 6 };
            foreach (uint argb in Presets)
            {
                uint preset = argb;
                var b = new Button { Width = 28, Height = 28, Margin = new Thickness(3), Style = Res<Style>("PresetBtn"),
                                     Background = Tint(FromArgb(preset)), BorderBrush = Brushes.Transparent, Tag = preset };
                b.Click += delegate { CloseAndEdit(delegate { popFor.Put(Theme.Hex(FromArgb(preset))); }); };
                grid.Children.Add(b);
                presetButtons.Add(b);
            }
            txtHex = new TextBox { Style = Res<Style>("Tb"), Width = 150, MaxLength = 7 };
            hexHint = new TextBlock { Text = "#RRGGBB", Style = Res<Style>("ValueText"), IsHitTestVisible = false,
                                      Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            var field = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            field.Children.Add(txtHex);
            field.Children.Add(hexHint);
            txtHex.TextChanged += delegate
            {
                hexHint.Visibility = txtHex.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
                if (!popLoading) LiveApply();
            };
            txtHex.KeyDown += delegate(object s, System.Windows.Input.KeyEventArgs e)
            {
                if (e.Key != System.Windows.Input.Key.Enter) return;
                LiveApply();
                popColour.IsOpen = false;
            };
            var reset = new Button { Style = Res<Style>("Link"), Content = L.ResetColor, Margin = new Thickness(0, 10, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
            reset.Click += delegate { CloseAndEdit(delegate { popFor.Put(null); }); };
            var sp = new StackPanel();
            sp.Children.Add(grid);
            sp.Children.Add(field);
            sp.Children.Add(reset);
            popBody = new Border { Padding = new Thickness(14), CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1),
                                   BorderBrush = Solid(dark ? "#22FFFFFF" : "#14000000"), Child = sp,
                                   Effect = new DropShadowEffect { BlurRadius = 16, ShadowDepth = 4, Opacity = 0.35 } };
            popColour = new Popup { Child = popBody, StaysOpen = false, Placement = PlacementMode.Bottom, AllowsTransparency = true,
                                    PopupAnimation = PopupAnimation.Fade };
        }

        static string Normalize(string text)
        {
            if (text == null) return null;
            string s = text.Trim();
            if (s.StartsWith("#")) s = s.Substring(1);
            if (s.Length == 6) s = "FF" + s;
            if (s.Length != 8) return null;
            return "#" + s.ToUpperInvariant();
        }

        // Applies as soon as the text is a valid colour.
        void LiveApply()
        {
            if (popFor == null) return;
            string s = Normalize(txtHex.Text);
            System.Drawing.Color c;
            if (s == null || !Theme.TryColor(s, out c)) return;
            Colour target = popFor;
            Edit(delegate { target.Put(Theme.Hex(c)); });
        }

        void CloseAndEdit(Action a)
        {
            popColour.IsOpen = false;
            Edit(a);
        }

        // MARK: Behavior

        // The position picker shared by the copied card and the Caps Lock card: a monitor with nine
        // spots, Near/Freedom chips (plus an optional "same as copied" chip), and a draggable mini
        // card in Freedom mode. `pos` is the effective position shown; `raw` is the stored value.
        sealed class Picker
        {
            readonly Customize c;
            readonly Func<int> pos, raw, fx, fy;
            readonly Action<int> setPos;
            readonly Action<int, int> setFree;
            readonly Func<Size> cardSize;
            public readonly ToggleButton[] Spots = new ToggleButton[9];
            public ToggleButton ChipSame, ChipNear, ChipFree;
            public readonly StackPanel Body;
            Border monitorEl, miniCard;
            Grid spotsPanel;
            Canvas overlay;
            TextBlock hintFree;
            bool miniDrag;
            double miniOffX, miniOffY;

            public Picker(Customize c, Func<int> pos, Func<int> raw, Action<int> setPos,
                          Func<int> fx, Func<int> fy, Action<int, int> setFree, Func<Size> cardSize, bool same)
            {
                this.c = c;
                this.pos = pos;
                this.raw = raw;
                this.setPos = setPos;
                this.fx = fx;
                this.fy = fy;
                this.setFree = setFree;
                this.cardSize = cardSize;

                monitorEl = new Border
                {
                    Width = 240, Height = 140, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), Padding = new Thickness(10),
                    HorizontalAlignment = HorizontalAlignment.Center, Cursor = System.Windows.Input.Cursors.Hand,
                };
                monitorEl.SetResourceReference(Border.BackgroundProperty, "CardSurface");
                monitorEl.SetResourceReference(Border.BorderBrushProperty, "ControlStroke");
                spotsPanel = new Grid();
                for (int i = 0; i < 3; i++)
                {
                    spotsPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                    spotsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                }
                for (int i = 0; i < 9; i++)
                {
                    int p = i;
                    var s = new ToggleButton { Style = c.Res<Style>("Spot"), Margin = new Thickness(4) };
                    Grid.SetRow(s, i / 3);
                    Grid.SetColumn(s, i % 3);
                    spotsPanel.Children.Add(s);
                    Spots[i] = s;
                    BindSticky(s, p);
                }
                // In Freedom mode the spots fade and a mini card (proportional to the real one) is dragged instead.
                overlay = new Canvas { Visibility = Visibility.Collapsed };
                miniCard = new Border { CornerRadius = new CornerRadius(3) };
                miniCard.SetResourceReference(Border.BackgroundProperty, "Accent");
                overlay.Children.Add(miniCard);
                var monitorFill = new Grid { Children = { spotsPanel, overlay } };
                monitorEl.Child = monitorFill;
                monitorEl.MouseLeftButtonDown += MonitorDown;
                monitorEl.MouseMove += MonitorMove;
                monitorEl.MouseLeftButtonUp += MonitorUp;
                var neck = new Border { Width = 16, Height = 10, HorizontalAlignment = HorizontalAlignment.Center };
                neck.SetResourceReference(Border.BackgroundProperty, "ControlFill");
                var foot = new Border { Width = 60, Height = 5, CornerRadius = new CornerRadius(3), HorizontalAlignment = HorizontalAlignment.Center };
                foot.SetResourceReference(Border.BackgroundProperty, "ControlFill");
                var monitorBlock = new StackPanel { Children = { monitorEl, neck, foot } };

                var chips = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center,
                                             Margin = new Thickness(0, 12, 0, 0) };
                if (same)
                {
                    ChipSame = new ToggleButton { Style = c.Res<Style>("Chip"), Content = L.PosSame, Margin = new Thickness(0, 0, 8, 0) };
                    BindSticky(ChipSame, -1, () => raw() == -1);
                    chips.Children.Add(ChipSame);
                }
                ChipNear = new ToggleButton { Style = c.Res<Style>("Chip"), Content = L.PosNear, Margin = new Thickness(0, 0, 8, 0) };
                ChipFree = new ToggleButton { Style = c.Res<Style>("Chip"), Content = L.PosFree };
                BindSticky(ChipNear, 9);
                BindSticky(ChipFree, 10);
                chips.Children.Add(ChipNear);
                chips.Children.Add(ChipFree);
                hintFree = new TextBlock { Text = L.HintFree, Style = c.Res<Style>("RowHint"), HorizontalAlignment = HorizontalAlignment.Center,
                                           Margin = new Thickness(0, 8, 0, 0), Visibility = Visibility.Collapsed };
                Body = new StackPanel { Children = { monitorBlock, chips, hintFree } };
            }

            public void Sync()
            {
                int p = pos();
                for (int i = 0; i < Spots.Length; i++) Spots[i].IsChecked = p == i;
                if (ChipSame != null) ChipSame.IsChecked = raw() == -1;
                ChipNear.IsChecked = p == 9;
                ChipFree.IsChecked = p == 10;
                SyncFree();
            }

            // Sticky ToggleButton: clicking the selected one keeps it selected.
            void BindSticky(ToggleButton b, int p) { BindSticky(b, p, () => pos() == p); }

            void BindSticky(ToggleButton b, int p, Func<bool> on)
            {
                b.Checked += delegate { c.Edit(delegate { setPos(p); }); };
                b.Unchecked += delegate
                {
                    if (!c.loading && on())
                    {
                        c.loading = true;
                        b.IsChecked = true;
                        c.loading = false;
                    }
                };
            }

            void SyncFree()
            {
                if (monitorEl == null) return;
                bool free = pos() == 10;
                spotsPanel.IsHitTestVisible = !free;
                spotsPanel.Opacity = free ? 0.25 : 1;
                overlay.Visibility = free ? Visibility.Visible : Visibility.Collapsed;
                hintFree.Visibility = free ? Visibility.Visible : Visibility.Collapsed;
                if (free) c.w.Dispatcher.BeginInvoke(new Action(PlaceMini), DispatcherPriority.Loaded);
            }

            // The mini card is the real card shrunk into the monitor's inner area by the primary-work-area ratio.
            void PlaceMini()
            {
                double iw = overlay.ActualWidth, ih = overlay.ActualHeight;
                if (iw <= 0 || ih <= 0) return;
                Size mini = MiniSize(iw, ih);
                miniCard.Width = mini.Width;
                miniCard.Height = mini.Height;
                Canvas.SetLeft(miniCard, fx() / 1000.0 * (iw - mini.Width));
                Canvas.SetTop(miniCard, fy() / 1000.0 * (ih - mini.Height));
            }

            Size MiniSize(double iw, double ih)
            {
                var wa = SystemParameters.WorkArea;
                double k = c.set.Scale / 100.0;
                Size sz = cardSize();
                double mw = Math.Max(28, Math.Min(iw, sz.Width * k / wa.Width * iw));
                double mh = Math.Max(8, Math.Min(ih, sz.Height * k / wa.Height * ih));
                return new Size(mw, mh);
            }

            void MonitorDown(object s, System.Windows.Input.MouseButtonEventArgs e)
            {
                if (pos() != 10) return;
                Point p = e.GetPosition(overlay);
                if (e.Source == miniCard)
                {
                    miniOffX = p.X - Canvas.GetLeft(miniCard);
                    miniOffY = p.Y - Canvas.GetTop(miniCard);
                }
                else
                {
                    miniOffX = miniCard.Width / 2;
                    miniOffY = miniCard.Height / 2;
                    MoveMini(p);
                }
                miniDrag = true;
                monitorEl.CaptureMouse();
                e.Handled = true;
            }

            void MoveMini(Point p)
            {
                double iw = overlay.ActualWidth, ih = overlay.ActualHeight;
                Canvas.SetLeft(miniCard, Math.Max(0, Math.Min(iw - miniCard.Width, p.X - miniOffX)));
                Canvas.SetTop(miniCard, Math.Max(0, Math.Min(ih - miniCard.Height, p.Y - miniOffY)));
            }

            void MonitorMove(object s, System.Windows.Input.MouseEventArgs e)
            {
                if (miniDrag) MoveMini(e.GetPosition(overlay));
            }

            void MonitorUp(object s, System.Windows.Input.MouseButtonEventArgs e)
            {
                if (!miniDrag) return;
                miniDrag = false;
                monitorEl.ReleaseMouseCapture();
                double iw = overlay.ActualWidth, ih = overlay.ActualHeight;
                int nx = (int)Math.Round(Canvas.GetLeft(miniCard) * 1000 / Math.Max(1, iw - miniCard.Width));
                int ny = (int)Math.Round(Canvas.GetTop(miniCard) * 1000 / Math.Max(1, ih - miniCard.Height));
                nx = Math.Max(0, Math.Min(1000, nx));
                ny = Math.Max(0, Math.Min(1000, ny));
                c.Edit(delegate { setFree(nx, ny); });
            }
        }

        StackPanel BuildBehavior()
        {
            pickMain = new Picker(this, () => set.Position, () => set.Position, p => set.Position = p,
                () => set.FreeX, () => set.FreeY, (x, y) => { set.FreeX = x; set.FreeY = y; },
                () => new Size(card.W, card.H), false);

            sldDur = Slide(500, 10000, 500, v => set.DurationMs = v);
            lblDur = new TextBlock { Style = Res<Style>("ValueText"), Width = 60, Margin = new Thickness(10, 0, 0, 0) };
            var durRow = new StackPanel { Orientation = Orientation.Horizontal, Children = { sldDur, lblDur } };
            chkStay = Toggle();
            BindToggle(chkStay, v => set.StayUntilClicked = v);

            return PageOf(
                Header(L.PositionTitle),
                new TextBlock { Text = L.HintPosition, Style = Res<Style>("RowHint"), Margin = new Thickness(0, 0, 0, 8) },
                Box(pickMain.Body),
                SettingCard(L.DurationTitle, L.HintDuration, durRow),
                SettingCard(L.StayUntilClicked, L.HintStay, chkStay));
        }

        // MARK: Caps Lock

        StackPanel BuildCaps()
        {
            chkCaps = Toggle();
            BindToggle(chkCaps, v => set.CapsHint = v);
            sldCapsDur = Slide(500, 10000, 100, v => set.CapsDurationMs = v);
            lblCapsDur = new TextBlock { Style = Res<Style>("ValueText"), Width = 60, Margin = new Thickness(10, 0, 0, 0) };
            var durRow = new StackPanel { Orientation = Orientation.Horizontal, Children = { sldCapsDur, lblCapsDur } };
            chkCapsStay = Toggle();
            BindToggle(chkCapsStay, v => set.CapsStayUntilClicked = v);
            sldCapsW = Slide(160, 360, 10, v => set.CapsWidth = v);
            lblCapsW = new TextBlock { Style = Res<Style>("ValueText"), Width = 56, Margin = new Thickness(10, 0, 0, 0) };
            var widthRow = new StackPanel { Orientation = Orientation.Horizontal, Children = { sldCapsW, lblCapsW } };
            sldCapsH = Slide(32, 64, 2, v => set.CapsHeight = v);
            lblCapsH = new TextBlock { Style = Res<Style>("ValueText"), Width = 56, Margin = new Thickness(10, 0, 0, 0) };
            var heightRow = new StackPanel { Orientation = Orientation.Horizontal, Children = { sldCapsH, lblCapsH } };
            pickCaps = new Picker(this,
                () => set.CapsPosition < 0 ? set.Position : set.CapsPosition, () => set.CapsPosition, p => set.CapsPosition = p,
                () => set.CapsPosition < 0 ? set.FreeX : set.CapsFreeX, () => set.CapsPosition < 0 ? set.FreeY : set.CapsFreeY,
                (x, y) => { set.CapsPosition = 10; set.CapsFreeX = x; set.CapsFreeY = y; },
                () => new Size(set.CapsWidth, set.CapsHeight), true);

            return PageOf(
                SettingCard(L.MenuCaps, L.HintCaps, chkCaps),
                SettingCard(L.DurationTitle, L.HintDuration, durRow),
                SettingCard(L.StayUntilClicked, L.HintStay, chkCapsStay),
                Header(L.SizeTitle),
                SettingCard(L.CapsWidth, L.HintCapsWidth, widthRow),
                SettingCard(L.CapsHeight, L.HintCapsHeight, heightRow),
                Header(L.PositionTitle),
                new TextBlock { Text = L.HintPosition, Style = Res<Style>("RowHint"), Margin = new Thickness(0, 0, 0, 8) },
                Box(pickCaps.Body));
        }

        // MARK: Wi-Fi

        StackPanel BuildWifi()
        {
            chkWifi = Toggle();
            BindToggle(chkWifi, v => set.WifiHint = v);
            sldWifiDur = Slide(500, 10000, 100, v => set.WifiDurationMs = v);
            lblWifiDur = new TextBlock { Style = Res<Style>("ValueText"), Width = 60, Margin = new Thickness(10, 0, 0, 0) };
            var durRow = new StackPanel { Orientation = Orientation.Horizontal, Children = { sldWifiDur, lblWifiDur } };
            chkWifiStay = Toggle();
            BindToggle(chkWifiStay, v => set.WifiStayUntilClicked = v);
            lblWifiNames = new TextBlock { Style = Res<Style>("ValueText"), VerticalAlignment = VerticalAlignment.Center };
            var link = LinkTo(L.WifiOpenLocation, "ms-settings:privacy-location");
            link.Margin = new Thickness(12, 0, 0, 0);
            var namesRow = new StackPanel { Orientation = Orientation.Horizontal, Children = { lblWifiNames, link } };
            return PageOf(
                SettingCard(L.MenuWifi, L.HintWifi, chkWifi),
                SettingCard(L.DurationTitle, L.HintDuration, durRow),
                SettingCard(L.StayUntilClicked, L.HintStay, chkWifiStay),
                SettingCard(L.WifiNames, L.HintWifiNames, namesRow));
        }

        // MARK: Sound output

        StackPanel BuildAudio()
        {
            chkAudio = Toggle();
            BindToggle(chkAudio, v => set.AudioHint = v);
            sldAudioDur = Slide(500, 10000, 100, v => set.AudioDurationMs = v);
            lblAudioDur = new TextBlock { Style = Res<Style>("ValueText"), Width = 60, Margin = new Thickness(10, 0, 0, 0) };
            var durRow = new StackPanel { Orientation = Orientation.Horizontal, Children = { sldAudioDur, lblAudioDur } };
            chkAudioStay = Toggle();
            BindToggle(chkAudioStay, v => set.AudioStayUntilClicked = v);
            return PageOf(
                SettingCard(L.MenuAudio, L.HintAudio, chkAudio),
                SettingCard(L.DurationTitle, L.HintDuration, durRow),
                SettingCard(L.StayUntilClicked, L.HintStay, chkAudioStay));
        }

        // MARK: About

        StackPanel BuildAbout()
        {
            return PageOf(
                SettingCard(L.AboutVersion, null, new TextBlock { Text = Updater.Show(Updater.Current), Style = Res<Style>("ValueText") }),
                SettingCard(L.AboutBuild, null, new TextBlock { Text = BuildInfo.Commit + " · " + BuildInfo.Date, Style = Res<Style>("ValueText") }),
                SettingCard(L.AboutWebsite, null, LinkTo("luojiajiang.uk/cccc", "https://luojiajiang.uk/cccc")),
                SettingCard(L.AboutDeveloper, null, LinkTo("@zw87652015", "https://github.com/zw87652015")),
                SettingCard(L.AboutContributors, null, new TextBlock { Text = BuildInfo.Contributors.Length > 0 ? string.Join(", ", BuildInfo.Contributors) : "—", Style = Res<Style>("ValueText") }),
                SettingCard(L.AboutLicense, null, LinkTo("MIT", "https://github.com/zw87652015/CCCC/blob/main/LICENSE")));
        }

        Button LinkTo(string text, string url)
        {
            var b = new Button { Style = Res<Style>("Link"), Content = text, VerticalAlignment = VerticalAlignment.Center };
            b.Click += delegate { try { System.Diagnostics.Process.Start(url); } catch { } };
            return b;
        }

        // MARK: Sound

        StackPanel BuildSound()
        {
            chkSound = Toggle();
            BindToggle(chkSound, v => set.Sound = v);
            soundOk = MakeSound(L.CopiedSound, true, () => set.SoundOkFile, v => set.SoundOkFile = v);
            soundFail = MakeSound(L.FailSound, false, () => set.SoundFailFile, v => set.SoundFailFile = v);
            return PageOf(
                SettingCard(L.PlaySounds, L.HintSound, chkSound),
                SoundCard(soundOk),
                SoundCard(soundFail));
        }

        Sound MakeSound(string title, bool ok, Func<string> get, Action<string> put)
        {
            var s = new Sound { Title = title, Get = get, Put = put, Ok = ok };
            s.Hint = new TextBlock { Style = Res<Style>("RowHint") };
            RadioButton[] rbs;
            s.Seg = SegOf(new[] { L.SoundBuiltIn, L.SoundCustom }, out rbs);
            s.BuiltIn = rbs[0];
            s.Custom = rbs[1];
            BindRadio(s.BuiltIn, delegate { put(null); });
            s.Custom.Checked += delegate { if (!loading && get() == null) Browse(s); };
            s.Browse = new Button { Style = Res<Style>("Subtle"), Content = L.Browse, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            s.Browse.Click += delegate { Browse(s); };
            s.Play = new Button { Style = Res<Style>("RoundIcon"), Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            s.Play.Content = new TextBlock { Text = "\uE768", Style = Res<Style>("Glyph"), FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center };
            s.Play.Click += delegate { test(ok); };
            return s;
        }

        UIElement SoundCard(Sound s)
        {
            var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
            left.Children.Add(new TextBlock { Text = s.Title, Style = Res<Style>("RowTitle") });
            left.Children.Add(s.Hint);
            var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center,
                                         Children = { s.Seg, s.Browse, s.Play } };
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(left);
            Grid.SetColumn(right, 1);
            g.Children.Add(right);
            return Box(g);
        }

        void ShowSound(Sound s)
        {
            string path = s.Get();
            s.BuiltIn.IsChecked = path == null;
            s.Custom.IsChecked = path != null;
            bool custom = s.Custom.IsChecked == true;
            s.Hint.Text = !custom ? L.SoundBuiltInHint : path == null ? L.SoundNoFile : System.IO.Path.GetFileName(path);
            s.Hint.ToolTip = custom ? path : null;
            s.Browse.Visibility = custom ? Visibility.Visible : Visibility.Collapsed;
        }

        void Browse(Sound s)
        {
            var dlg = new OpenFileDialog { Filter = L.AudioFilter };
            if (dlg.ShowDialog(w) == true)
            {
                string file = dlg.FileName;
                Edit(delegate { s.Put(file); });
            }
        }

        // MARK: Source

        StackPanel BuildSource()
        {
            chkSource = Toggle();
            BindToggle(chkSource, v => set.ShowSource = v);
            RadioButton[] when;
            var whenSeg = SegOf(new[] { L.SourceAlways, L.SourceAuto }, out when);
            rbWhen = when;
            BindRadio(rbWhen[0], delegate { set.SourceWhen = 0; });
            BindRadio(rbWhen[1], delegate { set.SourceWhen = 1; });
            whenPanel = Box(CardGrid(L.SourceWhen, L.HintWhen, whenSeg));

            var list = new StackPanel();
            string[] names = { L.DetailIcon, L.DetailApp, L.DetailSite, L.DetailTitle };
            string[] hints = { L.HintDetailIcon, L.HintDetailApp, L.HintDetailSite, L.HintDetailTitle };
            for (int i = 0; i < 4; i++)
            {
                int d = i;
                var content = new StackPanel();
                content.Children.Add(new TextBlock { Text = names[i], Style = Res<Style>("RowTitle") });
                content.Children.Add(new TextBlock { Text = hints[i], Style = Res<Style>("RowHint") });
                rbDetail[i] = new RadioButton { Style = Res<Style>("Radio"), Content = content };
                BindRadio(rbDetail[i], delegate { set.SourceDetail = d; });
                list.Children.Add(rbDetail[i]);
                if (i < 3) list.Children.Add(Divider());
            }
            detailPanel = Box(list);
            // Everything below the switch only matters while it's on, so it folds away instead of greying out.
            sourceBody = PageOf(whenPanel, Header(L.SourceDetail), detailPanel);
            return PageOf(SettingCard(L.MenuSource, L.HintSource, chkSource), sourceBody);
        }

        // MARK: Reset (two steps)

        void OnReset()
        {
            if (!armed)
            {
                armed = true;
                btnReset.Content = L.ResetArmed;
                armTimer.Start();
                return;
            }
            armTimer.Stop();
            armed = false;
            btnReset.Content = L.ResetDefaults;
            Edit(delegate { set.ResetDisplay(); });
        }

        // Test hooks: the harness reads the window content and drives controls through these.
        public FrameworkElement Root { get { return (FrameworkElement)w.Content; } }
        public string TitleText { get { return txtTitle.Text; } }
        public string HexText { get { return txtHex == null ? null : txtHex.Text; } }
        public bool PopupOpen { get { return popColour != null && popColour.IsOpen; } }
        public Popup ColourPopup { get { return popColour; } }
        public void OpenColourPopup(int index) { OpenPopup(colours[index]); }
        public void OpenFontDropdown() { cmbFont.IsDropDownOpen = true; }
        public void TypeHex(string text) { txtHex.Text = text; }
        public void SelectPage(int index) { ShowPage(index); }
        public void PressPreset(int i) { presetButtons[i].RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
        public void ScrollToBottom() { pageScroll.ScrollToVerticalOffset(pageScroll.ScrollableHeight); }
        public void PressReset() { btnReset.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
        public void Close() { w.Close(); }

        // Device-pixel rectangle of the window on screen, for the on-screen capture.
        public System.Drawing.Rectangle ScreenRect()
        {
            double scale = PresentationSource.FromVisual(w).CompositionTarget.TransformToDevice.M11;
            return new System.Drawing.Rectangle((int)(w.Left * scale), (int)(w.Top * scale),
                                                (int)(w.ActualWidth * scale), (int)(w.ActualHeight * scale));
        }

        public object Get(string key)
        {
            if (key != null && key.Length == 5 && key.StartsWith("spot", StringComparison.Ordinal))
            {
                int n = key[4] - '0';
                if (n >= 0 && n < 9) return pickMain.Spots[n];
            }
            switch (key)
            {
                case "pause": return chkPause;
                case "warn": return chkWarn;
                case "caps": return chkCaps;
                case "auto": return chkAuto;
                case "sound": return chkSound;
                case "source": return chkSource;
                case "stay": return chkStay;
                case "simple": return chkSimple;
                case "trans": return sldTrans;
                case "full": return chkFull;
                case "size": return sldSize;
                case "text": return sldText;
                case "duration": return sldDur;
                case "theme1": return rbTheme[1];
                case "lang2": return rbLang[2];
                case "when1": return rbWhen[1];
                case "detail2": return rbDetail[2];
                case "near": return pickMain.ChipNear;
                case "free": return pickMain.ChipFree;
                case "capsWidth": return sldCapsW;
                case "capsDuration": return sldCapsDur;
                case "capsStay": return chkCapsStay;
                case "tray": return chkTray;
                case "wifi": return chkWifi;
                case "wifiDuration": return sldWifiDur;
                case "wifiStay": return chkWifiStay;
                case "wifiNames": return lblWifiNames;
                case "audio": return chkAudio;
                case "audioDuration": return sldAudioDur;
                case "audioStay": return chkAudioStay;
                case "capsHeight": return sldCapsH;
                case "capsSame": return pickCaps.ChipSame;
            }
            return null;
        }
    }
}
