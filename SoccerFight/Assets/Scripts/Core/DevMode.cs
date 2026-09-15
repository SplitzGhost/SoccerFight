using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Developer switches (F3 panel). Any cheat marks the run as a dev run: it still plays normally
    /// but never counts for the best-stage record.
    /// </summary>
    public static class DevMode
    {
        public static bool God, NoCooldowns, OneHit, ShowInfo;
        /// <summary>Game speed multiplier on top of hit-stop and slow motion.</summary>
        public static float Speed = 1f;
        /// <summary>Something from the dev panel was used during this run.</summary>
        public static bool UsedThisRun { get; private set; }

        public static bool AnyCheat => God || NoCooldowns || OneHit || !Mathf.Approximately(Speed, 1f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            God = NoCooldowns = OneHit = ShowInfo = false;
            Speed = 1f;
            UsedThisRun = false;
        }

        public static void MarkRun() => UsedThisRun = true;

        /// <summary>A new run starts clean unless cheats are still switched on.</summary>
        public static void OnRunStart() => UsedThisRun = AnyCheat;

        /// <summary>Short list of what is active, for the HUD badge.</summary>
        public static string Badge()
        {
            var s = "DEV";
            if (God) s += "  ·  UNVERWUNDBAR";
            if (NoCooldowns) s += "  ·  KEINE ABKLINGZEITEN";
            if (OneHit) s += "  ·  EIN TREFFER";
            if (!Mathf.Approximately(Speed, 1f)) s += "  ·  TEMPO ×" + Speed.ToString("0.##");
            return s;
        }
    }
}
