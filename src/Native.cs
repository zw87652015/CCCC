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
        public const uint WDA_EXCLUDEFROMCAPTURE = 0x11;
        public const int SRCCOPY = 0x00CC0020;
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
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hwnd, int index);
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
        [DllImport("user32.dll")] public static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint affinity);
        [DllImport("gdi32.dll")] public static extern bool GdiFlush();
        [DllImport("gdi32.dll")] public static extern bool BitBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, int rop);
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

        // Taskbar and tray lookups for the Wi-Fi card anchor.
        public const uint ABM_GETTASKBARPOS = 5;

        [StructLayout(LayoutKind.Sequential)]
        public struct APPBARDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public uint uCallbackMessage;
            public uint uEdge;
            public RECT rc;
            public int lParam;
        }

        [DllImport("shell32.dll")] public static extern IntPtr SHAppBarMessage(uint msg, ref APPBARDATA data);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string cls, string title);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string title);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT r);
        [DllImport("user32.dll")] public static extern IntPtr MonitorFromRect(ref RECT r, uint flags);

        // Native Wi-Fi (wlanapi). Notification sources: ACM = 8, MSM = 16.
        public const int WLAN_ACM = 8, WLAN_MSM = 16;
        public const int WLAN_OP_RADIO_STATE = 4, WLAN_OP_INTERFACE_STATE = 6, WLAN_OP_CURRENT_CONNECTION = 7, WLAN_OP_CHANNEL_NUMBER = 8;
        public const int WLAN_ACM_CONNECTION_COMPLETE = 10, WLAN_ACM_DISCONNECTED = 21, WLAN_MSM_ROAMING_END = 6;

        public delegate void WlanNotify(ref WLAN_NOTIFICATION_DATA data, IntPtr context);

        [StructLayout(LayoutKind.Sequential)]
        public struct WLAN_NOTIFICATION_DATA
        {
            public int Source;
            public int Code;
            public Guid InterfaceGuid;
            public int DataSize;
            public IntPtr Data;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DOT11_SSID { public uint Length; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] Bytes; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WLAN_CONNECTION_NOTIFICATION_DATA
        {
            public int ConnectionMode;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string ProfileName;
            public DOT11_SSID Ssid;
            public int BssType;
            public int SecurityEnabled;
            public uint ReasonCode;
            public uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1)] public string ProfileXml;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WLAN_ASSOCIATION_ATTRIBUTES
        {
            public DOT11_SSID Ssid;
            public int BssType;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] public byte[] Bssid;
            public int PhyType;
            public uint PhyIndex;
            public uint SignalQuality;
            public uint RxRate, TxRate;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WLAN_SECURITY_ATTRIBUTES
        {
            public int SecurityEnabled, OneXEnabled;
            public int AuthAlgorithm, CipherAlgorithm;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WLAN_CONNECTION_ATTRIBUTES
        {
            public int State;
            public int ConnectionMode;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string ProfileName;
            public WLAN_ASSOCIATION_ATTRIBUTES Assoc;
            public WLAN_SECURITY_ATTRIBUTES Security;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WLAN_INTERFACE_INFO
        {
            public Guid Guid;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Description;
            public int State;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WLAN_BSS_ENTRY
        {
            public DOT11_SSID Ssid;
            public uint PhyId;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] public byte[] Bssid;
            public int BssType;
            public int BssPhyType;
            public int Rssi;
            public uint LinkQuality;
            public byte InRegDomain;
            public ushort BeaconPeriod;
            public ulong Timestamp;
            public ulong HostTimestamp;
            public ushort CapabilityInfo;
            public uint ChCenterFrequency;
            public uint RateSetLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 126)] public ushort[] RateSet;
            public uint IeOffset;
            public uint IeSize;
        }

        [DllImport("wlanapi.dll")] public static extern uint WlanOpenHandle(uint version, IntPtr reserved, out uint negotiated, out IntPtr handle);
        [DllImport("wlanapi.dll")] public static extern uint WlanCloseHandle(IntPtr handle, IntPtr reserved);
        [DllImport("wlanapi.dll")] public static extern void WlanFreeMemory(IntPtr mem);
        [DllImport("wlanapi.dll")] public static extern uint WlanRegisterNotification(IntPtr handle, uint sources, bool ignoreDup, WlanNotify callback, IntPtr context, IntPtr reserved, out uint prevSources);
        [DllImport("wlanapi.dll")] public static extern uint WlanEnumInterfaces(IntPtr handle, IntPtr reserved, out IntPtr list);
        [DllImport("wlanapi.dll")] public static extern uint WlanQueryInterface(IntPtr handle, ref Guid guid, int opcode, IntPtr reserved, out uint size, out IntPtr data, out int type);
        [DllImport("wlanapi.dll")] public static extern uint WlanGetNetworkBssList(IntPtr handle, ref Guid guid, IntPtr ssid, int bssType, bool security, IntPtr reserved, out IntPtr list);

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

        const int WS_CAPTION = 0x00C00000;

        // Fullscreen game, presentation, or locked screen. QUNS_BUSY(2) is only a heuristic —
        // a permanent borderless overlay can keep it on forever, so it also needs the
        // foreground window to really be full screen (not maximized, not the shell).
        public static bool UserBusy()
        {
            try
            {
                int st;
                if (SHQueryUserNotificationState(out st) != 0) return false;
                if (st == 2 && !ForegroundFullScreen())
                {
                    if (debugPath != null) Debug("busy: state=2 ignored, foreground not full screen (" + ForegroundDesc() + ")");
                    return false;
                }
                if (st >= 1 && st <= 4)
                {
                    if (debugPath != null) Debug("busy: state=" + st + ", foreground=" + ForegroundDesc());
                    return true;
                }
            }
            catch (Exception) { }
            return false;
        }

        static bool ForegroundFullScreen()
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero || IsIconic(hwnd) || IsZoomed(hwnd)) return false;
            string cls = ClassOf(hwnd);
            if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd") return false;
            if ((GetWindowLong(hwnd, -16 /* GWL_STYLE */) & WS_CAPTION) == WS_CAPTION) return false;
            RECT r;
            if (!GetWindowRect(hwnd, out r)) return false;
            IntPtr mon = MonitorFromWindow(hwnd, 2);
            var mi = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
            if (!GetMonitorInfo(mon, ref mi)) return false;
            return r.Left <= mi.rcMonitor.Left && r.Top <= mi.rcMonitor.Top
                && r.Right >= mi.rcMonitor.Right && r.Bottom >= mi.rcMonitor.Bottom;
        }

        static string ForegroundDesc()
        {
            string cls;
            try { cls = ClassOf(GetForegroundWindow()); } catch (Exception) { cls = "?"; }
            string exe = null;
            try { exe = Sources.ForegroundExe(); } catch (Exception) { }
            return exe != null ? cls + " " + exe : cls;
        }

        // CCCC_DEBUG=1 appends timestamped lines to %TEMP%\CCCC\debug.log; silent otherwise.
        static readonly string debugPath = Environment.GetEnvironmentVariable("CCCC_DEBUG") == "1"
            ? System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CCCC", "debug.log") : null;

        public static void Debug(string line)
        {
            if (debugPath == null) return;
            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(debugPath));
                System.IO.File.AppendAllText(debugPath, DateTime.Now.ToString("HH:mm:ss.fff ") + line + "\r\n");
            }
            catch (Exception) { }
        }
    }
}
