using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Cccc
{
    sealed class PasteHotkey : NativeWindow, IDisposable
    {
        [DllImport("user32.dll", SetLastError = true)] static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hwnd, int id);
        int currentKey, currentModifiers;
        bool registered;
        public event Action Pressed;
        public bool Active { get { return registered; } }
        public int ActiveKey { get { return currentKey; } }
        public PasteHotkey() { CreateHandle(new CreateParams { Caption = "CCCC.SuperPaste", Parent = new IntPtr(-3) }); }
        public static bool ValidKey(int key) { return key >= 0x30 && key <= 0x39 || key >= 0x41 && key <= 0x5A || key >= 0x70 && key <= 0x7B; }
        public bool Configure(bool enabled, int modifiers, int key)
        {
            if (!enabled) { Release(); return true; }
            // Ctrl+V must remain the normal paste chord, including our injected paste.
            if (!ValidKey(key) || modifiers < 1 || modifiers > 7 || key == 0x56 && modifiers == 2) return false;
            if (registered && currentKey == key && currentModifiers == modifiers) return true;
            // Register the candidate before releasing the working shortcut.
            int next = id == 1 ? 2 : 1;
            if (!RegisterHotKey(Handle, next, (uint)(modifiers | 0x4000), (uint)key)) return false;
            if (registered) UnregisterHotKey(Handle, id);
            id = next; currentKey = key; currentModifiers = modifiers; registered = true;
            return true;
        }
        int id;
        void Release() { if (registered) UnregisterHotKey(Handle, id); registered = false; }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x312) { if (registered && (int)m.WParam == id && Pressed != null) Pressed(); return; }
            base.WndProc(ref m);
        }
        public void Dispose() { Release(); DestroyHandle(); }
    }
}
