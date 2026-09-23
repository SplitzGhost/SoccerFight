using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// The SPORTFIGHTER logo, drawn from scratch in the look of the ruins it belongs to: a chunky
    /// display face built from boxes (the same counters and bar weights everywhere), "SPORT" small
    /// on top, "FIGHTER" big below. The letters are moonstone blocks: a dark carved slab, pale faces
    /// with grain, a lit bevel, moss creeping over some top edges and cracks with crystal light in
    /// them. The "O" is left as an empty socket: the menu puts a real spinning ball there.
    ///
    /// Units: 1 = the cap height FIGHTER would have at full size (it is set at 0.86). The signed distance of all letters is sampled once into a
    /// grid, so the outline, slab and shading passes are cheap lookups. Runs on a worker thread.
    /// </summary>
    public static class LogoArt
    {
        public const float Ppu = 210f;
        public static readonly Rect Area = new Rect(-2.45f, -0.36f, 4.95f, 2.6f);
        /// <summary>The whole logo leans up to the right by this much (degrees; the stone letters sit level).</summary>
        public const float TiltDeg = 0f;
        /// <summary>Where the ball sits (logo units) and its radius.</summary>
        public static Vector2 BallCenter { get; private set; }
        public static float BallRadius { get; private set; }

        const float T = 0.3f;   // stem width
        const float B = 0.22f;  // bar height
        const float Gap = 0.075f;

        static readonly Color Ink = new Color(0.015f, 0.04f, 0.06f, 1f);
        static readonly Color Slab = new Color(0.04f, 0.11f, 0.15f, 1f);
        static readonly Color SlabLight = new Color(0.09f, 0.2f, 0.25f, 1f);

        static Color Mul(Color c, float f) { c.r *= f; c.g *= f; c.b *= f; return c; }

        delegate float Glyph(Vector2 q);

        struct Placed
        {
            public char C;
            public Glyph G;
            public Vector2 Origin;   // bottom-left of the glyph box
            public float Scale, Rot, Width;
            public bool Top;
            public Rect Bounds;
        }

        // ------------------------------------------------------------------ distance grid

        sealed class Field
        {
            public readonly float[] D;
            public readonly int W, H;
            readonly Vector2 origin;
            readonly float ppu;

            public Field(Rect units, float ppu)
            {
                this.ppu = ppu;
                origin = units.min;
                W = Mathf.CeilToInt(units.width * ppu);
                H = Mathf.CeilToInt(units.height * ppu);
                D = new float[W * H];
                for (int i = 0; i < D.Length; i++) D[i] = 10f;
            }

            public void Stamp(System.Func<Vector2, float> f, Rect b)
            {
                int x0 = Mathf.Clamp(Mathf.FloorToInt((b.xMin - origin.x) * ppu), 0, W);
                int x1 = Mathf.Clamp(Mathf.CeilToInt((b.xMax - origin.x) * ppu), 0, W);
                int y0 = Mathf.Clamp(Mathf.FloorToInt((b.yMin - origin.y) * ppu), 0, H);
                int y1 = Mathf.Clamp(Mathf.CeilToInt((b.yMax - origin.y) * ppu), 0, H);
                Par.For(y0, y1, y =>
                {
                    for (int x = x0; x < x1; x++)
                    {
                        Vector2 p = origin + new Vector2((x + 0.5f) / ppu, (y + 0.5f) / ppu);
                        float d = f(p);
                        int i = y * W + x;
                        if (d < D[i]) D[i] = d;
                    }
                });
            }

            /// <summary>Bilinear lookup (exact at pixel centres).</summary>
            public float At(Vector2 p)
            {
                float fx = (p.x - origin.x) * ppu - 0.5f, fy = (p.y - origin.y) * ppu - 0.5f;
                int x = Mathf.FloorToInt(fx), y = Mathf.FloorToInt(fy);
                float tx = fx - x, ty = fy - y;
                return Mathf.Lerp(Mathf.Lerp(Get(x, y), Get(x + 1, y), tx), Mathf.Lerp(Get(x, y + 1), Get(x + 1, y + 1), tx), ty);
            }

            float Get(int x, int y) => (x < 0 || y < 0 || x >= W || y >= H) ? 10f : D[y * W + x];
        }

        // ------------------------------------------------------------------ glyphs (origin bottom-left, cap height 1)

        static float Bar(Vector2 q, float x0, float y0, float x1, float y1, float r = 0.035f)
            => Sdf.Box(q, new Vector2((x0 + x1) * 0.5f, (y0 + y1) * 0.5f), new Vector2((x1 - x0) * 0.5f, (y1 - y0) * 0.5f), r);

        static float Ring(Vector2 q, float x0, float y0, float x1, float y1, float r, float ix0, float iy0, float ix1, float iy1, float ir)
            => Mathf.Max(Bar(q, x0, y0, x1, y1, r), -Bar(q, ix0, iy0, ix1, iy1, ir));

        const float WS = 0.8f, WO = 0.9f, WC = 0.8f, WE = 0.66f, WR = 0.82f, WF = 0.64f, WI = T, WG = 0.88f, WH = 0.84f, WT = 0.78f, WP = 0.78f;

        static float GlyphS(Vector2 q)
        {
            float w = WS;
            float top = Ring(q, 0f, 0.39f, w, 1f, 0.22f, T, 0.39f + B, w - T, 1f - B, 0.05f);
            top = Mathf.Max(top, -Bar(q, w - T - 0.02f, 0.3f, w + 0.1f, 0.7f, 0f));
            float bot = Ring(q, 0f, 0f, w, 0.61f, 0.22f, T, B, w - T, 0.61f - B, 0.05f);
            bot = Mathf.Max(bot, -Bar(q, -0.1f, 0.3f, T + 0.02f, 0.7f, 0f));
            return Mathf.Min(top, bot);
        }

        static float GlyphC(Vector2 q)
        {
            float w = WC;
            float ring = Ring(q, 0f, 0f, w, 1f, 0.34f, T, B, w - T * 0.9f, 1f - B, 0.1f);
            return Mathf.Max(ring, -Bar(q, w * 0.6f, 0.3f, w + 0.1f, 0.7f, 0f));
        }

        static float GlyphE(Vector2 q)
            => Mathf.Min(Mathf.Min(Bar(q, 0f, 0f, T, 1f), Bar(q, 0f, 1f - B, WE, 1f)),
                         Mathf.Min(Bar(q, 0f, 0.39f, WE * 0.9f, 0.61f), Bar(q, 0f, 0f, WE, B)));

        static float GlyphR(Vector2 q)
        {
            float w = WR;
            float stem = Bar(q, 0f, 0f, T, 1f);
            float bowl = Ring(q, 0f, 0.36f, w * 0.95f, 1f, 0.22f, T, 0.36f + B, w * 0.95f - T, 1f - B, 0.05f);
            Vector2 a = new Vector2(w * 0.42f, 0.5f), b = new Vector2(w * 1.02f, -0.06f);
            float leg = Sdf.Box(q, (a + b) * 0.5f, new Vector2((b - a).magnitude * 0.5f, T * 0.5f), 0.02f, MathUtil.Angle(b - a));
            leg = Mathf.Max(leg, -q.y);
            leg = Mathf.Max(leg, q.y - 0.46f);
            return Mathf.Min(Mathf.Min(stem, bowl), leg);
        }

        static float GlyphP(Vector2 q)
        {
            float stem = Bar(q, 0f, 0f, T, 1f);
            float bowl = Ring(q, 0f, 0.36f, WP, 1f, 0.22f, T, 0.36f + B, WP - T, 1f - B, 0.05f);
            return Mathf.Min(stem, bowl);
        }

        static float GlyphF(Vector2 q)
            => Mathf.Min(Mathf.Min(Bar(q, 0f, 0f, T, 1f), Bar(q, 0f, 1f - B, WF, 1f)), Bar(q, 0f, 0.37f, WF * 0.88f, 0.37f + B));

        static float GlyphI(Vector2 q) => Bar(q, 0f, 0f, T, 1f);

        static float GlyphG(Vector2 q)
        {
            float w = WG;
            float ring = Ring(q, 0f, 0f, w, 1f, 0.36f, T, B, w - T, 1f - B, 0.1f);
            ring = Mathf.Max(ring, -Bar(q, w * 0.55f, 0.57f, w + 0.1f, 1f - B - 0.03f, 0f));
            float tongue = Bar(q, w * 0.46f, 0.37f, w - 0.01f, 0.57f);
            return Mathf.Min(ring, tongue);
        }

        static float GlyphH(Vector2 q)
            => Mathf.Min(Mathf.Min(Bar(q, 0f, 0f, T, 1f), Bar(q, WH - T, 0f, WH, 1f)), Bar(q, 0f, 0.39f, WH, 0.61f));

        static float GlyphT(Vector2 q)
            => Mathf.Min(Bar(q, 0f, 1f - B, WT, 1f), Bar(q, WT * 0.5f - T * 0.5f, 0f, WT * 0.5f + T * 0.5f, 1f));

        /// <summary>The socket for the ball: a circle the size of the letter.</summary>
        static float GlyphO(Vector2 q) => Sdf.Circle(q, new Vector2(WO * 0.5f, 0.5f), 0.53f);

        static Glyph GlyphFor(char c, out float width)
        {
            switch (c)
            {
                case 'S': width = WS; return GlyphS;
                case 'O': width = WO; return GlyphO;
                case 'C': width = WC; return GlyphC;
                case 'E': width = WE; return GlyphE;
                case 'R': width = WR; return GlyphR;
                case 'P': width = WP; return GlyphP;
                case 'F': width = WF; return GlyphF;
                case 'I': width = WI; return GlyphI;
                case 'G': width = WG; return GlyphG;
                case 'H': width = WH; return GlyphH;
                default: width = WT; return GlyphT;
            }
        }

        // ------------------------------------------------------------------ layout

        static Placed[] Layout()
        {
            const string top = "SPORT", bottom = "FIGHTER";
            // settled, weathered blocks: barely off true
            float[] topRot = { -1.2f, 0.8f, 0f, -0.6f, 1f }, topDy = { 0.01f, -0.01f, 0f, 0.01f, 0f };
            float[] botRot = { -1f, 1.4f, -0.6f, 0.9f, -1.2f, 0.7f, -0.9f }, botDy = { 0f, 0.02f, -0.01f, 0.01f, 0f, 0.015f, -0.01f };
            // FIGHTER is two letters longer than FIGHT was: set a little smaller to keep the logo's width
            const float topScale = 0.66f, botScale = 0.86f, topY = 1.06f;
            var list = new System.Collections.Generic.List<Placed>();

            void Word(string word, float scale, float y, float[] rot, float[] dy, bool isTop)
            {
                float total = 0f;
                foreach (char ch in word) { GlyphFor(ch, out float w); total += w + Gap; }
                total -= Gap;
                float x = -total * 0.5f * scale;
                for (int i = 0; i < word.Length; i++)
                {
                    var g = GlyphFor(word[i], out float w);
                    var pl = new Placed
                    {
                        C = word[i], G = g, Origin = new Vector2(x, y + dy[i] * scale), Scale = scale, Rot = rot[i], Width = w, Top = isTop,
                    };
                    pl.Bounds = new Rect(pl.Origin.x - 0.45f, pl.Origin.y - 0.45f, w * scale + 0.9f, scale + 0.9f);
                    list.Add(pl);
                    x += (w + Gap) * scale;
                }
            }
            Word(top, topScale, topY, topRot, topDy, true);
            Word(bottom, botScale, 0f, botRot, botDy, false);
            return list.ToArray();
        }

        /// <summary>Distance to one placed letter (rotated around its own centre).</summary>
        static float Eval(in Placed pl, Vector2 p)
        {
            Vector2 centre = pl.Origin + new Vector2(pl.Width * 0.5f, 0.5f) * pl.Scale;
            Vector2 q = MathUtil.Rotate(p - centre, -pl.Rot) + centre;
            return pl.G((q - pl.Origin) / pl.Scale) * pl.Scale;
        }

        /// <summary>Whole-logo tilt: rising slightly to the right (Untilt maps a canvas point into letter space).</summary>
        static Vector2 Untilt(Vector2 p) => MathUtil.Rotate(p, -TiltDeg);
        static Vector2 Tilt(Vector2 p) => MathUtil.Rotate(p, TiltDeg);

        // ------------------------------------------------------------------ build

        public static SdfCanvas Build()
        {
            var placed = Layout();
            var field = new Field(Area, Ppu);
            var topField = new Field(Area, Ppu);
            foreach (var pl in placed)
            {
                var copy = pl;
                if (copy.C == 'O')
                {
                    Vector2 centre = copy.Origin + new Vector2(copy.Width * 0.5f, 0.5f) * copy.Scale;
                    BallCenter = Tilt(centre);
                    BallRadius = 0.53f * copy.Scale;
                }
                // bounds must be rotated with the tilt: stamp a generous box
                Rect b = copy.Bounds;
                Vector2 bc = Tilt(b.center);
                Rect rb = new Rect(bc.x - b.width * 0.5f - 0.1f, bc.y - b.height * 0.5f - 0.1f, b.width + 0.2f, b.height + 0.2f);
                field.Stamp(p => Eval(copy, Untilt(p)), rb);
                if (copy.Top) topField.Stamp(p => Eval(copy, Untilt(p)), rb);
            }

            var c = new SdfCanvas(Area, Ppu);
            Rect letters = new Rect(-2.42f, -0.34f, 4.88f, 2.56f);

            // 1) a soft dark halo so the letters hold against the bright moon behind them
            c.Fill(p => field.At(p) - 0.16f, new Color(0.01f, 0.03f, 0.05f, 0.45f), 0.14f, letters);

            // 2) slab: the letters pushed straight down, carved from the same dark stone
            Vector2 depth = new Vector2(0.02f, -0.1f);
            const int steps = 6;
            SdfCanvas.SdfFn slab = p =>
            {
                float d = 10f;
                for (int i = 1; i <= steps; i++) d = Mathf.Min(d, field.At(p - depth * (i / (float)steps)));
                return d;
            };
            c.Fill(p => slab(p) - 0.05f, Ink, 0f, letters);
            c.Fill(p => slab(p) - 0.022f, p => Color.Lerp(Slab, SlabLight, MathUtil.Smooth01((p.y + 0.2f) / 2f)), 0f, letters);

            // 3) keyline around the faces
            c.Fill(p => field.At(p) - 0.042f, Ink, 0f, letters);

            // 4) faces: moonstone — SPORT cool and pale, FIGHTER bright with a teal foot; grain and streaks
            c.Fill(p => field.At(p), p =>
            {
                bool top = topField.At(p) < 0.02f;
                Color col;
                if (top)
                {
                    float k = MathUtil.Smooth01((p.y - 1.2f) / 0.62f);
                    col = Color.Lerp(new Color(0.38f, 0.72f, 0.8f), new Color(0.86f, 0.98f, 1f), k);
                }
                else
                {
                    float f = MathUtil.Smooth01((p.y + 0.02f) / 1.02f);
                    Color low = new Color(0.27f, 0.55f, 0.63f), mid = new Color(0.72f, 0.9f, 0.94f), high = new Color(0.97f, 1f, 1f);
                    col = f < 0.45f ? Color.Lerp(low, mid, f / 0.45f) : Color.Lerp(mid, high, (f - 0.45f) / 0.55f);
                }
                float grain = Noise.Perlin(p.x * 34f, p.y * 34f);
                float streak = Noise.Perlin(p.x * 60f, p.y * 3f);
                col = Mul(col, 0.93f + 0.08f * grain - 0.05f * MathUtil.Smooth01((streak - 0.62f) / 0.1f));
                col.a = 1f;
                return col;
            }, 0f, letters);

            // 5) bevel: a lit rim along the top edges, shade along the bottom edges
            c.Paint(p =>
            {
                float d = field.At(p);
                if (d > 0f) return Color.clear;
                float up = field.At(p + new Vector2(0f, 0.05f));
                float down = field.At(p - new Vector2(0f, 0.05f));
                float gloss = MathUtil.Smooth01(up / 0.02f) * 0.7f;
                float shade = MathUtil.Smooth01(down / 0.02f) * 0.4f;
                if (gloss > 0.01f) return new Color(1f, 1f, 1f, gloss);
                if (shade > 0.01f) return new Color(0.1f, 0.25f, 0.32f, shade);
                return Color.clear;
            }, letters);

            // 6) moss creeping over some of the top edges
            c.Paint(p =>
            {
                float d = field.At(p);
                if (d > 0f) return Color.clear;
                float up = field.At(p + new Vector2(0f, 0.09f));
                if (up < -0.01f) return Color.clear;
                float patch = Noise.Perlin(p.x * 3.4f + 7f, 1.3f);
                float lump = Noise.Perlin(p.x * 22f, p.y * 22f);
                float edge = MathUtil.Smooth01((up + 0.01f) / 0.03f) * MathUtil.Smooth01((patch - 0.63f) / 0.08f);
                float a = edge * MathUtil.Smooth01((lump - 0.35f) / 0.15f);
                return Color.Lerp(Palette.Moss, Palette.IvyLight, lump).WithAlpha(a * 0.95f);
            }, letters);

            // 7) cracks through FIGHTER with crystal light inside them
            var rng = new System.Random(7);
            float R() => (float)rng.NextDouble();
            foreach (var pl in placed)
            {
                if (pl.Top || pl.C == 'O') continue;
                int n = 1 + rng.Next(2);
                for (int k = 0; k < n; k++)
                {
                    Vector2 a = Tilt(pl.Origin + new Vector2(R() * pl.Width, 0.15f + R() * 0.7f) * pl.Scale);
                    Vector2 dir = MathUtil.Dir(R() * 360f);
                    var pts = new Vector2[5];
                    pts[0] = a;
                    for (int s = 1; s < pts.Length; s++) pts[s] = pts[s - 1] + MathUtil.Rotate(dir, (R() - 0.5f) * 70f) * (0.06f + R() * 0.07f);
                    SdfCanvas.SdfFn crack = p =>
                    {
                        if (field.At(p) > -0.008f) return 1f;
                        float d = 10f;
                        for (int s = 1; s < pts.Length; s++)
                            d = Mathf.Min(d, Sdf.Tapered(p, pts[s - 1], 0.016f * (1f - s * 0.18f) + 0.004f, pts[s], 0.004f));
                        return d;
                    };
                    Rect cb = new Rect(a.x - 0.4f, a.y - 0.4f, 0.8f, 0.8f);
                    c.Paint(crack, new Color(0.03f, 0.1f, 0.13f, 0.95f), 0f, cb);
                    c.Paint(p => crack(p) + 0.006f, new Color(0.55f, 0.97f, 1f, 1f), 0.004f, cb);
                    c.Paint(p => crack(p) - 0.018f, new Color(0.6f, 0.95f, 1f, 0.22f), 0.02f, cb);
                }
            }

            // 8) the ball socket: a dark disc the spinning ball will cover
            Vector2 bcn = BallCenter;
            float br = BallRadius;
            c.Fill(p => Sdf.Circle(p, bcn, br), new Color(0.03f, 0.08f, 0.11f), 0f, new Rect(bcn.x - br - 0.1f, bcn.y - br - 0.1f, br * 2f + 0.2f, br * 2f + 0.2f));
            return c;
        }

        /// <summary>Where the tagline goes under FIGHTER (logo units).</summary>
        public static Vector2 TaglineCenter => Tilt(new Vector2(0.05f, -0.52f));
    }
}
