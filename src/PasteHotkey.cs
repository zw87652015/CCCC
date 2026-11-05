using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Cccc
{
    sealed class PasteHotkey : NativeWindow, IDisposable
    {
        [DllImport("user32.dll", SetLastError = true)] static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hwnd, int id);
        readonly Dictionary<string, int> registrations = new Dictionary<string, int>();
        public event Action<int> Pressed;
        public string[] RegisteredShortcuts { get { return new List<string>(registrations.Keys).ToArray(); } }
        public bool Active { get { return registrations.Count > 0; } }
        public int ActiveKey { get; private set; }
        public string FailedShortcut { get; private set; }
        public string ActiveText { get { var names = new List<string>(); foreach (string s in registrations.Keys) names.Add(Display(s)); return string.Join(", ", names.ToArray()); } }
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
        public bool Configure(bool enabled, int modifiers, int key) { return Configure(enabled, new[] { modifiers + ":" + key }); }
        public bool Configure(bool enabled, IEnumerable<string> shortcuts)
        {
            FailedShortcut = null;
            if (!enabled) { Release(); return true; }
            var wanted = new HashSet<string>();
            foreach (string shortcut in shortcuts)
            {
                int mods, key;
                if (!Parse(shortcut, out mods, out key)) { FailedShortcut = shortcut; return false; }
                wanted.Add(mods + ":" + key);
            }
            if (wanted.Count == 0) { FailedShortcut = ""; return false; }
            // Stage additions first; a failure leaves the entire working set intact.
            var added = new Dictionary<string, int>();
            foreach (string shortcut in wanted)
            {
                if (registrations.ContainsKey(shortcut)) continue;
                int id = 1;
                while (registrations.ContainsValue(id) || added.ContainsValue(id)) id++;
                int mods, key; Parse(shortcut, out mods, out key);
                if (!RegisterHotKey(Handle, id, (uint)(mods | 0x4000), (uint)key))
                {
                    foreach (int staged in added.Values) UnregisterHotKey(Handle, staged);
                    FailedShortcut = Display(shortcut); return false;
                }
                added.Add(shortcut, id);
            }
            foreach (string old in new List<string>(registrations.Keys))
                if (!wanted.Contains(old)) { UnregisterHotKey(Handle, registrations[old]); registrations.Remove(old); }
            foreach (var entry in added) registrations.Add(entry.Key, entry.Value);
            foreach (string shortcut in registrations.Keys) { int mods, key; Parse(shortcut, out mods, out key); ActiveKey = key; break; }
            return true;
        }
        void Release() { foreach (int id in registrations.Values) UnregisterHotKey(Handle, id); registrations.Clear(); ActiveKey = 0; }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x312)
            {
                foreach (var entry in registrations)
                    if (entry.Value == (int)m.WParam)
                    {
                        int mods, key; Parse(entry.Key, out mods, out key);
                        // Reject queued messages from an old binding that reused this ID.
                        long chord = m.LParam.ToInt64();
                        if ((chord & 0xFFFF) == mods && ((chord >> 16) & 0xFFFF) == key && Pressed != null) Pressed(key);
                        break;
                    }
                return;
            }
            base.WndProc(ref m);
        }
        public void Dispose() { Release(); DestroyHandle(); }
    }
}
