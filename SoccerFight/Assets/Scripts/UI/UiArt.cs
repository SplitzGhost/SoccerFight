using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace SoccerFight
{
    /// <summary>Resolution-independent looking HUD sprites (rasterized at 2x) and the font assets (Lilita One + Inter).</summary>
    public static class UiArt
    {
        public static Sprite Pill, BarFill, Panel, PanelRing, Circle, Glow, RingThin, RingThick, RingRainbow;
        public static Sprite IconShot, IconFlick, IconMouse, IconMouseRight, LineFade, Heart;
        public static Sprite IconPower, IconStepOver, IconBicycle, IconJuggle, IconAirKick, IconLock, Diamond;
        public static Sprite IconTackle, IconPunt, IconWall, IconNutmeg, IconDecoy, IconWhistle, IconHeader, IconDash;
        public static Sprite IconThrow, IconThree, IconCrossover, IconDunk, IconAlleyOop, IconBlock, IconFastBreak;
        public static TMP_FontAsset FontBold, FontRegular;
        public static Material FontBoldShadow, FontRegularShadow;
        /// <summary>The display face is chunky: the wide tracking the old UI used for caps would tear its words apart.</summary>
        public const float DisplayTracking = 0.3f;

        static bool built;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { built = false; }

        /// <summary>
        /// Canvas units are UI pixels. uGUI measures sprites against referencePixelsPerUnit (100), so the
        /// sprite must use ppu = density * 100 for sliced borders to map 1:1 to UI pixels.
        /// </summary>
        internal static Sprite ToUi(SdfCanvas c, string name, Vector4 border = default)
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

            // outline of the panel shape: borders drawn on top of glass never tint it while fading
            var panelRing = new SdfCanvas(new Rect(-32, -32, 64, 64), D);
            panelRing.Fill(p => Mathf.Abs(Sdf.Box(p, Vector2.zero, new Vector2(30.75f, 30.75f), 14f)) - 0.9f, Color.white);
            PanelRing = ToUi(panelRing, "UiPanelRing", new Vector4(16 * D, 16 * D, 16 * D, 16 * D));

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

            // Keep-ups: the ball floats above a raised knee, the rhythm dotted in between
            var jg = new SdfCanvas(new Rect(-64, -64, 128, 128), D);
            Color heal = new Color(0.55f, 1f, 0.75f);
            jg.Fill(p => Sdf.Intersect(Sdf.Ring(p, new Vector2(-6f, -58f), 44f, 6.5f), Sdf.HalfPlane(p, new Vector2(-6f, -30f), Vector2.down)), heal);
            for (int i = 0; i < 3; i++)
            {
                Vector2 dp = new Vector2(-6f + (i - 1) * 3f, -8f + i * 11f);
                jg.Fill(p => Sdf.Circle(p, dp, 3.2f - i * 0.5f), heal.WithAlpha(0.9f - i * 0.2f));
            }
            IconBall(jg, new Vector2(-4f, 34f), 21f);
            jg.Fill(p => Sdf.Star4(p, new Vector2(30f, 14f), 11f, 0.5f), new Color(1f, 0.95f, 0.75f));
            IconJuggle = ToUi(jg, "UiIconJuggle");

            // Air kick: the ball shoots down-forward, the recoil throws the body up-back
            var ak = new SdfCanvas(new Rect(-64, -64, 128, 128), D);
            Color cyan = new Color(0.55f, 0.95f, 1f);
            Vector2 ab = new Vector2(24f, -26f);
            for (int i = 0; i < 3; i++)
            {
                Vector2 o = new Vector2((i - 1) * 12f, (i - 1) * -12f);
                ak.Fill(p => Sdf.Tapered(p, ab + new Vector2(-40f, 40f) + o * 0.6f, 1.2f, ab + new Vector2(-18f, 18f) + o * 0.4f, 3.6f), cyan.WithAlpha(0.8f));
            }
            IconBall(ak, ab, 19f);
            Vector2 ua = new Vector2(-30f, 10f), ub = new Vector2(-8f, 40f);
            ak.Fill(p => Sdf.Capsule(p, ua, ub, 5f), Color.white);
            Vector2 ud = (ub - ua).normalized, un = new Vector2(-ud.y, ud.x);
            ak.Fill(p => Sdf.Triangle(p, ub + ud * 14f, ub + un * 11f - ud * 2f, ub - un * 11f - ud * 2f), Color.white);
            IconAirKick = ToUi(ak, "UiIconAirKick");

            BuildMoveIcons(D);
            BuildHoopsIcons(D);

            // Lock for abilities the run hasn't unlocked yet
            var lk = new SdfCanvas(new Rect(-32, -32, 64, 64), D * 2f);
            lk.Fill(p => Sdf.Intersect(Sdf.Ring(p, new Vector2(0f, 4f), 11f, 3.6f), -(p.y - 4f)), Color.white);
            lk.Fill(p => Sdf.Capsule(p, new Vector2(-11f, 4f), new Vector2(-11f, -2f), 1.8f), Color.white);
            lk.Fill(p => Sdf.Capsule(p, new Vector2(11f, 4f), new Vector2(11f, -2f), 1.8f), Color.white);
            lk.Fill(p => Sdf.Subtract(Sdf.Box(p, new Vector2(0f, -10f), new Vector2(16f, 12f), 4f),
                Sdf.Union(Sdf.Circle(p, new Vector2(0f, -7f), 3.4f), Sdf.Box(p, new Vector2(0f, -13f), new Vector2(1.5f, 5f)))), Color.white);
            IconLock = ToUi(lk, "UiIconLock");

            var dm = new SdfCanvas(new Rect(-16, -16, 32, 32), D * 2f);
            dm.Fill(p => Sdf.Box(p, Vector2.zero, new Vector2(9f, 9f), 2f, 45f), Color.white);
            Diamond = ToUi(dm, "UiDiamond");
        }


        /// <summary>Icons for the six later moves. Each carries its own colours; the HUD draws them white.</summary>
        static void BuildMoveIcons(float D)
        {
            var rect = new Rect(-64, -64, 128, 128);
            Color dark = new Color(0.12f, 0.15f, 0.23f);

            // Slide tackle: the leg goes in low, turf sprays up behind the boot
            var tk = new SdfCanvas(rect, D);
            Color turf = new Color(0.61f, 0.9f, 0.39f);
            tk.Fill(p => Sdf.Box(p, new Vector2(0f, -46f), new Vector2(54f, 2.6f), 2.6f), turf.WithAlpha(0.9f));
            for (int i = 0; i < 5; i++)
            {
                Vector2 c = new Vector2(-50f + i * 11f, -34f + (i % 2) * 14f);
                float ang = 20f + i * 23f;
                tk.Fill(p => Sdf.Box(p, c, new Vector2(6f, 3.4f), 1.6f, ang), turf.WithAlpha(0.9f - i * 0.13f));
            }
            tk.Fill(p => Sdf.Union(Sdf.Capsule(p, new Vector2(-34f, 14f), new Vector2(-2f, -14f), 9.5f),
                                   Sdf.Capsule(p, new Vector2(-2f, -14f), new Vector2(30f, -26f), 8f)), Color.white);
            tk.Fill(p => Sdf.Box(p, new Vector2(40f, -30f), new Vector2(13f, 6.5f), 3f, -10f), dark);
            IconTackle = ToUi(tk, "UiIconTackle");

            // Goal kick: the ball comes down out of the sky into a target ring
            var pu = new SdfCanvas(rect, D);
            Color amber = new Color(1f, 0.69f, 0.23f);
            pu.Fill(p => Sdf.Ring(p, new Vector2(0f, -38f), 26f, 4f), amber);
            pu.Fill(p => Sdf.Ring(p, new Vector2(0f, -38f), 12f, 3f), amber.WithAlpha(0.75f));
            pu.Fill(p => Sdf.Circle(p, new Vector2(0f, -38f), 3.4f), Color.white);
            for (int i = 0; i < 3; i++)
            {
                Vector2 a = new Vector2(34f + i * 12f, 60f - i * 10f), b = new Vector2(12f + i * 12f, 16f - i * 10f);
                pu.Fill(p => Sdf.Tapered(p, a, 1.2f, b, 4f), amber.WithAlpha(0.85f - i * 0.2f));
            }
            IconBall(pu, new Vector2(4f, 8f), 20f);
            IconWhiteSparks(pu, new Vector2(0f, -38f), amber);
            IconPunt = ToUi(pu, "UiIconPunt");

            // Wall: three defenders shoulder to shoulder, a bolt breaking on them
            var wl = new SdfCanvas(rect, D);
            Color guard = new Color(0.36f, 0.55f, 1f);
            wl.Fill(p => Sdf.Box(p, new Vector2(0f, -46f), new Vector2(52f, 2.6f), 2.6f), guard.WithAlpha(0.8f));
            for (int i = 0; i < 3; i++)
            {
                float x = -28f + i * 28f;
                float h = i == 1 ? 26f : 22f;
                wl.Fill(p => Sdf.Box(p, new Vector2(x, -18f + h - 22f), new Vector2(11f, h), 8f), Color.white);
                wl.Fill(p => Sdf.Circle(p, new Vector2(x, h + 4f), 9f), Color.white);
            }
            for (int i = 0; i < 3; i++)
            {
                Vector2 a = new Vector2(62f, 30f - i * 6f), b = new Vector2(38f, 22f - i * 6f);
                wl.Fill(p => Sdf.Tapered(p, a, 1f, b, 3.4f), guard.WithAlpha(0.9f - i * 0.25f));
            }
            wl.Fill(p => Sdf.Star4(p, new Vector2(34f, 22f), 15f, 0.45f), Color.white);
            IconWall = ToUi(wl, "UiIconWall");

            // Nutmeg: the ball rolls between two legs
            var nm = new SdfCanvas(rect, D);
            Color pink = new Color(1f, 0.44f, 0.84f);
            nm.Fill(p => Sdf.Box(p, new Vector2(0f, 38f), new Vector2(28f, 10f), 8f), Color.white);
            nm.Fill(p => Sdf.Capsule(p, new Vector2(-22f, 34f), new Vector2(-30f, -34f), 9f), Color.white);
            nm.Fill(p => Sdf.Capsule(p, new Vector2(22f, 34f), new Vector2(30f, -34f), 9f), Color.white);
            nm.Fill(p => Sdf.Box(p, new Vector2(-34f, -42f), new Vector2(11f, 5.5f), 3f), dark);
            nm.Fill(p => Sdf.Box(p, new Vector2(34f, -42f), new Vector2(11f, 5.5f), 3f), dark);
            for (int i = 0; i < 3; i++)
            {
                float y = 4f + (i - 1) * 13f;
                nm.Fill(p => Sdf.Tapered(p, new Vector2(-16f, y), 1f, new Vector2(-2f, y), 3f), pink.WithAlpha(0.85f - Mathf.Abs(i - 1) * 0.25f));
            }
            IconBall(nm, new Vector2(16f, 2f), 17f);
            IconNutmeg = ToUi(nm, "UiIconNutmeg");

            // Decoy: the real body steps aside, the ghost stays behind
            var dc = new SdfCanvas(rect, D);
            Color trick = new Color(0.78f, 0.49f, 1f);
            dc.Fill(p => Sdf.Union(Sdf.Circle(p, new Vector2(20f, 28f), 12f),
                                   Sdf.Capsule(p, new Vector2(20f, 8f), new Vector2(20f, -28f), 14f)), trick.WithAlpha(0.45f));
            dc.Fill(p => Sdf.Union(Sdf.Circle(p, new Vector2(-16f, 30f), 13f),
                                   Sdf.Capsule(p, new Vector2(-16f, 8f), new Vector2(-16f, -30f), 15f)), Color.white);
            for (int i = 0; i < 3; i++)
            {
                float x = -1f + i * 7f;
                dc.Fill(p => Sdf.Box(p, new Vector2(x, 2f + i * 4f), new Vector2(2.4f, 6f), 2.4f), trick.WithAlpha(0.8f - i * 0.2f));
            }
            IconDecoy = ToUi(dc, "UiIconDecoy");

            // Whistle: the referee's whistle with two sound arcs
            var wh = new SdfCanvas(rect, D);
            Color silver = new Color(0.75f, 0.91f, 1f);
            wh.Fill(p => Sdf.Union(Sdf.Box(p, new Vector2(-10f, -6f), new Vector2(23f, 16f), 9f),
                                   Sdf.Box(p, new Vector2(20f, 2f), new Vector2(16f, 7f), 4f)), Color.white);
            wh.Fill(p => Sdf.Circle(p, new Vector2(-16f, -4f), 6f), dark);
            wh.Fill(p => Sdf.Capsule(p, new Vector2(-22f, 12f), new Vector2(-4f, 24f), 3f), silver);
            for (int i = 0; i < 2; i++)
            {
                float r = 20f + i * 13f;
                wh.Fill(p => Sdf.Intersect(Sdf.Ring(p, new Vector2(30f, 26f), r, 4f), p.x - 32f), silver.WithAlpha(0.9f - i * 0.3f));
            }
            IconWhistle = ToUi(wh, "UiIconWhistle");

            // Header: a head in profile snaps forward, the ball leaves the forehead with a burst
            var hd = new SdfCanvas(rect, D);
            Color blue = new Color(0.55f, 0.8f, 1f);
            hd.Fill(p => Sdf.Union(Sdf.Circle(p, new Vector2(-18f, 4f), 25f),
                                   Sdf.Capsule(p, new Vector2(-24f, -18f), new Vector2(-30f, -46f), 11f)), Color.white);
            hd.Fill(p => Sdf.Intersect(Sdf.Circle(p, new Vector2(-18f, 4f), 25f), -(p.y - 14f)), blue.WithAlpha(0.55f));
            for (int i = 0; i < 3; i++)
            {
                float y = 18f - i * 13f;
                hd.Fill(p => Sdf.Tapered(p, new Vector2(-56f, y + 4f), 1f, new Vector2(-44f, y), 3f), blue.WithAlpha(0.85f - i * 0.2f));
            }
            hd.Fill(p => Sdf.Star4(p, new Vector2(12f, 14f), 13f, 0.45f), new Color(1f, 0.95f, 0.75f));
            IconBall(hd, new Vector2(34f, 26f), 17f);
            IconHeader = ToUi(hd, "UiIconHeader");

            // Dash (the skiller's burst): a double chevron shooting forward out of three speed lines
            var ds = new SdfCanvas(rect, D);
            Color violet = new Color(0.82f, 0.6f, 1f);
            for (int i = 0; i < 3; i++)
            {
                float y = 18f - i * 18f, len = i == 1 ? 40f : 28f;
                ds.Fill(p => Sdf.Tapered(p, new Vector2(-58f, y), 1.2f, new Vector2(-58f + len, y), 4f), violet.WithAlpha(0.9f - Mathf.Abs(i - 1) * 0.3f));
            }
            for (int i = 0; i < 2; i++)
            {
                float x = -10f + i * 26f;
                ds.Fill(p => Sdf.Union(Sdf.Capsule(p, new Vector2(x, 28f), new Vector2(x + 24f, 0f), 6.5f),
                                       Sdf.Capsule(p, new Vector2(x + 24f, 0f), new Vector2(x, -28f), 6.5f)), i == 0 ? violet : Color.white);
            }
            IconBall(ds, new Vector2(42f, -40f), 14f);
            IconDash = ToUi(ds, "UiIconDash");
        }

        /// <summary>A basketball glyph: orange with dark ribs (a cross and the two curved seams).</summary>
        static void IconHoopBall(SdfCanvas c, Vector2 bc, float r)
        {
            Color leather = new Color(1f, 0.55f, 0.24f), rib = new Color(0.12f, 0.08f, 0.08f);
            c.Fill(p => Sdf.Circle(p, bc, r), leather);
            c.Fill(p => Sdf.Intersect(Sdf.Circle(p, bc + new Vector2(-r * 0.3f, r * 0.35f), r * 0.55f), Sdf.Circle(p, bc, r)), new Color(1f, 0.72f, 0.45f, 0.5f));
            float w = Mathf.Max(1.4f, r * 0.08f);
            c.Fill(p => Sdf.Intersect(Mathf.Abs(p.x - bc.x) - w, Sdf.Circle(p, bc, r - 0.5f)), rib);
            c.Fill(p => Sdf.Intersect(Mathf.Abs(p.y - bc.y) - w, Sdf.Circle(p, bc, r - 0.5f)), rib);
            for (int s = -1; s <= 1; s += 2)
            {
                int k = s;
                c.Fill(p => Sdf.Intersect(Mathf.Abs(Sdf.Circle(p, bc + new Vector2(k * r * 1.25f, 0f), r * 0.9f)) - w, Sdf.Circle(p, bc, r - 0.5f)), rib);
            }
        }

        /// <summary>Icons for the basketball moves, in the same language as the soccer ones.</summary>
        static void BuildHoopsIcons(float D)
        {
            var rect = new Rect(-64, -64, 128, 128);
            Color orange = new Color(1f, 0.6f, 0.26f);

            // Throw: the ball leaves the hand with speed lines
            var th = new SdfCanvas(rect, D);
            for (int i = 0; i < 3; i++)
            {
                float y = 4f + (i - 1) * 17f;
                float len = i == 1 ? 46f : 32f;
                th.Fill(p => Sdf.Tapered(p, new Vector2(-16f - len, y + (i - 1) * 3f), 1.2f, new Vector2(-16f, y), 4.2f), orange.WithAlpha(0.9f));
            }
            IconHoopBall(th, new Vector2(14f, 4f), 27f);
            IconThrow = ToUi(th, "UiIconThrow");

            // Three: a high arc into a ring with a burst, a big 3 at the start
            var tr = new SdfCanvas(rect, D);
            for (int i = 0; i < 5; i++)
            {
                float a = Mathf.Lerp(160f, 30f, i / 4f) * Mathf.Deg2Rad;
                Vector2 dot = new Vector2(Mathf.Cos(a) * 44f - 4f, Mathf.Sin(a) * 40f - 12f);
                tr.Fill(p => Sdf.Circle(p, dot, 3.2f + i * 0.5f), orange.WithAlpha(0.5f + i * 0.1f));
            }
            tr.Fill(p => Sdf.Ring(p, new Vector2(40f, -40f), 14f, 3f), Color.white);
            IconWhiteSparks(tr, new Vector2(40f, -40f), orange);
            IconHoopBall(tr, new Vector2(34f, -6f), 15f);
            // the digit 3, built from bars
            Vector2 o = new Vector2(-52f, -50f);
            tr.Fill(p => Mathf.Min(Mathf.Min(Sdf.Box(p, o + new Vector2(12f, 34f), new Vector2(12f, 3.5f), 2f), Sdf.Box(p, o + new Vector2(12f, 17f), new Vector2(10f, 3.5f), 2f)),
                Mathf.Min(Sdf.Box(p, o + new Vector2(12f, 0f), new Vector2(12f, 3.5f), 2f), Sdf.Box(p, o + new Vector2(22f, 17f), new Vector2(3.5f, 17f), 2f))), Color.white);
            IconThree = ToUi(tr, "UiIconThree");

            // Crossover: the ball zig-zags between two legs, a purple speed chevron after it
            var cr = new SdfCanvas(rect, D);
            Color violet = new Color(0.82f, 0.6f, 1f);
            cr.Fill(p => Sdf.Capsule(p, new Vector2(-24f, 40f), new Vector2(-34f, -44f), 9f), Color.white.WithAlpha(0.9f));
            cr.Fill(p => Sdf.Capsule(p, new Vector2(20f, 40f), new Vector2(32f, -44f), 9f), Color.white.WithAlpha(0.9f));
            cr.Fill(p => Sdf.Union(Sdf.Capsule(p, new Vector2(-46f, 20f), new Vector2(0f, -30f), 3.5f), Sdf.Capsule(p, new Vector2(0f, -30f), new Vector2(46f, 20f), 3.5f)), violet);
            IconHoopBall(cr, new Vector2(0f, -30f), 14f);
            IconCrossover = ToUi(cr, "UiIconCrossover");

            // Dunk: the ball hammered down, shock rings spreading on the floor
            var dk = new SdfCanvas(rect, D);
            Color slam = new Color(0.62f, 0.85f, 1f);
            for (int i = 0; i < 3; i++)
            {
                float rr = 16f + i * 15f;
                dk.Fill(p => Sdf.Intersect(Mathf.Abs(Sdf.Ellipse(p, new Vector2(0f, -44f), new Vector2(rr * 1.4f, rr * 0.35f))) - 2.6f, p.y + 56f), slam.WithAlpha(0.95f - i * 0.25f));
            }
            for (int i = 0; i < 3; i++)
            {
                float x = -14f + i * 14f;
                dk.Fill(p => Sdf.Tapered(p, new Vector2(x, 58f), 1.2f, new Vector2(x, 26f), 3.6f), slam.WithAlpha(0.85f - Mathf.Abs(i - 1) * 0.3f));
            }
            IconHoopBall(dk, new Vector2(0f, -8f), 22f);
            IconDunk = ToUi(dk, "UiIconDunk");

            // Alley-oop: the ball hangs high over a gold arrow pointing down at a target
            var oo = new SdfCanvas(rect, D);
            Color gold = new Color(1f, 0.85f, 0.4f);
            oo.Fill(p => Sdf.Ring(p, new Vector2(20f, -44f), 12f, 3f), gold);
            oo.Fill(p => Sdf.Union(Sdf.Capsule(p, new Vector2(-40f, -50f), new Vector2(-18f, 40f), 3f), Sdf.Capsule(p, new Vector2(-18f, 40f), new Vector2(12f, -26f), 3f)), gold.WithAlpha(0.8f));
            oo.Fill(p => Sdf.Triangle(p, new Vector2(4f, -20f), new Vector2(24f, -22f), new Vector2(15f, -40f)), gold);
            IconHoopBall(oo, new Vector2(-16f, 38f), 17f);
            oo.Fill(p => Sdf.Star4(p, new Vector2(10f, 52f), 10f, 0.45f), Color.white);
            IconAlleyOop = ToUi(oo, "UiIconAlleyOop");

            // Block: an open hand raised, a bolt breaking on it and flying back
            var bl = new SdfCanvas(rect, D);
            Color guard = new Color(0.45f, 0.65f, 1f);
            bl.Fill(p => Sdf.SmoothUnion(Sdf.Box(p, new Vector2(-6f, -14f), new Vector2(18f, 20f), 8f),
                Sdf.Union(Sdf.Union(Sdf.Capsule(p, new Vector2(-18f, 4f), new Vector2(-22f, 38f), 5.5f), Sdf.Capsule(p, new Vector2(-6f, 6f), new Vector2(-6f, 46f), 5.5f)),
                    Sdf.Union(Sdf.Capsule(p, new Vector2(6f, 6f), new Vector2(9f, 42f), 5.5f), Sdf.Capsule(p, new Vector2(12f, -14f), new Vector2(28f, 8f), 5.5f))), 3f), Color.white);
            bl.Fill(p => Sdf.Capsule(p, new Vector2(-6f, -34f), new Vector2(-8f, -58f), 12f), Color.white);
            for (int i = 0; i < 3; i++)
            {
                Vector2 a = new Vector2(34f, 30f - i * 8f), b = new Vector2(58f, 38f - i * 12f);
                bl.Fill(p => Sdf.Tapered(p, a, 3.4f, b, 1f), guard.WithAlpha(0.95f - i * 0.25f));
            }
            bl.Fill(p => Sdf.Star4(p, new Vector2(28f, 26f), 15f, 0.45f), guard);
            IconBlock = ToUi(bl, "UiIconBlock");

            // Fast break: speed lines behind a ball pounded low, a mint chevron driving forward
            var fb = new SdfCanvas(rect, D);
            Color mint = new Color(0.55f, 1f, 0.85f);
            for (int i = 0; i < 3; i++)
            {
                float y = 26f - i * 16f, len = i == 1 ? 44f : 30f;
                fb.Fill(p => Sdf.Tapered(p, new Vector2(-58f, y), 1.2f, new Vector2(-58f + len, y), 4f), mint.WithAlpha(0.9f - Mathf.Abs(i - 1) * 0.3f));
            }
            fb.Fill(p => Sdf.Union(Sdf.Capsule(p, new Vector2(4f, 34f), new Vector2(34f, 8f), 7f), Sdf.Capsule(p, new Vector2(34f, 8f), new Vector2(4f, -18f), 7f)), Color.white);
            IconHoopBall(fb, new Vector2(26f, -40f), 16f);
            fb.Fill(p => Sdf.Intersect(Mathf.Abs(p.y + 58f) - 2f, Mathf.Abs(p.x - 26f) - 28f), mint.WithAlpha(0.7f));
            IconFastBreak = ToUi(fb, "UiIconFastBreak");
        }

        static void IconWhiteSparks(SdfCanvas c, Vector2 at, Color col)
        {
            for (int i = 0; i < 4; i++)
            {
                Vector2 d = MathUtil.Dir(25f + i * 40f);
                Vector2 a = at + d * 30f, b = at + d * 44f;
                c.Fill(p => Sdf.Tapered(p, a, 3f, b, 1f), col.WithAlpha(0.8f));
            }
        }
        static void BuildFonts()
        {
            // Headings, buttons and numbers: Lilita One, a chunky rounded display face (the Project Rise
            // look). Body text: Inter SemiBold, which stays readable in long card descriptions.
            var display = Resources.Load<Font>("Fonts/LilitaOne-Regular");
            var body = Resources.Load<Font>("Fonts/Inter-SemiBold");
            FontBold = display != null ? TMP_FontAsset.CreateFontAsset(display, 90, 12, GlyphRenderMode.SDFAA, 1024, 1024) : TMP_Settings.defaultFontAsset;
            FontRegular = body != null ? TMP_FontAsset.CreateFontAsset(body, 90, 12, GlyphRenderMode.SDFAA, 1024, 1024) : FontBold;
            if (FontBold != null) FontBold.name = "Lilita One SDF";
            if (FontRegular != null) FontRegular.name = "Inter SemiBold SDF";

            FontBoldShadow = MakeShadowMaterial(FontBold, 0.34f);
            FontRegularShadow = MakeShadowMaterial(FontRegular, 0.24f);
        }

        /// <summary>
        /// Cartoon print: a dark ink halo around the letters that drops a little downwards, so white
        /// text reads on the bright sky and on light plates alike.
        /// </summary>
        static Material MakeShadowMaterial(TMP_FontAsset font, float dilate)
        {
            if (font == null || font.material == null) return null;
            var mat = new Material(font.material) { name = font.name + " Shadow" };
            mat.EnableKeyword("UNDERLAY_ON");
            mat.SetColor("_UnderlayColor", new Color(0.08f, 0.11f, 0.2f, 0.85f));
            mat.SetFloat("_UnderlayOffsetX", 0f);
            mat.SetFloat("_UnderlayOffsetY", -0.55f);
            mat.SetFloat("_UnderlayDilate", dilate);
            mat.SetFloat("_UnderlaySoftness", 0.12f);
            return mat;
        }
    }
}
