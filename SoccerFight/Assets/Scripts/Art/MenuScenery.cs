using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Art for the title screen's own place in the game world (MenuVista): a mountain valley
    /// after the catastrophe, under the game's moon. Far back a massif whose summit has been split
    /// open, crystal light welling out of the cleft, and a peak sheared flat; in front of it burnt
    /// ridges with the stumps of a dead forest and embers still glowing in a blast crater; below
    /// them a wrecked plain — a glowing rift, smouldering craters, slabs of rock tilted out of the
    /// ground, dead trees; and in front the cracked plateau the player stands on. Everything is
    /// painted in the backdrop's own style and palette
    /// (the recipes of DepthArt and EnvironmentArt: moonlit facets, strata, crag streaks, rim light)
    /// in world units, on worker threads while the game boots. The logo and the big ball for the
    /// kick-off transition are generated here too.
    ///
    /// Vista units: 1 = 1 world unit, origin = centre of the view (the menu camera is 9.8 tall).
    /// Each layer's canvas has its own origin; MenuVista places the layers.
    /// </summary>
    public static class MenuScenery
    {
        public const float MoonPpu = 560f;
        /// <summary>Where the moon stands (vista units): up to the right of the player, over the valley's low saddle.</summary>
        public static readonly Vector2 MoonPos = new Vector2(2.9f, 2.95f);

        /// <summary>Heights of the layers' origins in the vista (the ground is pinned under the player instead).</summary>
        public const float PeaksY = -0.4f, RidgeY = -0.85f, WasteY = -0.95f;
        /// <summary>Depth of the plateau's top face (from its back edge to the lip).</summary>
        public const float GroundBand = 0.5f;

        public static Sprite Logo, HeroBall, HeroHoop, HeroShade, HeroHighlight;
        public static Sprite Moon, Peaks, Ridge, Waste, Ground, Smoke;

        /// <summary>Crystal light in the split summit and its veins (peaks-local: x, y, size).</summary>
        public static readonly List<Vector3> CleftGlows = new List<Vector3>();
        /// <summary>Embers still burning on the ridge; smoke rises from them (ridge-local: x, y, size).</summary>
        public static readonly List<Vector3> RidgeFires = new List<Vector3>();
        /// <summary>Glow along the rift through the plain (waste-local: x, y, size).</summary>
        public static readonly List<Vector3> RiftGlows = new List<Vector3>();
        /// <summary>Smouldering craters on the plain (waste-local: x, y, width).</summary>
        public static readonly List<Vector3> Craters = new List<Vector3>();
        /// <summary>Glowing cracks in the plateau (ground-local: x, y, size).</summary>
        public static readonly List<Vector3> GroundGlows = new List<Vector3>();

        static ArtJobs jobs;
        static ArtJobs.Job jLogo, jHero, jHeroHoop, jHeroShade, jHeroHi;

        static readonly Color Clear = new Color(0f, 0f, 0f, 0f);
        static readonly Color Cyan = new Color(0.45f, 0.95f, 1f);
        static readonly Color Ember = new Color(1f, 0.52f, 0.2f);
        static float Sq(float v) => v * v;
        static float S01(float v) => MathUtil.Smooth01(v);
        static float G(float x, float c, float w) => Mathf.Exp(-Sq((x - c) / w));
        static Color Mul(Color c, float f) { c.r *= f; c.g *= f; c.b *= f; return c; }
        static float Fbm(float x, float seed, int oct = 4) => EnvironmentArt.Fbm(x, seed, oct);
        static float Fbm2(Vector2 p, float seed, int oct = 4) => EnvironmentArt.Fbm2(p, seed, oct);
        static float Ridged(float x, float seed) => 1f - Mathf.Abs(EnvironmentArt.Fbm(x, seed, 5));
        static float Hash01(int n) => MathUtil.Hash(n) * 0.5f + 0.5f;
        static Rect Around(Vector2 a, Vector2 b, float pad) =>
            Rect.MinMaxRect(Mathf.Min(a.x, b.x) - pad, Mathf.Min(a.y, b.y) - pad, Mathf.Max(a.x, b.x) + pad, Mathf.Max(a.y, b.y) + pad);
        static float[] Slope(float[] h, float ppu)
        {
            var s = new float[h.Length];
            for (int x = 0; x < h.Length; x++)
                s[x] = (h[Mathf.Min(h.Length - 1, x + 2)] - h[Mathf.Max(0, x - 2)]) / (4f / ppu);
            return s;
        }

        /// <summary>How much a facet with this slope faces the moon (moon left or right of it).</summary>
        static float Lit(float slope, float x, float k = 0.8f)
        {
            float toward = x < MoonPos.x ? 1f : -1f;
            return Mathf.Clamp(-slope * toward * k, -0.6f, 0.8f);
        }

        // ------------------------------------------------------------------ orchestration

        public static void Begin()
        {
            _ = Palette.Skin;       // palette statics parse hex through Unity: touch them on the main thread
            _ = Art.Rainbow(0f);    // the ball art reads static data as well
            CleftGlows.Clear(); RidgeFires.Clear(); RiftGlows.Clear(); Craters.Clear(); GroundGlows.Clear();

            jobs = new ArtJobs();
            // UI pieces: straight alpha, centre pivot
            jLogo = Ui("MenuLogo", LogoArt.Build);
            jHero = Ui("MenuHeroBall", () => Art.BallPatternCanvas(640f));
            jHeroHoop = Ui("MenuHeroHoop", () => Art.HoopPatternCanvas(640f));
            jHeroShade = Ui("MenuHeroShade", () => Art.BallShadeCanvas(640f));
            jHeroHi = Ui("MenuHeroHi", () => Art.BallHighlightCanvas(640f));

            jobs.Start();
        }

        static ArtJobs.Job Ui(string name, System.Func<SdfCanvas> build)
        {
            var j = jobs.Add(name, build, Vector2.zero, false);
            j.Premultiply = false;   // UI images expect straight alpha
            j.MakeSprite = false;
            return j;
        }

        public static void End()
        {
            if (jobs == null) return;
            jobs.Complete();
            Debug.Log("[SoccerFight] menu art: " + jobs.Slowest(8));
            Logo = UiSprite(jLogo);
            HeroBall = UiSprite(jHero); HeroHoop = UiSprite(jHeroHoop); HeroShade = UiSprite(jHeroShade); HeroHighlight = UiSprite(jHeroHi);
            Moon = Peaks = Ridge = Waste = Ground = Smoke = null;
            jobs = null;
        }

        /// <summary>UI sprite (centre pivot) from a finished job.</summary>
        static Sprite UiSprite(ArtJobs.Job j)
        {
            if (j.Texture == null) return null;
            var s = Sprite.Create(j.Texture, new Rect(0, 0, j.Texture.width, j.Texture.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            s.name = j.Name;
            return s;
        }

        // ------------------------------------------------------------------ shared brushes

        /// <summary>
        /// A dead tree: a bent trunk that splits into bare, forking branches, some of them snapped
        /// off. Drawn as tapered capsules; the rim light of the canvas catches its moon side.
        /// </summary>
        static void Tree(SdfCanvas c, Vector2 b, float h, float lean, int seed, Color bark, float spread = 1f)
        {
            var r = new System.Random(seed);
            float R() => (float)r.NextDouble();
            float w0 = h * 0.055f;
            // trunk in three bent segments
            Vector2 p = b;
            float ang = 90f - lean;
            float len = h * 0.5f;
            Vector2 prev = p;
            float wPrev = w0;
            for (int s = 0; s < 3; s++)
            {
                ang += (R() - 0.5f) * 18f;
                Vector2 next = prev + MathUtil.Dir(ang) * len / 3f;
                float wNext = w0 * (1f - (s + 1) * 0.16f);
                Vector2 a0 = prev, a1 = next; float r0 = wPrev, r1 = wNext;
                c.Fill(q => Sdf.Tapered(q, a0, r0, a1, r1), bark, 0f, Around(a0, a1, r0 + 0.05f));
                prev = next;
                wPrev = wNext;
            }
            // roots flaring into the ground
            for (int k = -1; k <= 1; k += 2)
            {
                Vector2 a0 = b + new Vector2(0f, w0 * 0.6f), a1 = b + new Vector2(k * w0 * (2.2f + R()), -w0 * 0.3f);
                c.Fill(q => Sdf.Tapered(q, a0, w0 * 0.7f, a1, w0 * 0.15f), bark, 0f, Around(a0, a1, w0 + 0.05f));
            }
            Branch(c, prev, ang, h * 0.34f, wPrev, 4, r, bark, spread);
            // a couple of side limbs lower down the trunk, one of them snapped
            Vector2 mid = Vector2.Lerp(b, prev, 0.55f + R() * 0.15f);
            Branch(c, mid, ang + (R() > 0.5f ? 1f : -1f) * (40f + R() * 20f) * spread, h * 0.22f, w0 * 0.45f, 2, r, bark, spread);
            Vector2 low = Vector2.Lerp(b, prev, 0.3f + R() * 0.1f);
            float sa = ang + (R() > 0.5f ? 1f : -1f) * (55f + R() * 15f);
            Vector2 stub = low + MathUtil.Dir(sa) * h * 0.07f;
            c.Fill(q => Sdf.Tapered(q, low, w0 * 0.35f, stub, w0 * 0.22f), bark, 0f, Around(low, stub, w0 + 0.05f));
        }

        static void Branch(SdfCanvas c, Vector2 p, float ang, float len, float w, int depth, System.Random r, Color bark, float spread)
        {
            float R() => (float)r.NextDouble();
            // each limb bends once on its way
            Vector2 mid = p + MathUtil.Dir(ang + (R() - 0.5f) * 20f) * len * 0.5f;
            Vector2 end = mid + MathUtil.Dir(ang + (R() - 0.5f) * 26f) * len * 0.5f;
            float wm = w * 0.8f, we = w * 0.6f;
            c.Fill(q => Sdf.Tapered(q, p, w, mid, wm), bark, 0f, Around(p, mid, w + 0.04f));
            // snapped: a blunt end and nothing further
            bool snapped = depth < 3 && R() < 0.18f;
            if (snapped)
            {
                Vector2 e2 = Vector2.Lerp(mid, end, 0.4f);
                c.Fill(q => Sdf.Tapered(q, mid, wm, e2, wm * 0.85f), bark, 0f, Around(mid, e2, wm + 0.04f));
                return;
            }
            c.Fill(q => Sdf.Tapered(q, mid, wm, end, depth == 0 ? w * 0.08f : we), bark, 0f, Around(mid, end, wm + 0.04f));
            if (depth == 0) return;
            int n = R() < 0.35f ? 3 : 2;
            for (int i = 0; i < n; i++)
            {
                float side = n == 2 ? (i == 0 ? -1f : 1f) : (i - 1);
                float a = ang + side * (22f + R() * 22f) * spread + (R() - 0.5f) * 10f;
                Branch(c, end, a, len * (0.62f + R() * 0.16f), we * 0.78f, depth - 1, r, bark, spread);
            }
            // twigs along the limb
            if (depth <= 2)
                for (int k = 0; k < 2; k++)
                {
                    Vector2 at = Vector2.Lerp(p, end, 0.3f + R() * 0.5f);
                    Vector2 tip = at + MathUtil.Dir(ang + (R() > 0.5f ? 1f : -1f) * (35f + R() * 30f)) * len * (0.2f + R() * 0.15f);
                    float tw = Mathf.Max(0.004f, w * 0.3f);
                    c.Fill(q => Sdf.Tapered(q, at, tw, tip, tw * 0.2f), bark, 0f, Around(at, tip, tw + 0.03f));
                }
        }

        /// <summary>A burnt conifer: bare trunk with a few drooping stubs where the branches were.</summary>
        static void DeadPine(SdfCanvas c, Vector2 b, float h, Color col, System.Random r)
        {
            float R() => (float)r.NextDouble();
            float w = Mathf.Max(0.006f, h * 0.045f);
            float lean = (R() - 0.5f) * 8f;
            Vector2 top = b + MathUtil.Dir(90f - lean) * h * (R() < 0.3f ? 0.65f : 1f);
            c.Fill(q => Sdf.Tapered(q, b, w, top, w * 0.25f), col, 0f, Around(b, top, w + 0.03f));
            int n = 3 + r.Next(4);
            for (int i = 0; i < n; i++)
            {
                float t = 0.3f + 0.6f * (i + R() * 0.5f) / n;
                Vector2 at = Vector2.Lerp(b, top, t);
                float side = R() > 0.5f ? 1f : -1f;
                Vector2 tip = at + new Vector2(side * h * (0.1f + 0.12f * (1f - t)), -h * 0.05f);
                float sw = w * 0.45f;
                c.Fill(q => Sdf.Tapered(q, at, sw, tip, sw * 0.3f), col, 0f, Around(at, tip, sw + 0.02f));
            }
        }

        /// <summary>A stone with a moonlit top (vista units).</summary>
        static void Stone(SdfCanvas c, Vector2 at, Vector2 rad, Color body, Color lit, float wobble, int seed)
        {
            float s = seed * 0.37f;
            SdfCanvas.SdfFn f = q => Sdf.Ellipse(q, at, rad) + wobble * (Noise.Perlin(q.x * 9f + s, q.y * 9f) - 0.5f) * rad.y;
            Rect b = new Rect(at.x - rad.x * 1.4f, at.y - rad.y * 1.4f, rad.x * 2.8f, rad.y * 2.8f);
            c.Fill(f, body, 0f, b);
            // the upper right, turned to the moon, catches light; the underside sinks into shadow
            c.Paint(q => Mathf.Max(f(q) + rad.y * 0.18f, (at.y + rad.y * 0.1f - q.y) + (at.x - q.x) * 0.25f), lit, rad.y * 0.35f, b);
            c.Paint(q => Mathf.Max(f(q), q.y - (at.y - rad.y * 0.45f)), Mul(body, 0.55f), rad.y * 0.3f, b);
        }

        /// <summary>A jagged polyline crack; glowing: a dark gash with a line of crystal light inside.</summary>
        static void Crack(SdfCanvas c, Vector2[] pts, float w0, float w1, Color dark, Color glow, float glowK)
        {
            Rect b = Around(pts[0], pts[pts.Length - 1], w0 + 0.3f);
            foreach (var p in pts) b = Rect.MinMaxRect(Mathf.Min(b.xMin, p.x - w0 - 0.1f), Mathf.Min(b.yMin, p.y - w0 - 0.1f), Mathf.Max(b.xMax, p.x + w0 + 0.1f), Mathf.Max(b.yMax, p.y + w0 + 0.1f));
            int n = pts.Length;
            SdfCanvas.SdfFn f = q =>
            {
                float d = 99f;
                for (int s = 1; s < n; s++)
                {
                    float t0 = (s - 1) / (float)(n - 1), t1 = s / (float)(n - 1);
                    d = Mathf.Min(d, Sdf.Tapered(q, pts[s - 1], Mathf.Lerp(w0, w1, t0), pts[s], Mathf.Lerp(w0, w1, t1)));
                }
                return d;
            };
            c.Paint(q => f(q) - w0 * 0.8f, Mul(dark, 1.6f).WithAlpha(0.35f), w0, b);
            c.Paint(f, dark, 0.004f, b);
            if (glowK > 0f) c.Paint(q => f(q) + Mathf.Lerp(w0, w1, 0.5f) * 0.55f, glow.WithAlpha(glowK), 0.006f, b);
        }

        static Vector2[] Jagged(Vector2 a, Vector2 b, int n, float amp, System.Random r)
        {
            var pts = new Vector2[n];
            Vector2 d = b - a, side = new Vector2(-d.y, d.x).normalized;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                float off = (i == 0 || i == n - 1) ? 0f : ((float)r.NextDouble() - 0.5f) * 2f * amp;
                pts[i] = Vector2.Lerp(a, b, t) + side * off;
            }
            return pts;
        }

        // ------------------------------------------------------------------ the far massif (split summit, sheared peak)

        const float CleftX = 6.1f, CleftFloor = 2.0f, CleftSteep = 3.1f;

        static float PeakBack(float x)
        {
            float h = 1.1f + 1.5f * Ridged(x * 0.19f, 3.3f) + 0.35f * Ridged(x * 0.55f, 9.1f) + 0.14f * Fbm(x * 1.2f, 8.1f) + 0.05f * Fbm(x * 4.5f, 1.7f)
                      + 1.5f * G(x, 6.2f, 1.5f) + 0.9f * G(x, -5.6f, 2.2f) + 0.6f * G(x, -10.6f, 1.4f) + 0.7f * G(x, 10.1f, 1.3f)
                      - 1.25f * G(x, MoonPos.x - 0.2f, 1.5f) - 0.45f * G(x, -0.5f, 2f);
            // the split summit: its left half has slumped, a V is torn deep into the massif
            float dx = x - CleftX;
            h -= 0.5f * S01(-dx / 0.25f) * G(x, CleftX - 0.9f, 1.0f);
            h = Mathf.Min(h, CleftFloor + Mathf.Abs(dx) * CleftSteep + 0.05f * Fbm(x * 9f, 4.4f));
            // the peak on the far left, sheared off flat
            float shear = 2.4f + 0.05f * Fbm(x * 7f, 2.2f) + (x + 5.6f) * 0.05f;
            h = Mathf.Lerp(h, Mathf.Min(h, shear), S01(1f - Mathf.Abs(x + 5.6f) / 1.4f));
            return h;
        }

        static float PeakFront(float x) =>
            0.3f + 0.85f * Ridged(x * 0.27f, 7.7f) + 0.1f * Fbm(x * 1.8f, 2.9f) + 0.03f * Fbm(x * 6f, 3.3f) + 0.35f * S01((Mathf.Abs(x) - 5f) / 4f) - 0.5f * G(x, MoonPos.x, 1.8f);

        /// <summary>
        /// Rock relief: a ridged height field over the face, lit from the moon's side — how much
        /// this bit of rock turns towards the light (-1 .. 1). Gives the faces crags and ledges.
        /// </summary>
        static float Relief(Vector2 p, float seed, Vector2 freq, float toward)
        {
            const float e = 0.015f;
            float H(float x, float y) => 1f - Mathf.Abs(Fbm2(new Vector2(x * freq.x, y * freq.y), seed, 4));
            float h0 = H(p.x, p.y);
            float dx = (H(p.x + e, p.y) - h0) / e;
            float dy = (H(p.x, p.y + e) - h0) / e;
            Vector3 n = new Vector3(-dx * 0.18f, -dy * 0.18f, 1f).normalized;
            Vector3 l = new Vector3(toward * 0.72f, 0.5f, 0.48f).normalized;
            return Mathf.Clamp(Vector3.Dot(n, l) * 1.6f - 0.8f, -1f, 1f);
        }

        static SdfCanvas BuildPeaks()
        {
            var c = new SdfCanvas(new Rect(-13.5f, -1.2f, 27f, 5.9f), 76f);
            int W = c.Width;
            float left = c.UnitRect.xMin, ppu = c.Ppu, aa = 1f / ppu;
            var back = new float[W];
            var front = new float[W];
            for (int x = 0; x < W; x++)
            {
                float ux = left + (x + 0.5f) / ppu;
                back[x] = PeakBack(ux);
                front[x] = PeakFront(ux);
            }
            var slopeB = Slope(back, ppu);
            var slopeF = Slope(front, ppu);

            Color bBase = new Color(0.19f, 0.33f, 0.43f), bLit = new Color(0.47f, 0.65f, 0.74f), snow = new Color(0.8f, 0.9f, 0.96f);
            Color fBase = new Color(0.1f, 0.2f, 0.27f), fLit = new Color(0.27f, 0.43f, 0.51f);
            Color mist = new Color(0.36f, 0.56f, 0.64f), valley = new Color(0.09f, 0.18f, 0.24f);
            c.Field(p =>
            {
                int x = Mathf.Clamp((int)((p.x - left) * ppu), 0, W - 1);
                float aB = Mathf.Clamp01(0.5f + (back[x] - p.y) / aa);
                float aF = Mathf.Clamp01(0.5f + (front[x] - p.y) / aa);
                float a = Mathf.Max(aB, aF);
                if (a <= 0f) return Clear;
                // back range: crags and ledges lit from the moon's side, faint strata, snow on the
                // ledges that face up and in the gullies near the crests
                float toward = p.x < MoonPos.x ? 1f : -1f;
                float litB = Lit(slopeB[x], p.x);
                // (each range's relief is only worked out where that range shows)
                float rel = aB > 0f && aF < 1f ? Relief(p, 3.1f, new Vector2(2.3f, 1.5f), toward) : 0f;
                float fine = Noise.Perlin(p.x * 26f, p.y * 14f);
                float strata = 0.5f + 0.5f * Mathf.Sin(p.y * 16f + p.x * 1.2f + Fbm(p.x * 0.8f, 1.9f) * 3f);
                Color b = Color.Lerp(bBase, bLit, Mathf.Clamp01(0.3f + rel * 0.5f + litB * 0.45f));
                b = Mul(b, (0.9f + 0.1f * fine) * (0.96f + 0.06f * strata));
                float below = back[x] - p.y;
                float snowMask = S01((0.4f - below) / 0.25f) * S01((Noise.Perlin(p.x * 4.4f, p.y * 3.6f) - 0.28f) / 0.2f) * S01((back[x] - 2.0f) / 0.4f);
                snowMask = Mathf.Max(snowMask, S01((rel - 0.25f) / 0.25f) * S01((p.y - 2.1f) / 0.35f) * S01((Noise.Perlin(p.x * 2.2f, p.y * 5f) - 0.35f) / 0.15f));
                b = Color.Lerp(b, Mul(snow, 0.66f + 0.4f * Mathf.Clamp01(rel * 0.6f + litB + 0.3f)), snowMask * 0.85f);
                // the cleft: its walls glow with the crystal light welling up from deep inside
                float dx = p.x - CleftX;
                if (Mathf.Abs(dx) < 1.4f && aF < 0.99f)
                {
                    float wallDist = p.y >= CleftFloor ? Mathf.Abs(dx) - (p.y - CleftFloor) / CleftSteep : new Vector2(dx, (CleftFloor - p.y) * 1.6f).magnitude;
                    float glow = Mathf.Exp(-Mathf.Max(0f, wallDist) / 0.16f) * (0.55f + 0.45f * Noise.Perlin(p.x * 14f, p.y * 14f));
                    b = Color.Lerp(b, Cyan, glow * 0.75f);
                    b = Color.Lerp(b, Color.white, Mathf.Exp(-Mathf.Max(0f, wallDist) / 0.035f) * 0.4f);
                }
                // front range: darker, burnt forest, landslide scars
                float litF = Lit(slopeF[x], p.x);
                float relF = aF > 0f ? Relief(p, 5.7f, new Vector2(3.2f, 2.2f), toward) : 0f;
                Color f = Mul(Color.Lerp(fBase, fLit, Mathf.Clamp01(0.2f + relF * 0.4f + litF * 0.4f)), 0.9f + 0.1f * Noise.Perlin(p.x * 18f, p.y * 9f));
                float scar = S01((Noise.Perlin(p.x * 1.1f + 4f, 2.2f) - 0.62f) / 0.08f) * S01((front[x] - p.y - 0.05f) / 0.1f) * (0.6f + 0.4f * Noise.Perlin(p.x * 30f, p.y * 4f));
                f = Color.Lerp(f, Mul(fLit, 1.15f), scar * 0.45f);
                Color col = Color.Lerp(b, f, aF);
                // mist pooling in the valley, then the valley floor far below
                col = Color.Lerp(col, mist, S01(1f - (p.y + 0.05f) / 0.85f) * 0.5f);
                col = Color.Lerp(col, valley, S01((-0.05f - p.y) / 0.9f));
                col.a = a * EnvironmentArt.EdgeFade(p.x, 13.5f, 1.6f);
                return col;
            });

            var r = new System.Random(91);
            float R() => (float)r.NextDouble();
            // fracture veins running from the cleft down the face, glowing
            for (int i = 0; i < 5; i++)
            {
                Vector2 a = new Vector2(CleftX + (R() - 0.5f) * 0.3f, CleftFloor - 0.05f);
                Vector2 bEnd = a + new Vector2((R() - 0.5f) * 2.2f, -(0.7f + R() * 0.9f));
                var pts = Jagged(a, bEnd, 6, 0.12f, r);
                Crack(c, pts, 0.022f, 0.006f, new Color(0.04f, 0.1f, 0.13f), new Color(0.6f, 1f, 1f), 0.9f);
                for (int k = 1; k < pts.Length; k += 2) CleftGlows.Add(new Vector3(pts[k].x, pts[k].y, 0.16f));
            }
            CleftGlows.Add(new Vector3(CleftX, CleftFloor + 0.2f, 1.6f));
            CleftGlows.Add(new Vector3(CleftX, CleftFloor + 0.7f, 0.9f));

            // dead forest on the front range, in patches
            Color pine = new Color(0.06f, 0.13f, 0.17f);
            for (int i = 0; i < 520; i++)
            {
                float px = left + 1f + R() * 25f;
                if (Noise.Perlin(px * 0.5f, 4.4f) < 0.4f) continue;
                int xi = Mathf.Clamp((int)((px - left) * ppu), 0, W - 1);
                float by = front[xi] - 0.02f - R() * R() * Mathf.Max(0f, front[xi] - 0.2f);
                DeadPine(c, new Vector2(px, by), 0.07f + R() * 0.1f, Mul(pine, 0.85f + R() * 0.3f), r);
            }
            c.RimLight(new Vector2(0.03f, 0.04f), new Color(0.76f, 0.9f, 0.96f), 0.75f);
            return c;
        }

        // ------------------------------------------------------------------ the burnt ridge

        static float RidgeH(float x) =>
            0.32f + 0.8f * Ridged(x * 0.36f, 5.1f) + 0.1f * Fbm(x * 2.3f, 1.3f) + 0.04f * Fbm(x * 8f, 6.6f)
            + 0.95f * S01((Mathf.Abs(x) - 3.2f) / 5.5f) - 0.28f * G(x, 0.6f, 2.2f) + 0.35f * G(x, -8.7f, 1.2f)
            - 0.34f * G(x, -3.0f, 0.42f);   // a blast crater bitten out of the crest

        static SdfCanvas BuildRidge()
        {
            var c = new SdfCanvas(new Rect(-13f, -1.3f, 26f, 3.6f), 72f);
            int W = c.Width;
            float left = c.UnitRect.xMin, ppu = c.Ppu, aa = 1f / ppu;
            var h = new float[W];
            for (int x = 0; x < W; x++) h[x] = RidgeH(left + (x + 0.5f) / ppu);
            var slope = Slope(h, ppu);

            Color rock = new Color(0.12f, 0.21f, 0.26f), rockLit = new Color(0.3f, 0.43f, 0.49f), burnt = new Color(0.05f, 0.09f, 0.11f);
            Color mist = new Color(0.3f, 0.5f, 0.58f);
            c.Field(p =>
            {
                int x = Mathf.Clamp((int)((p.x - left) * ppu), 0, W - 1);
                float a = Mathf.Clamp01(0.5f + (h[x] - p.y) / aa);
                if (a <= 0f) return Clear;
                float below = h[x] - p.y;
                float lit = Lit(slope[x], p.x, 0.7f);
                // cliffs: crags and ledges lit where they face the moon, strata running through them
                float rel = Relief(p, 8.3f, new Vector2(3.4f, 2.6f), p.x < MoonPos.x ? 1f : -1f);
                float strata = 0.5f + 0.5f * Mathf.Sin(p.y * 24f + p.x * 1.5f + Fbm(p.x * 1.2f, 3.1f) * 4f);
                Color col = Color.Lerp(rock, rockLit, Mathf.Clamp01(0.2f + rel * 0.6f + lit * 0.5f));
                col = Mul(col, (0.94f + 0.08f * strata) * (0.94f + 0.1f * Noise.Perlin(p.x * 40f, p.y * 30f)));
                // scorched patches, darkest just under the crest
                float scorch = S01((Fbm2(p * 0.9f, 7.7f) + 0.1f) / 0.3f) * S01((0.9f - below) / 0.5f);
                col = Color.Lerp(col, burnt, scorch * 0.55f);
                // lighter scree fans under the notches
                float scree = S01((Noise.Perlin(p.x * 0.9f + 12f, 1.1f) - 0.6f) / 0.1f) * S01((below - 0.15f) / 0.2f) * (0.5f + 0.5f * Noise.Perlin(p.x * 50f, p.y * 50f));
                col = Color.Lerp(col, Mul(rockLit, 0.9f), scree * 0.35f);
                // the crater: glowing embers in its bowl
                float crater = Mathf.Exp(-Sq((p.x + 3f) / 0.35f) - Sq((p.y - (h[x] - 0.05f)) / 0.14f));
                col = Color.Lerp(col, Ember, crater * 0.6f * (0.6f + 0.4f * Noise.Perlin(p.x * 30f, p.y * 30f)));
                // mist in the valley at the foot
                col = Color.Lerp(col, mist, S01(1f - (p.y + 0.9f) / 1.1f) * 0.45f);
                col.a = a * EnvironmentArt.EdgeFade(p.x, 13f, 1.4f);
                return col;
            });

            var r = new System.Random(57);
            float R() => (float)r.NextDouble();
            // the dead forest: burnt stems along the crests and down the slopes
            Color pine = new Color(0.03f, 0.07f, 0.09f);
            for (int i = 0; i < 380; i++)
            {
                float px = left + 0.5f + R() * 25f;
                if (Noise.Perlin(px * 0.45f, 9.9f) < 0.36f) continue;
                if (Mathf.Abs(px + 3f) < 0.4f) continue;   // nothing survived in the crater
                int xi = Mathf.Clamp((int)((px - left) * ppu), 0, W - 1);
                float by = h[xi] - 0.02f - R() * R() * 0.6f;
                DeadPine(c, new Vector2(px, by), 0.1f + R() * 0.16f, Mul(pine, 0.8f + R() * 0.4f), r);
            }
            // boulders on the slopes
            for (int i = 0; i < 40; i++)
            {
                float px = left + 0.5f + R() * 25f;
                int xi = Mathf.Clamp((int)((px - left) * ppu), 0, W - 1);
                float by = h[xi] - 0.05f - R() * 0.5f;
                float s = 0.03f + R() * 0.05f;
                Stone(c, new Vector2(px, by), new Vector2(s * 1.4f, s), new Color(0.09f, 0.16f, 0.2f), new Color(0.24f, 0.36f, 0.41f), 0.6f, i);
            }
            RidgeFires.Add(new Vector3(-3.0f, RidgeH(-3.0f) + 0.02f, 1f));
            RidgeFires.Add(new Vector3(5.35f, RidgeH(5.35f) - 0.02f, 0.6f));
            // a smaller fire on the far right slope: embers painted into the rock
            foreach (var f in RidgeFires)
            {
                Vector2 fc = new Vector2(f.x, f.y);
                for (int k = 0; k < 16; k++)
                {
                    Vector2 e = fc + new Vector2((R() - 0.5f) * 0.5f * f.z, -R() * 0.16f * f.z);
                    float s = 0.008f + R() * 0.014f;
                    c.Paint(q => Sdf.Circle(q, e, s), Color.Lerp(Ember, new Color(1f, 0.85f, 0.5f), R()), 0.006f, new Rect(e.x - 0.05f, e.y - 0.05f, 0.1f, 0.1f));
                }
            }
            c.RimLight(new Vector2(0.025f, 0.035f), new Color(0.62f, 0.84f, 0.9f), 0.7f);
            return c;
        }

        // ------------------------------------------------------------------ the wrecked plain

        /// <summary>The plain's far edge (its horizon, local y ≈ 0); the plain reaches down to the plateau from there.</summary>
        static float Plain(float x) => 0.07f * Fbm(x * 0.4f, 2.2f) + 0.03f * Fbm(x * 1.7f, 5.5f);

        /// <summary>How big things are at this height of the plain: far (y ≈ 0) small, near the plateau large.</summary>
        static float Persp(float y) => Mathf.Lerp(0.28f, 1.25f, Mathf.Clamp01(-y / 2.8f));

        struct Slab { public Vector2 Base; public float W, T, Angle; }

        static readonly Slab[] Slabs =
        {
            new Slab { Base = new Vector2(-6.9f, -2.05f), W = 2.5f, T = 0.3f, Angle = 27f },
            new Slab { Base = new Vector2(-2.4f, -0.72f), W = 0.95f, T = 0.12f, Angle = -23f },
            new Slab { Base = new Vector2(3.75f, -0.45f), W = 1.35f, T = 0.15f, Angle = 33f },
            new Slab { Base = new Vector2(-8.8f, -0.28f), W = 0.6f, T = 0.07f, Angle = 16f },
            new Slab { Base = new Vector2(7.4f, -0.22f), W = 0.55f, T = 0.06f, Angle = -18f },
            new Slab { Base = new Vector2(1.5f, -0.14f), W = 0.36f, T = 0.045f, Angle = 12f },
        };

        static SdfCanvas BuildWaste()
        {
            var c = new SdfCanvas(new Rect(-12.5f, -2.95f, 25f, 3.75f), 80f);
            float ppu = c.Ppu;
            Color far = new Color(0.27f, 0.4f, 0.44f), mid = new Color(0.15f, 0.24f, 0.27f), nearC = new Color(0.07f, 0.12f, 0.14f), dust = new Color(0.2f, 0.29f, 0.31f);
            c.Field(p =>
            {
                float top = Plain(p.x);
                float a = Mathf.Clamp01(0.5f + (top - p.y) * ppu);
                if (a <= 0f) return Clear;
                // the plain seen at a slant: the far strip pale with mist, darkening towards the viewer
                float v = Mathf.Clamp01((top - p.y) / 2.9f);
                float k = Persp(p.y);
                Color col = v < 0.3f ? Color.Lerp(far, mid, S01(v / 0.3f)) : Color.Lerp(mid, nearC, S01((v - 0.3f) / 0.7f));
                // ground grain in perspective: long streaks far away, coarse clods near
                Vector2 q = new Vector2(p.x / k, p.y / (k * k));
                float grain = Noise.Perlin(q.x * 4f, q.y * 9f) * 0.55f + Noise.Perlin(q.x * 15f, q.y * 34f) * 0.3f + Noise.Perlin(q.x * 40f, q.y * 80f) * 0.15f;
                col = Mul(col, 0.84f + 0.3f * grain);
                // drifts of ash and scorched ground
                float ashDrift = S01((Fbm2(new Vector2(q.x * 0.5f, q.y * 1.4f), 4.1f) + 0.05f) / 0.25f);
                col = Color.Lerp(col, dust, ashDrift * 0.35f * (1f - v * 0.6f));
                float scorch = S01((Fbm2(new Vector2(q.x * 0.6f, q.y * 1.8f), 9.3f) - 0.12f) / 0.2f);
                col = Color.Lerp(col, Mul(nearC, 0.55f), scorch * 0.45f);
                col = Color.Lerp(col, new Color(0.38f, 0.56f, 0.62f), S01(1f - (top - p.y) / 0.12f) * 0.45f);
                col.a = a * EnvironmentArt.EdgeFade(p.x, 12.5f, 1.2f);
                return col;
            });

            var r = new System.Random(77);
            float R() => (float)r.NextDouble();

            // the rift: a glowing crack running out of the distance, past the player, off the left edge
            {
                var pts = new[]
                {
                    new Vector2(2.7f, -0.04f), new Vector2(1.9f, -0.16f), new Vector2(1.0f, -0.31f), new Vector2(-0.1f, -0.5f),
                    new Vector2(-1.3f, -0.72f), new Vector2(-2.6f, -0.98f), new Vector2(-3.9f, -1.28f), new Vector2(-5.3f, -1.6f),
                    new Vector2(-6.7f, -1.94f), new Vector2(-8.3f, -2.32f), new Vector2(-10.1f, -2.74f), new Vector2(-12.4f, -3.1f),
                };
                for (int i = 1; i < pts.Length - 1; i++)
                {
                    float k = Persp(pts[i].y);
                    pts[i] += new Vector2((R() - 0.5f) * 0.3f * k, (R() - 0.5f) * 0.06f * k);
                }
                // a jagged edge: every segment split once more
                var fine = new Vector2[pts.Length * 2 - 1];
                for (int i = 0; i < pts.Length; i++)
                {
                    fine[i * 2] = pts[i];
                    if (i < pts.Length - 1)
                    {
                        float k = Persp(pts[i].y);
                        fine[i * 2 + 1] = Vector2.Lerp(pts[i], pts[i + 1], 0.5f) + new Vector2((R() - 0.5f) * 0.12f, (R() - 0.5f) * 0.1f) * k;
                    }
                }
                Crack(c, fine, 0.01f, 0.2f, new Color(0.02f, 0.05f, 0.07f), new Color(0.55f, 1f, 1f), 1f);
                // side cracks branching off the rift
                for (int i = 2; i < pts.Length - 1; i++)
                {
                    float k = Persp(pts[i].y);
                    float dir = i % 2 == 0 ? 1f : -1f;
                    var side = Jagged(pts[i], pts[i] + new Vector2(dir * (0.5f + R() * 0.7f), -(0.05f + R() * 0.15f)) * k, 5, 0.05f * k, r);
                    Crack(c, side, 0.022f * k, 0.004f, new Color(0.02f, 0.05f, 0.07f), new Color(0.55f, 1f, 1f), 0.75f);
                }
                for (int i = 1; i < pts.Length; i++)
                    RiftGlows.Add(new Vector3(pts[i].x, pts[i].y, 1.1f * Persp(pts[i].y)));
            }

            // craters: a thrown-up rim, a dark bowl, embers in the bottom
            Vector3[] craters = { new Vector3(-4.6f, -1.3f, 1.15f), new Vector3(2.75f, -1.18f, 1.2f), new Vector3(-1.8f, -0.4f, 0.55f), new Vector3(8.1f, -0.33f, 0.8f), new Vector3(-9.7f, -0.55f, 0.75f) };
            foreach (var cr in craters)
            {
                Vector2 cc = new Vector2(cr.x, cr.y);
                Vector2 rad = new Vector2(cr.z, cr.z * 0.2f);
                Rect b = new Rect(cc.x - rad.x * 1.5f, cc.y - rad.y * 3f, rad.x * 3f, rad.y * 6f);
                float seed = cr.x;
                c.Fill(q => Sdf.Ellipse(q, cc + new Vector2(0f, rad.y * 0.25f), rad * 1.28f) + 0.03f * rad.x * (Noise.Perlin(q.x * 9f / rad.x + seed, q.y * 20f) - 0.5f),
                    q => Mul(Color.Lerp(new Color(0.12f, 0.19f, 0.22f), new Color(0.32f, 0.44f, 0.47f), S01((q.y - cc.y) / (rad.y * 1.2f))), 0.9f + 0.2f * Noise.Perlin(q.x * 30f, q.y * 30f)), 0.004f, b);
                c.Fill(q => Sdf.Ellipse(q, cc, rad) + 0.02f * rad.x * (Noise.Perlin(q.x * 14f / rad.x, seed) - 0.5f),
                    q => Color.Lerp(new Color(0.03f, 0.045f, 0.055f), new Color(0.1f, 0.15f, 0.17f), S01((cc.y - q.y) / rad.y * 0.5f + 0.5f)), 0.004f, b);
                c.Paint(q => Sdf.Ellipse(q, cc - new Vector2(0f, rad.y * 0.25f), rad * 0.5f), Ember.WithAlpha(0.35f), rad.y * 0.8f, b);
                // rays of ejected rubble around it
                for (int k = 0; k < 14; k++)
                {
                    float ang = R() * 360f;
                    Vector2 dir = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad) * rad.x, Mathf.Sin(ang * Mathf.Deg2Rad) * rad.y);
                    Vector2 at = cc + dir * (1.35f + R() * 0.6f);
                    float s = (0.012f + R() * 0.03f) * Persp(at.y);
                    Stone(c, at, new Vector2(s * 1.5f, s), new Color(0.09f, 0.15f, 0.18f), new Color(0.32f, 0.44f, 0.48f), 0.8f, k + (int)(seed * 31f));
                }
                for (int k = 0; k < 12; k++)
                {
                    Vector2 e = cc + new Vector2((R() - 0.5f) * rad.x * 1.1f, (R() - 0.7f) * rad.y * 0.8f);
                    float s = (0.006f + R() * 0.01f) * Persp(e.y) * 1.3f;
                    c.Paint(q => Sdf.Circle(q, e, s), Color.Lerp(Ember, new Color(1f, 0.85f, 0.5f), R()), 0.005f, new Rect(e.x - 0.05f, e.y - 0.05f, 0.1f, 0.1f));
                }
                Craters.Add(cr);
            }

            // slabs of bedrock forced up out of the plain
            foreach (var s in Slabs)
            {
                float ang = s.Angle;
                // it rises out of the ground at Base, up to the right (positive angle) or to the left
                Vector2 up = ang >= 0f ? MathUtil.Dir(ang) : -MathUtil.Dir(ang);
                Vector2 centre = s.Base + up * s.W * 0.42f + new Vector2(0f, s.T * 0.2f);
                float seed = s.Base.x;
                float kk = s.W;
                SdfCanvas.SdfFn body = q =>
                {
                    float d = Sdf.Box(q, centre, new Vector2(s.W * 0.5f, s.T), 0.01f, ang);
                    d += 0.03f * kk * (Noise.Perlin(q.x * 9f / kk + seed, q.y * 9f / kk) - 0.5f) + 0.012f * kk * (Noise.Perlin(q.x * 35f / kk, q.y * 35f / kk + seed) - 0.5f);
                    // buried: nothing below the ground line at its foot
                    return Mathf.Max(d, s.Base.y - q.y + 0.03f * kk * Noise.Perlin(q.x * 14f / kk, seed));
                };
                Rect b = new Rect(centre.x - s.W, centre.y - s.W * 0.8f, s.W * 2f, s.W * 1.6f);
                c.Fill(body, q =>
                {
                    Vector2 l = MathUtil.Rotate(q - centre, -ang);
                    float across = l.y / s.T;                      // -1 underside .. 1 top face
                    float str = 0.5f + 0.5f * Mathf.Sin(l.y / kk * 70f + Noise.Perlin(l.x / kk * 3f, seed) * 3f);
                    Color face = Color.Lerp(new Color(0.09f, 0.16f, 0.2f), new Color(0.17f, 0.26f, 0.3f), str * 0.5f + 0.25f * Noise.Perlin(l.x / kk * 8f, l.y / kk * 30f));
                    // the upper face catches the moon, the snapped end shows fresh rock
                    face = Color.Lerp(face, new Color(0.38f, 0.52f, 0.56f), S01((across - 0.5f) / 0.3f));
                    face = Color.Lerp(face, new Color(0.26f, 0.36f, 0.38f), S01((Mathf.Abs(l.x) - s.W * 0.46f) / (s.W * 0.04f)) * S01(across + 0.5f) * 0.6f);
                    face = Mul(face, 0.88f + 0.22f * Noise.Perlin(q.x * 30f / kk, q.y * 30f / kk));
                    return face;
                }, 0f, b);
                // cracks across the slab, one of them glowing
                for (int k = 0; k < 2; k++)
                {
                    Vector2 ca = centre + MathUtil.Rotate(new Vector2((k == 0 ? -0.1f : 0.22f) * s.W, s.T * 0.95f), ang);
                    Vector2 cb = centre + MathUtil.Rotate(new Vector2((k == 0 ? 0.04f : 0.16f) * s.W, -s.T * 0.95f), ang);
                    Crack(c, Jagged(ca, cb, 4, 0.02f * kk, r), 0.012f * kk, 0.004f, new Color(0.02f, 0.05f, 0.06f), new Color(0.55f, 1f, 1f), k == 0 ? 0.8f : 0f);
                }
                // rubble heaped at its foot
                for (int k = 0; k < 12; k++)
                {
                    float rs = (0.02f + R() * 0.05f) * Persp(s.Base.y) * 1.2f;
                    Vector2 at = s.Base + new Vector2((R() - 0.5f) * s.W * 1.4f, -R() * 0.1f * Persp(s.Base.y));
                    Stone(c, at, new Vector2(rs * 1.5f, rs), new Color(0.09f, 0.15f, 0.18f), new Color(0.33f, 0.45f, 0.49f), 0.8f, k + (int)(seed * 10f));
                }
            }

            // dead trees on the plain (sized by distance), and stumps where others broke
            Color bark = new Color(0.035f, 0.07f, 0.09f);
            (Vector2 at, float h, float lean, int seed)[] trees =
            {
                (new Vector2(-9.2f, -2.5f), 2.7f, -9f, 11), (new Vector2(6.7f, -2.05f), 2.2f, 10f, 13),
                (new Vector2(-3.5f, -0.98f), 1.3f, 8f, 23), (new Vector2(2.05f, -0.82f), 1.05f, -12f, 37), (new Vector2(6.0f, -0.5f), 0.85f, 14f, 41),
                (new Vector2(-1.0f, -0.28f), 0.5f, -6f, 53), (new Vector2(4.9f, -0.2f), 0.45f, 9f, 59), (new Vector2(-6.3f, -0.36f), 0.6f, -4f, 61),
                (new Vector2(9.6f, -0.3f), 0.6f, 6f, 67), (new Vector2(-11.2f, -0.42f), 0.55f, -10f, 71),
            };
            foreach (var t in trees)
            {
                float depth = Mathf.Clamp01(-t.at.y / 2.8f);
                Tree(c, t.at, t.h, t.lean, t.seed, Color.Lerp(Mul(bark, 1.9f), bark, depth));
            }
            for (int i = 0; i < 26; i++)
            {
                float x = -12f + R() * 24f;
                float y = Plain(x) - 0.1f - R() * R() * 2.6f;
                float k = Persp(y);
                float hh = (0.1f + R() * 0.12f) * k;
                float w = hh * 0.22f;
                Vector2 a = new Vector2(x, y), bb = new Vector2(x + (R() - 0.5f) * 0.03f, y + hh);
                c.Fill(q => Sdf.Tapered(q, a, w, bb, w * 0.85f) + w * 0.4f * (q.y > bb.y - w ? Noise.Perlin(q.x * 90f / k, 1f) : 0f),
                    Color.Lerp(Mul(bark, 1.9f), bark, Mathf.Clamp01(-y / 2.8f)), 0f, Around(a, bb, w + 0.05f));
            }
            // boulders and rubble strewn over the plain, bigger the nearer they lie
            for (int i = 0; i < 150; i++)
            {
                float x = -12.2f + R() * 24.4f;
                float y = Plain(x) - 0.05f - R() * 2.8f;
                float k = Persp(y);
                float s = (0.015f + R() * R() * 0.07f) * k;
                Stone(c, new Vector2(x, y), new Vector2(s * (1.2f + R() * 0.6f), s), new Color(0.08f, 0.14f, 0.17f), new Color(0.29f, 0.41f, 0.45f), 0.8f, i + 300);
            }
            c.RimLight(new Vector2(0.02f, 0.03f), new Color(0.6f, 0.84f, 0.9f), 0.6f);
            return c;
        }

        // ------------------------------------------------------------------ the plateau the player stands on

        /// <summary>Back edge of the plateau (ground-local; the player's boots stand at y = 0 around x = 0).</summary>
        public static float GroundTop(float x) =>
            0.025f * Fbm(x * 2.5f, 1.1f) * S01((Mathf.Abs(x) - 1.2f) / 1f) + 0.2f * S01((Mathf.Abs(x) - 5.5f) / 4.5f) + 0.06f * G(x, -3.4f, 0.8f);

        /// <summary>Cellular noise: distance to the nearest feature point and the gap to the second nearest (cracks where it is small).</summary>
        static float Cells(Vector2 p, out float edge, out int id)
        {
            int cx = Mathf.FloorToInt(p.x), cy = Mathf.FloorToInt(p.y);
            float f1 = 9f, f2 = 9f;
            id = 0;
            for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    int gx = cx + i, gy = cy + j;
                    int h = gx * 73856093 ^ gy * 19349663;
                    Vector2 fp = new Vector2(gx + Hash01(h), gy + Hash01(h + 17));
                    float d = (p - fp).magnitude;
                    if (d < f1) { f2 = f1; f1 = d; id = h; }
                    else if (d < f2) f2 = d;
                }
            edge = f2 - f1;
            return f1;
        }

        static SdfCanvas BuildGround()
        {
            var c = new SdfCanvas(new Rect(-12.5f, -2.6f, 25f, 3.2f), 100f);
            float ppu = c.Ppu;
            Color earth = new Color(0.19f, 0.28f, 0.3f), earthLit = new Color(0.31f, 0.43f, 0.45f), crackCol = new Color(0.02f, 0.05f, 0.06f);
            Color face = new Color(0.07f, 0.12f, 0.14f), faceDeep = new Color(0.015f, 0.035f, 0.045f);
            c.Field(p =>
            {
                float top = GroundTop(p.x);
                float a = Mathf.Clamp01(0.5f + (top - p.y) * ppu);
                if (a <= 0f) return Clear;
                float v = top - p.y;
                Color col;
                if (v < GroundBand)
                {
                    // the top face at a slant: dried, cracked earth; cells squashed by perspective
                    float yy = 1f - v / GroundBand;               // 1 back edge .. 0 front lip
                    Vector2 q = new Vector2(p.x * 2.4f, v * 7.5f / (0.45f + 0.55f * (1f - yy)));
                    Cells(q, out float edge, out int id);
                    float cellTone = Hash01(id);
                    col = Color.Lerp(earth, earthLit, 0.25f + 0.35f * yy + 0.2f * cellTone);
                    col = Mul(col, 0.9f + 0.16f * Noise.Perlin(p.x * 30f, v * 80f) + 0.05f * Noise.Perlin(p.x * 3f, v * 6f));
                    // scorched patches
                    float scorch = S01((Fbm2(new Vector2(p.x * 0.6f, v * 3f), 3.7f) - 0.1f) / 0.25f);
                    col = Color.Lerp(col, Mul(earth, 0.45f), scorch * 0.5f);
                    // the cracks; some of them carry the crystal light
                    float crackW = 0.06f + 0.03f * (1f - yy);
                    float crack = 1f - S01((edge - crackW * 0.4f) / crackW);
                    float glowing = S01((Noise.Perlin(p.x * 0.55f + 3f, 7.1f) - 0.52f) / 0.1f) * (Mathf.Abs(p.x) < 1.3f ? 0.25f : 1f);
                    col = Color.Lerp(col, crackCol, crack * 0.9f);
                    col = Color.Lerp(col, Cyan, crack * glowing * S01((0.35f - edge) / 0.3f) * 0.75f);
                    // the lip: rounded over, lit, then falling into the face
                    col = Color.Lerp(col, Mul(earthLit, 1.08f), S01((v - (GroundBand - 0.05f)) / 0.04f) * 0.6f);
                }
                else
                {
                    // the broken front face: raw rock in strata, darker the deeper it goes
                    float e = v - GroundBand;
                    float deep = S01(e / 1.8f);
                    float strata = 0.5f + 0.5f * Mathf.Sin(p.y * 26f + Fbm(p.x * 1.4f, 2.4f) * 5f);
                    float crag = Noise.Perlin(p.x * 11f, p.y * 2f);
                    col = Color.Lerp(face, faceDeep, deep);
                    col = Mul(col, (0.8f + 0.35f * crag) * (0.92f + 0.14f * strata * (1f - deep)));
                    col = Color.Lerp(col, Mul(face, 1.6f), S01(1f - e / 0.06f) * 0.6f);
                }
                col.a = a;
                return col;
            });

            var r = new System.Random(31);
            float R() => (float)r.NextDouble();

            // a few deep cracks running over the lip and down the face, glowing inside
            float[] xs = { -7.8f, -3.6f, 2.4f, 5.9f, 9.6f };
            foreach (float x0 in xs)
            {
                float top = GroundTop(x0);
                var pts = Jagged(new Vector2(x0, top - 0.04f), new Vector2(x0 + (R() - 0.5f) * 0.7f, top - GroundBand - 0.5f - R() * 0.7f), 7, 0.08f, r);
                Crack(c, pts, 0.028f, 0.006f, crackCol, new Color(0.55f, 1f, 1f), 0.95f);
                for (int k = 1; k < pts.Length; k += 2) GroundGlows.Add(new Vector3(pts[k].x, pts[k].y, 0.35f));
            }
            // glowing spots in the cracked surface (for the vista's glow sprites)
            for (float x = -12f; x < 12f; x += 0.7f)
            {
                if (Noise.Perlin(x * 0.55f + 3f, 7.1f) < 0.6f || Mathf.Abs(x) < 1.4f) continue;
                GroundGlows.Add(new Vector3(x + (R() - 0.5f) * 0.4f, GroundTop(x) - 0.08f - R() * (GroundBand - 0.15f), 0.45f));
            }

            // rubble on the surface — the middle stays clear where the player stands
            for (int i = 0; i < 90; i++)
            {
                float x = -12.2f + R() * 24.4f;
                if (Mathf.Abs(x) < 1.7f) continue;
                float v = R() * (GroundBand - 0.06f);
                float s = (0.025f + R() * R() * 0.09f) * (0.7f + 0.6f * (v / GroundBand));
                Vector2 at = new Vector2(x, GroundTop(x) - v + s * 0.4f);
                Stone(c, at, new Vector2(s * (1.2f + R() * 0.7f), s), new Color(0.12f, 0.19f, 0.21f), new Color(0.38f, 0.5f, 0.52f), 0.9f, i + 700);
            }
            // bigger broken boulders towards the sides
            for (int i = 0; i < 9; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float x = side * (3.2f + R() * 8f);
                float s = 0.09f + R() * 0.12f;
                Vector2 at = new Vector2(x, GroundTop(x) - 0.1f - R() * 0.25f + s * 0.5f);
                Stone(c, at, new Vector2(s * (1.3f + R() * 0.5f), s), new Color(0.1f, 0.17f, 0.2f), new Color(0.36f, 0.48f, 0.51f), 1.1f, i + 900);
            }
            // dead roots torn out of the face
            Color root = new Color(0.02f, 0.05f, 0.06f);
            for (int i = 0; i < 22; i++)
            {
                float sx = -12f + R() * 24f;
                Vector2 prev = new Vector2(sx, GroundTop(sx) - GroundBand - 0.03f);
                float wid = 0.02f + R() * 0.025f;
                int segs = 5 + r.Next(6);
                for (int s = 0; s < segs; s++)
                {
                    float t = s / (float)segs;
                    Vector2 next = prev + new Vector2((R() - 0.5f) * 0.12f, -(0.08f + R() * 0.1f));
                    float w0 = wid * (1f - t * 0.8f);
                    Vector2 p0 = prev, p1 = next;
                    c.Fill(q => Sdf.Capsule(q, p0, p1, w0), root, 0f, Around(p0, p1, 0.08f));
                    prev = next;
                }
            }
            c.RimLight(new Vector2(0.02f, 0.03f), new Color(0.62f, 0.85f, 0.88f), 0.55f);
            return c;
        }

        // ------------------------------------------------------------------ smoke puff

        static SdfCanvas BuildSmoke()
        {
            var c = new SdfCanvas(new Rect(-1f, -1f, 2f, 2f), 64f);
            c.Field(p =>
            {
                float n = Fbm2(p * 1.6f, 6.2f, 5) * 0.5f + 0.5f;
                float fall = S01(1f - (p.magnitude + (n - 0.5f) * 0.5f) / 0.9f);
                float a = fall * (0.55f + 0.45f * n);
                // lit from above, darker underneath
                float shade = 0.75f + 0.25f * S01((p.y + 0.6f) / 1.2f);
                return new Color(shade, shade, shade, Mathf.Clamp01(a));
            });
            return c;
        }
    }
}
