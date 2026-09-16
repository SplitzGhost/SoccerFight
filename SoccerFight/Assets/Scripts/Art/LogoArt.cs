using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// The SOCCERFIGHT logo, drawn from scratch: a chunky display face built from boxes (the same
    /// counters and bar weights everywhere), "SOCCER" small and cool on top, "FIGHT" big and hot
    /// below, letters bouncing a little off the baseline. Every letter gets a deep indigo keyline and
    /// a 3D slab, the faces get a gradient, a top gloss and cracks (the world is broken, after all),
    /// and a jagged burst flares up behind them. The "O" is left as an empty socket: the menu puts a
    /// real spinning ball there.
    ///
    /// Units: 1 = cap height of "FIGHT". The signed distance of all letters is sampled once into a
    /// grid, so the outline, slab and shading passes are cheap lookups. Runs on a worker thread.
    /// </summary>
    public static class LogoArt
    {
        public const float Ppu = 210f;
        public static readonly Rect Area = new Rect(-2.9f, -0.72f, 5.8f, 3.32f);
        /// <summary>The whole logo leans up to the right by this much (degrees).</summary>
        public const float TiltDeg = 3f;
        /// <summary>Where the ball sits (logo units) and its radius.</summary>
        public static Vector2 BallCenter { get; private set; }
        public static float BallRadius { get; private set; }

        const float T = 0.3f;   // stem width
        const float B = 0.22f;  // bar height
        const float Gap = 0.075f;

        static readonly Color Ink = new Color(0.07f, 0.04f, 0.17f, 1f);
        static readonly Color Slab = new Color(0.16f, 0.07f, 0.3f, 1f);

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

        const float WS = 0.8f, WO = 0.9f, WC = 0.8f, WE = 0.66f, WR = 0.82f, WF = 0.64f, WI = T, WG = 0.88f, WH = 0.84f, WT = 0.78f;

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
            const string top = "SOCCER", bottom = "FIGHT";
            float[] topRot = { -5f, 3f, -3f, 4f, -2f, 5f }, topDy = { 0.03f, -0.01f, 0.04f, 0f, 0.03f, -0.01f };
            float[] botRot = { -4f, 5f, -3f, 4f, -5f }, botDy = { 0f, 0.05f, -0.02f, 0.04f, 0.01f };
            const float topScale = 0.66f, topY = 1.2f;
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
            Word(bottom, 1f, 0f, botRot, botDy, false);
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

        // ------------------------------------------------------------------ burst behind the words

        static float BurstSdf(Vector2 p)
        {
            Vector2 c = new Vector2(0.1f, 0.95f);
            Vector2 q = p - c;
            float ang = Mathf.Atan2(q.y, q.x);
            // uneven spikes, like a torn explosion
            float spikes = 0f;
            float a = ang * 7f;
            spikes += Mathf.Pow(Mathf.Abs(Mathf.Cos(a)), 6f) * 0.34f;
            spikes += Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 11f + 1.3f)), 10f) * 0.22f;
            spikes += (Noise.Perlin(ang * 3f + 10f, 0.5f) - 0.5f) * 0.18f;
            float rx = 1.95f * (1f + spikes * 0.55f), ry = 0.95f * (1f + spikes);
            float e = new Vector2(q.x / rx, q.y / ry).magnitude;
            return (e - 1f) * Mathf.Min(rx, ry) * 0.9f;
        }

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
            float px = 1f / Ppu;

            // 1) burst: keyline, hot gradient, a lighter inner flare
            Rect burstBounds = Area;
            var burst = new Field(Area, Ppu);
            burst.Stamp(BurstSdf, Area);
            SdfCanvas.SdfFn burstAt = burst.At;
            c.Fill(p => burstAt(p) - 0.05f, Ink, 0f, burstBounds);
            c.Fill(burstAt, p =>
            {
                float k = MathUtil.Smooth01((p.y + 0.3f) / 2.2f);
                Color col = Color.Lerp(new Color(0.92f, 0.16f, 0.36f), new Color(1f, 0.42f, 0.2f), k);
                float streak = Noise.Perlin(Mathf.Atan2(p.y - 0.95f, p.x - 0.1f) * 5f, 3.3f);
                return Color.Lerp(col, new Color(1f, 0.62f, 0.3f), streak * 0.35f);
            }, 0f, burstBounds);
            c.Paint(p => burstAt(p) + 0.32f, new Color(1f, 0.72f, 0.32f, 0.55f), 0.25f, burstBounds);
            c.Paint(p => burstAt(p) + 0.62f, new Color(1f, 0.88f, 0.5f, 0.35f), 0.35f, burstBounds);

            // 2) slab: the letters pushed down-right, drawn as one deep block
            Vector2 depth = new Vector2(0.045f, -0.12f);
            const int steps = 7;
            SdfCanvas.SdfFn slab = p =>
            {
                float d = 10f;
                for (int i = 1; i <= steps; i++) d = Mathf.Min(d, field.At(p - depth * (i / (float)steps)));
                return d;
            };
            Rect letters = new Rect(-2.2f, -0.45f, 4.5f, 2.75f);
            c.Fill(p => slab(p) - 0.085f, Ink, 0f, letters);
            c.Fill(p => slab(p) - 0.045f, p => Color.Lerp(Slab, new Color(0.3f, 0.12f, 0.45f), MathUtil.Smooth01((p.y + 0.2f) / 2f)), 0f, letters);

            // 3) keyline around the faces
            c.Fill(p => field.At(p) - 0.075f, Ink, 0f, letters);

            // 4) faces: SOCCER cool white-blue, FIGHT yellow to orange
            c.Fill(p => field.At(p), p =>
            {
                bool top = topField.At(p) < 0.02f;
                if (top)
                {
                    float k = MathUtil.Smooth01((p.y - 1.15f) / 0.7f);
                    return Color.Lerp(new Color(0.55f, 0.82f, 1f), Color.white, k);
                }
                float f = MathUtil.Smooth01((p.y + 0.05f) / 1.05f);
                Color low = new Color(1f, 0.42f, 0.12f), mid = new Color(1f, 0.72f, 0.16f), high = new Color(1f, 0.94f, 0.45f);
                return f < 0.5f ? Color.Lerp(low, mid, f * 2f) : Color.Lerp(mid, high, (f - 0.5f) * 2f);
            }, 0f, letters);

            // 5) shading: a gloss band along the top edges, a shadow along the bottom edges
            c.Paint(p =>
            {
                float d = field.At(p);
                if (d > 0f) return Color.clear;
                float up = field.At(p + new Vector2(0f, 0.07f));
                float down = field.At(p - new Vector2(0f, 0.06f));
                float gloss = MathUtil.Smooth01(up / 0.02f) * 0.75f;
                float shade = MathUtil.Smooth01(down / 0.02f) * 0.35f;
                if (gloss > 0.01f) return new Color(1f, 1f, 0.96f, gloss);
                if (shade > 0.01f) return new Color(0.55f, 0.18f, 0.2f, shade);
                return Color.clear;
            }, letters);

            // 6) cracks through FIGHT: dark jagged lines, clipped to the faces
            var rng = new System.Random(7);
            float R() => (float)rng.NextDouble();
            foreach (var pl in placed)
            {
                if (pl.Top || pl.C == 'O') continue;
                int n = 1 + rng.Next(2);
                for (int k = 0; k < n; k++)
                {
                    Vector2 a = Tilt(pl.Origin + new Vector2(R() * pl.Width, 0.2f + R() * 0.6f) * pl.Scale);
                    Vector2 dir = MathUtil.Dir(R() * 360f);
                    var pts = new Vector2[4];
                    pts[0] = a;
                    for (int s = 1; s < pts.Length; s++) pts[s] = pts[s - 1] + MathUtil.Rotate(dir, (R() - 0.5f) * 70f) * (0.07f + R() * 0.07f);
                    SdfCanvas.SdfFn crack = p =>
                    {
                        if (field.At(p) > -0.01f) return 1f;
                        float d = 10f;
                        for (int s = 1; s < pts.Length; s++)
                            d = Mathf.Min(d, Sdf.Tapered(p, pts[s - 1], 0.012f * (1f - s * 0.22f) + 0.004f, pts[s], 0.004f));
                        return d;
                    };
                    Rect cb = new Rect(a.x - 0.35f, a.y - 0.35f, 0.7f, 0.7f);
                    c.Paint(crack, new Color(0.45f, 0.1f, 0.12f, 0.85f), 0f, cb);
                    c.Paint(p => crack(p + new Vector2(0.008f, -0.01f)), new Color(1f, 0.98f, 0.8f, 0.5f), 0f, cb);
                }
            }

            // 7) the ball socket: a dark disc the spinning ball will cover
            Vector2 bcn = BallCenter;
            float br = BallRadius;
            c.Fill(p => Sdf.Circle(p, bcn, br), new Color(0.12f, 0.08f, 0.24f), 0f, new Rect(bcn.x - br - 0.1f, bcn.y - br - 0.1f, br * 2f + 0.2f, br * 2f + 0.2f));

            // 8) ribbon under FIGHT for the tagline
            SdfCanvas.SdfFn ribbon = p =>
            {
                Vector2 q = Untilt(p);
                q.y -= q.x * q.x * 0.03f;
                float body = Sdf.Box(q, new Vector2(0.05f, -0.34f), new Vector2(1.55f, 0.15f), 0.02f);
                float tailL = Sdf.Box(q, new Vector2(-1.62f, -0.44f), new Vector2(0.3f, 0.12f), 0.01f);
                float tailR = Sdf.Box(q, new Vector2(1.72f, -0.44f), new Vector2(0.3f, 0.12f), 0.01f);
                float notchL = Sdf.Triangle(q, new Vector2(-1.93f, -0.3f), new Vector2(-1.93f, -0.58f), new Vector2(-1.8f, -0.44f));
                float notchR = Sdf.Triangle(q, new Vector2(2.03f, -0.3f), new Vector2(2.03f, -0.58f), new Vector2(1.9f, -0.44f));
                float tails = Sdf.Subtract(Sdf.Subtract(Mathf.Min(tailL, tailR), notchL), notchR);
                return Mathf.Min(body, tails);
            };
            Rect rbnd = new Rect(-2.2f, -0.72f, 4.5f, 0.75f);
            c.Fill(p => ribbon(p) - 0.04f, Ink, 0f, rbnd);
            c.Fill(ribbon, p =>
            {
                Vector2 q = Untilt(p);
                bool tail = q.x < -1.5f || q.x > 1.6f;
                Color col = tail ? new Color(0.22f, 0.12f, 0.42f) : new Color(0.33f, 0.2f, 0.62f);
                return Color.Lerp(col * 0.85f, col, MathUtil.Smooth01((q.y - q.x * q.x * 0.03f + 0.5f) / 0.3f));
            }, 0f, rbnd);
            c.Paint(p =>
            {
                Vector2 q = Untilt(p);
                float bend = q.x * q.x * 0.03f;
                return Sdf.Box(new Vector2(q.x, q.y - bend), new Vector2(0.05f, -0.24f), new Vector2(1.5f, 0.012f));
            }, new Color(0.62f, 0.5f, 0.95f, 0.8f), 0f, rbnd);

            return c;
        }

        /// <summary>Tagline position under FIGHT (logo units) and its tilt.</summary>
        public static Vector2 TaglineCenter => Tilt(new Vector2(0.05f, -0.34f));
    }
}
