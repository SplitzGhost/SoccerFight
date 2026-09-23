using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Procedural backdrop, generated on worker threads (see ArtJobs): a blue day sky with a cartoon
    /// sun and puffy clouds (stars for the night stages), two mountain ranges with a waterfall, trees, a ruined
    /// stadium arcade built from individual stone blocks, voussoirs and fluted columns, ivy, rubble,
    /// a mown pitch and an earth cross-section with roots. Anchor lists tell WorldEnvironment where
    /// to hang lanterns, banners, ivy and canopies.
    /// </summary>
    public static class EnvironmentArt
    {
        public static Sprite Sky, Stars, Moon, Far, Mid, Ruins, Bushes, PitchTile, EarthTile, FogBand, Goal, Lantern, Chain;
        public static Sprite[] Clouds, BatFrames, Leaves;
        public static Texture2D WaterfallTex;
        public static Vector2 WaterfallTop;
        public static float WaterfallLength;

        public static readonly List<Vector2> LanternSpots = new List<Vector2>();
        public static readonly List<Vector2> ArchSpots = new List<Vector2>();
        public static readonly List<Vector2> PierTops = new List<Vector2>();
        public static readonly List<Vector2> IvyAnchors = new List<Vector2>();
        public static readonly List<Vector3> Canopies = new List<Vector3>();
        public static readonly List<Vector2> MossAnchors = new List<Vector2>();

        public const float FarW = 34f, FarH = 3.8f;
        public const float MidW = 36f, MidH = 12f;
        public const float RuinW = 40f, RuinH = 7.2f;
        public const float BushW = 44f, BushH = 2.4f;
        public const float PitchTileW = 4f, PitchH = 0.95f, PitchTop = 0.36f;
        public const float EarthTileW = 4f, EarthH = 3.6f;
        const float WaterfallX = -7f;

        static ArtJobs jobs;
        static ArtJobs.Job jSky, jStars, jMoon, jFar, jMid, jRuins, jBushes, jPitch, jEarth, jFog, jGoal, jLantern, jChain, jWater;
        static ArtJobs.Job[] jClouds, jBats, jLeaves;

        // ---------------------------------------------------------------- async orchestration

        /// <summary>Kick off all backdrop generation on worker threads.</summary>
        public static void Begin()
        {
            // Palette parses hex colors through a Unity API — initialize it on the main thread.
            _ = Palette.Skin;
            LanternSpots.Clear(); ArchSpots.Clear(); PierTops.Clear(); IvyAnchors.Clear(); Canopies.Clear(); MossAnchors.Clear();

            jobs = new ArtJobs();
            jSky = jobs.Add("Sky", BuildSky, Vector2.zero);
            jStars = jobs.Add("Stars", BuildStars, Vector2.zero);
            jMoon = jobs.Add("Moon", BuildMoon, Vector2.zero);
            jClouds = new ArtJobs.Job[4];
            for (int i = 0; i < jClouds.Length; i++) { int k = i; jClouds[i] = jobs.Add("Cloud" + k, () => BuildCloud(k), Vector2.zero); }
            jFar = jobs.Add("Far", BuildFar, Vector2.zero);
            jWater = jobs.Add("Waterfall", BuildWaterfall, Vector2.zero, true, TextureWrapMode.Repeat);
            jWater.MakeSprite = false;
            jMid = jobs.Add("Trees", BuildMid, Vector2.zero);
            jRuins = jobs.Add("Ruins", BuildRuins, Vector2.zero);
            jBushes = jobs.Add("Bushes", BuildBushes, Vector2.zero);
            jPitch = jobs.Add("Pitch", BuildPitch, Vector2.zero, true, TextureWrapMode.Repeat);
            jEarth = jobs.Add("Earth", BuildEarth, Vector2.zero, true, TextureWrapMode.Repeat);
            jFog = jobs.Add("Fog", BuildFog, Vector2.zero, true, TextureWrapMode.Repeat);
            jGoal = jobs.Add("Goal", BuildGoal, Vector2.zero, false);
            jLantern = jobs.Add("Lantern", BuildLantern, Vector2.zero, false);
            jChain = jobs.Add("Chain", BuildChain, Vector2.zero, false);
            jBats = new ArtJobs.Job[3];
            for (int i = 0; i < 3; i++) { int k = i; jBats[i] = jobs.Add("Bat" + k, () => BuildBat(k), Vector2.zero, false); }
            jLeaves = new ArtJobs.Job[2];
            for (int i = 0; i < 2; i++) { int k = i; jLeaves[i] = jobs.Add("Leaf" + k, () => BuildLeaf(k), Vector2.zero, false); }
            jobs.Start();
            DepthArt.Begin();
            FoliageArt.Begin();
        }

        /// <summary>Wait for the workers and upload textures (main thread).</summary>
        public static void End()
        {
            jobs.Complete();
            DepthArt.End();
            FoliageArt.End();
            Sky = jSky.Sprite; Stars = jStars.Sprite; Moon = jMoon.Sprite; Far = jFar.Sprite; Mid = jMid.Sprite;
            Ruins = jRuins.Sprite; Bushes = jBushes.Sprite; PitchTile = jPitch.Sprite; EarthTile = jEarth.Sprite;
            FogBand = jFog.Sprite; Goal = jGoal.Sprite; Lantern = jLantern.Sprite; Chain = jChain.Sprite;
            WaterfallTex = jWater.Texture;
            Clouds = new Sprite[jClouds.Length];
            for (int i = 0; i < Clouds.Length; i++) Clouds[i] = jClouds[i].Sprite;
            BatFrames = new Sprite[jBats.Length];
            for (int i = 0; i < BatFrames.Length; i++) BatFrames[i] = jBats[i].Sprite;
            Leaves = new Sprite[jLeaves.Length];
            for (int i = 0; i < Leaves.Length; i++) Leaves[i] = jLeaves[i].Sprite;
            jobs = null;
        }

        // ---------------------------------------------------------------- noise helpers (thread-safe)

        internal static float Fbm(float x, float seed, int octaves = 4)
        {
            float sum = 0f, amp = 0.5f, freq = 1f;
            for (int i = 0; i < octaves; i++)
            {
                sum += amp * (Noise.Perlin(x * freq + seed * 17.13f, seed * 3.7f + i * 11.1f) - 0.5f) * 2f;
                amp *= 0.5f;
                freq *= 2.03f;
            }
            return sum;
        }

        internal static float Fbm2(Vector2 p, float seed, int octaves = 4)
        {
            float sum = 0f, amp = 0.5f, freq = 1f;
            for (int i = 0; i < octaves; i++)
            {
                sum += amp * (Noise.Perlin(p.x * freq + seed * 13.7f, p.y * freq + seed * 5.3f + i * 7.7f) - 0.5f) * 2f;
                amp *= 0.5f;
                freq *= 2.07f;
            }
            return sum;
        }

        /// <summary>Noise that tiles seamlessly in x over [0, period].</summary>
        internal static float PeriodicX(float x, float y, float period)
            => Mathf.Lerp(Noise.Perlin(x, y), Noise.Perlin(x - period, y), MathUtil.Smooth01(x / period));

        static float PeriodicY(float x, float y, float period)
            => Mathf.Lerp(Noise.Perlin(x, y), Noise.Perlin(x, y - period), MathUtil.Smooth01(y / period));

        internal static float EdgeFade(float x, float halfW, float fade = 1.5f) => MathUtil.Smooth01((halfW - Mathf.Abs(x)) / fade);

        internal static float Sq(float v) => v * v;

        // ---------------------------------------------------------------- sky, stars, moon, clouds

        static SdfCanvas BuildSky()
        {
            // 0.25 x 16 units; the environment stretches it horizontally.
            var c = new SdfCanvas(new Rect(-0.125f, 0f, 0.25f, 16f), 32f);
            c.Field(p =>
            {
                // eye level sits at the bottom of the sprite and the top of the screen at about 0.42:
                // pale near the horizon, then quickly a deep, saturated cartoon blue
                float h = MathUtil.Smooth01(p.y / 16f / 0.42f);
                return h < 0.3f
                    ? Color.Lerp(Palette.SkyHorizon, Palette.SkyMid, MathUtil.Smooth01(h / 0.3f))
                    : Color.Lerp(Palette.SkyMid, Palette.SkyTop, MathUtil.Smooth01((h - 0.3f) / 0.7f));
            });
            return c;
        }

        static SdfCanvas BuildStars()
        {
            var c = new SdfCanvas(new Rect(-16f, 0f, 32f, 9f), 44f);
            // faint milky way band
            c.Fill(p => -1f, p =>
            {
                float d = p.y - (4.7f + 0.13f * p.x);
                float band = Mathf.Exp(-d * d / 1.7f);
                float n = Fbm2(p * 0.45f, 3f);
                return new Color(0.42f, 0.66f, 0.84f, Mathf.Clamp01(band * (0.45f + n) * 0.17f));
            });
            var r = new System.Random(7);
            float R() => (float)r.NextDouble();
            for (int i = 0; i < 360; i++)
            {
                float a = Mathf.Pow(R(), 2.4f);
                Vector2 sp = new Vector2(R() * 32f - 16f, R() * 9f);
                float rad = 0.009f + a * 0.028f;
                Color col = Color.Lerp(new Color(0.78f, 0.9f, 1f), new Color(1f, 0.93f, 0.82f), R() * R());
                col.a = 0.3f + a * 0.7f;
                c.Fill(p => Sdf.Circle(p, sp, rad), col, 0.02f, new Rect(sp.x - 0.15f, sp.y - 0.15f, 0.3f, 0.3f));
                if (a > 0.55f)
                {
                    Color halo = col; halo.a = 0.12f;
                    c.Fill(p => Sdf.Circle(p, sp, rad * 3.5f), halo, 0.1f, new Rect(sp.x - 0.4f, sp.y - 0.4f, 0.8f, 0.8f));
                }
            }
            return c;
        }

        static SdfCanvas BuildMoon() => SunCanvas(220f);

        /// <summary>The cartoon sun: a creamy disc, warmer towards the rim, with a soft bright core.</summary>
        internal static SdfCanvas SunCanvas(float ppu)
        {
            const float Rs = 0.55f;
            var c = new SdfCanvas(new Rect(-0.62f, -0.62f, 1.24f, 1.24f), ppu);
            c.Fill(p => Sdf.Circle(p, Vector2.zero, Rs), p =>
            {
                float r = p.magnitude / Rs;
                Color col = Color.Lerp(new Color(1f, 1f, 0.94f), new Color(1f, 0.9f, 0.62f), MathUtil.Smooth01((r - 0.35f) / 0.65f));
                col.a = 1f;
                return col;
            });
            return c;
        }

        static SdfCanvas BuildCloud(int k)
        {
            var r = new System.Random(71 + k * 13);
            float R() => (float)r.NextDouble();
            // big cartoon cumulus: round puffs on a flat base, bright on top, lavender underneath
            var c = new SdfCanvas(new Rect(-4f, -1.2f, 8f, 3.1f), 40f);
            var blobs = new List<(Vector2 c, float r)>();
            int n = 6 + r.Next(3);
            for (int i = 0; i < n; i++)
            {
                float t = (i + 0.5f) / n;
                float rad = (0.55f + 0.55f * Mathf.Sin(t * Mathf.PI)) * (0.85f + R() * 0.35f);
                blobs.Add((new Vector2(Mathf.Lerp(-2.8f, 2.8f, t) + (R() - 0.5f) * 0.4f, -0.45f + rad * 0.8f + (R() - 0.5f) * 0.15f), rad));
            }
            float seed = k * 3.7f;
            SdfCanvas.SdfFn shape = p =>
            {
                float d = 9f;
                for (int i = 0; i < blobs.Count; i++) d = Sdf.SmoothUnion(d, Sdf.Circle(p, blobs[i].c, blobs[i].r), 0.18f);
                d += Fbm2(p * 1.1f, seed) * 0.05f;
                return Mathf.Max(d, -(p.y + 0.5f));
            };
            c.Fill(shape, p =>
            {
                // each puff is lit from the upper right: a soft round shade per blob, cool at the base
                float lit = 0f;
                for (int i = 0; i < blobs.Count; i++)
                {
                    Vector2 q = (p - blobs[i].c) / blobs[i].r;
                    if (q.sqrMagnitude > 1.3f) continue;
                    lit = Mathf.Max(lit, Mathf.Clamp01(0.55f + 0.5f * Vector2.Dot(q, new Vector2(0.45f, 0.75f))));
                }
                float up = MathUtil.Smooth01((p.y + 0.5f) / 0.9f);
                Color col = Color.Lerp(Palette.CloudShade, Palette.CloudLight, Mathf.Clamp01(lit * 0.75f + up * 0.35f));
                col.a = 1f;
                return col;
            }, 0.05f);
            c.RimLight(new Vector2(0.05f, 0.07f), new Color(1f, 0.98f, 0.9f), 0.6f);
            return c;
        }

        // ---------------------------------------------------------------- far mountains + waterfall

        static SdfCanvas BuildFar()
        {
            // two ranges standing on the horizon (local y = 0 sits at eye level in the scene)
            var c = new SdfCanvas(new Rect(-FarW * 0.5f, 0f, FarW, FarH), 60f);
            float aa = 1f / c.Ppu;
            int W = c.Width;
            float left = c.UnitRect.xMin, ppu = c.Ppu;
            var back = new float[W];
            var front = new float[W];
            var slope = new float[W];
            for (int x = 0; x < W; x++)
            {
                float ux = left + (x + 0.5f) / ppu;
                back[x] = 1.9f + 1f * Fbm(ux * 0.07f, 11f) + 0.45f * Mathf.Abs(Fbm(ux * 0.25f, 12f));
                front[x] = 1.05f + 0.75f * Fbm(ux * 0.1f, 1.3f) + 0.35f * Mathf.Abs(Fbm(ux * 0.4f, 4.1f)) + 0.8f * Mathf.Exp(-Sq((ux - WaterfallX) / 1.3f));
            }
            for (int x = 0; x < W; x++)
                slope[x] = (front[Mathf.Min(W - 1, x + 2)] - front[Mathf.Max(0, x - 2)]) / (4f / ppu);

            Color backLow = Color.Lerp(Palette.FarBottom, Palette.Fog, 0.4f), backHigh = Color.Lerp(Palette.FarTop, Palette.Fog, 0.18f);
            Color frontLow = Color.Lerp(Palette.FarBottom, Palette.Fog, 0.16f), frontHigh = Palette.FarTop * 0.82f;
            c.Field(p =>
            {
                int x = Mathf.Clamp((int)((p.x - left) * ppu), 0, W - 1);
                float aB = Mathf.Clamp01(0.5f + (back[x] - p.y) / aa);
                float aF = Mathf.Clamp01(0.5f + (front[x] - p.y) / aa);
                if (aB <= 0f && aF <= 0f) return new Color(0, 0, 0, 0);
                Color b = Color.Lerp(backLow, backHigh, MathUtil.Smooth01(p.y / back[x]));
                // front range: lit facets face the moon (upper right), erosion gullies, misty base
                float lit = Mathf.Clamp(-slope[x] * 0.7f, -0.5f, 0.7f);
                float gully = Noise.Perlin(p.x * 5.5f, p.y * 0.9f);
                float strata = Noise.Perlin(p.x * 0.8f, p.y * 4.5f);
                Color f = Color.Lerp(frontLow, frontHigh, MathUtil.Smooth01(p.y / front[x]));
                f *= 1f + lit * 0.32f * MathUtil.Smooth01((p.y - 0.15f) / 0.8f);
                f *= 0.96f + 0.07f * gully;
                f *= 0.97f + 0.05f * strata;
                // moonlit snowless crests: a thin bright edge right under the ridge line
                f = Color.Lerp(f, Color.Lerp(Palette.Fog, Color.white, 0.2f), Mathf.Clamp01(1f - (front[x] - p.y) / 0.08f) * Mathf.Clamp01(lit + 0.2f) * 0.35f);
                b *= 0.95f + 0.1f * Noise.Perlin(p.x * 2f + 30f, p.y * 1.2f);
                Color col = Color.Lerp(b, f, aF);
                col.a = Mathf.Max(aB, aF) * EdgeFade(p.x, FarW * 0.5f);
                return col;
            });

            var r = new System.Random(19);
            float R() => (float)r.NextDouble();
            Color pine = Color.Lerp(Palette.FarTop, new Color(0.05f, 0.16f, 0.21f), 0.45f);
            for (int i = 0; i < 170; i++)
            {
                float x0 = -FarW * 0.5f + 0.6f + R() * (FarW - 1.2f);
                float h = 0.16f + R() * 0.36f, w = h * 0.27f, by = 0.04f + R() * 0.16f;
                SdfCanvas.ColorFn col = p =>
                {
                    Color k = Color.Lerp(Color.Lerp(pine, Palette.Fog, 0.38f), pine, MathUtil.Smooth01((p.y - by) / (h * 0.8f)));
                    k.a = EdgeFade(p.x, FarW * 0.5f);
                    return k;
                };
                c.Fill(p => Mathf.Min(
                        Sdf.Triangle(p, new Vector2(x0 - w, by), new Vector2(x0 + w, by), new Vector2(x0, by + h)),
                        Sdf.Triangle(p, new Vector2(x0 - w * 0.72f, by + h * 0.38f), new Vector2(x0 + w * 0.72f, by + h * 0.38f), new Vector2(x0, by + h * 1.13f))),
                    col, 0f, new Rect(x0 - w - 0.05f, by - 0.05f, w * 2f + 0.1f, h * 1.2f + 0.1f));
            }
            c.RimLight(new Vector2(0.04f, 0.05f), Color.Lerp(Palette.Fog, Color.white, 0.35f), 0.55f);

            int wi = Mathf.Clamp((int)((WaterfallX - left) * ppu), 0, W - 1);
            WaterfallTop = new Vector2(WaterfallX, front[wi] - 0.3f);
            WaterfallLength = WaterfallTop.y - 0.45f;
            return c;
        }

        static SdfCanvas BuildWaterfall()
        {
            var c = new SdfCanvas(new Rect(-0.5f, 0f, 1f, 4f), 48f);
            c.Field(p =>
            {
                float across = Mathf.Clamp01(1f - Mathf.Pow(Mathf.Abs(p.x) / 0.5f, 3f));
                float n = PeriodicY(p.x * 9f, p.y * 2.2f, 8.8f);
                float n2 = PeriodicY(p.x * 23f + 3f, p.y * 5.5f, 22f);
                float streak = MathUtil.Smooth01((n - 0.36f) / 0.26f) * 0.7f + n2 * 0.3f;
                Color col = Color.Lerp(new Color(0.55f, 0.84f, 0.98f), new Color(0.96f, 1f, 1f), streak);
                col.a = across * (0.3f + 0.6f * streak);
                return col;
            });
            return c;
        }

        // ---------------------------------------------------------------- trees (mid layer)

        internal static Color BarkColor(Vector2 p)
        {
            float n = Noise.Perlin(p.x * 9f, p.y * 1.1f);
            float n2 = Noise.Perlin(p.x * 23f + 5f, p.y * 3f);
            float groove = MathUtil.Smooth01((0.36f - n) / 0.07f);
            Color c = Color.Lerp(Palette.Bark * 0.85f, Palette.Bark * 1.25f, n2);
            c = Color.Lerp(c, Palette.Bark * 0.55f, groove * 0.7f);
            c.a = 1f;
            return c;
        }

        static SdfCanvas BuildMid()
        {
            var c = new SdfCanvas(new Rect(-MidW * 0.5f, 0f, MidW, MidH), 60f);
            var r = new System.Random(21);
            float R() => (float)r.NextDouble();
            SdfCanvas.ColorFn bark = BarkColor;

            float x = -MidW * 0.5f + 2.4f;
            while (x < MidW * 0.5f - 2.2f)
            {
                float x0 = x + (R() - 0.5f) * 1.2f;
                float s = 0.8f + R() * 0.5f;
                float b1 = (R() - 0.5f) * 1.2f, b2 = (R() - 0.5f) * 1.4f;
                Vector2 a = new Vector2(x0, -0.3f);
                Vector2 m = new Vector2(x0 + b1, 4.6f * s);
                Vector2 top = new Vector2(x0 + b1 + b2, 8.2f * s);
                float r0 = 0.46f * s, r1 = 0.28f * s, r2 = 0.16f * s;

                c.Fill(p => Sdf.SmoothUnion(Sdf.Tapered(p, a, r0, m, r1), Sdf.Tapered(p, m, r1, top, r2), 0.2f), bark, 0f,
                    new Rect(Mathf.Min(a.x, Mathf.Min(m.x, top.x)) - 1f, -0.5f, Mathf.Abs(top.x - a.x) + 2.4f, top.y + 1f));

                // roots
                for (int i = 0; i < 4; i++)
                {
                    float side = (i % 2 == 0) ? 1f : -1f;
                    Vector2 rs = new Vector2(x0 + side * (0.1f + R() * 0.2f), 0.65f + R() * 0.4f);
                    Vector2 re = new Vector2(x0 + side * (0.75f + R() * 0.8f), -0.35f);
                    Vector2 rm = Vector2.Lerp(rs, re, 0.5f) + new Vector2(side * 0.12f, 0.12f);
                    c.Fill(p => Mathf.Min(Sdf.Tapered(p, rs, 0.17f * s, rm, 0.1f * s), Sdf.Tapered(p, rm, 0.1f * s, re, 0.035f)), bark, 0f,
                        new Rect(Mathf.Min(rs.x, re.x) - 0.4f, -0.6f, Mathf.Abs(re.x - rs.x) + 0.8f, 2f));
                }

                // branches with canopy clumps and hanging moss
                int branches = 2 + r.Next(2);
                for (int i = 0; i < branches; i++)
                {
                    float t = 0.35f + R() * 0.55f;
                    Vector2 bs = Vector2.Lerp(m, top, t);
                    float side = (i % 2 == 0) ? 1f : -1f;
                    Vector2 be = bs + new Vector2(side * (1.3f + R() * 1.2f), 0.5f + R() * 1.2f);
                    Vector2 bm = Vector2.Lerp(bs, be, 0.5f) + new Vector2(0f, 0.18f);
                    c.Fill(p => Mathf.Min(Sdf.Tapered(p, bs, 0.13f * s, bm, 0.08f * s), Sdf.Tapered(p, bm, 0.08f * s, be, 0.03f)), bark, 0f,
                        new Rect(Mathf.Min(bs.x, be.x) - 0.3f, bs.y - 0.3f, Mathf.Abs(be.x - bs.x) + 0.6f, be.y - bs.y + 0.8f));
                    Canopies.Add(new Vector3(be.x, be.y - 0.45f, (2.4f + R() * 1.0f) * s));
                    MossAnchors.Add(Vector2.Lerp(bs, bm, 0.6f) + new Vector2(0f, -0.04f));
                    MossAnchors.Add(Vector2.Lerp(bm, be, 0.5f) + new Vector2(0f, -0.03f));
                }
                Canopies.Add(new Vector3(top.x, top.y - 0.6f, (3.6f + R() * 0.8f) * s));
                Canopies.Add(new Vector3(top.x + (R() - 0.5f) * 1.6f, top.y - 1.4f, (2.8f + R() * 0.6f) * s));

                // bracket fungi on the trunk
                for (int f = 0; f < 4; f++)
                {
                    float fy = 1.1f + R() * 3.4f * s;
                    float tt = Mathf.Clamp01(fy / (4.6f * s));
                    float cxT = Mathf.Lerp(a.x, m.x, tt), rad = Mathf.Lerp(r0, r1, tt);
                    float side = R() > 0.5f ? 1f : -1f;
                    Vector2 fc = new Vector2(cxT + side * rad * 0.8f, fy);
                    Vector2 ec = fc + new Vector2(side * 0.1f * s, 0f);
                    Vector2 er = new Vector2(0.19f * s, 0.07f * s);
                    c.Fill(p => Mathf.Max(Sdf.Ellipse(p, ec, er), fc.y - 0.012f - p.y),
                        p => Color.Lerp(new Color(0.78f, 0.52f, 0.3f), new Color(1f, 0.84f, 0.55f), MathUtil.Smooth01((p.y - fc.y) / er.y)),
                        0f, new Rect(ec.x - er.x - 0.05f, fc.y - 0.05f, er.x * 2f + 0.1f, er.y + 0.1f));
                }

                x += 4.3f + R() * 2.4f;
            }

            c.RimLight(new Vector2(0.06f, 0.03f), Palette.BarkLight, 0.7f);
            c.EdgeBand(Vector2.up, 0.07f, Palette.Moss, p => MathUtil.Smooth01((Noise.Perlin(p.x * 1.9f + 4f, p.y * 1.9f) - 0.42f) / 0.15f));
            // atmospheric perspective: the misty ground swallows the trunks' feet
            c.Paint(p =>
            {
                Color k = Palette.MidBottom;
                k.a = MathUtil.Smooth01(1f - p.y / 3.2f) * 0.3f;
                return k;
            });
            return c;
        }

        // ---------------------------------------------------------------- ruined stadium arcade

        const float ArchPeriod = 2.6f, ArchR = 0.82f, ArchSpring = 2.3f, VoussoirW = 0.27f;
        const float RuinCacheStep = 0.01f;
        static float[] ruinTopCache, tierMaskCache, tierNoiseCache;

        static float RuinTopExact(float x) =>
            4.25f - Mathf.Max(0f, Fbm(x * 0.17f, 5.5f) + 0.22f) * 3.5f + 0.12f * (Noise.Perlin(x * 2.7f, 9.1f) - 0.5f);

        static void BuildRuinCache()
        {
            int n = Mathf.CeilToInt((RuinW + 4f) / RuinCacheStep) + 2;
            var top = new float[n];
            var mask = new float[n];
            var tn = new float[n];
            for (int i = 0; i < n; i++)
            {
                float x = -RuinW * 0.5f - 2f + i * RuinCacheStep;
                top[i] = RuinTopExact(x);
                mask[i] = Fbm(x * 0.09f, 12.7f);
                tn[i] = Noise.Perlin(x * 3f, 1.7f);
            }
            ruinTopCache = top; tierMaskCache = mask; tierNoiseCache = tn;
        }

        static float SampleCache(float[] cache, float x)
        {
            float f = (x + RuinW * 0.5f + 2f) / RuinCacheStep;
            int i = Mathf.Clamp((int)f, 0, cache.Length - 2);
            return Mathf.Lerp(cache[i], cache[i + 1], Mathf.Clamp01(f - i));
        }

        static float RuinTop(float x) => ruinTopCache != null ? SampleCache(ruinTopCache, x) : RuinTopExact(x);

        static float OpeningSdf(Vector2 p, out float cx)
        {
            cx = Mathf.Round(p.x / ArchPeriod) * ArchPeriod;
            return Mathf.Min(Sdf.Box(p, new Vector2(cx, 1.25f), new Vector2(ArchR, 1.05f), 0.02f), Sdf.Circle(p, new Vector2(cx, ArchSpring), ArchR));
        }

        static float PierTop(float pk)
        {
            float px = pk * ArchPeriod + ArchPeriod * 0.5f;
            float seed = MathUtil.Hash((int)pk * 31 + 7);
            return Mathf.Max(RuinTop(px) + 0.15f, 0.6f) + (seed > 0.35f ? (seed - 0.35f) * 2.6f : 0f);
        }

        static float ColumnSdf(Vector2 p, out float px, out float colTop)
        {
            float pk = Mathf.Floor(p.x / ArchPeriod);
            px = pk * ArchPeriod + ArchPeriod * 0.5f;
            colTop = PierTop(pk);
            float column = Sdf.Box(p, new Vector2(px, colTop * 0.5f), new Vector2(0.27f, colTop * 0.5f));
            float capital = Sdf.Box(p, new Vector2(px, colTop), new Vector2(0.37f, 0.11f), 0.02f);
            float plinth = Sdf.Box(p, new Vector2(px, 0.12f), new Vector2(0.36f, 0.14f), 0.02f);
            return Mathf.Min(column, Mathf.Min(capital, plinth));
        }

        static float WallSdf(Vector2 p) =>
            Mathf.Max(Sdf.Box(p, new Vector2(0f, 2.2f), new Vector2(RuinW * 0.5f, 2.2f)), p.y - RuinTop(p.x));

        static float CorniceSdf(Vector2 p) =>
            Mathf.Max(Sdf.Box(p, new Vector2(0f, 4.02f), new Vector2(RuinW * 0.5f, 0.1f)), p.y - RuinTop(p.x) - 0.1f);

        static float TierSdf(Vector2 p)
        {
            float tierMask = SampleCache(tierMaskCache, p.x);
            if (tierMask <= 0.05f || RuinTop(p.x) <= 3.9f) return 9f;
            const float tp = 1.3f;
            float tk = Mathf.Round(p.x / tp) * tp;
            float tier = Sdf.Box(p, new Vector2(p.x, 5.0f), new Vector2(0.5f, 0.85f));
            float tOpen = Mathf.Min(Sdf.Box(p, new Vector2(tk, 4.85f), new Vector2(0.4f, 0.45f)), Sdf.Circle(p, new Vector2(tk, 5.3f), 0.4f));
            float tierTop = 5.9f + 0.1f * (SampleCache(tierNoiseCache, p.x) - 0.5f);
            return Sdf.Intersect(Sdf.Subtract(tier, tOpen), p.y - tierTop);
        }

        static float RuinSdf(Vector2 p)
        {
            float dOpen = OpeningSdf(p, out _);
            float d = Sdf.Subtract(WallSdf(p), dOpen);
            d = Mathf.Min(d, ColumnSdf(p, out _, out _));
            d = Mathf.Min(d, CorniceSdf(p));
            return Mathf.Min(d, TierSdf(p));
        }

        internal static Color BrickColor(Vector2 p, float bh, float bw, int salt)
        {
            float row = Mathf.Floor(p.y / bh);
            int ri = (int)row;
            float off = (ri & 1) == 0 ? 0f : bw * 0.5f;
            float colF = Mathf.Floor((p.x + off) / bw);
            float lx = p.x + off - colF * bw, ly = p.y - row * bh;
            float edge = Mathf.Min(Mathf.Min(lx, bw - lx), Mathf.Min(ly, bh - ly));
            float mortar = 1f - MathUtil.Smooth01((edge - 0.01f) / 0.008f);
            float h = MathUtil.Hash((int)colF * 57 + ri * 131 + salt * 7);
            float h2 = MathUtil.Hash((int)colF * 13 + ri * 71 + salt * 3 + 500);
            Color c = Color.Lerp(Palette.Stone, Palette.StoneLight, 0.26f + 0.2f * h + (ly / bh) * 0.14f);
            // some blocks are greener (lichen) or darker (weathered)
            if (h2 > 0.6f) c = Color.Lerp(c, new Color(0.52f, 0.66f, 0.42f), 0.35f);
            else if (h2 < -0.7f) c *= 0.82f;
            if (ly > bh - 0.03f) c *= 1.08f;
            else if (ly < 0.025f) c *= 0.9f;
            return Color.Lerp(c, Palette.Mortar, mortar * 0.85f);
        }

        static Color VoussoirColor(Vector2 q, float dist, float cx)
        {
            const int segs = 13;
            float seg = Mathf.PI / segs;
            float a = Mathf.Clamp(Mathf.Atan2(q.y, q.x), 0f, Mathf.PI) / seg;
            int si = Mathf.Min(segs - 1, (int)a);
            float fa = a - Mathf.Floor(a);
            float angEdge = Mathf.Min(fa, 1f - fa) * seg * dist;
            float radEdge = Mathf.Min(dist - ArchR, ArchR + VoussoirW - dist);
            float mortar = 1f - MathUtil.Smooth01((Mathf.Min(angEdge, radEdge) - 0.01f) / 0.008f);
            bool key = si == segs / 2;
            float h = MathUtil.Hash(si * 17 + (int)(cx * 3f) + 1000);
            Color c = Color.Lerp(Palette.Stone, Palette.StoneLight, 0.36f + 0.12f * h + (key ? 0.18f : 0f));
            c *= 0.92f + 0.12f * MathUtil.Smooth01((dist - ArchR) / VoussoirW);
            return Color.Lerp(c, Palette.Mortar, mortar * 0.85f);
        }

        static Color ColumnColor(Vector2 p, float px, float colTop)
        {
            float sx = Mathf.Clamp((p.x - px) / 0.27f, -1f, 1f);
            float flute = 0.5f + 0.5f * Mathf.Cos((p.x - px) * MathUtil.Tau / 0.09f);
            Color c = Color.Lerp(Palette.Stone * 0.85f, Palette.StoneLight * 1.05f, 0.4f + 0.35f * sx);
            c *= 0.9f + 0.1f * flute;
            float drum = Mathf.Abs(Mathf.Repeat(p.y, 0.7f) - 0.35f);
            if (drum > 0.338f) c *= 0.78f;
            if (p.y > colTop - 0.11f)
            {
                c = Color.Lerp(Palette.Stone, Palette.StoneLight, 0.62f + 0.2f * sx);
                if (Mathf.Abs(p.y - (colTop - 0.11f)) < 0.013f) c *= 0.6f;
            }
            else if (p.y < 0.26f)
            {
                c = Color.Lerp(Palette.Stone, Palette.StoneLight, 0.5f + 0.2f * sx);
                if (Mathf.Abs(p.y - 0.26f) < 0.013f) c *= 0.65f;
            }
            return c;
        }

        static Color RuinColor(Vector2 p, float aa)
        {
            float dOpen = OpeningSdf(p, out float cx);
            float dCol = ColumnSdf(p, out float px, out float colTop);
            float dWall = Mathf.Max(WallSdf(p), -dOpen);
            float dCorn = CorniceSdf(p);
            float dTier = TierSdf(p);
            float d = Mathf.Min(Mathf.Min(dWall, dCol), Mathf.Min(dCorn, dTier));
            float alpha = Mathf.Clamp01(0.5f - d / aa);
            if (alpha <= 0f) return new Color(0, 0, 0, 0);

            Vector2 q = p - new Vector2(cx, ArchSpring);
            float dist = q.magnitude;
            Color col;
            if (dCol <= dWall && dCol <= dCorn && dCol <= dTier) col = ColumnColor(p, px, colTop);
            else if (dTier <= dWall && dTier <= dCorn) col = BrickColor(p, 0.2f, 0.42f, 3);
            else if (dCorn <= dWall)
            {
                col = Color.Lerp(Palette.Stone, Palette.StoneLight, 0.55f) * (p.y > 4.06f ? 1.06f : 0.9f);
                if (p.y < 3.98f && Mathf.Repeat(p.x, 0.16f) < 0.075f) col *= 0.78f;
            }
            else if (q.y > -0.02f && dist > ArchR && dist < ArchR + VoussoirW) col = VoussoirColor(q, dist, cx);
            else col = BrickColor(p, 0.28f, 0.62f, 0);

            // shadowed reveal around the arch openings
            if (dOpen > 0f && dOpen < 0.07f && dCol > 0.01f) col *= Mathf.Lerp(0.68f, 1f, dOpen / 0.07f);
            // weathering: rain stains running down from the broken top, sparse cracks
            float top = RuinTop(p.x);
            float stain = Noise.Perlin(p.x * 3.3f, 7.1f);
            col *= 1f - Mathf.Max(0f, stain - 0.45f) * 0.5f * MathUtil.Smooth01((top - p.y) / 1.6f) * MathUtil.Smooth01(p.y - 0.4f);
            float cn = Mathf.Abs(Noise.Perlin(p.x * 1.9f + 3f, p.y * 1.9f) - 0.5f);
            if (cn < 0.011f && Noise.Perlin(p.x * 0.55f + 11f, p.y * 0.55f) > 0.57f) col *= 0.55f;
            // mist at the base
            col = Color.Lerp(col, Palette.RuinBottom, MathUtil.Smooth01(1f - p.y / 2f) * 0.22f);
            col.a = alpha * EdgeFade(p.x, RuinW * 0.5f);
            return col;
        }

        static SdfCanvas BuildRuins()
        {
            BuildRuinCache();
            var c = new SdfCanvas(new Rect(-RuinW * 0.5f, 0f, RuinW, RuinH), 86f);
            float aa = 1f / c.Ppu;
            c.Field(p => RuinColor(p, aa));
            c.RimLight(new Vector2(0.05f, 0.05f), Palette.RuinRim, 0.55f);
            c.EdgeBand(Vector2.up, 0.08f, Palette.Moss, p => MathUtil.Smooth01((Noise.Perlin(p.x * 1.7f, p.y * 1.7f) - 0.35f) / 0.2f));

            var r = new System.Random(5);
            float R() => (float)r.NextDouble();

            // ivy patches crawling over the stones
            for (int i = 0, tries = 0; i < 16 && tries < 200; tries++)
            {
                Vector2 center = new Vector2(-RuinW * 0.5f + 2f + R() * (RuinW - 4f), 0.6f + R() * 3.2f);
                if (RuinSdf(center) > -0.1f) continue;
                i++;
                float rad = 0.35f + R() * 0.6f;
                int leaves = (int)(rad * 220f);
                for (int l = 0; l < leaves; l++)
                {
                    float u1 = Mathf.Max(1e-4f, R()), u2 = R();
                    float g = Mathf.Sqrt(-2f * Mathf.Log(u1));
                    Vector2 pos = center + new Vector2(g * Mathf.Cos(MathUtil.Tau * u2), g * Mathf.Sin(MathUtil.Tau * u2)) * rad * 0.42f;
                    if (RuinSdf(pos) > -0.01f) continue;
                    float size = 0.055f + R() * 0.04f;
                    Color lc = Color.Lerp(Palette.Ivy, Palette.IvyLight, R() * 0.8f + (pos.y > center.y ? 0.2f : 0f));
                    FoliageArt.Leaf(c, pos, R() * 360f, size, size * 0.55f, lc, 0.15f);
                }
            }

            // rubble and shrubs at the base
            for (int i = 0; i < 34; i++)
            {
                Vector2 bc = new Vector2(-RuinW * 0.5f + 1f + R() * (RuinW - 2f), 0.08f + R() * 0.12f);
                Vector2 half = new Vector2(0.12f + R() * 0.2f, 0.07f + R() * 0.08f);
                float ang = (R() - 0.5f) * 40f;
                c.Fill(p => Sdf.Box(p, bc, half, 0.03f, ang),
                    p => Color.Lerp(Palette.Stone * 0.75f, Palette.StoneLight, MathUtil.Smooth01((p.y - bc.y + half.y) / (half.y * 2f)) * 0.8f),
                    0f, new Rect(bc.x - 0.6f, bc.y - 0.5f, 1.2f, 1f));
            }
            Color green = Color.Lerp(Palette.Bush, Palette.FolMid, 0.45f);
            for (float x = -RuinW * 0.5f; x < RuinW * 0.5f; x += 0.6f + R() * 1.1f)
            {
                Vector2 bp = new Vector2(x, 0.05f + R() * 0.25f);
                float br = 0.28f + R() * 0.4f;
                Color gc = green.WithAlpha(EdgeFade(x, RuinW * 0.5f));
                c.Fill(p => Sdf.Circle(p, bp, br), gc, 0f, new Rect(bp.x - br, bp.y - br, br * 2f, br * 2f));
            }

            // anchors for animated props
            for (float k = -8f; k <= 8f; k++)
            {
                float cx = k * ArchPeriod;
                if (Mathf.Abs(cx) > RuinW * 0.5f - 1.5f) continue;
                if (RuinTop(cx) > ArchSpring + ArchR + 0.35f)
                {
                    ArchSpots.Add(new Vector2(cx, 1.4f));
                    if (((int)k & 1) == 0) LanternSpots.Add(new Vector2(cx, ArchSpring + ArchR - 0.02f));
                }
                float px = cx + ArchPeriod * 0.5f;
                if (Mathf.Abs(px) < RuinW * 0.5f - 1.5f) PierTops.Add(new Vector2(px, PierTop(k) + 0.1f));
            }
            for (float x = -RuinW * 0.5f + 1f; x < RuinW * 0.5f - 1f; x += 0.9f + R() * 0.9f)
            {
                float top = RuinTop(x);
                if (top > 1.6f && RuinSdf(new Vector2(x, top - 0.05f)) < 0f) IvyAnchors.Add(new Vector2(x, top - 0.03f));
            }
            return c;
        }

        // ---------------------------------------------------------------- bushes directly behind the pitch

        static SdfCanvas BuildBushes()
        {
            var c = new SdfCanvas(new Rect(-BushW * 0.5f, 0f, BushW, BushH), 72f);
            var r = new System.Random(33);
            float R() => (float)r.NextDouble();
            float left = c.UnitRect.xMin, ppu = c.Ppu;
            int W = c.Width;
            var ground = new float[W];
            for (int x = 0; x < W; x++) ground[x] = 0.5f + 0.18f * Fbm((left + (x + 0.5f) / ppu) * 0.6f, 3f);
            SdfCanvas.ColorFn col = p =>
            {
                Color k = Color.Lerp(Palette.Bush, Palette.FolMid, MathUtil.Smooth01((p.y - 0.3f) / 1.3f));
                k.a = EdgeFade(p.x, BushW * 0.5f);
                return k;
            };
            c.Fill(p => p.y - ground[Mathf.Clamp((int)((p.x - left) * ppu), 0, W - 1)], col);
            Color leafDark = Palette.FolDark, leafLight = Palette.FolLight;
            for (float x = -BushW * 0.5f + 0.3f; x < BushW * 0.5f; x += 0.45f + R() * 0.55f)
            {
                Vector2 bp = new Vector2(x, 0.55f + R() * 0.3f);
                float br = 0.28f + R() * 0.42f;
                c.Fill(p => Sdf.Circle(p, bp, br), col, 0f, new Rect(bp.x - br, bp.y - br, br * 2f, br * 2f));
                int n = (int)(br * 70f);
                for (int l = 0; l < n; l++)
                {
                    float ang = Mathf.Lerp(12f, 168f, R());
                    Vector2 dir = MathUtil.Dir(ang);
                    Vector2 pos = bp + dir * br * (0.78f + R() * 0.26f);
                    Color lc = Color.Lerp(leafDark, leafLight, R() * 0.6f + (dir.x > 0f ? 0.3f : 0f));
                    FoliageArt.Leaf(c, pos, ang + (R() - 0.5f) * 90f, 0.1f + R() * 0.06f, 0.04f, lc, 0.08f);
                }
            }
            for (float x = -BushW * 0.5f; x < BushW * 0.5f; x += 0.07f + R() * 0.14f)
            {
                float h = 0.22f + R() * 0.5f;
                float lean = (R() - 0.5f) * 0.25f;
                Vector2 gb = new Vector2(x, 0.6f), gt = new Vector2(x + lean, 0.6f + h + R() * 0.3f);
                var bounds = Rect.MinMaxRect(Mathf.Min(gb.x, gt.x) - 0.04f, 0.55f, Mathf.Max(gb.x, gt.x) + 0.04f, gt.y + 0.02f);
                c.Fill(p => Sdf.Tapered(p, gb, 0.028f, gt, 0.004f), col, 0f, bounds);
            }
            c.RimLight(new Vector2(0.02f, 0.03f), Palette.FolRim, 0.45f);
            return c;
        }

        // ---------------------------------------------------------------- pitch + earth (tiled)

        static SdfCanvas BuildPitch()
        {
            var c = new SdfCanvas(new Rect(0f, 0f, PitchTileW, PitchH), 160f);
            var rng = new System.Random(11);
            float R() => (float)rng.NextDouble();
            const float bandBottom = 0.13f;   // hanging grass below the band
            c.Field(p =>
            {
                if (p.y < bandBottom) return new Color(0, 0, 0, 0);
                float yy = (p.y - bandBottom) / (PitchH - bandBottom); // 0 front .. 1 back
                float slant = p.x + (yy - 0.5f) * 0.35f;
                bool stripe = Mathf.Repeat(slant, PitchTileW) < PitchTileW * 0.5f;
                Color col = stripe ? Palette.PitchA : Palette.PitchB;
                float stripeEdge = Mathf.Min(Mathf.Repeat(slant, PitchTileW * 0.5f), PitchTileW * 0.5f - Mathf.Repeat(slant, PitchTileW * 0.5f));
                col = Color.Lerp(Color.Lerp(Palette.PitchA, Palette.PitchB, 0.5f), col, MathUtil.Smooth01(stripeEdge / 0.08f));
                // fine grass texture: grain, blade strokes, uneven patches (all seamless across the tile)
                float grain = PeriodicX(p.x * 38f, p.y * 9f, PitchTileW * 38f);
                float blades = PeriodicX(p.x * 110f, p.y * 2.2f, PitchTileW * 110f);
                float patch = PeriodicX(p.x * 1.4f, p.y * 2.5f + 7f, PitchTileW * 1.4f);
                col *= 0.93f + 0.14f * grain;
                col *= 1f - MathUtil.Smooth01((0.32f - blades) / 0.12f) * 0.13f;
                col *= 0.95f + 0.1f * patch;
                col = Color.Lerp(col, Palette.PitchBack, MathUtil.Smooth01((yy - 0.55f) / 0.45f) * 0.8f);
                col = Color.Lerp(col, Color.Lerp(Palette.PitchA, Palette.GrassEdge, 0.35f), MathUtil.Smooth01((0.22f - yy) / 0.22f) * 0.45f);
                // worn touchline chalk
                float line = Mathf.Abs(yy - 0.28f);
                float wear = 0.65f + 0.35f * PeriodicX(p.x * 7f, 3.3f, PitchTileW * 7f);
                col = Color.Lerp(col, new Color(0.9f, 1f, 0.97f), (1f - MathUtil.Smooth01((line - 0.018f) / 0.012f)) * 0.78f * wear);
                col = Color.Lerp(col, Palette.GrassEdge, 1f - MathUtil.Smooth01((p.y - bandBottom) / 0.035f));
                col.a = 1f;
                return col;
            });
            for (int i = 0; i < 220; i++)
            {
                Vector2 sp = new Vector2(R() * PitchTileW, bandBottom + 0.05f + R() * (PitchH - bandBottom - 0.07f));
                float sr = 0.008f + R() * 0.016f;
                Color sc = R() > 0.5f ? new Color(0.85f, 0.97f, 0.5f, 0.2f) : new Color(0.16f, 0.42f, 0.18f, 0.18f);
                for (int w = -1; w <= 1; w++)
                {
                    Vector2 q = sp + new Vector2(w * PitchTileW, 0f);
                    c.Paint(p => Sdf.Circle(p, q, sr), sc, 0.004f, new Rect(q.x - 0.05f, q.y - 0.05f, 0.1f, 0.1f));
                }
            }
            // a few tiny daisies near the back
            for (int i = 0; i < 6; i++)
            {
                Vector2 fc = new Vector2(R() * PitchTileW, bandBottom + 0.45f + R() * 0.3f);
                for (int w = -1; w <= 1; w++)
                {
                    Vector2 q = fc + new Vector2(w * PitchTileW, 0f);
                    for (int k = 0; k < 5; k++)
                    {
                        Vector2 pc = q + MathUtil.Dir(k * 72f) * 0.011f;
                        c.Fill(p => Sdf.Circle(p, pc, 0.008f), new Color(0.85f, 0.95f, 1f, 0.85f), 0f, new Rect(pc.x - 0.03f, pc.y - 0.03f, 0.06f, 0.06f));
                    }
                    c.Fill(p => Sdf.Circle(p, q, 0.005f), Palette.PetalWarm, 0f, new Rect(q.x - 0.02f, q.y - 0.02f, 0.04f, 0.04f));
                }
            }
            // hanging grass tips over the earth: irregular clumps instead of an even comb
            for (float x = 0f; x < PitchTileW; x += 0.05f + R() * 0.16f)
            {
                int blades = 1 + (R() > 0.6f ? 2 : 0);
                for (int k = 0; k < blades; k++)
                {
                    float h = 0.025f + R() * R() * 0.11f;
                    float lean = (R() - 0.5f) * 0.06f;
                    float bx = x + k * 0.018f;
                    Color gc = Color.Lerp(Palette.PitchA, Palette.GrassEdge, 0.25f + R() * 0.35f);
                    for (int w = -1; w <= 1; w++)
                    {
                        float gx = bx + w * PitchTileW;
                        Vector2 gt = new Vector2(gx + lean, bandBottom - h), gb = new Vector2(gx, bandBottom + 0.02f);
                        c.Fill(p => Sdf.Tapered(p, gb, 0.013f, gt, 0.002f), gc, 0f, new Rect(gx - 0.08f, 0f, 0.16f, 0.2f));
                    }
                }
            }
            return c;
        }

        static SdfCanvas BuildEarth()
        {
            var e = new SdfCanvas(new Rect(0f, 0f, EarthTileW, EarthH), 96f);
            e.Field(p =>
            {
                float t = p.y / EarthH;
                Color col = Color.Lerp(Palette.EarthBottom, Palette.EarthTop, MathUtil.Smooth01(t));
                float strata = 0.5f + 0.5f * Mathf.Sin(p.y * 7f + (PeriodicX(p.x * 1.5f, 2f, EarthTileW * 1.5f) - 0.5f) * 4f);
                col = Color.Lerp(col, col * 1.2f, strata * 0.25f * t);
                col *= 0.94f + 0.12f * PeriodicX(p.x * 9f, p.y * 9f, EarthTileW * 9f);
                col.a = 1f;
                return col;
            });
            var rng = new System.Random(3);
            float R() => (float)rng.NextDouble();
            for (int i = 0; i < 30; i++)
            {
                Vector2 sp = new Vector2(R() * EarthTileW, 0.3f + R() * (EarthH - 0.5f));
                Vector2 rad = new Vector2(0.06f + R() * 0.16f, 0.04f + R() * 0.07f);
                Color sc = Color.Lerp(Palette.EarthTop, new Color(0.72f, 0.62f, 0.55f), 0.35f + R() * 0.3f).WithAlpha(0.8f);
                for (int w = -1; w <= 1; w++)
                {
                    Vector2 q = sp + new Vector2(w * EarthTileW, 0f);
                    Rect b = new Rect(q.x - 0.3f, q.y - 0.2f, 0.6f, 0.4f);
                    e.Paint(p => Sdf.Ellipse(p, q, rad), sc, 0.01f, b);
                    e.Paint(p => Mathf.Max(Sdf.Ellipse(p, q, rad), q.y + rad.y * 0.35f - p.y), sc * 1.35f, 0.01f, b);
                }
            }
            // roots reaching down from the turf
            Color root = new Color(0.4f, 0.23f, 0.14f), rootLight = new Color(0.68f, 0.45f, 0.28f);
            for (int i = 0; i < 3; i++)
            {
                float x0 = (i + 0.2f + R() * 0.6f) * EarthTileW / 3f;
                Vector2 prev = new Vector2(x0, EarthH + 0.05f);
                float wid = 0.035f + R() * 0.03f;
                int segs = 8 + rng.Next(7);
                float seed = R() * 10f;
                for (int s = 0; s < segs; s++)
                {
                    float t = s / (float)segs;
                    Vector2 next = prev + new Vector2(Mathf.Sin(s * 1.3f + seed) * 0.08f + (R() - 0.5f) * 0.06f, -(0.13f + R() * 0.12f));
                    float w0 = wid * (1f - t * 0.8f);
                    for (int w = -1; w <= 1; w++)
                    {
                        Vector2 p0 = prev + new Vector2(w * EarthTileW, 0f), p1 = next + new Vector2(w * EarthTileW, 0f);
                        Rect b = Rect.MinMaxRect(Mathf.Min(p0.x, p1.x) - 0.1f, p1.y - 0.1f, Mathf.Max(p0.x, p1.x) + 0.1f, p0.y + 0.1f);
                        e.Fill(p => Sdf.Capsule(p, p0, p1, w0), root, 0f, b);
                        e.Fill(p => Sdf.Capsule(p, p0 + new Vector2(0f, w0 * 0.4f), p1 + new Vector2(0f, w0 * 0.4f), w0 * 0.3f), rootLight.WithAlpha(0.5f), 0f, b);
                        if (s > 2 && (s + i) % 4 == 0)
                        {
                            Vector2 tip = p1 + new Vector2((R() - 0.5f) * 0.4f, -0.12f - R() * 0.15f);
                            e.Fill(p => Sdf.Tapered(p, p1, w0 * 0.6f, tip, 0.004f), root, 0f, Rect.MinMaxRect(Mathf.Min(p1.x, tip.x) - 0.1f, tip.y - 0.1f, Mathf.Max(p1.x, tip.x) + 0.1f, p1.y + 0.1f));
                        }
                    }
                    prev = next;
                }
            }
            return e;
        }

        // ---------------------------------------------------------------- fog, goal, props

        static SdfCanvas BuildFog()
        {
            var c = new SdfCanvas(new Rect(0f, 0f, 8f, 2f), 48f);
            c.Field(p =>
            {
                // horizontally periodic noise built from sines so the band tiles seamlessly
                float u = p.x / 8f * MathUtil.Tau;
                float n = 0.5f + 0.25f * Mathf.Sin(u * 2f + 0.7f) + 0.15f * Mathf.Sin(u * 5f + 2.1f) + 0.1f * Mathf.Sin(u * 11f + 4f);
                float v = p.y / 2f;
                float band = Mathf.Exp(-Mathf.Pow((v - 0.45f) / 0.26f, 2f));
                return new Color(1f, 1f, 1f, Mathf.Clamp01(band * n));
            });
            return c;
        }

        static SdfCanvas BuildGoal()
        {
            // Side view of a goal, opening towards +x. Pivot at the base of the front post.
            var c = new SdfCanvas(new Rect(-2.3f, -0.1f, 2.5f, 2.9f), 110f);
            Vector2 postB = new Vector2(0f, 0f), postT = new Vector2(0f, 2.45f);
            Vector2 backT = new Vector2(-1.45f, 2.45f), backB = new Vector2(-2.1f, 0f);
            SdfCanvas.SdfFn inside = p => Mathf.Max(Mathf.Max(-p.y, p.y - 2.45f),
                Mathf.Max(p.x, Sdf.HalfPlane(p, backB, new Vector2(-(backT.y - backB.y), backT.x - backB.x))));
            c.Fill(p =>
            {
                const float g = 0.16f;
                float l1 = Mathf.Abs(Mathf.Repeat(p.x + p.y, g) - g * 0.5f);
                float l2 = Mathf.Abs(Mathf.Repeat(p.x - p.y, g) - g * 0.5f);
                float line = Mathf.Min(g * 0.5f - l1, g * 0.5f - l2) - 0.006f;
                return Mathf.Max(line, inside(p));
            }, new Color(0.85f, 0.95f, 1f, 0.28f));
            c.Fill(p => Sdf.Capsule(p, backT, backB, 0.03f), new Color(0.75f, 0.82f, 0.9f));
            c.Fill(p => Sdf.Capsule(p, backT, postT, 0.035f), new Color(0.82f, 0.88f, 0.94f));
            c.Fill(p => Sdf.Capsule(p, postB, postT, 0.065f), new Color(0.95f, 0.97f, 1f));
            c.Paint(p => Sdf.Capsule(p, postB + new Vector2(-0.03f, 0f), postT + new Vector2(-0.03f, 0f), 0.02f), new Color(0.7f, 0.78f, 0.88f));
            return c;
        }

        static SdfCanvas BuildLantern()
        {
            var c = new SdfCanvas(new Rect(-0.2f, -0.56f, 0.4f, 0.6f), 220f);
            Color iron = new Color(0.07f, 0.07f, 0.08f), ironLight = new Color(0.34f, 0.33f, 0.33f);
            c.Fill(p => Sdf.Ring(p, new Vector2(0f, -0.025f), 0.022f, 0.012f), iron);
            c.Fill(p => Sdf.Triangle(p, new Vector2(-0.13f, -0.1f), new Vector2(0.13f, -0.1f), new Vector2(0f, -0.045f)), iron);
            c.Fill(p => Sdf.Box(p, new Vector2(0f, -0.11f), new Vector2(0.115f, 0.018f), 0.008f), iron);
            c.Fill(p => Sdf.Box(p, new Vector2(0f, -0.26f), new Vector2(0.085f, 0.13f), 0.03f), p =>
            {
                float d = Mathf.Abs(p.x) / 0.085f;
                return Color.Lerp(new Color(1f, 0.93f, 0.7f), new Color(1f, 0.6f, 0.24f), d * d);
            });
            c.Fill(p => Sdf.Ellipse(p, new Vector2(0f, -0.28f), new Vector2(0.024f, 0.045f)), new Color(1f, 1f, 0.92f));
            for (int i = -1; i <= 1; i++)
            {
                float bx = i * 0.085f;
                c.Fill(p => Sdf.Box(p, new Vector2(bx, -0.26f), new Vector2(0.009f, 0.14f), 0.004f), iron);
            }
            c.Fill(p => Sdf.Box(p, new Vector2(0f, -0.405f), new Vector2(0.1f, 0.02f), 0.008f), iron);
            c.Fill(p => Sdf.Triangle(p, new Vector2(-0.05f, -0.42f), new Vector2(0.05f, -0.42f), new Vector2(0f, -0.5f)), iron);
            c.RimLight(new Vector2(0.01f, 0.005f), ironLight, 0.5f);
            return c;
        }

        static SdfCanvas BuildChain()
        {
            var c = new SdfCanvas(new Rect(-0.04f, -1.02f, 0.08f, 1.04f), 220f);
            Color iron = new Color(0.08f, 0.08f, 0.09f);
            for (int i = 0; i < 20; i++)
            {
                float y = -0.025f - i * 0.05f;
                if (i % 2 == 0) c.Fill(p => Mathf.Abs(Sdf.Ellipse(p, new Vector2(0f, y), new Vector2(0.016f, 0.03f))) - 0.005f, iron);
                else c.Fill(p => Sdf.Box(p, new Vector2(0f, y), new Vector2(0.005f, 0.028f), 0.004f), iron);
            }
            return c;
        }

        static SdfCanvas BuildBat(int frame)
        {
            // a gull seen from below/behind: slate body, wings bent at the wrist (three flap frames)
            var c = new SdfCanvas(new Rect(-0.38f, -0.22f, 0.76f, 0.44f), 110f);
            Color col = new Color(0.3f, 0.36f, 0.5f), light = new Color(0.97f, 0.98f, 1f);
            float tipY = frame == 0 ? 0.17f : frame == 1 ? 0.03f : -0.13f;
            float midY = frame == 0 ? 0.09f : frame == 1 ? 0.04f : -0.03f;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 s = new Vector2(side * 0.03f, 0.005f);
                Vector2 m = new Vector2(side * 0.16f, midY);
                Vector2 t = new Vector2(side * 0.34f, tipY);
                c.Fill(p => Mathf.Min(Sdf.Tapered(p, s, 0.035f, m, 0.028f), Sdf.Tapered(p, m, 0.028f, t, 0.006f)), light);
                c.Fill(p => Sdf.Tapered(p, Vector2.Lerp(m, t, 0.45f), 0.02f, t, 0.005f), col);
            }
            c.Fill(p => Sdf.Ellipse(p, new Vector2(0f, -0.005f), new Vector2(0.045f, 0.06f)), light);
            c.Fill(p => Sdf.Circle(p, new Vector2(0f, 0.05f), 0.026f), light);
            c.Fill(p => Sdf.Triangle(p, new Vector2(-0.012f, 0.07f), new Vector2(0.012f, 0.07f), new Vector2(0f, 0.095f)), new Color(1f, 0.72f, 0.25f));
            c.RimLight(new Vector2(-0.006f, -0.008f), col, 0.5f);
            return c;
        }

        static SdfCanvas BuildLeaf(int k)
        {
            var c = new SdfCanvas(new Rect(-0.11f, -0.06f, 0.22f, 0.12f), 260f);
            Color col = k == 0 ? new Color(0.42f, 0.74f, 0.26f) : new Color(0.95f, 0.75f, 0.25f);
            FoliageArt.Leaf(c, Vector2.zero, 0f, 0.17f, 0.045f, col, 0.35f);
            c.Fill(p => Sdf.Capsule(p, new Vector2(-0.085f, 0f), new Vector2(-0.105f, -0.012f), 0.004f), col * 0.7f);
            return c;
        }
    }
}
