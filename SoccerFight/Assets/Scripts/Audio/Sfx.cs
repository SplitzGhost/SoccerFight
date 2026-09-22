using System;
using UnityEngine;

namespace SoccerFight
{
    public enum Sound { CoinDrop, CoinChime, CounterPop, Purchase, Equip, Unequip, Denied, Select }

    /// <summary>
    /// Sound effects, synthesised at startup like the rest of the game's assets: a coin that tinks
    /// on the turf, a bell-like chime when it reaches the counter, a soft pop of the counter, an
    /// arpeggio for a purchase, clicks for equipping and a low double buzz for "not possible".
    /// A small pool of AudioSources plays them; each sound has a minimum interval so a big coin
    /// shower never turns into noise.
    /// </summary>
    public sealed class Sfx
    {
        public static Sfx I { get; private set; }

        const int Rate = 44100;
        const int Voices = 12;

        AudioSource[] voices;
        int next;
        AudioClip[] clips;
        readonly float[] lastPlayed = new float[Enum.GetValues(typeof(Sound)).Length];
        static readonly float[] MinGap = { 0.035f, 0.03f, 0.04f, 0.2f, 0.06f, 0.06f, 0.15f, 0.1f };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { I = null; }

        public static void Build(Transform parent, Camera cam)
        {
            var s = new Sfx();
            s.Init(parent, cam);
            I = s;
        }

        void Init(Transform parent, Camera cam)
        {
            if (UnityEngine.Object.FindAnyObjectByType<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
            var go = new GameObject("Audio");
            go.transform.SetParent(parent, false);
            voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                var a = go.AddComponent<AudioSource>();
                a.playOnAwake = false;
                a.spatialBlend = 0f;
                a.ignoreListenerPause = true;
                voices[i] = a;
            }
            clips = new AudioClip[lastPlayed.Length];
            clips[(int)Sound.CoinDrop] = Make("CoinDrop", 0.26f, CoinDrop);
            clips[(int)Sound.CoinChime] = Make("CoinChime", 0.5f, CoinChime);
            clips[(int)Sound.CounterPop] = Make("CounterPop", 0.14f, CounterPop);
            clips[(int)Sound.Purchase] = Make("Purchase", 0.95f, Purchase);
            clips[(int)Sound.Equip] = Make("Equip", 0.18f, Equip);
            clips[(int)Sound.Unequip] = Make("Unequip", 0.16f, Unequip);
            clips[(int)Sound.Denied] = Make("Denied", 0.26f, Denied);
            clips[(int)Sound.Select] = Make("Select", 0.5f, Select);
            for (int i = 0; i < lastPlayed.Length; i++) lastPlayed[i] = -10f;
        }

        /// <summary>Plays a sound. pitch is a multiplier (a little random variation is added).</summary>
        public void PlayNow(Sound s, float volume = 1f, float pitch = 1f, float jitter = 0.03f)
        {
            float now = Time.unscaledTime;
            int i = (int)s;
            if (now - lastPlayed[i] < MinGap[i]) return;
            float master = GameSettings.Volume;
            if (master <= 0.001f || volume <= 0.001f) return;
            lastPlayed[i] = now;
            var v = voices[next];
            next = (next + 1) % voices.Length;
            v.Stop();
            v.clip = clips[i];
            v.volume = Mathf.Clamp01(volume * master);
            v.pitch = pitch * (1f + UnityEngine.Random.Range(-jitter, jitter));
            v.Play();
        }

        public static void Play(Sound s, float volume = 1f, float pitch = 1f) => I?.PlayNow(s, volume, pitch);

        // ------------------------------------------------------------------ synthesis

