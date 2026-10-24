using System;
using System.Globalization;

namespace Cccc
{
    // All pause entry points share one state machine. Clock injection keeps sleep/restart tests deterministic.
    sealed class PauseControl
    {
        readonly Settings settings;
        readonly Func<DateTime> clock;
        public event Action Changed;

        public PauseControl(Settings settings, Func<DateTime> clock)
        {
            this.settings = settings;
            this.clock = clock;
            Check();
        }

        public DateTime? Until
        {
            get
            {
                DateTime parsed;
                return settings.Paused && DateTime.TryParseExact(settings.PauseUntilUtc, "o", CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out parsed) && parsed.Kind == DateTimeKind.Utc ? (DateTime?)parsed : null;
            }
        }

        public void Start(int minutes)
        {
            if (minutes < 1 || minutes > 1440) throw new ArgumentOutOfRangeException("minutes");
            settings.Paused = true;
            settings.PauseUntilUtc = clock().ToUniversalTime().AddMinutes(minutes).ToString("o", CultureInfo.InvariantCulture);
            Save();
        }

        public void SetManual(bool paused)
        {
            settings.Paused = paused;
            settings.PauseUntilUtc = null;
            Save();
        }

        public void Check()
        {
            if (settings.PauseUntilUtc == null) return;
            DateTime? until = Until;
            if (until.HasValue && until.Value > clock().ToUniversalTime()) return;
            settings.Paused = false;
            settings.PauseUntilUtc = null;
            Save();
        }

        void Save()
        {
            settings.Save();
            if (Changed != null) Changed();
        }
    }
}
