using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Cccc
{
    static class ClipReader
    {
        public static Content Read()
        {
            object com = null;
            for (int i = 0; i < 5 && com == null; i++)
            {
                if (Native.OleGetClipboard(out com) != 0) { com = null; Thread.Sleep(30); }
            }
            try { return Read(com != null ? new DataObject(com) : null); }
            finally { if (com != null && Marshal.IsComObject(com)) Marshal.FinalReleaseComObject(com); }
        }

        static Content Read(IDataObject d)
        {
            string[] f = null;
            if (d != null)
            {
                try { f = d.GetFormats(false); }
                catch (Exception) { }
            }
            // Photoshop layers, and other app-private objects, hide every format from OLE enumeration.
            if (f == null || f.Length == 0) return Opaque();

            var c = new Content();
            if (Secret(f) || Dword(d, f, "CanIncludeInClipboardHistory") == 0) return Private(c);
            c.Site = Site(d, f);

            try
            {
                var files = d.GetDataPresent(DataFormats.FileDrop) ? d.GetData(DataFormats.FileDrop) as string[] : null;
                if (files != null && files.Length > 0)
                {
                    Files(c, files);
                    int effect = Dword(d, f, "Preferred DropEffect");
                    c.CutHint = effect > 0 && (effect & 2) != 0 && (effect & 1) == 0;
                    return c;
                }

                string text = d.GetDataPresent(DataFormats.UnicodeText) ? d.GetData(DataFormats.UnicodeText) as string : null;
                if (text == null && d.GetDataPresent(DataFormats.Text)) text = d.GetData(DataFormats.Text) as string;
                if (!string.IsNullOrEmpty(text)) { Text(c, text); return c; }

                Image img = null;
                if (Has(f, "PNG"))
                {
                    var ms = d.GetData("PNG") as MemoryStream;
                    if (ms != null) using (var tmp = Image.FromStream(ms)) img = new Bitmap(tmp);
                }
                if (img == null && d.GetDataPresent(DataFormats.Bitmap)) img = d.GetData(DataFormats.Bitmap, true) as Image;
                if (img != null)
                {
                    using (img)
                    {
                        c.Meta = L.Image;
                        c.Sub = img.Width + " × " + img.Height;
                        c.Hold = L.ImageHold + c.Sub;
                        c.Thumb = Thumb(img, 80);
                        c.Sig = "img:" + c.Sub + ":" + Hash(c.Thumb);
                    }
                    return c;
                }
            }
            catch (Exception) { }

            c.Sub = c.Hold = L.Rich;
            c.Sig = "fmt:" + string.Join("|", f);
            return c;
        }

        static Content Opaque()
        {
            string[] raw = RawFormats();
            if (raw == null || raw.Length == 0) return null;
            var c = new Content();
            if (Secret(raw)) return Private(c);
            string app = Sources.OwnerApp();
            c.Meta = L.AppData;
            c.Sub = app != null ? L.KeptIn(app) : L.Rich;
            c.Hold = app != null ? L.AppContent(app) : L.Rich;
            c.Sig = "app:" + app + ":" + string.Join("|", raw);
            return c;
        }

        static Content Private(Content c)
        {
            c.Meta = L.Private;
            c.Sub = c.Hold = L.PrivateSub;
            c.Secret = true;
            return c;
        }

        static string Site(IDataObject d, string[] f)
        {
            if (!Has(f, "HTML Format")) return null;
            try
            {
                object o = d.GetData("HTML Format");
                var ms = o as MemoryStream;
                string html = ms != null ? Encoding.UTF8.GetString(ms.ToArray()) : o as string;
                return html != null ? Sources.SiteFromHtml(html) : null;
            }
            catch (Exception) { return null; }
        }

        static bool Secret(string[] f)
        {
            return Has(f, "ExcludeClipboardContentFromMonitorProcessing") || Has(f, "Clipboard Viewer Ignore");
        }

        static string[] RawFormats()
        {
            bool open = false;
            for (int i = 0; i < 5 && !open; i++)
            {
                open = Native.OpenClipboard(IntPtr.Zero);
                if (!open) Thread.Sleep(30);
            }
            if (!open) return null;
            try
            {
                var names = new System.Collections.Generic.List<string>();
                var sb = new StringBuilder(256);
                for (uint fmt = Native.EnumClipboardFormats(0); fmt != 0; fmt = Native.EnumClipboardFormats(fmt))
                    names.Add(Native.GetClipboardFormatName(fmt, sb, sb.Capacity) > 0 ? sb.ToString() : "#" + fmt);
                return names.ToArray();
            }
            finally { Native.CloseClipboard(); }
        }

        static bool Has(string[] f, string name) { return Array.IndexOf(f, name) >= 0; }

        static int Dword(IDataObject d, string[] f, string name)
        {
            if (!Has(f, name)) return -1;
            try
            {
                var ms = d.GetData(name) as MemoryStream;
                if (ms == null || ms.Length < 4) return -1;
                var buf = new byte[4];
                ms.Read(buf, 0, 4);
                return BitConverter.ToInt32(buf, 0);
            }
            catch (Exception) { return -1; }
        }

        static void Text(Content c, string t)
        {
            string trimmed = t.Trim();
            int lines = 1;
            foreach (char ch in trimmed) if (ch == '\n') lines++;

            Uri u;
            if (lines == 1 && trimmed.IndexOf(' ') < 0 && Uri.TryCreate(trimmed, UriKind.Absolute, out u) && (u.Scheme == "http" || u.Scheme == "https"))
            {
                c.Meta = L.Link;
                c.Sub = Flatten(trimmed.Substring(u.Scheme.Length + 3));
            }
            else
            {
                c.Meta = lines > 1 ? L.Lines(lines) : L.Chars(t.Length);
                c.Sub = trimmed.Length == 0 ? L.Blank : Flatten(trimmed);
            }
            c.Hold = c.Sub;
            c.Sig = "txt:" + t.Length + ":" + t.GetHashCode();
        }

        static void Files(Content c, string[] files)
        {
            if (files.Length == 1)
            {
                c.Meta = Directory.Exists(files[0]) ? L.Folder : L.File;
                c.Sub = Name(files[0]);
            }
            else
            {
                c.Meta = L.Items(files.Length);
                var sb = new StringBuilder();
                for (int i = 0; i < files.Length && i < 8; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(Name(files[i]));
                }
                c.Sub = sb.ToString();
            }
            c.Hold = c.Sub;
            c.Sig = "files:" + files.Length + ":" + string.Join("|", files).GetHashCode();
        }

        static string Name(string path)
        {
            string n = Path.GetFileName(path.TrimEnd('\\', '/'));
            return string.IsNullOrEmpty(n) ? path : n;
        }

        static string Flatten(string s)
        {
            var sb = new StringBuilder(Math.Min(s.Length, 200));
            bool space = false;
            foreach (char ch in s)
            {
                if (char.IsWhiteSpace(ch)) { space = sb.Length > 0; continue; }
                if (space) { sb.Append(' '); space = false; }
                sb.Append(ch);
                if (sb.Length >= 200) break;
            }
            return sb.ToString();
        }

        static Bitmap Thumb(Image img, int size)
        {
            var t = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(t))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                float k = Math.Max((float)size / img.Width, (float)size / img.Height);
                float w = img.Width * k, h = img.Height * k;
                g.DrawImage(img, (size - w) / 2, (size - h) / 2, w, h);
            }
            return t;
        }

        static int Hash(Bitmap b)
        {
            unchecked
            {
                int h = (int)2166136261;
                foreach (int p in Alpha.Read(b)) h = (h ^ p) * 16777619;
                return h;
            }
        }
    }

    // Hidden top-level window: clipboard notifications, key events from the hook thread,
    // and the "hello" broadcast from a second instance.
    sealed class Hub : NativeWindow
    {
        public static readonly int HelloMsg = Native.RegisterWindowMessage("CCCC.Hello");

        public event Action ClipboardChanged;
        public event Action<int, uint> Key;
        public event Action Hello;

        public Hub()
        {
            CreateHandle(new CreateParams { Caption = "CCCC.Hub", Style = Native.WS_POPUP, ExStyle = Native.WS_EX_TOOLWINDOW });
            Native.AddClipboardFormatListener(Handle);
        }

        public void Close()
        {
            Native.RemoveClipboardFormatListener(Handle);
            DestroyHandle();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_CLIPBOARDUPDATE) { if (ClipboardChanged != null) ClipboardChanged(); return; }
            if (m.Msg == Native.WM_APP_KEY) { if (Key != null) Key((int)m.WParam, unchecked((uint)(int)m.LParam.ToInt64())); return; }
            if (m.Msg == HelloMsg && HelloMsg != 0) { if (Hello != null) Hello(); return; }
            base.WndProc(ref m);
        }
    }

    // Low-level keyboard hook on its own thread so rendering never delays system input.
    // It only notes *that* a copy/cut chord was pressed, plus the clipboard sequence number at that instant,
    // and that Caps Lock was released (the toggle has flipped by then).
    sealed class KeyHook
    {
        public const int Copy = 1, CutKey = 2, Caps = 3, Passthrough = 0x100;

        static readonly string[] Shells =
        {
            "ConsoleWindowClass", "CASCADIA_HOSTING_WINDOW_CLASS", "mintty", "PuTTY", "VirtualConsoleClass",
            "org.wezfurlong.wezterm", "TscShellContainerClass", "VMUIFrame", "VMPlayerFrame", "Vim", "Emacs",
        };

        readonly IntPtr target;
        readonly uint ownPid = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
        Native.HookProc proc;
        uint threadId;
        bool cHeld, xHeld, insHeld;

        public KeyHook(IntPtr target) { this.target = target; }

        public void Start()
        {
            new Thread(Run) { IsBackground = true, Name = "CCCC.KeyHook" }.Start();
        }

        public void Stop()
        {
            if (threadId != 0) Native.PostThreadMessage(threadId, 0x0012, IntPtr.Zero, IntPtr.Zero);
        }

        void Run()
        {
            threadId = Native.GetCurrentThreadId();
            proc = Callback;
            IntPtr hook = Native.SetWindowsHookEx(13, proc, Native.GetModuleHandle(null), 0);
            Native.MSG msg;
            while (Native.GetMessage(out msg, IntPtr.Zero, 0, 0) > 0) { }
            if (hook != IntPtr.Zero) Native.UnhookWindowsHookEx(hook);
        }

        IntPtr Callback(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code >= 0)
            {
                int msg = (int)wParam, vk = Marshal.ReadInt32(lParam);
                if (vk == 0x14 && (msg == 0x101 || msg == 0x105))
                    Native.PostMessage(target, Native.WM_APP_KEY, (IntPtr)Caps, IntPtr.Zero);
                else if (vk == 0x43 || vk == 0x58 || vk == 0x2D)
                {
                    bool down = msg == 0x100 || msg == 0x104;
                    bool held = vk == 0x43 ? cHeld : vk == 0x58 ? xHeld : insHeld;
                    // Selection tools (translate/search bubbles) fake Ctrl+C, then restore the old clipboard.
                    bool injected = (Marshal.ReadInt32(lParam, 8) & 0x10) != 0;
                    if (down && !held && !injected && Chord(vk)) Fire(vk == 0x58 ? CutKey : Copy);
                    if (vk == 0x43) cHeld = down; else if (vk == 0x58) xHeld = down; else insHeld = down;
                }
            }
            return Native.CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
        }

        static bool Chord(int vk)
        {
            if (!Native.KeyDown(0x11) || Native.KeyDown(0x12) || Native.KeyDown(0x5B) || Native.KeyDown(0x5C)) return false;
            return vk == 0x2D || !Native.KeyDown(0x10);
        }

        void Fire(int kind)
        {
            uint seq = Native.GetClipboardSequenceNumber();
            IntPtr fg = Native.GetForegroundWindow();
            uint pid;
            Native.GetWindowThreadProcessId(fg, out pid);
            if (pid == ownPid || Array.IndexOf(Shells, Native.ClassOf(fg)) >= 0) kind |= Passthrough;
            Native.PostMessage(target, Native.WM_APP_KEY, (IntPtr)kind, new IntPtr(unchecked((int)seq)));
        }
    }
}
