using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Cccc
{
    // `CCCC.exe --sheet out.png` renders every card state in both themes, for design review.
    static class Sheet
    {
        public static void Render(string path)
        {
            const float s = 1.5f;
            var card = new Card();
            card.Apply(new Settings { OpenLinkOnClick = true });
            var capsCard = new Card(Role.Caps);
            var wifiCard = new Card(Role.Wifi);
            var audioCard = new Card(Role.Audio);
            int cw = (int)Math.Ceiling(card.CanvasW * s), ch = (int)Math.Ceiling(card.CanvasH * s);
            var rows = Rows();
            int iconRow = 120;
            using (var sheet = new Bitmap(cw * 4, ch * rows.Length + iconRow, PixelFormat.Format32bppArgb))
            using (var g = Graphics.FromImage(sheet))
            {
                using (var b = new LinearGradientBrush(new Rectangle(0, 0, cw, sheet.Height), Color.FromArgb(28, 34, 52), Color.FromArgb(52, 40, 64), 90f))
                    g.FillRectangle(b, 0, 0, cw, sheet.Height);
                using (var b = new LinearGradientBrush(new Rectangle(cw, 0, cw, sheet.Height), Color.FromArgb(206, 218, 236), Color.FromArgb(232, 220, 226), 90f))
                    g.FillRectangle(b, cw, 0, cw, sheet.Height);
                Wallpaper(g, new Rectangle(cw * 2, 0, cw, sheet.Height), true);
                Wallpaper(g, new Rectangle(cw * 3, 0, cw, sheet.Height), false);
                int[] wall = Alpha.Read(sheet);

                var themes = new[] { Theme.DarkTheme, Theme.LightTheme, Theme.GlassDark, Theme.GlassLight };
                for (int col = 0; col < themes.Length; col++)
                {
                    card.Layout(s, themes[col]);
                    capsCard.Layout(s, themes[col]);
                    wifiCard.Layout(s, themes[col]);
                    audioCard.Layout(s, themes[col]);
                    for (int row = 0; row < rows.Length; row++)
                    {
                        var r = rows[row];
                        var which = r.Item1.Kind == Kind.Caps ? capsCard
                            : r.Item1.Kind == Kind.WifiUp || r.Item1.Kind == Kind.WifiDown ? wifiCard
                            : r.Item1.Kind == Kind.Audio || r.Item1.Kind == Kind.AudioWarn ? audioCard : card;
                        int px = col * cw, py = row * ch + ch - (int)Math.Ceiling(which.CanvasH * s);
                        if (themes[col].Glass) which.Backdrop(wall, sheet.Width, px, py);
                        which.Still(r.Item1, r.Item2, r.Item3);
                        g.DrawImage(which.Pixels, px, py);
                    }
                }

                int y = ch * rows.Length + 20, x = 30;
                using (var app = Icons.App(64)) g.DrawImage(app, x, y);
                x += 90;
                foreach (int sz in new[] { 16, 24, 32 })
                {
                    using (var t = Icons.Tray(sz, false, false)) g.DrawImage(t, x, y + 20);
                    using (var t = Icons.Tray(sz, true, false)) g.DrawImage(t, cw + x, y + 20);
                    x += sz + 20;
                }
                sheet.Save(path, ImageFormat.Png);
            }
        }

        // A busy, colourful backdrop with hard edges, so the glass blur and rim bending show.
        static void Wallpaper(Graphics g, Rectangle r, bool dark)
        {
            Color a = dark ? Color.FromArgb(20, 40, 110) : Color.FromArgb(120, 180, 255);
            Color b = dark ? Color.FromArgb(120, 30, 90) : Color.FromArgb(255, 170, 120);
            using (var br = new LinearGradientBrush(r, a, b, 60f)) g.FillRectangle(br, r);
            var state = g.Save();
            g.SetClip(r);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rnd = new Random(7);
            for (int y = r.Top - 40; y < r.Bottom; y += 70)
            {
                int x = r.Left + rnd.Next(-60, r.Width);
                int d = rnd.Next(40, 110);
                Color c = Color.FromArgb(dark ? 150 : 170, rnd.Next(60, 256), rnd.Next(60, 256), rnd.Next(60, 256));
                using (var br = new SolidBrush(c)) g.FillEllipse(br, x, y, d, d);
            }
            using (var pen = new Pen(Color.FromArgb(dark ? 90 : 120, 255, 255, 255), 6))
                for (int x = r.Left - r.Height; x < r.Right; x += 46)
                    g.DrawLine(pen, x, r.Top, x + r.Height, r.Bottom);
            using (var font = new Font("Segoe UI Semibold", 22f, GraphicsUnit.Pixel))
            using (var br = new SolidBrush(dark ? Color.White : Color.FromArgb(20, 20, 30)))
                for (int y = r.Top + 30; y < r.Bottom; y += 95)
                    g.DrawString("Liquid Glass  液态玻璃  CCCC", font, br, r.Left + 14, y);
            g.Restore(state);
        }

        static Tuple<Content, bool, double>[] Rows()
        {
            var thumb = new Bitmap(80, 80);
            using (var g = Graphics.FromImage(thumb))
            using (var b = new LinearGradientBrush(new Rectangle(0, 0, 80, 80), Color.FromArgb(255, 120, 80), Color.FromArgb(90, 80, 220), 45f))
            {
                g.FillRectangle(b, 0, 0, 80, 80);
                g.FillEllipse(Brushes.White, 46, 14, 20, 20);
            }
            string text = L.Zh ? "放心复制：让 Ctrl+C 也有看得见的成功" : "The quick brown fox jumps over the lazy dog";
            var appIcon = Icons.App(32);
            return new[]
            {
                Row(new Content { Kind = Kind.Copied, Title = L.Again[0], Meta = L.Chars(text.Length), Sub = text }, false, 1.2),
                Row(new Content { Kind = Kind.Copied, Title = L.Again[0], Meta = L.Chars(text.Length), Sub = text }, false, 0.17),
                Row(new Content { Kind = Kind.Copied, Title = L.Again[2], Meta = L.Chars(text.Length), Sub = text, Count = 3 }, true, 0.9),
                Row(new Content { Kind = Kind.Copied, Title = L.Again[0], Meta = L.Link, Sub = "github.com/microsoft/terminal/issues/1234", Url = "https://github.com/microsoft/terminal/issues/1234" }, false, 1.2),
                Row(new Content { Kind = Kind.Copied, Title = L.Again[0], Meta = L.Image, Sub = "1920 × 1080", Thumb = thumb }, false, 1.2),
                Row(new Content { Kind = Kind.Cut, Title = L.Cut, Meta = L.Items(3), Sub = "report.pdf, photo.png, 笔记.txt" }, false, 1.2),
                Row(new Content { Kind = Kind.Fail, Title = L.NothingCopied, Sub = L.StillHolding + text, Count = 2 }, true, 1.2),
                Row(new Content { Kind = Kind.Info, Title = L.ReadyTitle, Sub = L.ReadySub }, false, 1.2),
                Row(new Content { Kind = Kind.Caps, Title = L.Caps, CapsOn = true }, false, 1.2),
                Row(new Content { Kind = Kind.Caps, Title = L.Caps }, false, 1.2),
                Row(new Content { Kind = Kind.Copied, Title = L.Again[0], Meta = L.Chars(text.Length), Sub = text, SrcIcon = appIcon, SrcText = "Google Chrome · github.com", SrcLine = true }, false, 1.2),
                Row(new Content { Kind = Kind.Copied, Title = L.Again[0], Meta = L.Link, Sub = "github.com/microsoft/terminal/issues/1234", Url = "https://github.com/microsoft/terminal/issues/1234", SrcIcon = appIcon }, false, 1.2),
                Row(new Content { Kind = Kind.Copied, Title = L.Again[0], Meta = L.Chars(text.Length), Sub = text, SrcIcon = appIcon, SrcText = "Visual Studio Code · github.com · Pull request #1234: Add customizable source display", SrcLine = true }, false, 1.2),
                Row(new Content { Kind = Kind.Fail, Title = L.NothingCopied, Sub = L.StillHolding + text, SrcIcon = appIcon, SrcText = "Microsoft Word" }, false, 1.2),
                Row(new Content { Kind = Kind.WifiUp, Title = L.WifiSwitched("CoffeeShop"), Sub = L.Detail(L.Band(5), L.Signal(82), L.WifiFrom("HomeNet")) }, false, 1.2),
                Row(new Content { Kind = Kind.WifiUp, Title = L.WifiNowBand("HomeNet", 5), Sub = L.Detail(L.Signal(74), L.WifiWasBand(2)) }, false, 1.2),
                Row(new Content { Kind = Kind.WifiUp, Title = L.WifiReconnected("HomeNet"), Sub = L.Detail(L.Band(2), L.Signal(91), L.WifiOffline(6)) }, false, 1.2),
                Row(new Content { Kind = Kind.WifiDown, Title = L.WifiDisconnected, Sub = L.WifiWasOn("CoffeeShop") }, false, 1.2),
                Row(new Content { Kind = Kind.AudioWarn, Glyph = 0, Title = L.AudioTitle("Speakers"), Sub = L.Detail(L.AudioVolume(64), L.AudioWas("Headphones"), "Realtek(R) Audio") }, false, 1.2),
                Row(new Content { Kind = Kind.Audio, Glyph = 1, Title = L.AudioTitle("Headphones"), Sub = L.Detail(L.AudioVolume(40), L.AudioWas("Speakers"), "WH-1000XM4") }, false, 1.2),
                Row(new Content { Kind = Kind.Audio, Glyph = 0, Muted = true, Title = L.AudioTitle("Speakers"), Sub = L.Detail(L.AudioMuted, L.AudioWas("Headphones"), "Realtek(R) Audio") }, false, 1.2),
                Row(new Content { Kind = Kind.AudioWarn, Glyph = 2, Title = L.AudioNone, Sub = L.AudioWas("Headphones") }, false, 1.2),
            };
        }

        static Tuple<Content, bool, double> Row(Content c, bool repeat, double age)
        {
            return Tuple.Create(c, repeat, age);
        }
    }
}
