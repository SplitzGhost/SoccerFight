using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// The basketball player's moves. The body, the jump and the air recoil are the same as the
    /// soccer player's; the ball is dribbled at the hand instead of the foot and every move is
    /// thrown: the throw on the left button (it comes back like the soccer shot), the class moves
    /// on the right one (the three with its step-back, the crossover, the dunk) and the three boss
    /// skills (alley-oop, block, fast break).
    /// </summary>
    public sealed partial class Player
    {
        // throw: the ball is drawn up beside the head and pushed out at the cursor in one motion
        public const float ThrowSet = 0.07f, ThrowRelease = 0.12f, ThrowFollow = 0.24f, ThrowDuration = 0.36f;

        // three: a hop back (the step-back), the jump shot at the top, a high arc onto the cursor
        public const float ThreeHop = 0.12f, ThreeRelease = 0.3f, ThreeDuration = 0.62f;
        public const float ThreeCooldown = 3.5f, ThreeDamage = 34f, ThreeRadius = 2.2f, ThreeRange = 17f;

        // crossover: twice through the legs, then a burst of speed and throws that fly through monsters
        public const float CrossHalf = 0.21f, CrossDuration = 0.46f, CrossCooldown = 7f, CrossBoost = 3f, CrossSpeed = 0.25f;

        // dunk: gather, leap onto the cursor (up to DunkRange away), slam the ball into the floor
        public const float DunkGather = 0.1f, DunkRecover = 0.3f, DunkCooldown = 5f, DunkRange = 7.5f;
        public const float DunkDamage = 34f, DunkWaveRadius = 4.4f, DunkKnock = 13f;

        // alley-oop: a two-handed toss straight up; the ball hangs, then comes down on the nearest monster
        public const float OopRelease = 0.16f, OopDuration = 0.4f, OopCooldown = 7f, OopDamage = 46f, OopRadius = 2.3f;

        // block: arms up in a jump; for a moment nothing gets through and every shot flies back
        public const float BlockPose = 0.55f, BlockTime = 1.5f, BlockCooldown = 9f, BlockReflect = 26f, BlockRadius = 1.7f;

        // fast break: an invulnerable sprint forward, dribbling low; whatever is in the way is knocked over
        public const float FastRun = 0.32f, FastDuration = 0.46f, FastCooldown = 6f, FastDamage = 20f, FastStun = 1f;
        const float FastSpeed = 22f;   // ≈ 7 units in FastRun

        public float ThreeCd, CrossCd, DunkCd, OopCd, BlockCd, FastCd;
        float threeBuffer, crossBuffer, dunkBuffer, oopBuffer, blockBuffer, fastBuffer;

        /// <summary>Crossover boost left: faster legs, throws pass through monsters.</summary>
        public float CrossBoostLeft;
        /// <summary>Block left: invulnerable, enemy shots near the body fly back.</summary>
        public float BlockLeft;
        /// <summary>Heisse Hand: the ball is on fire for this long.</summary>
        public float HotHandLeft;
        /// <summary>Flight time of the current dunk (the rig and the partner's screen need it).</summary>
        public float DunkFlight = 0.55f;
        /// <summary>Where the current dunk lands (also the three's target): world space.</summary>
        public Vector2 HoopTarget;

        Vector2 dunkFrom;
        bool dunkSlammed, dunkRefunded, blockHopped;
        int fastDir = 1;
        int hotStreak, lastHitFlight = -1, lastThrowFlight = -1;
        float blockFxT, hotFxT;

        /// <summary>Plays basketball: dribbles at the hand, throws instead of kicking.</summary>
        public bool Hoops => Rig != null && Rig.Sport == Sport.Basketball;

        public float ThreeCooldownTotal => ThreeCooldown * S.CooldownOf(SkillCategory.Shot) * S.ThreeCooldownMul;
        public float CrossCooldownTotal => CrossCooldown * S.CooldownOf(SkillCategory.Technique) * S.CrossCooldownMul;
        public float DunkCooldownTotal => DunkCooldown * S.CooldownOf(SkillCategory.Header) * S.DunkCooldownMul;
        public float OopCooldownTotal => OopCooldown * S.CooldownOf(SkillCategory.Shot) * S.OopCooldownMul;
        public float BlockCooldownTotal => BlockCooldown * S.CooldownOf(SkillCategory.Defense) * S.BlockCooldownMul;
        public float FastCooldownTotal => FastCooldown * S.CooldownOf(SkillCategory.Technique) * S.FastBreakCooldownMul;

        /// <summary>The dunk is in the air: the body follows its arc instead of the physics.</summary>
        public bool DunkFlying => CurrentAction == Action.Dunk && ActionTime >= DunkGather && !dunkSlammed;
        public bool IsFastBreaking => CurrentAction == Action.FastBreak && ActionTime < FastRun;
        /// <summary>Speed bonus of the crossover boost.</summary>
        float CrossSpeedMul => CrossBoostLeft > 0f ? 1f + CrossSpeed : 1f;

        void ResetHoops()
        {
            ThreeCd = CrossCd = DunkCd = OopCd = BlockCd = FastCd = 0f;
            threeBuffer = crossBuffer = dunkBuffer = oopBuffer = blockBuffer = fastBuffer = 0f;
            CrossBoostLeft = BlockLeft = HotHandLeft = 0f;
            hotStreak = 0; lastHitFlight = lastThrowFlight = -1;
        }

        void TickHoops(float dt)
        {
            ThreeCd = Mathf.Max(0f, ThreeCd - dt);
            CrossCd = Mathf.Max(0f, CrossCd - dt);
            DunkCd = Mathf.Max(0f, DunkCd - dt);
            OopCd = Mathf.Max(0f, OopCd - dt);
            BlockCd = Mathf.Max(0f, BlockCd - dt);
            FastCd = Mathf.Max(0f, FastCd - dt);
            if (DevMode.NoCooldowns) ThreeCd = CrossCd = DunkCd = OopCd = BlockCd = FastCd = 0f;
            threeBuffer = Mathf.Max(0f, threeBuffer - dt);
            crossBuffer = Mathf.Max(0f, crossBuffer - dt);
            dunkBuffer = Mathf.Max(0f, dunkBuffer - dt);
            oopBuffer = Mathf.Max(0f, oopBuffer - dt);
            blockBuffer = Mathf.Max(0f, blockBuffer - dt);
            fastBuffer = Mathf.Max(0f, fastBuffer - dt);

            if (CrossBoostLeft > 0f)
            {
                CrossBoostLeft -= dt;
                if (CrossBoostLeft <= 0f) { CrossBoostLeft = 0f; FxSystem.I.Ring(FxLayer.Front, Pos + new Vector2(0f, 1f), 0.8f, 0.2f, 0.06f, 0.01f, 0.2f, Palette.Trick, Palette.Trick.WithAlpha(0f), 1.8f); }
            }
            if (HotHandLeft > 0f)
            {
                HotHandLeft = Mathf.Max(0f, HotHandLeft - dt);
                hotFxT -= dt;
                if (hotFxT <= 0f && !Dead)
                {
                    hotFxT = 0.05f;
                    Vector2 hand = Ball.IsHeld ? Ball.Pos : Pos + new Vector2(Facing * 0.3f, 1.2f);
                    FxSystem.I.Streak(FxLayer.Front, hand + Random.insideUnitCircle * 0.12f, new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(2.5f, 4.5f)),
                        Random.Range(0.25f, 0.4f), 0.07f, 0.05f, new Color(1f, 0.85f, 0.45f), Palette.BlastOrange.WithAlpha(0f), 2.6f, 1.2f, 3f);
                }
            }
            if (BlockLeft > 0f)
            {
                BlockLeft = Mathf.Max(0f, BlockLeft - dt);
                DodgeTime = Mathf.Max(DodgeTime, Mathf.Min(BlockLeft, 0.1f));
                ReflectShots();
                blockFxT -= dt;
                if (blockFxT <= 0f)
                {
                    blockFxT = 0.09f;
                    float r = BlockRadius * S.BlockRadiusMul;
                    FxSystem.I.Ring(FxLayer.Front, Pos + new Vector2(0f, 1.05f), r * 0.9f, r * 1.02f, 0.05f, 0.01f, 0.18f,
                        Color.white.WithAlpha(0.55f), Palette.Guard.WithAlpha(0f), 1.8f);
                    if (Random.value < 0.6f)
                        FxSystem.I.Sparkles(Pos + new Vector2(0f, 1.05f) + Random.insideUnitCircle.normalized * r, 0.1f, 1, Palette.Guard, 2.4f, 0.3f);
                }
            }
        }

        /// <summary>A skill key of the basketball pool was pressed.</summary>
        bool PressHoopsSkill(Ability a)
        {
            switch (a)
            {
                case Ability.AlleyOop: oopBuffer = 0.3f; return true;
                case Ability.Block: blockBuffer = 0.25f; return true;
                case Ability.FastBreak: fastBuffer = 0.25f; return true;
                default: return false;
            }
        }

        bool PressHoopsClassMove(Ability a)
        {
            switch (a)
            {
                case Ability.Three: threeBuffer = 0.3f; return true;
                case Ability.Crossover: crossBuffer = 0.25f; return true;
                case Ability.Dunk: dunkBuffer = 0.3f; return true;
                default: return false;
            }
        }

        /// <summary>Starts a buffered basketball move. Returns true if one began.</summary>
        bool StartHoopsAction()
        {
            if (blockBuffer > 0f && BlockCd <= 0f) { StartBlock(); return true; }
            if (fastBuffer > 0f && FastCd <= 0f && Grounded) { StartFastBreak(); return true; }
            if (crossBuffer > 0f && CrossCd <= 0f && TakeBall()) { StartCrossover(); return true; }
            if (dunkBuffer > 0f && DunkCd <= 0f && TakeBall()) { StartDunk(); return true; }
            if (threeBuffer > 0f && ThreeCd <= 0f && TakeBall()) { StartThree(); return true; }
            if (oopBuffer > 0f && OopCd <= 0f && TakeBall()) { StartOop(); return true; }
            return false;
        }

        /// <summary>How much the current basketball move lets the legs run (1: fully).</summary>
        float HoopsSpeedMul()
        {
            switch (CurrentAction)
            {
                case Action.Throw: return 0.7f;
                case Action.Three: return ActionTime < ThreeHop ? 0f : 0.25f;
                case Action.Crossover: return 0.2f;
                case Action.Dunk: return ActionTime < DunkGather ? 0f : 0.1f;
                case Action.AlleyOop: return 0.35f;
                case Action.Block: return 0.3f;
                case Action.FastBreak: return 0.1f;
                default: return 1f;
            }
        }

        void UpdateHoopsAction(float dt)
        {
            switch (CurrentAction)
            {
                case Action.Throw: UpdateThrow(); break;
                case Action.Three: UpdateThree(dt); break;
                case Action.Crossover: UpdateCrossover(dt); break;
                case Action.Dunk: UpdateDunk(dt); break;
                case Action.AlleyOop: UpdateOop(); break;
                case Action.Block: UpdateBlock(); break;
                case Action.FastBreak: UpdateFastBreak(dt); break;
            }
        }

        /// <summary>A hit interrupts the move: an unreleased ball drops out of the hands.</summary>
        void AbortHoops()
        {
            switch (CurrentAction)
            {
                case Action.Throw: case Action.Three: case Action.AlleyOop:
                    if (!released) { Ball.Release(); CurrentAction = Action.None; }
                    break;
                case Action.Crossover: case Action.FastBreak:
                    CurrentAction = Action.None;
                    if (StepCarry) { Ball.EndScripted(); StepCarry = false; }
                    break;
                case Action.Block:
                    CurrentAction = Action.None;
                    break;
            }
        }

        // ------------------------------------------------------------------ throw

        void StartThrow()
        {
            shotBuffer = 0f;
            Vector2 aim = GameInput.AimWorld - (Pos + new Vector2(0f, 1.4f));
            if (aim.sqrMagnitude < 0.01f) aim = new Vector2(Facing, 0.1f);
            if (aim.x * Facing < -0.05f) Facing = aim.x > 0f ? 1 : -1;
            KickAimLocal = ToLocal(Pos + aim.normalized);
            KickBallLocal = ToLocal(Ball.Pos);
            CurrentAction = Action.Throw;
            ActionTime = 0f;
            released = false;
            ShotCd = ShotCooldownTotal;
            Ball.BeginScripted();
            Game.I.Hud.OnShotUsed();
        }

        void UpdateThrow()
        {
            if (!released && ActionTime >= ThrowRelease) { released = true; ReleaseThrow(); }
            if (ActionTime >= ThrowDuration) CurrentAction = Action.None;
        }

        void ReleaseThrow()
        {
            var fx = FxSystem.I;
            Vector2 from = Ball.Pos;
            Vector2 dir = GameInput.AimWorld - from;
            if (dir.sqrMagnitude < 0.01f) dir = new Vector2(Facing, 0.1f);
            dir.Normalize();
            if (Grounded && OnPlatform == Level.None && dir.y < -0.45f) dir = new Vector2(dir.x, -0.45f).normalized;
            KickAimLocal = new Vector2(dir.x * Facing, dir.y);

            var s = S;
            // the streak of the hot hand breaks when the last throw found nobody
            if (lastThrowFlight >= 0 && lastHitFlight != lastThrowFlight) hotStreak = 0;
            bool runUp = s.RunUpBonus > 0f && Grounded && Mathf.Abs(Vel.x) > MaxSpeedNow * 0.75f;
            Ball.Kick(dir * ShotSpeed * s.BallSpeedMul, Grounded ? OnPlatform : Level.None);
            lastThrowFlight = Ball.FlightId;
            Ball.ShotMul = (runUp ? 1f + s.RunUpBonus : 1f) * (!Grounded ? 1f + s.AirThrowBonus : 1f);
            Ball.RicochetsLeft = s.Ricochets;
            Ball.BanksLeft = s.BankShots;
            Ball.PierceShot = CrossBoostLeft > 0f;
            shotCount++;
            Ball.GoldenShot = s.GoldenBoot && shotCount % 3 == 0;
            if (Ball.GoldenShot)
            {
                fx.Flash(from, 1.8f, Palette.Gold, 0.16f, 3.2f);
                fx.Sparkles(from, 0.3f, 8, Palette.Gold, 3f, 0.5f);
            }
            if (runUp) fx.Sparks(from, -dir, 40f, 5, 4f, 8f, Palette.Gold, 2.2f, 0.035f, 0.18f);

            Color c = Palette.HoopOrange;
            for (int i = 0; i < s.EchoBalls; i++)
            {
                float ang = s.EchoSpread * ((i >> 1) + 1) * (i % 2 == 0 ? 1f : -1f);
                EchoBalls.I.Fire(from, MathUtil.Rotate(dir, ang), ShotSpeed * s.BallSpeedMul * 0.95f, ShotDamage, Src.Echo, c, Ball.PierceShot, false, s.EchoRicochet);
            }
            // Spiegelbild: during the crossover boost two piercing echoes fan out with every throw
            if (s.CrossMirror && CrossBoostLeft > 0f)
                for (int i = -1; i <= 1; i += 2)
                    EchoBalls.I.Fire(from, MathUtil.Rotate(dir, 7f * i), ShotSpeed * s.BallSpeedMul, ShotDamage, Src.Echo, Palette.Trick, true);

            Color core = new Color(1f, 0.93f, 0.82f);
            fx.Flash(from, 1f, c, 0.1f, 2.6f);
            fx.Ring(FxLayer.Front, from, 0.1f, 0.75f, 0.1f, 0.01f, 0.18f, core, c.WithAlpha(0f), 2.2f);
            fx.Sparks(from, dir, 40f, 7, 6f, 14f, Ball.PierceShot ? Palette.Trick : c, 2.4f, 0.045f, 0.2f);
            if (Ball.PierceShot)
                for (int i = 0; i < 3; i++)
                    fx.Streak(FxLayer.Front, from + Random.insideUnitCircle * 0.15f, dir * Random.Range(12f, 18f), 0.16f, 0.035f, 0.05f,
                        Color.white.WithAlpha(0.8f), Palette.Trick.WithAlpha(0f), 2.2f, 4f);

            var game = Game.I;
            game.Cam.Kick(-dir * 0.12f);
            game.Cam.AddTrauma(0.12f);
            game.Post.Impact(0.16f);
            TimeFx.HitStop(0.025f, 0.05f);
            Rig.OnThrowRelease();

            // throwing in the air pushes the body the other way: the basketball double jump
            if (!Grounded && airBoosts > 0) AirBoost(-dir * AirKickBoost);
        }

        /// <summary>A throw of this flight hit a monster (the hot hand counts throws in a row that hit).</summary>
        public void OnThrowHit()
        {
            if (Ball.FlightId == lastHitFlight) return;
            lastHitFlight = Ball.FlightId;
            if (!S.HotHand || HotHandLeft > 0f) return;
            if (++hotStreak < 3) return;
            hotStreak = 0;
            HotHandLeft = 5f;
            var fx = FxSystem.I;
            Vector2 c = Pos + new Vector2(0f, 1.2f);
            fx.Flash(c, 3f, Palette.BlastOrange, 0.2f, 3f);
            fx.Ring(FxLayer.Front, c, 0.3f, 2.2f, 0.2f, 0.01f, 0.35f, Color.white, Palette.BlastOrange.WithAlpha(0f), 2.6f);
            fx.Sparks(c, Vector2.up, 120f, 16, 4f, 10f, Palette.BlastOrange, 2.6f, 0.05f, 0.4f);
            Game.I.Hud.ShowToast("HEISSE HAND  ·  DER BALL BRENNT");
            Game.I.Cam.AddTrauma(0.15f);
        }

        // ------------------------------------------------------------------ three

        void StartThree()
        {
            threeBuffer = 0f;
            Vector2 aim = GameInput.AimWorld;
            float tx = Mathf.Clamp(aim.x, -ArenaHalf - 0.5f, ArenaHalf + 0.5f);
            // never into the ground: a cursor under a surface means that surface
            float floor = Level.FloorBelow(tx, aim.y + 0.3f);
            HoopTarget = new Vector2(tx, Mathf.Max(aim.y, floor + Art.BallRadius));
            Vector2 d = HoopTarget - Pos;
            if (d.magnitude > ThreeRange) HoopTarget = Pos + d.normalized * ThreeRange;
            if (Mathf.Abs(HoopTarget.x - Pos.x) > 0.2f) Facing = HoopTarget.x > Pos.x ? 1 : -1;
            KickAimLocal = ToLocal(Pos + new Vector2(Facing * 0.55f, 0.85f).normalized);
            KickBallLocal = ToLocal(Ball.Pos);
            CurrentAction = Action.Three;
            ActionTime = 0f;
            released = false;
            ThreeCd = ThreeCooldownTotal;
            Ball.BeginScripted();
            Game.I.Hud.OnSkillUsed(Ability.Three);
            Game.I.Cam.SetZoom(0.97f);
            FxSystem.I.Dust(Pos, new Vector2(Facing, 0.3f), 5, 1.8f, 0.34f, 0.3f);
        }

        void UpdateThree(float dt)
        {
            // the step-back: plant, then a hop away from the target
            if (ActionTime >= ThreeHop * 0.5f && Grounded && Vel.y <= 0.1f && !released)
            {
                Vel = new Vector2(-Facing * 6.8f, 8.2f * S.JumpMul);
                Grounded = false;
                OnPlatform = Level.None;
                boostRise = true;
                Rig.OnJump();
                FxSystem.I.Dust(Pos, new Vector2(Facing, 0.2f), 7, 2.4f, 0.4f, 0.34f);
                ghostTimer = 0f;
            }
            if (!released && ActionTime < ThreeRelease)
            {
                ghostTimer -= dt;
                if (ghostTimer <= 0f && ActionTime > ThreeHop * 0.5f) { ghostTimer = 0.04f; ghosts.Spawn(Palette.HoopOrange, 0.12f, 0.2f); }
            }
            if (!released && ActionTime >= ThreeRelease) { released = true; ReleaseThree(); }
            if (ActionTime >= ThreeDuration || (released && Grounded && ActionTime > ThreeRelease + 0.1f)) { CurrentAction = Action.None; Game.I.Cam.SetZoom(1f); }
        }

        void ReleaseThree()
        {
            var s = S;
            Vector2 from = Ball.Pos;
            float dist = (HoopTarget - from).magnitude;
            float flight = Mathf.Clamp(0.5f + dist * 0.045f, 0.55f, 1.15f);
            float damage = ThreeDamage;   // Combat applies the three's damage upgrades
            float radius = ThreeRadius * s.ThreeRadiusMul * s.AreaMul;
            Ball.Lob(HoopTarget, flight, damage, radius);
            Vector2 launch = Ball.Vel.sqrMagnitude > 0.01f ? Ball.Vel.normalized : new Vector2(Facing, 1f).normalized;
            KickAimLocal = new Vector2(launch.x * Facing, launch.y);
            // Dreier-Regen: two more balls land beside the target a beat later
            for (int i = 0; i < s.ThreeSplit; i++)
            {
                float off = (i % 2 == 0 ? 1f : -1f) * (2.1f + (i >> 1) * 1.2f);
                float tx = Mathf.Clamp(HoopTarget.x + off, -ArenaHalf, ArenaHalf);
                Vector2 to = new Vector2(tx, Level.FloorBelow(tx, HoopTarget.y + 1.5f) + Art.BallRadius);
                Court.I.GhostLob(from, to, flight + 0.08f * (i + 1), damage * 0.6f, radius * 0.8f);
            }
            Combat.Maestro(this);

            var fx = FxSystem.I;
            fx.Flash(from, 1.6f, Palette.HoopOrange, 0.14f, 3f);
            fx.Ring(FxLayer.Front, from, 0.12f, 1f, 0.14f, 0.01f, 0.22f, Color.white, Palette.HoopOrange.WithAlpha(0f), 2.4f);
            fx.Sparks(from, launch, 40f, 10, 6f, 14f, Palette.HoopFlame, 2.4f, 0.05f, 0.24f);
            fx.Sparkles(from, 0.3f, 5, Palette.Gold, 2.6f, 0.4f);
            var game = Game.I;
            game.Cam.Kick(-launch * 0.14f);
            game.Cam.AddTrauma(0.14f);
            game.Post.Impact(0.25f);
            TimeFx.HitStop(0.03f, 0.05f);
            Rig.OnThrowRelease();
        }

        // ------------------------------------------------------------------ crossover

        void StartCrossover()
        {
            crossBuffer = 0f;
            if (Mathf.Abs(GameInput.MoveX) > 0.01f) Facing = GameInput.MoveX > 0f ? 1 : -1;
            CurrentAction = Action.Crossover;
            ActionTime = 0f;
            CrossCd = CrossCooldownTotal;
            DodgeTime = Mathf.Max(DodgeTime, 0.25f);
            StepCarry = true;
            Ball.BeginScripted();
            ghostTimer = 0f;
            Game.I.Hud.OnSkillUsed(Ability.Crossover);
            FxSystem.I.Dust(Pos, new Vector2(-Facing, 0.2f), 5, 1.6f, 0.34f, 0.3f);
        }

        void UpdateCrossover(float dt)
        {
            ghostTimer -= dt;
            if (ghostTimer <= 0f) { ghostTimer = 0.05f; ghosts.Spawn(Palette.Trick, 0.2f, 0.2f); }
            if (ActionTime < CrossDuration) return;
            CurrentAction = Action.None;
            if (StepCarry) Ball.EndScripted();
            StepCarry = false;
            CrossBurst();
        }

        /// <summary>The crossover is done: the boost starts (and Ankle Breaker drops everyone around).</summary>
        void CrossBurst()
        {
            var s = S;
            CrossBoostLeft = CrossBoost + s.CrossTimeBonus;
            OnTrick();
            Combat.Maestro(this);
            var fx = FxSystem.I;
            Color c = Palette.Trick;
            Vector2 at = Pos + new Vector2(0f, 0.9f);
            fx.Ring(FxLayer.Front, Pos + new Vector2(0f, 0.12f), 0.2f, 1.6f, 0.14f, 0.01f, 0.28f, Color.white, c.WithAlpha(0f), 2.4f);
            fx.Flash(at, 2f, c, 0.14f, 2.6f);
            fx.Sparkles(at, 0.7f, 10, c, 2.8f, 0.5f);
            for (int i = 0; i < 6; i++)
                fx.Streak(FxLayer.Front, Pos + new Vector2(0f, Random.Range(0.2f, 1.7f)), new Vector2(-Facing * Random.Range(7f, 12f), 0f),
                    Random.Range(0.14f, 0.22f), 0.03f, 0.06f, Color.white.WithAlpha(0.8f), c.WithAlpha(0f), 2f, 5f);
            if (s.CrossStunRadius > 0f)
            {
                float r = s.CrossStunRadius * s.AreaMul;
                foreach (var m in Game.I.Waves.Monsters)
                {
                    if (!m.Alive || (m.Center - at).sqrMagnitude > (r + m.Radius) * (r + m.Radius) || m.Rank == Rank.Boss) continue;
                    m.Stun(1f);
                    m.Expose(1.5f);
                    fx.Sparkles(m.Center + new Vector2(0f, m.Radius), 0.4f, 5, c, 2.6f, 0.5f);
                }
                fx.Ring(FxLayer.Front, at, 0.3f, r, 0.2f, 0.01f, 0.36f, Color.white.WithAlpha(0.8f), c.WithAlpha(0f), 2.2f);
                Game.I.Hud.Popup(at + new Vector2(0f, 1.2f), "ANKLE BREAKER", c, 26f, false);
            }
            Game.I.Cam.Kick(new Vector2(Facing * 0.1f, 0f));
            Game.I.Cam.AddTrauma(0.1f);
            Rig.OnDash();
        }

        // ------------------------------------------------------------------ dunk

        void StartDunk()
        {
            dunkBuffer = 0f;
            var s = S;
            float range = DunkRange * s.DunkRangeMul;
            Vector2 aim = GameInput.AimWorld;
            float tx = Mathf.Clamp(aim.x, Pos.x - range, Pos.x + range);
            tx = Mathf.Clamp(tx, -ArenaHalf, ArenaHalf);
            // land on the surface under the cursor — never higher than a big jump can reach
            float top = Mathf.Min(aim.y + 0.35f, Pos.y + 4.6f);
            float ty = Level.FloorBelow(tx, Mathf.Max(top, Level.FloorBelow(tx, Pos.y + 0.05f) + 0.05f));
            HoopTarget = new Vector2(tx, ty);
            dunkFrom = Pos;
            float dx = Mathf.Abs(tx - Pos.x), dy = ty - Pos.y;
            DunkFlight = Mathf.Clamp(0.42f + dx * 0.035f + Mathf.Max(0f, dy) * 0.04f, 0.42f, 0.78f);
            if (Mathf.Abs(tx - Pos.x) > 0.2f) Facing = tx > Pos.x ? 1 : -1;
            KickBallLocal = ToLocal(Ball.Pos);
            CurrentAction = Action.Dunk;
            ActionTime = 0f;
            released = false;
            dunkSlammed = false;
            dunkRefunded = false;
            DunkCd = DunkCooldownTotal;
            Ball.BeginScripted();
            Game.I.Hud.OnSkillUsed(Ability.Dunk);
            Game.I.Cam.SetZoom(0.95f);
        }

        /// <summary>Height of the dunk's arc above the straight line from take-off to landing.</summary>
        float DunkArc => 1.9f + Mathf.Abs(HoopTarget.x - dunkFrom.x) * 0.12f;

        /// <summary>The scripted leap (called instead of gravity and collision while DunkFlying).</summary>
        void DunkMove(float dt)
        {
            float k = Mathf.Clamp01((ActionTime - DunkGather) / DunkFlight);
            Vector2 prev = Pos;
            // slow at the top, quick at the ends: the hang time of a real dunk
            float e = k < 0.5f ? 0.5f * MathUtil.EaseOutCubic(k * 2f) : 0.5f + 0.5f * MathUtil.EaseInCubic((k - 0.5f) * 2f);
            Pos = Vector2.Lerp(dunkFrom, HoopTarget, e) + new Vector2(0f, DunkArc * 4f * e * (1f - e));
            float dtAct = Mathf.Max(dt, 1e-4f);
            Vel = (Pos - prev) / dtAct;
            Grounded = false;
            OnPlatform = Level.None;
            DodgeTime = Mathf.Max(DodgeTime, 0.08f);
            if (k >= 1f) DunkSlam();
        }

        void UpdateDunk(float dt)
        {
            if (ActionTime >= DunkGather && !dunkSlammed && ActionTime - dt < DunkGather)
            {
                // take-off
                Rig.OnJump();
                var fx = FxSystem.I;
                fx.Dust(Pos, Vector2.right, 6, 2.6f, 0.45f, 0.36f);
                fx.Dust(Pos, Vector2.left, 6, 2.6f, 0.45f, 0.36f);
                fx.Ring(FxLayer.Front, Pos + new Vector2(0f, 0.1f), 0.2f, 1.2f, 0.08f, 0.01f, 0.2f, Color.white.WithAlpha(0.7f), Palette.Slam.WithAlpha(0f), 1.6f);
                ghostTimer = 0f;
            }
            if (DunkFlying)
            {
                ghostTimer -= dt;
                if (ghostTimer <= 0f) { ghostTimer = 0.06f; ghosts.Spawn(Palette.Slam, 0.08f, 0.2f); }
            }
            if (dunkSlammed && ActionTime >= DunkGather + DunkFlight + DunkRecover) { CurrentAction = Action.None; Game.I.Cam.SetZoom(1f); }
        }

        void DunkSlam()
        {
            dunkSlammed = true;
            released = true;
            var s = S;
            Pos = HoopTarget;
            Vel = Vector2.zero;
            Grounded = true;
            Level.FloorBelow(Pos.x, Pos.y + 0.05f, FootHalf, Level.None, out int under);
            OnPlatform = under;
            platformVersion = Level.Version;
            airBoosts = s.AirBoosts;
            airDashes = s.AirDashes;
            ActionTime = DunkGather + DunkFlight;

            // the ball is driven into the floor in front of the feet and bounces sky-high
            Vector2 at = new Vector2(Pos.x + Facing * 0.35f, Pos.y);
            Ball.Slam(at);
            Combat.Maestro(this);

            float waves = 2 + s.DunkExtraWaves;
            float radius = DunkWaveRadius * s.DunkWaveMul * s.AreaMul;
            for (int i = 0; i < waves; i++)
                Court.I.Shockwave(at + new Vector2(0f, 0.15f), radius * (1f + i * 0.28f), DunkDamage * (i == 0 ? 1f : 0.5f), DunkKnock * (i == 0 ? 1f : 0.7f), s.DunkStun, i * 0.11f, true);
            CoopFx.Send(CoopFx.Kind.Slam, at, radius, Palette.Slam);

            var fx = FxSystem.I;
            fx.Flash(at + new Vector2(0f, 0.3f), 1.5f, Palette.Slam, 0.12f, 1.6f);
            fx.Flash(at, 0.7f, Color.white, 0.06f, 2.4f);
            fx.Dust(at, Vector2.right, 10, 5f, 0.55f, 0.3f);
            fx.Dust(at, Vector2.left, 10, 5f, 0.55f, 0.3f);
            fx.Sparks(at, Vector2.up, 150f, 18, 5f, 13f, Palette.Slam, 2.4f, 0.05f, 0.35f, 10f);
            // cracks spray up out of the floor
            for (int i = 0; i < 8; i++)
            {
                float ang = Random.Range(25f, 155f);
                fx.Spawn(FxLayer.Front, false, Art.CellShard, at, MathUtil.Dir(ang) * Random.Range(4f, 9f), Random.Range(0.4f, 0.7f),
                    Random.Range(0.08f, 0.14f), 0.04f, Palette.StoneLight, Palette.Stone.WithAlpha(0f), 1f, 0.5f, 22f, Random.Range(0f, 360f), Random.Range(-600f, 600f));
            }
            var game = Game.I;
            game.Cam.AddTrauma(0.5f);
            game.Cam.Kick(new Vector2(0f, -0.45f));
            game.Cam.ZoomPunch(0.05f);
            game.Post.Impact(0.45f);
            TimeFx.HitStop(0.09f, 0.04f);
            Rig.OnLand(20f);
        }

        /// <summary>Skywalker: a monster fell to the dunk — it is ready again.</summary>
        public void OnDunkKill()
        {
            if (!S.DunkRefund || dunkRefunded) return;
            dunkRefunded = true;
            DunkCd = 0f;
            Game.I.Hud.ShowToast("SKYWALKER  ·  DUNK BEREIT");
        }

        // ------------------------------------------------------------------ alley-oop

        void StartOop()
        {
            oopBuffer = 0f;
            float dx = GameInput.AimWorld.x - Pos.x;
            if (Mathf.Abs(dx) > 0.3f) Facing = dx > 0f ? 1 : -1;
            CurrentAction = Action.AlleyOop;
            ActionTime = 0f;
            released = false;
            KickBallLocal = ToLocal(Ball.Pos);
            OopCd = OopCooldownTotal;
            Ball.BeginScripted();
            Game.I.Hud.OnSkillUsed(Ability.AlleyOop);
        }

        void UpdateOop()
        {
            if (!released && ActionTime >= OopRelease)
            {
                released = true;
                var s = S;
                // the ball comes down on the nearest monster (or where the cursor points if there is none)
                Vector2 fallback = new Vector2(GameInput.AimWorld.x, Level.FloorBelow(GameInput.AimWorld.x, GameInput.AimWorld.y + 0.3f));
                Ball.AlleyOop(new Vector2(Facing * 1.2f, 13f), fallback, OopDamage, OopRadius * s.AreaMul);
                Combat.Maestro(this);
                var fx = FxSystem.I;
                fx.Flash(Ball.Pos, 1.4f, Palette.Oop, 0.12f, 2.6f);
                fx.Sparks(Ball.Pos, Vector2.up, 40f, 9, 6f, 12f, Palette.Oop, 2.4f, 0.04f, 0.24f);
                Game.I.Cam.AddTrauma(0.1f);
                Rig.OnThrowRelease();
            }
            if (ActionTime >= OopDuration) CurrentAction = Action.None;
        }

        // ------------------------------------------------------------------ block

        void StartBlock()
        {
            blockBuffer = 0f;
            float dx = GameInput.AimWorld.x - Pos.x;
            if (Mathf.Abs(dx) > 0.3f) Facing = dx > 0f ? 1 : -1;
            CurrentAction = Action.Block;
            ActionTime = 0f;
            blockHopped = false;
            BlockCd = BlockCooldownTotal;
            var s = S;
            BlockLeft = BlockTime + s.BlockTimeBonus;
            Combat.Maestro(this);
            Game.I.Hud.OnSkillUsed(Ability.Block);

            // a shove on the way up: whatever stands close is pushed back (and frozen with Lockdown)
            float r = BlockRadius * 1.4f * s.BlockRadiusMul * s.AreaMul;
            Vector2 c = Pos + new Vector2(0f, 1f);
            foreach (var m in Game.I.Waves.Monsters)
            {
                if (!m.Alive) continue;
                Vector2 d = m.Center - c;
                if (d.magnitude > r + m.Radius) continue;
                Combat.Hit(m, 8f, new Vector2(Mathf.Sign(d.x == 0f ? Facing : d.x), 0.5f), 12f, Src.Block, big: true);
                if (s.BlockStun && m.Alive) m.Stun(1.2f);
            }
            var fx = FxSystem.I;
            fx.Ring(FxLayer.Front, c, 0.3f, r, 0.12f, 0.01f, 0.3f, Color.white.WithAlpha(0.8f), Palette.Guard.WithAlpha(0f), 1.8f);
            fx.Flash(c, 1.6f, Palette.Guard, 0.12f, 2f);
            fx.Sparkles(c + new Vector2(0f, 0.9f), 0.5f, 8, Color.white, 2.6f, 0.4f);
            Game.I.Cam.AddTrauma(0.14f);
        }

        void UpdateBlock()
        {
            if (!blockHopped && ActionTime >= 0.06f)
            {
                blockHopped = true;
                if (Grounded)
                {
                    Vel.y = 11f * S.JumpMul;
                    Grounded = false;
                    OnPlatform = Level.None;
                    boostRise = true;
                    Rig.OnJump();
                    FxSystem.I.Dust(Pos, Vector2.up, 5, 1.6f, 0.36f, 0.3f);
                }
            }
            if (ActionTime >= BlockPose || (blockHopped && Grounded && ActionTime > 0.2f)) CurrentAction = Action.None;
        }

        /// <summary>While the block holds, enemy shots that come close fly back at the monsters.</summary>
        void ReflectShots()
        {
            var s = S;
            float r = BlockRadius * s.BlockRadiusMul;
            Vector2 c = Pos + new Vector2(0f, 1.05f);
            EnemyProjectiles.I.Reflect(c, r, (at, col) =>
            {
                var target = Combat.NearestTo(at, 16f);
                Vector2 dir = target != null ? (target.Center - at).normalized : new Vector2(Facing, 0.3f).normalized;
                EchoBalls.I.Fire(at, dir, 26f, BlockReflect * s.BlockReflectMul, Src.Block, Palette.Guard, true);
                var fx = FxSystem.I;
                fx.Flash(at, 1.4f, Palette.Guard, 0.1f, 2.8f);
                fx.Ring(FxLayer.Front, at, 0.1f, 0.8f, 0.12f, 0.01f, 0.2f, Color.white, Palette.Guard.WithAlpha(0f), 2.4f);
                fx.Sparks(at, dir, 50f, 6, 5f, 11f, Palette.Guard, 2.4f, 0.04f, 0.2f);
                Game.I.Hud.Popup(at + new Vector2(0f, 0.5f), "BLOCK", Palette.Guard, 24f, false);
            });
        }

        // ------------------------------------------------------------------ fast break

        void StartFastBreak()
        {
            fastBuffer = 0f;
            if (Mathf.Abs(GameInput.MoveX) > 0.01f) Facing = GameInput.MoveX > 0f ? 1 : -1;
            fastDir = Facing;
            CurrentAction = Action.FastBreak;
            ActionTime = 0f;
            FastCd = FastCooldownTotal;
            DodgeTime = Mathf.Max(DodgeTime, FastRun + 0.1f);
            StepCarry = Ball.IsHeldFree;
            if (StepCarry) Ball.BeginScripted();
            sweepHits.Clear();
            ghostTimer = 0f;
            Game.I.Hud.OnSkillUsed(Ability.FastBreak);
            OnTrick();
            Combat.Maestro(this);

            var fx = FxSystem.I;
            Color c = Palette.DashMint;
            fx.Dust(Pos, new Vector2(-fastDir, 0.25f), 8, 2.8f, 0.42f, 0.36f);
            fx.Ring(FxLayer.Front, Pos + new Vector2(-fastDir * 0.2f, 0.85f), 0.2f, 1.2f, 0.12f, 0.01f, 0.22f, Color.white, c.WithAlpha(0f), 2f);
            Game.I.Cam.Kick(new Vector2(fastDir * 0.18f, 0f));
            Game.I.Cam.AddTrauma(0.08f);
            Rig.OnDash();
        }

        void UpdateFastBreak(float dt)
        {
            if (ActionTime < FastRun)
            {
                ghostTimer -= dt;
                if (ghostTimer <= 0f)
                {
                    ghostTimer = 0.026f;
                    ghosts.Spawn(S.FastBreakFire ? Palette.BlastOrange : Palette.DashMint, 0.16f, 0.26f);
                    if (S.FastBreakFire)
                        FxSystem.I.Streak(FxLayer.Front, Pos + new Vector2(0f, Random.Range(0.05f, 0.4f)), new Vector2(0f, Random.Range(1.5f, 3.5f)),
                            Random.Range(0.3f, 0.5f), 0.08f, 0.05f, new Color(1f, 0.85f, 0.45f), Palette.BlastOrange.WithAlpha(0f), 2.6f, 1.2f, 2f);
                }
                SweepMonsters(0.95f, 1.2f, FastBreakHit);
            }
            else if (Mathf.Abs(Vel.x) > MaxSpeedNow) Vel.x = fastDir * MaxSpeedNow;
            if (ActionTime >= FastDuration)
            {
                CurrentAction = Action.None;
                if (StepCarry) Ball.EndScripted();
                StepCarry = false;
            }
        }

        void FastBreakHit(Monster m)
        {
            var s = S;
            Combat.Hit(m, FastDamage, new Vector2(fastDir, 0.7f), 10f, Src.FastBreak, big: true);
            if (m.Alive) m.Stun(FastStun);
            if (s.FastBreakFire && m.Alive) m.Ignite(Mathf.Max(5f, FastDamage * 0.4f), 3f);
            if (s.FastBreakHeal > 0f) Heal(s.FastBreakHeal, true);
            var fx = FxSystem.I;
            fx.Sparks(m.Center, new Vector2(fastDir, 0.4f), 60f, 8, 5f, 11f, Palette.DashMint, 2.4f, 0.05f, 0.24f);
            fx.Ring(FxLayer.Front, m.Center, 0.1f, m.Radius * 1.8f, 0.14f, 0.01f, 0.22f, Color.white, Palette.DashMint.WithAlpha(0f), 2.2f);
            Game.I.Cam.AddTrauma(0.1f);
        }
    }
}
