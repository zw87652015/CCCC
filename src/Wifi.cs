using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Timer = System.Windows.Forms.Timer;

namespace Cccc
{
    // Watches the Native Wi-Fi service and decides when a change is worth a card.
    // Notifications arrive on a wlanapi thread and are marshalled to the UI thread;
    // everything below except OnNotify runs there.
    sealed class WifiWatch
    {
        public const int Switched = 0, BandChanged = 1, Reconnected = 2, Disconnected = 3;

        public sealed class Change
        {
            public int Kind;
            public string Name, Prev;    // null when the name can't be read (Location denied)
            public int Band, PrevBand;   // 0 unknown, else 2 / 5 / 6 GHz
            public int Signal = -1;      // 0-100, -1 unknown
            public int Seconds;          // how long it was offline (reconnected card)
            public bool Connected;
        }

        public event Action<Change> Changed;

        readonly Native.WlanNotify notify;   // rooted so the callback can't be collected
        readonly Timer lost = new Timer { Interval = 4000 };
        readonly Timer settle = new Timer { Interval = 3000 };
        readonly Stopwatch clock = Stopwatch.StartNew();
        SynchronizationContext ui;
        IntPtr wlan = IntPtr.Zero;
        Guid ifGuid;
        bool started, adapter, denied, probed;

        // state machine (UI thread only)
        string name, prev;
        int band;
        bool connected, dropped, lostShown;
        double lostAt, quietUntil;

        public WifiWatch()
        {
            notify = OnNotify;
            lost.Tick += delegate { lost.Stop(); OnLost(); };
            settle.Tick += delegate { settle.Stop(); OnSettle(); };
        }

        // null until the first probe has run; then whether the connection state could be read.
        public bool? NamesAllowed { get { return !probed ? (bool?)null : denied ? (bool?)false : (bool?)true; } }

        public bool Available
        {
            get { return started ? adapter : Detect(); }
        }

        public bool Started { get { return started; } }

        // A point-in-time read for the settings preview; null when there is no adapter.
        public Change Current()
        {
            if (!started || !adapter) return null;
            bool ok;
            var i = Query(out ok);
            return new Change
            {
                Name = i.Name,
                Band = i.Band,
                Signal = i.Signal,
                Connected = i.State >= 0 ? i.State == 1 : connected,
            };
        }

        // A quick open/enum/close used by Customize when the watcher isn't running.
        public static bool Detect()
        {
            IntPtr h;
            uint neg;
            if (Native.WlanOpenHandle(2, IntPtr.Zero, out neg, out h) != 0) return false;
            Guid g;
            bool any = FirstInterface(h, out g);
            Native.WlanCloseHandle(h, IntPtr.Zero);
            return any;
        }

        public void Start(SynchronizationContext ctx)
        {
            ui = ctx;
            if (started) return;
            uint neg;
            if (Native.WlanOpenHandle(2, IntPtr.Zero, out neg, out wlan) != 0) return;
            uint prevSources;
            Native.WlanRegisterNotification(wlan, Native.WLAN_ACM | Native.WLAN_MSM, false, notify, IntPtr.Zero, IntPtr.Zero, out prevSources);
            adapter = FirstInterface(wlan, out ifGuid);
            started = true;
            Probe();
            ReadState();
        }

        public void Stop()
        {
            if (!started) return;
            started = false;
            lost.Stop();
            settle.Stop();
            Native.WlanCloseHandle(wlan, IntPtr.Zero);
            wlan = IntPtr.Zero;
        }

        public void Suspend()
        {
            lost.Stop();
            settle.Stop();
        }

        public void Resume()
        {
            quietUntil = Now() + 20;
            ReadState();
        }

        // One current_connection query, done when the feature is switched on: it is what
        // makes Windows ask for Location on 24H2+.
        public void Probe()
        {
            if (!adapter) return;
            bool ok;
            Query(out ok);
        }

        static bool FirstInterface(IntPtr h, out Guid guid)
        {
            guid = Guid.Empty;
            IntPtr list;
            if (Native.WlanEnumInterfaces(h, IntPtr.Zero, out list) != 0) return false;
            try
            {
                if (Marshal.ReadInt32(list, 0) <= 0) return false;
                guid = (Guid)Marshal.PtrToStructure(new IntPtr(list.ToInt64() + 8), typeof(Guid));
                return true;
            }
            finally { Native.WlanFreeMemory(list); }
        }

        sealed class Info
        {
            public string Name;
            public byte[] Bssid;
            public int Band, Signal = -1, State = -1;
        }

        double Now() { return clock.Elapsed.TotalSeconds; }

