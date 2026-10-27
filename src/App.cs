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
        readonly Card capsCard = new Card(Role.Caps);
        readonly Card wifiCard = new Card(Role.Wifi);
        readonly Card audioCard = new Card(Role.Audio);
        readonly WifiWatch wifi = new WifiWatch();
        readonly AudioWatch audio = new AudioWatch();
        readonly Hub hub = new Hub();
        readonly KeyHook hook;
        readonly NotifyIcon tray = new NotifyIcon();
        readonly Timer debounce = new Timer { Interval = 60 };
        readonly Timer failCheck = new Timer { Interval = 450 };
        readonly Timer updateTimer = new Timer { Interval = 10000 };
        readonly Timer trim = new Timer { Interval = 3000 };
        readonly MenuItem miShow, miPause, miSound, miStopSounds, miHint, miCaps, miWifi, miAudio, miSource, miSourceOn, miWhen, miWhenAlways, miWhenAuto, miDetail;
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
            // A manual start asks about the tray icon; --silent (sign-in) and --updated reuse the saved choice.
            if (!silent && !updated)
            {
                set.HideTray = MessageBox.Show(L.TrayPrompt, "CCCC", MessageBoxButtons.YesNo, MessageBoxIcon.Information,
                    set.HideTray ? MessageBoxDefaultButton.Button2 : MessageBoxDefaultButton.Button1) == DialogResult.No;
                set.Save();
            }
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
            wifiCard.CreateWindow();
            audioCard.CreateWindow();
            wifi.Changed += OnWifi;
            audio.Changed += OnAudio;
            hook = new KeyHook(hub.Handle);

            hub.ClipboardChanged += delegate { debounce.Stop(); debounce.Start(); };
            hub.Key += OnKey;
            hub.Hello += delegate { if (set.HideTray) OpenCustomize(); else Notice(L.RunningTitle, L.RunningSub); };
            debounce.Tick += delegate { debounce.Stop(); OnClipboard(); };
            failCheck.Tick += delegate { failCheck.Stop(); OnFailCheck(); };
            updateTimer.Tick += delegate { updateTimer.Interval = 24 * 60 * 60 * 1000; if (set.AutoUpdate) CheckUpdate(false); };
            trim.Tick += delegate { trim.Stop(); if (customize == null) Native.TrimMemory(); };
            Card.AllHidden += delegate { trim.Stop(); trim.Start(); };

            miShow = new MenuItem(L.MenuShow, delegate { ShowClipboard(); }) { DefaultItem = true };
            miPause = new MenuItem(L.MenuPause, delegate { set.Paused = !set.Paused; set.Save(); Sync(); });
            miSound = new MenuItem(L.MenuSound, delegate { set.Sound = !set.Sound; set.Save(); Sync(); if (set.Sound) sounds.Ok(); });
            miStopSounds = new MenuItem(L.MenuStopSounds, delegate { sounds.StopAll(); }) { Enabled = false };
            miHint = new MenuItem(L.MenuHint, delegate { set.FailHint = !set.FailHint; set.Save(); Sync(); });
            miCaps = new MenuItem(L.MenuCaps, delegate { set.CapsHint = !set.CapsHint; set.Save(); Sync(); });
            miWifi = new MenuItem(L.MenuWifi, delegate { set.WifiHint = !set.WifiHint; set.Save(); Sync(); });
            miAudio = new MenuItem(L.MenuAudio, delegate { set.AudioHint = !set.AudioHint; set.Save(); Sync(); });
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
                new MenuItem("-"), miPause, miSound, miStopSounds, miHint, miCaps, miWifi, miAudio, miSource, miStartup, miAutoUpdate, miLang,
                new MenuItem("-"), miCustomize, miUpdate,
                new MenuItem("-"), miQuit,
            });
            tray.ContextMenu.Popup += delegate { miStopSounds.Enabled = sounds.Playing; };
            tray.MouseClick += (o, e) => { if (e.Button == MouseButtons.Left) ShowClipboard(); };
            SystemEvents.UserPreferenceChanged += (o, e) => { if (e.Category == UserPreferenceCategory.General) Sync(); };
            SystemEvents.PowerModeChanged += delegate(object o, PowerModeChangedEventArgs e)
            {
                if (e.Mode == PowerModes.Suspend) { wifi.Suspend(); audio.Suspend(); }
                else if (e.Mode == PowerModes.Resume) { wifi.Resume(); audio.Resume(); }
            };

            if (Settings.AutoStart) Settings.AutoStart = true;
            Sync();
            hook.Start();
            updateTimer.Start();
            trim.Start();

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
            else if (!silent) Notice(L.ReadyTitle, set.HideTray ? L.ReadyHiddenSub : L.ReadySub);
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
                customize = new Customize(set, Changed, ShowSample, ShowCapsSample, ShowWifiSample, ShowAudioSample, TestSound, wifi);
                customize.Closed += delegate { customize = null; trim.Stop(); trim.Start(); };
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

        // The preview shows the real current connection, not fake data.
        void ShowWifiSample()
        {
            var ch = wifi.Current();
            if (ch == null)
            {
                if (!wifi.Available)
                    wifiCard.Show(new Content { Kind = Kind.WifiDown, Title = L.WifiNoAdapter }, false);
                return;
            }
            Content c;
            if (!ch.Connected)
                c = new Content { Kind = Kind.WifiDown, Title = L.WifiDisconnected };
            else if (ch.Name != null)
                c = new Content { Kind = Kind.WifiUp, Title = L.WifiConnected(ch.Name),
                                  Sub = L.Detail(L.Band(ch.Band), ch.Signal >= 0 ? L.Signal(ch.Signal) : null) };
            else
                c = new Content { Kind = Kind.WifiUp, Title = L.WifiConnected(null), Sub = L.WifiNeedLocation };
            wifiCard.AnchorName = ch.Name;
            wifiCard.Show(c, false);
        }

        // The preview shows the real current output device, not fake data.
        void ShowAudioSample()
        {
            var ch = audio.Current();
            audioCard.AnchorName = ch != null && !ch.None ? ch.Hint : null;
            audioCard.Show(AudioContent(ch, true), false);
        }

        static Content AudioContent(AudioWatch.Change ch, bool preview)
        {
            if (ch == null || ch.None)
            {
                return new Content
                {
                    Kind = Kind.AudioWarn, Glyph = 2, Title = L.AudioNone,
                    Sub = !preview && ch != null && ch.PrevName != null ? L.AudioWas(ch.PrevName) : "",
                };
            }
            string prevLabel = null;
            if (!preview && ch.PrevName != null)
                prevLabel = ch.PrevName == ch.Name ? ch.PrevDevice ?? ch.PrevName : ch.PrevName;
            return new Content
            {
                // Loud over the room's speakers right after headphones were the output: warn.
                Kind = !ch.Personal && !ch.Muted && ch.Volume != 0 && ch.PrevPersonal ? Kind.AudioWarn : Kind.Audio,
                Glyph = ch.Personal ? 1 : 0,
                Muted = ch.Muted,
                Title = L.AudioTitle(ch.Name),
                Sub = L.Detail(ch.Muted ? L.AudioMuted : ch.Volume >= 0 ? L.AudioVolume(ch.Volume) : null,
                    prevLabel != null ? L.AudioWas(prevLabel) : null, ch.Device),
            };
        }

        void OnAudio(AudioWatch.Change ch)
        {
            if (!set.AudioHint || set.Paused) return;
            if (Native.UserBusy()) { Native.Debug("audio: skipped (busy)"); return; }
            audioCard.AnchorName = ch.Hint ?? ch.Name;
            audioCard.Show(AudioContent(ch, false), false);
        }

        void OnWifi(WifiWatch.Change ch)
        {
            if (!set.WifiHint || set.Paused) return;
            if (Native.UserBusy()) { Native.Debug("wifi: skipped (busy)"); return; }
            wifiCard.AnchorName = ch.Name ?? ch.Prev;
            string sig = ch.Signal >= 0 ? L.Signal(ch.Signal) : null;
            var c = new Content { Kind = ch.Kind == WifiWatch.Disconnected ? Kind.WifiDown : Kind.WifiUp };
            if (ch.Kind == WifiWatch.Switched)
            {
                c.Title = L.WifiSwitched(ch.Name);
                c.Sub = ch.Name == null ? L.WifiNeedLocation
                    : L.Detail(L.Band(ch.Band), sig, ch.Prev != null ? L.WifiFrom(ch.Prev) : null);
            }
            else if (ch.Kind == WifiWatch.BandChanged)
            {
                c.Title = L.WifiNowBand(ch.Name, ch.Band);
                c.Sub = ch.Name == null ? L.WifiNeedLocation : L.Detail(sig, L.WifiWasBand(ch.PrevBand));
            }
            else if (ch.Kind == WifiWatch.Reconnected)
            {
                c.Title = L.WifiReconnected(ch.Name);
                c.Sub = ch.Name == null ? L.WifiNeedLocation : L.Detail(L.Band(ch.Band), sig, L.WifiOffline(ch.Seconds));
            }
            else
            {
                c.Title = L.WifiDisconnected;
                c.Sub = ch.Prev != null ? L.WifiWasOn(ch.Prev) : (ch.Name == null ? L.WifiNeedLocation : null);
            }
            wifiCard.Show(c, false);
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
            wifiCard.Apply(set);
            audioCard.Apply(set);
            if (set.WifiHint) wifi.Start(ui); else wifi.Stop();
            if (set.AudioHint) audio.Start(ui); else audio.Stop();
            miShow.Text = L.MenuShow;
            miPause.Text = L.MenuPause;
            miSound.Text = L.MenuSound;
            miStopSounds.Text = L.MenuStopSounds;
            miHint.Text = L.MenuHint;
            miCaps.Text = L.MenuCaps;
            miWifi.Text = L.MenuWifi;
            miAudio.Text = L.MenuAudio;
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
            miWifi.Checked = set.WifiHint;
            miAudio.Checked = set.AudioHint;
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
            tray.Visible = !set.HideTray;
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
            if (!set.CapsHint || set.Paused) return;
            if (Native.UserBusy()) { Native.Debug("caps: skipped (busy)"); return; }
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
            bool ignored = src != null && set.Ignores(src.Exe);
            if ((c.Sig != null && c.Sig == lastSig && !keyed) || ignored)
            {
                lastSig = c.Sig; lastHold = c.Hold; srcSeq = seq; lastSrc = src; lastSecret = c.Secret; keyKind = 0;
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
            if (Native.UserBusy()) { Native.Debug("copy: skipped (busy)"); if (c.Thumb != null) c.Thumb.Dispose(); return; }
            card.Show(c, repeat);
            if (set.Sound) sounds.Ok();
        }

        void OnFailCheck()
        {
            if (Native.GetClipboardSequenceNumber() != keySeq) return;
            if (Native.UserBusy()) { Native.Debug("fail check: skipped (busy)"); return; }
            if (set.Ignores(Sources.ForegroundExe())) return;
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
            wifi.Stop();
            audio.Stop();
            card.DestroyHandle();
            capsCard.DestroyHandle();
            wifiCard.DestroyHandle();
            audioCard.DestroyHandle();
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
