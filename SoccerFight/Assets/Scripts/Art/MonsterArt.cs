using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>One stage's monster look: blob and wisp bodies in the theme colours plus a head feature.</summary>
    public sealed class MonsterSkin
    {
        public Sprite BlobBody, BlobFeature, BlobFoot, WispBody, WispTail;
        public Color Glow, WispGlow, Eye;
        public bool EmissiveFeature;
        public float FeatureTilt = 1f;    // horn-style features splay outwards, antennae stand upright
    }

    /// <summary>
    /// Corrupted creatures: soft-shaded bodies with a rim light, glowing eyes and a head feature that
    /// tells which stage they belong to. Skins are generated on demand (once per theme) so a stage
    /// transition only pays for the new colours.
    /// </summary>
    public static class MonsterArt
    {
        public static Sprite BlobBody, BlobHorn, BlobFoot;
        public static Sprite Eye, Pupil, EyeGlow;
        public static Sprite WispBody, WispTail, WispEye;
        public static Sprite Portal, Crown, Orb;

        const float P = 300f;
        static readonly Dictionary<StageTheme, MonsterSkin> skins = new Dictionary<StageTheme, MonsterSkin>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { skins.Clear(); }

        public static void Build()
        {
            skins.Clear();
            var first = Skin(StageThemes.All[0]);
            BlobBody = first.BlobBody; BlobHorn = first.BlobFeature; BlobFoot = first.BlobFoot;
            WispBody = first.WispBody; WispTail = first.WispTail;
            BuildEyes();
            BuildPortal();
            BuildCrown();
            BuildOrb();
        }

        public static MonsterSkin Skin(StageTheme t)
        {
            if (skins.TryGetValue(t, out var s)) return s;
            s = new MonsterSkin
            {
                BlobBody = BuildBlobBody(t), BlobFeature = BuildFeature(t.Feature, t.BlobTop, t.Glow), BlobFoot = BuildFoot(t.BlobBottom),
                WispBody = BuildWispBody(t), WispTail = BuildWispTail(t),
                Glow = t.Glow, WispGlow = t.WispGlow, Eye = t.Eye,
                EmissiveFeature = t.Feature == MonsterFeature.Flames || t.Feature == MonsterFeature.Lamps || t.Feature == MonsterFeature.Stars,
                FeatureTilt = t.Feature == MonsterFeature.Lamps || t.Feature == MonsterFeature.Stars ? 0.35f : 1f,
            };
            skins[t] = s;
            return s;
        }

        static Color Vertical(Vector2 p, float y0, float y1, Color bottom, Color top)
            => Color.Lerp(bottom, top, MathUtil.Smooth01((p.y - y0) / (y1 - y0)));

        static Color Rim(Color top) => Color.Lerp(top, Color.white, 0.55f).WithAlpha(0.75f);

        // ------------------------------------------------------------------ blob

        static Sprite BuildBlobBody(StageTheme t)
        {
            // Pivot at the ground contact point; ~0.9 wide, ~0.78 tall.
            var c = new SdfCanvas(new Rect(-0.55f, -0.05f, 1.1f, 0.95f), P);
            SdfCanvas.SdfFn body = p =>
            {
                float dome = Sdf.Ellipse(p, new Vector2(0f, 0.39f), new Vector2(0.45f, 0.39f));
                float bottom = Sdf.Box(p, new Vector2(0f, 0.16f), new Vector2(0.43f, 0.16f), 0.14f);
                return Sdf.SmoothUnion(dome, bottom, 0.08f);
            };
            c.Fill(body, p => Vertical(p, 0.0f, 0.78f, t.BlobBottom, t.BlobTop));
            // belly glow and rim light
            c.Paint(p => Sdf.Ellipse(p, new Vector2(0.02f, 0.26f), new Vector2(0.24f, 0.16f)), Color.Lerp(t.BlobTop, t.Glow, 0.35f).WithAlpha(0.55f), 0.12f);
            c.Paint(p => Sdf.Intersect(body(p) + 0.01f, -body(p + new Vector2(-0.035f, 0.045f))), Rim(t.BlobTop), 0.012f);
            // markings: speckles, or crystal veins / cracks for the rocky themes
            bool veins = t.Feature == MonsterFeature.Crystals || t.Feature == MonsterFeature.Flames || t.Feature == MonsterFeature.Void;
            if (veins)
            {
                for (int i = 0; i < 4; i++)
                {
                    Vector2 a = new Vector2(-0.3f + i * 0.18f, 0.18f + MathUtil.Hash(i * 5 + 1) * 0.06f);
                    Vector2 b = a + new Vector2(0.08f + MathUtil.Hash(i * 9 + 3) * 0.06f, 0.22f + MathUtil.Hash(i * 3 + 7) * 0.12f);
                    c.Paint(p => Sdf.Intersect(Sdf.Capsule(p, a, b, 0.012f), body(p) + 0.03f), t.Glow.WithAlpha(0.8f), 0.01f);
                }
            }
            else
            {
                for (int i = 0; i < 7; i++)
                {
                    Vector2 sp = new Vector2(MathUtil.Hash(i * 7 + 1) * 0.3f, 0.35f + MathUtil.Hash(i * 13 + 5) * 0.22f);
                    float sr = 0.018f + 0.012f * Mathf.Abs(MathUtil.Hash(i * 3 + 2));
                    c.Paint(p => Sdf.Circle(p, sp, sr), t.BlobBottom.WithAlpha(0.45f), 0.006f);
                }
            }
            return c.ToSprite("BlobBody", Vector2.zero);
        }

        static Sprite BuildFoot(Color bottom)
        {
            var f = new SdfCanvas(new Rect(-0.1f, -0.06f, 0.2f, 0.12f), P);
            f.Fill(p => Sdf.Ellipse(p, Vector2.zero, new Vector2(0.085f, 0.045f)), bottom);
            return f.ToSprite("BlobFoot", Vector2.zero);
        }

        /// <summary>Head feature, pivot at its base. Drawn pointing up; the monster mirrors and tilts them.</summary>
        static Sprite BuildFeature(MonsterFeature f, Color top, Color glow)
        {
            var c = new SdfCanvas(new Rect(-0.11f, -0.05f, 0.24f, 0.36f), P);
            Color light = Color.Lerp(top, Color.white, 0.5f);
            switch (f)
            {
                case MonsterFeature.Thorns:
                    c.Fill(p => Sdf.Triangle(p, new Vector2(-0.045f, 0f), new Vector2(0.045f, 0f), new Vector2(0.03f, 0.25f)),
                        p => Vertical(p, 0f, 0.25f, Color.Lerp(top, Color.black, 0.3f), Color.Lerp(glow, Color.white, 0.3f)));
                    c.Fill(p => Sdf.Triangle(p, new Vector2(-0.02f, 0.06f), new Vector2(0.04f, 0.1f), new Vector2(0.09f, 0.16f)), Color.Lerp(top, Color.black, 0.15f));
                    break;
                case MonsterFeature.Crystals:
                    c.Fill(p => Sdf.SmoothUnion(Sdf.Box(p, new Vector2(0.01f, 0.1f), new Vector2(0.04f, 0.1f), 0.005f, -8f),
                        Sdf.Triangle(p, new Vector2(-0.03f, 0.19f), new Vector2(0.06f, 0.2f), new Vector2(0.03f, 0.29f)), 0.01f),
                        p => Vertical(p, 0f, 0.29f, Color.Lerp(glow, top, 0.5f), Color.Lerp(glow, Color.white, 0.6f)));
                    c.Paint(p => Sdf.Box(p, new Vector2(0.025f, 0.12f), new Vector2(0.012f, 0.09f), 0.004f, -8f), Color.white.WithAlpha(0.6f), 0.01f);
                    break;
                case MonsterFeature.Flames:
                    c.Fill(p => Sdf.SmoothUnion(Sdf.Circle(p, new Vector2(0f, 0.06f), 0.055f),
                        Sdf.Tapered(p, new Vector2(0f, 0.06f), 0.05f, new Vector2(0.03f, 0.28f), 0.004f), 0.03f),
                        p => Vertical(p, 0f, 0.28f, glow, new Color(1f, 0.95f, 0.6f)));
                    c.Paint(p => Sdf.Tapered(p, new Vector2(0f, 0.06f), 0.025f, new Vector2(0.015f, 0.18f), 0.003f), new Color(1f, 1f, 0.85f, 0.9f), 0.015f);
                    break;
                case MonsterFeature.Ice:
                    c.Fill(p => Sdf.Union(Sdf.Triangle(p, new Vector2(-0.04f, 0f), new Vector2(0.05f, 0f), new Vector2(0.02f, 0.3f)),
                        Sdf.Triangle(p, new Vector2(0.01f, 0.02f), new Vector2(0.08f, 0.04f), new Vector2(0.1f, 0.17f))),
                        p => Vertical(p, 0f, 0.3f, new Color(0.62f, 0.82f, 0.95f, 0.95f), new Color(0.95f, 1f, 1f, 0.95f)));
                    c.Paint(p => Sdf.Capsule(p, new Vector2(0f, 0.03f), new Vector2(0.018f, 0.24f), 0.008f), Color.white.WithAlpha(0.7f), 0.008f);
                    break;
                case MonsterFeature.Stars:
                case MonsterFeature.Lamps:
                    c.Fill(p => Sdf.Capsule(p, Vector2.zero, new Vector2(0.01f, 0.2f), 0.012f), Color.Lerp(top, Color.black, 0.2f));
                    if (f == MonsterFeature.Stars) c.Fill(p => Sdf.Star4(p, new Vector2(0.01f, 0.24f), 0.07f, 0.55f), Color.Lerp(glow, Color.white, 0.4f));
                    else
                    {
                        c.Fill(p => Sdf.Box(p, new Vector2(0.01f, 0.25f), new Vector2(0.04f, 0.05f), 0.015f), Color.Lerp(top, Color.black, 0.4f));
                        c.Fill(p => Sdf.Box(p, new Vector2(0.01f, 0.25f), new Vector2(0.026f, 0.036f), 0.012f), new Color(1f, 0.9f, 0.6f));
                    }
                    break;
                case MonsterFeature.Void:
                    c.Fill(p => Sdf.Union(Sdf.Triangle(p, new Vector2(-0.05f, 0f), new Vector2(0.04f, 0f), new Vector2(-0.01f, 0.3f)),
                        Sdf.Triangle(p, new Vector2(0f, 0.05f), new Vector2(0.07f, 0.06f), new Vector2(0.09f, 0.2f))), Color.Lerp(top, Color.black, 0.5f));
                    c.Paint(p => Sdf.Intersect(Sdf.Triangle(p, new Vector2(-0.05f, 0f), new Vector2(0.04f, 0f), new Vector2(-0.01f, 0.3f)) + 0.012f,
                        -Sdf.Triangle(p + new Vector2(-0.008f, 0f), new Vector2(-0.05f, 0f), new Vector2(0.04f, 0f), new Vector2(-0.01f, 0.3f))), glow, 0.01f);
                    break;
                default:   // horns
                    c.Fill(p => Sdf.Tapered(p, Vector2.zero, 0.055f, new Vector2(0.06f, 0.22f), 0.008f), p => Vertical(p, 0f, 0.22f, top, light));
                    break;
            }
            return c.ToSprite("Feature" + f, Vector2.zero);
        }

        // ------------------------------------------------------------------ wisp

        static Sprite BuildWispBody(StageTheme t)
        {
            var c = new SdfCanvas(new Rect(-0.38f, -0.38f, 0.76f, 0.76f), P);
            SdfCanvas.SdfFn body = p => Sdf.Circle(p, Vector2.zero, 0.31f);
            c.Fill(body, p => Vertical(p, -0.3f, 0.3f, t.WispBottom, t.WispTop));
            c.Paint(p => Sdf.Intersect(body(p) + 0.01f, -body(p + new Vector2(-0.03f, 0.04f))), Rim(t.WispTop), 0.012f);
            c.Paint(p => Sdf.Circle(p, new Vector2(0f, -0.05f), 0.2f), t.WispGlow.WithAlpha(0.25f), 0.12f);
            return c.ToSprite("WispBody", Vector2.zero);
        }

        static Sprite BuildWispTail(StageTheme t)
        {
            var c = new SdfCanvas(new Rect(-0.2f, -0.2f, 0.4f, 0.4f), P);
            c.Fill(p => Sdf.Circle(p, Vector2.zero, 0.17f), p => Vertical(p, -0.17f, 0.17f, t.WispBottom, t.WispTop));
            return c.ToSprite("WispTail", Vector2.zero);
        }

        // ------------------------------------------------------------------ shared

        static void BuildEyes()
        {
            // white so the skin can tint it (eyes glow in the theme's eye colour)
            var e = new SdfCanvas(new Rect(-0.08f, -0.1f, 0.16f, 0.2f), P);
            e.Fill(p => Sdf.Ellipse(p, Vector2.zero, new Vector2(0.058f, 0.078f)), Color.white);
            Eye = e.ToSprite("Eye", Vector2.zero);

            var pu = new SdfCanvas(new Rect(-0.04f, -0.06f, 0.08f, 0.12f), P);
            pu.Fill(p => Sdf.Ellipse(p, Vector2.zero, new Vector2(0.018f, 0.045f)), new Color(0.12f, 0.04f, 0.16f));
            Pupil = pu.ToSprite("Pupil", Vector2.zero);

            var we = new SdfCanvas(new Rect(-0.16f, -0.16f, 0.32f, 0.32f), P);
            we.Fill(p => Sdf.Circle(p, Vector2.zero, 0.13f), new Color(1f, 0.98f, 0.9f));
            we.Fill(p => Sdf.Ellipse(p, new Vector2(0.03f, 0f), new Vector2(0.035f, 0.085f)), new Color(0.1f, 0.05f, 0.2f));
            WispEye = we.ToSprite("WispEye", Vector2.zero);

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

        /// <summary>Spiked crown worn by bosses (white, tinted per theme).</summary>
        static void BuildCrown()
        {
            var c = new SdfCanvas(new Rect(-0.32f, -0.06f, 0.64f, 0.36f), P);
            SdfCanvas.SdfFn crown = p =>
            {
                float band = Sdf.Box(p, new Vector2(0f, 0.04f), new Vector2(0.26f, 0.045f), 0.02f);
                float d = band;
                for (int i = 0; i < 5; i++)
                {
                    float x = -0.2f + i * 0.1f, h = i == 2 ? 0.27f : (i == 1 || i == 3) ? 0.2f : 0.15f;
                    d = Sdf.SmoothUnion(d, Sdf.Triangle(p, new Vector2(x - 0.05f, 0.05f), new Vector2(x + 0.05f, 0.05f), new Vector2(x, h)), 0.01f);
                }
                return d;
            };
            c.Fill(p => crown(p) - 0.01f, new Color(0.15f, 0.1f, 0.05f));
            c.Fill(crown, p => Vertical(p, 0f, 0.27f, new Color(0.8f, 0.55f, 0.2f), new Color(1f, 0.93f, 0.6f)));
            for (int i = 0; i < 3; i++)
            {
                float x = -0.1f + i * 0.1f;
                c.Fill(p => Sdf.Circle(p, new Vector2(x, 0.045f), 0.02f), i == 1 ? new Color(1f, 0.3f, 0.4f) : new Color(0.4f, 0.9f, 1f));
            }
            Crown = c.ToSprite("Crown", new Vector2(0f, 0f));
        }

        /// <summary>Glowing projectile core (white, tinted per shooter).</summary>
        static void BuildOrb()
        {
            var c = new SdfCanvas(new Rect(-0.16f, -0.16f, 0.32f, 0.32f), 256f);
            c.Fill(p => Sdf.Circle(p, Vector2.zero, 0.1f), p => Color.Lerp(Color.white, new Color(1f, 1f, 1f, 0.85f), MathUtil.Smooth01(p.magnitude / 0.1f)), 0.03f);
            Orb = c.ToSprite("Orb", Vector2.zero);
        }
    }
}