        // The current connection's SSID (or profile name), BSSID, band and signal.
        // current_connection is ACCESS_DENIED on 24H2+ while Location is off.
        Info Query(out bool ok)
        {
            var i = new Info();
            ok = false;
            uint size, err;
            IntPtr data;
            int type;
            probed = true;
            err = Native.WlanQueryInterface(wlan, ref ifGuid, Native.WLAN_OP_CURRENT_CONNECTION, IntPtr.Zero, out size, out data, out type);
            if (err == 5) { denied = true; i.Band = ChannelBand(); i.State = InterfaceState(); return i; }
            if (err != 0) { i.State = InterfaceState(); i.Band = ChannelBand(); return i; }
            denied = false;
            ok = true;
            try
            {
                var c = (Native.WLAN_CONNECTION_ATTRIBUTES)Marshal.PtrToStructure(data, typeof(Native.WLAN_CONNECTION_ATTRIBUTES));
                i.Name = Ssid(c.Assoc.Ssid);
                if (i.Name == null && c.ProfileName != null && c.ProfileName.Length > 0) i.Name = c.ProfileName;
                i.Signal = (int)c.Assoc.SignalQuality;
                i.State = c.State;
                i.Bssid = c.Assoc.Bssid;
                i.Band = FreqBand(i.Bssid);
                if (i.Band == 0) i.Band = ChannelBand();
            }
            finally { Native.WlanFreeMemory(data); }
            return i;
        }

        int InterfaceState()
        {
            uint size;
            IntPtr data;
            int type;
            if (Native.WlanQueryInterface(wlan, ref ifGuid, Native.WLAN_OP_INTERFACE_STATE, IntPtr.Zero, out size, out data, out type) != 0) return -1;
            try { return Marshal.ReadInt32(data); }
            finally { Native.WlanFreeMemory(data); }
        }

        // 2.4 GHz on channels 1-14, 5 GHz on 32+. 6 GHz can't be told from 5 without the frequency.
        int ChannelBand()
        {
            uint size;
            IntPtr data;
            int type;
            if (Native.WlanQueryInterface(wlan, ref ifGuid, Native.WLAN_OP_CHANNEL_NUMBER, IntPtr.Zero, out size, out data, out type) != 0) return 0;
            try
            {
                uint ch = (uint)Marshal.ReadInt32(data);
                return ch >= 1 && ch <= 14 ? 2 : ch >= 32 ? 5 : 0;
            }
            finally { Native.WlanFreeMemory(data); }
        }

        // The BSS list carries each access point's centre frequency; needs Location on 24H2+.
        int FreqBand(byte[] bssid)
        {
            if (bssid == null) return 0;
            IntPtr list;
            if (Native.WlanGetNetworkBssList(wlan, ref ifGuid, IntPtr.Zero, 0, false, IntPtr.Zero, out list) != 0) return 0;
            try
            {
                int count = Marshal.ReadInt32(list, 0);
                int stride = Marshal.SizeOf(typeof(Native.WLAN_BSS_ENTRY));
                for (int n = 0; n < count; n++)
                {
                    var e = (Native.WLAN_BSS_ENTRY)Marshal.PtrToStructure(new IntPtr(list.ToInt64() + 8 + n * stride), typeof(Native.WLAN_BSS_ENTRY));
                    if (!Same(e.Bssid, bssid)) continue;
                    uint mhz = e.ChCenterFrequency / 1000;
                    return mhz < 3000 ? 2 : mhz < 6000 ? 5 : 6;
                }
                return 0;
            }
            finally { Native.WlanFreeMemory(list); }
        }

