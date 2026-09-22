using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Everything the partner's screen needs to draw a player: where the body is, what move it plays
    /// and how far in, the key points the rig poses the move around, and the ball. Sent ~30 times a
    /// second on the unreliable lane; the receiver plays it back a little in the past, blending
    /// between the two packets around that moment.
    /// </summary>
    public struct PlayerNet
    {
        public float T;
        public Vector2 Pos, Vel;
        public int Facing, OnPlatform, Character, Shield;
        public bool Grounded, Dead, Released, StepCarry, JuggleDropped, LastWasWhiff, JuggleLastPerfect;
        public Player.Action Action;
        public float ActionTime;
        public Vector2 KickAim, KickBall, FlickStart, FlickTarget, BikeBall, HeaderBall, JugBall, JugContact;
        public Player.Touch NextTouch, LastTouch;
        public float SinceTouch, JugDropTime, JugTtc;
        public float Invuln, Hp, MaxHp, DownLeft;
        public Ball.State BallSt;
        public Vector2 BallPos, BallVel, MeteorTarget;
        public float Charge, BallSize;
        public bool JuggleMode, MeteorFalling;

        public void Write(NetWriter w)
        {
            w.Float(T);
            w.Pos(Pos); w.Pos(Vel);
            w.SByte((sbyte)Facing); w.SByte((sbyte)Mathf.Clamp(OnPlatform, -100, 100));
            w.Byte((byte)Character); w.Byte((byte)Shield);
            int flags = (Grounded ? 1 : 0) | (Dead ? 2 : 0) | (Released ? 4 : 0) | (StepCarry ? 8 : 0)
                      | (JuggleDropped ? 16 : 0) | (LastWasWhiff ? 32 : 0) | (JuggleLastPerfect ? 64 : 0);
            w.Byte((byte)flags);
            w.Byte((byte)Action); w.Float(ActionTime);
            w.Pos(KickAim); w.Pos(KickBall); w.Pos(FlickStart); w.Pos(FlickTarget);
            w.Pos(BikeBall); w.Pos(HeaderBall); w.Pos(JugBall); w.Pos(JugContact);
            w.Byte((byte)NextTouch); w.Byte((byte)LastTouch);
            w.Float(SinceTouch); w.Float(JugDropTime); w.Float(JugTtc);
            w.Float(Invuln); w.Float(Hp); w.Float(MaxHp); w.Float(DownLeft);
            w.Byte((byte)BallSt); w.Pos(BallPos); w.Pos(BallVel); w.Pos(MeteorTarget);
            w.Unit(Charge); w.Unit(BallSize * 0.5f);
            w.Byte((byte)((JuggleMode ? 1 : 0) | (MeteorFalling ? 2 : 0)));
        }

        public static PlayerNet Read(NetReader r)
        {
            var s = new PlayerNet();
            s.T = r.Float();
            s.Pos = r.Pos(); s.Vel = r.Pos();
            s.Facing = r.SByte() < 0 ? -1 : 1; s.OnPlatform = r.SByte();
            s.Character = r.Byte(); s.Shield = r.Byte();
            int flags = r.Byte();
            s.Grounded = (flags & 1) != 0; s.Dead = (flags & 2) != 0; s.Released = (flags & 4) != 0; s.StepCarry = (flags & 8) != 0;
            s.JuggleDropped = (flags & 16) != 0; s.LastWasWhiff = (flags & 32) != 0; s.JuggleLastPerfect = (flags & 64) != 0;
            s.Action = (Player.Action)r.Byte(); s.ActionTime = r.Float();
            s.KickAim = r.Pos(); s.KickBall = r.Pos(); s.FlickStart = r.Pos(); s.FlickTarget = r.Pos();
            s.BikeBall = r.Pos(); s.HeaderBall = r.Pos(); s.JugBall = r.Pos(); s.JugContact = r.Pos();
            s.NextTouch = (Player.Touch)r.Byte(); s.LastTouch = (Player.Touch)r.Byte();
            s.SinceTouch = r.Float(); s.JugDropTime = r.Float(); s.JugTtc = r.Float();
            s.Invuln = r.Float(); s.Hp = r.Float(); s.MaxHp = r.Float(); s.DownLeft = r.Float();
            s.BallSt = (Ball.State)r.Byte(); s.BallPos = r.Pos(); s.BallVel = r.Pos(); s.MeteorTarget = r.Pos();
            s.Charge = r.Unit(); s.BallSize = r.Unit() * 2f;
            int bf = r.Byte();
            s.JuggleMode = (bf & 1) != 0; s.MeteorFalling = (bf & 2) != 0;
            return s;
        }

        /// <summary>The state between two packets: continuous values blend, the rest comes from the nearer one.</summary>
        public static PlayerNet Lerp(in PlayerNet a, in PlayerNet b, float k)
        {
            var s = k < 0.5f ? a : b;
            s.Pos = Vector2.Lerp(a.Pos, b.Pos, k);
            s.Vel = Vector2.Lerp(a.Vel, b.Vel, k);
            s.Hp = Mathf.Lerp(a.Hp, b.Hp, k);
            s.Invuln = Mathf.Lerp(a.Invuln, b.Invuln, k);
            if (a.Action == b.Action && b.ActionTime >= a.ActionTime)
            {
                s.Action = b.Action;
                s.ActionTime = Mathf.Lerp(a.ActionTime, b.ActionTime, k);
                s.KickAim = Vector2.Lerp(a.KickAim, b.KickAim, k);
                s.BikeBall = Vector2.Lerp(a.BikeBall, b.BikeBall, k);
                s.HeaderBall = Vector2.Lerp(a.HeaderBall, b.HeaderBall, k);
                s.JugBall = Vector2.Lerp(a.JugBall, b.JugBall, k);
                s.SinceTouch = Mathf.Lerp(a.SinceTouch, b.SinceTouch, k);
                s.JugTtc = Mathf.Lerp(a.JugTtc, b.JugTtc, k);
            }
            if (a.BallSt == b.BallSt)
            {
                s.BallPos = Vector2.Lerp(a.BallPos, b.BallPos, k);
                s.BallVel = Vector2.Lerp(a.BallVel, b.BallVel, k);
                s.Charge = Mathf.Lerp(a.Charge, b.Charge, k);
            }
            return s;
        }
    }

    public sealed partial class Player
    {
        /// <summary>The duo partner's body: posed from the network, never simulated here.</summary>
        public bool Puppet;
        float netJugTtc;

        /// <summary>This player as the partner will see it.</summary>
        public PlayerNet ToNet(int character, float downLeft)
        {
            return new PlayerNet
            {
                T = Time.realtimeSinceStartup,
                Pos = Pos, Vel = Vel, Facing = Facing, OnPlatform = OnPlatform, Character = character, Shield = Shield,
                Grounded = Grounded, Dead = Dead, Released = released, StepCarry = StepCarry,
                JuggleDropped = JuggleDropped, LastWasWhiff = LastWasWhiff, JuggleLastPerfect = JuggleLastPerfect,
                Action = CurrentAction, ActionTime = ActionTime,
                KickAim = KickAimLocal, KickBall = KickBallLocal, FlickStart = FlickBallStartLocal, FlickTarget = FlickTarget,
                BikeBall = BikeBallLocal, HeaderBall = HeaderBallLocal, JugBall = JuggleBallLocal, JugContact = JuggleContactLocal,
                NextTouch = NextTouch, LastTouch = LastTouch, SinceTouch = SinceTouch, JugDropTime = JuggleDropTime, JugTtc = JuggleTimeToContact,
                Invuln = InvulnTimer, Hp = Hp, MaxHp = MaxHp, DownLeft = downLeft,
                BallSt = Ball.St, BallPos = Ball.Pos, BallVel = Ball.Vel, MeteorTarget = Ball.MeteorTarget,
                Charge = Ball.Charge, BallSize = Ball.SizeMul, JuggleMode = Ball.JuggleMode, MeteorFalling = Ball.MeteorFalling,
            };
        }

        /// <summary>Pose this puppet the way the partner's player stands right now.</summary>
        public void ApplyNet(in PlayerNet s)
        {
            Pos = s.Pos; Vel = s.Vel; Facing = s.Facing; OnPlatform = s.OnPlatform; Shield = s.Shield;
            Grounded = s.Grounded; Dead = s.Dead; released = s.Released; StepCarry = s.StepCarry;
            JuggleDropped = s.JuggleDropped; LastWasWhiff = s.LastWasWhiff; JuggleLastPerfect = s.JuggleLastPerfect;
            CurrentAction = s.Action; ActionTime = s.ActionTime;
            KickAimLocal = s.KickAim; KickBallLocal = s.KickBall; FlickBallStartLocal = s.FlickStart; FlickTarget = s.FlickTarget;
            BikeBallLocal = s.BikeBall; HeaderBallLocal = s.HeaderBall; JuggleBallLocal = s.JugBall; JuggleContactLocal = s.JugContact;
            NextTouch = s.NextTouch; LastTouch = s.LastTouch; SinceTouch = s.SinceTouch; JuggleDropTime = s.JugDropTime; netJugTtc = s.JugTtc;
            InvulnTimer = s.Invuln; Hp = s.Hp; MaxHp = Mathf.Max(1f, s.MaxHp);
            Ball.ApplyNet(s.BallSt, s.BallPos, s.BallVel, s.Charge, s.JuggleMode, s.BallSize, s.MeteorTarget, s.MeteorFalling, s.Facing);
        }

        /// <summary>Duo: back in the game after the wait — half health, a moment of safety, next to the partner.</summary>
        public void CoopRevive(Vector2 at)
        {
            Dead = false;
            DeadTime = 0f;
            Pos = new Vector2(Mathf.Clamp(at.x, -ArenaHalf, ArenaHalf), Level.FloorBelow(at.x, at.y + 0.5f));
            Vel = Vector2.zero;
            Grounded = true;
            OnPlatform = Level.None;
            Hp = MaxHp * 0.5f;
            InvulnTimer = 2.5f;
            CurrentAction = Action.None;
            ActionTime = 0f;
            Rig.ResetPose();
            Rig.SetVisible(true);
            Ball.ResetTo(Pos + new Vector2(0.5f * Facing, Art.BallRadius));
            Ball.SetVisible(true);
            var fx = FxSystem.I;
            Vector2 c = Pos + new Vector2(0f, 1f);
            fx.Flash(c, 3.4f, Palette.ShotCyan, 0.25f, 3f);
            fx.Ring(FxLayer.Front, c, 0.3f, 3f, 0.4f, 0.02f, 0.5f, Color.white, Palette.ShotCyan.WithAlpha(0f), 2.6f);
            fx.Sparkles(c, 0.9f, 14, Palette.Gold, 2.8f, 0.8f);
            Game.I.Cam.AddTrauma(0.25f);
        }
    }
}
