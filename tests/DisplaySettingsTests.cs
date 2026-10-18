using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Cccc
{
    static class DisplaySettingsTests
    {
        static int checks;
        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        static void Check(bool ok, string name) { if (!ok) throw new Exception(name); checks++; }
        static object Field(Card c, string name) { return typeof(Card).GetField(name, Private).GetValue(c); }
        static void Prepare(Card c) { typeof(Card).GetMethod("Prepare", Private).Invoke(c, null); }
        static int Px(float n, float scale) { return (int)Math.Round(n * scale); }

        [STAThread]
        static void Main(string[] args)
        {
            string key = (string)typeof(Settings).GetField("Key", BindingFlags.NonPublic | BindingFlags.Static).GetRawConstantValue();
            try
            {
                var defaults = Settings.Load();
                Check(defaults.Appearance == "auto" && defaults.DurationMs == 0 && defaults.Opacity == 100 && !defaults.Simple, "migration defaults");
                foreach (string theme in new[] { "auto", "light", "dark" })
                foreach (int ms in new[] { 0, 1000, 2000, 3000, 5000, 10000 })
                foreach (int opacity in new[] { 100, 90, 75, 50, 25 })
                foreach (bool simple in new[] { false, true })
                {
                    var settings = new Settings { Appearance = theme, DurationMs = ms, Opacity = opacity, Simple = simple, Position = "right", Lang = "zh", Sound = true };
                    settings.Save();
                    var loaded = Settings.Load();
                    Check(loaded.Appearance == theme && loaded.DurationMs == ms && loaded.Opacity == opacity && loaded.Simple == simple, "preferences survive reload");
                    Check(loaded.Position == "right" && loaded.Lang == "zh" && loaded.Sound, "existing preferences preserved");
                }
                using (var k = Registry.CurrentUser.CreateSubKey(key))
                {
                    k.SetValue("Appearance", "unknown"); k.SetValue("DurationMs", -1); k.SetValue("Opacity", 0); k.SetValue("Simple", "yes");
                    var invalid = Settings.Load();
                    Check(invalid.Appearance == "auto" && invalid.DurationMs == 0 && invalid.Opacity == 100 && !invalid.Simple, "invalid preferences fall back safely");
                    foreach (object bad in new object[] { 1, 30001, "5000" })
                    {
                        k.SetValue("DurationMs", bad);
                        Check(Settings.Load().DurationMs == 0, "invalid duration rejected");
                    }
                    foreach (object bad in new object[] { -1, 101, "50" })
                    {
                        k.SetValue("Opacity", bad);
                        Check(Settings.Load().Opacity == 100, "invalid opacity rejected");
                    }
                }

                L.Apply("zh");
                var card = new Card();
                foreach (bool simple in new[] { false, true })
                foreach (string appearance in new[] { "auto", "light", "dark" })
                {
                    card.Configure(new Settings { Simple = simple, Appearance = appearance });
                    Prepare(card);
                    var th = (Theme)Field(card, "th");
                    Check(th == (appearance == "auto" ? Theme.Current() : appearance == "light" ? Theme.LightTheme : Theme.DarkTheme), "selected theme used by renderer");
                    foreach (float scale in new[] { 1f, 1.25f, 1.5f, 2f })
                    {
                        card.Layout(scale, th);
                        Check(card.Pixels.Width == (int)Math.Ceiling((simple ? 268 : 368) * scale) && card.Pixels.Height == (int)Math.Ceiling((simple ? 100 : 116) * scale), "layout size at different DPI");
                        card.Still(new Content { Title = "已复制", Sub = "example preview", Meta = "14 字" }, false, 1.2);
                    }
                    foreach (string position in new[] { "left", "center", "right" })
                    {
                        card.Position = position;
                        Prepare(card);
                        float scale = (float)Field(card, "s");
                        int x = (int)Field(card, "baseX"), y = (int)Field(card, "baseY");
                        Native.POINT cp; Native.GetCursorPos(out cp);
                        IntPtr fg = Native.GetForegroundWindow();
                        IntPtr mon = fg != IntPtr.Zero ? Native.MonitorFromWindow(fg, 2) : Native.MonitorFromPoint(cp, 2);
                        var mi = new Native.MONITORINFO { cbSize = Marshal.SizeOf(typeof(Native.MONITORINFO)) };
                        Native.GetMonitorInfo(mon, ref mi);
                        int left = x + Px(Card.CX, scale), width = Px(simple ? 220 : 320, scale);
                        if (position == "left") Check(left == mi.rcWork.Left + Px(16, scale), "left margin");
                        if (position == "center") Check(Math.Abs(left * 2 + width - mi.rcWork.Left - mi.rcWork.Right) <= 1, "center alignment");
                        if (position == "right") Check(left + width == mi.rcWork.Right - Px(16, scale), "right margin");
                        Check(y + Px(Card.CY + (simple ? 48 : 64), scale) == mi.rcWork.Bottom - Px(16, scale), "bottom margin");
                    }
                }

                // Avoid displaying a test window: exercise the already-visible update path.
                typeof(Card).GetField("shown", Private).SetValue(card, true);
                foreach (int ms in new[] { 0, 1000, 2000, 3000, 5000, 10000 })
                foreach (Kind kind in new[] { Kind.Copied, Kind.Cut, Kind.Fail, Kind.Info, Kind.Empty })
                {
                    card.Configure(new Settings { DurationMs = ms });
                    card.Show(new Content { Kind = kind, Title = "Test" }, false);
                    double expected = ms > 0 ? ms / 1000.0 : kind == Kind.Info || kind == Kind.Empty ? 2.6 : 1.9;
                    Check(Math.Abs((double)Field(card, "duration") - expected) < 0.001, "duration applies to every notification kind");
                    Check(Math.Abs((double)Field(card, "deadline") - (double)Field(card, "tEvent") - expected) < 0.001, "new notification resets lifetime");
                }
                typeof(Card).GetField("shown", Private).SetValue(card, false);
                card.Configure(new Settings { Simple = true }); card.Layout(1, Theme.LightTheme);
                card.Still(new Content { Title = "已复制", Sub = "secret A", Meta = "A", Count = 1 }, false, 1.2);
                using (var original = (Bitmap)card.Pixels.Clone())
                using (var thumb = new Bitmap(40, 40))
                {
                    card.Still(new Content { Title = "已复制", Sub = "secret B", Meta = "B", Count = 99, Thumb = thumb }, false, 1.2);
                    bool same = true;
                    for (int y = 0; y < original.Height && same; y++)
                    for (int x = 0; x < original.Width && same; x++) same = original.GetPixel(x, y) == card.Pixels.GetPixel(x, y);
                    Check(same, "simple mode never draws content, metadata, thumbnail or badge");
                }
                RenderSamples(card, args[0]);
                Console.WriteLine("PASS: " + checks + " display settings checks.");
            }
            finally { Registry.CurrentUser.DeleteSubKeyTree(key, false); }
        }

        static void RenderSamples(Card card, string output)
        {
            using (var sheet = new Bitmap(1104, 600))
            using (var g = Graphics.FromImage(sheet))
            {
                g.Clear(Color.FromArgb(210, 219, 232));
                int row = 0;
                foreach (bool simple in new[] { false, true })
                foreach (Theme theme in new[] { Theme.LightTheme, Theme.DarkTheme })
                {
                    card.Configure(new Settings { Simple = simple }); card.Layout(1.5f, theme);
                    card.Still(new Content { Title = "已复制", Sub = "放心复制：让 Ctrl+C 也有看得见的成功", Meta = "24 字" }, false, 1.2);
                    g.DrawImage(card.Pixels, (row % 2) * 552, (row / 2) * 190);
                    row++;
                }
                card.Configure(new Settings()); card.Layout(1.5f, Theme.DarkTheme);
                card.Still(new Content { Kind = Kind.Fail, Title = "没有复制上", Sub = "可能没有选中内容" }, false, 1.2);
                g.DrawImage(card.Pixels, 0, 380);
                using (var attributes = new ImageAttributes())
                {
                    var matrix = new ColorMatrix(); matrix.Matrix33 = 0.5f;
                    attributes.SetColorMatrix(matrix);
                    g.DrawImage(card.Pixels, new Rectangle(552, 380, card.Pixels.Width, card.Pixels.Height), 0, 0, card.Pixels.Width, card.Pixels.Height, GraphicsUnit.Pixel, attributes);
                }
                sheet.Save(System.IO.Path.Combine(output, "display-settings.png"));
            }
        }
    }
}
