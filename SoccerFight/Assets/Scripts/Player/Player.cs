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
        public const float ShotDamage = 14f;

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

        public enum Action { None, Kick, Flick, Juggle }

        public Vector2 Pos;
        public Vector2 Vel;
        public int Facing = 1;
        public bool Grounded = true;
        /// <summary>Platform index the player stands on (Level.None on the pitch or in the air).</summary>
        public int OnPlatform = Level.None;
        /// <summary>Height of the surface under the feet (the one the player stands on, or would land on).</summary>
        public float GroundY => Grounded ? Pos.y : Level.FloorBelow(Pos.x, Pos.y + 0.02f, FootHalf);
        public float MaxHp = 100f;
        public float Hp = 100f;
        public float InvulnTimer;
        public bool Dead;
        public float DeadTime;

        public float ShotCd;
        public float FlickCd;
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

        float coyote, jumpBuffer, shotBuffer, flickBuffer, juggleBuffer;
        float dropTimer;
        int dropIgnore = Level.None;
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
            Hp = MaxHp;
            InvulnTimer = 0f;
            Dead = false;
            ShotCd = FlickCd = 0f;
            CurrentAction = Action.None;
            ActionTime = 0f;
            coyote = jumpBuffer = shotBuffer = flickBuffer = juggleBuffer = 0f;
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

        Vector2 ToLocal(Vector2 world) => new Vector2((world.x - Pos.x) * Facing, world.y - Pos.y);
        Vector2 ToWorld(Vector2 local) => Pos + new Vector2(local.x * Facing, local.y);

        // ------------------------------------------------------------------ update

        public void Update(float dt)
        {
            var game = Game.I;
            ShotCd = Mathf.Max(0f, ShotCd - dt);
            FlickCd = Mathf.Max(0f, FlickCd - dt);
            InvulnTimer = Mathf.Max(0f, InvulnTimer - dt);
            boostT = Mathf.Max(0f, boostT - dt);

            float input = Dead ? 0f : GameInput.MoveX;

            // --- buffers
            jumpBuffer = GameInput.JumpPressed && !Dead ? JumpBufferTime : Mathf.Max(0f, jumpBuffer - dt);
            shotBuffer = GameInput.ShootPressed && !Dead ? 0.35f : Mathf.Max(0f, shotBuffer - dt);
            flickBuffer = GameInput.FlickPressed && !Dead ? 0.3f : Mathf.Max(0f, flickBuffer - dt);
            juggleBuffer = GameInput.JugglePressed && !Dead ? 0.2f : Mathf.Max(0f, juggleBuffer - dt);

            // --- start actions
            if (CurrentAction == Action.None && !Dead)
            {
                if (flickBuffer > 0f && FlickCd <= 0f && Ball.IsHeld && Grounded) StartFlick();
                else if (juggleBuffer > 0f && Ball.IsHeldFree && Grounded) StartJuggle();
                else if (shotBuffer > 0f && ShotCd <= 0f)
                {
                    // one-touch: a ball that is almost home gets taken first time
                    if (!Ball.IsHeld && Ball.IsCatchable(Rig.BallHold, 1.6f)) Ball.ForceCatch(this);
                    if (Ball.IsHeld) StartKick();
                }
            }

            // --- horizontal movement
            float speedMul = 1f;
            if (CurrentAction == Action.Kick) speedMul = 0.6f;
            else if (CurrentAction == Action.Flick) speedMul = ActionTime < FlickRelease ? 0.12f : 0.65f;
            else if (CurrentAction == Action.Juggle) speedMul = 0f;
            float target = input * MaxSpeed * speedMul;
            float accel;
            bool turning = false;
            if (Grounded)
            {
                turning = Mathf.Abs(target) > 0.01f && Mathf.Sign(target) != Mathf.Sign(Vel.x) && Mathf.Abs(Vel.x) > 0.1f;
                accel = turning ? TurnAccel : (Mathf.Abs(target) > Mathf.Abs(Vel.x) ? Accel : Decel);
            }
            else
            {
                accel = Mathf.Abs(target) > 0.01f ? AirAccel : AirDecel;
                if (boostT > 0f) accel *= 0.2f;   // let the recoil carry before air control takes over again
            }
            Vel.x = Mathf.MoveTowards(Vel.x, target, accel * dt);

            // --- facing
            if (CurrentAction == Action.None && Mathf.Abs(input) > 0.01f) Facing = input > 0f ? 1 : -1;

            // --- drop through the platform underfoot (down, or down + jump)
            bool canMove = CurrentAction != Action.Flick && CurrentAction != Action.Juggle && !Dead;
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
                Vel.y = JumpVel;
                Grounded = false;
                coyote = jumpBuffer = 0f;
                Rig.OnJump();
                FxSystem.I.Dust(Pos, new Vector2(-Vel.x * 0.1f, 0f), 6, 1.6f, 0.4f, 0.3f);
            }

            // --- gravity with variable height and apex hang (a recoil boost rises like a held jump)
            if (!Grounded)
            {
                if (Vel.y <= 0f) boostRise = false;
                bool floaty = GameInput.JumpHeld || boostRise;
                float g = Vel.y > 0f ? (floaty ? RiseGravity : RiseGravity * 2.3f) : FallGravity;
                if (Mathf.Abs(Vel.y) < 1.6f && floaty) g *= 0.55f;
                Vel.y = Mathf.Max(Vel.y - g * dt, -MaxFall);
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
                airBoosts = 1;
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
            if (Grounded && Mathf.Abs(Vel.x) > MaxSpeed * 0.8f)
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
                    float s = Mathf.Sign(Vel.x);
                    FxSystem.I.Dust(Pos + new Vector2(s * 0.3f, 0f), new Vector2(s, 0.35f), 2, 1.8f, 0.3f, 0.3f);
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
            ShotCd = ShotCooldown;
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

            Ball.Kick(dir * ShotSpeed, Grounded ? OnPlatform : Level.None);

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
            FlickCd = FlickCooldown;
            Ball.BeginScripted();
            Game.I.Cam.SetZoom(0.94f);
            Game.I.Hud.OnFlickUsed();
        }

        void ReleaseFlick()
        {
            Vector2 at = Ball.Pos;
            Ball.StartRainbow(at, FlickTarget, Facing, Pos.y);
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
            JuggleWindow = JuggleWindowEasy;
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
            if (GameInput.JugglePressed && !Dead && SinceTouch > JuggleIgnore)
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
            JuggleWindow = Mathf.Lerp(JuggleWindowEasy, JuggleWindowHard, Mathf.Clamp01(JuggleCount / 24f));

            bool streak = JuggleCount % 10 == 0;
            float heal = (perfect ? JuggleHealPerfect : JuggleHeal) + (streak ? JuggleHealStreak : 0f);
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
        }

        /// <summary>Called after the rig pose is computed so the scripted ball follows the leg.</summary>
        public void LateVisuals(float dt)
        {
            ghosts.Update(dt);
        }

        // ------------------------------------------------------------------ damage

        public void TakeDamage(float amount, Vector2 from)
        {
            if (Dead || InvulnTimer > 0f) return;
            var game = Game.I;
            AbortJuggle();
            Hp = Mathf.Max(0f, Hp - amount);
            InvulnTimer = 1.0f;
            float dir = Mathf.Sign(Pos.x - from.x);
            if (dir == 0f) dir = -Facing;
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
            if (CurrentAction == Action.Flick && !released) { CurrentAction = Action.None; Ball.Release(); game.Cam.SetZoom(1f); }

            if (Hp <= 0f) Die();
        }

        void Die()
        {
            Dead = true;
            DeadTime = 0f;
            CurrentAction = Action.None;
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
