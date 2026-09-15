using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Fully procedural character animation. Nothing is keyframed in an editor: the run cycle is
    /// driven by distance travelled (feet never slide), legs are solved with 2-bone IK, and every
    /// secondary motion (lean, bob, squash, head, hair) runs through damped springs so it is
    /// perfectly smooth at any frame rate. Kick and rainbow flick are authored as eased key poses
    /// layered on top of locomotion.
    /// </summary>
    public sealed class PlayerRig
    {
        public const int BaseOrder = 100;
        public const int BallOrderFront = 130;
        public const int BallOrderBetweenLegs = 117;

        sealed class Leg { public Transform thigh, shin, boot, glow; }
        sealed class Arm { public Transform upper, fore, hand; }

        readonly Player player;
        Transform root, flip;
        Transform torso, pelvis, neck, head, tuft;
        Leg nearLeg, farLeg;
        Arm nearArm, farArm;
        SpriteRenderer shadow;
        public readonly List<SpriteRenderer> Parts = new List<SpriteRenderer>();
        readonly List<Color> partColors = new List<Color>();

        // locomotion state
        float phase;
        float runBlend, runBlendVel;
        float moveBlend, moveBlendVel;
        float airBlend, airBlendVel;
        float lean, leanVel;
        float headTilt, headTiltVel;
        float squashX = 1f, squashXVel, squashY = 1f, squashYVel;
        float flipT = 1f;
        float idleTime;
        float footOnBall, footOnBallVel;
        float tuftAngle, tuftVel;
        float hipDip, hipDipVel;
        float prevVelX;
        float time;
        float flashTimer;
        float touchKick;

        // outputs
        public Vector2 BallHold { get; private set; }
        public Vector2 BallScripted { get; private set; }
        public bool BallIsScripted { get; private set; }
        public Vector2 NearFootWorld { get; private set; }
        public Vector2 HeadWorld { get; private set; }

        public PlayerRig(Player player) { this.player = player; }

        // ------------------------------------------------------------------ construction

        SpriteRenderer Part(string name, Sprite sprite, int order, bool back, Material mat = null)
        {
            var sr = Art.MakeSprite(name, flip, sprite, BaseOrder + order, mat);
            Color c = back ? Palette.BackLimbTint : Color.white;
            if (mat == Art.SpriteGlowMat) c = back ? Palette.Neon.WithAlpha(0.35f) : Palette.Neon.WithAlpha(0.7f);
            sr.color = c;
            Parts.Add(sr);
            partColors.Add(c);
            return sr;
        }

        public void Build(Transform parent)
        {
            root = new GameObject("Player").transform;
            root.SetParent(parent, false);
            flip = new GameObject("Flip").transform;
            flip.SetParent(root, false);

            shadow = Art.MakeSprite("Shadow", root, Art.Shadow, -45, Art.SpriteMat, new Color(0, 0, 0, 0.5f));

            farArm = new Arm
            {
                upper = Part("FarUpperArm", PlayerArt.UpperArm, 1, true).transform,
                fore = Part("FarForearm", PlayerArt.Forearm, 2, true).transform,
                hand = Part("FarHand", PlayerArt.Hand, 3, true).transform
            };
            farLeg = new Leg
            {
                thigh = Part("FarThigh", PlayerArt.Thigh, 4, true).transform,
                shin = Part("FarShin", PlayerArt.Shin, 5, true).transform,
                boot = Part("FarBoot", PlayerArt.Boot, 6, true).transform,
                glow = Part("FarBootGlow", PlayerArt.BootGlow, 7, true, Art.SpriteGlowMat).transform
            };
            neck = Part("Neck", PlayerArt.Neck, 8, false).transform;
            torso = Part("Torso", PlayerArt.Torso, 10, false).transform;
            pelvis = Part("Pelvis", PlayerArt.Pelvis, 11, false).transform;
            head = Part("Head", PlayerArt.Head, 14, false).transform;
            tuft = Part("HairTuft", PlayerArt.HairTuft, 13, false).transform;
            nearLeg = new Leg
            {
                thigh = Part("NearThigh", PlayerArt.Thigh, 20, false).transform,
                shin = Part("NearShin", PlayerArt.Shin, 21, false).transform,
                boot = Part("NearBoot", PlayerArt.Boot, 22, false).transform,
                glow = Part("NearBootGlow", PlayerArt.BootGlow, 23, false, Art.SpriteGlowMat).transform
            };
            nearArm = new Arm
            {
                upper = Part("NearUpperArm", PlayerArt.UpperArm, 25, false).transform,
                fore = Part("NearForearm", PlayerArt.Forearm, 26, false).transform,
                hand = Part("NearHand", PlayerArt.Hand, 27, false).transform
            };
        }

        public void ResetPose()
        {
            phase = 0f; runBlend = moveBlend = airBlend = 0f; lean = headTilt = 0f;
            squashX = squashY = 1f; flipT = player.Facing; idleTime = 0f; footOnBall = 0f;
            runBlendVel = moveBlendVel = airBlendVel = leanVel = headTiltVel = squashXVel = squashYVel = 0f;
            hipDip = hipDipVel = 0f; tuftAngle = tuftVel = 0f; flashTimer = 0f;
            for (int i = 0; i < Parts.Count; i++)
            {
                if (Parts[i].sharedMaterial == Art.SpriteSolidMat) Parts[i].sharedMaterial = Art.SpriteMat;
                Parts[i].enabled = true;
                Parts[i].color = partColors[i];
            }
        }

        public void SetVisible(bool visible)
        {
            flip.gameObject.SetActive(visible);
            shadow.enabled = visible;
        }

        // ------------------------------------------------------------------ events

        public void OnJump()
        {
            squashXVel -= 3.2f; squashYVel += 4.5f;
        }

        public void OnLand(float impactSpeed)
        {
            float k = Mathf.Clamp01(impactSpeed / 18f);
            squashXVel += 4f * k + 1f; squashYVel -= 6f * k + 1.2f;
            hipDipVel -= 1.6f * k + 0.3f;
        }

        public void OnBallReceived() { touchKick = 1f; }

        public void Flash(float duration) { flashTimer = duration; }

        // ------------------------------------------------------------------ helpers

        static Quaternion Z(float deg) => Quaternion.Euler(0f, 0f, deg);

        static void Place(Transform t, Vector2 p, float rotDeg)
        {
            t.localPosition = new Vector3(p.x, p.y, 0f);
            t.localRotation = Z(rotDeg);
        }

        void PoseLeg(Leg leg, Vector2 hip, Vector2 ankleTarget, float footPoint, float footFlatWeight)
        {
            Vector2 knee = MathUtil.SolveTwoBone(hip, ankleTarget, PlayerDims.ThighLen, PlayerDims.ShinLen, 1f, out Vector2 ankle);
            Vector2 shinDir = ankle - knee;
            float shinAngle = MathUtil.Angle(shinDir);
            Place(leg.thigh, hip, MathUtil.DownAngle(knee - hip));
            Place(leg.shin, knee, MathUtil.DownAngle(shinDir));
            // Boot: flat on the ground when planted, otherwise follows the shin with a pointed toe.
            float follow = shinAngle + 90f - 55f * footPoint;
            float bootRot = Mathf.LerpAngle(follow, 0f, footFlatWeight);
            Place(leg.boot, ankle, bootRot);
            Place(leg.glow, ankle, bootRot);
        }

        void PoseArm(Arm arm, Vector2 shoulder, float shoulderDeg, float elbowDeg)
        {
            Vector2 dirU = MathUtil.Rotate(Vector2.down, shoulderDeg);
            Vector2 elbow = shoulder + dirU * PlayerDims.UpperArmLen;
            Vector2 dirF = MathUtil.Rotate(dirU, elbowDeg);
            Vector2 wrist = elbow + dirF * PlayerDims.ForearmLen;
            Place(arm.upper, shoulder, MathUtil.DownAngle(dirU));
            Place(arm.fore, elbow, MathUtil.DownAngle(dirF));
            Place(arm.hand, wrist, MathUtil.DownAngle(dirF));
        }

        /// <summary>Gait foot position (ankle, root-local, hip-relative x) for a leg phase in [0,1).</summary>
        Vector2 GaitFoot(float p, float stanceFrac, float stride, float lift, out float swing)
        {
            if (p < stanceFrac)
            {
                swing = 0f;
                float t = p / stanceFrac;
                return new Vector2(Mathf.Lerp(stride * 0.5f, -stride * 0.5f, t), PlayerDims.AnkleHeight);
            }
            else
            {
                float t = (p - stanceFrac) / (1f - stanceFrac);
                swing = Mathf.Sin(t * Mathf.PI);
                float e = MathUtil.EaseInOutSine(t);
                float x = Mathf.Lerp(-stride * 0.5f, stride * 0.5f, e);
                // running: heel kicks up behind early in the swing, then the foot reaches forward
                x -= runBlend * 0.16f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, t * 1.4f)) * (1f - t);
                float y = lift * Mathf.Pow(swing, 0.85f) + runBlend * 0.14f * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Min(1f, t * 1.6f)), 2f);
                return new Vector2(x, PlayerDims.AnkleHeight + y);
            }
        }

        static Vector2 Polar(Vector2 center, float angDeg, float radius) => center + MathUtil.Dir(angDeg) * radius;

        // ------------------------------------------------------------------ main update

        public void Update(float dt)
        {
            time += dt;
            Vector2 vel = player.Vel;
            float speed01 = Mathf.Clamp01(Mathf.Abs(vel.x) / Player.MaxSpeed);
            bool grounded = player.Grounded;

            // --- facing flip ("paper turn": fast and smooth instead of an instant mirror)
            flipT = Mathf.MoveTowards(flipT, player.Facing, dt / 0.075f);
            float flipScale = Mathf.Sin(flipT * Mathf.PI * 0.5f);

            // --- blends
            MathUtil.Spring(ref runBlend, ref runBlendVel, speed01, 3.2f, 1f, dt);
            MathUtil.Spring(ref moveBlend, ref moveBlendVel, Mathf.Clamp01(speed01 * 4f), 5f, 1f, dt);
            MathUtil.Spring(ref airBlend, ref airBlendVel, grounded ? 0f : 1f, 5.5f, 0.9f, dt);
            runBlend = Mathf.Clamp01(runBlend);
            moveBlend = Mathf.Clamp01(moveBlend);
            float air = Mathf.Clamp01(airBlend);

            bool acting = player.CurrentAction != Player.Action.None;
            if (speed01 < 0.05f && grounded && !acting) idleTime += dt; else idleTime = 0f;
            float footOnBallTarget = idleTime > 0.7f && player.Ball.IsHeldFree ? 1f : 0f;
            MathUtil.Spring(ref footOnBall, ref footOnBallVel, footOnBallTarget, 2.6f, 1f, dt);
            float fob = Mathf.Clamp01(footOnBall);

            // --- gait
            float cycleLen = Mathf.Lerp(1.25f, 2.35f, runBlend);
            float forwardVel = vel.x * player.Facing;
            if (grounded) phase += forwardVel * dt / cycleLen;
            phase = Mathf.Repeat(phase, 1f);
            float stanceFrac = Mathf.Lerp(0.6f, 0.4f, runBlend);
            float stride = cycleLen * stanceFrac;
            float lift = Mathf.Lerp(0.1f, 0.24f, runBlend);

            Vector2 nearGait = GaitFoot(phase, stanceFrac, stride, lift, out float nearSwing);
            Vector2 farGait = GaitFoot(Mathf.Repeat(phase + 0.5f, 1f), stanceFrac, stride, lift, out float farSwing);

            // --- acceleration lean + hip
            float accel = (vel.x - prevVelX) / Mathf.Max(dt, 1e-4f);
            prevVelX = vel.x;
            float leanTarget = -(runBlend * 11f + Mathf.Clamp(accel * player.Facing * 0.35f, -7f, 9f)) * (1f - air * 0.5f);
            leanTarget += air * Mathf.Clamp(-vel.y * 0.9f, -8f, 10f) * 0.4f;

            float hipY = PlayerDims.StandHip - runBlend * 0.06f;
            float bobAmp = Mathf.Lerp(0.012f, 0.065f, runBlend) * moveBlend;
            float bob = bobAmp * (0.5f + 0.5f * Mathf.Cos(MathUtil.Tau * 2f * (phase - stanceFrac * 0.5f)));
            float breathe = Mathf.Sin(time * 2.1f) * 0.006f * (1f - moveBlend);
            MathUtil.Spring(ref hipDip, ref hipDipVel, 0f, 3.5f, 0.55f, dt);
            hipY += -bob + breathe + hipDip - fob * 0.03f;

            // --- idle feet (ready stance, or sole resting on the ball)
            Vector2 ballLocalIdle = new Vector2(0.44f, Art.BallRadius);
            Vector2 idleNear = Vector2.Lerp(new Vector2(0.1f, PlayerDims.AnkleHeight),
                ballLocalIdle + new Vector2(-0.06f, Art.BallRadius + 0.02f), fob);
            Vector2 idleFar = new Vector2(Mathf.Lerp(-0.1f, -0.06f, fob), PlayerDims.AnkleHeight);

            Vector2 nearFoot = Vector2.Lerp(idleNear, nearGait, moveBlend);
            Vector2 farFoot = Vector2.Lerp(idleFar, farGait, moveBlend);
            float nearFlat = 1f - Mathf.Clamp01((nearFoot.y - PlayerDims.AnkleHeight) / 0.12f);
            float farFlat = 1f - Mathf.Clamp01((farFoot.y - PlayerDims.AnkleHeight) / 0.12f);
            float nearPoint = nearSwing * moveBlend, farPoint = farSwing * moveBlend;
            nearFlat = Mathf.Max(nearFlat, fob);

            // --- air pose
            if (air > 0.001f)
            {
                float rising = Mathf.Clamp01(vel.y / 7f * 0.5f + 0.5f);
                Vector2 airNear = Vector2.Lerp(new Vector2(0.14f, hipY - 0.66f), new Vector2(0.24f, hipY - 0.44f), rising);
                Vector2 airFar = Vector2.Lerp(new Vector2(-0.06f, hipY - 0.72f), new Vector2(-0.22f, hipY - 0.62f), rising);
                nearFoot = Vector2.Lerp(nearFoot, airNear, air);
                farFoot = Vector2.Lerp(farFoot, airFar, air);
                nearFlat *= 1f - air; farFlat *= 1f - air;
                nearPoint = Mathf.Lerp(nearPoint, 0.5f, air); farPoint = Mathf.Lerp(farPoint, 0.7f, air);
            }

            // --- arms follow the opposite leg (works for walking backwards too)
            float halfStride = Mathf.Max(0.05f, stride * 0.5f);
            float nearArmSwing = Mathf.Clamp(farGait.x / halfStride, -1.2f, 1.2f) * Mathf.Lerp(16f, 52f, runBlend) * moveBlend;
            float farArmSwing = Mathf.Clamp(nearGait.x / halfStride, -1.2f, 1.2f) * Mathf.Lerp(16f, 52f, runBlend) * moveBlend;
            float elbowBase = Mathf.Lerp(14f, 92f, runBlend * moveBlend);
            float nearShoulder = 5f + nearArmSwing + Mathf.Sin(time * 2.1f) * 1.5f * (1f - moveBlend);
            float farShoulder = -4f + farArmSwing;
            float nearElbow = elbowBase + Mathf.Max(0f, nearArmSwing) * 0.45f;
            float farElbow = elbowBase + Mathf.Max(0f, farArmSwing) * 0.45f;
            if (air > 0.001f)
            {
                float rising = Mathf.Clamp01(vel.y / 7f * 0.5f + 0.5f);
                nearShoulder = Mathf.Lerp(nearShoulder, Mathf.Lerp(40f, 125f, rising), air);
                farShoulder = Mathf.Lerp(farShoulder, Mathf.Lerp(-55f, -110f, rising), air);
                nearElbow = Mathf.Lerp(nearElbow, 35f, air);
                farElbow = Mathf.Lerp(farElbow, 25f, air);
            }

            float headTarget = -leanTarget * 0.45f;
            float extraHipY = 0f;
            float torsoTwist = Mathf.Sin(MathUtil.Tau * phase * 2f) * 2.2f * runBlend * moveBlend;

            // --- default ball hold: rolled ahead of the feet with little dribble touches
            float touchT = Mathf.Repeat(phase + 0.02f, 1f);
            float touch = touchT < 0.28f ? MathUtil.EaseOutQuad(touchT / 0.28f) : 1f - MathUtil.Smooth01((touchT - 0.28f) / 0.72f);
            touchKick = Mathf.Max(0f, touchKick - dt * 5f);
            // keep the ball ahead of the leading foot's reach (stride/2 ≈ 0.48 at full speed)
            Vector2 ballLocal = new Vector2(0.5f + 0.27f * runBlend + 0.13f * runBlend * moveBlend * touch + touchKick * 0.08f, Art.BallRadius);
            if (air > 0.001f)
            {
                Vector2 airBall = nearFoot + new Vector2(0.17f, -0.03f);
                airBall.y = Mathf.Max(airBall.y, Art.BallRadius);
                ballLocal = Vector2.Lerp(ballLocal, airBall, air);
            }
            ballLocal = Vector2.Lerp(ballLocal, ballLocalIdle, fob * (1f - moveBlend));
            BallIsScripted = false;

            // =============================================================== action layers
            float t = player.ActionTime;
            if (player.CurrentAction == Player.Action.Kick)
            {
                PoseKick(t, hipY, ref nearFoot, ref nearFlat, ref nearPoint, ref farFoot, ref farFlat,
                    ref nearShoulder, ref nearElbow, ref farShoulder, ref farElbow, ref leanTarget, ref extraHipY);
                if (t < Player.KickContact) ballLocal = player.KickBallLocal;
            }
            else if (player.CurrentAction == Player.Action.Flick)
            {
                PoseFlick(t, hipY, ref nearFoot, ref nearFlat, ref nearPoint, ref farFoot, ref farFlat, ref farPoint,
                    ref nearShoulder, ref nearElbow, ref farShoulder, ref farElbow, ref leanTarget, ref headTarget,
                    ref extraHipY, ref ballLocal);
            }

            // --- springs for the upper body
            MathUtil.Spring(ref lean, ref leanVel, leanTarget, acting ? 5.5f : 3f, 0.72f, dt);
            MathUtil.Spring(ref headTilt, ref headTiltVel, headTarget, 3.2f, 0.8f, dt);
            MathUtil.Spring(ref squashX, ref squashXVel, 1f, 3.4f, 0.42f, dt);
            MathUtil.Spring(ref squashY, ref squashYVel, 1f, 3.4f, 0.42f, dt);

            // --- apply
            root.localPosition = new Vector3(player.Pos.x, player.Pos.y, 0f);
            flip.localScale = new Vector3(flipScale * squashX, squashY, 1f);

            float hy = hipY + extraHipY;
            Vector2 hip = new Vector2(0f, hy);
            float torsoRot = lean + torsoTwist;
            Place(torso, hip, torsoRot);
            Place(pelvis, hip, torsoRot * 0.35f);
            Vector2 shoulder = hip + MathUtil.Rotate(new Vector2(0.0f, 0.465f), torsoRot);
            Vector2 neckBase = hip + MathUtil.Rotate(new Vector2(0.03f, 0.525f), torsoRot);
            Place(neck, neckBase, torsoRot * 0.6f + headTilt * 0.3f);
            float headRot = torsoRot + headTilt;
            Vector2 headPos = neckBase + MathUtil.Rotate(new Vector2(0.0f, 0.065f), torsoRot * 0.6f);
            Place(head, headPos, headRot);

            // hair tuft: spring-driven secondary motion
            float tuftTarget = Mathf.Clamp(-vel.x * player.Facing * 1.6f - vel.y * 1.8f, -28f, 28f) + Mathf.Sin(time * 3f) * 2f;
            MathUtil.Spring(ref tuftAngle, ref tuftVel, tuftTarget, 2.2f, 0.3f, dt);
            Vector2 tuftPos = headPos + MathUtil.Rotate(new Vector2(0.03f, 0.35f), headRot);
            Place(tuft, tuftPos, headRot + tuftAngle);

            PoseLeg(farLeg, hip + new Vector2(-0.025f, 0f), farFoot, farPoint, farFlat);
            PoseLeg(nearLeg, hip + new Vector2(0.02f, 0f), nearFoot, nearPoint, nearFlat);

            Vector2 nearSh = shoulder + MathUtil.Rotate(new Vector2(0.015f, -0.01f), torsoRot);
            Vector2 farSh = shoulder + MathUtil.Rotate(new Vector2(-0.035f, 0.01f), torsoRot);
            PoseArm(farArm, farSh, farShoulder + torsoRot, farElbow);
            PoseArm(nearArm, nearSh, nearShoulder + torsoRot, nearElbow);

            // --- outputs (world space)
            float facing = player.Facing;
            Vector2 rootW = player.Pos;
            BallHold = rootW + new Vector2(ballLocal.x * facing, ballLocal.y);
            if (BallIsScripted) BallScripted = BallHold;
            NearFootWorld = rootW + new Vector2(nearFoot.x * facing, nearFoot.y);
            HeadWorld = rootW + new Vector2(headPos.x * facing, headPos.y + 0.2f);

            // --- contact shadow
            float h = Mathf.Max(0f, player.Pos.y);
            float s = Mathf.Lerp(1.05f, 0.55f, Mathf.Clamp01(h / 3f));
            shadow.transform.position = new Vector3(player.Pos.x, 0.02f, 0f);
            shadow.transform.localScale = new Vector3(s * 1.15f, s, 1f);
            shadow.color = new Color(0f, 0f, 0f, Mathf.Lerp(0.5f, 0.12f, Mathf.Clamp01(h / 3f)));

            UpdateFlash(dt);
        }

        // ------------------------------------------------------------------ kick

        void PoseKick(float t, float hipY, ref Vector2 nearFoot, ref float nearFlat, ref float nearPoint,
            ref Vector2 farFoot, ref float farFlat,
            ref float nearShoulder, ref float nearElbow, ref float farShoulder, ref float farElbow,
            ref float leanTarget, ref float extraHipY)
        {
            Vector2 hip = new Vector2(0.02f, hipY);
            Vector2 aim = player.KickAimLocal;
            float aimAng = Mathf.Clamp(MathUtil.Angle(aim), -80f, 80f);

            // Pendulum swing in polar coordinates around the hip.
            Vector2 contactPos = player.KickBallLocal + new Vector2(-0.17f, -0.06f);
            Vector2 cRel = contactPos - hip;
            float contactAng = MathUtil.Angle(cRel);
            float contactRad = Mathf.Min(cRel.magnitude, 0.79f);
            const float windAng = -128f, windRad = 0.56f;
            float followAng = Mathf.Clamp(aimAng * 0.75f - 22f, -62f, 38f);
            const float followRad = 0.78f;

            Vector2 foot;
            float point;
            if (t < Player.KickWindup)
            {
                float k = MathUtil.EaseOutCubic(t / Player.KickWindup);
                Vector2 wind = Polar(hip, windAng, windRad);
                foot = Vector2.Lerp(nearFoot, wind, k);
                point = Mathf.Lerp(nearPoint, 0.9f, k);
            }
            else if (t < Player.KickContact)
            {
                float k = MathUtil.EaseInQuad((t - Player.KickWindup) / (Player.KickContact - Player.KickWindup));
                foot = Polar(hip, Mathf.Lerp(windAng, contactAng, k), Mathf.Lerp(windRad, contactRad, k));
                point = Mathf.Lerp(0.9f, 0.55f, k);
            }
            else if (t < Player.KickFollow)
            {
                float k = MathUtil.EaseOutCubic((t - Player.KickContact) / (Player.KickFollow - Player.KickContact));
                foot = Polar(hip, Mathf.Lerp(contactAng, followAng, k), Mathf.Lerp(contactRad, followRad, k));
                point = Mathf.Lerp(0.55f, 1f, k);
            }
            else
            {
                float k = MathUtil.EaseInOutSine((t - Player.KickFollow) / (Player.KickDuration - Player.KickFollow));
                foot = Vector2.Lerp(Polar(hip, followAng, followRad), nearFoot, k);
                point = Mathf.Lerp(1f, nearPoint, k);
            }
            nearFoot = foot;
            nearPoint = point;
            nearFlat = 0f;

            // plant leg: firm, slightly bent, planted beside the ball
            float plantW = 1f - MathUtil.Smooth01((t - Player.KickFollow) / (Player.KickDuration - Player.KickFollow));
            farFoot = Vector2.Lerp(farFoot, new Vector2(0.05f, PlayerDims.AnkleHeight), plantW);
            farFlat = Mathf.Lerp(farFlat, 1f, plantW);

            // upper body: lean back into the strike, arms counter-balance
            float w = MathUtil.Bump(Mathf.Clamp01(t / Player.KickDuration));
            float strike = MathUtil.Smooth01(t / Player.KickContact) * (1f - MathUtil.Smooth01((t - Player.KickFollow) / 0.16f));
            leanTarget = Mathf.Lerp(leanTarget, 9f * strike - 5f * MathUtil.Smooth01((t - Player.KickContact) / 0.1f) * (1f - MathUtil.Smooth01((t - Player.KickFollow) / 0.18f)), Mathf.Max(w, strike));
            farShoulder = Mathf.Lerp(farShoulder, 78f, w);
            farElbow = Mathf.Lerp(farElbow, 30f, w);
            nearShoulder = Mathf.Lerp(nearShoulder, -52f, w);
            nearElbow = Mathf.Lerp(nearElbow, 38f, w);
            extraHipY += 0.035f * MathUtil.Bump(Mathf.Clamp01((t - Player.KickWindup) / 0.16f));
        }

        // ------------------------------------------------------------------ rainbow flick

        void PoseFlick(float t, float hipY, ref Vector2 nearFoot, ref float nearFlat, ref float nearPoint,
            ref Vector2 farFoot, ref float farFlat, ref float farPoint,
            ref float nearShoulder, ref float nearElbow, ref float farShoulder, ref float farElbow,
            ref float leanTarget, ref float headTarget, ref float extraHipY, ref Vector2 ballLocal)
        {
            const float A = PlayerDims.AnkleHeight;
            float tSet = Player.FlickSet, tRoll = Player.FlickRoll, tRel = Player.FlickRelease;
            float tFol = Player.FlickFollow, tEnd = Player.FlickDuration;

            Vector2 nearPlant = new Vector2(0.2f, A);
            Vector2 heelUp = new Vector2(-0.3f, 0.64f);

            // near leg: plants in front, then the heel whips up behind to launch the ball
            Vector2 nf; float np, nflat;
            if (t < tSet) { float k = MathUtil.EaseInOutCubic(t / tSet); nf = Vector2.Lerp(nearFoot, nearPlant, k); np = nearPoint * (1f - k); nflat = Mathf.Lerp(nearFlat, 1f, k); }
            else if (t < tRoll) { nf = nearPlant; np = 0f; nflat = 1f; }
            else if (t < tRel)
            {
                float k = MathUtil.EaseInQuad((t - tRoll) / (tRel - tRoll));
                nf = Vector2.Lerp(nearPlant, heelUp, k);
                np = Mathf.Lerp(0f, 0.8f, k); nflat = 1f - k;
            }
            else if (t < tFol) { float k = MathUtil.EaseOutCubic((t - tRel) / (tFol - tRel)); nf = Vector2.Lerp(heelUp, new Vector2(-0.05f, 0.28f), k); np = 0.8f; nflat = 0f; }
            else { float k = MathUtil.EaseInOutSine((t - tFol) / (tEnd - tFol)); nf = Vector2.Lerp(new Vector2(-0.05f, 0.28f), nearFoot, k); np = Mathf.Lerp(0.8f, nearPoint, k); nflat = Mathf.Lerp(0f, nearFlat, k); }

            // far leg: steps onto the ball, drags it up the calf, then plants
            Vector2 farOnBall = new Vector2(-0.2f, A + 0.2f);
            Vector2 farLift = new Vector2(-0.24f, A + 0.3f);
            Vector2 farPlant = new Vector2(-0.12f, A);
            Vector2 ff; float fp, fflat;
            if (t < tSet) { float k = MathUtil.EaseInOutCubic(t / tSet); ff = Vector2.Lerp(farFoot, farOnBall, k); fp = 0.2f * k; fflat = Mathf.Lerp(farFlat, 0.6f, k); }
            else if (t < tRoll) { float k = MathUtil.EaseInOutSine((t - tSet) / (tRoll - tSet)); ff = Vector2.Lerp(farOnBall, farLift, k); fp = 0.35f; fflat = 0.4f; }
            else if (t < tRel) { float k = MathUtil.EaseInOutCubic((t - tRoll) / (tRel - tRoll)); ff = Vector2.Lerp(farLift, farPlant, k); fp = 0.35f * (1f - k); fflat = k; }
            else if (t < tFol) { ff = farPlant; fp = 0f; fflat = 1f; }
            else { float k = MathUtil.EaseInOutSine((t - tFol) / (tEnd - tFol)); ff = Vector2.Lerp(farPlant, farFoot, k); fp = farPoint * k; fflat = Mathf.Lerp(1f, farFlat, k); }

            nearFoot = nf; nearPoint = np; nearFlat = nflat;
            farFoot = ff; farPoint = fp; farFlat = fflat;

            // ball: rolled back between the feet → up the back of the calf → rides the heel
            if (t < tRel)
            {
                Vector2 bSet = new Vector2(-0.02f, Art.BallRadius);
                Vector2 bRoll = new Vector2(-0.08f, 0.38f);
                Vector2 b;
                if (t < tSet) b = Vector2.Lerp(player.FlickBallStartLocal, bSet, MathUtil.EaseInOutCubic(t / tSet));
                else if (t < tRoll) b = Vector2.Lerp(bSet, bRoll, MathUtil.EaseInOutSine((t - tSet) / (tRoll - tSet)));
                else
                {
                    float k = MathUtil.EaseInQuad((t - tRoll) / (tRel - tRoll));
                    // ride on the back of the heel as it whips up
                    Vector2 heel = Vector2.Lerp(nearPlant, heelUp, k) + new Vector2(-0.12f, 0.2f + 0.05f * k);
                    b = Vector2.Lerp(bRoll, heel, MathUtil.Smooth01(k * 1.6f));
                }
                ballLocal = b;
                BallIsScripted = true;
            }

            // body: crouch and look down while rolling, snap up and look at the ball after release
            float crouch = MathUtil.Smooth01(t / tSet) * (1f - MathUtil.Smooth01((t - tRoll) / (tRel - tRoll)));
            float hop = MathUtil.Bump(Mathf.Clamp01((t - tRoll) / (tFol - tRoll)));
            extraHipY += -0.07f * crouch + 0.06f * hop;

            float lookUp = MathUtil.Smooth01((t - tRel + 0.04f) / 0.12f) * (1f - MathUtil.Smooth01((t - tFol) / (tEnd - tFol)));
            leanTarget = Mathf.Lerp(-14f * crouch, 7f, lookUp);
            headTarget = Mathf.Lerp(-18f * crouch, 26f, lookUp);

            float balance = MathUtil.Smooth01(t / tSet) * (1f - lookUp);
            float fling = lookUp;
            nearShoulder = Mathf.Lerp(Mathf.Lerp(nearShoulder, -32f, balance), 145f, fling);
            farShoulder = Mathf.Lerp(Mathf.Lerp(farShoulder, 38f, balance), -148f, fling);
            nearElbow = Mathf.Lerp(Mathf.Lerp(nearElbow, 42f, balance), 12f, fling);
            farElbow = Mathf.Lerp(Mathf.Lerp(farElbow, 36f, balance), 16f, fling);
        }

        public void OnFlickRelease() { squashXVel -= 1.8f; squashYVel += 3f; }

        public void OnKickContact() { squashXVel += 1.2f; squashYVel -= 0.8f; }

        // ------------------------------------------------------------------ flashes / invulnerability blink

        void UpdateFlash(float dt)
        {
            bool flashing = flashTimer > 0f;
            if (flashing) flashTimer -= Time.unscaledDeltaTime;
            bool blinkOff = player.InvulnTimer > 0f && !flashing && Mathf.Repeat(player.InvulnTimer, 0.14f) < 0.06f;
            for (int i = 0; i < Parts.Count; i++)
            {
                var sr = Parts[i];
                bool glow = sr.sharedMaterial == Art.SpriteGlowMat;
                if (glow) { sr.enabled = !flashing; continue; }
                sr.sharedMaterial = flashing ? Art.SpriteSolidMat : Art.SpriteMat;
                Color c = flashing ? new Color(1f, 0.55f, 0.6f, 1f) : partColors[i];
                if (blinkOff) c.a *= 0.35f;
                sr.color = c;
            }
        }

        /// <summary>Local (facing-right) vector → world vector for the current facing.</summary>
        public Vector2 LocalToWorldDir(Vector2 v) => new Vector2(v.x * player.Facing, v.y);
    }
}
