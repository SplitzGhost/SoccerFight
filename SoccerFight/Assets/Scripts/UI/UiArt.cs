using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace SoccerFight
{
    /// <summary>Resolution-independent looking HUD sprites (rasterized at 2x) and the Inter font assets.</summary>
    public static class UiArt
    {
        public static Sprite Pill, BarFill, Panel, Circle, Glow, RingThin, RingThick, RingRainbow;
        public static Sprite IconShot, IconFlick, IconMouse, IconMouseRight, LineFade, Heart;
        public static Sprite IconPower, IconStepOver, IconBicycle;
        public static TMP_FontAsset FontBold, FontRegular;
        public static Material FontBoldShadow, FontRegularShadow;

        static bool built;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { built = false; }

        /// <summary>
        /// Canvas units are UI pixels. uGUI measures sprites against referencePixelsPerUnit (100), so the
        /// sprite must use ppu = density * 100 for sliced borders to map 1:1 to UI pixels.
        /// </summary>
        static Sprite ToUi(SdfCanvas c, string name, Vector4 border = default)
        {
            var tex = c.ToTexture(name, false, true, TextureWrapMode.Clamp, false, false);
            var s = Sprite.Create(tex, new Rect(0, 0, c.Width, c.Height), new Vector2(0.5f, 0.5f), c.Ppu * 100f, 0,
                SpriteMeshType.FullRect, border);
            s.name = name;
            return s;
        }

        public static void Build()
        {
            if (built) return;
            built = true;

            // Units here are UI pixels (canvas reference 1920x1080); rasterized at 2x density.
            const float D = 2f;

            var pill = new SdfCanvas(new Rect(-24, -24, 48, 48), D);
            pill.Fill(p => Sdf.Box(p, Vector2.zero, new Vector2(23.5f, 23.5f), 23.5f), Color.white);
            Pill = ToUi(pill, "UiPill", new Vector4(24 * D, 24 * D, 24 * D, 24 * D));

            var fill = new SdfCanvas(new Rect(-24, -24, 48, 48), D);
            fill.Fill(p => Sdf.Box(p, Vector2.zero, new Vector2(23.5f, 23.5f), 23.5f),
                p => Color.Lerp(new Color(0.9f, 0.9f, 0.93f), Color.white, MathUtil.Smooth01((p.y + 14f) / 28f)));
            BarFill = ToUi(fill, "UiBarFill", new Vector4(24 * D, 24 * D, 24 * D, 24 * D));

            var panel = new SdfCanvas(new Rect(-32, -32, 64, 64), D);
            panel.Fill(p => Sdf.Box(p, Vector2.zero, new Vector2(31.5f, 31.5f), 14f), Color.white);
            Panel = ToUi(panel, "UiPanel", new Vector4(16 * D, 16 * D, 16 * D, 16 * D));

            var circle = new SdfCanvas(new Rect(-64, -64, 128, 128), D);
            circle.Fill(p => Sdf.Circle(p, Vector2.zero, 63f), Color.white);
            Circle = ToUi(circle, "UiCircle");

            var glow = new SdfCanvas(new Rect(-64, -64, 128, 128), 1f);
            glow.Field(p =>
            {
                float r = p.magnitude / 64f;
                return new Color(1, 1, 1, Mathf.Exp(-r * r * 4f) * MathUtil.Smooth01((1f - r) / 0.2f));
            });
            Glow = ToUi(glow, "UiGlow");

            var ringThin = new SdfCanvas(new Rect(-64, -64, 128, 128), D);
            ringThin.Fill(p => Sdf.Ring(p, Vector2.zero, 61.5f, 2.2f), Color.white);
            RingThin = ToUi(ringThin, "UiRingThin");

            var ringThick = new SdfCanvas(new Rect(-64, -64, 128, 128), D);
            ringThick.Fill(p => Sdf.Ring(p, Vector2.zero, 59f, 6.5f), Color.white);
            RingThick = ToUi(ringThick, "UiRingThick");

            var ringRb = new SdfCanvas(new Rect(-64, -64, 128, 128), D);
            ringRb.Fill(p => Sdf.Ring(p, Vector2.zero, 59f, 6.5f), p =>
            {
                // clockwise from the top: red → violet, matching the radial fill direction
                float a = Mathf.Repeat(90f - Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg, 360f) / 360f;
                return Color.HSVToRGB(Mathf.Lerp(0f, 0.8f, a), 0.75f, 1f);
            });
            RingRainbow = ToUi(ringRb, "UiRingRainbow");

            var line = new SdfCanvas(new Rect(-64, -2, 128, 4), D);
            line.Field(p => new Color(1, 1, 1, MathUtil.Smooth01((64f - Mathf.Abs(p.x)) / 64f) * MathUtil.Smooth01((2f - Mathf.Abs(p.y)) / 1f)));
            LineFade = ToUi(line, "UiLine");

            BuildIcons(D); BuildTimer.Mark("ui art");
            BuildFonts(); BuildTimer.Mark("fonts");
        }

        static void BuildIcons(float D)
        {
            // Shot: ball with speed lines
            var s = new SdfCanvas(new Rect(-64, -64, 128, 128), D);
            Vector2 bc = new Vector2(14f, 4f);
            for (int i = 0; i < 3; i++)
            {
                float y = bc.y + (i - 1) * 17f;
                float len = i == 1 ? 46f : 32f;
                Vector2 a = new Vector2(bc.x - 30f - len, y + (i - 1) * 3f), b = new Vector2(bc.x - 30f, y);
                s.Fill(p => Sdf.Tapered(p, a, 1.2f, b, 4.2f), new Color(0.55f, 0.95f, 1f, 0.9f));
            }
            s.Fill(p => Sdf.Circle(p, bc, 27f), Color.white);
            s.Fill(p => Sdf.Intersect(Sdf.Circle(p, bc + new Vector2(3f, 2f), 10f), Sdf.Circle(p, bc, 27f)), new Color(0.12f, 0.15f, 0.23f));
            for (int k = 0; k < 5; k++)
            {
                Vector2 d = MathUtil.Dir(90f + k * 72f + 10f);
                Vector2 pc = bc + new Vector2(3f, 2f) + d * 24f;
                s.Fill(p => Sdf.Intersect(Sdf.Circle(p, pc, 8.5f), Sdf.Circle(p, bc, 26.5f)), new Color(0.12f, 0.15f, 0.23f));
            }
            IconShot = ToUi(s, "UiIconShot");

            // Flick: rainbow arc with the ball landing
            var f = new SdfCanvas(new Rect(-64, -64, 128, 128), D);
            Vector2 ac = new Vector2(0f, -22f);
            for (int i = 0; i < 6; i++)
            {
                float r = 52f - i * 7.2f;
                Color col = Color.HSVToRGB(i / 6f * 0.8f, 0.72f, 1f);
                f.Fill(p => Sdf.Intersect(Sdf.Ring(p, ac, r, 6.4f), -(p.y - ac.y) + 0f), col);
            }
            f.Fill(p => Sdf.Circle(p, new Vector2(40f, -30f), 14f), Color.white);
            f.Fill(p => Sdf.Intersect(Sdf.Circle(p, new Vector2(42f, -28f), 5.5f), Sdf.Circle(p, new Vector2(40f, -30f), 13.5f)), new Color(0.12f, 0.15f, 0.23f));
            for (int i = 0; i < 3; i++)
            {
                Vector2 sp = new Vector2(-34f + i * 13f, 34f - i * 5f);
                float sr = 4.5f - i;
                f.Fill(p => Sdf.Star4(p, sp, sr * 2.2f, 0.55f), new Color(1f, 0.95f, 0.75f));
            }
            IconFlick = ToUi(f, "UiIconFlick");

            // Mouse with the left button lit
            var m = new SdfCanvas(new Rect(-16, -20, 32, 40), D * 2f);
            SdfCanvas.SdfFn body = p => Sdf.Box(p, Vector2.zero, new Vector2(11f, 16f), 10.5f);
            m.Fill(p => Mathf.Abs(body(p)) - 1.3f, Color.white);
            m.Fill(p => Sdf.Intersect(Sdf.Intersect(body(p) + 2.6f, 1.5f - p.y), p.x + 0.8f), new Color(0.4f, 0.95f, 1f));
            m.Fill(p => Sdf.Box(p, new Vector2(0f, 6f), new Vector2(0.9f, 7f), 0.9f), Color.white);
            IconMouse = ToUi(m, "UiIconMouse");

            var mr = new SdfCanvas(new Rect(-16, -20, 32, 40), D * 2f);
            mr.Fill(p => Mathf.Abs(body(p)) - 1.3f, Color.white);
            mr.Fill(p => Sdf.Intersect(Sdf.Intersect(body(p) + 2.6f, 1.5f - p.y), 0.8f - p.x), new Color(1f, 0.82f, 0.38f));
            mr.Fill(p => Sdf.Box(p, new Vector2(0f, 6f), new Vector2(0.9f, 7f), 0.9f), Color.white);
            IconMouseRight = ToUi(mr, "UiIconMouseRight");

            BuildSkillIcons(D);

            var h = new SdfCanvas(new Rect(-32, -32, 64, 64), D * 2f);
            h.Fill(p =>
            {
                Vector2 q = new Vector2(Mathf.Abs(p.x), p.y + 4f);
                float lobes = Sdf.Circle(q, new Vector2(10f, 8f), 12f);
                float tip = Sdf.Triangle(new Vector2(p.x, p.y + 4f), new Vector2(-21f, 5f), new Vector2(21f, 5f), new Vector2(0f, -22f));
                return Sdf.SmoothUnion(lobes, tip, 3f);
            }, Color.white);
            Heart = ToUi(h, "UiHeart");
        }

        /// <summary>Small football (white with dark panels) for the skill icons.</summary>
        static void IconBall(SdfCanvas c, Vector2 bc, float r)
        {
            Color panel = new Color(0.12f, 0.15f, 0.23f);
            c.Fill(p => Sdf.Circle(p, bc, r), Color.white);
            Vector2 pc0 = bc + new Vector2(r * 0.1f, r * 0.08f);
            c.Fill(p => Sdf.Intersect(Sdf.Circle(p, pc0, r * 0.37f), Sdf.Circle(p, bc, r)), panel);
            for (int k = 0; k < 5; k++)
            {
                Vector2 pc = pc0 + MathUtil.Dir(90f + k * 72f + 10f) * r * 0.9f;
                c.Fill(p => Sdf.Intersect(Sdf.Circle(p, pc, r * 0.31f), Sdf.Circle(p, bc, r - 0.5f)), panel);
            }
        }

        static void BuildSkillIcons(float D)
        {
            // Power shot: a golden spear drives through the ball
            var pw = new SdfCanvas(new Rect(-64, -64, 128, 128), D);
            Color gold = new Color(1f, 0.8f, 0.36f);
            for (int i = 0; i < 2; i++)
            {
                float x = -52f + i * 14f;
                pw.Fill(p => Sdf.Union(Sdf.Capsule(p, new Vector2(x, 14f), new Vector2(x + 10f, 0f), 3.2f),
                    Sdf.Capsule(p, new Vector2(x + 10f, 0f), new Vector2(x, -14f), 3.2f)), gold.WithAlpha(0.55f + 0.3f * i));
            }
            pw.Fill(p => Sdf.Tapered(p, new Vector2(-30f, 0f), 2.5f, new Vector2(40f, 0f), 8.5f), gold);
            IconBall(pw, new Vector2(2f, 0f), 23f);
            pw.Fill(p => Sdf.Intersect(Sdf.Capsule(p, new Vector2(-21f, 0f), new Vector2(25f, 0f), 3f), Sdf.Circle(p, new Vector2(2f, 0f), 23.5f)), gold.WithAlpha(0.9f));
            pw.Fill(p => Sdf.Triangle(p, new Vector2(34f, 18f), new Vector2(34f, -18f), new Vector2(60f, 0f)), gold);
            IconPower = ToUi(pw, "UiIconPower");

            // Step-over: the foot's loop over the ball, then dash lines
            var so = new SdfCanvas(new Rect(-64, -64, 128, 128), D);
            Color mint = new Color(0.55f, 1f, 0.85f);
            Vector2 sb = new Vector2(-10f, -24f);
            so.Fill(p => Sdf.Intersect(Sdf.Ring(p, sb + new Vector2(0f, 6f), 33f, 6.5f), -(p.y - sb.y - 4f)), mint);
            so.Fill(p => Sdf.Triangle(p, new Vector2(sb.x + 22f, sb.y + 8f), new Vector2(sb.x + 44f, sb.y + 8f), new Vector2(sb.x + 33f, sb.y - 8f)), mint);
            IconBall(so, sb, 19f);
            for (int i = 0; i < 3; i++)
            {
                float y = 26f - i * 13f, len = i == 1 ? 34f : 24f;
                so.Fill(p => Sdf.Tapered(p, new Vector2(58f - len, y), 1.2f, new Vector2(58f, y), 3.6f), mint.WithAlpha(0.9f - i * 0.15f));
            }
            IconStepOver = ToUi(so, "UiIconStepOver");

            // Bicycle kick: an overhead arc from the ball to a burst where it lands
            var bk = new SdfCanvas(new Rect(-64, -64, 128, 128), D);
            Color orange = new Color(1f, 0.55f, 0.28f);
            Vector2 ac = new Vector2(-4f, -12f);
            bk.Fill(p => Sdf.Intersect(Sdf.Ring(p, ac, 38f, 7f), -(p.y - ac.y)), p => Color.Lerp(new Color(1f, 0.9f, 0.6f), orange, MathUtil.Smooth01((p.x + 30f) / 60f)));
            bk.Fill(p => Sdf.Triangle(p, new Vector2(ac.x + 27f, ac.y + 2f), new Vector2(ac.x + 49f, ac.y + 2f), new Vector2(ac.x + 38f, ac.y - 14f)), orange);
            IconBall(bk, new Vector2(ac.x - 38f, ac.y - 8f), 14f);
            Vector2 burst = new Vector2(ac.x + 38f, ac.y - 34f);
            bk.Fill(p => Sdf.Star4(p, burst, 19f, 0.5f), orange);
            bk.Fill(p => Sdf.Star4(MathUtil.Rotate(p - burst, 45f) + burst, burst, 13f, 0.5f), new Color(1f, 0.85f, 0.45f));
            bk.Fill(p => Sdf.Circle(p, burst, 5f), Color.white);
            IconBicycle = ToUi(bk, "UiIconBicycle");
        }

        static void BuildFonts()
        {
            var bold = Resources.Load<Font>("Fonts/Inter-SemiBold");
            var regular = Resources.Load<Font>("Fonts/Inter-Regular");
            FontBold = bold != null ? TMP_FontAsset.CreateFontAsset(bold, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024) : TMP_Settings.defaultFontAsset;
            FontRegular = regular != null ? TMP_FontAsset.CreateFontAsset(regular, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024) : FontBold;
            if (FontBold != null) FontBold.name = "Inter SemiBold SDF";
            if (FontRegular != null) FontRegular.name = "Inter Regular SDF";

            FontBoldShadow = MakeShadowMaterial(FontBold);
            FontRegularShadow = MakeShadowMaterial(FontRegular);
        }

        static Material MakeShadowMaterial(TMP_FontAsset font)
        {
            if (font == null || font.material == null) return null;
            var mat = new Material(font.material) { name = font.name + " Shadow" };
            mat.EnableKeyword("UNDERLAY_ON");
            mat.SetColor("_UnderlayColor", new Color(0f, 0.02f, 0.06f, 0.55f));
            mat.SetFloat("_UnderlayOffsetX", 0f);
            mat.SetFloat("_UnderlayOffsetY", -0.55f);
            mat.SetFloat("_UnderlayDilate", 0.1f);
            mat.SetFloat("_UnderlaySoftness", 0.55f);
            return mat;
        }
    }
}
