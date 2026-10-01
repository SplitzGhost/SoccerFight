using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Die Boxer im Rig. Grundhaltung ist der Boxstand: Führhand (hinterer Arm) vorn auf Kinnhöhe, Schlaghand (vorderer
    /// Arm) dicht am Kinn, die Füße versetzt, die Knie federn im Takt. Die Fäuste werden über Arm-IK gesetzt, und zwar
    /// relativ zur Schulter: so wandern sie mit, wenn sich der Körper beugt oder hebt, statt an einem Punkt in der Luft
    /// zu hängen. Jeder Schlag läuft pro Arm als eigene kleine Zeitlinie (ausholen, schnappen, halten, zurückziehen),
    /// damit eine schnelle Eins-Zwei sauber ineinanderläuft. Der Körper geht mit: Gewicht nach vorn, Ferse hoch.
    /// </summary>
    public sealed partial class PlayerRig
    {
        sealed class PunchArm { public float t = 9f, e, e0; public int kind; public Vector2 aim = Vector2.right; }

        // Fäuste relativ zur Schulter (Blickrichtung rechts, aufrecht); erst nach dem Rumpf in Ziele umgerechnet
        bool boxRel;
        Vector2 nearRel, farRel;
        float nearFollow = 1f, farFollow = 1f;
        readonly PunchArm nearPunch = new PunchArm(), farPunch = new PunchArm();
        int seenSerial = int.MinValue;
        float guardShake, guardShakeVel;

        /// <summary>Ein Schlag verlässt die Faust: ein kleiner Ruck durch den Körper.</summary>
        public void OnPunch(int step) { squashXVel += step == 2 ? 1.3f : 0.6f; squashYVel -= step == 2 ? 0.8f : 0.35f; }

        /// <summary>Ein Treffer prallt auf die Deckung: die Fäuste werden zurückgedrückt, die Knie geben nach.</summary>
        public void OnGuardHit() { guardShakeVel -= 7f; squashXVel -= 0.7f; squashYVel -= 0.5f; hipDipVel -= 0.45f; }

        // Grundhaltung der Fäuste (Handgelenk relativ zur eigenen Schulter)
        static readonly Vector2 GuardNear = new Vector2(0.2f, -0.02f), GuardFar = new Vector2(0.3f, -0.05f);

        // ------------------------------------------------------------------ timing of one punch

        /// <summary>kind: 0 Jab, 1 Gerade, 2 Haken (Führhand), 3 Haken (Schlaghand), 4 Trommelfeuer, 5 Schlag im Tanz.</summary>
        static void Timing(int kind, out float wind, out float hit, out float hold, out float back)
        {
            switch (kind)
            {
                case 1: wind = 0.025f; hit = Player.StraightHit; hold = 0.035f; back = 0.17f; break;
                case 2: case 3: wind = 0.035f; hit = Player.HookHit; hold = 0.04f; back = 0.19f; break;
                case 4: wind = 0f; hit = 0.04f; hold = 0.012f; back = 0.085f; break;
                case 5: wind = 0f; hit = 0.05f; hold = 0.03f; back = 0.12f; break;
                default: wind = 0.02f; hit = Player.JabHit; hold = 0.03f; back = 0.14f; break;
            }
        }

        /// <summary>Wie weit der Arm gerade ausgestreckt ist (−0,12 beim Ausholen … 1 voll gestreckt).</summary>
        static float Extension(PunchArm a)
        {
            Timing(a.kind, out float wind, out float hit, out float hold, out float back);
            float t = a.t;
            if (t >= hit + hold + back) return 0f;
            if (t < wind) return Mathf.Lerp(a.e0, -0.12f, MathUtil.Smooth01(t / wind));
            if (t < hit)
            {
                float from = wind > 0f ? -0.12f : a.e0;
                float u = (t - wind) / Mathf.Max(0.001f, hit - wind);
                return Mathf.Lerp(from, 1f, 1f - Mathf.Pow(1f - u, 2.2f));   // schnappt heraus und bremst in die volle Streckung
            }
            if (t < hit + hold) return 1f;
            return 1f - MathUtil.EaseInOutSine((t - hit - hold) / back);
        }

        void StartPunchArm(PunchArm a, int kind, Vector2 aim, bool late)
        {
            a.e0 = a.e;   // der neue Schlag beginnt dort, wo der Arm gerade ist (kein Sprung)
            a.kind = kind;
            a.aim = aim.sqrMagnitude > 0.01f ? aim.normalized : Vector2.right;
            Timing(kind, out _, out float hit, out _, out _);
            // ein Schlag, den eine Fähigkeit erst im Treffmoment meldet: die Faust ist sofort fast vorn
            a.t = late ? Mathf.Max(0f, hit - 0.035f) : 0f;
        }

        /// <summary>Wo das Handgelenk für eine Streckung e steht (relativ zur Schulter).</summary>
        Vector2 PunchRel(PunchArm a, Vector2 guard, float e)
        {
            float reach = Reach;
            if (a.kind == 2 || a.kind == 3)
            {
                // der Haken: der Ellbogen kommt hoch und raus, die Faust kreist auf Kopfhöhe nach vorn
                Vector2 end = a.aim * reach * 0.84f + new Vector2(0f, 0.04f);
                if (e < 0f) return guard + new Vector2(e * 0.4f, e * 0.12f);
                Vector2 ctrl = guard * 0.4f + end * 0.35f + new Vector2(-0.06f, 0.26f);
                float u = e;
                return (1f - u) * (1f - u) * guard + 2f * (1f - u) * u * ctrl + u * u * end;
            }
            Vector2 straight = a.aim * reach * 0.97f;
            if (e < 0f) return guard + new Vector2(e * 0.3f, -e * 0.05f);
            return Vector2.Lerp(guard, straight, e);
        }

        // ------------------------------------------------------------------ the stance (every frame)

        void BoxCarry(float dt, ref float hipY, float air, float sk, ref Vector2 nearFoot, ref float nearFlat, ref float nearPoint,
            ref Vector2 farFoot, ref float farFlat, ref float farPoint, ref float leanTarget, ref float headTarget, ref float extraHipY)
        {
            float ground = 1f - air;
            float stand = (1f - moveBlend) * ground;

            // ein neuer Schlag (eigener oder vom Duo-Partner gemeldet)
            if (player.PunchSerial != seenSerial)
            {
                bool first = seenSerial == int.MinValue;
                seenSerial = player.PunchSerial;
                if (!first)
                {
                    var act = player.CurrentAction;
                    int step = player.PunchStep;
                    bool near = step == 1;
                    int kind = act == Player.Action.Flurry ? 4
                             : act == Player.Action.Ulti ? 5
                             : act == Player.Action.Hooks ? (step == 2 ? 2 : 3)
                             : step;
                    if (act == Player.Action.Hooks) near = step != 2;
                    bool late = act != Player.Action.Punch && act != Player.Action.Flurry;
                    StartPunchArm(near ? nearPunch : farPunch, kind, player.KickAimLocal, late);
                }
            }
            nearPunch.t += dt; farPunch.t += dt;
            nearPunch.e = Extension(nearPunch);
            farPunch.e = Extension(farPunch);

            // Boxstand: Füße versetzt (Führbein vorn), Knie gebeugt, im Stand ein federndes Wippen auf den Fußballen
            float beat = 0.5f + 0.5f * Mathf.Cos(time * MathUtil.Tau * 1.7f);
            nearFoot.x = Mathf.Lerp(nearFoot.x, -0.2f, stand);
            farFoot.x = Mathf.Lerp(farFoot.x, 0.25f, stand);
            hipY -= 0.065f * ground + 0.028f * beat * stand;
            nearPoint = Mathf.Max(nearPoint, 0.12f * beat * stand);
            nearFlat = Mathf.Min(nearFlat, 1f - 0.25f * beat * stand);
            leanTarget -= 5f * ground;
            headTarget -= 3f;

            // Grundhaltung der Fäuste; im Lauf pumpen sie leicht gegen die Beine, in der Luft ziehen sie sich zusammen
            float swing = Mathf.Sin(MathUtil.Tau * phase) * runBlend * moveBlend;
            float sway = Mathf.Sin(time * 3.3f) * 0.012f * stand;
            Vector2 gn = GuardNear + new Vector2(-0.05f * runBlend * moveBlend + 0.045f * swing, -0.06f * runBlend * moveBlend + beat * 0.01f * stand);
            Vector2 gf = GuardFar + new Vector2(-0.06f * runBlend * moveBlend - 0.045f * swing + sway, -0.05f * runBlend * moveBlend + beat * 0.012f * stand);
            gn = Vector2.Lerp(gn, new Vector2(0.16f, 0.04f), air);
            gf = Vector2.Lerp(gf, new Vector2(0.24f, 0.02f), air);
            // die Deckung zittert nach, wenn ein Treffer darauf prallt
            MathUtil.Spring(ref guardShake, ref guardShakeVel, 0f, 6f, 0.35f, dt);
            Vector2 shake = new Vector2(guardShake * 0.05f, guardShake * 0.012f);
            gn += shake; gf += shake;

            nearRel = PunchRel(nearPunch, gn, nearPunch.e);
            farRel = PunchRel(farPunch, gf, farPunch.e);
            nearFollow = 1f - 0.85f * Mathf.Clamp01(nearPunch.e);
            farFollow = 1f - 0.85f * Mathf.Clamp01(farPunch.e);
            boxRel = true;
            nearIKw = farIKw = 1f;
            nearWrist = farWrist = 0f;

            // der Körper geht mit: die Gerade dreht die Hüfte ein (Gewicht nach vorn, Ferse hoch), der Jab schiebt das Führbein vor
            float straight = Mathf.Max(0f, nearPunch.e) * (nearPunch.kind == 1 || nearPunch.kind == 4 || nearPunch.kind == 5 ? 1f : 0f);
            float jab = Mathf.Max(0f, farPunch.e) * (farPunch.kind == 0 || farPunch.kind == 4 || farPunch.kind == 5 ? 1f : 0f);
            float hook = Mathf.Max(0f, farPunch.kind == 2 ? farPunch.e : 0f) + Mathf.Max(0f, nearPunch.kind == 3 ? nearPunch.e : 0f);
            leanTarget -= 9f * straight + 4f * jab + 8f * hook;
            extraHipY -= (0.022f * straight + 0.012f * jab + 0.03f * hook) * ground;
            headTarget -= 3f * (straight + hook);
            nearPoint = Mathf.Max(nearPoint, 0.45f * straight * ground);
            nearFlat = Mathf.Min(nearFlat, 1f - 0.6f * straight * ground);
            farFoot.x += 0.05f * jab * ground;
        }

        /// <summary>Die Fäuste werden erst hier zu Zielen: die Schultern stehen jetzt fest.</summary>
        void ResolveBoxIK(Vector2 nearSh, Vector2 farSh, float torsoRot)
        {
            if (!boxRel) return;
            nearIK = nearSh + MathUtil.Rotate(nearRel, torsoRot * nearFollow);
            farIK = farSh + MathUtil.Rotate(farRel, torsoRot * farFollow);
        }

        // ------------------------------------------------------------------ the moves

        void PoseBox(float t, float hipY, float air, ref Vector2 nearFoot, ref float nearFlat, ref float nearPoint,
            ref Vector2 farFoot, ref float farFlat, ref float farPoint,
            ref float leanTarget, ref float headTarget, ref float extraHipY)
        {
            const float A = PlayerDims.AnkleHeight;
            float ground = 1f - air;
            float reach = Reach;
            switch (player.CurrentAction)
            {
                // ---- Kraftgerade: eindrehen, Schritt nach vorn, alles in eine Gerade
                case Player.Action.PowerCross:
                {
                    float tW = Player.PcWind, tC = Player.PcContact, tE = Player.PcDuration;
                    Vector2 aim = player.KickAimLocal.sqrMagnitude > 0.01f ? player.KickAimLocal.normalized : Vector2.right;
                    float coil = MathUtil.Smooth01(t / tW);
                    float strike = t < tW ? 0f : MathUtil.EaseOutCubic(Mathf.Clamp01((t - tW) / (tC - tW)));
                    float back = MathUtil.Smooth01((t - tC - 0.12f) / (tE - tC - 0.12f));
                    float w = MathUtil.Smooth01(t / 0.05f) * (1f - back);
                    Vector2 cocked = new Vector2(-0.14f, 0.01f);
                    Vector2 n = t < tW ? Vector2.Lerp(GuardNear, cocked, coil) : Vector2.Lerp(cocked, aim * reach * 0.99f, strike);
                    nearRel = Vector2.Lerp(nearRel, n, w);
                    nearFollow = Mathf.Lerp(nearFollow, t < tW ? 1f : 1f - strike, w);
                    farRel = Vector2.Lerp(farRel, Vector2.Lerp(new Vector2(0.22f, 0.04f), new Vector2(0.12f, -0.03f), strike), w);
                    leanTarget = Mathf.Lerp(leanTarget, Mathf.Lerp(7f * coil, -16f, strike), w);
                    extraHipY += (-0.07f * coil * (1f - strike) - 0.09f * strike) * w * ground;
                    headTarget = Mathf.Lerp(headTarget, -6f, w);
                    // das Führbein hebt beim Eindrehen ab und setzt im Schlag weit vorn auf, die hintere Ferse geht hoch
                    Vector2 ff = new Vector2(Mathf.Lerp(0.18f, 0.5f, strike), A + 0.07f * coil * (1f - strike));
                    farFoot = Vector2.Lerp(farFoot, ff, w * ground);
                    farFlat = Mathf.Lerp(farFlat, strike > 0.5f ? 1f : 0.5f, w);
                    nearFoot = Vector2.Lerp(nearFoot, new Vector2(Mathf.Lerp(-0.18f, -0.32f, strike), A), w * ground);
                    nearPoint = Mathf.Lerp(nearPoint, 0.55f * strike, w);
                    nearFlat = Mathf.Lerp(nearFlat, 1f - 0.7f * strike, w);
                    break;
                }

                // ---- Deckung: Fäuste dicht vor dem Gesicht, Kinn runter, Knie weich; am Ende der Konter mit der Schlaghand
                case Player.Action.Guard:
                {
                    float hold = player.GuardHold;
                    float w = MathUtil.Smooth01(t / 0.06f) * (1f - MathUtil.Smooth01((t - hold - 0.1f) / 0.22f));
                    Vector2 shake = new Vector2(guardShake * 0.06f, guardShake * 0.015f);
                    Vector2 gn = new Vector2(0.15f, 0.07f) + shake, gf = new Vector2(0.22f, 0.05f) + shake;
                    if (!player.ActionReleased) { nearRel = Vector2.Lerp(nearRel, gn, w); nearFollow = Mathf.Lerp(nearFollow, 1f, w); }
                    farRel = Vector2.Lerp(farRel, gf, w);
                    farFollow = Mathf.Lerp(farFollow, 1f, w);
                    // ein Atemzug Pulsieren, je mehr die Deckung gefangen hat
                    float charge = Mathf.Clamp01(player.GuardHits / 5f);
                    extraHipY -= (0.08f + 0.01f * Mathf.Sin(time * 14f) * charge) * w * ground;
                    leanTarget = Mathf.Lerp(leanTarget, player.ActionReleased ? -12f : -8f, w);
                    headTarget = Mathf.Lerp(headTarget, -9f, w);
                    farFoot = Vector2.Lerp(farFoot, new Vector2(0.24f, A), w * ground);
                    nearFoot = Vector2.Lerp(nearFoot, new Vector2(-0.2f, A), w * ground);
                    break;
                }

                // ---- Konterschritt: tief und schnell, Deckung oben
                case Player.Action.Slip:
                {
                    float w = MathUtil.Smooth01(t / 0.04f) * (1f - MathUtil.Smooth01((t - Player.SlipRun) / 0.14f));
                    bool backward = player.Vel.x * player.Facing < -0.5f;
                    float d = backward ? -1f : 1f;
                    nearRel = Vector2.Lerp(nearRel, new Vector2(0.16f, 0.05f), w);
                    farRel = Vector2.Lerp(farRel, new Vector2(0.23f, 0.03f), w);
                    extraHipY -= 0.12f * w * Mathf.Sin(Mathf.Clamp01(t / Player.SlipDuration) * Mathf.PI);
                    leanTarget = Mathf.Lerp(leanTarget, backward ? 12f : -16f, w);
                    headTarget = Mathf.Lerp(headTarget, backward ? -4f : -8f, w);
                    nearFoot = Vector2.Lerp(nearFoot, new Vector2(0.38f * d, A + 0.08f), w);
                    farFoot = Vector2.Lerp(farFoot, new Vector2(-0.4f * d, A + 0.12f), w);
                    nearFlat = Mathf.Lerp(nearFlat, 0.2f, w); farFlat = Mathf.Lerp(farFlat, 0f, w);
                    nearPoint = Mathf.Lerp(nearPoint, 0.5f, w); farPoint = Mathf.Lerp(farPoint, 0.9f, w);
                    break;
                }

                // ---- Uppercut: tief in die Knie, die Faust kommt von unten und reißt den Körper mit hoch
                case Player.Action.Uppercut:
                {
                    float tC = Player.UpperCrouch, tH = Player.UpperContact, tE = Player.UpperDuration;
                    float k = MathUtil.Smooth01(t / tC);
                    float r = MathUtil.EaseOutCubic(Mathf.Clamp01((t - tH * 0.55f) / 0.12f));
                    float back = MathUtil.Smooth01((t - 0.36f) / (tE - 0.36f));
                    float w = MathUtil.Smooth01(t / 0.04f) * (1f - back);
                    Vector2 low = new Vector2(0.12f, -0.36f), up = new Vector2(0.4f, 0.9f).normalized * reach * 0.96f;
                    Vector2 n = t < tH * 0.55f ? Vector2.Lerp(GuardNear, low, k) : Vector2.Lerp(low, up, r);
                    nearRel = Vector2.Lerp(nearRel, n, w);
                    nearFollow = Mathf.Lerp(nearFollow, 1f - 0.7f * r, w);
                    farRel = Vector2.Lerp(farRel, new Vector2(0.2f, 0.06f), w);
                    extraHipY -= 0.16f * k * (1f - r) * w * ground;
                    leanTarget = Mathf.Lerp(leanTarget, Mathf.Lerp(-9f * k, 9f, r), w);
                    headTarget = Mathf.Lerp(headTarget, Mathf.Lerp(-8f, 12f, r), w);
                    if (r < 0.2f)
                    {
                        farFoot = Vector2.Lerp(farFoot, new Vector2(0.26f, A), w * ground);
                        nearFoot = Vector2.Lerp(nearFoot, new Vector2(-0.22f, A), w * ground);
                    }
                    break;
                }

                // ---- Doppelhaken: breiter Stand, der Oberkörper dreht in jeden Haken hinein
                case Player.Action.Hooks:
                {
                    float w = MathUtil.Smooth01(t / 0.05f) * (1f - MathUtil.Smooth01((t - Player.HooksDuration + 0.14f) / 0.14f));
                    float b1 = MathUtil.Bump(Mathf.Clamp01((t - Player.HooksFirst + 0.08f) / 0.2f));
                    float b2 = MathUtil.Bump(Mathf.Clamp01((t - Player.HooksSecond + 0.08f) / 0.2f));
                    leanTarget = Mathf.Lerp(leanTarget, -6f - 9f * b1 - 9f * b2 + 6f * Mathf.Clamp01((t - Player.HooksFirst) / 0.1f) * (1f - b2), w);
                    extraHipY -= 0.07f * w * ground;
                    headTarget = Mathf.Lerp(headTarget, -6f, w);
                    farFoot = Vector2.Lerp(farFoot, new Vector2(0.3f, A), w * ground);
                    nearFoot = Vector2.Lerp(nearFoot, new Vector2(-0.26f, A), w * ground);
                    break;
                }

                // ---- Bodenschlag: beide Fäuste über den Kopf, dann mit dem ganzen Körper in den Boden
                case Player.Action.Pound:
                {
                    float tC = Player.PoundContact;
                    bool slammed = player.ActionReleased;
                    float raise = MathUtil.Smooth01(t / tC);
                    float slam = slammed ? MathUtil.EaseOutCubic(Mathf.Clamp01((t - tC) / 0.06f)) : 0f;
                    float back = slammed ? MathUtil.Smooth01((t - tC - 0.12f) / (Player.PoundRecover - 0.12f)) : 0f;
                    float w = MathUtil.Smooth01(t / 0.05f) * (1f - back);
                    Vector2 over = new Vector2(0.06f, 0.42f), down = new Vector2(0.5f, -0.9f);
                    Vector2 n = Vector2.Lerp(Vector2.Lerp(GuardNear, over, raise), down, slam);
                    Vector2 f = Vector2.Lerp(Vector2.Lerp(GuardFar, over + new Vector2(0.06f, -0.02f), raise), down + new Vector2(0.06f, 0f), slam);
                    nearRel = Vector2.Lerp(nearRel, n, w); farRel = Vector2.Lerp(farRel, f, w);
                    nearFollow = Mathf.Lerp(nearFollow, 0.6f, w); farFollow = Mathf.Lerp(farFollow, 0.6f, w);
                    leanTarget = Mathf.Lerp(leanTarget, Mathf.Lerp(6f * raise, -32f, slam), w);
                    headTarget = Mathf.Lerp(headTarget, Mathf.Lerp(8f * raise, -10f, slam), w);
                    extraHipY -= 0.3f * slam * w * ground;
                    if (slam > 0f)
                    {
                        farFoot = Vector2.Lerp(farFoot, new Vector2(0.3f, A), w * slam);
                        nearFoot = Vector2.Lerp(nearFoot, new Vector2(-0.3f, A), w * slam);
                    }
                    break;
                }

                // ---- Schattenboxer: kurz einatmen, die Fäuste an die Brust, dann eine Gerade in die Luft
                case Player.Action.Shadow:
                {
                    float w = MathUtil.Smooth01(t / 0.05f) * (1f - MathUtil.Smooth01((t - Player.ShadowDuration + 0.12f) / 0.12f));
                    float inhale = MathUtil.Bump(Mathf.Clamp01(t / Player.ShadowSet));
                    if (!player.ActionReleased) nearRel = Vector2.Lerp(nearRel, new Vector2(0.1f, -0.06f), w * inhale);
                    farRel = Vector2.Lerp(farRel, new Vector2(0.16f, -0.04f), w * inhale);
                    leanTarget = Mathf.Lerp(leanTarget, 5f * inhale, w);
                    headTarget = Mathf.Lerp(headTarget, 6f * inhale, w);
                    extraHipY += 0.02f * inhale * w * ground;
                    break;
                }

                // ---- Trommelfeuer: weit vorgebeugt, die Arme laufen über ihre eigenen Zeitlinien
                case Player.Action.Flurry:
                {
                    float w = MathUtil.Smooth01(t / 0.05f) * (1f - MathUtil.Smooth01((t - player.FlurryDuration + 0.12f) / 0.12f));
                    leanTarget = Mathf.Lerp(leanTarget, -11f + 2f * Mathf.Sin(time * 40f), w);
                    headTarget = Mathf.Lerp(headTarget, -6f, w);
                    extraHipY -= 0.05f * w * ground;
                    farFoot = Vector2.Lerp(farFoot, new Vector2(0.28f, A), w * ground);
                    nearFoot = Vector2.Lerp(nearFoot, new Vector2(-0.24f, A), w * ground);
                    nearPoint = Mathf.Lerp(nearPoint, 0.3f, w * ground);
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ the ultis

        void PoseBoxUlti(float t, float hipY, float air, ref Vector2 nearFoot, ref float nearFlat, ref float nearPoint,
            ref Vector2 farFoot, ref float farFlat, ref float farPoint, ref float leanTarget, ref float headTarget, ref float extraHipY)
        {
            const float A = PlayerDims.AnkleHeight;
            float ground = 1f - air;
            float reach = Reach;
            switch (player.Ulti)
            {
                // ---- KAI: tief einrollen und die Faust weit zurück, flach hinterher hechten, Gerade mit allem
                case UltiKind.Knockout:
                {
                    float g = MathUtil.Smooth01(t / Player.KoGather);
                    bool hit = player.ActionReleased;
                    float dash = Mathf.Clamp01((t - Player.KoGather) / Player.KoDash);
                    float strike = hit ? MathUtil.EaseOutCubic(Mathf.Clamp01((t - Player.KoGather - Player.KoDash) / 0.05f)) : 0f;
                    float back = hit ? MathUtil.Smooth01((t - Player.KoGather - Player.KoDash - 0.3f) / (Player.KoEnd - 0.3f)) : 0f;
                    float w = MathUtil.Smooth01(t / 0.06f) * (1f - back);
                    Vector2 cocked = new Vector2(-0.24f, 0.02f);
                    Vector2 n = hit ? Vector2.Lerp(cocked, new Vector2(1f, 0.05f).normalized * reach * 0.99f, strike) : Vector2.Lerp(GuardNear, cocked, g);
                    nearRel = Vector2.Lerp(nearRel, n, w);
                    nearFollow = Mathf.Lerp(nearFollow, hit ? 1f - strike : 1f, w);
                    farRel = Vector2.Lerp(farRel, hit ? new Vector2(0.12f, -0.03f) : new Vector2(0.3f, 0f), w);
                    float lean = hit ? -18f : t < Player.KoGather ? 10f * g : Mathf.Lerp(10f, -28f, dash);
                    leanTarget = Mathf.Lerp(leanTarget, lean, w);
                    headTarget = Mathf.Lerp(headTarget, -6f, w);
                    extraHipY += (hit ? -0.1f : t < Player.KoGather ? -0.16f * g : -0.12f) * w;
                    if (!hit && t >= Player.KoGather)
                    {
                        // im Flug: das hintere Bein stößt sich ab, das vordere zieht nach vorn
                        nearFoot = Vector2.Lerp(nearFoot, new Vector2(-0.5f, A + 0.32f), w);
                        farFoot = Vector2.Lerp(farFoot, new Vector2(0.22f, A + 0.16f), w);
                        nearFlat = farFlat = 0f; nearPoint = 0.8f; farPoint = 0.5f;
                    }
                    else
                    {
                        farFoot = Vector2.Lerp(farFoot, new Vector2(hit ? 0.45f : 0.3f, A), w);
                        nearFoot = Vector2.Lerp(nearFoot, new Vector2(hit ? -0.36f : -0.28f, A), w);
                        nearPoint = Mathf.Lerp(nearPoint, hit ? 0.55f : 0f, w);
                        nearFlat = Mathf.Lerp(nearFlat, hit ? 0.3f : 1f, w);
                        farFlat = Mathf.Lerp(farFlat, 1f, w);
                    }
                    break;
                }

                // ---- VERA: dreimal die Fäuste hoch über den Kopf und mit dem ganzen Körper in den Boden
                case UltiKind.Quake:
                {
                    float first = Player.QuakeFirst, gap = Player.QuakeGap;
                    int k = Mathf.Clamp(Mathf.FloorToInt((t - first + gap * 0.5f) / gap), 0, Player.QuakeSlams - 1);
                    float u = t - player.QuakeAt(k);
                    float raise = u < 0f ? MathUtil.Smooth01((u + (k == 0 ? first : gap * 0.5f)) / (k == 0 ? first : gap * 0.5f)) : 1f;
                    float slam = u >= 0f ? MathUtil.EaseOutCubic(Mathf.Clamp01(u / 0.06f)) : 0f;
                    float end = MathUtil.Smooth01((t - player.QuakeAt(Player.QuakeSlams - 1) - 0.15f) / 0.3f);
                    float w = MathUtil.Smooth01(t / 0.06f) * (1f - end);
                    Vector2 over = new Vector2(0.05f, 0.42f), down = new Vector2(0.5f, -0.88f);
                    Vector2 n = Vector2.Lerp(Vector2.Lerp(GuardNear, over, raise), down, slam);
                    Vector2 f = Vector2.Lerp(Vector2.Lerp(GuardFar, over + new Vector2(0.06f, -0.02f), raise), down + new Vector2(0.06f, 0f), slam);
                    // nach dem Hieb kommen die Fäuste wieder hoch (sanft, bevor der nächste ausholt)
                    if (u >= 0.08f && k < Player.QuakeSlams - 1)
                    {
                        float up = MathUtil.Smooth01((u - 0.08f) / 0.14f);
                        n = Vector2.Lerp(n, GuardNear, up); f = Vector2.Lerp(f, GuardFar, up); slam *= 1f - up;
                    }
                    nearRel = Vector2.Lerp(nearRel, n, w); farRel = Vector2.Lerp(farRel, f, w);
                    nearFollow = Mathf.Lerp(nearFollow, 0.6f, w); farFollow = Mathf.Lerp(farFollow, 0.6f, w);
                    leanTarget = Mathf.Lerp(leanTarget, Mathf.Lerp(6f * raise, -32f, slam), w);
                    headTarget = Mathf.Lerp(headTarget, Mathf.Lerp(8f * raise, -10f, slam), w);
                    extraHipY -= 0.3f * slam * w * ground;
                    farFoot = Vector2.Lerp(farFoot, new Vector2(0.3f, A), w * ground);
                    nearFoot = Vector2.Lerp(nearFoot, new Vector2(-0.3f, A), w * ground);
                    break;
                }

                // ---- LUZ: geduckt von Gegner zu Gegner, an jedem eine Eins-Zwei; am Ende die Faust in die Höhe
                case UltiKind.Butterfly:
                {
                    float crouch = MathUtil.Smooth01(t / Player.AnkleCrouch);
                    float poseAt = Player.AnkleCrouch + player.UltiSteps * Player.ButterflyHop;
                    float pose = MathUtil.Smooth01((t - poseAt) / 0.12f);
                    float end = MathUtil.Smooth01((t - poseAt - Player.ButterflyPose + 0.2f) / 0.2f);
                    float w = MathUtil.Smooth01(t / 0.04f) * (1f - end);
                    // während der Sätze: tiefe, schnelle Haltung (die Schläge laufen über die Zeitlinien)
                    extraHipY -= 0.12f * crouch * (1f - pose) * w;
                    leanTarget = Mathf.Lerp(leanTarget, Mathf.Lerp(-12f * crouch, 4f, pose), w);
                    headTarget = Mathf.Lerp(headTarget, Mathf.Lerp(-6f, 12f, pose), w);
                    if (pose > 0f)
                    {
                        nearRel = Vector2.Lerp(nearRel, new Vector2(0.06f, 0.56f), w * pose);
                        nearFollow = Mathf.Lerp(nearFollow, 0.3f, w * pose);
                        farRel = Vector2.Lerp(farRel, new Vector2(0.2f, 0.04f), w * pose);
                        farFoot = Vector2.Lerp(farFoot, new Vector2(0.18f, A), w * pose);
                        nearFoot = Vector2.Lerp(nearFoot, new Vector2(-0.14f, A), w * pose);
                    }
                    else if (t >= Player.AnkleCrouch && player.UltiSteps > 0)
                    {
                        float hop = Mathf.Repeat((t - Player.AnkleCrouch) / Player.ButterflyHop, 1f);
                        float lift = MathUtil.Bump(hop);
                        nearFoot = Vector2.Lerp(nearFoot, new Vector2(-0.3f, A + 0.18f * lift), w);
                        farFoot = Vector2.Lerp(farFoot, new Vector2(0.32f, A + 0.1f * lift), w);
                        nearFlat = Mathf.Lerp(nearFlat, 1f - lift, w); farFlat = Mathf.Lerp(farFlat, 1f - lift, w);
                    }
                    break;
                }
            }
        }
    }
}
