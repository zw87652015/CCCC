using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
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
        public bool Dark, Glass;
        public Color Bg, Border, Title, Sub, Meta, Ok, Warn;
        public float ShadowFar, ShadowNear, Radius = 12;

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

        // Liquid Glass: Bg tints the blurred screen behind the card; its alpha is the lightest tint allowed.
        public static readonly Theme GlassDark = new Theme
        {
            Dark = true,
            Glass = true,
            Radius = 20,
            Bg = Color.FromArgb(92, 14, 14, 18),
            Border = Color.FromArgb(56, 255, 255, 255),
            Title = Color.FromArgb(255, 255, 255),
            Sub = Color.FromArgb(222, 222, 230),
            Meta = Color.FromArgb(172, 172, 182),
            Ok = Color.FromArgb(48, 209, 88),
            Warn = Color.FromArgb(255, 172, 28),
            ShadowFar = 0.30f,
            ShadowNear = 0.16f,
        };

        public static readonly Theme GlassLight = new Theme
        {
            Dark = false,
            Glass = true,
            Radius = 20,
            Bg = Color.FromArgb(104, 255, 255, 255),
            Border = Color.FromArgb(36, 0, 0, 0),
            Title = Color.FromArgb(10, 10, 14),
            Sub = Color.FromArgb(46, 46, 54),
            Meta = Color.FromArgb(96, 96, 106),
            Ok = Color.FromArgb(30, 168, 74),
            Warn = Color.FromArgb(226, 132, 0),
            ShadowFar = 0.14f,
            ShadowNear = 0.07f,
        };

        static Theme Make(bool dark, Color bg, Color border, Color title, Color sub, Color meta, Color ok, Color warn)
        {
            return new Theme
            {
                Dark = dark, Bg = bg, Border = border, Title = title, Sub = sub, Meta = meta, Ok = ok, Warn = warn,
                ShadowFar = dark ? 0.55f : 0.22f, ShadowNear = dark ? 0.35f : 0.12f,
            };
        }

        // Graphite, High contrast, Pastel: dark then light for each, indexed by (palette - 1) * 2 + (light ? 1 : 0).
        static readonly Theme[] Palettes =
        {
            Make(true, Color.FromArgb(32, 32, 34), Color.FromArgb(30, 255, 255, 255), Color.FromArgb(240, 240, 240), Color.FromArgb(170, 170, 170), Color.FromArgb(120, 120, 120), Color.FromArgb(120, 120, 128), Color.FromArgb(200, 150, 60)),
            Make(false, Color.FromArgb(250, 250, 250), Color.FromArgb(24, 0, 0, 0), Color.FromArgb(24, 24, 24), Color.FromArgb(96, 96, 96), Color.FromArgb(150, 150, 150), Color.FromArgb(70, 70, 76), Color.FromArgb(190, 130, 40)),
            Make(true, Color.FromArgb(0, 0, 0), Color.FromArgb(120, 255, 255, 255), Color.FromArgb(255, 255, 255), Color.FromArgb(230, 230, 230), Color.FromArgb(200, 200, 200), Color.FromArgb(0, 200, 80), Color.FromArgb(255, 190, 0)),
            Make(false, Color.FromArgb(255, 255, 255), Color.FromArgb(140, 0, 0, 0), Color.FromArgb(0, 0, 0), Color.FromArgb(30, 30, 30), Color.FromArgb(70, 70, 70), Color.FromArgb(0, 130, 50), Color.FromArgb(190, 100, 0)),
            Make(true, Color.FromArgb(44, 40, 56), Color.FromArgb(26, 255, 255, 255), Color.FromArgb(248, 244, 255), Color.FromArgb(196, 188, 214), Color.FromArgb(146, 138, 166), Color.FromArgb(126, 214, 170), Color.FromArgb(255, 190, 130)),
            Make(false, Color.FromArgb(250, 246, 255), Color.FromArgb(24, 60, 40, 100), Color.FromArgb(40, 32, 56), Color.FromArgb(110, 100, 130), Color.FromArgb(160, 150, 180), Color.FromArgb(88, 180, 136), Color.FromArgb(240, 160, 90)),
        };

        const string Personalize = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

        public static Theme Resolve(Settings s, bool systemDark)
        {
            bool dark = s.ThemeMode == 2 || (s.ThemeMode == 0 && systemDark);
            Theme v = s.Palette == 0 ? (dark ? DarkTheme : LightTheme)
                : s.Palette == 4 ? (dark ? GlassDark : GlassLight)
                : Palettes[(s.Palette - 1) * 2 + (dark ? 0 : 1)];
            var t = (Theme)v.MemberwiseClone();
            Color c;
            if (TryColor(s.OkColor, out c)) t.Ok = c;
            if (TryColor(s.WarnColor, out c)) t.Warn = c;
            if (s.FullPalette)
            {
                if (TryColor(s.BgColor, out c)) t.Bg = v.Glass ? Color.FromArgb(v.Bg.A, c) : c;
                if (TryColor(s.BorderColor, out c)) t.Border = c;
                if (TryColor(s.TitleColor, out c)) t.Title = c;
                if (TryColor(s.SubColor, out c)) t.Sub = c;
                if (TryColor(s.MetaColor, out c)) t.Meta = c;
            }
            t.Dark = Lum(t.Bg) < 0.5;
            if (t.Dark != v.Dark)
            {
                Theme pair = v.Glass ? (t.Dark ? GlassDark : GlassLight) : (t.Dark ? DarkTheme : LightTheme);
                t.ShadowFar = pair.ShadowFar;
                t.ShadowNear = pair.ShadowNear;
            }
            return t;
        }

        public static bool TryColor(string s, out Color c)
        {
            long v;
            c = Color.Empty;
            if (s == null || s.Length != 9 || s[0] != '#' || !long.TryParse(s.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v)) return false;
            c = Color.FromArgb((int)(uint)v);
            return true;
        }

        public static string Hex(Color c) { return string.Format("#{0:X2}{1:X2}{2:X2}{3:X2}", c.A, c.R, c.G, c.B); }

        public static double Lum(Color c) { return 0.2126 * Lin(c.R) + 0.7152 * Lin(c.G) + 0.0722 * Lin(c.B); }

        static double Lin(int v)
        {
            double x = v / 255.0;
            return x <= 0.03928 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4);
        }

        public static Color Glyph(Color accent) { return Lum(accent) > 0.7 ? Color.FromArgb(30, 30, 30) : Color.White; }

        public static bool SystemLight() { return ReadFlag("AppsUseLightTheme"); }
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
        public readonly IntPtr Hdc, Bits;
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
            hbmp = Native.CreateDIBSection(Hdc, ref bi, 0, out Bits, IntPtr.Zero, 0);
            old = Native.SelectObject(Hdc, hbmp);
            Bmp = new Bitmap(w, h, w * 4, PixelFormat.Format32bppPArgb, Bits);
            G = Graphics.FromImage(Bmp);
            G.SmoothingMode = SmoothingMode.AntiAlias;
            G.PixelOffsetMode = PixelOffsetMode.HighQuality;
            G.InterpolationMode = InterpolationMode.HighQualityBilinear;
            // Grid fitting snaps glyphs to whole pixels, so sliding or scaling text would jitter.
            G.TextRenderingHint = TextRenderingHint.AntiAlias;
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
        public static int[] Shadow(int w, int h, float s, RectangleF card, float rad, Theme th)
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
            return px;
        }

        static RectangleF Offset(RectangleF r, float dy) { r.Offset(0, dy); return r; }
    }

    // Liquid Glass material: the screen behind the card, blurred, bent at the rim like a lens, tinted, with a specular edge.
    static class GlassLayer
    {
        // Opaque screen pixels of a w×h box at (x, y). Every visible card window is kept out
        // of the capture, which needs Windows 10 2004; if one can't be excluded, returns null.
        public static int[] Capture(IntPtr[] exclude, int x, int y, int w, int h)
        {
            var hidden = new List<IntPtr>();
            bool failed = false;
            foreach (IntPtr wnd in exclude)
            {
                if (wnd == IntPtr.Zero) continue;
                if (Native.SetWindowDisplayAffinity(wnd, Native.WDA_EXCLUDEFROMCAPTURE)) hidden.Add(wnd);
                else { failed = true; break; }
            }
            int[] px = null;
            if (!failed)
            {
                IntPtr screen = Native.GetDC(IntPtr.Zero), dc = Native.CreateCompatibleDC(screen), bits;
                var bi = new Native.BITMAPINFOHEADER { biSize = 40, biWidth = w, biHeight = -h, biPlanes = 1, biBitCount = 32 };
                IntPtr bmp = Native.CreateDIBSection(dc, ref bi, 0, out bits, IntPtr.Zero, 0);
                IntPtr old = Native.SelectObject(dc, bmp);
                if (Native.BitBlt(dc, 0, 0, w, h, screen, x, y, Native.SRCCOPY))
                {
                    px = new int[w * h];
                    Marshal.Copy(bits, px, 0, px.Length);
                }
                Native.SelectObject(dc, old);
                Native.DeleteObject(bmp);
                Native.DeleteDC(dc);
                Native.ReleaseDC(IntPtr.Zero, screen);
            }
            foreach (IntPtr wnd in hidden) Native.SetWindowDisplayAffinity(wnd, 0);
            return px;
        }

        // R, G, B planes (0-255) of opaque pixels: 0-2 frosted (sigma near 5.5 DIP), 3-5 barely softened for the rim lens.
        public static float[][] Blur(int[] px, int w, int h, float s)
        {
            var planes = new float[6][];
            for (int k = 0; k < 6; k++) planes[k] = new float[px.Length];
            for (int i = 0; i < px.Length; i++)
            {
                int v = px[i];
                planes[0][i] = planes[3][i] = (v >> 16) & 255;
                planes[1][i] = planes[4][i] = (v >> 8) & 255;
                planes[2][i] = planes[5][i] = v & 255;
            }
            int frost = Math.Max(1, (int)Math.Round(5.5 * s)), soft = Math.Max(1, (int)Math.Round(1.2 * s));
            for (int k = 0; k < 3; k++)
            {
                Alpha.Blur(planes[k], w, h, frost, 3);
                Alpha.Blur(planes[k + 3], w, h, soft, 2);
            }
            return planes;
        }

        // Glass for `card` (DIP) laid over `under`, the black drop shadow on a canvas the size of `bg`.
        public static int[] Compose(float[][] bg, int[] under, int w, int h, float s, RectangleF card, float rad, Theme th)
        {
            var px = (int[])under.Clone();
            float hw = card.Width / 2, hh = card.Height / 2, cx = card.X + hw, cy = card.Y + hh;
            rad = Math.Min(rad, Math.Min(hw, hh));
            float band = Math.Min(rad, Math.Min(16, 0.75f * Math.Min(hw, hh))), bend = 0.7f * band;
            float tint = TintAlpha(bg, w, h, s, card, th);
            float tr = th.Bg.R * tint, tg = th.Bg.G * tint, tb = th.Bg.B * tint, keep = 1 - tint;
            float shine = th.Dark ? 0.55f : 0.9f, sheen = th.Dark ? 0.04f : 0.08f;
            // Red bends most and blue least, so busy edges get a thin spectral fringe.
            const float lx = -0.6f, ly = -0.8f, sat = 1.4f, disperse = 0.3f;

            int x0 = Math.Max(0, (int)Math.Floor(card.Left * s) - 1), x1 = Math.Min(w, (int)Math.Ceiling(card.Right * s) + 1);
            int y0 = Math.Max(0, (int)Math.Floor(card.Top * s) - 1), y1 = Math.Min(h, (int)Math.Ceiling(card.Bottom * s) + 1);
            var c = new float[3];
            for (int y = y0; y < y1; y++)
            {
                float py = (y + 0.5f) / s - cy, ay = Math.Abs(py), qy = ay - (hh - rad);
                for (int x = x0; x < x1; x++)
                {
                    float qx = Math.Abs((x + 0.5f) / s - cx) - (hw - rad);
                    float ox = Math.Max(qx, 0), oy = Math.Max(qy, 0), outside = (float)Math.Sqrt(ox * ox + oy * oy);
                    float d = outside + Math.Min(Math.Max(qx, qy), 0) - rad;
                    float cov = Math.Min(1, 0.5f - d * s);
                    if (cov <= 0) continue;

                    float nx, ny;
                    if (qx > 0 && qy > 0) { nx = ox / outside; ny = oy / outside; }
                    else if (qx > qy) { nx = 1; ny = 0; }
                    else { nx = 0; ny = 1; }
                    float px_ = (x + 0.5f) / s - cx;
                    if (px_ < 0) nx = -nx;
                    if (py < 0) ny = -ny;

                    float depth = Math.Max(0, -d), t = Math.Max(0, 1 - depth / band), push = bend * t * t;
                    for (int k = 0; k < 3; k++)
                    {
                        float pk = push * (1 + disperse * (1 - k));
                        float sx = (cx + px_ + nx * pk) * s - 0.5f, sy = (cy + py + ny * pk) * s - 0.5f;
                        float v = At(bg[k], w, h, sx, sy);
                        if (t > 0) v += (At(bg[k + 3], w, h, sx, sy) - v) * t;
                        c[k] = v;
                    }

                    float l = 0.299f * c[0] + 0.587f * c[1] + 0.114f * c[2];
                    float f = nx * lx + ny * ly;
                    f *= f;
                    float rim = Math.Max(0, 1 - depth / 1.4f), glow = Math.Max(0, 1 - depth / 8);
                    float hi = Math.Min(1, shine * (rim * (0.35f + 0.65f * f) + 0.4f * glow * glow * (0.25f + 0.75f * f))
                                           + sheen * (0.5f - py / (2 * hh)));
                    float a = cov * 255, inv = 1 - cov;
                    int ua = (px[y * w + x] >> 24) & 255;
                    int pr = Channel(l + (c[0] - l) * sat, keep, tr, hi, a);
                    int pg = Channel(l + (c[1] - l) * sat, keep, tg, hi, a);
                    int pb = Channel(l + (c[2] - l) * sat, keep, tb, hi, a);
                    int pa = (int)(a + ua * inv + 0.5f);
                    px[y * w + x] = (Math.Min(255, pa) << 24) | (pr << 16) | (pg << 8) | pb;
                }
            }
            return px;
        }

        // Saturated, tinted, highlighted, then premultiplied by `a` (0-255).
        static int Channel(float v, float keep, float tint, float hi, float a)
        {
            v = Math.Max(0, Math.Min(255, v)) * keep + tint;
            v += (255 - v) * hi;
            return Math.Min(255, (int)(v * a / 255 + 0.5f));
        }

        // The tint alpha, raised over busy or contrasting backdrops so text stays readable.
        static float TintAlpha(float[][] bg, int w, int h, float s, RectangleF card, Theme th)
        {
            double sum = 0;
            int n = 0;
            int x0 = Math.Max(0, (int)(card.Left * s)), x1 = Math.Min(w, (int)(card.Right * s));
            int y0 = Math.Max(0, (int)(card.Top * s)), y1 = Math.Min(h, (int)(card.Bottom * s));
            for (int y = y0; y < y1; y += 3)
                for (int x = x0; x < x1; x += 3)
                {
                    int i = y * w + x;
                    sum += 0.299 * bg[0][i] + 0.587 * bg[1][i] + 0.114 * bg[2][i];
                    n++;
                }
            float lb = n > 0 ? (float)(sum / n / 255) : 0.5f;
            float lt = (0.299f * th.Bg.R + 0.587f * th.Bg.G + 0.114f * th.Bg.B) / 255;
            float need = 0;
            if (th.Dark && lb > 0.3f && lb > lt) need = (lb - 0.3f) / (lb - lt);
            if (!th.Dark && lb < 0.62f && lt > lb) need = (0.62f - lb) / (lt - lb);
            return Math.Min(0.82f, Math.Max(th.Bg.A / 255f, need));
        }

        // Bilinear read of one plane, clamped to the edges.
        static float At(float[] p, int w, int h, float x, float y)
        {
            x = Math.Max(0, Math.Min(w - 1.001f, x));
            y = Math.Max(0, Math.Min(h - 1.001f, y));
            int ix = (int)x, iy = (int)y;
            float fx = x - ix, fy = y - iy;
            int i = iy * w + ix;
            float top = p[i] + (p[i + 1] - p[i]) * fx, bot = p[i + w] + (p[i + w + 1] - p[i + w]) * fx;
            return top + (bot - top) * fy;
        }
    }
}
