using UnityEngine;

namespace SoccerFight
{
    /// <summary>Die Symbole der Boxer-Moves für die Fähigkeitsleiste: dieselbe Sprache wie die Ball-Symbole, mit einem Handschuh.</summary>
    public static partial class UiArt
    {
        public static Sprite IconPunch, IconCross, IconGuard, IconSlip, IconUppercut, IconHooks, IconPound, IconShadow, IconFlurry;

        static readonly Color GloveRed = new Color(1f, 0.42f, 0.32f), GloveDark = new Color(0.62f, 0.14f, 0.12f), CuffWhite = new Color(1f, 0.96f, 0.92f);

        /// <summary>Die Form eines Boxhandschuhs (Knöchel zeigen nach +x) im lokalen Maß, s = halbe Länge.</summary>
        internal static float GloveSdf(Vector2 p, Vector2 at, float s, float angleDeg)
        {
            Vector2 q = MathUtil.Rotate(p - at, -angleDeg) / s;
            float fist = Sdf.SmoothUnion(Sdf.Ellipse(q, new Vector2(0.12f, 0f), new Vector2(0.66f, 0.54f)), Sdf.Circle(q, new Vector2(0.2f, 0.16f), 0.48f), 0.12f);
            float thumb = Sdf.Capsule(q, new Vector2(-0.12f, -0.36f), new Vector2(0.34f, -0.44f), 0.17f);
            float cuff = Sdf.Box(q, new Vector2(-0.66f, -0.04f), new Vector2(0.26f, 0.38f), 0.1f);
            return Sdf.SmoothUnion(Sdf.SmoothUnion(fist, thumb, 0.08f), cuff, 0.06f) * s;
        }

        /// <summary>Ein Handschuh in Farbe: rot mit Lichtkante, weiße Stulpe, eine dunkle Linie zwischen Daumen und Faust.</summary>
        static void IconGlove(SdfCanvas c, Vector2 at, float s, float angleDeg, Color body, float alpha = 1f)
        {
            c.Fill(p => GloveSdf(p, at, s, angleDeg), body.WithAlpha(alpha));
            // die Stulpe
            c.Fill(p =>
            {
                Vector2 q = MathUtil.Rotate(p - at, -angleDeg) / s;
                return Sdf.Intersect(Sdf.Box(q, new Vector2(-0.66f, -0.04f), new Vector2(0.24f, 0.36f), 0.09f), GloveSdf(p, at, s, angleDeg) / s) * s;
            }, CuffWhite.WithAlpha(alpha));
            // Licht oben auf den Knöcheln
            c.Fill(p =>
            {
                Vector2 q = MathUtil.Rotate(p - at, -angleDeg) / s;
                return Sdf.Ellipse(q, new Vector2(0.24f, 0.34f), new Vector2(0.3f, 0.12f)) * s;
            }, Color.white.WithAlpha(0.45f * alpha));
            // die Daumenlinie
            c.Fill(p =>
            {
                Vector2 q = MathUtil.Rotate(p - at, -angleDeg) / s;
                return (Sdf.Segment(q, new Vector2(-0.18f, -0.2f), new Vector2(0.36f, -0.26f)) - 0.035f) * s;
            }, GloveDark.WithAlpha(0.8f * alpha));
        }

        /// <summary>Die Sichel der Druckwelle vor der Faust.</summary>
        static void IconWave(SdfCanvas c, Vector2 at, float r, float angleDeg, Color col)
        {
            c.Fill(p =>
            {
                Vector2 q = MathUtil.Rotate(p - at, -angleDeg);
                return Sdf.Subtract(Sdf.Circle(q, new Vector2(-0.2f * r, 0f), r), Sdf.Circle(q, new Vector2(-0.55f * r, 0f), r * 1.05f));
            }, col);
        }

        static void SpeedLines(SdfCanvas c, Vector2 from, float len, float gap, int n, Color col)
        {
            for (int i = 0; i < n; i++)
            {
                float y = from.y + (i - (n - 1) * 0.5f) * gap;
                float l = i == n / 2 ? len : len * 0.7f;
                c.Fill(p => Sdf.Tapered(p, new Vector2(from.x - l, y), 1.2f, new Vector2(from.x, y), 4f), col.WithAlpha(0.9f - Mathf.Abs(i - (n - 1) * 0.5f) * 0.25f));
            }
        }

        static readonly System.Collections.Generic.Dictionary<Color, Sprite> heroGloves = new System.Collections.Generic.Dictionary<Color, Sprite>();

