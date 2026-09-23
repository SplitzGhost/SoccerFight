using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// The duo partner's ball on this screen: at the feet it follows the partner's rig exactly like
    /// the own ball does; in flight it takes the position the partner reported. It never hits
    /// anything here — the partner's game already decided what it hit.
    /// </summary>
    public sealed partial class Ball
    {
        public Vector2 MeteorTarget => meteorTarget;
        public bool MeteorFalling => meteorFalling;

        Vector2 netPos, netVel;
        bool visible = true;

        public void SetVisible(bool on)
        {
            if (visible == on) return;
            visible = on;
            root.gameObject.SetActive(on);
            shadow.enabled = on;
            if (!on) HideMarker();
        }

        public void ApplyNet(State st, Vector2 pos, Vector2 vel, float charge, bool juggle, float size, Vector2 meteorAt, bool falling, int facing)
        {
            if (st != St)
            {
                // a new flight: its own trail, and no streak from wherever the last one ended
                Enter(st);
                if (st == State.Pierce) heavyTrail.colorGradient = pierceGradient;
                else if (st == State.Blast || st == State.Meteor) heavyTrail.colorGradient = blastGradient;
                else if (st == State.Header) heavyTrail.colorGradient = headerGradient;
                else if (st == State.Lob) heavyTrail.colorGradient = lobGradient;
                else if (st == State.Oop) heavyTrail.colorGradient = oopGradient;
                if (st == State.Pierce || st == State.Blast || st == State.Meteor || st == State.Header || st == State.Lob || st == State.Oop) heavyTrail.Clear();
                if (st == State.Rainbow) rainbowTrail.Clear();
                if (st == State.Shot) squashVel += 6f;
            }
            if (st == State.Meteor && falling && !meteorFalling) heavyTrail.Clear();
            netPos = pos;
            netVel = vel;
            Charge = charge;
            JuggleMode = juggle;
            SizeMul = Mathf.Max(0.5f, size);
            meteorTarget = meteorAt;
            meteorFalling = falling;
            flickFacing = facing;
        }

        /// <summary>Moves and draws the partner's ball (after the partner's rig was posed).</summary>
        public void UpdateNet(float dt, Player player)
        {
            stateTime += dt;
            prevPos = Pos;
            var rig = player.Rig;
            switch (St)
            {
                case State.Held:
                {
                    Vector2 was = Pos;
                    MathUtil.Spring(ref Pos, ref Vel, rig.BallHold, 7.5f, 0.9f, dt);
                    // the partner's basketball keeps the rhythm of the dribble like the own one
                    if (Kind == Sport.Basketball && (rig.BallHold - was).sqrMagnitude < 0.25f)
                        Pos = Vector2.Lerp(Pos, rig.BallHold, MathUtil.Smooth01(stateTime / 0.16f));
                    float floor = Level.FloorBelow(Pos.x, Mathf.Min(Pos.y - R, player.Pos.y) + 0.02f);
                    if (Pos.y < floor + R) Pos.y = floor + R;
                    break;
                }
                case State.Scripted:
                    Vel = dt > 0f ? (rig.BallHold - Pos) / dt : Vector2.zero;
                    Pos = rig.BallHold;
                    break;
                default:
                    // a jump of several metres (meteor coming down, a catch) must not draw a streak
                    if ((netPos - Pos).sqrMagnitude > 9f) { shotTrail.Clear(); heavyTrail.Clear(); rainbowTrail.Clear(); Pos = netPos; prevPos = Pos; }
                    Pos = netPos;
                    Vel = netVel;
                    break;
            }
            if (St == State.Meteor) UpdateMarker(dt);
            UpdateVisuals(dt, player);
        }
    }
}
