using UnityEngine;

namespace SoccerFight
{
    /// <summary>Corrupted forest creatures: dark violet bodies, soft rim light, glowing eyes.</summary>
    public static class MonsterArt
    {
        public static Sprite BlobBody, BlobHorn, BlobFoot;
        public static Sprite Eye, Pupil, EyeGlow;
        public static Sprite WispBody, WispTail, WispEye;
        public static Sprite Portal;

        const float P = 300f;

        public static void Build()
        {
            BuildBlob();
            BuildWisp();
            BuildEyes();
            BuildPortal();
        }

        static Color Vertical(Vector2 p, float y0, float y1, Color bottom, Color top)
            => Color.Lerp(bottom, top, MathUtil.Smooth01((p.y - y0) / (y1 - y0)));

        static void BuildBlob()
        {
            // Pivot at the ground contact point; ~0.9 wide, ~0.78 tall.
            var c = new SdfCanvas(new Rect(-0.55f, -0.05f, 1.1f, 0.95f), P);
            SdfCanvas.SdfFn body = p =>
            {
                float dome = Sdf.Ellipse(p, new Vector2(0f, 0.39f), new Vector2(0.45f, 0.39f));
                float bottom = Sdf.Box(p, new Vector2(0f, 0.16f), new Vector2(0.43f, 0.16f), 0.14f);
                return Sdf.SmoothUnion(dome, bottom, 0.08f);
            };
            c.Fill(body, p => Vertical(p, 0.0f, 0.78f, Palette.MonsterBottom, Palette.MonsterTop));
            // belly glow and rim light
            c.Paint(p => Sdf.Ellipse(p, new Vector2(0.02f, 0.26f), new Vector2(0.24f, 0.16f)), Color.Lerp(Palette.MonsterTop, Palette.MonsterGlow, 0.35f).WithAlpha(0.55f), 0.12f);
            c.Paint(p => Sdf.Intersect(body(p) + 0.01f, -body(p + new Vector2(-0.035f, 0.045f))), new Color(0.78f, 0.62f, 1f, 0.75f), 0.012f);
            // subtle speckles
            for (int i = 0; i < 7; i++)
            {
                Vector2 sp = new Vector2(MathUtil.Hash(i * 7 + 1) * 0.3f, 0.35f + MathUtil.Hash(i * 13 + 5) * 0.22f);
                float sr = 0.018f + 0.012f * Mathf.Abs(MathUtil.Hash(i * 3 + 2));
                c.Paint(p => Sdf.Circle(p, sp, sr), Palette.MonsterBottom.WithAlpha(0.45f), 0.006f);
            }
            BlobBody = c.ToSprite("BlobBody", Vector2.zero);

            var h = new SdfCanvas(new Rect(-0.08f, -0.04f, 0.2f, 0.3f), P);
            h.Fill(p => Sdf.Tapered(p, Vector2.zero, 0.055f, new Vector2(0.06f, 0.22f), 0.008f),
                p => Vertical(p, 0f, 0.22f, Palette.MonsterTop, new Color(0.85f, 0.7f, 1f)));
            BlobHorn = h.ToSprite("BlobHorn", Vector2.zero);

            var f = new SdfCanvas(new Rect(-0.1f, -0.06f, 0.2f, 0.12f), P);
            f.Fill(p => Sdf.Ellipse(p, Vector2.zero, new Vector2(0.085f, 0.045f)), Palette.MonsterBottom);
            BlobFoot = f.ToSprite("BlobFoot", Vector2.zero);
        }

        static void BuildWisp()
        {
            var c = new SdfCanvas(new Rect(-0.38f, -0.38f, 0.76f, 0.76f), P);
            SdfCanvas.SdfFn body = p => Sdf.Circle(p, Vector2.zero, 0.31f);
            c.Fill(body, p => Vertical(p, -0.3f, 0.3f, Palette.WispBottom, Palette.WispTop));
            c.Paint(p => Sdf.Intersect(body(p) + 0.01f, -body(p + new Vector2(-0.03f, 0.04f))), new Color(0.7f, 0.85f, 1f, 0.8f), 0.012f);
            c.Paint(p => Sdf.Circle(p, new Vector2(0f, -0.05f), 0.2f), Palette.WispGlow.WithAlpha(0.25f), 0.12f);
            WispBody = c.ToSprite("WispBody", Vector2.zero);

            var t = new SdfCanvas(new Rect(-0.2f, -0.2f, 0.4f, 0.4f), P);
            t.Fill(p => Sdf.Circle(p, Vector2.zero, 0.17f), p => Vertical(p, -0.17f, 0.17f, Palette.WispBottom, Palette.WispTop));
            WispTail = t.ToSprite("WispTail", Vector2.zero);

            var e = new SdfCanvas(new Rect(-0.16f, -0.16f, 0.32f, 0.32f), P);
            e.Fill(p => Sdf.Circle(p, Vector2.zero, 0.13f), new Color(1f, 0.98f, 0.9f));
            e.Fill(p => Sdf.Ellipse(p, new Vector2(0.03f, 0f), new Vector2(0.035f, 0.085f)), new Color(0.1f, 0.05f, 0.2f));
            WispEye = e.ToSprite("WispEye", Vector2.zero);
        }

        static void BuildEyes()
        {
            var e = new SdfCanvas(new Rect(-0.08f, -0.1f, 0.16f, 0.2f), P);
            e.Fill(p => Sdf.Ellipse(p, Vector2.zero, new Vector2(0.058f, 0.078f)), Palette.MonsterEye);
            Eye = e.ToSprite("Eye", Vector2.zero);

            var pu = new SdfCanvas(new Rect(-0.04f, -0.06f, 0.08f, 0.12f), P);
            pu.Fill(p => Sdf.Ellipse(p, Vector2.zero, new Vector2(0.018f, 0.045f)), new Color(0.12f, 0.04f, 0.16f));
            Pupil = pu.ToSprite("Pupil", Vector2.zero);

            EyeGlow = Art.SoftGlow;
        }

        static void BuildPortal()
        {
            // Vertical swirl ellipse used as spawn gate.
            var c = new SdfCanvas(new Rect(-0.75f, -1.25f, 1.5f, 2.5f), 160f);
            c.Fill(p =>
            {
                Vector2 q = new Vector2(p.x / 0.6f, p.y / 1.1f);
                return (q.magnitude - 1f) * 0.6f;
            }, p =>
            {
                Vector2 q = new Vector2(p.x / 0.6f, p.y / 1.1f);
                float r = q.magnitude;
                float a = Mathf.Pow(Mathf.Clamp01(r), 3.2f);
                float ang = Mathf.Atan2(q.y, q.x);
                float swirl = 0.5f + 0.5f * Mathf.Sin(ang * 3f + r * 9f);
                return new Color(1f, 1f, 1f, Mathf.Clamp01(a * (0.55f + 0.45f * swirl)));
            }, 0.03f);
            Portal = c.ToSprite("Portal", Vector2.zero);
        }
    }
}
