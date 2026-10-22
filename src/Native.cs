using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Cccc
{
    static class Native
    {
        public const int WM_CLIPBOARDUPDATE = 0x031D;
        public const int WM_MOUSEACTIVATE = 0x0021;
        public const int WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202, WM_RBUTTONUP = 0x0205;
        public const int WM_MOUSEMOVE = 0x0200;
        public const int WM_APP_KEY = 0x8001, WM_APP_FRAME = 0x8002;
        public const int MA_NOACTIVATE = 3;
        public const int WS_POPUP = unchecked((int)0x80000000);
        public const int WS_EX_LAYERED = 0x80000, WS_EX_TOOLWINDOW = 0x80, WS_EX_TOPMOST = 0x8, WS_EX_NOACTIVATE = 0x08000000;
        public const int SW_HIDE = 0, SW_SHOWNOACTIVATE = 4;
        public const uint SWP_NOSIZE = 1, SWP_NOMOVE = 2, SWP_NOACTIVATE = 0x10;
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public static readonly IntPtr HWND_BROADCAST = new IntPtr(0xFFFF);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X, Y; public POINT(int x, int y) { X = x; Y = y; } }

        [StructLayout(LayoutKind.Sequential)]
        public struct SIZE { public int W, H; public SIZE(int w, int h) { W = w; H = h; } }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO { public int cbSize; public RECT rcMonitor, rcWork; public int dwFlags; }

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER
        {
            public int biSize, biWidth, biHeight;
            public short biPlanes, biBitCount;
            public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public POINT pt; }

        public delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")] public static extern bool AddClipboardFormatListener(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern uint GetClipboardSequenceNumber();
        [DllImport("user32.dll")] public static extern IntPtr GetClipboardOwner();
        [DllImport("user32.dll")] public static extern bool OpenClipboard(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool CloseClipboard();
        [DllImport("ole32.dll")] public static extern int OleGetClipboard([MarshalAs(UnmanagedType.IUnknown)] out object data);
        [DllImport("user32.dll")] public static extern uint EnumClipboardFormats(uint format);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClipboardFormatName(uint format, StringBuilder sb, int max);
        [DllImport("user32.dll")] public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hwnd, int cmd);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT pt);
        [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
        [DllImport("user32.dll")] public static extern IntPtr MonitorFromPoint(POINT pt, uint flags);
        [DllImport("user32.dll")] public static extern IntPtr SetCapture(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool ReleaseCapture();
        [DllImport("user32.dll")] public static extern bool GetMonitorInfo(IntPtr hmon, ref MONITORINFO mi);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hwnd, StringBuilder sb, int max);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowTextLength(IntPtr hwnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hwnd, StringBuilder sb, int max);
        [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vk);
        [DllImport("user32.dll")] static extern short GetKeyState(int vk);
        [DllImport("user32.dll")] public static extern IntPtr SetWindowsHookEx(int id, HookProc fn, IntPtr hMod, uint threadId);
        [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] public static extern int GetMessage(out MSG msg, IntPtr hwnd, uint min, uint max);
        [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] public static extern bool PostThreadMessage(uint threadId, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int RegisterWindowMessage(string name);
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr hicon);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")] public static extern bool GdiFlush();
        [DllImport("gdi32.dll")] public static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER bmi, uint usage, out IntPtr bits, IntPtr section, uint offset);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr GetModuleHandle(string name);
        [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
        [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder sb, ref uint size);
        [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr handle);
        [DllImport("shcore.dll")] public static extern int GetDpiForMonitor(IntPtr hmon, int type, out uint dpiX, out uint dpiY);
        [DllImport("shell32.dll")] public static extern int SHQueryUserNotificationState(out int state);
        [DllImport("dwmapi.dll")] public static extern int DwmFlush();
        [DllImport("winmm.dll", CharSet = CharSet.Unicode)] public static extern int mciSendString(string command, StringBuilder ret, int retLen, IntPtr callback);
        [DllImport("uxtheme.dll", EntryPoint = "#135")] static extern int SetPreferredAppMode(int mode);
        [DllImport("uxtheme.dll", EntryPoint = "#136")] static extern void FlushMenuThemes();

        public static bool KeyDown(int vk) { return (GetAsyncKeyState(vk) & 0x8000) != 0; }
        public static bool CapsLock() { return (GetKeyState(0x14) & 1) != 0; }

        public static string ClassOf(IntPtr hwnd)
        {
            var sb = new StringBuilder(128);
            GetClassName(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }

        // Undocumented but stable since 1903; popup menus follow it.
        public static void ThemeMenus(bool dark)
        {
            try
            {
                var v = Environment.OSVersion.Version;
                if (v.Major < 10 || v.Build < 18362) return;
                SetPreferredAppMode(dark ? 2 : 3);
                FlushMenuThemes();
            }
            catch (Exception) { }
        }

        // Fullscreen game, presentation, or locked screen.
        public static bool UserBusy()
        {
            try
            {
                int st;
                if (SHQueryUserNotificationState(out st) == 0) return st >= 1 && st <= 4;
            }
            catch (Exception) { }
            return false;
        }
    }
}
