using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// The basketball bodies: a tank top with trim, side panels and the number on the chest, long
    /// loose shorts that end above the knee, crew socks, chunky high-top sneakers, open hands for the
    /// dribble and three heads (fade with a headband, buzz cut with a beard, cornrows with long
    /// braids). Bones and shoulder width come from the character's PlayerBody, so the same code
    /// draws the lanky shooter and the broad centre. Same pivots, contour and shading rules as the
    /// soccer parts, so the rig and the character shader treat them alike.
    /// </summary>
    public static partial class PlayerArt
    {
        /// <summary>Width of the current build (1 = soccer shoulders).</summary>
        static float Wd => Bd.Build;

        static readonly Color SoleWhite = new Color(0.93f, 0.91f, 0.87f);
        static readonly Color SoleShade = new Color(0.62f, 0.64f, 0.66f);
        static readonly Color PadBlack = new Color(0.08f, 0.085f, 0.1f);

        // ------------------------------------------------------------------ legs

        static void HoopsLegs()
        {
            float w = Mathf.Lerp(1f, Wd, 0.7f);

            // Thigh: a bare thigh under long, loose shorts whose hem hangs just above the knee.
            {
                float L = Bd.ThighLen;
                float hem = -L * 0.8f;
                var c = new SdfCanvas(new Rect(-0.22f * w, -L - 0.1f, 0.44f * w, L + 0.3f), P);
                SdfCanvas.SdfFn leg = p => Sdf.SmoothUnion(
                    Sdf.Tapered(p, Vector2.zero, 0.092f * w, new Vector2(0f, -L), 0.066f * w),
                    Sdf.Ellipse(p, new Vector2(0.022f, -L * 0.52f), new Vector2(0.068f * w, 0.13f)), 0.04f);
                SdfCanvas.SdfFn shorts = p => Sdf.Intersect(
                    Sdf.Tapered(p, new Vector2(0f, 0.04f), 0.146f * w, new Vector2(0.016f, hem - 0.06f), 0.156f * w),
                    Sdf.HalfPlane(p, new Vector2(0f, hem), new Vector2(0.16f, -1f)));

                // the knee end lies over the shin: no contour there
                Contour(c, p => Sdf.Union(leg(p), shorts(p)), -0.03f, -L + 0.04f);
                c.Fill(leg, K.Skin);
                ShadeBack(c, -0.09f, 0.02f, 0.78f);
                c.Paint(p => Sdf.Circle(p, new Vector2(0.034f, -L + 0.012f), 0.03f), K.SkinLight.WithAlpha(0.35f), 0.03f);

                c.Fill(p => shorts(p) - LineW * 0.8f, Palette.PlayerLine);
                c.Fill(shorts, K.Jersey);
                Tint(c, shorts, K.JerseyShade, p => 0.85f * (1f - MathUtil.Smooth01((p.x + 0.13f * w) / 0.17f)));
                Tint(c, shorts, K.JerseyLight, p => 0.4f * MathUtil.Smooth01((p.x - 0.03f) / 0.1f) * MathUtil.Smooth01((p.y + L * 0.3f) / 0.25f));
                // side panel with white piping down the outside of the leg, following its slight slant
                System.Func<Vector2, float> panelX = p => p.x - 0.006f - 0.03f * (p.y / L);
                c.Paint(p => Sdf.Intersect(Mathf.Abs(panelX(p)) - 0.036f * w, shorts(p)), K.Stripe);
                c.Paint(p => Sdf.Intersect(Mathf.Abs(Mathf.Abs(panelX(p)) - 0.036f * w) - 0.0055f, shorts(p)), K.KitWhite);
                // hem band, then the loose folds of the fabric
                c.Paint(p => Sdf.Intersect(shorts(p), Sdf.HalfPlane(p, new Vector2(0f, hem + 0.04f), new Vector2(-0.16f, 1f))), K.JerseyShade.WithAlpha(0.5f), 0.004f);
                c.Paint(p => Sdf.Intersect(shorts(p), Mathf.Abs(Sdf.HalfPlane(p, new Vector2(0f, hem + 0.04f), new Vector2(-0.16f, 1f))) - 0.004f), K.KitWhite.WithAlpha(0.8f));
                Stroke(c, shorts, new Vector2(0.06f, -0.04f), new Vector2(0.1f, -L * 0.45f), 0.006f, K.JerseyShade.WithAlpha(0.45f), 0.014f);
                Stroke(c, shorts, new Vector2(-0.07f, -L * 0.25f), new Vector2(-0.05f, -L * 0.62f), 0.007f, K.JerseyShade.WithAlpha(0.4f), 0.016f);
                Stroke(c, shorts, new Vector2(0.09f, -L * 0.55f), new Vector2(0.12f, hem + 0.05f), 0.005f, K.JerseyLight.WithAlpha(0.35f), 0.012f);
                Thigh = c.ToSprite("Thigh", Vector2.zero);
            }

            // Shin: knee, calf, crew sock with a jersey stripe; the centre wears a black knee sleeve.
            {
                float L = Bd.ShinLen;
                var c = new SdfCanvas(new Rect(-0.15f * w, -L - 0.08f, 0.3f * w, L + 0.2f), P);
                SdfCanvas.SdfFn shin = p => Sdf.SmoothUnion(
                    Sdf.SmoothUnion(
                        Sdf.Tapered(p, Vector2.zero, 0.068f * w, new Vector2(0, -L), 0.046f * w),
                        Sdf.Ellipse(p, new Vector2(-0.028f, -L * 0.32f), new Vector2(0.064f * w, 0.13f)), 0.03f),
                    Sdf.Ellipse(p, new Vector2(0.024f, -L * 0.46f), new Vector2(0.05f * w, 0.11f)), 0.02f);

                Contour(c, shin, 0f);
                c.Fill(shin, K.Skin);
                ShadeBack(c, -0.085f, 0.015f, 0.76f);
                Stroke(c, shin, new Vector2(0.036f, -0.06f), new Vector2(0.03f, -L * 0.4f), 0.01f, K.SkinLight.WithAlpha(0.35f), 0.02f);

                // crew sock: up to a third of the calf, two stripes in the jersey colour at the cuff
                float cuff = -L * 0.62f;
                SdfCanvas.SdfFn sock = p => Sdf.Intersect(shin(p), p.y - cuff);
                c.Paint(sock, K.KitWhite);
                Tint(c, sock, K.KitWhiteShade, p => 0.7f * (1f - MathUtil.Smooth01((p.x + 0.07f) / 0.09f)));
                c.Paint(p => Sdf.Intersect(Mathf.Abs(p.y - cuff + 0.03f) - 0.011f, shin(p)), K.Jersey);
                c.Paint(p => Sdf.Intersect(Mathf.Abs(p.y - cuff + 0.058f) - 0.006f, shin(p)), K.Stripe);
                c.Paint(p => Sdf.Intersect(Mathf.Abs(p.y - cuff) - 0.004f, shin(p)), K.KitWhiteShade.WithAlpha(0.8f));

                if (K.KneePads)
                {
                    SdfCanvas.SdfFn pad = p => Sdf.Intersect(shin(p) - 0.01f, Mathf.Abs(p.y + 0.07f) - 0.1f);
                    c.Fill(p => pad(p) - LineW * 0.7f, Palette.PlayerLine);
                    c.Fill(pad, PadBlack);
                    // the padded dome over the kneecap catches the light
                    c.Paint(p => Sdf.Intersect(Sdf.Ellipse(p, new Vector2(0.03f, -0.05f), new Vector2(0.045f, 0.06f)), pad(p)), new Color(0.3f, 0.32f, 0.38f, 0.8f), 0.02f);
                    c.Paint(p => Sdf.Intersect(Mathf.Abs(p.y + 0.155f) - 0.006f, pad(p)), K.Stripe.WithAlpha(0.9f));
                }
                Shin = c.ToSprite("Shin", Vector2.zero);
            }

            // Sneaker: a high-top with a padded collar, chunky midsole, accent swoosh and laces.
            {
                var c = new SdfCanvas(new Rect(-0.16f, -0.16f, 0.44f, 0.34f), P);
                SdfCanvas.SdfFn upper = p =>
                {
                    float collar = Sdf.Box(p, new Vector2(-0.012f, 0.02f), new Vector2(0.066f, 0.085f), 0.04f, -6f);
                    float foot = Sdf.Capsule(p, new Vector2(-0.05f, -0.05f), new Vector2(0.15f, -0.058f), 0.05f);
                    float toe = Sdf.Ellipse(p, new Vector2(0.165f, -0.064f), new Vector2(0.066f, 0.04f));
                    float heel = Sdf.Box(p, new Vector2(-0.05f, -0.055f), new Vector2(0.055f, 0.04f), 0.025f);
                    float d = Sdf.SmoothUnion(Sdf.SmoothUnion(collar, foot, 0.05f), Sdf.Union(toe, heel), 0.03f);
                    return Sdf.Intersect(d, -(p.y + 0.072f));
                };
                SdfCanvas.SdfFn sole = p => Sdf.Box(p, new Vector2(0.055f, -0.083f), new Vector2(0.162f, 0.02f), 0.014f);

                Contour(c, p => Sdf.Union(upper(p), sole(p)), 0.1f);
                c.Fill(upper, p => Color.Lerp(K.Boot, K.BootLight,
                    0.55f * MathUtil.Smooth01((p.y + 0.07f) / 0.1f) * MathUtil.Smooth01((p.x + 0.05f) / 0.16f)));
                // toe box and heel counter a shade apart, the padded collar rim
                c.Paint(p => Sdf.Ellipse(p, new Vector2(0.17f, -0.062f), new Vector2(0.058f, 0.032f)), K.BootLight.WithAlpha(0.35f), 0.02f);
                c.Paint(p => Sdf.Box(p, new Vector2(-0.075f, -0.04f), new Vector2(0.03f, 0.05f), 0.02f), K.Boot.WithAlpha(0.85f), 0.02f);
                c.Paint(p => Sdf.Intersect(upper(p), Mathf.Abs(p.y - 0.09f + 0.1f * p.x) - 0.016f), K.BootLight.WithAlpha(0.8f), 0.006f);
                // the accent swoosh sweeping from the heel under the laces
                c.Paint(p => Sdf.Intersect(Sdf.Tapered(p, new Vector2(-0.07f, -0.03f), 0.018f, new Vector2(0.13f, -0.012f), 0.004f), upper(p)), K.Neon);
                c.Paint(p => Sdf.Intersect(Sdf.Tapered(p, new Vector2(-0.07f, -0.03f), 0.007f, new Vector2(0.1f, -0.018f), 0.002f), upper(p)), Color.white.WithAlpha(0.5f));
                // laces up the tongue
                for (int i = 0; i < 4; i++)
                {
                    Vector2 a = new Vector2(0.025f + i * 0.026f, 0.055f - i * 0.028f);
                    c.Paint(p => Sdf.Capsule(p, a, a + new Vector2(0.02f, 0.006f), 0.0055f), K.KitWhite);
                }
                // heel tab in the accent colour
                c.Paint(p => Sdf.Intersect(Sdf.Box(p, new Vector2(-0.08f, 0.07f), new Vector2(0.012f, 0.04f), 0.008f), upper(p)), K.Neon.WithAlpha(0.9f));
                // midsole: light foam with a groove and the accent line, dark rubber underneath
                c.Fill(sole, p => Color.Lerp(SoleShade, SoleWhite, MathUtil.Smooth01((p.y + 0.1f) / 0.03f)));
                c.Paint(p => Sdf.Intersect(sole(p), Mathf.Abs(p.y + 0.08f) - 0.003f), K.Neon.WithAlpha(0.8f));
                c.Paint(p => Sdf.Intersect(sole(p), -(p.y + 0.094f)), Color.Lerp(K.Boot, Color.black, 0.3f));
                Boot = c.ToSprite("Boot", Vector2.zero);

                var g = new SdfCanvas(new Rect(-0.2f, -0.22f, 0.52f, 0.26f), 180f);
                g.Fill(p => Sdf.Capsule(p, new Vector2(-0.08f, -0.1f), new Vector2(0.19f, -0.1f), 0.004f), K.Neon, 0.07f);
                BootGlow = g.ToSprite("BootGlow", Vector2.zero);
            }
        }

        // ------------------------------------------------------------------ arms

        static void HoopsArms()
        {
            float w = Mathf.Lerp(1f, Wd, 0.8f);
            Color sleeveCol = Color.Lerp(K.Stripe, PadBlack, 0.5f);

            // Upper arm: bare (a tank top), round shoulder cap and bicep. The shooter's throwing arm
            // wears a compression sleeve in the trim colour.
            {
                float L = Bd.UpperArmLen;
                SdfCanvas.SdfFn arm = p => Sdf.SmoothUnion(Sdf.SmoothUnion(
                    Sdf.Tapered(p, Vector2.zero, 0.066f * w, new Vector2(0, -L), 0.048f * w),
                    Sdf.Ellipse(p, new Vector2(0.01f, -0.035f), new Vector2(0.07f * w, 0.07f)), 0.03f),
                    Sdf.Ellipse(p, new Vector2(0.016f, -L * 0.55f), new Vector2(0.056f * w, 0.085f)), 0.03f);
                for (int s = 0; s < 2; s++)
                {
                    bool sleeve = s == 1;
                    if (sleeve && !K.Sleeve) { UpperArmNear = null; break; }
                    var c = new SdfCanvas(new Rect(-0.13f * w, -L - 0.08f, 0.26f * w, L + 0.2f), P);
                    Contour(c, arm, 99f, -L + 0.035f);
                    c.Fill(arm, K.Skin);
                    ShadeBack(c, -0.06f, 0.012f, 0.78f);
                    c.Paint(p => Sdf.Ellipse(p, new Vector2(0.03f, -0.03f), new Vector2(0.03f, 0.04f)), K.SkinLight.WithAlpha(0.45f), 0.025f);
                    c.Paint(p => Sdf.Ellipse(p, new Vector2(0.032f, -L * 0.55f), new Vector2(0.016f, 0.05f)), K.SkinLight.WithAlpha(0.35f), 0.025f);
                    Stroke(c, arm, new Vector2(-0.02f, -0.07f), new Vector2(0.01f, -0.1f), 0.004f, K.SkinShade.WithAlpha(0.45f), 0.008f);
                    if (sleeve)
                    {
                        SdfCanvas.SdfFn cover = p => Sdf.Intersect(arm(p) - 0.004f, p.y + 0.06f);
                        c.Fill(p => cover(p) - LineW * 0.6f, Palette.PlayerLine);
                        c.Fill(cover, sleeveCol);
                        Tint(c, cover, Color.white, p => 0.16f * MathUtil.Smooth01((p.x - 0.0f) / 0.05f));
                        c.Paint(p => Sdf.Intersect(Mathf.Abs(p.y + 0.075f) - 0.008f, cover(p)), K.Neon.WithAlpha(0.85f));
                        UpperArmNear = c.ToSprite("UpperArmSleeve", Vector2.zero);
                    }
                    else UpperArm = c.ToSprite("UpperArm", Vector2.zero);
                }
            }

            // Forearm with a wristband (the sleeve runs on down to the band).
            {
                float L = Bd.ForearmLen;
                SdfCanvas.SdfFn fore = p => Sdf.SmoothUnion(
                    Sdf.Tapered(p, Vector2.zero, 0.052f * w, new Vector2(0, -L), 0.04f * w),
                    Sdf.Ellipse(p, new Vector2(-0.008f, -L * 0.3f), new Vector2(0.048f * w, 0.08f)), 0.03f);
                SdfCanvas.SdfFn band = p => Sdf.Intersect(Mathf.Abs(p.y + L - 0.045f) - 0.026f, fore(p) - 0.007f);
                for (int s = 0; s < 2; s++)
                {
                    bool sleeve = s == 1;
                    if (sleeve && !K.Sleeve) { ForearmNear = null; break; }
                    var c = new SdfCanvas(new Rect(-0.11f * w, -L - 0.07f, 0.22f * w, L + 0.16f), P);
                    Contour(c, fore, 0f, -L + 0.02f);
                    c.Fill(fore, K.Skin);
                    ShadeBack(c, -0.05f, 0.012f, 0.78f);
                    Stroke(c, fore, new Vector2(0.028f, -0.04f), new Vector2(0.02f, -L * 0.6f), 0.008f, K.SkinLight.WithAlpha(0.3f), 0.016f);
                    if (sleeve)
                    {
                        SdfCanvas.SdfFn cover = p => Sdf.Intersect(fore(p) - 0.004f, -(p.y + L - 0.07f));
                        c.Fill(cover, sleeveCol);
                        Tint(c, cover, Color.white, p => 0.14f * MathUtil.Smooth01(p.x / 0.045f));
                    }
                    c.Fill(p => band(p) - LineW * 0.7f, Palette.PlayerLine);
                    c.Fill(band, sleeve ? K.Neon : K.KitWhite);
                    Tint(c, band, sleeve ? Color.black : K.KitWhiteShade, p => 0.6f * (1f - MathUtil.Smooth01((p.x + 0.045f) / 0.07f)));
                    c.Paint(p => Sdf.Intersect(Mathf.Abs(p.y + L - 0.045f) - 0.006f, band(p)), sleeve ? K.KitWhite : K.Jersey);
                    if (sleeve) ForearmNear = c.ToSprite("ForearmSleeve", Vector2.zero);
                    else Forearm = c.ToSprite("Forearm", Vector2.zero);
                }
            }

            // Hand: open for the dribble — palm, three fingers fanned out along the arm, the thumb apart.
            {
                var c = new SdfCanvas(new Rect(-0.1f, -0.2f, 0.2f, 0.24f), P);
                SdfCanvas.SdfFn hand = p =>
                {
                    float palm = Sdf.Ellipse(p, new Vector2(0.002f, -0.05f), new Vector2(0.05f, 0.056f));
                    float f1 = Sdf.Capsule(p, new Vector2(-0.024f, -0.08f), new Vector2(-0.034f, -0.148f), 0.0165f);
                    float f2 = Sdf.Capsule(p, new Vector2(0.004f, -0.086f), new Vector2(0.006f, -0.16f), 0.0165f);
                    float f3 = Sdf.Capsule(p, new Vector2(0.03f, -0.082f), new Vector2(0.04f, -0.145f), 0.015f);
                    float thumb = Sdf.Capsule(p, new Vector2(0.036f, -0.03f), new Vector2(0.066f, -0.08f), 0.015f);
                    return Sdf.SmoothUnion(Sdf.SmoothUnion(palm, Sdf.Union(Sdf.Union(f1, f2), f3), 0.012f), thumb, 0.01f);
                };
                Contour(c, hand, -0.005f);
                c.Fill(hand, K.Skin);
                ShadeBack(c, -0.05f, 0.03f, 0.8f);
                Stroke(c, hand, new Vector2(-0.01f, -0.085f), new Vector2(-0.018f, -0.145f), 0.003f, K.SkinShade.WithAlpha(0.6f), 0.005f);
                Stroke(c, hand, new Vector2(0.018f, -0.087f), new Vector2(0.022f, -0.15f), 0.003f, K.SkinShade.WithAlpha(0.6f), 0.005f);
                c.Paint(p => Sdf.Ellipse(p, new Vector2(0.01f, -0.04f), new Vector2(0.025f, 0.02f)), K.SkinLight.WithAlpha(0.3f), 0.02f);
                Hand = c.ToSprite("Hand", Vector2.zero);
            }
        }

        // ------------------------------------------------------------------ body

        /// <summary>Seven-segment style jersey digits (0..9), height h, bottom-left at o.</summary>
        static float Digit(Vector2 p, int d, Vector2 o, float h)
        {
            // segments: a top, b top-right, c bottom-right, d bottom, e bottom-left, f top-left, g middle
            int[] masks = { 0x3F, 0x06, 0x5B, 0x4F, 0x66, 0x6D, 0x7D, 0x07, 0x7F, 0x6F };
            int m = masks[Mathf.Clamp(d, 0, 9)];
            float w = h * 0.56f, t = h * 0.17f, r = t * 0.35f;
            float best = 9f;
            void Seg(int bit, Vector2 c, Vector2 half) { if ((m & (1 << bit)) != 0) best = Mathf.Min(best, Sdf.Box(p, o + c, half, r)); }
            Seg(0, new Vector2(w * 0.5f, h - t * 0.5f), new Vector2(w * 0.5f, t * 0.5f));
            Seg(1, new Vector2(w - t * 0.5f, h * 0.75f), new Vector2(t * 0.5f, h * 0.25f));
            Seg(2, new Vector2(w - t * 0.5f, h * 0.25f), new Vector2(t * 0.5f, h * 0.25f));
            Seg(3, new Vector2(w * 0.5f, t * 0.5f), new Vector2(w * 0.5f, t * 0.5f));
            Seg(4, new Vector2(t * 0.5f, h * 0.25f), new Vector2(t * 0.5f, h * 0.25f));
            Seg(5, new Vector2(t * 0.5f, h * 0.75f), new Vector2(t * 0.5f, h * 0.25f));
            Seg(6, new Vector2(w * 0.5f, h * 0.5f), new Vector2(w * 0.5f, t * 0.5f));
            return best;
        }

        /// <summary>The jersey number centred on c (one or two digits), leaning with the chest.</summary>
        static float Number(Vector2 p, int n, Vector2 c, float h)
        {
            string s = Mathf.Clamp(n, 0, 99).ToString();
            float w = h * 0.56f, gap = h * 0.14f;
            float total = s.Length * w + (s.Length - 1) * gap;
            float best = 9f;
            for (int i = 0; i < s.Length; i++)
            {
                Vector2 o = c + new Vector2(-total * 0.5f + i * (w + gap), -h * 0.5f);
                best = Mathf.Min(best, Digit(p, s[i] - '0', o, h));
            }
            return best;
        }

        static void HoopsBody()
        {
            float sy = Bd.ShoulderY / 0.465f;
            float w = Wd;
            float sh = Bd.ShoulderY;

            // Tank top: the body of the jersey with deep arm holes and a scooped neck, skin at the
            // shoulders. Trim in the second colour, a side panel and the number on the chest; the
            // hem is tucked into the shorts, whose waistband is drawn over it.
            {
                var c = new SdfCanvas(new Rect(-0.3f * w, -0.14f, 0.6f * w, sh + 0.34f), P);
                SdfCanvas.SdfFn torso = p =>
                {
                    Vector2 q = new Vector2(p.x / w, p.y / sy);
                    float body = Sdf.Tapered(q, new Vector2(0f, 0.05f), 0.13f, new Vector2(0.008f, 0.42f), 0.16f);
                    float chest = Sdf.Ellipse(q, new Vector2(0.04f, 0.35f), new Vector2(0.16f, 0.15f));
                    float back = Sdf.Ellipse(q, new Vector2(-0.03f, 0.31f), new Vector2(0.142f, 0.2f));
                    float shoulder = Sdf.Circle(q, new Vector2(0f, 0.46f), 0.125f);
                    float d = Sdf.SmoothUnion(Sdf.SmoothUnion(Sdf.SmoothUnion(body, chest, 0.05f), back, 0.05f), shoulder, 0.04f);
                    return Sdf.Intersect(d * Mathf.Min(w, sy), -(p.y + 0.1f));
                };
                SdfCanvas.SdfFn jersey = p =>
                {
                    float armhole = Sdf.Intersect(Sdf.Ellipse(p, new Vector2(-0.01f * w, sh - 0.04f), new Vector2(0.105f * w, 0.14f)), p.y - (sh + 0.06f));
                    float neckline = p.y - (sh + 0.095f - 0.07f * MathUtil.Smooth01((p.x - 0.02f * w) / (0.11f * w)));
                    return Sdf.Intersect(Sdf.Subtract(torso(p), armhole), neckline);
                };

                Contour(c, torso);
                c.Fill(torso, K.Skin);
                ShadeBack(c, -0.16f * w, 0.02f, 0.76f);
                c.Paint(p => Sdf.Ellipse(p, new Vector2(0.03f * w, sh + 0.02f), new Vector2(0.07f * w, 0.05f)), K.SkinLight.WithAlpha(0.35f), 0.03f);
                Stroke(c, torso, new Vector2(0.02f * w, sh + 0.05f), new Vector2(0.11f * w, sh + 0.02f), 0.004f, K.SkinShade.WithAlpha(0.5f), 0.008f);

                c.Fill(p => jersey(p) - LineW * 0.7f, Palette.PlayerLine);
                c.Fill(jersey, K.Jersey);
                Tint(c, jersey, K.JerseyShade, p =>
                    0.75f * (1f - MathUtil.Smooth01((p.x + 0.15f * w) / (0.14f * w))) + 0.3f * (1f - MathUtil.Smooth01((p.y + 0.02f) / 0.2f)));
                Tint(c, jersey, K.JerseyLight, p =>
                    0.5f * MathUtil.Smooth01((p.x - 0.02f) / (0.1f * w)) * MathUtil.Smooth01((p.y - sh * 0.55f) / 0.12f));
                // side panel down the seam, piped in white
                System.Func<Vector2, float> seam = p => p.x + 0.02f * w - 0.06f * (p.y - sh * 0.6f);
                c.Paint(p => Sdf.Intersect(Mathf.Abs(seam(p)) - 0.03f * w, Sdf.Intersect(jersey(p), p.y - (sh - 0.16f))), K.Stripe);
                c.Paint(p => Sdf.Intersect(Mathf.Abs(Mathf.Abs(seam(p)) - 0.03f * w) - 0.005f, Sdf.Intersect(jersey(p), p.y - (sh - 0.16f))), K.KitWhite.WithAlpha(0.9f));
                // trim around the arm hole and the neck
                c.Paint(p => Sdf.Intersect(Sdf.Intersect(jersey(p), -(jersey(p) + 0.022f)), -(p.y - (sh - 0.2f))), K.Stripe, 0.003f);
                // the number: trim-coloured outline under a white face
                int number = K.Number;
                if (number > 0)
                {
                    Vector2 nc = new Vector2(0.075f * w, sh * 0.62f);
                    float nh = 0.12f * sy;
                    c.Paint(p => Sdf.Intersect(Number(p, number, nc, nh) - 0.008f, jersey(p) + 0.01f), K.Stripe);
                    c.Paint(p => Sdf.Intersect(Number(p, number, nc, nh), jersey(p) + 0.01f), K.KitWhite);
                    Tint(c, p => Sdf.Intersect(Number(p, number, nc, nh), jersey(p) + 0.01f), K.KitWhiteShade, p => 0.6f * (1f - MathUtil.Smooth01((p.x - nc.x + 0.04f) / 0.07f)));
                }
                // cloth folds, the belly of the jersey tucked in, then the shorts' waistband over the hem
                Stroke(c, jersey, new Vector2(-0.08f * w, 0.14f), new Vector2(0.06f * w, 0.18f), 0.006f, K.JerseyShade.WithAlpha(0.4f), 0.014f);
                Stroke(c, jersey, new Vector2(-0.1f * w, 0.06f), new Vector2(0.04f * w, 0.1f), 0.005f, K.JerseyShade.WithAlpha(0.35f), 0.014f);
                SdfCanvas.SdfFn waist = p => Sdf.Intersect(torso(p), p.y - 0.01f);
                c.Fill(p => Sdf.Intersect(torso(p), p.y - 0.01f - LineW), Palette.PlayerLine);
                c.Fill(waist, K.Stripe);
                c.Paint(p => Sdf.Intersect(waist(p), Mathf.Abs(p.y + 0.02f) - 0.006f), K.KitWhite);
                Tint(c, waist, Color.black, p => 0.45f * (1f - MathUtil.Smooth01((p.x + 0.14f * w) / (0.16f * w))));
                Torso = c.ToSprite("Torso", Vector2.zero);
            }

            // Shorts: baggy, same colour as the jersey, the side panel and a broad waistband.
            {
                var c = new SdfCanvas(new Rect(-0.24f * w, -0.22f, 0.48f * w, 0.36f), P);
                SdfCanvas.SdfFn shorts = p => Sdf.Box(p, new Vector2(0.005f, -0.035f), new Vector2(0.17f * w, 0.125f), 0.08f);
                Contour(c, shorts);
                c.Fill(shorts, K.Jersey);
                Tint(c, shorts, K.JerseyShade, p => 0.85f * (1f - MathUtil.Smooth01((p.x + 0.16f * w) / (0.17f * w))) + 0.3f * (1f - MathUtil.Smooth01((p.y + 0.13f) / 0.08f)));
                c.Paint(p => Sdf.Intersect(Mathf.Abs(p.x + 0.005f) - 0.036f * w, shorts(p)), K.Stripe);
                c.Paint(p => Sdf.Intersect(Mathf.Abs(Mathf.Abs(p.x + 0.005f) - 0.036f * w) - 0.005f, shorts(p)), K.KitWhite.WithAlpha(0.9f));
                c.Paint(p => Sdf.Intersect(shorts(p), -(p.y - 0.035f)), K.Stripe);
                c.Paint(p => Sdf.Intersect(shorts(p), Mathf.Abs(p.y - 0.035f) - 0.005f), K.KitWhite);
                Stroke(c, shorts, new Vector2(0.07f, -0.03f), new Vector2(0.11f, -0.13f), 0.006f, K.JerseyShade.WithAlpha(0.6f), 0.012f);
                Pelvis = c.ToSprite("Pelvis", Vector2.zero);
            }

            // Neck: a little thicker on the broad builds.
            {
                float nw = Mathf.Lerp(1f, w, 0.8f);
                var c = new SdfCanvas(new Rect(-0.1f * nw, -0.07f, 0.2f * nw, 0.24f), P);
                SdfCanvas.SdfFn neck = p => Sdf.Capsule(p, Vector2.zero, new Vector2(0.01f, 0.1f), 0.058f * nw);
                Contour(c, neck);
                c.Fill(neck, K.Skin);
                ShadeBack(c, -0.05f, 0.03f, 0.72f);
                c.Shade(p => Mathf.Lerp(0.78f, 1f, MathUtil.Smooth01((p.y - 0.05f) / -0.08f)));
                Neck = c.ToSprite("Neck", Vector2.zero);
            }
        }

        // ------------------------------------------------------------------ head

        static void HoopsHead()
        {
            Vector2 hc = HeadCenter;
            var c = new SdfCanvas(new Rect(-0.27f, -0.06f, 0.54f, 0.54f), P);
            SdfCanvas.SdfFn head = HeadShape;
            var style = K.Hair2;

            // hair cap: the fade and the braids sit higher and fuller than the buzz cut
            float capR = style == HairStyle.Buzz ? 0.19f : style == HairStyle.Braids ? 0.207f : 0.203f;
            SdfCanvas.SdfFn hair = p =>
            {
                float cap = Sdf.Circle(p, hc + new Vector2(-0.012f, 0.012f), capR);
                float line = Sdf.HalfPlane(p, new Vector2(0.075f, 0.255f), new Vector2(0.62f, -0.78f));
                float d = Sdf.Intersect(cap, line);
                float back = Sdf.Intersect(Sdf.Circle(p, hc + new Vector2(-0.018f, 0f), capR - 0.004f), p.x + 0.07f);
                d = Sdf.SmoothUnion(d, Sdf.Intersect(back, -(p.y - 0.08f)), 0.02f);
                if (style == HairStyle.Fade)
                    d = Sdf.SmoothUnion(d, Sdf.Box(p, new Vector2(-0.01f, 0.37f), new Vector2(0.13f, 0.035f), 0.035f), 0.03f);   // flat top
                return d;
            };

            Contour(c, p => Sdf.Union(head(p), hair(p)));
            SkinHead(c, head);
            Ear(c);

            switch (style)
            {
                case HairStyle.Fade:
                {
                    // dense on top, fading to skin over the ears; a line-up at the front, light stubble
                    c.Fill(hair, p => K.Hair.WithAlpha(Mathf.Lerp(0.3f, 1f, MathUtil.Smooth01((p.y - 0.2f) / 0.1f))));
                    c.Paint(p => Sdf.Intersect(Sdf.Ring(p, hc + new Vector2(-0.02f, 0.02f), 0.16f, 0.018f), -(p.y - 0.3f)), K.HairLight.WithAlpha(0.6f), 0.012f);
                    for (int i = 0; i < 6; i++)
                    {
                        float x = -0.12f + i * 0.04f;
                        Stroke(c, hair, new Vector2(x, 0.36f), new Vector2(x + 0.015f, 0.32f), 0.003f, K.HairLight.WithAlpha(0.45f), 0.004f);
                    }
                    c.Paint(p => Sdf.Intersect(Sdf.Ellipse(p, new Vector2(0.09f, 0.07f), new Vector2(0.1f, 0.05f)), head(p)), K.Hair.WithAlpha(0.22f), 0.02f);
                    break;
                }
                case HairStyle.Buzz:
                {
                    // a close crop with a grain of stubble, then a full short beard and moustache
                    c.Fill(hair, p => K.Hair.WithAlpha(0.62f + 0.12f * MathUtil.Hash(Mathf.FloorToInt(p.x * 240f) * 7919 + Mathf.FloorToInt(p.y * 240f) * 104729)));
                    c.Paint(p => Sdf.Intersect(Sdf.Ring(p, hc + new Vector2(-0.02f, 0.02f), 0.165f, 0.014f), -(p.y - 0.3f)), K.HairLight.WithAlpha(0.45f), 0.012f);
                    SdfCanvas.SdfFn beard = p => Sdf.Intersect(Sdf.SmoothUnion(
                        Sdf.Ellipse(p, new Vector2(0.1f, 0.06f), new Vector2(0.095f, 0.058f)),
                        Sdf.Capsule(p, new Vector2(0.02f, 0.15f), new Vector2(0.07f, 0.06f), 0.03f), 0.03f), head(p));
                    c.Paint(beard, K.Hair.WithAlpha(0.88f), 0.006f);
                    c.Paint(p => Sdf.Intersect(beard(p), Sdf.Ellipse(p, new Vector2(0.12f, 0.03f), new Vector2(0.05f, 0.025f))), K.HairLight.WithAlpha(0.35f), 0.01f);
                    c.Paint(p => Sdf.Capsule(p, new Vector2(0.135f, 0.117f), new Vector2(0.18f, 0.122f), 0.011f), K.Hair.WithAlpha(0.9f), 0.004f);
                    break;
                }
                default:
                {
                    // cornrows: rows running from the hairline over the crown, each lit on its ridge
                    c.Fill(hair, K.Hair);
                    for (int i = 0; i < 5; i++)
                    {
                        float r = 0.05f + i * 0.034f;
                        c.Paint(p => Sdf.Intersect(Sdf.Ring(p, new Vector2(0.06f, 0.12f), r + 0.12f, 0.004f), hair(p) + 0.006f), K.HairLight.WithAlpha(0.7f), 0.004f);
                        c.Paint(p => Sdf.Intersect(Sdf.Ring(p, new Vector2(0.06f, 0.12f), r + 0.135f, 0.005f), hair(p) + 0.006f), Color.black.WithAlpha(0.35f), 0.004f);
                    }
                    break;
                }
            }
            if (K.Headband) Headband(c, p => Sdf.Union(head(p), hair(p)));
            Face(c);
            Head = c.ToSprite("Head", Vector2.zero);

            // hair tuft: the braids hang from the back of the head (beads in the trim colour) and
            // swing with every move; the short cuts have none
            float s = Mathf.Max(0.2f, K.Tuft);
            if (style == HairStyle.Braids)
            {
                var t = new SdfCanvas(new Rect(-0.46f * s, -0.52f * s, 0.46f * s, 0.56f * s), P);
                Vector2 T(float x, float y) => new Vector2(x * s, y * s);
                SdfCanvas.SdfFn braid(int i) => p =>
                {
                    Vector2 a = T(-0.12f - i * 0.022f, -0.02f - i * 0.03f), b = T(-0.2f - i * 0.045f, -0.42f + i * 0.045f);
                    return Sdf.Tapered(p, a, 0.02f * s, b, 0.013f * s);
                };
                for (int i = 0; i < 4; i++)
                {
                    var br = braid(i);
                    int k = i;
                    Contour(t, br);
                    t.Fill(br, Color.Lerp(K.Hair, Color.black, k * 0.08f));
                    // the plait: little lit ridges along the braid
                    t.Paint(p => Sdf.Intersect(br(p), Mathf.Abs(Mathf.Repeat(p.y * 55f / s + p.x * 20f / s, 1f) - 0.5f) * 0.02f - 0.004f), K.HairLight.WithAlpha(0.55f));
                    Vector2 bead = T(-0.2f - k * 0.045f, -0.42f + k * 0.045f);
                    t.Fill(p => Sdf.Circle(p, bead, 0.019f * s) - LineW * 0.6f, Palette.PlayerLine);
                    t.Fill(p => Sdf.Circle(p, bead, 0.019f * s), K.Stripe);
                    t.Paint(p => Sdf.Circle(p, bead + T(0.005f, 0.006f), 0.007f * s), Color.white.WithAlpha(0.6f), 0.004f);
                }
                HairTuft = t.ToSprite("HairTuft", Vector2.zero);
            }
            else
            {
                var t = new SdfCanvas(new Rect(-0.02f, -0.02f, 0.04f, 0.04f), P);
                HairTuft = t.ToSprite("HairTuft", Vector2.zero);
            }
        }
    }
}
