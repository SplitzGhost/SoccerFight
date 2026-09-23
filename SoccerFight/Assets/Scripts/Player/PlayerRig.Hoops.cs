using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// The basketball layers of the rig. On the move the ball bounces between the near hand and the
    /// floor in the rhythm of the stride (one bounce per stride, a relaxed beat standing), the far
    /// arm guards; in the air both hands hold it at the chest. The moves are key poses like the
    /// soccer ones, but most of them place the ball and let arm IK put the hands on it.
    /// </summary>
    public sealed partial class PlayerRig
    {
        // arm IK targets (root-local, facing right) and their weights; hand bends against the forearm
        Vector2 nearIK, farIK;
        float nearIKw, farIKw, nearWrist, farWrist;
        float dribU;

        /// <summary>The throw leaves the hand: a small pop through the body.</summary>
        public void OnThrowRelease() { squashXVel += 1f; squashYVel -= 0.9f; }

        float Reach => body.UpperArmLen + body.ForearmLen;

        /// <summary>Near shoulder, root-local, standing upright (the moves aim the hand from here).</summary>
        Vector2 ShoulderAt(float hipY) => new Vector2(0.015f, hipY + body.ShoulderY - 0.01f);

        // ------------------------------------------------------------------ carrying the ball

        void HoopsCarry(float dt, float hipY, float air, float cycleLen, float sk,
            ref float nearShoulder, ref float nearElbow, ref float farShoulder, ref float farElbow, ref Vector2 ballLocal)
        {
            float R = Art.BallRadius;
            // one bounce per stride while running, a relaxed beat standing still
            float stridesPerSec = Mathf.Abs(player.Vel.x) / Mathf.Max(0.5f, cycleLen);
            float bps = Mathf.Lerp(1.7f, Mathf.Clamp(stridesPerSec, 1.7f, 3.3f), moveBlend);
            dribU = Mathf.Repeat(dribU + bps * dt, 1f);

            // the ball: from the palm to the floor and back, fastest at the bounce
            float wristTop = hipY + 0.14f - 0.06f * runBlend;
            float topY = wristTop - 0.07f - R;
            float bx = 0.36f + 0.16f * runBlend + 0.05f * moveBlend + 0.1f * sk;
            float by = R + (topY - R) * Mathf.Abs(Mathf.Cos(Mathf.PI * dribU));
            Vector2 dribble = new Vector2(bx, by);
            // the hand rides the ball down a little way, then waits for it to come back up
            float handY = Mathf.Max(by + R + 0.07f, wristTop - 0.16f);
            Vector2 wrist = new Vector2(bx - 0.035f, handY);

            // in the air the ball is gathered in both hands in front of the chest
            Vector2 chest = new Vector2(0.24f, hipY + body.ShoulderY * 0.7f);
            ballLocal = Vector2.Lerp(dribble, chest, air);
            nearIK = Vector2.Lerp(wrist, chest + new Vector2(0.01f, -(R + 0.07f)), air);
            nearIKw = 1f;
            nearWrist = Mathf.Lerp(28f, -30f, air);
            farIK = chest + new Vector2(-(R + 0.06f), 0.05f);
            farIKw = air;
            farWrist = 40f * air;

            // the free arm guards the ball: raised forward, forearm up
            float guard = (1f - air) * 0.75f;
            farShoulder = Mathf.Lerp(farShoulder, 32f + 12f * runBlend, guard);
            farElbow = Mathf.Lerp(farElbow, 75f, guard);
        }

        // ------------------------------------------------------------------ moves

        void PoseHoops(float t, float hipY, float air, ref Vector2 nearFoot, ref float nearFlat, ref float nearPoint,
            ref Vector2 farFoot, ref float farFlat, ref float farPoint,
            ref float nearShoulder, ref float nearElbow, ref float farShoulder, ref float farElbow,
            ref float leanTarget, ref float headTarget, ref float extraHipY, ref Vector2 ballLocal)
        {
            const float A = PlayerDims.AnkleHeight;
            float R = Art.BallRadius;
            Vector2 sh = ShoulderAt(hipY);
            switch (player.CurrentAction)
            {
                // ---- the throw: set beside the head, push out along the aim, gooseneck follow-through
                case Player.Action.Throw:
                {
                    float tS = Player.ThrowSet, tR = Player.ThrowRelease, tF = Player.ThrowFollow, tE = Player.ThrowDuration;
                    Vector2 aim = player.KickAimLocal.sqrMagnitude > 0.01f ? player.KickAimLocal.normalized : Vector2.right;
                    float w = MathUtil.Smooth01(t / 0.04f) * (1f - MathUtil.Smooth01((t - tF) / (tE - tF)));
                    Vector2 set = sh + new Vector2(0.13f, 0.2f);
                    Vector2 rel = sh + aim * Reach * 0.9f + aim * (R + 0.06f);
                    if (!player.ActionReleased)
                    {
                        Vector2 b = t < tS ? Vector2.Lerp(player.KickBallLocal, set, MathUtil.EaseOutCubic(t / tS))
                                           : Vector2.Lerp(set, rel, MathUtil.EaseInQuad((t - tS) / (tR - tS)));
                        ballLocal = b;
                        BallIsScripted = true;
                        nearIK = b - aim * (R + 0.07f);
                        nearIKw = 1f;
                        nearWrist = -55f;
                        farIK = b + new Vector2(-0.02f, R + 0.04f);
                        farIKw = 1f - MathUtil.Smooth01((t - tS * 0.6f) / 0.05f);
                        farWrist = 60f;
                    }
                    else
                    {
                        // arm stays out along the aim, the wrist snapped over
                        nearIK = sh + aim * Reach * 0.97f;
                        nearIKw = w;
                        nearWrist = 70f * MathUtil.Smooth01((t - tR) / 0.06f);
                        farIKw = 0f;
                    }
                    farShoulder = Mathf.Lerp(farShoulder, -20f, w);
                    farElbow = Mathf.Lerp(farElbow, 50f, w);
                    // legs dip into the throw and push up through it; the body leans into the aim
                    extraHipY += (-0.05f * MathUtil.Bump(Mathf.Clamp01(t / tR)) + 0.02f * MathUtil.Bump(Mathf.Clamp01((t - tR) / 0.16f))) * (1f - air);
                    leanTarget = Mathf.Lerp(leanTarget, -5f - aim.y * 6f, w);
                    headTarget = Mathf.Lerp(headTarget, Mathf.Clamp(MathUtil.Angle(aim) * 0.35f, -12f, 22f), w);
                    break;
                }

                // ---- the three: plant and step back, rise with the ball above the forehead, release at the top
                case Player.Action.Three:
                {
                    float tH = Player.ThreeHop, tR = Player.ThreeRelease, tE = Player.ThreeDuration;
                    Vector2 aim = player.KickAimLocal.sqrMagnitude > 0.01f ? player.KickAimLocal.normalized : new Vector2(0.55f, 0.85f);
                    float w = MathUtil.Smooth01(t / 0.05f) * (1f - MathUtil.Smooth01((t - (tE - 0.12f)) / 0.12f));
                    Vector2 chestB = new Vector2(0.2f, hipY + body.ShoulderY * 0.72f);
                    Vector2 setB = sh + new Vector2(0.06f, 0.36f);
                    if (t < tH)
                    {
                        // the plant: front foot stamps, knees load
                        float k = MathUtil.Smooth01(t / tH);
                        nearFoot = Vector2.Lerp(nearFoot, new Vector2(0.34f, A), k);
                        nearFlat = Mathf.Lerp(nearFlat, 1f, k);
                        farFoot = Vector2.Lerp(farFoot, new Vector2(-0.14f, A), k);
                        farFlat = Mathf.Lerp(farFlat, 1f, k);
                        extraHipY += -0.11f * k;
                        leanTarget = Mathf.Lerp(leanTarget, -8f, k);
                    }
                    else leanTarget = Mathf.Lerp(leanTarget, 9f, w);   // the fade-away
                    if (!player.ActionReleased)
                    {
                        float k = MathUtil.EaseInOutSine(Mathf.Clamp01((t - tH * 0.3f) / (tR - tH * 0.3f)));
                        Vector2 b = t < tH ? Vector2.Lerp(player.KickBallLocal, chestB, MathUtil.EaseOutCubic(t / tH))
                                           : Vector2.Lerp(chestB, setB, k);
                        ballLocal = b;
                        BallIsScripted = true;
                        nearIK = b + new Vector2(-0.03f, -(R + 0.07f));
                        nearIKw = 1f;
                        nearWrist = -70f;
                        farIK = b + new Vector2(-(R + 0.05f), 0.02f);
                        farIKw = 1f;
                        farWrist = 70f;
                        headTarget = Mathf.Lerp(headTarget, 14f, w);
                    }
                    else
                    {
                        nearIK = sh + aim * Reach * 0.97f;
                        nearIKw = w;
                        nearWrist = 75f * MathUtil.Smooth01((t - tR) / 0.06f);
                        farShoulder = Mathf.Lerp(farShoulder, 60f, w);
                        farElbow = Mathf.Lerp(farElbow, 40f, w);
                        headTarget = Mathf.Lerp(headTarget, 18f, w);
                    }
                    break;
                }

                // ---- the crossover: low lunge, the ball goes through the legs twice
                case Player.Action.Crossover:
                {
                    float tHalf = Player.CrossHalf, tE = Player.CrossDuration;
                    float w = MathUtil.Smooth01(t / 0.05f) * (1f - MathUtil.Smooth01((t - (tE - 0.05f)) / 0.05f));
                    nearFoot = Vector2.Lerp(nearFoot, new Vector2(0.3f, A), w);
                    farFoot = Vector2.Lerp(farFoot, new Vector2(-0.28f, A), w);
                    nearFlat = Mathf.Lerp(nearFlat, 1f, w); farFlat = Mathf.Lerp(farFlat, 0.6f, w);
                    nearPoint = Mathf.Lerp(nearPoint, 0f, w); farPoint = Mathf.Lerp(farPoint, 0.4f, w);
                    int half = t < tHalf ? 0 : 1;
                    float u = Mathf.Clamp01((t - half * tHalf) / tHalf);
                    extraHipY += (-0.14f - 0.03f * Mathf.Abs(Mathf.Sin(Mathf.PI * u))) * w;
                    leanTarget = Mathf.Lerp(leanTarget, -16f, w);
                    headTarget = Mathf.Lerp(headTarget, -6f + 8f * w, w);

                    // front hand low in front of the near knee, back hand just behind it (the far side of the legs)
                    Vector2 front = new Vector2(0.3f, hipY - 0.26f), back = new Vector2(0.0f, hipY - 0.3f);
                    Vector2 from = half == 0 ? front : back, to = half == 0 ? back : front;
                    float by = R + Mathf.Lerp(from.y - R, to.y - R, u) * Mathf.Abs(Mathf.Cos(Mathf.PI * u));
                    Vector2 b = new Vector2(Mathf.Lerp(from.x, to.x, u), by);
                    if (t < tE - 0.04f)
                    {
                        ballLocal = b;
                        BallIsScripted = true;
                    }
                    // each hand reaches for the ball on its side; the other waits beside the thigh
                    float nearOn = half == 0 ? 1f - MathUtil.Smooth01(u / 0.45f) : MathUtil.Smooth01((u - 0.55f) / 0.45f);
                    float farOn = 1f - nearOn;
                    nearIK = Vector2.Lerp(new Vector2(0.18f, hipY - 0.08f), b + new Vector2(-0.02f, R + 0.07f), nearOn);
                    nearIKw = w;
                    nearWrist = 25f;
                    farIK = Vector2.Lerp(new Vector2(-0.12f, hipY - 0.06f), b + new Vector2(-0.04f, R + 0.07f), farOn);
                    farIKw = w;
                    farWrist = 25f;
                    break;
                }

                // ---- the dunk: gather, rise with the ball overhead, cock it back, hammer it down on landing
                case Player.Action.Dunk:
                {
                    float tG = Player.DunkGather, T = player.DunkFlight, tS = tG + T, tE = tS + Player.DunkRecover;
                    Vector2 chestB = new Vector2(0.22f, hipY + body.ShoulderY * 0.7f);
                    Vector2 overhead = sh + new Vector2(0.1f, 0.52f);
                    Vector2 cocked = sh + new Vector2(-0.14f, 0.46f);
                    Vector2 slam = new Vector2(0.44f, hipY + body.ShoulderY * 0.25f);
                    if (t < tG)
                    {
                        float k = MathUtil.Smooth01(t / tG);
                        extraHipY += -0.15f * k;
                        leanTarget = Mathf.Lerp(leanTarget, -14f, k);
                        nearFoot = Vector2.Lerp(nearFoot, new Vector2(0.2f, A), k);
                        farFoot = Vector2.Lerp(farFoot, new Vector2(-0.18f, A), k);
                        ballLocal = Vector2.Lerp(player.KickBallLocal, chestB, MathUtil.EaseOutCubic(k));
                        BallIsScripted = true;
                        nearIK = ballLocal + new Vector2(0.01f, -(R + 0.07f)); nearIKw = 1f; nearWrist = -30f;
                        farIK = ballLocal + new Vector2(-(R + 0.06f), 0.04f); farIKw = 1f; farWrist = 40f;
                    }
                    else if (t < tS)
                    {
                        float k = Mathf.Clamp01((t - tG) / T);
                        Vector2 b;
                        if (k < 0.55f) b = Vector2.Lerp(chestB, overhead, MathUtil.EaseOutCubic(k / 0.55f));
                        else if (k < 0.82f) b = Vector2.Lerp(overhead, cocked, MathUtil.EaseInOutSine((k - 0.55f) / 0.27f));
                        else b = Vector2.Lerp(cocked, slam, MathUtil.EaseInQuad((k - 0.82f) / 0.18f));
                        ballLocal = b;
                        BallIsScripted = true;
                        // both hands on the ball: under it on the way up, behind it for the hammer
                        Vector2 dir = (b - sh).sqrMagnitude > 0.001f ? (b - sh).normalized : Vector2.up;
                        nearIK = b - dir * (R + 0.07f) + new Vector2(0.02f, 0f); nearIKw = 1f;
                        farIK = b - dir * (R + 0.06f) + new Vector2(-0.05f, 0.03f); farIKw = 1f;
                        nearWrist = Mathf.Lerp(-60f, 40f, MathUtil.Smooth01((k - 0.8f) / 0.15f));
                        farWrist = nearWrist;
                        // the legs: near knee drives up, far leg trails; the body arches, then snaps forward
                        float cock = MathUtil.Smooth01((k - 0.5f) / 0.3f), whip = MathUtil.Smooth01((k - 0.82f) / 0.18f);
                        nearFoot = new Vector2(0.24f, hipY - 0.4f);
                        farFoot = new Vector2(-0.2f, hipY - body.ThighLen - body.ShinLen * 0.85f);
                        nearFlat = 0f; farFlat = 0f; nearPoint = 0.5f; farPoint = 0.85f;
                        leanTarget = Mathf.Lerp(Mathf.Lerp(-4f, 12f, cock), -24f, whip);
                        headTarget = Mathf.Lerp(12f, -10f, whip);
                    }
                    else
                    {
                        // the landing crouch, arms flung down and out after the slam
                        float k = Mathf.Clamp01((t - tS) / (tE - tS));
                        float crouch = 1f - MathUtil.Smooth01(k);
                        extraHipY += -0.2f * crouch;
                        nearFoot = new Vector2(0.24f, A); farFoot = new Vector2(-0.24f, A);
                        nearFlat = farFlat = 1f;
                        leanTarget = Mathf.Lerp(leanTarget, -26f * crouch, 1f);
                        nearShoulder = Mathf.Lerp(-20f, nearShoulder, k); nearElbow = Mathf.Lerp(20f, nearElbow, k);
                        farShoulder = Mathf.Lerp(-40f, farShoulder, k); farElbow = Mathf.Lerp(20f, farElbow, k);
                        nearIKw = farIKw = 0f;
                    }
                    break;
                }

                // ---- the alley-oop: both hands scoop the ball up and fling it straight into the sky
                case Player.Action.AlleyOop:
                {
                    float tR = Player.OopRelease, tE = Player.OopDuration;
                    float w = MathUtil.Smooth01(t / 0.04f) * (1f - MathUtil.Smooth01((t - (tE - 0.12f)) / 0.12f));
                    Vector2 low = new Vector2(0.24f, hipY - 0.05f), top = sh + new Vector2(0.12f, 0.5f);
                    if (!player.ActionReleased)
                    {
                        float k = Mathf.Clamp01(t / tR);
                        Vector2 b = Vector2.Lerp(Vector2.Lerp(player.KickBallLocal, low, MathUtil.Smooth01(k * 2f)), top, MathUtil.EaseInQuad(Mathf.Clamp01(k * 1.4f - 0.4f)));
                        ballLocal = b;
                        BallIsScripted = true;
                        nearIK = b + new Vector2(0.01f, -(R + 0.07f)); farIK = b + new Vector2(-0.07f, -(R + 0.05f));
                        nearIKw = farIKw = 1f;
                        nearWrist = farWrist = -40f;
                    }
                    else
                    {
                        // both arms follow the ball up (the dribble hand lets go)
                        nearIKw *= 1f - w; farIKw *= 1f - w;
                        nearShoulder = Mathf.Lerp(nearShoulder, 160f, w); nearElbow = Mathf.Lerp(nearElbow, 10f, w);
                        farShoulder = Mathf.Lerp(farShoulder, 150f, w); farElbow = Mathf.Lerp(farElbow, 14f, w);
                        headTarget = Mathf.Lerp(headTarget, 28f, w);
                    }
                    extraHipY += (-0.07f * MathUtil.Bump(Mathf.Clamp01(t / tR)) + 0.03f * MathUtil.Bump(Mathf.Clamp01((t - tR) / 0.2f))) * (1f - air);
                    leanTarget = Mathf.Lerp(leanTarget, 6f, w);
                    break;
                }

                // ---- the block: a jump with both arms thrown straight up, hands open
                case Player.Action.Block:
                {
                    float w = MathUtil.Smooth01(t / 0.05f) * (1f - MathUtil.Smooth01((t - (Player.BlockPose - 0.16f)) / 0.16f));
                    nearShoulder = Mathf.Lerp(nearShoulder, 172f, w); nearElbow = Mathf.Lerp(nearElbow, 4f, w);
                    farShoulder = Mathf.Lerp(farShoulder, 162f, w); farElbow = Mathf.Lerp(farElbow, 8f, w);
                    nearWrist = 20f * w; farWrist = 30f * w;
                    nearIKw *= 1f - w; farIKw *= 1f - w;
                    leanTarget = Mathf.Lerp(leanTarget, -2f, w);
                    headTarget = Mathf.Lerp(headTarget, 16f, w);
                    // the ball is let go and bounces on by itself in front of the feet
                    float u = Mathf.Repeat(t * 3.2f, 1f);
                    ballLocal = new Vector2(0.5f, R + 0.3f * Mathf.Abs(Mathf.Cos(Mathf.PI * u)));
                    BallIsScripted = true;
                    break;
                }

                // ---- the fast break: frozen sprint stride, the ball pounded low and fast ahead
                case Player.Action.FastBreak:
                {
                    PoseRush(t, Player.FastRun, 1f, ref nearFoot, ref nearFlat, ref nearPoint, ref farFoot, ref farFlat, ref farPoint,
                        ref nearShoulder, ref nearElbow, ref farShoulder, ref farElbow, ref leanTarget, ref headTarget, ref extraHipY);
                    if (player.StepCarry)
                    {
                        float u = Mathf.Repeat(t * 7f, 1f);
                        float back = MathUtil.Smooth01((t - Player.FastRun) / (Player.FastDuration - Player.FastRun));
                        float topY = hipY - 0.1f;
                        Vector2 b = new Vector2(0.72f, R + (topY - R) * Mathf.Abs(Mathf.Cos(Mathf.PI * u)));
                        ballLocal = Vector2.Lerp(b, ballLocal, back);
                        BallIsScripted = true;
                        nearIK = new Vector2(b.x - 0.04f, Mathf.Max(b.y + R + 0.07f, topY + R - 0.05f));
                        nearIKw = 1f - back;
                        nearWrist = 35f;
                    }
                    break;
                }
            }
        }
    }
}
