using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Gives every stage its own look without regenerating the world: all environment renderers get
    /// graded material clones, and one global colour matrix (hue rotation, saturation, brightness,
    /// contrast, tint) re-lights the whole backdrop. Player, ball, monsters and effects keep their
    /// true colours. Stage changes crossfade the matrix. Also runs the stage's weather particles.
    /// </summary>
    public sealed class ThemeGrade
    {
        static readonly int GradeId = Shader.PropertyToID("_SF_EnvGrade");
        static readonly int GradedId = Shader.PropertyToID("_EnvGraded");

        readonly Dictionary<Material, Material> clones = new Dictionary<Material, Material>();
        Matrix4x4 from = Matrix4x4.identity, to = Matrix4x4.identity, cur = Matrix4x4.identity;
        float t = 1f, dur;
        StageTheme theme;
        CameraRig cam;
        float emit;
        SpriteRenderer ballLight;
        float darkness;

        public StageTheme Theme => theme;

        public void Build(WorldEnvironment env, CameraRig cameraRig, Transform parent)
        {
            cam = cameraRig;
            Shader.SetGlobalMatrix(GradeId, Matrix4x4.identity);
            Adopt(env.Root, new HashSet<Transform> { env.LeftPortal, env.RightPortal });

            ballLight = Art.MakeSprite("Ball Light", parent, Art.SoftGlow, -40, Art.SpriteGlowMat, Color.clear);
            ballLight.transform.localScale = Vector3.one * 7f;
        }

        /// <summary>Switch every environment renderer under root to its graded material (new platforms after a stage change).</summary>
        public void Adopt(Transform root, HashSet<Transform> skip = null)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (skip != null && UnderAny(r.transform, skip)) continue;
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null || !m.HasProperty(GradedId) || clones.ContainsValue(m)) continue;
                    if (!clones.TryGetValue(m, out var c))
                    {
                        c = new Material(m) { name = m.name + " (Graded)" };
                        c.SetFloat(GradedId, 1f);
                        clones[m] = c;
                    }
                    mats[i] = c;
                    changed = true;
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        static bool UnderAny(Transform t, HashSet<Transform> set)
        {
            for (var p = t; p != null; p = p.parent) if (set.Contains(p)) return true;
            return false;
        }

        // Jede Stage hat ihre eigene Grafik aus ihrem Design-Bogen und damit schon ihre Farben: der Farbfilter
        // bleibt neutral (die Werte in StageTheme wirken nicht mehr auf die Welt).
        const float HueAmount = 0f, SatAmount = 0f, LightAmount = 0f, TintAmount = 0f;

        /// <summary>Colour matrix for a theme: tint · contrast · brightness · saturation · hue.</summary>
        public static Matrix4x4 Matrix(StageTheme th)
        {
            float a = th.Hue * HueAmount * Mathf.Deg2Rad, cos = Mathf.Cos(a), sin = Mathf.Sin(a);
            float k = (1f - cos) / 3f, q = Mathf.Sqrt(1f / 3f) * sin;
            var hue = Matrix4x4.identity;
            hue.m00 = cos + k; hue.m01 = k - q; hue.m02 = k + q;
            hue.m10 = k + q; hue.m11 = cos + k; hue.m12 = k - q;
            hue.m20 = k - q; hue.m21 = k + q; hue.m22 = cos + k;

            const float lr = 0.2126f, lg = 0.7152f, lb = 0.0722f;
            float s = Mathf.Lerp(1f, th.Saturation, SatAmount);
            var sat = Matrix4x4.identity;
            sat.m00 = (1f - s) * lr + s; sat.m01 = (1f - s) * lg;     sat.m02 = (1f - s) * lb;
            sat.m10 = (1f - s) * lr;     sat.m11 = (1f - s) * lg + s; sat.m12 = (1f - s) * lb;
            sat.m20 = (1f - s) * lr;     sat.m21 = (1f - s) * lg;     sat.m22 = (1f - s) * lb + s;

            // contrast pivots on a dark mid-tone (the scene lives in the shadows)
            float c = Mathf.Lerp(1f, th.Contrast, LightAmount), b = Mathf.Lerp(1f, th.Brightness, LightAmount);
            const float pivot = 0.12f;
            var m = sat * hue;
            var result = Matrix4x4.identity;
            for (int r = 0; r < 3; r++)
            {
                float tint = Mathf.Lerp(1f, th.Tint[r], TintAmount);
                for (int col = 0; col < 3; col++) result[r, col] = m[r, col] * b * c * tint;
                result[r, 3] = (1f - c) * pivot * tint;
            }
            return result;
        }

        static Matrix4x4 Lerp(Matrix4x4 a, Matrix4x4 b, float k)
        {
            var r = new Matrix4x4();
            for (int i = 0; i < 16; i++) r[i] = Mathf.LerpUnclamped(a[i], b[i], k);
            return r;
        }

        public void Transition(StageTheme next, float seconds)
        {
            theme = next;
            from = cur;
            to = Matrix(next);
            dur = seconds;
            t = 0f;
            if (seconds <= 0f) { cur = to; t = 1f; Shader.SetGlobalMatrix(GradeId, cur); }
            Game.I.Post.SetTheme(next.Vignette, next.Mechanic == StageMechanic.Darkness);
        }

        public void Update(float dt, Player player, Ball ball)
        {
            if (t < 1f)
            {
                t = Mathf.Min(1f, t + dt / Mathf.Max(0.01f, dur));
                cur = Lerp(from, to, MathUtil.Smooth01(t));
                Shader.SetGlobalMatrix(GradeId, cur);
            }
            if (theme == null) return;

            // darkness: the ball carries a pool of light, the view closes in around the player
            bool dark = theme.Mechanic == StageMechanic.Darkness;
            darkness = Mathf.MoveTowards(darkness, dark ? 1f : 0f, dt * 0.8f);
            ballLight.color = new Color(0.6f, 0.95f, 1f, 0.16f * darkness);
            ballLight.transform.position = new Vector3(ball.Pos.x, ball.Pos.y, 0f);
            ballLight.enabled = darkness > 0.001f;
            if (darkness > 0f)
            {
                Vector3 vp = cam.Cam.WorldToViewportPoint(new Vector3(player.Pos.x, player.Pos.y + 1f, 0f));
                Game.I.Post.SetDarkness(darkness, new Vector2(vp.x, vp.y));
            }
            else Game.I.Post.SetDarkness(0f, new Vector2(0.5f, 0.5f));

            EmitWeather(dt);
        }

        // ------------------------------------------------------------------ weather

        void EmitWeather(float dt)
        {
            var fx = FxSystem.I;
            if (fx == null) return;
            Rect v = cam.ViewRect;
            Color c = theme.AmbientColor;
            float wind = StageMechanics.I != null ? StageMechanics.I.Wind : 0f;
            float rate;
            switch (theme.Weather)
            {
                case Weather.Leaves: rate = 14f; break;
                case Weather.Rain: rate = 110f; break;
                case Weather.Spores: rate = 12f; break;
                case Weather.Embers: rate = 22f; break;
                case Weather.Snow: rate = 38f; break;
                case Weather.Stars: rate = 10f; break;
                case Weather.Ash: rate = 20f; break;
                default: return;   // fireflies: the world already has them
            }
            emit += dt * rate;
            while (emit >= 1f)
            {
                emit -= 1f;
                float x = v.xMin - 2f + Random.value * (v.width + 4f);
                float yTop = v.yMax + 0.5f;
                FxLayer layer = Random.value < 0.35f ? FxLayer.Front : FxLayer.Back;
                switch (theme.Weather)
                {
                    case Weather.Leaves:
                    {
                        Color lc = Color.Lerp(c, new Color(1f, 0.82f, 0.35f), Random.value * 0.6f);
                        float s = Random.Range(0.1f, 0.2f);
                        fx.Spawn(layer, false, Art.CellShard, new Vector2(x, yTop), new Vector2(Random.Range(-1.5f, 0.5f) + wind * 0.3f, Random.Range(-1.6f, -0.9f)),
                            Random.Range(5f, 8f), s, s * 0.8f, lc, lc.WithAlpha(0f), 1f, 0.1f, 0.05f, Random.Range(0f, 360f), Random.Range(-160f, 160f), false, true);
                        break;
                    }
                    case Weather.Rain:
                    {
                        Vector2 vel = new Vector2(-4f + wind * 0.4f, -22f);
                        fx.Streak(layer, new Vector2(x + 3f, yTop + Random.value * 2f), vel, Random.Range(0.5f, 0.8f), layer == FxLayer.Front ? 0.03f : 0.022f, 0.022f,
                            c.WithAlpha(layer == FxLayer.Front ? 0.55f : 0.4f), c.WithAlpha(0.15f), 1.3f, 0f);
                        if (Random.value < 0.25f)
                        {
                            float gx = v.xMin + Random.value * v.width;
                            float floor = Level.FloorBelow(gx, v.yMax);
                            if (floor > v.yMin)
                                fx.Sparks(new Vector2(gx, floor + 0.02f), Vector2.up, 70f, 2, 1.2f, 2.4f, c.WithAlpha(0.6f), 1.2f, 0.018f, 0.2f, 8f);
                        }
                        break;
                    }
                    case Weather.Spores:
                    {
                        Vector2 p = new Vector2(x, v.yMin + Random.value * v.height);
                        float s = Random.Range(0.05f, 0.12f);
                        fx.Spawn(layer, true, Art.CellGlow, p, new Vector2(Random.Range(-0.2f, 0.2f), Random.Range(0.1f, 0.35f)),
                            Random.Range(3f, 6f), s, s * 1.4f, c.WithAlpha(0.8f), c.WithAlpha(0f), 2f, 0.2f, -0.02f, 0f, 0f, false, true);
                        break;
                    }
                    case Weather.Embers:
                    {
                        Vector2 p = new Vector2(x, v.yMin + Random.value * 1.5f);
                        Color ec = Color.Lerp(c, new Color(1f, 0.9f, 0.4f), Random.value * 0.5f);
                        fx.Spawn(layer, true, Art.CellDot, p, new Vector2(Random.Range(-0.4f, 0.4f) + wind * 0.2f, Random.Range(1.2f, 2.6f)),
                            Random.Range(2.2f, 4f), Random.Range(0.04f, 0.08f), 0.01f, ec, ec.WithAlpha(0f), 3f, 0.3f, -0.1f, 0f, 0f, false, true);
                        break;
                    }
                    case Weather.Snow:
                    {
                        float s = Random.Range(0.04f, 0.09f);
                        fx.Spawn(layer, false, Art.CellDot, new Vector2(x, yTop), new Vector2(Random.Range(-0.6f, 0.2f) + wind * 0.3f, Random.Range(-1.4f, -0.7f)),
                            Random.Range(6f, 9f), s, s, c.WithAlpha(0.85f), c.WithAlpha(0f), 1f, 0f, 0f, 0f, 0f, false, true);
                        break;
                    }
                    case Weather.Stars:
                    {
                        Vector2 p = new Vector2(x, v.yMin + 1f + Random.value * (v.height - 1f));
                        float s = Random.Range(0.1f, 0.22f);
                        fx.Spawn(FxLayer.Back, true, Art.CellSparkle, p, new Vector2(0f, 0.05f), Random.Range(1.5f, 3f), s, 0f,
                            c, c.WithAlpha(0f), 2.6f, 0f, 0f, Random.Range(0f, 90f), Random.Range(-30f, 30f), false, true);
                        break;
                    }
                    case Weather.Ash:
                    {
                        float s = Random.Range(0.06f, 0.14f);
                        bool ember = Random.value < 0.2f;
                        Color ac = ember ? theme.Accent : c.WithAlpha(0.5f);
                        fx.Spawn(layer, ember, ember ? Art.CellDot : Art.CellPuff, new Vector2(x, yTop), new Vector2(Random.Range(-0.5f, 0.3f), Random.Range(-1f, -0.5f)),
                            Random.Range(6f, 9f), s, s * 1.2f, ac, ac.WithAlpha(0f), ember ? 2.4f : 1f, 0f, 0f, Random.Range(0f, 360f), Random.Range(-40f, 40f), false, true);
                        break;
                    }
                }
            }
        }
    }
}
