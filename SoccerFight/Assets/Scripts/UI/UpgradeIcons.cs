using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// White line-art glyphs for the upgrade cards (tinted by the card's rarity medallion). Built
    /// lazily the first time a card needs them so startup stays fast.
    /// </summary>
    public static class UpgradeIcons
    {
        static readonly Dictionary<UpIcon, Sprite> cache = new Dictionary<UpIcon, Sprite>();
        static readonly Color W = Color.white;
        static readonly Color H = new Color(1f, 1f, 1f, 0.45f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => cache.Clear();

        public static Sprite Get(UpIcon icon)
        {
            if (cache.TryGetValue(icon, out var s)) return s;
            var c = new SdfCanvas(new Rect(-64, -64, 128, 128), 1.5f);
            Draw(c, icon);
            s = UiArt.ToUi(c, "UpIcon " + icon);
            cache[icon] = s;
            return s;
        }

        static Vector2 V(float x, float y) => new Vector2(x, y);

        static void Ball(SdfCanvas c, Vector2 bc, float r)
        {
            c.Fill(p => Sdf.Circle(p, bc, r), W);
            Vector2 pc0 = bc + new Vector2(r * 0.1f, r * 0.08f);
            c.Erase(p => Sdf.Intersect(Sdf.Circle(p, pc0, r * 0.36f), Sdf.Circle(p, bc, r - 0.6f)));   // panels punched out: the medallion shows through
            for (int k = 0; k < 5; k++)
            {
                Vector2 pc = pc0 + MathUtil.Dir(90f + k * 72f + 10f) * r * 0.9f;
                c.Erase(p => Sdf.Intersect(Sdf.Circle(p, pc, r * 0.3f), Sdf.Circle(p, bc, r - 0.6f)));
            }
        }

        /// <summary>A basketball in line art: the disc with its ribs punched out.</summary>
        static void HoopBall(SdfCanvas c, Vector2 bc, float r)
        {
            c.Fill(p => Sdf.Circle(p, bc, r), W);
            float w = Mathf.Max(1.2f, r * 0.07f);
            c.Erase(p => Sdf.Intersect(Mathf.Abs(p.x - bc.x) - w, Sdf.Circle(p, bc, r - 0.6f)));
            c.Erase(p => Sdf.Intersect(Mathf.Abs(p.y - bc.y) - w, Sdf.Circle(p, bc, r - 0.6f)));
            for (int s = -1; s <= 1; s += 2)
            {
                int k = s;
                c.Erase(p => Sdf.Intersect(Mathf.Abs(Sdf.Circle(p, bc + new Vector2(k * r * 1.25f, 0f), r * 0.9f)) - w, Sdf.Circle(p, bc, r - 0.6f)));
            }
        }

        static void Line(SdfCanvas c, Vector2 a, Vector2 b, float w, Color col) => c.Fill(p => Sdf.Capsule(p, a, b, w), col);

        static void Arrow(SdfCanvas c, Vector2 from, Vector2 to, float w, float head, Color col)
        {
            Vector2 d = (to - from).normalized, n = new Vector2(-d.y, d.x);
            c.Fill(p => Sdf.Capsule(p, from, to - d * head * 0.6f, w), col);
            c.Fill(p => Sdf.Triangle(p, to + d * head * 0.4f, to - d * head + n * head * 0.8f, to - d * head - n * head * 0.8f), col);
        }

        static void Arc(SdfCanvas c, Vector2 center, float r, float thick, float a0, float a1, Color col)
        {
            float mid = (a0 + a1) * 0.5f, half = Mathf.Abs(a1 - a0) * 0.5f;
            c.Fill(p =>
            {
                Vector2 q = p - center;
                float ang = Mathf.Atan2(q.y, q.x) * Mathf.Rad2Deg;
                float da = Mathf.Abs(Mathf.DeltaAngle(ang, mid)) - half;
                float ring = Sdf.Ring(p, center, r, thick);
                return Mathf.Max(ring, da * Mathf.Deg2Rad * r);
            }, col);
        }

        static float HeartSdf(Vector2 p, Vector2 c, float s)
        {
            Vector2 q = (p - c) / s;
            Vector2 a = new Vector2(Mathf.Abs(q.x), q.y + 4f);
            float lobes = Sdf.Circle(a, new Vector2(10f, 8f), 12f);
            float tip = Sdf.Triangle(new Vector2(q.x, q.y + 4f), new Vector2(-21f, 5f), new Vector2(21f, 5f), new Vector2(0f, -22f));
            return Sdf.SmoothUnion(lobes, tip, 3f) * s;
        }

        static float ShieldSdf(Vector2 p, Vector2 c, float s)
        {
            Vector2 q = (p - c) / s;
            float top = Sdf.Box(q, new Vector2(0f, 10f), new Vector2(26f, 18f), 6f);
            float tip = Sdf.Triangle(q, new Vector2(-26f, 4f), new Vector2(26f, 4f), new Vector2(0f, -32f));
            return Sdf.SmoothUnion(top, tip, 6f) * s;
        }

        static float FlameSdf(Vector2 p, Vector2 c, float s)
        {
            Vector2 q = (p - c) / s;
            float body = Sdf.Circle(q, new Vector2(0f, -10f), 20f);
            float tip = Sdf.Triangle(q, new Vector2(-18f, -4f), new Vector2(18f, -4f), new Vector2(4f, 36f));
            float lick = Sdf.Triangle(q, new Vector2(-20f, -12f), new Vector2(-2f, -4f), new Vector2(-18f, 18f));
            return Sdf.SmoothUnion(Sdf.SmoothUnion(body, tip, 8f), lick, 5f) * s;
        }

        static void Bolt(SdfCanvas c, Vector2 o, float s, Color col)
        {
            c.Fill(p =>
            {
                Vector2 q = (p - o) / s;
                float a = Sdf.Triangle(q, V(6f, 34f), V(-16f, -2f), V(4f, 2f));
                float b = Sdf.Triangle(q, V(-4f, -2f), V(16f, 2f), V(-6f, -34f));
                float mid = Sdf.Box(q, V(0f, 0f), V(11f, 4f), 1f);
                return Mathf.Min(Mathf.Min(a, b), mid) * s;
            }, col);
        }

        static void Draw(SdfCanvas c, UpIcon icon)
        {
            switch (icon)
            {
                case UpIcon.Damage:
                    c.Fill(p => Sdf.Star4(p, V(18f, 16f), 40f, 0.5f), H);
                    Ball(c, V(-8f, -8f), 30f);
                    c.Fill(p => Sdf.Star4(p, V(28f, 26f), 16f, 0.5f), W);
                    break;
                case UpIcon.Speed:
                    for (int i = 0; i < 2; i++)
                    {
                        float x = -18f + i * 30f;
                        c.Fill(p => Sdf.Union(Sdf.Capsule(p, V(x - 14f, 26f), V(x + 10f, 0f), 7f), Sdf.Capsule(p, V(x + 10f, 0f), V(x - 14f, -26f), 7f)), i == 0 ? H : W);
                    }
                    for (int i = 0; i < 3; i++) Line(c, V(-56f, 18f - i * 18f), V(-40f, 18f - i * 18f), 3f, H);
                    break;
                case UpIcon.AttackSpeed:
                    Ball(c, V(0f, 0f), 22f);
                    Arc(c, V(0f, 0f), 38f, 7f, 20f, 300f, W);
                    Arrow(c, MathUtil.Dir(12f) * 38f + V(0f, -6f), MathUtil.Dir(12f) * 38f + V(-2f, 10f), 0.1f, 11f, W);
                    break;
                case UpIcon.Crit:
                    c.Fill(p => Sdf.Ring(p, Vector2.zero, 32f, 7f), W);
                    for (int i = 0; i < 4; i++) { Vector2 d = MathUtil.Dir(i * 90f); Line(c, d * 20f, d * 50f, 4f, W); }
                    c.Fill(p => Sdf.Circle(p, Vector2.zero, 7f), W);
                    break;
                case UpIcon.CritDamage:
                    c.Fill(p => Sdf.Star4(p, V(-4f, -4f), 46f, 0.45f), W);
                    c.Fill(p => Sdf.Star4(p, V(34f, 32f), 16f, 0.5f), H);
                    c.Fill(p => Sdf.Star4(p, V(-36f, 36f), 10f, 0.5f), H);
                    break;
                case UpIcon.Heart:
                    c.Fill(p => HeartSdf(p, V(-4f, -2f), 1.55f), W);
                    c.Fill(p => Sdf.Union(Sdf.Box(p, V(36f, 34f), V(4.5f, 14f), 2f), Sdf.Box(p, V(36f, 34f), V(14f, 4.5f), 2f)), W);
                    break;
                case UpIcon.Knockback:
                    Arrow(c, V(-46f, 0f), V(10f, 0f), 8f, 20f, W);
                    c.Fill(p => Sdf.Circle(p, V(38f, 0f), 16f), H);
                    for (int i = 0; i < 2; i++) Arc(c, V(38f, 0f), 24f + i * 10f, 4f, -50f, 50f, H);
                    break;
                case UpIcon.Area:
                    c.Fill(p => Sdf.Circle(p, Vector2.zero, 10f), W);
                    c.Fill(p => Sdf.Ring(p, Vector2.zero, 26f, 6f), W);
                    c.Fill(p => Sdf.Ring(p, Vector2.zero, 44f, 5f), H);
                    break;
                case UpIcon.Cooldown:
                    c.Fill(p => Sdf.Union(Sdf.Box(p, V(0f, 44f), V(30f, 5f), 2.5f), Sdf.Box(p, V(0f, -44f), V(30f, 5f), 2.5f)), W);
                    c.Fill(p => Sdf.Union(Sdf.Triangle(p, V(-24f, 38f), V(24f, 38f), V(0f, 2f)), Sdf.Triangle(p, V(-24f, -38f), V(24f, -38f), V(0f, -2f))), H);
                    c.Fill(p => Sdf.Triangle(p, V(-14f, -36f), V(14f, -36f), V(0f, -14f)), W);
                    c.Fill(p => Sdf.Triangle(p, V(-10f, 30f), V(10f, 30f), V(0f, 14f)), W);
                    break;
                case UpIcon.BallSpeed:
                    for (int i = 0; i < 3; i++) c.Fill(p => Sdf.Tapered(p, V(-58f, (i - 1) * 18f + (i - 1) * 3f), 1.5f, V(-12f, (i - 1) * 16f), 5f), i == 1 ? W : H);
                    Ball(c, V(20f, 0f), 28f);
                    break;
                case UpIcon.Regen:
                    Arc(c, Vector2.zero, 36f, 7f, 60f, 330f, H);
                    Arrow(c, MathUtil.Dir(40f) * 36f + V(8f, -10f), MathUtil.Dir(60f) * 36f + V(4f, 4f), 0.1f, 12f, H);
                    c.Fill(p => Sdf.Union(Sdf.Box(p, Vector2.zero, V(6f, 20f), 3f), Sdf.Box(p, Vector2.zero, V(20f, 6f), 3f)), W);
                    break;
                case UpIcon.Heal:
                    c.Fill(p => Sdf.Union(Sdf.Box(p, Vector2.zero, V(12f, 36f), 5f), Sdf.Box(p, Vector2.zero, V(36f, 12f), 5f)), W);
                    c.Fill(p => Sdf.Star4(p, V(38f, 38f), 12f, 0.5f), H);
                    c.Fill(p => Sdf.Star4(p, V(-40f, -36f), 9f, 0.5f), H);
                    break;
                case UpIcon.Leech:
                    c.Fill(p => Sdf.SmoothUnion(Sdf.Circle(p, V(0f, -12f), 26f), Sdf.Triangle(p, V(-22f, -2f), V(22f, -2f), V(0f, 44f)), 6f), W);
                    c.Erase(p => HeartSdf(p, V(0f, -12f), 0.55f));
                    break;
                case UpIcon.Armor:
                    c.Fill(p => ShieldSdf(p, V(0f, 2f), 1.35f), W);
                    c.Erase(p => Sdf.Subtract(ShieldSdf(p, V(0f, 2f), 1.05f), ShieldSdf(p, V(0f, 2f), 0.85f)), 0.6f);
                    break;
                case UpIcon.Magnet:
                    Arc(c, V(0f, 4f), 28f, 16f, 180f, 360f, W);
                    c.Fill(p => Sdf.Box(p, V(-28f, 22f), V(8f, 18f), 1f), W);
                    c.Fill(p => Sdf.Box(p, V(28f, 22f), V(8f, 18f), 1f), W);
                    c.Fill(p => Sdf.Box(p, V(-28f, 38f), V(8f, 5f), 1f), H);
                    c.Fill(p => Sdf.Box(p, V(28f, 38f), V(8f, 5f), 1f), H);
                    break;
                case UpIcon.Power:
                    c.Fill(p => Sdf.Tapered(p, V(-52f, -30f), 3f, V(34f, 22f), 9f), W);
                    c.Fill(p => Sdf.Triangle(p, V(28f, 40f), V(48f, 6f), V(56f, 36f)), W);
                    Ball(c, V(-10f, -6f), 20f);
                    break;
                case UpIcon.Rainbow:
                    for (int i = 0; i < 4; i++) { int k = i; c.Fill(p => Sdf.Intersect(Sdf.Ring(p, V(0f, -16f), 50f - k * 11f, 7f), -(p.y + 16f)), k % 2 == 0 ? W : H); }
                    c.Fill(p => Sdf.Star4(p, V(0f, 10f), 12f, 0.5f), W);
                    break;
                case UpIcon.Jump:
                    Arrow(c, V(0f, -8f), V(0f, 44f), 8f, 20f, W);
                    for (int i = 0; i < 3; i++) Line(c, V(-22f, -24f - i * 10f), V(22f, -30f - i * 10f), 3.5f, H);
                    break;
                case UpIcon.Dodge:
                    for (int i = 0; i < 3; i++)
                    {
                        float x = -28f + i * 22f;
                        Color col = i == 2 ? W : new Color(1f, 1f, 1f, 0.2f + i * 0.2f);
                        c.Fill(p => Sdf.Union(Sdf.Circle(p, V(x, 26f), 11f), Sdf.Box(p, V(x, -10f), V(12f, 24f), 10f)), col);
                    }
                    break;
                case UpIcon.Luck:
                    for (int i = 0; i < 4; i++) { Vector2 d = MathUtil.Dir(45f + i * 90f) * 18f + V(0f, 8f); c.Fill(p => Sdf.Circle(p, d, 17f), W); }
                    c.Fill(p => Sdf.Tapered(p, V(0f, 4f), 3f, V(14f, -48f), 4f), W);
                    c.Erase(p => Sdf.Circle(p, V(0f, 8f), 5f));
                    break;
                case UpIcon.Juggle:
                    Ball(c, V(0f, 26f), 20f);
                    Arc(c, V(0f, -60f), 44f, 8f, 40f, 140f, W);
                    for (int i = 0; i < 3; i++) c.Fill(p => Sdf.Circle(p, V(0f, -6f + i * 8f - 8f), 3.5f - i * 0.6f), H);
                    break;
                case UpIcon.Blast:
                    c.Fill(p => Sdf.Star4(p, Vector2.zero, 52f, 0.5f), H);
                    c.Fill(p => Sdf.Star4(MathUtil.Rotate(p, 45f), Vector2.zero, 40f, 0.5f), W);
                    c.Fill(p => Sdf.Circle(p, Vector2.zero, 12f), W);
                    break;
                case UpIcon.Echo:
                    c.Fill(p => Sdf.Circle(p, V(-22f, -14f), 26f), new Color(1f, 1f, 1f, 0.3f));
                    Ball(c, V(14f, 12f), 28f);
                    break;
                case UpIcon.Ricochet:
                    c.Fill(p => Sdf.Union(Sdf.Capsule(p, V(-48f, -30f), V(-6f, 26f), 4f), Sdf.Capsule(p, V(-6f, 26f), V(30f, -18f), 4f)), H);
                    c.Fill(p => Sdf.Box(p, V(-6f, 38f), V(20f, 4f), 2f), W);
                    Ball(c, V(36f, -26f), 16f);
                    break;
                case UpIcon.Chain:
                    c.Fill(p => Sdf.Circle(p, V(-38f, -30f), 12f), W);
                    c.Fill(p => Sdf.Circle(p, V(38f, 30f), 12f), W);
                    c.Fill(p => Sdf.Union(Sdf.Union(Sdf.Capsule(p, V(-30f, -22f), V(-6f, 6f), 4f), Sdf.Capsule(p, V(-6f, 6f), V(6f, -8f), 4f)), Sdf.Capsule(p, V(6f, -8f), V(30f, 22f), 4f)), W);
                    c.Fill(p => Sdf.Circle(p, V(-40f, 34f), 7f), H);
                    c.Fill(p => Sdf.Circle(p, V(40f, -34f), 7f), H);
                    break;
                case UpIcon.Fire:
                    c.Fill(p => FlameSdf(p, V(0f, -6f), 1.3f), W);
                    c.Erase(p => FlameSdf(p, V(2f, -18f), 0.6f), 0.6f);
                    break;
                case UpIcon.Frost:
                    for (int i = 0; i < 3; i++)
                    {
                        Vector2 d = MathUtil.Dir(90f + i * 60f);
                        Line(c, -d * 48f, d * 48f, 4.5f, W);
                        for (int s = -1; s <= 1; s += 2)
                        {
                            Vector2 at = d * 30f * s, n = new Vector2(-d.y, d.x);
                            Line(c, at, at + (d * s + n) * 10f, 3.5f, W);
                            Line(c, at, at + (d * s - n) * 10f, 3.5f, W);
                        }
                    }
                    c.Fill(p => Sdf.Circle(p, Vector2.zero, 9f), H);
                    break;
                case UpIcon.Explode:
                    c.Fill(p => Sdf.Circle(p, V(-6f, -10f), 32f), W);
                    c.Erase(p => Sdf.Circle(p, V(-16f, 0f), 9f), 0.45f);
                    c.Fill(p => Sdf.Box(p, V(14f, 22f), V(10f, 8f), 2f, -40f), W);
                    c.Fill(p => Sdf.Tapered(p, V(20f, 28f), 3f, V(36f, 44f), 2f), H);
                    c.Fill(p => Sdf.Star4(p, V(42f, 48f), 14f, 0.5f), W);
                    break;
                case UpIcon.Shockwave:
                    c.Fill(p => Sdf.Box(p, V(0f, -30f), V(56f, 4f), 2f), W);
                    for (int i = 0; i < 3; i++) Arc(c, V(0f, -30f), 16f + i * 16f, 6f - i, 20f, 160f, i == 0 ? W : H);
                    break;
                case UpIcon.Shield:
                    c.Fill(p => ShieldSdf(p, V(0f, 2f), 1.35f), W);
                    c.Erase(p => Sdf.Star4(p, V(0f, 4f), 20f, 0.5f));
                    break;
                case UpIcon.Adrenaline:
                    {
                        Vector2[] pts = { V(-56f, 0f), V(-24f, 0f), V(-12f, 30f), V(4f, -36f), V(16f, 12f), V(24f, 0f), V(56f, 0f) };
                        for (int i = 0; i < pts.Length - 1; i++) { var a = pts[i]; var b = pts[i + 1]; Line(c, a, b, 4.5f, W); }
                        c.Fill(p => HeartSdf(p, V(40f, 34f), 0.5f), H);
                    }
                    break;
                case UpIcon.AirKick:
                    Ball(c, V(24f, -28f), 18f);
                    Arrow(c, V(-26f, 0f), V(-6f, 44f), 7f, 16f, W);
                    for (int i = 0; i < 3; i++) Line(c, V(-4f + i * 8f, -4f - i * 8f), V(8f + i * 8f, -16f - i * 8f), 3f, H);
                    break;
                case UpIcon.Dash:
                    c.Fill(p => Sdf.Union(Sdf.Capsule(p, V(4f, 30f), V(32f, 0f), 8f), Sdf.Capsule(p, V(32f, 0f), V(4f, -30f), 8f)), W);
                    for (int i = 0; i < 3; i++) Line(c, V(-56f + i * 6f, 20f - i * 20f), V(-14f, 20f - i * 20f), 3.5f, H);
                    break;
                case UpIcon.Boomerang:
                    c.Fill(p => Sdf.SmoothUnion(Sdf.Capsule(p, V(-34f, 30f), V(4f, -8f), 10f), Sdf.Capsule(p, V(4f, -8f), V(40f, 26f), 10f), 6f), W);
                    Arc(c, V(4f, 10f), 52f, 3.5f, 200f, 330f, H);
                    break;
                case UpIcon.Trident:
                    Line(c, V(0f, -54f), V(0f, 20f), 5f, W);
                    Arc(c, V(0f, 34f), 26f, 7f, 190f, 350f, W);
                    for (int i = -1; i <= 1; i++)
                    {
                        float x = i * 26f;
                        float top = i == 0 ? 54f : 44f;
                        Line(c, V(x, 30f), V(x, top), 4.5f, W);
                        c.Fill(p => Sdf.Triangle(p, V(x - 8f, top - 4f), V(x + 8f, top - 4f), V(x, top + 12f)), W);
                    }
                    break;
                case UpIcon.Fan:
                    for (int i = -1; i <= 1; i++)
                    {
                        Vector2 d = MathUtil.Dir(90f + i * 32f);
                        Arrow(c, V(0f, -44f), V(0f, -44f) + d * 88f, 4.5f, 13f, i == 0 ? W : H);
                    }
                    c.Fill(p => Sdf.Circle(p, V(0f, -44f), 9f), W);
                    break;
                case UpIcon.Storm:
                    c.Fill(p => Sdf.SmoothUnion(Sdf.SmoothUnion(Sdf.Circle(p, V(-18f, 18f), 18f), Sdf.Circle(p, V(8f, 28f), 22f), 6f),
                        Sdf.Box(p, V(0f, 10f), V(40f, 10f), 10f), 6f), H);
                    Bolt(c, V(4f, -20f), 0.95f, W);
                    break;
                case UpIcon.Nova:
                    c.Fill(p => Sdf.Circle(p, Vector2.zero, 18f), W);
                    for (int i = 0; i < 8; i++) { Vector2 d = MathUtil.Dir(i * 45f + 22.5f); c.Fill(p => Sdf.Tapered(p, d * 26f, 5f, d * (i % 2 == 0 ? 54f : 44f), 1f), i % 2 == 0 ? W : H); }
                    c.Fill(p => Sdf.Ring(p, Vector2.zero, 26f, 3f), H);
                    break;
                case UpIcon.Time:
                    c.Fill(p => Sdf.Ring(p, Vector2.zero, 42f, 8f), W);
                    Line(c, Vector2.zero, V(0f, 28f), 4.5f, W);
                    Line(c, Vector2.zero, V(20f, -8f), 4.5f, W);
                    for (int i = 0; i < 12; i++) { Vector2 d = MathUtil.Dir(i * 30f); c.Fill(p => Sdf.Circle(p, d * 32f, i % 3 == 0 ? 3f : 1.8f), H); }
                    break;
                case UpIcon.Frenzy:
                    for (int i = 0; i < 3; i++)
                    {
                        float x = -26f + i * 26f;
                        c.Fill(p => Sdf.Tapered(p, V(x + 16f, 46f), 2f, V(x - 10f, -46f), 6f), i == 1 ? W : H);
                    }
                    break;
                case UpIcon.Cyclone:
                    for (int i = 0; i < 4; i++) Arc(c, V(i * 3f, 30f - i * 20f), 40f - i * 9f, 7f - i, 190f - i * 10f, 400f - i * 10f, i % 2 == 0 ? W : H);
                    break;
                case UpIcon.GoldenBoot:
                    c.Fill(p => Sdf.SmoothUnion(Sdf.Box(p, V(-14f, 10f), V(18f, 30f), 6f), Sdf.Box(p, V(8f, -14f), V(40f, 14f), 12f), 8f), W);
                    for (int i = 0; i < 4; i++) c.Fill(p => Sdf.Box(p, V(-26f + i * 20f, -34f), V(4f, 6f), 2f), H);
                    c.Fill(p => Sdf.Star4(p, V(38f, 36f), 16f, 0.5f), W);
                    break;
                case UpIcon.TwinSun:
                    c.Fill(p => Mathf.Abs(Sdf.Ellipse(p, Vector2.zero, V(50f, 22f))) - 2.5f, H);
                    c.Fill(p => Sdf.Circle(p, V(-10f, 2f), 20f), W);
                    c.Fill(p => Sdf.Circle(p, V(40f, 12f), 11f), W);
                    break;
                case UpIcon.Phoenix:
                    for (int s = -1; s <= 1; s += 2)
                    {
                        int k = s;
                        c.Fill(p => Sdf.Tapered(p, V(0f, 6f), 9f, V(k * 52f, 38f), 2f), W);
                        c.Fill(p => Sdf.Tapered(p, V(k * 6f, 0f), 7f, V(k * 44f, 14f), 1.5f), H);
                    }
                    c.Fill(p => Sdf.Tapered(p, V(0f, 10f), 9f, V(0f, -50f), 2f), W);
                    c.Fill(p => Sdf.Circle(p, V(0f, 24f), 9f), W);
                    break;
                case UpIcon.BlackHole:
                    c.Fill(p => Sdf.Circle(p, Vector2.zero, 44f), H);
                    c.Fill(p => Mathf.Abs(Sdf.Ellipse(p, Vector2.zero, V(54f, 14f))) - 3f, W);
                    c.Erase(p => Sdf.Circle(p, Vector2.zero, 22f));
                    c.Fill(p => Sdf.Ring(p, Vector2.zero, 24f, 4f), W);
                    break;
                case UpIcon.Infinity:
                    c.Fill(p => Sdf.Union(Sdf.Ring(p, V(-22f, 0f), 20f, 9f), Sdf.Ring(p, V(22f, 0f), 20f, 9f)), W);
                    break;
                case UpIcon.Maestro:
                    c.Fill(p => Sdf.Ellipse(p, V(-12f, -30f), V(16f, 11f)), W);
                    Line(c, V(2f, -28f), V(2f, 40f), 4f, W);
                    c.Fill(p => Sdf.Tapered(p, V(2f, 40f), 5f, V(26f, 18f), 2f), W);
                    Line(c, V(-50f, 50f), V(-22f, 20f), 3f, H);
                    c.Fill(p => Sdf.Star4(p, V(38f, -30f), 12f, 0.5f), H);
                    break;
                case UpIcon.Hoop:
                    HoopBall(c, V(0f, 0f), 34f);
                    c.Fill(p => Sdf.Star4(p, V(34f, 34f), 16f, 0.5f), W);
                    break;
                case UpIcon.Swish:
                    // the ring with its net, a ball dropping through
                    c.Fill(p => Mathf.Abs(Sdf.Ellipse(p, V(0f, 14f), V(34f, 8f))) - 3.5f, W);
                    for (int i = 0; i < 5; i++)
                    {
                        float x0 = -30f + i * 15f, x1 = -16f + i * 8f;
                        Line(c, V(x0, 12f), V(x1, -34f), 2.2f, H);
                    }
                    Line(c, V(-16f, -34f), V(16f, -34f), 2.2f, H);
                    HoopBall(c, V(0f, 34f), 16f);
                    break;
                case UpIcon.Slam:
                    for (int i = 0; i < 3; i++)
                    {
                        float rr = 14f + i * 13f;
                        c.Fill(p => Sdf.Intersect(Mathf.Abs(Sdf.Ellipse(p, V(0f, -38f), V(rr * 1.4f, rr * 0.35f))) - 2.8f, p.y + 50f), i == 0 ? W : H);
                    }
                    Arrow(c, V(0f, 54f), V(0f, 22f), 4f, 12f, H);
                    HoopBall(c, V(0f, -2f), 20f);
                    break;
                case UpIcon.Crossover:
                    Line(c, V(-22f, 44f), V(-30f, -44f), 7f, H);
                    Line(c, V(22f, 44f), V(30f, -44f), 7f, H);
                    c.Fill(p => Sdf.Union(Sdf.Capsule(p, V(-44f, 18f), V(0f, -28f), 4f), Sdf.Capsule(p, V(0f, -28f), V(44f, 18f), 4f)), W);
                    HoopBall(c, V(0f, -28f), 13f);
                    break;
                case UpIcon.Palm:
                    c.Fill(p => Sdf.SmoothUnion(Sdf.Box(p, V(-4f, -12f), V(20f, 22f), 9f),
                        Sdf.Union(Sdf.Union(Sdf.Capsule(p, V(-17f, 6f), V(-21f, 40f), 6f), Sdf.Capsule(p, V(-4f, 8f), V(-4f, 48f), 6f)),
                            Sdf.Union(Sdf.Capsule(p, V(9f, 8f), V(12f, 44f), 6f), Sdf.Capsule(p, V(15f, -12f), V(32f, 10f), 6f))), 3f), W);
                    c.Fill(p => Sdf.Star4(p, V(36f, 38f), 14f, 0.5f), H);
                    break;
                case UpIcon.Distance:
                    for (int i = 0; i < 6; i++)
                    {
                        float a = Mathf.Lerp(170f, 20f, i / 5f) * Mathf.Deg2Rad;
                        Vector2 d = V(Mathf.Cos(a) * 46f, Mathf.Sin(a) * 40f - 14f);
                        c.Fill(p => Sdf.Circle(p, d, 3.5f), H);
                    }
                    HoopBall(c, V(44f, -6f), 14f);
                    c.Fill(p => Sdf.Ring(p, V(-46f, -30f), 10f, 3f), W);
                    c.Fill(p => Sdf.Circle(p, V(-46f, -30f), 3f), W);
                    break;
                default: // Synergy
                    c.Fill(p => Sdf.Ring(p, V(-16f, 0f), 28f, 8f), W);
                    c.Fill(p => Sdf.Ring(p, V(16f, 0f), 28f, 8f), H);
                    c.Fill(p => Sdf.Star4(p, V(0f, 0f), 14f, 0.5f), W);
                    break;
            }
        }
    }
}
