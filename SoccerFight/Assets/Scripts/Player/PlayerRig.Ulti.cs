using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Die Posen der sechs Ultis. Wie die anderen Bewegungen sind es Schlüsselposen über der Zeit; den
    /// Ball setzen sie selbst (er hängt am Fuß, liegt in den Händen oder dreht sich auf dem Finger).
    /// Alles hängt nur an ActionTime und den mitgeschickten Werten, damit der Duo-Partner dasselbe sieht.
    /// </summary>
    public sealed partial class PlayerRig
    {
        /// <summary>Der Kinomoment: der Körper spannt sich, die Haare fliegen.</summary>
        public void OnUlti() { squashXVel -= 1.2f; squashYVel += 2.4f; tuftVel -= 160f; }

        void PoseUlti(float t, float hipY, float air, ref Vector2 nearFoot, ref float nearFlat, ref float nearPoint,
            ref Vector2 farFoot, ref float farFlat, ref float farPoint,
            ref float nearShoulder, ref float nearElbow, ref float farShoulder, ref float farElbow,
            ref float leanTarget, ref float headTarget, ref float extraHipY, ref Vector2 ballLocal)
        {
            nearGripW = farGripW = 0f;
            switch (player.Ulti)
            {
                case UltiKind.Volley:
                    PoseVolley(t, hipY, air, ref nearFoot, ref nearFlat, ref nearPoint, ref farFoot, ref farFlat,
                        ref nearShoulder, ref nearElbow, ref farShoulder, ref farElbow, ref leanTarget, ref headTarget, ref extraHipY, ref ballLocal);
                    break;
                case UltiKind.Bulwark:
                    PoseBulwark(t, hipY, air, ref nearFoot, ref nearFlat, ref nearPoint, ref farFoot, ref farFlat,
                        ref nearShoulder, ref nearElbow, ref farShoulder, ref farElbow, ref leanTarget, ref headTarget, ref extraHipY);
                    break;
                case UltiKind.Storm:
                    PoseStorm(t, hipY, ref nearFoot, ref nearFlat, ref nearPoint, ref farFoot, ref farFlat, ref farPoint,
                        ref nearShoulder, ref nearElbow, ref farShoulder, ref farElbow, ref leanTarget, ref headTarget, ref ballLocal);
                    break;
                case UltiKind.Buzzer:
                    PoseBuzzer(t, hipY, air, ref nearFoot, ref farFoot, ref farShoulder, ref farElbow, ref leanTarget, ref headTarget, ref extraHipY, ref ballLocal);
                    break;
                case UltiKind.Meteor:
                    PoseDunk(t, Player.MeteorGather, Player.MeteorFlight, Player.MeteorRecover, hipY, ref nearFoot, ref nearFlat, ref nearPoint,
                        ref farFoot, ref farFlat, ref farPoint, ref nearShoulder, ref nearElbow, ref farShoulder, ref farElbow,
                        ref leanTarget, ref headTarget, ref extraHipY, ref ballLocal);
                    break;
                case UltiKind.Knockout:
                case UltiKind.Quake:
                case UltiKind.Butterfly:
                    PoseBoxUlti(t, hipY, air, ref nearFoot, ref nearFlat, ref nearPoint, ref farFoot, ref farFlat, ref farPoint,
                        ref leanTarget, ref headTarget, ref extraHipY);
                    break;
                case UltiKind.AnkleBreaker:
                    PoseAnkle(t, hipY, ref nearFoot, ref nearFlat, ref nearPoint, ref farFoot, ref farFlat, ref farPoint,
                        ref nearShoulder, ref nearElbow, ref farShoulder, ref farElbow, ref leanTarget, ref headTarget, ref extraHipY, ref ballLocal);
                    break;
            }
        }

        // ------------------------------------------------------------------ RIO

        /// <summary>
        /// Der Ball wird mit der Fußspitze gelupft und steigt hoch über den Kopf; Rio schaut ihm nach,
        /// holt aus und trifft ihn im Fallen auf Hüfthöhe (die Schusspendel des Power-Schusses).
        /// </summary>
        void PoseVolley(float t, float hipY, float air, ref Vector2 nearFoot, ref float nearFlat, ref float nearPoint,
            ref Vector2 farFoot, ref float farFlat,
            ref float nearShoulder, ref float nearElbow, ref float farShoulder, ref float farElbow,
            ref float leanTarget, ref float headTarget, ref float extraHipY, ref Vector2 ballLocal)
        {
            const float A = PlayerDims.AnkleHeight;
            float R = Art.BallRadius;
            const float tPop = 0.1f, tKick = 0.2f;
            Vector2 start = new Vector2(0.42f, R), onToe = new Vector2(0.42f, 0.45f);
            Vector2 contact = player.KickBallLocal;
            Vector2 b;
            if (t < tPop) b = Vector2.Lerp(start, onToe, MathUtil.Smooth01(t / tPop));
            else
            {
                // ein echter Bogen: hoch über den Kopf, dann fällt er in den Volley
                float k = Mathf.Clamp01((t - tPop) / (Player.VolleyContact - tPop));
                b = new Vector2(Mathf.Lerp(onToe.x, contact.x, k), Mathf.Lerp(onToe.y, contact.y, k) + 4f * 1.35f * k * (1f - k));
            }
            if (t < Player.VolleyContact)
            {
                ballLocal = b;
                BallIsScripted = true;
            }

            if (t < tKick)
            {
                // die Fußspitze geht unter den Ball und hebt ihn an, dann setzt der Fuß wieder ab
                float lift = t < tPop ? 1f : 1f - MathUtil.Smooth01((t - tPop) / (tKick - tPop));
                Vector2 toe = (t < tPop ? b : onToe) - new Vector2(0.12f, R + 0.02f);
                nearFoot = Vector2.Lerp(new Vector2(0.12f, A), toe, lift);
                nearFlat = 0f;
                nearPoint = Mathf.Lerp(0.1f, 0.25f, lift);
                farFoot = Vector2.Lerp(farFoot, new Vector2(-0.08f, A), 1f - air);
                farFlat = 1f - air;
                nearShoulder = Mathf.Lerp(nearShoulder, 30f, 0.8f); nearElbow = Mathf.Lerp(nearElbow, 45f, 0.8f);
                farShoulder = Mathf.Lerp(farShoulder, 55f, 0.8f); farElbow = Mathf.Lerp(farElbow, 40f, 0.8f);
                leanTarget = Mathf.Lerp(leanTarget, 6f, 0.8f);
            }
            else
            {
                PoseKick(t - tKick, 0.12f, Player.VolleyContact - tKick, 0.42f, Player.VolleyEnd - tKick, 1f, hipY, air,
                    ref nearFoot, ref nearFlat, ref nearPoint, ref farFoot, ref farFlat,
                    ref nearShoulder, ref nearElbow, ref farShoulder, ref farElbow, ref leanTarget, ref extraHipY);
            }
            // der Blick folgt dem Ball
            if (t < Player.VolleyContact)
            {
                float up = Mathf.Clamp01((b.y - 1.6f) / 1.2f);
                headTarget = Mathf.Lerp(headTarget, 6f + 22f * up, 0.9f);
            }
        }

        // ------------------------------------------------------------------ BRUNO

        /// <summary>Knie hoch, Arme weit auseinander – dann der Stampfer: tief in den Stand, die Fäuste nach unten.</summary>
        void PoseBulwark(float t, float hipY, float air, ref Vector2 nearFoot, ref float nearFlat, ref float nearPoint,
            ref Vector2 farFoot, ref float farFlat,
            ref float nearShoulder, ref float nearElbow, ref float farShoulder, ref float farElbow,
            ref float leanTarget, ref float headTarget, ref float extraHipY)
        {
            const float A = PlayerDims.AnkleHeight;
            float tS = Player.BulwarkStomp, tE = Player.BulwarkEnd;
            if (!player.ActionReleased)
            {
                float k = MathUtil.Smooth01(t / tS);
                nearFoot = Vector2.Lerp(nearFoot, new Vector2(0.26f, hipY - 0.2f), k);
                nearFlat = 0f;
                nearPoint = Mathf.Lerp(nearPoint, 0.35f, k);
                farFoot = Vector2.Lerp(farFoot, new Vector2(-0.12f, A), k * (1f - air));
                farFlat = Mathf.Lerp(farFlat, 1f, k * (1f - air));
                nearShoulder = Mathf.Lerp(nearShoulder, 122f, k); nearElbow = Mathf.Lerp(nearElbow, 48f, k);
                farShoulder = Mathf.Lerp(farShoulder, -128f, k); farElbow = Mathf.Lerp(farElbow, -40f, k);
                leanTarget = Mathf.Lerp(leanTarget, 10f, k);
                headTarget = Mathf.Lerp(headTarget, 12f, k);
                extraHipY += 0.05f * k * (1f - air);
                return;
            }
            float u = Mathf.Clamp01((t - tS) / (tE - tS));
            float slam = MathUtil.Smooth01(u / 0.12f);
            float crouch = 1f - MathUtil.Smooth01((u - 0.35f) / 0.65f);
            float w = 1f - MathUtil.Smooth01((u - 0.8f) / 0.2f);
            nearFoot = Vector2.Lerp(nearFoot, new Vector2(0.34f, A), w);
            farFoot = Vector2.Lerp(farFoot, new Vector2(-0.3f, A), w);
            nearFlat = Mathf.Lerp(nearFlat, 1f, w); farFlat = Mathf.Lerp(farFlat, 1f, w);
            nearPoint = Mathf.Lerp(nearPoint, 0f, w);
            extraHipY += -0.26f * crouch * w;
            leanTarget = Mathf.Lerp(leanTarget, -18f * crouch, w);
            headTarget = Mathf.Lerp(headTarget, -8f * crouch, w);
            nearShoulder = Mathf.Lerp(nearShoulder, Mathf.Lerp(122f, -22f, slam), w); nearElbow = Mathf.Lerp(nearElbow, 32f, w);
            farShoulder = Mathf.Lerp(farShoulder, Mathf.Lerp(-128f, -38f, slam), w); farElbow = Mathf.Lerp(farElbow, 36f, w);
        }

        // ------------------------------------------------------------------ MIRA

        /// <summary>
        /// Der Ball wird vom Spann hochgelupft und steigt über den Kopf, die Arme gehen mit; dann schwebt Mira
        /// mit erhobenen Armen, während die Zauberbälle um sie kreisen, und zeigt jedem Ball sein Ziel.
        /// </summary>
        void PoseStorm(float t, float hipY, ref Vector2 nearFoot, ref float nearFlat, ref float nearPoint,
            ref Vector2 farFoot, ref float farFlat, ref float farPoint,
            ref float nearShoulder, ref float nearElbow, ref float farShoulder, ref float farElbow,
            ref float leanTarget, ref float headTarget, ref Vector2 ballLocal)
        {
            float R = Art.BallRadius;
            float tSp = Player.StormSplit, tL = Player.StormLaunch, tE = Player.StormEnd;
            float w = MathUtil.Smooth01(t / 0.06f) * (1f - MathUtil.Smooth01((t - (tE - 0.15f)) / 0.15f));
            const float tToe = 0.16f;
            Vector2 start = new Vector2(0.42f, R), knee = new Vector2(0.38f, 0.62f), top = new Vector2(0.16f, body.HeadTop + 0.55f);
            if (t < tSp)
            {
                Vector2 b = t < tToe ? Vector2.Lerp(start, knee, MathUtil.Smooth01(t / tToe))
                                     : Vector2.Lerp(knee, top, MathUtil.EaseOutCubic((t - tToe) / (tSp - tToe)));
                ballLocal = b;
                BallIsScripted = true;
                if (t < tToe + 0.04f)
                {
                    float lift = 1f - MathUtil.Smooth01((t - tToe) / 0.04f);
                    nearFoot = Vector2.Lerp(nearFoot, b - new Vector2(0.1f, R + 0.03f), lift);
                    nearFlat = Mathf.Lerp(nearFlat, 0f, lift);
                    nearPoint = Mathf.Lerp(nearPoint, 0.3f, lift);
                }
                headTarget = Mathf.Lerp(headTarget, 4f + 22f * Mathf.Clamp01((b.y - 1.5f) / 1.3f), w);
            }
            // die Beine schweben locker, die Zehen zeigen nach unten
            farPoint = Mathf.Lerp(farPoint, 0.95f, w);
            nearPoint = Mathf.Lerp(nearPoint, 0.75f, w * MathUtil.Smooth01((t - tToe) / 0.1f));
            farFoot = Vector2.Lerp(farFoot, new Vector2(-0.16f, hipY - body.ThighLen - body.ShinLen * 0.92f), w * MathUtil.Smooth01(t / 0.3f));

            float raise = MathUtil.Smooth01((t - 0.12f) / 0.3f) * w;
            float sway = Mathf.Sin(t * 6.5f);
            if (t < tL)
            {
                nearShoulder = Mathf.Lerp(nearShoulder, 152f + 12f * sway, raise); nearElbow = Mathf.Lerp(nearElbow, 30f + 10f * sway, raise);
                farShoulder = Mathf.Lerp(farShoulder, 138f - 12f * sway, raise); farElbow = Mathf.Lerp(farElbow, 34f - 8f * sway, raise);
                leanTarget = Mathf.Lerp(leanTarget, 7f, raise);
                if (t >= tSp) headTarget = Mathf.Lerp(headTarget, 6f, w);
            }
            else
            {
                // der vordere Arm zeigt auf das Ziel des Balls, der hintere hält das Gleichgewicht
                Vector2 point = player.UltiPoint.sqrMagnitude > 0.01f ? player.UltiPoint.normalized : new Vector2(1f, 0.3f);
                float aim = MathUtil.DownAngle(point);
                nearShoulder = Mathf.Lerp(nearShoulder, aim, w); nearElbow = Mathf.Lerp(nearElbow, 6f, w);
                farShoulder = Mathf.Lerp(farShoulder, 70f, w); farElbow = Mathf.Lerp(farElbow, 28f, w);
                leanTarget = Mathf.Lerp(leanTarget, -4f, w);
                headTarget = Mathf.Lerp(headTarget, Mathf.Clamp(MathUtil.Angle(point) * 0.35f, -14f, 20f), w);
            }
        }

        // ------------------------------------------------------------------ DRE

        /// <summary>Der Sprungwurf: in die Knie, hoch mit dem Ball über die Stirn, am Scheitel loslassen, der Arm bleibt oben.</summary>
        void PoseBuzzer(float t, float hipY, float air, ref Vector2 nearFoot, ref Vector2 farFoot,
            ref float farShoulder, ref float farElbow, ref float leanTarget, ref float headTarget, ref float extraHipY, ref Vector2 ballLocal)
        {
            float tJ = Player.BuzzerJump, tR = Player.BuzzerRelease;
            Vector2 sh = ShoulderAt(hipY);
            Vector2 chestB = new Vector2(0.2f, hipY + body.Shoulder.y * 0.72f);
            Vector2 setB = sh + new Vector2(0.06f, 0.4f);
            if (t < tJ)
            {
                float k = MathUtil.Smooth01(t / tJ);
                extraHipY += -0.13f * k * (1f - air);
                leanTarget = Mathf.Lerp(leanTarget, -6f, k);
            }
            if (!player.ActionReleased)
            {
                Vector2 b = t < tJ ? Vector2.Lerp(player.KickBallLocal, chestB, MathUtil.EaseOutCubic(t / tJ))
                                   : Vector2.Lerp(chestB, setB, MathUtil.EaseInOutSine(Mathf.Clamp01((t - tJ) / (tR - tJ))));
                ballLocal = b;
                BallIsScripted = true;
                GripNear(b, new Vector2(-0.15f, -1f));
                GripFar(b, new Vector2(-1f, 0.1f));
                headTarget = Mathf.Lerp(headTarget, 16f, 0.9f);
                // im Steigen zieht das vordere Knie hoch
                float rise = MathUtil.Smooth01((t - tJ) / 0.12f) * air;
                nearFoot = Vector2.Lerp(nearFoot, new Vector2(0.18f, hipY - 0.46f), rise);
            }
            else
            {
                Vector2 aim = player.KickAimLocal.sqrMagnitude > 0.01f ? player.KickAimLocal.normalized : new Vector2(0.55f, 0.85f);
                nearIK = sh + aim * Reach * 0.97f;
                nearIKw = 1f;
                nearWrist = 75f * MathUtil.Smooth01((t - tR) / 0.06f);
                farShoulder = Mathf.Lerp(farShoulder, 60f, 0.9f);
                farElbow = Mathf.Lerp(farElbow, 40f, 0.9f);
                headTarget = Mathf.Lerp(headTarget, 20f, 0.9f);
                leanTarget = Mathf.Lerp(leanTarget, 8f, 0.9f);
            }
        }

        // ------------------------------------------------------------------ NOVA

        /// <summary>
        /// Tief im Ausfallschritt, der Ball wechselt bei jedem Sprung zwischen den Beinen die Seite; am Ende
        /// steht Nova aufrecht, die Hand in der Hüfte, und lässt den Ball auf dem Zeigefinger kreiseln.
        /// </summary>
        void PoseAnkle(float t, float hipY, ref Vector2 nearFoot, ref float nearFlat, ref float nearPoint,
            ref Vector2 farFoot, ref float farFlat, ref float farPoint,
            ref float nearShoulder, ref float nearElbow, ref float farShoulder, ref float farElbow,
            ref float leanTarget, ref float headTarget, ref float extraHipY, ref Vector2 ballLocal)
        {
            const float A = PlayerDims.AnkleHeight;
            float R = Art.BallRadius;
            int steps = Mathf.Max(1, player.UltiSteps);
            float tC = Player.AnkleCrouch, hop = Player.AnkleHop;
            float poseAt = tC + steps * hop, tE = poseAt + Player.AnklePose;
            Vector2 front = new Vector2(0.3f, hipY - 0.26f), back = new Vector2(0f, hipY - 0.3f);
            if (t < poseAt)
            {
                float w = MathUtil.Smooth01(t / 0.05f);
                nearFoot = Vector2.Lerp(nearFoot, new Vector2(0.34f, A), w);
                farFoot = Vector2.Lerp(farFoot, new Vector2(-0.32f, A), w);
                nearFlat = Mathf.Lerp(nearFlat, 0.8f, w); farFlat = Mathf.Lerp(farFlat, 0.5f, w);
                nearPoint = Mathf.Lerp(nearPoint, 0f, w); farPoint = Mathf.Lerp(farPoint, 0.45f, w);
                extraHipY += -0.17f * w;
                leanTarget = Mathf.Lerp(leanTarget, -18f, w);
                headTarget = Mathf.Lerp(headTarget, -2f, w);
                int i = t < tC ? 0 : Mathf.FloorToInt((t - tC) / hop);
                float u = t < tC ? Mathf.Clamp01(t / tC) : Mathf.Repeat((t - tC) / hop, 1f);
                int half = i % 2;
                Vector2 from = half == 0 ? front : back, to = half == 0 ? back : front;
                float by = R + Mathf.Lerp(from.y - R, to.y - R, u) * Mathf.Abs(Mathf.Cos(Mathf.PI * u));
                Vector2 b = new Vector2(Mathf.Lerp(from.x, to.x, u), by);
                ballLocal = b;
                BallIsScripted = true;
                float nearOn = half == 0 ? 1f - MathUtil.Smooth01(u / 0.45f) : MathUtil.Smooth01((u - 0.55f) / 0.45f);
                float farOn = 1f - nearOn;
                nearIK = Vector2.Lerp(new Vector2(0.18f, hipY - 0.08f), Palm(b, new Vector2(-0.1f, 1f)), nearOn);
                nearIKw = w;
                nearWrist = 25f;
                nearGrip = b; nearGripW = w * nearOn;
                farIK = Vector2.Lerp(new Vector2(-0.12f, hipY - 0.06f), Palm(b, new Vector2(-0.2f, 1f)), farOn);
                farIKw = w;
                farWrist = 25f;
                farGrip = b; farGripW = w * farOn;
                return;
            }
            float k = MathUtil.Smooth01((t - poseAt) / 0.14f);
            float wp = k * (1f - MathUtil.Smooth01((t - (tE - 0.12f)) / 0.12f));
            nearFoot = Vector2.Lerp(nearFoot, new Vector2(0.1f, A), wp);
            farFoot = Vector2.Lerp(farFoot, new Vector2(-0.17f, A), wp);
            nearFlat = Mathf.Lerp(nearFlat, 1f, wp); farFlat = Mathf.Lerp(farFlat, 1f, wp);
            extraHipY += (0.02f + 0.015f * Mathf.Sin(t * 9f)) * wp;
            leanTarget = Mathf.Lerp(leanTarget, 5f, wp);
            headTarget = Mathf.Lerp(headTarget, 18f, wp);
            nearShoulder = Mathf.Lerp(nearShoulder, 176f, wp); nearElbow = Mathf.Lerp(nearElbow, 4f, wp);
            farShoulder = Mathf.Lerp(farShoulder, -32f, wp); farElbow = Mathf.Lerp(farElbow, 112f, wp);
            nearIKw = farIKw = 0f;
            // der Ball springt aus dem Dribbling hoch auf die Fingerspitze und dreht sich dort
            Vector2 sh = ShoulderAt(hipY);
            Vector2 finger = sh + new Vector2(0.03f, Reach + body.HandLen + R * 0.85f);
            float hopUp = MathUtil.EaseOutCubic(Mathf.Clamp01((t - poseAt) / 0.18f));
            Vector2 lift = Vector2.Lerp(front, finger, hopUp) + new Vector2(0f, 0.35f * MathUtil.Bump(hopUp));
            // am Ende fällt er zurück in die Dribbelhand
            float hold = 1f - MathUtil.Smooth01((t - (tE - 0.12f)) / 0.12f);
            ballLocal = Vector2.Lerp(ballLocal, lift, hold);
            BallIsScripted = true;
        }
    }
}
