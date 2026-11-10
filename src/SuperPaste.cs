using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Cccc
{
    interface IPasteDesktop
    {
        IntPtr Foreground { get; }
        uint Sequence { get; }
        bool KeysReleased { get; }
        bool TryRead(out string text, out string reason);
        bool TryWrite(string text, uint expected, out uint written);
        bool SendPaste();
    }
    // Nonblocking transaction: each timer tick revalidates both focus and clipboard ownership.
    sealed class SuperPaste : IDisposable
    {
        readonly IPasteDesktop desktop;
        readonly Func<double> now;
        readonly Timer timer = new Timer { Interval = 25 };
        readonly Action<string> notice;
        readonly Action<uint> ownWrite;
        public bool Busy { get; private set; }
        IntPtr target;
        uint sequence;
        double start;
        string text;
        bool committed;
        PasteRules rules;
        public SuperPaste(IPasteDesktop desktop, Func<double> now, Action<string> notice, Action<uint> ownWrite)
        {
            this.desktop = desktop; this.now = now; this.notice = notice; this.ownWrite = ownWrite;
            timer.Tick += delegate { Step(); };
        }
        public void Begin(PasteRules rules)
        {
            if (Busy) return;
            this.rules = rules; target = desktop.Foreground; sequence = desktop.Sequence; start = now(); text = null; committed = false;
            Busy = true; timer.Start();
        }
        public void Cancel() { timer.Stop(); Busy = false; text = null; }
        void Fail(string message) { Cancel(); notice(message); }
        public void Step()
        {
            try { StepCore(); }
            catch (Exception) { Fail(committed ? L.SPManualPaste : L.SPClipboardBusy); }
        }
        void StepCore()
        {
            if (!Busy) return;
            if (target == IntPtr.Zero || desktop.Foreground != target || desktop.Sequence != sequence)
            { Fail(L.SPChanged); return; }
            if (now() - start >= 2) { Fail(L.SPTimeout); return; }
            if (text == null)
            {
                string reason;
                if (!desktop.TryRead(out text, out reason))
                {
                    if (reason != null) Fail(reason);
                    else if (now() - start >= 1) Fail(L.SPClipboardBusy);
                    return;
                }
                if (desktop.Sequence != sequence) { Fail(L.SPChanged); return; }
                text = SuperPasteText.Clean(text, rules);
                if (string.IsNullOrWhiteSpace(text)) { Fail(L.SPEmpty); return; }
            }
            if (!desktop.KeysReleased) return;
            if (now() - start >= 2) { Fail(L.SPTimeout); return; }
            if (desktop.Foreground != target || desktop.Sequence != sequence) { Fail(L.SPChanged); return; }
            uint written;
            if (!desktop.TryWrite(text, sequence, out written))
            { if (now() - start >= 1) Fail(L.SPClipboardBusy); return; }
            committed = true;
            ownWrite(written);
            // Do not send into a newly focused window even if clipboard writing yielded to it.
            bool sameClipboard = desktop.Sequence == written;
            bool sameFocus = desktop.Foreground == target;
            Native.Debug("super-paste: committed=" + written + ", current=" + desktop.Sequence + ", same-focus=" + (desktop.Foreground == target));
            Cancel();
            if (!sameClipboard) notice(L.SPChanged);
            else if (!sameFocus || !desktop.SendPaste()) notice(L.SPManualPaste);
        }
        public void Dispose() { Cancel(); timer.Dispose(); }
    }

    sealed class WindowsPasteDesktop : IPasteDesktop
    {
        [DllImport("user32.dll")] static extern bool IsClipboardFormatAvailable(uint format);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint RegisterClipboardFormat(string name);
        [DllImport("user32.dll")] static extern IntPtr GetClipboardData(uint format);
        [DllImport("user32.dll")] static extern bool EmptyClipboard();
        [DllImport("user32.dll")] static extern IntPtr SetClipboardData(uint format, IntPtr value);
        [DllImport("kernel32.dll")] static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);
        [DllImport("kernel32.dll")] static extern IntPtr GlobalLock(IntPtr handle);
        [DllImport("kernel32.dll")] static extern bool GlobalUnlock(IntPtr handle);
        [DllImport("kernel32.dll")] static extern IntPtr GlobalFree(IntPtr handle);
        [DllImport("kernel32.dll")] static extern UIntPtr GlobalSize(IntPtr handle);
        [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public UNION data; }
        [StructLayout(LayoutKind.Explicit)] struct UNION { [FieldOffset(0)] public KEYBD keyboard; [FieldOffset(0)] public MOUSE mouse; }
        [StructLayout(LayoutKind.Sequential)] struct KEYBD { public ushort key, scan; public uint flags, time; public UIntPtr extra; }
        [StructLayout(LayoutKind.Sequential)] struct MOUSE { public int x, y; public uint data, flags, time; public UIntPtr extra; }
        [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint count, INPUT[] inputs, int size);
        readonly IntPtr owner;
        readonly Func<int> triggerKey;
        public WindowsPasteDesktop(IntPtr owner, Func<int> triggerKey = null) { this.owner = owner; this.triggerKey = triggerKey ?? (() => 0x56); }
        public IntPtr Foreground { get { return Native.GetForegroundWindow(); } }
        public uint Sequence { get { return Native.GetClipboardSequenceNumber(); } }
        public bool KeysReleased
        {
            get { foreach (int key in new[] { 0x10, 0x11, 0x12, 0x5B, 0x5C, triggerKey() }) if (Native.KeyDown(key)) return false; return true; }
        }
        internal static bool SupportsText(Func<uint, bool> available)
        {
            // Rich-text providers (including Excel) may also advertise a bitmap preview.
            // Consume Unicode text only; file lists and textless images remain unsupported.
            return !available(15) && available(13);
        }
        public bool TryRead(out string text, out string reason)
        {
            text = null; reason = null;
            if (!Native.OpenClipboard(owner)) return false;
            try
            {
                foreach (string format in new[] { "ExcludeClipboardContentFromMonitorProcessing", "Clipboard Viewer Ignore" })
                    if (IsClipboardFormatAvailable(RegisterClipboardFormat(format))) { reason = L.SPPrivate; return false; }
                uint history = RegisterClipboardFormat("CanIncludeInClipboardHistory");
                if (IsClipboardFormatAvailable(history))
                {
                    IntPtr h = GetClipboardData(history), p = GlobalLock(h);
                    if (p == IntPtr.Zero) { reason = L.SPPrivate; return false; }
                    try { if (GlobalSize(h).ToUInt64() < 4 || Marshal.ReadInt32(p) == 0) { reason = L.SPPrivate; return false; } }
                    finally { GlobalUnlock(h); }
                }
                if (!SupportsText(IsClipboardFormatAvailable))
                { reason = L.SPNotText; return false; }
                IntPtr handle = GetClipboardData(13), data = GlobalLock(handle);
                if (data == IntPtr.Zero) return false;
                try
                {
                    ulong size = GlobalSize(handle).ToUInt64();
                    if (size > 2 * 1024 * 1024 || size < 2) { reason = L.SPTooLarge; return false; }
                    int chars = (int)size / 2; var buffer = new char[chars]; Marshal.Copy(data, buffer, 0, chars);
                    int end = Array.IndexOf(buffer, '\0'); if (end < 0) { reason = L.SPNotText; return false; }
                    text = new string(buffer, 0, end); return true;
                }
                finally { GlobalUnlock(handle); }
            }
            finally { Native.CloseClipboard(); }
        }
        public bool TryWrite(string text, uint expected, out uint written)
        {
            written = 0;
            IntPtr h = GlobalAlloc(0x42, (UIntPtr)((text.Length + 1) * 2));
            if (h == IntPtr.Zero) return false;
            try
            {
                IntPtr p = GlobalLock(h); if (p == IntPtr.Zero) return false;
                try { var chars = (text + "\0").ToCharArray(); Marshal.Copy(chars, 0, p, chars.Length); }
                finally { GlobalUnlock(h); }
                if (!Native.OpenClipboard(owner)) return false;
                try
                {
                    if (Sequence != expected || !EmptyClipboard()) return false;
                    if (SetClipboardData(13, h) == IntPtr.Zero) return false;
                    h = IntPtr.Zero;
                }
                finally { Native.CloseClipboard(); }
                // Capture the revision after the write transaction has closed, so any
                // format finalization during CloseClipboard is included.
                // A different owner means another app has already replaced our result.
                if (Native.GetClipboardOwner() != owner) return false;
                written = Sequence;
                return true;
            }
            finally { if (h != IntPtr.Zero) GlobalFree(h); }
        }
        public bool SendPaste()
        {
            var keys = new INPUT[4];
            for (int i = 0; i < 4; i++)
            { keys[i].type = 1; keys[i].data.keyboard.key = (ushort)(i == 0 || i == 3 ? 0x11 : 0x56); keys[i].data.keyboard.flags = (uint)(i >= 2 ? 2 : 0); }
            uint sent = SendInput(4, keys, Marshal.SizeOf(typeof(INPUT)));
            Native.Debug("super-paste: input-events=" + sent + "/4, win32-error=" + (sent == 4 ? 0 : Marshal.GetLastWin32Error()));
            if (sent > 0 && sent < 4)
            {
                // Release only our injected keys after a partial send; never retry the paste.
                var release = new[] { keys[2], keys[3] };
                SendInput(2, release, Marshal.SizeOf(typeof(INPUT)));
            }
            return sent == 4;
        }
    }
}
