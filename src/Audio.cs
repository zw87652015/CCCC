using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Timer = System.Windows.Forms.Timer;

namespace Cccc
{
    // Watches the Windows default audio output (eRender / eMultimedia) and raises a Change
    // when it switches to a different endpoint. The Core Audio callback arrives on a COM
    // thread and only posts to the UI thread; everything else runs there.
    sealed class AudioWatch
    {
        public sealed class Change
        {
            public bool None;               // no default output device
            public string Name, Device;     // endpoint name, audio device name
            public string Hint;             // friendly name for tray anchoring
            public bool Personal, Muted;    // headphones/headset/handset; endpoint muted
            public int Volume = -1;         // 0-100, -1 unknown
            public string PrevName, PrevDevice;
            public bool PrevPersonal;
        }

        public event Action<Change> Changed;

        readonly Timer debounce = new Timer { Interval = 700 };
        readonly Stopwatch clock = Stopwatch.StartNew();
        SynchronizationContext ui;
        IMMDeviceEnumerator enumerator;
        Notifier notifier;          // rooted so the callback can't be collected
        bool started;
        string lastId;              // null = "no output"
        Endpoint last;
        double quietUntil;

        public AudioWatch()
        {
            debounce.Tick += delegate { debounce.Stop(); OnSettled(); };
        }

        // A point-in-time read for the settings preview.
        public Change Current()
        {
            var ep = Read();
            return ToChange(ep, null);
        }

        public void Start(SynchronizationContext ctx)
        {
            ui = ctx;
            if (started) return;
            enumerator = Create();
            if (enumerator == null) return;
            notifier = new Notifier(this);
            try
            {
                if (enumerator.RegisterEndpointNotificationCallback(notifier) != 0)
                {
                    Marshal.ReleaseComObject(enumerator);
                    enumerator = null;
                    notifier = null;
                    return;
                }
            }
            catch (Exception)
            {
                try { Marshal.ReleaseComObject(enumerator); } catch (Exception) { }
                enumerator = null;
                notifier = null;
                return;
            }
            started = true;
            last = Read();   // baseline, silent
            lastId = last != null ? last.Id : null;
        }

        public void Stop()
        {
            if (!started) return;
            started = false;
            debounce.Stop();
            try { enumerator.UnregisterEndpointNotificationCallback(notifier); } catch (Exception) { }
            try { Marshal.ReleaseComObject(enumerator); } catch (Exception) { }
            enumerator = null;
            notifier = null;
        }

        public void Suspend()
        {
            debounce.Stop();
        }

        public void Resume()
        {
            quietUntil = clock.Elapsed.TotalSeconds + 10;
            last = Read();   // silent baseline, no cards
            lastId = last != null ? last.Id : null;
        }

        const int Erender = 0, Emultimedia = 1;

        static IMMDeviceEnumerator Create()
        {
            try { return (IMMDeviceEnumerator)(object)new MMDeviceEnumeratorCom(); }
            catch (Exception) { return null; }
        }

        // The current default render endpoint, or null when there is none or the service is down.
        Endpoint Read()
        {
            IMMDeviceEnumerator en = enumerator;
            bool own = false;
            if (en == null)
            {
                en = Create();
                if (en == null) return null;
                own = true;
            }
            try
            {
                IMMDevice dev;
                try
                {
                    if (en.GetDefaultAudioEndpoint(Erender, Emultimedia, out dev) != 0 || dev == null) return null;
                }
                catch (Exception) { return null; }
                try { return Endpoint.From(dev); }
                finally { try { Marshal.ReleaseComObject(dev); } catch (Exception) { } }
            }
            finally { if (own) { try { Marshal.ReleaseComObject(en); } catch (Exception) { } } }
        }