        static AudioClip Make(string name, float seconds, Func<float, float> wave)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            float peak = 1e-5f;
            for (int i = 0; i < n; i++)
            {
                data[i] = wave(i / (float)Rate);
                peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            }
            // normalise, and a few milliseconds of fade at the end so nothing clicks
            float g = 0.85f / peak;
            int tail = Mathf.Min(n, Rate / 200);
            for (int i = 0; i < n; i++)
            {
                float f = i >= n - tail ? (n - 1 - i) / (float)tail : 1f;
                data[i] *= g * f;
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        const float Tau = Mathf.PI * 2f;
        static float Sine(float f, float t) => Mathf.Sin(Tau * f * t);
        static float Env(float t, float attack, float decay) => t < attack ? t / attack : Mathf.Exp(-(t - attack) * decay);

        // deterministic noise, so every build sounds the same
        static float Noise(float t)
        {
            uint x = (uint)(t * Rate) * 747796405u + 2891336453u;
            x = ((x >> (int)((x >> 28) + 4u)) ^ x) * 277803737u;
            return ((x >> 22) ^ x) / (float)uint.MaxValue * 2f - 1f;
        }

        /// <summary>A bell: a few inharmonic partials, the higher ones dying faster.</summary>
        static float Bell(float f, float t, float decay)
        {
            if (t < 0f) return 0f;
            return Env(t, 0.002f, decay) * (Sine(f, t) + 0.45f * Sine(f * 2.01f, t) * Mathf.Exp(-t * decay * 0.8f)
                   + 0.2f * Sine(f * 3.03f, t) * Mathf.Exp(-t * decay * 1.6f));
        }

        // metallic tink of a coin landing on the turf
        static float CoinDrop(float t)
            => Env(t, 0.001f, 16f) * (0.6f * Sine(2350f, t) + 0.35f * Sine(3525f, t) * Mathf.Exp(-t * 10f) + 0.22f * Sine(5870f, t) * Mathf.Exp(-t * 22f))
               + (t < 0.004f ? Noise(t) * 0.5f * (1f - t / 0.004f) : 0f);

        // bright chime when a coin reaches the counter (pitched up along a combo)
        static float CoinChime(float t) => Bell(1568f, t, 7f) + 0.5f * Bell(2349f, t - 0.035f, 9f);

        // soft tock of the counter popping
        static float CounterPop(float t)
        {
            float f = Mathf.Lerp(560f, 360f, Mathf.Clamp01(t / 0.08f));
            return Env(t, 0.002f, 32f) * Sine(f, t) + (t < 0.003f ? Noise(t) * 0.3f : 0f);
        }

        static readonly float[] notes = { 1047f, 1319f, 1568f, 2093f };

        // rising arpeggio with a shimmer on top
        static float Purchase(float t)
        {
            float v = 0f;
            for (int i = 0; i < notes.Length; i++) v += Bell(notes[i], t - i * 0.075f, i == notes.Length - 1 ? 4.5f : 8f) * (i == notes.Length - 1 ? 1f : 0.7f);
            v += 0.12f * Noise(t) * Env(t - 0.3f, 0.05f, 6f) * (t > 0.3f ? 1f : 0f);
            return v;
        }

        // a clack and an upward blip
        static float Equip(float t)
        {
            float f = Mathf.Lerp(660f, 990f, Mathf.Clamp01(t / 0.05f));
            return 0.7f * Env(t, 0.002f, 26f) * Sine(f, t) + 0.5f * Env(t, 0.001f, 60f) * Sine(220f, t) + (t < 0.004f ? Noise(t) * 0.4f : 0f);
        }

        static float Unequip(float t)
        {
            float f = Mathf.Lerp(880f, 500f, Mathf.Clamp01(t / 0.07f));
            return 0.7f * Env(t, 0.002f, 24f) * Sine(f, t) + (t < 0.003f ? Noise(t) * 0.3f : 0f);
        }

        // two low, soft buzzes: "not possible"
        static float Denied(float t)
        {
            float gate = t < 0.08f ? 1f : t > 0.13f && t < 0.21f ? 1f : 0f;
            float local = t < 0.1f ? t : t - 0.13f;
            float edge = Mathf.Clamp01(local / 0.006f) * Mathf.Clamp01((0.08f - local) / 0.01f);
            float w = Sine(150f, t) + 0.35f * Sine(300f, t) + 0.15f * Sine(450f, t);
            return gate * edge * w;
        }

        // a card is chosen: a small airy whoosh into a two-note chime
        static float Select(float t)
            => 0.25f * Noise(t) * Env(t, 0.04f, 18f) + Bell(1175f, t - 0.02f, 7f) + 0.6f * Bell(1760f, t - 0.07f, 8f);
    }
}
