using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Die Moves der Boxer. Körper, Sprung und Laufen sind dieselben wie bei den anderen; einen Ball gibt es nicht.
    /// Linksklick schlägt eine Kombination (Jab mit der Führhand, Gerade mit der Schlaghand, dritter Schlag in Folge
    /// ein Haken), jeder Schlag schickt eine kurze Druckwelle aus dem Handschuh (<see cref="FistWaves"/>). Rechtsklick
    /// spielt den Klassen-Move (Kraftgerade, Deckung mit Konter, Konterschritt), dazu die fünf Boss-Fähigkeiten
    /// (Uppercut, Doppelhaken, Bodenschlag, Schattenboxer, Trommelfeuer).
    /// Die Arme selbst bewegt der Rig: Er bekommt für jeden Schlag eine neue Nummer (<see cref="PunchSerial"/>) und
    /// spielt Ausholen, Strecken und Zurückziehen pro Arm ab, auch wenn der nächste Schlag schon läuft.
    /// </summary>
    public sealed partial class Player
    {
        // ---- the combo: jab (lead hand), straight (rear hand), hook (lead hand again)
        public const float JabHit = 0.065f, StraightHit = 0.075f, HookHit = 0.11f;
        /// <summary>So lange nach dem Treffpunkt sind die Beine wieder frei (der Arm zieht im Rig weiter zurück).</summary>
        public const float PunchFree = 0.05f;
        public const float PunchCooldown = 0.32f, ComboWindow = 0.6f;
        const float JabDamage = 15f, StraightDamage = 17f, HookDamage = 24f;
        const float JoltSpeed = 24f, JoltRange = 5.2f;

        // ---- power cross (striker): coil, a step in, everything into one straight
        public const float PcWind = 0.22f, PcContact = 0.31f, PcDuration = 0.62f, PowerCrossCooldown = 3.5f;
        const float PowerCrossDamage = 46f, PowerCrossRange = 11f;

        // ---- guard (defender): gloves up, everything is caught, the counter comes at the end
        public const float GuardRaise = 0.08f, GuardTime = 1.1f, GuardRecover = 0.34f, GuardCooldown = 5f;
        const float CounterDamage = 30f, CounterPerHit = 12f, CounterRadius = 2.6f;
        const int CounterMaxHits = 5;

        // ---- slip (skiller): a quick low step, the next punch is a sure counter
        public const float SlipRun = 0.16f, SlipDuration = 0.3f, SlipCooldown = 2.2f, SlipReady = 1.6f;
        const float SlipSpeed = 21f;   // ≈ 3.4 units in SlipRun

        // ---- the boss skills
        public const float UpperCrouch = 0.1f, UpperContact = 0.16f, UpperDuration = 0.56f, UpperCooldown = 5f;
        const float UpperDamage = 40f, UpperJump = 12.5f;
        public const float HooksFirst = 0.12f, HooksSecond = 0.3f, HooksDuration = 0.52f, HooksCooldown = 6f;
        const float HooksDamage = 26f, HooksRadius = 3f;
        public const float PoundContact = 0.26f, PoundRecover = 0.32f, PoundCooldown = 8f;
        const float PoundDamage = 30f, PoundRadius = 5.2f, PoundStun = 1f;
        public const float ShadowSet = 0.2f, ShadowDuration = 0.42f, ShadowCooldown = 10f;
        const float ShadowLife = 4f, ShadowEvery = 0.45f, ShadowDamage = 12f;
        public const float FlurryStart = 0.06f, FlurryGap = 0.065f, FlurryCooldown = 7f;
        const int FlurryCount = 8;
        const float FlurryDamage = 10f;

        public float CrossCdB, GuardCd, SlipCd, UpperCd, HooksCd, PoundCd, ShadowCd, FlurryCd;
        float pcBuffer, guardBuffer, slipBuffer, upperBuffer, hooksBuffer, poundBuffer, shadowBuffer, flurryBuffer;

        /// <summary>Jeder Schlag bekommt eine neue Nummer: daran erkennt der Rig (auch beim Duo-Partner) den nächsten Schlag.</summary>
        public int PunchSerial;
        /// <summary>Welcher Schlag der Kombination: 0 Jab (Führhand), 1 Gerade (Schlaghand), 2 Haken (Führhand).</summary>
        public int PunchStep;
        /// <summary>Nach dem Konterschritt: der nächste Schlag ist ein sicherer Konter.</summary>
        public float SlipReadyLeft;
        /// <summary>So viele Treffer hat die Deckung gerade gefangen (der Rig lässt die Fäuste zittern).</summary>
        public int GuardHits;
        /// <summary>Die Deckung hält so lange (Upgrades verlängern sie).</summary>
        public float GuardHold = GuardTime;

        int comboStep;
        float sinceLastPunch = 99f;
        int slipDir = 1;
        int flurryDone, flurryTotal;
        bool poundSlammed, upperTwinPending;
        float upperTwinT, guardAbsorbCd, shadowLeft, shadowPunchT;
        int slipAirUsed;
        Vector2 slipFrom;
        readonly List<Vector2> shadowSpots = new List<Vector2>();

        /// <summary>Boxt: keine Bälle, Schläge statt Schüsse.</summary>
        public bool Boxing => Rig != null && Rig.Sport == Sport.Boxing;

        public float PowerCrossCooldownTotal => PowerCrossCooldown * S.CooldownOf(SkillCategory.Shot) * S.PcCooldownMul;
        public float GuardCooldownTotal => GuardCooldown * S.CooldownOf(SkillCategory.Header) * S.GuardCooldownMul;
        public float SlipCooldownTotal => SlipCooldown * S.CooldownOf(SkillCategory.Technique) * S.SlipCooldownMul;
        public float UpperCooldownTotal => UpperCooldown * S.CooldownOf(SkillCategory.Shot) * S.UpperCooldownMul;
        public float HooksCooldownTotal => HooksCooldown * S.CooldownOf(SkillCategory.Technique) * S.HooksCooldownMul;
        public float PoundCooldownTotal => PoundCooldown * S.CooldownOf(SkillCategory.Defense) * S.PoundCooldownMul;
        public float ShadowCooldownTotal => ShadowCooldown * S.CooldownOf(SkillCategory.Technique) * S.ShadowCooldownMul;
        public float FlurryCooldownTotal => FlurryCooldown * S.CooldownOf(SkillCategory.Shot) * S.FlurryCooldownMul;

        /// <summary>Der Konterschritt geht am Boden und einmal pro Sprung.</summary>
        public bool CanSlip => Grounded || slipAirUsed < S.AirDashes;
        public bool IsSlipping => CurrentAction == Action.Slip && ActionTime < SlipRun;
        /// <summary>Die Deckung steht: Treffer werden gefangen statt eingesteckt.</summary>
        public bool IsGuarding => CurrentAction == Action.Guard && ActionTime >= GuardRaise * 0.5f && !released;
        /// <summary>Der Schritt in die Kraftgerade.</summary>
        bool IsLunging => CurrentAction == Action.PowerCross && ActionTime >= PcWind && ActionTime < PcContact + 0.05f;

        public static bool IsBoxAction(Action a) => a >= Action.Punch && a <= Action.Flurry;

        /// <summary>Für die Fähigkeitsleiste: wie lange ein Boxer-Move noch lädt, wie lange insgesamt, ob er gerade geht.</summary>
        public float BoxCooldown(Ability a)
        {
            switch (a)
            {
                case Ability.PowerCross: return CrossCdB;
                case Ability.Guard: return GuardCd;
                case Ability.Slip: return SlipCd;
                case Ability.Uppercut: return UpperCd;
                case Ability.Hooks: return HooksCd;
                case Ability.Pound: return PoundCd;
                case Ability.Shadow: return ShadowCd;
                case Ability.Flurry: return FlurryCd;
                default: return 0f;
            }
        }

        public float BoxCooldownTotal(Ability a)
        {
            switch (a)
            {
                case Ability.PowerCross: return PowerCrossCooldownTotal;
                case Ability.Guard: return GuardCooldownTotal;
                case Ability.Slip: return SlipCooldownTotal;
                case Ability.Uppercut: return UpperCooldownTotal;
                case Ability.Hooks: return HooksCooldownTotal;
                case Ability.Pound: return PoundCooldownTotal;
                case Ability.Shadow: return ShadowCooldownTotal;
                case Ability.Flurry: return FlurryCooldownTotal;
                default: return 1f;
            }
        }

        public bool BoxAvailable(Ability a)
        {
            if (Dead) return false;
            switch (a)
            {
                case Ability.PowerCross: case Ability.Shadow: return Grounded;
                case Ability.Slip: return CanSlip;
                default: return true;
            }
        }

        /// <summary>Ein Boxer hat keinen Ball: er bleibt weggepackt. Die anderen bekommen ihn zurück, falls er noch weg ist.</summary>
        public void SyncBallToSport()
        {
            if (Ball == null) return;
            if (Boxing) { if (Ball.St != Ball.State.Away) Ball.Park(); }
            else if (Ball.St == Ball.State.Away && !UltiBallOut) { Ball.ResetTo(Pos + new Vector2(0.5f * Facing, Art.BallRadius)); Ball.SetVisible(true); }
        }

        void ResetBoxing()
        {
            CrossCdB = GuardCd = SlipCd = UpperCd = HooksCd = PoundCd = ShadowCd = FlurryCd = 0f;
            pcBuffer = guardBuffer = slipBuffer = upperBuffer = hooksBuffer = poundBuffer = shadowBuffer = flurryBuffer = 0f;
            SlipReadyLeft = 0f;
            comboStep = 0;
            sinceLastPunch = 99f;
            slipAirUsed = 0;
            shadowLeft = 0f;
            upperTwinPending = false;
            GuardHits = 0;
        }

        void TickBoxing(float dt)
        {
            CrossCdB = Mathf.Max(0f, CrossCdB - dt);
            GuardCd = Mathf.Max(0f, GuardCd - dt);
            SlipCd = Mathf.Max(0f, SlipCd - dt);
            UpperCd = Mathf.Max(0f, UpperCd - dt);
            HooksCd = Mathf.Max(0f, HooksCd - dt);
            PoundCd = Mathf.Max(0f, PoundCd - dt);
            ShadowCd = Mathf.Max(0f, ShadowCd - dt);
            FlurryCd = Mathf.Max(0f, FlurryCd - dt);
            if (DevMode.NoCooldowns) CrossCdB = GuardCd = SlipCd = UpperCd = HooksCd = PoundCd = ShadowCd = FlurryCd = 0f;
            pcBuffer = Mathf.Max(0f, pcBuffer - dt);
            guardBuffer = Mathf.Max(0f, guardBuffer - dt);
            slipBuffer = Mathf.Max(0f, slipBuffer - dt);
            upperBuffer = Mathf.Max(0f, upperBuffer - dt);
            hooksBuffer = Mathf.Max(0f, hooksBuffer - dt);
            poundBuffer = Mathf.Max(0f, poundBuffer - dt);
            shadowBuffer = Mathf.Max(0f, shadowBuffer - dt);
            flurryBuffer = Mathf.Max(0f, flurryBuffer - dt);
            guardAbsorbCd = Mathf.Max(0f, guardAbsorbCd - dt);
            if (CurrentAction != Action.Punch) sinceLastPunch += dt;
            if (Grounded && CurrentAction != Action.Slip) slipAirUsed = 0;

            if (SlipReadyLeft > 0f)
            {
                SlipReadyLeft = Mathf.Max(0f, SlipReadyLeft - dt);
                // die Fäuste glimmen, solange der Konter bereitsteht
                if (!Dead && Random.value < dt * 30f)
                    FxSystem.I.Motes(Pos + new Vector2(Facing * 0.35f, 1.35f) + Random.insideUnitCircle * 0.2f, Vector2.up * 0.4f, Palette.Slip, 1, 0.08f);
            }
            if (upperTwinPending && (upperTwinT -= dt) <= 0f) { upperTwinPending = false; UpperWave(0.5f, true); }
            if (shadowLeft > 0f) TickShadow(dt);
        }

        bool PressBoxSkill(Ability a)
        {
            switch (a)
            {
                case Ability.Uppercut: upperBuffer = 0.28f; return true;
                case Ability.Hooks: hooksBuffer = 0.25f; return true;
                case Ability.Pound: poundBuffer = 0.25f; return true;
                case Ability.Shadow: shadowBuffer = 0.25f; return true;
                case Ability.Flurry: flurryBuffer = 0.28f; return true;
                default: return false;
            }
        }

        bool PressBoxClassMove(Ability a)
        {
            switch (a)
            {
                case Ability.PowerCross: pcBuffer = 0.3f; return true;
                case Ability.Guard: guardBuffer = 0.25f; return true;
                case Ability.Slip: slipBuffer = 0.22f; return true;
                default: return false;
            }
        }

        /// <summary>Startet einen gepufferten Boxer-Move. true, wenn einer begann.</summary>
        bool StartBoxAction()
        {
            if (guardBuffer > 0f && GuardCd <= 0f) { StartGuard(); return true; }
            if (slipBuffer > 0f && SlipCd <= 0f && CanSlip) { StartSlip(); return true; }
            if (upperBuffer > 0f && UpperCd <= 0f) { StartUppercut(); return true; }
            if (pcBuffer > 0f && CrossCdB <= 0f && Grounded) { StartPowerCross(); return true; }
            if (hooksBuffer > 0f && HooksCd <= 0f) { StartHooks(); return true; }
            if (poundBuffer > 0f && PoundCd <= 0f) { StartPound(); return true; }
            if (shadowBuffer > 0f && ShadowCd <= 0f && Grounded) { StartShadow(); return true; }
            if (flurryBuffer > 0f && FlurryCd <= 0f) { StartFlurry(); return true; }
            if (shotBuffer > 0f && ShotCd <= 0f) { StartPunch(); return true; }
            return false;
        }

        /// <summary>Wie viel die Beine während eines Boxer-Moves laufen dürfen (1: ganz).</summary>
        float BoxSpeedMul()
        {
            switch (CurrentAction)
            {
                case Action.Punch: return 0.72f;
                case Action.PowerCross: return ActionTime < PcWind ? 0f : 0.2f;
                case Action.Guard: return 0.12f;
                case Action.Slip: return 0.1f;
                case Action.Uppercut: return 0.4f;
                case Action.Hooks: return 0.15f;
                case Action.Pound: return 0.2f;
                case Action.Shadow: return 0.25f;
                case Action.Flurry: return 0.15f;
                default: return 1f;
            }
        }

        /// <summary>Eigene Bahnen: der Konterschritt und der Schritt in die Kraftgerade.</summary>
        bool BoxVelocity(float rate)
        {
            if (IsSlipping)
            {
                // ein harter Abdruck, der zum Ende ausgleitet
                float k = Mathf.Clamp01(ActionTime / SlipRun);
                Vel.x = slipDir * SlipSpeed * S.SlipDistMul * rate * (1.25f - 0.6f * k);
                return true;
            }
            if (IsLunging && Grounded)
            {
                Vel.x = Facing * 11f * (1f - Mathf.Clamp01((ActionTime - PcWind) / 0.14f));
                return true;
            }
            return false;
        }

        void UpdateBoxAction(float dt)
        {
            switch (CurrentAction)
            {
                case Action.Punch: UpdatePunch(); break;
                case Action.PowerCross: UpdatePowerCross(dt); break;
                case Action.Guard: UpdateGuard(dt); break;
                case Action.Slip: UpdateSlip(dt); break;
                case Action.Uppercut: UpdateUppercut(dt); break;
                case Action.Hooks: UpdateHooks(dt); break;
                case Action.Pound: UpdatePound(dt); break;
                case Action.Shadow: UpdateShadow(); break;
                case Action.Flurry: UpdateFlurry(); break;
            }
        }

        /// <summary>Ein Treffer unterbricht den Move (Deckung fängt ihn vorher ab).</summary>
        void AbortBoxing()
        {
            if (IsBoxAction(CurrentAction)) { CurrentAction = Action.None; Game.I.Cam.SetZoom(1f); }
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Schulter der Schlaghand (Welt), aufrecht gemessen.</summary>
        Vector2 ShoulderWorld => ToWorld(new Vector2(Rig.Body.Shoulder.x, Rig.Body.StandHip + Rig.Body.Shoulder.y));

        float ArmReach => Rig.Body.UpperArmLen + Rig.Body.ForearmLen;

        Vector2 AimFromShoulder()
        {
            Vector2 d = GameInput.AimWorld - ShoulderWorld;
            if (d.sqrMagnitude < 0.04f) d = new Vector2(Facing, 0f);
            return d.normalized;
        }

        /// <summary>Wo der Handschuh beim Treffer steckt (Welt), für eine Richtung aus der Schulter.</summary>
        Vector2 FistAt(Vector2 dir, float stretch = 0.95f) => ShoulderWorld + dir * (ArmReach * stretch + Rig.Body.HandLen * 0.6f);

        /// <summary>Alles im Umkreis bekommt einen Treffer, weg von der Mitte.</summary>
        void RingHit(Vector2 at, float radius, float dmg, float knock, Src src, float stun = 0f)
        {
            var list = Game.I.Waves.Monsters;
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                if (!m.Alive) continue;
                Vector2 d = m.Center - at;
                if (d.magnitude > radius + m.Radius) continue;
                Combat.Hit(m, dmg, d.normalized + Vector2.up * 0.4f, knock, src, big: true);
                if (stun > 0f && m.Alive) m.Stun(stun);
            }
        }

        // ------------------------------------------------------------------ punches

        void StartPunch()
        {
            shotBuffer = 0f;
            // die Kombination läuft weiter, solange die Schläge dicht aufeinander folgen
            comboStep = sinceLastPunch <= ComboWindow ? (comboStep + 1) % 3 : 0;
            PunchStep = comboStep;
            PunchSerial++;
            Vector2 dir = AimFromShoulder();
            if (dir.x * Facing < -0.05f) Facing = dir.x > 0f ? 1 : -1;
            KickAimLocal = new Vector2(dir.x * Facing, dir.y);
            CurrentAction = Action.Punch;
            ActionTime = 0f;
            released = false;
            sinceLastPunch = 0f;
            ShotCd = ShotCooldownTotal;
            Game.I.Hud.OnShotUsed();
        }

        float PunchHitTime => PunchStep == 2 ? HookHit : PunchStep == 1 ? StraightHit : JabHit;

        void UpdatePunch()
        {
            if (!released && ActionTime >= PunchHitTime) { released = true; ReleasePunch(); }
            if (ActionTime >= PunchHitTime + PunchFree) CurrentAction = Action.None;
        }

        void ReleasePunch()
        {
            var s = S;
            var fx = FxSystem.I;
            // die Richtung gilt erst jetzt (das Fadenkreuz kann sich in den paar Hundertsteln bewegt haben)
            Vector2 dir = AimFromShoulder();
            if (dir.x * Facing < 0f) dir.x = 0f;
            if (Grounded && OnPlatform == Level.None && dir.y < -0.3f) dir = new Vector2(dir.x, -0.3f);
            dir.Normalize();
            KickAimLocal = new Vector2(dir.x * Facing, dir.y);
            bool hook = PunchStep == 2;
            Vector2 from = FistAt(dir, hook ? 0.8f : 0.95f);

            float dmg = PunchStep == 2 ? HookDamage * (1f + s.ComboBonus) : PunchStep == 1 ? StraightDamage : JabDamage;
            bool runUp = s.RunUpBonus > 0f && Grounded && Mathf.Abs(Vel.x) > MaxSpeedNow * 0.6f;
            if (runUp) dmg *= 1f + s.RunUpBonus;
            bool counter = SlipReadyLeft > 0f;
            if (counter) { dmg *= 1f + s.SlipCritBonus; SlipReadyLeft = 0f; }
            shotCount++;
            bool golden = s.GoldenBoot && shotCount % 3 == 0;
            Color c = counter ? Palette.Slip : golden ? Palette.Gold : hook ? Palette.Cross : Palette.Punch;
            float range = JoltRange * s.JoltRangeMul * (hook ? 0.85f : 1f);
            FistWaves.I.Fire(from, dir, JoltSpeed, range, dmg, hook ? 0.62f : 0.45f, hook ? 9f : PunchStep == 1 ? 5f : 4f, Src.Jolt, c,
                s.JoltPierce + (counter ? 3 : 0), counter || golden, golden, s.Ricochets, hook ? 1.3f : 1f, hook || counter);
            // Fächer: weitere Wellen links und rechts der Schlagrichtung (schwächer)
            for (int i = 0; i < s.EchoBalls; i++)
            {
                float ang = s.EchoSpread * ((i >> 1) + 1) * (i % 2 == 0 ? 1f : -1f);
                FistWaves.I.Fire(from, MathUtil.Rotate(dir, ang), JoltSpeed * 0.95f, range * 0.9f, dmg, 0.4f, 3f, Src.Echo, c,
                    s.JoltPierce, false, false, s.EchoRicochet ? s.Ricochets : 0, 0.8f);
            }
            if (hook && s.HookRing)
            {
                Vector2 at = Pos + new Vector2(0f, 1f);
                float r = 2.4f * s.AreaMul;
                RingHit(at, r, dmg * 0.6f, 8f, Src.Hook);
                fx.Ring(FxLayer.Front, at, 0.3f, r, 0.22f, 0.01f, 0.3f, Color.white.WithAlpha(0.8f), Palette.Cross.WithAlpha(0f), 2.2f);
            }
            if (counter)
            {
                fx.Flash(from, 1.6f, Palette.Slip, 0.12f, 2.4f);
                fx.Sparkles(from, 0.3f, 6, Palette.Slip, 2.6f, 0.45f);
                Game.I.Hud.Popup(from + new Vector2(0f, 0.6f), "KONTER", Palette.Slip, 26f, false);
            }
            if (golden) { fx.Flash(from, 1.8f, Palette.Gold, 0.16f, 3.2f); fx.Sparkles(from, 0.3f, 8, Palette.Gold, 3f, 0.5f); }
            if (runUp) fx.Sparks(from, -dir, 40f, 5, 4f, 8f, Palette.Gold, 2.2f, 0.035f, 0.18f);
            // in der Luft stößt ein Schlag nach unten den Körper hoch (wie der Luft-Rückstoß der Schützen)
            if (!Grounded && airBoosts > 0 && dir.y < -0.35f) AirBoost(-dir * AirKickBoost);
            else Vel.x -= dir.x * (hook ? 1.2f : 0.6f);
            Game.I.Cam.Kick(dir * (hook ? 0.07f : 0.035f));
            Rig.OnPunch(PunchStep);
        }

        // ------------------------------------------------------------------ power cross

        void StartPowerCross()
        {
            pcBuffer = 0f;
            Vector2 dir = AimFromShoulder();
            if (Mathf.Abs(dir.x) > 0.05f) Facing = dir.x > 0f ? 1 : -1;
            KickAimLocal = new Vector2(dir.x * Facing, Mathf.Clamp(dir.y, -0.5f, 0.8f));
            CurrentAction = Action.PowerCross;
            ActionTime = 0f;
            released = false;
            CrossCdB = PowerCrossCooldownTotal;
            PunchStep = 1;
            Game.I.Hud.OnSkillUsed(Ability.PowerCross);
            Game.I.Cam.SetZoom(0.95f);
        }

        void UpdatePowerCross(float dt)
        {
            if (!released)
            {
                // die Kraft sammelt sich in der Schlaghand
                chargeFxTimer -= dt;
                if (chargeFxTimer <= 0f)
                {
                    chargeFxTimer = 0.03f;
                    Vector2 fist = ToWorld(new Vector2(Rig.Body.Shoulder.x - 0.2f, Rig.Body.StandHip + Rig.Body.Shoulder.y + 0.05f));
                    Vector2 off = Random.insideUnitCircle.normalized * Random.Range(0.6f, 1.1f);
                    FxSystem.I.Streak(FxLayer.Front, fist + off, -off * Random.Range(5f, 8f), 0.12f, 0.028f, 0.05f,
                        Palette.PunchCore.WithAlpha(0.9f), Palette.Cross.WithAlpha(0f), 2.2f, 2f);
                }
                Vector2 dir = AimFromShoulder();
                if (dir.x * Facing > 0f) KickAimLocal = new Vector2(dir.x * Facing, Mathf.Clamp(dir.y, -0.5f, 0.8f)).normalized;
                if (ActionTime >= PcContact) { released = true; ReleasePowerCross(); }
            }
            if (ActionTime >= PcDuration) CurrentAction = Action.None;
        }

        void ReleasePowerCross()
        {
            var s = S;
            var fx = FxSystem.I;
            Vector2 dir = new Vector2(KickAimLocal.x * Facing, KickAimLocal.y).normalized;
            Vector2 from = FistAt(dir);
            Color c = Palette.Cross;
            FistWaves.I.Fire(from, dir, 30f, PowerCrossRange, PowerCrossDamage, 0.78f, 14f, Src.Cross, c, 99, false, false, 0, 1.45f, true);
            if (s.PcTriple)
                for (int i = -1; i <= 1; i += 2)
                    FistWaves.I.Fire(from, MathUtil.Rotate(dir, 11f * i), 28f, PowerCrossRange * 0.85f, PowerCrossDamage * 0.6f, 0.6f, 10f, Src.Cross, c, 99, false, false, 0, 1.35f, true);
            fx.Flash(from, 1.3f, c, 0.14f, 1.8f);
            fx.Flash(from, 0.55f, Palette.PunchCore, 0.06f, 2f);
            fx.Ring(FxLayer.Front, from, 0.15f, 1.4f, 0.2f, 0.01f, 0.22f, Color.white.WithAlpha(0.6f), c.WithAlpha(0f), 2f);
            fx.Sparks(from, dir, 30f, 18, 10f, 22f, Palette.Upper, 2.6f, 0.05f, 0.26f);
            for (int i = 0; i < 8; i++)
            {
                Vector2 side = new Vector2(-dir.y, dir.x) * Random.Range(-0.6f, 0.6f);
                fx.Streak(FxLayer.Front, from + side, dir * Random.Range(16f, 26f), Random.Range(0.14f, 0.24f), 0.045f, 0.06f,
                    Color.white.WithAlpha(0.85f), c.WithAlpha(0f), 2.4f, 4f);
            }
            if (Grounded) fx.Dust(Pos, new Vector2(-dir.x, 0.2f), 8, 2.8f, 0.45f, 0.36f);
            var game = Game.I;
            game.Cam.Kick(dir * 0.3f);
            game.Cam.AddTrauma(0.4f);
            game.Cam.SetZoom(1f);
            game.Cam.ZoomPunch(0.04f);
            game.Post.Impact(0.5f);
            TimeFx.HitStop(0.07f, 0.03f);
            Rig.OnPowerContact();
            Combat.Maestro(this);
        }

        // ------------------------------------------------------------------ guard and counter

        void StartGuard()
        {
            guardBuffer = 0f;
            CurrentAction = Action.Guard;
            ActionTime = 0f;
            released = false;
            GuardHits = 0;
            GuardHold = GuardTime + S.GuardTimeBonus;
            GuardCd = GuardCooldownTotal;
            float dx = GameInput.AimWorld.x - Pos.x;
            if (Mathf.Abs(dx) > 0.3f) Facing = dx > 0f ? 1 : -1;
            KickAimLocal = Vector2.right;
            Game.I.Hud.OnSkillUsed(Ability.Guard);
            var fx = FxSystem.I;
            Vector2 at = Pos + new Vector2(Facing * 0.3f, 1.3f);
            fx.Ring(FxLayer.Front, at, 0.2f, 1.2f, 0.12f, 0.01f, 0.22f, Color.white.WithAlpha(0.7f), Palette.Counter.WithAlpha(0f), 2f);
            Combat.Maestro(this);
        }

        /// <summary>Ein Treffer landet auf der Deckung: gefangen, gezählt, und mit Spiegeldeckung kommt er zurück.</summary>
        bool GuardAbsorb(float amount, Vector2 from)
        {
            if (!IsGuarding) return false;
            var s = S;
            var fx = FxSystem.I;
            float dir = Mathf.Sign(from.x - Pos.x);
            if (dir == 0f) dir = Facing;
            Vector2 at = Pos + new Vector2(dir * 0.4f, 1.3f);
            // ein Monster, das an der Deckung klebt, zählt nur alle paar Hundertstel
            if (guardAbsorbCd <= 0f)
            {
                guardAbsorbCd = 0.18f;
                GuardHits = Mathf.Min(CounterMaxHits, GuardHits + 1);
                if (s.GuardHeal > 0f) Heal(s.GuardHeal, true);
                if (s.GuardReflect)
                {
                    var m = Combat.NearestTo(at, 12f);
                    Vector2 back = m != null ? (m.Center - at).normalized : new Vector2(dir, 0.1f).normalized;
                    FistWaves.I.Fire(at, back, 26f, 12f, Mathf.Max(18f, amount * 1.5f), 0.55f, 8f, Src.Counter, Palette.Counter, 1, false, false, 0, 1.2f, true);
                }
                fx.Ring(FxLayer.Front, at, 0.15f, 1.3f + 0.15f * GuardHits, 0.2f, 0.01f, 0.22f, Color.white, Palette.Counter.WithAlpha(0f), 2.4f);
                fx.Sparks(at, new Vector2(dir, 0.3f), 110f, 10, 4f, 9f, Palette.Counter, 2.4f, 0.04f, 0.22f);
                fx.Flash(at, 1.4f, Palette.Counter, 0.1f, 2.2f);
                Game.I.Cam.AddTrauma(0.12f);
                TimeFx.HitStop(0.04f, 0.05f);
                Rig.OnGuardHit();
            }
            // der Treffer schiebt ein wenig nach hinten, mehr nicht
            Vel.x = -dir * 2.2f;
            return true;
        }

        void UpdateGuard(float dt)
        {
            // die Deckung schimmert, je mehr sie gefangen hat
            if (!released)
            {
                ghostTimer -= dt;
                if (ghostTimer <= 0f)
                {
                    ghostTimer = 0.07f;
                    Vector2 at = Pos + new Vector2(Facing * 0.35f, 1.3f);
                    FxSystem.I.Ring(FxLayer.Front, at, 0.9f, 1.05f + 0.05f * GuardHits, 0.04f, 0.01f, 0.16f,
                        Color.white.WithAlpha(0.3f + 0.08f * GuardHits), Palette.Counter.WithAlpha(0f), 1.8f);
                }
                if (ActionTime >= GuardHold) { released = true; Counter(); }
            }
            if (ActionTime >= GuardHold + GuardRecover) CurrentAction = Action.None;
        }

        void Counter()
        {
            var s = S;
            var fx = FxSystem.I;
            float dmg = CounterDamage + CounterPerHit * GuardHits;
            Vector2 dir = new Vector2(Facing, 0.08f).normalized;
            Vector2 from = FistAt(dir);
            // vor der Faust: alles in Reichweite fliegt, dazu eine Welle, die durchschlägt
            float r = CounterRadius * s.AreaMul * (1f + 0.08f * GuardHits);
            RingHit(Pos + new Vector2(Facing * 1.1f, 1f), r * 0.75f, dmg, 12f + GuardHits, Src.Counter, 0.5f);
            FistWaves.I.Fire(from, dir, 26f, 6f + GuardHits, dmg * 0.7f, 0.7f + 0.05f * GuardHits, 12f, Src.Counter, Palette.Counter, 99, false, false, 0, 1.15f + 0.06f * GuardHits, true);
            fx.Flash(from, 1.2f + 0.15f * GuardHits, Palette.Counter, 0.14f, 1.8f);
            fx.Ring(FxLayer.Front, from, 0.2f, r, 0.22f, 0.01f, 0.28f, Color.white.WithAlpha(0.65f), Palette.Counter.WithAlpha(0f), 2f);
            fx.Sparks(from, dir, 60f, 12 + 3 * GuardHits, 6f, 16f, Palette.Counter, 2.6f, 0.05f, 0.26f);
            if (GuardHits > 0) Game.I.Hud.Popup(from + new Vector2(0f, 0.8f), "KONTER ×" + GuardHits, Palette.Counter, 28f + 3f * GuardHits, GuardHits >= 3);
            var game = Game.I;
            game.Cam.Kick(dir * (0.12f + 0.04f * GuardHits));
            game.Cam.AddTrauma(0.2f + 0.06f * GuardHits);
            TimeFx.HitStop(0.05f + 0.01f * GuardHits, 0.04f);
            Rig.OnPowerContact();
            PunchStep = 1;
            PunchSerial++;
        }

        // ------------------------------------------------------------------ slip

        void StartSlip()
        {
            slipBuffer = 0f;
            slipDir = Mathf.Abs(GameInput.MoveX) > 0.01f ? (GameInput.MoveX > 0f ? 1 : -1) : Facing;
            if (!Grounded) slipAirUsed++;
            CurrentAction = Action.Slip;
            ActionTime = 0f;
            SlipCd = SlipCooldownTotal;
            DodgeTime = Mathf.Max(DodgeTime, SlipRun + 0.1f);
            slipFrom = Pos;
            ghostTimer = 0f;
            Game.I.Hud.OnSkillUsed(Ability.Slip);
            OnTrick();
            var fx = FxSystem.I;
            Color c = Palette.Slip;
            if (Grounded) fx.Dust(Pos, new Vector2(-slipDir, 0.25f), 6, 2.4f, 0.38f, 0.32f);
            fx.Ring(FxLayer.Front, Pos + new Vector2(-slipDir * 0.2f, 0.85f), 0.2f, 1f, 0.1f, 0.01f, 0.2f, Color.white, c.WithAlpha(0f), 2f);
            for (int i = 0; i < 5; i++)
                fx.Streak(FxLayer.Front, Pos + new Vector2(0f, Random.Range(0.2f, 1.7f)), new Vector2(-slipDir * Random.Range(8f, 14f), 0f),
                    Random.Range(0.14f, 0.22f), 0.03f, 0.06f, Color.white.WithAlpha(0.8f), c.WithAlpha(0f), 2f, 5f);
            Game.I.Cam.Kick(new Vector2(slipDir * 0.1f, 0f));
            Rig.OnDash();
            Combat.Maestro(this);
        }

        void UpdateSlip(float dt)
        {
            if (ActionTime < SlipRun)
            {
                ghostTimer -= dt;
                if (ghostTimer <= 0f) { ghostTimer = 0.024f; ghosts.Spawn(Palette.Slip, 0.24f, 0.32f); }
            }
            else if (Mathf.Abs(Vel.x) > MaxSpeedNow) Vel.x = slipDir * MaxSpeedNow;
            if (ActionTime >= SlipDuration)
            {
                CurrentAction = Action.None;
                SlipReadyLeft = SlipReady;
                if (S.SlipShock)
                {
                    Vector2 at = slipFrom + new Vector2(0f, 0.9f);
                    float r = 2.4f * S.AreaMul;
                    RingHit(at, r, 30f, 9f, Src.Hook, 0.4f);
                    FxSystem.I.Ring(FxLayer.Front, at, 0.2f, r, 0.24f, 0.01f, 0.3f, Color.white, Palette.Slip.WithAlpha(0f), 2.4f);
                    FxSystem.I.Flash(at, 2f, Palette.Slip, 0.14f, 2.2f);
                }
            }
        }

        // ------------------------------------------------------------------ uppercut

        void StartUppercut()
        {
            upperBuffer = 0f;
            float dx = GameInput.AimWorld.x - Pos.x;
            if (Mathf.Abs(dx) > 0.3f) Facing = dx > 0f ? 1 : -1;
            CurrentAction = Action.Uppercut;
            ActionTime = 0f;
            released = false;
            UpperCd = UpperCooldownTotal;
            PunchStep = 1;
            Game.I.Hud.OnSkillUsed(Ability.Uppercut);
            Combat.Maestro(this);
        }

        void UpdateUppercut(float dt)
        {
            if (!released && ActionTime >= UpperContact)
            {
                released = true;
                var s = S;
                if (Grounded)
                {
                    Vel.y = UpperJump * s.JumpMul;
                    Grounded = false;
                    OnPlatform = Level.None;
                    boostRise = true;
                    Rig.OnJump();
                    FxSystem.I.Dust(Pos, Vector2.right, 5, 2.2f, 0.4f, 0.32f);
                    FxSystem.I.Dust(Pos, Vector2.left, 5, 2.2f, 0.4f, 0.32f);
                }
                else Vel.y = Mathf.Max(Vel.y, 9f);
                // vor dem Körper und darüber fliegt alles in die Luft
                var list = Game.I.Waves.Monsters;
                for (int i = 0; i < list.Count; i++)
                {
                    var m = list[i];
                    if (!m.Alive) continue;
                    Vector2 d = m.Center - Pos;
                    float fwd = d.x * Facing;
                    if (fwd < -0.5f - m.Radius || fwd > 2.4f + m.Radius || d.y < -0.4f - m.Radius || d.y > 3.4f + m.Radius) continue;
                    Combat.Hit(m, UpperDamage, new Vector2(Facing * 0.2f, 1f), 16f, Src.Uppercut, big: true);
                    if (m.Alive && s.UpperStun > 0f) m.Stun(s.UpperStun);
                }
                UpperWave(1f, false);
                if (s.UpperTwin) { upperTwinPending = true; upperTwinT = 0.12f; }
                var game = Game.I;
                game.Cam.Kick(new Vector2(0f, 0.2f));
                game.Cam.AddTrauma(0.28f);
                TimeFx.HitStop(0.05f, 0.04f);
                Rig.OnPowerContact();
            }
            if (ActionTime >= UpperDuration) CurrentAction = Action.None;
        }

        /// <summary>Die Druckwelle des Uppercuts steigt steil auf (trifft auch Flieger über dem Kopf).</summary>
        void UpperWave(float power, bool twin)
        {
            var fx = FxSystem.I;
            Vector2 dir = new Vector2(Facing * (twin ? 0.42f : 0.22f), 1f).normalized;
            Vector2 from = Pos + new Vector2(Facing * 0.45f, Rig.Body.StandHip + Rig.Body.Shoulder.y + 0.4f);
            FistWaves.I.Fire(from, dir, 26f, 7.5f, 26f * power, 0.7f, 10f, Src.Uppercut, Palette.Upper, 3, false, false, 0, 1.1f, true);
            fx.Flash(from, 1.2f, Palette.Upper, 0.12f, 1.8f);
            fx.Ring(FxLayer.Front, from, 0.15f, 1.1f, 0.16f, 0.01f, 0.2f, Color.white.WithAlpha(0.6f), Palette.Upper.WithAlpha(0f), 2f);
            fx.Sparks(from, dir, 40f, 12, 7f, 16f, Palette.Upper, 2.4f, 0.05f, 0.24f);
            if (twin) PunchSerial++;
        }

        // ------------------------------------------------------------------ double hook

        void StartHooks()
        {
            hooksBuffer = 0f;
            CurrentAction = Action.Hooks;
            ActionTime = 0f;
            released = false;
            ultiStep = 0;
            HooksCd = HooksCooldownTotal;
            Game.I.Hud.OnSkillUsed(Ability.Hooks);
            OnTrick();
            if (S.HooksPull) Vortices.I.Spawn(Pos + new Vector2(0f, 1f), 5f * S.AreaMul, 0.4f, 0f, 0f, false, Palette.Trick);
            Combat.Maestro(this);
        }

        void UpdateHooks(float dt)
        {
            if (ultiStep == 0 && ActionTime >= HooksFirst) { ultiStep = 1; HookRingNow(Facing, 1f); }
            if (ultiStep == 1 && ActionTime >= HooksSecond) { ultiStep = 2; HookRingNow(-Facing, 1f); released = true; }
            if (ultiStep == 2 && S.HooksDouble && ActionTime >= HooksSecond + 0.12f) { ultiStep = 3; HookRingNow(Facing, 1.5f); }
            if (ActionTime >= HooksDuration) CurrentAction = Action.None;
        }

        void HookRingNow(int side, float scale)
        {
            var s = S;
            var fx = FxSystem.I;
            Vector2 at = Pos + new Vector2(0f, 1f);
            float r = HooksRadius * s.HooksRadiusMul * s.AreaMul * scale;
            RingHit(at, r, HooksDamage * (scale > 1f ? 0.6f : 1f), 10f, Src.Hook);
            // die Welle läuft vor allem zur Seite der Faust
            FistWaves.I.Fire(at + new Vector2(side * 0.4f, 0.1f), new Vector2(side, 0.05f), 22f, r + 1.2f, HooksDamage * 0.5f, 0.6f, 6f, Src.Hook, Palette.Trick, 2, false, false, 0, 1.05f * scale, false);
            fx.Ring(FxLayer.Front, at, 0.3f, r, 0.2f, 0.01f, 0.3f, Color.white.WithAlpha(0.6f), Palette.Trick.WithAlpha(0f), 2f);
            fx.Ring(FxLayer.Back, at, 0.2f, r * 0.85f, 0.5f, 0.05f, 0.34f, Palette.Trick.WithAlpha(0.25f), Palette.Trick.WithAlpha(0f), 1.4f, false, false);
            for (int i = 0; i < 10; i++)
            {
                float ang = (side > 0 ? 0f : 180f) + Random.Range(-70f, 70f);
                fx.Streak(FxLayer.Front, at + MathUtil.Dir(ang) * 0.5f, MathUtil.Dir(ang) * Random.Range(7f, 12f), Random.Range(0.18f, 0.28f), 0.04f, 0.05f,
                    Color.white.WithAlpha(0.85f), Palette.Trick.WithAlpha(0f), 2.2f, 4f);
            }
            Game.I.Cam.Kick(new Vector2(side * 0.1f, 0f));
            Game.I.Cam.AddTrauma(0.16f);
            TimeFx.HitStop(0.03f, 0.05f);
            PunchStep = side == Facing ? 2 : 1;
            PunchSerial++;
            Rig.OnPunch(2);
        }

        // ------------------------------------------------------------------ ground pound

        void StartPound()
        {
            poundBuffer = 0f;
            CurrentAction = Action.Pound;
            ActionTime = 0f;
            released = false;
            poundSlammed = false;
            ultiWait = 0f;
            PoundCd = PoundCooldownTotal;
            if (Grounded)
            {
                Vel.y = 7f;
                Grounded = false;
                OnPlatform = Level.None;
                Rig.OnJump();
            }
            Game.I.Hud.OnSkillUsed(Ability.Pound);
            Combat.Maestro(this);
        }

        void UpdatePound(float dt)
        {
            if (!poundSlammed && ActionTime >= PoundContact)
            {
                // in der Luft: die Pose halten und mit Wucht herunter
                if (Grounded || ultiWait > 1f) { poundSlammed = true; released = true; PoundSlam(); }
                else
                {
                    ultiWait += dt;
                    ActionTime = PoundContact;
                    Vel.y = Mathf.Min(Vel.y, -20f);
                }
            }
            if (poundSlammed && ActionTime >= PoundContact + PoundRecover) CurrentAction = Action.None;
        }

        void PoundSlam()
        {
            var s = S;
            Vector2 at = new Vector2(Pos.x + Facing * 0.4f, Pos.y);
            float r = PoundRadius * s.PoundRadiusMul * s.AreaMul;
            Court.I.Shockwave(at + new Vector2(0f, 0.15f), r, PoundDamage, 10f, PoundStun + s.PoundStunBonus, 0f, true, Src.Pound);
            if (s.PoundFire)
                foreach (var m in Game.I.Waves.Monsters)
                    if (m.Alive && Mathf.Abs(m.Center.x - at.x) < r + m.Radius && Mathf.Abs(m.Pos.y - at.y) < 1.2f) m.Ignite(10f, 3f);
            CoopFx.Send(CoopFx.Kind.Slam, at, r, Palette.Slam);
            var fx = FxSystem.I;
            fx.Flash(at + new Vector2(0f, 0.3f), 1.6f, Palette.Slam, 0.14f, 1.8f);
            fx.Dust(at, Vector2.right, 9, 4.2f, 0.6f, 0.42f);
            fx.Dust(at, Vector2.left, 9, 4.2f, 0.6f, 0.42f);
            fx.Sparks(at, Vector2.up, 120f, 14, 5f, 12f, s.PoundFire ? Palette.BlastOrange : Palette.Slam, 2.4f, 0.05f, 0.3f, 10f);
            var game = Game.I;
            game.Cam.AddTrauma(0.45f);
            game.Cam.Kick(new Vector2(0f, -0.35f));
            game.Post.Impact(0.45f);
            TimeFx.HitStop(0.07f, 0.04f);
            Rig.OnLand(20f);
        }

        // ------------------------------------------------------------------ shadow boxer

        void StartShadow()
        {
            shadowBuffer = 0f;
            CurrentAction = Action.Shadow;
            ActionTime = 0f;
            released = false;
            ShadowCd = ShadowCooldownTotal;
            Game.I.Hud.OnSkillUsed(Ability.Shadow);
            OnTrick();
            Combat.Maestro(this);
        }

        void UpdateShadow()
        {
            if (!released && ActionTime >= ShadowSet)
            {
                released = true;
                float life = ShadowLife + S.ShadowLifeBonus;
                Decoys.I.Spawn(this, life, S.ShadowCount);
                shadowLeft = life;
                shadowPunchT = 0.3f;
                Rig.OnPunch(1);
                PunchStep = 1;
                PunchSerial++;
            }
            if (ActionTime >= ShadowDuration) CurrentAction = Action.None;
        }

        void TickShadow(float dt)
        {
            shadowLeft -= dt;
            shadowPunchT -= dt;
            if (shadowLeft <= 0f)
            {
                if (S.ShadowBurst)
                {
                    shadowSpots.Clear();
                    Decoys.I.OwnSpots(shadowSpots);
                    foreach (var p in shadowSpots) Combat.Explosion(p + new Vector2(0f, 0.9f), 2.8f * S.AreaMul, 50f, Palette.Shadow, Src.Hook);
                }
                return;
            }
            if (shadowPunchT > 0f || Dead) return;
            shadowPunchT = ShadowEvery;
            shadowSpots.Clear();
            Decoys.I.OwnSpots(shadowSpots);
            foreach (var p in shadowSpots)
            {
                Vector2 c = p + new Vector2(0f, 1.25f);
                var m = Combat.NearestTo(c, 4f);
                if (m == null) continue;
                Vector2 dir = (m.Center - c).normalized;
                FistWaves.I.Fire(c + dir * 0.45f, dir, 22f, 4.2f, ShadowDamage, 0.45f, 4f, Src.Hook, Palette.Shadow);
            }
        }

        // ------------------------------------------------------------------ flurry

        void StartFlurry()
        {
            flurryBuffer = 0f;
            Vector2 dir = AimFromShoulder();
            if (Mathf.Abs(dir.x) > 0.05f) Facing = dir.x > 0f ? 1 : -1;
            KickAimLocal = new Vector2(dir.x * Facing, dir.y);
            CurrentAction = Action.Flurry;
            ActionTime = 0f;
            released = false;
            flurryDone = 0;
            flurryTotal = FlurryCount + S.FlurryExtra;
            FlurryCd = FlurryCooldownTotal;
            Game.I.Hud.OnSkillUsed(Ability.Flurry);
            Combat.Maestro(this);
        }

        public float FlurryDuration => FlurryStart + flurryTotal * FlurryGap + 0.2f;

        void UpdateFlurry()
        {
            while (flurryDone < flurryTotal && ActionTime >= FlurryStart + flurryDone * FlurryGap) FlurryPunch(flurryDone++);
            if (flurryDone >= flurryTotal) released = true;
            if (ActionTime >= FlurryDuration) CurrentAction = Action.None;
        }

        void FlurryPunch(int i)
        {
            var s = S;
            Vector2 dir = AimFromShoulder();
            if (dir.x * Facing < 0.1f) dir = new Vector2(Facing * 0.1f, dir.y).normalized;
            KickAimLocal = new Vector2(dir.x * Facing, dir.y);
            Vector2 shot = MathUtil.Rotate(dir, Random.Range(-7f, 7f));
            PunchStep = i % 2;
            PunchSerial++;
            Color c = s.FlurryBoom ? Palette.BlastOrange : Palette.Punch;
            FistWaves.I.Fire(FistAt(dir), shot, 26f, 6f * s.JoltRangeMul, FlurryDamage, 0.4f, 3f, Src.Punch, c, s.JoltPierce, false, s.FlurryBoom, 0, 0.85f);
            Game.I.Cam.Kick(dir * 0.025f);
            if (i % 2 == 1) Game.I.Cam.AddTrauma(0.04f);
            Rig.OnPunch(PunchStep);
        }
    }
}
