using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Die Ultis der Boxer. KAI: der K.O.-Schlag (Kraft sammeln, zum stärksten Gegner hechten, ein Schlag, der die
    /// Arena erschüttert). VERA: das Dreifach-Beben (drei Hiebe in den Boden, jede Welle größer). LUZ: der
    /// Schmetterlingstanz (von Gegner zu Gegner, an jedem eine Eins-Zwei, am Ende fallen alle). Wie die anderen
    /// Ultis hängen die Posen nur an ActionTime und den mitgeschickten Werten.
    /// </summary>
    public sealed partial class Player
    {
        // KAI: sammeln, hechten, Schlag, ausschwingen
        public const float KoGather = 0.46f, KoDash = 0.2f, KoEnd = 0.6f, KoRange = 12f;
        // VERA: drei Hiebe in den Boden
        public const float QuakeFirst = 0.32f, QuakeGap = 0.42f, QuakeEnd = 1.62f;
        public const int QuakeSlams = 3;
        // LUZ: ducken, ein Satz pro Gegner (mit Eins-Zwei), Schlusspose
        public const float ButterflyHop = 0.17f, ButterflyPose = 0.62f;

        public float QuakeAt(int k) => QuakeFirst + k * QuakeGap;
        int quakeHops;

        // ------------------------------------------------------------------ KAI: K.O.-Schlag

        void StartKnockout()
        {
            // das Ziel: der stärkste Gegner in Reichweite, bevorzugt auf der Seite des Fadenkreuzes
            ultiTargets.Clear();
            Vector2 c = Pos + new Vector2(0f, 1f);
            int aimSide = GameInput.AimWorld.x >= Pos.x ? 1 : -1;
            Monster best = null;
            float bestScore = float.MinValue;
            foreach (var m in Game.I.Waves.Monsters)
            {
                if (!m.Alive) continue;
                Vector2 d = m.Center - c;
                if (d.sqrMagnitude > KoRange * KoRange) continue;
                float score = m.MaxHp * (m.Rank == Rank.Boss ? 4f : m.Rank == Rank.MiniBoss ? 2.5f : m.Rank == Rank.Elite ? 1.6f : 1f)
                              * (Mathf.Sign(d.x) == aimSide ? 1.6f : 1f) - d.magnitude * 3f;
                if (score > bestScore) { bestScore = score; best = m; }
            }
            if (best != null)
            {
                ultiTargets.Add(best);
                int side = best.Center.x >= Pos.x ? 1 : -1;
                float x = Mathf.Clamp(best.Center.x - side * (best.Radius + 0.75f), -ArenaHalf + 0.3f, ArenaHalf - 0.3f);
                // nie weiter weg landen, als er gerade stand (zu nah dran: bleibt stehen)
                if ((x - Pos.x) * side < 0f) x = Pos.x;
                HoopTarget = new Vector2(x, Level.FloorBelow(x, Mathf.Max(best.Pos.y, Pos.y) + 0.6f));
                Facing = side;
            }
            else
            {
                HoopTarget = Pos;
                Facing = aimSide;
            }
            KickAimLocal = Vector2.right;
        }

        void UpdateKnockout(float dt)
        {
            var fx = FxSystem.I;
            Color c = UltiDefs.Accent(UltiKind.Knockout);
            if (!ultiFired && ActionTime < KoGather)
            {
                // die Faust lädt sich auf: Funken strömen hinein, das Licht wird dichter
                ultiFxT -= dt;
                if (ultiFxT <= 0f)
                {
                    ultiFxT = 0.02f;
                    float k = ActionTime / KoGather;
                    Vector2 fist = ToWorld(new Vector2(Rig.Body.Shoulder.x - 0.28f, Rig.Body.StandHip + Rig.Body.Shoulder.y - 0.02f));
                    Vector2 off = Random.insideUnitCircle.normalized * Random.Range(0.8f, 1.6f);
                    fx.Streak(FxLayer.Front, fist + off, -off * Random.Range(6f, 10f), 0.14f, 0.04f, 0.05f,
                        Palette.PunchCore, c.WithAlpha(0f), 2.4f, 2f);
                    fx.Spawn(FxLayer.Front, true, Art.CellGlow, fist, Vector2.zero, 0.06f, 0.6f + k, 0.9f + k, c.WithAlpha(0.35f), c.WithAlpha(0f), 2f);
                }
            }
            else if (!ultiFired && ActionTime < KoGather + KoDash)
            {
                ghostTimer -= dt;
                if (ghostTimer <= 0f) { ghostTimer = 0.016f; ghosts.Spawn(c, 0.16f, 0.3f); }
            }
            if (!ultiFired && ActionTime >= KoGather + KoDash) { ultiFired = true; released = true; KnockoutHit(); }
            if (ActionTime >= KoGather + KoDash + KoEnd) EndUlti();
        }

        void KnockoutHit()
        {
            var s = S;
            Pos = HoopTarget;
            Vel = Vector2.zero;
            Grounded = true;
            Level.FloorBelow(Pos.x, Pos.y + 0.05f, FootHalf, Level.None, out int under);
            OnPlatform = under;
            platformVersion = Level.Version;
            airBoosts = s.AirBoosts;

            float p = UltiPower, area = s.AreaMul;
            Vector2 dir = new Vector2(Facing, 0.05f).normalized;
            Vector2 at = FistAt(dir);
            var target = ultiTargets.Count > 0 ? ultiTargets[0] : null;
            Color c = UltiDefs.Accent(UltiKind.Knockout);
            if (target != null && target.Alive)
            {
                at = target.Center - dir * Mathf.Min(target.Radius * 0.5f, 0.6f);
                Combat.Hit(target, 160f * p, dir + Vector2.up * 0.35f, 26f, Src.Ulti, forceCrit: true, big: true);
            }
            FistWaves.I.Fire(FistAt(dir), dir, 32f, 14f, 70f * p, 1.1f, 18f, Src.Ulti, c, 99, false, false, 0, 1.6f, true);
            Combat.Explosion(at, 3.4f * area, 60f * p, c, Src.Ulti);
            Ultis.KnockoutFx(at, dir, 3.4f * area);
            Coop.SendUlti(UltiKind.Knockout, 1, at, dir, 3.4f * area);
            Game.I.Hud.Popup(at + new Vector2(0f, 1.6f), "K.O.!", Palette.Gold, 56f, true);
            PunchStep = 1;
            PunchSerial++;

            var game = Game.I;
            game.Cam.Kick(dir * 0.6f);
            game.Cam.AddTrauma(0.95f);
            game.Cam.SetZoom(1f);
            game.Cam.ZoomPunch(0.08f);
            game.Post.Impact(1f);
            TimeFx.HitStop(0.16f, 0.02f);
            Rig.OnPowerContact();
        }

        // ------------------------------------------------------------------ VERA: Dreifach-Beben

        void UpdateQuake(float dt)
        {
            if (ultiStep < QuakeSlams && ActionTime >= QuakeAt(ultiStep))
            {
                // in der Luft hält sie die Pose und kracht erst herunter
                if (Grounded || ultiWait > 0.8f) { QuakeSlam(ultiStep); ultiStep++; ultiWait = 0f; }
                else
                {
                    ultiWait += dt;
                    ActionTime = QuakeAt(ultiStep);
                    Vel.y = Mathf.Min(Vel.y, -20f);
                }
            }
            // zwischen den Hieben ein kleiner Satz nach oben: Schwung holen für den nächsten
            if (quakeHops < ultiStep && ultiStep < QuakeSlams && Grounded && ActionTime >= QuakeAt(ultiStep - 1) + 0.09f)
            {
                quakeHops++;
                Vel.y = 7.5f;
                Grounded = false;
                OnPlatform = Level.None;
                Rig.OnJump();
            }
            if (ActionTime >= QuakeEnd) EndUlti();
        }

        void QuakeSlam(int k)
        {
            var s = S;
            float p = UltiPower, area = s.AreaMul;
            Vector2 at = new Vector2(Pos.x + Facing * 0.45f, Pos.y);
            float r = (4.4f + 1.9f * k) * area;
            Court.I.Shockwave(at + new Vector2(0f, 0.15f), r, 42f * p * (1f + 0.25f * k), 12f + 3f * k, 1f, 0f, true, Src.Ulti);
            Ultis.StompFx(at, r * 0.45f);
            Coop.SendUlti(UltiKind.Quake, (byte)(1 + k), at, Vector2.zero, r);
            var fx = FxSystem.I;
            Color c = UltiDefs.Accent(UltiKind.Quake);
            fx.Flash(at + new Vector2(0f, 0.4f), 1.6f + 0.4f * k, c, 0.16f, 1.8f);
            fx.Sparks(at, Vector2.up, 140f, 14 + 4 * k, 6f, 14f, c, 2.6f, 0.06f, 0.32f, 10f);
            var game = Game.I;
            game.Cam.AddTrauma(0.45f + 0.15f * k);
            game.Cam.Kick(new Vector2(0f, -0.35f - 0.1f * k));
            if (k == 0) game.Cam.SetZoom(1f);
            game.Post.Impact(0.5f + 0.2f * k);
            TimeFx.HitStop(0.06f + 0.02f * k, 0.04f);
            Rig.OnLand(20f + 3f * k);
        }

        // ------------------------------------------------------------------ LUZ: Schmetterlingstanz

        void UpdateButterfly(float dt)
        {
            var fx = FxSystem.I;
            Color c = UltiDefs.Accent(UltiKind.Butterfly);
            if (ActionTime >= AnkleCrouch && ultiStep < UltiSteps)
            {
                ghostTimer -= dt;
                if (ghostTimer <= 0f)
                {
                    ghostTimer = 0.02f;
                    ghosts.Spawn(ghostIndex++ % 2 == 0 ? c : Palette.Upper, 0.22f, 0.34f);
                    if (Random.value < 0.5f) fx.Motes(Pos + new Vector2(0f, Random.Range(0.4f, 1.6f)), new Vector2(-Facing * 1.5f, 0.8f), c, 1, 0.2f);
                }
            }
            while (ultiStep < UltiSteps && ActionTime >= AnkleCrouch + (ultiStep + 1) * ButterflyHop) ButterflyHit(ultiStep++);
            float poseAt = AnkleCrouch + UltiSteps * ButterflyHop;
            if (!ultiFired && ultiStep >= UltiSteps && ActionTime >= poseAt) { ultiFired = true; ButterflyFinale(); }
            if (ActionTime >= poseAt + ButterflyPose) EndUlti();
        }

        void ButterflyHit(int i)
        {
            var m = ultiTargets[i];
            Pos = ultiSpots[i];
            Vel = Vector2.zero;
            Grounded = true;
            Level.FloorBelow(Pos.x, Pos.y + 0.05f, FootHalf, Level.None, out int under);
            OnPlatform = under;
            platformVersion = Level.Version;
            // sie landet hinter ihm und dreht sich zum Schlag um
            if (m.Alive && Mathf.Abs(m.Center.x - Pos.x) > 0.05f) Facing = m.Center.x > Pos.x ? 1 : -1;

            var fx = FxSystem.I;
            Color c = UltiDefs.Accent(UltiKind.Butterfly);
            if (m.Alive)
            {
                Vector2 at = m.Center;
                Combat.Hit(m, 42f * UltiPower, new Vector2(Facing, 0.4f), 6f, Src.Ulti, big: true);
                if (m.Alive) m.Stun(1.6f);
                float ang = Facing > 0 ? 0f : 180f;
                for (int k = 0; k < 2; k++)
                    fx.Spawn(FxLayer.Front, true, Art.CellSparkle, at + new Vector2(0f, k * 0.25f - 0.12f), Vector2.zero, 0.18f, 1.4f, 0.3f,
                        Palette.PunchCore, c.WithAlpha(0f), 2.4f, 0f, 0f, ang + k * 45f, 0f, false);
                fx.Ring(FxLayer.Front, at, 0.2f, m.Radius * 2.6f, 0.2f, 0.02f, 0.34f, Color.white, c.WithAlpha(0f), 2.4f);
                fx.Sparks(at, new Vector2(Facing, 0.3f), 70f, 10, 5f, 12f, c, 2.4f, 0.05f, 0.24f);
            }
            fx.Dust(Pos, new Vector2(-Facing, 0.3f), 4, 2f, 0.34f, 0.28f);
            TimeFx.HitStop(0.035f, 0.05f);
            Game.I.Cam.AddTrauma(0.14f);
            Game.I.Cam.Kick(new Vector2(Facing * 0.1f, 0f));
            PunchStep = i % 2;
            PunchSerial++;
            Rig.OnPunch(PunchStep);
        }

        void ButterflyFinale()
        {
            var fx = FxSystem.I;
            Color c = UltiDefs.Accent(UltiKind.Butterfly);
            float p = UltiPower;
            foreach (var m in ultiTargets)
            {
                if (!m.Alive) continue;
                Combat.Hit(m, 28f * p, Vector2.up, 5f, Src.Ulti, big: true);
                fx.Sparkles(m.Center + new Vector2(0f, m.Radius), 0.5f, 6, c, 2.6f, 0.6f);
            }
            Vector2 at = Pos + new Vector2(0f, 1.2f);
            Ultis.ButterflyFinaleFx(at);
            Coop.SendUlti(UltiKind.Butterfly, 1, at, Vector2.zero, 0f);
            Game.I.Hud.Popup(Pos + new Vector2(0f, 3.1f), "SCHMETTERLINGSTANZ", c, 40f, true);
            Game.I.Cam.SetZoom(1f);
            Game.I.Cam.ZoomPunch(-0.05f);
            Game.I.Post.Impact(0.5f);
            PunchStep = 1;
            PunchSerial++;
        }
    }
}
