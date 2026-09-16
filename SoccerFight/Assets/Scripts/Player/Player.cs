using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Player movement, abilities and health. Feel tricks: acceleration curves with a fast turn-around,
    /// coyote time, jump buffering, variable jump height, apex hang, input buffering for shots.
    /// </summary>
    public sealed class Player
    {
        // movement tuning
        public const float MaxSpeed = 7.2f;
        // TurnAccel leaves ~0.09 s of skid at full speed: long enough for the rig to show the brake
        const float Accel = 58f, Decel = 72f, TurnAccel = 85f, AirAccel = 44f, AirDecel = 22f;
        const float JumpVel = 13.4f, RiseGravity = 36f, FallGravity = 60f, MaxFall = 22f;
        const float CoyoteTime = 0.1f, JumpBufferTime = 0.13f;
        public const float ArenaHalf = 15.5f;
        /// <summary>Half the width of the stance: how far the feet can hang over a platform edge.</summary>
        public const float FootHalf = 0.16f;
        const float DropThroughTime = 0.18f;

        // action timing (seconds) — shared with the rig
        public const float KickWindup = 0.07f, KickContact = 0.1f, KickFollow = 0.22f, KickDuration = 0.4f;
        public const float FlickSet = 0.1f, FlickRoll = 0.26f, FlickRelease = 0.34f, FlickFollow = 0.52f, FlickDuration = 0.72f;

        // abilities
        public const float ShotCooldown = 0.45f;
        public const float FlickCooldown = 6f;
        public const float ShotSpeed = 25f;
        public const float ShotDamage = 18f;

        // air kick: the recoil pushes the player the opposite way (once per airtime — a double jump)
        const float AirKickBoost = 12.5f;
        const float BoostControlTime = 0.26f;

        // keep-ups: tap the juggle key exactly when the ball drops into the touch point. Presses are
        // judged, not buffered: tapping early swings through empty air and the ball falls, so
        // mashing the key never works. The window tightens as the streak grows.
        public enum Touch { Foot, Knee, Head }
        const float JuggleGravity = 17f;                                    // floatier than a loose ball: readable rhythm
        const float JuggleWindowEasy = 0.15f, JuggleWindowHard = 0.065f;    // ± seconds around the contact moment
        const float JugglePerfect = 0.38f;                                  // share of the window that counts as perfect
        const float JuggleIgnore = 0.12f;                                   // presses right after a touch are ignored
        public const float JuggleRecover = 0.3f;                            // stance after a drop before control returns
        const float JuggleHeal = 1f, JuggleHealPerfect = 2f, JuggleHealStreak = 4f;
        static readonly Touch[] TouchPattern = { Touch.Foot, Touch.Foot, Touch.Knee, Touch.Foot, Touch.Foot, Touch.Foot, Touch.Head, Touch.Foot };

        // power shot (standing): longer wind-up, then a straight drive that passes through every
        // monster in its path — each takes less than a normal shot
        public const float PowerWindup = 0.2f, PowerContact = 0.26f, PowerFollow = 0.4f, PowerDuration = 0.58f;
        public const float PowerCooldown = 3.5f, PowerSpeed = 36f, PowerDamage = 12f;

        // step-over (on the ground): a feint over the ball, then an invulnerable dash through
        // whatever stands in front
        public const float StepOverTime = 0.3f, DashTime = 0.2f, StepOverDuration = 0.62f;
        public const float StepOverCooldown = 3.5f;
        const float DashSpeed = 24f;   // ≈ 4.8 units in DashTime

        // bicycle kick (airborne): a backflip with a scissor kick; the ball explodes where it lands
        public const float BicycleSet = 0.14f, BicycleContact = 0.22f, BicycleDuration = 0.62f;
        public const float BicycleCooldown = 5f, BicycleSpeed = 27f, BlastDamage = 38f, BlastRadius = 2.8f;

        // slide tackle (on the ground): a low slide that sweeps monsters off their feet and ducks
        // under anything flying at chest height
        public const float TackleSlide = 0.46f, TackleDuration = 0.72f;
        public const float TackleCooldown = 4f, TackleSpeed = 16f, TackleDamage = 12f, TackleStun = 1.2f;

        // goal kick: the ball leaves the frame and comes down as a meteor where the cursor pointed
        public const float PuntWindup = 0.16f, PuntContact = 0.24f, PuntDuration = 0.5f;
        public const float PuntCooldown = 8f, PuntDamage = 55f, PuntRadius = 2.6f, PuntFlight = 0.85f;

        // free-kick wall: three ghost defenders hold a line in front of the player
        public const float WallSet = 0.22f, WallDuration = 0.46f, WallCooldown = 12f, WallLife = 4f;

        // nutmeg: the ball goes through the legs, the victim is left staggering and wide open
        public const float NutmegRun = 0.32f, NutmegDuration = 0.5f, NutmegCooldown = 6f;
        public const float NutmegDamage = 14f, NutmegExpose = 3f, NutmegReach = 3.4f;

        // decoy: a sidestep that leaves a ghost behind to soak up the attention
        public const float DecoyStep = 0.26f, DecoyDuration = 0.4f, DecoyCooldown = 7f;
        public const float DecoyDist = 3f, DecoyLife = 2.5f;

        // whistle: charged by kills, not by a timer
        public const float WhistleDuration = 0.65f, WhistleFreeze = 2f, WhistleBossFreeze = 0.8f;

        public enum Action { None, Kick, Flick, Juggle, Power, StepOver, Bicycle, Tackle, Punt, Wall, Nutmeg, Decoy, Whistle }

        public const float BaseMaxHp = 120f;
        const float DashStrikeDamage = 30f;

        public Vector2 Pos;
        public Vector2 Vel;
        public int Facing = 1;
        public bool Grounded = true;
        /// <summary>Platform index the player stands on (Level.None on the pitch or in the air).</summary>
        public int OnPlatform = Level.None;
        /// <summary>Height of the surface under the feet (the one the player stands on, or would land on).</summary>
        public float GroundY => Grounded ? Pos.y : Level.FloorBelow(Pos.x, Pos.y + 0.02f, FootHalf);
        public float MaxHp = BaseMaxHp;
        public float Hp = BaseMaxHp;

        // ---- build (upgrades and unlocked abilities)
        static RunState Run => Game.I.Run;
        static PlayerStats S => Game.I.Run.Stats;
        public float ShotCooldownTotal => ShotCooldown * S.ShotCooldownMul / Combat.AdrenalineMul;
        public float PowerCooldownTotal => PowerCooldown * S.CooldownMul;
        public float FlickCooldownTotal => FlickCooldown * S.CooldownMul;
        public float StepOverCooldownTotal => StepOverCooldown * S.CooldownMul;
        public float BicycleCooldownTotal => BicycleCooldown * S.CooldownMul;
        public float TackleCooldownTotal => TackleCooldown * S.CooldownMul;
        public float PuntCooldownTotal => PuntCooldown * S.CooldownMul;
        public float WallCooldownTotal => WallCooldown * S.CooldownMul;
        public float NutmegCooldownTotal => NutmegCooldown * S.CooldownMul;
        public float DecoyCooldownTotal => DecoyCooldown * S.CooldownMul;
        public float MaxSpeedNow => MaxSpeed * S.MoveSpeedMul * Combat.AdrenalineMul;
        /// <summary>Captain's shield: blocks one hit, then recharges.</summary>
        public int Shield;
        public float ShieldCharge;
        readonly System.Collections.Generic.HashSet<int> dashHits = new System.Collections.Generic.HashSet<int>();
        int shotCount;
        float regenAcc;
        Vector2 dashStartPos;
        public float InvulnTimer;
        public bool Dead;
        public float DeadTime;

        public float ShotCd;
        public float FlickCd;
        public float PowerCd, StepOverCd, BicycleCd;
        public float TackleCd, PuntCd, WallCd, NutmegCd, DecoyCd;
        /// <summary>Whistle charge (0..1): filled by kills, spent in one go.</summary>
        public float Ultimate;
        /// <summary>Height of the body's hit point above the feet — a slide ducks under chest-high shots.</summary>
        public float HitHeight => CurrentAction == Action.Tackle && ActionTime < TackleSlide ? 0.3f : 0.8f;
        /// <summary>The slide is running: it carries the ball and sweeps monsters over.</summary>
        public bool IsSliding => CurrentAction == Action.Tackle && ActionTime < TackleSlide;
        /// <summary>Step-over i-frames (separate from the post-hit blink).</summary>
        public float DodgeTime;
        /// <summary>The ball was at the feet when the step-over began and is carried through the dash.</summary>
        public bool StepCarry;
        /// <summary>Bicycle kick: ball position relative to the player (facing-local, not rotated with the body).</summary>
        public Vector2 BikeBallLocal;
        public bool ActionReleased => released;
        public bool IsDashing => CurrentAction == Action.StepOver && ActionTime >= StepOverTime && ActionTime < StepOverTime + DashTime;
        public Action CurrentAction;
        public float ActionTime;
        public Vector2 KickAimLocal = Vector2.right;
        public Vector2 KickBallLocal;
        public Vector2 FlickBallStartLocal;
        public Vector2 FlickTarget;

        // juggle state (read by the rig and the HUD)
        public int JuggleCount;
        public Touch NextTouch;
        public Touch LastTouch;
        public float SinceTouch = 99f;
        public bool LastWasWhiff;
        public bool JuggleLastPerfect;
        public bool JuggleDropped;
        public float JuggleDropTime;
        public Vector2 JuggleBallLocal;
        public Vector2 JuggleContactLocal;
        public float JuggleWindow { get; private set; }
        public float JuggleTimeToContact => jugTc - jugT;

        public PlayerRig Rig;
        public Ball Ball;
        Afterimages ghosts;

        float coyote, jumpBuffer, shotBuffer, flickBuffer, juggleBuffer, powerBuffer, stepBuffer, bikeBuffer;
        float tackleBuffer, puntBuffer, wallBuffer, nutmegBuffer, decoyBuffer, whistleBuffer;
        int slideDir = 1, nutmegDir = 1, decoyDir = 1;
        bool tackleBurst, wallPlaced, whistleBlown;
        Vector2 puntTarget;
        readonly System.Collections.Generic.HashSet<int> sweepHits = new System.Collections.Generic.HashSet<int>();
        int dashDir = 1;
        bool dashStarted, dashEnded;
        Vector2 bikeBallStart;
        float chargeFxTimer;
        float dropTimer;
        int dropIgnore = Level.None;
        int platformVersion;
        bool released;
        float ghostTimer;
        int ghostIndex;
        float stepDustTimer, skidDustTimer;
        int airBoosts = 1;
        float boostT, boostGhostT;
        bool boostRise;
        float jugT, jugTc, jugVy0, jugY0, jugX0, jugVx;

        public void Build(Transform parent, Ball ball)
        {
            Ball = ball;
            Rig = new PlayerRig(this);
            Rig.Build(parent);
            ghosts = new Afterimages(parent, Rig);
            Rig.Ghosts = ghosts;
        }

        public void Respawn()
        {
            Pos = new Vector2(-2f, 0f);
            Vel = Vector2.zero;
            Facing = 1;
            Grounded = true;
            OnPlatform = Level.None;
            dropTimer = 0f;
            dropIgnore = Level.None;
            ApplyStats(true);
            InvulnTimer = 0f;
            Dead = false;
            shotCount = 0;
            regenAcc = 0f;
            ShotCd = FlickCd = PowerCd = StepOverCd = BicycleCd = 0f;
            TackleCd = PuntCd = WallCd = NutmegCd = DecoyCd = 0f;
            Ultimate = 0f;
            DodgeTime = 0f;
            CurrentAction = Action.None;
            ActionTime = 0f;
            coyote = jumpBuffer = shotBuffer = flickBuffer = juggleBuffer = powerBuffer = stepBuffer = bikeBuffer = 0f;
            tackleBuffer = puntBuffer = wallBuffer = nutmegBuffer = decoyBuffer = whistleBuffer = 0f;
            airBoosts = 1;
            boostT = boostGhostT = 0f;
            boostRise = false;
            JuggleCount = 0;
            JuggleDropped = false;
            SinceTouch = 99f;
            Rig.ResetPose();
            Rig.SetVisible(true);
            ghosts.Clear();
        }

        void DecaySkillBuffers(float dt)
        {
            flickBuffer = Mathf.Max(0f, flickBuffer - dt);
            juggleBuffer = Mathf.Max(0f, juggleBuffer - dt);
            stepBuffer = Mathf.Max(0f, stepBuffer - dt);
            bikeBuffer = Mathf.Max(0f, bikeBuffer - dt);
            tackleBuffer = Mathf.Max(0f, tackleBuffer - dt);
            puntBuffer = Mathf.Max(0f, puntBuffer - dt);
            wallBuffer = Mathf.Max(0f, wallBuffer - dt);
            nutmegBuffer = Mathf.Max(0f, nutmegBuffer - dt);
            decoyBuffer = Mathf.Max(0f, decoyBuffer - dt);
            whistleBuffer = Mathf.Max(0f, whistleBuffer - dt);
        }

        /// <summary>A skill key was pressed: buffer whatever ability sits in that slot.</summary>
        void PressSkill(Ability a, int slot)
        {
            switch (a)
            {
                case Ability.Flick: flickBuffer = 0.3f; break;
                case Ability.Juggle: juggleBuffer = 0.2f; break;
                case Ability.StepOver: stepBuffer = 0.25f; break;
                case Ability.Bicycle: bikeBuffer = 0.25f; break;
                case Ability.Tackle: tackleBuffer = 0.25f; break;
                case Ability.Punt: puntBuffer = 0.3f; break;
                case Ability.Wall: wallBuffer = 0.25f; break;
                case Ability.Nutmeg: nutmegBuffer = 0.25f; break;
                case Ability.Decoy: decoyBuffer = 0.25f; break;
                case Ability.Whistle: whistleBuffer = 0.25f; break;
                default: Game.I.Hud.OnEmptySlot(slot); break;   // nothing in this slot yet
            }
        }

        /// <summary>Was the key of the slot this ability sits in pressed this frame?</summary>
        bool SkillDown(Ability a)
        {
            int slot = Run.SlotOf(a);
            return slot >= 0 && slot < GameInput.Slots && GameInput.SkillPressed[slot];
        }

        Vector2 ToLocal(Vector2 world) => new Vector2((world.x - Pos.x) * Facing, world.y - Pos.y);
        Vector2 ToWorld(Vector2 local) => Pos + new Vector2(local.x * Facing, local.y);

        // ------------------------------------------------------------------ build

        /// <summary>Re-read the build after a pick. fresh: new run (full health, full shield).</summary>
        public void ApplyStats(bool fresh)
        {
            var s = S;
            MaxHp = BaseMaxHp + s.MaxHpBonus;
            Hp = fresh ? MaxHp : Mathf.Min(Hp, MaxHp);
            if (fresh || Shield > s.ShieldCharges) Shield = s.ShieldCharges;
            if (s.ShieldCharges > 0 && Shield == 0 && fresh) Shield = 1;

        }

        public void Heal(float amount, bool popup = false)
        {
            if (Dead || amount <= 0f) return;
            float before = Hp;
            Hp = Mathf.Min(MaxHp, Hp + amount);
            float gained = Hp - before;
            if (popup && gained >= 1f)
            {
                Game.I.Hud.Popup(Pos + new Vector2(0f, 2.2f), "+" + Mathf.RoundToInt(gained), Palette.Heal, 30f, gained >= 20f);
                FxSystem.I.Motes(Pos + new Vector2(0f, 1f), Vector2.up * 1.2f, Palette.Heal, Mathf.Clamp(Mathf.RoundToInt(gained / 4f), 3, 12), 0.45f);
            }
        }

        /// <summary>Geysers and bosses throw the player into the air.</summary>
        public void Launch(float vy)
        {
            if (Dead) return;
            AbortJuggle();
            Vel.y = Mathf.Max(Vel.y, vy);
            Grounded = false;
            OnPlatform = Level.None;
            boostRise = true;
        }

        public void ReduceCooldowns(float seconds)
        {
            ShotCd = Mathf.Max(0f, ShotCd - seconds);
            FlickCd = Mathf.Max(0f, FlickCd - seconds);
            PowerCd = Mathf.Max(0f, PowerCd - seconds);
            StepOverCd = Mathf.Max(0f, StepOverCd - seconds);
            BicycleCd = Mathf.Max(0f, BicycleCd - seconds);
            TackleCd = Mathf.Max(0f, TackleCd - seconds);
            PuntCd = Mathf.Max(0f, PuntCd - seconds);
            WallCd = Mathf.Max(0f, WallCd - seconds);
            NutmegCd = Mathf.Max(0f, NutmegCd - seconds);
            DecoyCd = Mathf.Max(0f, DecoyCd - seconds);
        }

        /// <summary>Afterburner: the dash tears through monsters, each once per dash.</summary>
        public void DashStrike(Monster m)
        {
            if (!dashHits.Add(m.Id)) return;
            Combat.Hit(m, DashStrikeDamage * S.DashDamageFrac, new Vector2(dashDir, 0.5f), 8f, Src.Dash, big: true);
            FxSystem.I.Sparks(m.Center, new Vector2(dashDir, 0.2f), 60f, 8, 5f, 12f, Palette.DashMint, 2.4f, 0.05f, 0.25f);
        }

        // ------------------------------------------------------------------ update

        public void Update(float dt)
        {
            var game = Game.I;
            ShotCd = Mathf.Max(0f, ShotCd - dt);
            FlickCd = Mathf.Max(0f, FlickCd - dt);
            PowerCd = Mathf.Max(0f, PowerCd - dt);
            StepOverCd = Mathf.Max(0f, StepOverCd - dt);
            BicycleCd = Mathf.Max(0f, BicycleCd - dt);
            TackleCd = Mathf.Max(0f, TackleCd - dt);
            PuntCd = Mathf.Max(0f, PuntCd - dt);
            WallCd = Mathf.Max(0f, WallCd - dt);
            NutmegCd = Mathf.Max(0f, NutmegCd - dt);
            DecoyCd = Mathf.Max(0f, DecoyCd - dt);
            InvulnTimer = Mathf.Max(0f, InvulnTimer - dt);
            DodgeTime = Mathf.Max(0f, DodgeTime - dt);
            boostT = Mathf.Max(0f, boostT - dt);
            if (DevMode.NoCooldowns)
            {
                ShotCd = FlickCd = PowerCd = StepOverCd = BicycleCd = 0f;
                TackleCd = PuntCd = WallCd = NutmegCd = DecoyCd = 0f;
                Ultimate = 1f;
            }

            var s = S;
            var run = Run;

            // a moving platform carries whoever stands on it (a new layout invalidates the old index)
            if (Grounded && OnPlatform != Level.None)
            {
                if (platformVersion == Level.Version) Pos += Level.DeltaOf(OnPlatform);
                else { OnPlatform = Level.None; Grounded = false; }
            }

            if (!Dead)
            {
                // regeneration ticks in whole points so the number doesn't flicker
                if (s.RegenPerSec > 0f && Hp < MaxHp)
                {
                    regenAcc += s.RegenPerSec * dt;
                    if (regenAcc >= 1f) { float n = Mathf.Floor(regenAcc); regenAcc -= n; Hp = Mathf.Min(MaxHp, Hp + n); }
                }
                if (s.ShieldCharges > 0 && Shield < s.ShieldCharges)
                {
                    ShieldCharge += dt / Mathf.Max(1f, s.ShieldRecharge);
                    if (ShieldCharge >= 1f)
                    {
                        ShieldCharge = 0f;
                        Shield++;
                        FxSystem.I.Ring(FxLayer.Front, Pos + new Vector2(0f, 0.95f), 0.4f, 1.1f, 0.12f, 0.01f, 0.35f, Color.white, Palette.ShotCyan.WithAlpha(0f), 2.2f);
                    }
                }
            }

            float input = Dead ? 0f : GameInput.MoveX;

            // --- buffers
            jumpBuffer = GameInput.JumpPressed && !Dead ? JumpBufferTime : Mathf.Max(0f, jumpBuffer - dt);
            shotBuffer = GameInput.ShootPressed && !Dead ? 0.35f : Mathf.Max(0f, shotBuffer - dt);
            powerBuffer = GameInput.PowerPressed && !Dead ? 0.3f : Mathf.Max(0f, powerBuffer - dt);
            // the four skill keys play whatever the run has put into their slot
            DecaySkillBuffers(dt);
            if (!Dead)
                for (int i = 0; i < GameInput.Slots; i++)
                    if (GameInput.SkillPressed[i]) PressSkill(run.SkillAt(i), i);

            // --- start actions
            if (CurrentAction == Action.None && !Dead)
            {
                if (whistleBuffer > 0f && Ultimate >= 1f) StartWhistle();
                else if (flickBuffer > 0f && FlickCd <= 0f && Ball.IsHeld && Grounded) StartFlick();
                else if (bikeBuffer > 0f && BicycleCd <= 0f && !Grounded && TakeBall()) StartBicycle();
                else if (powerBuffer > 0f && PowerCd <= 0f && Grounded && TakeBall()) StartPower();
                else if (puntBuffer > 0f && PuntCd <= 0f && Grounded && TakeBall()) StartPunt();
                else if (stepBuffer > 0f && StepOverCd <= 0f && Grounded) StartStepOver();
                else if (tackleBuffer > 0f && TackleCd <= 0f && Grounded) StartTackle();
                else if (nutmegBuffer > 0f && NutmegCd <= 0f && Grounded) StartNutmeg();
                else if (wallBuffer > 0f && WallCd <= 0f && Grounded) StartWall();
                else if (decoyBuffer > 0f && DecoyCd <= 0f) StartDecoy();
                else if (juggleBuffer > 0f && Ball.IsHeldFree && Grounded) StartJuggle();
                else if (shotBuffer > 0f && ShotCd <= 0f)
                {
                    // one-touch: a ball that is almost home gets taken first time
                    if (!Ball.IsHeld && Ball.IsCatchable(Rig.BallHold, 2.1f * s.CatchRadiusMul)) Ball.ForceCatch(this);
                    if (Ball.IsHeld) StartKick();
                }
            }

            // --- horizontal movement
            float speedMul = 1f;
            if (CurrentAction == Action.Kick) speedMul = 0.6f;
            else if (CurrentAction == Action.Flick) speedMul = ActionTime < FlickRelease ? 0.12f : 0.65f;
            else if (CurrentAction == Action.Juggle) speedMul = 0f;
            else if (CurrentAction == Action.Power) speedMul = ActionTime < PowerContact ? 0f : 0.3f;
            else if (CurrentAction == Action.StepOver) speedMul = 0.1f;
            else if (CurrentAction == Action.Bicycle) speedMul = 0.4f;
            else if (CurrentAction == Action.Punt) speedMul = ActionTime < PuntContact ? 0f : 0.35f;
            else if (CurrentAction == Action.Wall || CurrentAction == Action.Whistle) speedMul = 0.15f;
            else if (CurrentAction == Action.Tackle || CurrentAction == Action.Nutmeg || CurrentAction == Action.Decoy) speedMul = 0.1f;
            float target = input * MaxSpeedNow * speedMul;
            float accel;
            bool turning = false;
            if (Grounded)
            {
                turning = Mathf.Abs(target) > 0.01f && Mathf.Sign(target) != Mathf.Sign(Vel.x) && Mathf.Abs(Vel.x) > 0.1f;
                accel = turning ? TurnAccel : (Mathf.Abs(target) > Mathf.Abs(Vel.x) ? Accel : Decel);
                // ice: the feet find little grip for braking and turning
                if (s.Slippery) accel *= turning ? 0.2f : Mathf.Abs(target) > Mathf.Abs(Vel.x) ? 0.55f : 0.12f;
            }
            else
            {
                accel = Mathf.Abs(target) > 0.01f ? AirAccel : AirDecel;
                if (boostT > 0f) accel *= 0.2f;   // let the recoil carry before air control takes over again
            }
            if (IsDashing) Vel.x = dashDir * DashSpeed * s.DashDistanceMul;
            // the slide starts fast and runs out of steam — on ice it keeps going much further
            else if (IsSliding) Vel.x = slideDir * TackleSpeed * (1f - 0.72f * MathUtil.EaseInQuad(ActionTime / TackleSlide)) * (s.Slippery ? 1.45f : 1f);
            else if (CurrentAction == Action.Nutmeg && ActionTime < NutmegRun) Vel.x = nutmegDir * (NutmegReach / NutmegRun);
            else if (CurrentAction == Action.Decoy && ActionTime < DecoyStep) Vel.x = decoyDir * (DecoyDist / DecoyStep);
            else Vel.x = Mathf.MoveTowards(Vel.x, target, accel * dt);

            // --- facing
            if (CurrentAction == Action.None && Mathf.Abs(input) > 0.01f) Facing = input > 0f ? 1 : -1;

            // --- drop through the platform underfoot (down, or down + jump)
            bool canMove = (CurrentAction == Action.None || CurrentAction == Action.Kick) && !Dead;
            if (Grounded && OnPlatform != Level.None && canMove && (GameInput.DownPressed || (jumpBuffer > 0f && GameInput.DownHeld)))
            {
                dropIgnore = OnPlatform;
                dropTimer = DropThroughTime;
                OnPlatform = Level.None;
                Grounded = false;
                coyote = jumpBuffer = 0f;
                Vel.y = -2.5f;
                Rig.OnJump();
                FxSystem.I.Dust(Pos, new Vector2(Vel.x * 0.1f, 0.3f), 4, 1.1f, 0.3f, 0.26f);
            }

            // --- jump
            coyote = Grounded ? CoyoteTime : Mathf.Max(0f, coyote - dt);
            if (jumpBuffer > 0f && coyote > 0f && canMove)
            {
                Vel.y = JumpVel * s.JumpMul;
                Grounded = false;
                coyote = jumpBuffer = 0f;
                Rig.OnJump();
                FxSystem.I.Dust(Pos, new Vector2(-Vel.x * 0.1f, 0f), 6, 1.6f, 0.4f, 0.3f);
            }

            // --- gravity with variable height and apex hang (a recoil boost rises like a held jump).
            // The dash is flat even off a ledge; the bicycle kick hangs in the air for the scissor.
            if (IsDashing) Vel.y = 0f;
            else if (!Grounded)
            {
                if (Vel.y <= 0f) boostRise = false;
                bool floaty = GameInput.JumpHeld || boostRise;
                float g = Vel.y > 0f ? (floaty ? RiseGravity : RiseGravity * 2.3f) : FallGravity;
                if (Mathf.Abs(Vel.y) < 1.6f && floaty) g *= 0.55f;
                if (CurrentAction == Action.Bicycle && ActionTime < BicycleContact + 0.16f) g *= 0.25f;
                g *= s.GravityMul;
                Vel.y = Mathf.Max(Vel.y - g * dt, -MaxFall * Mathf.Sqrt(s.GravityMul));
            }

            float prevY = Pos.y;
            Pos += Vel * dt;

            // --- ground and one-way platforms: a surface only catches feet that come down onto it.
            // Holding down in the air falls through every platform, a drop skips the one it left.
            dropTimer = Mathf.Max(0f, dropTimer - dt);
            int ignore = !Grounded && GameInput.DownHeld && !Dead ? Level.All : dropTimer > 0f ? dropIgnore : Level.None;
            float floor = Level.FloorBelow(Pos.x, prevY + 0.02f, FootHalf, ignore, out int floorIndex);
            if (Vel.y <= 0f && Pos.y <= floor)
            {
                if (!Grounded)
                {
                    float impact = -Vel.y;
                    Rig.OnLand(impact);
                    Vector2 at = new Vector2(Pos.x, floor);
                    FxSystem.I.Dust(at, Vector2.right, 5, 1.2f + impact * 0.08f, 0.38f, 0.32f);
                    FxSystem.I.Dust(at, Vector2.left, 5, 1.2f + impact * 0.08f, 0.38f, 0.32f);
                    if (impact > 12f) game.Cam.AddTrauma(0.08f);
                }
                Pos.y = floor;
                Vel.y = 0f;
                Grounded = true;
                OnPlatform = floorIndex;
                platformVersion = Level.Version;
                airBoosts = s.AirBoosts;
                boostRise = false;
            }
            else if (Pos.y > floor + 0.001f)
            {
                Grounded = false;   // jumped, or walked off an edge (coyote time still allows the jump)
                OnPlatform = Level.None;
            }

            // --- walls
            if (Pos.x < -ArenaHalf) { Pos.x = -ArenaHalf; Vel.x = Mathf.Max(0f, Vel.x); }
            if (Pos.x > ArenaHalf) { Pos.x = ArenaHalf; Vel.x = Mathf.Min(0f, Vel.x); }

            // --- running dust puffs, and a spray from the planted foot while skidding into a turn
            if (Grounded && Mathf.Abs(Vel.x) > MaxSpeedNow * 0.8f)
            {
                stepDustTimer -= dt;
                if (stepDustTimer <= 0f)
                {
                    stepDustTimer = 0.16f;
                    FxSystem.I.Dust(Pos + new Vector2(-Facing * 0.1f, 0f), new Vector2(-Mathf.Sign(Vel.x), 0f), 1, 1f, 0.28f, 0.22f);
                }
            }
            if (turning && Mathf.Abs(Vel.x) > 2.5f)
            {
                skidDustTimer -= dt;
                if (skidDustTimer <= 0f)
                {
                    skidDustTimer = 0.035f;
                    float side = Mathf.Sign(Vel.x);
                    FxSystem.I.Dust(Pos + new Vector2(side * 0.3f, 0f), new Vector2(side, 0.35f), 2, 1.8f, 0.3f, 0.3f);
                }
            }

            UpdateAction(dt);
        }

        // ------------------------------------------------------------------ kick

        void StartKick()
        {
            shotBuffer = 0f;
            Vector2 aim = GameInput.AimWorld - Ball.Pos;
            if (aim.sqrMagnitude < 0.01f) aim = new Vector2(Facing, 0.1f);
            if (aim.x * Facing < -0.05f) Facing = aim.x > 0f ? 1 : -1;
            KickAimLocal = ToLocal(Pos + aim.normalized);
            KickBallLocal = ToLocal(Ball.Pos);
            // keep the strike point in a natural range in front of the plant foot
            KickBallLocal = new Vector2(Mathf.Clamp(KickBallLocal.x, 0.3f, 0.58f), Mathf.Clamp(KickBallLocal.y, Art.BallRadius, 0.9f));
            CurrentAction = Action.Kick;
            ActionTime = 0f;
            released = false;
            ShotCd = ShotCooldownTotal;
            Game.I.Hud.OnShotUsed();
        }

        void ReleaseKick()
        {
            var fx = FxSystem.I;
            Vector2 from = Ball.Pos;
            Vector2 dir = GameInput.AimWorld - from;
            if (dir.sqrMagnitude < 0.01f) dir = new Vector2(Facing, 0.1f);
            dir.Normalize();
            // on the pitch a steep downward shot would only thump into the turf; from a platform
            // it may go straight down through the ledge at the monsters below
            if (Grounded && OnPlatform == Level.None && dir.y < -0.35f) dir = new Vector2(dir.x, -0.35f).normalized;
            KickAimLocal = new Vector2(dir.x * Facing, dir.y);

            var s = S;
            bool runUp = s.RunUpBonus > 0f && Grounded && Mathf.Abs(Vel.x) > MaxSpeedNow * 0.75f;
            Ball.Kick(dir * ShotSpeed * s.BallSpeedMul, Grounded ? OnPlatform : Level.None);
            Ball.ShotMul = runUp ? 1f + s.RunUpBonus : 1f;
            Ball.RicochetsLeft = s.Ricochets;
            shotCount++;
            Ball.GoldenShot = s.GoldenBoot && shotCount % 3 == 0;
            if (Ball.GoldenShot)
            {
                fx.Flash(from, 1.8f, Palette.Gold, 0.16f, 3.2f);
                fx.Sparkles(from, 0.3f, 8, Palette.Gold, 3f, 0.5f);
            }
            if (runUp) fx.Sparks(from, -dir, 40f, 5, 4f, 8f, Palette.Gold, 2.2f, 0.035f, 0.18f);

            // echo balls fan out around the real shot, alternating sides
            for (int i = 0; i < s.EchoBalls; i++)
            {
                float ang = s.EchoSpread * ((i >> 1) + 1) * (i % 2 == 0 ? 1f : -1f);
                EchoBalls.I.Fire(from, MathUtil.Rotate(dir, ang), ShotSpeed * s.BallSpeedMul * 0.95f, ShotDamage, Src.Echo, Palette.ShotCyan, false, false, s.EchoRicochet);
            }

            fx.Flash(from, 1.1f, Palette.ShotCyan, 0.12f, 3f);
            fx.Ring(FxLayer.Front, from, 0.12f, 0.85f, 0.12f, 0.01f, 0.2f, Palette.ShotCore, Palette.ShotCyan.WithAlpha(0f), 2.4f);
            fx.Sparks(from, dir, 50f, 9, 7f, 16f, Palette.ShotCyan, 2.6f, 0.05f, 0.22f);
            fx.Sparks(from, -dir, 70f, 4, 3f, 6f, Palette.ShotCore, 2f, 0.04f, 0.16f);
            if (Grounded) fx.Dust(Pos + new Vector2(Facing * 0.05f, 0f), new Vector2(-dir.x, 0f), 4, 1.4f, 0.3f, 0.28f);

            var game = Game.I;
            game.Cam.Kick(-dir * 0.16f);
            game.Cam.AddTrauma(0.16f);
            game.Post.Impact(0.22f);
            TimeFx.HitStop(0.035f, 0.05f);
            Rig.OnKickContact();

            if (!Grounded && airBoosts > 0) AirBoost(-dir * AirKickBoost);
        }

        void AirBoost(Vector2 push)
        {
            airBoosts--;
            Vel.x = push.x + Vel.x * 0.2f;
            // downward shots launch you up like a second jump, sideways shots dash with a little lift,
            // upward shots slam you down
            Vel.y = push.y >= 0f ? Mathf.Max(push.y, 3f) + Mathf.Max(Vel.y, 0f) * 0.2f : Mathf.Max(push.y, -MaxFall);
            boostT = BoostControlTime;
            boostRise = Vel.y > 0f;
            boostGhostT = 0.2f;
            ghostTimer = 0f;

            var fx = FxSystem.I;
            Vector2 n = push.normalized;
            Vector2 c = Pos + new Vector2(0f, 0.85f);
            fx.Ring(FxLayer.Front, c - n * 0.35f, 0.15f, 1.3f, 0.14f, 0.01f, 0.26f, Palette.ShotCore, Palette.ShotCyan.WithAlpha(0f), 2.2f);
            fx.Sparks(c - n * 0.4f, -n, 38f, 10, 5f, 12f, Palette.ShotCyan, 2.2f, 0.04f, 0.22f);
            for (int i = 0; i < 5; i++)
            {
                Vector2 side = new Vector2(-n.y, n.x) * Random.Range(-0.45f, 0.45f);
                fx.Streak(FxLayer.Front, c + side - n * 0.2f, -n * Random.Range(7f, 12f), Random.Range(0.18f, 0.28f), 0.035f, 0.05f,
                    Palette.ShotCore.WithAlpha(0.8f), Palette.ShotCyan.WithAlpha(0f), 2f, 6f);
            }
            var game = Game.I;
            game.Cam.Kick(n * 0.14f);
            game.Cam.AddTrauma(0.1f);
            Rig.OnAirBoost(push);
        }

        /// <summary>Ball at the feet, or close enough to take first time.</summary>
        bool TakeBall()
        {
            if (!Ball.IsHeld && Ball.IsCatchable(Rig.BallHold, 2.1f * S.CatchRadiusMul)) Ball.ForceCatch(this);
            return Ball.IsHeldFree;
        }

        Vector2 AimFrom(Vector2 from)
        {
            Vector2 dir = GameInput.AimWorld - from;
            if (dir.sqrMagnitude < 0.01f) dir = new Vector2(Facing, 0.1f);
            return dir.normalized;
        }

        // ------------------------------------------------------------------ power shot

        void StartPower()
        {
            powerBuffer = 0f;
            Vector2 aim = GameInput.AimWorld - Ball.Pos;
            if (Mathf.Abs(aim.x) > 0.05f) Facing = aim.x > 0f ? 1 : -1;
            KickAimLocal = ToLocal(Pos + aim.normalized);
            KickBallLocal = ToLocal(Ball.Pos);
            KickBallLocal = new Vector2(Mathf.Clamp(KickBallLocal.x, 0.34f, 0.56f), Art.BallRadius);
            CurrentAction = Action.Power;
            ActionTime = 0f;
            released = false;
            chargeFxTimer = 0f;
            PowerCd = PowerCooldownTotal;
            Game.I.Hud.OnSkillUsed(Ability.Power);
            Game.I.Cam.SetZoom(0.96f);
        }

        void ReleasePower()
        {
            Ball.Charge = 0f;
            Vector2 from = Ball.Pos;
            Vector2 dir = AimFrom(from);
            if (Grounded && OnPlatform == Level.None && dir.y < -0.3f) dir = new Vector2(dir.x, -0.3f).normalized;
            KickAimLocal = new Vector2(dir.x * Facing, dir.y);
            var s = S;
            Ball.Pierce(dir * PowerSpeed * s.BallSpeedMul, Grounded ? OnPlatform : Level.None, GameInput.AimWorld);
            if (s.Trident)
                for (int i = -1; i <= 1; i += 2)
                    EchoBalls.I.Fire(from, MathUtil.Rotate(dir, 11f * i), PowerSpeed * s.BallSpeedMul, PowerDamage, Src.Power, Palette.PowerGold, true);
            Combat.Maestro(this);

            var fx = FxSystem.I;
            fx.Flash(from, 2.4f, Palette.PowerGold, 0.18f, 3.2f);
            fx.Flash(from, 1.1f, Color.white, 0.08f, 3.5f);
            fx.Ring(FxLayer.Front, from, 0.15f, 1.5f, 0.24f, 0.01f, 0.28f, Color.white, Palette.PowerGold.WithAlpha(0f), 2.6f);
            fx.Ring(FxLayer.Front, from + dir * 0.5f, 0.1f, 0.9f, 0.14f, 0.01f, 0.2f, Palette.Gold, Palette.BlastOrange.WithAlpha(0f), 2.4f);
            fx.Sparks(from, dir, 28f, 16, 10f, 22f, Palette.PowerGold, 2.8f, 0.055f, 0.26f);
            fx.Sparks(from, -dir, 80f, 6, 3f, 7f, Color.white, 2.2f, 0.04f, 0.18f);
            for (int i = 0; i < 6; i++)
            {
                Vector2 side = new Vector2(-dir.y, dir.x) * Random.Range(-0.35f, 0.35f);
                fx.Streak(FxLayer.Front, from + side, dir * Random.Range(14f, 24f), Random.Range(0.14f, 0.24f), 0.04f, 0.06f,
                    Color.white.WithAlpha(0.9f), Palette.PowerGold.WithAlpha(0f), 2.4f, 4f);
            }
            if (Grounded) fx.Dust(Pos, new Vector2(-dir.x, 0.1f), 7, 2.4f, 0.4f, 0.34f);

            var game = Game.I;
            game.Cam.Kick(-dir * 0.3f);
            game.Cam.AddTrauma(0.34f);
            game.Cam.SetZoom(1f);
            game.Cam.ZoomPunch(0.04f);
            game.Post.Impact(0.5f);
            TimeFx.HitStop(0.07f, 0.04f);
            Vel.x -= dir.x * 4.5f;   // recoil slide
            Rig.OnPowerContact();
        }

        // ------------------------------------------------------------------ step-over + dash

        void StartStepOver()
        {
            stepBuffer = 0f;
            if (Mathf.Abs(GameInput.MoveX) > 0.01f) Facing = GameInput.MoveX > 0f ? 1 : -1;
            dashDir = Facing;
            CurrentAction = Action.StepOver;
            ActionTime = 0f;
            dashStarted = dashEnded = false;
            StepOverCd = StepOverCooldownTotal;
            DodgeTime = StepOverDuration + 0.08f;
            StepCarry = Ball.IsHeldFree;
            if (StepCarry) Ball.BeginScripted();
            ghostTimer = 0f;
            dashHits.Clear();
            Game.I.Hud.OnSkillUsed(Ability.StepOver);
        }

        void DashBurst()
        {
            var fx = FxSystem.I;
            Vector2 back = new Vector2(-dashDir, 0f);
            fx.Dust(Pos, new Vector2(-dashDir, 0.25f), 8, 2.8f, 0.42f, 0.36f);
            fx.Ring(FxLayer.Front, Pos + new Vector2(-dashDir * 0.2f, 0.8f), 0.2f, 1.1f, 0.12f, 0.01f, 0.22f, Color.white, Palette.DashMint.WithAlpha(0f), 2f);
            for (int i = 0; i < 6; i++)
            {
                Vector2 p = Pos + new Vector2(0f, Random.Range(0.2f, 1.7f));
                fx.Streak(FxLayer.Front, p, back * Random.Range(9f, 15f), Random.Range(0.16f, 0.26f), 0.03f, 0.07f,
                    Color.white.WithAlpha(0.85f), Palette.DashMint.WithAlpha(0f), 2f, 5f);
            }
            Game.I.Cam.Kick(new Vector2(dashDir * 0.18f, 0f));
            Game.I.Cam.AddTrauma(0.08f);
            Rig.OnDash();
            dashStartPos = Pos;
            Combat.Maestro(this);
        }

        // ------------------------------------------------------------------ bicycle kick

        void StartBicycle()
        {
            bikeBuffer = 0f;
            // back to the target: the ball goes over the head
            float dx = GameInput.AimWorld.x - Pos.x;
            if (Mathf.Abs(dx) > 0.2f) Facing = dx > 0f ? -1 : 1;
            CurrentAction = Action.Bicycle;
            ActionTime = 0f;
            released = false;
            ghostTimer = 0f;
            BicycleCd = BicycleCooldownTotal;
            bikeBallStart = ToLocal(Ball.Pos);
            BikeBallLocal = bikeBallStart;
            Ball.BeginScripted();
            Vel.y = Mathf.Max(Vel.y, 4.5f);
            boostRise = true;
            Game.I.Hud.OnSkillUsed(Ability.Bicycle);
            Game.I.Cam.SetZoom(0.95f);
        }

        void ReleaseBicycle()
        {
            Vector2 from = Ball.Pos;
            Vector2 dir = AimFrom(from);
            var s = S;
            Ball.Blast(dir * BicycleSpeed * s.BallSpeedMul);
            if (s.EchoFlip)
                for (int i = -1; i <= 1; i += 2)
                    EchoBalls.I.Fire(from, MathUtil.Rotate(dir, 18f * i), BicycleSpeed * 0.9f, BlastDamage, Src.Blast, Palette.BlastOrange, false, true);
            Combat.Maestro(this);

            var fx = FxSystem.I;
            fx.Flash(from, 1.8f, Palette.BlastOrange, 0.14f, 3f);
            fx.Ring(FxLayer.Front, from, 0.12f, 1.1f, 0.18f, 0.01f, 0.22f, Color.white, Palette.BlastOrange.WithAlpha(0f), 2.4f);
            fx.Sparks(from, dir, 40f, 12, 8f, 18f, Palette.BlastOrange, 2.6f, 0.05f, 0.24f);
            fx.Sparks(from, -dir, 90f, 5, 3f, 6f, Palette.Gold, 2f, 0.04f, 0.16f);

            var game = Game.I;
            game.Cam.Kick(-dir * 0.22f);
            game.Cam.AddTrauma(0.26f);
            game.Cam.SetZoom(1f);
            game.Post.Impact(0.4f);
            TimeFx.HitStop(0.05f, 0.05f);
            Rig.OnKickContact();
        }

        // ------------------------------------------------------------------ rainbow flick

        void StartFlick()
        {
            flickBuffer = 0f;
            float dx = GameInput.AimWorld.x - Pos.x;
            if (Mathf.Abs(dx) > 0.3f) Facing = dx > 0f ? 1 : -1;
            float dist = Mathf.Clamp(Mathf.Abs(dx), 3.2f, 8.5f);
            float tx = Mathf.Clamp(Pos.x + Facing * dist, -ArenaHalf, ArenaHalf);
            // lands on whatever surface is under the target at or below the player's level
            FlickTarget = new Vector2(tx, Level.FloorBelow(tx, Pos.y + 0.02f) + Art.BallRadius);
            FlickBallStartLocal = ToLocal(Ball.Pos);
            CurrentAction = Action.Flick;
            ActionTime = 0f;
            released = false;
            ghostTimer = 0f;
            ghostIndex = 0;
            FlickCd = FlickCooldownTotal;
            Ball.BeginScripted();
            Game.I.Cam.SetZoom(0.94f);
            Game.I.Hud.OnFlickUsed();
        }

        void ReleaseFlick()
        {
            Vector2 at = Ball.Pos;
            Ball.StartRainbow(at, FlickTarget, Facing, Pos.y);
            Combat.Maestro(this);
            // Release accent: small and crisp — the rainbow arc itself is the star of the move.
            var fx = FxSystem.I;
            fx.Flash(at, 0.85f, Color.white, 0.12f, 2f);
            fx.Ring(FxLayer.Front, at, 0.08f, 0.8f, 0.12f, 0.01f, 0.18f, Color.white, Color.white.WithAlpha(0f), 2f, true);
            fx.Sparkles(at, 0.25f, 6, Palette.Gold, 2.6f, 0.5f);
            for (int i = 0; i < 7; i++)
            {
                Color c = Art.Rainbow(i / 6f);
                fx.Sparks(at, new Vector2(-Facing * 0.3f, 1f), 70f, 1, 4f, 8f, c, 2.4f, 0.04f, 0.26f);
            }

            var game = Game.I;
            TimeFx.SlowMo(0.28f, 0.1f, 0.28f);
            game.Post.Impact(0.35f);
            game.Cam.ZoomPunch(-0.05f);
            game.Cam.SetZoom(1f);
            game.Cam.AddTrauma(0.12f);
            Rig.OnFlickRelease();
        }

        // ------------------------------------------------------------------ keep-ups

        static float ContactHeight(Touch t) => t == Touch.Head ? 2.06f : t == Touch.Knee ? 1.1f : 0.55f;
        static float ContactX(Touch t) => t == Touch.Head ? 0.13f : t == Touch.Knee ? 0.34f : 0.36f;

        static float ApexFor(Touch next) => next == Touch.Head ? Random.Range(3.05f, 3.35f)
            : next == Touch.Knee ? Random.Range(1.75f, 2.0f) : Random.Range(1.45f, 1.85f);

        void StartJuggle()
        {
            juggleBuffer = 0f;
            CurrentAction = Action.Juggle;
            ActionTime = 0f;
            JuggleCount = 0;
            JuggleDropped = false;
            JuggleDropTime = 0f;
            LastWasWhiff = false;
            JuggleLastPerfect = false;
            LastTouch = Touch.Foot;
            SinceTouch = 0f;          // the opening scoop animates like a foot touch
            JuggleWindow = JuggleWindowEasy * S.JuggleWindowMul;
            Ball.BeginScripted();
            Ball.JuggleMode = true;
            Vector2 from = ToLocal(Ball.Pos);
            LaunchJuggle(new Vector2(Mathf.Clamp(from.x, 0.25f, 0.6f), Mathf.Max(from.y, Art.BallRadius)), Touch.Foot, 1.4f);
            JuggleBallLocal = new Vector2(jugX0, jugY0);
            Ball.OnJuggleTouch(-Facing * 380f);
            Rig.OnJuggleTouch(Touch.Foot);
            Game.I.Hud.OnJuggleStart();
        }

        void LaunchJuggle(Vector2 from, Touch next, float apexY)
        {
            NextTouch = next;
            float cy = ContactHeight(next);
            apexY = Mathf.Max(apexY, Mathf.Max(from.y, cy) + 0.25f);
            jugY0 = from.y;
            jugX0 = from.x;
            jugVy0 = Mathf.Sqrt(2f * JuggleGravity * (apexY - from.y));
            jugTc = jugVy0 / JuggleGravity + Mathf.Sqrt(2f * (apexY - cy) / JuggleGravity);
            float cx = ContactX(next) + Random.Range(-0.04f, 0.04f);
            JuggleContactLocal = new Vector2(cx, cy);
            jugVx = (cx - from.x) / jugTc;
            jugT = 0f;
        }

        void UpdateJuggle(float dt)
        {
            SinceTouch += dt;
            if (JuggleDropped)
            {
                JuggleDropTime += dt;
                if (JuggleDropTime >= JuggleRecover) CurrentAction = Action.None;
                return;
            }

            jugT += dt;
            JuggleBallLocal = new Vector2(jugX0 + jugVx * jugT, jugY0 + jugVy0 * jugT - 0.5f * JuggleGravity * jugT * jugT);

            float err = jugTc - jugT;   // > 0: the ball is still above the touch point
            if (SkillDown(Ability.Juggle) && !Dead && SinceTouch > JuggleIgnore)
            {
                if (Mathf.Abs(err) <= JuggleWindow) JuggleHit(Mathf.Abs(err) <= JuggleWindow * JugglePerfect);
                else if (err > 0f) JuggleMiss(true);
            }
            else if (err < -JuggleWindow) JuggleMiss(false);
        }

        void JuggleHit(bool perfect)
        {
            JuggleCount++;
            LastTouch = NextTouch;
            LastWasWhiff = false;
            JuggleLastPerfect = perfect;
            SinceTouch = 0f;
            Vector2 at = JuggleBallLocal;
            Touch next = TouchPattern[JuggleCount % TouchPattern.Length];
            LaunchJuggle(at, next, ApexFor(next));
            JuggleWindow = Mathf.Lerp(JuggleWindowEasy, JuggleWindowHard, Mathf.Clamp01(JuggleCount / 24f)) * S.JuggleWindowMul;

            bool streak = JuggleCount % 10 == 0;
            float heal = ((perfect ? JuggleHealPerfect : JuggleHeal) + (streak ? JuggleHealStreak : 0f)) * S.JuggleHealMul;
            if (perfect && S.BulletTime) TimeFx.SlowMo(0.5f, 0.08f, 0.2f);
            float before = Hp;
            Hp = Mathf.Min(MaxHp, Hp + heal);

            Vector2 w = ToWorld(at);
            Color accent = perfect ? Palette.Gold : Palette.ShotCyan;
            var fx = FxSystem.I;
            fx.Ring(FxLayer.Front, w, 0.06f, perfect ? 0.8f : 0.55f, 0.1f, 0.01f, 0.2f, perfect ? Color.white : Palette.ShotCore, accent.WithAlpha(0f), 2.2f);
            fx.Sparks(w, Vector2.up, 70f, perfect ? 7 : 4, 3f, 7f, accent, 2.2f, 0.035f, 0.2f);
            if (Hp > before) fx.Motes(Pos + new Vector2(0f, 1.1f), Vector2.up * 0.9f, Palette.Heal, perfect ? 4 : 2, 0.35f);
            if (streak)
            {
                fx.Sparkles(w, 0.5f, 10, Palette.Gold, 2.6f, 0.6f);
                Game.I.Cam.ZoomPunch(-0.03f);
            }
            Ball.OnJuggleTouch(-Facing * (LastTouch == Touch.Head ? 260f : 540f));
            Rig.OnJuggleTouch(LastTouch);
            Game.I.Hud.OnJuggleTouch(JuggleCount, perfect, Hp - before, w);
        }

        /// <summary>The ball gets away: too early swings through empty air, too late lets it fall past.</summary>
        void JuggleMiss(bool early)
        {
            LastWasWhiff = early;
            if (early) { LastTouch = NextTouch; SinceTouch = 0f; }
            JuggleDropped = true;
            JuggleDropTime = 0f;
            Vector2 v = new Vector2(jugVx, jugVy0 - JuggleGravity * jugT);
            Ball.Drop(new Vector2(v.x * Facing, v.y));
            Game.I.Hud.OnJuggleEnd(JuggleCount, early);
        }

        void AbortJuggle()
        {
            if (CurrentAction != Action.Juggle) return;
            if (!JuggleDropped)
            {
                Ball.Drop(new Vector2(-Facing * 1.5f, 3f));
                Game.I.Hud.OnJuggleEnd(JuggleCount, false);
            }
            CurrentAction = Action.None;
        }

        // ------------------------------------------------------------------ action ticking

        void UpdateAction(float dt)
        {
            // cyan afterimages right after an air boost
            if (boostGhostT > 0f)
            {
                boostGhostT -= dt;
                ghostTimer -= dt;
                if (ghostTimer <= 0f) { ghostTimer = 0.035f; ghosts.Spawn(Palette.ShotCyan, 0.22f, 0.3f); }
            }

            if (CurrentAction == Action.None) return;
            ActionTime += dt;

            if (CurrentAction == Action.Kick)
            {
                if (!released && ActionTime >= KickContact) { released = true; ReleaseKick(); }
                if (ActionTime >= KickDuration) CurrentAction = Action.None;
            }
            else if (CurrentAction == Action.Flick)
            {
                // glide forward into the move so it never looks like a static pose
                if (ActionTime < FlickRelease)
                {
                    float g = MathUtil.Bump(ActionTime / FlickRelease) * 2.1f;
                    float nx = Mathf.Clamp(Pos.x + Facing * g * dt, -ArenaHalf, ArenaHalf);
                    // never glide off the platform in the middle of the move
                    if (OnPlatform != Level.None)
                    {
                        var p = Level.Platforms[OnPlatform];
                        if (nx < p.X0 || nx > p.X1) nx = Pos.x;
                    }
                    Pos.x = nx;
                }
                if (ActionTime > FlickSet * 0.6f && ActionTime < FlickRelease + 0.1f)
                {
                    ghostTimer -= dt;
                    if (ghostTimer <= 0f)
                    {
                        ghostTimer = 0.028f;
                        ghosts.Spawn(Art.Rainbow(Mathf.Repeat(ghostIndex * 0.13f, 1f)), 0.3f);
                        ghostIndex++;
                    }
                }
                if (!released && ActionTime >= FlickRelease) { released = true; ReleaseFlick(); }
                if (ActionTime >= FlickDuration) CurrentAction = Action.None;
            }
            else if (CurrentAction == Action.Juggle) UpdateJuggle(dt);
            else if (CurrentAction == Action.Power) UpdatePower(dt);
            else if (CurrentAction == Action.StepOver) UpdateStepOver(dt);
            else if (CurrentAction == Action.Bicycle) UpdateBicycle(dt);
            else if (CurrentAction == Action.Tackle) UpdateTackle(dt);
            else if (CurrentAction == Action.Punt) UpdatePunt(dt);
            else if (CurrentAction == Action.Wall) UpdateWall(dt);
            else if (CurrentAction == Action.Nutmeg) UpdateNutmeg(dt);
            else if (CurrentAction == Action.Decoy) UpdateDecoy(dt);
            else if (CurrentAction == Action.Whistle) UpdateWhistle(dt);
        }

        void UpdatePower(float dt)
        {
            if (!released)
            {
                // overcharge: the whole wind-up plays faster (the rig follows ActionTime)
                ActionTime += dt * (1f / Mathf.Max(0.2f, S.PowerChargeMul) - 1f);
                float charge = Mathf.Clamp01(ActionTime / PowerContact);
                Ball.Charge = charge;
                // energy gathers into the ball during the wind-up
                chargeFxTimer -= dt;
                if (chargeFxTimer <= 0f)
                {
                    chargeFxTimer = 0.03f;
                    Vector2 off = Random.insideUnitCircle.normalized * Random.Range(0.6f, 1.1f);
                    FxSystem.I.Streak(FxLayer.Front, Ball.Pos + off, -off * Random.Range(5f, 8f), 0.12f, 0.028f, 0.05f,
                        Palette.Gold.WithAlpha(0.9f), Palette.PowerGold.WithAlpha(0f), 2.4f, 2f);
                }
                if (ActionTime >= PowerContact) { released = true; ReleasePower(); }
            }
            if (ActionTime >= PowerDuration) CurrentAction = Action.None;
        }

        void UpdateStepOver(float dt)
        {
            if (IsDashing)
            {
                if (!dashStarted) { dashStarted = true; DashBurst(); }
                ghostTimer -= dt;
                if (ghostTimer <= 0f) { ghostTimer = 0.024f; ghosts.Spawn(Palette.DashMint, 0.26f, 0.34f); }
            }
            else if (dashStarted && !dashEnded)
            {
                dashEnded = true;
                Vel.x = dashDir * MaxSpeedNow;   // carry the momentum out of the dash
                // cyclone: a swirl stays behind in the middle of the dash path
                if (S.CycloneStep)
                    Vortices.I.Spawn((dashStartPos + Pos) * 0.5f + new Vector2(0f, 0.9f), 2.4f * S.AreaMul, 2f, 22f, 0f, false, Palette.DashMint);
            }
            if (ActionTime >= StepOverDuration)
            {
                CurrentAction = Action.None;
                if (StepCarry) Ball.EndScripted();
                StepCarry = false;
            }
        }

        void UpdateBicycle(float dt)
        {
            if (!released)
            {
                // the first leg pops the ball up above and behind the head, where the kicking foot meets it
                Vector2 contact = new Vector2(-0.2f, PlayerDims.StandHip + 0.66f);
                float k = Mathf.Clamp01(ActionTime / BicycleContact);
                BikeBallLocal = Vector2.Lerp(bikeBallStart, contact, MathUtil.EaseOutCubic(k)) + new Vector2(0f, 0.2f * MathUtil.Bump(k));
                if (ActionTime >= BicycleContact) { released = true; ReleaseBicycle(); }
            }
            if (ActionTime > BicycleSet * 0.5f && ActionTime < BicycleContact + 0.2f)
            {
                ghostTimer -= dt;
                if (ghostTimer <= 0f) { ghostTimer = 0.045f; ghosts.Spawn(Palette.MoonRim, 0.18f, 0.16f); }
            }
            if (ActionTime >= BicycleDuration || (released && Grounded)) CurrentAction = Action.None;
        }


        // ------------------------------------------------------------------ slide tackle

        /// <summary>Applies an effect to every monster the body sweeps through, each once per move.</summary>
        void SweepMonsters(float reachX, float reachY, System.Action<Monster> hit)
        {
            var list = Game.I.Waves.Monsters;
            Vector2 c = Pos + new Vector2(0f, reachY * 0.5f);
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                if (!m.Alive || sweepHits.Contains(m.Id)) continue;
                Vector2 d = m.Center - c;
                if (Mathf.Abs(d.x) > reachX + m.Radius || Mathf.Abs(d.y) > reachY + m.Radius) continue;
                sweepHits.Add(m.Id);
                hit(m);
            }
        }

        void StartTackle()
        {
            tackleBuffer = 0f;
            if (Mathf.Abs(GameInput.MoveX) > 0.01f) Facing = GameInput.MoveX > 0f ? 1 : -1;
            slideDir = Facing;
            CurrentAction = Action.Tackle;
            ActionTime = 0f;
            tackleBurst = false;
            sweepHits.Clear();
            TackleCd = TackleCooldownTotal;
            StepCarry = Ball.IsHeldFree;
            if (StepCarry) Ball.BeginScripted();
            Vel.x = slideDir * TackleSpeed;
            ghostTimer = 0f;
            var fx = FxSystem.I;
            fx.Dust(Pos, new Vector2(-slideDir, 0.5f), 10, 3.4f, 0.5f, 0.4f);
            fx.Ring(FxLayer.Front, Pos + new Vector2(0f, 0.2f), 0.2f, 1.6f, 0.14f, 0.01f, 0.24f, Color.white, Palette.Turf.WithAlpha(0f), 2f);
            Game.I.Cam.Kick(new Vector2(slideDir * 0.12f, 0f));
            Game.I.Hud.OnSkillUsed(Ability.Tackle);
        }

        void UpdateTackle(float dt)
        {
            if (IsSliding)
            {
                // turf sprays from under the sliding leg
                skidDustTimer -= dt;
                if (skidDustTimer <= 0f)
                {
                    skidDustTimer = 0.028f;
                    var spray = FxSystem.I;
                    spray.Dust(Pos + new Vector2(-slideDir * 0.35f, 0.05f), new Vector2(-slideDir, Random.Range(0.4f, 1.2f)), 2, 2.8f, 0.34f, 0.3f);
                    spray.Sparks(Pos + new Vector2(-slideDir * 0.3f, 0.12f), new Vector2(-slideDir, 0.9f), 40f, 1, 2.5f, 6f, Palette.Turf, 2f, 0.04f, 0.32f, 7f);
                }
                ghostTimer -= dt;
                if (ghostTimer <= 0f) { ghostTimer = 0.05f; ghosts.Spawn(Palette.Turf, 0.22f, 0.22f); }
                SweepMonsters(1.0f, 0.8f, TackleHit);
            }
            else if (!tackleBurst)
            {
                tackleBurst = true;
                Vel.x *= 0.3f;
                if (S.TackleWave) GroundWave();
            }
            if (ActionTime >= TackleDuration)
            {
                CurrentAction = Action.None;
                if (StepCarry) Ball.EndScripted();
                StepCarry = false;
            }
        }

        void TackleHit(Monster m)
        {
            Combat.Hit(m, TackleDamage * S.DamageMul, new Vector2(slideDir, 0.8f), 12f, Src.Tackle, big: true);
            m.Stun(TackleStun);
            if (S.TackleHeal > 0f) Heal(S.TackleHeal, true);
            var fx = FxSystem.I;
            fx.Dust(m.Pos, new Vector2(slideDir, 0.7f), 5, 2.6f, 0.4f, 0.32f);
            fx.Sparks(m.Center, new Vector2(slideDir, 0.5f), 60f, 6, 4f, 9f, Palette.Turf, 2.4f, 0.05f, 0.26f);
            Game.I.Cam.AddTrauma(0.12f);
        }

        /// <summary>Bodenwelle: the slide finishes with a shockwave along the ground.</summary>
        void GroundWave()
        {
            Vector2 at = Pos + new Vector2(slideDir * 0.5f, 0.15f);
            var fx = FxSystem.I;
            Combat.Explosion(at, 2.7f * S.AreaMul, 26f, Palette.Turf, Src.Tackle);
            fx.Ring(FxLayer.Front, at, 0.3f, 3f * S.AreaMul, 0.3f, 0.02f, 0.34f, Color.white, Palette.Turf.WithAlpha(0f), 2.4f);
            fx.Dust(at, Vector2.right, 8, 3.2f, 0.5f, 0.4f);
            fx.Dust(at, Vector2.left, 8, 3.2f, 0.5f, 0.4f);
            Game.I.Cam.AddTrauma(0.22f);
        }

        // ------------------------------------------------------------------ goal kick (meteor)

        void StartPunt()
        {
            puntBuffer = 0f;
            Vector2 aim = GameInput.AimWorld;
            float tx = Mathf.Clamp(aim.x, -ArenaHalf + 0.3f, ArenaHalf - 0.3f);
            float ty = Level.FloorBelow(tx, Mathf.Max(aim.y + 0.35f, GroundY + 0.05f));
            puntTarget = new Vector2(tx, ty);
            if (Mathf.Abs(tx - Pos.x) > 0.3f) Facing = tx > Pos.x ? 1 : -1;
            KickAimLocal = new Vector2(0.3f, 1f);
            KickBallLocal = ToLocal(Ball.Pos);
            KickBallLocal = new Vector2(Mathf.Clamp(KickBallLocal.x, 0.3f, 0.55f), Art.BallRadius);
            CurrentAction = Action.Punt;
            ActionTime = 0f;
            released = false;
            PuntCd = PuntCooldownTotal;
            Ball.BeginScripted();
            Game.I.Hud.OnSkillUsed(Ability.Punt);
        }

        void UpdatePunt(float dt)
        {
            if (!released && ActionTime >= PuntContact) { released = true; ReleasePunt(); }
            if (ActionTime >= PuntDuration) CurrentAction = Action.None;
        }

        void ReleasePunt()
        {
            Vector2 from = Ball.Pos;
            Ball.Punt(puntTarget, PuntFlight);
            var fx = FxSystem.I;
            fx.Flash(from, 1.4f, Palette.Amber, 0.14f, 3f);
            fx.Ring(FxLayer.Front, from, 0.12f, 1.2f, 0.16f, 0.01f, 0.22f, Color.white, Palette.Amber.WithAlpha(0f), 2.4f);
            fx.Sparks(from, Vector2.up, 34f, 12, 9f, 18f, Palette.Amber, 2.6f, 0.05f, 0.26f);
            if (Grounded) fx.Dust(Pos, new Vector2(-Facing * 0.4f, 0.2f), 7, 2.2f, 0.42f, 0.34f);
            var game = Game.I;
            game.Cam.Kick(new Vector2(0f, -0.2f));
            game.Cam.AddTrauma(0.18f);
            TimeFx.HitStop(0.04f, 0.05f);
            Rig.OnKickContact();
        }

        // ------------------------------------------------------------------ free-kick wall

        void StartWall()
        {
            wallBuffer = 0f;
            float dx = GameInput.AimWorld.x - Pos.x;
            if (Mathf.Abs(dx) > 0.3f) Facing = dx > 0f ? 1 : -1;
            CurrentAction = Action.Wall;
            ActionTime = 0f;
            wallPlaced = false;
            WallCd = WallCooldownTotal;
            Game.I.Hud.OnSkillUsed(Ability.Wall);
        }

        void UpdateWall(float dt)
        {
            if (!wallPlaced && ActionTime >= WallSet)
            {
                wallPlaced = true;
                float x = Mathf.Clamp(Pos.x + Facing * 1.8f, -ArenaHalf + 0.6f, ArenaHalf - 0.6f);
                Barrier.I.Spawn(x, GroundY, WallLife + S.WallLifeBonus);
            }
            if (ActionTime >= WallDuration) CurrentAction = Action.None;
        }

        // ------------------------------------------------------------------ nutmeg

        void StartNutmeg()
        {
            nutmegBuffer = 0f;
            if (Mathf.Abs(GameInput.MoveX) > 0.01f) Facing = GameInput.MoveX > 0f ? 1 : -1;
            else { float dx = GameInput.AimWorld.x - Pos.x; if (Mathf.Abs(dx) > 0.3f) Facing = dx > 0f ? 1 : -1; }
            nutmegDir = Facing;
            CurrentAction = Action.Nutmeg;
            ActionTime = 0f;
            sweepHits.Clear();
            NutmegCd = NutmegCooldownTotal;
            DodgeTime = NutmegRun + 0.12f;
            StepCarry = Ball.IsHeldFree;
            if (StepCarry) Ball.BeginScripted();
            ghostTimer = 0f;
            Game.I.Hud.OnSkillUsed(Ability.Nutmeg);
        }

        void UpdateNutmeg(float dt)
        {
            if (ActionTime < NutmegRun)
            {
                ghostTimer -= dt;
                if (ghostTimer <= 0f) { ghostTimer = 0.03f; ghosts.Spawn(Palette.Showboat, 0.24f, 0.3f); }
                SweepMonsters(0.9f, 1.1f, Nutmegged);
            }
            if (ActionTime >= NutmegDuration)
            {
                CurrentAction = Action.None;
                if (StepCarry) Ball.EndScripted();
                StepCarry = false;
            }
        }

        /// <summary>Through the legs: the victim staggers and takes more from everything for a while.</summary>
        void Nutmegged(Monster m)
        {
            var s = S;
            m.Expose(NutmegExpose);
            m.Stun(0.7f);
            Combat.Hit(m, NutmegDamage * s.DamageMul, new Vector2(nutmegDir, 0.2f), 4f, Src.Nutmeg);
            if (s.NutmegRefund > 0f) ReduceCooldowns(s.NutmegRefund);
            var fx = FxSystem.I;
            if (s.NutmegSpread)
            {
                var o = Combat.NearestTo(m.Center, 3.6f, m);
                if (o != null)
                {
                    o.Expose(NutmegExpose);
                    fx.Ring(FxLayer.Front, o.Center, 0.2f, o.Radius * 2.4f, 0.14f, 0.01f, 0.3f, Palette.Showboat, Palette.Showboat.WithAlpha(0f), 2.2f);
                }
            }
            fx.Ring(FxLayer.Front, m.Center, 0.2f, m.Radius * 2.8f, 0.2f, 0.02f, 0.4f, Color.white, Palette.Showboat.WithAlpha(0f), 2.6f);
            fx.Sparkles(m.Center, m.Radius + 0.4f, 10, Palette.Showboat, 2.8f, 0.7f);
            TimeFx.SlowMo(0.4f, 0.05f, 0.2f);
            Game.I.Cam.AddTrauma(0.1f);
            Game.I.Hud.Popup(m.Center + new Vector2(0f, m.Radius + 0.5f), "TUNNEL", Palette.Showboat, 28f, false);
        }

        // ------------------------------------------------------------------ decoy

        void StartDecoy()
        {
            decoyBuffer = 0f;
            // sidestep away from the facing, so the ghost stays between you and the fight
            decoyDir = Mathf.Abs(GameInput.MoveX) > 0.01f ? (GameInput.MoveX > 0f ? 1 : -1) : -Facing;
            CurrentAction = Action.Decoy;
            ActionTime = 0f;
            DecoyCd = DecoyCooldownTotal;
            DodgeTime = DecoyStep + 0.14f;
            Decoys.I.Spawn(this, DecoyLife, S.DecoyCount);
            ghostTimer = 0f;
            Game.I.Hud.OnSkillUsed(Ability.Decoy);
        }

        void UpdateDecoy(float dt)
        {
            if (ActionTime < DecoyStep)
            {
                ghostTimer -= dt;
                if (ghostTimer <= 0f) { ghostTimer = 0.03f; ghosts.Spawn(Palette.Trick, 0.26f, 0.35f); }
            }
            if (ActionTime >= DecoyDuration) CurrentAction = Action.None;
        }

        // ------------------------------------------------------------------ whistle

        /// <summary>Kills fill the whistle; elites and bosses fill much more.</summary>
        public void AddUltimate(float amount)
        {
            if (!Run.Has(Ability.Whistle) || Dead) return;
            bool wasFull = Ultimate >= 1f;
            Ultimate = Mathf.Clamp01(Ultimate + amount);
            if (!wasFull && Ultimate >= 1f) Game.I.Hud.OnWhistleReady();
        }

        void StartWhistle()
        {
            whistleBuffer = 0f;
            CurrentAction = Action.Whistle;
            ActionTime = 0f;
            whistleBlown = false;
            Ultimate = 0f;
            Game.I.Hud.OnSkillUsed(Ability.Whistle);
        }

        void UpdateWhistle(float dt)
        {
            if (!whistleBlown && ActionTime >= 0.16f) { whistleBlown = true; BlowWhistle(); }
            if (ActionTime >= WhistleDuration) CurrentAction = Action.None;
        }

        void BlowWhistle()
        {
            var s = S;
            float freeze = WhistleFreeze + s.WhistleBonus;
            Monster card = null;
            var list = Game.I.Waves.Monsters;
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                if (!m.Alive) continue;
                m.Stun(m.Rank == Rank.Boss ? WhistleBossFreeze + s.WhistleBonus * 0.4f : freeze);
                if (s.RedCard && card == null && m.Rank == Rank.Normal) card = m;
            }
            EnemyProjectiles.I.PopAll();

            var fx = FxSystem.I;
            var game = Game.I;
            Vector2 c = Pos + new Vector2(0f, 1.6f);
            for (int i = 0; i < 3; i++)
                fx.Ring(FxLayer.Front, c, 0.4f + i * 0.5f, 15f + i * 4f, 0.26f, 0.015f, 0.5f + i * 0.12f, i == 0 ? Color.white : Palette.Silver, Palette.Silver.WithAlpha(0f), 2.2f);
            fx.Flash(c, 3.4f, Palette.Silver, 0.2f, 3f);
            fx.Sparkles(c, 1.6f, 16, Color.white, 3f, 0.7f);
            TimeFx.SlowMo(0.22f, 0.22f, 0.5f);
            game.Cam.AddTrauma(0.3f);
            game.Post.Impact(0.7f);
            game.Hud.ShowToast(card != null ? "ABPFIFF  ·  ROTE KARTE" : "ABPFIFF  ·  SPIEL UNTERBROCHEN");

            if (card != null)
            {
                Vector2 at = card.Center;
                fx.Flash(at, 2.2f, Palette.Hurt, 0.2f, 3f);
                fx.Sparkles(at, 1f, 10, Palette.Hurt, 2.8f, 0.6f);
                Combat.Hit(card, card.Hp + 1f, Vector2.up, 6f, Src.Whistle, big: true);
            }
            Rig.OnWhistle();
        }
        /// <summary>Called after the rig pose is computed so the scripted ball follows the leg.</summary>
        public void LateVisuals(float dt)
        {
            ghosts.Update(dt);
        }

        // ------------------------------------------------------------------ damage

        /// <summary>Returns true if the hit connected (shield blocks count).</summary>
        public bool TakeDamage(float amount, Vector2 from)
        {
            if (Dead || InvulnTimer > 0f || DodgeTime > 0f || DevMode.God) return false;
            var game = Game.I;
            var s = S;
            float dir = Mathf.Sign(Pos.x - from.x);
            if (dir == 0f) dir = -Facing;

            // captain's shield soaks the whole hit
            if (Shield > 0)
            {
                Shield--;
                ShieldCharge = 0f;
                InvulnTimer = 0.6f;
                Vel.x += dir * 4f;
                var fx = FxSystem.I;
                Vector2 c = Pos + new Vector2(0f, 0.95f);
                fx.Ring(FxLayer.Front, c, 0.6f, 1.6f, 0.2f, 0.01f, 0.3f, Color.white, Palette.ShotCyan.WithAlpha(0f), 2.6f);
                fx.Sparks(c, new Vector2(-dir, 0.3f), 120f, 12, 4f, 9f, Palette.ShotCyan, 2.4f, 0.04f, 0.25f);
                game.Cam.AddTrauma(0.15f);
                game.Hud.ShowToast("SCHILD BLOCKT");
                return true;
            }

            AbortJuggle();
            amount *= 1f - Mathf.Min(0.6f, s.Armor);
            Hp = Mathf.Max(0f, Hp - amount);
            InvulnTimer = 1.3f + s.InvulnBonus;
            Vel = new Vector2(dir * 7.5f, Grounded ? 6.5f : Mathf.Max(Vel.y, 4f));
            Grounded = false;
            Rig.Flash(0.09f);
            game.Post.Hurt();
            game.Cam.AddTrauma(0.38f);
            game.Cam.Kick(new Vector2(dir * 0.25f, 0.1f));
            TimeFx.HitStop(0.07f, 0.03f);
            FxSystem.I.Sparks(Pos + new Vector2(0f, 1f), new Vector2(dir, 0.4f), 80f, 10, 5f, 11f, Palette.Hurt, 2.4f, 0.05f, 0.3f);
            FxSystem.I.Flash(Pos + new Vector2(0f, 1f), 1.8f, Palette.Hurt, 0.16f, 2.2f);
            game.Hud.OnPlayerDamaged(amount);
            if ((CurrentAction == Action.Flick || CurrentAction == Action.Bicycle) && !released) { CurrentAction = Action.None; Ball.Release(); game.Cam.SetZoom(1f); }
            if (CurrentAction == Action.Power && !released) { CurrentAction = Action.None; Ball.Charge = 0f; game.Cam.SetZoom(1f); }
            if (CurrentAction == Action.Punt && !released) { CurrentAction = Action.None; Ball.Release(); }
            // a hit ends a slide or a placement on the spot; the ball simply drops back out
            if (CurrentAction == Action.Tackle || CurrentAction == Action.Nutmeg || CurrentAction == Action.Wall || CurrentAction == Action.Whistle)
            {
                CurrentAction = Action.None;
                if (StepCarry) { Ball.EndScripted(); StepCarry = false; }
            }

            if (Hp <= 0f)
            {
                if (s.Revives > 0) Revive();
                else Die();
                return true;
            }
            if (s.CounterStomp > 0f) Combat.Stomp(this, s.CounterStomp);
            return true;
        }

        /// <summary>Phoenix: back on your feet at half health in a burst of fire.</summary>
        void Revive()
        {
            var run = Run;
            run.RevivesUsed++;
            run.Rebuild();
            Hp = MaxHp * 0.5f;
            InvulnTimer = 2.2f;
            var fx = FxSystem.I;
            Vector2 c = Pos + new Vector2(0f, 1f);
            fx.Flash(c, 7f, Palette.BlastOrange, 0.4f, 3f);
            fx.Flash(c, 3f, Color.white, 0.15f, 3.4f);
            fx.Ring(FxLayer.Front, c, 0.3f, 5f, 0.6f, 0.03f, 0.6f, Color.white, Palette.BlastOrange.WithAlpha(0f), 2.8f);
            for (int i = 0; i < 24; i++)
            {
                float ang = Random.Range(20f, 160f);
                Color col = Color.Lerp(Palette.Gold, Palette.BlastOrange, Random.value);
                fx.Streak(FxLayer.Front, c, MathUtil.Dir(ang) * Random.Range(6f, 14f), Random.Range(0.4f, 0.7f), 0.08f, 0.05f,
                    Color.Lerp(col, Color.white, 0.4f), col.WithAlpha(0f), 2.8f, 2.5f, -3f);
            }
            Combat.Explosion(c, 4f, 80f, Palette.BlastOrange);
            TimeFx.SlowMo(0.25f, 0.35f, 0.6f);
            Game.I.Cam.AddTrauma(0.6f);
            Game.I.Post.Impact(0.9f);
            Game.I.Hud.ShowToast("PHÖNIX  ·  WIEDERBELEBT");
        }

        void Die()
        {
            Dead = true;
            DeadTime = 0f;
            CurrentAction = Action.None;
            Game.I.Director.OnPlayerDied();
            TimeFx.SlowMo(0.2f, 0.4f, 0.9f);
            var fx = FxSystem.I;
            Vector2 c = Pos + new Vector2(0f, 1f);
            fx.Flash(c, 4f, Palette.Hurt, 0.4f, 3f);
            fx.Ring(FxLayer.Front, c, 0.3f, 3.5f, 0.4f, 0.02f, 0.7f, Palette.Hurt, Palette.Hurt.WithAlpha(0f), 2.5f);
            fx.Sparks(c, Vector2.up, 360f, 30, 4f, 12f, Palette.Hurt, 2.5f, 0.06f, 0.5f, 3f);
            Game.I.Cam.AddTrauma(0.6f);
            Game.I.Post.Impact(0.8f);
        }
    }
}