        static Change ToChange(Endpoint ep, Endpoint prev)
        {
            var c = new Change();
            if (ep == null) { c.None = true; }
            else
            {
                c.Name = ep.Name;
                c.Device = ep.Device;
                c.Hint = ep.Hint;
                c.Personal = ep.Personal;
                c.Volume = ep.Volume;
                c.Muted = ep.Muted;
            }
            if (prev != null)
            {
                c.PrevName = prev.Name;
                c.PrevDevice = prev.Device;
                c.PrevPersonal = prev.Personal;
            }
            return c;
        }

        // Fired once the burst settles: compare the now-current endpoint with the baseline.
        void OnSettled()
        {
            var ep = Read();
            string id = ep != null ? ep.Id : null;
            if (id == lastId) return;    // flapped back, or still nothing
            var prev = last;
            last = ep;
            lastId = id;
            if (clock.Elapsed.TotalSeconds < quietUntil) return;
            if (Changed != null) Changed(ToChange(ep, prev));
        }

        // COM callback thread: hop to the UI thread and restart the debounce.
        void OnDefaultChanged()
        {
            var ctx = ui;
            if (ctx == null) return;
            ctx.Post(delegate
            {
                if (!started) return;
                debounce.Stop();
                debounce.Start();
            }, null);
        }

        sealed class Notifier : IMMNotificationClient
        {
            readonly AudioWatch owner;
            public Notifier(AudioWatch owner) { this.owner = owner; }
            public void OnDeviceStateChanged(string id, int state) { }
            public void OnDeviceAdded(string id) { }
            public void OnDeviceRemoved(string id) { }
            public void OnDefaultDeviceChanged(int flow, int role, string id)
            {
                if (flow == Erender && role == Emultimedia) owner.OnDefaultChanged();
            }
            public void OnPropertyValueChanged(string id, PROPERTYKEY key) { }
        }

        sealed class Endpoint
        {
            public string Id, Name, Device, Hint;
            public bool Personal, Muted;
            public int Volume = -1;

            public static Endpoint From(IMMDevice dev)
            {
                var ep = new Endpoint();
                try { dev.GetId(out ep.Id); } catch (Exception) { }
                IPropertyStore ps;
                try
                {
                    if (dev.OpenPropertyStore(0 /* STGM_READ */, out ps) == 0 && ps != null)
                    {
                        try
                        {
                            string desc = PropString(ps, ref PkeyDeviceDesc);
                            string friendly = PropString(ps, ref PkeyFriendlyName);
                            ep.Name = !string.IsNullOrEmpty(desc) ? desc : friendly;
                            ep.Hint = friendly;
                            ep.Device = PropString(ps, ref PkeyInterfaceName);
                            if (string.IsNullOrEmpty(ep.Device) || ep.Device == ep.Name) ep.Device = null;
                            ep.Personal = IsPersonal(PropUint(ps, ref PkeyFormFactor));
                        }
                        finally { Marshal.ReleaseComObject(ps); }
                    }
                }
                catch (Exception) { }
                object activated;
                var iid = typeof(IAudioEndpointVolume).GUID;
                try
                {
                    if (dev.Activate(ref iid, 0x17 /* CLSCTX_ALL */, IntPtr.Zero, out activated) == 0 && activated != null)
                    {
                        var vol = (IAudioEndpointVolume)activated;
                        try
                        {
                            float scalar;
                            if (vol.GetMasterVolumeLevelScalar(out scalar) == 0)
                                ep.Volume = Math.Max(0, Math.Min(100, (int)Math.Round(scalar * 100)));
                            int mute;
                            if (vol.GetMute(out mute) == 0) ep.Muted = mute != 0;
                        }
                        finally { Marshal.ReleaseComObject(vol); }
                    }
                }
                catch (Exception) { }
                return ep;
            }

            // Headphones(3) / Headset(5) / Handset(6): sound meant for one pair of ears.
            static bool IsPersonal(uint form) { return form == 3 || form == 5 || form == 6; }
        }

