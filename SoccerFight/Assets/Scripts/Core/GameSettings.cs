using UnityEngine;

namespace SoccerFight
{
    /// <summary>Player-facing options, persisted in PlayerPrefs and applied immediately.</summary>
    public static class GameSettings
    {
        public static bool Fullscreen = true;
        public static bool VSync = true;
        public static bool ShowFps = true;
        public static bool ChromaticAberration = true;
        public static float ScreenShake = 1f;
        public static float Bloom = 1f;

        static bool loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { loaded = false; }

        public static void Load()
        {
            if (loaded) return;
            loaded = true;
            Fullscreen = PlayerPrefs.GetInt("sf_fullscreen", 1) == 1;
            VSync = PlayerPrefs.GetInt("sf_vsync", 1) == 1;
            ShowFps = PlayerPrefs.GetInt("sf_fps", 1) == 1;
            ChromaticAberration = PlayerPrefs.GetInt("sf_chroma", 1) == 1;
            ScreenShake = PlayerPrefs.GetFloat("sf_shake", 1f);
            Bloom = PlayerPrefs.GetFloat("sf_bloom", 1f);
        }

        public static void Save()
        {
            PlayerPrefs.SetInt("sf_fullscreen", Fullscreen ? 1 : 0);
            PlayerPrefs.SetInt("sf_vsync", VSync ? 1 : 0);
            PlayerPrefs.SetInt("sf_fps", ShowFps ? 1 : 0);
            PlayerPrefs.SetInt("sf_chroma", ChromaticAberration ? 1 : 0);
            PlayerPrefs.SetFloat("sf_shake", ScreenShake);
            PlayerPrefs.SetFloat("sf_bloom", Bloom);
            PlayerPrefs.Save();
        }

        public static void Apply()
        {
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            Application.targetFrameRate = -1;
            // The editor's Game view can't go fullscreen; only builds react to this.
            if (!Application.isEditor)
            {
                var mode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
                if (Screen.fullScreenMode != mode) Screen.fullScreenMode = mode;
            }
        }
    }
}
