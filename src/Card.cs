using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Cccc
{
    enum Kind { Copied, Cut, Info, Fail, Empty, Caps, WifiUp, WifiDown, Audio, AudioWarn }
    enum Role { Main, Caps, Wifi, Audio }

    sealed class Content
    {
        public Kind Kind;
        public string Title = "", Meta = "", Sub = "";
        public string Sig, Hold;
        public Bitmap Thumb;
        public bool CutHint, Secret, SrcLine, CapsOn, Muted;
        public int Glyph;   // audio card: 0 speaker, 1 headphones, 2 no output
        public int Count = 1;
        public string Site;
        public Bitmap SrcIcon;
        public string SrcText = "";
    }

    sealed class Card : NativeWindow
    {
        public const float CX = 24, CY = 20;
        const float Edge = 16;
        float R { get { return th.Radius; } }

        public float W, H, Line, HMax, CanvasW, CanvasH;
        public float DpiScale = 1;

        readonly Stopwatch clock = Stopwatch.StartNew();
        Vsync vsync;
        readonly StringFormat fmt, fmtCenter;
        Font fTitle, fSub, fMeta, fBadge, fCaps;

        Settings st = new Settings();
        readonly Role role;
        readonly bool caps;
        Anchor.Spot wifiSpot;
        string sizeSig = "", themeSig = "", placeSig;
        Theme resolved;
        bool sysLight, slideDown;
        float k = 1, s = 1;

        Surface surf;
        Bitmap shadow;
        int[] shadowPx, plainShadow;
        float shadowH;
        float[][] backdrop;
        bool glassDirty;
        float[][] glideStrip;
        int glideStripTop;
        double stripTarget = double.NaN, lastCapture;
        Theme th = Theme.DarkTheme, shadowTheme;
        Content cur;
        bool shown, repeatHit, hoverArmed;
        double vis, visTarget, last, deadline, duration = 2;
        double tEvent = -9, tPop = -9, tBump = -9, tShake = -9, tText = -9, tTitle = -9;
        int baseX, baseY;
        bool drag;
        Point dragStart;
        int dragOffX, dragOffY;

        // The tray-anchored cards stack so simultaneous ones never overlap: `stack` holds
        // every visible card in show order, `stackDY` is this card's animated vertical offset
        // from its home spot (device px), `stackTarget` where Arrange wants it.
        static readonly List<Card> stack = new List<Card>();
        double stackDY, stackTarget;

        // Raised when the user drags a Freedom card to a new spot (0-1000 within the work area).
        public event Action<int, int> Moved;

        // Raised when the last visible card hides (the stack is empty again).
        public static event Action AllHidden;

        // The name the tray-anchored card should anchor to (its tray button's name contains it).
        public string AnchorName;

        bool Tray { get { return role == Role.Wifi || role == Role.Audio; } }

        public Card(Role role = Role.Main)
        {
            this.role = role;
            caps = role == Role.Caps;
            fmt = new StringFormat(StringFormat.GenericTypographic);
            fmt.FormatFlags |= StringFormatFlags.NoWrap;
            fmt.Trimming = StringTrimming.EllipsisCharacter;
            fmtCenter = new StringFormat(fmt) { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

            Apply(st);
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
            vsync = new Vsync(Handle, Native.WM_APP_FRAME);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_APP_FRAME) { vsync.Done(); if (shown) Frame(); return; }
            if (m.Msg == Native.WM_MOUSEACTIVATE) { m.Result = (IntPtr)Native.MA_NOACTIVATE; return; }
            if (!Tray && Pos == 10 && shown)
            {
                if (m.Msg == Native.WM_LBUTTONDOWN) { BeginDrag(); return; }
                if (m.Msg == Native.WM_MOUSEMOVE && drag) { DragMove(); return; }
                if (m.Msg == Native.WM_LBUTTONUP && drag) { EndDrag(); return; }
            }
            if (m.Msg == Native.WM_LBUTTONUP || m.Msg == Native.WM_RBUTTONUP) { visTarget = 0; return; }
            base.WndProc(ref m);
        }

        void BeginDrag()
        {
            baseY += (int)Math.Round(stackDY);
            stackDY = stackTarget = 0;
            Native.POINT p;
            Native.GetCursorPos(out p);
            drag = true;
            dragStart = new Point(p.X, p.Y);
            dragOffX = p.X - (baseX + Px(CX));
            dragOffY = p.Y - (baseY + Px(Top));
            deadline = double.PositiveInfinity;
            Native.SetCapture(Handle);
        }

        void DragMove()
        {
            Native.POINT p;
            Native.GetCursorPos(out p);
            baseX = p.X - Px(CX) - dragOffX;
            baseY = p.Y - Px(Top) - dragOffY;
            if (th.Glass && Now - lastCapture > 0.05) CaptureBackdrop();
        }

        void EndDrag()
        {
            Native.POINT p;
            Native.GetCursorPos(out p);
            int moved = Math.Abs(p.X - dragStart.X) + Math.Abs(p.Y - dragStart.Y);
            Native.ReleaseCapture();
            drag = false;
            Arrange();
            if (th.Glass) CaptureBackdrop();
            if (moved > 4)
            {
                int sx = baseX + Px(CX), sy = baseY + Px(CY);
                IntPtr mon = Native.MonitorFromPoint(new Native.POINT(sx + Px(W) / 2, sy + Px(HMax) / 2), 2);
                var mi = new Native.MONITORINFO { cbSize = Marshal.SizeOf(typeof(Native.MONITORINFO)) };
                Native.GetMonitorInfo(mon, ref mi);
                int fx = 0, fy = 0;
                Unplace(mi.rcWork, sx, sy, Px(Edge), out fx, out fy);
                if (caps) { st.CapsPosition = 10; st.CapsFreeX = fx; st.CapsFreeY = fy; }
                else { st.Position = 10; st.FreeX = fx; st.FreeY = fy; }
                if (Moved != null) Moved(fx, fy);
                deadline = (caps ? st.CapsStayUntilClicked : st.StayUntilClicked) ? double.PositiveInfinity : Now + duration;
            }
            else visTarget = 0;
        }

        // Inverse of Place for a dropped card: returns 0-1000 along each axis of the work area.
        void Unplace(Native.RECT work, int slotX, int slotY, int margin, out int fx, out int fy)
        {
            int spanX = work.Right - work.Left - 2 * margin - Px(W);
            int spanY = work.Bottom - work.Top - 2 * margin - Px(HMax);
            fx = Clamp1000((int)Math.Round((slotX - work.Left - margin) * 1000.0 / Math.Max(1, spanX)));
            fy = Clamp1000((int)Math.Round((slotY - work.Top - margin) * 1000.0 / Math.Max(1, spanY)));
        }

        static int Clamp1000(int v) { return Math.Max(0, Math.Min(1000, v)); }

        // Called on every settings change; only the parts whose inputs changed are rebuilt.
        public void Apply(Settings settings)
        {
            st = settings;
            bool sys = Theme.SystemLight();
            string size = st.Scale + "|" + st.TextScale + "|" + st.FontFamily + "|" + st.Simple + "|" + L.Zh
                + (caps ? "|" + st.CapsWidth + "|" + st.CapsHeight : "");
            string theme = st.ThemeMode + "|" + st.Palette + "|" + st.FullPalette + "|" + st.OkColor + "|" + st.WarnColor
                + "|" + st.BgColor + "|" + st.BorderColor + "|" + st.TitleColor + "|" + st.SubColor + "|" + st.MetaColor + "|" + sys + "|" + st.Transparency;
            if (size != sizeSig)
            {
                sizeSig = size;
                k = st.TextScale / 100f;
                if (caps)
                {
                    W = st.CapsWidth;
                    H = Math.Max(st.CapsHeight, 8 + 22 * k);
                    Line = 18 * k;
                    HMax = H;
                }
                else if (st.Simple)
                {
                    W = 220;
                    H = Math.Max(48, 26 + 22 * k);
                    Line = 18 * k;
                    HMax = H;
                }
                else
                {
                    W = (float)Math.Round(320 * (0.5 + 0.5 * k));
                    H = Math.Max(64, 25 + 39 * k);
                    Line = 18 * k;
                    HMax = Tray ? H : H + Line;
                }
                CanvasW = W + 2 * CX;
                CanvasH = HMax + CY + 32;
                Fonts();
                if (surf != null) { surf.Dispose(); surf = null; }
            }
            if (theme != themeSig)
            {
                themeSig = theme;
                Resolve(sys);
            }
            if (shown) Layout(DpiScale * st.Scale / 100f, resolved);
            if (th.Glass && backdrop == null && shown) CaptureBackdrop();
        }

        void Resolve(bool light)
        {
            sysLight = light;
            resolved = Theme.Resolve(st, !light);
        }

        void Fonts()
        {
            string face = L.Zh ? "Microsoft YaHei UI" : "Segoe UI";
            string bold = L.Zh ? "Microsoft YaHei UI" : "Segoe UI Semibold";
            FontStyle boldStyle = L.Zh ? FontStyle.Bold : FontStyle.Regular;
            FontFamily custom = Installed(st.FontFamily);
            if (custom != null)
            {
                face = bold = custom.Name;
                boldStyle = custom.IsStyleAvailable(FontStyle.Bold) ? FontStyle.Bold : FontStyle.Regular;
            }
            if (fTitle != null) { fTitle.Dispose(); fSub.Dispose(); fMeta.Dispose(); fBadge.Dispose(); fCaps.Dispose(); }
            fTitle = new Font(bold, 14f * k, boldStyle, GraphicsUnit.Pixel);
            fSub = new Font(face, 12.5f * k, FontStyle.Regular, GraphicsUnit.Pixel);
            fMeta = new Font(face, 12f * k, FontStyle.Regular, GraphicsUnit.Pixel);
            fBadge = new Font(bold, 12f * k, boldStyle, GraphicsUnit.Pixel);
            fCaps = new Font(bold, 15f, boldStyle, GraphicsUnit.Pixel);
        }

        static FontFamily Installed(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (FontFamily f in FontFamily.Families)
                if (string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase) && f.IsStyleAvailable(FontStyle.Regular)) return f;
            return null;
        }

        public void Show(Content c, bool repeat)
        {
            double now = Now;
            bool fresh = !shown;
            if (fresh || placeSig != PlaceSig()) Prepare();
            if (!fresh && th.Glass) CaptureBackdrop();

            string oldTitle = cur != null ? cur.Title : null;
            if (cur != null && cur.Thumb != null && cur.Thumb != c.Thumb) cur.Thumb.Dispose();
            cur = c;
            repeatHit = repeat;
            tEvent = now;
            if (repeat) { tPop = now; tBump = now; if (!fresh && oldTitle != c.Title) tTitle = now; }
            else tText = now;
            if (c.Kind == Kind.Fail) tShake = now;
            bool info = c.Kind == Kind.Info || c.Kind == Kind.Empty;
            duration = role == Role.Wifi ? st.WifiDurationMs / 1000.0 : role == Role.Audio ? st.AudioDurationMs / 1000.0
                : caps ? st.CapsDurationMs / 1000.0 : (st.DurationMs + (info ? 700 : 0)) / 1000.0;
            deadline = (role == Role.Wifi ? st.WifiStayUntilClicked : role == Role.Audio ? st.AudioStayUntilClicked
                : caps ? st.CapsStayUntilClicked : st.StayUntilClicked) ? double.PositiveInfinity : now + duration;
            visTarget = 1;

            if (!fresh) { Arrange(); return; }
            vis = 0; last = now; shown = true;
            stack.Add(this);
            Arrange();
            stackDY = stackTarget;   // a fresh card appears already stacked — no glide-in
            if (th.Glass) CaptureBackdrop();   // at the stacked spot, not the home one
            hoverArmed = !CursorInCard();
            Frame();
            Native.ShowWindow(Handle, Native.SW_SHOWNOACTIVATE);
            Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
            vsync.Start();
        }

        string PlaceSig()
        {
            if (Tray)
                return "W|" + Anchor.Sig + "|" + st.Scale + "|" + st.TextScale + "|" + st.FontFamily + "|" + st.Simple + "|" + AnchorName;
            return Pos + "|" + FreeX + "|" + FreeY + "|" + st.Scale + "|" + st.TextScale + "|" + st.FontFamily + "|" + st.Simple
                + (caps ? "|" + st.CapsPosition + "|" + st.CapsFreeX + "|" + st.CapsFreeY + "|" + st.CapsWidth + "|" + st.CapsHeight : "");
        }

        void Prepare()
        {
            Rectangle work = Rectangle.Empty;
            Point cursor = Point.Empty;
            int pos = 0;
            if (Tray)
            {
                float ds;
                wifiSpot = Anchor.Tray(AnchorName, out ds);
                DpiScale = ds;
            }
            else
            {
                IntPtr fg = Native.GetForegroundWindow();
                Native.POINT cp;
                Native.GetCursorPos(out cp);
                cursor = new Point(cp.X, cp.Y);
                pos = Pos;
                IntPtr mon = fg != IntPtr.Zero && pos != 9 ? Native.MonitorFromWindow(fg, 2) : Native.MonitorFromPoint(cp, 2);
                var mi = new Native.MONITORINFO { cbSize = Marshal.SizeOf(typeof(Native.MONITORINFO)) };
                Native.GetMonitorInfo(mon, ref mi);
                uint dpi = 96, dpiY;
                try { if (Native.GetDpiForMonitor(mon, 0, out dpi, out dpiY) != 0) dpi = 96; }
                catch (Exception) { dpi = 96; }
                DpiScale = dpi / 96f;
                work = Rectangle.FromLTRB(mi.rcWork.Left, mi.rcWork.Top, mi.rcWork.Right, mi.rcWork.Bottom);
            }

            bool light = Theme.SystemLight();
            if (light != sysLight) Resolve(light);
            Layout(DpiScale * st.Scale / 100f, resolved);

            Point p;
            if (Tray)
            {
                bool down;
                p = Anchor.Place(wifiSpot, new Size(Px(W), Px(HMax)), Px(Edge), out down);
                slideDown = down;
            }
            else
            {
                slideDown = pos <= 2 || (pos == 10 && FreeY < 333);
                p = Place(pos, work, new Size(Px(W), Px(HMax)), cursor, Px(Edge), FreeX, FreeY);
            }
            baseX = p.X - Px(CX);
            baseY = p.Y + Px(HMax) - Px(CY + HMax);
            placeSig = PlaceSig();
        }

        // Top-left of a `card`-sized box at grid position `pos` (0-8, row-major), next to the cursor (9),
        // or at the free spot fx/fy per mille of the margin-to-margin span (10).
        public static Point Place(int pos, Rectangle work, Size card, Point cursor, int margin, int fx, int fy)
        {
            if (pos == 10)
                return new Point(work.Left + margin + (int)Math.Round(fx * (work.Width - 2 * margin - card.Width) / 1000.0),
                                 work.Top + margin + (int)Math.Round(fy * (work.Height - 2 * margin - card.Height) / 1000.0));
            if (pos == 9)
            {
                int nx = cursor.X + margin, ny = cursor.Y + margin;
                if (nx + card.Width > work.Right) nx = cursor.X - margin - card.Width;
                if (ny + card.Height > work.Bottom) ny = cursor.Y - margin - card.Height;
                return new Point(Math.Max(work.Left, Math.Min(nx, work.Right - card.Width)),
                                 Math.Max(work.Top, Math.Min(ny, work.Bottom - card.Height)));
            }
            int col = pos % 3, row = pos / 3;
            int x = col == 0 ? work.Left + margin : col == 1 ? work.Left + (work.Width - card.Width) / 2 : work.Right - margin - card.Width;
            int y = row == 0 ? work.Top + margin : row == 1 ? work.Top + (work.Height - card.Height) / 2 : work.Bottom - margin - card.Height;
            return new Point(x, y);
        }

        public void Layout(float scale, Theme theme)
        {
            if (surf == null || scale != s)
            {
                if (surf != null) surf.Dispose();
                s = scale;
                surf = new Surface((int)Math.Ceiling(CanvasW * s), (int)Math.Ceiling(CanvasH * s));
                shadowTheme = null;
                backdrop = null;
            }
            th = theme;
            if (!theme.Glass) backdrop = null;
            if (shadowTheme != theme || shadowH != CardH)
            {
                var card = new RectangleF(CX, Top, W, CardH);
                plainShadow = Alpha.Shadow(surf.W, surf.H, s, card, R, theme);
                shadowTheme = theme;
                shadowH = CardH;
                glassDirty = true;
            }
            if (glassDirty)
            {
                glassDirty = false;
                var card = new RectangleF(CX, Top, W, CardH);
                shadowPx = backdrop != null ? GlassLayer.Compose(backdrop, plainShadow, surf.W, surf.H, s, card, R, theme) : plainShadow;
                if (shadow != null) shadow.Dispose();
                shadow = Alpha.Write(shadowPx, surf.W, surf.H, PixelFormat.Format32bppPArgb);
            }
        }

        // Every card nudged off its home spot by an already-shown one; the earliest card wins.
        // `slideDown` cards (top taskbar) push newcomers below them, everyone else pushes up.
        static void Arrange()
        {
            var placed = new List<Rectangle>();
            foreach (Card c in stack)
            {
                int x = c.baseX + c.Px(CX), y0 = c.baseY + c.Px(c.Top);
                int w = c.Px(c.W), h = c.Px(c.CardH), gap = c.Px(8);
                var r = new Rectangle(x, y0, w, h);
                if (c.drag) { placed.Add(r); continue; }

                var hr = new Native.RECT { Left = x, Top = y0, Right = x + w, Bottom = y0 + h };
                IntPtr mon = Native.MonitorFromRect(ref hr, 2);
                var mi = new Native.MONITORINFO { cbSize = Marshal.SizeOf(typeof(Native.MONITORINFO)) };
                Native.GetMonitorInfo(mon, ref mi);
                var work = Rectangle.FromLTRB(mi.rcWork.Left, mi.rcWork.Top, mi.rcWork.Right, mi.rcWork.Bottom);

                // Move past the first blocking card, then re-test: chains push further still.
                bool blocked = true;
                int guard = 0;
                while (blocked && guard++ < placed.Count + 1)
                {
                    blocked = false;
                    for (int i = 0; i < placed.Count; i++)
                    {
                        var pr = placed[i];
                        if (!Rectangle.Inflate(r, 0, gap).IntersectsWith(pr)) continue;
                        r.Y = stack[i].slideDown ? pr.Bottom + gap : pr.Top - gap - h;
                        blocked = true;
                        break;
                    }
                }
                if (!work.Contains(r)) r = new Rectangle(x, y0, w, h);   // would leave the monitor: stay home
                c.stackTarget = r.Y - y0;
                placed.Add(r);
            }
        }

        // Grabs the screen behind the card's resting spot; a visible card leaves itself out of the capture.
        void CaptureBackdrop()
        {
            lastCapture = Now;
            int[] px = Handle == IntPtr.Zero ? null : GlassLayer.Capture(ShownHandles(), baseX, baseY + (int)Math.Round(stackDY), surf.W, surf.H);
            backdrop = px != null ? GlassLayer.Blur(px, surf.W, surf.H, s) : null;
            glideStrip = null;
            stripTarget = double.NaN;
            glassDirty = true;
        }

        // Handles of every card currently on screen, so glass captures leave them all out.
        static IntPtr[] ShownHandles()
        {
            var list = new List<IntPtr>(stack.Count);
            foreach (Card c in stack) list.Add(c.Handle);
            return list.ToArray();
        }

        // While the card glides between stack spots, the glass follows a single tall strip
        // covering the whole travel range; each frame slices out the card-height piece at
        // the current offset. On arrival the slice already matches the resting spot.
        void GlideGlass()
        {
            if (stripTarget != stackTarget)
            {
                int a = (int)Math.Round(stackDY), b = (int)Math.Round(stackTarget);
                glideStripTop = baseY + Math.Min(a, b);
                int h = Math.Abs(b - a) + surf.H;
                int[] px = GlassLayer.Capture(ShownHandles(), baseX, glideStripTop, surf.W, h);
                glideStrip = px != null ? GlassLayer.Blur(px, surf.W, h, s) : null;
                stripTarget = stackTarget;
            }
            if (glideStrip == null) return;
            if (backdrop == null)
            {
                backdrop = new float[6][];
                for (int k = 0; k < 6; k++) backdrop[k] = new float[surf.W * surf.H];
            }
            int off = baseY + (int)Math.Round(stackDY) - glideStripTop;
            for (int k = 0; k < 6; k++) Array.Copy(glideStrip[k], off * surf.W, backdrop[k], 0, surf.W * surf.H);
            glassDirty = true;
            if (stackDY == stackTarget) { glideStrip = null; stripTarget = double.NaN; }
        }

        // For the design sheet: the `w`-pixel-wide image `px` stands in for the screen under a canvas at (x, y).
        public void Backdrop(int[] px, int w, int x, int y)
        {
            var part = new int[surf.W * surf.H];
            for (int r = 0; r < surf.H; r++) Array.Copy(px, (y + r) * w + x, part, r * surf.W, surf.W);
            backdrop = GlassLayer.Blur(part, surf.W, surf.H, s);
            glassDirty = true;
        }

        int Px(float dip) { return (int)Math.Round(dip * s); }

        int Pos { get { return caps && st.CapsPosition >= 0 ? st.CapsPosition : st.Position; } }
        int FreeX { get { return caps && st.CapsPosition >= 0 ? st.CapsFreeX : st.FreeX; } }
        int FreeY { get { return caps && st.CapsPosition >= 0 ? st.CapsFreeY : st.FreeY; } }

        float CardH { get { return cur != null && cur.SrcLine ? HMax : H; } }
        float Top { get { return CY + HMax - CardH; } }

        bool CursorInCard()
        {
            Native.POINT p;
            if (!Native.GetCursorPos(out p)) return false;
            int x0 = baseX + Px(CX), y0 = baseY + Px(Top) + (int)Math.Round(stackDY);
            return p.X >= x0 && p.X < x0 + Px(W) && p.Y >= y0 && p.Y < y0 + Px(CardH);
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

            if (stackDY != stackTarget)
            {
                stackDY += (stackTarget - stackDY) * (1 - Math.Exp(-dt * 14));
                if (Math.Abs(stackTarget - stackDY) < 0.5) stackDY = stackTarget;
                if (th.Glass) GlideGlass();
            }

            double dy = (1 - Ease.OutQuint(vis)) * 18, dx = 0, ts = now - tShake;
            if (slideDown) dy = -dy;
            if (ts >= 0 && ts < 0.5)
            {
                double u = ts / 0.5;
                dx = 7 * Math.Sin(u * Math.PI * 7) * (1 - u) * (1 - u);
            }
            // The window moves by whole pixels; the remainder is drawn into the bitmap.
            double ox = dx * s, oy = dy * s;
            int ix = (int)Math.Floor(ox), iy = (int)Math.Floor(oy);
            Render(now, (float)(ox - ix), (float)(oy - iy));
            byte alpha = (byte)Math.Round(255 * (100 - st.Transparency) / 100.0 * Ease.OutCubic(Math.Min(1, vis * 1.5)));
            Push(baseX + ix, baseY + iy + (int)Math.Round(stackDY), alpha);
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
            vsync.Stop();
            Native.ShowWindow(Handle, Native.SW_HIDE);
            shown = false;
            // Free the drawing buffers; Prepare/Layout rebuild them on the next Show.
            if (surf != null) { surf.Dispose(); surf = null; }
            if (shadow != null) { shadow.Dispose(); shadow = null; }
            shadowPx = null; plainShadow = null; backdrop = null; glideStrip = null;
            shadowTheme = null; stripTarget = double.NaN;
            if (stack.Remove(this))
            {
                stackDY = stackTarget = 0;
                Arrange();   // the rest glide back to their home spots
                if (stack.Count == 0 && AllHidden != null) AllHidden();
            }
        }

        // Renders a frozen moment `age` seconds after an event, for the design sheet and the settings preview.
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

        void Render(double now) { Render(now, 0, 0); }

        // ox, oy: sub-pixel shift in device pixels.
        void Render(double now, float ox, float oy)
        {
            Graphics g = surf.G;
            g.ResetTransform();
            if (cur == null) { g.Clear(Color.Transparent); return; }
            if (shadowH != CardH || shadowTheme != th || glassDirty) Layout(s, th);

            bool warn = cur.Kind == Kind.Fail || cur.Kind == Kind.Empty || cur.Kind == Kind.WifiDown || cur.Kind == Kind.AudioWarn;
            Color accent = cur.Kind == Kind.Caps ? (cur.CapsOn ? th.Ok : th.Meta) : (warn ? th.Warn : th.Ok);
            double te = now - tEvent;

            double tp = now - tPop;
            bool pop = tp >= 0 && tp < 0.24;
            if (!pop && ox == 0 && oy == 0) Marshal.Copy(shadowPx, 0, surf.Bits, shadowPx.Length);
            else
            {
                g.Clear(Color.Transparent);
                g.TranslateTransform(ox, oy);
                if (pop)
                {
                    float k = 1 + 0.03f * (float)Math.Sin(Math.PI * tp / 0.24);
                    float cx = (CX + W / 2) * s, cy = (Top + CardH / 2) * s;
                    g.TranslateTransform(cx, cy);
                    g.ScaleTransform(k, k);
                    g.TranslateTransform(-cx, -cy);
                }
                g.DrawImage(shadow, new Rectangle(0, 0, surf.W, surf.H));
            }
            g.ScaleTransform(s, s);

            // Glass is already in the shadow layer; without a backdrop it falls back to a nearly solid tint.
            bool glass = th.Glass && backdrop != null;
            var card = new RectangleF(CX, Top, W, CardH);
            using (var path = Shapes.RoundRect(card, R))
            {
                if (!glass)
                    using (var b = new SolidBrush(th.Glass ? Color.FromArgb(235, th.Bg) : th.Bg)) g.FillPath(b, path);
                if (te >= 0 && te < 0.8)
                {
                    int a = (int)(255 * 0.10 * (1 - Ease.OutCubic(te / 0.8)));
                    using (var b = new SolidBrush(Color.FromArgb(a, accent))) g.FillPath(b, path);
                }
            }
            float inset = 0.5f / s;
            if (!glass)
                using (var path = Shapes.RoundRect(RectangleF.Inflate(card, -inset, -inset), R - inset))
                using (var pen = new Pen(th.Border, 1f / s))
                    g.DrawPath(pen, path);

            double remain = Ease.Clamp01((deadline - now) / duration);
            if (visTarget > 0 && remain > 0 && !(role == Role.Wifi ? st.WifiStayUntilClicked : role == Role.Audio ? st.AudioStayUntilClicked : caps ? st.CapsStayUntilClicked : st.StayUntilClicked))
            {
                float x0 = CX + R, x1 = CX + W - R, y = Top + CardH - 3f;
                using (var pen = new Pen(Color.FromArgb(th.Dark ? 70 : 60, accent), 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawLine(pen, x0, y, x0 + (float)((x1 - x0) * remain), y);
            }

            DrawIcon(g, te, accent, warn);
            DrawText(g, now, accent);
        }

        void DrawIcon(Graphics g, double te, Color accent, bool warn)
        {
            bool caps = cur.Kind == Kind.Caps;
            float ix = CX + (caps ? 24 : 32), iy = Top + CardH / 2;
            Color glyph = Theme.Glyph(accent);

            double tr = te - 0.04;
            if (tr >= 0 && tr < 0.6)
            {
                double u = tr / 0.6;
                float rr = (caps ? 11 : 14) + (caps ? 10 : 12) * (float)Ease.OutCubic(u);
                int a = (int)(255 * 0.45 * Math.Pow(1 - u, 1.6));
                using (var pen = new Pen(Color.FromArgb(a, accent), 1.2f + 1.6f * (float)(1 - u)))
                    g.DrawEllipse(pen, ix - rr, iy - rr, 2 * rr, 2 * rr);
            }

            float from = repeatHit ? 0.8f : 0.5f;
            float k = from + (1 - from) * (float)Ease.OutBack(te / 0.34);
            float r = (caps ? 11 : 14) * k;
            using (var b = new SolidBrush(accent)) g.FillEllipse(b, ix - r, iy - r, 2 * r, 2 * r);

            double pc = Ease.OutCubic((te - 0.1) / 0.26);
            if (caps)
            {
                using (var gp = new GraphicsPath())
                {
                    gp.AddString(cur.CapsOn ? "A" : "a", fCaps.FontFamily, (int)fCaps.Style, fCaps.Size,
                                 PointF.Empty, StringFormat.GenericTypographic);
                    var bnd = gp.GetBounds();
                    var gs = g.Save();
                    g.TranslateTransform(ix, iy);
                    g.ScaleTransform(k, k);
                    g.TranslateTransform(-(bnd.X + bnd.Width / 2), -(bnd.Y + bnd.Height / 2));
                    using (var b = new SolidBrush(Color.FromArgb((int)(255 * Ease.Clamp01(pc)), glyph)))
                        g.FillPath(b, gp);
                    g.Restore(gs);
                }
                return;
            }
            if (cur.Kind == Kind.WifiUp || cur.Kind == Kind.WifiDown)
            {
                // Wi-Fi glyph: two arcs then the dot, each stroke growing in; a slash marks a drop.
                double a1 = Ease.Clamp01(pc / 0.4), a2 = Ease.Clamp01((pc - 0.3) / 0.4);
                double a3 = Ease.Clamp01((pc - 0.55) / 0.3), a4 = Ease.Clamp01((pc - 0.7) / 0.3);
                float d = 4.5f * k;
                using (var pen = new Pen(glyph, 2.3f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    if (a1 > 0) g.DrawArc(pen, ix - 10.5f * k, iy + d - 10.5f * k, 21f * k, 21f * k, 205, 130 * (float)a1);
                    if (a2 > 0) g.DrawArc(pen, ix - 6f * k, iy + d - 6f * k, 12f * k, 12f * k, 205, 130 * (float)a2);
                    if (a3 > 0)
                    {
                        float dr = 1.9f * k * (float)Ease.OutBack(a3);
                        using (var b = new SolidBrush(glyph)) g.FillEllipse(b, ix - dr, iy + d - dr, 2 * dr, 2 * dr);
                    }
                    if (cur.Kind == Kind.WifiDown && a4 > 0)
                        Shapes.Partial(g, pen, new[] { new PointF(ix - 8.5f * k, iy + 11f * k), new PointF(ix + 8.5f * k, iy - 9f * k) }, a4);
                }
                return;
            }
            if (cur.Kind == Kind.Audio || cur.Kind == Kind.AudioWarn)
            {
                // Sound output glyph: speaker + sound waves, headphones, or a slashed speaker
                // for "no output"; a × replaces the waves while the endpoint is muted.
                double a1 = Ease.Clamp01(pc / 0.4), a2 = Ease.Clamp01((pc - 0.3) / 0.4);
                double a3 = Ease.Clamp01((pc - 0.55) / 0.3), a4 = Ease.Clamp01((pc - 0.7) / 0.3);
                using (var pen = new Pen(glyph, 2.3f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                {
                    if (cur.Glyph == 1)
                    {
                        // Headband arc, then the two cups fading in.
                        if (a1 > 0) g.DrawArc(pen, ix - 9f * k, iy - 8.5f * k, 18f * k, 18f * k, 180, 180 * (float)a1);
                        if (a2 > 0)
                            using (var b = new SolidBrush(Color.FromArgb((int)(255 * a2), glyph)))
                            {
                                using (var cup = Shapes.RoundRect(new RectangleF(ix - 9.5f * k, iy - 0.5f * k, 5.5f * k, 8f * k), 2.5f * k))
                                    g.FillPath(b, cup);
                                using (var cup = Shapes.RoundRect(new RectangleF(ix + 4f * k, iy - 0.5f * k, 5.5f * k, 8f * k), 2.5f * k))
                                    g.FillPath(b, cup);
                            }
                        return;
                    }
                    if (a1 > 0)
                        using (var b = new SolidBrush(Color.FromArgb((int)(255 * a1), glyph)))
                        {
                            g.FillRectangle(b, ix - 9f * k, iy - 3f * k, 4f * k, 6f * k);
                            g.FillPolygon(b, new[]
                            {
                                new PointF(ix - 5f * k, iy - 3f * k), new PointF(ix - 0.5f * k, iy - 7f * k),
                                new PointF(ix - 0.5f * k, iy + 7f * k), new PointF(ix - 5f * k, iy + 3f * k),
                            });
                        }
                    if (cur.Glyph == 2)
                    {
                        if (a4 > 0)
                            Shapes.Partial(g, pen, new[] { new PointF(ix - 8.5f * k, iy + 11f * k), new PointF(ix + 8.5f * k, iy - 9f * k) }, a4);
                        return;
                    }
                    if (cur.Muted)
                    {
                        if (a2 > 0)
                        {
                            Shapes.Partial(g, pen, new[] { new PointF(ix + 4f * k, iy - 4f * k), new PointF(ix + 11f * k, iy + 4f * k) }, a2);
                            Shapes.Partial(g, pen, new[] { new PointF(ix + 4f * k, iy + 4f * k), new PointF(ix + 11f * k, iy - 4f * k) }, a2);
                        }
                        return;
                    }
                    if (a2 > 0) g.DrawArc(pen, ix - 1f * k - 5.5f * k, iy - 5.5f * k, 11f * k, 11f * k, -52, 104 * (float)a2);
                    if (a3 > 0) g.DrawArc(pen, ix - 1f * k - 9.5f * k, iy - 9.5f * k, 19f * k, 19f * k, -52, 104 * (float)a3);
                }
                return;
            }
            using (var pen = new Pen(glyph, 2.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                if (warn)
                {
                    Shapes.Partial(g, pen, new[] { new PointF(ix, iy - 6 * k), new PointF(ix, iy + 1.2f * k) }, pc / 0.7);
                    if (pc > 0.6)
                    {
                        float dr = 1.45f * k * (float)Ease.OutBack((pc - 0.6) / 0.4);
                        using (var gb = new SolidBrush(glyph)) g.FillEllipse(gb, ix - dr, iy + 5.2f * k - dr, 2 * dr, 2 * dr);
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

        void DrawSrcIcon(Graphics g, float x, float y, float size, float ta)
        {
            using (var ia = new ImageAttributes())
            {
                ia.SetColorMatrix(new ColorMatrix { Matrix33 = ta });
                var mode = g.InterpolationMode;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                var dst = new[] { new PointF(x, y), new PointF(x + size, y), new PointF(x, y + size) };
                g.DrawImage(cur.SrcIcon, dst, new RectangleF(0, 0, cur.SrcIcon.Width, cur.SrcIcon.Height), GraphicsUnit.Pixel, ia);
                g.InterpolationMode = mode;
            }
        }

        void DrawText(Graphics g, double now, Color accent)
        {
            float right = CX + W - 14;

            if (cur.Kind == Kind.Caps)
            {
                float ca = (float)Ease.OutCubic((now - tText) / 0.26);
                float cl = (float)Ease.OutCubic((now - tTitle) / 0.28);
                float cx = CX + 46 + 6 * (1 - ca);
                string bt = cur.CapsOn ? "ON" : "OFF";
                float bh = 20 * k;
                float bw = Math.Max(36, g.MeasureString(bt, fBadge, PointF.Empty, fmt).Width + 16);
                float bx = CX + W - 12 - bw, by = Top + (CardH - bh) / 2;
                var rect = new RectangleF(bx, by, bw, bh);
                using (var p = Shapes.RoundRect(rect, bh / 2))
                using (var b = new SolidBrush(Color.FromArgb((int)((th.Dark ? 48 : 34) * ca), accent)))
                    g.FillPath(b, p);
                rect.Offset(0, 0.5f);
                using (var b = new SolidBrush(Color.FromArgb((int)(255 * ca), accent)))
                    g.DrawString(bt, fBadge, b, rect, fmtCenter);
                using (var b = new SolidBrush(Color.FromArgb((int)(255 * ca * cl), th.Title)))
                    g.DrawString(cur.Title, fTitle, b, new RectangleF(cx, Top + (CardH - 22 * k) / 2, bx - 10 - cx, 22 * k), fmt);
                return;
            }

            if (st.Simple)
            {
                // One-line card: just the title next to the status icon.
                float sa = (float)Ease.OutCubic((now - tText) / 0.26);
                float sl = (float)Ease.OutCubic((now - tTitle) / 0.28);
                using (var b = new SolidBrush(Color.FromArgb((int)(255 * sa * sl), th.Title)))
                    g.DrawString(cur.Title, fTitle, b, new RectangleF(CX + 58 + 6 * (1 - sa), Top + (CardH - 22 * k) / 2, W - 72, 22 * k), fmt);
                return;
            }

            if (cur.Thumb != null)
            {
                var tr = new RectangleF(CX + W - 12 - 40, Top + (CardH - 40) / 2, 40, 40);
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
                float bw = Math.Max(28, g.MeasureString(bt, fBadge, PointF.Empty, fmt).Width + 16), bh = 22 * k;
                float bx = right - bw, by = Top + (CardH - bh) / 2;
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
            float top = L.Zh ? Top + 10 : Top + 11;

            using (var b = new SolidBrush(Color.FromArgb((int)(255 * ta * la), th.Title)))
                g.DrawString(cur.Title, fTitle, b, new RectangleF(tx, top + 5 * (1 - la), maxW, 22 * k), fmt);

            float tw = g.MeasureString(cur.Title, fTitle, PointF.Empty, fmt).Width;
            float end = tx + Math.Min(tw, maxW);
            if (cur.Meta.Length > 0)
            {
                float mw = g.MeasureString(cur.Meta, fMeta, PointF.Empty, fmt).Width;
                float mx = tx + tw + 8;
                if (mx + mw <= tx + maxW)
                {
                    using (var b = new SolidBrush(Color.FromArgb((int)(255 * ta), th.Meta)))
                        g.DrawString(cur.Meta, fMeta, b, new RectangleF(mx, top + 2.2f * k, maxW - (mx - tx), 20 * k), fmt);
                    end = mx + mw;
                }
            }

            float x0 = end + 10;
            if (cur.SrcLine)
            {
                float lx = tx;
                if (cur.SrcIcon != null)
                {
                    DrawSrcIcon(g, tx, top + 40 * k, 14 * k, ta);
                    lx = tx + 18 * k;
                }
                if (cur.SrcText.Length > 0)
                    using (var b = new SolidBrush(Color.FromArgb((int)(255 * ta), th.Meta)))
                        g.DrawString(cur.SrcText, fMeta, b, new RectangleF(lx, top + 39 * k, maxW - (lx - tx), 20 * k), fmt);
            }
            else if (x0 + 14 * k <= tx + maxW)
            {
                float textX = x0;
                if (cur.SrcIcon != null)
                {
                    DrawSrcIcon(g, x0, top + 3 * k, 14 * k, ta);
                    textX = x0 + 18 * k;
                }
                float sw = tx + maxW - textX;
                if (cur.SrcText.Length > 0 && sw >= 24)
                    using (var b = new SolidBrush(Color.FromArgb((int)(255 * ta), th.Meta)))
                        g.DrawString(cur.SrcText, fMeta, b, new RectangleF(textX, top + 2.2f * k, sw, 20 * k), fmt);
            }

            using (var b = new SolidBrush(Color.FromArgb((int)(255 * ta), th.Sub)))
                g.DrawString(cur.Sub, fSub, b, new RectangleF(tx, top + 21 * k, maxW, 20 * k), fmt);
        }
    }

    // Posts `msg` to `hwnd` once per display refresh while running, never more than one at a time.
    // Waiting off the UI thread keeps input responsive and frames evenly spaced at any refresh rate.
    sealed class Vsync
    {
        readonly IntPtr hwnd;
        readonly int msg;
        readonly AutoResetEvent wake = new AutoResetEvent(false);
        volatile bool running;
        int pending;
        Thread thread;

        public Vsync(IntPtr hwnd, int msg) { this.hwnd = hwnd; this.msg = msg; }

        public void Start()
        {
            running = true;
            if (thread == null)
            {
                thread = new Thread(Loop) { IsBackground = true, Name = "CCCC vsync" };
                thread.Start();
            }
            wake.Set();
        }

        public void Stop() { running = false; }

        // Call when the posted message has been handled.
        public void Done() { Interlocked.Exchange(ref pending, 0); }

        void Loop()
        {
            var sw = Stopwatch.StartNew();
            int fast = 0;
            while (true)
            {
                if (!running) { wake.WaitOne(); continue; }
                double t0 = sw.Elapsed.TotalMilliseconds;
                bool ok = Native.DwmFlush() == 0;
                // DwmFlush returns at once when nothing is composing (e.g. locked session); fall back to ~60 Hz.
                if (!ok || sw.Elapsed.TotalMilliseconds - t0 < 1) { if (++fast >= 3) Thread.Sleep(15); }
                else fast = 0;
                if (running && Interlocked.Exchange(ref pending, 1) == 0)
                    Native.PostMessage(hwnd, msg, IntPtr.Zero, IntPtr.Zero);
            }
        }
    }
}
