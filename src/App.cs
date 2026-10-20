using System;
using System.Diagnostics;
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
        readonly Card capsCard = new Card(true);
        readonly Hub hub = new Hub();
        readonly KeyHook hook;
        readonly NotifyIcon tray = new NotifyIcon();
        readonly Timer debounce = new Timer { Interval = 60 };
        readonly Timer failCheck = new Timer { Interval = 450 };
        readonly Timer updateTimer = new Timer { Interval = 10000 };
        readonly MenuItem miShow, miPause, miSound, miStopSounds, miHint, miCaps, miSource, miSourceOn, miWhen, miWhenAlways, miWhenAuto, miDetail;
        readonly MenuItem[] miDetails = new MenuItem[4];
        readonly MenuItem miStartup, miAutoUpdate, miLang, miLangEn, miLangZh, miCustomize, miUpdate, miQuit;
        readonly SynchronizationContext ui;
        readonly Sounds sounds = new Sounds();
        Customize customize;
        Bitmap sampleIcon;
        IntPtr trayHandle;

        string lastSig, lastHold;
        double lastTime = -99, lastFail = -99;
        int count, failCount, keyKind, keyTick;
        uint keySeq, lastSeq;
        bool checking, capsOn = Native.CapsLock();
        string declinedTag;
        Source lastSrc;
        uint srcSeq;
        bool lastSecret;

        public App(bool silent, bool updated)
        {
            ui = new WindowsFormsSynchronizationContext();
            L.Apply(set.Lang);
            sounds.Load(set.SoundOkFile, set.SoundFailFile);
            card.CreateWindow();
            card.Moved += delegate(int x, int y)
            {
                set.FreeX = x;
                set.FreeY = y;
                Changed();
            };
            capsCard.CreateWindow();
            capsCard.Moved += delegate(int x, int y)
            {
                set.CapsPosition = 10;
                set.CapsFreeX = x;
                set.CapsFreeY = y;
                Changed();
            };
            hook = new KeyHook(hub.Handle);

            hub.ClipboardChanged += delegate { debounce.Stop(); debounce.Start(); };
            hub.Key += OnKey;
            hub.Hello += delegate { Notice(L.RunningTitle, L.RunningSub); };
            debounce.Tick += delegate { debounce.Stop(); OnClipboard(); };
            failCheck.Tick += delegate { failCheck.Stop(); OnFailCheck(); };
            updateTimer.Tick += delegate { updateTimer.Interval = 24 * 60 * 60 * 1000; if (set.AutoUpdate) CheckUpdate(false); };

            miShow = new MenuItem(L.MenuShow, delegate { ShowClipboard(); }) { DefaultItem = true };
            miPause = new MenuItem(L.MenuPause, delegate { set.Paused = !set.Paused; set.Save(); Sync(); });
            miSound = new MenuItem(L.MenuSound, delegate { set.Sound = !set.Sound; set.Save(); Sync(); if (set.Sound) sounds.Ok(); });
            miStopSounds = new MenuItem(L.MenuStopSounds, delegate { sounds.StopAll(); }) { Enabled = false };
            miHint = new MenuItem(L.MenuHint, delegate { set.FailHint = !set.FailHint; set.Save(); Sync(); });
            miCaps = new MenuItem(L.MenuCaps, delegate { set.CapsHint = !set.CapsHint; set.Save(); Sync(); });
            miSourceOn = new MenuItem(L.MenuSourceOn, delegate { set.ShowSource = !set.ShowSource; set.Save(); Sync(); });
            miWhen = new MenuItem(L.SourceWhen) { Enabled = false };
            miWhenAlways = new MenuItem(L.SourceAlways, delegate { Pick(ref set.SourceWhen, 0); }) { RadioCheck = true };
            miWhenAuto = new MenuItem(L.SourceAuto, delegate { Pick(ref set.SourceWhen, 1); }) { RadioCheck = true };
            miDetail = new MenuItem(L.SourceDetail) { Enabled = false };
            miDetails[0] = new MenuItem(L.DetailIcon, delegate { Pick(ref set.SourceDetail, 0); }) { RadioCheck = true };
            miDetails[1] = new MenuItem(L.DetailApp, delegate { Pick(ref set.SourceDetail, 1); }) { RadioCheck = true };
            miDetails[2] = new MenuItem(L.DetailSite, delegate { Pick(ref set.SourceDetail, 2); }) { RadioCheck = true };
            miDetails[3] = new MenuItem(L.DetailTitle, delegate { Pick(ref set.SourceDetail, 3); }) { RadioCheck = true };
            miSource = new MenuItem(L.MenuSource, new[]
            {
                miSourceOn, new MenuItem("-"), miWhen, miWhenAlways, miWhenAuto,
                new MenuItem("-"), miDetail, miDetails[0], miDetails[1], miDetails[2], miDetails[3],
            });
            miStartup = new MenuItem(L.MenuStartup, delegate { Settings.AutoStart = !Settings.AutoStart; Sync(); });
            miAutoUpdate = new MenuItem(L.MenuAutoUpdate, delegate
            {
                set.AutoUpdate = !set.AutoUpdate; set.Save(); Sync();
                if (set.AutoUpdate) CheckUpdate(false);
            });
            miLangEn = new MenuItem("English", delegate { SetLang("en"); }) { RadioCheck = true };
            miLangZh = new MenuItem("中文", delegate { SetLang("zh"); }) { RadioCheck = true };
            miLang = new MenuItem(L.MenuLanguage, new[] { miLangEn, miLangZh });
            miCustomize = new MenuItem(L.MenuCustomize, delegate { OpenCustomize(); });
            miUpdate = new MenuItem(L.MenuCheckUpdate, delegate { CheckUpdate(true); });
            miQuit = new MenuItem(L.MenuQuit, delegate { Quit(); });
            tray.ContextMenu = new ContextMenu(new[]
            {
                miShow,
                new MenuItem("-"), miPause, miSound, miStopSounds, miHint, miCaps, miSource, miStartup, miAutoUpdate, miLang,
                new MenuItem("-"), miCustomize, miUpdate,
                new MenuItem("-"), miQuit,
            });
            tray.ContextMenu.Popup += delegate { miStopSounds.Enabled = sounds.Playing; };
            tray.MouseClick += (o, e) => { if (e.Button == MouseButtons.Left) ShowClipboard(); };
            SystemEvents.UserPreferenceChanged += (o, e) => { if (e.Category == UserPreferenceCategory.General) Sync(); };

            if (Settings.AutoStart) Settings.AutoStart = true;
            Sync();
            tray.Visible = true;
            hook.Start();
            updateTimer.Start();

            lastSeq = Native.GetClipboardSequenceNumber();
            var now = ClipReader.Read();
            if (now != null)
            {
                lastSig = now.Sig;
                lastHold = now.Hold;
                lastSrc = Sources.Detect(false, now.Site);
                srcSeq = lastSeq;
                lastSecret = now.Secret;
                if (now.Thumb != null) now.Thumb.Dispose();
            }
            if (updated) Notice(L.UpdatedTitle(Updater.Show(Updater.Current)), L.UpdatedSub);
            else if (!silent) Notice(L.ReadyTitle, L.ReadySub);
        }

        void SetLang(string lang)
        {
            if (set.Lang == lang) return;
            set.Lang = lang;
            set.Save();
            Sync();
        }

        void Changed()
        {
            set.Save();
            Sync();
            sounds.Load(set.SoundOkFile, set.SoundFailFile);
        }

        void OpenCustomize()
        {
            if (customize == null)
            {
                customize = new Customize(set, Changed, ShowSample, ShowCapsSample, TestSound);
                customize.Closed += delegate { customize = null; };
            }
            customize.Show();
            customize.Activate();
        }

        void ShowSample(bool failed)
        {
            string text = L.Zh ? "放心复制：让 Ctrl+C 也有看得见的成功" : "The quick brown fox jumps over the lazy dog";
            if (sampleIcon == null) sampleIcon = Icons.App(32);
            Content c;
            if (failed)
            {
                c = new Content { Kind = Kind.Fail, Title = L.NothingCopied, Sub = L.StillHolding + text };
                ApplySource(c, new Source { App = "Microsoft Word", Icon = sampleIcon }, set.ShowSource);
            }
            else
            {
                c = new Content { Kind = Kind.Copied, Title = L.Again[0], Meta = L.Chars(text.Length), Sub = text };
                ApplySource(c, new Source { App = "Google Chrome", Site = "github.com", Title = "GitHub", Icon = sampleIcon }, set.ShowSource);
            }
            card.Show(c, false);
        }

        void ShowCapsSample()
        {
            capsCard.Show(new Content { Kind = Kind.Caps, Title = L.Caps, CapsOn = Native.CapsLock() }, false);
        }

        void TestSound(bool ok)
        {
            if (ok) sounds.Ok(); else sounds.No();
        }

        void Sync()
        {
            L.Apply(set.Lang);
            card.Apply(set);
            capsCard.Apply(set);
            miShow.Text = L.MenuShow;
            miPause.Text = L.MenuPause;
            miSound.Text = L.MenuSound;
            miStopSounds.Text = L.MenuStopSounds;
            miHint.Text = L.MenuHint;
            miCaps.Text = L.MenuCaps;
            miSource.Text = L.MenuSource;
            miSourceOn.Text = L.MenuSourceOn;
            miWhen.Text = L.SourceWhen;
            miWhenAlways.Text = L.SourceAlways;
            miWhenAuto.Text = L.SourceAuto;
            miDetail.Text = L.SourceDetail;
            miDetails[0].Text = L.DetailIcon;
            miDetails[1].Text = L.DetailApp;
            miDetails[2].Text = L.DetailSite;
            miDetails[3].Text = L.DetailTitle;
            miStartup.Text = L.MenuStartup;
            miAutoUpdate.Text = L.MenuAutoUpdate;
            miLang.Text = L.MenuLanguage;
            miCustomize.Text = L.MenuCustomize;
            miUpdate.Text = L.MenuCheckUpdate;
            miQuit.Text = L.MenuQuit;
            miPause.Checked = set.Paused;
            miSound.Checked = set.Sound;
            miHint.Checked = set.FailHint;
            miCaps.Checked = set.CapsHint;
            miSourceOn.Checked = set.ShowSource;
            miWhenAlways.Checked = set.SourceWhen == 0;
            miWhenAuto.Checked = set.SourceWhen == 1;
            miWhenAlways.Enabled = miWhenAuto.Enabled = set.ShowSource;
            for (int i = 0; i < miDetails.Length; i++)
            {
                miDetails[i].Checked = set.SourceDetail == i;
                miDetails[i].Enabled = set.ShowSource;
            }
            miStartup.Checked = Settings.AutoStart;
            miAutoUpdate.Checked = set.AutoUpdate;
            miLangEn.Checked = !L.Zh;
            miLangZh.Checked = L.Zh;
            if (customize != null) customize.Reload();

            Native.ThemeMenus(Theme.Resolve(set, !Theme.SystemLight()).Dark);
            IntPtr old = trayHandle;
            int size = SystemInformation.SmallIconSize.Width;
            using (var bmp = Icons.Tray(size, Theme.TaskbarLight(), set.Paused)) trayHandle = bmp.GetHicon();
            tray.Icon = Icon.FromHandle(trayHandle);
            tray.Text = set.Paused ? L.TipPaused : L.Tip;
            if (old != IntPtr.Zero) Native.DestroyIcon(old);
        }

        void OnKey(int packed, uint seq)
        {
            if ((packed & 0xFF) == KeyHook.Caps) { OnCaps(); return; }
            keyKind = packed & 0xFF;
            keyTick = Environment.TickCount;
            if ((packed & KeyHook.Passthrough) != 0 || set.Paused || !set.FailHint) return;
            keySeq = seq;
            failCheck.Stop();
            failCheck.Start();
        }

        void OnCaps()
        {
            bool on = Native.CapsLock();
            if (on == capsOn) return;
            capsOn = on;
            if (!set.CapsHint || set.Paused || Native.UserBusy()) return;
            capsCard.Show(new Content { Kind = Kind.Caps, Title = L.Caps, CapsOn = on }, false);
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
            Source src = Sources.Detect(true, c.Site);

            // Same content rewritten with no copy key pressed, e.g. Office re-publishing
            // the clipboard when the source document closes.
            bool keyed = keyKind != 0 && unchecked(Environment.TickCount - keyTick) < 1500;
            if (c.Sig != null && c.Sig == lastSig && !keyed)
            {
                lastHold = c.Hold; srcSeq = seq; lastSrc = src; lastSecret = c.Secret;
                if (c.Thumb != null) c.Thumb.Dispose();
                return;
            }

            double now = card.Now;
            bool cut = c.CutHint || (keyKind == KeyHook.CutKey && keyed);
            bool repeat = c.Sig != null && c.Sig == lastSig && now - lastTime < 15;
            count = repeat ? count + 1 : 1;
            lastSig = c.Sig; lastHold = c.Hold; srcSeq = seq; lastSrc = src; lastSecret = c.Secret; lastTime = now; lastFail = -99;
            keyKind = 0;

            c.Kind = cut ? Kind.Cut : Kind.Copied;
            c.Count = count;
            c.Title = cut && count == 1 ? L.Cut : L.Again[Math.Min(count, L.Again.Length) - 1];
            ApplySource(c, src, set.ShowSource && src != null && (set.SourceWhen == 0 || !(keyed && src.Obvious)));
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
            var fail = new Content
            {
                Kind = Kind.Fail,
                Title = keyKind == KeyHook.CutKey ? L.NothingCut : L.NothingCopied,
                Sub = lastHold != null ? L.StillHolding + lastHold : L.MaybeEmpty,
                Count = failCount,
                Secret = lastSecret,
            };
            ApplySource(fail, lastSrc, set.ShowSource && lastSrc != null);
            card.Show(fail, repeat);
            if (set.Sound) sounds.No();
        }

        void ShowClipboard()
        {
            Content c = ClipReader.Read();
            if (c == null) c = new Content { Kind = Kind.Empty, Title = L.EmptyTitle, Sub = L.EmptySub };
            else
            {
                c.Kind = Kind.Info; c.Title = L.OnClipboard;
                Source src = Native.GetClipboardSequenceNumber() == srcSeq ? lastSrc : Sources.Detect(false, c.Site);
                ApplySource(c, src, set.ShowSource && src != null);
            }
            card.Show(c, false);
        }

        void ApplySource(Content c, Source src, bool show)
        {
            if (!show) return;
            c.SrcIcon = src.Icon;
            c.SrcText = Sources.Text(src, set.SourceDetail, c.Secret);
            c.SrcLine = c.SrcText.Length > 0 && (c.Secret ? Math.Min(set.SourceDetail, 1) : set.SourceDetail) >= 2;
        }

        void Pick(ref int field, int value)
        {
            field = value;
            set.Save();
            Sync();
        }

        void Notice(string title, string sub)
        {
            card.Show(new Content { Kind = Kind.Info, Title = title, Sub = sub }, false);
        }

        void CheckUpdate(bool manual)
        {
            if (checking) return;
            checking = true;
            if (manual) Notice(L.UpdateChecking, L.UpdateCheckingSub);
            ThreadPool.QueueUserWorkItem(delegate
            {
                Release r = null;
                Exception err = null;
                try { r = Updater.Fetch(); }
                catch (Exception ex) { err = ex; }
                ui.Post(delegate { OnChecked(manual, r, err); }, null);
            });
        }

        void OnChecked(bool manual, Release r, Exception err)
        {
            if (err != null)
            {
                checking = false;
                if (manual) Notice(L.CheckFailed, err.Message);
                return;
            }
            if (!Updater.Newer(r.Version, Updater.Current))
            {
                checking = false;
                if (manual) Notice(L.UpToDate, L.UpToDateSub(Updater.Show(Updater.Current)));
                return;
            }
            if (!manual && r.Tag == declinedTag) { checking = false; return; }

            string ver = Updater.Show(r.Version);
            bool yes = MessageBox.Show(L.UpdatePrompt(ver, Updater.Show(Updater.Current), r.Name == r.Tag ? null : r.Name),
                "CCCC", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes;
            if (!yes)
            {
                declinedTag = r.Tag;
                checking = false;
                return;
            }
            if (r.AssetUrl == null)
            {
                checking = false;
                Process.Start(r.HtmlUrl);
                return;
            }

            Notice(L.Downloading(ver), L.DownloadingSub);
            ThreadPool.QueueUserWorkItem(delegate
            {
                Exception install = null;
                try { Updater.Install(r, Application.ExecutablePath); }
                catch (Exception ex) { install = ex; }
                ui.Post(delegate { OnInstalled(r, install); }, null);
            });
        }

        void OnInstalled(Release r, Exception err)
        {
            checking = false;
            if (err == null)
            {
                Process.Start(Application.ExecutablePath, "--updated " + Process.GetCurrentProcess().Id);
                Quit();
                return;
            }
            if (MessageBox.Show(L.UpdateFailed(err.Message), "CCCC", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                Process.Start(r.HtmlUrl);
        }

        void Quit()
        {
            hook.Stop();
            sounds.StopAll();
            tray.Visible = false;
            tray.Dispose();
            hub.Close();
            card.DestroyHandle();
            capsCard.DestroyHandle();
            Application.Exit();
        }
    }

    static class Program
    {
        static App app;

        [STAThread]
        static void Main(string[] args)
        {
            AppContext.SetSwitch("Switch.System.Windows.DoNotScaleForDpiChanges", false);
            string verb = args.Length > 0 ? args[0] : "";
            if (verb == "--export-icon" && args.Length > 1) { Icons.ExportIco(args[1]); return; }
            if (verb == "--sheet" && args.Length > 1) { Sheet.Render(args[1]); return; }

            int oldPid;
            if (verb == "--updated" && args.Length > 1 && int.TryParse(args[1], out oldPid)) Updater.WaitForExit(oldPid);

            bool created;
            using (var mutex = new Mutex(true, @"Local\CCCC.Singleton", out created))
            {
                if (!created)
                {
                    Native.PostMessage(Native.HWND_BROADCAST, Hub.HelloMsg, IntPtr.Zero, IntPtr.Zero);
                    return;
                }
                Updater.Cleanup(Application.ExecutablePath);
                app = new App(Array.IndexOf(args, "--silent") >= 0, verb == "--updated");
                Application.Run();
                GC.KeepAlive(mutex);
            }
        }
    }
}
