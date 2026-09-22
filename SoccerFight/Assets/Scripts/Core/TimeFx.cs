using UnityEngine;

namespace SoccerFight
{
    /// <summary>Hit-stop (freeze frames) and slow motion, both driven in real time.</summary>
    public static class TimeFx
    {
        static float hitStopTimer;
        static float hitStopScale = 1f;
        static float slowHold;
        static float slowRecover;
        static float slowRecoverTotal;
        static float slowScale = 1f;

        public static float Scale { get; private set; } = 1f;

        /// <summary>Real frame time for UI and time effects — or the fixed step while a capture records frames.</summary>
        public static float UiDelta => Mathf.Min(Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Time.unscaledDeltaTime, 0.05f);

        /// <summary>Pause menu open: freezes game time completely (UI keeps running on unscaled time).</summary>
        public static bool Paused;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetAll()
        {
            hitStopTimer = 0f;
            hitStopScale = 1f;
            slowHold = slowRecover = slowRecoverTotal = 0f;
            slowScale = 1f;
            Scale = 1f;
            Paused = false;
            Time.timeScale = 1f;
        }

        /// <summary>Brief near-freeze that sells impacts.</summary>
        public static void HitStop(float duration, float scale = 0.04f)
        {
            if (duration > hitStopTimer) hitStopTimer = duration;
            hitStopScale = Mathf.Min(hitStopScale, scale);
        }

        /// <summary>Slow motion: hold at scale, then ease back to 1 over recover seconds.</summary>
        public static void SlowMo(float scale, float hold, float recover)
        {
            // a duo shares one world: slowing it down for one player would slow it for both
            if (Coop.Active) return;
            slowScale = scale;
            slowHold = hold;
            slowRecover = slowRecoverTotal = recover;
        }

        public static void Update(float unscaledDt)
        {
            if (Paused)
            {
                Scale = 0f;
                Time.timeScale = 0f;
                return;
            }
            float s = 1f;

            if (hitStopTimer > 0f)
            {
                hitStopTimer -= unscaledDt;
                s = Mathf.Min(s, hitStopScale);
                if (hitStopTimer <= 0f) hitStopScale = 1f;
            }

            if (slowHold > 0f)
            {
                slowHold -= unscaledDt;
                s = Mathf.Min(s, slowScale);
            }
            else if (slowRecover > 0f)
            {
                slowRecover -= unscaledDt;
                float t = 1f - Mathf.Clamp01(slowRecover / slowRecoverTotal);
                s = Mathf.Min(s, Mathf.Lerp(slowScale, 1f, MathUtil.EaseInOutSine(t)));
            }

            Scale = s;
            Time.timeScale = s * DevMode.Speed;
        }
    }
}
