using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace Cccc
{
    sealed class Release
    {
        public string Tag, Name, HtmlUrl, AssetUrl, Sha256;
        public long AssetSize;
        public Version Version;
    }

    static class Updater
    {
        const string Api = "https://api.github.com/repos/zw87652015/CCCC/releases/latest";
        const string Download = "https://downloads.luojiajiang.uk/CCCC.exe";

        static Updater() { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; }

        public static Version Current { get { return Assembly.GetExecutingAssembly().GetName().Version; } }

        public static Version ParseTag(string tag)
        {
            if (tag == null) return null;
            Version v;
            return Version.TryParse(tag.Trim().TrimStart('v', 'V'), out v) ? v : null;
        }

        public static bool Newer(Version remote, Version current)
        {
            return Pad(remote).CompareTo(Pad(current)) > 0;
        }

        static Version Pad(Version v)
        {
            return new Version(v.Major, v.Minor, Math.Max(v.Build, 0), Math.Max(v.Revision, 0));
        }

        public static string Show(Version v)
        {
            string s = "v" + v.Major + "." + v.Minor;
            if (v.Build > 0) s += "." + v.Build;
            if (v.Revision > 0) s += "." + v.Revision;
            return s;
        }

        public static Release Fetch()
        {
            var d = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(Get(Api, "application/vnd.github+json"));
            var r = new Release { Tag = Str(d, "tag_name"), Name = Str(d, "name"), HtmlUrl = Str(d, "html_url") };
            r.Version = ParseTag(r.Tag);
            if (r.Version == null) throw new InvalidOperationException("Unrecognised release tag: " + r.Tag);

            Dictionary<string, object> pick = null, fallback = null;
            object[] assets = d.ContainsKey("assets") ? d["assets"] as object[] : null;
            if (assets != null)
            {
                foreach (object o in assets)
                {
                    var a = o as Dictionary<string, object>;
                    if (a == null) continue;
                    string n = Str(a, "name") ?? "";
                    if (string.Equals(n, "CCCC.exe", StringComparison.OrdinalIgnoreCase)) { pick = a; break; }
                    if (fallback == null && n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) fallback = a;
                }
            }
            if (pick == null) pick = fallback;
            r.AssetUrl = Download + "?v=" + Uri.EscapeDataString(r.Tag);
            if (pick != null)
            {
                r.AssetSize = Num(pick, "size");
                string digest = Str(pick, "digest");
                if (digest != null && digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                    r.Sha256 = digest.Substring(7).ToLowerInvariant();
            }
            return r;
        }

        public static void Install(Release r, string exePath)
        {
            if (r.AssetUrl == null) throw new InvalidOperationException("No downloadable asset");
            string tmp = exePath + ".new", old = exePath + ".old";
            try
            {
                using (var resp = Request(r.AssetUrl, null).GetResponse())
                using (var src = resp.GetResponseStream())
                using (var dst = File.Create(tmp))
                    src.CopyTo(dst);
                Verify(tmp, r);
                if (File.Exists(old)) File.Delete(old);
                File.Move(exePath, old);
                try { File.Move(tmp, exePath); }
                catch { File.Move(old, exePath); throw; }
            }
            catch
            {
                TryDelete(tmp);
                throw;
            }
        }

        public static void Cleanup(string exePath)
        {
            TryDelete(exePath + ".old");
            TryDelete(exePath + ".new");
        }

        public static void WaitForExit(int pid)
        {
            try { Process.GetProcessById(pid).WaitForExit(10000); }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }
        }

        static void Verify(string path, Release r)
        {
            if (r.AssetSize > 0 && new FileInfo(path).Length != r.AssetSize) throw new IOException("Download size mismatch");
            using (var fs = File.OpenRead(path))
                if (fs.ReadByte() != 'M' || fs.ReadByte() != 'Z') throw new IOException("Download is not a Windows executable");
            if (r.Sha256 == null) return;
            string hash;
            using (var fs = File.OpenRead(path))
            using (var sha = SHA256.Create())
                hash = BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
            if (hash != r.Sha256) throw new IOException("SHA-256 mismatch");
        }

        static HttpWebRequest Request(string url, string accept)
        {
            var q = (HttpWebRequest)WebRequest.Create(url);
            q.UserAgent = "CCCC/" + Current;
            if (accept != null) q.Accept = accept;
            q.Timeout = q.ReadWriteTimeout = 15000;
            return q;
        }

        static string Get(string url, string accept)
        {
            using (var resp = Request(url, accept).GetResponse())
            using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                return sr.ReadToEnd();
        }

        static string Str(Dictionary<string, object> d, string key)
        {
            object v;
            return d.TryGetValue(key, out v) ? v as string : null;
        }

        static long Num(Dictionary<string, object> d, string key)
        {
            object v;
            if (!d.TryGetValue(key, out v)) return 0;
            return v is long ? (long)v : v is int ? (int)v : 0;
        }

        static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
