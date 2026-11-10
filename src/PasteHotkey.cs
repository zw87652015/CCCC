using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Cccc
{
    sealed class PasteHotkey : NativeWindow, IDisposable
    {
        [DllImport("user32.dll", SetLastError = true)] static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hwnd, int id);
        int id = 1;
        string active;
        public event Action<int> Pressed;
        public bool Active { get { return active != null; } }
        public int ActiveKey { get; private set; }
        public string FailedShortcut { get; private set; }
        public string ActiveText { get { return active == null ? "" : Display(active); } }
        public string ActiveShortcut { get { return active; } }
        public PasteHotkey() { CreateHandle(new CreateParams { Caption = "CCCC.SuperPaste", Parent = new IntPtr(-3) }); }
        public static bool ValidKey(int key) { return key == 0x2D || key >= 0x30 && key <= 0x39 || key >= 0x41 && key <= 0x5A || key >= 0x70 && key <= 0x7B; }
        public static bool Parse(string text, out int modifiers, out int key)
        {
            modifiers = key = 0;
            string[] parts = (text ?? "").Split(':');
            return parts.Length == 2 && int.TryParse(parts[0], out modifiers) && int.TryParse(parts[1], out key) &&
                ValidKey(key) && modifiers >= 1 && modifiers <= 15 && !(key == 0x56 && modifiers == 2);
        }
        public static string Display(string text)
        {
            int mods, key;
            if (!Parse(text, out mods, out key)) return text;
            return ((mods & 2) != 0 ? "Ctrl+" : "") + ((mods & 4) != 0 ? "Shift+" : "") +
                ((mods & 1) != 0 ? "Alt+" : "") + ((mods & 8) != 0 ? "Windows+" : "") + ((Keys)key).ToString();
        }
        public bool Configure(bool enabled, string shortcut)
        {
            FailedShortcut = null;
            if (!enabled) { Release(); return true; }
            int mods, key;
            if (!Parse(shortcut, out mods, out key)) { FailedShortcut = shortcut ?? ""; return false; }
            string wanted = mods + ":" + key;
            if (active == wanted) return true;
            // Stage the new chord on the spare ID; a failure keeps the working registration.
            int next = id == 1 ? 2 : 1;
            if (!RegisterHotKey(Handle, next, (uint)(mods | 0x4000), (uint)key))
            { FailedShortcut = Display(wanted); return false; }
            if (active != null) UnregisterHotKey(Handle, id);
            id = next; active = wanted; ActiveKey = key;
            return true;
        }
        void Release() { if (active != null) { UnregisterHotKey(Handle, id); active = null; } ActiveKey = 0; }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x312 && active != null && (int)m.WParam == id)
            {
                // Reject queued messages from an old binding that reused this ID.
                int mods, key; Parse(active, out mods, out key);
                long chord = m.LParam.ToInt64();
                if ((chord & 0xFFFF) == mods && ((chord >> 16) & 0xFFFF) == key && Pressed != null) Pressed(key);
                return;
            }
            base.WndProc(ref m);
        }
        public void Dispose() { Release(); DestroyHandle(); }
    }
}
