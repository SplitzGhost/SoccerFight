using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// The title screen's own world: a broken land at sunset. A cracked planet hangs over layered
    /// lavender peaks, floating islands with waterfalls drift in front of a ruined stadium whose
    /// floodlights still burn, and the foreground is a split-open pitch with glowing fissures,
    /// framed by big leaves. The sun sits low right behind the menu's player.
    ///
    /// Everything is painted in UI pixels of a 2200 × 1240 stage (centre origin) on worker threads
    /// while the game boots; MenuBackdrop places and animates the pieces.
    /// </summary>
    public static class MenuScenery
    {
        public const float StageW = 2200f, StageH = 1240f;
        public static readonly Vector2 Sun = new Vector2(0f, -70f);
        public static readonly Vector2 PlanetPos = new Vector2(520f, 300f);

        /// <summary>A big version of the ball for the kick that starts the game.</summary>
        public static Sprite HeroBall, HeroShade, HeroHighlight;
        public static Sprite Sky, Planet, Peaks, Hills, Stadium, Ground, Canopy, Ferns, IslandBig, IslandSmall, Crystal, BirdUp, BirdDown;
        public static Sprite[] Clouds, Shards;
        public static Texture2D Waterfall;

        /// <summary>Floodlight heads on the stadium layer (stage px).</summary>
        public static readonly List<Vector2> Lamps = new List<Vector2>();
        /// <summary>Glowing fissures in the ground (stage px) — embers rise from here.</summary>
        public static readonly List<Vector2> Fissures = new List<Vector2>();
        /// <summary>Where the waterfall leaves the big island (island-local px) and how long it falls.</summary>
        public static Vector2 FallTop;
        public static Vector2 LogoBall;

        public static float GroundTopAt(float x) => -176f + 36f * Sq(x / 1100f) + 6f * Mathf.Sin(x * 0.011f);

        static ArtJobs jobs;
        static ArtJobs.Job jSky, jPlanet, jPeaks, jHills, jStadium, jGround, jCanopy, jFerns, jIslandBig, jIslandSmall, jCrystal, jBirdUp, jBirdDown, jFall, jLogo;
        static ArtJobs.Job[] jClouds, jShards;
        static ArtJobs.Job jHero, jHeroShade, jHeroHi;
        public static Sprite Logo;

        static readonly Color Clear = new Color(0f, 0f, 0f, 0f);
        static float Sq(float v) => v * v;
        static float S01(float v) => MathUtil.Smooth01(v);
        static Color Mul(Color c, float f) { c.r *= f; c.g *= f; c.b *= f; return c; }
        static float Fbm(float x, float y, int oct = 4)
        {
            float sum = 0f, amp = 0.5f, f = 1f;
            for (int i = 0; i < oct; i++) { sum += (Noise.Perlin(x * f, y * f) - 0.5f) * amp; f *= 2.03f; amp *= 0.5f; }
            return sum;
        }
        static float Hash01(int n) => MathUtil.Hash(n) * 0.5f + 0.5f;

        // ------------------------------------------------------------------ orchestration

        public static void Begin()
        {
            Lamps.Clear();
            Fissures.Clear();
            _ = Art.Rainbow(0f);   // the ball art reads static data: initialise it here on the main thread
            jobs = new ArtJobs();
            jSky = Add("MenuSky", BuildSky, true);
            jPlanet = Add("MenuPlanet", BuildPlanet);
            jPeaks = Add("MenuPeaks", BuildPeaks);
            jHills = Add("MenuHills", BuildHills);
            jStadium = Add("MenuStadium", BuildStadium);
            jGround = Add("MenuGround", BuildGround);
            jCanopy = Add("MenuCanopy", BuildCanopy);
            jFerns = Add("MenuFerns", BuildFerns);
            jIslandBig = Add("MenuIslandBig", () => BuildIsland(true));
            jIslandSmall = Add("MenuIslandSmall", () => BuildIsland(false));
            jCrystal = Add("MenuCrystal", BuildCrystal);
            jBirdUp = Add("MenuBirdUp", () => BuildBird(true));
            jBirdDown = Add("MenuBirdDown", () => BuildBird(false));
            jFall = Add("MenuWaterfall", BuildWaterfall);
            jFall.MakeSprite = false;
            jFall.Wrap = TextureWrapMode.Repeat;
            jLogo = Add("MenuLogo", LogoArt.Build);
            jLogo.Dither = false;
            jLogo.Mips = true;       // the logo is drawn denser than it is shown
            jHero = Add("MenuHeroBall", () => Art.BallPatternCanvas(640f));
            jHeroShade = Add("MenuHeroShade", () => Art.BallShadeCanvas(640f));
            jHeroHi = Add("MenuHeroHi", () => Art.BallHighlightCanvas(640f));
            jClouds = new ArtJobs.Job[3];
            for (int i = 0; i < jClouds.Length; i++) { int k = i; jClouds[i] = Add("MenuCloud" + k, () => BuildCloud(k)); }
            jShards = new ArtJobs.Job[3];
            for (int i = 0; i < jShards.Length; i++) { int k = i; jShards[i] = Add("MenuShard" + k, () => BuildShard(k)); }
            jobs.Start();
        }

        static ArtJobs.Job Add(string name, System.Func<SdfCanvas> build, bool dither = false)
        {
            var j = jobs.Add(name, build, Vector2.zero, dither);
            j.Premultiply = false;   // UI images expect straight alpha
            j.Mips = false;          // the backdrop layers are shown at or above their own resolution
            j.MakeSprite = false;
            return j;
        }

        public static void End()
        {
            if (jobs == null) return;
            jobs.Complete();
            Debug.Log("[SoccerFight] menu art: " + jobs.Slowest(8));
            Sky = Ui(jSky); Planet = Ui(jPlanet); Peaks = Ui(jPeaks); Hills = Ui(jHills); Stadium = Ui(jStadium);
            Ground = Ui(jGround); Canopy = Ui(jCanopy); Ferns = Ui(jFerns); IslandBig = Ui(jIslandBig); IslandSmall = Ui(jIslandSmall);
            Crystal = Ui(jCrystal); BirdUp = Ui(jBirdUp); BirdDown = Ui(jBirdDown); Logo = Ui(jLogo);
            Waterfall = jFall.Texture;
            HeroBall = Ui(jHero); HeroShade = Ui(jHeroShade); HeroHighlight = Ui(jHeroHi);
            Clouds = new Sprite[jClouds.Length];
            for (int i = 0; i < Clouds.Length; i++) Clouds[i] = Ui(jClouds[i]);
            Shards = new Sprite[jShards.Length];
            for (int i = 0; i < Shards.Length; i++) Shards[i] = Ui(jShards[i]);
            jobs = null;
        }

        /// <summary>UI sprite (centre pivot) from a finished job.</summary>
        static Sprite Ui(ArtJobs.Job j)
        {
            if (j.Texture == null) return null;
            var s = Sprite.Create(j.Texture, new Rect(0, 0, j.Texture.width, j.Texture.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            s.name = j.Name;
            return s;
        }

        // ------------------------------------------------------------------ sky

        static SdfCanvas BuildSky()
        {
            var c = new SdfCanvas(new Rect(-StageW * 0.5f, -StageH * 0.5f, StageW, StageH), 0.24f);
            Color[] cols =
            {
                new Color(1f, 0.8f, 0.46f),    // -300
                new Color(1f, 0.58f, 0.36f),   // -170
                new Color(0.93f, 0.36f, 0.5f), // -60
                new Color(0.62f, 0.24f, 0.56f),// 80
                new Color(0.3f, 0.15f, 0.46f), // 300
                new Color(0.12f, 0.08f, 0.27f),// 620
            };
            float[] ys = { -300f, -170f, -60f, 80f, 300f, 620f };
            c.Field(p =>
            {
                float y = p.y + 10f * Fbm(p.x * 0.002f, 3.1f);
                Color col = cols[cols.Length - 1];
                for (int i = 0; i < ys.Length - 1; i++)
                {
                    if (y < ys[i + 1])
                    {
                        float t = S01((y - ys[i]) / (ys[i + 1] - ys[i]));
                        col = Color.Lerp(cols[i], cols[i + 1], t);
                        break;
                    }
                }
                if (y < ys[0]) col = cols[0];
                // sun bloom right behind the menu's player
                float ds = (p - Sun).magnitude;
                col = Color.Lerp(col, new Color(1f, 0.93f, 0.7f), Mathf.Exp(-ds * ds / (2f * 170f * 170f)) * 0.75f);
                col = Color.Lerp(col, new Color(1f, 0.7f, 0.5f), Mathf.Exp(-ds / 520f) * 0.25f);
                // soft nebula bands up high
                float neb = S01((p.y - 120f) / 400f) * Mathf.Clamp01(Fbm(p.x * 0.0016f + 4f, p.y * 0.003f, 5) * 2.2f + 0.2f);
                col = Color.Lerp(col, Color.Lerp(new Color(0.95f, 0.45f, 0.8f), new Color(0.45f, 0.75f, 1f), Noise.Perlin(p.x * 0.001f, 7f)), neb * 0.28f);
                // stars
                int sx = Mathf.FloorToInt((p.x + 5000f) / 26f), sy = Mathf.FloorToInt((p.y + 5000f) / 26f);
                int h = sx * 7349 + sy * 1931;
                if (Hash01(h) > 0.93f && p.y > 60f)
                {
                    Vector2 sc = new Vector2(sx * 26f - 5000f + 13f + (Hash01(h + 1) - 0.5f) * 14f, sy * 26f - 5000f + 13f + (Hash01(h + 2) - 0.5f) * 14f);
                    float d = (p - sc).magnitude;
                    float star = Mathf.Exp(-d * d / (2f * 4.2f * 4.2f)) * S01((p.y - 60f) / 260f) * (0.4f + 0.6f * Hash01(h + 3));
                    col = Color.Lerp(col, Color.white, star);
                }
                col.a = 1f;
                return col;
            });
            return c;
        }

        // ------------------------------------------------------------------ the cracked planet

        static SdfCanvas BuildPlanet()
        {
            const float R = 236f;
            var c = new SdfCanvas(new Rect(-260f, -260f, 520f, 520f), 0.62f);
            Vector2 toSun = (Sun - PlanetPos).normalized;
            // a wedge broke off at the lower left and drifts a little away
            Vector2 cut0 = new Vector2(-250f, 40f), cut1 = new Vector2(-40f, -30f), cut2 = new Vector2(30f, -250f);
            System.Func<Vector2, float> crackLine = p =>
            {
                float d = 1e3f;
                Vector2 prev = cut0;
                for (int i = 1; i <= 12; i++)
                {
                    float t = i / 12f;
                    Vector2 q = t < 0.5f ? Vector2.Lerp(cut0, cut1, t * 2f) : Vector2.Lerp(cut1, cut2, (t - 0.5f) * 2f);
                    q += new Vector2(Mathf.Sin(i * 2.1f) * 12f, Mathf.Cos(i * 1.7f) * 10f);
                    d = Mathf.Min(d, Sdf.Segment(p, prev, q));
                    prev = q;
                }
                return d;
            };
            System.Func<Vector2, float> side = p =>
            {
                // which side of the crack: sign against the polyline's broad direction
                Vector2 a = cut0, b = cut1, e = cut2;
                float s1 = (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
                float s2 = (e.x - b.x) * (p.y - b.y) - (e.y - b.y) * (p.x - b.x);
                return Mathf.Max(s1, s2);
            };
            Vector2 drift = new Vector2(-22f, -18f);

            SdfCanvas.ColorFn surface = p =>
            {
                Vector2 q = p;
                bool chunk = side(p - drift) < 0f && side(p) < 0f;
                if (chunk) q = p - drift;
                Vector2 n2 = q / R;
                float z = Mathf.Sqrt(Mathf.Max(0f, 1f - n2.sqrMagnitude));
                float light = Mathf.Clamp01(Vector2.Dot(n2, toSun) * 0.9f + z * 0.55f);
                Color baseCol = Color.Lerp(new Color(0.55f, 0.42f, 0.78f), new Color(1f, 0.84f, 0.8f), light);
                // craters
                float cr = 0f;
                for (int i = 0; i < 14; i++)
                {
                    Vector2 cc = new Vector2(Hash01(i * 11) * 400f - 200f, Hash01(i * 17 + 3) * 400f - 200f);
                    float rr = 16f + Hash01(i * 5 + 9) * 42f;
                    float d = (q - cc).magnitude / rr;
                    if (d < 1f) cr += (d > 0.78f ? 0.25f : -0.18f) * (1f - d * 0.3f);
                }
                float mottled = Fbm(q.x * 0.012f, q.y * 0.012f, 4);
                Color col = Mul(baseCol, 1f + cr * 0.6f + mottled * 0.45f);
                // rim darkening and a warm terminator glow
                col = Color.Lerp(col, new Color(0.3f, 0.2f, 0.48f), S01((0.35f - z) / 0.35f) * (1f - light) * 0.7f);
                col.a = 1f;
                return col;
            };

            SdfCanvas.SdfFn whole = p => Sdf.Circle(p, Vector2.zero, R);
            SdfCanvas.SdfFn main = p => Sdf.Intersect(whole(p), -side(p) + 0f);
            SdfCanvas.SdfFn chunkShape = p => Sdf.Intersect(whole(p - drift), side(p - drift));
            // lava glow in the gap
            c.Fill(p => Mathf.Min(whole(p), whole(p - drift)) + 4f, p => Color.Lerp(new Color(1f, 0.35f, 0.4f), new Color(1f, 0.75f, 0.35f), S01(1f - crackLine(p) / 40f)));
            c.Fill(p => Mathf.Max(main(p), 1f - (crackLine(p) - 9f)), surface);
            c.Fill(p => Mathf.Max(chunkShape(p), 1f - (crackLine(p - drift) - 9f)), surface);
            // hot seams along both edges of the break
            c.Paint(p => crackLine(p) - 11f, new Color(1f, 0.55f, 0.35f, 0.7f), 8f);
            // smaller glowing cracks spreading over the main body
            var rng = new System.Random(4);
            for (int k = 0; k < 7; k++)
            {
                Vector2 a = cut1 + new Vector2((float)rng.NextDouble() * 60f - 20f, (float)rng.NextDouble() * 60f - 10f);
                var pts = new Vector2[6];
                pts[0] = a;
                float ang = 10f + k * 30f + (float)rng.NextDouble() * 20f;
                for (int i = 1; i < pts.Length; i++)
                {
                    ang += ((float)rng.NextDouble() - 0.5f) * 60f;
                    pts[i] = pts[i - 1] + MathUtil.Dir(ang) * (18f + (float)rng.NextDouble() * 22f);
                }
                Vector2 lo = pts[0], hi = pts[0];
                foreach (var q in pts) { lo = Vector2.Min(lo, q); hi = Vector2.Max(hi, q); }
                c.Paint(p =>
                {
                    if (whole(p) > -4f || side(p) < 0f) return 1f;
                    float d = 1e3f;
                    for (int i = 1; i < pts.Length; i++) d = Mathf.Min(d, Sdf.Tapered(p, pts[i - 1], 3.2f * (1f - i * 0.14f), pts[i], 1f));
                    return d;
                }, new Color(1f, 0.6f, 0.4f, 0.95f), 1.5f, Rect.MinMaxRect(lo.x - 10f, lo.y - 10f, hi.x + 10f, hi.y + 10f));
            }
            // rim light from the sun side
            c.Paint(p =>
            {
                Vector2 n2 = p / R;
                float rim = S01((n2.magnitude - 0.9f) / 0.1f) * Mathf.Clamp01(Vector2.Dot(n2.normalized, toSun));
                return new Color(1f, 0.9f, 0.75f, rim * 0.6f);
            });
            return c;
        }

        static SdfCanvas BuildShard(int k)
        {
            var c = new SdfCanvas(new Rect(-48f, -48f, 96f, 96f), 1f);
            var rng = new System.Random(20 + k);
            int n = 7;
            var pts = new Vector2[n];
            float scale = 26f + k * 8f;
            for (int i = 0; i < n; i++)
            {
                float a = i * 360f / n + (float)rng.NextDouble() * 25f;
                pts[i] = MathUtil.Dir(a) * scale * (0.65f + (float)rng.NextDouble() * 0.45f);
            }
            SdfCanvas.SdfFn poly = p =>
            {
                float d = 1e3f;
                for (int i = 0; i < n; i++) d = Mathf.Min(d, Sdf.Triangle(p, Vector2.zero, pts[i], pts[(i + 1) % n]));
                return d;
            };
            c.Fill(poly, p =>
            {
                float lit = S01((-p.x - p.y) / (scale * 1.6f) + 0.5f);
                return Color.Lerp(new Color(0.4f, 0.3f, 0.6f), new Color(0.95f, 0.78f, 0.82f), lit);
            });
            c.Paint(p => Mathf.Abs(poly(p) + 3f) - 1.5f, new Color(1f, 0.62f, 0.45f, 0.8f), 1f);
            return c;
        }

        // ------------------------------------------------------------------ clouds

        static SdfCanvas BuildCloud(int k)
        {
            var c = new SdfCanvas(new Rect(-320f, -110f, 640f, 220f), 0.45f);
            var rng = new System.Random(40 + k);
            var puffs = new List<Vector3>();
            int n = 7 + k;
            for (int i = 0; i < n; i++)
            {
                float x = -240f + i * 480f / (n - 1) + (float)rng.NextDouble() * 30f;
                float r = 40f + (float)rng.NextDouble() * 50f * (1f - Mathf.Abs(x) / 320f) + 20f;
                puffs.Add(new Vector3(x, -30f + r * 0.55f, r));
            }
            SdfCanvas.SdfFn shape = p =>
            {
                float d = 1e3f;
                foreach (var pf in puffs) d = Sdf.SmoothUnion(d, Sdf.Circle(p, new Vector2(pf.x, pf.y), pf.z), 22f);
                return Mathf.Max(d, -(p.y + 42f));
            };
            c.Fill(shape, p =>
            {
                float k2 = S01((p.y + 40f) / 110f);
                Color col = Color.Lerp(new Color(0.78f, 0.42f, 0.66f), new Color(1f, 0.82f, 0.78f), k2);
                return col.WithAlpha(0.95f);
            }, 10f);
            c.Blur(1);
            return c;
        }

        // ------------------------------------------------------------------ mountain ranges

        static float[] Profile(int w, float left, float ppu, System.Func<float, float> h)
        {
            var arr = new float[w];
            for (int x = 0; x < w; x++) arr[x] = h(left + (x + 0.5f) / ppu);
            return arr;
        }

        /// <summary>0 right behind the menu's player, 1 further out: keeps a gap open around the sun.</summary>
        static float Gap(float x) => 0.5f + 0.5f * S01((Mathf.Abs(x) - 120f) / 480f);

        static SdfCanvas BuildPeaks()
        {
            var c = new SdfCanvas(new Rect(-StageW * 0.5f, -320f, StageW, 640f), 0.42f);
            float left = c.UnitRect.xMin, ppu = c.Ppu;
            var back = Profile(c.Width, left, ppu, x => (40f + 170f * (1f - Mathf.Abs(Fbm(x * 0.0022f, 1.3f, 5)) * 2.2f) + 60f * Mathf.Sin(x * 0.0019f + 1f)) * Gap(x));
            var front = Profile(c.Width, left, ppu, x => (-30f + 120f * (1f - Mathf.Abs(Fbm(x * 0.0031f, 5.7f, 5)) * 2.4f)) * Gap(x) - 40f * (1f - Gap(x)));
            int W = c.Width;
            c.Field(p =>
            {
                int xi = Mathf.Clamp((int)((p.x - left) * ppu), 0, W - 1);
                float hb = back[xi], hf = front[xi];
                float aa = 1.2f / ppu;
                float ab = Mathf.Clamp01(0.5f + (hb - p.y) / aa), af = Mathf.Clamp01(0.5f + (hf - p.y) / aa);
                float a = Mathf.Max(ab, af);
                if (a <= 0f) return Clear;
                float slopeB = back[Mathf.Min(W - 1, xi + 1)] - back[Mathf.Max(0, xi - 1)];
                float slopeF = front[Mathf.Min(W - 1, xi + 1)] - front[Mathf.Max(0, xi - 1)];
                // sun in the middle: slopes facing the centre catch the light
                float faceB = Mathf.Clamp(-slopeB * Mathf.Sign(p.x) * 0.25f, -1f, 1f);
                float faceF = Mathf.Clamp(-slopeF * Mathf.Sign(p.x) * 0.25f, -1f, 1f);
                float crag = Noise.Perlin(p.x * 0.02f, p.y * 0.006f);
                Color b = Color.Lerp(new Color(0.55f, 0.43f, 0.78f), new Color(0.98f, 0.72f, 0.78f), Mathf.Clamp01(faceB) * 0.8f);
                b = Mul(b, 0.9f + 0.18f * crag);
                float snow = S01((40f - (hb - p.y)) / 30f) * S01((hb - 120f) / 60f) * S01((Noise.Perlin(p.x * 0.03f, p.y * 0.03f) - 0.35f) / 0.2f);
                b = Color.Lerp(b, new Color(1f, 0.9f, 0.94f), snow * 0.85f);
                b = Color.Lerp(b, new Color(0.93f, 0.55f, 0.62f), S01((20f - p.y) / 200f) * 0.55f);   // haze low down
                Color f = Color.Lerp(new Color(0.42f, 0.3f, 0.66f), new Color(0.9f, 0.55f, 0.66f), Mathf.Clamp01(faceF) * 0.7f);
                f = Mul(f, 0.92f + 0.14f * Noise.Perlin(p.x * 0.015f + 9f, p.y * 0.01f));
                f = Color.Lerp(f, new Color(0.88f, 0.48f, 0.6f), S01((-60f - p.y) / 200f) * 0.5f);
                Color col = Color.Lerp(b, f, af);
                col.a = a;
                return col;
            });
            c.RimLight(new Vector2(0f, 3f), new Color(1f, 0.85f, 0.75f), 0.7f);
            return c;
        }

        static SdfCanvas BuildHills()
        {
            var c = new SdfCanvas(new Rect(-StageW * 0.5f, -360f, StageW, 560f), 0.45f);
            float left = c.UnitRect.xMin, ppu = c.Ppu;
            System.Func<float, float> hf = x => -150f + 110f * Mathf.Abs(x) / 1100f + 55f * Fbm(x * 0.0026f, 8.8f, 4) * 2f + 26f * Mathf.Sin(x * 0.004f);
            var hill = Profile(c.Width, left, ppu, hf);
            int W = c.Width;
            c.Field(p =>
            {
                int xi = Mathf.Clamp((int)((p.x - left) * ppu), 0, W - 1);
                float a = Mathf.Clamp01(0.5f + (hill[xi] - p.y) * ppu / 1.2f);
                if (a <= 0f) return Clear;
                float depthK = S01((hill[xi] - p.y) / 160f);
                Color col = Color.Lerp(new Color(0.46f, 0.3f, 0.64f), new Color(0.3f, 0.2f, 0.5f), depthK);
                col = Mul(col, 0.92f + 0.14f * Noise.Perlin(p.x * 0.012f, p.y * 0.02f));
                col = Color.Lerp(col, new Color(0.85f, 0.46f, 0.58f), S01((-120f - p.y) / 160f) * 0.45f);
                col.a = a;
                return col;
            });
            // pine forests on the ridges
            var rng = new System.Random(12);
            for (int i = 0; i < 260; i++)
            {
                float x = left + 10f + (float)rng.NextDouble() * (StageW - 20f);
                if (Noise.Perlin(x * 0.004f, 2.2f) < 0.42f) continue;
                if (Mathf.Abs(x) < 160f) continue;   // keep the sun gap open
                float by = hf(x) - 4f - (float)rng.NextDouble() * 20f;
                float h = 22f + (float)rng.NextDouble() * 30f, w = h * 0.34f;
                Color pc = Color.Lerp(new Color(0.24f, 0.16f, 0.42f), new Color(0.34f, 0.22f, 0.52f), (float)rng.NextDouble());
                c.Fill(p => Mathf.Min(Sdf.Triangle(p, new Vector2(x - w, by), new Vector2(x + w, by), new Vector2(x, by + h)),
                                      Sdf.Triangle(p, new Vector2(x - w * 0.75f, by + h * 0.35f), new Vector2(x + w * 0.75f, by + h * 0.35f), new Vector2(x, by + h * 1.2f))),
                    pc, 0f, new Rect(x - w - 3f, by - 3f, w * 2f + 6f, h * 1.3f + 6f));
            }
            c.RimLight(new Vector2(0f, 3f), new Color(1f, 0.72f, 0.62f), 0.55f);
            return c;
        }

        // ------------------------------------------------------------------ ruined stadium with floodlights

        static SdfCanvas BuildStadium()
        {
            var c = new SdfCanvas(new Rect(-StageW * 0.5f, -330f, StageW, 640f), 0.48f);
            Color wall = new Color(0.33f, 0.22f, 0.52f), wallLit = new Color(0.62f, 0.42f, 0.72f), dark = new Color(0.2f, 0.13f, 0.36f);

            // right: a curved bowl of stands, broken open in the middle
            Vector2 bowlC = new Vector2(560f, -330f);
            SdfCanvas.SdfFn bowl = p =>
            {
                Vector2 q = p - bowlC;
                float outer = Sdf.Ellipse(q, Vector2.zero, new Vector2(460f, 300f));
                float inner = Sdf.Ellipse(q, new Vector2(0f, -30f), new Vector2(330f, 200f));
                float d = Mathf.Max(outer, -inner);
                d = Mathf.Max(d, -q.y + 6f);
                // broken gap with jagged edges
                float jag = 18f * Mathf.Sin(q.y * 0.08f) + 10f * Mathf.Sin(q.y * 0.21f + 1f);
                d = Mathf.Max(d, -(Mathf.Abs(q.x + 60f + jag) - 36f - q.y * 0.18f));
                // crumbled top edge
                d = Mathf.Max(d, q.y - 270f - 22f * Fbm(q.x * 0.02f, 1.1f));
                return d;
            };
            c.Fill(bowl, p =>
            {
                Vector2 q = p - bowlC;
                float tier = Mathf.Repeat(q.y, 26f);
                float step = S01((tier - 20f) / 3f);
                Color col = Color.Lerp(wall, wallLit, S01((-q.x - 100f) / 360f) * 0.6f);
                col = Color.Lerp(col, dark, step * 0.5f);
                // seats: little dots of colour on the tiers
                float seat = Noise.Perlin(q.x * 0.2f, Mathf.Floor(q.y / 26f)) > 0.62f && tier < 14f ? 1f : 0f;
                col = Color.Lerp(col, new Color(0.85f, 0.4f, 0.55f), seat * 0.35f);
                col = Mul(col, 0.9f + 0.15f * Noise.Perlin(q.x * 0.05f, q.y * 0.05f));
                return col;
            }, 0f, new Rect(bowlC.x - 470f, bowlC.y, 940f, 320f));
            // arches under the stands
            for (int i = 0; i < 7; i++)
            {
                float ax = bowlC.x - 380f + i * 127f;
                if (Mathf.Abs(ax - bowlC.x + 60f) < 70f) continue;
                Vector2 ac = new Vector2(ax, -300f);
                c.Paint(p => Sdf.Union(Sdf.Box(p, ac, new Vector2(30f, 26f)), Sdf.Circle(p, ac + new Vector2(0f, 26f), 30f)), new Color(0.12f, 0.07f, 0.24f, 0.9f), 1f, new Rect(ac.x - 40f, ac.y - 40f, 80f, 110f));
            }

            // left: a broken aqueduct / arcade ruin
            SdfCanvas.SdfFn arcade = p =>
            {
                float d = Sdf.Box(p, new Vector2(-700f, -200f), new Vector2(360f, 110f));
                float cell = Mathf.Repeat(p.x + 700f + 360f, 120f) - 60f;
                float arch = Sdf.Union(Sdf.Box(new Vector2(cell, p.y), new Vector2(0f, -250f), new Vector2(40f, 60f)), Sdf.Circle(new Vector2(cell, p.y), new Vector2(0f, -190f), 40f));
                d = Mathf.Max(d, -arch);
                // ruined top: jagged and missing chunks
                float top = -108f + 30f * Fbm(p.x * 0.01f, 4.4f) + (p.x < -820f ? -60f : 0f) + (p.x > -520f ? -40f - (p.x + 520f) * 0.6f : 0f);
                d = Mathf.Max(d, p.y - top);
                return d;
            };
            c.Fill(arcade, p => Mul(Color.Lerp(wall, wallLit, S01((p.x + 400f) / 400f) * 0.5f), 0.9f + 0.15f * Noise.Perlin(p.x * 0.06f, p.y * 0.06f)),
                0f, new Rect(-1100f, -330f, 800f, 280f));
            // brick lines on the ruin
            c.Paint(p =>
            {
                if (arcade(p) > 0f) return 1f;
                float row = Mathf.Abs(Mathf.Repeat(p.y, 22f) - 11f) - 10f;
                return row;
            }, new Color(0.18f, 0.11f, 0.32f, 0.5f), 1f, new Rect(-1100f, -330f, 800f, 280f));

            // floodlight masts: two standing, one snapped and leaning
            Mast(c, new Vector2(270f, -330f), 420f, 0f);
            Mast(c, new Vector2(960f, -330f), 480f, 6f);
            Mast(c, new Vector2(-380f, -330f), 380f, -22f);

            c.RimLight(new Vector2(-2f, 3f), new Color(1f, 0.72f, 0.6f), 0.55f);
            return c;
        }

        /// <summary>Lattice mast with a lamp head. Lamps are recorded for the glow overlay.</summary>
        static void Mast(SdfCanvas c, Vector2 foot, float height, float lean)
        {
            Vector2 up = MathUtil.Dir(90f + lean);
            Vector2 top = foot + up * height;
            Vector2 side = new Vector2(up.y, -up.x);
            Color metal = new Color(0.24f, 0.16f, 0.4f);
            float wBase = 16f, wTop = 7f;
            SdfCanvas.SdfFn legs = p => Mathf.Min(Sdf.Segment(p, foot - side * wBase, top - side * wTop), Sdf.Segment(p, foot + side * wBase, top + side * wTop)) - 2.6f;
            SdfCanvas.SdfFn braces = p =>
            {
                float d = 1e3f;
                int n = Mathf.RoundToInt(height / 34f);
                for (int i = 0; i < n; i++)
                {
                    float t0 = i / (float)n, t1 = (i + 1) / (float)n;
                    Vector2 l0 = Vector2.Lerp(foot - side * wBase, top - side * wTop, t0), r1 = Vector2.Lerp(foot + side * wBase, top + side * wTop, t1);
                    Vector2 r0 = Vector2.Lerp(foot + side * wBase, top + side * wTop, t0), l1 = Vector2.Lerp(foot - side * wBase, top - side * wTop, t1);
                    d = Mathf.Min(d, Mathf.Min(Sdf.Segment(p, l0, r1), Sdf.Segment(p, r0, l1)));
                }
                return d - 1.2f;
            };
            Rect bounds = Rect.MinMaxRect(Mathf.Min(foot.x, top.x) - 60f, foot.y - 4f, Mathf.Max(foot.x, top.x) + 60f, Mathf.Max(foot.y, top.y) + 60f);
            c.Fill(p => Mathf.Min(legs(p), braces(p)), metal, 0f, bounds);
            // lamp head: a frame with 3 × 2 lights
            Vector2 head = top + up * 20f;
            float ang = lean;
            c.Fill(p => Sdf.Box(p, head, new Vector2(52f, 30f), 4f, ang), new Color(0.18f, 0.12f, 0.3f), 0f, bounds);
            for (int ix = 0; ix < 3; ix++)
            for (int iy = 0; iy < 2; iy++)
            {
                Vector2 l = head + MathUtil.Rotate(new Vector2((ix - 1) * 32f, (iy - 0.5f) * 26f), ang);
                bool broken = lean < -10f && (ix + iy) % 2 == 0;
                c.Fill(p => Sdf.Box(p, l, new Vector2(12f, 9f), 3f, ang), broken ? new Color(0.35f, 0.3f, 0.45f) : new Color(1f, 0.93f, 0.7f), 0f, bounds);
                if (!broken) Lamps.Add(l);
            }
        }

        // ------------------------------------------------------------------ the broken pitch in front

        static SdfCanvas BuildGround()
        {
            var c = new SdfCanvas(new Rect(-StageW * 0.5f, -StageH * 0.5f, StageW, 720f), 0.56f);
            float bottom = -StageH * 0.5f;
            System.Func<float, float> top = GroundTopAt;

            // fissures: a few jagged lines running over the pitch, glowing from inside
            var rng = new System.Random(77);
            var cracks = new List<Vector2[]>();
            float[] starts = { -760f, -420f, 360f, 700f, 980f, -1000f };
            foreach (float sx in starts)
            {
                var pts = new Vector2[7];
                pts[0] = new Vector2(sx, top(sx) - 8f);
                float ang = -90f + ((float)rng.NextDouble() - 0.5f) * 50f;
                for (int i = 1; i < pts.Length; i++)
                {
                    ang += ((float)rng.NextDouble() - 0.5f) * 70f;
                    ang = Mathf.Clamp(ang, -150f, -30f);
                    pts[i] = pts[i - 1] + MathUtil.Dir(ang) * (30f + (float)rng.NextDouble() * 30f);
                }
                cracks.Add(pts);
                for (int i = 0; i < pts.Length; i += 2) Fissures.Add(pts[i]);
            }
            System.Func<Vector2, float> crackDist = p =>
            {
                float d = 1e3f;
                foreach (var pts in cracks)
                    for (int i = 1; i < pts.Length; i++)
                        d = Mathf.Min(d, Sdf.Tapered(p, pts[i - 1], 9f * (1f - i * 0.1f), pts[i], 3f));
                return d;
            };

            SdfCanvas.SdfFn land = p => p.y - top(p.x);
            c.Fill(land, p =>
            {
                float depth = top(p.x) - p.y;
                // turf with a mown stripe pattern near the top, earth further down
                float stripe = Mathf.Repeat((p.x + p.y * 0.8f) / 110f, 1f) < 0.5f ? 1f : 0.93f;
                Color grass = Color.Lerp(new Color(0.45f, 0.84f, 0.42f), new Color(0.2f, 0.55f, 0.4f), S01(depth / 150f));
                grass = Mul(grass, stripe * (0.92f + 0.14f * Noise.Perlin(p.x * 0.03f, p.y * 0.03f)));
                Color earth = Color.Lerp(new Color(0.36f, 0.22f, 0.36f), new Color(0.18f, 0.1f, 0.24f), S01((depth - 150f) / 200f));
                float edge = S01((depth - 120f - 30f * Fbm(p.x * 0.01f, 9f)) / 30f);
                Color col = Color.Lerp(grass, earth, edge);
                // warm light from the sun gap in the middle
                col = Color.Lerp(col, new Color(1f, 0.86f, 0.55f), Mathf.Exp(-Sq(p.x / 360f)) * S01(1f - depth / 90f) * 0.35f);
                return col;
            });
            // chalk lines: the halfway circle and line, broken where the pitch split
            c.Paint(p =>
            {
                float depth = top(p.x) - p.y;
                if (depth < 6f) return 1f;
                Vector2 q = new Vector2(p.x, (p.y - (-230f)) * 3.4f);
                float ring = Mathf.Abs(q.magnitude - 250f) - 3.5f;
                float line = Mathf.Abs(p.x - 4f) - 2.6f;
                float d = Mathf.Min(ring, line);
                // only near a line is it worth asking whether a fissure broke it
                return d > 4f || crackDist(p) >= 16f ? d : 1f;
            }, new Color(0.96f, 0.98f, 0.92f, 0.75f), 1f);

            // the fissures: dark rim, hot core
            // (each fissure is painted only inside its own bounds)
            float[] grow = { -3f, 1.5f, 4f, -18f };
            float[] soft = { 1.5f, 1.5f, 1f, 14f };
            Color[] cols = { new Color(0.18f, 0.06f, 0.2f, 0.95f), new Color(1f, 0.55f, 0.35f, 1f), new Color(1f, 0.92f, 0.6f, 1f), new Color(1f, 0.5f, 0.45f, 0.18f) };
            for (int pass = 0; pass < grow.Length; pass++)
            {
                foreach (var pts in cracks)
                {
                    var line = pts;
                    float g = grow[pass];
                    Vector2 min = line[0], max = line[0];
                    foreach (var q in line) { min = Vector2.Min(min, q); max = Vector2.Max(max, q); }
                    Rect b = Rect.MinMaxRect(min.x - 50f, min.y - 50f, max.x + 50f, max.y + 50f);
                    c.Paint(p =>
                    {
                        float d = 1e3f;
                        for (int i = 1; i < line.Length; i++) d = Mathf.Min(d, Sdf.Tapered(p, line[i - 1], 9f * (1f - i * 0.1f), line[i], 3f));
                        return d + g;
                    }, cols[pass], soft[pass], b);
                }
            }

            // grass blades along the top edge
            for (int i = 0; i < 700; i++)
            {
                float x = -StageW * 0.5f + (float)rng.NextDouble() * StageW;
                float y0 = top(x) - 2f - (float)rng.NextDouble() * 6f;
                float h = 10f + (float)rng.NextDouble() * 18f;
                float lean = ((float)rng.NextDouble() - 0.5f) * 10f;
                Color g = Color.Lerp(new Color(0.5f, 0.9f, 0.45f), new Color(0.72f, 1f, 0.55f), (float)rng.NextDouble());
                c.Fill(p => Sdf.Tapered(p, new Vector2(x, y0), 2.4f, new Vector2(x + lean, y0 + h), 0.3f), g, 0f, new Rect(x - 14f, y0 - 3f, 28f, h + 6f));
            }
            // flowers
            for (int i = 0; i < 90; i++)
            {
                float x = -StageW * 0.5f + (float)rng.NextDouble() * StageW;
                if (Mathf.Abs(x) < 260f) continue;
                float y = top(x) - 8f - (float)rng.NextDouble() * 70f;
                Color fc = i % 3 == 0 ? new Color(1f, 0.5f, 0.7f) : i % 3 == 1 ? new Color(1f, 0.85f, 0.35f) : new Color(0.7f, 0.6f, 1f);
                float r = 3.5f + (float)rng.NextDouble() * 3f;
                for (int k = 0; k < 5; k++)
                {
                    Vector2 pc = new Vector2(x, y) + MathUtil.Dir(k * 72f) * r;
                    c.Fill(p => Sdf.Circle(p, pc, r * 0.75f), fc, 0f, new Rect(pc.x - r, pc.y - r, r * 2f, r * 2f));
                }
                c.Fill(p => Sdf.Circle(p, new Vector2(x, y), r * 0.55f), new Color(1f, 0.95f, 0.7f), 0f, new Rect(x - r, y - r, r * 2f, r * 2f));
            }
            // rocks
            float[] rockX = { -880f, -560f, 520f, 840f, -1040f, 1040f };
            for (int i = 0; i < rockX.Length; i++)
            {
                float rx = rockX[i];
                float ry = top(rx) - 14f;
                float rs = 26f + Hash01(i * 3) * 30f;
                SdfCanvas.SdfFn rock = p => Sdf.Intersect(Sdf.Ellipse(p, new Vector2(rx, ry), new Vector2(rs * 1.3f, rs)), -(p.y - ry + rs * 0.35f));
                c.Fill(p => rock(p) - 2f, new Color(0.14f, 0.08f, 0.22f), 0f, new Rect(rx - rs * 1.5f, ry - rs, rs * 3f, rs * 2.2f));
                c.Fill(rock, p => Color.Lerp(new Color(0.42f, 0.34f, 0.62f), new Color(0.78f, 0.66f, 0.9f), S01((p.y - ry + rs) / (rs * 1.4f)) * S01((rx - p.x) * Mathf.Sign(rx) / rs + 0.6f)),
                    0f, new Rect(rx - rs * 1.5f, ry - rs, rs * 3f, rs * 2.2f));
                c.Paint(p => Sdf.Intersect(rock(p), -(p.y - ry - rs * 0.4f)), new Color(0.45f, 0.8f, 0.45f, 0.8f), 3f);
            }

            // a broken goal left of the middle: the frame leans, the crossbar snapped in two and one half
            // hangs down, the net has a hole torn into it
            const float gx = -390f, gw = 120f;
            Vector2 footA = new Vector2(gx - gw, top(gx - gw) - 26f), footB = new Vector2(gx + gw, top(gx + gw) - 30f);
            Vector2 topA = footA + new Vector2(-10f, 190f), topB = footB + new Vector2(-4f, 150f);
            Vector2 barA = topA + new Vector2(130f, -10f);                 // the left half of the bar, still attached
            Vector2 hangB = topB + new Vector2(-40f, -80f);                 // the right half, dangling from its post
            Rect goalRect = Rect.MinMaxRect(gx - gw - 40f, footA.y - 30f, gx + gw + 40f, topA.y + 30f);
            System.Func<Vector2, float> hole = p => Sdf.Ellipse(p, new Vector2(gx + 30f, footA.y + 95f), new Vector2(46f, 34f)) + 10f * Noise.Perlin(p.x * 0.08f, p.y * 0.08f);
            // net: a sagging grid between the posts, back-lit by the sun
            c.Fill(p =>
            {
                float u = Mathf.InverseLerp(footA.x, footB.x, p.x);
                if (u <= 0f || u >= 1f) return 1f;
                float yTop = Mathf.Lerp(topA.y - 6f, topB.y - 6f, u) - 20f * Mathf.Sin(u * Mathf.PI);
                float yBot = Mathf.Lerp(footA.y, footB.y, u);
                if (p.y > yTop || p.y < yBot || hole(p) < 0f) return 1f;
                float gridX = Mathf.Abs(Mathf.Repeat(p.x + (p.y - yBot) * 0.15f, 22f) - 11f);
                float gridY = Mathf.Abs(Mathf.Repeat(p.y - yBot + 3f * Mathf.Sin(p.x * 0.05f), 20f) - 10f);
                return Mathf.Min(11f - gridX, 10f - gridY) - 1.2f;
            }, new Color(1f, 0.96f, 0.95f, 0.55f), 0f, goalRect);
            c.Paint(p => Mathf.Abs(hole(p)) - 2f, new Color(0.3f, 0.2f, 0.4f, 0.4f), 1f, goalRect);
            Color white = new Color(0.97f, 0.97f, 1f), shade = new Color(0.66f, 0.64f, 0.82f);
            SdfCanvas.SdfFn frame = p => Mathf.Min(Mathf.Min(Sdf.Capsule(p, footA, topA, 8f), Sdf.Capsule(p, footB, topB, 8f)),
                                                   Mathf.Min(Sdf.Capsule(p, topA, barA, 8f), Sdf.Capsule(p, topB, hangB, 7f)));
            c.Fill(p => frame(p) - 3.5f, new Color(0.12f, 0.07f, 0.2f), 0f, goalRect);
            c.Fill(frame, p => Color.Lerp(shade, white, S01(0.5f + (p.x - Mathf.Round((p.x - gx) / (gw * 2f)) * gw * 2f - gx) / 12f)), 0f, goalRect);
            // jagged broken ends
            c.Fill(p => Sdf.Triangle(p, barA + new Vector2(-2f, 8f), barA + new Vector2(-2f, -8f), barA + new Vector2(12f, 3f)), white, 0f, goalRect);
            c.Fill(p => Sdf.Triangle(p, hangB + new Vector2(-7f, 3f), hangB + new Vector2(7f, 3f), hangB + new Vector2(-2f, -12f)), white, 0f, goalRect);
            return c;
        }

        // ------------------------------------------------------------------ floating islands

        static SdfCanvas BuildIsland(bool big)
        {
            float w = big ? 380f : 230f, h = big ? 420f : 260f;
            var c = new SdfCanvas(new Rect(-w * 0.5f, -h * 0.65f, w, h), big ? 0.9f : 1f);
            float topY = 0f;
            var rng = new System.Random(big ? 61 : 62);
            float halfTop = w * 0.42f;
            SdfCanvas.SdfFn rock = p =>
            {
                float t = Mathf.Clamp01(-p.y / (h * 0.6f));
                float half = halfTop * (1f - Mathf.Pow(t, 1.35f)) + 6f * Mathf.Sin(p.y * 0.08f);
                float d = Mathf.Abs(p.x + 8f * Mathf.Sin(p.y * 0.02f)) - half;
                d = Mathf.Max(d, p.y - topY);
                d = Mathf.Max(d, -p.y - h * 0.6f + 20f * Noise.Perlin(p.x * 0.05f, 3f));
                return d;
            };
            SdfCanvas.SdfFn cap = p => Sdf.Intersect(Sdf.Ellipse(p, new Vector2(0f, topY - 2f), new Vector2(halfTop + 14f, 22f)), -(p.y - topY - 14f - 6f * Noise.Perlin(p.x * 0.06f, 1f)));

            // roots and vines under the island
            for (int i = 0; i < (big ? 9 : 5); i++)
            {
                float x0 = -halfTop * 0.8f + (float)rng.NextDouble() * halfTop * 1.6f;
                float len = 40f + (float)rng.NextDouble() * (big ? 150f : 80f);
                Vector2 a = new Vector2(x0, topY - 30f - (float)rng.NextDouble() * 40f);
                Vector2 b = a + new Vector2(((float)rng.NextDouble() - 0.5f) * 30f, -len);
                c.Fill(p => Sdf.Tapered(p, a, 3.4f, b, 0.8f), new Color(0.32f, 0.62f, 0.42f), 0f, Rect.MinMaxRect(Mathf.Min(a.x, b.x) - 6f, b.y - 6f, Mathf.Max(a.x, b.x) + 6f, a.y + 6f));
            }
            c.Fill(p => rock(p) - 3f, new Color(0.12f, 0.07f, 0.2f));
            c.Fill(rock, p =>
            {
                float strata = Mathf.Repeat(p.y + 6f * Noise.Perlin(p.x * 0.04f, 7f), 26f) < 4f ? 0.82f : 1f;
                float lit = S01((-p.x) / (halfTop * 1.2f) + 0.5f);
                Color col = Color.Lerp(new Color(0.34f, 0.22f, 0.44f), new Color(0.72f, 0.48f, 0.62f), lit * S01(1f + p.y / (h * 0.3f)));
                return Mul(col, strata * (0.88f + 0.2f * Noise.Perlin(p.x * 0.05f, p.y * 0.05f)));
            });
            // crystals poking out of the rock
            var crystalCols = new[] { new Color(0.4f, 0.95f, 1f), new Color(1f, 0.45f, 0.85f) };
            for (int i = 0; i < (big ? 4 : 2); i++)
            {
                float cy = topY - 40f - (float)rng.NextDouble() * h * 0.3f;
                float cx = ((float)rng.NextDouble() - 0.5f) * halfTop * 1.1f;
                Color cc = crystalCols[i % 2];
                float ang = ((float)rng.NextDouble() - 0.5f) * 60f + 180f;
                float len = 20f + (float)rng.NextDouble() * 16f;
                Vector2 b0 = new Vector2(cx, cy), tip = b0 + MathUtil.Dir(ang + 90f) * -len;
                Vector2 n = MathUtil.Dir(ang) * 7f;
                c.Fill(p => Sdf.Triangle(p, b0 + n, b0 - n, tip), cc, 0f, new Rect(cx - 40f, cy - 40f, 80f, 80f));
                c.Fill(p => Sdf.Triangle(p, b0 + n * 0.2f, b0 - n, tip), Color.Lerp(cc, Color.white, 0.5f), 0f, new Rect(cx - 40f, cy - 40f, 80f, 80f));
            }
            // grass cap with a lit rim
            c.Fill(p => cap(p) - 3f, new Color(0.1f, 0.25f, 0.18f));
            c.Fill(cap, p => Color.Lerp(new Color(0.24f, 0.62f, 0.42f), new Color(0.55f, 0.92f, 0.45f), S01((p.y - topY + 16f) / 26f)));
            for (int i = 0; i < (big ? 70 : 40); i++)
            {
                float x = -halfTop + (float)rng.NextDouble() * halfTop * 2f;
                float y0 = topY + 8f;
                float bh = 8f + (float)rng.NextDouble() * 14f;
                float tipX = x + ((float)rng.NextDouble() - 0.5f) * 6f;
                c.Fill(p => Sdf.Tapered(p, new Vector2(x, y0), 2f, new Vector2(tipX, y0 + bh), 0.3f), new Color(0.58f, 0.95f, 0.5f), 0f, new Rect(x - 8f, y0 - 2f, 16f, bh + 4f));
            }
            if (big)
            {
                // a broken column and a little bush on top
                Vector2 colBase = new Vector2(-70f, topY + 8f);
                SdfCanvas.SdfFn column = p => Mathf.Max(Sdf.Box(p, colBase + new Vector2(0f, 60f), new Vector2(20f, 60f)), p.y - (colBase.y + 110f + 14f * Mathf.Sin(p.x * 0.3f)));
                c.Fill(p => Mathf.Min(column(p), Sdf.Box(p, colBase + new Vector2(0f, 4f), new Vector2(30f, 6f), 2f)) - 3f, new Color(0.14f, 0.08f, 0.22f));
                c.Fill(column, p => Color.Lerp(new Color(0.66f, 0.58f, 0.86f), new Color(0.95f, 0.88f, 1f), S01((p.x - colBase.x + 20f) / 40f)));
                c.Paint(p => Sdf.Intersect(column(p), Mathf.Abs(Mathf.Repeat(p.x - colBase.x + 20f, 10f) - 5f) - 1.2f), new Color(0.5f, 0.42f, 0.72f, 0.7f), 0.5f);
                c.Fill(p => Sdf.Box(p, colBase + new Vector2(0f, 4f), new Vector2(30f, 6f), 2f), new Color(0.8f, 0.74f, 0.95f));
                for (int k = 0; k < 5; k++)
                {
                    Vector2 bc = new Vector2(60f + k * 16f - 30f, topY + 20f + (k % 2) * 8f);
                    c.Fill(p => Sdf.Circle(p, bc, 18f) - 3f, new Color(0.1f, 0.25f, 0.18f));
                }
                for (int k = 0; k < 5; k++)
                {
                    Vector2 bc = new Vector2(60f + k * 16f - 30f, topY + 20f + (k % 2) * 8f);
                    c.Fill(p => Sdf.Circle(p, bc, 18f), p => Color.Lerp(new Color(0.22f, 0.6f, 0.45f), new Color(0.5f, 0.9f, 0.5f), S01((p.y - bc.y + 10f) / 24f)));
                }
                FallTop = new Vector2(halfTop - 34f, topY - 6f);
            }
            return c;
        }

        static SdfCanvas BuildWaterfall()
        {
            // tileable vertically: streaks of foam on translucent water
            var c = new SdfCanvas(new Rect(0f, 0f, 64f, 256f), 1f);
            c.Field(p =>
            {
                float across = Mathf.Abs(p.x - 32f) / 32f;
                float body = S01((1f - across) / 0.35f);
                float u = p.x / 64f * 6f;
                float v = p.y / 256f;
                float streak = 0f;
                for (int k = 0; k < 3; k++)
                {
                    float f = 2f + k * 2f;
                    streak += Noise.Perlin(u * (1.5f + k), Mathf.Repeat(v, 1f) * f * 0f + k * 3.1f) * 0.2f;
                }
                // periodic along v so the texture repeats
                float wave = 0.5f + 0.5f * Mathf.Sin((v * 3f + Noise.Perlin(u * 2f, 1f) * 2f) * Mathf.PI * 2f);
                float foam = S01((streak + wave * 0.45f - 0.5f) / 0.2f);
                Color col = Color.Lerp(new Color(0.55f, 0.85f, 1f, 0.55f), new Color(1f, 1f, 1f, 0.95f), foam);
                col.a *= body;
                return col;
            });
            return c;
        }

        static SdfCanvas BuildCrystal()
        {
            var c = new SdfCanvas(new Rect(-70f, -20f, 140f, 200f), 1f);
            var rng = new System.Random(90);
            for (int i = 0; i < 5; i++)
            {
                float ang = (i - 2) * 16f + ((float)rng.NextDouble() - 0.5f) * 8f;
                float len = (i == 2 ? 150f : 70f + (float)rng.NextDouble() * 50f);
                float wid = i == 2 ? 22f : 13f + (float)rng.NextDouble() * 5f;
                Vector2 b = new Vector2((i - 2) * 16f, 0f);
                Vector2 dir = MathUtil.Dir(90f - ang);
                Vector2 n = new Vector2(dir.y, -dir.x) * wid;
                Vector2 shoulder = b + dir * (len - wid * 1.4f);
                Vector2 tip = b + dir * len;
                SdfCanvas.SdfFn shape = p => Mathf.Min(Mathf.Min(Sdf.Triangle(p, b - n, b + n, shoulder + n), Sdf.Triangle(p, b - n, shoulder + n, shoulder - n)),
                                                       Sdf.Triangle(p, shoulder - n, shoulder + n, tip));
                c.Fill(p => shape(p) - 3f, new Color(0.08f, 0.1f, 0.25f));
                c.Fill(shape, p =>
                {
                    float s = Vector2.Dot(p - b, n.normalized) / wid;
                    Color col = s > 0f ? new Color(0.35f, 0.88f, 1f) : new Color(0.72f, 0.98f, 1f);
                    return Color.Lerp(col, Color.white, S01(Vector2.Dot(p - b, dir) / len - 0.6f) * 0.5f);
                });
            }
            c.Fill(p => Sdf.Ellipse(p, new Vector2(0f, -4f), new Vector2(62f, 14f)) - 2f, new Color(0.14f, 0.08f, 0.22f));
            c.Fill(p => Sdf.Ellipse(p, new Vector2(0f, -4f), new Vector2(62f, 14f)), new Color(0.42f, 0.34f, 0.62f));
            return c;
        }

        static SdfCanvas BuildBird(bool up)
        {
            var c = new SdfCanvas(new Rect(-24f, -12f, 48f, 24f), 2f);
            float wy = up ? 8f : -6f;
            SdfCanvas.SdfFn bird = p => Mathf.Min(Mathf.Min(Sdf.Tapered(p, new Vector2(0f, 0f), 2.4f, new Vector2(-18f, wy), 0.6f),
                                                            Sdf.Tapered(p, new Vector2(0f, 0f), 2.4f, new Vector2(18f, wy), 0.6f)),
                                                  Sdf.Ellipse(p, new Vector2(0f, -0.5f), new Vector2(4.5f, 2.6f)));
            c.Fill(bird, new Color(0.22f, 0.12f, 0.34f));
            return c;
        }

        // ------------------------------------------------------------------ foreground foliage

        /// <summary>Leaf with a midrib: base at a, tip at b.</summary>
        static void Leaf(SdfCanvas c, Vector2 a, Vector2 b, float width, Color col, Color rib, Color rim)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            Vector2 dir = d / len;
            Vector2 nrm = new Vector2(-dir.y, dir.x);
            SdfCanvas.SdfFn leaf = p =>
            {
                Vector2 q = p - a;
                float u = Vector2.Dot(q, dir) / len;
                float v = Vector2.Dot(q, nrm);
                float half = width * Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI) * (1f - 0.3f * u);
                float bend = Mathf.Sin(u * Mathf.PI) * width * 0.3f;
                return Mathf.Max(Mathf.Abs(v - bend) - half, Mathf.Max(-u * len, (u - 1f) * len));
            };
            Rect r = Rect.MinMaxRect(Mathf.Min(a.x, b.x) - width - 4f, Mathf.Min(a.y, b.y) - width - 4f, Mathf.Max(a.x, b.x) + width + 4f, Mathf.Max(a.y, b.y) + width + 4f);
            c.Fill(p => leaf(p) - 2.5f, new Color(0.04f, 0.12f, 0.12f, 0.95f), 0f, r);
            c.Fill(leaf, p =>
            {
                Vector2 q = p - a;
                float v = Vector2.Dot(q, nrm);
                return Color.Lerp(Mul(col, 0.82f), col, S01(v / width + 0.5f));
            }, 0f, r);
            c.Paint(p => Mathf.Max(Sdf.Segment(p, a, Vector2.Lerp(a, b, 0.92f)) - 1.4f, leaf(p)), rib, 0.5f, r);
            c.Paint(p => Mathf.Max(Mathf.Abs(leaf(p) + 2.5f) - 1.5f, -Vector2.Dot(p - a, nrm)), rim, 1f, r);
        }

        static SdfCanvas BuildCanopy()
        {
            // top-left corner: leaves hanging in from the edge (pivot at the corner)
            var c = new SdfCanvas(new Rect(0f, -560f, 760f, 560f), 0.5f);
            var rng = new System.Random(5);
            Color[] greens = { new Color(0.16f, 0.52f, 0.46f), new Color(0.2f, 0.62f, 0.44f), new Color(0.12f, 0.42f, 0.44f), new Color(0.3f, 0.7f, 0.42f) };
            // a dark branch mass along the edge
            c.Fill(p => Sdf.Union(Sdf.Ellipse(p, new Vector2(80f, -20f), new Vector2(360f, 110f)), Sdf.Ellipse(p, new Vector2(0f, -140f), new Vector2(120f, 260f))),
                new Color(0.05f, 0.18f, 0.2f));
            c.Fill(p => Sdf.Tapered(p, new Vector2(0f, -60f), 26f, new Vector2(520f, -40f), 6f), new Color(0.2f, 0.12f, 0.2f));
            for (int i = 0; i < 70; i++)
            {
                float t = (float)rng.NextDouble();
                Vector2 a = new Vector2(t * 560f + 20f, -30f - (float)rng.NextDouble() * 120f - (1f - t) * 140f);
                float ang = -90f + ((float)rng.NextDouble() - 0.5f) * 120f - t * 20f;
                float len = 70f + (float)rng.NextDouble() * 80f * (1f - t * 0.5f);
                Vector2 b = a + MathUtil.Dir(ang) * len;
                Color g = greens[rng.Next(greens.Length)];
                float light = S01((a.y + 300f) / 300f);
                Leaf(c, a, b, len * 0.24f, Mul(g, 0.8f + 0.4f * (float)rng.NextDouble()), Mul(g, 0.6f), Color.Lerp(new Color(0.6f, 1f, 0.7f, 0.6f), new Color(1f, 0.85f, 0.6f, 0.7f), light));
            }
            // hanging vines with small leaves and a few flowers
            for (int i = 0; i < 6; i++)
            {
                Vector2 a = new Vector2(60f + i * 90f + (float)rng.NextDouble() * 30f, -60f - (float)rng.NextDouble() * 60f);
                float len = 180f + (float)rng.NextDouble() * 260f;
                Vector2 b = a + new Vector2(((float)rng.NextDouble() - 0.5f) * 40f, -len);
                c.Fill(p => Sdf.Tapered(p, a, 2.6f, b, 1f) + 0f, new Color(0.14f, 0.4f, 0.3f), 0f, Rect.MinMaxRect(Mathf.Min(a.x, b.x) - 10f, b.y - 10f, Mathf.Max(a.x, b.x) + 10f, a.y + 10f));
                for (int k = 1; k < 8; k++)
                {
                    Vector2 m = Vector2.Lerp(a, b, k / 8f);
                    float side = k % 2 == 0 ? 1f : -1f;
                    Leaf(c, m, m + new Vector2(side * 18f, -10f), 6f, new Color(0.3f, 0.72f, 0.46f), new Color(0.2f, 0.5f, 0.35f), new Color(0.7f, 1f, 0.7f, 0.5f));
                }
                Vector2 fc = b;
                for (int k = 0; k < 5; k++)
                {
                    Vector2 pc = fc + MathUtil.Dir(k * 72f + 20f) * 7f;
                    c.Fill(p => Sdf.Circle(p, pc, 6f), new Color(1f, 0.45f, 0.72f), 0f, new Rect(pc.x - 8f, pc.y - 8f, 16f, 16f));
                }
                c.Fill(p => Sdf.Circle(p, fc, 4f), new Color(1f, 0.92f, 0.5f), 0f, new Rect(fc.x - 6f, fc.y - 6f, 12f, 12f));
            }
            return c;
        }

        static SdfCanvas BuildFerns()
        {
            // bottom-left corner: ferns and big round leaves growing in from the edge (pivot at the corner)
            var c = new SdfCanvas(new Rect(0f, 0f, 720f, 460f), 0.5f);
            var rng = new System.Random(8);
            // big round leaves at the back
            for (int i = 0; i < 6; i++)
            {
                Vector2 a = new Vector2(20f + i * 40f, 10f);
                float ang = 30f + i * 14f + ((float)rng.NextDouble() - 0.5f) * 10f;
                float len = 220f + (float)rng.NextDouble() * 90f;
                Vector2 b = a + MathUtil.Dir(ang) * len;
                Leaf(c, a, b, len * 0.36f, Color.Lerp(new Color(0.12f, 0.42f, 0.4f), new Color(0.18f, 0.55f, 0.42f), (float)rng.NextDouble()),
                    new Color(0.08f, 0.3f, 0.3f), new Color(1f, 0.8f, 0.6f, 0.5f));
            }
            // fern fronds: a curved stem with paired leaflets
            for (int f = 0; f < 5; f++)
            {
                Vector2 root = new Vector2(10f + f * 30f, 0f);
                float ang0 = 20f + f * 16f;
                float len = 300f + (float)rng.NextDouble() * 120f;
                const int segs = 14;
                var pts = new Vector2[segs + 1];
                pts[0] = root;
                for (int i = 1; i <= segs; i++)
                {
                    float t = i / (float)segs;
                    pts[i] = pts[i - 1] + MathUtil.Dir(ang0 - t * t * 40f) * (len / segs);
                }
                Color frond = Color.Lerp(new Color(0.28f, 0.72f, 0.42f), new Color(0.42f, 0.85f, 0.45f), (float)rng.NextDouble());
                for (int i = 1; i < segs; i++)
                {
                    float t = i / (float)segs;
                    Vector2 dir = (pts[i + 1] - pts[i - 1]).normalized;
                    Vector2 nrm = new Vector2(-dir.y, dir.x);
                    float ll = 46f * Mathf.Sin(Mathf.Clamp01(t * 1.1f) * Mathf.PI) + 8f;
                    Leaf(c, pts[i], pts[i] + (nrm * 0.9f + dir * 0.5f).normalized * ll, ll * 0.28f, frond, Mul(frond, 0.7f), new Color(0.85f, 1f, 0.7f, 0.5f));
                    Leaf(c, pts[i], pts[i] + (-nrm * 0.9f + dir * 0.5f).normalized * ll, ll * 0.28f, Mul(frond, 0.85f), Mul(frond, 0.6f), new Color(0.85f, 1f, 0.7f, 0.35f));
                }
                c.Fill(p =>
                {
                    float d = 1e3f;
                    for (int i = 1; i <= segs; i++) d = Mathf.Min(d, Sdf.Segment(p, pts[i - 1], pts[i]));
                    return d - 2.2f;
                }, new Color(0.16f, 0.45f, 0.3f));
            }
            // dark ground mass in the very corner
            c.Fill(p => Sdf.Ellipse(p, new Vector2(0f, 0f), new Vector2(300f, 90f)), new Color(0.05f, 0.14f, 0.16f));
            return c;
        }
    }
}
