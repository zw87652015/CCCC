using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Media;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Cccc
{
    sealed class Settings
    {
        internal static string Key = @"Software\CCCC";
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public bool Paused, Sound, FailHint = true, CapsHint = true, AutoUpdate = true, ShowSource = true, Simple;
        public bool FullPalette, StayUntilClicked, CapsStayUntilClicked;
        public int SourceWhen = 1, SourceDetail = 1;
        public int ThemeMode, Palette, DurationMs = 1900, Position = 6, Scale = 100, TextScale = 100;
        public int FreeX = 500, FreeY = 900, Transparency;
        public int CapsPosition = -1, CapsFreeX = 500, CapsFreeY = 900, CapsWidth = 200, CapsHeight = 40, CapsDurationMs = 2600;
        public string Lang = "auto";
        public string PauseUntilUtc;
        public string OkColor, WarnColor, BgColor, BorderColor, TitleColor, SubColor, MetaColor;
        public string FontFamily, SoundOkFile, SoundFailFile;
        public List<string> IgnoreApps = new List<string>();

        public bool Ignores(string exe)
        {
            if (string.IsNullOrEmpty(exe)) return false;
            foreach (string e in IgnoreApps)
                if (string.Equals(e, exe, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static Settings Load()
        {
            var s = new Settings();
            using (var k = Registry.CurrentUser.OpenSubKey(Key))
            {
                if (k == null) return s;
                s.Paused = Flag(k, "Paused", s.Paused);
                object until = k.GetValue("PauseUntilUtc");
                s.PauseUntilUtc = until == null ? null : until as string ?? "invalid";
                s.Sound = Flag(k, "Sound", s.Sound);
                s.FailHint = Flag(k, "FailHint", s.FailHint);
                s.CapsHint = Flag(k, "CapsHint", s.CapsHint);
                s.AutoUpdate = Flag(k, "AutoUpdate", s.AutoUpdate);
                s.ShowSource = Flag(k, "ShowSource", s.ShowSource);
                s.SourceWhen = Level(k, "SourceWhen", s.SourceWhen, 1);
                s.SourceDetail = Level(k, "SourceDetail", s.SourceDetail, 3);
                s.Lang = Choice(k, "Lang", s.Lang);
                s.FullPalette = Flag(k, "FullPalette", s.FullPalette);
                s.StayUntilClicked = Flag(k, "StayUntilClicked", s.StayUntilClicked);
                s.CapsStayUntilClicked = Flag(k, "CapsStayUntilClicked", s.CapsStayUntilClicked);
                s.ThemeMode = Num(k, "ThemeMode", s.ThemeMode, 0, 2);
                s.Palette = Num(k, "Palette", s.Palette, 0, 4);
                s.DurationMs = Num(k, "DurationMs", s.DurationMs, 500, 10000);
                s.CapsDurationMs = Num(k, "CapsDurationMs", s.CapsDurationMs, 500, 10000);
                s.Position = Num(k, "Position", s.Position, 0, 10);
                s.FreeX = Num(k, "FreeX", s.FreeX, 0, 1000);
                s.FreeY = Num(k, "FreeY", s.FreeY, 0, 1000);
                s.Transparency = Num(k, "Transparency", s.Transparency, 0, 75);
                s.CapsPosition = Num(k, "CapsPosition", s.CapsPosition, -1, 10);
                s.CapsFreeX = Num(k, "CapsFreeX", s.CapsFreeX, 0, 1000);
                s.CapsFreeY = Num(k, "CapsFreeY", s.CapsFreeY, 0, 1000);
                s.CapsWidth = Num(k, "CapsWidth", s.CapsWidth, 160, 360);
                s.CapsHeight = Num(k, "CapsHeight", s.CapsHeight, 32, 64);
                s.Simple = Flag(k, "Simple", s.Simple);
                s.Scale = Num(k, "Scale", s.Scale, 75, 200);
                s.TextScale = Num(k, "TextScale", s.TextScale, 80, 160);
                s.OkColor = HexColor(k, "OkColor");
                s.WarnColor = HexColor(k, "WarnColor");
                s.BgColor = HexColor(k, "BgColor");
                s.BorderColor = HexColor(k, "BorderColor");
                s.TitleColor = HexColor(k, "TitleColor");
                s.SubColor = HexColor(k, "SubColor");
                s.MetaColor = HexColor(k, "MetaColor");
                s.FontFamily = Str(k, "FontFamily");
                s.SoundOkFile = Str(k, "SoundOkFile");
                s.SoundFailFile = Str(k, "SoundFailFile");
                string[] ignored = k.GetValue("IgnoreApps") as string[];
                if (ignored != null)
                    foreach (string e in ignored)
                    {
                        string t = (e ?? "").Trim();
                        if (t.Length > 0) s.IgnoreApps.Add(t);
                    }
            }
            return s;
        }

        public void Save()
        {
            using (var k = Registry.CurrentUser.CreateSubKey(Key))
            {
                k.SetValue("Paused", Paused ? 1 : 0, RegistryValueKind.DWord);
                SetStr(k, "PauseUntilUtc", PauseUntilUtc);
                k.SetValue("Sound", Sound ? 1 : 0, RegistryValueKind.DWord);
                k.SetValue("FailHint", FailHint ? 1 : 0, RegistryValueKind.DWord);
                k.SetValue("CapsHint", CapsHint ? 1 : 0, RegistryValueKind.DWord);
                k.SetValue("AutoUpdate", AutoUpdate ? 1 : 0, RegistryValueKind.DWord);
                k.SetValue("ShowSource", ShowSource ? 1 : 0, RegistryValueKind.DWord);
                k.SetValue("SourceWhen", SourceWhen, RegistryValueKind.DWord);
                k.SetValue("SourceDetail", SourceDetail, RegistryValueKind.DWord);
                k.SetValue("Lang", Lang, RegistryValueKind.String);
                k.SetValue("FullPalette", FullPalette ? 1 : 0, RegistryValueKind.DWord);
                k.SetValue("StayUntilClicked", StayUntilClicked ? 1 : 0, RegistryValueKind.DWord);
                k.SetValue("CapsStayUntilClicked", CapsStayUntilClicked ? 1 : 0, RegistryValueKind.DWord);
                k.SetValue("ThemeMode", ThemeMode, RegistryValueKind.DWord);
                k.SetValue("Palette", Palette, RegistryValueKind.DWord);
                k.SetValue("DurationMs", DurationMs, RegistryValueKind.DWord);
                k.SetValue("CapsDurationMs", CapsDurationMs, RegistryValueKind.DWord);
                k.SetValue("Position", Position, RegistryValueKind.DWord);
                k.SetValue("FreeX", FreeX, RegistryValueKind.DWord);
                k.SetValue("FreeY", FreeY, RegistryValueKind.DWord);
                k.SetValue("Transparency", Transparency, RegistryValueKind.DWord);
                k.SetValue("CapsPosition", CapsPosition, RegistryValueKind.DWord);
                k.SetValue("CapsFreeX", CapsFreeX, RegistryValueKind.DWord);
                k.SetValue("CapsFreeY", CapsFreeY, RegistryValueKind.DWord);
                k.SetValue("CapsWidth", CapsWidth, RegistryValueKind.DWord);
                k.SetValue("CapsHeight", CapsHeight, RegistryValueKind.DWord);
                k.SetValue("Simple", Simple ? 1 : 0, RegistryValueKind.DWord);
                k.SetValue("Scale", Scale, RegistryValueKind.DWord);
                k.SetValue("TextScale", TextScale, RegistryValueKind.DWord);
                SetStr(k, "OkColor", OkColor);
                SetStr(k, "WarnColor", WarnColor);
                SetStr(k, "BgColor", BgColor);
                SetStr(k, "BorderColor", BorderColor);
                SetStr(k, "TitleColor", TitleColor);
                SetStr(k, "SubColor", SubColor);
                SetStr(k, "MetaColor", MetaColor);
                SetStr(k, "FontFamily", FontFamily);
                SetStr(k, "SoundOkFile", SoundOkFile);
                SetStr(k, "SoundFailFile", SoundFailFile);
                if (IgnoreApps.Count == 0) k.DeleteValue("IgnoreApps", false);
                else k.SetValue("IgnoreApps", IgnoreApps.ToArray(), RegistryValueKind.MultiString);
            }
        }

        public Settings Clone()
        {
            var clone = (Settings)MemberwiseClone();
            clone.IgnoreApps = new List<string>(IgnoreApps);
            return clone;
        }

        public void ResetDisplay()
        {
            ThemeMode = Palette = 0;
            FullPalette = StayUntilClicked = CapsStayUntilClicked = Simple = false;
            Transparency = 0;
            DurationMs = 1900; Position = 6; Scale = 100; TextScale = 100;
            CapsDurationMs = 2600;
            FreeX = 500; FreeY = 900;
            CapsPosition = -1; CapsFreeX = 500; CapsFreeY = 900; CapsWidth = 200; CapsHeight = 40;
            OkColor = WarnColor = BgColor = BorderColor = TitleColor = SubColor = MetaColor = null;
            FontFamily = SoundOkFile = SoundFailFile = null;
            ShowSource = true; SourceWhen = 1; SourceDetail = 1;
        }

        static bool Flag(RegistryKey k, string name, bool fallback)
        {
            object v = k.GetValue(name);
            return v is int ? (int)v != 0 : fallback;
        }

        static int Level(RegistryKey k, string name, int fallback, int max)
        {
            return Num(k, name, fallback, 0, max);
        }

        static int Num(RegistryKey k, string name, int fallback, int min, int max)
        {
            object v = k.GetValue(name);
            return v is int ? Math.Min(max, Math.Max(min, (int)v)) : fallback;
        }

        static string Str(RegistryKey k, string name)
        {
            object v = k.GetValue(name);
            return v is string && ((string)v).Length > 0 ? (string)v : null;
        }

        static string HexColor(RegistryKey k, string name)
        {
            string v = Str(k, name);
            Color c;
            return Theme.TryColor(v, out c) ? v : null;
        }

        static void SetStr(RegistryKey k, string name, string v)
        {
            if (string.IsNullOrEmpty(v)) k.DeleteValue(name, false);
            else k.SetValue(name, v, RegistryValueKind.String);
        }

        static string Choice(RegistryKey k, string name, string fallback)
        {
            object v = k.GetValue(name);
            if (!(v is string)) return fallback;
            string s = ((string)v).ToLowerInvariant();
            return s == "en" || s == "zh" ? s : fallback;
        }

        public static bool AutoStart
        {
            get
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                    return k != null && k.GetValue("CCCC") is string;
            }
            set
            {
                using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (value) k.SetValue("CCCC", "\"" + Application.ExecutablePath + "\" --silent");
                    else k.DeleteValue("CCCC", false);
                }
            }
        }
    }

    // Synthesized cues written to %TEMP%\CCCC once, plus optional custom files. Every play opens a fresh
    // MCI alias so cues overlap and run to the end; a timer reaps aliases once they report "stopped".
    // All methods must be called on the UI thread.
    sealed class Sounds
    {
        const int MaxTracks = 32;

        readonly string okWav, noWav;
        readonly List<string> active = new List<string>();
        readonly Timer reap = new Timer { Interval = 250 };
        string customOk, customNo;
        int counter;

        public Sounds()
        {
            string dir = Path.Combine(Path.GetTempPath(), "CCCC");
            try { Directory.CreateDirectory(dir); } catch (Exception) { }
            okWav = Write(Path.Combine(dir, "ok.wav"), Make(new[] { 1318.5, 0.0, 0.06, 1760.0, 0.045, 0.11 }, 0.16, 36));
            noWav = Write(Path.Combine(dir, "no.wav"), Make(new[] { 392.0, 0.0, 0.08, 311.1, 0.07, 0.12 }, 0.2, 22));
            reap.Tick += delegate { Reap(); };
        }

        public bool Playing { get { return active.Count > 0; } }

        // The harness reads this to verify overlap.
        public int ActiveCount { get { return active.Count; } }

        public void Load(string okFile, string failFile)
        {
            customOk = Existing(okFile);
            customNo = Existing(failFile);
        }

        public void Ok() { Start(customOk, okWav); }
        public void No() { Start(customNo, noWav); }

        public void StopAll()
        {
            foreach (string a in active) Mci("close " + a);
            active.Clear();
            reap.Stop();
        }

        static string Existing(string path) { return !string.IsNullOrEmpty(path) && File.Exists(path) ? path : null; }

        void Start(string custom, string builtin)
        {
            string alias = "cccc_s" + (++counter);
            bool open = custom != null && Mci("open \"" + custom + "\" type mpegvideo alias " + alias) == 0;
            if (!open && builtin != null) open = Mci("open \"" + builtin + "\" type mpegvideo alias " + alias) == 0;
            if (!open) return;
            if (Mci("play " + alias) != 0) { Mci("close " + alias); return; }
            active.Add(alias);
            if (active.Count > MaxTracks)
            {
                Mci("close " + active[0]);
                active.RemoveAt(0);
            }
            else reap.Start();
        }

        void Reap()
        {
            var sb = new StringBuilder(32);
            for (int i = active.Count - 1; i >= 0; i--)
            {
                string a = active[i];
                sb.Length = 0;
                if (Native.mciSendString("status " + a + " mode", sb, sb.Capacity, IntPtr.Zero) != 0
                    || sb.ToString() == "stopped")
                {
                    active.RemoveAt(i);
                    Mci("close " + a);
                }
            }
            if (active.Count == 0) reap.Stop();
        }

        static int Mci(string command) { return Native.mciSendString(command, null, 0, IntPtr.Zero); }

        static string Write(string path, byte[] data)
        {
            try { File.WriteAllBytes(path, data); return path; }
            catch (Exception) { return null; }
        }

        // notes: triples of (frequency, start, duration) in seconds.
        static byte[] Make(double[] notes, double amp, double decay)
        {
            const int rate = 44100;
            double end = 0;
            for (int i = 0; i < notes.Length; i += 3) end = Math.Max(end, notes[i + 1] + notes[i + 2]);
            int n = (int)(rate * (end + 0.01));
            var mix = new double[n];
            for (int i = 0; i < notes.Length; i += 3)
            {
                double f = notes[i], start = notes[i + 1], dur = notes[i + 2];
                int s0 = (int)(start * rate), s1 = Math.Min(n, (int)((start + dur) * rate));
                for (int j = s0; j < s1; j++)
                {
                    double t = (j - s0) / (double)rate;
                    double env = Math.Min(1, t / 0.003) * Math.Exp(-t * decay) * Math.Min(1, (dur - t) / 0.01);
                    mix[j] += amp * env * (Math.Sin(2 * Math.PI * f * t) + 0.18 * Math.Sin(4 * Math.PI * f * t));
                }
            }

            var ms = new MemoryStream();
            var w = new BinaryWriter(ms);
            w.Write("RIFF".ToCharArray()); w.Write(36 + n * 2); w.Write("WAVE".ToCharArray());
            w.Write("fmt ".ToCharArray()); w.Write(16); w.Write((short)1); w.Write((short)1);
            w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
            w.Write("data".ToCharArray()); w.Write(n * 2);
            foreach (double v in mix) w.Write((short)(Math.Max(-1, Math.Min(1, v)) * 32767));
            w.Flush();
            return ms.ToArray();
        }
    }
}
