using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Stage 1 „Mondlicht-Ruinen“, nachgebaut nach dem Referenzbild: tiefblauer Nachthimmel mit großem
    /// Vollmond und Wolkenbank, blaue Berge, Klippen mit Aquädukt, Turmruinen, Wasserfall und leuchtenden
    /// Kristallen, ein Waldtal mit Fluss und Steinbrücke, dunkle Baumgruppen mit Säulenstümpfen, große
    /// Rahmenbäume, eine hellgrüne Wiese mit ausgetretenen Stellen über einer Quadermauer, Fackelruinen
    /// mit Banner und eigene, sattgrüne Pflanzen. Jede Ebene ist ein eigenes Bild in hoher Auflösung, damit
    /// WorldEnvironment sie mit eigener Parallaxe bewegen kann. Alles hier ist reine Mathematik und läuft
    /// auf Hintergrund-Threads (siehe ArtJobs).
    /// </summary>
    public static class Stage1Art
    {
        // ---------------------------------------------------------------- Aufbau (teilt WorldEnvironment.Stage1)

        public static readonly Vector2 MoonPos = new Vector2(-5.2f, 6.7f);
        public const float MoonR = 0.74f;
        // jede Ebene in Weltkoordinaten bei Kamera-Mitte gezeichnet
        public static readonly Rect MountainRect = new Rect(-12.5f, 1.5f, 25f, 6.4f);
        public static readonly Rect CliffRect = new Rect(-13.5f, 0.8f, 27f, 7.3f);
        public static readonly Rect ValleyRect = new Rect(-15.5f, -0.6f, 31f, 5.4f);
        public static readonly Rect NearRect = new Rect(-17.5f, -1.2f, 35f, 5.2f);
        public const float PitchTileW = 13f, WallTileW = 8f;

        public static Sprite Sky, Moon, Mountains, Cliffs, Valley, Near, Pitch, Wall, Ruin, Flame;
        public static Sprite[] Clouds;
        public static Texture2D PlantAtlas;
        public static FoliageArt.Variant[] Grass, TallGrass, Ferns, Flowers, Vines, Bushes, FgLeaves, Trees;
        public static FoliageArt.Variant Banner;

        /// <summary>Leuchtkristalle je Ebene: x, y, Größe des Scheins.</summary>
        public static readonly List<Vector3> CliffCrystals = new List<Vector3>();
        public static readonly List<Vector3> ValleyCrystals = new List<Vector3>();
        public static readonly List<Vector3> NearCrystals = new List<Vector3>();
        /// <summary>Stellen auf dem Fluss, an denen Mondlicht glitzert.</summary>
        public static readonly List<Vector2> RiverGlints = new List<Vector2>();
        public static Vector2 FallTop, CascadeTop;
        public static float FallLength, CascadeLength;
        /// <summary>In der Ruine: Fuß der Fackelflamme und Aufhängung des Banners.</summary>
        public static readonly Vector2 TorchSpot = new Vector2(-0.66f, 1.2f);
        public static readonly Vector2 BannerSpot = new Vector2(-0.32f, 2.66f);

        static readonly Vector2 L = new Vector2(-0.55f, 0.83f).normalized;   // Mondlicht von links oben
        static readonly Color Clear = new Color(0f, 0f, 0f, 0f);

        static ArtJobs jobs;
        static ArtJobs.Job jSky, jMoon, jMountains, jCliffs, jValley, jNear, jPitch, jWall, jRuin, jFlame;
        static ArtJobs.Job[] jClouds;
        static AtlasBuilder plants;

        // ---------------------------------------------------------------- Ablauf

        /// <summary>Startet alle Stage-1-Bilder auf Hintergrund-Threads.</summary>
        public static void Begin()
        {
            CliffCrystals.Clear(); ValleyCrystals.Clear(); NearCrystals.Clear(); RiverGlints.Clear();
            jobs = new ArtJobs();
            jSky = jobs.Add("S1 Sky", BuildSky, Vector2.zero);
            jMoon = jobs.Add("S1 Moon", BuildMoon, Vector2.zero);
            jClouds = new ArtJobs.Job[3];
            for (int i = 0; i < jClouds.Length; i++) { int k = i; jClouds[i] = jobs.Add("S1 Cloud" + k, () => BuildCloud(k), Vector2.zero); }
            jMountains = jobs.Add("S1 Mountains", BuildMountains, Vector2.zero);
            jCliffs = jobs.Add("S1 Cliffs", BuildCliffs, Vector2.zero);
            jValley = jobs.Add("S1 Valley", BuildValley, Vector2.zero);
            jNear = jobs.Add("S1 Near", BuildNear, Vector2.zero);
            jPitch = jobs.Add("S1 Pitch", BuildPitch, Vector2.zero, true, TextureWrapMode.Repeat);
            jWall = jobs.Add("S1 Wall", BuildWall, Vector2.zero, true, TextureWrapMode.Repeat);
            jRuin = jobs.Add("S1 Ruin", BuildRuin, Vector2.zero, false);
            jFlame = jobs.Add("S1 Flame", BuildFlame, Vector2.zero, false);
            jobs.Start();

            plants = new AtlasBuilder();
            Grass = Many("s1grass", 5, FoliageArt.Mode.Rooted, 1f, i => GrassTuft(2101 + i * 17, 0.26f + i * 0.07f, 10 + i));
            TallGrass = Many("s1tall", 2, FoliageArt.Mode.Rooted, 1.1f, i => GrassTuft(2201 + i * 13, 0.7f + i * 0.25f, 8, true));
            Ferns = Many("s1fern", 2, FoliageArt.Mode.Rooted, 0.8f, i => Fern(2301 + i * 7, 0.6f + i * 0.2f));
            Flowers = Many("s1flower", 2, FoliageArt.Mode.Rooted, 1f, i => FlowerTuft(2401 + i * 19, i));
            Vines = Many("s1vine", 4, FoliageArt.Mode.Hanging, 0.8f, i => Vine(2501 + i * 31, 0.55f + i * 0.35f));
            Bushes = Many("s1bush", 3, FoliageArt.Mode.Rooted, 0.3f, i => Bush(2601 + i * 29, 1.2f + i * 0.3f));
            FgLeaves = Many("s1fg", 3, FoliageArt.Mode.Rooted, 0.45f, i => FrontLeaves(2701 + i * 43));
            Trees = Many("s1tree", 2, FoliageArt.Mode.Rooted, 0.09f, i => FrameTree(2801 + i * 71, i));
            Banner = Many("s1banner", 1, FoliageArt.Mode.Hanging, 1.2f, i => BannerCloth())[0];
            plants.Start(2048);
        }

        /// <summary>Wartet auf die Threads und lädt die Texturen hoch (Hauptthread).</summary>
        public static void End()
        {
            jobs.Complete();
            plants.Complete("Stage1Plants");
            PlantAtlas = plants.Texture;
            Sky = jSky.Sprite; Moon = jMoon.Sprite; Mountains = jMountains.Sprite; Cliffs = jCliffs.Sprite;
            Valley = jValley.Sprite; Near = jNear.Sprite; Pitch = jPitch.Sprite; Wall = jWall.Sprite;
            Ruin = jRuin.Sprite; Flame = jFlame.Sprite;
            Clouds = new Sprite[jClouds.Length];
            for (int i = 0; i < Clouds.Length; i++) Clouds[i] = jClouds[i].Sprite;
            Debug.Log("[SoccerFight] stage 1 art: " + jobs.Slowest(6));
            jobs = null;
            plants = null;
        }

        static FoliageArt.Variant[] Many(string name, int count, FoliageArt.Mode mode, float sway, System.Func<int, SdfCanvas> build)
        {
            var arr = new FoliageArt.Variant[count];
            for (int i = 0; i < count; i++)
            {
                int k = i;
                var v = new FoliageArt.Variant { Name = name + k, Mode = mode, Sway = sway };
                v.Entry = plants.Add(v.Name, () => build(k));
                arr[i] = v;
            }
            return arr;
        }

        // ---------------------------------------------------------------- Helfer (threadsicher)

        static Color C(int r, int g, int b, float a = 1f) => new Color(r / 255f, g / 255f, b / 255f, a);
        static Color Mul(Color c, float f) { c.r *= f; c.g *= f; c.b *= f; return c; }
        static float S01(float v) => MathUtil.Smooth01(v);
        static float Hash01(int n) => MathUtil.Hash(n) * 0.5f + 0.5f;
        static float Fbm(float x, float seed, int oct = 4) => EnvironmentArt.Fbm(x, seed, oct);
        static float Fbm2(Vector2 p, float seed, int oct = 4) => EnvironmentArt.Fbm2(p, seed, oct);
        static float Ridge(float x, float seed) => 1f - Mathf.Abs(EnvironmentArt.Fbm(x, seed, 4));
        static float G(float x, float c, float w) { float d = (x - c) / w; return Mathf.Exp(-d * d); }
        static float Window(float x, float a, float b, float soft) => S01((x - a) / soft) * S01((b - x) / soft);
        static float PX(float x, float y, float period) => EnvironmentArt.PeriodicX(x, y, period);
        static int Col(SdfCanvas c, float x) => Mathf.Clamp((int)((x - c.UnitRect.xMin) * c.Ppu), 0, c.Width - 1);
        static Rect Around(Vector2 a, Vector2 b, float pad) =>
            Rect.MinMaxRect(Mathf.Min(a.x, b.x) - pad, Mathf.Min(a.y, b.y) - pad, Mathf.Max(a.x, b.x) + pad, Mathf.Max(a.y, b.y) + pad);

        /// <summary>
        /// Ein Blätterbüschel wie im Bild gemalt: gewellter Rand, zur Mondseite hell, zur Rückseite
        /// dunkel, mit kleinen Blatt-Tupfern und einer Lichtkante oben links.
        /// </summary>
        static void Clump(SdfCanvas c, Vector2 ctr, float r, Color dark, Color lit, Color rim, float seed, float alpha = 1f)
        {
            float amp = r * 0.13f, pad = r + amp + 0.04f;
            float freq = 2.6f / r;
            float lobes = 5f + Mathf.Floor(Hash01((int)(seed * 13f)) * 3f);
            c.Fill(p =>
            {
                Vector2 d = p - ctr;
                float ang = Mathf.Atan2(d.y, d.x);
                float bump = Mathf.Abs(Mathf.Sin(ang * lobes * 0.5f + seed));
                return d.magnitude - r * (0.88f + 0.12f * bump) - amp * (Noise.Perlin(p.x * freq + seed, p.y * freq) - 0.5f) * 2f;
            }, p =>
            {
                Vector2 n = (p - ctr) / r;
                float l = Vector2.Dot(n, L);
                float leaf = Noise.Perlin(p.x * freq * 2.3f + seed * 3f, p.y * freq * 2.3f);
                float k = S01(0.42f + l * 0.6f + (leaf - 0.5f) * 0.55f);
                Color col = Color.Lerp(dark, lit, k);
                if (l > -0.3f)
                {
                    float dab = Noise.Perlin(p.x * freq * 5.5f, p.y * freq * 5.5f + seed);
                    col = Color.Lerp(col, lit, S01((dab - 0.66f) / 0.08f) * S01(l + 0.3f) * 0.45f);
                }
                float edge = S01((n.magnitude - 0.68f) / 0.3f) * S01(l * 1.5f);
                col = Color.Lerp(col, rim, edge * 0.6f);
                col.a = alpha;
                return col;
            }, 0f, new Rect(ctr.x - pad, ctr.y - pad, pad * 2f, pad * 2f));
        }

        /// <summary>Eine Baumkrone aus vielen Büscheln: erst die dunkle Masse, dann Büschel von unten nach oben.</summary>
        static void Crown(SdfCanvas c, Vector2 ctr, Vector2 radii, int count, System.Random r, Color dark, Color lit, Color rim, float alpha = 1f)
        {
            float R() => (float)r.NextDouble();
            var list = new List<Vector3>();
            for (int i = 0; i < count; i++)
            {
                float a = R() * MathUtil.Tau, d = Mathf.Sqrt(R());
                Vector2 p = ctr + new Vector2(Mathf.Cos(a) * radii.x, Mathf.Sin(a) * radii.y) * d * 0.8f;
                float rr = Mathf.Min(radii.x, radii.y) * (0.32f + R() * 0.25f);
                list.Add(new Vector3(p.x, p.y, rr));
            }
            list.Sort((a, b) => a.y.CompareTo(b.y));
            foreach (var b in list) Clump(c, new Vector2(b.x, b.y), b.z * 1.05f, Mul(dark, 0.8f), dark, dark, R() * 10f, alpha);
            foreach (var b in list) Clump(c, new Vector2(b.x, b.y), b.z, dark, lit, rim, R() * 10f, alpha);
        }

        /// <summary>Leuchtende Kristallsplitter (blau, mit heller Kante), wachsen aus Fels.</summary>
        static void CrystalCluster(SdfCanvas c, Vector2 root, float size, int seed)
        {
            var r = new System.Random(seed);
            float R() => (float)r.NextDouble();
            int n = 3 + r.Next(2);
            for (int i = 0; i < n; i++)
            {
                float ang = 90f + (i - (n - 1) * 0.5f) * 24f + (R() - 0.5f) * 16f;
                float len = size * (0.55f + R() * 0.6f) * (i == n / 2 ? 1.3f : 1f);
                float w = size * (0.13f + R() * 0.06f);
                Vector2 dir = MathUtil.Dir(ang), side = new Vector2(-dir.y, dir.x);
                Vector2 a = root + side * (i - (n - 1) * 0.5f) * w * 0.9f;
                Vector2 tip = a + dir * len, mid = a + dir * len * 0.62f;
                c.Fill(q => Mathf.Min(Sdf.Triangle(q, a - side * w * 0.8f, a + side * w * 0.8f, mid + side * w),
                        Mathf.Min(Sdf.Triangle(q, a - side * w * 0.8f, mid + side * w, mid - side * w), Sdf.Triangle(q, mid - side * w, mid + side * w, tip))),
                    q =>
                    {
                        float u = Vector2.Dot(q - a, side) / w;
                        float along = Mathf.Clamp01(Vector2.Dot(q - a, dir) / len);
                        Color col = u < -0.1f ? C(40, 96, 200) : C(92, 170, 250);
                        col = Color.Lerp(col, C(200, 240, 255), S01((along - 0.55f) / 0.45f) * 0.7f);
                        if (Mathf.Abs(u + 0.1f) < 0.14f) col = Color.Lerp(col, C(215, 245, 255), 0.6f);
                        return col;
                    }, 0f, Around(a, tip, w + 0.05f));
            }
        }

        /// <summary>Rauschen, das bei period > 0 in x nahtlos kachelt (für Wiese und Mauer).</summary>
        static float N(Vector2 p, float f, float seed, float period = 0f)
            => period > 0f ? PX(p.x * f, p.y * f + seed, period * f) : Noise.Perlin(p.x * f + seed * 1.7f, p.y * f + seed);

        /// <summary>
        /// Quadermauerwerk: Lage und Stein aus der Position; jeder Quader mit gerundeten Kanten, die sich
        /// zum Mond (oben links) aufhellen und nach unten rechts in den Schatten drehen, dazu Körnung,
        /// Abplatzer, seltene Risse und Moospolster auf den Oberkanten.
        /// </summary>
        static Color Masonry(Vector2 p, float x0, float top, float courseH, float blockW, int seed, Color lit, Color mid, Color dark,
            float period = 0f, float topMoss = 0f)
        {
            float fromTop = top - p.y;
            int k = Mathf.FloorToInt(fromTop / courseH);
            float v = fromTop - k * courseH;
            float w = blockW * (0.8f + 0.4f * Hash01(k * 7 + seed));
            int count = 0;
            if (period > 0f) { count = Mathf.Max(1, Mathf.RoundToInt(period / w)); w = period / count; }
            float bx = p.x - x0 + Hash01(k * 13 + seed * 3) * w * 3f;
            if (period > 0f) bx = Mathf.Repeat(bx, period);
            int j = Mathf.FloorToInt(bx / w);
            float u = bx - j * w;
            if (period > 0f) j = (int)Mathf.Repeat(j, count);
            const float joint = 0.018f, bevel = 0.075f;
            Vector2 hs = new Vector2(w * 0.5f, courseH * 0.5f) - new Vector2(joint, joint);
            Vector2 l = new Vector2(u - w * 0.5f, courseH * 0.5f - v);
            float d = Sdf.Box(l, Vector2.zero, hs, 0.05f) + 0.008f * (N(p, 30f, seed, period) - 0.5f) * 2f;
            Color jointCol = Color.Lerp(Mul(dark, 0.5f), Mul(dark, 0.72f), N(p, 12f, seed, period));
            if (d > 0.006f) return jointCol;
            float tone = Hash01(j * 31 + k * 17 + seed);
            Color col = Color.Lerp(Mul(mid, 0.84f), Mul(mid, 1.12f), tone);
            col = Mul(col, 0.82f + 0.16f * N(p, 2.5f, seed + j, period) + 0.12f * N(p, 9f, seed, period) + 0.08f * N(p, 27f, seed, period));
            // Kanten drehen sich zum Licht oder weg davon
            Vector2 inner = hs - new Vector2(bevel, bevel);
            Vector2 n = new Vector2(Mathf.Sign(l.x) * S01((Mathf.Abs(l.x) - inner.x) / bevel), Mathf.Sign(l.y) * S01((Mathf.Abs(l.y) - inner.y) / bevel));
            float facing = Mathf.Clamp(n.x * L.x + n.y * L.y * 1.2f, -1f, 1f);
            col = facing > 0f ? Color.Lerp(col, lit, facing * 0.8f) : Color.Lerp(col, dark, -facing * 0.75f);
            // Abplatzer und seltene Risse (nur in einzelnen verwitterten Flecken)
            if (N(p, 14f, seed + 7f, period) > 0.78f) col = Mul(col, 0.8f);
            if (Crack(p, seed, period)) col = Mul(col, 0.66f);
            // Moospolster wachsen auf den Oberkanten
            if (topMoss > 0f && N(new Vector2(p.x, k * 3.1f), 1.6f, seed + 5f, period) > 1f - (k == 0 ? topMoss : topMoss * 0.45f))
            {
                float thick = 0.015f + 0.06f * N(p, 7f, seed + 9f, period) + 0.04f * (N(p, 26f, seed + 4f, period) - 0.5f);
                if (hs.y - l.y < thick) col = MossColor(p, seed, period);
            }
            // weiche Kante zur Fuge (kein Treppeneffekt)
            return Color.Lerp(col, jointCol, Mathf.Clamp01(0.5f + d / 0.012f));
        }

        /// <summary>Seltene, lang gezogene Haarrisse, nur in einzelnen verwitterten Flecken.</summary>
        static bool Crack(Vector2 p, float seed, float period = 0f)
        {
            if (N(p, 0.9f, seed + 3f, period) < 0.66f) return false;
            float line = period > 0f ? PX(p.x * 5f, p.y * 1.6f + seed, period * 5f) : Noise.Perlin(p.x * 5f + seed, p.y * 1.6f);
            return Mathf.Abs(line - 0.5f) < 0.007f;
        }

        static Color MossColor(Vector2 p, float seed, float period = 0f)
        {
            float f = N(p, 22f, seed, period), g = N(p, 55f, seed + 2f, period);
            return Mul(Color.Lerp(C(46, 98, 36), C(114, 176, 64), f), 0.85f + 0.3f * g);
        }

        /// <summary>Moos in Flecken mit krümeligem, klarem Rand statt weicher Schlieren.</summary>
        static Color MossOver(Color col, Vector2 p, float amount, float seed, float period = 0f)
        {
            if (amount <= 0f) return col;
            float mask = N(p, 2.4f, seed, period) + (N(p, 16f, seed + 1f, period) - 0.5f) * 0.4f + (N(p, 45f, seed + 2f, period) - 0.5f) * 0.18f;
            float m = S01((mask - (0.78f - amount * 0.35f)) / 0.035f);
            return Color.Lerp(col, MossColor(p, seed, period), m * 0.95f);
        }
        // ---------------------------------------------------------------- Himmel, Mond, Wolken

        static SdfCanvas BuildSky()
        {
            var c = new SdfCanvas(new Rect(-0.125f, -2f, 0.25f, 14f), 24f);
            Color horizon = C(64, 102, 164), mid = C(30, 56, 116), top = C(9, 18, 50);
            c.Field(p =>
            {
                float y = p.y;
                Color col = y < 4.2f ? Color.Lerp(horizon, mid, S01((y - 1.5f) / 2.7f)) : Color.Lerp(mid, top, S01((y - 4.2f) / 5.2f));
                col.a = 1f;
                return col;
            });
            return c;
        }

        static SdfCanvas BuildMoon()
        {
            var c = new SdfCanvas(new Rect(-0.9f, -0.9f, 1.8f, 1.8f), 230f);
            var r = new System.Random(77);
            var craters = new List<Vector3>();
            for (int i = 0; i < 16; i++)
            {
                float a = (float)r.NextDouble() * MathUtil.Tau, d = Mathf.Sqrt((float)r.NextDouble()) * 0.6f;
                craters.Add(new Vector3(Mathf.Cos(a) * d, Mathf.Sin(a) * d, 0.035f + (float)r.NextDouble() * 0.07f));
            }
            c.Fill(p => p.magnitude - MoonR, p =>
            {
                Vector2 q = p / MoonR;
                float z = Mathf.Sqrt(Mathf.Max(0f, 1f - q.sqrMagnitude));
                float maria = S01((Fbm2(p * 2.2f, 7f) + 0.02f) / 0.22f);
                Color col = Color.Lerp(C(244, 248, 255), C(190, 206, 234), maria * 0.6f);
                foreach (var cr in craters)
                {
                    float d = (p - new Vector2(cr.x, cr.y)).magnitude / cr.z;
                    if (d > 1.25f) continue;
                    col = Mul(col, 1f - 0.07f * (1f - S01((d - 0.6f) / 0.4f)) + 0.06f * Mathf.Exp(-(d - 1f) * (d - 1f) / 0.02f));
                }
                col = Mul(col, 0.84f + 0.16f * z + 0.05f * Vector2.Dot(q, L));
                col.a = 1f;
                return col;
            });
            return c;
        }

        static SdfCanvas BuildCloud(int k)
        {
            var r = new System.Random(501 + k * 37);
            float R() => (float)r.NextDouble();
            float halfW = 3.1f - k * 0.55f;
            var c = new SdfCanvas(new Rect(-halfW - 0.8f, -0.45f, halfW * 2f + 1.6f, 2.7f), 60f);
            var puffs = new List<Vector3>();
            int n = 7 + k;
            for (int i = 0; i < n; i++)
            {
                float t = (i + 0.5f) / n;
                float rad = (0.3f + 0.5f * Mathf.Sin(t * Mathf.PI)) * (0.8f + R() * 0.45f) * (1f - k * 0.1f);
                puffs.Add(new Vector3(Mathf.Lerp(-halfW, halfW, t) + (R() - 0.5f) * 0.3f, rad * 0.55f + R() * 0.12f, rad));
            }
            for (int i = 0; i < n / 2; i++)
            {
                var b = puffs[1 + r.Next(n - 2)];
                float rad = b.z * (0.55f + R() * 0.3f);
                puffs.Add(new Vector3(b.x + (R() - 0.5f) * b.z, b.y + b.z * 0.65f, rad));
            }
            puffs.Sort((a, b) => a.y.CompareTo(b.y));
            Color shadow = C(40, 58, 106), body = C(64, 88, 142), lit = C(150, 172, 216);
            float seed = k * 5.3f, height = 0.1f;
            foreach (var b in puffs) height = Mathf.Max(height, b.y + b.z);
            foreach (var b in puffs)
            {
                Vector2 pc = new Vector2(b.x, b.y);
                float pr = b.z;
                c.Fill(q => Mathf.Max((q - pc).magnitude - pr + 0.06f * pr * (Noise.Perlin(q.x * 4f + seed, q.y * 4f) - 0.5f) * 2f, -q.y),
                    q =>
                    {
                        Vector2 nn = (q - pc) / pr;
                        float l = Vector2.Dot(nn, L);
                        float wisp = Noise.Perlin(q.x * 2.2f + seed, q.y * 3.5f);
                        Color col = Color.Lerp(shadow, body, S01(q.y / height * 1.3f + (wisp - 0.5f) * 0.4f));
                        col = Color.Lerp(col, lit, S01((l - 0.25f) / 0.5f) * S01((nn.magnitude - 0.62f) / 0.33f) * 0.8f);
                        col.a = 0.55f + 0.4f * S01(q.y / 0.45f);
                        return col;
                    }, 0.05f, new Rect(pc.x - pr - 0.12f, pc.y - pr - 0.12f, pr * 2f + 0.24f, pr * 2f + 0.24f));
            }
            return c;
        }

        // ---------------------------------------------------------------- Berge

        static SdfCanvas BuildMountains()
        {
            var c = new SdfCanvas(MountainRect, 64f);
            int W = c.Width;
            float left = c.UnitRect.xMin, ppu = c.Ppu, aa = 1f / ppu;
            var back = new float[W]; var front = new float[W];
            var sb = new float[W]; var sf = new float[W];
            for (int x = 0; x < W; x++)
            {
                float ux = left + (x + 0.5f) / ppu;
                back[x] = 4.6f + 0.7f * Fbm(ux * 0.11f, 21f) + 0.4f * Ridge(ux * 0.55f, 22f) + 2.1f * G(ux, -0.4f, 2.2f)
                          + 1.0f * G(ux, 5.9f, 2f) + 0.8f * G(ux, -8.4f, 2.2f) + 0.6f * G(ux, 10.6f, 1.6f);
                front[x] = 3.6f + 0.5f * Fbm(ux * 0.17f, 23f) + 0.3f * Ridge(ux * 0.9f, 24f) + 1.5f * G(ux, -3.4f, 1.5f)
                           + 1.2f * G(ux, 4.9f, 1.8f) + 0.9f * G(ux, -9.6f, 1.7f) + 0.8f * G(ux, 9.8f, 1.5f);
            }
            for (int x = 0; x < W; x++)
            {
                sb[x] = (back[Mathf.Min(W - 1, x + 2)] - back[Mathf.Max(0, x - 2)]) / (4f / ppu);
                sf[x] = (front[Mathf.Min(W - 1, x + 2)] - front[Mathf.Max(0, x - 2)]) / (4f / ppu);
            }
            Color backLit = C(92, 122, 178), backShadow = C(56, 82, 138);
            Color frontLit = C(76, 106, 164), frontShadow = C(42, 64, 116);
            Color haze = C(74, 106, 166);
            c.Field(p =>
            {
                int x = Mathf.Clamp((int)((p.x - left) * ppu), 0, W - 1);
                float aB = Mathf.Clamp01(0.5f + (back[x] - p.y) / aa);
                float aF = Mathf.Clamp01(0.5f + (front[x] - p.y) / aa);
                if (aB <= 0f && aF <= 0f) return Clear;
                Color b = RangeShade(p, back[x], sb[x], backLit, backShadow, 31f);
                b = Color.Lerp(b, haze, 0.3f);
                Color f = RangeShade(p, front[x], sf[x], frontLit, frontShadow, 37f);
                Color col = Color.Lerp(b, f, aF);
                col = Color.Lerp(col, haze, S01((3.9f - p.y) / 2.1f) * 0.6f);
                col.a = Mathf.Max(aB, aF);
                return col;
            });
            return c;
        }

        /// <summary>Bergflanke: zum Mond geneigte Hänge hell, Rinnen laufen schräg vom Grat herab.</summary>
        static Color RangeShade(Vector2 p, float top, float slope, Color lit, Color shadow, float seed)
        {
            float depth = top - p.y;
            float g = Noise.Perlin(p.x * 2.4f + depth * slope * 0.7f + seed, depth * 0.8f);
            float g2 = Noise.Perlin(p.x * 6.5f + depth * slope * 1.6f, depth * 1.8f + seed);
            float light = 0.5f + Mathf.Clamp(slope * 0.8f, -0.42f, 0.42f) + (g - 0.5f) * 0.95f + (g2 - 0.5f) * 0.35f;
            Color col = Color.Lerp(shadow, lit, S01(light));
            col = Color.Lerp(col, Mul(lit, 1.18f), S01(1f - depth / 0.06f) * S01(slope * 2f + 0.4f) * 0.55f);
            return col;
        }

        // ---------------------------------------------------------------- Klippen, Aquädukt, Türme, Wasserfall

        static SdfCanvas BuildCliffs()
        {
            var c = new SdfCanvas(CliffRect, 72f);
            var r = new System.Random(4711);
            float R() => (float)r.NextDouble();
            int W = c.Width;
            float left = c.UnitRect.xMin, ppu = c.Ppu, aa = 1f / ppu;
            var top = new float[W]; var slope = new float[W];
            for (int x = 0; x < W; x++)
            {
                float ux = left + (x + 0.5f) / ppu;
                top[x] = 4.2f + 0.45f * Fbm(ux * 0.3f, 41f) + 0.2f * Fbm(ux * 1.4f, 42f)
                         + 1.5f * Window(ux, 1.8f, 7.7f, 0.6f) + 0.6f * G(ux, -4.6f, 1.6f)
                         + 0.5f * G(ux, -10f, 2f) + 0.6f * G(ux, 10.6f, 1.8f)
                         - 0.55f * G(ux, -1.25f, 0.8f);
            }
            for (int x = 0; x < W; x++) slope[x] = (top[Mathf.Min(W - 1, x + 2)] - top[Mathf.Max(0, x - 2)]) / (4f / ppu);

            Color rockLit = C(72, 94, 142), rockMid = C(46, 64, 108), rockDark = C(30, 44, 82), haze = C(62, 92, 148);
            c.Field(p =>
            {
                int x = Mathf.Clamp((int)((p.x - left) * ppu), 0, W - 1);
                float a = Mathf.Clamp01(0.5f + (top[x] - p.y) / aa);
                if (a <= 0f) return Clear;
                float depth = top[x] - p.y;
                // gemalte Felsflächen: verzogenes Rauschen in Stufen, jede Stufe eine Fläche mit eigener Helligkeit
                float wx = p.x * 1.25f + Noise.Perlin(p.y * 0.8f, 3f) * 1.8f, wy = p.y * 0.5f + Noise.Perlin(p.x * 0.7f, 6f) * 0.8f;
                float facet = Noise.Perlin(wx, wy) * 4f;
                float step = Mathf.Floor(facet), frac = facet - step;
                float plane = (step + S01((frac - 0.7f) / 0.3f)) / 4f;
                float grain = Noise.Perlin(p.x * 8f, p.y * 2.2f + 2f);
                float light = 0.12f + plane * 1.0f + Mathf.Clamp(slope[x] * 0.4f, -0.25f, 0.25f) + (grain - 0.5f) * 0.22f;
                Color col = light < 0.5f ? Color.Lerp(rockDark, rockMid, S01(light * 2f)) : Color.Lerp(rockMid, rockLit, S01((light - 0.5f) * 2f));
                // senkrechte Spalten
                float crack = Mathf.Abs(Noise.Perlin(p.x * 4.2f + Noise.Perlin(p.y * 1.2f, 9f) * 0.7f, p.y * 0.35f + 4f) - 0.5f);
                if (crack < 0.012f) col = Color.Lerp(col, rockDark, 0.7f);
                col = Color.Lerp(col, Mul(rockLit, 1.12f), S01(1f - depth / 0.07f) * 0.5f);
                col = Color.Lerp(col, haze, S01((3.2f - p.y) / 2.2f) * 0.55f);
                col.a = a;
                return col;
            });

            // Wasserfall durch die Kerbe in der Mitte, dazu ein dünner Fall vom Aquädukt
            float fx = -1.25f;
            FallTop = new Vector2(fx, top[Col(c, fx)] + 0.02f);
            FallLength = FallTop.y - 0.95f;
            Water(c, FallTop, 0.46f, FallLength, 91f);
            CascadeTop = new Vector2(7.45f, 5.45f);
            CascadeLength = 2.1f;
            Water(c, CascadeTop, 0.12f, CascadeLength, 93f);

            // Wald am Fuß der Klippen und auf den Kanten
            Color fDark = C(22, 46, 72), fLit = C(46, 84, 112), fRim = C(96, 136, 178);
            for (float x = CliffRect.xMin + 0.2f; x < CliffRect.xMax - 0.2f; x += 0.28f + R() * 0.3f)
            {
                float rr = 0.3f + R() * 0.3f;
                Clump(c, new Vector2(x, 1.5f + R() * 1.1f + 0.4f * G(x, 6f, 3f)), rr, fDark, fLit, fRim, R() * 10f);
            }
            // Türme und Säulenreste vor und auf den Klippen
            (float x, float w, float b, float t)[] towers =
            {
                (1.25f, 0.2f, 3.3f, 7.3f), (1.9f, 0.15f, 3.9f, 6.7f), (-5.45f, 0.15f, 3.2f, 5.3f), (-5.0f, 0.11f, 3.2f, 4.9f),
                (-3.4f, 0.14f, 3.6f, 5.9f), (5.75f, 0.16f, 3.3f, 5.55f), (-9.8f, 0.17f, 3.4f, 6.4f), (10.4f, 0.18f, 3.6f, 6.9f),
                (-12.2f, 0.14f, 3.4f, 5.2f), (12.4f, 0.13f, 3.6f, 5.4f)
            };
            foreach (var t in towers) Tower(c, t.x, t.w, t.b, t.t, r.Next(1, 999));
            Aqueduct(c);
            // Bäume auf den Kanten und Absätzen
            for (float x = CliffRect.xMin + 0.2f; x < CliffRect.xMax - 0.2f; x += 0.35f + R() * 0.55f)
            {
                if (Mathf.Abs(x - fx) < 0.45f || R() < 0.25f) continue;
                float ty = top[Col(c, x)];
                if (x > 2.3f && x < 7.3f && ty > 5.3f) ty = 5.45f + R() * 0.2f;   // unter den Bögen, nicht darüber
                float rr = 0.18f + R() * 0.24f;
                Clump(c, new Vector2(x, ty - rr * 0.1f), rr, fDark, fLit, fRim, R() * 10f);
            }
            // Baumgruppen wachsen an den Hängen hoch: Haufen aus mehreren Kronen, unten dichter
            for (float x = CliffRect.xMin + 0.4f; x < CliffRect.xMax - 0.4f; x += 0.8f + R() * 0.9f)
            {
                if (Mathf.Abs(x - fx) < 0.7f) continue;
                float ty = top[Col(c, x)];
                float gy = Mathf.Lerp(2.5f, ty - 0.6f, Mathf.Pow(R(), 1.5f));
                int n = 4 + r.Next(4);
                var grove = new List<Vector3>();
                for (int k = 0; k < n; k++)
                {
                    float gx = x + (R() - 0.5f) * 0.9f;
                    float yy = Mathf.Min(gy + (R() - 0.3f) * 0.45f, top[Col(c, gx)] - 0.3f);
                    grove.Add(new Vector3(gx, yy, 0.18f + R() * 0.2f));
                }
                grove.Sort((a, b) => b.y.CompareTo(a.y));
                foreach (var g in grove) Clump(c, new Vector2(g.x, g.y), g.z, fDark, fLit, fRim, R() * 10f);
            }
            // Leuchtkristalle in den Felsen
            Vector2[] cry = { new Vector2(1.75f, 5.55f), new Vector2(6.2f, 4.35f), new Vector2(6.45f, 4.0f), new Vector2(-6.1f, 4.75f),
                new Vector2(-5.05f, 3.62f), new Vector2(9.2f, 4.55f), new Vector2(-9.0f, 4.9f), new Vector2(3.85f, 5.1f), new Vector2(-2.3f, 3.9f) };
            for (int i = 0; i < cry.Length; i++)
            {
                float s = 0.2f + R() * 0.14f;
                CrystalCluster(c, cry[i], s, 900 + i * 7);
                CliffCrystals.Add(new Vector3(cry[i].x, cry[i].y + s * 0.5f, s * 2.6f));
            }
            c.RimLight(new Vector2(-0.035f, 0.035f), C(150, 180, 226), 0.35f);
            return c;
        }

        static void Water(SdfCanvas c, Vector2 top, float width, float length, float seed)
        {
            c.Fill(q =>
            {
                float t = Mathf.Clamp01((top.y - q.y) / length);
                float hw = width * (0.5f + 0.25f * t);
                return Mathf.Max(Mathf.Abs(q.x - top.x - 0.02f * Mathf.Sin(q.y * 3f)) - hw, Mathf.Max(q.y - top.y, top.y - length - q.y));
            }, q =>
            {
                float t = Mathf.Clamp01((top.y - q.y) / length);
                float streak = Noise.Perlin((q.x - top.x) * 26f + seed, q.y * 1.6f);
                Color col = Color.Lerp(C(112, 156, 214), C(214, 234, 255), S01((streak - 0.35f) / 0.35f));
                col = Color.Lerp(col, C(230, 242, 255), S01(1f - t * 12f) * 0.6f);
                col.a = 0.85f;
                return col;
            }, 0f, new Rect(top.x - width, top.y - length - 0.05f, width * 2f, length + 0.1f));
        }

        static void Tower(SdfCanvas c, float x, float hw, float bottom, float top, int seed)
        {
            float s = seed;
            SdfCanvas.SdfFn shape = q =>
            {
                float body = Sdf.Box(q, new Vector2(x, (bottom + top) * 0.5f), new Vector2(hw, (top - bottom) * 0.5f));
                // Kapitell und Gesims
                body = Mathf.Min(body, Sdf.Box(q, new Vector2(x, top - 0.55f), new Vector2(hw * 1.35f, 0.05f)));
                // abgebrochene Krone
                float broken = q.y - (top - 0.25f * Mathf.Abs(Noise.Perlin(q.x * 9f + s, 1f) - 0.5f) * 4f * hw);
                return Mathf.Max(body, broken);
            };
            c.Fill(shape, q =>
            {
                float u = (q.x - x) / hw;
                float light = 0.55f - u * 0.4f;
                Color col = Color.Lerp(C(34, 48, 88), C(80, 100, 148), S01(light));
                if (Mathf.Repeat(q.y + Hash01(seed) * 0.3f, 0.26f) < 0.018f) col = Mul(col, 0.72f);
                col = Mul(col, 0.9f + 0.18f * Noise.Perlin(q.x * 14f, q.y * 14f + s));
                col = Color.Lerp(col, C(40, 72, 70), S01((Noise.Perlin(q.x * 3f + s, q.y * 2f) - 0.6f) / 0.1f) * 0.6f);
                col = Color.Lerp(col, C(62, 92, 148), S01((3.4f - q.y) / 1.8f) * 0.5f);
                return col;
            }, 0f, new Rect(x - hw * 1.5f, bottom - 0.05f, hw * 3f, top - bottom + 0.1f));
        }

        static void Aqueduct(SdfCanvas c)
        {
            const float x0 = 2.3f, x1 = 7.35f, deckTop = 7.52f, deckBottom = 7.12f, baseY = 5.2f;
            const float pierStep = 0.8f, spring = 6.5f, archR = 0.3f;
            SdfCanvas.SdfFn shape = q =>
            {
                float body = Sdf.Box(q, new Vector2((x0 + x1) * 0.5f, (baseY + deckTop) * 0.5f), new Vector2((x1 - x0) * 0.5f, (deckTop - baseY) * 0.5f));
                body = Mathf.Min(body, Sdf.Box(q, new Vector2((x0 + x1) * 0.5f, deckBottom), new Vector2((x1 - x0) * 0.5f + 0.06f, 0.035f)));
                float k = Mathf.Floor((q.x - (x0 + 0.1f)) / pierStep);
                float xc = x0 + 0.1f + k * pierStep + pierStep * 0.5f;
                float open = Mathf.Min(Sdf.Box(q, new Vector2(xc, (baseY - 0.2f + spring) * 0.5f), new Vector2(archR, (spring - baseY + 0.2f) * 0.5f)),
                    Sdf.Circle(q, new Vector2(xc, spring), archR));
                body = Mathf.Max(body, -open);
                // gebrochene Enden: rechts fehlt die Brüstung, links bröckelt die Kante
                float crumble = 0.35f * S01((q.x - (x1 - 1.5f)) / 0.7f) + 0.06f * Noise.Perlin(q.x * 7f, 3f);
                float rightCut = Mathf.Max(q.y - (deckTop - crumble), q.x - x1);
                float leftCut = (x0 + 0.15f * Noise.Perlin(q.y * 7f, 8f)) - q.x;
                return Mathf.Max(body, Mathf.Max(rightCut, leftCut));
            };
            c.Fill(shape, q =>
            {
                float k = Mathf.Floor((q.x - (x0 + 0.1f)) / pierStep);
                float xc = x0 + 0.1f + k * pierStep + pierStep * 0.5f;
                float du = q.x - xc;
                float light = 0.5f;
                // Pfeiler: linke Kante hell, rechte dunkel; Bogenlaibung im Schatten
                if (q.y < spring + archR + 0.05f)
                {
                    if (du > archR - 0.02f && du < archR + 0.06f) light = 0.2f;
                    else if (du < -archR + 0.02f && du > -archR - 0.06f) light = 0.85f;
                }
                if (q.y > deckBottom - 0.035f && q.y < deckBottom + 0.035f) light = 0.9f;
                Color col = Color.Lerp(C(40, 54, 94), C(92, 112, 158), light);
                if (Mathf.Repeat(q.y, 0.2f) < 0.015f) col = Mul(col, 0.8f);
                col = Mul(col, 0.9f + 0.16f * Noise.Perlin(q.x * 12f, q.y * 12f));
                col = Color.Lerp(col, C(44, 80, 76), S01((Noise.Perlin(q.x * 2.5f, q.y * 3f + 4f) - 0.6f) / 0.1f) * 0.5f);
                return col;
            }, 0f, new Rect(x0 - 0.2f, baseY - 0.3f, x1 - x0 + 0.4f, deckTop - baseY + 0.4f));
        }

        // ---------------------------------------------------------------- Waldtal mit Fluss und Brücke

        const float WaterY = 1.95f;
        const float BridgeX0 = -2.8f, BridgeX1 = -0.55f, BridgeTop = 2.86f;

        static float BridgeSdf(Vector2 q)
        {
            float deck = Sdf.Box(q, new Vector2((BridgeX0 + BridgeX1) * 0.5f, (WaterY - 0.2f + BridgeTop) * 0.5f), new Vector2((BridgeX1 - BridgeX0) * 0.5f, (BridgeTop - WaterY + 0.2f) * 0.5f));
            float arch1 = Mathf.Min(Sdf.Circle(q, new Vector2(-2.2f, 2.18f), 0.36f), Sdf.Box(q, new Vector2(-2.2f, 1.9f), new Vector2(0.36f, 0.3f)));
            float arch2 = Mathf.Min(Sdf.Circle(q, new Vector2(-1.18f, 2.18f), 0.36f), Sdf.Box(q, new Vector2(-1.18f, 1.9f), new Vector2(0.36f, 0.3f)));
            float d = Mathf.Max(deck, -Mathf.Min(arch1, arch2));
            // bröckelnde Brüstung
            d = Mathf.Max(d, q.y - (BridgeTop - 0.08f * Mathf.Abs(Noise.Perlin(q.x * 6f, 2f) - 0.5f) * 4f));
            return d;
        }

        static float PillarSdf(Vector2 q, float x, float hw, float b, float t, float seed)
            => Mathf.Max(Sdf.Box(q, new Vector2(x, (b + t) * 0.5f), new Vector2(hw, (t - b) * 0.5f)),
                q.y - (t - 0.3f * Mathf.Abs(Noise.Perlin(q.x * 10f + seed, 2f) - 0.5f) * 3f * hw));

        static SdfCanvas BuildValley()
        {
            var c = new SdfCanvas(ValleyRect, 80f);
            var r = new System.Random(812);
            float R() => (float)r.NextDouble();
            float xL = -3.35f, xR = 1.35f;   // wo der Wald dem Fluss Platz lässt

            // Wasser: heller zum fernen Ufer, Wellenlinien, Spiegelung von Brücke und Säule
            c.Fill(q => q.y - WaterY, q =>
            {
                float t = S01((WaterY - q.y) / 2.2f);
                Color col = Color.Lerp(C(100, 138, 196), C(40, 66, 118), t);
                float rip = Noise.Perlin(q.x * 1.4f, q.y * 24f);
                float rip2 = Noise.Perlin(q.x * 4f + 3f, q.y * 40f);
                col = Color.Lerp(col, C(150, 186, 236), S01((rip - 0.66f) / 0.12f) * (0.5f + 0.4f * rip2));
                Vector2 m = new Vector2(q.x + 0.05f * Mathf.Sin(q.y * 40f), 2f * WaterY - q.y + 0.1f);
                if (BridgeSdf(m) < 0f || PillarSdf(m, 0.15f, 0.13f, 1.7f, 3.0f, 5f) < 0f) col = Color.Lerp(col, C(34, 48, 86), 0.55f * (1f - t * 0.5f));
                col = Color.Lerp(col, C(170, 204, 245), Mathf.Exp(-Mathf.Pow((q.x + 1.25f) / 0.5f, 2f)) * S01(1f - (WaterY - q.y) / 0.35f) * 0.6f);
                col.a = 1f;
                return col;
            }, 0f, new Rect(xL - 1.5f, ValleyRect.yMin, xR - xL + 3f, WaterY - ValleyRect.yMin + 0.05f));

            // Brücke
            c.Fill(BridgeSdf, q =>
            {
                Color col = Masonry(q, BridgeX0, BridgeTop, 0.2f, 0.34f, 61, C(118, 132, 172), C(70, 86, 124), C(36, 46, 78), 0f, 0.4f);
                float inArch = Mathf.Min(Sdf.Circle(q, new Vector2(-2.2f, 2.18f), 0.44f), Sdf.Circle(q, new Vector2(-1.18f, 2.18f), 0.44f));
                if (inArch < 0f && q.y > 2.0f) col = Mul(col, 0.72f);
                col = MossOver(col, q, 0.3f, 12f);
                return Color.Lerp(col, C(82, 112, 164), 0.18f);
            }, 0f, new Rect(BridgeX0 - 0.1f, WaterY - 0.3f, BridgeX1 - BridgeX0 + 0.2f, BridgeTop - WaterY + 0.4f));
            // Säule im Wasser
            c.Fill(q => PillarSdf(q, 0.15f, 0.13f, 1.7f, 3.0f, 5f), q => PillarColor(q, 0.15f, 0.13f, 0.18f), 0f, new Rect(-0.1f, 1.6f, 0.5f, 1.5f));

            // Waldhänge links und rechts; der Rand zum Wasser bildet das Ufer
            Color fDark = C(18, 42, 58), fLit = C(40, 80, 98), fRim = C(80, 124, 150);
            float Hill(float x) => 2.1f + 0.55f * Fbm(x * 0.2f, 55f) + 1.2f * G(x, -8.5f, 3.2f) + 1.4f * G(x, 8.8f, 3.4f) + 0.6f * G(x, 13.5f, 2f)
                                   + 0.5f * G(x, -13f, 2f);
            c.Fill(q =>
            {
                float bank = Mathf.Min(q.x - (xL + 0.5f), (xR - 0.5f) - q.x);   // > 0 im Flussbereich
                float h = Mathf.Lerp(Hill(q.x), -1f, S01(bank / 0.6f + 0.5f));
                return q.y - h;
            }, q => Color.Lerp(Mul(fDark, 0.75f), fDark, S01((q.y + 0.6f) / 3f)), 0f);
            for (int pass = 0; pass < 2; pass++)
            {
                for (float x = ValleyRect.xMin + 0.1f; x < ValleyRect.xMax - 0.1f; x += 0.24f + R() * 0.3f)
                {
                    bool inRiver = x > xL + 0.1f && x < xR - 0.1f;
                    if (inRiver) continue;
                    float h = Mathf.Lerp(Hill(x), -1f, S01(Mathf.Min(x - (xL + 0.5f), (xR - 0.5f) - x) / 0.6f + 0.5f));
                    float rr = pass == 0 ? 0.34f + R() * 0.3f : 0.26f + R() * 0.24f;
                    float y = pass == 0 ? h - rr * 0.25f : h - 0.35f - R() * 1.4f;
                    Clump(c, new Vector2(x, y), rr, fDark, fLit, fRim, R() * 10f);
                }
            }
            // Uferfelsen und Büsche am Wasser
            for (int i = 0; i < 9; i++)
            {
                float x = i < 5 ? xL - 0.2f + R() * 0.9f : xR - 0.6f + R() * 0.9f;
                Boulder(c, new Vector2(x, WaterY - 0.5f - R() * 1.2f), 0.14f + R() * 0.14f, R() * 10f, C(46, 58, 88), C(96, 110, 150));
            }
            for (int i = 0; i < 6; i++)
            {
                float x = xL + 1.1f + R() * (xR - xL - 1.8f);
                RiverGlints.Add(new Vector2(x, WaterY - 0.25f - R() * 1.5f));
            }
            Vector2[] cry = { new Vector2(-3.55f, 1.5f), new Vector2(1.55f, 1.25f), new Vector2(-7.2f, 2.9f), new Vector2(6.9f, 3.25f) };
            for (int i = 0; i < cry.Length; i++)
            {
                float s = 0.18f + R() * 0.1f;
                CrystalCluster(c, cry[i], s, 700 + i * 11);
                ValleyCrystals.Add(new Vector3(cry[i].x, cry[i].y + s * 0.5f, s * 2.8f));
            }
            c.RimLight(new Vector2(-0.03f, 0.03f), C(130, 168, 210), 0.35f);
            return c;
        }

        static Color PillarColor(Vector2 q, float x, float hw, float seed)
        {
            float u = (q.x - x) / hw;
            float light = 0.62f - u * 0.45f;
            Color col = Color.Lerp(C(40, 52, 86), C(118, 132, 170), S01(light));
            if (Mathf.Abs(Mathf.Repeat(q.y + seed, 0.42f) - 0.21f) > 0.195f) col = Mul(col, 0.7f);
            if (Mathf.Abs(Mathf.Repeat(u * 2.5f, 1f) - 0.5f) > 0.44f) col = Mul(col, 0.88f);   // Kannelur
            col = Mul(col, 0.9f + 0.18f * Noise.Perlin(q.x * 16f, q.y * 16f + seed));
            return MossOver(col, q, 0.5f, seed * 3f);
        }

        static void Boulder(SdfCanvas c, Vector2 ctr, float r, float seed, Color dark, Color lit)
        {
            c.Fill(q => Sdf.Ellipse(q, ctr, new Vector2(r * 1.35f, r)) + 0.04f * r * (Noise.Perlin(q.x * 9f / r + seed, q.y * 9f / r) - 0.5f) * 4f,
                q =>
                {
                    Vector2 n = new Vector2((q.x - ctr.x) / (r * 1.35f), (q.y - ctr.y) / r);
                    float l = Vector2.Dot(n, L);
                    Color col = Color.Lerp(dark, lit, S01(0.4f + l * 0.7f));
                    col = Mul(col, 0.9f + 0.18f * Noise.Perlin(q.x * 20f, q.y * 20f + seed));
                    return MossOver(col, q, 0.5f * S01(n.y + 0.3f), seed);
                }, 0f, new Rect(ctr.x - r * 1.6f, ctr.y - r * 1.3f, r * 3.2f, r * 2.6f));
        }

        // ---------------------------------------------------------------- nahe Baumgruppen und Säulen

        static SdfCanvas BuildNear()
        {
            var c = new SdfCanvas(NearRect, 88f);
            var r = new System.Random(1203);
            float R() => (float)r.NextDouble();
            // dunkles Ufer als Grund, in der Mitte ein Streifen Wasser
            float Bank(float x) => 0.22f + 0.18f * Fbm(x * 0.5f, 61f);
            c.Fill(q => q.y - Bank(q.x), q => Color.Lerp(C(10, 24, 28), C(20, 40, 42), S01((q.y + 1.2f) / 1.4f)), 0f, new Rect(NearRect.xMin, NearRect.yMin, NearRect.width, 1.9f));
            float wx0 = -3.25f, wx1 = 1.45f;
            c.Fill(q => Mathf.Max(q.y - 0.32f, Mathf.Max(wx0 - q.x, q.x - wx1)), q =>
            {
                Color col = Color.Lerp(C(78, 112, 170), C(34, 56, 100), S01((0.32f - q.y) / 1.3f));
                float rip = Noise.Perlin(q.x * 1.6f + 9f, q.y * 26f);
                col = Color.Lerp(col, C(140, 176, 228), S01((rip - 0.68f) / 0.1f) * 0.45f);
                return col;
            }, 0f, new Rect(wx0 - 0.1f, NearRect.yMin, wx1 - wx0 + 0.2f, 1.6f));

            // Baumgruppen (dunkler als das Tal), mit hellen Oberkanten
            Color tDark = C(13, 32, 40), tLit = C(30, 64, 68), tRim = C(62, 108, 116);
            (float a, float b, float h)[] groups = { (-17.5f, -9.4f, 3.4f), (-8.4f, -5.2f, 2.1f), (3.4f, 8.3f, 2.7f), (10.2f, 17.5f, 3.7f),
                (-4.9f, -3.3f, 0.9f), (1.5f, 3.1f, 0.8f) };
            foreach (var g in groups)
            {
                float span = g.b - g.a;
                for (float x = g.a + 0.1f; x < g.b - 0.1f; x += 0.22f + R() * 0.3f)
                {
                    float u = (x - g.a) / span;
                    float h = 0.3f + g.h * Mathf.Pow(Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI), 0.6f) * (0.8f + 0.3f * R());
                    for (float y = 0.1f; y < h; y += 0.55f + R() * 0.3f)
                    {
                        float rr = 0.28f + R() * 0.26f + (h - y < 0.6f ? 0.08f : 0f);
                        Clump(c, new Vector2(x, y), rr, tDark, tLit, tRim, R() * 10f);
                    }
                }
            }
            // stehende Säulen
            (float x, float hw, float t)[] pillars = { (2.35f, 0.14f, 2.75f), (0.1f, 0.13f, 1.25f), (-6.6f, 0.15f, 2.1f), (-12.2f, 0.15f, 3.0f),
                (9.2f, 0.14f, 1.65f), (14.0f, 0.16f, 2.5f) };
            for (int i = 0; i < pillars.Length; i++)
            {
                var pl = pillars[i];
                float sd = 20f + i * 7f;
                c.Fill(q => Mathf.Min(PillarSdf(q, pl.x, pl.hw, -0.4f, pl.t, sd),
                        Sdf.Box(q, new Vector2(pl.x, 0f), new Vector2(pl.hw * 1.35f, 0.09f), 0.02f)),
                    q => Color.Lerp(PillarColor(q, pl.x, pl.hw, sd), C(22, 36, 56), 0.35f), 0f, new Rect(pl.x - 0.4f, -0.5f, 0.8f, pl.t + 0.6f));
            }
            // Felsen, Büsche und Kristalle am Ufer
            for (int i = 0; i < 16; i++)
            {
                float x = NearRect.xMin + 0.5f + R() * (NearRect.width - 1f);
                Boulder(c, new Vector2(x, Bank(x) + 0.02f), 0.14f + R() * 0.2f, R() * 10f, C(26, 36, 54), C(76, 88, 116));
            }
            for (float x = NearRect.xMin + 0.2f; x < NearRect.xMax - 0.2f; x += 0.3f + R() * 0.7f)
            {
                if (x > wx0 + 0.3f && x < wx1 - 0.3f) continue;
                Clump(c, new Vector2(x, Bank(x) + 0.08f), 0.16f + R() * 0.16f, C(16, 40, 34), C(40, 84, 56), C(80, 130, 100), R() * 10f);
            }
            Vector2[] cry = { new Vector2(1.95f, 0.42f), new Vector2(-4.1f, 0.5f), new Vector2(6.25f, 0.55f), new Vector2(-9.1f, 0.4f), new Vector2(12.3f, 0.5f) };
            for (int i = 0; i < cry.Length; i++)
            {
                float s = 0.22f + R() * 0.1f;
                CrystalCluster(c, cry[i], s, 500 + i * 13);
                NearCrystals.Add(new Vector3(cry[i].x, cry[i].y + s * 0.5f, s * 3f));
            }
            c.RimLight(new Vector2(-0.03f, 0.03f), C(110, 150, 180), 0.35f);
            return c;
        }

        // ---------------------------------------------------------------- Wiese und Mauer (Kacheln)

        static SdfCanvas BuildPitch()
        {
            float H = EnvironmentArt.PitchH;
            const float band = 0.13f;   // darunter hängt Gras über die Mauerkante
            var c = new SdfCanvas(new Rect(0f, 0f, PitchTileW, H), 120f);
            var rng = new System.Random(19);
            float R() => (float)rng.NextDouble();
            var patches = new List<Vector4>();
            float[] px = { 1.6f, 4.3f, 6.9f, 9.6f, 11.6f };
            for (int i = 0; i < px.Length; i++)
                patches.Add(new Vector4(px[i] + (R() - 0.5f) * 0.4f, band + 0.2f + R() * 0.45f, 0.35f + R() * 0.5f, 0.06f + R() * 0.05f));
            c.Field(p =>
            {
                if (p.y < band) return Clear;
                float yy = (p.y - band) / (H - band);   // 0 vorn .. 1 hinten
                float blades = PX(p.x * 46f, p.y * 7f, PitchTileW * 46f);
                float blades2 = PX(p.x * 90f + 4f, p.y * 13f, PitchTileW * 90f);
                float clump = PX(p.x * 2.4f, p.y * 5f, PitchTileW * 2.4f);
                Color col = Color.Lerp(C(116, 196, 54), C(80, 150, 50), S01(yy * 1.1f));
                col = Color.Lerp(col, C(150, 218, 76), S01((clump - 0.55f) / 0.2f) * 0.55f);
                col = Mul(col, 0.84f + 0.2f * blades + 0.1f * blades2);
                // ausgetretene Stellen mit Sand und Grasrand
                foreach (var pt in patches)
                {
                    float d = Sdf.Ellipse(p, new Vector2(pt.x, pt.y), new Vector2(pt.z, pt.w)) + 0.03f * (PX(p.x * 9f, p.y * 9f, PitchTileW * 9f) - 0.5f) * 2f;
                    if (d > 0.02f) continue;
                    Color dirt = Color.Lerp(C(186, 166, 112), C(140, 124, 86), PX(p.x * 14f, p.y * 30f, PitchTileW * 14f));
                    dirt = Mul(dirt, 0.9f + 0.2f * PX(p.x * 40f, p.y * 60f, PitchTileW * 40f));
                    col = d > 0f ? Mul(col, 0.88f) : Color.Lerp(col, dirt, 0.88f * S01(-d / 0.02f));
                }
                // Vorderkante etwas heller, hintere Kante im Schatten
                col = Color.Lerp(col, C(170, 230, 96), S01(1f - yy / 0.08f) * 0.35f);
                col = Mul(col, 1f - 0.18f * S01((yy - 0.8f) / 0.2f));
                col.a = 1f;
                return col;
            });
            // Grasspitzen hängen über die Vorderkante
            for (float x = 0.02f; x < PitchTileW - 0.02f; x += 0.02f + R() * 0.05f)
            {
                float h = 0.03f + R() * R() * 0.11f;
                Vector2 b = new Vector2(x, band + 0.03f), t = new Vector2(x + (R() - 0.5f) * 0.05f, band - h);
                Color gc = Color.Lerp(C(70, 140, 40), C(140, 210, 70), R());
                c.Fill(q => Sdf.Tapered(q, b, 0.011f, t, 0.002f), gc, 0f, Around(b, t, 0.03f));
            }
            return c;
        }

        static SdfCanvas BuildWall()
        {
            float H = EnvironmentArt.EarthH;
            var c = new SdfCanvas(new Rect(0f, 0f, WallTileW, H), 110f);
            c.Field(p =>
            {
                float fromTop = H - p.y;
                Color col;
                if (fromTop < 2.6f)
                {
                    col = Masonry(p, 0f, H, 0.62f, 1.2f, 7, C(170, 178, 192), C(112, 120, 136), C(54, 60, 78), WallTileW, 0.45f);
                    col = MossOver(col, p, 0.45f * S01(1f - fromTop / 1.3f) + 0.08f, 3f, WallTileW);
                    col = Mul(col, 1f - 0.72f * S01((fromTop - 0.35f) / 1.5f));   // nach unten im Schatten der Blätter
                    // Schatten direkt unter der überhängenden Wiese
                    col = Mul(col, 0.55f + 0.45f * S01((fromTop - 0.02f) / 0.14f));
                }
                else
                {
                    float n = PX(p.x * 3f, p.y * 3f, WallTileW * 3f);
                    col = Color.Lerp(C(14, 20, 26), C(28, 36, 40), n);
                }
                col.a = 1f;
                return col;
            });
            return c;
        }

        // ---------------------------------------------------------------- Fackelruine und Flamme

        static SdfCanvas BuildRuin()
        {
            var c = new SdfCanvas(new Rect(-1.35f, -0.35f, 2.7f, 3.55f), 130f);
            var r = new System.Random(31);
            float R() => (float)r.NextDouble();
            Color lit = C(170, 176, 190), mid = C(116, 122, 138), dark = C(54, 58, 74);

            // Blöcke wie im Bild: breiter Sockel, niedrige Mauer links mit der Fackel, hoher Pfeiler rechts
            (Vector2 c, Vector2 h)[] blocks =
            {
                (new Vector2(-0.05f, -0.1f), new Vector2(1.2f, 0.25f)),
                (new Vector2(-0.62f, 0.38f), new Vector2(0.56f, 0.23f)), (new Vector2(0.45f, 0.38f), new Vector2(0.5f, 0.23f)),
                (new Vector2(-0.72f, 0.84f), new Vector2(0.38f, 0.22f)), (new Vector2(0.38f, 0.84f), new Vector2(0.4f, 0.22f)),
                (new Vector2(0.4f, 1.3f), new Vector2(0.38f, 0.23f)), (new Vector2(0.36f, 1.76f), new Vector2(0.4f, 0.22f)),
                (new Vector2(0.42f, 2.22f), new Vector2(0.37f, 0.23f)), (new Vector2(0.38f, 2.68f), new Vector2(0.42f, 0.22f)),
                (new Vector2(0.52f, 3.06f), new Vector2(0.24f, 0.15f)),
            };
            for (int i = 0; i < blocks.Length; i++)
            {
                var b = blocks[i];
                int seed = 40 + i * 9;
                c.Fill(q => Sdf.Box(q, b.c, b.h, 0.035f) + 0.012f * (Noise.Perlin(q.x * 18f + seed, q.y * 18f) - 0.5f) * 2f, q =>
                {
                    Vector2 u = new Vector2((q.x - b.c.x) / b.h.x, (q.y - b.c.y) / b.h.y);
                    float tone = Hash01(seed);
                    Color col = Color.Lerp(Mul(mid, 0.9f), Mul(mid, 1.08f), tone);
                    col = Mul(col, 0.86f + 0.2f * Noise.Perlin(q.x * 9f, q.y * 9f + seed) + 0.08f * Noise.Perlin(q.x * 30f, q.y * 30f));
                    float top = S01((u.y - 0.72f) / 0.2f), leftE = S01((-u.x - 0.8f) / 0.15f);
                    float bottom = S01((-u.y - 0.62f) / 0.3f), rightE = S01((u.x - 0.78f) / 0.2f);
                    col = Color.Lerp(col, lit, Mathf.Max(top * 0.8f, leftE * 0.5f));
                    col = Color.Lerp(col, dark, Mathf.Max(bottom * 0.75f, rightE * 0.55f));
                    if (Crack(q, seed)) col = Mul(col, 0.66f);
                    if (N(q, 14f, seed + 7f) > 0.78f) col = Mul(col, 0.82f);
                    if (u.y > 0.55f && Noise.Perlin(q.x * 3f + seed, 2f) > 0.45f && 1f - u.y < 0.12f + 0.3f * Noise.Perlin(q.x * 8f, seed)) col = MossColor(q, seed);
                    return MossOver(col, q, 0.25f * S01(u.y + 0.2f), seed);
                }, 0f, new Rect(b.c.x - b.h.x - 0.05f, b.c.y - b.h.y - 0.05f, b.h.x * 2f + 0.1f, b.h.y * 2f + 0.1f));
            }
            // Fackelschale aus Eisen auf der niedrigen Mauer
            Vector2 bowl = TorchSpot + new Vector2(0f, -0.04f);
            c.Fill(q => Mathf.Max(Sdf.Ellipse(q, bowl, new Vector2(0.2f, 0.12f)), q.y - bowl.y),
                q => Color.Lerp(C(40, 32, 30), C(120, 82, 52), S01((q.x - bowl.x + 0.2f) / 0.4f) * 0.5f + S01((q.y - bowl.y + 0.12f) / 0.12f) * 0.4f), 0f,
                new Rect(bowl.x - 0.25f, bowl.y - 0.15f, 0.5f, 0.2f));
            c.Fill(q => Sdf.Box(q, bowl, new Vector2(0.21f, 0.018f), 0.01f), C(150, 104, 60), 0f, new Rect(bowl.x - 0.25f, bowl.y - 0.05f, 0.5f, 0.1f));
            c.Fill(q => Sdf.Box(q, bowl + new Vector2(0f, -0.2f), new Vector2(0.05f, 0.1f)), C(46, 38, 34), 0f, new Rect(bowl.x - 0.1f, bowl.y - 0.35f, 0.2f, 0.3f));
            // glühende Kohlen
            c.Fill(q => Mathf.Max(Sdf.Ellipse(q, bowl + new Vector2(0f, 0.02f), new Vector2(0.16f, 0.05f)), bowl.y + 0.01f - q.y),
                q => Color.Lerp(C(255, 150, 60), C(255, 220, 140), Noise.Perlin(q.x * 30f, q.y * 30f)), 0f,
                new Rect(bowl.x - 0.2f, bowl.y - 0.05f, 0.4f, 0.12f));
            // Holzstange für das Banner, aus dem Pfeiler ragend
            Vector2 pa = new Vector2(0.55f, BannerSpot.y + 0.03f), pb = new Vector2(BannerSpot.x - 0.38f, BannerSpot.y + 0.03f);
            c.Fill(q => Sdf.Capsule(q, pa, pb, 0.032f), q => Color.Lerp(C(58, 40, 30), C(128, 92, 62), S01((q.y - pa.y + 0.03f) / 0.06f)), 0f, Around(pa, pb, 0.06f));
            c.Fill(q => Sdf.Circle(q, pb, 0.045f), C(92, 66, 46), 0f, new Rect(pb.x - 0.08f, pb.y - 0.08f, 0.16f, 0.16f));
            // Moospolster und Grasbüschel auf den Oberkanten
            c.EdgeBand(Vector2.up, 0.05f, C(92, 160, 56), q => S01((Noise.Perlin(q.x * 5f, q.y * 5f) - 0.4f) / 0.15f));
            for (int i = 0; i < 40; i++)
            {
                float x = -1.2f + R() * 2.3f;
                float y = -1f;
                foreach (var b in blocks)
                    if (x > b.c.x - b.h.x + 0.05f && x < b.c.x + b.h.x - 0.05f && Mathf.Abs(x - bowl.x) > 0.25f) y = Mathf.Max(y, b.c.y + b.h.y);
                if (y < 0f) continue;
                Vector2 gb = new Vector2(x, y - 0.01f), gt = gb + new Vector2((R() - 0.5f) * 0.08f, 0.05f + R() * 0.1f);
                c.Fill(q => Sdf.Tapered(q, gb, 0.014f, gt, 0.002f), Color.Lerp(C(60, 120, 40), C(140, 206, 80), R()), 0f, Around(gb, gt, 0.03f));
            }
            c.RimLight(new Vector2(-0.02f, 0.02f), C(200, 214, 240), 0.35f);
            return c;
        }

        static SdfCanvas BuildFlame()
        {
            var c = new SdfCanvas(new Rect(-0.22f, -0.06f, 0.44f, 0.7f), 220f);
            SdfCanvas.SdfFn outer = q => Sdf.Tapered(q, new Vector2(0f, 0.11f), 0.13f, new Vector2(0.02f, 0.56f), 0.005f);
            SdfCanvas.SdfFn inner = q => Sdf.Tapered(q, new Vector2(0f, 0.09f), 0.075f, new Vector2(0.01f, 0.36f), 0.004f);
            c.Fill(outer, q => Color.Lerp(C(255, 120, 30), C(255, 70, 20, 0.6f), S01((q.y - 0.2f) / 0.35f)), 0.02f);
            c.Fill(inner, q => Color.Lerp(C(255, 238, 170), C(255, 190, 80), S01((q.y - 0.08f) / 0.28f)), 0.015f);
            return c;
        }

        // ---------------------------------------------------------------- Pflanzen (Atlas)

        static float Rnd(System.Random r) => (float)r.NextDouble();

        static void Blade(SdfCanvas c, Vector2 b, Vector2 tip, float curve, float w0, Color bottom, Color top)
        {
            Vector2 mid = Vector2.Lerp(b, tip, 0.5f) + new Vector2(-(tip.x - b.x) * curve, 0f);
            float y0 = Mathf.Min(b.y, tip.y), y1 = Mathf.Max(b.y, tip.y);
            Rect bounds = Rect.MinMaxRect(Mathf.Min(b.x, Mathf.Min(mid.x, tip.x)) - w0, y0 - w0, Mathf.Max(b.x, Mathf.Max(mid.x, tip.x)) + w0, y1 + w0);
            c.Fill(p => Mathf.Min(Sdf.Tapered(p, b, w0, mid, w0 * 0.62f), Sdf.Tapered(p, mid, w0 * 0.62f, tip, 0.0025f)),
                p => Color.Lerp(bottom, top, S01((p.y - y0) / Mathf.Max(1e-4f, y1 - y0))), 0f, bounds);
        }

        static SdfCanvas GrassTuft(int seed, float height, int blades, bool tall = false)
        {
            var r = new System.Random(seed);
            var c = new SdfCanvas(new Rect(-0.42f, 0f, 0.84f, height + 0.06f), tall ? 170f : 220f);
            for (int k = 0; k < blades; k++)
            {
                float bx = (Rnd(r) - 0.5f) * (tall ? 0.18f : 0.26f);
                float h = height * (0.45f + Rnd(r) * 0.55f);
                float lean = (Rnd(r) - 0.5f) * 0.6f * h + bx * 1.1f;
                float w0 = (tall ? 0.018f : 0.024f) + Rnd(r) * 0.012f;
                float shade = k / (float)blades;
                Color top = Color.Lerp(C(96, 170, 50), C(172, 230, 92), 0.3f + 0.7f * Rnd(r) * shade);
                Blade(c, new Vector2(bx, 0f), new Vector2(bx + lean, h), 0.28f, w0, C(40, 92, 30), top);
            }
            c.RimLight(new Vector2(-0.012f, 0.006f), C(210, 245, 160), 0.45f);
            return c;
        }

        static SdfCanvas Fern(int seed, float size)
        {
            var r = new System.Random(seed);
            var c = new SdfCanvas(new Rect(-size, 0f, size * 2f, size * 0.95f), 170f);
            int fronds = 6;
            for (int f = 0; f < fronds; f++)
            {
                float ang = 30f + 120f * f / (fronds - 1) + (Rnd(r) - 0.5f) * 10f;
                float len = size * (0.7f + Rnd(r) * 0.3f) * (1f - Mathf.Abs(ang - 90f) / 200f);
                Vector2 dir = MathUtil.Dir(ang);
                Vector2 tip = dir * len + new Vector2(0f, -len * 0.25f * Mathf.Abs(dir.x));
                Color stem = C(40, 96, 38);
                c.Fill(q => Sdf.Tapered(q, Vector2.zero, 0.012f, tip, 0.003f), stem, 0f, Around(Vector2.zero, tip, 0.03f));
                for (float t = 0.15f; t < 0.95f; t += 0.09f)
                {
                    Vector2 at = Vector2.Lerp(Vector2.zero, tip, t);
                    float ll = len * 0.22f * (1f - t * 0.7f);
                    Color lc = Color.Lerp(C(58, 128, 48), C(122, 190, 78), Rnd(r) * 0.6f + (1f - t) * 0.2f);
                    FoliageArt.Leaf(c, at + MathUtil.Dir(ang + 55f) * ll * 0.5f, ang + 55f, ll, ll * 0.22f, lc, 0.15f);
                    FoliageArt.Leaf(c, at + MathUtil.Dir(ang - 55f) * ll * 0.5f, ang - 55f, ll, ll * 0.22f, Mul(lc, 0.85f), 0.15f);
                }
            }
            c.RimLight(new Vector2(-0.01f, 0.008f), C(190, 235, 150), 0.35f);
            return c;
        }

        static SdfCanvas FlowerTuft(int seed, int kind)
        {
            var r = new System.Random(seed);
            var c = new SdfCanvas(new Rect(-0.3f, 0f, 0.6f, 0.42f), 220f);
            for (int k = 0; k < 7; k++)
            {
                float bx = (Rnd(r) - 0.5f) * 0.3f;
                Blade(c, new Vector2(bx, 0f), new Vector2(bx + (Rnd(r) - 0.5f) * 0.12f, 0.12f + Rnd(r) * 0.14f), 0.25f, 0.016f, C(40, 92, 30), C(120, 196, 70));
            }
            Color petal = kind == 0 ? C(240, 244, 255) : C(255, 226, 120);
            for (int k = 0; k < 4; k++)
            {
                Vector2 b = new Vector2((Rnd(r) - 0.5f) * 0.3f, 0f);
                Vector2 fc = b + new Vector2((Rnd(r) - 0.5f) * 0.08f, 0.2f + Rnd(r) * 0.16f);
                c.Fill(q => Sdf.Tapered(q, b, 0.006f, fc, 0.004f), C(56, 120, 44), 0f, Around(b, fc, 0.02f));
                for (int l = 0; l < 5; l++)
                {
                    Vector2 pc = fc + MathUtil.Dir(l * 72f + k * 20f) * 0.018f;
                    c.Fill(q => Sdf.Circle(q, pc, 0.015f), petal, 0f, new Rect(pc.x - 0.03f, pc.y - 0.03f, 0.06f, 0.06f));
                }
                c.Fill(q => Sdf.Circle(q, fc, 0.009f), C(255, 190, 70), 0f, new Rect(fc.x - 0.02f, fc.y - 0.02f, 0.04f, 0.04f));
            }
            return c;
        }

        static SdfCanvas Vine(int seed, float length)
        {
            var r = new System.Random(seed);
            var c = new SdfCanvas(new Rect(-0.3f, -length - 0.1f, 0.6f, length + 0.14f), 190f);
            int strands = 2 + r.Next(2);
            for (int s = 0; s < strands; s++)
            {
                float len = length * (0.55f + Rnd(r) * 0.45f);
                float x0 = (Rnd(r) - 0.5f) * 0.14f, ph = Rnd(r) * 6f;
                Vector2 Pos(float t) => new Vector2(x0 + 0.06f * Mathf.Sin(t * 7f + ph) * t, -t * len);
                for (float t = 0f; t < 1f; t += 0.02f)
                {
                    Vector2 a = Pos(t), b = Pos(t + 0.02f);
                    c.Fill(q => Sdf.Capsule(q, a, b, 0.009f * (1f - t * 0.5f)), C(44, 86, 34), 0f, Around(a, b, 0.03f));
                }
                for (float t = 0.05f; t < 0.98f; t += 0.07f + Rnd(r) * 0.05f)
                {
                    Vector2 at = Pos(t);
                    float side = Rnd(r) > 0.5f ? 1f : -1f;
                    float ang = -90f + side * (40f + Rnd(r) * 40f);
                    float ll = 0.07f + Rnd(r) * 0.05f;
                    Color lc = Color.Lerp(C(56, 118, 40), C(124, 192, 74), Rnd(r));
                    FoliageArt.Leaf(c, at + MathUtil.Dir(ang) * ll * 0.5f, ang, ll, ll * 0.36f, lc, 0.2f);
                }
            }
            c.RimLight(new Vector2(-0.01f, 0.006f), C(180, 232, 140), 0.35f);
            return c;
        }

        static SdfCanvas Bush(int seed, float width)
        {
            var r = new System.Random(seed);
            float h = width * 0.62f;
            var c = new SdfCanvas(new Rect(-width * 0.6f, -0.05f, width * 1.2f, h + 0.2f), 120f);
            var crown = new System.Random(seed + 1);
            Crown(c, new Vector2(0f, h * 0.48f), new Vector2(width * 0.46f, h * 0.44f), 14, crown, C(20, 56, 36), C(58, 116, 54), C(110, 170, 96));
            // ein paar Blätter stehen heraus
            for (int i = 0; i < 10; i++)
            {
                float a = 20f + Rnd(r) * 140f;
                Vector2 at = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad) * width * 0.44f, h * 0.45f + Mathf.Sin(a * Mathf.Deg2Rad) * h * 0.45f);
                FoliageArt.Leaf(c, at, a + (Rnd(r) - 0.5f) * 30f, 0.12f, 0.045f, Color.Lerp(C(36, 86, 44), C(84, 148, 66), Rnd(r)), 0.2f);
            }
            return c;
        }

        static SdfCanvas FrontLeaves(int seed)
        {
            var r = new System.Random(seed);
            var c = new SdfCanvas(new Rect(-1.4f, 0f, 2.8f, 2.2f), 110f);
            int n = 7 + r.Next(3);
            for (int i = 0; i < n; i++)
            {
                float ang = 35f + 110f * i / (n - 1) + (Rnd(r) - 0.5f) * 14f;
                float len = 1.1f + Rnd(r) * 0.7f;
                Vector2 dir = MathUtil.Dir(ang);
                Vector2 stemEnd = dir * len * 0.08f;
                Color lc = Color.Lerp(C(8, 20, 24), C(20, 44, 44), Rnd(r));
                FoliageArt.Leaf(c, stemEnd + dir * len * 0.38f, ang, len * 0.78f, len * 0.22f, lc, 0.08f);
            }
            c.RimLight(new Vector2(-0.02f, 0.02f), C(46, 80, 84), 0.5f);
            return c;
        }

        /// <summary>Großer Rahmenbaum: schräger Stamm mit Wurzeln, zwei Äste, dichte Krone aus Büscheln.</summary>
        static SdfCanvas FrameTree(int seed, int kind)
        {
            var r = new System.Random(seed);
            var c = new SdfCanvas(new Rect(-3.8f, -0.2f, 7.6f, 10.4f), 88f);
            Color bark = C(30, 34, 46), barkLit = C(72, 78, 98);
            SdfCanvas.ColorFn barkFn = q =>
            {
                float n = Noise.Perlin(q.x * 7f, q.y * 0.9f + seed);
                Color col = Color.Lerp(bark, barkLit, S01(n * 1.2f - 0.25f) * 0.55f);
                return Mul(col, 0.85f + 0.25f * Noise.Perlin(q.x * 20f, q.y * 3f));
            };
            float lean = kind == 0 ? 0.7f : -0.5f;
            Vector2 a = new Vector2(0f, 0f), m = new Vector2(lean * 0.5f, 3.6f), t = new Vector2(lean, 6.3f);
            c.Fill(q => Sdf.SmoothUnion(Sdf.Tapered(q, a, 0.42f, m, 0.3f), Sdf.Tapered(q, m, 0.3f, t, 0.18f), 0.2f), barkFn, 0f, new Rect(-1.5f, -0.2f, 3.5f, 7f));
            for (int i = 0; i < 4; i++)
            {
                float side = i % 2 == 0 ? 1f : -1f;
                Vector2 rs = new Vector2(side * 0.15f, 0.7f), re = new Vector2(side * (0.7f + Rnd(r) * 0.4f), 0f);
                c.Fill(q => Sdf.Tapered(q, rs, 0.16f, re, 0.04f), barkFn, 0f, Around(rs, re, 0.25f));
            }
            // Äste
            Vector2[] ends = { new Vector2(lean - 1.9f, 7.3f), new Vector2(lean + 1.8f, 7.5f), new Vector2(lean * 1.2f, 8.4f) };
            foreach (var e in ends)
            {
                Vector2 bs = Vector2.Lerp(m, t, 0.6f);
                c.Fill(q => Sdf.Tapered(q, bs, 0.14f, e, 0.05f), barkFn, 0f, Around(bs, e, 0.2f));
            }
            c.RimLight(new Vector2(-0.04f, 0.02f), C(110, 124, 150), 0.45f);
            // Krone: drei Massen, jeweils Büschel von unten nach oben
            Color dark = C(14, 34, 40), lit = C(36, 76, 66), rim = C(86, 136, 142);
            Crown(c, new Vector2(lean - 1.3f, 7.4f), new Vector2(1.4f, 1.0f), 26, r, dark, lit, rim);
            Crown(c, new Vector2(lean + 1.3f, 7.6f), new Vector2(1.4f, 1.0f), 26, r, dark, lit, rim);
            Crown(c, new Vector2(lean * 1.1f, 8.6f), new Vector2(1.7f, 1.0f), 30, r, dark, lit, rim);
            // hängende Ranken aus der Krone
            for (int i = 0; i < 7; i++)
            {
                float x = lean - 2.4f + Rnd(r) * 4.8f;
                float y0 = 6.9f + Rnd(r) * 0.3f, len = 0.5f + Rnd(r) * 1.1f;
                Vector2 va = new Vector2(x, y0), vb = new Vector2(x + (Rnd(r) - 0.5f) * 0.1f, y0 - len);
                c.Fill(q => Sdf.Tapered(q, va, 0.018f, vb, 0.006f), C(26, 60, 44), 0f, Around(va, vb, 0.04f));
                for (float k = 0.1f; k < 1f; k += 0.14f)
                    FoliageArt.Leaf(c, Vector2.Lerp(va, vb, k) + new Vector2(0.03f, 0f), -60f + Rnd(r) * 120f, 0.09f, 0.03f, C(40, 88, 60), 0.1f);
            }
            return c;
        }

        static SdfCanvas BannerCloth()
        {
            // hängt an der Stange (y = 0), Tuch 0.52 breit, unten zwei Spitzen
            var c = new SdfCanvas(new Rect(-0.34f, -1.18f, 0.68f, 1.24f), 200f);
            const float hw = 0.25f, len = 1.02f;
            SdfCanvas.SdfFn cloth = q =>
            {
                float d = Sdf.Box(q, new Vector2(0f, -len * 0.5f + 0.02f), new Vector2(hw, len * 0.5f));
                float notch = Sdf.Triangle(q, new Vector2(-0.12f, -len - 0.1f), new Vector2(0.12f, -len - 0.1f), new Vector2(0f, -len + 0.14f));
                return Mathf.Max(d, -notch);
            };
            c.Fill(cloth, q =>
            {
                float u = q.x / hw;
                float fold = 0.5f + 0.5f * Mathf.Sin(u * 5.2f + 0.8f);
                Color col = Color.Lerp(C(44, 30, 96), C(96, 70, 176), fold * 0.7f + 0.15f);
                col = Mul(col, 0.8f + 0.25f * S01((q.y + len) / len));
                // Borte und Rautenwappen
                if (Mathf.Abs(u) > 0.84f || q.y > -0.08f) col = Color.Lerp(col, C(170, 150, 220), 0.55f);
                Vector2 e = q - new Vector2(0f, -0.45f);
                float dia = Mathf.Abs(e.x) / 0.15f + Mathf.Abs(e.y) / 0.22f;
                if (Mathf.Abs(dia - 1f) < 0.1f || dia < 0.35f) col = Color.Lerp(col, C(214, 206, 240), 0.85f);
                else if (Mathf.Abs(dia - 0.62f) < 0.07f) col = Color.Lerp(col, C(180, 170, 226), 0.6f);
                col.a = 1f;
                return col;
            });
            // Ringe an der Stange
            for (int i = 0; i < 3; i++)
            {
                Vector2 rc = new Vector2(-0.18f + i * 0.18f, 0.0f);
                c.Fill(q => Sdf.Ring(q, rc, 0.035f, 0.014f), C(150, 120, 80), 0f, new Rect(rc.x - 0.06f, rc.y - 0.06f, 0.12f, 0.12f));
            }
            c.RimLight(new Vector2(-0.012f, 0.01f), C(190, 180, 240), 0.35f);
            return c;
        }
    }
}
