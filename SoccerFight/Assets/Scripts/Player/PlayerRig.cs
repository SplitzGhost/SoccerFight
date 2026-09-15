using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Fully procedural character animation. Nothing is keyframed in an editor: the run cycle is
    /// driven by distance travelled (feet never slide), legs are solved with 2-bone IK, and every
    /// secondary motion (lean, bob, squash, head, hair) runs through damped springs so it is
    /// perfectly smooth at any frame rate. Kick, rainbow flick and keep-ups are authored as eased
    /// key poses layered on top of locomotion.
    /// </summary>
    public sealed class PlayerRig
    {
        public const int BaseOrder = 100;
        public const int BallOrderFront = 130;
        public const int BallOrderBetweenLegs = 111;

        sealed class Leg { public Transform thigh, shin, boot, glow; }
        sealed class Arm { public Transform upper, fore, hand; }

        static readonly int FloorId = Shader.PropertyToID("_FloorY");

        readonly Player player;
        Transform root, flip;
        Transform torso, pelvis, neck, head, tuft;
        Leg nearLeg, farLeg;
        Arm nearArm, farArm;
        SpriteRenderer shadow;
        public readonly List<SpriteRenderer> Parts = new List<SpriteRenderer>();
        readonly List<Color> partColors = new List<Color>();
        readonly List<Material> partMats = new List<Material>();

        /// <summary>Set by the player: snapshots of the old pose sell the turn.</summary>
        public Afterimages Ghosts;

        // locomotion state
        float phase;
        float runBlend, runBlendVel;
        float moveBlend, moveBlendVel;
        float airBlend, airBlendVel;
        float lean, leanVel;
        float headTilt, headTiltVel;
        float squashX = 1f, squashXVel, squashY = 1f, squashYVel;
        float idleTime;
        float footOnBall, footOnBallVel;
        float tuftAngle, tuftVel;
        float hipDip, hipDipVel;
        float prevVelX;
        float time;
        float flashTimer;
        float touchKick;

        // turning: the rig is drawn with its own facing, which lags the logical one during a skid
        int visFacing = 1;
        float turnT = 99f;
        float skid, skidVel;

        // air boost tuck and the last keep-up contact
        float tuck;
        Vector2 touchAt;

        // whole-body rotation around the hip (bicycle-kick backflip), degrees, positive = backwards
        float spin;

        // outputs
        public Vector2 BallHold { get; private set; }
        public Vector2 BallScripted { get; private set; }
        public bool BallIsScripted { get; private set; }
        public Vector2 NearFootWorld { get; private set; }
        public Vector2 HeadWorld { get; private set; }

        public PlayerRig(Player player) { this.player = player; }

        // ------------------------------------------------------------------ construction

        SpriteRenderer Part(string name, Sprite sprite, int order, bool back, bool glow = false)
        {
            var mat = glow ? Art.SpriteGlowMat : Art.CharacterMat;
            var sr = Art.MakeSprite(name, flip, sprite, BaseOrder + order, mat);
            Color c = back ? Palette.BackLimbTint : Color.white;
            if (glow) c = back ? Palette.Neon.WithAlpha(0.35f) : Palette.Neon.WithAlpha(0.7f);
            sr.color = c;
            Parts.Add(sr);
            partColors.Add(c);
            partMats.Add(mat);
            return sr;
        }

        public void Build(Transform parent)
        {
            root = new GameObject("Player").transform;
            root.SetParent(parent, false);
            flip = new GameObject("Flip").transform;
            flip.SetParent(root, false);

            shadow = Art.MakeSprite("Shadow", root, Art.Shadow, -45, Art.SpriteMat, new Color(0, 0, 0, 0.5f));

            // back to front: far arm, far leg, neck, shorts, near leg, jersey (worn untucked over
            // the hips), hair tuft, head, near arm. Within a limb the lower segment sits under the
            // upper one (the boot over the sock), so the moonlit top of a joint cap is never exposed.
            farArm = new Arm
            {
                hand = Part("FarHand", PlayerArt.Hand, 1, true).transform,
                fore = Part("FarForearm", PlayerArt.Forearm, 2, true).transform,
                upper = Part("FarUpperArm", PlayerArt.UpperArm, 3, true).transform
            };
            farLeg = new Leg
            {
                shin = Part("FarShin", PlayerArt.Shin, 4, true).transform,
                thigh = Part("FarThigh", PlayerArt.Thigh, 5, true).transform,
                boot = Part("FarBoot", PlayerArt.Boot, 6, true).transform,
                glow = Part("FarBootGlow", PlayerArt.BootGlow, 7, true, true).transform
            };
            neck = Part("Neck", PlayerArt.Neck, 8, false).transform;
            pelvis = Part("Pelvis", PlayerArt.Pelvis, 10, false).transform;
            nearLeg = new Leg
            {
                shin = Part("NearShin", PlayerArt.Shin, 12, false).transform,
                thigh = Part("NearThigh", PlayerArt.Thigh, 13, false).transform,
                boot = Part("NearBoot", PlayerArt.Boot, 14, false).transform,
                glow = Part("NearBootGlow", PlayerArt.BootGlow, 15, false, true).transform
            };
            torso = Part("Torso", PlayerArt.Torso, 18, false).transform;
            tuft = Part("HairTuft", PlayerArt.HairTuft, 19, false).transform;
            head = Part("Head", PlayerArt.Head, 20, false).transform;
            nearArm = new Arm
            {
                hand = Part("NearHand", PlayerArt.Hand, 25, false).transform,
                fore = Part("NearForearm", PlayerArt.Forearm, 26, false).transform,
                upper = Part("NearUpperArm", PlayerArt.UpperArm, 27, false).transform
            };
        }

        public void ResetPose()
        {
            phase = 0f; runBlend = moveBlend = airBlend = 0f; lean = headTilt = 0f;
            squashX = squashY = 1f; idleTime = 0f; footOnBall = 0f;
            runBlendVel = moveBlendVel = airBlendVel = leanVel = headTiltVel = squashXVel = squashYVel = 0f;
            hipDip = hipDipVel = 0f; tuftAngle = tuftVel = 0f; flashTimer = 0f;
            visFacing = player.Facing; turnT = 99f; skid = skidVel = 0f; tuck = 0f; spin = 0f;
            for (int i = 0; i < Parts.Count; i++)
            {
                Parts[i].sharedMaterial = partMats[i];
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
            tuck = 0f;
        }

        public void OnBallReceived() { touchKick = 1f; }

        public void Flash(float duration) { flashTimer = duration; }

        public void OnAirBoost(Vector2 push)
        {
            tuck = 1f;
            float vertical = Mathf.Abs(push.normalized.y);
            squashXVel -= 2.6f * vertical - 1.5f * (1f - vertical);
            squashYVel += 3.6f * vertical - 1.2f * (1f - vertical);
            tuftVel += 140f;
        }

        public void OnPowerContact() { squashXVel += 2.4f; squashYVel -= 1.6f; hipDipVel -= 0.6f; }

        public void OnDash() { squashXVel += 3f; squashYVel -= 1.4f; tuftVel -= 180f; }

        public void OnJuggleTouch(Player.Touch part)
        {
            touchAt = player.JuggleBallLocal;
            if (part == Player.Touch.Head) { squashYVel += 1.4f; squashXVel -= 0.8f; }
            else { squashYVel -= 0.7f; squashXVel += 0.4f; }
        }

        /// <summary>Flip the drawn facing in one frame. Springs that live in local space are mirrored so
        /// the body carries its motion through the turn instead of snapping.</summary>
        void Turn()
        {
            Ghosts?.Spawn(Palette.MoonRim, 0.15f, 0.2f);
            visFacing = player.Facing;
            turnT = 0f;
            lean = -lean; leanVel = -leanVel;
            headTilt = -headTilt; headTiltVel = -headTiltVel;
            tuftAngle = -tuftAngle; tuftVel = -tuftVel + 150f;
            squashXVel -= 2.2f; squashYVel += 1f;
            hipDipVel -= 0.5f;
        }

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
            bool acting = player.CurrentAction != Player.Action.None;

            // --- facing: while the body still slides the old way it skids (planted foot, leaning back),
            //     then the drawn facing flips in one frame. Scaling through zero width looked like a
            //     sheet of paper; the afterimage, a squash pop and the mirrored springs read as a turn.
            bool sliding = false;
            if (player.Facing != visFacing)
            {
                sliding = grounded && !acting && vel.x * visFacing > 1.2f;
                if (!sliding) Turn();
            }
            turnT += dt;
            MathUtil.Spring(ref skid, ref skidVel, sliding ? 1f : 0f, 7f, 1f, dt);
            float sk = Mathf.Clamp01(skid);
            float facing = visFacing;

            // --- blends
            MathUtil.Spring(ref runBlend, ref runBlendVel, speed01, 3.2f, 1f, dt);
            MathUtil.Spring(ref moveBlend, ref moveBlendVel, Mathf.Clamp01(speed01 * 4f), 5f, 1f, dt);
            MathUtil.Spring(ref airBlend, ref airBlendVel, grounded ? 0f : 1f, 5.5f, 0.9f, dt);
            runBlend = Mathf.Clamp01(runBlend);
            moveBlend = Mathf.Clamp01(moveBlend);
            float air = Mathf.Clamp01(airBlend);
            tuck = Mathf.Max(0f, tuck - dt * 2.6f);

            // backflip angle: scripted during the bicycle kick; a flip cut short by landing spins on
            // to upright instead of snapping back
            if (player.CurrentAction == Player.Action.Bicycle) spin = BicycleSpin(player.ActionTime);
            else if (spin != 0f)
            {
                spin = Mathf.MoveTowards(spin, spin > 180f ? 360f : 0f, 1500f * dt);
                if (spin >= 360f || spin <= 0f) spin = 0f;
            }

            if (speed01 < 0.05f && grounded && !acting) idleTime += dt; else idleTime = 0f;
            float footOnBallTarget = idleTime > 0.7f && player.Ball.IsHeldFree ? 1f : 0f;
            MathUtil.Spring(ref footOnBall, ref footOnBallVel, footOnBallTarget, 2.6f, 1f, dt);
            float fob = Mathf.Clamp01(footOnBall);

            // --- gait
            float cycleLen = Mathf.Lerp(1.25f, 2.35f, runBlend);
            float forwardVel = vel.x * facing;
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
            float leanTarget = -(runBlend * 11f + Mathf.Clamp(accel * facing * 0.35f, -7f, 9f)) * (1f - air * 0.5f);
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

            // --- air pose (tucks tight for a moment after a recoil boost)
            if (air > 0.001f)
            {
                float rising = Mathf.Clamp01(vel.y / 7f * 0.5f + 0.5f);
                Vector2 airNear = Vector2.Lerp(new Vector2(0.14f, hipY - 0.66f), new Vector2(0.24f, hipY - 0.44f), rising);
                Vector2 airFar = Vector2.Lerp(new Vector2(-0.06f, hipY - 0.72f), new Vector2(-0.22f, hipY - 0.62f), rising);
                float tk = MathUtil.Smooth01(tuck);
                airNear = Vector2.Lerp(airNear, new Vector2(0.22f, hipY - 0.34f), tk);
                airFar = Vector2.Lerp(airFar, new Vector2(-0.02f, hipY - 0.4f), tk);
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
                float tk = MathUtil.Smooth01(tuck);
                nearShoulder = Mathf.Lerp(nearShoulder, Mathf.Lerp(Mathf.Lerp(40f, 125f, rising), 20f, tk), air);
                farShoulder = Mathf.Lerp(farShoulder, Mathf.Lerp(Mathf.Lerp(-55f, -110f, rising), -30f, tk), air);
                nearElbow = Mathf.Lerp(nearElbow, Mathf.Lerp(35f, 95f, tk), air);
                farElbow = Mathf.Lerp(farElbow, Mathf.Lerp(25f, 90f, tk), air);
            }

            float headTarget = -leanTarget * 0.45f;
            float extraHipY = 0f;
            float torsoTwist = Mathf.Sin(MathUtil.Tau * phase * 2f) * 2.2f * runBlend * moveBlend;

            // --- skid into a turn: front foot digs in, body leans back against the slide, arms balance
            if (sk > 0.001f)
            {
                nearFoot = Vector2.Lerp(nearFoot, new Vector2(0.34f, PlayerDims.AnkleHeight), sk);
                farFoot = Vector2.Lerp(farFoot, new Vector2(-0.17f, PlayerDims.AnkleHeight + 0.03f), sk);
                nearFlat = Mathf.Lerp(nearFlat, 1f, sk); nearPoint = Mathf.Lerp(nearPoint, 0f, sk);
                farFlat = Mathf.Lerp(farFlat, 0.3f, sk); farPoint = Mathf.Lerp(farPoint, 0.5f, sk);
                leanTarget = Mathf.Lerp(leanTarget, 14f, sk);
                headTarget = Mathf.Lerp(headTarget, -4f, sk);
                hipY -= 0.07f * sk;
                nearShoulder = Mathf.Lerp(nearShoulder, -40f, sk); nearElbow = Mathf.Lerp(nearElbow, 55f, sk);
                farShoulder = Mathf.Lerp(farShoulder, 60f, sk); farElbow = Mathf.Lerp(farElbow, 35f, sk);
            }

            // --- just after a turn the arms and hips swing through
            if (turnT < 0.3f)
            {
                float k = 1f - MathUtil.Smooth01(turnT / 0.3f);
                nearShoulder -= 26f * k;
                farShoulder += 22f * k;
                torsoTwist += 5f * k;
            }

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
            ballLocal.x = Mathf.Lerp(ballLocal.x, 0.72f, sk);   // the ball keeps rolling on while the player brakes
            BallIsScripted = false;

            // =============================================================== action layers
            float t = player.ActionTime;
            if (player.CurrentAction == Player.Action.Kick)
            {
                PoseKick(t, Player.KickWindup, Player.KickContact, Player.KickFollow, Player.KickDuration, 0f, hipY, air,
                    ref nearFoot, ref nearFlat, ref nearPoint, ref farFoot, ref farFlat,
                    ref nearShoulder, ref nearElbow, ref farShoulder, ref farElbow, ref leanTarget, ref extraHipY);
                if (t < Player.KickContact) ballLocal = player.KickBallLocal;
            }
            else if (player.CurrentAction == Player.Action.Power)
            {
                PoseKick(t, Player.PowerWindup, Player.PowerContact, Player.PowerFollow, Player.PowerDuration, 1f, hipY, air,
                    ref nearFoot, ref nearFlat, ref nearPoint, ref farFoot, ref farFlat,
                    ref nearShoulder, ref nearElbow, ref farShoulder, ref farElbow, ref leanTarget, ref extraHipY);
                if (t < Player.PowerContact) ballLocal = player.KickBallLocal;
            }
            else if (player.CurrentAction == Player.Action.StepOver)
            {
                PoseStepOver(t, hipY, ref nearFoot, ref nearFlat, ref nearPoint, ref farFoot, ref farFlat, ref farPoint,
                    ref nearShoulder, ref nearElbow, ref farShoulder, ref farElbow, ref leanTarget, ref headTarget,
                    ref extraHipY, ref ballLocal);
            }
            else if (player.CurrentAction == Player.Action.Bicycle)
            {
                PoseBicycle(t, hipY, ref nearFoot, ref nearFlat, ref nearPoint, ref farFoot, ref farFlat, ref farPoint,
                    ref nearShoulder, ref nearElbow, ref farShoulder, ref farElbow, ref leanTarget, ref headTarget, ref ballLocal);
            }
            else if (player.CurrentAction == Player.Action.Flick)
            {
                PoseFlick(t, hipY, ref nearFoot, ref nearFlat, ref nearPoint, ref farFoot, ref farFlat, ref farPoint,
                    ref nearShoulder, ref nearElbow, ref farShoulder, ref farElbow, ref leanTarget, ref headTarget,
                    ref extraHipY, ref ballLocal);
            }
            else if (player.CurrentAction == Player.Action.Juggle)
            {
                PoseJuggle(hipY, ref nearFoot, ref nearFlat, ref nearPoint, ref farFoot, ref farFlat, ref farPoint,
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
            flip.localScale = new Vector3(facing * squashX, squashY, 1f);

            float hy = hipY + extraHipY;
            Vector2 hip = new Vector2(0f, hy);

            // backflip: rotate the whole body around the hip (the angle flips with the mirror)
            float sp = spin * facing;
            Vector2 rp = MathUtil.Rotate(hip, sp);
            flip.localRotation = Z(sp);
            flip.localPosition = new Vector3(hip.x - rp.x, hip.y - rp.y, 0f);
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
            float tuftTarget = Mathf.Clamp(-vel.x * facing * 1.6f - vel.y * 1.8f, -28f, 28f) + Mathf.Sin(time * 3f) * 2f;
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
            Vector2 rootW = player.Pos;
            BallHold = rootW + new Vector2(ballLocal.x * facing, ballLocal.y);
            if (BallIsScripted) BallScripted = BallHold;
            Vector2 footBody = hip + MathUtil.Rotate(nearFoot - hip, spin);
            Vector2 headBody = hip + MathUtil.Rotate(headPos + new Vector2(0f, 0.2f) - hip, spin);
            NearFootWorld = rootW + new Vector2(footBody.x * facing, footBody.y);
            HeadWorld = rootW + new Vector2(headBody.x * facing, headBody.y);

            // --- contact shadow on whatever surface is below (pitch or platform)
            float floor = player.GroundY;
            float h = Mathf.Max(0f, player.Pos.y - floor);
            float s = Mathf.Lerp(1.05f, 0.55f, Mathf.Clamp01(h / 3f));
            shadow.transform.position = new Vector3(player.Pos.x, floor + 0.02f, 0f);
            Art.CharacterMat.SetFloat(FloorId, floor);   // contact occlusion follows the surface
            shadow.transform.localScale = new Vector3(s * 1.15f, s, 1f);
            shadow.color = new Color(0f, 0f, 0f, Mathf.Lerp(0.5f, 0.12f, Mathf.Clamp01(h / 3f)));

            UpdateFlash(dt);
        }

        // ------------------------------------------------------------------ kick

        /// <summary>Shot and power shot share one pendulum swing; power (0..1) winds further back,
        /// leans harder into it and follows through higher.</summary>
        void PoseKick(float t, float tWind, float tContact, float tFollow, float tEnd, float power, float hipY, float air,
            ref Vector2 nearFoot, ref float nearFlat, ref float nearPoint,
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
            float windAng = Mathf.Lerp(-128f, -150f, power), windRad = Mathf.Lerp(0.56f, 0.66f, power);
            float followAng = Mathf.Clamp(aimAng * 0.75f - 22f + 26f * power, -62f, 38f + 30f * power);
            const float followRad = 0.78f;

            Vector2 foot;
            float point;
            if (t < tWind)
            {
                float k = MathUtil.EaseOutCubic(t / tWind);
                Vector2 wind = Polar(hip, windAng, windRad);
                foot = Vector2.Lerp(nearFoot, wind, k);
                point = Mathf.Lerp(nearPoint, 0.9f, k);
            }
            else if (t < tContact)
            {
                float k = MathUtil.EaseInQuad((t - tWind) / (tContact - tWind));
                foot = Polar(hip, Mathf.Lerp(windAng, contactAng, k), Mathf.Lerp(windRad, contactRad, k));
                point = Mathf.Lerp(0.9f, 0.55f, k);
            }
            else if (t < tFollow)
            {
                float k = MathUtil.EaseOutCubic((t - tContact) / (tFollow - tContact));
                foot = Polar(hip, Mathf.Lerp(contactAng, followAng, k), Mathf.Lerp(contactRad, followRad, k));
                point = Mathf.Lerp(0.55f, 1f, k);
            }
            else
            {
                float k = MathUtil.EaseInOutSine((t - tFollow) / (tEnd - tFollow));
                foot = Vector2.Lerp(Polar(hip, followAng, followRad), nearFoot, k);
                point = Mathf.Lerp(1f, nearPoint, k);
            }
            nearFoot = foot;
            nearPoint = point;
            nearFlat = 0f;

            // plant leg: firm, slightly bent, planted beside the ball (tucked instead when airborne)
            float plantW = (1f - MathUtil.Smooth01((t - tFollow) / (tEnd - tFollow))) * (1f - air);
            farFoot = Vector2.Lerp(farFoot, new Vector2(0.05f - 0.06f * power, PlayerDims.AnkleHeight), plantW);
            farFlat = Mathf.Lerp(farFlat, 1f, plantW);

            // upper body: lean back into the strike, arms counter-balance (wider for the power shot)
            float w = MathUtil.Bump(Mathf.Clamp01(t / tEnd));
            float strike = MathUtil.Smooth01(t / tContact) * (1f - MathUtil.Smooth01((t - tFollow) / 0.16f));
            float lean = (9f + 7f * power) * strike - (5f + 5f * power) * MathUtil.Smooth01((t - tContact) / 0.1f) * (1f - MathUtil.Smooth01((t - tFollow) / 0.18f));
            leanTarget = Mathf.Lerp(leanTarget, lean, Mathf.Max(w, strike));
            farShoulder = Mathf.Lerp(farShoulder, 78f + 30f * power, w);
            farElbow = Mathf.Lerp(farElbow, 30f, w);
            nearShoulder = Mathf.Lerp(nearShoulder, -52f - 30f * power, w);
            nearElbow = Mathf.Lerp(nearElbow, 38f - 10f * power, w);
            // the power shot sinks into the wind-up, then rises through the strike
            float sink = power * MathUtil.Smooth01(t / tWind) * (1f - MathUtil.Smooth01((t - tWind) / (tContact - tWind)));
            extraHipY += (0.035f * MathUtil.Bump(Mathf.Clamp01((t - tWind) / 0.16f)) - 0.08f * sink) * (1f - air);
        }

        // ------------------------------------------------------------------ step-over + dash

        void PoseStepOver(float t, float hipY, ref Vector2 nearFoot, ref float nearFlat, ref float nearPoint,
            ref Vector2 farFoot, ref float farFlat, ref float farPoint,
            ref float nearShoulder, ref float nearElbow, ref float farShoulder, ref float farElbow,
            ref float leanTarget, ref float headTarget, ref float extraHipY, ref Vector2 ballLocal)
        {
            const float A = PlayerDims.AnkleHeight;
            float tS = Player.StepOverTime, tD = Player.StepOverTime + Player.DashTime, tE = Player.StepOverDuration;
            float w = MathUtil.Smooth01(t / 0.06f) * (1f - MathUtil.Smooth01((t - tD) / (tE - tD)));

            Vector2 nf, ff; float nflat, npoint, fflat, fpoint, lean, head, hipOff;
            float nsh, nel, fsh, fel;
            Vector2 ball;
            if (t < tS)
            {
                // the near foot circles over the ball: from behind it, up and over, planting beyond it
                float circleEnd = tS * 0.72f;
                if (t < circleEnd)
                {
                    float k = MathUtil.EaseInOutSine(t / circleEnd);
                    float ang = Mathf.Lerp(200f, -25f, k) * Mathf.Deg2Rad;
                    nf = new Vector2(0.42f + Mathf.Cos(ang) * 0.3f, 0.3f + Mathf.Sin(ang) * 0.25f);
                    nf.y = Mathf.Max(nf.y, A);
                    nflat = 0f; npoint = 0.45f;
                }
                else
                {
                    float k = MathUtil.EaseOutCubic((t - circleEnd) / (tS - circleEnd));
                    Vector2 from = new Vector2(0.42f + Mathf.Cos(-25f * Mathf.Deg2Rad) * 0.3f, Mathf.Max(A, 0.3f + Mathf.Sin(-25f * Mathf.Deg2Rad) * 0.25f));
                    nf = Vector2.Lerp(from, new Vector2(0.64f, A), k);
                    nflat = k; npoint = 0.45f * (1f - k);
                }
                // the standing leg sinks as the body feints, then loads for the burst
                float load = MathUtil.Smooth01((t - circleEnd) / (tS - circleEnd));
                ff = new Vector2(Mathf.Lerp(-0.08f, -0.2f, load), A); fflat = 1f; fpoint = 0f;
                float sway = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / circleEnd));
                lean = Mathf.Lerp(7f * sway, -20f, load);
                head = Mathf.Lerp(-14f * sway, -6f, load);
                hipOff = -0.05f * sway - 0.09f * load;
                nsh = Mathf.Lerp(40f * sway, -30f, load); nel = 40f;
                fsh = Mathf.Lerp(-30f * sway, 50f, load); fel = 45f;
                ball = new Vector2(0.42f, Art.BallRadius);
            }
            else
            {
                // dash: low, long stride frozen mid-sprint, arms swept back
                float k = MathUtil.Smooth01((t - tS) / 0.05f);
                nf = Vector2.Lerp(new Vector2(0.64f, A), new Vector2(0.5f, A + 0.06f), k); nflat = 0.2f; npoint = 0.5f;
                ff = Vector2.Lerp(new Vector2(-0.2f, A), new Vector2(-0.55f, A + 0.16f), k); fflat = 0f; fpoint = 1f;
                lean = -26f; head = 8f; hipOff = -0.11f;
                nsh = -70f; nel = 30f; fsh = -88f; fel = 24f;
                ball = new Vector2(0.72f, Art.BallRadius);
            }

            nearFoot = Vector2.Lerp(nearFoot, nf, w); nearFlat = Mathf.Lerp(nearFlat, nflat, w); nearPoint = Mathf.Lerp(nearPoint, npoint, w);
            farFoot = Vector2.Lerp(farFoot, ff, w); farFlat = Mathf.Lerp(farFlat, fflat, w); farPoint = Mathf.Lerp(farPoint, fpoint, w);
            nearShoulder = Mathf.Lerp(nearShoulder, nsh, w); nearElbow = Mathf.Lerp(nearElbow, nel, w);
            farShoulder = Mathf.Lerp(farShoulder, fsh, w); farElbow = Mathf.Lerp(farElbow, fel, w);
            leanTarget = Mathf.Lerp(leanTarget, lean, w);
            headTarget = Mathf.Lerp(headTarget, head, w);
            extraHipY += hipOff * w;

            if (player.StepCarry)
            {
                ballLocal = t < tD ? ball : Vector2.Lerp(ball, ballLocal, MathUtil.Smooth01((t - tD) / (tE - tD)));
                BallIsScripted = true;
            }
        }

        // ------------------------------------------------------------------ bicycle kick

        /// <summary>Backflip schedule: tip back, whip through the strike, finish the full turn.</summary>
        static float BicycleSpin(float t)
        {
            if (t < Player.BicycleSet) return 70f * MathUtil.EaseInOutSine(t / Player.BicycleSet);
            if (t < Player.BicycleContact) return Mathf.Lerp(70f, 125f, (t - Player.BicycleSet) / (Player.BicycleContact - Player.BicycleSet));
            return Mathf.Lerp(125f, 360f, MathUtil.EaseOutCubic(Mathf.Clamp01((t - Player.BicycleContact) / (Player.BicycleDuration - Player.BicycleContact))));
        }

        void PoseBicycle(float t, float hipY, ref Vector2 nearFoot, ref float nearFlat, ref float nearPoint,
            ref Vector2 farFoot, ref float farFlat, ref float farPoint,
            ref float nearShoulder, ref float nearElbow, ref float farShoulder, ref float farElbow,
            ref float leanTarget, ref float headTarget, ref Vector2 ballLocal)
        {
            float tS = Player.BicycleSet, tC = Player.BicycleContact, tE = Player.BicycleDuration;
            float w = MathUtil.Smooth01(t / 0.05f) * (1f - MathUtil.Smooth01((t - (tE - 0.1f)) / 0.1f));
            Vector2 pivot = new Vector2(0f, hipY);

            // where the ball hangs, seen from the rotating body: the kicking foot aims there
            Vector2 ballBody = pivot + MathUtil.Rotate(player.BikeBallLocal - pivot, -spin);
            Vector2 toBall = ballBody - pivot;
            Vector2 strikeAt = pivot + toBall.normalized * Mathf.Min(toBall.magnitude - 0.1f, 0.78f);

            Vector2 cocked = new Vector2(-0.12f, hipY - 0.72f);
            Vector2 tuckNear = new Vector2(0.18f, hipY - 0.42f), tuckFar = new Vector2(-0.02f, hipY - 0.46f);
            Vector2 nf, ff; float np;
            if (t < tS)
            {
                // scissor: the non-kicking leg swings up first while the kicking leg cocks low
                float k = MathUtil.EaseOutCubic(t / tS);
                nf = Vector2.Lerp(nearFoot, cocked, k); np = 0.6f;
                ff = Vector2.Lerp(farFoot, new Vector2(0.36f, hipY + 0.14f), k);
            }
            else if (t < tC)
            {
                // the kicking leg whips up to the ball as the other drops away
                float k = MathUtil.EaseInQuad((t - tS) / (tC - tS));
                nf = Vector2.Lerp(cocked, strikeAt, k); np = Mathf.Lerp(0.6f, 0.9f, k);
                ff = Vector2.Lerp(new Vector2(0.36f, hipY + 0.14f), new Vector2(-0.08f, hipY - 0.6f), k);
            }
            else
            {
                // follow through over the top, then tuck to finish the flip
                float k = MathUtil.EaseOutCubic(Mathf.Clamp01((t - tC) / 0.14f));
                float k2 = MathUtil.EaseInOutSine(Mathf.Clamp01((t - tC - 0.1f) / (tE - tC - 0.1f)));
                Vector2 over = pivot + new Vector2(0.3f, 0.62f);
                nf = Vector2.Lerp(Vector2.Lerp(strikeAt, over, k), tuckNear, k2); np = 1f;
                ff = Vector2.Lerp(new Vector2(-0.08f, hipY - 0.6f), tuckFar, k2);
            }

            nearFoot = Vector2.Lerp(nearFoot, nf, w); nearFlat *= 1f - w; nearPoint = Mathf.Lerp(nearPoint, np, w);
            farFoot = Vector2.Lerp(farFoot, ff, w); farFlat *= 1f - w; farPoint = Mathf.Lerp(farPoint, 0.8f, w);
            // arms thrown out towards the ground to brace, head watching the ball
            nearShoulder = Mathf.Lerp(nearShoulder, -95f, w); nearElbow = Mathf.Lerp(nearElbow, 18f, w);
            farShoulder = Mathf.Lerp(farShoulder, -135f, w); farElbow = Mathf.Lerp(farElbow, 14f, w);
            headTarget = Mathf.Lerp(headTarget, 26f, w);
            leanTarget = Mathf.Lerp(leanTarget, 0f, w);

            if (!player.ActionReleased)
            {
                ballLocal = player.BikeBallLocal;
                BallIsScripted = true;
            }
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

        // ------------------------------------------------------------------ keep-ups

        void PoseJuggle(float hipY, ref Vector2 nearFoot, ref float nearFlat, ref float nearPoint,
            ref Vector2 farFoot, ref float farFlat, ref float farPoint,
            ref float nearShoulder, ref float nearElbow, ref float farShoulder, ref float farElbow,
            ref float leanTarget, ref float headTarget, ref float extraHipY, ref Vector2 ballLocal)
        {
            const float A = PlayerDims.AnkleHeight;
            float w = MathUtil.Smooth01(player.ActionTime / 0.14f);
            if (player.JuggleDropped) w *= 1f - MathUtil.Smooth01(player.JuggleDropTime / Player.JuggleRecover);

            // Each body part rises to meet the ball as it drops into its touch point (reach), and after
            // a touch flicks through and settles (recoil). A whiff plays the same swing into thin air.
            float reach = player.JuggleDropped ? 0f : MathUtil.Smooth01(1f - player.JuggleTimeToContact / 0.3f);
            float recoil = 1f - MathUtil.Smooth01(player.SinceTouch / 0.26f);
            float flick = MathUtil.Bump(Mathf.Clamp01(player.SinceTouch / 0.18f));
            Player.Touch next = player.NextTouch, last = player.LastTouch;
            float wFoot = Mathf.Max(next == Player.Touch.Foot ? reach : 0f, last == Player.Touch.Foot ? recoil : 0f);
            float wKnee = Mathf.Max(next == Player.Touch.Knee ? reach : 0f, last == Player.Touch.Knee ? recoil : 0f);
            float wHead = Mathf.Max(next == Player.Touch.Head ? reach : 0f, last == Player.Touch.Head ? recoil : 0f);
            float flickFoot = last == Player.Touch.Foot ? flick : 0f;
            float flickKnee = last == Player.Touch.Knee ? flick : 0f;
            float flickHead = last == Player.Touch.Head ? flick : 0f;

            // where the part meets the ball: the coming contact while reaching, the last one while recoiling
            Vector2 contact = reach > recoil ? player.JuggleContactLocal : touchAt;
            Vector2 footAt = contact + new Vector2(-0.08f, -0.18f);
            footAt.y = Mathf.Max(footAt.y, A);

            // ready stance on soft knees; weight shifts onto the standing leg while a leg works
            float legBusy = Mathf.Max(wFoot, wKnee);
            Vector2 nf = new Vector2(0.1f, A), ff = new Vector2(Mathf.Lerp(-0.13f, -0.09f, legBusy), A);
            float nflat = 1f, npoint = 0f;
            nf = Vector2.Lerp(nf, footAt + new Vector2(0.02f, 0.06f) * flickFoot, wFoot);
            nf = Vector2.Lerp(nf, new Vector2(0.27f, 0.5f) + new Vector2(0.02f, 0.07f) * flickKnee, wKnee);
            npoint = Mathf.Lerp(npoint, 0.45f, wKnee);
            nflat = Mathf.Lerp(nflat, 0f, wKnee);

            // header: dip early while the ball is still high, stand tall into the contact, nod through
            float reachHead = next == Player.Touch.Head ? reach : 0f;
            float hipOff = -0.035f + Mathf.Sin(time * 5.5f) * 0.008f - 0.02f * legBusy;
            hipOff += -0.07f * Mathf.Sin(Mathf.PI * reachHead) + 0.05f * flickHead;

            // look at the ball
            Vector2 toBall = player.JuggleBallLocal - new Vector2(0.05f, 1.63f);
            float look = Mathf.Clamp(Mathf.Atan2(toBall.y, Mathf.Max(toBall.x, 0.05f)) * Mathf.Rad2Deg * 0.5f, -26f, 34f);
            float headT = Mathf.Lerp(look, 30f, wHead) - 14f * flickHead;
            float leanT = 2f + 8f * reachHead - 5f * flickHead;

            // arms out for balance, wider for a header
            float nsh = 28f + 12f * legBusy + 14f * wHead, nel = 44f;
            float fsh = -38f - 10f * legBusy - 14f * wHead, fel = 34f;

            nearFoot = Vector2.Lerp(nearFoot, nf, w); nearFlat = Mathf.Lerp(nearFlat, nflat, w); nearPoint = Mathf.Lerp(nearPoint, npoint, w);
            farFoot = Vector2.Lerp(farFoot, ff, w); farFlat = Mathf.Lerp(farFlat, 1f, w); farPoint = Mathf.Lerp(farPoint, 0f, w);
            nearShoulder = Mathf.Lerp(nearShoulder, nsh, w); nearElbow = Mathf.Lerp(nearElbow, nel, w);
            farShoulder = Mathf.Lerp(farShoulder, fsh, w); farElbow = Mathf.Lerp(farElbow, fel, w);
            headTarget = Mathf.Lerp(headTarget, headT, w);
            leanTarget = Mathf.Lerp(leanTarget, leanT, w);
            extraHipY += hipOff * w;

            if (!player.JuggleDropped)
            {
                ballLocal = player.JuggleBallLocal;
                BallIsScripted = true;
            }
        }

        // ------------------------------------------------------------------ flashes / invulnerability blink

        void UpdateFlash(float dt)
        {
            bool flashing = flashTimer > 0f;
            if (flashing) flashTimer -= Time.unscaledDeltaTime;
            bool blinkOff = player.InvulnTimer > 0f && !flashing && Mathf.Repeat(player.InvulnTimer, 0.14f) < 0.06f;
            for (int i = 0; i < Parts.Count; i++)
            {
                var sr = Parts[i];
                if (partMats[i] == Art.SpriteGlowMat) { sr.enabled = !flashing; continue; }
                sr.sharedMaterial = flashing ? Art.SpriteSolidMat : partMats[i];
                Color c = flashing ? new Color(1f, 0.55f, 0.6f, 1f) : partColors[i];
                if (blinkOff) c.a *= 0.35f;
                sr.color = c;
            }
        }

        /// <summary>Local (facing-right) vector → world vector for the current facing.</summary>
        public Vector2 LocalToWorldDir(Vector2 v) => new Vector2(v.x * visFacing, v.y);
    }
}
