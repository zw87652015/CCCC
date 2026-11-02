using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
namespace Cccc {
    static class ClickThroughTests {
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out Native.RECT rect);
        [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Native.POINT p);
        static int count;
        static void Check(bool ok, string name) { if (!ok) throw new Exception(name); count++; }
        static bool Transparent(Card card) { return (Native.GetWindowLong(card.Handle, -20) & 0x20) != 0; }
        [STAThread] static void Main() {
            Settings.Key = @"Software\CCCC-click-tests-" + Guid.NewGuid().ToString("N");
            try {
                Check(Settings.Load().NearPointerClickThrough, "default enabled");
                var s = new Settings { Position = 9, NearPointerClickThrough = false };
                s.Save(); Check(!Settings.Load().NearPointerClickThrough, "disabled survives reload");
                s.NearPointerClickThrough = true; s.Save(); Check(Settings.Load().NearPointerClickThrough, "enabled survives reload");
                s.NearPointerClickThrough = false;
                Check(!Settings.Import(s.ToJson()).NearPointerClickThrough, "JSON backup retains disabled option");
                s.ResetDisplay(); Check(s.NearPointerClickThrough, "reset restores default"); s.Position = 9;
                foreach (Role role in new[] { Role.Main, Role.Caps, Role.Wifi, Role.Audio }) {
                    var c = new Card(role); c.Apply(s); c.CreateWindow();
                    Check(Transparent(c) == (role == Role.Main || role == Role.Caps), "only pointer cards pass through");
                    s.NearPointerClickThrough = false; c.Apply(s); Check(!Transparent(c), "live toggle off");
                    s.NearPointerClickThrough = true; s.Position = 6; c.Apply(s); Check(!Transparent(c), "fixed position interactive");
                    s.Position = 9; c.Apply(s); Check(Transparent(c) == (role == Role.Main || role == Role.Caps), "live position change");
                    if (role == Role.Caps) { s.CapsPosition = 6; c.Apply(s); Check(!Transparent(c), "caps independent fixed position"); s.CapsPosition = -1; }
                    c.DestroyHandle();
                }
                var card = new Card(); card.Apply(s); card.CreateWindow();
                try {
                    s.StayUntilClicked = true; card.Apply(s);
                    card.Show(new Content { Title = "Click-through test", Sub = "No clipboard content" }, false);
                    var clock = Stopwatch.StartNew();
                    while (clock.ElapsedMilliseconds < 450) { Application.DoEvents(); System.Threading.Thread.Sleep(5); }
                    Native.RECT r; GetWindowRect(card.Handle, out r);
                    var center = new Native.POINT((r.Left + r.Right) / 2, (r.Top + r.Bottom) / 2);
                    Check(WindowFromPoint(center) != card.Handle, "OS hit testing passes through visible card");
                    var deadline = typeof(Card).GetField("deadline", BindingFlags.NonPublic | BindingFlags.Instance);
                    Check(!double.IsInfinity((double)deadline.GetValue(card)), "click-through always expires");
                    s.NearPointerClickThrough = false; card.Apply(s);
                    Check(WindowFromPoint(center) == card.Handle, "disabled mode receives mouse hit");
                } finally { typeof(Card).GetMethod("HideNow", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(card, null); card.DestroyHandle(); }
                Console.WriteLine("PASS: " + count + " click-through checks");
            } catch (Exception e) { Console.WriteLine("FAIL: " + e.Message); Environment.ExitCode = 1; }
            finally { Registry.CurrentUser.DeleteSubKeyTree(Settings.Key, false); }
        }
    }
}
