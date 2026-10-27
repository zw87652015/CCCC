using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Forms;

namespace Cccc
{
    // Where the Wi-Fi card parks: on the inner side of the taskbar, centred on the tray's
    // network button (Control Center on Windows 11, the notify area on Windows 10).
    static class Anchor
    {
        public struct Spot
        {
            public Rectangle Work;   // work area of the taskbar monitor, device px
            public Rectangle Icon;   // tray anchor rect, device px
            public int Edge;         // ABE_*: 0 left, 1 top, 2 right, 3 bottom
        }

        public static string Sig = "";

        // nameHint: used by the Wi-Fi and sound-output cards — the network name or the output
        // device's friendly name; on recent Windows the matching tray button's accessible name
        // contains it, whatever the display language.
        public static Spot Tray(string nameHint, out float dpiScale)
        {
            dpiScale = 1;
            var spot = new Spot { Edge = 3 };
            IntPtr tray = Native.FindWindow("Shell_TrayWnd", null);
            var abd = new Native.APPBARDATA { cbSize = Marshal.SizeOf(typeof(Native.APPBARDATA)), hWnd = tray };
            Rectangle tb = Rectangle.Empty;
            if (Native.SHAppBarMessage(Native.ABM_GETTASKBARPOS, ref abd) != IntPtr.Zero)
            {
                tb = Rect(abd.rc);
                spot.Edge = (int)abd.uEdge;
                IntPtr mon = Native.MonitorFromRect(ref abd.rc, 2);
                var mi = new Native.MONITORINFO { cbSize = Marshal.SizeOf(typeof(Native.MONITORINFO)) };
                Native.GetMonitorInfo(mon, ref mi);
                spot.Work = Rect(mi.rcWork);
                uint dpi = 96, dpiY;
                try { if (Native.GetDpiForMonitor(mon, 0, out dpi, out dpiY) == 0) dpiScale = dpi / 96f; }
                catch (Exception) { }
            }
            else spot.Work = SystemInformation.WorkingArea;

            spot.Icon = ControlCenter(tray);
            if (spot.Icon.IsEmpty) spot.Icon = TrayIcon(tray, nameHint);
            if (spot.Icon.IsEmpty && tray != IntPtr.Zero)
            {
                IntPtr area = Native.FindWindowEx(tray, IntPtr.Zero, "TrayNotifyWnd", null);
                Native.RECT r;
                if (area != IntPtr.Zero && Native.GetWindowRect(area, out r) && r.Right > r.Left && r.Bottom > r.Top)
                    spot.Icon = Rect(r);
            }
            if (spot.Icon.IsEmpty)
            {
                // Last resort: the tray end of the taskbar (right for top/bottom, bottom for left/right).
                if (!tb.IsEmpty)
                    spot.Icon = spot.Edge == 0 || spot.Edge == 2
                        ? new Rectangle(tb.Left, Math.Max(tb.Top, tb.Bottom - 200), tb.Width, 200)
                        : new Rectangle(Math.Max(tb.Left, tb.Right - 240), tb.Top, 240, tb.Height);
                else
                    spot.Icon = new Rectangle(spot.Work.Right - 240, spot.Work.Bottom - 48, 240, 48);
            }
            Sig = string.Join("|", new object[] { spot.Work.Left, spot.Work.Top, spot.Work.Right, spot.Work.Bottom,
                spot.Icon.Left, spot.Icon.Top, spot.Icon.Right, spot.Icon.Bottom, spot.Edge, dpiScale });
            return spot;
        }

        // The card's top-left corner: centred on the anchor along the taskbar axis, Edge DIP in from it,
        // and clamped inside the work area. A top taskbar slides the card down.
        public static Point Place(Spot s, Size card, int margin, out bool slideDown)
        {
            slideDown = s.Edge == 1;
            int x, y;
            if (s.Edge == 0 || s.Edge == 2)
            {
                x = s.Edge == 0 ? s.Work.Left + margin : s.Work.Right - margin - card.Width;
                y = Center(s.Icon.Top + s.Icon.Height / 2, card.Height, s.Work.Top + margin, s.Work.Bottom - margin);
            }
            else
            {
                y = s.Edge == 1 ? s.Work.Top + margin : s.Work.Bottom - margin - card.Height;
                x = Center(s.Icon.Left + s.Icon.Width / 2, card.Width, s.Work.Left + margin, s.Work.Right - margin);
            }
            return new Point(x, y);
        }

        static int Center(int at, int size, int lo, int hi)
        {
            return Math.Max(lo, Math.Min(at - size / 2, hi - size));
        }

        static Rectangle Rect(Native.RECT r)
        {
            return Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
        }

        // Windows 11: the network/volume/battery group is a UIA element under Shell_TrayWnd.
        static Rectangle ControlCenter(IntPtr tray)
        {
            if (tray == IntPtr.Zero) return Rectangle.Empty;
            try
            {
                var el = AutomationElement.FromHandle(tray).FindFirst(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.AutomationIdProperty, "ControlCenterButton"));
                if (el == null) return Rectangle.Empty;
                var r = el.Current.BoundingRectangle;
                if (r.IsEmpty || r.Width <= 0 || r.Height <= 0) return Rectangle.Empty;
                return new Rectangle((int)Math.Round(r.X), (int)Math.Round(r.Y), (int)Math.Round(r.Width), (int)Math.Round(r.Height));
            }
            catch (Exception) { return Rectangle.Empty; }
        }

        // Windows 11 26H2: the tray buttons are SystemTrayIcon elements; the Wi-Fi one's
        // name carries the network name ("网络 PolyUWLAN" / "Network PolyUWLAN").
        static Rectangle TrayIcon(IntPtr tray, string nameHint)
        {
            if (tray == IntPtr.Zero || string.IsNullOrEmpty(nameHint)) return Rectangle.Empty;
            try
            {
                var all = AutomationElement.FromHandle(tray).FindAll(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.AutomationIdProperty, "SystemTrayIcon"));
                for (int i = 0; i < all.Count; i++)
                {
                    var el = all[i];
                    string name = el.Current.Name;
                    if (name == null || name.IndexOf(nameHint, StringComparison.Ordinal) < 0) continue;
                    var r = el.Current.BoundingRectangle;
                    if (r.IsEmpty || r.Width <= 0 || r.Height <= 0) continue;
                    return new Rectangle((int)Math.Round(r.X), (int)Math.Round(r.Y), (int)Math.Round(r.Width), (int)Math.Round(r.Height));
                }
            }
            catch (Exception) { }
            return Rectangle.Empty;
        }
    }
}
