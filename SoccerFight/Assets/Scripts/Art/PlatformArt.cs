using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>One platform's generated art plus the anchors the world dresses it with.</summary>
    public sealed class PlatformLook
    {
        public Level.Platform P;
        public Sprite Body, Support, Glow;
        public Vector2 BodyCenter, SupportCenter, GlowCenter;
        public readonly List<Vector2> Hangs = new List<Vector2>();      // anchors along the underside (base position)
        public readonly List<Vector2> Crystals = new List<Vector2>();   // glowing crystal tips (sparkle specks drift off them)
        public readonly List<Vector3> Spots = new List<Vector3>();      // soft glow spots: x, y, size
        public readonly List<float> ChainX = new List<float>();         // hanging platforms: where the chains attach
        public Vector2 Lantern = new Vector2(float.NaN, 0f);            // lantern hook
        public bool HasLantern => !float.IsNaN(Lantern.x);
        internal readonly List<Sprite> Owned = new List<Sprite>();
    }

    /// <summary>
    /// The one-way platforms, drawn in the moonlit base palette (the stage grade re-lights them):
    /// ruined terraces on loggias, column capitals, floating mossy rocks, crystal slabs, rune-carved
    /// floating blocks, wooden decks hanging on chains and giant mushroom caps. Generated per stage
    /// through the ArtQueue — every function here is pure math and runs off the main thread.
    /// </summary>
    public static class PlatformArt
    {
        const float TopFront = 0.12f, TopBack = 0.14f;    // walkable strip: front edge / back edge around the standing line
        static readonly Color Clear = new Color(0f, 0f, 0f, 0f);

        /// <summary>Base extent of a platform (art is drawn where the platform sits at rest).</summary>
        struct Spec
        {
            public float X0, X1, T, Seed;
            public int SeedI;
            public Level.Style Kind;
            public float Width => X1 - X0;
            public float Center => (X0 + X1) * 0.5f;
        }

        static Spec SpecOf(Level.Platform p) => new Spec { X0 = p.BaseX0, X1 = p.BaseX1, T = p.BaseY, Seed = p.Seed, SeedI = p.Seed, Kind = p.Kind };

        /// <summary>Queue the art for a layout. The sprites arrive through the ArtQueue.</summary>
        public static PlatformLook[] Prepare(Level.Platform[] layout, string tag)
        {
            var looks = new PlatformLook[layout.Length];
            for (int i = 0; i < layout.Length; i++)
            {
                var a = new PlatformLook { P = layout[i] };
                looks[i] = a;
                var s = SpecOf(layout[i]);
                a.BodyCenter = BodyRect(s).center;
                ArtQueue.Add(tag + " Platform" + i, () => BuildBody(a, s), a.BodyCenter, sp => { a.Body = sp; a.Owned.Add(sp); }, false, true);
                if (s.Kind == Level.Style.Terrace || s.Kind == Level.Style.Capital || s.Kind == Level.Style.Mushroom)
                {
                    a.SupportCenter = SupportRect(s).center;
                    ArtQueue.Add(tag + " Support" + i, () => BuildSupport(s), a.SupportCenter, sp => { a.Support = sp; a.Owned.Add(sp); }, false, true);
                }
                if (s.Kind == Level.Style.Block)
                {
                    a.GlowCenter = BodyRect(s).center;
                    ArtQueue.Add(tag + " Runes" + i, () => BuildRunes(s, true), a.GlowCenter, sp => { a.Glow = sp; a.Owned.Add(sp); }, false, false);
                }
            }
            return looks;
        }

        /// <summary>
        /// A platform body drawn only for looks (the title screen's floating islands). The look's
        /// anchor lists fill like a real platform's. Pure math: any thread.
        /// </summary>
        public static SdfCanvas Decor(PlatformLook look, Level.Style kind, float x0, float x1, float top, int seed)
            => BuildBody(look, new Spec { X0 = x0, X1 = x1, T = top, Seed = seed, SeedI = seed, Kind = kind });

        /// <summary>Centre of a decor body's canvas (use it as the sprite pivot).</summary>
        public static Vector2 DecorCenter(Level.Style kind, float x0, float x1, float top)
            => BodyRect(new Spec { X0 = x0, X1 = x1, T = top, Kind = kind }).center;

        /// <summary>Free the textures of a layout that is no longer shown.</summary>
        public static void Release(PlatformLook[] looks)
        {
            if (looks == null) return;
            foreach (var a in looks)
            {
                foreach (var s in a.Owned)
                {
                    if (s == null) continue;
                    if (s.texture != null) Object.Destroy(s.texture);
                    Object.Destroy(s);
                }
                a.Owned.Clear();
            }
        }

        // ---------------------------------------------------------------- helpers (thread-safe)

        static float Fbm(float x, float seed, int octaves = 4) => EnvironmentArt.Fbm(x, seed, octaves);
        static float S01(float v) => MathUtil.Smooth01(v);
        static float Hash01(int n) => MathUtil.Hash(n) * 0.5f + 0.5f;
        static Color Mul(Color c, float f) { c.r *= f; c.g *= f; c.b *= f; return c; }
        static int Col(SdfCanvas c, float x) => Mathf.Clamp((int)((x - c.UnitRect.xMin) * c.Ppu), 0, c.Width - 1);
        static Rect Around(Vector2 a, Vector2 b, float pad) =>
            Rect.MinMaxRect(Mathf.Min(a.x, b.x) - pad, Mathf.Min(a.y, b.y) - pad, Mathf.Max(a.x, b.x) + pad, Mathf.Max(a.y, b.y) + pad);

        static Rect BodyRect(in Spec p)
        {
            switch (p.Kind)
            {
                case Level.Style.Rock: return new Rect(p.X0 - 0.45f, p.T - 1.75f, p.Width + 0.9f, 2.1f);
                case Level.Style.Terrace: return new Rect(p.X0 - 0.45f, p.T - 0.95f, p.Width + 0.9f, 1.75f);
                case Level.Style.Crystal: return new Rect(p.X0 - 0.6f, p.T - 1.85f, p.Width + 1.2f, 2.3f);
                case Level.Style.Block: return new Rect(p.X0 - 0.3f, p.T - 1.35f, p.Width + 0.6f, 1.75f);
                case Level.Style.Plank: return new Rect(p.X0 - 0.35f, p.T - 0.85f, p.Width + 0.7f, 1.2f);
                case Level.Style.Mushroom: return new Rect(p.X0 - 0.6f, p.T - 0.95f, p.Width + 1.2f, 1.5f);
                default: return new Rect(p.X0 - 0.4f, p.T - 1.0f, p.Width + 0.8f, 1.35f);
            }
        }

        static Rect SupportRect(in Spec p) => new Rect(p.X0 - 0.3f, 0.1f, p.Width + 0.6f, p.T - 0.3f);

        static SdfCanvas BuildBody(PlatformLook a, Spec s)
        {
            switch (s.Kind)
            {
                case Level.Style.Terrace: return BuildTerrace(a, s);
                case Level.Style.Capital: return BuildCapital(a, s);
                case Level.Style.Crystal: return BuildCrystal(a, s);
                case Level.Style.Block: return BuildBlock(a, s);
                case Level.Style.Plank: return BuildPlank(a, s);
                case Level.Style.Mushroom: return BuildMushroom(a, s);
                default: return BuildRock(a, s);
            }
        }

        enum Surface { Stone, Turf, Wood, Crystal, Cap, Rune }

        /// <summary>
        /// The walkable top in slight perspective: v = 0 at the front edge, 1 at the back edge. Stone
        /// shows worn flagstones through moss, rocks are grown over with turf, decks are boards.
        /// </summary>
        static Color TopColor(Vector2 p, float top, Surface kind, float seed)
        {
            float v = Mathf.Clamp01((p.y - (top - TopFront)) / (TopFront + TopBack));
            Color turf = Color.Lerp(new Color(0.5f, 0.78f, 0.3f), new Color(0.34f, 0.6f, 0.26f), v);
            float grain = Noise.Perlin(p.x * 30f + seed, p.y * 9f);
            float patch = Noise.Perlin(p.x * 2.1f + seed * 3f, p.y * 4f);
            float slant = p.x + (v - 0.5f) * 0.35f;
            Color col = turf;
            switch (kind)
            {
                case Surface.Stone:
                case Surface.Rune:
                {
                    float jx = Mathf.Abs(Mathf.Repeat(slant, 0.55f) - 0.275f);
                    Color flag = Color.Lerp(new Color(0.7f, 0.7f, 0.8f), new Color(0.9f, 0.87f, 0.83f), Hash01((int)Mathf.Floor(slant / 0.55f) * 31 + (int)seed));
                    if (jx > 0.262f || Mathf.Abs(v - 0.5f) < 0.03f) flag = Mul(flag, 0.62f);
                    col = Color.Lerp(flag, turf, S01((patch - (kind == Surface.Rune ? 0.6f : 0.45f)) / 0.15f));
                    break;
                }
                case Surface.Wood:
                {
                    // boards running into the depth, each a slightly different plank
                    float board = Mathf.Floor(slant / 0.24f);
                    float jx = Mathf.Abs(Mathf.Repeat(slant, 0.24f) - 0.12f);
                    Color wood = Color.Lerp(new Color(0.62f, 0.39f, 0.22f), new Color(0.8f, 0.53f, 0.3f), Hash01((int)board * 17 + (int)seed));
                    wood = Mul(wood, 0.9f + 0.2f * Noise.Perlin(p.x * 3f + board * 7f, p.y * 40f));
                    if (jx > 0.108f) wood = Mul(wood, 0.55f);
                    col = Color.Lerp(wood, turf, S01((patch - 0.68f) / 0.1f) * 0.8f);
                    break;
                }
                case Surface.Crystal:
                {
                    float facet = Noise.Perlin(p.x * 3.4f + seed, v * 2f);
                    col = Color.Lerp(new Color(0.42f, 0.66f, 0.95f), new Color(0.68f, 0.9f, 1f), facet);
                    if (Mathf.Abs(Mathf.Repeat(slant * 1.3f + facet * 0.4f, 0.7f) - 0.35f) > 0.338f) col = Color.Lerp(col, new Color(0.8f, 0.97f, 1f), 0.5f);
                    break;
                }
                case Surface.Cap:
                {
                    col = Color.Lerp(Mul(Palette.MushCap, 0.78f), Mul(Palette.MushCapDark, 0.9f), v);
                    float spot = Noise.Perlin(p.x * 4.2f + seed, v * 3f);
                    col = Color.Lerp(col, Palette.MushStem, S01((spot - 0.72f) / 0.05f) * 0.7f);
                    break;
                }
            }
            col = Mul(col, 0.88f + 0.22f * grain);
            // the front lip catches the moonlight, the back edge falls into shade
            Color lip = kind == Surface.Crystal ? new Color(0.9f, 1f, 1f) : kind == Surface.Wood ? new Color(0.92f, 0.7f, 0.45f) : new Color(0.78f, 0.95f, 0.45f);
            col = Color.Lerp(col, lip, S01((0.2f - v) / 0.2f) * 0.45f);
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

        static void MossSpill(SdfCanvas c, float x0, float x1, float y, System.Random r, float skip)
        {
            float R() => (float)r.NextDouble();
            for (float x = x0 + 0.1f; x < x1 - 0.1f; x += 0.25f + R() * 0.6f)
            {
                if (R() < skip) continue;
                Vector2 mc = new Vector2(x, y);
                Vector2 mr = new Vector2(0.12f + R() * 0.2f, 0.04f + R() * 0.05f);
                c.Paint(q => Sdf.Ellipse(q, mc, mr) + 0.02f * Noise.Perlin(q.x * 40f, q.y * 40f), Palette.Moss.WithAlpha(0.8f), 0.01f, new Rect(mc.x - 0.4f, mc.y - 0.2f, 0.8f, 0.4f));
            }
        }

        static Color FaceStone(Vector2 p, float light) =>
            Color.Lerp(new Color(0.55f, 0.57f, 0.72f), new Color(0.92f, 0.88f, 0.82f), Mathf.Clamp01(light)).WithAlpha(1f);

        /// <summary>Sparse hairline cracks: noise contour lines, but only inside a few weathered patches.</summary>
        static bool Crack(Vector2 p, float seed) =>
            Mathf.Abs(Noise.Perlin(p.x * 3.3f + seed, p.y * 3.3f) - 0.5f) < 0.008f && Noise.Perlin(p.x * 0.9f + seed, p.y * 0.9f + 3f) > 0.58f;

        // ---------------------------------------------------------------- terrace

        static SdfCanvas BuildTerrace(PlatformLook art, Spec pl)
        {
            float T = pl.T, x0 = pl.X0, x1 = pl.X1, seed = pl.Seed;
            const float faceBottom = 0.6f;
            var c = new SdfCanvas(BodyRect(pl), 110f);
            var r = new System.Random(pl.SeedI);
            float R() => (float)r.NextDouble();

            // broken balustrade along the back edge (drawn behind whoever stands on the terrace)
            float railGap = x0 + (x1 - x0) * (0.3f + R() * 0.4f);
            SdfCanvas.SdfFn balustrade = q =>
            {
                float d = Sdf.Box(q, new Vector2(pl.Center, T + 0.13f), new Vector2(pl.Width * 0.5f - 0.05f, 0.03f));
                float k = Mathf.Round((q.x - x0) / 0.2f);
                float bx = x0 + k * 0.2f;
                bool missing = MathUtil.Hash((int)k * 17 + pl.SeedI) > 0.5f || bx < x0 + 0.2f || bx > x1 - 0.2f;
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
                if (q.y > T - TopFront) return TopColor(q, T, Surface.Stone, seed);
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
                    if (Mathf.Repeat(q.x - x0 + MathUtil.Hash(pl.SeedI) * 0.4f, 0.8f) < 0.014f) col = Mul(col, 0.62f);
                }
                else col = FaceStone(q, lit - 0.4f);                                                    // chamfer
                col = Mul(col, 0.9f + 0.18f * Noise.Perlin(q.x * 13f, q.y * 13f));
                float stain = Noise.Perlin(q.x * 4.3f, seed);
                col = Mul(col, 1f - Mathf.Max(0f, stain - 0.5f) * 0.6f * S01(f / 0.3f));
                if (Crack(q, seed)) col = Mul(col, 0.62f);
                col.a = 1f;
                return col;
            }, 0f, new Rect(x0 - 0.3f, T - faceBottom - 0.1f, pl.Width + 0.6f, faceBottom + 0.35f));

            MossSpill(c, x0, x1, T - TopFront - 0.03f, r, 0.35f);
            Fringe(c, x0 - 0.05f, x1 + 0.05f, T - TopFront, r, 0.6f);
            c.RimLight(new Vector2(0.03f, 0.03f), new Color(1f, 0.96f, 0.86f), 0.45f);

            for (float x = x0 + 0.25f; x < x1 - 0.25f; x += 0.3f + R() * 0.55f) art.Hangs.Add(new Vector2(x, T - faceBottom + 0.03f));
            art.Lantern = new Vector2(pl.Center < 0f ? x1 - 0.45f : x0 + 0.45f, T - faceBottom + 0.02f);
            return c;
        }

        // ---------------------------------------------------------------- column capital

        static SdfCanvas BuildCapital(PlatformLook art, Spec pl)
        {
            float T = pl.T, x0 = pl.X0, x1 = pl.X1, seed = pl.Seed, cx = pl.Center;
            const float abacusBottom = 0.36f, echinusBottom = 0.72f;
            var c = new SdfCanvas(BodyRect(pl), 110f);
            var r = new System.Random(pl.SeedI);
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
                float kk = k;
                c.Fill(q => Sdf.Box(q, new Vector2(cx, y), new Vector2(0.46f - kk * 0.01f, 0.018f), 0.01f),
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
                if (q.y > T - TopFront) return TopColor(q, T, Surface.Stone, seed);
                float f = (T - TopFront) - q.y;
                float lit = 0.35f + 0.3f * S01((q.x - x0) / pl.Width);
                Color col = FaceStone(q, f < 0.04f ? lit + 0.35f : f > abacusBottom - TopFront - 0.04f ? lit - 0.4f : lit);
                if (Mathf.Abs(f - 0.11f) < 0.01f) col = Mul(col, 0.7f);
                col = Mul(col, 0.9f + 0.18f * Noise.Perlin(q.x * 13f, q.y * 13f));
                if (Crack(q, seed)) col = Mul(col, 0.62f);
                col.a = 1f;
                return col;
            }, 0f, new Rect(x0 - 0.3f, T - abacusBottom - 0.1f, pl.Width + 0.6f, abacusBottom + 0.35f));
            MossSpill(c, x0, x1, T - TopFront - 0.025f, r, 0.4f);
            Fringe(c, x0 - 0.05f, x1 + 0.05f, T - TopFront, r, 0.7f);
            c.RimLight(new Vector2(0.03f, 0.03f), new Color(1f, 0.96f, 0.86f), 0.45f);

            art.Hangs.Add(new Vector2(x0 + 0.12f + R() * 0.15f, T - abacusBottom + 0.03f));
            art.Hangs.Add(new Vector2(x1 - 0.12f - R() * 0.15f, T - abacusBottom + 0.03f));
            art.Hangs.Add(new Vector2(cx + (R() - 0.5f) * 0.5f, T - echinusBottom - 0.12f));
            if (MathUtil.Hash(pl.SeedI) > -0.2f) art.Lantern = new Vector2(pl.Center < 0f ? x0 + 0.22f : x1 - 0.22f, T - abacusBottom + 0.02f);
            return c;
        }

        // ---------------------------------------------------------------- floating rock

        static SdfCanvas BuildRock(PlatformLook art, Spec pl)
        {
            float T = pl.T, x0 = pl.X0, x1 = pl.X1, seed = pl.Seed;
            Rect rect = BodyRect(pl);
            var c = new SdfCanvas(rect, 110f);
            int W = c.Width;
            float left = rect.xMin, ppu = c.Ppu;
            var r = new System.Random(pl.SeedI);
            float R() => (float)r.NextDouble();

            // underside: a lumpy inverted cone with a few hanging lobes
            var bottom = new float[W];
            float depth = 0.95f + 0.3f * Hash01(pl.SeedI);
            if (pl.Width < 2.2f) depth *= 0.8f;
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
                if (p.y > T - TopFront) col = TopColor(p, T, Surface.Turf, seed);
                else
                {
                    float below = (T - TopFront) - p.y;
                    // layered sediment, a touch warmer than the teal ruins so the rock separates from them
                    float strata = 0.5f + 0.5f * Mathf.Sin(p.y * 22f + Noise.Perlin(p.x * 1.6f, seed) * 5f);
                    float grain = Noise.Perlin(p.x * 6f, p.y * 6f);
                    Color rock = Color.Lerp(new Color(0.52f, 0.5f, 0.64f), new Color(0.8f, 0.76f, 0.74f), strata * 0.55f + 0.3f * grain);
                    rock = Mul(rock, 0.78f + 0.4f * Mathf.Clamp01((p.x - x0) / pl.Width));
                    rock = Mul(rock, 1f - 0.6f * S01((below - 0.2f) / 0.85f));
                    col = Color.Lerp(new Color(0.42f, 0.4f, 0.54f), rock, S01((below - 0.07f) / 0.14f));
                    if (Crack(p, seed)) col = Mul(col, 0.55f);
                    float moss = S01((Noise.Perlin(p.x * 2.4f, p.y * 2.4f + seed) - 0.5f) / 0.12f) * S01((0.5f - below) / 0.3f);
                    col = Color.Lerp(col, new Color(0.46f, 0.74f, 0.3f), moss * 0.7f);
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
            c.RimLight(new Vector2(0.03f, 0.03f), new Color(1f, 0.95f, 0.82f), 0.5f);

            for (float x = x0 + 0.2f; x < x1 - 0.2f; x += 0.22f + R() * 0.4f) art.Hangs.Add(new Vector2(x, bottom[Col(c, x)] + 0.06f));
            return c;
        }

        // ---------------------------------------------------------------- crystal slab

        /// <summary>A thin crust of dark stone with a forest of faceted crystal prisms hanging from it.</summary>
        static SdfCanvas BuildCrystal(PlatformLook art, Spec pl)
        {
            float T = pl.T, x0 = pl.X0, x1 = pl.X1, seed = pl.Seed, cx = pl.Center;
            var c = new SdfCanvas(BodyRect(pl), 110f);
            var r = new System.Random(pl.SeedI);
            float R() => (float)r.NextDouble();
            Color deep = new Color(0.3f, 0.48f, 0.86f), mid = new Color(0.5f, 0.78f, 1f), bright = new Color(0.9f, 1f, 1f);

            void Prism(Vector2 root, float ang, float len, float w, float shade)
            {
                Vector2 dir = MathUtil.Dir(ang), side = new Vector2(-dir.y, dir.x);
                Vector2 tip = root + dir * len, shoulder = root + dir * (len - w * 1.6f);
                SdfCanvas.SdfFn f = q =>
                {
                    float body = Sdf.Capsule(q, root, shoulder, w) ;
                    float point = Sdf.Triangle(q, shoulder + side * w, shoulder - side * w, tip);
                    return Mathf.Min(body, point);
                };
                c.Fill(f, q =>
                {
                    // three facets across the prism: shadow side, lit ridge, mid side
                    float u = Vector2.Dot(q - root, side) / w;
                    float along = Mathf.Clamp01(Vector2.Dot(q - root, dir) / len);
                    Color col = u < -0.3f ? Color.Lerp(deep, mid, 0.25f) : u < 0.25f ? Color.Lerp(mid, bright, 0.45f) : mid;
                    col = Color.Lerp(col, bright, S01((along - 0.75f) / 0.25f) * 0.6f);
                    col = Mul(col, shade * (0.92f + 0.12f * Noise.Perlin(q.x * 18f, q.y * 18f)));
                    col.a = 0.97f;
                    return col;
                }, 0f, Around(root, tip, w + 0.05f));
                art.Crystals.Add(tip - dir * 0.05f);
            }

            // back row (smaller, darker), then the big front prisms, longest in the middle
            int n = Mathf.Max(3, Mathf.RoundToInt(pl.Width / 0.42f));
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < n; i++)
                {
                    float u = (i + 0.5f + (R() - 0.5f) * 0.6f) / n;
                    float x = x0 + 0.1f + u * (pl.Width - 0.2f);
                    float mid01 = Mathf.Sin(u * Mathf.PI);
                    float len = (pass == 0 ? 0.35f : 0.4f) + mid01 * (pass == 0 ? 0.45f : 0.72f) * (0.7f + R() * 0.5f);
                    float spread = (u - 0.5f) * 60f;
                    float ang = -90f + spread + (R() - 0.5f) * 18f;
                    Prism(new Vector2(x, T - 0.2f), ang, len, (pass == 0 ? 0.07f : 0.09f) + R() * 0.05f, pass == 0 ? 0.65f : 1f);
                }
            }
            // a few splinters jutting sideways at both ends
            for (int s = -1; s <= 1; s += 2)
            {
                float ex = s < 0 ? x0 + 0.05f : x1 - 0.05f;
                Prism(new Vector2(ex, T - 0.18f), s < 0 ? 205f + R() * 20f : -25f - R() * 20f, 0.35f + R() * 0.25f, 0.06f, 0.85f);
            }

            // the crust: a chipped slab of dark stone with a frosted crystal top
            SdfCanvas.SdfFn crust = q =>
            {
                float n2 = 0.05f * Noise.Perlin(q.y * 14f, seed) + 0.04f * Noise.Perlin(q.x * 9f, seed + 3f);
                float ends = Mathf.Max(x0 - 0.08f + n2 - q.x, q.x - (x1 + 0.08f - n2));
                float bottomY = T - 0.3f - 0.06f * Noise.Perlin(q.x * 5f, seed);
                return Mathf.Max(ends, Mathf.Max(bottomY - q.y, q.y - (T + TopBack)));
            };
            c.Fill(crust, q =>
            {
                if (q.y > T - TopFront) return TopColor(q, T, Surface.Crystal, seed);
                float f = (T - TopFront) - q.y;
                Color col = Color.Lerp(new Color(0.64f, 0.64f, 0.78f), new Color(0.46f, 0.46f, 0.62f), S01(f / 0.2f));
                col = Mul(col, 0.8f + 0.35f * S01((q.x - x0) / pl.Width) + 0.1f * Noise.Perlin(q.x * 12f, q.y * 12f));
                if (Crack(q, seed)) col = Color.Lerp(col, mid, 0.6f);
                col.a = 1f;
                return col;
            }, 0f, new Rect(x0 - 0.2f, T - 0.45f, pl.Width + 0.4f, 0.65f));

            // small crystal nubs along the back edge
            for (float x = x0 + 0.15f; x < x1 - 0.15f; x += 0.35f + R() * 0.6f)
            {
                if (R() < 0.4f) continue;
                Vector2 a = new Vector2(x, T + 0.08f), b = a + MathUtil.Dir(90f + (R() - 0.5f) * 40f) * (0.12f + R() * 0.16f);
                c.Fill(q => Sdf.Tapered(q, a, 0.035f, b, 0.004f), q => Color.Lerp(mid, bright, S01((q - a).magnitude / 0.25f)).WithAlpha(1f), 0f, Around(a, b, 0.05f));
            }
            c.RimLight(new Vector2(0.03f, 0.03f), new Color(1f, 0.97f, 0.9f), 0.55f);
            art.Spots.Add(new Vector3(cx, T - 0.9f, pl.Width * 1.1f + 1f));
            return c;
        }

        // ---------------------------------------------------------------- rune block

        static float Glyph(Vector2 q, Vector2 c, int kind, float s)
        {
            Vector2 p = (q - c) / s;
            float d;
            switch (kind)
            {
                case 0: d = Mathf.Min(Sdf.Segment(p, new Vector2(0f, -0.8f), new Vector2(0f, 0.8f)), Sdf.Segment(p, new Vector2(-0.45f, 0.25f), new Vector2(0.45f, 0.25f))); break;
                case 1: d = Mathf.Min(Mathf.Abs(Sdf.Circle(p, Vector2.zero, 0.62f)), Sdf.Circle(p, Vector2.zero, 0.12f)); break;
                case 2: d = Mathf.Abs(Sdf.Triangle(p, new Vector2(-0.65f, -0.55f), new Vector2(0.65f, -0.55f), new Vector2(0f, 0.75f))); break;
                case 3: d = Mathf.Min(Sdf.Segment(p, new Vector2(-0.55f, 0.75f), new Vector2(0f, -0.75f)), Sdf.Segment(p, new Vector2(0f, -0.75f), new Vector2(0.55f, 0.75f))); break;
                default:
                    d = Mathf.Min(Mathf.Min(Sdf.Segment(p, new Vector2(-0.5f, 0.7f), new Vector2(0.5f, 0.25f)), Sdf.Segment(p, new Vector2(0.5f, 0.25f), new Vector2(-0.5f, -0.25f))),
                        Sdf.Segment(p, new Vector2(-0.5f, -0.25f), new Vector2(0.5f, -0.7f)));
                    break;
            }
            return d * s;
        }

        static float BlockShape(in Spec pl, Vector2 q)
        {
            float x0 = pl.X0, x1 = pl.X1, T = pl.T, seed = pl.Seed;
            float n = 0.03f * Noise.Perlin(q.y * 15f, seed);
            float d = Sdf.Box(q, new Vector2(pl.Center, T - 0.43f), new Vector2(pl.Width * 0.5f + 0.05f - n, 0.57f), 0.04f);
            // chipped corners and a broken, uneven underside
            float chipL = Sdf.HalfPlane(q, new Vector2(x0 - 0.05f, T - 0.72f - 0.12f * Hash01(pl.SeedI)), new Vector2(-1f, -0.9f));
            float chipR = Sdf.HalfPlane(q, new Vector2(x1 + 0.05f, T - 0.8f - 0.12f * Hash01(pl.SeedI + 3)), new Vector2(1f, -0.7f));
            d = Mathf.Max(d, Mathf.Max(chipL, chipR));
            float under = T - 0.9f - 0.22f * Noise.Perlin(q.x * 2.2f, seed) - 0.1f * Noise.Perlin(q.x * 7f, seed + 4f);
            return Mathf.Max(d, under - q.y);
        }

        /// <summary>A floating block carved from the old stadium: bevelled stone with a glowing band of runes.</summary>
        static SdfCanvas BuildBlock(PlatformLook art, Spec pl)
        {
            float T = pl.T, x0 = pl.X0, x1 = pl.X1, seed = pl.Seed;
            var c = new SdfCanvas(BodyRect(pl), 110f);
            var r = new System.Random(pl.SeedI);
            float R() => (float)r.NextDouble();
            var spec = pl;
            c.Fill(q => BlockShape(spec, q), q =>
            {
                if (q.y > T - TopFront) return TopColor(q, T, Surface.Rune, seed);
                float f = (T - TopFront) - q.y;
                float lit = 0.3f + 0.35f * S01((q.x - x0) / pl.Width);
                Color col = FaceStone(q, f < 0.05f ? lit + 0.4f : lit);
                // two courses of big ashlar blocks
                float course = f < 0.42f ? 0f : 1f;
                float bx = Mathf.Repeat(q.x - x0 + course * 0.45f + MathUtil.Hash(pl.SeedI) * 0.3f, 0.9f);
                if (bx < 0.018f || Mathf.Abs(f - 0.42f) < 0.012f) col = Mul(col, 0.6f);
                // the engraved band
                if (Mathf.Abs(f - 0.3f) < 0.13f) col = Mul(col, 0.82f);
                if (Mathf.Abs(Mathf.Abs(f - 0.3f) - 0.13f) < 0.01f) col = Mul(col, 1.25f);
                col = Mul(col, 0.9f + 0.16f * Noise.Perlin(q.x * 12f, q.y * 12f));
                col = Mul(col, 1f - 0.4f * S01((f - 0.55f) / 0.4f));
                if (Crack(q, seed)) col = Mul(col, 0.6f);
                col.a = 1f;
                return col;
            });
            // carved runes (dark grooves; the glow layer lights them)
            float band = T - TopFront - 0.3f;
            int gi = 0;
            for (float x = x0 + 0.28f; x < x1 - 0.2f; x += 0.34f, gi++)
            {
                Vector2 gc = new Vector2(x, band);
                int kind = (int)(Hash01(pl.SeedI * 13 + gi * 7) * 5f);
                c.Fill(q => Glyph(q, gc, kind, 0.08f) - 0.012f, new Color(0.38f, 0.4f, 0.56f), 0f, new Rect(gc.x - 0.12f, gc.y - 0.12f, 0.24f, 0.24f));
            }
            MossSpill(c, x0, x1, T - TopFront - 0.03f, r, 0.55f);
            Fringe(c, x0 - 0.05f, x1 + 0.05f, T - TopFront, r, 0.45f);
            c.RimLight(new Vector2(0.03f, 0.03f), new Color(1f, 0.96f, 0.86f), 0.45f);
            for (float x = x0 + 0.25f; x < x1 - 0.25f; x += 0.3f + R() * 0.5f)
                art.Hangs.Add(new Vector2(x, T - 0.9f - 0.22f * Noise.Perlin(x * 2.2f, seed) + 0.08f));
            art.Spots.Add(new Vector3(pl.Center, band, pl.Width * 0.9f));
            return c;
        }

        /// <summary>Just the runes, bright, for an additive layer that pulses.</summary>
        static SdfCanvas BuildRunes(Spec pl, bool soft)
        {
            float T = pl.T, x0 = pl.X0, x1 = pl.X1;
            var c = new SdfCanvas(BodyRect(pl), 110f);
            float band = T - TopFront - 0.3f;
            int gi = 0;
            for (float x = x0 + 0.28f; x < x1 - 0.2f; x += 0.34f, gi++)
            {
                Vector2 gc = new Vector2(x, band);
                int kind = (int)(Hash01(pl.SeedI * 13 + gi * 7) * 5f);
                Rect b = new Rect(gc.x - 0.18f, gc.y - 0.18f, 0.36f, 0.36f);
                c.Fill(q => Glyph(q, gc, kind, 0.08f) - 0.012f, new Color(0.55f, 0.95f, 1f, 0.35f), 0.07f, b);
                c.Fill(q => Glyph(q, gc, kind, 0.08f) - 0.008f, new Color(0.85f, 1f, 1f, 1f), 0f, b);
            }
            return c;
        }

        // ---------------------------------------------------------------- hanging wooden deck

        static SdfCanvas BuildPlank(PlatformLook art, Spec pl)
        {
            float T = pl.T, x0 = pl.X0, x1 = pl.X1, seed = pl.Seed;
            var c = new SdfCanvas(BodyRect(pl), 110f);
            var r = new System.Random(pl.SeedI);
            float R() => (float)r.NextDouble();
            Color woodDark = new Color(0.42f, 0.25f, 0.15f), wood = new Color(0.66f, 0.42f, 0.24f), woodLight = new Color(0.9f, 0.64f, 0.37f);
            Color iron = new Color(0.3f, 0.32f, 0.42f), ironLight = new Color(0.64f, 0.68f, 0.78f);
            float cl = x0 + 0.35f, cr = x1 - 0.35f;

            // joists and diagonal braces under the deck (behind the edge beam)
            for (int k = 0; k < 2; k++)
            {
                float jx = k == 0 ? cl : cr;
                Vector2 a = new Vector2(jx, T - 0.18f), b = new Vector2(jx + (k == 0 ? 0.55f : -0.55f), T - 0.52f);
                c.Fill(q => Sdf.Capsule(q, a, b, 0.035f), q => Mul(wood, 0.6f).WithAlpha(1f), 0f, Around(a, b, 0.06f));
                c.Fill(q => Sdf.Box(q, new Vector2(jx, T - 0.38f), new Vector2(0.05f, 0.2f), 0.01f), q => Mul(Color.Lerp(woodDark, wood, 0.6f), 0.9f + 0.2f * Noise.Perlin(q.x * 30f, q.y * 8f)).WithAlpha(1f));
            }
            c.Fill(q => Sdf.Box(q, new Vector2(pl.Center, T - 0.5f), new Vector2(pl.Width * 0.5f - 0.2f, 0.035f), 0.01f), Mul(wood, 0.55f).WithAlpha(1f));

            // deck and the thick edge beam, ends a little ragged, one board broken off
            float broken = R() < 0.5f ? x0 : x1;
            SdfCanvas.SdfFn deck = q =>
            {
                float ends = Mathf.Max(x0 - 0.12f - q.x, q.x - (x1 + 0.12f));
                float d = Mathf.Max(ends, Mathf.Max(T - 0.3f - q.y, q.y - (T + TopBack)));
                // a missing chunk at one corner of the beam
                d = Mathf.Max(d, -Sdf.Box(q, new Vector2(broken, T - 0.25f), new Vector2(0.14f, 0.08f)));
                return d;
            };
            c.Fill(deck, q =>
            {
                if (q.y > T - TopFront) return TopColor(q, T, Surface.Wood, seed);
                float f = (T - TopFront) - q.y;
                // edge beam: horizontal boards with end grain and nails
                float row = f < 0.09f ? 0f : 1f;
                Color col = Color.Lerp(wood, woodLight, 0.35f + 0.35f * S01((q.x - x0) / pl.Width));
                col = Mul(col, 0.85f + 0.25f * Noise.Perlin(q.x * 2f + row * 5f, q.y * 60f));
                if (Mathf.Abs(f - 0.09f) < 0.008f) col = Mul(col, 0.5f);
                float seam = Mathf.Repeat(q.x - x0 + row * 0.6f + Hash01(pl.SeedI) * 0.5f, 1.2f);
                if (seam < 0.012f) col = Mul(col, 0.45f);
                if (Mathf.Abs(seam - 0.06f) < 0.012f && Mathf.Abs(f - (row == 0f ? 0.045f : 0.13f)) < 0.012f) col = ironLight;
                if (f < 0.02f) col = Color.Lerp(col, woodLight, 0.6f);
                col.a = 1f;
                return col;
            }, 0f, new Rect(x0 - 0.3f, T - 0.4f, pl.Width + 0.6f, 0.65f));

            // iron brackets where the chains hold the deck
            foreach (float bx in new[] { cl, cr })
            {
                float x = bx;
                c.Fill(q => Sdf.Box(q, new Vector2(x, T - 0.1f), new Vector2(0.07f, 0.16f), 0.015f), q => Color.Lerp(iron, ironLight, S01((q.x - x + 0.07f) / 0.14f) * 0.6f).WithAlpha(1f));
                c.Fill(q => Mathf.Abs(Sdf.Circle(q, new Vector2(x, T + 0.1f), 0.06f)) - 0.016f, ironLight);
                art.ChainX.Add(x);
            }
            MossSpill(c, x0, x1, T - TopFront - 0.02f, r, 0.6f);
            Fringe(c, x0 + 0.1f, x1 - 0.1f, T - TopFront, r, 0.3f);
            c.RimLight(new Vector2(0.03f, 0.03f), new Color(1f, 0.92f, 0.78f), 0.4f);
            for (float x = x0 + 0.3f; x < x1 - 0.3f; x += 0.45f + R() * 0.6f) art.Hangs.Add(new Vector2(x, T - 0.28f));
            if (R() < 0.6f) art.Lantern = new Vector2(pl.Center < 0f ? x1 - 0.15f : x0 + 0.15f, T - 0.28f);
            return c;
        }

        // ---------------------------------------------------------------- giant mushroom

        static SdfCanvas BuildMushroom(PlatformLook art, Spec pl)
        {
            float T = pl.T, x0 = pl.X0, x1 = pl.X1, seed = pl.Seed, cx = pl.Center;
            var c = new SdfCanvas(BodyRect(pl), 110f);
            var r = new System.Random(pl.SeedI);
            float R() => (float)r.NextDouble();
            float hw = pl.Width * 0.5f + 0.45f;
            Color capDark = Mul(Palette.MushCapDark, 0.95f), cap = Palette.MushCap, stem = Mul(Palette.MushStem, 0.92f);

            // gills under the cap (seen from slightly below)
            c.Fill(q =>
            {
                float dx = (q.x - cx) / hw;
                float rim = T - 0.58f + 0.28f * (1f - dx * dx);
                float under = T - 0.72f + 0.14f * (1f - dx * dx);
                return Mathf.Max(Mathf.Abs(q.x - cx) - hw * 0.96f, Mathf.Max(under - q.y, q.y - rim));
            }, q =>
            {
                float dx = (q.x - cx) / hw;
                float lines = 0.5f + 0.5f * Mathf.Cos(Mathf.Atan2(q.x - cx, (T - 0.2f) - q.y) * 70f);
                Color col = Color.Lerp(Mul(stem, 0.55f), stem, lines * 0.6f);
                return Mul(col, 0.7f + 0.3f * (1f - Mathf.Abs(dx))).WithAlpha(1f);
            }, 0f, new Rect(cx - hw, T - 0.75f, hw * 2f, 0.6f));

            // the cap: a flattened dome, clipped flat on top where it is walked on
            SdfCanvas.SdfFn capFn = q =>
            {
                float dx = (q.x - cx) / hw;
                float rim = T - 0.62f + 0.3f * (1f - dx * dx);
                float dome = Sdf.Ellipse(q, new Vector2(cx, T - 0.12f), new Vector2(hw, 0.58f));
                return Mathf.Max(dome, Mathf.Max(q.y - (T + TopBack), rim - q.y));
            };
            c.Fill(capFn, q =>
            {
                if (q.y > T - TopFront) return TopColor(q, T, Surface.Cap, seed);
                float f = (T - TopFront) - q.y;
                float dx = (q.x - cx) / hw;
                Color col = Color.Lerp(cap, capDark, S01(f / 0.5f));
                col = Mul(col, 0.8f + 0.35f * S01((dx + 1f) * 0.5f));
                col = Mul(col, 0.92f + 0.12f * Noise.Perlin(q.x * 8f, q.y * 8f));
                col.a = 1f;
                return col;
            });
            // pale spots on the cap face
            int spots = Mathf.Max(3, Mathf.RoundToInt(pl.Width * 1.8f));
            for (int i = 0; i < spots; i++)
            {
                Vector2 sc = new Vector2(x0 - 0.2f + R() * (pl.Width + 0.4f), T - 0.12f - R() * 0.3f);
                Vector2 sr = new Vector2(0.05f + R() * 0.07f, 0.03f + R() * 0.035f);
                c.Paint(q => Sdf.Ellipse(q, sc, sr), Color.Lerp(Palette.MushStem, Color.white, 0.2f).WithAlpha(0.85f), 0.008f, new Rect(sc.x - 0.2f, sc.y - 0.12f, 0.4f, 0.24f));
                if (i % 2 == 0) art.Spots.Add(new Vector3(sc.x, sc.y, 0.35f));
            }
            // rolled rim catching the light
            c.Paint(q =>
            {
                float dx = (q.x - cx) / hw;
                float rim = T - 0.62f + 0.3f * (1f - dx * dx);
                return Mathf.Abs(q.y - rim - 0.03f) - 0.03f;
            }, Color.Lerp(cap, Palette.MushStem, 0.35f).WithAlpha(0.8f), 0.01f);
            Fringe(c, x0 + 0.05f, x1 - 0.05f, T - TopFront, r, 0.35f);
            c.RimLight(new Vector2(0.03f, 0.03f), new Color(1f, 0.95f, 0.85f), 0.5f);
            art.Spots.Add(new Vector3(cx, T - 0.55f, pl.Width + 0.8f));
            return c;
        }

        // ---------------------------------------------------------------- supports down to the pitch

        static SdfCanvas BuildSupport(Spec pl)
        {
            float T = pl.T;
            const float baseY = 0.22f;          // standing on the far edge of the pitch
            var c = new SdfCanvas(SupportRect(pl), 90f);
            Color dark = new Color(0.5f, 0.5f, 0.64f), lightC = new Color(0.74f, 0.72f, 0.76f);

            if (pl.Kind == Level.Style.Mushroom)
            {
                // a thick, slightly bent stalk with a skirt and a bulbous foot
                float top = T - 0.5f;
                float sw = Mathf.Clamp(pl.Width * 0.16f, 0.26f, 0.5f);
                float bend = (Hash01(pl.SeedI) - 0.5f) * 0.5f;
                Color stem = Mul(Palette.MushStem, 0.85f);
                SdfCanvas.SdfFn stalk = q =>
                {
                    float t = Mathf.Clamp01((q.y - baseY) / (top - baseY));
                    float xc = pl.Center + bend * Mathf.Sin(t * Mathf.PI);
                    float w = sw * (1f + 0.35f * Sq(1f - t) * 1.6f);
                    float d = Mathf.Max(Mathf.Abs(q.x - xc) - w, Mathf.Max(baseY + 0.05f - q.y, q.y - top));
                    float skirtY = baseY + (top - baseY) * 0.72f;
                    d = Mathf.Min(d, Sdf.Ellipse(q, new Vector2(pl.Center + bend * Mathf.Sin(0.72f * Mathf.PI), skirtY), new Vector2(sw * 1.55f, 0.09f)));
                    d = Mathf.Min(d, Sdf.Ellipse(q, new Vector2(pl.Center, baseY + 0.12f), new Vector2(sw * 1.5f, 0.16f)));
                    return d;
                };
                c.Fill(stalk, q =>
                {
                    float t = Mathf.Clamp01((q.y - baseY) / (top - baseY));
                    float xc = pl.Center + bend * Mathf.Sin(t * Mathf.PI);
                    float sx = Mathf.Clamp((q.x - xc) / sw, -1f, 1f);
                    Color col = Mul(stem, 0.7f + 0.45f * (sx * 0.5f + 0.5f));
                    col = Mul(col, 0.9f + 0.15f * Noise.Perlin(q.x * 6f, q.y * 25f));
                    col = Mul(col, 1f - 0.45f * S01((q.y - (top - 0.5f)) / 0.5f));
                    col.a = 1f;
                    return col;
                });
                c.RimLight(new Vector2(0.03f, 0.02f), new Color(1f, 0.95f, 0.85f), 0.4f);
                return c;
            }

            float slabTop = pl.Kind == Level.Style.Terrace ? T - 0.55f : T - 0.78f;
            SdfCanvas.ColorFn shade = q =>
            {
                Color col = EnvironmentArt.BrickColor(q, 0.24f, 0.5f, pl.SeedI);
                col = Color.Lerp(Mul(col, 0.62f), dark, 0.35f);
                col = Mul(col, 1f - 0.35f * S01((q.y - (slabTop - 0.6f)) / 0.6f));   // shade under the slab
                col.a = 1f;
                return col;
            };

            if (pl.Kind == Level.Style.Terrace)
            {
                // a loggia: square piers carrying low arches (two piers on a narrow terrace)
                float[] piers = pl.Width < 2.8f ? new[] { pl.X0 + 0.4f, pl.X1 - 0.4f } : new[] { pl.X0 + 0.55f, pl.Center, pl.X1 - 0.55f };
                SdfCanvas.SdfFn loggia = q =>
                {
                    float d = 9f;
                    foreach (float px in piers)
                    {
                        d = Mathf.Min(d, Sdf.Box(q, new Vector2(px, (baseY + slabTop) * 0.5f), new Vector2(0.19f, (slabTop - baseY) * 0.5f)));
                        d = Mathf.Min(d, Sdf.Box(q, new Vector2(px, baseY + 0.1f), new Vector2(0.26f, 0.1f)));
                    }
                    float band = Mathf.Max(Mathf.Abs(q.x - pl.Center) - (pl.Width * 0.5f - 0.3f), Mathf.Max(slabTop - 0.42f - q.y, q.y - slabTop - 0.05f));
                    int k = 0;
                    while (k < piers.Length - 2 && q.x > piers[k + 1]) k++;
                    float span = (piers[k + 1] - piers[k]) * 0.5f;
                    float ac = (piers[k] + piers[k + 1]) * 0.5f;
                    band = Mathf.Max(band, -Sdf.Ellipse(q, new Vector2(ac, slabTop - 0.42f), new Vector2(span - 0.17f, 0.28f)));
                    return Mathf.Min(d, band);
                };
                c.Fill(loggia, shade);
            }
            else
            {
                // one fluted column shaft under the capital
                c.Fill(q => Mathf.Max(Mathf.Abs(q.x - pl.Center) - (0.4f - 0.03f * (q.y - baseY) / (slabTop - baseY)), Mathf.Max(baseY + 0.3f - q.y, q.y - slabTop - 0.05f)), q =>
                {
                    float sx = Mathf.Clamp((q.x - pl.Center) / 0.4f, -1f, 1f);
                    float flute = 0.5f + 0.5f * Mathf.Cos((q.x - pl.Center) * MathUtil.Tau / 0.11f);
                    Color col = Color.Lerp(dark, lightC, 0.35f + 0.45f * sx) * (0.9f + 0.12f * flute);
                    if (Mathf.Abs(Mathf.Repeat(q.y, 0.66f) - 0.33f) > 0.318f) col = Mul(col, 0.72f);
                    col = Mul(col, 1f - 0.3f * S01((q.y - (slabTop - 0.5f)) / 0.5f));
                    col.a = 1f;
                    return col;
                });
                c.Fill(q => Sdf.Box(q, new Vector2(pl.Center, baseY + 0.15f), new Vector2(0.55f, 0.15f), 0.02f), q => Color.Lerp(dark, lightC, 0.5f + 0.3f * Mathf.Clamp((q.x - pl.Center) / 0.5f, -1f, 1f)).WithAlpha(1f));
            }
            c.RimLight(new Vector2(0.03f, 0.02f), new Color(0.95f, 0.9f, 0.8f), 0.4f);
            c.EdgeBand(Vector2.up, 0.06f, Mul(Palette.Moss, 0.7f).WithAlpha(1f), p => S01((Noise.Perlin(p.x * 2.4f, p.y * 2.4f) - 0.35f) / 0.2f));
            return c;
        }

        static float Sq(float v) => v * v;
    }
}
