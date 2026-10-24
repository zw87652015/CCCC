using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace Cccc
{
    static class SettingsFeaturesTests
    {
        static int checks;
        static Type pauseType, configType;
        const BindingFlags Public = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
        static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
        static void Check(bool value, string description) { if (!value) throw new Exception("FAIL: " + description); checks++; }
        static object Call(object target, string method, params object[] args)
        {
            Type type = target as Type ?? target.GetType();
            try { return type.GetMethod(method, Public).Invoke(target is Type ? null : target, args); }
            catch (TargetInvocationException e) { throw e.InnerException; }
        }
        static void Reject(Action action, string description)
        {
            bool failed = false;
            try { action(); } catch (Exception) { failed = true; }
            Check(failed, description);
        }
        static void Deadline(Settings s, string value) { typeof(Settings).GetField("PauseUntilUtc").SetValue(s, value); }
        static string Deadline(Settings s) { return (string)typeof(Settings).GetField("PauseUntilUtc").GetValue(s); }
        static string Export(Settings s) { return (string)Call(configType, "Export", s); }
        static object Parse(string json, Settings current) { return Call(configType, "Parse", json, current); }
        static Settings Candidate(object preview) { return (Settings)preview.GetType().GetField("Candidate").GetValue(preview); }
        static Dictionary<string, object> Document(string json) { return Json.Deserialize<Dictionary<string, object>>(json); }
        static Dictionary<string, object> Values(Dictionary<string, object> doc) { return (Dictionary<string, object>)doc["settings"]; }

        [STAThread] static void Main(string[] args)
        {
            Settings.Key = @"Software\CCCC-tests-" + Guid.NewGuid().ToString("N");
            try
            {
                pauseType = typeof(Settings).Assembly.GetType("Cccc.PauseControl");
                configType = typeof(Settings).Assembly.GetType("Cccc.ConfigFile");
                Check(pauseType != null, "timed pause controller exists");
                Check(configType != null, "configuration file codec exists");
                PauseTests();
                ConfigTests(args[0]);
                Check(typeof(Card).GetMethod("Dismiss") != null, "pause can dismiss already-visible cards");
                Check(typeof(App).GetField("pauseTimer", BindingFlags.NonPublic | BindingFlags.Instance) != null, "application schedules timed resume");
                WindowTests(args[0]);
                Console.WriteLine("PASS: " + checks + " settings and timed-pause checks.");
            }
            catch (Exception e) { Console.WriteLine(e.Message); Environment.ExitCode = 1; }
            finally { Registry.CurrentUser.DeleteSubKeyTree(Settings.Key, false); }
        }

        static void PauseTests()
        {
            var now = new DateTime(2026, 10, 24, 8, 0, 0, DateTimeKind.Utc);
            var settings = new Settings();
            var pause = Activator.CreateInstance(pauseType, new object[] { settings, new Func<DateTime>(() => now) });
            Call(pause, "Start", 15);
            Check(settings.Paused && Deadline(settings) != null, "timed pause starts");
            Check(Settings.Load().Paused && Deadline(Settings.Load()) == Deadline(settings), "deadline persists");
            now = now.AddMinutes(14); Call(pause, "Check");
            Check(settings.Paused, "pause stays until deadline");
            now = now.AddMinutes(1); Call(pause, "Check");
            Check(!settings.Paused && string.IsNullOrEmpty(Deadline(settings)), "resume exactly at deadline");
            Call(pause, "Start", 60); now = now.AddMinutes(5); Call(pause, "Start", 15);
            now = now.AddMinutes(16); Call(pause, "Check"); Check(!settings.Paused, "rescheduling uses new deadline");
            Call(pause, "Start", 15); Call(pause, "SetManual", true);
            now = now.AddHours(2); Call(pause, "Check"); Check(settings.Paused && string.IsNullOrEmpty(Deadline(settings)), "manual pause cancels auto-resume");
            Call(pause, "SetManual", false); Check(!settings.Paused, "manual resume");
            Call(pause, "Start", 30); var restored = Settings.Load();
            Activator.CreateInstance(pauseType, new object[] { restored, new Func<DateTime>(() => now) });
            Check(restored.Paused && restored.PauseUntilUtc == settings.PauseUntilUtc, "restart before deadline preserves original deadline");
            now = now.AddMinutes(31);
            Activator.CreateInstance(pauseType, new object[] { restored, new Func<DateTime>(() => now) });
            Check(!restored.Paused, "restart after sleep/deadline reconciles");
            settings.Paused = true; Deadline(settings, "broken");
            Call(pause, "Check"); Check(!settings.Paused, "corrupt deadline resumes instead of pausing forever");
            using (var key = Registry.CurrentUser.CreateSubKey(Settings.Key))
            {
                key.SetValue("Paused", 1); key.SetValue("PauseUntilUtc", "");
            }
            var emptyDeadline = Settings.Load();
            Activator.CreateInstance(pauseType, new object[] { emptyDeadline, new Func<DateTime>(() => now) });
            Check(!emptyDeadline.Paused && emptyDeadline.PauseUntilUtc == null, "empty persisted deadline recovers on startup");
            using (var key = Registry.CurrentUser.CreateSubKey(Settings.Key))
            { key.SetValue("Paused", 1); key.SetValue("PauseUntilUtc", 123); }
            var wrongType = Settings.Load();
            Activator.CreateInstance(pauseType, new object[] { wrongType, new Func<DateTime>(() => now) });
            Check(!wrongType.Paused, "wrong-type persisted deadline recovers");
            Call(pause, "SetManual", true);
            var manualRestart = Settings.Load();
            Activator.CreateInstance(pauseType, new object[] { manualRestart, new Func<DateTime>(() => now.AddDays(1)) });
            Check(manualRestart.Paused && manualRestart.PauseUntilUtc == null, "legacy manual pause survives restart");
            Reject(() => Call(pause, "Start", 0), "invalid pause duration rejected");
        }

        static void ConfigTests(string dir)
        {
            var settings = new Settings { ThemeMode = 2, Transparency = 45, Simple = true, Sound = true, Lang = "zh", Paused = true, DurationMs = 3500 };
            settings.IgnoreApps.Add("example.exe"); Deadline(settings, "2026-10-24T10:00:00.0000000Z");
            string json = Export(settings);
            var doc = Document(json); var values = Values(doc);
            Check((string)doc["platform"] == "windows" && (int)doc["version"] == 1, "versioned platform format");
            Check(!values.ContainsKey("Paused") && !values.ContainsKey("PauseUntilUtc") && !values.ContainsKey("AutoStart"), "runtime/system state excluded");
            var current = new Settings { Paused = true }; Deadline(current, Deadline(settings));
            var preview = Parse(json, current); var candidate = Candidate(preview);
            Check(candidate.ThemeMode == 2 && candidate.Simple && candidate.Transparency == 45 && candidate.DurationMs == 3500, "display round trip");
            Check(candidate.IgnoreApps.Count == 1 && candidate.Sound && candidate.Lang == "zh", "behavior round trip");
            Check(candidate.Paused && Deadline(candidate) == Deadline(current), "parse preserves local runtime state");
            Check(Export(candidate) == json, "complete whitelist round trip");
            foreach (string name in new[] { "format", "platform", "version" })
            {
                var bad = Document(json); bad[name] = name == "version" ? (object)99 : "bad";
                Reject(() => Parse(Json.Serialize(bad), current), "reject wrong " + name);
            }
            foreach (object bad in new object[] { -1, 76, "50", true, 1.5 })
            {
                var badDoc = Document(json); Values(badDoc)["Transparency"] = bad;
                Reject(() => Parse(Json.Serialize(badDoc), current), "invalid numeric type/range rejected");
            }
            var invalidColor = Document(json); Values(invalidColor)["OkColor"] = "not-a-color";
            Reject(() => Parse(Json.Serialize(invalidColor), current), "invalid color rejected");
            Reject(() => Parse("{", current), "malformed JSON rejected");
            Reject(() => Parse(new string(' ', 1024 * 1024 + 1), current), "oversized input rejected");
            var partial = Document(json); partial["settings"] = new Dictionary<string, object> { { "Simple", true }, { "UnknownFuture", 42 }, { "FontFamily", null } };
            current.Transparency = 20; current.FontFamily = "example";
            var parsedPartial = Candidate(Parse(Json.Serialize(partial), current));
            Check(parsedPartial.Simple && parsedPartial.Transparency == 20 && parsedPartial.FontFamily == null, "partial/unknown/null fields handled");
            Check(current.FontFamily == "example", "parsing never changes live config");
            var omittedResource = Document(json); omittedResource["settings"] = new Dictionary<string, object> { { "Simple", true } };
            current.SoundFailFile = Path.Combine(dir, "old-missing.wav");
            var omittedCandidate = Candidate(Parse(Json.Serialize(omittedResource), current));
            Check(omittedCandidate.FontFamily == current.FontFamily && omittedCandidate.SoundFailFile == current.SoundFailFile, "omitted resource fields retain current preferences");
            current.SoundFailFile = null;
            var missing = Document(json); Values(missing)["SoundOkFile"] = Path.Combine(dir, "missing.wav");
            var resourcePreview = Parse(Json.Serialize(missing), current);
            Check(Candidate(resourcePreview).SoundOkFile == null, "missing sound falls back");
            string backup = Path.Combine(dir, "before.json");
            string before = Export(current); string deadline = Deadline(current);
            Call(configType, "Import", preview, current, backup, new Action(() => current.Save()));
            Check(File.ReadAllText(backup) == before, "pre-import backup contains previous settings");
            Check(current.ThemeMode == 2 && current.Paused && Deadline(current) == deadline, "import applies preferences preserving timer");
            var resetPreview = Parse(Export(new Settings()), current);
            string preFailure = Export(current); int calls = 0;
            Reject(() => Call(configType, "Import", resetPreview, current, backup, new Action(() => { if (++calls == 1) throw new IOException("simulated save failure"); current.Save(); })), "failed persistence reported");
            Check(Export(current) == preFailure && Export(Settings.Load()) == preFailure, "failed save rolls back memory and storage");
            string blocked = Path.Combine(dir, "blocked"); File.WriteAllText(blocked, "file");
            Reject(() => Call(configType, "Import", resetPreview, current, Path.Combine(blocked, "backup.json"), new Action(() => current.Save())), "backup failure aborts import");
            Check(Export(current) == preFailure, "backup failure leaves settings unchanged");
            string output = Path.Combine(dir, "export.json"); Call(configType, "Write", output, current); Call(configType, "Write", output, new Settings());
            Check(File.ReadAllText(output) == Export(new Settings()), "atomic overwrite creates readable export");
            Check(Export(Candidate(Call(configType, "Read", output, new Settings()))) == File.ReadAllText(output), "bounded file reader loads an exported backup");
            string oversized = Path.Combine(dir, "oversized.json"); File.WriteAllBytes(oversized, new byte[1024 * 1024 + 1]);
            Reject(() => Call(configType, "Read", oversized, current), "bounded reader rejects an oversized file");
            File.WriteAllBytes(oversized, new byte[] { 0xff, 0xfe, 0xff });
            Reject(() => Call(configType, "Read", oversized, current), "invalid UTF-8 file rejected");
            var runtime = Document(Export(new Settings()));
            Values(runtime)["Paused"] = false; Values(runtime)["PauseUntilUtc"] = null; Values(runtime)["AutoStart"] = false;
            var runtimePreview = Parse(Json.Serialize(runtime), current);
            Call(configType, "Import", runtimePreview, current, backup, new Action(() => current.Save()));
            Check(current.Paused && Deadline(current) == deadline, "injected runtime keys cannot change active pause");
        }

        static void WindowTests(string dir)
        {
            var settings = new Settings { Lang = "zh", ThemeMode = 2 };
            var pause = new PauseControl(settings, () => DateTime.UtcNow);
            pause.Start(15);
            L.Apply("zh");
            var ui = new Customize(settings, () => settings.Save(), b => { }, () => { }, b => { }, pause.SetManual);
            try
            {
                var toggle = (System.Windows.Controls.CheckBox)ui.Get("pause");
                Check(toggle.IsChecked == true, "settings window shows timed pause");
                toggle.IsChecked = false;
                Check(!settings.Paused && pause.Until == null, "settings window resume cancels deadline");
                toggle.IsChecked = true;
                Check(settings.Paused && pause.Until == null, "settings window manual pause has no timer");
                RenderWindow(ui, Path.Combine(dir, "settings-zh.png"));
                ui.ScrollToBottom();
                RenderWindow(ui, Path.Combine(dir, "settings-zh-backup.png"));
                settings.Lang = "en"; settings.ThemeMode = 1; L.Apply("en"); ui.Reload();
                RenderWindow(ui, Path.Combine(dir, "settings-en-backup.png"));
            }
            finally { ui.Close(); }
            Console.WriteLine("UI previews: " + dir);
        }

        static void RenderWindow(Customize ui, string path)
        {
            var root = ui.Root;
            root.Measure(new System.Windows.Size(980, 640));
            root.Arrange(new System.Windows.Rect(0, 0, 980, 640));
            root.UpdateLayout();
            var bmp = new System.Windows.Media.Imaging.RenderTargetBitmap(980, 640, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            var backdrop = new System.Windows.Media.DrawingVisual();
            using (var dc = backdrop.RenderOpen())
                dc.DrawRectangle((System.Windows.Media.Brush)root.FindResource("WinBg"), null, new System.Windows.Rect(0, 0, 980, 640));
            bmp.Render(backdrop);
            bmp.Render(root);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));
            using (var file = File.Create(path)) encoder.Save(file);
        }
    }
}
