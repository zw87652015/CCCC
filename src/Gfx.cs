using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Cccc
{
    static class Ease
    {
        public static double Clamp01(double t) { return t < 0 ? 0 : t > 1 ? 1 : t; }
        public static double OutCubic(double t) { t = 1 - Clamp01(t); return 1 - t * t * t; }
        public static double OutQuint(double t) { t = 1 - Clamp01(t); return 1 - t * t * t * t * t; }
        public static double OutBack(double t)
        {
            const double c1 = 1.70158, c3 = c1 + 1;
            double u = Clamp01(t) - 1;
            return 1 + c3 * u * u * u + c1 * u * u;
        }
    }

    sealed class Theme
    {
        public bool Dark;
        public Color Bg, Border, Title, Sub, Meta, Ok, Warn;
        public float ShadowFar, ShadowNear;

        public static readonly Theme DarkTheme = new Theme
        {
            Dark = true,
            Bg = Color.FromArgb(255, 36, 36, 40),
            Border = Color.FromArgb(30, 255, 255, 255),
            Title = Color.FromArgb(246, 246, 248),
            Sub = Color.FromArgb(168, 168, 176),
            Meta = Color.FromArgb(116, 116, 124),
            Ok = Color.FromArgb(48, 209, 88),
            Warn = Color.FromArgb(255, 172, 28),
            ShadowFar = 0.55f,
            ShadowNear = 0.35f,
        };

        public static readonly Theme LightTheme = new Theme
        {
            Dark = false,
            Bg = Color.FromArgb(255, 252, 252, 253),
            Border = Color.FromArgb(24, 0, 0, 0),
            Title = Color.FromArgb(22, 22, 26),
            Sub = Color.FromArgb(98, 98, 106),
            Meta = Color.FromArgb(150, 150, 158),
            Ok = Color.FromArgb(30, 168, 74),
            Warn = Color.FromArgb(232, 138, 0),
            ShadowFar = 0.22f,
            ShadowNear = 0.12f,
        };

        const string Personalize = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

        public static Theme Current() { return ReadFlag("AppsUseLightTheme") ? LightTheme : DarkTheme; }
        public static bool TaskbarLight() { return ReadFlag("SystemUsesLightTheme"); }

        static bool ReadFlag(string name)
        {
            try
            {
                object v = Registry.GetValue(Personalize, name, 1);
                return !(v is int) || (int)v != 0;
            }
            catch (Exception) { return true; }
        }
    }

    static class Shapes
    {
        public static GraphicsPath RoundRect(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // Draws the first `progress` fraction of a polyline, by length.
        public static void Partial(Graphics g, Pen pen, PointF[] pts, double progress)
        {
            if (progress <= 0) return;
            double total = 0;
            var len = new double[pts.Length - 1];
            for (int i = 0; i < len.Length; i++)
            {
                double dx = pts[i + 1].X - pts[i].X, dy = pts[i + 1].Y - pts[i].Y;
                len[i] = Math.Sqrt(dx * dx + dy * dy);
                total += len[i];
            }
            double want = total * Math.Min(1, progress);
            var outPts = new List<PointF> { pts[0] };
            for (int i = 0; i < len.Length; i++)
            {
                if (want >= len[i]) { outPts.Add(pts[i + 1]); want -= len[i]; continue; }
                float f = (float)(want / len[i]);
                outPts.Add(new PointF(pts[i].X + (pts[i + 1].X - pts[i].X) * f, pts[i].Y + (pts[i + 1].Y - pts[i].Y) * f));
                break;
            }
            if (outPts.Count >= 2) g.DrawLines(pen, outPts.ToArray());
        }
    }

    // Premultiplied 32bpp DIB that GDI+ draws into and UpdateLayeredWindow reads from, with no copies.
    sealed class Surface : IDisposable
    {
        public readonly int W, H;
        public readonly IntPtr Hdc;
        public readonly Bitmap Bmp;
        public readonly Graphics G;
        readonly IntPtr hbmp, old;

        public Surface(int w, int h)
        {
            W = w; H = h;
            var bi = new Native.BITMAPINFOHEADER { biSize = 40, biWidth = w, biHeight = -h, biPlanes = 1, biBitCount = 32 };
            IntPtr screen = Native.GetDC(IntPtr.Zero);
            Hdc = Native.CreateCompatibleDC(screen);
            Native.ReleaseDC(IntPtr.Zero, screen);
            IntPtr bits;
            hbmp = Native.CreateDIBSection(Hdc, ref bi, 0, out bits, IntPtr.Zero, 0);
            old = Native.SelectObject(Hdc, hbmp);
            Bmp = new Bitmap(w, h, w * 4, PixelFormat.Format32bppPArgb, bits);
            G = Graphics.FromImage(Bmp);
            G.SmoothingMode = SmoothingMode.AntiAlias;
            G.PixelOffsetMode = PixelOffsetMode.HighQuality;
            G.InterpolationMode = InterpolationMode.HighQualityBilinear;
            G.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        }

        public void Dispose()
        {
            G.Dispose();
            Bmp.Dispose();
            Native.SelectObject(Hdc, old);
            Native.DeleteObject(hbmp);
            Native.DeleteDC(Hdc);
        }
    }

    static class Alpha
    {
        // Renders `draw` in white and returns its coverage as 0..1 per pixel.
        public static float[] Mask(int w, int h, float scale, Action<Graphics> draw)
        {
            using (var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.ScaleTransform(scale, scale);
                    draw(g);
                }
                var px = Read(bmp);
                var m = new float[px.Length];
                for (int i = 0; i < px.Length; i++) m[i] = ((px[i] >> 24) & 255) / 255f;
                return m;
            }
        }

        public static int[] Read(Bitmap bmp)
        {
            var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var px = new int[bmp.Width * bmp.Height];
            Marshal.Copy(data.Scan0, px, 0, px.Length);
            bmp.UnlockBits(data);
            return px;
        }

        public static Bitmap Write(int[] px, int w, int h, PixelFormat format)
        {
            var bmp = new Bitmap(w, h, format);
            var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, format);
            Marshal.Copy(px, 0, data.Scan0, px.Length);
            bmp.UnlockBits(data);
            return bmp;
        }

        // Repeated box blur approximates a gaussian with sigma close to `r`.
        public static void Blur(float[] a, int w, int h, int r, int passes)
        {
            var tmp = new float[a.Length];
            for (int p = 0; p < passes; p++)
            {
                BoxH(a, tmp, w, h, r);
                BoxV(tmp, a, w, h, r);
            }
        }

        static void BoxH(float[] src, float[] dst, int w, int h, int r)
        {
            float inv = 1f / (2 * r + 1);
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                float sum = 0;
                for (int x = -r; x <= r; x++) if (x >= 0 && x < w) sum += src[row + x];
                for (int x = 0; x < w; x++)
                {
                    dst[row + x] = sum * inv;
                    int add = x + r + 1, sub = x - r;
                    if (add < w) sum += src[row + add];
                    if (sub >= 0) sum -= src[row + sub];
                }
            }
        }

        static void BoxV(float[] src, float[] dst, int w, int h, int r)
        {
            float inv = 1f / (2 * r + 1);
            for (int x = 0; x < w; x++)
            {
                float sum = 0;
                for (int y = -r; y <= r; y++) if (y >= 0 && y < h) sum += src[y * w + x];
                for (int y = 0; y < h; y++)
                {
                    dst[y * w + x] = sum * inv;
                    int add = y + r + 1, sub = y - r;
                    if (add < h) sum += src[add * w + x];
                    if (sub >= 0) sum -= src[sub * w + x];
                }
            }
        }

        // Two-layer drop shadow: a wide ambient one and a tight contact one.
        public static Bitmap Shadow(int w, int h, float s, RectangleF card, float rad, Theme th)
        {
            var far = Mask(w, h, s, g => { using (var p = Shapes.RoundRect(Offset(card, 5), rad)) g.FillPath(Brushes.White, p); });
            var near = Mask(w, h, s, g => { using (var p = Shapes.RoundRect(Offset(card, 1), rad)) g.FillPath(Brushes.White, p); });
            Blur(far, w, h, Math.Max(1, (int)Math.Round(7 * s)), 3);
            Blur(near, w, h, Math.Max(1, (int)Math.Round(1.5 * s)), 2);
            var px = new int[w * h];
            for (int i = 0; i < px.Length; i++)
            {
                float v = Math.Min(1f, far[i] * th.ShadowFar + near[i] * th.ShadowNear);
                px[i] = (int)(v * 255 + 0.5f) << 24;
            }
            return Write(px, w, h, PixelFormat.Format32bppPArgb);
        }

        static RectangleF Offset(RectangleF r, float dy) { r.Offset(0, dy); return r; }
    }
}
