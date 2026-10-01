using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Die Bilder der Belohnungskarten, alle aus einem Guss: Jede Glyphe wird zu einem Relief geprägt
    /// (Höhenfeld aus der Form, Licht von links oben, Glanzkanten, dunkle Kontur, weicher Schatten).
    /// Upgrades sind freistehendes Silber, Fähigkeiten ein Gold-Medaillon mit Emaille in ihrer Farbe –
    /// so sieht man sofort, welche Art Karte es ist. Gebaut wird erst, wenn eine Karte das Bild braucht.
    /// </summary>
    public static class CardEmblems
    {
        /// <summary>Einheiten: die Glyphen liegen in ±56, der Rand bleibt für Kontur und Schatten.</summary>
        const float Half = 72f;
        const float Ppu = 2f;
        const int N = 288;   // Pixel je Seite (2 · Half · Ppu)

        static readonly Dictionary<UpIcon, Sprite> upgrades = new Dictionary<UpIcon, Sprite>();
        static readonly Dictionary<Ability, Sprite> abilities = new Dictionary<Ability, Sprite>();
        static Sprite halo;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { upgrades.Clear(); abilities.Clear(); halo = null; }

        struct Metal { public Color hi, mid, lo, sec, spec; }

        static readonly Metal Silver = new Metal
        {
            hi = new Color(0.95f, 0.97f, 1f), mid = new Color(0.68f, 0.74f, 0.84f), lo = new Color(0.34f, 0.39f, 0.5f),
            sec = new Color(0.5f, 0.6f, 0.76f), spec = new Color(1f, 1f, 1f)
        };
        static readonly Metal Gold = new Metal
        {
            hi = new Color(1f, 0.93f, 0.7f), mid = new Color(0.88f, 0.66f, 0.3f), lo = new Color(0.46f, 0.28f, 0.11f),
            sec = new Color(0.62f, 0.42f, 0.18f), spec = new Color(1f, 0.97f, 0.86f)
        };
        static readonly Metal Ivory = new Metal
        {
            hi = new Color(1f, 0.98f, 0.92f), mid = new Color(0.93f, 0.85f, 0.66f), lo = new Color(0.66f, 0.52f, 0.32f),
            sec = new Color(0.8f, 0.66f, 0.44f), spec = new Color(1f, 1f, 0.95f)
        };
        static readonly Color Ink = new Color(0.035f, 0.05f, 0.085f);

        // ------------------------------------------------------------------ öffentlich

        /// <summary>Silbernes Relief für eine Upgrade-Karte.</summary>
        public static Sprite ForUpgrade(UpIcon icon)
        {
            if (upgrades.TryGetValue(icon, out var s)) return s;
            Masks(c => UpgradeIcons.Draw(c, icon), 1f, out var shape, out var prim);
            var col = new Color[N * N];
            Shadow(col, shape, 0.6f);
            Outline(col, shape, Ink, 0.95f);
            Relief(col, shape, prim, Silver);
            s = ToSprite(col, "CardEmblem " + icon);
            upgrades[icon] = s;
            return s;
        }

        /// <summary>Gold-Medaillon für eine Fähigkeitskarte, die Emaille in der Farbe der Fähigkeit.</summary>
        public static Sprite ForAbility(Ability a, Color accent)
        {
            if (abilities.TryGetValue(a, out var s)) return s;
            const float R = 60f, Rin = 49f;
            Masks(AbilityGlyph(a), 0.77f, out var shape, out var prim);
            var disc = Field(p => Cov(Sdf.Circle(p, Vector2.zero, R)));
            var ring = Field(p => Cov(Sdf.Subtract(Sdf.Circle(p, Vector2.zero, R), Sdf.Circle(p, Vector2.zero, Rin))));
            // zwölf eingeschlagene Punkte im Goldring
            var ringPrim = Field(p =>
            {
                float d = float.MaxValue;
                for (int k = 0; k < 12; k++) d = Mathf.Min(d, Sdf.Circle(p, MathUtil.Dir(k * 30f) * 54.5f, 1.9f));
                return 1f - Cov(d);
            });
            var glow = Blur(shape, 7);

            var col = new Color[N * N];
            Shadow(col, disc, 0.65f);
            Outline(col, disc, Ink, 1f);
            Color dark = new Color(0.045f, 0.055f, 0.09f);
            Color deep = Color.Lerp(dark, accent, 0.42f);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int i = y * N + x;
                    if (disc[i] <= 0f) continue;
                    Vector2 p = Units(x, y);
                    float r = p.magnitude / Rin;
                    Color c = Color.Lerp(deep, dark, MathUtil.Smooth01(r * 1.05f));
                    c *= 1f - 0.5f * MathUtil.Smooth01((r - 0.72f) / 0.28f);                 // Innenschatten am Ring
                    c += accent * (0.5f * glow[i]);                                          // die Glyphe leuchtet in die Emaille
                    float gloss = MathUtil.Smooth01(1f - Sdf.Ellipse(p, new Vector2(-6f, 26f), new Vector2(36f, 17f)) / 12f);
                    c += new Color(1f, 1f, 1f) * (0.05f * gloss * MathUtil.Smooth01((p.y - 4f) / 20f));   // Glasur oben
                    c.a = disc[i];
                    Over(ref col[i], c);
                }
            Outline(col, ring, Ink, 0.75f);
            Relief(col, ring, ringPrim, Gold);
            Outline(col, shape, Ink, 0.9f);
            Relief(col, shape, prim, Ivory);
            s = ToSprite(col, "CardMedal " + a);
            abilities[a] = s;
            return s;
        }

        /// <summary>Weicher Lichtschein um eine Karte (Neun-Teilung, wird an die Kartengröße gestreckt).</summary>
        public static Sprite Halo
        {
            get
            {
                if (halo != null) return halo;
                const float D = 2f, E = 80f, Box = 8f;
                var c = new SdfCanvas(new Rect(-E, -E, 2f * E, 2f * E), D);
                c.Field(p =>
                {
                    float d = Sdf.Circle(p, Vector2.zero, Box);
                    float a = d <= 0f ? 1f : Mathf.Exp(-(d / 30f) * (d / 30f));
                    return new Color(1f, 1f, 1f, a * MathUtil.Smooth01((E - Box - d) / 12f));
                });
                float b = (E - Box) * D;
                halo = UiArt.ToUi(c, "CardHalo", new Vector4(b, b, b, b));
                return halo;
            }
        }

        /// <summary>So weit reicht der Schein über die Kante der Kartengröße hinaus, die man ihm gibt.</summary>
        public const float HaloReach = 72f;

        // ------------------------------------------------------------------ Glyphen der Fähigkeiten

        static Color Wc => UpgradeIcons.W;
        static Color Sc => UpgradeIcons.H;
        static Vector2 V(float x, float y) => new Vector2(x, y);

        static System.Action<SdfCanvas> AbilityGlyph(Ability a)
        {
            switch (a)
            {
                case SoccerFight.Ability.Flick: return Flick;
                case SoccerFight.Ability.Juggle: return Juggle;
                case SoccerFight.Ability.StepOver: return StepOver;
                case SoccerFight.Ability.Bicycle: return Bicycle;
                case SoccerFight.Ability.Tackle: return Tackle;
                case SoccerFight.Ability.Punt: return Punt;
                case SoccerFight.Ability.Wall: return Wall;
                case SoccerFight.Ability.Nutmeg: return Nutmeg;
                case SoccerFight.Ability.Decoy: return Decoy;
                case SoccerFight.Ability.Whistle: return Whistle;
                case SoccerFight.Ability.AlleyOop: return AlleyOop;
                case SoccerFight.Ability.Block: return Block;
                case SoccerFight.Ability.FastBreak: return FastBreak;
                case SoccerFight.Ability.StepBack: return StepBack;
                case SoccerFight.Ability.PumpFake: return PumpFake;
                case SoccerFight.Ability.Power: return c => UpgradeIcons.Draw(c, UpIcon.Power);
                case SoccerFight.Ability.AirKick: return c => UpgradeIcons.Draw(c, UpIcon.AirKick);
                case SoccerFight.Ability.Dash: return c => UpgradeIcons.Draw(c, UpIcon.Dash);
                case SoccerFight.Ability.Three: return c => UpgradeIcons.Draw(c, UpIcon.Distance);
                case SoccerFight.Ability.Crossover: return c => UpgradeIcons.Draw(c, UpIcon.Crossover);
                case SoccerFight.Ability.Dunk: return c => UpgradeIcons.Draw(c, UpIcon.Slam);
                case SoccerFight.Ability.Header: return c => UpgradeIcons.Draw(c, UpIcon.Knockback);
                case SoccerFight.Ability.PowerCross: return PowerCrossGlyph;
                case SoccerFight.Ability.Guard: return c => UpgradeIcons.Draw(c, UpIcon.Counter);
                case SoccerFight.Ability.Slip: return SlipGlyph;
                case SoccerFight.Ability.Uppercut: return c => UpgradeIcons.Draw(c, UpIcon.Upper);
                case SoccerFight.Ability.Hooks: return HooksGlyph;
                case SoccerFight.Ability.Pound: return c => UpgradeIcons.Draw(c, UpIcon.Quake);
                case SoccerFight.Ability.Shadow: return ShadowGlyph;
                case SoccerFight.Ability.Flurry: return FlurryGlyph;
                default: return c => UpgradeIcons.Draw(c, UpIcon.BallSpeed);
            }
        }

        /// <summary>Regenbogen-Bogen über die Gegner, der Ball landet rechts.</summary>
        static void Flick(SdfCanvas c)
        {
            Vector2 o = V(-8f, -24f);
            for (int i = 0; i < 3; i++)
            {
                float r = 48f - i * 11f;
                Color col = i == 1 ? Sc : Wc;
                c.Fill(p => Sdf.Intersect(Sdf.Ring(p, o, r, 7.5f), o.y - p.y), col);
            }
            UpgradeIcons.Ball(c, V(40f, -40f), 15f);
            c.Fill(p => Sdf.Star4(p, V(-40f, 36f), 13f, 0.5f), Wc);
            c.Fill(p => Sdf.Star4(p, V(-22f, 50f), 7f, 0.5f), Sc);
        }

        /// <summary>Ball im Takt über dem Spann, dazwischen der Rhythmus, ein kleines Herz fürs Heilen.</summary>
        static void Juggle(SdfCanvas c)
        {
            UpgradeIcons.Ball(c, V(-4f, 30f), 21f);
            UpgradeIcons.Arc(c, V(-4f, 30f), 30f, 3.5f, 20f, 75f, Sc);
            UpgradeIcons.Arc(c, V(-4f, 30f), 30f, 3.5f, 105f, 160f, Sc);
            c.Fill(p => Sdf.Tapered(p, V(-46f, -26f), 7f, V(26f, -34f), 9f), Wc);
            c.Fill(p => Sdf.Box(p, V(34f, -36f), V(12f, 7f), 4f, -8f), Wc);
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                c.Fill(p => Sdf.Circle(p, V(-4f, -18f + k * 8f), 3.4f - k * 0.6f), Sc);
            }
            c.Fill(p => UpgradeIcons.HeartSdf(p, V(40f, 4f), 0.55f), Sc);
        }

        /// <summary>Die Schleife des Fußes über den Ball, dann der Antritt.</summary>
        static void StepOver(SdfCanvas c)
        {
            Vector2 b = V(-10f, -30f);
            UpgradeIcons.Ball(c, b, 18f);
            UpgradeIcons.Arc(c, b + V(0f, 8f), 34f, 8f, 12f, 172f, Wc);
            Vector2 tip = b + V(0f, 8f) + MathUtil.Dir(12f) * 34f;
            c.Fill(p => Sdf.Triangle(p, tip + V(-11f, 4f), tip + V(11f, 4f), tip + V(0f, -16f)), Wc);
            for (int i = 0; i < 3; i++)
            {
                float y = 44f - i * 12f, len = i == 1 ? 30f : 22f;
                c.Fill(p => Sdf.Tapered(p, V(56f - len, y), 1.5f, V(56f, y), 4.5f), Sc);
            }
        }

        /// <summary>Fallrückzieher: der Bogen über Kopf, der Ball schlägt mit einem Knall ein.</summary>
        static void Bicycle(SdfCanvas c)
        {
            Vector2 o = V(-2f, -12f);
            UpgradeIcons.Arc(c, o, 38f, 8f, 18f, 200f, Wc);
            Vector2 tip = o + MathUtil.Dir(18f) * 38f;
            c.Fill(p => Sdf.Triangle(p, tip + V(-12f, 6f), tip + V(10f, 8f), tip + V(2f, -14f)), Wc);
            UpgradeIcons.Ball(c, o + MathUtil.Dir(200f) * 38f + V(0f, -8f), 14f);
            Vector2 burst = V(38f, -40f);
            c.Fill(p => Sdf.Star4(MathUtil.Rotate(p - burst, 45f) + burst, burst, 17f, 0.5f), Sc);
            c.Fill(p => Sdf.Star4(p, burst, 22f, 0.45f), Wc);
        }

        /// <summary>Grätsche: das Bein flach über den Rasen, Grasfetzen fliegen.</summary>
        static void Tackle(SdfCanvas c)
        {
            c.Fill(p => Sdf.Box(p, V(0f, -48f), V(56f, 3.5f), 2f), Sc);
            c.Fill(p => Sdf.Union(Sdf.Capsule(p, V(-38f, 12f), V(-2f, -18f), 10f), Sdf.Capsule(p, V(-2f, -18f), V(28f, -30f), 8.5f)), Wc);
            c.Fill(p => Sdf.Box(p, V(40f, -32f), V(14f, 7f), 3.5f, -8f), Wc);
            for (int i = 0; i < 4; i++)
            {
                int k = i;
                Vector2 at = V(-52f + k * 10f, -30f + (k % 2) * 13f);
                c.Fill(p => Sdf.Box(p, at, V(5.5f, 3.2f), 1.5f, 25f + k * 30f), Sc);
            }
            for (int i = 0; i < 3; i++)
            {
                float y = 40f - i * 11f, len = i == 1 ? 34f : 24f;
                c.Fill(p => Sdf.Tapered(p, V(-54f, y), 1.5f, V(-54f + len, y), 4f), Sc);
            }
        }

        /// <summary>Abstoß: der Ball kommt als Meteor genau ins Ziel.</summary>
        static void Punt(SdfCanvas c)
        {
            c.Fill(p => Mathf.Abs(Sdf.Ellipse(p, V(-10f, -42f), V(34f, 10f))) - 2.5f, Wc);
            c.Fill(p => Sdf.Ellipse(p, V(-10f, -42f), V(13f, 4f)), Sc);
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                Vector2 a = V(56f - k * 4f, 58f - k * 12f), b = V(20f - k * 2f, 20f - k * 4f);
                c.Fill(p => Sdf.Tapered(p, a, 1.5f, b, 7f - k), k == 1 ? Wc : Sc);
            }
            UpgradeIcons.Ball(c, V(6f, 4f), 19f);
        }

        /// <summary>Mauer: drei Spieler Schulter an Schulter, ein Ball prallt ab.</summary>
        static void Wall(SdfCanvas c)
        {
            c.Fill(p => Sdf.Box(p, V(0f, -48f), V(54f, 3.5f), 2f), Sc);
            for (int i = 0; i < 3; i++)
            {
                float x = -30f + i * 30f, h = i == 1 ? 25f : 21f;
                c.Fill(p => Sdf.Union(Sdf.Box(p, V(x, -44f + h), V(12f, h), 8f), Sdf.Circle(p, V(x, 2f * h - 30f), 9.5f)), Wc);
            }
            UpgradeIcons.Ball(c, V(42f, 44f), 11f);
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                c.Fill(p => Sdf.Tapered(p, V(30f - k * 6f, 30f + k * 10f), 3.5f, V(12f - k * 10f, 42f + k * 10f), 1f), Sc);
            }
        }

        /// <summary>Tunnel: der Ball rollt zwischen zwei Beinen hindurch.</summary>
        static void Nutmeg(SdfCanvas c)
        {
            c.Fill(p => Sdf.Union(Sdf.Box(p, V(0f, 40f), V(28f, 10f), 8f),
                Sdf.Union(Sdf.Capsule(p, V(-20f, 36f), V(-28f, -34f), 9f), Sdf.Capsule(p, V(20f, 36f), V(28f, -34f), 9f))), Wc);
            c.Fill(p => Sdf.Box(p, V(-34f, -42f), V(12f, 6f), 3f), Sc);
            c.Fill(p => Sdf.Box(p, V(34f, -42f), V(12f, 6f), 3f), Sc);
            UpgradeIcons.Ball(c, V(2f, -24f), 14f);
            for (int i = 0; i < 3; i++)
            {
                float y = -24f + (i - 1) * 10f, len = i == 1 ? 26f : 18f;
                c.Fill(p => Sdf.Tapered(p, V(-58f, y), 1.2f, V(-58f + len, y), 3.5f), Sc);
            }
        }

        /// <summary>Lockvogel: der echte Körper weicht aus, das Nachbild bleibt stehen.</summary>
        static void Decoy(SdfCanvas c)
        {
            c.Fill(p => Sdf.Union(Sdf.Circle(p, V(20f, 28f), 12.5f), Sdf.Capsule(p, V(20f, 6f), V(20f, -34f), 14.5f)), Sc);
            c.Fill(p => Sdf.Union(Sdf.Circle(p, V(-18f, 30f), 13f), Sdf.Capsule(p, V(-18f, 8f), V(-18f, -36f), 15f)), Wc);
            c.Fill(p => Sdf.Star4(p, V(44f, 44f), 13f, 0.5f), Wc);
            for (int i = 0; i < 3; i++)
            {
                float y = -8f + i * 12f;
                c.Fill(p => Sdf.Tapered(p, V(-58f, y), 1.2f, V(-40f, y), 3.5f), Sc);
            }
        }

        /// <summary>Schlusspfiff: die Pfeife mit Schallbögen.</summary>
        static void Whistle(SdfCanvas c)
        {
            c.Fill(p => Sdf.Subtract(Sdf.SmoothUnion(Sdf.Circle(p, V(-14f, -8f), 25f), Sdf.Box(p, V(18f, 6f), V(22f, 8f), 4f), 6f),
                Sdf.Circle(p, V(-16f, -8f), 8f)), Wc);
            c.Fill(p => Sdf.Ring(p, V(-34f, 20f), 9f, 4.5f), Sc);
            for (int i = 0; i < 2; i++)
                UpgradeIcons.Arc(c, V(38f, 8f), 16f + i * 12f, 5f - i, 25f, 95f, i == 0 ? Wc : Sc);
        }

        /// <summary>Alley-Oop: der Ball hängt hoch oben, die gepunktete Bahn führt in den Korb.</summary>
        static void AlleyOop(SdfCanvas c)
        {
            c.Fill(p => Sdf.Box(p, V(52f, -4f), V(3.5f, 30f), 1.5f), Sc);
            c.Fill(p => Mathf.Abs(Sdf.Ellipse(p, V(28f, -18f), V(21f, 6f))) - 3.2f, Wc);
            for (int i = 0; i < 4; i++)
            {
                float x0 = 12f + i * 10.5f, x1 = 20f + i * 5f;
                c.Fill(p => Sdf.Capsule(p, V(x0, -20f), V(x1, -46f), 1.8f), Sc);
            }
            c.Fill(p => Sdf.Capsule(p, V(20f, -46f), V(35f, -46f), 1.8f), Sc);
            for (int i = 0; i < 5; i++)
            {
                float t = (i + 1) / 6f;
                Vector2 a = V(-22f, 32f), m = V(22f, 44f), b = V(28f, -6f);
                Vector2 q = Vector2.Lerp(Vector2.Lerp(a, m, t), Vector2.Lerp(m, b, t), t);
                c.Fill(p => Sdf.Circle(p, q, 3.2f), Sc);
            }
            UpgradeIcons.HoopBall(c, V(-28f, 28f), 19f);
            c.Fill(p => Sdf.Star4(p, V(-4f, 54f), 10f, 0.5f), Wc);
        }

        /// <summary>Block: die offene Hand hoch, ein Geschoss prallt zurück.</summary>
        static void Block(SdfCanvas c)
        {
            c.Fill(p =>
            {
                float palm = Sdf.Box(p, V(-8f, -12f), V(20f, 20f), 9f);
                float f = Sdf.Capsule(p, V(-24f, 2f), V(-26f, 30f), 5.6f);
                f = Sdf.Union(f, Sdf.Capsule(p, V(-12f, 6f), V(-12f, 42f), 5.6f));
                f = Sdf.Union(f, Sdf.Capsule(p, V(0f, 6f), V(2f, 40f), 5.6f));
                f = Sdf.Union(f, Sdf.Capsule(p, V(10f, 2f), V(14f, 30f), 5.6f));
                f = Sdf.Union(f, Sdf.Capsule(p, V(10f, -16f), V(26f, 2f), 6f));
                float wrist = Sdf.Capsule(p, V(-8f, -34f), V(-10f, -58f), 13f);
                return Sdf.Union(Sdf.SmoothUnion(palm, f, 3f), wrist);
            }, Wc);
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                Vector2 a = V(32f, 30f), b = a + MathUtil.Dir(20f + k * 28f) * (26f - k * 3f);
                c.Fill(p => Sdf.Tapered(p, a, 4f, b, 1.2f), Sc);
            }
            c.Fill(p => Sdf.Star4(p, V(32f, 30f), 15f, 0.45f), Wc);
        }

        /// <summary>Fastbreak: der Ball vorneweg, ein Pfeil treibt ihn nach vorn.</summary>
        static void FastBreak(SdfCanvas c)
        {
            for (int i = 0; i < 3; i++)
            {
                float y = 18f - i * 18f, len = i == 1 ? 38f : 26f;
                c.Fill(p => Sdf.Tapered(p, V(-58f, y), 1.5f, V(-58f + len, y), 5f), Sc);
            }
            UpgradeIcons.HoopBall(c, V(2f, 0f), 23f);
            c.Fill(p => Sdf.Union(Sdf.Capsule(p, V(36f, 24f), V(54f, 0f), 6.5f), Sdf.Capsule(p, V(54f, 0f), V(36f, -24f), 6.5f)), Wc);
            c.Fill(p => Sdf.Box(p, V(0f, -40f), V(40f, 3f), 1.5f), Sc);
        }

        // ------------------------------------------------------------------ Boxen

        /// <summary>Kraftgerade: ein großer Handschuh, vor den Knöcheln ein Stern.</summary>
        static void PowerCrossGlyph(SdfCanvas c)
        {
            for (int i = 0; i < 3; i++) UpgradeIcons.Line(c, V(-58f, 20f - i * 20f), V(-36f, 20f - i * 20f), 3f, Sc);
            UpgradeIcons.Glove(c, V(-4f, 0f), 34f, 0f, Wc);
            c.Fill(p => Sdf.Star4(p, V(42f, 2f), 24f, 0.45f), Sc);
        }

        /// <summary>Konterschritt: zwei Winkel nach hinten, der Handschuh bereit mit Krit-Funken.</summary>
        static void SlipGlyph(SdfCanvas c)
        {
            for (int i = 0; i < 2; i++)
            {
                float x = -12f - i * 25f;
                c.Fill(p => Sdf.Union(Sdf.Capsule(p, V(x + 19f, 28f), V(x, 0f), 6.5f), Sdf.Capsule(p, V(x, 0f), V(x + 19f, -28f), 6.5f)), i == 0 ? Wc : Sc);
            }
            UpgradeIcons.Glove(c, V(30f, 6f), 22f, 0f, Wc);
            c.Fill(p => Sdf.Star4(p, V(48f, 42f), 13f, 0.5f), Wc);
        }

        /// <summary>Doppelhaken: zwei Bögen rundherum, der Handschuh in der Mitte.</summary>
        static void HooksGlyph(SdfCanvas c)
        {
            UpgradeIcons.Arc(c, Vector2.zero, 46f, 4f, 20f, 150f, Wc);
            UpgradeIcons.Arc(c, Vector2.zero, 46f, 4f, 200f, 330f, Sc);
            UpgradeIcons.Glove(c, V(0f, 0f), 24f, 20f, Wc);
        }

        /// <summary>Schattenboxer: der Handschuh und sein Schatten dahinter.</summary>
        static void ShadowGlyph(SdfCanvas c)
        {
            UpgradeIcons.Glove(c, V(-14f, 14f), 28f, 10f, Sc);
            UpgradeIcons.Glove(c, V(12f, -10f), 28f, 10f, Wc);
        }

        /// <summary>Trommelfeuer: drei Handschuhe mit ihren Wellen.</summary>
        static void FlurryGlyph(SdfCanvas c)
        {
            for (int i = 0; i < 3; i++)
            {
                float y = 28f - i * 28f, x = -22f + (i % 2) * 12f;
                UpgradeIcons.Glove(c, V(x, y), 18f, 0f, Wc);
                c.Fill(p => Sdf.Subtract(Sdf.Circle(p, V(x + 40f, y), 14f), Sdf.Circle(p, V(x + 33f, y), 15f)), Sc);
            }
        }

        /// <summary>Stepback: zwei Winkel treiben nach hinten, der Ball liegt wurfbereit mit Krit-Funken.</summary>
        static void StepBack(SdfCanvas c)
        {
            for (int i = 0; i < 2; i++)
            {
                float x = -10f - i * 25f;
                c.Fill(p => Sdf.Union(Sdf.Capsule(p, V(x + 19f, 28f), V(x, 0f), 6.5f), Sdf.Capsule(p, V(x, 0f), V(x + 19f, -28f), 6.5f)), i == 0 ? Wc : Sc);
            }
            UpgradeIcons.HoopBall(c, V(32f, 12f), 19f);
            c.Fill(p => Sdf.Star4(p, V(48f, 42f), 13f, 0.5f), Wc);
            c.Fill(p => Sdf.Box(p, V(-4f, -46f), V(44f, 3f), 1.5f), Sc);
        }

        /// <summary>Pump Fake: der echte Ball bleibt unten, die gepunktete Bahn führt zu einem Geisterball.</summary>
        static void PumpFake(SdfCanvas c)
        {
            for (int i = 0; i < 5; i++)
            {
                float t = (i + 1) / 6f;
                Vector2 a = V(-22f, 0f), m = V(-2f, 56f), b = V(24f, 28f);
                Vector2 q = Vector2.Lerp(Vector2.Lerp(a, m, t), Vector2.Lerp(m, b, t), t);
                c.Fill(p => Sdf.Circle(p, q, 3.4f), Sc);
            }
            c.Fill(p => Sdf.Ring(p, V(34f, 22f), 16f, 3.4f), Sc);
            UpgradeIcons.HoopBall(c, V(-24f, -24f), 22f);
            c.Fill(p => Sdf.Star4(p, V(50f, 50f), 11f, 0.5f), Wc);
        }

        // ------------------------------------------------------------------ Relief

        static Vector2 Units(int x, int y) => new Vector2(-Half + (x + 0.5f) / Ppu, -Half + (y + 0.5f) / Ppu);
        static float Cov(float d) => Mathf.Clamp01(0.5f - d * Ppu);
        static float Sq(float x) => x * x;

        static float[] Field(System.Func<Vector2, float> fn)
        {
            var a = new float[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++) a[y * N + x] = fn(Units(x, y));
            return a;
        }

        /// <summary>Die Glyphe zweimal zeichnen: ganze Form und nur die Hauptteile. scale &lt; 1 verkleinert sie.</summary>
        static void Masks(System.Action<SdfCanvas> draw, float scale, out float[] shape, out float[] prim)
        {
            // Nebenteile (Halbweiß) zählen voll, blassere „Geister“ (Echo, Ausweichen) bleiben halb durchsichtig
            shape = Alpha(Canvas(scale, c => UpgradeIcons.DrawMask(c, draw, true)), 1f / 0.45f);
            prim = Alpha(Canvas(scale, c => UpgradeIcons.DrawMask(c, draw, false)), 1f);
        }

        static SdfCanvas Canvas(float scale, System.Action<SdfCanvas> draw)
        {
            float ppu = Ppu * scale;
            float size = N / ppu - 1e-3f;
            var c = new SdfCanvas(new Rect(-Half / scale, -Half / scale, size, size), ppu);
            draw(c);
            return c;
        }

        static float[] Alpha(SdfCanvas c, float gain)
        {
            var a = new float[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++) a[y * N + x] = Mathf.Clamp01(c.Get(x, y).a * gain);
            return a;
        }

        /// <summary>Zwei Kastenfilter hintereinander, außen zählt als leer.</summary>
        static float[] Blur(float[] src, int r)
        {
            var a = (float[])src.Clone();
            var tmp = new float[a.Length];
            for (int it = 0; it < 2; it++) { Box(a, tmp, r, 1, N); Box(tmp, a, r, N, 1); }
            return a;
        }

        /// <summary>step: Abstand zweier Nachbarn in Filterrichtung, lane: Abstand zweier Zeilen/Spalten.</summary>
        static void Box(float[] s, float[] d, int r, int step, int lane)
        {
            float inv = 1f / (2 * r + 1);
            for (int l = 0; l < N; l++)
            {
                int o = l * lane;
                float acc = 0f;
                for (int k = 0; k <= r && k < N; k++) acc += s[o + k * step];
                for (int x = 0; x < N; x++)
                {
                    d[o + x * step] = acc * inv;
                    int drop = x - r, add = x + r + 1;
                    if (drop >= 0) acc -= s[o + drop * step];
                    if (add < N) acc += s[o + add * step];
                }
            }
        }

        static void Over(ref Color d, Color s)
        {
            float sa = s.a;
            if (sa <= 0f) return;
            float oa = sa + d.a * (1f - sa);
            if (oa <= 1e-6f) return;
            float inv = 1f / oa, k = d.a * (1f - sa);
            d.r = (s.r * sa + d.r * k) * inv;
            d.g = (s.g * sa + d.g * k) * inv;
            d.b = (s.b * sa + d.b * k) * inv;
            d.a = oa;
        }

        /// <summary>Weicher Schatten schräg unter der Form.</summary>
        static void Shadow(Color[] col, float[] shape, float strength)
        {
            var b = Blur(shape, 5);
            const int dx = 2, dy = 6;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int sx = x - dx, sy = y + dy;
                    if (sx < 0 || sy >= N) continue;
                    float a = b[sy * N + sx] * strength;
                    if (a > 0.002f) Over(ref col[y * N + x], new Color(0f, 0.01f, 0.03f, a));
                }
        }

        /// <summary>Dunkle Kontur, zwei bis drei Pixel breit und weich auslaufend.</summary>
        static void Outline(Color[] col, float[] shape, Color ink, float strength)
        {
            var b = Blur(shape, 2);
            for (int i = 0; i < col.Length; i++)
            {
                float a = Mathf.Clamp01(b[i] * 3.2f) * strength;
                if (a > 0.002f) Over(ref col[i], ink.WithAlpha(a));
            }
        }

        /// <summary>
        /// Prägt die Form als Metall: aus der weichgezeichneten Form wird ein Höhenfeld (Nebenteile liegen tiefer),
        /// dessen Neigung das Licht von links oben bestimmt. Dazu der Farbverlauf von hell oben nach dunkel unten
        /// mit dem gespiegelten Horizont eines Chroms und scharfe Glanzkanten.
        /// </summary>
        static void Relief(Color[] col, float[] shape, float[] prim, Metal m)
        {
            var h = Blur(shape, 2);
            var ps = Blur(prim, 1);
            var height = new float[N * N];
            for (int i = 0; i < height.Length; i++) height[i] = h[i] * (0.6f + 0.4f * ps[i]);
            Vector3 L = new Vector3(-0.5f, 0.62f, 0.6f).normalized;
            Vector3 hv = (L + Vector3.forward).normalized;
            const float slope = 7f;
            for (int y = 1; y < N - 1; y++)
                for (int x = 1; x < N - 1; x++)
                {
                    int i = y * N + x;
                    float a = shape[i];
                    if (a <= 0.003f) continue;
                    float gx = (height[i + 1] - height[i - 1]) * 0.5f, gy = (height[i + N] - height[i - N]) * 0.5f;
                    Vector3 n = new Vector3(-gx * slope, -gy * slope, 1f).normalized;
                    float diff = Mathf.Max(0f, Vector3.Dot(n, L));
                    float spec = Mathf.Pow(Mathf.Max(0f, Vector3.Dot(n, hv)), 36f);
                    float v = Mathf.Clamp01((y / (float)N - 0.14f) / 0.72f);
                    Color c = v < 0.5f ? Color.Lerp(m.lo, m.mid, v * 2f) : Color.Lerp(m.mid, m.hi, v * 2f - 1f);
                    c *= 1f - 0.16f * Mathf.Exp(-Sq((v - 0.44f) / 0.06f));
                    c = Color.Lerp(c, m.sec * (0.62f + 0.5f * v), (1f - Mathf.Clamp01(ps[i])) * 0.7f);
                    c = c * (0.32f + diff) + m.spec * (spec * 0.85f);
                    c.r = Mathf.Min(1f, c.r); c.g = Mathf.Min(1f, c.g); c.b = Mathf.Min(1f, c.b);
                    c.a = a;
                    Over(ref col[i], c);
                }
        }

        static Sprite ToSprite(Color[] col, string name)
        {
            float size = N / Ppu - 1e-3f;
            var c = new SdfCanvas(new Rect(-Half, -Half, size, size), Ppu);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++) c.Set(x, y, col[y * N + x]);
            return UiArt.ToUi(c, name);
        }
    }
}
