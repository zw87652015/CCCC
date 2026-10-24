using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;

namespace Cccc
{
    sealed class ConfigPreview
    {
        public Settings Candidate;
        public readonly List<string> Warnings = new List<string>();
    }

    // Only named preferences are portable. Runtime state, startup and permissions never enter this codec.
    static class ConfigFile
    {
        const int MaxBytes = 1024 * 1024;
        static readonly string[] Bools = { "Sound", "FailHint", "CapsHint", "AutoUpdate", "ShowSource", "FullPalette", "StayUntilClicked", "CapsStayUntilClicked", "Simple" };
        static readonly string[] Texts = { "OkColor", "WarnColor", "BgColor", "BorderColor", "TitleColor", "SubColor", "MetaColor", "FontFamily", "SoundOkFile", "SoundFailFile", "Lang" };
        static readonly string[] Numbers = { "SourceWhen", "SourceDetail", "ThemeMode", "Palette", "DurationMs", "Position", "Scale", "TextScale", "FreeX", "FreeY", "Transparency", "CapsPosition", "CapsFreeX", "CapsFreeY", "CapsWidth", "CapsHeight", "CapsDurationMs" };
        static readonly int[] Min = { 0, 0, 0, 0, 500, 0, 75, 80, 0, 0, 0, -1, 0, 0, 160, 32, 500 };
        static readonly int[] Max = { 1, 3, 2, 4, 10000, 10, 200, 160, 1000, 1000, 75, 10, 1000, 1000, 360, 64, 10000 };

        static JavaScriptSerializer Serializer() { return new JavaScriptSerializer { MaxJsonLength = MaxBytes, RecursionLimit = 16 }; }
        static FieldInfo Field(string name) { return typeof(Settings).GetField(name); }
        static object Get(Settings s, string name) { return Field(name).GetValue(s); }
        static void Set(Settings s, string name, object value) { Field(name).SetValue(s, value); }
        static IEnumerable<string> Fields()
        {
            foreach (string key in Bools) yield return key;
            foreach (string key in Numbers) yield return key;
            foreach (string key in Texts) yield return key;
        }

        public static string Export(Settings settings)
        {
            var values = new Dictionary<string, object>();
            foreach (string name in Fields()) values[name] = Get(settings, name);
            values["IgnoreApps"] = settings.IgnoreApps.ToArray();
            return Serializer().Serialize(new Dictionary<string, object>
            {
                { "format", "cccc-settings" }, { "version", 1 }, { "platform", "windows" }, { "settings", values }
            });
        }

        public static ConfigPreview Parse(string json, Settings current)
        {
            if (json == null || Encoding.UTF8.GetByteCount(json) > MaxBytes) throw new FormatException("Configuration exceeds 1 MiB.");
            var doc = Serializer().DeserializeObject(json) as Dictionary<string, object>;
            if (doc == null || !doc.ContainsKey("format") || !object.Equals(doc["format"], "cccc-settings") ||
                !doc.ContainsKey("version") || !(doc["version"] is int) || (int)doc["version"] != 1 ||
                !doc.ContainsKey("platform") || !object.Equals(doc["platform"], "windows"))
                throw new FormatException("Expected a version 1 Windows CCCC settings file.");
            object raw;
            if (!doc.TryGetValue("settings", out raw) || !(raw is Dictionary<string, object>)) throw new FormatException("Missing settings object.");
            var values = (Dictionary<string, object>)raw;
            var preview = new ConfigPreview { Candidate = current.Clone() };
            Settings result = preview.Candidate;
            foreach (string key in Bools)
                if (values.TryGetValue(key, out raw))
                {
                    if (!(raw is bool)) throw Invalid(key);
                    Set(result, key, raw);
                }
            for (int i = 0; i < Numbers.Length; i++)
                if (values.TryGetValue(Numbers[i], out raw))
                {
                    if (!(raw is int) || (int)raw < Min[i] || (int)raw > Max[i]) throw Invalid(Numbers[i]);
                    Set(result, Numbers[i], raw);
                }
            foreach (string key in Texts)
                if (values.TryGetValue(key, out raw))
                {
                    if (raw != null && (!(raw is string) || ((string)raw).Length > 4096)) throw Invalid(key);
                    string value = raw as string;
                    if (key == "Lang")
                    {
                        if (value != "auto" && value != "en" && value != "zh") throw Invalid(key);
                    }
                    else if (key.EndsWith("Color") && value != null)
                    {
                        Color color;
                        if (!Theme.TryColor(value, out color)) throw Invalid(key);
                    }
                    Set(result, key, value);
                }
            if (values.TryGetValue("IgnoreApps", out raw))
            {
                var array = raw as object[];
                if (array == null || array.Length > 256) throw Invalid("IgnoreApps");
                result.IgnoreApps.Clear();
                foreach (object entry in array)
                {
                    string app = entry as string;
                    if (string.IsNullOrWhiteSpace(app) || app.Length > 512) throw Invalid("IgnoreApps");
                    if (!result.Ignores(app.Trim())) result.IgnoreApps.Add(app.Trim());
                }
            }
            foreach (string key in new[] { "SoundOkFile", "SoundFailFile" })
            {
                string path = Get(result, key) as string;
                if (values.ContainsKey(key) && path != null && !File.Exists(path)) { Set(result, key, null); preview.Warnings.Add(key); }
            }
            if (values.ContainsKey("FontFamily") && result.FontFamily != null)
            {
                bool found = false;
                using (var fonts = new InstalledFontCollection())
                    foreach (var font in fonts.Families) if (string.Equals(font.Name, result.FontFamily, StringComparison.OrdinalIgnoreCase)) { found = true; break; }
                if (!found) { result.FontFamily = null; preview.Warnings.Add("FontFamily"); }
            }
            return preview;
        }

        static FormatException Invalid(string key) { return new FormatException("Invalid setting: " + key); }

        public static ConfigPreview Read(string path, Settings current)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var bytes = new MemoryStream())
            {
                var buffer = new byte[8192];
                int n;
                while ((n = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (bytes.Length + n > MaxBytes) throw new FormatException("Configuration exceeds 1 MiB.");
                    bytes.Write(buffer, 0, n);
                }
                string text = new UTF8Encoding(false, true).GetString(bytes.ToArray()).TrimStart('\uFEFF');
                return Parse(text, current);
            }
        }

        public static void Write(string path, Settings settings)
        {
            string target = Path.GetFullPath(path);
            string dir = Path.GetDirectoryName(target);
            Directory.CreateDirectory(dir);
            string temporary = Path.Combine(dir, ".cccc-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(Export(settings));
                if (bytes.Length > MaxBytes) throw new FormatException("Configuration exceeds 1 MiB.");
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                if (File.Exists(target)) File.Replace(temporary, target, null);
                else File.Move(temporary, target);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        static void Copy(Settings source, Settings target)
        {
            foreach (string key in Fields()) Set(target, key, Get(source, key));
            target.IgnoreApps = new List<string>(source.IgnoreApps);
        }

        public static void Import(ConfigPreview preview, Settings current, string backupPath, Action save)
        {
            Settings previous = current.Clone();
            Write(backupPath, previous);
            try { Copy(preview.Candidate, current); save(); }
            catch (Exception original)
            {
                Copy(previous, current);
                try { save(); }
                catch (Exception rollback) { throw new AggregateException("Import and rollback both failed. Restore settings-before-import.json.", original, rollback); }
                throw;
            }
        }
    }
}
