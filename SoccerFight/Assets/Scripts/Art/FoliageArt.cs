using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// All vegetation variants, generated in parallel and packed into one atlas. Each variant's canvas
    /// is authored with its pivot at (0,0): rooted plants grow up from it, hanging ones hang down.
    /// Glow spots (blossoms, mushroom caps, crystals) are recorded for the emissive glow meshes.
    /// </summary>
    public static class FoliageArt
    {
        public enum Mode { Rooted, Hanging, Static }

        public struct GlowSpot
        {
            public Vector2 Pos;
            public float Size;
            public Color Color;
        }

        public sealed class Variant
        {
            public string Name;
            public Mode Mode;
            public float Sway = 1f;
            public AtlasBuilder.Entry Entry;
            public readonly List<GlowSpot> Glows = new List<GlowSpot>();
            public Rect Units => Entry.Units;
            public Rect Uv => Entry.Uv;
        }

        public static Texture2D Atlas;
        public static Variant[] Grass, TallGrass, Ferns, Flowers, Mushrooms, Bushes, Ivy, Moss, Canopies, FgLeaves, Banners, Clover, Crystals, Stones, Reeds, Roots;
        public static Variant Glow;

        static AtlasBuilder builder;

        static Variant Make(string name, Mode mode, float sway, Func<Variant, SdfCanvas> build)
        {
            var v = new Variant { Name = name, Mode = mode, Sway = sway };
            v.Entry = builder.Add(name, () => build(v));
            return v;
        }

        static Variant[] Many(string name, int count, Mode mode, float sway, Func<Variant, int, SdfCanvas> build)
        {
            var arr = new Variant[count];
            for (int i = 0; i < count; i++)
            {
                int k = i;
                arr[i] = Make(name + k, mode, sway, v => build(v, k));
            }
            return arr;
        }

        public static void Begin()
        {
            builder = new AtlasBuilder();
            Grass = Many("grass", 5, Mode.Rooted, 1f, (v, i) => GrassTuft(101 + i * 17, 0.28f + i * 0.09f, 9 + i));
            TallGrass = Many("tallgrass", 3, Mode.Rooted, 1.1f, (v, i) => GrassTuft(301 + i * 13, 0.85f + i * 0.2f, 7, true));
            Reeds = Many("reeds", 2, Mode.Rooted, 1f, (v, i) => ReedClump(707 + i * 11));
            Ferns = Many("fern", 3, Mode.Rooted, 0.8f, (v, i) => Fern(401 + i * 7, 0.75f + i * 0.18f));
            Flowers = Many("flower", 3, Mode.Rooted, 1f, (v, i) => FlowerClump(v, 501 + i * 19, i == 2));
            Mushrooms = Many("mushroom", 3, Mode.Rooted, 0.25f, (v, i) => MushroomCluster(v, 601 + i * 23));
            Bushes = Many("bush", 3, Mode.Rooted, 0.35f, (v, i) => Bush(801 + i * 29, 1.1f + i * 0.25f));
            Ivy = Many("ivy", 4, Mode.Hanging, 0.7f, (v, i) => IvyStrand(901 + i * 31, 0.9f + i * 0.45f));
            Moss = Many("moss", 2, Mode.Hanging, 0.9f, (v, i) => MossCurtain(1001 + i * 37));
            Roots = Many("roots", 3, Mode.Hanging, 0.55f, (v, i) => HangingRoots(1701 + i * 67, 0.6f + i * 0.35f));
            Canopies = Many("canopy", 4, Mode.Rooted, 0.22f, (v, i) => Canopy(1101 + i * 41, 3.2f + i * 0.5f));
            FgLeaves = Many("fgleaf", 3, Mode.Rooted, 0.5f, (v, i) => ForegroundFrond(1201 + i * 43));
            Banners = Many("banner", 2, Mode.Hanging, 1.3f, (v, i) => Banner(1301 + i * 47, i));
            Clover = Many("clover", 2, Mode.Rooted, 0.6f, (v, i) => CloverPatch(1401 + i * 53));
            Crystals = Many("crystal", 2, Mode.Static, 0f, (v, i) => CrystalCluster(v, 1501 + i * 59));
            Stones = Many("stone", 3, Mode.Static, 0f, (v, i) => Pebbles(1601 + i * 61));
            Glow = Make("glow", Mode.Static, 0f, v => GlowBlob());
            builder.Start(2048);
        }

        public static void End()
        {
            builder.Complete("FoliageAtlas");
            Atlas = builder.Texture;
        }

        // ------------------------------------------------------------------ helpers

        static float Rnd(System.Random r) => (float)r.NextDouble();

        static SdfCanvas.ColorFn Vertical(float y0, float y1, Color bottom, Color top)
            => p => Color.Lerp(bottom, top, MathUtil.Smooth01((p.y - y0) / Mathf.Max(1e-4f, y1 - y0)));

        /// <summary>Curved tapered blade from base to tip, colored along its height.</summary>
        static void Blade(SdfCanvas c, Vector2 b, Vector2 tip, float curve, float w0, Color bottom, Color top)
        {
            Vector2 mid = Vector2.Lerp(b, tip, 0.5f) + new Vector2(-(tip.x - b.x) * curve, 0f);
            float y0 = Mathf.Min(b.y, tip.y), y1 = Mathf.Max(b.y, tip.y);
            var col = Vertical(y0, y1, bottom, top);
            Rect bounds = Rect.MinMaxRect(Mathf.Min(b.x, Mathf.Min(mid.x, tip.x)) - w0, y0 - w0, Mathf.Max(b.x, Mathf.Max(mid.x, tip.x)) + w0, y1 + w0);
            c.Fill(p => Mathf.Min(Sdf.Tapered(p, b, w0, mid, w0 * 0.62f), Sdf.Tapered(p, mid, w0 * 0.62f, tip, 0.0025f)), col, 0f, bounds);
        }

        /// <summary>Pointed leaf (lens shape) with a lighter midrib.</summary>
        internal static void Leaf(SdfCanvas c, Vector2 center, float angleDeg, float length, float width, Color col, float midrib = 0.25f)
        {
            Vector2 dir = MathUtil.Dir(angleDeg);
            Vector2 a = center - dir * length * 0.5f, b = center + dir * length * 0.5f;
            float r = (length * length * 0.25f + width * width) / (2f * width);
            Vector2 n = new Vector2(-dir.y, dir.x);
            Vector2 c1 = center + n * (r - width), c2 = center - n * (r - width);
            Rect bounds = new Rect(center.x - length, center.y - length, length * 2f, length * 2f);
            SdfCanvas.SdfFn shape = p => Mathf.Max(Sdf.Circle(p, c1, r), Sdf.Circle(p, c2, r));
            c.Fill(shape, col, 0f, bounds);
            if (midrib > 0f)
                c.Paint(p => Sdf.Capsule(p, a + dir * length * 0.1f, b - dir * length * 0.15f, width * 0.12f),
                    Color.Lerp(col, Color.white, 0.35f).WithAlpha(midrib), 0.004f, bounds);
        }

        // ------------------------------------------------------------------ grasses

        static SdfCanvas GrassTuft(int seed, float height, int blades, bool tall = false)
        {
            var r = new System.Random(seed);
            var c = new SdfCanvas(new Rect(-0.42f, 0f, 0.84f, height + 0.06f), tall ? 170f : 220f);
            for (int k = 0; k < blades; k++)
            {
                float bx = (Rnd(r) - 0.5f) * (tall ? 0.18f : 0.24f);
                float h = height * (0.45f + Rnd(r) * 0.55f);
                float lean = (Rnd(r) - 0.5f) * 0.6f * h + bx * 1.1f;
                float w0 = (tall ? 0.018f : 0.024f) + Rnd(r) * 0.012f;
                float shade = k / (float)blades;
                Color top = Color.Lerp(Palette.FolMid, Palette.FolLight, 0.35f + 0.65f * Rnd(r) * shade);
                if (tall) top = Color.Lerp(top, new Color(0.45f, 0.62f, 0.45f), 0.25f);
                Blade(c, new Vector2(bx, 0f), new Vector2(bx + lean, h), 0.28f, w0, Palette.FolDark, top);
                if (tall && Rnd(r) > 0.45f)
                {
                    Vector2 tip = new Vector2(bx + lean, h);
                    float ang = 90f - lean * 30f;
                    Leaf(c, tip + MathUtil.Dir(ang) * 0.02f, ang, 0.09f, 0.022f, new Color(0.5f, 0.68f, 0.52f), 0f);
                }
            }
            c.RimLight(new Vector2(0.012f, 0.004f), Palette.FolRim, 0.55f);
            return c;
        }

        static SdfCanvas ReedClump(int seed)
        {
            var r = new System.Random(seed);
            var c = new SdfCanvas(new Rect(-0.5f, 0f, 1f, 1.55f), 160f);
            int n = 6 + r.Next(4);
            for (int k = 0; k < n; k++)
            {
                float bx = (Rnd(r) - 0.5f) * 0.3f;
                float h = 0.9f + Rnd(r) * 0.55f;
                float lean = (Rnd(r) - 0.5f) * 0.3f + bx * 0.6f;
                Blade(c, new Vector2(bx, 0f), new Vector2(bx + lean, h), 0.15f, 0.014f, Palette.FolDark, Color.Lerp(Palette.FolMid, Palette.FolLight, 0.4f));
                if (Rnd(r) > 0.35f)
                {
                    // cattail head
                    Vector2 hc = new Vector2(bx + lean * 0.93f, h * 0.9f);
                    float ang = 90f - lean * 18f;
                    c.Fill(p => Sdf.Capsule(p, hc - MathUtil.Dir(ang) * 0.07f, hc + MathUtil.Dir(ang) * 0.07f, 0.028f),
                        new Color(0.22f, 0.2f, 0.17f), 0f, new Rect(hc.x - 0.15f, hc.y - 0.15f, 0.3f, 0.3f));
                }
            }
            c.RimLight(new Vector2(0.01f, 0.004f), Palette.FolRim, 0.45f);
            return c;
        }

        static SdfCanvas CloverPatch(int seed)
        {
            var r = new System.Random(seed);
            var c = new SdfCanvas(new Rect(-0.3f, 0f, 0.6f, 0.22f), 240f);
            for (int k = 0; k < 9; k++)
            {
                Vector2 b = new Vector2((Rnd(r) - 0.5f) * 0.4f, 0f);
                Vector2 top = b + new Vector2((Rnd(r) - 0.5f) * 0.05f, 0.06f + Rnd(r) * 0.1f);
                Blade(c, b, top, 0f, 0.006f, Palette.FolDark, Palette.FolMid);
                for (int l = 0; l < 3; l++)
                {
                    float ang = 90f + (l - 1) * 55f;
                    Vector2 lc = top + MathUtil.Dir(ang) * 0.022f;
                    c.Fill(p => Sdf.Circle(p, lc, 0.02f), Color.Lerp(Palette.FolMid, Palette.FolLight, Rnd(r)), 0f, new Rect(lc.x - 0.05f, lc.y - 0.05f, 0.1f, 0.1f));
                }
            }
            if (seed % 2 == 1)
            {
                Vector2 fc = new Vector2(0.08f, 0.16f);
                for (int l = 0; l < 5; l++)
                {
                    Vector2 pc = fc + MathUtil.Dir(l * 72f + 18f) * 0.016f;
                    c.Fill(p => Sdf.Circle(p, pc, 0.012f), new Color(0.92f, 0.96f, 1f), 0f, new Rect(pc.x - 0.03f, pc.y - 0.03f, 0.06f, 0.06f));
                }
                c.Fill(p => Sdf.Circle(p, fc, 0.008f), Palette.PetalWarm, 0f, new Rect(fc.x - 0.02f, fc.y - 0.02f, 0.04f, 0.04f));
            }
            c.RimLight(new Vector2(0.006f, 0.004f), Palette.FolRim, 0.4f);
            return c;
        }

        // ------------------------------------------------------------------ ferns & flowers

        static SdfCanvas Fern(int seed, float size)
        {
            var r = new System.Random(seed);
            var c = new SdfCanvas(new Rect(-size * 1.1f, 0f, size * 2.2f, size * 1.05f), 190f);
            int fronds = 5 + r.Next(3);
            for (int f = 0; f < fronds; f++)
            {
                float a0 = Mathf.Lerp(-62f, 62f, (f + 0.5f) / fronds) + (Rnd(r) - 0.5f) * 12f;
                float len = size * (0.65f + Rnd(r) * 0.4f) * (1f - Mathf.Abs(a0) / 180f);
                float droop = Mathf.Sign(a0) * (25f + Rnd(r) * 25f);
                Vector2 prev = Vector2.zero;
                const int steps = 14;
                for (int s = 1; s <= steps; s++)
                {
                    float t = s / (float)steps;
                    float ang = 90f - (a0 + droop * t * t);
                    Vector2 pt = prev + MathUtil.Dir(ang) * (len / steps);
                    Vector2 p0 = prev, p1 = pt;
                    float w = Mathf.Lerp(0.012f, 0.004f, t);
                    c.Fill(p => Sdf.Capsule(p, p0, p1, w), Palette.FolDark, 0f, Rect.MinMaxRect(Mathf.Min(p0.x, p1.x) - 0.03f, Mathf.Min(p0.y, p1.y) - 0.03f, Mathf.Max(p0.x, p1.x) + 0.03f, Mathf.Max(p0.y, p1.y) + 0.03f));
                    if (t > 0.12f)
                    {
                        float leafLen = Mathf.Lerp(0.16f, 0.04f, t) * size;
                        Color lc = Color.Lerp(Palette.Fern, Palette.FernLight, t * 0.8f + Rnd(r) * 0.2f);
                        Leaf(c, pt + MathUtil.Dir(ang + 68f) * leafLen * 0.5f, ang + 68f, leafLen, leafLen * 0.22f, lc, 0.15f);
                        Leaf(c, pt + MathUtil.Dir(ang - 68f) * leafLen * 0.5f, ang - 68f, leafLen, leafLen * 0.22f, lc, 0.15f);
                    }
                    prev = pt;
                }
            }
            c.RimLight(new Vector2(0.01f, 0.006f), Palette.FolRim, 0.5f);
            return c;
        }

        static SdfCanvas FlowerClump(Variant v, int seed, bool warm)
        {
            var r = new System.Random(seed);
            var c = new SdfCanvas(new Rect(-0.4f, 0f, 0.8f, 0.95f), 220f);
            int stems = 2 + r.Next(3);
            Color petal = warm ? Palette.PetalWarm : Palette.Petal;
            for (int k = 0; k < stems; k++)
            {
                float bx = (Rnd(r) - 0.5f) * 0.25f;
                float h = 0.4f + Rnd(r) * 0.42f;
                Vector2 tip = new Vector2(bx + (Rnd(r) - 0.5f) * 0.2f, h);
                Blade(c, new Vector2(bx, 0f), tip, 0.2f, 0.01f, Palette.FolDark, Palette.FolMid);
                Leaf(c, new Vector2(bx + 0.04f, h * 0.3f), 30f + Rnd(r) * 20f, 0.14f, 0.03f, Palette.FolMid, 0.2f);
                Leaf(c, new Vector2(bx - 0.04f, h * 0.45f), 150f - Rnd(r) * 20f, 0.12f, 0.026f, Palette.FolMid, 0.2f);
                float pr = 0.045f + Rnd(r) * 0.02f;
                for (int pIdx = 0; pIdx < 5; pIdx++)
                {
                    float ang = pIdx * 72f + Rnd(r) * 10f;
                    Leaf(c, tip + MathUtil.Dir(ang) * pr * 0.75f, ang, pr * 1.5f, pr * 0.55f, Color.Lerp(petal, Color.white, 0.2f + Rnd(r) * 0.2f), 0.1f);
                }
                c.Fill(p => Sdf.Circle(p, tip, pr * 0.35f), Color.Lerp(petal, Palette.Gold, 0.6f), 0f, new Rect(tip.x - 0.05f, tip.y - 0.05f, 0.1f, 0.1f));
                v.Glows.Add(new GlowSpot { Pos = tip, Size = pr * 11f, Color = petal });
            }
            c.RimLight(new Vector2(0.008f, 0.005f), Palette.FolRim, 0.35f);
            return c;
        }

        static SdfCanvas MushroomCluster(Variant v, int seed)
        {
            var r = new System.Random(seed);
            var c = new SdfCanvas(new Rect(-0.35f, 0f, 0.7f, 0.5f), 240f);
            int n = 2 + r.Next(3);
            var items = new List<(float x, float h, float cw, float lean)>();
            for (int k = 0; k < n; k++) items.Add(((Rnd(r) - 0.5f) * 0.36f, 0.1f + Rnd(r) * 0.22f, 0.07f + Rnd(r) * 0.07f, (Rnd(r) - 0.5f) * 0.08f));
            items.Sort((a, b) => b.h.CompareTo(a.h));
            foreach (var m in items)
            {
                Vector2 b = new Vector2(m.x, 0f), top = new Vector2(m.x + m.lean, m.h);
                c.Fill(p => Sdf.Tapered(p, b, m.cw * 0.28f, top, m.cw * 0.2f), Vertical(0f, m.h, new Color(0.45f, 0.6f, 0.58f), Palette.MushStem), 0f,
                    new Rect(m.x - 0.15f, -0.02f, 0.3f, m.h + 0.05f));
                Vector2 cc = top + new Vector2(0f, -0.005f);
                float capH = m.cw * 0.62f;
                SdfCanvas.SdfFn cap = p => Mathf.Max(Sdf.Ellipse(p, cc, new Vector2(m.cw, capH)), cc.y - 0.012f - p.y);
                c.Fill(cap, p => Color.Lerp(Palette.MushCapDark, Palette.MushCap, MathUtil.Smooth01((p.y - cc.y) / capH + 0.2f)), 0f,
                    new Rect(cc.x - m.cw - 0.02f, cc.y - 0.03f, m.cw * 2f + 0.04f, capH + 0.05f));
                c.Fill(p => Sdf.Box(p, cc + new Vector2(0f, -0.004f), new Vector2(m.cw * 0.92f, 0.009f), 0.008f), Palette.MushCapDark, 0f,
                    new Rect(cc.x - m.cw, cc.y - 0.03f, m.cw * 2f, 0.05f));
                for (int s = 0; s < 4; s++)
                {
                    Vector2 sp = cc + new Vector2((Rnd(r) - 0.5f) * m.cw * 1.3f, capH * (0.25f + Rnd(r) * 0.55f));
                    float sr = m.cw * (0.08f + Rnd(r) * 0.07f);
                    c.Paint(p => Sdf.Circle(p, sp, sr), new Color(0.85f, 1f, 1f, 0.9f), 0.003f, new Rect(sp.x - 0.04f, sp.y - 0.04f, 0.08f, 0.08f));
                }
                v.Glows.Add(new GlowSpot { Pos = cc + new Vector2(0f, capH * 0.4f), Size = m.cw * 9f, Color = Palette.MushCap });
            }
            c.RimLight(new Vector2(0.006f, 0.006f), new Color(0.8f, 1f, 1f), 0.4f);
            return c;
        }

        // ------------------------------------------------------------------ bushes & canopies

        internal static void LeafMass(SdfCanvas c, System.Random r, SdfCanvas.SdfFn mass, Rect area, int leaves, float leafSize,
            Color dark, Color light, Vector2 lightDir)
        {
            c.Fill(mass, p => Color.Lerp(dark * 0.75f, dark, MathUtil.Smooth01((p.y - area.yMin) / area.height)), 0f, area);
            for (int placed = 0, attempts = 0; placed < leaves && attempts < leaves * 25; attempts++)
            {
                Vector2 pos = new Vector2(area.xMin + Rnd(r) * area.width, area.yMin + Rnd(r) * area.height);
                float d = mass(pos);
                // leaves crowd the silhouette edge; fewer in the shaded interior
                if (d > 0.02f || (d < -leafSize * 5f && Rnd(r) > 0.3f)) continue;
                placed++;
                Vector2 rel = new Vector2((pos.x - area.center.x) / area.width, (pos.y - area.yMin) / area.height);
                float lit = Mathf.Clamp01(Vector2.Dot(rel, lightDir) * 1.2f + 0.35f + (Rnd(r) - 0.5f) * 0.4f);
                Color col = Color.Lerp(dark, light, lit);
                float ang = Rnd(r) * 360f;
                float len = leafSize * (0.8f + Rnd(r) * 0.5f);
                Leaf(c, pos, ang, len, len * 0.42f, col, 0.12f);
            }
        }

        static SdfCanvas Bush(int seed, float width)
        {
            var r = new System.Random(seed);
            float h = width * 0.7f;
            var c = new SdfCanvas(new Rect(-width * 0.62f, 0f, width * 1.24f, h + 0.12f), 170f);
            var blobs = new List<(Vector2 c, float r)>();
            for (int i = 0; i < 6; i++)
            {
                float t = i / 5f;
                blobs.Add((new Vector2(Mathf.Lerp(-width * 0.38f, width * 0.38f, t), h * (0.35f + 0.25f * Mathf.Sin(t * Mathf.PI)) ), width * (0.2f + 0.08f * Mathf.Sin(t * Mathf.PI)) + Rnd(r) * 0.05f));
            }
            SdfCanvas.SdfFn mass = p =>
            {
                float m = 9f;
                for (int i = 0; i < blobs.Count; i++) m = Sdf.SmoothUnion(m, Sdf.Circle(p, blobs[i].c, blobs[i].r), 0.08f);
                return Sdf.Intersect(m, -p.y);
            };
            LeafMass(c, r, mass, new Rect(-width * 0.6f, 0f, width * 1.2f, h + 0.1f), (int)(width * 170f), 0.085f,
                Palette.FolDark, Palette.FolLight, new Vector2(0.6f, 0.8f));
            c.RimLight(new Vector2(0.012f, 0.012f), Palette.FolRim, 0.5f);
            return c;
        }

        static SdfCanvas Canopy(int seed, float width)
        {
            var r = new System.Random(seed);
            float h = width * 0.66f;
            var c = new SdfCanvas(new Rect(-width * 0.58f, 0f, width * 1.16f, h + 0.2f), 90f);
            var blobs = new List<(Vector2 c, float r)>();
            int n = 7 + r.Next(3);
            for (int i = 0; i < n; i++)
            {
                float ang = Mathf.Lerp(15f, 165f, Rnd(r));
                float dist = Rnd(r) * 0.35f;
                Vector2 bc = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad) * width * (0.18f + dist), h * 0.42f + Mathf.Sin(ang * Mathf.Deg2Rad) * h * 0.25f);
                blobs.Add((bc, width * (0.16f + Rnd(r) * 0.1f)));
            }
            SdfCanvas.SdfFn mass = p =>
            {
                float m = 9f;
                for (int i = 0; i < blobs.Count; i++) m = Sdf.SmoothUnion(m, Sdf.Circle(p, blobs[i].c, blobs[i].r), 0.25f);
                return m;
            };
            Color dark = new Color(0.035f, 0.13f, 0.16f), light = new Color(0.12f, 0.36f, 0.38f);
            LeafMass(c, r, mass, new Rect(-width * 0.56f, 0f, width * 1.12f, h + 0.15f), (int)(width * 190f), 0.13f,
                dark, light, new Vector2(0.55f, 0.85f));
            c.RimLight(new Vector2(0.03f, 0.03f), new Color(0.35f, 0.72f, 0.74f), 0.55f);
            return c;
        }

        // ------------------------------------------------------------------ hanging plants

        static SdfCanvas IvyStrand(int seed, float length)
        {
            var r = new System.Random(seed);
            var c = new SdfCanvas(new Rect(-0.28f, -length - 0.08f, 0.56f, length + 0.12f), 200f);
            Vector2 prev = Vector2.zero;
            int steps = Mathf.CeilToInt(length / 0.06f);
            float wob = Rnd(r) * 10f;
            for (int s = 1; s <= steps; s++)
            {
                float t = s / (float)steps;
                Vector2 pt = new Vector2(Mathf.Sin(t * 5f + wob) * 0.05f * t, -length * t);
                Vector2 p0 = prev, p1 = pt;
                float w = Mathf.Lerp(0.011f, 0.004f, t);
                c.Fill(p => Sdf.Capsule(p, p0, p1, w), Palette.FolDark, 0f, Rect.MinMaxRect(Mathf.Min(p0.x, p1.x) - 0.03f, p1.y - 0.03f, Mathf.Max(p0.x, p1.x) + 0.03f, p0.y + 0.03f));
                if (s % 2 == 0)
                {
                    float side = (s / 2) % 2 == 0 ? 1f : -1f;
                    float size = Mathf.Lerp(0.085f, 0.045f, t) * (0.8f + Rnd(r) * 0.4f);
                    float ang = -90f + side * (50f + Rnd(r) * 30f);
                    Color lc = Color.Lerp(Palette.Ivy, Palette.IvyLight, Rnd(r) * 0.8f + (side > 0 ? 0.2f : 0f));
                    Vector2 lp = pt + MathUtil.Dir(ang) * size * 0.55f;
                    // ivy leaf: three-lobed from two overlapping lens leaves
                    Leaf(c, lp, ang, size * 1.2f, size * 0.55f, lc, 0.18f);
                    Leaf(c, lp + MathUtil.Dir(ang + 90f) * size * 0.15f, ang + 35f, size * 0.8f, size * 0.35f, lc, 0f);
                }
                prev = pt;
            }
            c.RimLight(new Vector2(0.008f, 0.006f), Palette.FolRim, 0.4f);
            return c;
        }

        /// <summary>Roots dangling from a floating rock: tapered, wandering strands with little rootlets.</summary>
        static SdfCanvas HangingRoots(int seed, float length)
        {
            var r = new System.Random(seed);
            var c = new SdfCanvas(new Rect(-0.32f, -length - 0.1f, 0.64f, length + 0.14f), 200f);
            Color dark = new Color(0.06f, 0.13f, 0.14f), light = new Color(0.22f, 0.34f, 0.31f);
            int strands = 2 + r.Next(2);
            for (int k = 0; k < strands; k++)
            {
                Vector2 prev = new Vector2((Rnd(r) - 0.5f) * 0.14f, 0.02f);
                float len = length * (0.55f + Rnd(r) * 0.45f);
                float w = 0.02f + Rnd(r) * 0.012f;
                float wob = Rnd(r) * 6f;
                const int segs = 8;
                for (int s = 1; s <= segs; s++)
                {
                    float t0 = (s - 1) / (float)segs, t1 = s / (float)segs;
                    Vector2 next = new Vector2(prev.x + Mathf.Sin(t1 * 5f + wob) * 0.022f + (Rnd(r) - 0.5f) * 0.018f, 0.02f - len * t1);
                    Vector2 p0 = prev, p1 = next;
                    float w0 = Mathf.Lerp(w, 0.004f, t0), w1 = Mathf.Lerp(w, 0.004f, t1);
                    Color col = Color.Lerp(light, dark, t0 * 0.7f);
                    c.Fill(p => Sdf.Tapered(p, p0, w0, p1, w1), col, 0f, Rect.MinMaxRect(Mathf.Min(p0.x, p1.x) - 0.05f, p1.y - 0.05f, Mathf.Max(p0.x, p1.x) + 0.05f, p0.y + 0.05f));
                    if (s > 2 && Rnd(r) > 0.55f)
                    {
                        Vector2 tip = p1 + new Vector2((Rnd(r) - 0.5f) * 0.2f, -0.04f - Rnd(r) * 0.1f);
                        float wr = w1 * 0.7f;
                        c.Fill(p => Sdf.Tapered(p, p1, wr, tip, 0.002f), col, 0f, Rect.MinMaxRect(Mathf.Min(p1.x, tip.x) - 0.04f, tip.y - 0.04f, Mathf.Max(p1.x, tip.x) + 0.04f, p1.y + 0.04f));
                    }
                    prev = next;
                }
            }
            c.RimLight(new Vector2(0.01f, 0.008f), new Color(0.45f, 0.72f, 0.66f), 0.5f);
            return c;
        }

        static SdfCanvas MossCurtain(int seed)
        {
            var r = new System.Random(seed);
            var c = new SdfCanvas(new Rect(-0.45f, -1.5f, 0.9f, 1.55f), 170f);
            for (int k = 0; k < 26; k++)
            {
                float x0 = (Rnd(r) - 0.5f) * 0.7f;
                float len = 0.35f + Rnd(r) * 1.05f;
                Vector2 prev = new Vector2(x0, 0f);
                int steps = 6;
                float a = Rnd(r) * 6f;
                for (int s = 1; s <= steps; s++)
                {
                    float t = s / (float)steps;
                    Vector2 pt = new Vector2(x0 + Mathf.Sin(t * 3f + a) * 0.03f, -len * t);
                    Vector2 p0 = prev, p1 = pt;
                    float w = Mathf.Lerp(0.01f, 0.003f, t);
                    Color col = Color.Lerp(Palette.Moss * 0.8f, Palette.Moss * 1.25f, t).WithAlpha(0.9f);
                    c.Fill(p => Sdf.Capsule(p, p0, p1, w), col, 0f, Rect.MinMaxRect(Mathf.Min(p0.x, p1.x) - 0.03f, p1.y - 0.03f, Mathf.Max(p0.x, p1.x) + 0.03f, p0.y + 0.03f));
                    prev = pt;
                }
            }
            return c;
        }

        static SdfCanvas Banner(int seed, int style)
        {
            var r = new System.Random(seed);
            const float W = 0.5f, H = 1.3f;
            var c = new SdfCanvas(new Rect(-0.34f, -H - 0.1f, 0.68f, H + 0.16f), 190f);
            Color cloth = style == 0 ? Palette.Banner : new Color(0.16f, 0.42f, 0.5f);
            Color clothLight = style == 0 ? Palette.BannerLight : new Color(0.3f, 0.62f, 0.68f);
            // tattered bottom: zig-zag cuts
            SdfCanvas.SdfFn shape = p =>
            {
                float d = Sdf.Box(p, new Vector2(0f, -H * 0.5f - 0.02f), new Vector2(W * 0.5f, H * 0.5f), 0.01f);
                float cut = p.y + H - 0.02f - 0.12f * Mathf.Abs(Mathf.Sin((p.x + 0.3f) * 18f + seed)) - 0.05f * Mathf.Sin(p.x * 41f);
                d = Mathf.Max(d, -cut);
                d = Sdf.Subtract(d, Sdf.Circle(p, new Vector2(0.12f, -0.95f), 0.035f));
                return d;
            };
            c.Fill(shape, p =>
            {
                float fold = 0.5f + 0.5f * Mathf.Sin(p.x * 26f + p.y * 1.5f);
                Color col = Color.Lerp(cloth * 0.8f, clothLight, fold * 0.55f + MathUtil.Smooth01((p.y + H) / H) * 0.25f);
                return col;
            });
            c.Paint(p => Mathf.Abs(p.x - 0.0f + (p.y + 0.4f) * 0.18f) - 0.035f, new Color(0.9f, 0.88f, 0.82f, 0.8f), 0.004f);
            Vector2 em = new Vector2(0f, -0.38f);
            c.Paint(p => Sdf.Circle(p, em, 0.12f), new Color(0.92f, 0.92f, 0.88f, 0.95f), 0.004f);
            c.Paint(p => Sdf.Circle(p, em, 0.045f), cloth * 0.6f, 0.004f);
            for (int k = 0; k < 5; k++)
            {
                Vector2 pc = em + MathUtil.Dir(90f + k * 72f) * 0.085f;
                c.Paint(p => Sdf.Circle(p, pc, 0.022f), cloth * 0.6f, 0.003f);
            }
            // pole
            c.Fill(p => Sdf.Capsule(p, new Vector2(-0.3f, 0.01f), new Vector2(0.3f, 0.01f), 0.022f), new Color(0.12f, 0.1f, 0.09f));
            c.Fill(p => Sdf.Circle(p, new Vector2(-0.3f, 0.01f), 0.03f), new Color(0.5f, 0.42f, 0.28f));
            c.Fill(p => Sdf.Circle(p, new Vector2(0.3f, 0.01f), 0.03f), new Color(0.5f, 0.42f, 0.28f));
            return c;
        }

        // ------------------------------------------------------------------ misc

        static SdfCanvas ForegroundFrond(int seed)
        {
            var r = new System.Random(seed);
            var c = new SdfCanvas(new Rect(-1.6f, 0f, 3.2f, 2.8f), 64f);
            Color col = Palette.Foreground;
            int fronds = 4 + r.Next(3);
            for (int f = 0; f < fronds; f++)
            {
                float a0 = Mathf.Lerp(-55f, 55f, (f + 0.5f) / fronds) + (Rnd(r) - 0.5f) * 15f;
                float len = 1.5f + Rnd(r) * 1.1f;
                Vector2 prev = Vector2.zero;
                const int steps = 10;
                for (int s = 1; s <= steps; s++)
                {
                    float t = s / (float)steps;
                    float ang = 90f - (a0 + Mathf.Sign(a0) * 30f * t * t);
                    Vector2 pt = prev + MathUtil.Dir(ang) * (len / steps);
                    Vector2 p0 = prev, p1 = pt;
                    c.Fill(p => Sdf.Capsule(p, p0, p1, 0.03f * (1f - t * 0.7f)), col, 0.03f);
                    if (t > 0.1f)
                    {
                        float ll = Mathf.Lerp(0.45f, 0.12f, t);
                        Leaf(c, pt + MathUtil.Dir(ang + 60f) * ll * 0.5f, ang + 60f, ll, ll * 0.22f, col, 0f);
                        Leaf(c, pt + MathUtil.Dir(ang - 60f) * ll * 0.5f, ang - 60f, ll, ll * 0.22f, col, 0f);
                    }
                    prev = pt;
                }
            }
            c.Blur(2);
            return c;
        }

        static SdfCanvas CrystalCluster(Variant v, int seed)
        {
            var r = new System.Random(seed);
            var c = new SdfCanvas(new Rect(-0.3f, -0.02f, 0.6f, 0.5f), 240f);
            int n = 3 + r.Next(3);
            for (int k = 0; k < n; k++)
            {
                float ang = Mathf.Lerp(55f, 125f, Rnd(r));
                float len = 0.12f + Rnd(r) * 0.28f;
                float w = 0.03f + Rnd(r) * 0.03f;
                Vector2 b = new Vector2((Rnd(r) - 0.5f) * 0.18f, 0f);
                Vector2 dir = MathUtil.Dir(ang), n2 = new Vector2(-dir.y, dir.x);
                Vector2 tip = b + dir * len;
                Vector2 b1 = b + n2 * w, b2 = b - n2 * w, s1 = tip - dir * w * 1.4f + n2 * w, s2 = tip - dir * w * 1.4f - n2 * w;
                SdfCanvas.SdfFn body = p => Mathf.Min(Mathf.Min(Sdf.Triangle(p, b1, s1, s2), Sdf.Triangle(p, b1, s2, b2)), Sdf.Triangle(p, s1, tip, s2));
                c.Fill(body, Color.Lerp(Palette.Crystal * 0.55f, Palette.Crystal, 0.3f + Rnd(r) * 0.3f));
                c.Paint(p => Mathf.Max(body(p), Vector2.Dot(p - b, n2)), Color.Lerp(Palette.Crystal, Color.white, 0.55f).WithAlpha(0.8f));
                v.Glows.Add(new GlowSpot { Pos = b + dir * len * 0.55f, Size = len * 2.2f, Color = Palette.Crystal * 0.8f });
            }
            return c;
        }

        static SdfCanvas Pebbles(int seed)
        {
            var r = new System.Random(seed);
            var c = new SdfCanvas(new Rect(-0.3f, -0.02f, 0.6f, 0.2f), 220f);
            for (int k = 0; k < 4; k++)
            {
                Vector2 pc = new Vector2((Rnd(r) - 0.5f) * 0.4f, 0.03f + Rnd(r) * 0.02f);
                Vector2 rad = new Vector2(0.03f + Rnd(r) * 0.05f, 0.02f + Rnd(r) * 0.025f);
                c.Fill(p => Sdf.Ellipse(p, pc, rad), p => Color.Lerp(Palette.Stone * 0.8f, Palette.StoneLight, MathUtil.Smooth01((p.y - pc.y) / rad.y * 0.8f + 0.4f)));
            }
            return c;
        }

        static SdfCanvas GlowBlob()
        {
            var c = new SdfCanvas(new Rect(-0.5f, -0.5f, 1f, 1f), 96f);
            c.Field(p =>
            {
                float d = p.magnitude / 0.5f;
                return new Color(1f, 1f, 1f, Mathf.Exp(-d * d * 4.5f) * MathUtil.Smooth01((1f - d) / 0.15f));
            });
            return c;
        }
    }
}