        /// <summary>Ein großer Handschuh in der Farbe des Boxers (Menü: fliegt beim Start in die Kamera, sitzt im Namensschild).</summary>
        public static Sprite HeroGlove(Color body)
        {
            if (heroGloves.TryGetValue(body, out var s) && s != null) return s;
            var c = new SdfCanvas(new Rect(-64, -64, 128, 128), 4f);
            Color dark = Color.Lerp(body, Color.black, 0.45f), light = Color.Lerp(body, Color.white, 0.35f);
            // Körper mit Schattierung von unten nach oben, Lichtkante oben, weiße Stulpe
            c.Fill(p => GloveSdf(p, new Vector2(4f, 0f), 54f, 0f), p => Color.Lerp(dark, light, MathUtil.Smooth01((p.y + 40f) / 80f)), 0.8f);
            c.Fill(p =>
            {
                Vector2 q = (p - new Vector2(4f, 0f)) / 54f;
                return Sdf.Intersect(Sdf.Box(q, new Vector2(-0.66f, -0.04f), new Vector2(0.24f, 0.36f), 0.09f), GloveSdf(p, new Vector2(4f, 0f), 54f, 0f) / 54f) * 54f;
            }, p => Color.Lerp(new Color(0.78f, 0.75f, 0.72f), CuffWhite, MathUtil.Smooth01((p.y + 20f) / 40f)), 0.8f);
            c.Fill(p => Sdf.Ellipse((p - new Vector2(4f, 0f)) / 54f, new Vector2(0.26f, 0.32f), new Vector2(0.32f, 0.11f)) * 54f, Color.white.WithAlpha(0.5f), 3f);
            c.Fill(p => (Sdf.Segment((p - new Vector2(4f, 0f)) / 54f, new Vector2(-0.16f, -0.2f), new Vector2(0.36f, -0.26f)) - 0.03f) * 54f, dark.WithAlpha(0.85f), 0.8f);
            s = ToUi(c, "UiHeroGlove");
            heroGloves[body] = s;
            return s;
        }