        // Core Audio interop. PROPERTYKEY ids from Functiondiscoverykeys_devicedef.h.
        static PROPERTYKEY PkeyDeviceDesc = new PROPERTYKEY(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 2);
        static PROPERTYKEY PkeyFriendlyName = new PROPERTYKEY(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 14);
        static PROPERTYKEY PkeyInterfaceName = new PROPERTYKEY(new Guid("026e516e-b814-414b-83cd-856d6fef4822"), 2);
        static PROPERTYKEY PkeyFormFactor = new PROPERTYKEY(new Guid("1da5d803-d492-4edd-8c23-e0c0ffee7f0e"), 0);

        static string PropString(IPropertyStore ps, ref PROPERTYKEY key)
        {
            PROPVARIANT v;
            if (ps.GetValue(ref key, out v) != 0) return null;
            try
            {
                return v.vt == 31 /* VT_LPWSTR */ && v.p != IntPtr.Zero ? Marshal.PtrToStringUni(v.p) : null;
            }
            finally { PropVariantClear(ref v); }
        }

        static uint PropUint(IPropertyStore ps, ref PROPERTYKEY key)
        {
            PROPVARIANT v;
            if (ps.GetValue(ref key, out v) != 0) return 0;
            try { return v.vt == 19 /* VT_UI4 */ ? (uint)v.p.ToInt32() : 0; }
            finally { PropVariantClear(ref v); }
        }

        [DllImport("ole32.dll")] static extern int PropVariantClear(ref PROPVARIANT pvar);

        [StructLayout(LayoutKind.Sequential)]
        public struct PROPERTYKEY
        {
            public Guid fmtid;
            public int pid;
            public PROPERTYKEY(Guid g, int p) { fmtid = g; pid = p; }
        }

        // 16-byte union on 32-bit, 24 on 64-bit (DECIMAL-sized); we read VT_LPWSTR/VT_UI4 only.
        [StructLayout(LayoutKind.Sequential)]
        public struct PROPVARIANT
        {
            public ushort vt;
            public ushort wReserved1, wReserved2, wReserved3;
            public IntPtr p;
            public int p2;
        }

        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        sealed class MMDeviceEnumeratorCom { }

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceEnumerator
        {
            [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
            [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
            [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
            [PreserveSig] int RegisterEndpointNotificationCallback(IMMNotificationClient client);
            [PreserveSig] int UnregisterEndpointNotificationCallback(IMMNotificationClient client);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDevice
        {
            [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
            [PreserveSig] int OpenPropertyStore(int stgmAccess, out IPropertyStore props);
            [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
            [PreserveSig] int GetState(out int state);
        }

        [ComImport, Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMNotificationClient
        {
            void OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string id, int state);
            void OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string id);
            void OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string id);
            void OnDefaultDeviceChanged(int flow, int role, [MarshalAs(UnmanagedType.LPWStr)] string id);
            void OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string id, PROPERTYKEY key);
        }

        [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IPropertyStore
        {
            [PreserveSig] int GetCount(out int count);
            [PreserveSig] int GetAt(int i, out PROPERTYKEY key);
            [PreserveSig] int GetValue(ref PROPERTYKEY key, out PROPVARIANT value);
        }

        [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioEndpointVolume
        {
            [PreserveSig] int RegisterControlChangeNotify(IntPtr notify);
            [PreserveSig] int UnregisterControlChangeNotify(IntPtr notify);
            [PreserveSig] int GetChannelCount(out int count);
            [PreserveSig] int SetMasterVolumeLevel(float levelDb, ref Guid eventContext);
            [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid eventContext);
            [PreserveSig] int GetMasterVolumeLevel(out float levelDb);
            [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
            [PreserveSig] int SetChannelVolumeLevel(int channel, float levelDb, ref Guid eventContext);
            [PreserveSig] int SetChannelVolumeLevelScalar(int channel, float level, ref Guid eventContext);
            [PreserveSig] int GetChannelVolumeLevel(int channel, out float levelDb);
            [PreserveSig] int GetChannelVolumeLevelScalar(int channel, out float level);
            [PreserveSig] int SetMute(int mute, ref Guid eventContext);
            [PreserveSig] int GetMute(out int mute);
        }
    }
}
