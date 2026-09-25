using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Ein Dribbel-Schlag wie im echten Basketball, für Spielfigur und Menüfigur gemeinsam.
    /// Oben liegt die Hand auf dem Ball, der Ellbogen ist angewinkelt. Dann drückt der ganze Arm den Ball
    /// nach unten (Ellbogen streckt sich, Handgelenk klappt ab), der Ball fliegt frei zum Boden und zurück,
    /// die Hand holt ihn auf dem Weg nach oben ab und federt ihn weich bis zum Umkehrpunkt.
    /// Alle Kurven gehen mit stetiger Geschwindigkeit ineinander über (nur der Aufprall am Boden kehrt sie um),
    /// deshalb ruckelt nichts. Die Arm-Abstände sind Anteile der Armlänge und bleiben sicher unter der
    /// vollen Streckung, wo die Gelenkrechnung sonst hin- und herspringt.
    /// Koordinaten: figurlokal, Blick nach rechts, y = 0 ist der Boden.
    /// </summary>
    public static class DribbleMotion
    {
        public struct Frame
        {
            public Vector2 Ball, Wrist;
            /// <summary>Drehung der offenen Hand (0: Finger waagerecht nach vorn, positiv: Finger heben sich).</summary>
            public float HandAngle;
            /// <summary>0..1: wie stark der Arm gerade drückt (für ein kleines Mitgehen des Oberkörpers).</summary>
            public float Push;
        }

        // Zeitpunkte im Takt (0 = Ball oben in der Hand)
        const float Release = 0.24f, Low = 0.38f, Catch = 0.80f;

        // Handgelenk-Winkel über den Takt: angenommen leicht nach oben abgeknickt, beim Drücken abgeklappt,
        // nach dem Loslassen schnappt es nach, dann hebt sich die Hand locker wieder
        static readonly float[] AngU = { 0f, Release, Low, 0.55f, Catch };
        static readonly float[] AngV = { 2f, -5f, -12f, -4f, 5f };

        /// <param name="u">Takt 0..1</param>
        /// <param name="shoulder">vorderes Schultergelenk</param>
        /// <param name="upper">Länge Oberarm</param>
        /// <param name="fore">Länge Unterarm</param>
        /// <param name="bx">wo der Ball aufspringt</param>
        /// <param name="palmReach">Handgelenk → Handfläche entlang der offenen Hand</param>
        /// <param name="drive">0..1 beim Laufen: die Hand sitzt hinten auf dem Ball und schiebt ihn mit</param>
        /// <param name="low">0..1: tiefer und kürzer dribbeln</param>
        public static Frame Eval(float u, Vector2 shoulder, float upper, float fore, float bx, float palmReach, float drive, float low)
        {
            float R = Art.BallRadius;
            float L = upper + fore;
            u = Mathf.Repeat(u, 1f);

            // Ballhöhen an den Schlüsselstellen, aus dem Abstand Schulter → Handgelenk abgeleitet
            float yTop = BallY(0f, Mathf.Lerp(0.65f, 0.7f, low));
            float yRel = BallY(Release, Mathf.Lerp(0.87f, 0.89f, low));
            float yCatch = BallY(Catch, Mathf.Lerp(0.74f, 0.8f, low));
            yRel = Mathf.Min(yRel, yTop - 0.06f);
            yCatch = Mathf.Min(yCatch, yTop - 0.05f);

            // Drücken: aus der Ruhe beschleunigt
            float vRel = 2f * (yRel - yTop) / Release;
            // Freier Fall bis zum Boden, dabei noch etwas schneller
            float fallU = Mathf.Clamp(2f * (R - yRel) / (vRel * 2.15f), 0.08f, 0.3f);
            float uGround = Release + fallU;
            float fallSec = (R - yRel) / fallU;
            float vGround = Mathf.Clamp(2f * (R - yRel) / fallU - vRel, 3f * fallSec, fallSec);
            // Rücksprung mit etwas Energieverlust, dann holt die Hand ihn ab und bremst ihn bis oben
            float riseSec = (yCatch - R) / (Catch - uGround);
            float vUp = Mathf.Clamp(-0.8f * vGround, riseSec, 3f * riseSec);
            float vCatch = Mathf.Clamp(2f * (yTop - yCatch) / (1f - Catch), 0f, 3f * riseSec);

            float by;
            if (u < Release) by = Hermite(yTop, 0f, yRel, vRel, u / Release, Release);
            else if (u < uGround) by = Hermite(yRel, vRel, R, vGround, (u - Release) / fallU, fallU);
            else if (u < Catch) by = Hermite(R, vUp, yCatch, vCatch, (u - uGround) / (Catch - uGround), Catch - uGround);
            else by = Hermite(yCatch, vCatch, yTop, 0f, (u - Catch) / (1f - Catch), 1f - Catch);

            var f = new Frame { Ball = new Vector2(bx, by), HandAngle = Angle(u) };
            f.Push = MathUtil.Bump(Mathf.Clamp01(u / (Low + 0.05f)));
            if (u >= Catch || u <= Release)
            {
                // die Hand liegt auf dem Ball
                f.Wrist = f.Ball + Offset(f.HandAngle);
                return f;
            }

            // frei: das Handgelenk schwingt nach dem Loslassen noch etwas durch und hebt sich dann dem Ball entgegen,
            // mit genau der Geschwindigkeit, die es beim Loslassen hatte und beim Annehmen braucht
            const float e = 0.004f;
            Vector2 wRel = OnBall(Release), vwRel = (OnBall(Release) - OnBall(Release - e)) / e;
            Vector2 wCatch = OnBall(Catch), vwCatch = (OnBall(Catch + e) - OnBall(Catch)) / e;
            // am tiefsten Punkt ist der Arm fast gestreckt (SoftReach fängt den Rest weich ab)
            float dLow = 0.88f * L;
            Vector2 wLow = new Vector2(bx + Offset(Angle(Low)).x, 0f);
            wLow.y = Mathf.Min(WristY(wLow.x, dLow), wRel.y - 0.03f);
            if (u < Low)
            {
                float s = (u - Release) / (Low - Release);
                f.Wrist = new Vector2(Hermite(wRel.x, vwRel.x, wLow.x, 0f, s, Low - Release), Hermite(wRel.y, vwRel.y, wLow.y, 0f, s, Low - Release));
            }
            else
            {
                float s = (u - Low) / (Catch - Low);
                f.Wrist = new Vector2(Hermite(wLow.x, 0f, wCatch.x, vwCatch.x, s, Catch - Low), Hermite(wLow.y, 0f, wCatch.y, vwCatch.y, s, Catch - Low));
            }
            return f;

            // die Hand liegt tangential auf dem Ball: der Winkel verschiebt den Auflagepunkt über die Balloberfläche
            Vector2 Offset(float a) => MathUtil.Rotate(new Vector2(-palmReach, R + 0.035f), a);
            float Angle(float uu) => PeriodicSpline(AngU, AngV, uu) + 12f * drive;
            float WristY(float wx, float d)
            {
                float dx = wx - shoulder.x;
                return shoulder.y - Mathf.Sqrt(Mathf.Max(d * d * 0.1f, d * d - dx * dx));
            }
            float BallY(float uu, float frac)
            {
                Vector2 off = Offset(Angle(uu));
                return Mathf.Max(R + 0.2f, WristY(bx + off.x, frac * L) - off.y);
            }
            Vector2 OnBall(float uu)
            {
                float yy = uu <= Release ? Hermite(yTop, 0f, yRel, vRel, uu / Release, Release)
                                         : Hermite(yCatch, vCatch, yTop, 0f, (uu - Catch) / (1f - Catch), 1f - Catch);
                return new Vector2(bx, yy) + Offset(Angle(uu));
            }
        }

        /// <summary>Kubische Hermite-Kurve; v0/v1 sind Geschwindigkeiten pro Takt, span die Länge des Abschnitts im Takt.</summary>
        static float Hermite(float y0, float v0, float y1, float v1, float s, float span)
        {
            s = Mathf.Clamp01(s);
            float s2 = s * s, s3 = s2 * s;
            return (2f * s3 - 3f * s2 + 1f) * y0 + (s3 - 2f * s2 + s) * v0 * span + (-2f * s3 + 3f * s2) * y1 + (s3 - s2) * v1 * span;
        }

        /// <summary>Weiche, geschlossene Kurve durch Schlüsselwerte (Catmull-Rom über den Takt).</summary>
        static float PeriodicSpline(float[] us, float[] vs, float u)
        {
            int n = us.Length;
            int i = n - 1;
            for (int k = 0; k < n - 1; k++) if (u < us[k + 1]) { i = k; break; }
            float u0 = us[i], u1 = i + 1 < n ? us[i + 1] : 1f;
            float y0 = vs[i], y1 = vs[(i + 1) % n];
            float span = u1 - u0;
            float m0 = Slope(i), m1 = Slope((i + 1) % n);
            return Hermite(y0, m0, y1, m1, (u - u0) / span, span);

            float Slope(int k)
            {
                int p = (k - 1 + n) % n, q = (k + 1) % n;
                float up = us[p] - (k == 0 ? 1f : 0f), uq = us[q] + (q == 0 ? 1f : 0f);
                return (vs[q] - vs[p]) / (uq - up);
            }
        }
    }
}