        static bool Same(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        static string Ssid(Native.DOT11_SSID s)
        {
            int n = s.Bytes != null ? (int)Math.Min(s.Length, s.Bytes.Length) : 0;
            if (n <= 0) return null;
            string t = Encoding.UTF8.GetString(s.Bytes, 0, n).TrimEnd('\0');
            return t.Length > 0 ? t : null;
        }

        // Any PHY reporting software or hardware off (or no PHYs at all) means the radio is off.
        // A failed query counts as on — better a card than silence.
        bool RadioOn()
        {
            uint size;
            IntPtr data;
            int type;
            if (Native.WlanQueryInterface(wlan, ref ifGuid, Native.WLAN_OP_RADIO_STATE, IntPtr.Zero, out size, out data, out type) != 0) return true;
            try
            {
                int count = Marshal.ReadInt32(data);
                if (count <= 0) return false;
                for (int i = 0; i < count; i++)
                {
                    int sw = Marshal.ReadInt32(data, 4 + i * 12 + 4);
                    int hw = Marshal.ReadInt32(data, 4 + i * 12 + 8);
                    if (sw == 2 || hw == 2) return false;
                }
                return true;
            }
            finally { Native.WlanFreeMemory(data); }
        }

        void ReadState()
        {
            bool ok;
            var i = Query(out ok);
            if (i.Name != null) name = i.Name;
            if (i.Band != 0) band = i.Band;
            if (i.State >= 0) connected = i.State == 1;
        }

        // wlanapi callback: runs on a service thread; hop to the UI thread with the bits we need.
        void OnNotify(ref Native.WLAN_NOTIFICATION_DATA d, IntPtr context)
        {
            if (!adapter || d.InterfaceGuid != ifGuid || ui == null) return;
            int code = -1;
            string profile = null;
            if (d.Source == Native.WLAN_ACM && d.Code == Native.WLAN_ACM_CONNECTION_COMPLETE)
            {
                if (d.DataSize >= Marshal.SizeOf(typeof(Native.WLAN_CONNECTION_NOTIFICATION_DATA)) && d.Data != IntPtr.Zero)
                {
                    var c = (Native.WLAN_CONNECTION_NOTIFICATION_DATA)Marshal.PtrToStructure(d.Data, typeof(Native.WLAN_CONNECTION_NOTIFICATION_DATA));
                    if (c.ReasonCode != 0) return;   // failed attempt
                    profile = c.ProfileName;
                }
                code = 0;
            }
            else if (d.Source == Native.WLAN_ACM && d.Code == Native.WLAN_ACM_DISCONNECTED) code = 1;
            else if (d.Source == Native.WLAN_MSM && d.Code == Native.WLAN_MSM_ROAMING_END) code = 2;
            if (code < 0) return;
            ui.Post(delegate
            {
                if (!started) return;
                if (code == 0) LinkUp(profile);
                else if (code == 1) LinkDown();
                else Roam();
            }, null);
        }

        void LinkUp(string profile)
        {
            bool ok;
            var i = Query(out ok);
            string newName = i.Name ?? (profile != null && profile.Length > 0 ? profile : null);
            bool wasDown = dropped;
            dropped = false;
            lost.Stop();
            connected = true;
            if (Now() >= quietUntil)
            {
                int kind = -1;
                if (name != null && newName != null && name != newName) kind = Switched;
                else if (wasDown)
                {
                    // A quick same-name drop is how band steering looks: call it a band
                    // change when both bands are known and different.
                    kind = !lostShown && band != 0 && i.Band != 0 && i.Band != band ? BandChanged : Reconnected;
                }
                if (kind >= 0)
                    Raise(new Change
                    {
                        Kind = kind,
                        Name = newName,
                        Prev = name,
                        Band = i.Band,
                        PrevBand = band,
                        Signal = i.Signal,
                        Seconds = lostAt > 0 ? Math.Max(0, (int)Math.Round(Now() - lostAt)) : 0,
                    });
            }
            lostShown = false;
            name = newName;
            if (i.Band != 0) band = i.Band;
        }

        void LinkDown()
        {
            if (!connected) return;
            connected = false;
            prev = name;
            lostAt = Now();
            dropped = true;
            settle.Stop();
            lost.Stop();
            lost.Start();
        }

        void Roam()
        {
            if (!connected) return;
            bool ok;
            var i = Query(out ok);
            if (i.Name != null && name != null && i.Name != name)
            {
                // A different network with no link-down is a switch, not a roam.
                string old = name;
                int oldBand = band;
                name = i.Name;
                if (i.Band != 0) band = i.Band;
                if (Now() >= quietUntil)
                    Raise(new Change { Kind = Switched, Name = i.Name, Prev = old, Band = i.Band, PrevBand = oldBand, Signal = i.Signal });
                return;
            }
            settle.Stop();
            settle.Start();
        }

        // Still down 4 s after the drop: say so, unless the radio is off or we're quiet.
        // `dropped` stays set so a later rejoin still counts as a reconnect; a radio-off
        // drop forgets the drop but keeps the name so rejoining the same network is silent.
        void OnLost()
        {
            if (connected || !dropped || Now() < quietUntil) return;
            if (!RadioOn()) { dropped = false; return; }
            lostShown = true;
            Raise(new Change { Kind = Disconnected, Prev = prev });
        }

        // A roam settled: read the band again — a different band is worth a card, the same band isn't.
        void OnSettle()
        {
            if (!connected || Now() < quietUntil) return;
            bool ok;
            var i = Query(out ok);
            if (i.Band != 0 && band != 0 && i.Band != band)
                Raise(new Change { Kind = BandChanged, Name = i.Name ?? name, Band = i.Band, PrevBand = band, Signal = i.Signal });
            if (i.Band != 0) band = i.Band;
            if (i.Name != null) name = i.Name;
        }

        void Raise(Change c)
        {
            if (Changed != null) Changed(c);
        }
    }
}
