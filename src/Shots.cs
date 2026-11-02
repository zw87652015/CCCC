using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Cccc
{
    // Store listing screenshots (`CCCC.exe --shots <dir>`): 1920x1080 logical units, scaled by the primary monitor's DPI.
    static class Shots
    {
        const float Gap = 28;
        const float RegionX = 820, RegionW = 980;
        static float k;
        static bool zh;
        static int W, H;

        public static void Render(string dir)
        {
            Directory.CreateDirectory(dir);
            using (var screen = Graphics.FromHwnd(IntPtr.Zero)) k = screen.DpiX / 96f;
            zh = L.Zh;
            W = (int)Math.Round(1920 * k);
            H = (int)Math.Round(1080 * k);
            float baseW = new Card().CanvasW;
            float scale = 860 / baseW * k;
            Save(Shot1(scale), dir, "01.png");
            Save(Shot2(scale), dir, "02.png");
            Save(Shot3(scale), dir, "03.png");
            Save(Shot4((RegionW - Gap) / 2 / baseW * k), dir, "04.png");
            Save(Shot5(scale), dir, "05.png");
            Save(Shot6(), dir, "06.png");
        }

        static void Save(Bitmap b, string dir, string name)
        {
            using (b) b.Save(Path.Combine(dir, name), ImageFormat.Png);
        }

        static string Pick(string en, string z)
        {
            return zh ? z : en;
        }

        static Graphics Setup(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            return g;
        }

        static Bitmap Background(bool dark)
        {
            var bmp = new Bitmap(W, H, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            using (var b = new LinearGradientBrush(new Rectangle(0, 0, W, H),
                dark ? Color.FromArgb(28, 34, 52) : Color.FromArgb(206, 218, 236),
                dark ? Color.FromArgb(52, 40, 64) : Color.FromArgb(232, 220, 226), 90f))
                g.FillRectangle(b, 0, 0, W, H);
            return bmp;
        }

        // Vertical tops that centre a stack of heights (device px) in the canvas.
        static float[] Stack(params int[] heights)
        {
            float total = (heights.Length - 1) * Gap * k;
            foreach (int h in heights) total += h;
            var tops = new float[heights.Length];
            float y = (H - total) / 2;
            for (int i = 0; i < heights.Length; i++)
            {
                tops[i] = y;
                y += heights[i] + Gap * k;
            }
            return tops;
        }

        static float CenterX(int pw)
        {
            return (RegionX + RegionW / 2) * k - pw / 2f;
        }

        // Draws one frozen card at (x, y); glass cards refract `wall` (the background under the card).
        static void Draw(Graphics g, Card card, Tuple<Content, bool, double> row, float x, float y, int[] wall)
        {
            if (wall != null) card.Backdrop(wall, W, (int)x, (int)y);
            card.Still(row.Item1, row.Item2, row.Item3);
            g.DrawImage(card.Pixels, x, y);
        }

        static void Caption(Graphics g, bool dark, string head, string sub)
        {
            string face = zh ? "Microsoft YaHei UI" : "Segoe UI";
            float x = 120 * k, width = 640 * k;
            using (var brand = new Font(zh ? face : "Segoe UI Semibold", 26 * k, zh ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel))
            using (var fh = new Font(face, 60 * k, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var fs = new Font(face, 28 * k, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var bh = new SolidBrush(dark ? Color.White : Color.FromArgb(26, 26, 26)))
            using (var bs = new SolidBrush(dark ? Color.FromArgb(178, 255, 255, 255) : Color.FromArgb(95, 95, 95)))
            using (var icon = Icons.App((int)Math.Round(40 * k)))
            {
                float hh = g.MeasureString(head, fh, (int)width).Height;
                float sh = g.MeasureString(sub, fs, (int)width).Height;
                float top = (H - (40 * k + 36 * k + hh + 24 * k + sh)) / 2;
                float bt = g.MeasureString("CtrlCCCC", brand).Height;
                g.DrawImage(icon, x, top);
                g.DrawString("CtrlCCCC", brand, bh, x + 52 * k, top + (40 * k - bt) / 2);
                g.DrawString(head, fh, bh, new RectangleF(x, top + 76 * k, width, hh));
                g.DrawString(sub, fs, bs, new RectangleF(x, top + 76 * k + hh + 24 * k, width, sh));
            }
        }

        static Bitmap Shot1(float scale)
        {
            var bmp = Background(true);
            using (var g = Setup(Graphics.FromImage(bmp)))
            {
                var top = new Card();
                var bottom = new Card();
                top.Layout(scale, Theme.DarkTheme);
                bottom.Layout(scale, Theme.DarkTheme);
                float[] y = Stack(top.Pixels.Height, bottom.Pixels.Height);
                Draw(g, top, Sheet.Rows()[0], CenterX(top.Pixels.Width), y[0], null);
                Draw(g, bottom, Sheet.Rows()[2], CenterX(bottom.Pixels.Width), y[1], null);
                Caption(g, true,
                    Pick("It's copied.\nYou can stop.", "复制上了，别按了。"),
                    Pick("A small card confirms every Ctrl+C, Ctrl+X and Ctrl+Insert.", "每次 Ctrl+C、Ctrl+X、Ctrl+Insert，都有一张小卡片告诉你。"));
            }
            return bmp;
        }

        static Bitmap Shot2(float scale)
        {
            var bmp = Background(false);
            using (var g = Setup(Graphics.FromImage(bmp)))
            {
                var a = new Card();
                var b = new Card();
                a.Layout(scale, Theme.LightTheme);
                b.Layout(scale, Theme.LightTheme);
                float[] y = Stack(a.Pixels.Height, b.Pixels.Height);
                Draw(g, a, Sheet.Rows()[6], CenterX(a.Pixels.Width), y[0], null);
                Draw(g, b, Sheet.Rows()[13], CenterX(b.Pixels.Width), y[1], null);
                Caption(g, false,
                    Pick("Tells you when\nnothing was copied.", "没复制上，\n也会告诉你。"),
                    Pick("Pressed Ctrl+C on an empty selection? CCCC says so, and shows what the clipboard still holds.", "选区是空的？CCCC 会提醒你，并显示剪贴板里现在还是什么。"));
            }
            return bmp;
        }

        static Bitmap Shot3(float scale)
        {
            var bmp = Background(true);
            using (var g = Setup(Graphics.FromImage(bmp)))
            {
                var card = new Card();
                card.Apply(new Settings { OpenLinkOnClick = true });
                card.Layout(scale, Theme.DarkTheme);
                int pw = card.Pixels.Width, ph = card.Pixels.Height;
                float[] y = Stack(ph, ph, ph);
                Draw(g, card, Sheet.Rows()[10], CenterX(pw), y[0], null);
                Draw(g, card, Sheet.Rows()[3], CenterX(pw), y[1], null);
                Draw(g, card, Sheet.Rows()[4], CenterX(pw), y[2], null);
                Caption(g, true,
                    Pick("See what you copied,\nand from where.", "复制了什么、\n从哪复制，一眼看清。"),
                    Pick("Text length, links, images and files, with the app and page they came from.", "字数、链接、图片和文件，\n还有来源应用和网页。"));
            }
            return bmp;
        }

        static Bitmap Shot4(float scale)
        {
            var bmp = Background(false);
            using (var g = Setup(Graphics.FromImage(bmp)))
            {
                var rnd = new Random(11);
                Color[] palette = { Color.FromArgb(255, 120, 160), Color.FromArgb(110, 190, 255), Color.FromArgb(255, 205, 90),
                    Color.FromArgb(160, 130, 255), Color.FromArgb(90, 215, 175), Color.FromArgb(255, 150, 100) };
                for (int i = 0; i < 6; i++)
                {
                    float rl = rnd.Next(120, 261);
                    float cxl = RegionX - 40 + rl * 1.15f + rnd.Next(0, (int)RegionW);
                    float r = rl * k, cx = cxl * k, cy = rnd.Next(0, 1081) * k;
                    Color c = palette[i];
                    using (var halo = new SolidBrush(Color.FromArgb(60, c))) g.FillEllipse(halo, cx - r * 1.15f, cy - r * 1.15f, r * 2.3f, r * 2.3f);
                    using (var core = new SolidBrush(Color.FromArgb(170, c))) g.FillEllipse(core, cx - r, cy - r, 2 * r, 2 * r);
                }

                int[] wall = Alpha.Read(bmp);
                Theme[] themes = { Theme.DarkTheme, Theme.LightTheme, Theme.GlassDark, Theme.GlassLight };
                var cards = new Card[themes.Length];
                for (int i = 0; i < themes.Length; i++)
                {
                    cards[i] = new Card();
                    cards[i].Layout(scale, themes[i]);
                }
                int pw = cards[0].Pixels.Width, ph = cards[0].Pixels.Height;
                float[] rowY = Stack(ph, ph);
                float[] colX = { RegionX * k, (RegionX + RegionW) * k - pw };
                for (int i = 0; i < themes.Length; i++)
                    Draw(g, cards[i], Sheet.Rows()[0], colX[i % 2], rowY[i / 2], themes[i].Glass ? wall : null);
                Caption(g, false,
                    Pick("Make it yours.", "按你的喜好来。"),
                    Pick("Dark, light and glass themes, plus size, font, colours, position and sounds.", "深色、浅色和玻璃主题，大小、字体、配色、位置和提示音都能调。"));
            }
            return bmp;
        }

        static Bitmap Shot5(float scale)
        {
            var bmp = Background(true);
            using (var g = Setup(Graphics.FromImage(bmp)))
            {
                var caps = new Card(Role.Caps);
                var wifi = new Card(Role.Wifi);
                var audio = new Card(Role.Audio);
                caps.Layout(scale, Theme.DarkTheme);
                wifi.Layout(scale, Theme.DarkTheme);
                audio.Layout(scale, Theme.DarkTheme);
                float[] y = Stack(caps.Pixels.Height, wifi.Pixels.Height, audio.Pixels.Height);
                Draw(g, caps, Sheet.Rows()[8], CenterX(caps.Pixels.Width), y[0], null);
                Draw(g, wifi, Sheet.Rows()[14], CenterX(wifi.Pixels.Width), y[1], null);
                Draw(g, audio, Sheet.Rows()[19], CenterX(audio.Pixels.Width), y[2], null);
                Caption(g, true,
                    Pick("Caps Lock, Wi-Fi\nand sound, too.", "大写锁定、Wi-Fi\n和声音也有卡片。"),
                    Pick("Optional cards when Caps Lock toggles, Wi-Fi changes, or the sound output switches.", "大写锁定切换、Wi-Fi 变化、声音输出切换时，可选弹出提示。"));
            }
            return bmp;
        }

        static Bitmap Shot6()
        {
            var wifi = new WifiWatch();
            Customize.UseMica = false;
            var c = new Customize(new Settings { ThemeMode = 2 }, () => { }, b => { }, () => { }, () => { }, () => { }, b => { }, wifi);
            c.SelectPage(1);
            c.Window.Topmost = true;
            c.Show();
            Pump(600);
            Native.RECT r;
            Native.DwmGetWindowAttribute(new WindowInteropHelper(c.Window).Handle, 9, out r, Marshal.SizeOf(typeof(Native.RECT)));
            var win = new Bitmap(r.Right - r.Left - 2, r.Bottom - r.Top - 2);
            using (var g = Graphics.FromImage(win)) g.CopyFromScreen(r.Left + 1, r.Top + 1, 0, 0, win.Size);
            c.Close();
            wifi.Stop();

            var bmp = Background(true);
            using (var g = Setup(Graphics.FromImage(bmp)))
            {
                float x = CenterX(win.Width), y = (H - win.Height) / 2f, radius = 8 * k;
                for (int i = 16; i >= 1; i--)
                {
                    float d = i * 2 * k;
                    using (var p = Shapes.RoundRect(new RectangleF(x - d, y - d + 8 * k, win.Width + 2 * d, win.Height + 2 * d), radius + d))
                    using (var br = new SolidBrush(Color.FromArgb(7, 0, 0, 0)))
                        g.FillPath(br, p);
                }
                using (var p = Shapes.RoundRect(new RectangleF(x, y, win.Width, win.Height), radius))
                {
                    g.SetClip(p);
                    g.DrawImage(win, x, y);
                    g.ResetClip();
                    using (var pen = new Pen(Color.FromArgb(40, 255, 255, 255), 1)) g.DrawPath(pen, p);
                }
                win.Dispose();
                Caption(g, true,
                    Pick("Everything in\none window.", "所有设置，\n一个窗口搞定。"),
                    Pick("Lives in the tray.\nNo account, no ads, nothing uploaded.", "住在托盘里。\n不用注册，没有广告，什么都不上传。"));
            }
            return bmp;
        }

        static void Pump(int ms)
        {
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer(TimeSpan.FromMilliseconds(ms), DispatcherPriority.Background,
                delegate { frame.Continue = false; }, Dispatcher.CurrentDispatcher);
            timer.Start();
            Dispatcher.PushFrame(frame);
            timer.Stop();
        }
    }
}
