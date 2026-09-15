using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// The deep backdrop and the play-plane structures, generated on worker threads next to
    /// EnvironmentArt: a far range of snowy peaks with a castle, a forest hill with a ruined stadium
    /// and its floodlight masts, a broken aqueduct, near columns and a giant trunk, the one-way
    /// platforms (terraces, column capitals, floating rocks) and the chalk markings of the pitch.
    /// Backdrop colours are authored at full brightness — WorldEnvironment darkens every layer by
    /// its depth, so the scene gets darker the further back it goes.
    /// </summary>
    public static class DepthArt
    {
        public sealed class PlatformArt
        {
            public Level.Platform P;
            public Sprite Body, Support;
            public Vector2 BodyCenter, SupportCenter;
            public readonly List<Vector2> Hangs = new List<Vector2>();      // anchors along the underside (world)
            public readonly List<Vector2> Crystals = new List<Vector2>();   // glowing crystal spots (world)
            public Vector2 Lantern = new Vector2(float.NaN, 0f);            // lantern hook (world)
            public bool HasLantern => !float.IsNaN(Lantern.x);
            internal ArtJobs.Job BodyJob, SupportJob;
        }

        public static Sprite Peaks, FarForest, Aqueduct, ColumnTall, ColumnBroken, Trunk;
        public static Sprite MarkCenter, MarkLeft, MarkRight, Puddle, GroundStrip;
        public static Sprite[] Pebbles;
        public static PlatformArt[] Platforms;

        public static readonly List<Vector2> PeakLights = new List<Vector2>();     // castle windows (layer space)
        public static readonly List<Vector3> ForestLights = new List<Vector3>();   // x, y, size (layer space)
        public static readonly List<Vector2> AqueductIvy = new List<Vector2>();    // hanging ivy anchors (layer space)
        public static readonly List<Vector2> TrunkMoss = new List<Vector2>();      // relative to the trunk pivot
        public static readonly List<Vector2> ColumnIvy = new List<Vector2>();      // relative to the tall column pivot
        public static Vector2 AqueductFallTop;
        public static float AqueductFallLength;

        public const float PeaksW = 26f, ForestW = 30f, AqW = 34f;
        public const float MarkBoxCenter = 14.2f;
        // every far layer stands on the horizon: local y = 0 is placed at eye level by WorldEnvironment
        const float PeaksY0 = -3f, PeaksY1 = 4.4f, CastleX = 5.3f, CastleK = 0.62f;
        const float ForestY0 = -2.6f, ForestY1 = 4.2f, StadiumX = -6.4f, TowerX = 6.9f;
        const float AqY0 = -1.6f, AqY1 = 5.4f;
        const float AqP1 = 2.5f, AqR1 = 0.88f, AqSpring1 = 1.95f, AqTop1 = 3.1f;
        const float AqP2 = 1.25f, AqR2 = 0.36f, AqSpring2 = 3.62f, AqTop2 = 4.3f, AqTop3 = 4.95f;
        const float TopFront = 0.12f, TopBack = 0.14f;    // walkable strip: front edge / back edge around the standing line

        static ArtJobs jobs;
        static ArtJobs.Job jPeaks, jForest, jAqueduct, jColTall, jColBroken, jTrunk, jMarkC, jMarkL, jMarkR, jPuddle, jStrip;
        static ArtJobs.Job[] jPebbles;

        // ---------------------------------------------------------------- orchestration

        public static void Begin()
        {
            PeakLights.Clear(); ForestLights.Clear(); AqueductIvy.Clear(); TrunkMoss.Clear(); ColumnIvy.Clear();
            jobs = new ArtJobs();
            jPeaks = jobs.Add("Peaks", BuildPeaks, Vector2.zero);
            jForest = jobs.Add("Far Forest", BuildFarForest, Vector2.zero);
            jAqueduct = jobs.Add("Aqueduct", BuildAqueduct, Vector2.zero);
            jColTall = jobs.Add("Column Tall", () => BuildColumn(true), Vector2.zero);
            jColBroken = jobs.Add("Column Broken", () => BuildColumn(false), Vector2.zero);
            jTrunk = jobs.Add("Trunk", BuildTrunk, Vector2.zero);
            jMarkC = jobs.Add("Markings Centre", () => BuildMarkings(0f), Vector2.zero);
            jMarkL = jobs.Add("Markings Left", () => BuildMarkings(-1f), new Vector2(-MarkBoxCenter, 0f));
            jMarkR = jobs.Add("Markings Right", () => BuildMarkings(1f), new Vector2(MarkBoxCenter, 0f));
            jPuddle = jobs.Add("Puddle", BuildPuddle, Vector2.zero);
            jStrip = jobs.Add("Ground Strip", BuildStrip, Vector2.zero, false);
            jPebbles = new ArtJobs.Job[3];
            for (int i = 0; i < jPebbles.Length; i++) { int k = i; jPebbles[i] = jobs.Add("Pebble" + k, () => BuildPebble(k), Vector2.zero, false); }

            var plats = Level.Platforms;
            Platforms = new PlatformArt[plats.Length];
            for (int i = 0; i < plats.Length; i++)
            {
                var art = new PlatformArt { P = plats[i] };
                Platforms[i] = art;
                art.BodyCenter = BodyRect(plats[i]).center;
                art.BodyJob = jobs.Add("Platform" + i, () => BuildPlatformBody(art), art.BodyCenter);
                if (plats[i].Kind != Level.Style.Rock)
                {
                    art.SupportCenter = SupportRect(plats[i]).center;
                    art.SupportJob = jobs.Add("Support" + i, () => BuildSupport(art), art.SupportCenter);
                }
            }
            jobs.Start();
        }

        public static void End()
        {
            jobs.Complete();
            Peaks = jPeaks.Sprite; FarForest = jForest.Sprite; Aqueduct = jAqueduct.Sprite;
            ColumnTall = jColTall.Sprite; ColumnBroken = jColBroken.Sprite; Trunk = jTrunk.Sprite;
            MarkCenter = jMarkC.Sprite; MarkLeft = jMarkL.Sprite; MarkRight = jMarkR.Sprite;
            Puddle = jPuddle.Sprite; GroundStrip = jStrip.Sprite;
            Pebbles = new Sprite[jPebbles.Length];
            for (int i = 0; i < Pebbles.Length; i++) Pebbles[i] = jPebbles[i].Sprite;
            foreach (var a in Platforms)
            {
                a.Body = a.BodyJob.Sprite;
                a.Support = a.SupportJob?.Sprite;
                a.BodyJob = a.SupportJob = null;
            }
            jobs = null;
        }

        // ---------------------------------------------------------------- helpers (thread-safe)

        static float Fbm(float x, float seed, int octaves = 4) => EnvironmentArt.Fbm(x, seed, octaves);
        static float Sq(float v) => v * v;
        static float S01(float v) => MathUtil.Smooth01(v);
        static float Ridge(float x, float seed) => 1f - Mathf.Abs(Fbm(x, seed, 5));
        static float Hash01(int n) => MathUtil.Hash(n) * 0.5f + 0.5f;
        static readonly Color Clear = new Color(0f, 0f, 0f, 0f);

        /// <summary>Scale rgb, keep alpha (Color * float would scale alpha too).</summary>
        static Color Mul(Color c, float f) { c.r *= f; c.g *= f; c.b *= f; return c; }

        static float[] Slope(float[] h, float ppu)
        {
            int w = h.Length;
            var s = new float[w];
            for (int x = 0; x < w; x++) s[x] = (h[Mathf.Min(w - 1, x + 2)] - h[Mathf.Max(0, x - 2)]) / (4f / ppu);
            return s;
        }

        static int Col(SdfCanvas c, float x) => Mathf.Clamp((int)((x - c.UnitRect.xMin) * c.Ppu), 0, c.Width - 1);

        static Rect Around(Vector2 a, Vector2 b, float pad) =>
            Rect.MinMaxRect(Mathf.Min(a.x, b.x) - pad, Mathf.Min(a.y, b.y) - pad, Mathf.Max(a.x, b.x) + pad, Mathf.Max(a.y, b.y) + pad);

        /// <summary>Layered conifer silhouette standing at b.</summary>
        static void Conifer(SdfCanvas c, Vector2 b, float h, Color col, float widthK)
        {
            float w = h * widthK;
            SdfCanvas.SdfFn sdf = q =>
            {
                float d = Sdf.Box(q, new Vector2(b.x, b.y + h * 0.08f), new Vector2(w * 0.12f, h * 0.1f));
                for (int k = 0; k < 3; k++)
                {
                    float t = k / 3f;
                    float y0 = b.y + h * (0.12f + t * 0.62f);
                    float tw = w * (1f - t * 0.55f);
                    float th = h * (0.46f - t * 0.06f);
                    d = Mathf.Min(d, Sdf.Triangle(q, new Vector2(b.x - tw, y0), new Vector2(b.x + tw, y0), new Vector2(b.x, y0 + th)));
                }
                return d;
            };
            c.Fill(sdf, col, 0f, new Rect(b.x - w - 0.05f, b.y - 0.05f, w * 2f + 0.1f, h * 1.25f + 0.1f));
        }

        static void RoundTree(SdfCanvas c, Vector2 b, float h, Color col, System.Random r)
        {
            float rad = h * 0.34f;
            Vector2 crown = b + new Vector2(0f, h - rad);
            float o1 = ((float)r.NextDouble() - 0.5f) * rad, o2 = ((float)r.NextDouble() - 0.5f) * rad;
            SdfCanvas.SdfFn sdf = q => Mathf.Min(Sdf.Box(q, new Vector2(b.x, b.y + h * 0.25f), new Vector2(h * 0.03f, h * 0.25f)),
                Sdf.SmoothUnion(Sdf.Circle(q, crown, rad), Mathf.Min(Sdf.Circle(q, crown + new Vector2(rad * 0.7f, -rad * 0.3f + o1 * 0.3f), rad * 0.7f),
                    Sdf.Circle(q, crown + new Vector2(-rad * 0.7f, -rad * 0.25f + o2 * 0.3f), rad * 0.72f)), rad * 0.3f));
            c.Fill(sdf, col, 0f, new Rect(b.x - rad * 1.6f, b.y - 0.05f, rad * 3.2f, h + 0.1f));
        }

        // ---------------------------------------------------------------- far peaks with a castle (p ≈ 0.95)

        static SdfCanvas BuildPeaks()
        {
            var c = new SdfCanvas(new Rect(-PeaksW * 0.5f, PeaksY0, PeaksW, PeaksY1 - PeaksY0), 54f);
            int W = c.Width;
            float left = c.UnitRect.xMin, ppu = c.Ppu, aa = 1f / ppu;
            var back = new float[W];
            var front = new float[W];
            for (int x = 0; x < W; x++)
            {
                float ux = left + (x + 0.5f) / ppu;
                back[x] = 1.15f + 1.25f * Ridge(ux * 0.15f, 3.3f) + 0.12f * Fbm(ux * 1.1f, 8.1f) + 0.55f * Mathf.Exp(-Sq((ux - CastleX) / 1.2f));
                front[x] = 0.45f + 0.8f * Ridge(ux * 0.24f, 7.7f) + 0.09f * Fbm(ux * 1.7f, 2.9f);
            }
            var slopeB = Slope(back, ppu);
            var slopeF = Slope(front, ppu);

            Color bBase = new Color(0.2f, 0.35f, 0.45f), bLit = new Color(0.46f, 0.64f, 0.73f), snow = new Color(0.8f, 0.9f, 0.96f);
            Color fBase = new Color(0.11f, 0.22f, 0.29f), fLit = new Color(0.28f, 0.45f, 0.53f);
            Color mist = new Color(0.36f, 0.56f, 0.64f), valley = new Color(0.09f, 0.18f, 0.24f);
            c.Field(p =>
            {
                int x = Mathf.Clamp((int)((p.x - left) * ppu), 0, W - 1);
                float aB = Mathf.Clamp01(0.5f + (back[x] - p.y) / aa);
                float aF = Mathf.Clamp01(0.5f + (front[x] - p.y) / aa);
                float a = Mathf.Max(aB, aF);
                if (a <= 0f) return Clear;
                // back range: facets lit by the moon, crag streaks, snow caught in the upper gullies
                float litB = Mathf.Clamp(-slopeB[x] * 0.8f, -0.6f, 0.8f);
                float crag = Noise.Perlin(p.x * 6.5f, p.y * 0.7f + 3f);
                Color b = Color.Lerp(bBase, bLit, Mathf.Clamp01(litB) * 0.8f);
                b = Mul(b, (1f + Mathf.Min(0f, litB) * 0.3f) * (0.84f + 0.26f * crag));
                float below = back[x] - p.y;
                float snowMask = S01((0.32f - below) / 0.2f) * S01((Noise.Perlin(p.x * 4.4f, p.y * 3.6f) - 0.32f) / 0.2f) * S01((back[x] - 1.85f) / 0.45f);
                b = Color.Lerp(b, Mul(snow, 0.7f + 0.4f * Mathf.Clamp01(litB + 0.3f)), snowMask * 0.85f);
                // front range: darker, forested
                float litF = Mathf.Clamp(-slopeF[x] * 0.8f, -0.6f, 0.8f);
                Color f = Mul(Color.Lerp(fBase, fLit, Mathf.Clamp01(litF) * 0.6f), 0.88f + 0.2f * Noise.Perlin(p.x * 4f + 9f, p.y * 1.3f));
                Color col = Color.Lerp(b, f, aF);
                // valley mist pooling at the feet, then solid ground far below (never shows a gap)
                col = Color.Lerp(col, mist, S01(1f - (p.y - 0.05f) / 0.8f) * 0.5f);
                col = Color.Lerp(col, valley, S01((0.05f - p.y) / 1f));
                col.a = a * EnvironmentArt.EdgeFade(p.x, PeaksW * 0.5f, 2f);
                return col;
            });

            // pine forests clinging to the front range in patches
            var r = new System.Random(91);
            float R() => (float)r.NextDouble();
            Color pine = new Color(0.07f, 0.16f, 0.21f);
            for (int i = 0; i < 360; i++)
            {
                float px = left + 1f + R() * (PeaksW - 2f);
                if (Noise.Perlin(px * 0.5f, 4.4f) < 0.45f) continue;
                int xi = Col(c, px);
                float by = front[xi] - 0.03f - R() * R() * Mathf.Max(0f, front[xi] - 0.3f);
                float h = 0.06f + R() * 0.11f, w = h * 0.3f;
                Color pc = Mul(pine, 0.85f + R() * 0.3f);
                c.Fill(q => Mathf.Min(Sdf.Triangle(q, new Vector2(px - w, by), new Vector2(px + w, by), new Vector2(px, by + h)),
                        Sdf.Triangle(q, new Vector2(px - w * 0.7f, by + h * 0.4f), new Vector2(px + w * 0.7f, by + h * 0.4f), new Vector2(px, by + h * 1.15f))),
                    pc, 0f, new Rect(px - w - 0.05f, by - 0.05f, w * 2f + 0.1f, h * 1.2f + 0.1f));
            }

            // a castle on the highest summit, right in front of the moon
            // the castle is built in "castle units" (CastleK scales it to the distance)
            float cb = back[Col(c, CastleX)] - 0.08f;
            Vector2 K(float dx, float dy) => new Vector2(CastleX + dx * CastleK, cb + dy * CastleK);
            Vector2 H(float hx, float hy) => new Vector2(hx * CastleK, hy * CastleK);
            Color stone = new Color(0.12f, 0.21f, 0.28f);
            SdfCanvas.SdfFn castle = q =>
            {
                float d = Sdf.Box(q, K(0f, 0.34f), H(0.5f, 0.34f));
                d = Mathf.Min(d, Sdf.Box(q, K(0.05f, 0.62f), H(0.17f, 0.62f)));
                d = Mathf.Min(d, Sdf.Box(q, K(-0.46f, 0.5f), H(0.09f, 0.5f)));
                d = Mathf.Min(d, Sdf.Box(q, K(0.5f, 0.42f), H(0.08f, 0.42f)));
                d = Mathf.Min(d, Sdf.Triangle(q, K(-0.14f, 1.24f), K(0.24f, 1.24f), K(0.05f, 1.62f)));
                d = Mathf.Min(d, Sdf.Triangle(q, K(-0.57f, 1f), K(-0.35f, 1f), K(-0.46f, 1.3f)));
                d = Mathf.Min(d, Sdf.Triangle(q, K(0.41f, 0.84f), K(0.59f, 0.84f), K(0.5f, 1.08f)));
                float step = 0.08f * CastleK;
                float cx = Mathf.Round((q.x - CastleX) / step) * step + CastleX;
                d = Mathf.Min(d, Mathf.Max(Sdf.Box(q, new Vector2(cx, cb + 0.71f * CastleK), H(0.022f, 0.035f)), Mathf.Abs(q.x - CastleX) - 0.5f * CastleK));
                return d;
            };
            c.Fill(castle, q => Mul(stone, 0.9f + 0.25f * S01((q.x - CastleX + 0.2f) / 0.5f)), 0f, new Rect(CastleX - 0.6f, cb - 0.1f, 1.2f, 1.25f));
            // moonlit crests keep the far range readable even after the depth darkening
            c.RimLight(new Vector2(0.03f, 0.04f), new Color(0.76f, 0.9f, 0.96f), 0.75f);

            Vector2[] windows = { K(0.05f, 0.98f), K(0.05f, 0.64f), K(-0.46f, 0.78f), K(0.27f, 0.4f), K(-0.24f, 0.44f) };
            foreach (var w in windows)
            {
                c.Fill(q => Sdf.Box(q, w, H(0.024f, 0.042f), 0.008f), new Color(1f, 0.8f, 0.48f), 0f, new Rect(w.x - 0.08f, w.y - 0.08f, 0.16f, 0.16f));
                PeakLights.Add(w);
            }
            return c;
        }

        // ---------------------------------------------------------------- forest hill, ruined stadium, bell tower (p ≈ 0.77)

        static SdfCanvas BuildFarForest()
        {
            var c = new SdfCanvas(new Rect(-ForestW * 0.5f, ForestY0, ForestW, ForestY1 - ForestY0), 48f);
            int W = c.Width;
            float left = c.UnitRect.xMin, ppu = c.Ppu, aa = 1f / ppu;
            var hill = new float[W];
            for (int x = 0; x < W; x++)
            {
                float ux = left + (x + 0.5f) / ppu;
                float plateau = S01(1f - Mathf.Abs(ux - StadiumX) / 3.8f) * 0.25f + S01(1f - Mathf.Abs(ux - TowerX) / 1.3f) * 0.2f;
                hill[x] = 0.5f + 0.35f * Fbm(ux * 0.1f, 21f) + 0.15f * Fbm(ux * 0.45f, 22f) + plateau;
            }
            Color ground = new Color(0.1f, 0.2f, 0.25f), mist = new Color(0.34f, 0.52f, 0.58f), valley = new Color(0.08f, 0.16f, 0.21f);
            c.Field(p =>
            {
                int x = Mathf.Clamp((int)((p.x - left) * ppu), 0, W - 1);
                float a = Mathf.Clamp01(0.5f + (hill[x] - p.y) / aa);
                if (a <= 0f) return Clear;
                Color col = Mul(ground, 0.9f + 0.2f * Noise.Perlin(p.x * 2.2f, p.y * 3f));
                col = Color.Lerp(col, mist, S01(1f - (p.y + 0.2f) / 1.2f) * 0.45f);
                col = Color.Lerp(col, valley, S01((-0.2f - p.y) / 1.2f));
                col.a = a * EnvironmentArt.EdgeFade(p.x, ForestW * 0.5f, 2f);
                return col;
            });

            var r = new System.Random(57);
            float R() => (float)r.NextDouble();
            Color backTree = new Color(0.17f, 0.3f, 0.36f), frontTree = new Color(0.08f, 0.17f, 0.21f);

            // back row of hazy firs
            for (float tx = left + 0.4f; tx < -left - 0.4f; tx += 0.12f + R() * 0.2f)
            {
                if (Mathf.Abs(tx - StadiumX) < 3f && R() < 0.85f) continue;
                float h = 0.3f + R() * 0.34f;
                Conifer(c, new Vector2(tx, hill[Col(c, tx)] - 0.06f), h, Mul(backTree, 0.9f + R() * 0.2f), 0.22f + R() * 0.08f);
            }

            // the old stadium bowl: three tiers of arches, the right end collapsed
            float sb = hill[Col(c, StadiumX)] - 0.08f;
            const float halfW = 2.7f, height = 1.15f, tierH = 0.34f;
            Color facade = new Color(0.17f, 0.28f, 0.34f), facadeLit = new Color(0.32f, 0.47f, 0.52f), dark = new Color(0.04f, 0.08f, 0.11f);
            SdfCanvas.SdfFn bowl = q =>
            {
                float u = (q.x - StadiumX) / halfW;
                float top = sb + height * (0.92f + 0.08f * Mathf.Sqrt(Mathf.Max(0f, 1f - u * u)))
                            - S01((u - 0.35f) / 0.5f) * (0.66f + 0.2f * Noise.Perlin(q.x * 7f, 3.3f)) - 0.04f * Noise.Perlin(q.x * 16f, 1.1f);
                float body = Sdf.Box(q, new Vector2(StadiumX, sb + height * 0.5f), new Vector2(halfW, height * 0.5f + 0.2f), 0.28f);
                return Mathf.Max(body, q.y - top);
            };
            c.Fill(bowl, q =>
            {
                float u = Mathf.Clamp((q.x - StadiumX) / halfW, -0.999f, 0.999f);
                float round = Mathf.Sqrt(1f - u * u);
                Color col = Color.Lerp(facade, facadeLit, S01((u + 0.2f) / 1.2f) * 0.6f);
                col = Mul(col, 0.72f + 0.28f * round);
                float lv = q.y - sb;
                int tier = Mathf.FloorToInt(lv / tierH);
                float ty = lv - tier * tierH;
                // arches follow the curved wall: their rhythm compresses where it turns away
                float s = Mathf.Asin(u) * halfW;
                float ax = Mathf.Repeat(s, 0.26f) - 0.13f;
                float open = Mathf.Min(Sdf.Box(new Vector2(ax, ty), new Vector2(0f, 0.11f), new Vector2(0.075f, 0.1f)), Sdf.Circle(new Vector2(ax, ty), new Vector2(0f, 0.2f), 0.075f));
                if (tier >= 0 && tier < 3)
                    col = Color.Lerp(col, Color.Lerp(dark, Mul(dark, 1.8f), S01(ty / 0.3f)), Mathf.Clamp01(0.5f - open * round * ppu));
                if (Mathf.Abs(ty - (tierH - 0.02f)) < 0.014f) col = Mul(col, 1.2f);
                col.a = 1f;
                return col;
            }, 0f, new Rect(StadiumX - halfW - 0.2f, sb - 0.3f, halfW * 2f + 0.4f, height + 0.6f));

            // floodlight masts at both ends: the left one leans broken and dark, the right one still burns
            Color steel = new Color(0.1f, 0.18f, 0.22f);
            for (int side = -1; side <= 1; side += 2)
            {
                float mx = StadiumX + side * (halfW + 0.12f);
                float mb = hill[Col(c, mx)] - 0.04f;
                float lean = side < 0 ? -0.13f : 0.03f;
                Vector2 top = new Vector2(mx + lean, mb + 1.9f);
                Vector2 l0 = new Vector2(mx - 0.1f, mb), r0 = new Vector2(mx + 0.1f, mb);
                Vector2 l1 = top + new Vector2(-0.03f, 0f), r1 = top + new Vector2(0.03f, 0f);
                c.Fill(q => Mathf.Min(Sdf.Capsule(q, l0, l1, 0.011f), Sdf.Capsule(q, r0, r1, 0.011f)), steel, 0f, Around(l0, r1, 0.1f));
                for (int k = 0; k < 9; k++)
                {
                    float t0 = k / 9f, t1 = (k + 1) / 9f;
                    Vector2 a = k % 2 == 0 ? Vector2.Lerp(l0, l1, t0) : Vector2.Lerp(r0, r1, t0);
                    Vector2 b = k % 2 == 0 ? Vector2.Lerp(r0, r1, t1) : Vector2.Lerp(l0, l1, t1);
                    c.Fill(q => Sdf.Capsule(q, a, b, 0.005f), steel, 0f, Around(a, b, 0.05f));
                }
                float tilt = side < 0 ? -28f : 14f;
                Vector2 hc = top + new Vector2(0f, 0.12f);
                c.Fill(q => Sdf.Box(q, hc, new Vector2(0.2f, 0.11f), 0.015f, tilt), Mul(steel, 1.3f), 0f, new Rect(hc.x - 0.3f, hc.y - 0.3f, 0.6f, 0.6f));
                for (int gx = 0; gx < 4; gx++)
                    for (int gy = 0; gy < 3; gy++)
                    {
                        Vector2 lp = hc + MathUtil.Rotate(new Vector2(-0.135f + gx * 0.09f, -0.065f + gy * 0.065f), tilt);
                        bool lit = side > 0 && MathUtil.Hash(gx * 7 + gy * 13 + 3) > -0.35f;
                        Color lc = lit ? new Color(0.92f, 0.98f, 1f) : new Color(0.2f, 0.27f, 0.3f);
                        c.Fill(q => Sdf.Circle(q, lp, 0.022f), lc, 0f, new Rect(lp.x - 0.05f, lp.y - 0.05f, 0.1f, 0.1f));
                    }
                if (side > 0) ForestLights.Add(new Vector3(hc.x, hc.y, 1.1f));
            }

            // bell tower with a broken roof
            float tb = hill[Col(c, TowerX)] - 0.08f;
            const float tw = 0.22f, th = 1.7f;
            Color tstone = new Color(0.16f, 0.26f, 0.32f);
            SdfCanvas.SdfFn tower = q =>
            {
                float d = Sdf.Box(q, new Vector2(TowerX, tb + th * 0.5f), new Vector2(tw, th * 0.5f));
                float roof = Sdf.Triangle(q, new Vector2(TowerX - tw - 0.04f, tb + th), new Vector2(TowerX + tw + 0.04f, tb + th), new Vector2(TowerX, tb + th + 0.5f));
                roof = Mathf.Max(roof, q.x - (TowerX + 0.03f + 0.07f * Noise.Perlin(q.y * 12f, 2f)));
                d = Mathf.Min(d, roof);
                d = Mathf.Min(d, Sdf.Box(q, new Vector2(TowerX - 0.42f, tb + 0.24f + 0.07f * Noise.Perlin(q.x * 8f, 5f)), new Vector2(0.22f, 0.26f)));
                return d;
            };
            c.Fill(tower, q =>
            {
                Color col = Mul(tstone, 0.85f + 0.3f * S01((q.x - TowerX + tw) / (2f * tw)));
                if (Mathf.Repeat(q.y - tb, 0.1f) < 0.011f) col = Mul(col, 0.8f);
                col.a = 1f;
                return col;
            }, 0f, new Rect(TowerX - 0.75f, tb - 0.1f, 1.1f, th + 0.7f));
            Vector2 bo = new Vector2(TowerX, tb + th - 0.28f);
            c.Fill(q => Mathf.Min(Sdf.Box(q, bo + new Vector2(0f, -0.045f), new Vector2(0.1f, 0.12f)), Sdf.Circle(q, bo + new Vector2(0f, 0.075f), 0.1f)), dark, 0f,
                new Rect(bo.x - 0.15f, bo.y - 0.22f, 0.3f, 0.45f));
            c.Fill(q => Mathf.Min(Sdf.Ellipse(q, bo + new Vector2(0f, -0.015f), new Vector2(0.05f, 0.065f)), Sdf.Box(q, bo + new Vector2(0f, -0.075f), new Vector2(0.065f, 0.015f))),
                new Color(0.2f, 0.24f, 0.2f), 0f, new Rect(bo.x - 0.12f, bo.y - 0.15f, 0.24f, 0.24f));

            // front row: taller firs and a few broadleaf trees hiding the feet of the buildings
            for (float tx = left + 0.3f; tx < -left - 0.3f; tx += 0.14f + R() * 0.26f)
            {
                if (Mathf.Abs(tx - StadiumX) < 2.4f && R() < 0.55f) continue;
                if (Mathf.Abs(tx - TowerX) < 0.35f) continue;
                bool nearBuilding = Mathf.Abs(tx - StadiumX) < 2.9f || Mathf.Abs(tx - TowerX) < 0.8f;
                float h = nearBuilding ? 0.22f + R() * 0.24f : 0.3f + R() * 0.46f;
                Vector2 b = new Vector2(tx, hill[Col(c, tx)] - 0.12f);
                Color col = Mul(frontTree, 0.85f + R() * 0.3f);
                if (R() < 0.18f) RoundTree(c, b, h * 0.85f, col, r);
                else Conifer(c, b, h, col, 0.2f + R() * 0.1f);
            }
            c.RimLight(new Vector2(0.03f, 0.04f), new Color(0.62f, 0.82f, 0.88f), 0.6f);

            // lit windows (after the rim so they stay warm)
            Vector2 wl = new Vector2(TowerX + 0.06f, tb + 0.95f);
            c.Fill(q => Sdf.Box(q, wl, new Vector2(0.026f, 0.05f), 0.011f), new Color(1f, 0.78f, 0.45f), 0f, new Rect(wl.x - 0.08f, wl.y - 0.1f, 0.16f, 0.2f));
            ForestLights.Add(new Vector3(wl.x, wl.y, 0.4f));
            Vector2 wd = new Vector2(TowerX - 0.07f, tb + 0.6f);
            c.Fill(q => Sdf.Box(q, wd, new Vector2(0.022f, 0.045f), 0.009f), dark, 0f, new Rect(wd.x - 0.08f, wd.y - 0.08f, 0.16f, 0.16f));
            return c;
        }

        // ---------------------------------------------------------------- aqueduct (p ≈ 0.54)

        // three spans still stand; between them only broken piers and rubble remain, so the forest,
        // the stadium and the mountains show through
        static readonly Vector2[] AqSpans = { new Vector2(-10.8f, 3.1f), new Vector2(-1.9f, 2.4f), new Vector2(8.2f, 3.3f) };   // centre, half width

        static float AqTopExact(float x)
        {
            float intact = 0f;
            foreach (var s in AqSpans) intact = Mathf.Max(intact, S01((s.y - Mathf.Abs(x - s.x)) / 0.9f));
            float jag = 0.2f * (Noise.Perlin(x * 5.1f, 2.2f) - 0.5f) + 0.09f * (Noise.Perlin(x * 13f, 7.7f) - 0.5f);
            float stub = 1.2f + 1.2f * Noise.Perlin(x * 0.8f, 4.4f) + jag;
            float top = Mathf.Lerp(stub, AqTop3 + 0.06f, intact);
            return intact > 0.02f && intact < 0.98f ? top + jag * 2f : top;
        }

        static float AqArch1(Vector2 p, out float cx)
        {
            cx = Mathf.Round(p.x / AqP1) * AqP1;
            return Mathf.Min(Sdf.Box(p, new Vector2(cx, (AqY0 + AqSpring1) * 0.5f), new Vector2(AqR1, (AqSpring1 - AqY0) * 0.5f)), Sdf.Circle(p, new Vector2(cx, AqSpring1), AqR1));
        }

        static float AqArch2(Vector2 p, out float cx)
        {
            cx = Mathf.Round(p.x / AqP2) * AqP2;
            const float y0 = AqTop1 + 0.14f;
            return Mathf.Min(Sdf.Box(p, new Vector2(cx, (y0 + AqSpring2) * 0.5f), new Vector2(AqR2, (AqSpring2 - y0) * 0.5f)), Sdf.Circle(p, new Vector2(cx, AqSpring2), AqR2));
        }

        static Color Voussoirs(Vector2 q, float dist, float r, float width, int segs, float cx)
        {
            float seg = Mathf.PI / segs;
            float a = Mathf.Clamp(Mathf.Atan2(q.y, q.x), 0f, Mathf.PI) / seg;
            int si = Mathf.Min(segs - 1, (int)a);
            float fa = a - Mathf.Floor(a);
            float edge = Mathf.Min(Mathf.Min(fa, 1f - fa) * seg * dist, Mathf.Min(dist - r, r + width - dist));
            float mortar = 1f - S01((edge - 0.008f) / 0.007f);
            float h = MathUtil.Hash(si * 17 + (int)(cx * 5f) + 300);
            Color c = Color.Lerp(Palette.Stone, Palette.StoneLight, 0.4f + 0.12f * h + (si == segs / 2 ? 0.15f : 0f));
            return Color.Lerp(c, Palette.Mortar, mortar * 0.85f);
        }

        static SdfCanvas BuildAqueduct()
        {
            var c = new SdfCanvas(new Rect(-AqW * 0.5f, AqY0, AqW, AqY1 - AqY0 + 0.4f), 52f);
            int W = c.Width;
            float left = c.UnitRect.xMin, ppu = c.Ppu, aa = 1f / ppu;
            var top = new float[W];
            for (int x = 0; x < W; x++) top[x] = AqTopExact(left + (x + 0.5f) / ppu);

            Color mist = new Color(0.3f, 0.47f, 0.53f);
            c.Field(p =>
            {
                int xi = Mathf.Clamp((int)((p.x - left) * ppu), 0, W - 1);
                float t = top[xi];
                float o1 = AqArch1(p, out float cx1);
                float o2 = AqArch2(p, out float cx2);
                float d1 = Mathf.Max(Mathf.Max(AqY0 - 1f - p.y, p.y - AqTop1), -o1);
                float d2 = Mathf.Max(Mathf.Max(AqTop1 - p.y, p.y - AqTop2), -o2);
                float d3 = Mathf.Max(AqTop2 - p.y, p.y - AqTop3);
                float d = Mathf.Max(Mathf.Min(d1, Mathf.Min(d2, d3)), p.y - t);
                float a = Mathf.Clamp01(0.5f - d / aa);
                if (a <= 0f) return Clear;

                Color col;
                Vector2 q1 = p - new Vector2(cx1, AqSpring1), q2 = p - new Vector2(cx2, AqSpring2);
                float dist1 = q1.magnitude, dist2 = q2.magnitude;
                if (p.y > AqTop2)
                {
                    // water channel: smooth dressed stone, a lighter coping on top
                    col = Color.Lerp(Palette.Stone, Palette.StoneLight, 0.45f + 0.1f * Noise.Perlin(p.x * 4f, p.y * 9f));
                    if (p.y > AqTop3 - 0.08f) col = Mul(col, 1.15f);
                    if (Mathf.Abs(p.y - (AqTop3 - 0.08f)) < 0.012f) col = Mul(col, 0.7f);
                    if (Mathf.Repeat(p.x, 0.9f) < 0.012f) col = Mul(col, 0.75f);
                }
                else if (Mathf.Abs(p.y - AqTop1) < 0.07f)
                {
                    col = Mul(Color.Lerp(Palette.Stone, Palette.StoneLight, 0.6f), p.y > AqTop1 ? 1.08f : 0.9f);
                }
                else if (p.y > AqTop1)
                {
                    if (q2.y > -0.02f && dist2 > AqR2 && dist2 < AqR2 + 0.13f) col = Voussoirs(q2, dist2, AqR2, 0.13f, 9, cx2);
                    else col = EnvironmentArt.BrickColor(p, 0.17f, 0.38f, 11);
                }
                else
                {
                    if (q1.y > -0.02f && dist1 > AqR1 && dist1 < AqR1 + 0.22f) col = Voussoirs(q1, dist1, AqR1, 0.22f, 15, cx1);
                    else col = EnvironmentArt.BrickColor(p, 0.22f, 0.5f, 7);
                }
                // shadowed reveals, stains below the broken top, cracks, mist at the feet
                float reveal = Mathf.Min(o1 > 0f ? o1 : 9f, o2 > 0f ? o2 : 9f);
                if (reveal < 0.06f) col = Mul(col, Mathf.Lerp(0.66f, 1f, reveal / 0.06f));
                float stain = Noise.Perlin(p.x * 3.7f, 3.1f);
                col = Mul(col, 1f - Mathf.Max(0f, stain - 0.45f) * 0.5f * S01((t - p.y) / 1.4f) * S01(p.y));
                float cn = Mathf.Abs(Noise.Perlin(p.x * 2.1f + 5f, p.y * 2.1f) - 0.5f);
                if (cn < 0.01f && Noise.Perlin(p.x * 0.6f, p.y * 0.6f + 3f) > 0.56f) col = Mul(col, 0.55f);
                col = Color.Lerp(col, mist, S01(1f - p.y / 1.8f) * 0.5f);
                col.a = a * EnvironmentArt.EdgeFade(p.x, AqW * 0.5f, 1.5f);
                return col;
            });
            c.RimLight(new Vector2(0.04f, 0.04f), Palette.RuinRim, 0.5f);
            c.EdgeBand(Vector2.up, 0.07f, Palette.Moss, p => S01((Noise.Perlin(p.x * 1.8f, p.y * 1.8f + 4f) - 0.35f) / 0.2f));

            // rubble heaps under the collapsed spans and shrubs along the foot
            var r = new System.Random(77);
            float R() => (float)r.NextDouble();
            Color rubble = Color.Lerp(Palette.Stone, Palette.StoneLight, 0.3f);
            float[] heaps = { -6.2f, 2.8f, 13.6f, -15.5f };
            foreach (float hx in heaps)
                for (int i = 0; i < 16; i++)
                {
                    Vector2 bc = new Vector2(hx + (R() - 0.5f) * 3f, -0.05f + R() * 0.45f * (1f - Mathf.Abs(R() - 0.5f)));
                    Vector2 half = new Vector2(0.1f + R() * 0.18f, 0.06f + R() * 0.08f);
                    float ang = (R() - 0.5f) * 50f;
                    c.Fill(q => Sdf.Box(q, bc, half, 0.025f, ang), q => Mul(rubble, 0.7f + 0.5f * S01((q.y - bc.y + half.y) / (2f * half.y))),
                        0f, new Rect(bc.x - 0.4f, bc.y - 0.4f, 0.8f, 0.8f));
                }
            Color shrub = new Color(0.08f, 0.2f, 0.21f);
            for (float x = left + 0.3f; x < -left - 0.3f; x += 0.5f + R() * 1.1f)
            {
                Vector2 bp = new Vector2(x, 0.05f + R() * 0.25f);
                float br = 0.22f + R() * 0.35f;
                c.Fill(q => Sdf.Circle(q, bp, br), shrub, 0f, new Rect(bp.x - br, bp.y - br, br * 2f, br * 2f));
            }

            // ivy anchors along the intact channel and the lower cornice
            for (float x = left + 1f; x < -left - 1f; x += 0.7f + R() * 1.1f)
            {
                float t = top[Col(c, x)];
                if (t > AqTop3 - 0.05f && R() > 0.3f) AqueductIvy.Add(new Vector2(x, AqTop3 - 0.02f));
                else if (t > AqTop1 + 0.1f && R() > 0.6f) AqueductIvy.Add(new Vector2(x, AqTop1 - 0.04f));
            }
            // water still pours out of the broken end of the middle span
            AqueductFallTop = new Vector2(AqSpans[1].x + AqSpans[1].y - 0.72f, AqTop2 + 0.12f);
            AqueductFallLength = AqueductFallTop.y - 0.1f;
            return c;
        }

        // ---------------------------------------------------------------- near columns and a giant trunk (p ≈ 0.28)

        static SdfCanvas BuildColumn(bool tall)
        {
            var c = new SdfCanvas(tall ? new Rect(-1f, -1.6f, 2f, 9f) : new Rect(-1.2f, -1.6f, 2.8f, 5.6f), 72f);
            float shaftTop = tall ? 6.1f : 3.4f;
            int seed = tall ? 3 : 8;
            SdfCanvas.ColorFn stone = p =>
            {
                float sx = Mathf.Clamp(p.x / 0.3f, -1f, 1f);
                float flute = 0.5f + 0.5f * Mathf.Cos(p.x * MathUtil.Tau / 0.1f);
                Color col = Color.Lerp(Mul(Palette.Stone, 0.95f), Mul(Palette.StoneLight, 1.12f), 0.35f + 0.4f * sx);
                col = Mul(col, 0.9f + 0.12f * flute);
                if (Mathf.Abs(Mathf.Repeat(p.y + (p.x > 0f ? 0.03f : 0f), 0.72f) - 0.36f) > 0.345f) col = Mul(col, 0.72f);
                col = Mul(col, 0.9f + 0.2f * Noise.Perlin(p.x * 7f + seed, p.y * 3f));
                col.a = 1f;
                return col;
            };
            SdfCanvas.SdfFn shaft = q =>
            {
                float hw = 0.3f - 0.035f * (q.y / 6f) + 0.012f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(q.y / 6f));
                float d = Mathf.Max(Mathf.Abs(q.x) - hw, Mathf.Max(0.35f - q.y, q.y - shaftTop));
                if (!tall) d = Mathf.Max(d, q.y - (shaftTop - 0.35f * Noise.Perlin(q.x * 9f, 4f) - 0.25f * S01((q.x + 0.3f) / 0.6f)));
                return d;
            };
            // foundation + plinth
            c.Fill(q => Sdf.Box(q, new Vector2(0f, -0.65f), new Vector2(0.5f, 0.95f)), q => EnvironmentArt.BrickColor(q, 0.3f, 0.5f, seed).WithAlpha(1f), 0f, new Rect(-0.6f, -1.7f, 1.2f, 2f));
            c.Fill(q => Sdf.Box(q, new Vector2(0f, 0.2f), new Vector2(0.46f, 0.18f), 0.02f), q => Mul(Color.Lerp(Palette.Stone, Palette.StoneLight, 0.55f + 0.3f * Mathf.Clamp(q.x / 0.46f, -1f, 1f)), 1f).WithAlpha(1f),
                0f, new Rect(-0.55f, -0.05f, 1.1f, 0.5f));
            c.Fill(q => Sdf.Ellipse(q, new Vector2(0f, 0.43f), new Vector2(0.37f, 0.07f)), q => Color.Lerp(Palette.Stone, Palette.StoneLight, 0.6f + 0.3f * Mathf.Clamp(q.x / 0.37f, -1f, 1f)).WithAlpha(1f),
                0f, new Rect(-0.45f, 0.3f, 0.9f, 0.25f));
            c.Fill(shaft, stone, 0f, new Rect(-0.4f, 0.3f, 0.8f, shaftTop));
            if (tall)
            {
                // capital and a broken piece of the architrave
                c.Fill(q => Mathf.Max(Sdf.Ellipse(q, new Vector2(0f, 6.1f), new Vector2(0.44f, 0.24f)), Mathf.Max(6.1f - q.y, q.y - 6.34f)),
                    q => Color.Lerp(Palette.Stone, Palette.StoneLight, 0.5f + 0.35f * Mathf.Clamp(q.x / 0.44f, -1f, 1f)).WithAlpha(1f), 0f, new Rect(-0.5f, 6f, 1f, 0.4f));
                c.Fill(q => Sdf.Box(q, new Vector2(0f, 6.42f), new Vector2(0.48f, 0.08f), 0.01f), q => Color.Lerp(Palette.Stone, Palette.StoneLight, 0.7f).WithAlpha(1f), 0f, new Rect(-0.55f, 6.3f, 1.1f, 0.25f));
                c.Fill(q => Mathf.Max(Sdf.Box(q, new Vector2(0.2f, 6.76f), new Vector2(0.72f, 0.26f)), q.x - (0.78f - 0.2f * Noise.Perlin(q.y * 8f, 1.5f) - 0.3f * S01((q.y - 6.5f) / 0.5f))),
                    q =>
                    {
                        Color col = Color.Lerp(Palette.Stone, Palette.StoneLight, 0.45f);
                        if (Mathf.Abs(q.y - 6.83f) < 0.015f || Mathf.Abs(q.y - 6.66f) < 0.012f) col = Mul(col, 0.72f);
                        col.a = 1f;
                        return col;
                    }, 0f, new Rect(-0.6f, 6.4f, 1.6f, 0.7f));
            }
            else
            {
                // a fallen drum lies beside the stump
                c.Fill(q => Sdf.Box(q, new Vector2(0.95f, 0.26f), new Vector2(0.4f, 0.26f), 0.05f, -8f),
                    q => Mul(Color.Lerp(Palette.Stone, Palette.StoneLight, 0.45f + 0.15f * Mathf.Cos(q.x * MathUtil.Tau / 0.1f)), 0.85f + 0.3f * S01(q.y / 0.5f)).WithAlpha(1f),
                    0f, new Rect(0.4f, -0.1f, 1.1f, 0.7f));
            }
            c.RimLight(new Vector2(0.04f, 0.03f), Palette.RuinRim, 0.6f);
            c.EdgeBand(Vector2.up, 0.08f, Palette.Moss, p => S01((Noise.Perlin(p.x * 2.5f, p.y * 2.5f) - 0.3f) / 0.2f));

            // ivy climbing the shaft
            var r = new System.Random(seed * 13);
            float R() => (float)r.NextDouble();
            float ivyTop = tall ? 4.6f : 2.9f;
            for (int i = 0; i < (tall ? 190 : 110); i++)
            {
                float y = 0.3f + R() * ivyTop;
                float x = Mathf.Sin(y * 1.9f + 1f) * 0.24f + (R() - 0.5f) * 0.14f;
                float size = 0.07f + R() * 0.05f;
                Color lc = Color.Lerp(Palette.Ivy, Palette.IvyLight, R() * 0.7f + (x > 0f ? 0.25f : 0f));
                FoliageArt.Leaf(c, new Vector2(x, y), R() * 360f, size, size * 0.55f, lc, 0.15f);
            }
            if (tall)
                for (float x = -0.5f; x < 0.8f; x += 0.35f + R() * 0.3f) ColumnIvy.Add(new Vector2(x, 6.52f));
            return c;
        }

        static SdfCanvas BuildTrunk()
        {
            var c = new SdfCanvas(new Rect(-1.8f, -1.6f, 3.6f, 12.6f), 58f);
            float CenterX(float y) => 0.14f * Mathf.Sin(y * 0.35f + 0.5f);
            float HalfW(float y) => 0.5f - 0.012f * y + 0.55f * Mathf.Exp(-Mathf.Max(0f, y) * 2.2f);
            SdfCanvas.SdfFn trunk = q => Mathf.Abs(q.x - CenterX(q.y)) - HalfW(q.y) - 0.03f * Noise.Perlin(q.y * 3f, 1f);
            SdfCanvas.ColorFn bark = q =>
            {
                // a near trunk: lighter bark than the trees further back
                Color col = EnvironmentArt.BarkColor(q);
                float sx = Mathf.Clamp((q.x - CenterX(q.y)) / HalfW(q.y), -1f, 1f);
                col = Mul(col, (0.78f + 0.4f * S01((sx + 0.6f) / 1.6f)) * 1.35f);
                col.a = 1f;
                return col;
            };
            c.Fill(trunk, bark, 0f, new Rect(-1.4f, -1.6f, 2.8f, 12.6f));
            // roots gripping the ground
            var r = new System.Random(29);
            float R() => (float)r.NextDouble();
            for (int i = 0; i < 5; i++)
            {
                float side = i % 2 == 0 ? 1f : -1f;
                Vector2 a = new Vector2(side * (0.2f + R() * 0.3f), 0.7f + R() * 0.5f);
                Vector2 e = new Vector2(side * (1f + R() * 0.6f), -0.35f - R() * 0.3f);
                Vector2 m = Vector2.Lerp(a, e, 0.5f) + new Vector2(side * 0.15f, 0.15f);
                c.Fill(q => Mathf.Min(Sdf.Tapered(q, a, 0.2f, m, 0.12f), Sdf.Tapered(q, m, 0.12f, e, 0.03f)), bark, 0f, Around(a, e, 0.4f));
            }
            // a heavy branch reaching left and a broken stub on the right
            Vector2 bs = new Vector2(CenterX(6.6f) - 0.3f, 6.6f), be = new Vector2(-1.7f, 8.4f), bm = Vector2.Lerp(bs, be, 0.5f) + new Vector2(0.1f, 0.25f);
            c.Fill(q => Mathf.Min(Sdf.Tapered(q, bs, 0.2f, bm, 0.12f), Sdf.Tapered(q, bm, 0.12f, be, 0.06f)), bark, 0f, Around(bs, be, 0.4f));
            Vector2 ss = new Vector2(CenterX(4.8f) + 0.35f, 4.8f), se = ss + new Vector2(0.55f, 0.35f);
            c.Fill(q => Sdf.Tapered(q, ss, 0.13f, se, 0.09f), bark, 0f, Around(ss, se, 0.3f));
            // knot hole
            Vector2 k = new Vector2(CenterX(3.1f) + 0.1f, 3.1f);
            c.Fill(q => Sdf.Ellipse(q, k, new Vector2(0.14f, 0.22f)), Mul(Palette.Bark, 1.4f).WithAlpha(1f), 0f, new Rect(k.x - 0.3f, k.y - 0.4f, 0.6f, 0.8f));
            c.Fill(q => Sdf.Ellipse(q, k + new Vector2(0.01f, -0.02f), new Vector2(0.09f, 0.16f)), new Color(0.01f, 0.03f, 0.04f), 0f, new Rect(k.x - 0.3f, k.y - 0.4f, 0.6f, 0.8f));
            c.RimLight(new Vector2(0.05f, 0.03f), Palette.BarkLight, 0.7f);
            c.EdgeBand(Vector2.up, 0.08f, Palette.Moss, p => S01((Noise.Perlin(p.x * 1.9f + 4f, p.y * 1.9f) - 0.38f) / 0.15f));
            TrunkMoss.Add(Vector2.Lerp(bs, bm, 0.55f) + new Vector2(0f, -0.1f));
            TrunkMoss.Add(Vector2.Lerp(bm, be, 0.45f) + new Vector2(0f, -0.06f));
            TrunkMoss.Add(Vector2.Lerp(bm, be, 0.85f) + new Vector2(0f, -0.04f));
            TrunkMoss.Add(se + new Vector2(-0.05f, -0.08f));
            return c;
        }

        // ---------------------------------------------------------------- platforms

        static Rect BodyRect(in Level.Platform p)
        {
            switch (p.Kind)
            {
                case Level.Style.Rock: return new Rect(p.X0 - 0.45f, p.Y - 1.75f, p.Width + 0.9f, 2.1f);
                case Level.Style.Terrace: return new Rect(p.X0 - 0.45f, p.Y - 0.95f, p.Width + 0.9f, 1.75f);
                default: return new Rect(p.X0 - 0.4f, p.Y - 1.0f, p.Width + 0.8f, 1.35f);
            }
        }

        static Rect SupportRect(in Level.Platform p) => new Rect(p.X0 - 0.3f, 0.1f, p.Width + 0.6f, p.Y - 0.45f);

        static SdfCanvas BuildPlatformBody(PlatformArt art)
        {
            switch (art.P.Kind)
            {
                case Level.Style.Terrace: return BuildTerrace(art);
                case Level.Style.Capital: return BuildCapital(art);
                default: return BuildRock(art);
            }
        }

        /// <summary>
        /// The walkable top in slight perspective: v = 0 at the front edge, 1 at the back edge. Stone
        /// platforms show worn flagstones through the moss; rocks are grown over with turf.
        /// </summary>
        static Color TopColor(Vector2 p, float top, bool stone, float seed)
        {
            float v = Mathf.Clamp01((p.y - (top - TopFront)) / (TopFront + TopBack));
            Color turf = Color.Lerp(new Color(0.2f, 0.52f, 0.42f), new Color(0.12f, 0.34f, 0.3f), v);
            float grain = Noise.Perlin(p.x * 30f + seed, p.y * 9f);
            float patch = Noise.Perlin(p.x * 2.1f + seed * 3f, p.y * 4f);
            Color col = turf;
            if (stone)
            {
                float slant = p.x + (v - 0.5f) * 0.35f;
                float jx = Mathf.Abs(Mathf.Repeat(slant, 0.55f) - 0.275f);
                Color flag = Color.Lerp(new Color(0.2f, 0.33f, 0.36f), new Color(0.31f, 0.46f, 0.49f), Hash01((int)Mathf.Floor(slant / 0.55f) * 31 + (int)seed));
                if (jx > 0.262f || Mathf.Abs(v - 0.5f) < 0.03f) flag = Mul(flag, 0.62f);
                col = Color.Lerp(flag, turf, S01((patch - 0.45f) / 0.15f));
            }
            col = Mul(col, 0.88f + 0.22f * grain);
            // the front lip catches the moonlight, the back edge falls into shade
            col = Color.Lerp(col, new Color(0.56f, 0.86f, 0.74f), S01((0.2f - v) / 0.2f) * 0.45f);
            col = Mul(col, 1f - 0.25f * S01((v - 0.6f) / 0.4f));
            col.a = 1f;
            return col;
        }

        /// <summary>Hanging grass tips along the front edge of a walkable top.</summary>
        static void Fringe(SdfCanvas c, float x0, float x1, float edgeY, System.Random r, float density)
        {
            for (float x = x0; x < x1; x += (0.05f + (float)r.NextDouble() * 0.14f) / density)
            {
                float h = 0.025f + (float)(r.NextDouble() * r.NextDouble()) * 0.1f;
                float lean = ((float)r.NextDouble() - 0.5f) * 0.05f;
                Vector2 gb = new Vector2(x, edgeY + 0.02f), gt = new Vector2(x + lean, edgeY - h);
                Color gc = Color.Lerp(Palette.PitchA, Palette.GrassEdge, 0.25f + (float)r.NextDouble() * 0.35f);
                c.Fill(q => Sdf.Tapered(q, gb, 0.012f, gt, 0.002f), gc, 0f, Around(gb, gt, 0.03f));
            }
        }

        static Color FaceStone(Vector2 p, float light) =>
            Color.Lerp(new Color(0.15f, 0.26f, 0.3f), new Color(0.33f, 0.49f, 0.53f), Mathf.Clamp01(light)).WithAlpha(1f);

        /// <summary>Sparse hairline cracks: noise contour lines, but only inside a few weathered patches.</summary>
        static bool Crack(Vector2 p, float seed) =>
            Mathf.Abs(Noise.Perlin(p.x * 3.3f + seed, p.y * 3.3f) - 0.5f) < 0.008f && Noise.Perlin(p.x * 0.9f + seed, p.y * 0.9f + 3f) > 0.58f;

        static SdfCanvas BuildTerrace(PlatformArt art)
        {
            var pl = art.P;
            float T = pl.Y, x0 = pl.X0, x1 = pl.X1, seed = pl.Seed;
            const float faceBottom = 0.6f;
            var c = new SdfCanvas(BodyRect(pl), 110f);
            var r = new System.Random(pl.Seed);
            float R() => (float)r.NextDouble();

            // broken balustrade along the back edge (drawn behind whoever stands on the terrace)
            float railGap = x0 + (x1 - x0) * (0.3f + R() * 0.4f);
            SdfCanvas.SdfFn balustrade = q =>
            {
                float d = Sdf.Box(q, new Vector2(pl.Center, T + 0.13f), new Vector2(pl.Width * 0.5f - 0.05f, 0.03f));
                float k = Mathf.Round((q.x - x0) / 0.2f);
                float bx = x0 + k * 0.2f;
                bool missing = MathUtil.Hash((int)k * 17 + pl.Seed) > 0.5f || bx < x0 + 0.2f || bx > x1 - 0.2f;
                if (!missing)
                {
                    float vase = Mathf.Min(Sdf.Ellipse(q, new Vector2(bx, T + 0.27f), new Vector2(0.058f, 0.1f)), Sdf.Box(q, new Vector2(bx, T + 0.4f), new Vector2(0.024f, 0.08f)));
                    d = Mathf.Min(d, Mathf.Min(vase, Sdf.Box(q, new Vector2(bx, T + 0.18f), new Vector2(0.045f, 0.03f))));
                }
                float rail = Sdf.Box(q, new Vector2(pl.Center, T + 0.52f), new Vector2(pl.Width * 0.5f - 0.05f, 0.045f), 0.01f);
                rail = Mathf.Max(rail, 0.35f - Mathf.Abs(q.x - railGap) + 0.05f * Noise.Perlin(q.y * 20f, seed));
                d = Mathf.Min(d, rail);
                d = Mathf.Min(d, Sdf.Box(q, new Vector2(x0 + 0.12f, T + 0.33f), new Vector2(0.07f, 0.27f), 0.01f));
                d = Mathf.Min(d, Sdf.Box(q, new Vector2(x1 - 0.12f, T + 0.33f), new Vector2(0.07f, 0.27f), 0.01f));
                return d;
            };
            c.Fill(balustrade, q => Mul(FaceStone(q, 0.25f + 0.35f * S01((q.x - x0) / pl.Width)), 0.78f + 0.15f * Noise.Perlin(q.x * 9f, q.y * 9f)),
                0f, new Rect(x0 - 0.1f, T + 0.05f, pl.Width + 0.2f, 0.7f));

            // the slab: walkable top + carved cornice face, crumbling at both ends
            SdfCanvas.SdfFn slab = q =>
            {
                float n = 0.07f * Noise.Perlin(q.y * 11f, seed) + 0.05f * Noise.Perlin(q.y * 29f, seed + 5f);
                float ends = Mathf.Max(x0 - 0.1f + n - q.x, q.x - (x1 + 0.1f - n));
                return Mathf.Max(ends, Mathf.Max(T - faceBottom - q.y, q.y - (T + TopBack)));
            };
            c.Fill(slab, q =>
            {
                if (q.y > T - TopFront) return TopColor(q, T, true, seed);
                float f = (T - TopFront) - q.y;
                float lit = 0.35f + 0.25f * S01((q.x - x0) / pl.Width);
                Color col;
                if (f < 0.05f) col = FaceStone(q, lit + 0.35f);                                         // fillet
                else if (f < 0.19f) col = FaceStone(q, lit + 0.3f * Mathf.Cos((f - 0.05f) / 0.14f * Mathf.PI));   // cyma
                else if (f < 0.29f)
                {
                    bool gap = Mathf.Repeat(q.x - x0, 0.12f) > 0.075f;                                  // dentils
                    col = FaceStone(q, gap ? lit - 0.45f : lit + 0.1f);
                }
                else if (f < 0.44f)
                {
                    col = FaceStone(q, lit);                                                            // fascia blocks
                    if (Mathf.Repeat(q.x - x0 + MathUtil.Hash((int)seed) * 0.4f, 0.8f) < 0.014f) col = Mul(col, 0.62f);
                }
                else col = FaceStone(q, lit - 0.4f);                                                    // chamfer
                col = Mul(col, 0.9f + 0.18f * Noise.Perlin(q.x * 13f, q.y * 13f));
                float stain = Noise.Perlin(q.x * 4.3f, seed);
                col = Mul(col, 1f - Mathf.Max(0f, stain - 0.5f) * 0.6f * S01(f / 0.3f));
                if (Crack(q, seed)) col = Mul(col, 0.62f);
                col.a = 1f;
                return col;
            }, 0f, new Rect(x0 - 0.3f, T - faceBottom - 0.1f, pl.Width + 0.6f, faceBottom + 0.35f));

            // moss spilling over the fillet
            for (float x = x0 + 0.1f; x < x1 - 0.1f; x += 0.25f + R() * 0.6f)
            {
                if (R() < 0.35f) continue;
                Vector2 mc = new Vector2(x, T - TopFront - 0.03f);
                Vector2 mr = new Vector2(0.12f + R() * 0.2f, 0.04f + R() * 0.05f);
                c.Paint(q => Sdf.Ellipse(q, mc, mr) + 0.02f * Noise.Perlin(q.x * 40f, q.y * 40f), Palette.Moss.WithAlpha(0.8f), 0.01f, new Rect(mc.x - 0.4f, mc.y - 0.2f, 0.8f, 0.4f));
            }
            Fringe(c, x0 - 0.05f, x1 + 0.05f, T - TopFront, r, 0.6f);
            c.RimLight(new Vector2(0.03f, 0.03f), new Color(0.62f, 0.9f, 0.9f), 0.45f);

            for (float x = x0 + 0.25f; x < x1 - 0.25f; x += 0.3f + R() * 0.55f) art.Hangs.Add(new Vector2(x, T - faceBottom + 0.03f));
            art.Lantern = new Vector2(pl.Center < 0f ? x1 - 0.45f : x0 + 0.45f, T - faceBottom + 0.02f);
            return c;
        }

        static SdfCanvas BuildCapital(PlatformArt art)
        {
            var pl = art.P;
            float T = pl.Y, x0 = pl.X0, x1 = pl.X1, seed = pl.Seed, cx = pl.Center;
            const float abacusBottom = 0.36f, echinusBottom = 0.72f;
            var c = new SdfCanvas(BodyRect(pl), 110f);
            var r = new System.Random(pl.Seed);
            float R() => (float)r.NextDouble();
            float hwTop = pl.Width * 0.44f, hwBottom = 0.45f;

            // echinus: a cushion that swells out under the slab, then three annulets
            c.Fill(q =>
            {
                float t = Mathf.Clamp01(((T - abacusBottom) - q.y) / (echinusBottom - abacusBottom));
                float hw = hwBottom + (hwTop - hwBottom) * Mathf.Sqrt(Mathf.Max(0f, 1f - t * t));
                return Mathf.Max(Mathf.Abs(q.x - cx) - hw, Mathf.Max((T - echinusBottom) - q.y, q.y - (T - abacusBottom + 0.02f)));
            }, q =>
            {
                float t = Mathf.Clamp01(((T - abacusBottom) - q.y) / (echinusBottom - abacusBottom));
                float sx = Mathf.Clamp((q.x - cx) / hwTop, -1f, 1f);
                return Mul(FaceStone(q, 0.45f + 0.35f * sx - 0.35f * t), 0.92f + 0.12f * Noise.Perlin(q.x * 11f, q.y * 11f));
            }, 0f, new Rect(cx - hwTop - 0.1f, T - echinusBottom - 0.1f, hwTop * 2f + 0.2f, 0.5f));
            for (int k = 0; k < 3; k++)
            {
                float y = T - echinusBottom - 0.025f - k * 0.05f;
                c.Fill(q => Sdf.Box(q, new Vector2(cx, y), new Vector2(0.46f - k * 0.01f, 0.018f), 0.01f),
                    q => FaceStone(q, 0.35f + 0.3f * Mathf.Clamp((q.x - cx) / 0.45f, -1f, 1f)), 0f, new Rect(cx - 0.6f, y - 0.1f, 1.2f, 0.2f));
            }

            // abacus: the heavy square slab that is the walkable surface
            SdfCanvas.SdfFn slab = q =>
            {
                float n = 0.06f * Noise.Perlin(q.y * 12f, seed) + 0.04f * Noise.Perlin(q.y * 31f, seed + 2f);
                float ends = Mathf.Max(x0 - 0.08f + n - q.x, q.x - (x1 + 0.08f - n));
                return Mathf.Max(ends, Mathf.Max(T - abacusBottom - q.y, q.y - (T + TopBack)));
            };
            c.Fill(slab, q =>
            {
                if (q.y > T - TopFront) return TopColor(q, T, true, seed);
                float f = (T - TopFront) - q.y;
                float lit = 0.35f + 0.3f * S01((q.x - x0) / pl.Width);
                Color col = FaceStone(q, f < 0.04f ? lit + 0.35f : f > abacusBottom - TopFront - 0.04f ? lit - 0.4f : lit);
                if (Mathf.Abs(f - 0.11f) < 0.01f) col = Mul(col, 0.7f);
                col = Mul(col, 0.9f + 0.18f * Noise.Perlin(q.x * 13f, q.y * 13f));
                if (Crack(q, seed)) col = Mul(col, 0.62f);
                col.a = 1f;
                return col;
            }, 0f, new Rect(x0 - 0.3f, T - abacusBottom - 0.1f, pl.Width + 0.6f, abacusBottom + 0.35f));
            for (float x = x0 + 0.1f; x < x1 - 0.1f; x += 0.3f + R() * 0.5f)
            {
                if (R() < 0.4f) continue;
                Vector2 mc = new Vector2(x, T - TopFront - 0.025f);
                Vector2 mr = new Vector2(0.1f + R() * 0.16f, 0.035f + R() * 0.04f);
                c.Paint(q => Sdf.Ellipse(q, mc, mr) + 0.02f * Noise.Perlin(q.x * 40f, q.y * 40f), Palette.Moss.WithAlpha(0.8f), 0.01f, new Rect(mc.x - 0.4f, mc.y - 0.2f, 0.8f, 0.4f));
            }
            Fringe(c, x0 - 0.05f, x1 + 0.05f, T - TopFront, r, 0.7f);
            c.RimLight(new Vector2(0.03f, 0.03f), new Color(0.62f, 0.9f, 0.9f), 0.45f);

            art.Hangs.Add(new Vector2(x0 + 0.12f + R() * 0.15f, T - abacusBottom + 0.03f));
            art.Hangs.Add(new Vector2(x1 - 0.12f - R() * 0.15f, T - abacusBottom + 0.03f));
            art.Hangs.Add(new Vector2(cx + (R() - 0.5f) * 0.5f, T - echinusBottom - 0.12f));
            if (MathUtil.Hash(pl.Seed) > -0.2f) art.Lantern = new Vector2(pl.Center < 0f ? x0 + 0.22f : x1 - 0.22f, T - abacusBottom + 0.02f);
            return c;
        }

        static SdfCanvas BuildRock(PlatformArt art)
        {
            var pl = art.P;
            float T = pl.Y, x0 = pl.X0, x1 = pl.X1, seed = pl.Seed;
            Rect rect = BodyRect(pl);
            var c = new SdfCanvas(rect, 110f);
            int W = c.Width;
            float left = rect.xMin, ppu = c.Ppu;
            var r = new System.Random(pl.Seed);
            float R() => (float)r.NextDouble();

            // underside: a lumpy inverted cone with a few hanging lobes
            var bottom = new float[W];
            float depth = 0.95f + 0.3f * Hash01(pl.Seed);
            float lobePhase = R() * 6f;
            for (int x = 0; x < W; x++)
            {
                float ux = left + (x + 0.5f) / ppu;
                float u = (ux - (x0 - 0.05f)) / (pl.Width + 0.1f);
                if (u <= 0f || u >= 1f) { bottom[x] = T + 1f; continue; }
                float shape = Mathf.Pow(Mathf.Sin(u * Mathf.PI), 0.7f);
                float lobes = 0.3f * Mathf.Pow(Mathf.Abs(Mathf.Sin(u * Mathf.PI * 2.5f + lobePhase)), 3f);
                bottom[x] = T - 0.2f - depth * shape * (0.72f + lobes) - 0.12f * Fbm(ux * 2.5f, seed) * shape;
            }

            c.Field(p =>
            {
                int x = Mathf.Clamp((int)((p.x - left) * ppu), 0, W - 1);
                float n = 0.06f * Noise.Perlin(p.y * 8f, seed);
                float side = Mathf.Max(x0 - 0.05f + n - p.x, p.x - (x1 + 0.05f - n));
                float d = Mathf.Max(side, Mathf.Max(bottom[x] - p.y, p.y - (T + TopBack)));
                float a = Mathf.Clamp01(0.5f - d * ppu);
                if (a <= 0f) return Clear;
                Color col;
                if (p.y > T - TopFront) col = TopColor(p, T, false, seed);
                else
                {
                    float below = (T - TopFront) - p.y;
                    // layered sediment, a touch warmer than the teal ruins so the rock separates from them
                    float strata = 0.5f + 0.5f * Mathf.Sin(p.y * 22f + Noise.Perlin(p.x * 1.6f, seed) * 5f);
                    float grain = Noise.Perlin(p.x * 6f, p.y * 6f);
                    Color rock = Color.Lerp(new Color(0.15f, 0.19f, 0.2f), new Color(0.31f, 0.36f, 0.34f), strata * 0.55f + 0.3f * grain);
                    rock = Mul(rock, 0.78f + 0.4f * Mathf.Clamp01((p.x - x0) / pl.Width));
                    rock = Mul(rock, 1f - 0.6f * S01((below - 0.2f) / 0.85f));
                    col = Color.Lerp(new Color(0.1f, 0.17f, 0.17f), rock, S01((below - 0.07f) / 0.14f));
                    if (Crack(p, seed)) col = Mul(col, 0.55f);
                    float moss = S01((Noise.Perlin(p.x * 2.4f, p.y * 2.4f + seed) - 0.5f) / 0.12f) * S01((0.5f - below) / 0.3f);
                    col = Color.Lerp(col, new Color(0.17f, 0.42f, 0.35f), moss * 0.7f);
                }
                col.a = a;
                return col;
            });

            // crystals growing out of the underside — the glow that keeps the rock aloft
            float[] spots = { 0.3f + R() * 0.1f, 0.52f + R() * 0.08f, 0.7f + R() * 0.1f };
            foreach (float u in spots)
            {
                float sx = x0 + u * pl.Width;
                float sy = bottom[Col(c, sx)] + 0.08f;
                for (int k = 0; k < 3; k++)
                {
                    float ang = -90f + (k - 1) * 28f + (R() - 0.5f) * 14f;
                    float len = 0.12f + R() * 0.12f;
                    Vector2 a = new Vector2(sx + (k - 1) * 0.04f, sy + 0.03f), b = a + MathUtil.Dir(ang) * len;
                    c.Fill(q => Sdf.Tapered(q, a, 0.035f, b, 0.004f), q => Color.Lerp(new Color(0.45f, 0.9f, 1f), new Color(0.85f, 1f, 1f), S01(((q - a).magnitude) / len)).WithAlpha(1f),
                        0f, Around(a, b, 0.06f));
                }
                art.Crystals.Add(new Vector2(sx, sy - 0.08f));
            }
            Fringe(c, x0 - 0.05f, x1 + 0.05f, T - TopFront, r, 1f);
            c.RimLight(new Vector2(0.03f, 0.03f), new Color(0.55f, 0.85f, 0.82f), 0.5f);

            for (float x = x0 + 0.2f; x < x1 - 0.2f; x += 0.22f + R() * 0.4f) art.Hangs.Add(new Vector2(x, bottom[Col(c, x)] + 0.06f));
            return c;
        }

        static SdfCanvas BuildSupport(PlatformArt art)
        {
            var pl = art.P;
            float T = pl.Y;
            bool terrace = pl.Kind == Level.Style.Terrace;
            float top = terrace ? T - 0.55f : T - 0.78f;
            const float baseY = 0.22f;          // standing on the far edge of the pitch
            var c = new SdfCanvas(SupportRect(pl), 90f);
            Color dark = new Color(0.14f, 0.23f, 0.28f), lightC = new Color(0.25f, 0.37f, 0.42f);
            SdfCanvas.ColorFn shade = q =>
            {
                Color col = EnvironmentArt.BrickColor(q, 0.24f, 0.5f, pl.Seed);
                col = Color.Lerp(Mul(col, 0.62f), dark, 0.35f);
                col = Mul(col, 1f - 0.35f * S01((q.y - (top - 0.6f)) / 0.6f));   // shade under the slab
                col.a = 1f;
                return col;
            };

            if (terrace)
            {
                // a loggia: three square piers carrying low arches
                float[] piers = { pl.X0 + 0.55f, pl.Center, pl.X1 - 0.55f };
                SdfCanvas.SdfFn loggia = q =>
                {
                    float d = 9f;
                    foreach (float px in piers)
                    {
                        d = Mathf.Min(d, Sdf.Box(q, new Vector2(px, (baseY + top) * 0.5f), new Vector2(0.19f, (top - baseY) * 0.5f)));
                        d = Mathf.Min(d, Sdf.Box(q, new Vector2(px, baseY + 0.1f), new Vector2(0.26f, 0.1f)));
                    }
                    float band = Mathf.Max(Mathf.Abs(q.x - pl.Center) - (pl.Width * 0.5f - 0.4f), Mathf.Max(top - 0.42f - q.y, q.y - top - 0.05f));
                    float span = (piers[1] - piers[0]) * 0.5f;
                    float ac = q.x < pl.Center ? (piers[0] + piers[1]) * 0.5f : (piers[1] + piers[2]) * 0.5f;
                    band = Mathf.Max(band, -Sdf.Ellipse(q, new Vector2(ac, top - 0.42f), new Vector2(span - 0.17f, 0.28f)));
                    return Mathf.Min(d, band);
                };
                c.Fill(loggia, shade);
            }
            else
            {
                // one fluted column shaft under the capital
                c.Fill(q => Mathf.Max(Mathf.Abs(q.x - pl.Center) - (0.4f - 0.03f * (q.y - baseY) / (top - baseY)), Mathf.Max(baseY + 0.3f - q.y, q.y - top - 0.05f)), q =>
                {
                    float sx = Mathf.Clamp((q.x - pl.Center) / 0.4f, -1f, 1f);
                    float flute = 0.5f + 0.5f * Mathf.Cos((q.x - pl.Center) * MathUtil.Tau / 0.11f);
                    Color col = Color.Lerp(dark, lightC, 0.35f + 0.45f * sx) * (0.9f + 0.12f * flute);
                    if (Mathf.Abs(Mathf.Repeat(q.y, 0.66f) - 0.33f) > 0.318f) col = Mul(col, 0.72f);
                    col = Mul(col, 1f - 0.3f * S01((q.y - (top - 0.5f)) / 0.5f));
                    col.a = 1f;
                    return col;
                });
                c.Fill(q => Sdf.Box(q, new Vector2(pl.Center, baseY + 0.15f), new Vector2(0.55f, 0.15f), 0.02f), q => Color.Lerp(dark, lightC, 0.5f + 0.3f * Mathf.Clamp((q.x - pl.Center) / 0.5f, -1f, 1f)).WithAlpha(1f));
            }
            c.RimLight(new Vector2(0.03f, 0.02f), new Color(0.3f, 0.52f, 0.58f), 0.4f);
            c.EdgeBand(Vector2.up, 0.06f, Mul(Palette.Moss, 0.7f).WithAlpha(1f), p => S01((Noise.Perlin(p.x * 2.4f, p.y * 2.4f) - 0.35f) / 0.2f));
            return c;
        }

        // ---------------------------------------------------------------- pitch markings, puddles, props

        /// <summary>
        /// Chalk lines drawn with the same oblique projection as the mowing stripes: halfway line and
        /// centre circle (side = 0), or penalty area, goal area, spot and arc (side = ±1), with the
        /// turf worn to mud in front of the goal.
        /// </summary>
        static SdfCanvas BuildMarkings(float side)
        {
            float cx = side == 0f ? 0f : side * MarkBoxCenter;
            float halfW = side == 0f ? 3.5f : 3.4f;
            var c = new SdfCanvas(new Rect(cx - halfW, -0.5f, halfW * 2f, 0.9f), 100f);
            float ppu = c.Ppu;
            const float lw = 0.028f, dl = 0.59f;
            c.Field(p =>
            {
                float yy = (p.y + 0.46f) / 0.82f;
                float D = (yy - 0.28f) / 0.72f;
                float X = p.x + (yy - 0.5f) * 0.35f;
                if (D < -0.03f || D > 1.03f) return Clear;
                float dy = (D - 0.5f) * dl;
                float d = 9f;
                Color col = Clear;
                if (side == 0f)
                {
                    d = Mathf.Min(d, Mathf.Abs(X) - lw);
                    d = Mathf.Min(d, Mathf.Abs(Sdf.Ellipse(new Vector2(X, dy), Vector2.zero, new Vector2(2.95f, 0.135f * dl))) - 0.013f);
                    d = Mathf.Min(d, Sdf.Ellipse(new Vector2(X, dy), Vector2.zero, new Vector2(0.06f, 0.02f)));
                }
                else
                {
                    float ax = X * side;
                    // worn mud in the goal mouth and around the penalty spot
                    float mud = S01((0.2f - Mathf.Abs(D - 0.5f)) / 0.12f) * S01((ax - 15.1f) / 0.6f) * S01((17.2f - ax) / 0.3f);
                    mud = Mathf.Max(mud, S01(1f - new Vector2((ax - 13.36f) / 0.45f, dy / 0.05f).magnitude));
                    mud *= S01((Noise.Perlin(p.x * 5f, p.y * 14f) - 0.25f) / 0.3f);
                    col = new Color(0.16f, 0.19f, 0.15f, mud * 0.75f);
                    d = Mathf.Min(d, Mathf.Abs(ax - 16.9f) - lw);
                    float inBox = Mathf.Max(11.6f - ax, ax - 16.9f);
                    d = Mathf.Min(d, Mathf.Max(Mathf.Abs(ax - 11.6f) - lw, Mathf.Abs(D - 0.5f) * dl - 0.296f * dl));
                    d = Mathf.Min(d, Mathf.Max(Mathf.Abs(Mathf.Abs(D - 0.5f) - 0.296f) * dl - 0.012f, inBox));
                    float inGoal = Mathf.Max(15.13f - ax, ax - 16.9f);
                    d = Mathf.Min(d, Mathf.Max(Mathf.Abs(ax - 15.13f) - lw, Mathf.Abs(D - 0.5f) * dl - 0.135f * dl));
                    d = Mathf.Min(d, Mathf.Max(Mathf.Abs(Mathf.Abs(D - 0.5f) - 0.135f) * dl - 0.012f, inGoal));
                    d = Mathf.Min(d, Sdf.Ellipse(new Vector2(ax - 13.36f, dy), Vector2.zero, new Vector2(0.06f, 0.02f)));
                    float arc = Mathf.Abs(Sdf.Ellipse(new Vector2(ax - 13.36f, dy), Vector2.zero, new Vector2(2.95f, 0.135f * dl))) - 0.013f;
                    d = Mathf.Min(d, Mathf.Max(arc, ax - 11.6f));
                }
                float chalk = Mathf.Clamp01(0.5f - d * ppu) * (0.5f + 0.5f * Noise.Perlin(p.x * 9f, p.y * 30f)) * 0.75f;
                Color white = new Color(0.9f, 1f, 0.96f);
                float outA = chalk + col.a * (1f - chalk);
                if (outA <= 0.001f) return Clear;
                Color o = (white * chalk + col * col.a * (1f - chalk)) / outA;
                o.a = outA;
                return o;
            });
            return c;
        }

        static SdfCanvas BuildPuddle()
        {
            var c = new SdfCanvas(new Rect(-0.85f, -0.13f, 1.7f, 0.26f), 120f);
            SdfCanvas.SdfFn outer = p => Sdf.Ellipse(p, Vector2.zero, new Vector2(0.78f, 0.095f)) + 0.012f * Noise.Perlin(p.x * 9f, 1.3f);
            SdfCanvas.SdfFn water = p => Sdf.Ellipse(p, Vector2.zero, new Vector2(0.7f, 0.075f)) + 0.012f * Noise.Perlin(p.x * 9f, 1.3f);
            c.Fill(outer, new Color(0.12f, 0.22f, 0.18f, 0.45f), 0.02f);
            c.Fill(water, p =>
            {
                // the water mirrors the night sky: lighter towards the far edge, dark at the near edge
                float v = Mathf.Clamp01(p.y / 0.15f + 0.5f);
                Color col = Color.Lerp(new Color(0.11f, 0.25f, 0.3f), new Color(0.24f, 0.44f, 0.5f), v);
                col = Color.Lerp(col, new Color(0.5f, 0.74f, 0.78f), S01((v - 0.82f) / 0.18f) * 0.6f);
                // the moon's reflection: a soft streak of light
                col = Color.Lerp(col, new Color(0.7f, 0.92f, 0.98f), Mathf.Exp(-Sq((p.x - 0.18f) / 0.16f) - Sq((p.y + 0.005f) / 0.022f)) * 0.8f);
                col.a = 1f;
                return col;
            }, 0.004f);
            return c;
        }

        static SdfCanvas BuildStrip()
        {
            var c = new SdfCanvas(new Rect(-0.5f, -3f, 1f, 4f), 16f);
            c.Field(p => new Color(1f, 1f, 1f, S01((0.9f - p.y) / 0.9f)));
            return c;
        }

        static SdfCanvas BuildPebble(int k)
        {
            var c = new SdfCanvas(new Rect(-0.16f, -0.13f, 0.32f, 0.26f), 160f);
            float seed = k * 7.3f;
            c.Fill(p => Sdf.Ellipse(p, Vector2.zero, new Vector2(0.12f - k * 0.02f, 0.085f - k * 0.01f)) + 0.02f * Noise.Perlin(p.x * 20f + seed, p.y * 20f),
                p => Color.Lerp(new Color(0.2f, 0.29f, 0.32f), new Color(0.42f, 0.55f, 0.58f), S01((p.x + p.y + 0.1f) / 0.25f)).WithAlpha(1f));
            c.Paint(p => Sdf.Ellipse(p, new Vector2(-0.02f, 0.05f), new Vector2(0.08f, 0.03f)), Palette.Moss.WithAlpha(0.7f), 0.02f);
            return c;
        }
    }
}
