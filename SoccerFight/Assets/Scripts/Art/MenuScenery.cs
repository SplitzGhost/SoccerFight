using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Art for the title screen's own place in the game world (MenuVista): the moonlit valley the
    /// ruined stadium stands in, a stretch of pitch torn apart by a glowing chasm, the centre circle
    /// floating above it as the player's pedestal, floating rocks and the last floodlight, leaning
    /// over the gap. Everything is painted in the backdrop's own style and palette (the same recipes
    /// as DepthArt, EnvironmentArt and PlatformArt) and in world units, on worker threads while the
    /// game boots. The logo and the big ball for the kick-off transition are generated here too.
    ///
    /// Vista units: 1 = 1 world unit, origin = centre of the view (the menu camera is 9.8 tall).
    /// </summary>
    public static class MenuScenery
    {
        /// <summary>Back edge of the pitch surface.</summary>
        public const float GroundTop = -2.4f;
        /// <summary>How deep the pitch surface reaches towards the viewer (its front edge).</summary>
        public const float PitchBand = 0.82f;
        /// <summary>Half width of the chasm at the surface.</summary>
        public const float GapTop = 2.05f;
        public const float MoonPpu = 560f;

        public static Sprite Logo, HeroBall, HeroShade, HeroHighlight;
        public static Sprite Moon, Valley, Ground, Mast, Pedestal;

        /// <summary>The floating centre circle the menu's player stands on (walk line at y = 0).</summary>
        public static readonly PlatformLook PedestalLook = new PlatformLook();
        public static Vector2 PedestalCenter;

        public struct IslandSpec
        {
            public Level.Style Kind;
            public float HalfW;
            public int Seed;
        }

        public static readonly IslandSpec[] IslandSpecs =
        {
            new IslandSpec { Kind = Level.Style.Rock, HalfW = 1.0f, Seed = 23 },
            new IslandSpec { Kind = Level.Style.Crystal, HalfW = 0.72f, Seed = 41 },
            new IslandSpec { Kind = Level.Style.Rock, HalfW = 0.55f, Seed = 57 },
        };
        public static Sprite[] Islands;
        public static PlatformLook[] IslandLooks;
        public static Vector2[] IslandCenters;

        /// <summary>Crystals grown into the chasm walls (vista units): glow spots.</summary>
        public static readonly List<Vector2> GroundCrystals = new List<Vector2>();
        /// <summary>Glowing cracks in the pitch (x, y, size).</summary>
        public static readonly List<Vector3> GroundCracks = new List<Vector3>();
        /// <summary>Lamps on the floodlight head (mast-local units; z = 1 lit, 0 broken).</summary>
        public static readonly List<Vector3> MastLamps = new List<Vector3>();
        public static Vector2 MastHead;
        public const float MastHeadTilt = 38f;

        static ArtJobs jobs;
        static ArtJobs.Job jLogo, jHero, jHeroShade, jHeroHi, jMoon, jValley, jGround, jMast, jPedestal;
        static ArtJobs.Job[] jIslands;

        static readonly Color Clear = new Color(0f, 0f, 0f, 0f);
        static float Sq(float v) => v * v;
        static float S01(float v) => MathUtil.Smooth01(v);
        static Color Mul(Color c, float f) { c.r *= f; c.g *= f; c.b *= f; return c; }
        static float Fbm(float x, float seed, int oct = 4) => EnvironmentArt.Fbm(x, seed, oct);
        static float Ridge(float x, float seed) => 1f - Mathf.Abs(EnvironmentArt.Fbm(x, seed, 5));
        static Rect Around(Vector2 a, Vector2 b, float pad) =>
            Rect.MinMaxRect(Mathf.Min(a.x, b.x) - pad, Mathf.Min(a.y, b.y) - pad, Mathf.Max(a.x, b.x) + pad, Mathf.Max(a.y, b.y) + pad);

        // ------------------------------------------------------------------ orchestration

        public static void Begin()
        {
            _ = Palette.Skin;       // palette statics parse hex through Unity: touch them on the main thread
            _ = Art.Rainbow(0f);    // the ball art reads static data as well
            GroundCrystals.Clear();
            GroundCracks.Clear();
            MastLamps.Clear();
            PedestalLook.Crystals.Clear();
            PedestalLook.Hangs.Clear();
            PedestalLook.Spots.Clear();

            jobs = new ArtJobs();
            // UI pieces: straight alpha, centre pivot
            jLogo = Ui("MenuLogo", LogoArt.Build);
            jHero = Ui("MenuHeroBall", () => Art.BallPatternCanvas(640f));
            jHeroShade = Ui("MenuHeroShade", () => Art.BallShadeCanvas(640f));
            jHeroHi = Ui("MenuHeroHi", () => Art.BallHighlightCanvas(640f));

            // world pieces: premultiplied like every other backdrop sprite
            jMoon = jobs.Add("MenuMoon", () => EnvironmentArt.MoonCanvas(MoonPpu), Vector2.zero);
            jValley = jobs.Add("MenuValley", BuildValley, Vector2.zero);
            jGround = jobs.Add("MenuGround", BuildGround, new Vector2(0f, GroundCanvas.center.y));
            jMast = jobs.Add("MenuMast", BuildMast, Vector2.zero);
            PedestalCenter = PlatformArt.DecorCenter(Level.Style.Rock, -PedestalHalf, PedestalHalf, 0f);
            jPedestal = jobs.Add("MenuPedestal", BuildPedestal, PedestalCenter);

            Islands = new Sprite[IslandSpecs.Length];
            IslandLooks = new PlatformLook[IslandSpecs.Length];
            IslandCenters = new Vector2[IslandSpecs.Length];
            jIslands = new ArtJobs.Job[IslandSpecs.Length];
            for (int i = 0; i < IslandSpecs.Length; i++)
            {
                var s = IslandSpecs[i];
                var look = new PlatformLook();
                IslandLooks[i] = look;
                IslandCenters[i] = PlatformArt.DecorCenter(s.Kind, -s.HalfW, s.HalfW, 0f);
                jIslands[i] = jobs.Add("MenuIsland" + i, () => PlatformArt.Decor(look, s.Kind, -s.HalfW, s.HalfW, 0f, s.Seed), IslandCenters[i]);
            }
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
            Debug.Log("[SoccerFight] menu art: " + jobs.Slowest(6));
            Logo = UiSprite(jLogo);
            HeroBall = UiSprite(jHero); HeroShade = UiSprite(jHeroShade); HeroHighlight = UiSprite(jHeroHi);
            Moon = jMoon.Sprite; Valley = jValley.Sprite; Ground = jGround.Sprite; Mast = jMast.Sprite; Pedestal = jPedestal.Sprite;
            for (int i = 0; i < jIslands.Length; i++) Islands[i] = jIslands[i].Sprite;
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

        // ------------------------------------------------------------------ the valley under the moon

        /// <summary>
        /// Two far ranges that open into a wide V in the middle, so the moon can rise between them.
        /// Same recipe as DepthArt's peaks: moonlit facets, snow in the upper gullies, pine patches,
        /// mist pooling at the feet. Local y = 0 is the valley floor.
        /// </summary>
        static SdfCanvas BuildValley()
        {
            var c = new SdfCanvas(new Rect(-13f, -1.4f, 26f, 4.6f), 54f);
            int W = c.Width;
            float left = c.UnitRect.xMin, ppu = c.Ppu, aa = 1f / ppu;
            var back = new float[W];
            var front = new float[W];
            for (int x = 0; x < W; x++)
            {
                float ux = left + (x + 0.5f) / ppu;
                float open = S01((Mathf.Abs(ux) - 0.8f) / 6.8f);
                back[x] = 0.62f + 1.7f * open + 0.42f * Ridge(ux * 0.24f, 3.7f) * (0.35f + open) + 0.08f * Fbm(ux * 1.3f, 8.4f)
                          + 0.45f * Mathf.Exp(-Sq((ux - 6.1f) / 1.1f)) + 0.32f * Mathf.Exp(-Sq((ux + 7.2f) / 1.4f));
                front[x] = 0.18f + 0.95f * S01((Mathf.Abs(ux) - 2.4f) / 6.5f) + 0.3f * Ridge(ux * 0.36f, 7.1f) * (0.4f + open) + 0.05f * Fbm(ux * 2.1f, 2.2f);
            }
            var slopeB = new float[W];
            var slopeF = new float[W];
            for (int x = 0; x < W; x++)
            {
                slopeB[x] = (back[Mathf.Min(W - 1, x + 2)] - back[Mathf.Max(0, x - 2)]) / (4f / ppu);
                slopeF[x] = (front[Mathf.Min(W - 1, x + 2)] - front[Mathf.Max(0, x - 2)]) / (4f / ppu);
            }

            Color bBase = new Color(0.2f, 0.35f, 0.45f), bLit = new Color(0.46f, 0.64f, 0.73f), snow = new Color(0.8f, 0.9f, 0.96f);
            Color fBase = new Color(0.11f, 0.22f, 0.29f), fLit = new Color(0.28f, 0.45f, 0.53f);
            Color mist = new Color(0.36f, 0.56f, 0.64f), floor = new Color(0.09f, 0.18f, 0.24f);
            c.Field(p =>
            {
                int x = Mathf.Clamp((int)((p.x - left) * ppu), 0, W - 1);
                float aB = Mathf.Clamp01(0.5f + (back[x] - p.y) / aa);
                float aF = Mathf.Clamp01(0.5f + (front[x] - p.y) / aa);
                float a = Mathf.Max(aB, aF);
                if (a <= 0f) return Clear;
                // the moon stands in the middle: facets turned towards it are lit
                float toward = p.x < 0f ? 1f : -1f;
                float litB = Mathf.Clamp(-slopeB[x] * toward * 0.8f, -0.6f, 0.8f);
                float crag = Noise.Perlin(p.x * 6.5f, p.y * 0.7f + 3f);
                Color b = Color.Lerp(bBase, bLit, Mathf.Clamp01(litB) * 0.8f);
                b = Mul(b, (1f + Mathf.Min(0f, litB) * 0.3f) * (0.84f + 0.26f * crag));
                float below = back[x] - p.y;
                float snowMask = S01((0.32f - below) / 0.2f) * S01((Noise.Perlin(p.x * 4.4f, p.y * 3.6f) - 0.32f) / 0.2f) * S01((back[x] - 1.6f) / 0.45f);
                b = Color.Lerp(b, Mul(snow, 0.7f + 0.4f * Mathf.Clamp01(litB + 0.3f)), snowMask * 0.85f);
                float litF = Mathf.Clamp(-slopeF[x] * toward * 0.8f, -0.6f, 0.8f);
                Color f = Mul(Color.Lerp(fBase, fLit, Mathf.Clamp01(litF) * 0.6f), 0.88f + 0.2f * Noise.Perlin(p.x * 4f + 9f, p.y * 1.3f));
                Color col = Color.Lerp(b, f, aF);
                // moon glow washes over the ridges closest to it, mist pools in the valley
                col = Color.Lerp(col, mist, Mathf.Exp(-Sq(p.x / 2.6f)) * 0.25f);
                col = Color.Lerp(col, mist, S01(1f - (p.y - 0.05f) / 0.8f) * 0.5f);
                col = Color.Lerp(col, floor, S01((0.05f - p.y) / 1.2f));
                col.a = a * EnvironmentArt.EdgeFade(p.x, 13f, 1.5f);
                return col;
            });

            var r = new System.Random(93);
            float R() => (float)r.NextDouble();
            Color pine = new Color(0.07f, 0.16f, 0.21f);
            for (int i = 0; i < 420; i++)
            {
                float px = left + 1f + R() * 24f;
                if (Noise.Perlin(px * 0.5f, 6.1f) < 0.42f) continue;
                int xi = Mathf.Clamp((int)((px - left) * ppu), 0, W - 1);
                float by = front[xi] - 0.03f - R() * R() * Mathf.Max(0f, front[xi] - 0.15f);
                float h = 0.06f + R() * 0.11f;
                DepthArt.Conifer(c, new Vector2(px, by), h, Mul(pine, 0.85f + R() * 0.3f), 0.28f);
            }
            c.RimLight(new Vector2(0f, 0.04f), new Color(0.76f, 0.9f, 0.96f), 0.75f);
            return c;
        }

        // ------------------------------------------------------------------ the torn pitch

        static readonly Rect GroundCanvas = new Rect(-12f, -5.9f, 24f, 3.8f);

        /// <summary>|x| of the chasm wall at height y (it widens with depth).</summary>
        public static float GapEdge(float y, float side)
        {
            float down = Mathf.Max(0f, GroundTop - y);
            float jag = 0.2f * (Noise.Perlin(y * 3.1f, side * 5.3f + 11f) - 0.5f) + 0.08f * (Noise.Perlin(y * 9f, side * 2.1f + 3f) - 0.5f);
            return GapTop + (side > 0f ? 0.06f : 0f) + down * 0.3f + jag * S01(down / 0.3f);
        }

        /// <summary>Height of the surface: the broken lip crumbles down towards the gap.</summary>
        public static float SurfaceAt(float x)
        {
            float ax = Mathf.Abs(x);
            float lip = S01(1f - (ax - GapTop) / 0.55f);
            return GroundTop - 0.2f * lip * lip - 0.03f * Noise.Perlin(x * 3f, 1.7f) * lip;
        }

        static SdfCanvas BuildGround()
        {
            var c = new SdfCanvas(GroundCanvas, 72f);
            float ppu = c.Ppu;
            Color chalk = new Color(0.9f, 1f, 0.97f);
            Color cyan = new Color(0.45f, 0.95f, 1f);

            c.Field(p =>
            {
                float side = p.x < 0f ? -1f : 1f;
                float ax = Mathf.Abs(p.x);
                float edge = GapEdge(p.y, side);
                float top = SurfaceAt(p.x);
                float d = Mathf.Max(edge - ax, p.y - top);
                float a = Mathf.Clamp01(0.5f - d * ppu);
                if (a <= 0f) return Clear;
                float v = top - p.y;           // depth below the surface
                float wall = ax - edge;         // distance from the chasm wall
                Color col;
                if (v < PitchBand)
                {
                    // the pitch surface in perspective: mown stripes, grain, worn touchline, lit front lip
                    float yy = 1f - v / PitchBand;               // 1 back edge .. 0 front edge
                    float slant = p.x + (yy - 0.5f) * 0.35f;
                    col = Mathf.Repeat(slant, 4f) < 2f ? Palette.PitchA : Palette.PitchB;
                    float stripeEdge = Mathf.Min(Mathf.Repeat(slant, 2f), 2f - Mathf.Repeat(slant, 2f));
                    col = Color.Lerp(Color.Lerp(Palette.PitchA, Palette.PitchB, 0.5f), col, S01(stripeEdge / 0.08f));
                    col *= 0.93f + 0.14f * Noise.Perlin(p.x * 38f, p.y * 9f);
                    col *= 1f - S01((0.32f - Noise.Perlin(p.x * 110f, p.y * 2.2f)) / 0.12f) * 0.13f;
                    col *= 0.95f + 0.1f * Noise.Perlin(p.x * 1.4f, p.y * 2.5f + 7f);
                    col = Color.Lerp(col, Palette.PitchBack, S01((yy - 0.55f) / 0.45f) * 0.8f);
                    col = Color.Lerp(col, Color.Lerp(Palette.PitchA, Palette.GrassEdge, 0.35f), S01((0.22f - yy) / 0.22f) * 0.45f);
                    float wear = 0.65f + 0.35f * Noise.Perlin(p.x * 7f, 3.3f);
                    float line = Mathf.Abs(yy - 0.28f);
                    col = Color.Lerp(col, chalk, (1f - S01((line - 0.018f) / 0.012f)) * 0.78f * wear);
                    // the edge of both penalty areas, running back into the distance
                    float box = Mathf.Abs(ax - 6.3f + (yy - 0.28f) * 0.35f * side);
                    if (yy > 0.28f) col = Color.Lerp(col, chalk, (1f - S01((box - 0.016f) / 0.01f)) * 0.7f * wear);
                    if (v > PitchBand - 0.04f) col = Color.Lerp(col, Palette.GrassEdge, 1f - S01((PitchBand - v) / 0.04f));
                    // torn turf darkens at the break
                    col = Color.Lerp(col, Mul(Palette.PitchB, 0.55f), S01(1f - wall / 0.14f) * 0.8f);
                }
                else
                {
                    // earth: strata, grain, darkening with depth
                    float e = v - PitchBand;
                    float deep = S01(e / 2.4f);
                    col = Color.Lerp(Palette.EarthTop, Palette.EarthBottom, deep);
                    float strata = 0.5f + 0.5f * Mathf.Sin(p.y * 7f + (Noise.Perlin(p.x * 1.5f, 2f) - 0.5f) * 4f);
                    col = Color.Lerp(col, Mul(col, 1.25f), strata * 0.25f * (1f - deep));
                    col = Mul(col, 0.94f + 0.12f * Noise.Perlin(p.x * 9f, p.y * 9f));
                }
                // the torn walls: raw rock lit from the glow deep in the chasm
                float face = S01(1f - wall / 0.45f);
                if (face > 0f && v > 0.06f)
                {
                    float strata = 0.5f + 0.5f * Mathf.Sin(p.y * 22f + Noise.Perlin(p.x * 1.6f, 4f) * 5f);
                    Color rock = Color.Lerp(new Color(0.13f, 0.2f, 0.22f), new Color(0.27f, 0.36f, 0.36f), strata * 0.55f + 0.3f * Noise.Perlin(p.x * 6f, p.y * 6f));
                    col = Color.Lerp(col, rock, face * S01((v - 0.04f) / 0.1f));
                    float glow = Mathf.Exp(-wall / 0.07f) * S01((v - 0.5f) / 1.6f);
                    col = Color.Lerp(col, cyan, glow * 0.55f);
                }
                col = Color.Lerp(col, Palette.Foreground, S01((-4.9f - p.y) / 0.9f));
                col.a = a;
                return col;
            });

            var r = new System.Random(31);
            float R() => (float)r.NextDouble();

            // stones in the earth
            for (int i = 0; i < 70; i++)
            {
                Vector2 sp = new Vector2(-11.5f + R() * 23f, GroundTop - PitchBand - 0.25f - R() * 2.6f);
                if (Mathf.Abs(sp.x) < GapEdge(sp.y, Mathf.Sign(sp.x)) + 0.1f) continue;
                Vector2 rad = new Vector2(0.06f + R() * 0.16f, 0.04f + R() * 0.07f);
                Color sc = Color.Lerp(Palette.EarthTop, new Color(0.1f, 0.25f, 0.26f), 0.35f + R() * 0.3f).WithAlpha(0.75f);
                Rect b = new Rect(sp.x - 0.3f, sp.y - 0.2f, 0.6f, 0.4f);
                c.Paint(q => Sdf.Ellipse(q, sp, rad), sc, 0.01f, b);
                c.Paint(q => Mathf.Max(Sdf.Ellipse(q, sp, rad), sp.y + rad.y * 0.35f - q.y), Mul(sc, 1.35f), 0.01f, b);
            }

            // roots reaching down from the turf; at the break they hang free into the gap
            Color root = new Color(0.02f, 0.07f, 0.08f), rootLight = new Color(0.09f, 0.22f, 0.22f);
            for (int i = 0; i < 26; i++)
            {
                float sx = -11.5f + R() * 23f;
                if (Mathf.Abs(sx) < GapTop + 0.2f) continue;
                Vector2 prev = new Vector2(sx, GroundTop - PitchBand + 0.05f);
                float wid = 0.03f + R() * 0.03f;
                int segs = 6 + r.Next(7);
                float seed = R() * 10f;
                for (int s = 0; s < segs; s++)
                {
                    float t = s / (float)segs;
                    Vector2 next = prev + new Vector2(Mathf.Sin(s * 1.3f + seed) * 0.08f + (R() - 0.5f) * 0.06f, -(0.12f + R() * 0.12f));
                    float w0 = wid * (1f - t * 0.8f);
                    Vector2 p0 = prev, p1 = next;
                    Rect b = Around(p0, p1, 0.1f);
                    c.Paint(q => Sdf.Capsule(q, p0, p1, w0), root, 0f, b);
                    c.Paint(q => Sdf.Capsule(q, p0 + new Vector2(0f, w0 * 0.4f), p1 + new Vector2(0f, w0 * 0.4f), w0 * 0.3f), rootLight.WithAlpha(0.5f), 0f, b);
                    prev = next;
                }
            }
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 7; i++)
                {
                    float y0 = GroundTop - 0.15f - R() * 1.8f;
                    float ex = GapEdge(y0, side);
                    Vector2 prev = new Vector2(side * (ex + 0.04f), y0);
                    float len = 0.35f + R() * 0.7f;
                    const int segs = 5;
                    for (int s = 0; s < segs; s++)
                    {
                        Vector2 next = prev + new Vector2(-side * (0.04f + R() * 0.05f), -len / segs);
                        float w0 = Mathf.Lerp(0.03f, 0.005f, s / (float)segs);
                        Vector2 p0 = prev, p1 = next;
                        c.Fill(q => Sdf.Capsule(q, p0, p1, w0), root, 0f, Around(p0, p1, 0.08f));
                        prev = next;
                    }
                }
            }

            // crystals growing out of the walls; the vista lights them
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 5; i++)
                {
                    float y0 = GroundTop - 0.9f - i * 0.55f - R() * 0.3f;
                    float ex = GapEdge(y0, side);
                    Vector2 root0 = new Vector2(side * (ex + 0.02f), y0);
                    for (int k = 0; k < 3; k++)
                    {
                        float ang = (side < 0 ? 0f : 180f) + (k - 1) * 30f + (R() - 0.5f) * 16f;
                        float len = 0.12f + R() * 0.16f;
                        Vector2 a = root0 + new Vector2(0f, (k - 1) * 0.04f), b = a + MathUtil.Dir(ang) * len;
                        c.Fill(q => Sdf.Tapered(q, a, 0.04f, b, 0.004f),
                            q => Color.Lerp(new Color(0.45f, 0.9f, 1f), new Color(0.85f, 1f, 1f), S01((q - a).magnitude / len)).WithAlpha(1f), 0f, Around(a, b, 0.07f));
                    }
                    GroundCrystals.Add(root0 + new Vector2(-side * 0.1f, 0f));
                }
            }

            // cracks running out from the break through the pitch, glowing faintly inside
            for (int side = -1; side <= 1; side += 2)
            {
                for (int k = 0; k < 3; k++)
                {
                    float y0 = GroundTop - 0.12f - k * 0.25f - R() * 0.1f;
                    Vector2 a = new Vector2(side * (GapEdge(y0, side) - 0.02f), y0);
                    var pts = new Vector2[5];
                    pts[0] = a;
                    for (int s = 1; s < pts.Length; s++)
                        pts[s] = pts[s - 1] + new Vector2(side * (0.28f + R() * 0.35f), (R() - 0.55f) * 0.16f);
                    Rect cb = Rect.MinMaxRect(Mathf.Min(a.x, pts[4].x) - 0.2f, a.y - 0.7f, Mathf.Max(a.x, pts[4].x) + 0.2f, a.y + 0.3f);
                    SdfCanvas.SdfFn crack = q =>
                    {
                        float dd = 10f;
                        for (int s = 1; s < pts.Length; s++)
                            dd = Mathf.Min(dd, Sdf.Tapered(q, pts[s - 1], 0.03f * (1f - s * 0.18f), pts[s], 0.01f * (1f - s * 0.15f)));
                        return dd;
                    };
                    c.Paint(crack, new Color(0.02f, 0.06f, 0.07f, 0.95f), 0.005f, cb);
                    c.Paint(q => crack(q) + 0.012f, new Color(0.5f, 0.95f, 1f, 0.8f), 0.006f, cb);
                    GroundCracks.Add(new Vector3((a.x + pts[2].x) * 0.5f, a.y, 0.9f));
                }
            }

            // turf hanging over the front lip and over the break
            for (float x = -12f; x < 12f; x += 0.05f + R() * 0.16f)
            {
                if (Mathf.Abs(x) < GapEdge(GroundTop - PitchBand, Mathf.Sign(x)) + 0.03f) continue;
                float h = 0.025f + R() * R() * 0.11f;
                float lean = (R() - 0.5f) * 0.06f;
                float by = SurfaceAt(x) - PitchBand;
                Vector2 gt = new Vector2(x + lean, by - h), gb = new Vector2(x, by + 0.02f);
                Color gc = Color.Lerp(Palette.PitchA, Palette.GrassEdge, 0.25f + R() * 0.35f);
                c.Fill(q => Sdf.Tapered(q, gb, 0.013f, gt, 0.002f), gc, 0f, Around(gb, gt, 0.03f));
            }
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 16; i++)
                {
                    float y0 = GroundTop - R() * PitchBand;
                    float ex = GapEdge(y0, side);
                    float h = 0.05f + R() * 0.14f;
                    Vector2 gb = new Vector2(side * (ex + 0.03f), y0), gt = gb + new Vector2(-side * (0.03f + R() * 0.05f), -h);
                    Color gc = Color.Lerp(Palette.PitchB, Palette.GrassEdge, 0.2f + R() * 0.3f);
                    c.Fill(q => Sdf.Tapered(q, gb, 0.014f, gt, 0.002f), gc, 0f, Around(gb, gt, 0.03f));
                }
            }

            c.RimLight(new Vector2(0f, 0.03f), Color.Lerp(Palette.GrassEdge, Color.white, 0.2f), 0.55f);
            return c;
        }

        // ------------------------------------------------------------------ floating centre circle

        public const float PedestalHalf = 1.75f;

        static SdfCanvas BuildPedestal()
        {
            var c = PlatformArt.Decor(PedestalLook, Level.Style.Rock, -PedestalHalf, PedestalHalf, 0f, 11);
            // the centre circle, the halfway line and the spot, in the same perspective as the pitch
            Color chalk = new Color(0.9f, 1f, 0.97f, 0.85f);
            Rect top = new Rect(-PedestalHalf, -0.12f, PedestalHalf * 2f, 0.28f);
            Vector2 cc = new Vector2(0f, 0.012f);
            c.Paint(q => Mathf.Abs(Sdf.Ellipse(q, cc, new Vector2(1.28f, 0.098f))) - 0.007f * (1f + Noise.Perlin(q.x * 9f, 2f)), chalk, 0.004f, top);
            c.Paint(q => Sdf.Capsule(q, new Vector2(0.045f, -0.11f), new Vector2(-0.04f, 0.13f), 0.008f), chalk, 0.004f, top);
            c.Paint(q => Sdf.Ellipse(q, cc, new Vector2(0.035f, 0.012f)), chalk, 0.003f, top);
            // worn patches where the chalk has rubbed away
            c.Paint(q => Noise.Perlin(q.x * 14f, q.y * 30f) - 0.4f, Palette.PitchB.WithAlpha(0.3f), 0.05f, top);
            return c;
        }

        // ------------------------------------------------------------------ the last floodlight

        /// <summary>
        /// A lattice mast drawn upright (base at the origin); the vista leans it over the gap. Its
        /// head is tilted towards the floating centre circle and two of its six lamps are dead.
        /// </summary>
        static SdfCanvas BuildMast()
        {
            var c = new SdfCanvas(new Rect(-1.0f, -0.3f, 2.0f, 5.9f), 96f);
            const float H = 4.75f;
            Color steel = new Color(0.12f, 0.2f, 0.24f), steelLit = new Color(0.3f, 0.43f, 0.47f);
            Vector2 l0 = new Vector2(-0.17f, 0f), r0 = new Vector2(0.17f, 0f);
            Vector2 l1 = new Vector2(-0.055f, H), r1 = new Vector2(0.055f, H);
            SdfCanvas.ColorFn metal = q => Color.Lerp(steel, steelLit, S01((q.x + 0.2f) / 0.4f) * 0.5f).WithAlpha(1f);
            c.Fill(q => Mathf.Min(Sdf.Capsule(q, l0, l1, 0.024f), Sdf.Capsule(q, r0, r1, 0.024f)), metal, 0f, new Rect(-0.3f, -0.1f, 0.6f, H + 0.2f));
            const int n = 16;
            for (int k = 0; k < n; k++)
            {
                float t0 = k / (float)n, t1 = (k + 1) / (float)n;
                Vector2 a = k % 2 == 0 ? Vector2.Lerp(l0, l1, t0) : Vector2.Lerp(r0, r1, t0);
                Vector2 b = k % 2 == 0 ? Vector2.Lerp(r0, r1, t1) : Vector2.Lerp(l0, l1, t1);
                if (k == 6) b = Vector2.Lerp(a, b, 0.45f) + new Vector2(0.03f, -0.06f);   // a snapped brace
                Vector2 aa = a, bb = b;
                c.Fill(q => Sdf.Capsule(q, aa, bb, 0.011f), metal, 0f, Around(aa, bb, 0.05f));
            }
            // concrete foot and an access ring two thirds up
            c.Fill(q => Sdf.Box(q, new Vector2(0f, -0.08f), new Vector2(0.32f, 0.14f), 0.03f),
                q => Mul(new Color(0.2f, 0.29f, 0.31f), 0.85f + 0.2f * Noise.Perlin(q.x * 20f, q.y * 20f)).WithAlpha(1f), 0f, new Rect(-0.4f, -0.3f, 0.8f, 0.4f));
            c.Fill(q => Sdf.Box(q, new Vector2(0f, H * 0.62f), new Vector2(0.26f, 0.018f), 0.01f), metal, 0f, new Rect(-0.35f, H * 0.62f - 0.1f, 0.7f, 0.2f));

            // the head: a frame of six lamps, tilted towards the pitch
            MastHead = new Vector2(0.02f, H + 0.28f);
            Vector2 hc = MastHead;
            float tilt = MastHeadTilt;
            Rect headRect = new Rect(hc.x - 0.7f, hc.y - 0.7f, 1.4f, 1.4f);
            c.Fill(q => Sdf.Capsule(q, new Vector2(0f, H - 0.02f), hc, 0.03f), metal, 0f, headRect);
            c.Fill(q => Sdf.Box(q, hc, new Vector2(0.44f, 0.27f), 0.03f, tilt), Mul(steel, 1.15f), 0f, headRect);
            c.Fill(q => Sdf.Box(q, hc + MathUtil.Rotate(new Vector2(0f, -0.3f), tilt), new Vector2(0.44f, 0.035f), 0.02f, tilt), Mul(steel, 0.8f), 0f, headRect);
            for (int gx = 0; gx < 3; gx++)
                for (int gy = 0; gy < 2; gy++)
                {
                    Vector2 lp = hc + MathUtil.Rotate(new Vector2(-0.28f + gx * 0.28f, -0.1f + gy * 0.2f), tilt);
                    bool lit = !(gx == 2 && gy == 1) && !(gx == 0 && gy == 0);
                    Rect lr = new Rect(lp.x - 0.12f, lp.y - 0.12f, 0.24f, 0.24f);
                    c.Fill(q => Sdf.Circle(q, lp, 0.085f), new Color(0.05f, 0.08f, 0.1f), 0f, lr);
                    c.Fill(q => Sdf.Circle(q, lp, 0.066f), lit ? new Color(0.9f, 0.98f, 1f) : new Color(0.16f, 0.22f, 0.25f), 0f, lr);
                    if (!lit)
                        c.Paint(q => Sdf.Capsule(q, lp + new Vector2(-0.04f, 0.03f), lp + new Vector2(0.03f, -0.04f), 0.006f), new Color(0.4f, 0.5f, 0.55f), 0f, lr);
                    MastLamps.Add(new Vector3(lp.x, lp.y, lit ? 1f : 0f));
                }

            // a torn cable dangling from the head
            Vector2 prev = hc + MathUtil.Rotate(new Vector2(0.36f, -0.26f), tilt);
            for (int s = 0; s < 9; s++)
            {
                Vector2 next = prev + new Vector2(0.03f + 0.015f * Mathf.Sin(s * 0.9f), -0.14f);
                Vector2 p0 = prev, p1 = next;
                c.Fill(q => Sdf.Capsule(q, p0, p1, 0.009f), new Color(0.05f, 0.07f, 0.08f), 0f, Around(p0, p1, 0.05f));
                prev = next;
            }
            // ivy climbing the lower half
            var r = new System.Random(5);
            float R() => (float)r.NextDouble();
            for (int i = 0; i < 90; i++)
            {
                float t = R();
                float y = t * t * H * 0.55f;
                float half = Mathf.Lerp(0.17f, 0.055f, y / H);
                Vector2 lc = new Vector2((R() > 0.5f ? half : -half) + (R() - 0.5f) * 0.12f, y);
                float s = 0.03f + R() * 0.03f;
                Color ic = Color.Lerp(Palette.Ivy, Palette.IvyLight, R());
                c.Fill(q => Sdf.Ellipse(q, lc, new Vector2(s, s * 0.7f)), ic, 0f, new Rect(lc.x - 0.08f, lc.y - 0.08f, 0.16f, 0.16f));
            }
            c.RimLight(new Vector2(-0.02f, 0.02f), new Color(0.62f, 0.86f, 0.9f), 0.6f);
            return c;
        }
    }
}
