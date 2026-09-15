using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SoccerFight
{
    /// <summary>Global URP volume built in code, plus short pulses for impacts and damage.</summary>
    public sealed class PostFx
    {
        Bloom bloom;
        Vignette vignette;
        ChromaticAberration chroma;
        LensDistortion lens;
        ColorAdjustments color;

        float impact;      // 0..1 decays
        float hurt;        // 0..1 decays
        float bloomBoost;  // additive bloom intensity
        float lowHealth;   // 0..1 steady state

        const float BaseBloom = 1.05f;
        const float BaseVignette = 0.3f;

        public void Init(Transform parent)
        {
            var go = new GameObject("PostFX Volume");
            go.transform.SetParent(parent, false);
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 100f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            vol.sharedProfile = profile;

            bloom = profile.Add<Bloom>(true);
            bloom.threshold.value = 0.92f;
            bloom.intensity.value = BaseBloom;
            bloom.scatter.value = 0.72f;
            bloom.clamp.value = 65000f;
            bloom.highQualityFiltering.value = true;
            bloom.tint.value = new Color(0.92f, 0.98f, 1f);

            vignette = profile.Add<Vignette>(true);
            vignette.intensity.value = BaseVignette;
            vignette.smoothness.value = 0.5f;
            vignette.color.value = new Color(0.01f, 0.02f, 0.05f);
            vignette.rounded.value = false;

            color = profile.Add<ColorAdjustments>(true);
            color.contrast.value = 10f;
            color.saturation.value = 10f;
            color.postExposure.value = 0.05f;

            chroma = profile.Add<ChromaticAberration>(true);
            chroma.intensity.value = 0f;

            lens = profile.Add<LensDistortion>(true);
            lens.intensity.value = 0f;
            lens.scale.value = 1f;

            var tonemap = profile.Add<Tonemapping>(true);
            tonemap.mode.value = TonemappingMode.Neutral;
        }

        public void Impact(float strength)
        {
            impact = Mathf.Clamp01(Mathf.Max(impact, strength));
            bloomBoost = Mathf.Max(bloomBoost, strength * 1.4f);
        }

        public void Hurt() => hurt = 1f;

        public void SetLowHealth(float amount) => lowHealth = amount;

        // ---- stage looks
        float themeVignette, themeVignetteShown, eclipse, darkness;
        Vector2 darkCenter = new Vector2(0.5f, 0.5f);

        public void SetTheme(float extraVignette, bool dark) { themeVignette = extraVignette; }

        /// <summary>Eclipse pulse (0..1): the picture reddens and closes in.</summary>
        public void SetEclipse(float k) => eclipse = Mathf.Clamp01(k);

        /// <summary>Darkness stage: a round window of sight centred on the player (viewport coords).</summary>
        public void SetDarkness(float k, Vector2 center) { darkness = k; darkCenter = center; }

        public void Update(float unscaledDt)
        {
            impact = Mathf.Max(0f, impact - unscaledDt * 3.2f);
            hurt = Mathf.Max(0f, hurt - unscaledDt * 2.2f);
            bloomBoost = Mathf.Max(0f, bloomBoost - unscaledDt * 4f);
            themeVignetteShown = Mathf.MoveTowards(themeVignetteShown, themeVignette, unscaledDt * 0.2f);

            // subtle: a hint of aberration sells the hit, too much just looks broken
            float e = impact * impact;
            float fringe = GameSettings.ChromaticAberration ? 1f : 0f;
            chroma.intensity.value = Mathf.Min(0.42f, e * 0.45f + hurt * 0.25f + eclipse * 0.12f) * fringe;
            lens.intensity.value = -0.12f * e * fringe;
            bloom.intensity.value = (BaseBloom + bloomBoost) * GameSettings.Bloom;

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
            float red = Mathf.Max(hurt, lowHealth * (0.35f + 0.25f * pulse));
            Color baseVig = Color.Lerp(new Color(0.01f, 0.02f, 0.05f), new Color(0.2f, 0f, 0.04f), eclipse);
            vignette.color.value = Color.Lerp(baseVig, new Color(0.55f, 0.02f, 0.08f), red);
            vignette.intensity.value = BaseVignette + themeVignetteShown + red * 0.18f + eclipse * 0.16f + darkness * 0.05f;
            vignette.center.value = Vector2.Lerp(new Vector2(0.5f, 0.5f), darkCenter, darkness);
            vignette.smoothness.value = Mathf.Lerp(0.5f, 0.62f, darkness);
            vignette.rounded.value = darkness > 0.5f;

            color.colorFilter.value = Color.Lerp(Color.white, new Color(1f, 0.8f, 0.84f), eclipse);
            color.saturation.value = 10f - 22f * eclipse;
            color.postExposure.value = 0.05f - 0.25f * eclipse;
        }
    }
}
