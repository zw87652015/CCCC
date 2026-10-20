using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace Cccc
{
    sealed class Source { public string App, Site, Title, Exe; public Bitmap Icon; public bool Obvious; }

    static class Sources
    {
        sealed class AppInfo { public string Name, Exe; public Bitmap Icon; }

        static readonly Dictionary<string, AppInfo> apps = new Dictionary<string, AppInfo>(StringComparer.OrdinalIgnoreCase);
        static readonly string[] Seps = { " - ", " — ", " – ", " | " };

        public static Source Detect(bool atCopy, string site)
        {
            IntPtr fg = Native.GetForegroundWindow();
            uint fgPid = PidOf(fg);
            IntPtr owner = Native.GetClipboardOwner();
            uint pid = owner != IntPtr.Zero ? PidOf(owner) : atCopy ? fgPid : 0;
            if (pid == 0 || pid == (uint)Process.GetCurrentProcess().Id)
                return site == null ? null : new Source { Site = site };

            var src = new Source { Site = site, Obvious = atCopy && fgPid == pid };
            AppInfo info = Info(pid);
            if (info != null) { src.App = NameOf(info); src.Icon = info.Icon; }

            string raw = src.Obvious ? Window(Native.GetAncestor(fg, 2)) : MainTitle(pid);
            string exe = info != null ? info.Exe : null;
            src.Exe = exe;
            src.Title = StripApp(raw, src.App, exe);
            return src;
        }

        public static string ForegroundExe()
        {
            uint pid = PidOf(Native.GetForegroundWindow());
            if (pid == 0) return null;
            AppInfo info = Info(pid);
            return info != null ? info.Exe : null;
        }

        public static List<KeyValuePair<string, string>> Seen()
        {
            string self = Process.GetCurrentProcess().ProcessName;
            var byExe = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (AppInfo info in apps.Values)
                if (info.Exe != null && !byExe.ContainsKey(info.Exe)
                    && !string.Equals(info.Exe, self, StringComparison.OrdinalIgnoreCase))
                    byExe[info.Exe] = NameOf(info);
            var list = new List<KeyValuePair<string, string>>();
            foreach (var kv in byExe) list.Add(new KeyValuePair<string, string>(kv.Key, kv.Value));
            list.Sort(delegate (KeyValuePair<string, string> a, KeyValuePair<string, string> b)
            {
                return string.Compare(a.Value, b.Value, StringComparison.CurrentCultureIgnoreCase);
            });
            return list;
        }

        public static string NameFor(string exe)
        {
            foreach (AppInfo info in apps.Values)
                if (string.Equals(info.Exe, exe, StringComparison.OrdinalIgnoreCase)) return NameOf(info);
            return exe;
        }

        public static string OwnerApp()
        {
            IntPtr owner = Native.GetClipboardOwner();
            if (owner == IntPtr.Zero) return null;
            AppInfo info = Info(PidOf(owner));
            return info != null ? NameOf(info) : null;
        }

        static string NameOf(AppInfo info)
        {
            return string.Equals(info.Exe, "rdpclip", StringComparison.OrdinalIgnoreCase) ? L.RemoteDesktop : info.Name;
        }

        public static string StripApp(string title, string app, string exe)
        {
            string t = (title ?? "").Replace("\u200B", "").Replace("\u200C", "").Replace("\u200D", "").Replace("\uFEFF", "").Trim();
            if (t.Length == 0 || Matches(t, app, exe)) return null;

            int cut = -1, len = 0;
            foreach (string sep in Seps)
            {
                int i = t.LastIndexOf(sep, StringComparison.Ordinal);
                if (i > cut) { cut = i; len = sep.Length; }
            }
            if (cut < 0) return t;

            if (!Matches(t.Substring(cut + len).Trim(), app, exe)) return t;
            string rest = t.Substring(0, cut).Trim();
            return rest.Length == 0 ? null : rest;
        }

        public static string SiteFromHtml(string html)
        {
            if (string.IsNullOrEmpty(html)) return null;
            int end = html.IndexOf('<');
            string header = end < 0 ? html : html.Substring(0, end);
            foreach (string line in header.Split('\n'))
            {
                string l = line.Trim();
                if (l.StartsWith("SourceURL:", StringComparison.OrdinalIgnoreCase)) return Host(l.Substring(10).Trim());
            }
            return null;
        }

        public static string Text(Source s, int detail, bool secret)
        {
            if (secret && detail > 1) detail = 1;
            if (detail == 0 && s.Icon == null) detail = 1;
            if (detail == 0) return "";

            var parts = new List<string>();
            Add(parts, s.App);
            if (detail >= 2) Add(parts, s.Site);
            if (detail >= 3 && !string.Equals(s.Title, s.Site, StringComparison.OrdinalIgnoreCase)) Add(parts, s.Title);
            return string.Join(" · ", parts.ToArray());
        }

        static void Add(List<string> parts, string s)
        {
            if (!string.IsNullOrEmpty(s)) parts.Add(s);
        }

        static bool Matches(string seg, string app, string exe)
        {
            if (string.IsNullOrEmpty(seg)) return false;
            if (!string.IsNullOrEmpty(app) && (string.Equals(seg, app, StringComparison.OrdinalIgnoreCase)
                || app.EndsWith(" " + seg, StringComparison.OrdinalIgnoreCase)
                || app.StartsWith(seg + " ", StringComparison.OrdinalIgnoreCase))) return true;
            return !string.IsNullOrEmpty(exe) && string.Equals(seg, exe, StringComparison.OrdinalIgnoreCase);
        }

        static string Host(string url)
        {
            Uri u;
            if (!Uri.TryCreate(url, UriKind.Absolute, out u) || (u.Scheme != "http" && u.Scheme != "https")) return null;
            return u.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? u.Host.Substring(4) : u.Host;
        }

        static uint PidOf(IntPtr hwnd)
        {
            uint pid;
            Native.GetWindowThreadProcessId(hwnd, out pid);
            return pid;
        }

        static string Window(IntPtr hwnd)
        {
            int len = Native.GetWindowTextLength(hwnd);
            if (len <= 0) return null;
            var sb = new StringBuilder(len + 1);
            Native.GetWindowText(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }

        static string MainTitle(uint pid)
        {
            try { using (var p = Process.GetProcessById((int)pid)) return p.MainWindowTitle; }
            catch (Exception) { return null; }
        }

        static AppInfo Info(uint pid)
        {
            string path = ExePath(pid);
            if (path == null)
            {
                try { using (var p = Process.GetProcessById((int)pid)) return new AppInfo { Name = p.ProcessName, Exe = p.ProcessName }; }
                catch (Exception) { return null; }
            }

            AppInfo info;
            if (apps.TryGetValue(path, out info)) return info;
            string exe = Path.GetFileNameWithoutExtension(path);
            info = new AppInfo { Exe = exe, Name = exe };
            try
            {
                string desc = FileVersionInfo.GetVersionInfo(path).FileDescription;
                if (!string.IsNullOrEmpty(desc) && desc.Trim().Length > 0) info.Name = desc.Trim();
            }
            catch (Exception) { }
            try { using (Icon ic = Icon.ExtractAssociatedIcon(path)) info.Icon = ic.ToBitmap(); }
            catch (Exception) { }
            apps[path] = info;
            return info;
        }

        static string ExePath(uint pid)
        {
            IntPtr h = Native.OpenProcess(0x1000, false, pid);
            if (h == IntPtr.Zero) return null;
            try
            {
                var sb = new StringBuilder(1024);
                uint size = (uint)sb.Capacity;
                return Native.QueryFullProcessImageName(h, 0, sb, ref size) ? sb.ToString() : null;
            }
            finally { Native.CloseHandle(h); }
        }
    }
}
