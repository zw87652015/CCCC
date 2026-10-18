using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Cccc
{
    enum Kind { Copied, Cut, Info, Fail, Empty }

    sealed class Content
    {
        public Kind Kind;
        public string Title = "", Meta = "", Sub = "";
        public string Sig, Hold;
        public Bitmap Thumb;
        public bool CutHint;
        public int Count = 1;
    }

    sealed class Card : NativeWindow
    {
        public const float W = 320, H = 64, R = 12, CX = 24, CY = 20;
        public const float CanvasW = W + 2 * CX, CanvasH = H + CY + 32;
        const float Edge = 16;

        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly Timer timer = new Timer { Interval = 1 };
        readonly Font fTitle, fSub, fMeta, fBadge;
        readonly StringFormat fmt, fmtCenter;

        Surface surf;
        Bitmap shadow;
        Theme th = Theme.DarkTheme, shadowTheme;
        float s = 1;
        Content cur;
        bool shown, repeatHit, hoverArmed, fastTimer;
        double vis, visTarget, last, deadline, duration = 2;
        double tEvent = -9, tPop = -9, tBump = -9, tShake = -9, tText = -9, tTitle = -9;
        int baseX, baseY;
        string position = "left";

        public string Position
        {
            get { return position; }
            set
            {
                if (position == value) return;
                position = value;
                if (shown) { Prepare(); hoverArmed = !CursorInCard(); }
            }
        }

        public Card()
        {
            string face = L.Zh ? "Microsoft YaHei UI" : "Segoe UI";
            string bold = L.Zh ? "Microsoft YaHei UI" : "Segoe UI Semibold";
            FontStyle boldStyle = L.Zh ? FontStyle.Bold : FontStyle.Regular;
            fTitle = new Font(bold, 14f, boldStyle, GraphicsUnit.Pixel);
            fSub = new Font(face, 12.5f, FontStyle.Regular, GraphicsUnit.Pixel);
            fMeta = new Font(face, 12f, FontStyle.Regular, GraphicsUnit.Pixel);
            fBadge = new Font(bold, 12f, boldStyle, GraphicsUnit.Pixel);

            fmt = new StringFormat(StringFormat.GenericTypographic);
            fmt.FormatFlags |= StringFormatFlags.NoWrap;
            fmt.Trimming = StringTrimming.EllipsisCharacter;
            fmtCenter = new StringFormat(fmt) { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

            timer.Tick += delegate { Frame(); };
        }

        public double Now { get { return clock.Elapsed.TotalSeconds; } }
        public Bitmap Pixels { get { return surf.Bmp; } }

        public void CreateWindow()
        {
            CreateHandle(new CreateParams
            {
                Caption = "CCCC",
                Style = Native.WS_POPUP,
                ExStyle = Native.WS_EX_LAYERED | Native.WS_EX_TOOLWINDOW | Native.WS_EX_TOPMOST | Native.WS_EX_NOACTIVATE,
            });
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_MOUSEACTIVATE) { m.Result = (IntPtr)Native.MA_NOACTIVATE; return; }
            if (m.Msg == Native.WM_LBUTTONUP || m.Msg == Native.WM_RBUTTONUP) { visTarget = 0; return; }
            base.WndProc(ref m);
        }

        public void Show(Content c, bool repeat)
        {
            double now = Now;
            bool fresh = !shown;
            if (fresh) Prepare();

            string oldTitle = cur != null ? cur.Title : null;
            if (cur != null && cur.Thumb != null && cur.Thumb != c.Thumb) cur.Thumb.Dispose();
            cur = c;
            repeatHit = repeat;
            tEvent = now;
            if (repeat) { tPop = now; tBump = now; if (!fresh && oldTitle != c.Title) tTitle = now; }
            else tText = now;
            if (c.Kind == Kind.Fail) tShake = now;
            duration = c.Kind == Kind.Info || c.Kind == Kind.Empty ? 2.6 : 1.9;
            deadline = now + duration;
            visTarget = 1;

            if (!fresh) return;
            vis = 0; last = now; shown = true;
            hoverArmed = !CursorInCard();
            Frame();
            Native.ShowWindow(Handle, Native.SW_SHOWNOACTIVATE);
            Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
            if (!fastTimer) { Native.timeBeginPeriod(1); fastTimer = true; }
            timer.Start();
        }

        void Prepare()
        {
            IntPtr fg = Native.GetForegroundWindow();
            Native.POINT cp;
            Native.GetCursorPos(out cp);
            IntPtr mon = fg != IntPtr.Zero ? Native.MonitorFromWindow(fg, 2) : Native.MonitorFromPoint(cp, 2);
            var mi = new Native.MONITORINFO { cbSize = Marshal.SizeOf(typeof(Native.MONITORINFO)) };
            Native.GetMonitorInfo(mon, ref mi);
            uint dpi = 96, dpiY;
            try { if (Native.GetDpiForMonitor(mon, 0, out dpi, out dpiY) != 0) dpi = 96; }
            catch (Exception) { dpi = 96; }

            Layout(dpi / 96f, Theme.Current());
            baseX = mi.rcWork.Left + Px(Edge) - Px(CX);
            if (position == "center")
                baseX = mi.rcWork.Left + (mi.rcWork.Right - mi.rcWork.Left - Px(W)) / 2 - Px(CX);
            else if (position == "right")
                baseX = mi.rcWork.Right - Px(Edge) - Px(W) - Px(CX);
            baseY = mi.rcWork.Bottom - Px(Edge) - Px(CY + H);
        }

        public void Layout(float scale, Theme theme)
        {
            if (surf == null || scale != s)
            {
                if (surf != null) surf.Dispose();
                s = scale;
                surf = new Surface((int)Math.Ceiling(CanvasW * s), (int)Math.Ceiling(CanvasH * s));
                shadowTheme = null;
            }
            th = theme;
            if (shadowTheme != theme)
            {
                if (shadow != null) shadow.Dispose();
                shadow = Alpha.Shadow(surf.W, surf.H, s, new RectangleF(CX, CY, W, H), R, theme);
                shadowTheme = theme;
            }
        }

        int Px(float dip) { return (int)Math.Round(dip * s); }

        bool CursorInCard()
        {
            Native.POINT p;
            if (!Native.GetCursorPos(out p)) return false;
            int x0 = baseX + Px(CX), y0 = baseY + Px(CY);
            return p.X >= x0 && p.X < x0 + Px(W) && p.Y >= y0 && p.Y < y0 + Px(H);
        }

        void Frame()
        {
            double now = Now, dt = Math.Min(0.05, Math.Max(0, now - last));
            last = now;

            bool inCard = CursorInCard();
            if (!inCard) hoverArmed = true;
            if (visTarget > 0 && hoverArmed && inCard) deadline = Math.Max(deadline, now + 0.8);
            if (visTarget > 0 && now >= deadline) visTarget = 0;

            vis = visTarget > vis ? Math.Min(1, vis + dt / 0.34) : Math.Max(0, vis - dt / 0.26);
            if (vis <= 0 && visTarget <= 0) { HideNow(); return; }

            Render(now);

            double dy = (1 - Ease.OutQuint(vis)) * 18, dx = 0, ts = now - tShake;
            if (ts >= 0 && ts < 0.5)
            {
                double u = ts / 0.5;
                dx = 7 * Math.Sin(u * Math.PI * 7) * (1 - u) * (1 - u);
            }
            byte alpha = (byte)Math.Round(255 * Ease.OutCubic(Math.Min(1, vis * 1.5)));
            Push(baseX + (int)Math.Round(dx * s), baseY + (int)Math.Round(dy * s), alpha);
            Native.DwmFlush();
        }

        void Push(int x, int y, byte alpha)
        {
            var dst = new Native.POINT(x, y);
            var size = new Native.SIZE(surf.W, surf.H);
            var src = new Native.POINT(0, 0);
            var bf = new Native.BLENDFUNCTION { SourceConstantAlpha = alpha, AlphaFormat = 1 };
            Native.GdiFlush();
            Native.UpdateLayeredWindow(Handle, IntPtr.Zero, ref dst, ref size, surf.Hdc, ref src, 0, ref bf, 2);
        }

        void HideNow()
        {
            timer.Stop();
            Native.ShowWindow(Handle, Native.SW_HIDE);
            shown = false;
            if (fastTimer) { Native.timeEndPeriod(1); fastTimer = false; }
        }

        // Renders a frozen moment `age` seconds after an event, for the design sheet.
        public void Still(Content c, bool repeat, double age)
        {
            double now = 100, t = now - age;
            cur = c; repeatHit = repeat;
            tEvent = tText = t;
            tPop = tBump = repeat ? t : -9;
            tShake = -9; tTitle = -9;
            duration = 1.9; deadline = t + duration; visTarget = 1;
            Render(now);
        }

        void Render(double now)
        {
            Graphics g = surf.G;
            g.ResetTransform();
            g.Clear(Color.Transparent);
            if (cur == null) return;

            bool warn = cur.Kind == Kind.Fail || cur.Kind == Kind.Empty;
            Color accent = warn ? th.Warn : th.Ok;
            double te = now - tEvent;

            double tp = now - tPop;
            if (tp >= 0 && tp < 0.24)
            {
                float k = 1 + 0.03f * (float)Math.Sin(Math.PI * tp / 0.24);
                float cx = (CX + W / 2) * s, cy = (CY + H / 2) * s;
                g.TranslateTransform(cx, cy);
                g.ScaleTransform(k, k);
                g.TranslateTransform(-cx, -cy);
            }
            g.DrawImage(shadow, new Rectangle(0, 0, surf.W, surf.H));
            g.ScaleTransform(s, s);

            var card = new RectangleF(CX, CY, W, H);
            using (var path = Shapes.RoundRect(card, R))
            {
                using (var b = new SolidBrush(th.Bg)) g.FillPath(b, path);
                if (te >= 0 && te < 0.8)
                {
                    int a = (int)(255 * 0.10 * (1 - Ease.OutCubic(te / 0.8)));
                    using (var b = new SolidBrush(Color.FromArgb(a, accent))) g.FillPath(b, path);
                }
            }
            float inset = 0.5f / s;
            using (var path = Shapes.RoundRect(RectangleF.Inflate(card, -inset, -inset), R - inset))
            using (var pen = new Pen(th.Border, 1f / s))
                g.DrawPath(pen, path);

            double remain = Ease.Clamp01((deadline - now) / duration);
            if (visTarget > 0 && remain > 0)
            {
                float x0 = CX + R, x1 = CX + W - R, y = CY + H - 3f;
                using (var pen = new Pen(Color.FromArgb(th.Dark ? 70 : 60, accent), 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawLine(pen, x0, y, x0 + (float)((x1 - x0) * remain), y);
            }

            DrawIcon(g, te, accent, warn);
            DrawText(g, now, accent);
        }

        void DrawIcon(Graphics g, double te, Color accent, bool warn)
        {
            float ix = CX + 32, iy = CY + H / 2;

            double tr = te - 0.04;
            if (tr >= 0 && tr < 0.6)
            {
                double u = tr / 0.6;
                float rr = 14 + 12 * (float)Ease.OutCubic(u);
                int a = (int)(255 * 0.45 * Math.Pow(1 - u, 1.6));
                using (var pen = new Pen(Color.FromArgb(a, accent), 1.2f + 1.6f * (float)(1 - u)))
                    g.DrawEllipse(pen, ix - rr, iy - rr, 2 * rr, 2 * rr);
            }

            float from = repeatHit ? 0.8f : 0.5f;
            float k = from + (1 - from) * (float)Ease.OutBack(te / 0.34);
            float r = 14 * k;
            using (var b = new SolidBrush(accent)) g.FillEllipse(b, ix - r, iy - r, 2 * r, 2 * r);

            double pc = Ease.OutCubic((te - 0.1) / 0.26);
            using (var pen = new Pen(Color.White, 2.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                if (warn)
                {
                    Shapes.Partial(g, pen, new[] { new PointF(ix, iy - 6 * k), new PointF(ix, iy + 1.2f * k) }, pc / 0.7);
                    if (pc > 0.6)
                    {
                        float dr = 1.45f * k * (float)Ease.OutBack((pc - 0.6) / 0.4);
                        g.FillEllipse(Brushes.White, ix - dr, iy + 5.2f * k - dr, 2 * dr, 2 * dr);
                    }
                }
                else
                {
                    Shapes.Partial(g, pen, new[] {
                        new PointF(ix - 5.6f * k, iy + 0.4f * k),
                        new PointF(ix - 1.8f * k, iy + 4.2f * k),
                        new PointF(ix + 5.8f * k, iy - 4.4f * k) }, pc);
                }
            }
        }

        void DrawText(Graphics g, double now, Color accent)
        {
            float right = CX + W - 14;

            if (cur.Thumb != null)
            {
                var tr = new RectangleF(CX + W - 12 - 40, CY + 12, 40, 40);
                using (var tb = new TextureBrush(cur.Thumb, WrapMode.Clamp))
                using (var p = Shapes.RoundRect(tr, 8))
                using (var pen = new Pen(th.Border, 1f / s))
                {
                    tb.TranslateTransform(tr.X, tr.Y);
                    tb.ScaleTransform(tr.Width / cur.Thumb.Width, tr.Height / cur.Thumb.Height);
                    g.FillPath(tb, p);
                    g.DrawPath(pen, p);
                }
                right = tr.Left - 10;
            }

            if (cur.Count >= 2)
            {
                string bt = "×" + cur.Count;
                float bw = Math.Max(28, g.MeasureString(bt, fBadge, PointF.Empty, fmt).Width + 16), bh = 22;
                float bx = right - bw, by = CY + (H - bh) / 2;
                double tb = now - tBump;
                float bk = tb >= 0 && tb < 0.6 ? 1 + 0.3f * (float)(Math.Exp(-7 * tb) * Math.Cos(16 * tb)) : 1;
                var state = g.Save();
                g.TranslateTransform(bx + bw / 2, by + bh / 2);
                g.ScaleTransform(bk, bk);
                g.TranslateTransform(-(bx + bw / 2), -(by + bh / 2));
                var rect = new RectangleF(bx, by, bw, bh);
                using (var p = Shapes.RoundRect(rect, bh / 2))
                using (var b = new SolidBrush(Color.FromArgb(th.Dark ? 48 : 34, accent)))
                    g.FillPath(b, p);
                rect.Offset(0, 0.5f);
                using (var b = new SolidBrush(accent)) g.DrawString(bt, fBadge, b, rect, fmtCenter);
                g.Restore(state);
                right = bx - 10;
            }

            float ta = (float)Ease.OutCubic((now - tText) / 0.26);
            float la = (float)Ease.OutCubic((now - tTitle) / 0.28);
            float tx = CX + 58 + 6 * (1 - ta);
            float maxW = Math.Max(10, right - tx);
            float top = L.Zh ? CY + 10 : CY + 11;

            using (var b = new SolidBrush(Color.FromArgb((int)(255 * ta * la), th.Title)))
                g.DrawString(cur.Title, fTitle, b, new RectangleF(tx, top + 5 * (1 - la), maxW, 22), fmt);

            if (cur.Meta.Length > 0)
            {
                float tw = g.MeasureString(cur.Title, fTitle, PointF.Empty, fmt).Width;
                float mw = g.MeasureString(cur.Meta, fMeta, PointF.Empty, fmt).Width;
                float mx = tx + tw + 8;
                if (mx + mw <= tx + maxW)
                    using (var b = new SolidBrush(Color.FromArgb((int)(255 * ta), th.Meta)))
                        g.DrawString(cur.Meta, fMeta, b, new PointF(mx, top + 2.2f), fmt);
            }

            using (var b = new SolidBrush(Color.FromArgb((int)(255 * ta), th.Sub)))
                g.DrawString(cur.Sub, fSub, b, new RectangleF(tx, top + 21, maxW, 20), fmt);
        }
    }
}
