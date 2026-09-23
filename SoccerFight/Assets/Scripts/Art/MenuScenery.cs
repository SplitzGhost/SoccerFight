using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Art for the title screen's own place in the game world (MenuVista), in the bright Project
    /// Rise look: a sunny valley under a deep blue sky. Far back lavender mountains with snow on the
    /// peaks; in front of them green hills with chunky cartoon trees and, standing on them like the
    /// tower in Project Rise, a huge stadium — a cream bowl with three tiers of arches, colourful
    /// stands and a green pitch showing over its rim, banners, floodlight masts and flag poles.
    /// Below it a meadow in perspective with mowing stripes, flowers, wooden palisades and a sandy
    /// path that leads from the player up to the stadium gate; in front the turf plateau with the
    /// chalk centre circle the player stands on. Painted in world units on worker threads while the
    /// game boots, lit by the sun up to the right. The logo and the big ball for the kick-off
    /// transition are generated here too.
    ///
    /// Vista units: 1 = 1 world unit, origin = centre of the view (the menu camera is 9.8 tall).
    /// Each layer's canvas has its own origin; MenuVista places the layers.
    /// </summary>
    public static class MenuScenery
    {
        public const float SunPpu = 360f;
        /// <summary>Where the sun stands (vista units): up in the right corner, over the stadium.</summary>
        public static readonly Vector2 SunPos = new Vector2(8.1f, 3.5f);

        /// <summary>Heights of the layers' origins in the vista (the ground is pinned under the player instead).</summary>
        public const float PeaksY = -0.45f, HillsY = -1.2f, MeadowY = -1.2f;
        /// <summary>Depth of the plateau's top face (from its back edge to the lip).</summary>
        public const float GroundBand = 0.5f;
        /// <summary>The stadium (hills-local).</summary>
        public const float StadiumX = 4.6f, StadiumBase = 0.22f, StadiumHalfW = 3.2f, StadiumH = 2.05f;

        public static Sprite Logo, HeroBall, HeroHoop, HeroShade, HeroHighlight;
        public static Sprite Sun, Peaks, Hills, Meadow, Ground, Pennant, Butterfly;

        /// <summary>Tops of the flag poles on the stadium (hills-local); MenuVista hangs waving pennants there.</summary>
        public static readonly List<Vector2> StadiumFlags = new List<Vector2>();
        /// <summary>Lamp heads of the floodlight masts (hills-local: x, y, size).</summary>
        public static readonly List<Vector3> Floodlights = new List<Vector3>();

        static ArtJobs jobs;
        static ArtJobs.Job jLogo, jHero, jHeroHoop, jHeroShade, jHeroHi, jSun, jPeaks, jHills, jMeadow, jGround, jPennant, jButterfly;

        static readonly Color Clear = new Color(0f, 0f, 0f, 0f);
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

        /// <summary>How much a slope faces the sun (the sun stands to the right: slopes falling to the right are lit).</summary>
        static float Lit(float slope, float k = 0.8f) => Mathf.Clamp(-slope * k, -0.6f, 0.8f);

        // ------------------------------------------------------------------ orchestration

        public static void Begin()
        {
            _ = Palette.Skin;       // palette statics parse hex through Unity: touch them on the main thread
            _ = Art.Rainbow(0f);    // the ball art reads static data as well
            StadiumFlags.Clear(); Floodlights.Clear();

            jobs = new ArtJobs();
            // UI pieces: straight alpha, centre pivot
            jLogo = Ui("MenuLogo", LogoArt.Build);
            jHero = Ui("MenuHeroBall", () => Art.BallPatternCanvas(640f));
            jHeroHoop = Ui("MenuHeroHoop", () => Art.HoopPatternCanvas(640f));
            jHeroShade = Ui("MenuHeroShade", () => Art.BallShadeCanvas(640f));
            jHeroHi = Ui("MenuHeroHi", () => Art.BallHighlightCanvas(640f));

            // world pieces: premultiplied like every other backdrop sprite, drawn in their own units
            jSun = jobs.Add("MenuSun", () => EnvironmentArt.SunCanvas(SunPpu), Vector2.zero);
            jPeaks = jobs.Add("MenuPeaks", BuildPeaks, Vector2.zero);
            jHills = jobs.Add("MenuHills", BuildHills, Vector2.zero);
            jMeadow = jobs.Add("MenuMeadow", BuildMeadow, Vector2.zero);
            jGround = jobs.Add("MenuGround", BuildGround, Vector2.zero);
            jPennant = jobs.Add("MenuPennant", BuildPennant, Vector2.zero, false);
            jButterfly = jobs.Add("MenuButterfly", BuildButterfly, Vector2.zero, false);
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
            Sun = jSun.Sprite; Peaks = jPeaks.Sprite; Hills = jHills.Sprite; Meadow = jMeadow.Sprite; Ground = jGround.Sprite;
            Pennant = jPennant.Sprite; Butterfly = jButterfly.Sprite;
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
        /// A chunky cartoon tree (Project Rise style): a short trunk under a crown of three to five
        /// round puffs, lit yellow-green from the upper right and falling into teal shade at the lower left.
        /// </summary>
        static void CartoonTree(SdfCanvas c, Vector2 b, float h, System.Random r, Color dark, Color light)
        {
            float R() => (float)r.NextDouble();
            float rad = h * 0.3f;
            Vector2 crown = b + new Vector2(0f, h - rad * 1.1f);
            int n = 3 + r.Next(3);
            var puffs = new (Vector2 c, float r)[n];
            puffs[0] = (crown, rad);
            for (int i = 1; i < n; i++)
            {
                float a = Mathf.Lerp(200f, 340f, R()) * Mathf.Deg2Rad;
                puffs[i] = (crown + new Vector2(Mathf.Cos(a) * rad * 0.85f, Mathf.Sin(a) * rad * 0.55f + rad * 0.1f), rad * (0.55f + R() * 0.25f));
            }
            Color bark = new Color(0.48f, 0.3f, 0.19f);
            Vector2 tTop = crown - new Vector2(0f, rad * 0.3f);
            c.Fill(q => Sdf.Tapered(q, b, h * 0.06f, tTop, h * 0.035f), bark, 0f, Around(b, tTop, h * 0.1f));
            SdfCanvas.SdfFn mass = q =>
            {
                float d = 9f;
                for (int i = 0; i < n; i++) d = Sdf.SmoothUnion(d, Sdf.Circle(q, puffs[i].c, puffs[i].r), rad * 0.18f);
                return d;
            };
            Rect bounds = new Rect(crown.x - rad * 2.2f, crown.y - rad * 1.6f, rad * 4.4f, rad * 3f);
            c.Fill(mass, q =>
            {
                // each puff shaded as a ball lit from the upper right
                float lit = 0f;
                for (int i = 0; i < n; i++)
                {
                    Vector2 k = (q - puffs[i].c) / puffs[i].r;
                    if (k.sqrMagnitude > 1.4f) continue;
                    lit = Mathf.Max(lit, Mathf.Clamp01(0.5f + 0.55f * Vector2.Dot(k, new Vector2(0.55f, 0.7f))));
                }
                Color col = Color.Lerp(dark, light, lit);
                col.a = 1f;
                return col;
            }, 0f, bounds);
            c.Paint(q => Mathf.Max(mass(q) + rad * 0.12f, -(q.x - crown.x) - (q.y - crown.y) + rad * 0.4f), Color.Lerp(light, new Color(1f, 1f, 0.7f), 0.45f).WithAlpha(0.55f), rad * 0.15f, bounds);
        }

        static void Conifer(SdfCanvas c, Vector2 b, float h, Color dark, Color light)
        {
            float w = h * 0.3f;
            SdfCanvas.SdfFn sdf = q =>
            {
                float d = Sdf.Box(q, new Vector2(b.x, b.y + h * 0.06f), new Vector2(w * 0.12f, h * 0.08f));
                for (int k = 0; k < 3; k++)
                {
                    float t = k / 3f;
                    float y0 = b.y + h * (0.1f + t * 0.6f);
                    float tw = w * (1f - t * 0.5f);
                    d = Mathf.Min(d, Sdf.Triangle(q, new Vector2(b.x - tw, y0), new Vector2(b.x + tw, y0), new Vector2(b.x, y0 + h * (0.46f - t * 0.06f))));
                }
                return d - h * 0.015f;
            };
            c.Fill(sdf, q => Color.Lerp(dark, light, S01((q.x - b.x) / w + 0.3f)).WithAlpha(1f), 0f, new Rect(b.x - w - 0.05f, b.y - 0.05f, w * 2f + 0.1f, h * 1.2f + 0.1f));
        }

        /// <summary>A round stone lit from the upper right.</summary>
        static void Stone(SdfCanvas c, Vector2 at, Vector2 rad, Color body, Color lit, int seed)
        {
            float s = seed * 0.37f;
            SdfCanvas.SdfFn f = q => Sdf.Ellipse(q, at, rad) + 0.8f * (Noise.Perlin(q.x * 9f + s, q.y * 9f) - 0.5f) * rad.y;
            Rect b = new Rect(at.x - rad.x * 1.4f, at.y - rad.y * 1.4f, rad.x * 2.8f, rad.y * 2.8f);
            c.Fill(f, body, 0f, b);
            c.Paint(q => Mathf.Max(f(q) + rad.y * 0.18f, (at.y + rad.y * 0.1f - q.y) + (at.x - q.x) * 0.25f), lit, rad.y * 0.35f, b);
            c.Paint(q => Mathf.Max(f(q), q.y - (at.y - rad.y * 0.45f)), Mul(body, 0.72f), rad.y * 0.3f, b);
        }

        // ------------------------------------------------------------------ far mountains

        static SdfCanvas BuildPeaks()
        {
            var c = new SdfCanvas(new Rect(-13f, -1.2f, 26f, 4.4f), 46f);
            int W = c.Width;
            float left = c.UnitRect.xMin, ppu = c.Ppu, aa = 1f / ppu;
            var back = new float[W];
            var front = new float[W];
            for (int x = 0; x < W; x++)
            {
                float ux = left + (x + 0.5f) / ppu;
                back[x] = 1.1f + 1.35f * Ridged(ux * 0.2f, 3.3f) + 0.25f * Ridged(ux * 0.6f, 9.1f) + 0.08f * Fbm(ux * 1.6f, 8.1f)
                          + 0.8f * G(ux, -6.5f, 2f) + 0.5f * G(ux, 9.5f, 1.8f);
                front[x] = 0.35f + 0.8f * Ridged(ux * 0.3f, 7.7f) + 0.08f * Fbm(ux * 2f, 2.9f);
            }
            var sB = Slope(back, ppu);
            var sF = Slope(front, ppu);
            Color bBase = new Color(0.58f, 0.6f, 0.86f), bLit = new Color(0.82f, 0.84f, 1f);
            Color fBase = new Color(0.46f, 0.56f, 0.82f), fLit = new Color(0.66f, 0.78f, 0.96f);
            Color snow = Color.white, mist = new Color(0.86f, 0.93f, 0.98f);
            c.Field(p =>
            {
                int x = Mathf.Clamp((int)((p.x - left) * ppu), 0, W - 1);
                float aB = Mathf.Clamp01(0.5f + (back[x] - p.y) / aa);
                float aF = Mathf.Clamp01(0.5f + (front[x] - p.y) / aa);
                float a = Mathf.Max(aB, aF);
                if (a <= 0f) return Clear;
                float litB = Lit(sB[x]);
                Color b = Color.Lerp(bBase, bLit, Mathf.Clamp01(litB + 0.2f) * 0.9f);
                b = Mul(b, 0.97f + 0.05f * Noise.Perlin(p.x * 1.6f, p.y * 1.6f));
                // snow caps: thick on the upper slopes, gullies of rock showing through
                float below = back[x] - p.y;
                float snowMask = S01((0.45f - below) / 0.2f) * S01((back[x] - 1.9f) / 0.35f) * S01((Noise.Perlin(p.x * 3.5f, p.y * 3f) - 0.25f) / 0.2f);
                b = Color.Lerp(b, Mul(snow, 0.86f + 0.14f * Mathf.Clamp01(litB + 0.5f)), snowMask);
                float litF = Lit(sF[x]);
                Color f = Color.Lerp(fBase, fLit, Mathf.Clamp01(litF + 0.2f) * 0.8f);
                Color col = Color.Lerp(b, f, aF);
                col = Color.Lerp(col, mist, S01(1f - (p.y + 0.2f) / 1f) * 0.35f);
                col.a = a * EnvironmentArt.EdgeFade(p.x, 13f, 1.5f);
                return col;
            });
            c.RimLight(new Vector2(0.03f, 0.04f), new Color(1f, 0.98f, 0.92f), 0.6f);
            return c;
        }

        // ------------------------------------------------------------------ hills and the stadium

        static float HillH(float x)
        {
            float h = 0.42f + 0.3f * Fbm(x * 0.17f, 21f) + 0.12f * Fbm(x * 0.6f, 22f) + 0.55f * G(x, -7.2f, 2.4f) + 0.3f * G(x, 11f, 2f);
            // a broad, level shoulder for the stadium
            return Mathf.Lerp(h, 0.34f, S01(1f - Mathf.Abs(x - StadiumX) / (StadiumHalfW + 0.8f)));
        }

        static float BowlTop(float x)
        {
            float u = (x - StadiumX) / StadiumHalfW;
            return StadiumBase + StadiumH * (0.93f + 0.07f * Mathf.Sqrt(Mathf.Max(0f, 1f - u * u)));
        }

        static SdfCanvas BuildHills()
        {
            var c = new SdfCanvas(new Rect(-13f, -1.4f, 26f, 5.4f), 64f);
            int W = c.Width;
            float left = c.UnitRect.xMin, ppu = c.Ppu, aa = 1f / ppu;
            var hill = new float[W];
            for (int x = 0; x < W; x++) hill[x] = HillH(left + (x + 0.5f) / ppu);
            var slope = Slope(hill, ppu);
            Color grass = new Color(0.4f, 0.7f, 0.34f), grassLit = new Color(0.66f, 0.86f, 0.4f), grassFar = new Color(0.5f, 0.72f, 0.5f);
            c.Field(p =>
            {
                int x = Mathf.Clamp((int)((p.x - left) * ppu), 0, W - 1);
                float a = Mathf.Clamp01(0.5f + (hill[x] - p.y) / aa);
                if (a <= 0f) return Clear;
                float lit = Lit(slope[x], 1.4f);
                Color col = Color.Lerp(grass, grassLit, Mathf.Clamp01(lit + 0.35f) * S01((hill[x] - p.y) < 0.25f ? 1f : 0.4f));
                col = Mul(col, 0.94f + 0.1f * Noise.Perlin(p.x * 3f, p.y * 5f));
                // meadow patches and a lighter crest line
                col = Color.Lerp(col, grassLit, S01(1f - (hill[x] - p.y) / 0.05f) * 0.5f);
                col = Color.Lerp(col, grassFar, S01((-0.2f - p.y) / 1f) * 0.4f);
                col.a = a * EnvironmentArt.EdgeFade(p.x, 13f, 1.2f);
                return col;
            });

            var r = new System.Random(57);
            float R() => (float)r.NextDouble();

            // a back row of small trees along the crests
            Color tDark = new Color(0.2f, 0.48f, 0.32f), tLight = new Color(0.52f, 0.78f, 0.34f);
            for (float tx = left + 0.4f; tx < -left - 0.4f; tx += 0.35f + R() * 0.7f)
            {
                if (Mathf.Abs(tx - StadiumX) < StadiumHalfW + 0.2f) continue;
                if (Noise.Perlin(tx * 0.4f, 3.3f) < 0.42f) continue;
                Vector2 b = new Vector2(tx, hill[Mathf.Clamp((int)((tx - left) * ppu), 0, W - 1)] - 0.04f);
                float h = 0.3f + R() * 0.3f;
                if (R() < 0.3f) Conifer(c, b, h * 1.2f, tDark, tLight);
                else CartoonTree(c, b, h, r, tDark, tLight);
            }

            BuildStadium(c, r);

            // bigger trees on the near slopes, left of the stadium and on the far right
            float[] big = { -11.2f, -9.6f, -8.3f, -4.6f, -3.1f, 9.4f, 10.8f, 12.2f };
            foreach (float bx in big)
            {
                Vector2 b = new Vector2(bx + (R() - 0.5f) * 0.4f, hill[Mathf.Clamp((int)((bx - left) * ppu), 0, W - 1)] - 0.25f - R() * 0.3f);
                CartoonTree(c, b, 0.7f + R() * 0.45f, r, new Color(0.18f, 0.46f, 0.3f), new Color(0.58f, 0.84f, 0.34f));
            }
            c.RimLight(new Vector2(0.025f, 0.03f), new Color(1f, 0.97f, 0.82f), 0.5f);
            return c;
        }

        /// <summary>
        /// The big stadium: seen a little from above, so over the front rim of the cream bowl the
        /// stands on the far side show (colour blocks in rows), and inside them the green pitch.
        /// </summary>
        static void BuildStadium(SdfCanvas c, System.Random r)
        {
            float R() => (float)r.NextDouble();
            const float cx = StadiumX, hw = StadiumHalfW, b0 = StadiumBase, H = StadiumH;
            float rimY = b0 + H;
            Vector2 bowlC = new Vector2(cx, rimY + 0.02f);
            Vector2 bowlR = new Vector2(hw * 0.98f, 0.62f);
            Color[] seatCols = { Palette.Seat1, Palette.Seat2, Palette.Seat3, new Color(0.35f, 0.78f, 0.45f) };
            Color slate = Palette.Slate, slateLit = Mul(Palette.Slate, 1.45f);

            // floodlight masts behind the bowl
            for (int s = -1; s <= 1; s += 2)
            {
                for (int k = 0; k < 2; k++)
                {
                    float mx = cx + s * (hw * (0.62f + 0.36f * k));
                    Vector2 foot = new Vector2(mx, rimY - 0.3f), top = new Vector2(mx + s * 0.05f, rimY + 1.05f + 0.25f * k);
                    Color steel = new Color(0.66f, 0.7f, 0.8f);
                    c.Fill(q => Sdf.Tapered(q, foot, 0.045f, top, 0.025f), steel, 0f, Around(foot, top, 0.1f));
                    Vector2 hc = top + new Vector2(0f, 0.12f);
                    c.Fill(q => Sdf.Box(q, hc, new Vector2(0.22f, 0.13f), 0.03f, s * 8f), new Color(0.5f, 0.55f, 0.66f), 0f, new Rect(hc.x - 0.3f, hc.y - 0.3f, 0.6f, 0.6f));
                    for (int gx = 0; gx < 4; gx++)
                        for (int gy = 0; gy < 2; gy++)
                        {
                            Vector2 lp = hc + MathUtil.Rotate(new Vector2(-0.15f + gx * 0.1f, -0.045f + gy * 0.09f), s * 8f);
                            c.Fill(q => Sdf.Circle(q, lp, 0.035f), new Color(1f, 1f, 0.94f), 0f, new Rect(lp.x - 0.06f, lp.y - 0.06f, 0.12f, 0.12f));
                        }
                    Floodlights.Add(new Vector3(hc.x, hc.y, 0.7f));
                }
            }

            // the far side of the bowl: roof lip, stands in colour blocks, the pitch in the middle
            SdfCanvas.SdfFn inside = q => Sdf.Ellipse(q, bowlC, bowlR);
            c.Fill(q => Sdf.Ellipse(q, bowlC, bowlR + new Vector2(0.08f, 0.1f)), q => Color.Lerp(slate, slateLit, S01((q.x - cx + hw) / (2f * hw))).WithAlpha(1f), 0f,
                new Rect(cx - hw - 0.3f, rimY - 1f, hw * 2f + 0.6f, 2f));
            c.Fill(inside, q =>
            {
                Vector2 k = new Vector2((q.x - bowlC.x) / bowlR.x, (q.y - bowlC.y) / bowlR.y);
                float dist = k.magnitude;
                float ang = Mathf.Atan2(k.y, k.x);
                Color col;
                if (dist < 0.52f)
                {
                    // the pitch, mowing stripes and a chalk halfway line
                    bool stripe = Mathf.Repeat(q.x * 3.2f, 1f) < 0.5f;
                    col = stripe ? new Color(0.42f, 0.74f, 0.32f) : new Color(0.36f, 0.66f, 0.28f);
                    if (Mathf.Abs(q.x - cx) < 0.018f || Mathf.Abs(dist - 0.5f) < 0.012f) col = new Color(0.92f, 0.96f, 0.9f);
                }
                else
                {
                    // stands: rows of seats, blocks of colour around the bowl, darker in the shade at the back
                    int block = Mathf.FloorToInt((ang + Mathf.PI) / (Mathf.PI * 2f) * 16f);
                    col = seatCols[(block * 7 + 3) % seatCols.Length];
                    float row = Mathf.Repeat(dist * 16f, 1f);
                    col = Mul(col, row < 0.28f ? 0.72f : 1f);
                    col = Mul(col, Mathf.Lerp(1f, 0.7f, S01(k.y)));   // the far stands sit under the roof's shadow
                    if (Hash01(block * 131 + Mathf.FloorToInt(dist * 16f) * 17 + Mathf.FloorToInt(ang * 30f)) > 0.9f) col = Color.Lerp(col, Color.white, 0.5f);
                }
                col.a = 1f;
                return col;
            }, 0f, new Rect(cx - hw - 0.2f, rimY - 0.8f, hw * 2f + 0.4f, 1.6f));

            // the facade: cream stone, three tiers of arches with colour showing through, pilasters
            Color facade = new Color(0.86f, 0.82f, 0.76f), facadeLit = new Color(1f, 0.97f, 0.88f), stoneShade = new Color(0.62f, 0.6f, 0.72f);
            const float tierH = 0.62f;
            SdfCanvas.SdfFn body = q => Mathf.Max(Sdf.Box(q, new Vector2(cx, b0 + H * 0.5f), new Vector2(hw, H * 0.5f + 0.3f), 0.3f), q.y - BowlTop(q.x));
            c.Fill(body, q =>
            {
                float u = Mathf.Clamp((q.x - cx) / hw, -0.999f, 0.999f);
                float round = Mathf.Sqrt(1f - u * u);
                Color col = Color.Lerp(stoneShade, Color.Lerp(facade, facadeLit, S01((u + 0.1f) / 1.1f)), 0.55f + 0.45f * round);
                float lv = q.y - b0;
                int tier = Mathf.FloorToInt(lv / tierH);
                float ty = lv - tier * tierH;
                float s = Mathf.Asin(u) * hw;
                float ax = Mathf.Repeat(s, 0.44f) - 0.22f;
                float open = Mathf.Min(Sdf.Box(new Vector2(ax, ty), new Vector2(0f, 0.2f), new Vector2(0.13f, 0.18f)), Sdf.Circle(new Vector2(ax, ty), new Vector2(0f, 0.37f), 0.13f));
                if (tier >= 0 && tier < 3 && ty < tierH - 0.08f)
                {
                    Color seat = seatCols[(tier + 1) % 3];
                    Color deep = Mul(seat, Mathf.Lerp(0.95f, 0.55f, S01(ty / 0.5f)));
                    col = Color.Lerp(col, deep, Mathf.Clamp01(0.5f - open * round * c.Ppu));
                    // the arch's lit reveal
                    col = Color.Lerp(col, facadeLit, Mathf.Clamp01(1f - Mathf.Abs(open + 0.025f) * c.Ppu * 0.5f) * 0.4f * round);
                }
                // cornice bands between the tiers
                float band = Mathf.Abs(ty - (tierH - 0.04f));
                if (band < 0.04f) col = Color.Lerp(col, Mul(facadeLit, 1.02f), 0.8f);
                if (Mathf.Abs(ty - (tierH - 0.085f)) < 0.012f) col = Mul(col, 0.78f);
                // the top rim of the bowl
                if (BowlTop(q.x) - q.y < 0.09f) col = Color.Lerp(facadeLit, Color.white, 0.3f);
                col.a = 1f;
                return col;
            }, 0f, new Rect(cx - hw - 0.2f, b0 - 0.4f, hw * 2f + 0.4f, H + 0.8f));

            // the gate in the middle with a big emblem above it
            Vector2 gate = new Vector2(cx, b0 + 0.02f);
            c.Fill(q => Mathf.Min(Sdf.Box(q, gate + new Vector2(0f, 0.26f), new Vector2(0.34f, 0.28f)), Sdf.Circle(q, gate + new Vector2(0f, 0.54f), 0.34f)),
                q => Color.Lerp(new Color(0.3f, 0.26f, 0.42f), new Color(0.46f, 0.4f, 0.58f), S01((q.y - gate.y) / 0.8f)).WithAlpha(1f), 0f, new Rect(cx - 0.5f, b0 - 0.1f, 1f, 1f));
            Vector2 em = new Vector2(cx, b0 + 1.2f);
            c.Fill(q => Sdf.Circle(q, em, 0.3f), new Color(0.98f, 0.76f, 0.3f), 0f, new Rect(em.x - 0.4f, em.y - 0.4f, 0.8f, 0.8f));
            c.Fill(q => Sdf.Circle(q, em, 0.22f), Color.white, 0f, new Rect(em.x - 0.4f, em.y - 0.4f, 0.8f, 0.8f));
            c.Fill(q => Sdf.Circle(q, em + new Vector2(0.01f, 0.01f), 0.075f), new Color(0.16f, 0.2f, 0.3f), 0f, new Rect(em.x - 0.2f, em.y - 0.2f, 0.4f, 0.4f));
            for (int k = 0; k < 5; k++)
            {
                Vector2 pc = em + MathUtil.Dir(90f + k * 72f) * 0.17f;
                c.Fill(q => Mathf.Max(Sdf.Circle(q, pc, 0.055f), Sdf.Circle(q, em, 0.215f)), new Color(0.16f, 0.2f, 0.3f), 0f, new Rect(pc.x - 0.1f, pc.y - 0.1f, 0.2f, 0.2f));
            }

            // long banners hanging between the arches of the middle tier
            float[] bxs = { -2.2f, -1.1f, 1.1f, 2.2f };
            for (int i = 0; i < bxs.Length; i++)
            {
                float bx = cx + bxs[i];
                float top = b0 + H - 0.14f, bot = b0 + 0.75f;
                Color bc = seatCols[i % 3];
                c.Fill(q => Mathf.Max(Sdf.Box(q, new Vector2(bx, (top + bot) * 0.5f), new Vector2(0.14f, (top - bot) * 0.5f)),
                        -(q.y - bot) - Mathf.Abs(q.x - bx) * 0.7f + 0.1f),
                    q => Color.Lerp(Mul(bc, 0.8f), bc, S01((q.x - bx + 0.14f) / 0.28f)).WithAlpha(1f), 0f, new Rect(bx - 0.2f, bot - 0.2f, 0.4f, top - bot + 0.3f));
                Vector2 dot = new Vector2(bx, top - 0.3f);
                c.Fill(q => Sdf.Circle(q, dot, 0.07f), Color.white, 0f, new Rect(dot.x - 0.1f, dot.y - 0.1f, 0.2f, 0.2f));
            }

            // flag poles along the front rim (the pennants themselves are animated sprites)
            for (int k = 0; k < 7; k++)
            {
                float fx = cx - hw + 0.4f + k * (hw * 2f - 0.8f) / 6f;
                Vector2 pb = new Vector2(fx, BowlTop(fx) - 0.03f), pt = pb + new Vector2(0f, 0.42f);
                c.Fill(q => Sdf.Capsule(q, pb, pt, 0.014f), new Color(0.92f, 0.92f, 0.96f), 0f, Around(pb, pt, 0.04f));
                c.Fill(q => Sdf.Circle(q, pt, 0.028f), new Color(1f, 0.8f, 0.35f), 0f, Around(pt, pt, 0.05f));
                StadiumFlags.Add(pt - new Vector2(0f, 0.03f));
            }

            // shrubs and a low wall hiding the stadium's foot
            for (float x = cx - hw - 0.3f; x < cx + hw + 0.3f; x += 0.22f + R() * 0.3f)
            {
                if (Mathf.Abs(x - cx) < 0.5f) continue;
                Vector2 bp = new Vector2(x, b0 - 0.08f + R() * 0.06f);
                float br = 0.14f + R() * 0.14f;
                c.Fill(q => Sdf.Circle(q, bp, br), q => Color.Lerp(new Color(0.22f, 0.5f, 0.3f), new Color(0.55f, 0.8f, 0.35f), S01((q.y - bp.y + br) / (2f * br) + (q.x - bp.x) * 1.5f)).WithAlpha(1f), 0f,
                    new Rect(bp.x - br, bp.y - br, br * 2f, br * 2f));
            }
        }

        // ------------------------------------------------------------------ the meadow in perspective

        /// <summary>Scale of things at this height of the meadow (0 = far edge, -3.9 = right in front).</summary>
        static float Persp(float y) => Mathf.Lerp(0.3f, 1.3f, Mathf.Clamp01(-y / 3.6f));

        /// <summary>Centre of the sandy path at this height (it runs from the player up to the stadium gate).</summary>
        static float PathX(float y)
        {
            float t = Mathf.Clamp01(-y / 3.9f);   // 0 at the far edge .. 1 in front
            return Mathf.Lerp(StadiumX, 0.4f, MathUtil.EaseInOutSine(t)) + 0.35f * Mathf.Sin(t * 5f) * t;
        }

        static SdfCanvas BuildMeadow()
        {
            var c = new SdfCanvas(new Rect(-12.8f, -3.9f, 25.6f, 4.05f), 80f);
            var r = new System.Random(13);
            float R() => (float)r.NextDouble();
            Color near = new Color(0.42f, 0.74f, 0.3f), nearB = new Color(0.36f, 0.66f, 0.27f);
            Color far = new Color(0.56f, 0.8f, 0.44f), farB = new Color(0.5f, 0.74f, 0.4f);
            Color sand = new Color(0.9f, 0.78f, 0.55f), sandShade = new Color(0.76f, 0.62f, 0.44f);
            Vector2 vp = new Vector2(0.8f, 1.6f);   // vanishing point of the mowing stripes
            c.Field(p =>
            {
                float top = 0.03f * Fbm(p.x * 0.8f, 4.4f);
                float a = Mathf.Clamp01(0.5f + (top - p.y) * c.Ppu);
                if (a <= 0f) return Clear;
                float t = Mathf.Clamp01(-p.y / 3.9f);
                float u = (p.x - vp.x) / (vp.y - p.y);
                bool stripe = Mathf.Repeat(u * 2.2f, 1f) < 0.5f;
                Color col = Color.Lerp(stripe ? far : farB, stripe ? near : nearB, S01(t * 1.3f));
                col = Mul(col, 0.95f + 0.08f * Noise.Perlin(p.x * 4f / Persp(p.y), p.y * 14f));
                // the sandy path
                float pw = 0.08f + 0.5f * Persp(p.y) * Persp(p.y);
                float dPath = Mathf.Abs(p.x - PathX(p.y)) - pw;
                if (dPath < 0.03f)
                {
                    Color s = Color.Lerp(sandShade, sand, S01(-dPath / (pw * 0.6f)));
                    s = Mul(s, 0.95f + 0.1f * Noise.Perlin(p.x * 12f, p.y * 30f));
                    col = Color.Lerp(col, s, Mathf.Clamp01(0.5f - dPath * c.Ppu));
                }
                // a paler line of haze along the far edge
                col = Color.Lerp(col, new Color(0.78f, 0.9f, 0.8f), S01(1f - t / 0.12f) * 0.4f);
                col.a = a * EnvironmentArt.EdgeFade(p.x, 12.8f, 0.8f);
                return col;
            });

            // flowers scattered over the grass: bigger in front
            Color[] petals = { Color.white, new Color(1f, 0.85f, 0.3f), new Color(1f, 0.55f, 0.65f), new Color(0.7f, 0.6f, 1f) };
            for (int i = 0; i < 520; i++)
            {
                float y = -R() * 3.8f;
                float x = -12.5f + R() * 25f;
                if (Mathf.Abs(x - PathX(y)) < 0.2f + 0.5f * Persp(y) * Persp(y)) continue;
                float s = 0.018f * Persp(y) * (0.7f + R() * 0.6f);
                Color pc = petals[r.Next(petals.Length)];
                Vector2 at = new Vector2(x, y);
                c.Fill(q => Sdf.Circle(q, at, s), pc, 0f, new Rect(at.x - s * 2f, at.y - s * 2f, s * 4f, s * 4f));
                if (s > 0.02f) c.Fill(q => Sdf.Circle(q, at, s * 0.4f), new Color(1f, 0.8f, 0.25f), 0f, new Rect(at.x - s, at.y - s, s * 2f, s * 2f));
            }

            // round bushes and a few trees at mid distance, clear of the path and the middle
            for (int i = 0; i < 26; i++)
            {
                float y = -0.15f - R() * 1.6f;
                float x = (R() < 0.5f ? -1f : 1f) * (2.5f + R() * 10f);
                if (Mathf.Abs(x - PathX(y)) < 1.2f) continue;
                float k = Persp(y);
                if (R() < 0.25f) CartoonTree(c, new Vector2(x, y), 0.9f * k + R() * 0.3f * k, r, new Color(0.18f, 0.46f, 0.3f), new Color(0.58f, 0.84f, 0.34f));
                else
                {
                    Vector2 bp = new Vector2(x, y + 0.1f * k);
                    float br = (0.14f + R() * 0.12f) * k * 1.4f;
                    c.Fill(q => Sdf.SmoothUnion(Sdf.Circle(q, bp, br), Sdf.Circle(q, bp + new Vector2(br * 0.8f, -br * 0.2f), br * 0.75f), br * 0.3f),
                        q => Color.Lerp(new Color(0.22f, 0.52f, 0.3f), new Color(0.6f, 0.85f, 0.36f), S01((q.y - bp.y + br) / (2f * br) + (q.x - bp.x) * 0.8f / br * 0.3f)).WithAlpha(1f), 0f,
                        new Rect(bp.x - br * 1.3f, bp.y - br * 1.3f, br * 3.4f, br * 2.6f));
                }
            }

            // wooden palisades (sharpened stakes on two rails), one on each side, running into the distance
            Palisade(c, new Vector2(-12.6f, -1.9f), new Vector2(-3.6f, -0.25f), r);
            Palisade(c, new Vector2(12.6f, -2.1f), new Vector2(7.8f, -0.55f), r);

            // stones along the path
            for (int i = 0; i < 26; i++)
            {
                float y = -0.2f - R() * 3.5f;
                float k = Persp(y);
                float side = R() < 0.5f ? -1f : 1f;
                float x = PathX(y) + side * (0.1f + 0.5f * k * k + R() * 0.15f);
                float s = (0.03f + R() * 0.04f) * k;
                Stone(c, new Vector2(x, y), new Vector2(s * 1.4f, s), new Color(0.62f, 0.62f, 0.74f), new Color(0.95f, 0.93f, 0.88f), i + 300);
            }
            c.RimLight(new Vector2(0.02f, 0.025f), new Color(1f, 0.97f, 0.82f), 0.4f);
            return c;
        }

        /// <summary>Project Rise's pointed wooden fence: stakes that grow with perspective, two rails.</summary>
        static void Palisade(SdfCanvas c, Vector2 nearEnd, Vector2 farEnd, System.Random r)
        {
            float R() => (float)r.NextDouble();
            Color wood = Palette.Wood, woodLit = Palette.WoodLight, woodDark = Palette.WoodDark;
            int n = 34;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                Vector2 b = Vector2.Lerp(farEnd, nearEnd, t * t * 0.6f + t * 0.4f);
                float k = Persp(b.y);
                float h = (0.62f + R() * 0.12f) * k, w = 0.07f * k;
                Vector2 top = b + new Vector2(0f, h);
                SdfCanvas.SdfFn stake = q => Mathf.Min(Sdf.Box(q, b + new Vector2(0f, h * 0.43f), new Vector2(w, h * 0.43f)),
                    Sdf.Triangle(q, top + new Vector2(-w, -h * 0.15f), top + new Vector2(w, -h * 0.15f), top + new Vector2(0f, h * 0.08f)));
                c.Fill(stake, q => Color.Lerp(woodDark, Color.Lerp(wood, woodLit, S01((q.x - b.x + w) / (2f * w))), S01((q.y - b.y) / (h * 0.3f) + 0.4f)).WithAlpha(1f), 0f,
                    new Rect(b.x - w - 0.05f, b.y - 0.05f, w * 2f + 0.1f, h * 1.2f + 0.1f));
            }
            for (int rail = 0; rail < 2; rail++)
            {
                float hy = rail == 0 ? 0.22f : 0.46f;
                Vector2 a = farEnd + new Vector2(0f, hy * Persp(farEnd.y)), bb = nearEnd + new Vector2(0f, hy * Persp(nearEnd.y));
                c.Fill(q => Sdf.Tapered(q, a, 0.018f * Persp(farEnd.y), bb, 0.03f * Persp(nearEnd.y)), wood, 0f, Around(a, bb, 0.1f));
            }
        }

        // ------------------------------------------------------------------ the turf the player stands on

        /// <summary>Back edge of the plateau (ground-local; the player's boots stand at y = 0 around x = 0).</summary>
        public static float GroundTop(float x) =>
            0.02f * Fbm(x * 2.5f, 1.1f) * S01((Mathf.Abs(x) - 1.2f) / 1f) + 0.16f * S01((Mathf.Abs(x) - 5.5f) / 4.5f);

        static SdfCanvas BuildGround()
        {
            var c = new SdfCanvas(new Rect(-12.5f, -2.6f, 25f, 3.2f), 100f);
            float ppu = c.Ppu;
            Color turf = new Color(0.4f, 0.73f, 0.3f), turfB = new Color(0.34f, 0.65f, 0.26f), turfLit = new Color(0.62f, 0.86f, 0.38f);
            Color stone = Palette.Stone, stoneLit = Palette.StoneLight, mortar = Palette.Mortar;
            c.Field(p =>
            {
                float top = GroundTop(p.x);
                float a = Mathf.Clamp01(0.5f + (top - p.y) * ppu);
                if (a <= 0f) return Clear;
                float v = top - p.y;
                Color col;
                if (v < GroundBand)
                {
                    // the turf top at a slant: mowing stripes and the chalk centre circle around the player
                    float yy = 1f - v / GroundBand;               // 1 back edge .. 0 front lip
                    bool stripe = Mathf.Repeat(p.x * 0.6f + v * 0.5f, 1f) < 0.5f;
                    col = Color.Lerp(stripe ? turf : turfB, turfLit, yy * 0.2f);
                    col = Mul(col, 0.93f + 0.12f * Noise.Perlin(p.x * 26f, v * 70f));
                    float ellipse = new Vector2(p.x / 2.1f, (v - GroundBand * 0.5f) / (GroundBand * 0.42f)).magnitude;
                    float chalk = 1f - S01((Mathf.Abs(ellipse - 1f) * 2.1f - 0.018f) / 0.01f);
                    col = Color.Lerp(col, new Color(0.96f, 0.98f, 0.94f), chalk * 0.85f);
                    // the lip: a rounded, lit edge of grass
                    col = Color.Lerp(col, turfLit, S01((v - (GroundBand - 0.06f)) / 0.05f) * 0.7f);
                }
                else
                {
                    // the front face: big blocks of cream stone with a carved band, lit from the right
                    float e = v - GroundBand;
                    const float bh = 0.34f, bw = 0.9f;
                    int row = Mathf.FloorToInt(e / bh);
                    float off = (row & 1) == 0 ? 0f : bw * 0.5f;
                    float colF = Mathf.Floor((p.x + off) / bw);
                    float lx = p.x + off - colF * bw, ly = e - row * bh;
                    float edge = Mathf.Min(Mathf.Min(lx, bw - lx), Mathf.Min(ly, bh - ly));
                    float h = Hash01((int)colF * 57 + row * 131);
                    col = Color.Lerp(stone, stoneLit, 0.35f + 0.25f * h + 0.25f * S01(1f - ly / bh));
                    col = Color.Lerp(col, mortar, 1f - S01((edge - 0.012f) / 0.012f));
                    // carved swirl band along the top row
                    if (row == 0) col = Color.Lerp(col, Mul(stoneLit, 1.02f), 0.35f);
                    col = Mul(col, Mathf.Lerp(1f, 0.72f, S01(e / 2f)));
                }
                col.a = a;
                return col;
            });

            var r = new System.Random(31);
            float R() => (float)r.NextDouble();
            // grass hanging over the lip
            for (float x = -12.4f; x < 12.4f; x += 0.04f + R() * 0.1f)
            {
                float top = GroundTop(x) - GroundBand;
                float h = 0.04f + R() * R() * 0.12f;
                Vector2 gb = new Vector2(x, top + 0.03f), gt = new Vector2(x + (R() - 0.5f) * 0.05f, top - h);
                c.Fill(q => Sdf.Tapered(q, gb, 0.018f, gt, 0.003f), Color.Lerp(turf, turfLit, R() * 0.6f), 0f, Around(gb, gt, 0.05f));
            }
            // a few stones on the turf towards the sides
            for (int i = 0; i < 14; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float x = side * (3.5f + R() * 8.5f);
                float s = 0.05f + R() * 0.07f;
                Stone(c, new Vector2(x, GroundTop(x) - 0.12f - R() * 0.25f + s * 0.5f), new Vector2(s * 1.4f, s), new Color(0.6f, 0.6f, 0.72f), new Color(0.96f, 0.94f, 0.88f), i + 900);
            }
            c.RimLight(new Vector2(0.02f, 0.03f), new Color(1f, 0.97f, 0.85f), 0.45f);
            return c;
        }

        // ------------------------------------------------------------------ small animated props

        /// <summary>A pennant: white (tinted per use), pivot at the pole; MenuVista waves it.</summary>
        static SdfCanvas BuildPennant()
        {
            var c = new SdfCanvas(new Rect(0f, -0.14f, 0.5f, 0.2f), 160f);
            c.Fill(p => Sdf.Triangle(p, new Vector2(0f, 0.05f), new Vector2(0f, -0.12f), new Vector2(0.48f, -0.04f)),
                p => Color.Lerp(new Color(0.8f, 0.8f, 0.8f), Color.white, S01(p.y / 0.1f + 0.7f)));
            return c;
        }

        /// <summary>A butterfly seen from above: two pairs of rounded wings, white (tinted per use).</summary>
        static SdfCanvas BuildButterfly()
        {
            var c = new SdfCanvas(new Rect(-0.12f, -0.1f, 0.24f, 0.2f), 220f);
            for (int s = -1; s <= 1; s += 2)
            {
                c.Fill(p => Sdf.Ellipse(p, new Vector2(s * 0.055f, 0.03f), new Vector2(0.05f, 0.045f)), Color.white);
                c.Fill(p => Sdf.Ellipse(p, new Vector2(s * 0.04f, -0.04f), new Vector2(0.035f, 0.03f)), new Color(0.9f, 0.9f, 0.9f));
            }
            c.Fill(p => Sdf.Capsule(p, new Vector2(0f, -0.06f), new Vector2(0f, 0.06f), 0.008f), new Color(0.25f, 0.2f, 0.25f));
            return c;
        }
    }
}
