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
            int cw = (int)Math.Ceiling(card.CanvasW * s), ch = (int)Math.Ceiling(card.CanvasH * s);
            var rows = Rows();
            int iconRow = 120;
            using (var sheet = new Bitmap(cw * 2, ch * rows.Length + iconRow, PixelFormat.Format32bppArgb))
            using (var g = Graphics.FromImage(sheet))
            {
                using (var b = new LinearGradientBrush(new Rectangle(0, 0, cw, sheet.Height), Color.FromArgb(28, 34, 52), Color.FromArgb(52, 40, 64), 90f))
                    g.FillRectangle(b, 0, 0, cw, sheet.Height);
                using (var b = new LinearGradientBrush(new Rectangle(cw, 0, cw, sheet.Height), Color.FromArgb(206, 218, 236), Color.FromArgb(232, 220, 226), 90f))
                    g.FillRectangle(b, cw, 0, cw, sheet.Height);

                var themes = new[] { Theme.DarkTheme, Theme.LightTheme };
                for (int col = 0; col < 2; col++)
                {
                    card.Layout(s, themes[col]);
                    for (int row = 0; row < rows.Length; row++)
                    {
                        var r = rows[row];
                        card.Still(r.Item1, r.Item2, r.Item3);
                        g.DrawImage(card.Pixels, col * cw, row * ch);
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
                Row(new Content { Kind = Kind.Copied, Title = L.Again[0], Meta = L.Link, Sub = "github.com/microsoft/terminal/issues/1234" }, false, 1.2),
                Row(new Content { Kind = Kind.Copied, Title = L.Again[0], Meta = L.Image, Sub = "1920 × 1080", Thumb = thumb }, false, 1.2),
                Row(new Content { Kind = Kind.Cut, Title = L.Cut, Meta = L.Items(3), Sub = "report.pdf, photo.png, 笔记.txt" }, false, 1.2),
                Row(new Content { Kind = Kind.Fail, Title = L.NothingCopied, Sub = L.StillHolding + text, Count = 2 }, true, 1.2),
                Row(new Content { Kind = Kind.Info, Title = L.ReadyTitle, Sub = L.ReadySub }, false, 1.2),
                Row(new Content { Kind = Kind.Copied, Title = L.Again[0], Meta = L.Chars(text.Length), Sub = text, SrcIcon = appIcon, SrcText = "Google Chrome · github.com", SrcLine = true }, false, 1.2),
                Row(new Content { Kind = Kind.Copied, Title = L.Again[0], Meta = L.Link, Sub = "github.com/microsoft/terminal/issues/1234", SrcIcon = appIcon }, false, 1.2),
                Row(new Content { Kind = Kind.Copied, Title = L.Again[0], Meta = L.Chars(text.Length), Sub = text, SrcIcon = appIcon, SrcText = "Visual Studio Code · github.com · Pull request #1234: Add customizable source display", SrcLine = true }, false, 1.2),
                Row(new Content { Kind = Kind.Fail, Title = L.NothingCopied, Sub = L.StillHolding + text, SrcIcon = appIcon, SrcText = "Microsoft Word" }, false, 1.2),
            };
        }

        static Tuple<Content, bool, double> Row(Content c, bool repeat, double age)
        {
            return Tuple.Create(c, repeat, age);
        }
    }
}
