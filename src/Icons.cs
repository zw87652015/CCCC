using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace Cccc
{
    // Glyph: two stacked cards, the front one stamped with a check (cut out, so it reads at 16px).
    static class Icons
    {
        static readonly RectangleF Back = new RectangleF(1.6f, 1.4f, 8.6f, 10.2f);
        static readonly RectangleF Front = new RectangleF(5.4f, 4.4f, 9.2f, 10.2f);
        static readonly PointF[] Check = { new PointF(7.7f, 9.7f), new PointF(9.4f, 11.4f), new PointF(12.4f, 8.0f) };

        public static float[] Glyph(int size)
        {
            float k = size / 16f;
            var back = Alpha.Mask(size, size, k, g => { using (var p = Shapes.RoundRect(Back, 2.2f)) using (var pen = new Pen(Color.White, 1.45f)) g.DrawPath(pen, p); });
            var front = Alpha.Mask(size, size, k, g => { using (var p = Shapes.RoundRect(Front, 2.3f)) g.FillPath(Brushes.White, p); });
            var gap = Alpha.Mask(size, size, k, g =>
            {
                using (var p = Shapes.RoundRect(Front, 2.3f))
                using (var pen = new Pen(Color.White, 2.8f))
                {
                    g.FillPath(Brushes.White, p);
                    g.DrawPath(pen, p);
                }
            });
            var check = Alpha.Mask(size, size, k, g =>
            {
                using (var pen = new Pen(Color.White, 1.55f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                    g.DrawLines(pen, Check);
            });
            var a = new float[size * size];
            for (int i = 0; i < a.Length; i++) a[i] = Math.Max(back[i] * (1 - gap[i]), front[i] * (1 - check[i]));
            return a;
        }

        public static Bitmap Tray(int size, bool light, bool paused)
        {
            var a = Glyph(size);
            int rgb = light ? 0x1C1C1C : 0xFFFFFF;
            float mul = paused ? 0.42f : 1f;
            var px = new int[a.Length];
            for (int i = 0; i < a.Length; i++) px[i] = ((int)(a[i] * mul * 255 + 0.5f) << 24) | rgb;
            return Alpha.Write(px, size, size, PixelFormat.Format32bppArgb);
        }

        public static Bitmap App(int size)
        {
            var bg = Alpha.Mask(size, size, 1, g => { using (var p = Shapes.RoundRect(new RectangleF(0, 0, size, size), size * 0.225f)) g.FillPath(Brushes.White, p); });
            int inner = Math.Max(10, (int)Math.Round(size * 0.64));
            int off = (size - inner) / 2;
            var glyph = Glyph(inner);
            Color top = Color.FromArgb(70, 224, 124), bottom = Color.FromArgb(22, 158, 70);
            var px = new int[size * size];
            for (int y = 0; y < size; y++)
            {
                float t = size > 1 ? y / (float)(size - 1) : 0;
                float r = top.R + (bottom.R - top.R) * t, gr = top.G + (bottom.G - top.G) * t, b = top.B + (bottom.B - top.B) * t;
                for (int x = 0; x < size; x++)
                {
                    int i = y * size + x, gx = x - off, gy = y - off;
                    float w = gx >= 0 && gy >= 0 && gx < inner && gy < inner ? glyph[gy * inner + gx] : 0;
                    int R = (int)(r + (255 - r) * w), G = (int)(gr + (255 - gr) * w), B = (int)(b + (255 - b) * w);
                    px[i] = ((int)(bg[i] * 255 + 0.5f) << 24) | (R << 16) | (G << 8) | B;
                }
            }
            return Alpha.Write(px, size, size, PixelFormat.Format32bppArgb);
        }

        public static void ExportIco(string path)
        {
            int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 256 };
            var images = new List<byte[]>();
            foreach (int sz in sizes)
                using (var bmp = App(sz))
                using (var ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    images.Add(ms.ToArray());
                }

            using (var w = new BinaryWriter(File.Create(path)))
            {
                w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                    w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                    w.Write((byte)0); w.Write((byte)0);
                    w.Write((short)1); w.Write((short)32);
                    w.Write(images[i].Length); w.Write(offset);
                    offset += images[i].Length;
                }
                foreach (var img in images) w.Write(img);
            }
        }
    }
}