        static void BuildBoxIcons(float D)
        {
            var rect = new Rect(-64, -64, 128, 128);
            Color punch = new Color(1f, 0.62f, 0.42f), mint = new Color(0.45f, 1f, 0.72f), blue = new Color(0.5f, 0.72f, 1f),
                  gold = new Color(1f, 0.82f, 0.36f), violet = new Color(0.72f, 0.58f, 1f);

            // Schlag: der Handschuh fliegt nach rechts, vor ihm die Druckwelle
            var pu = new SdfCanvas(rect, D);
            SpeedLines(pu, new Vector2(-26f, -2f), 30f, 15f, 3, punch);
            IconWave(pu, new Vector2(44f, 0f), 30f, 0f, punch.WithAlpha(0.85f));
            IconGlove(pu, new Vector2(4f, 0f), 30f, 0f, GloveRed);
            IconPunch = ToUi(pu, "UiIconPunch");

            // Kraftgerade: großer Handschuh, eine Sternexplosion vor den Knöcheln
            var cr = new SdfCanvas(rect, D);
            cr.Fill(p => Sdf.Star4(p, new Vector2(36f, 4f), 30f, 0.38f), gold.WithAlpha(0.9f));
            cr.Fill(p => Sdf.Star4(p, new Vector2(36f, 4f), 18f, 0.4f), Color.white);
            SpeedLines(cr, new Vector2(-34f, 0f), 26f, 13f, 3, punch);
            IconGlove(cr, new Vector2(-2f, 0f), 36f, 0f, GloveRed);
            IconCross = ToUi(cr, "UiIconCross");

            // Deckung: zwei Handschuhe senkrecht vor einem Schild aus Bögen
            var gu = new SdfCanvas(rect, D);
            for (int i = 0; i < 2; i++)
            {
                float r = 50f - i * 10f;
                int k = i;
                gu.Fill(p => Sdf.Intersect(Sdf.Ring(p, new Vector2(-8f, -6f), r, 3.4f), -(p.x + 8f) + 20f), blue.WithAlpha(0.9f - k * 0.3f));
            }
            IconGlove(gu, new Vector2(-16f, 6f), 24f, 90f, GloveRed);
            IconGlove(gu, new Vector2(16f, 2f), 24f, 90f, GloveRed);
            IconGuard = ToUi(gu, "UiIconGuard");

            // Konterschritt: zwei Pfeilspitzen nach hinten, der Handschuh bereit, ein Funke für den sicheren Konter
            var sl = new SdfCanvas(rect, D);
            for (int i = 0; i < 2; i++)
            {
                float x = -16f - i * 24f;
                sl.Fill(p => Sdf.Union(Sdf.Capsule(p, new Vector2(x + 18f, 26f), new Vector2(x, 0f), 6.5f), Sdf.Capsule(p, new Vector2(x, 0f), new Vector2(x + 18f, -26f), 6.5f)),
                    i == 0 ? Color.white : mint.WithAlpha(0.75f));
            }
            IconGlove(sl, new Vector2(30f, 4f), 24f, 0f, GloveRed);
            sl.Fill(p => Sdf.Star4(p, new Vector2(48f, 40f), 14f, 0.45f), mint);
            IconSlip = ToUi(sl, "UiIconSlip");

            // Uppercut: der Handschuh steil nach oben, eine Bogenwelle darüber
            var up = new SdfCanvas(rect, D);
            for (int i = 0; i < 3; i++)
            {
                float x = -22f + i * 14f;
                up.Fill(p => Sdf.Tapered(p, new Vector2(x, -58f), 1.2f, new Vector2(x + 4f, -28f), 3.6f), gold.WithAlpha(0.85f - Mathf.Abs(i - 1) * 0.3f));
            }
            IconWave(up, new Vector2(4f, 40f), 28f, 90f, gold.WithAlpha(0.9f));
            IconGlove(up, new Vector2(0f, -2f), 30f, 80f, GloveRed);
            IconUppercut = ToUi(up, "UiIconUppercut");

            // Doppelhaken: zwei Bögen im Kreis, je mit Pfeilspitze, der Handschuh in der Mitte
            var ho = new SdfCanvas(rect, D);
            for (int side = 0; side < 2; side++)
            {
                float a0 = side == 0 ? 20f : 200f;
                int sd = side;
                ho.Fill(p =>
                {
                    float ang = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg;
                    float da = Mathf.Abs(Mathf.DeltaAngle(ang, a0 + 65f)) - 65f;
                    return Mathf.Max(Sdf.Ring(p, Vector2.zero, 46f, 4f), da * Mathf.Deg2Rad * 46f);
                }, sd == 0 ? violet : Color.white.WithAlpha(0.85f));
                Vector2 tip = MathUtil.Dir(a0 + 130f) * 46f, dirT = MathUtil.Dir(a0 + 130f + 90f);
                Vector2 n = new Vector2(-dirT.y, dirT.x);
                ho.Fill(p => Sdf.Triangle(p, tip + dirT * 12f, tip - n * 10f, tip + n * 10f), sd == 0 ? violet : Color.white.WithAlpha(0.85f));
            }
            IconGlove(ho, new Vector2(0f, 0f), 24f, 20f, GloveRed);
            IconHooks = ToUi(ho, "UiIconHooks");

            // Bodenschlag: zwei Handschuhe nach unten, Bebenringe am Boden
            var po = new SdfCanvas(rect, D);
            for (int i = 0; i < 3; i++)
            {
                float rr = 16f + i * 15f;
                po.Fill(p => Sdf.Intersect(Mathf.Abs(Sdf.Ellipse(p, new Vector2(0f, -46f), new Vector2(rr * 1.4f, rr * 0.35f))) - 2.6f, p.y + 58f), blue.WithAlpha(0.95f - i * 0.25f));
            }
            IconGlove(po, new Vector2(-14f, 4f), 24f, -95f, GloveRed);
            IconGlove(po, new Vector2(14f, 8f), 24f, -85f, GloveRed);
            IconPound = ToUi(po, "UiIconPound");

            // Schattenboxer: hinter dem Handschuh sein violetter Schatten, leicht versetzt
            var sh = new SdfCanvas(rect, D);
            IconGlove(sh, new Vector2(-14f, 12f), 30f, 10f, violet, 0.55f);
            IconGlove(sh, new Vector2(12f, -10f), 30f, 10f, GloveRed);
            sh.Fill(p => Sdf.Star4(p, new Vector2(-38f, 44f), 11f, 0.45f), violet);
            IconShadow = ToUi(sh, "UiIconShadow");

            // Trommelfeuer: drei Handschuhe hintereinander, Wellen davor
            var fl = new SdfCanvas(rect, D);
            for (int i = 0; i < 3; i++)
            {
                float y = 26f - i * 26f, x = -20f + (i % 2) * 12f;
                IconWave(fl, new Vector2(x + 44f, y), 16f, 0f, punch.WithAlpha(0.8f));
                IconGlove(fl, new Vector2(x, y), 20f, 0f, GloveRed);
            }
            IconFlurry = ToUi(fl, "UiIconFlurry");
        }
    }
}
