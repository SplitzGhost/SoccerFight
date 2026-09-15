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
        const float Accel = 58f, Decel = 72f, TurnAccel = 125f, AirAccel = 44f, AirDecel = 22f;
        const float JumpVel = 13.4f, RiseGravity = 36f, FallGravity = 60f, MaxFall = 22f;
        const float CoyoteTime = 0.1f, JumpBufferTime = 0.13f;
        public const float ArenaHalf = 15.5f;

        // action timing (seconds) — shared with the rig
        public const float KickWindup = 0.07f, KickContact = 0.1f, KickFollow = 0.22f, KickDuration = 0.4f;
        public const float FlickSet = 0.1f, FlickRoll = 0.26f, FlickRelease = 0.34f, FlickFollow = 0.52f, FlickDuration = 0.72f;

        // abilities
        public const float ShotCooldown = 0.45f;
        public const float FlickCooldown = 6f;
        public const float ShotSpeed = 25f;
        public const float ShotDamage = 14f;

        public enum Action { None, Kick, Flick }

        public Vector2 Pos;
        public Vector2 Vel;
        public int Facing = 1;
        public bool Grounded = true;
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

        public PlayerRig Rig;
        public Ball Ball;
        Afterimages ghosts;

        float coyote, jumpBuffer, shotBuffer, flickBuffer;
        bool released;
        float ghostTimer;
        int ghostIndex;
        float stepDustTimer;

        public void Build(Transform parent, Ball ball)
        {
            Ball = ball;
            Rig = new PlayerRig(this);
            Rig.Build(parent);
            ghosts = new Afterimages(parent, Rig);
        }

        public void Respawn()
        {
            Pos = new Vector2(-2f, 0f);
            Vel = Vector2.zero;
            Facing = 1;
            Grounded = true;
            Hp = MaxHp;
            InvulnTimer = 0f;
            Dead = false;
            ShotCd = FlickCd = 0f;
            CurrentAction = Action.None;
            ActionTime = 0f;
            coyote = jumpBuffer = shotBuffer = flickBuffer = 0f;
            Rig.ResetPose();
            Rig.SetVisible(true);
            ghosts.Clear();
        }

        Vector2 ToLocal(Vector2 world) => new Vector2((world.x - Pos.x) * Facing, world.y - Pos.y);

        // ------------------------------------------------------------------ update

        public void Update(float dt)
        {
            var game = Game.I;
            ShotCd = Mathf.Max(0f, ShotCd - dt);
            FlickCd = Mathf.Max(0f, FlickCd - dt);
            InvulnTimer = Mathf.Max(0f, InvulnTimer - dt);

            float input = Dead ? 0f : GameInput.MoveX;

            // --- buffers
            jumpBuffer = GameInput.JumpPressed && !Dead ? JumpBufferTime : Mathf.Max(0f, jumpBuffer - dt);
            shotBuffer = GameInput.ShootPressed && !Dead ? 0.35f : Mathf.Max(0f, shotBuffer - dt);
            flickBuffer = GameInput.FlickPressed && !Dead ? 0.3f : Mathf.Max(0f, flickBuffer - dt);

            // --- start actions
            if (CurrentAction == Action.None && !Dead)
            {
                if (flickBuffer > 0f && FlickCd <= 0f && Ball.IsHeld && Grounded) StartFlick();
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
            float target = input * MaxSpeed * speedMul;
            float accel;
            if (Grounded)
            {
                bool turning = Mathf.Abs(target) > 0.01f && Mathf.Sign(target) != Mathf.Sign(Vel.x) && Mathf.Abs(Vel.x) > 0.1f;
                accel = turning ? TurnAccel : (Mathf.Abs(target) > Mathf.Abs(Vel.x) ? Accel : Decel);
            }
            else accel = Mathf.Abs(target) > 0.01f ? AirAccel : AirDecel;
            Vel.x = Mathf.MoveTowards(Vel.x, target, accel * dt);

            // --- facing
            if (CurrentAction == Action.None && Mathf.Abs(input) > 0.01f) Facing = input > 0f ? 1 : -1;

            // --- jump
            coyote = Grounded ? CoyoteTime : Mathf.Max(0f, coyote - dt);
            if (jumpBuffer > 0f && coyote > 0f && CurrentAction != Action.Flick && !Dead)
            {
                Vel.y = JumpVel;
                Grounded = false;
                coyote = jumpBuffer = 0f;
                Rig.OnJump();
                FxSystem.I.Dust(Pos, new Vector2(-Vel.x * 0.1f, 0f), 6, 1.6f, 0.4f, 0.3f);
            }

            // --- gravity with variable height and apex hang
            if (!Grounded)
            {
                float g = Vel.y > 0f ? (GameInput.JumpHeld ? RiseGravity : RiseGravity * 2.3f) : FallGravity;
                if (Mathf.Abs(Vel.y) < 1.6f && GameInput.JumpHeld) g *= 0.55f;
                Vel.y = Mathf.Max(Vel.y - g * dt, -MaxFall);
            }

            Pos += Vel * dt;

            // --- ground
            if (Pos.y <= 0f)
            {
                if (!Grounded)
                {
                    float impact = -Vel.y;
                    Rig.OnLand(impact);
                    FxSystem.I.Dust(Pos, Vector2.right, 5, 1.2f + impact * 0.08f, 0.38f, 0.32f);
                    FxSystem.I.Dust(Pos, Vector2.left, 5, 1.2f + impact * 0.08f, 0.38f, 0.32f);
                    if (impact > 12f) game.Cam.AddTrauma(0.08f);
                }
                Pos.y = 0f;
                Vel.y = 0f;
                Grounded = true;
            }
            else if (Pos.y > 0.001f) Grounded = false;

            // --- walls
            if (Pos.x < -ArenaHalf) { Pos.x = -ArenaHalf; Vel.x = Mathf.Max(0f, Vel.x); }
            if (Pos.x > ArenaHalf) { Pos.x = ArenaHalf; Vel.x = Mathf.Min(0f, Vel.x); }

            // --- running dust puffs
            if (Grounded && Mathf.Abs(Vel.x) > MaxSpeed * 0.8f)
            {
                stepDustTimer -= dt;
                if (stepDustTimer <= 0f)
                {
                    stepDustTimer = 0.16f;
                    FxSystem.I.Dust(Pos + new Vector2(-Facing * 0.1f, 0f), new Vector2(-Mathf.Sign(Vel.x), 0f), 1, 1f, 0.28f, 0.22f);
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
            if (Grounded && dir.y < -0.35f) dir = new Vector2(dir.x, -0.35f).normalized;
            KickAimLocal = new Vector2(dir.x * Facing, dir.y);

            Ball.Kick(dir * ShotSpeed);

            fx.Flash(from, 1.1f, Palette.ShotCyan, 0.12f, 3f);
            fx.Ring(FxLayer.Front, from, 0.12f, 0.85f, 0.12f, 0.01f, 0.2f, Palette.ShotCore, Palette.ShotCyan.WithAlpha(0f), 2.4f);
            fx.Sparks(from, dir, 50f, 9, 7f, 16f, Palette.ShotCyan, 2.6f, 0.05f, 0.22f);
            fx.Sparks(from, -dir, 70f, 4, 3f, 6f, Palette.ShotCore, 2f, 0.04f, 0.16f);
            fx.Dust(Pos + new Vector2(Facing * 0.05f, 0f), new Vector2(-dir.x, 0f), 4, 1.4f, 0.3f, 0.28f);

            var game = Game.I;
            game.Cam.Kick(-dir * 0.16f);
            game.Cam.AddTrauma(0.16f);
            game.Post.Impact(0.22f);
            TimeFx.HitStop(0.035f, 0.05f);
            Rig.OnKickContact();
        }

        // ------------------------------------------------------------------ rainbow flick

        void StartFlick()
        {
            flickBuffer = 0f;
            float dx = GameInput.AimWorld.x - Pos.x;
            if (Mathf.Abs(dx) > 0.3f) Facing = dx > 0f ? 1 : -1;
            float dist = Mathf.Clamp(Mathf.Abs(dx), 3.2f, 8.5f);
            FlickTarget = new Vector2(Mathf.Clamp(Pos.x + Facing * dist, -ArenaHalf, ArenaHalf), Art.BallRadius);
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
            Ball.StartRainbow(at, FlickTarget, Facing);
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

        // ------------------------------------------------------------------ action ticking

        void UpdateAction(float dt)
        {
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
                    Pos.x = Mathf.Clamp(Pos.x + Facing * g * dt, -ArenaHalf, ArenaHalf);
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
