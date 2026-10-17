using System;
using System.IO;
using System.Media;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Cccc
{
    sealed class Settings
    {
        const string Key = @"Software\CCCC";
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public bool Paused, Sound, FailHint = true;
        public string Lang = "auto";

        public static Settings Load()
        {
            var s = new Settings();
            using (var k = Registry.CurrentUser.OpenSubKey(Key))
            {
                if (k == null) return s;
                s.Paused = Flag(k, "Paused", s.Paused);
                s.Sound = Flag(k, "Sound", s.Sound);
                s.FailHint = Flag(k, "FailHint", s.FailHint);
                s.Lang = Choice(k, "Lang", s.Lang);
            }
            return s;
        }

        public void Save()
        {
            using (var k = Registry.CurrentUser.CreateSubKey(Key))
            {
                k.SetValue("Paused", Paused ? 1 : 0, RegistryValueKind.DWord);
                k.SetValue("Sound", Sound ? 1 : 0, RegistryValueKind.DWord);
                k.SetValue("FailHint", FailHint ? 1 : 0, RegistryValueKind.DWord);
                k.SetValue("Lang", Lang, RegistryValueKind.String);
            }
        }

        static bool Flag(RegistryKey k, string name, bool fallback)
        {
            object v = k.GetValue(name);
            return v is int ? (int)v != 0 : fallback;
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

    // Tiny synthesized cues: a rising two-note blip for success, a soft falling one for failure.
    sealed class Sounds
    {
        readonly SoundPlayer ok = Make(new[] { 1318.5, 0.0, 0.06, 1760.0, 0.045, 0.11 }, 0.16, 36);
        readonly SoundPlayer no = Make(new[] { 392.0, 0.0, 0.08, 311.1, 0.07, 0.12 }, 0.2, 22);

        public void Ok() { Play(ok); }
        public void No() { Play(no); }

        static void Play(SoundPlayer p) { try { p.Play(); } catch (Exception) { } }

        // notes: triples of (frequency, start, duration) in seconds.
        static SoundPlayer Make(double[] notes, double amp, double decay)
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
            ms.Position = 0;
            var p = new SoundPlayer(ms);
            p.Load();
            return p;
        }
    }
}
