using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using Timer = System.Windows.Forms.Timer;

namespace Cccc
{
    sealed class App
    {
        readonly Settings set = Settings.Load();
        readonly Card card = new Card();
        readonly Hub hub = new Hub();
        readonly KeyHook hook;
        readonly NotifyIcon tray = new NotifyIcon();
        readonly Timer debounce = new Timer { Interval = 60 };
        readonly Timer failCheck = new Timer { Interval = 450 };
        readonly MenuItem miPause, miSound, miHint, miStartup;
        Sounds sounds;
        IntPtr trayHandle;

        string lastSig, lastHold;
        double lastTime = -99, lastFail = -99;
        int count, failCount, keyKind, keyTick;
        uint keySeq, lastSeq;

        public App(bool silent)
        {
            card.CreateWindow();
            hook = new KeyHook(hub.Handle);

            hub.ClipboardChanged += delegate { debounce.Stop(); debounce.Start(); };
            hub.Key += OnKey;
            hub.Hello += delegate { Notice(L.RunningTitle, L.RunningSub); };
            debounce.Tick += delegate { debounce.Stop(); OnClipboard(); };
            failCheck.Tick += delegate { failCheck.Stop(); OnFailCheck(); };

            miPause = new MenuItem(L.MenuPause, delegate { set.Paused = !set.Paused; set.Save(); Sync(); });
            miSound = new MenuItem(L.MenuSound, delegate { set.Sound = !set.Sound; set.Save(); Sync(); if (set.Sound) sounds.Ok(); });
            miHint = new MenuItem(L.MenuHint, delegate { set.FailHint = !set.FailHint; set.Save(); Sync(); });
            miStartup = new MenuItem(L.MenuStartup, delegate { Settings.AutoStart = !Settings.AutoStart; Sync(); });
            tray.ContextMenu = new ContextMenu(new[]
            {
                new MenuItem(L.MenuShow, delegate { ShowClipboard(); }) { DefaultItem = true },
                new MenuItem("-"), miPause, miSound, miHint, miStartup,
                new MenuItem("-"), new MenuItem(L.MenuQuit, delegate { Quit(); }),
            });
            tray.MouseClick += (o, e) => { if (e.Button == MouseButtons.Left) ShowClipboard(); };
            SystemEvents.UserPreferenceChanged += (o, e) => { if (e.Category == UserPreferenceCategory.General) Sync(); };

            if (Settings.AutoStart) Settings.AutoStart = true;
            Sync();
            tray.Visible = true;
            hook.Start();

            lastSeq = Native.GetClipboardSequenceNumber();
            var now = ClipReader.Read();
            if (now != null)
            {
                lastSig = now.Sig;
                lastHold = now.Hold;
                if (now.Thumb != null) now.Thumb.Dispose();
            }
            if (!silent) Notice(L.ReadyTitle, L.ReadySub);
        }

        void Sync()
        {
            miPause.Checked = set.Paused;
            miSound.Checked = set.Sound;
            miHint.Checked = set.FailHint;
            miStartup.Checked = Settings.AutoStart;
            if (set.Sound && sounds == null) sounds = new Sounds();

            Native.ThemeMenus(Theme.Current().Dark);
            IntPtr old = trayHandle;
            int size = SystemInformation.SmallIconSize.Width;
            using (var bmp = Icons.Tray(size, Theme.TaskbarLight(), set.Paused)) trayHandle = bmp.GetHicon();
            tray.Icon = Icon.FromHandle(trayHandle);
            tray.Text = set.Paused ? L.TipPaused : L.Tip;
            if (old != IntPtr.Zero) Native.DestroyIcon(old);
        }

        void OnKey(int packed, uint seq)
        {
            keyKind = packed & 0xFF;
            keyTick = Environment.TickCount;
            if ((packed & KeyHook.Passthrough) != 0 || set.Paused || !set.FailHint) return;
            keySeq = seq;
            failCheck.Stop();
            failCheck.Start();
        }

        void OnClipboard()
        {
            // Office raises WM_CLIPBOARDUPDATE on every paste without changing anything.
            uint seq = Native.GetClipboardSequenceNumber();
            if (seq == lastSeq) return;
            lastSeq = seq;
            if (set.Paused) return;
            Content c = ClipReader.Read();
            if (c == null) return;

            // Same content rewritten with no copy key pressed, e.g. Office re-publishing
            // the clipboard when the source document closes.
            bool keyed = keyKind != 0 && unchecked(Environment.TickCount - keyTick) < 1500;
            if (c.Sig != null && c.Sig == lastSig && !keyed)
            {
                lastHold = c.Hold;
                if (c.Thumb != null) c.Thumb.Dispose();
                return;
            }

            double now = card.Now;
            bool cut = c.CutHint || (keyKind == KeyHook.CutKey && keyed);
            bool repeat = c.Sig != null && c.Sig == lastSig && now - lastTime < 15;
            count = repeat ? count + 1 : 1;
            lastSig = c.Sig; lastHold = c.Hold; lastTime = now; lastFail = -99;
            keyKind = 0;

            c.Kind = cut ? Kind.Cut : Kind.Copied;
            c.Count = count;
            c.Title = cut && count == 1 ? L.Cut : L.Again[Math.Min(count, L.Again.Length) - 1];
            if (Native.UserBusy()) { if (c.Thumb != null) c.Thumb.Dispose(); return; }
            card.Show(c, repeat);
            if (set.Sound) sounds.Ok();
        }

        void OnFailCheck()
        {
            if (Native.GetClipboardSequenceNumber() != keySeq || Native.UserBusy()) return;
            double now = card.Now;
            bool repeat = now - lastFail < 6;
            failCount = repeat ? failCount + 1 : 1;
            lastFail = now;
            card.Show(new Content
            {
                Kind = Kind.Fail,
                Title = keyKind == KeyHook.CutKey ? L.NothingCut : L.NothingCopied,
                Sub = lastHold != null ? L.StillHolding + lastHold : L.MaybeEmpty,
                Count = failCount,
            }, repeat);
            if (set.Sound) sounds.No();
        }

        void ShowClipboard()
        {
            Content c = ClipReader.Read();
            if (c == null) c = new Content { Kind = Kind.Empty, Title = L.EmptyTitle, Sub = L.EmptySub };
            else { c.Kind = Kind.Info; c.Title = L.OnClipboard; }
            card.Show(c, false);
        }

        void Notice(string title, string sub)
        {
            card.Show(new Content { Kind = Kind.Info, Title = title, Sub = sub }, false);
        }

        void Quit()
        {
            hook.Stop();
            tray.Visible = false;
            tray.Dispose();
            hub.Close();
            card.DestroyHandle();
            Application.Exit();
        }
    }

    static class Program
    {
        static App app;

        [STAThread]
        static void Main(string[] args)
        {
            string verb = args.Length > 0 ? args[0] : "";
            if (verb == "--export-icon" && args.Length > 1) { Icons.ExportIco(args[1]); return; }
            if (verb == "--sheet" && args.Length > 1) { Sheet.Render(args[1]); return; }

            bool created;
            using (var mutex = new Mutex(true, @"Local\CCCC.Singleton", out created))
            {
                if (!created)
                {
                    Native.PostMessage(Native.HWND_BROADCAST, Hub.HelloMsg, IntPtr.Zero, IntPtr.Zero);
                    return;
                }
                app = new App(Array.IndexOf(args, "--silent") >= 0);
                Application.Run();
                GC.KeepAlive(mutex);
            }
        }
    }
}
