using UnityEngine;
using UnityEngine.UI;

namespace SoccerFight
{
    /// <summary>
    /// The player as a UI puppet: the rig's own body sprites placed as images (each rotates around
    /// its sprite pivot, which is the joint) and posed every frame with the same two-bone maths the
    /// rig uses, on the character's own bones. Three moods: standing (cards), showing off (the
    /// title screen: keep-ups, or a quicker dribble for a basketball player), and one big strike
    /// on the way into the game (a kick, or a two-handed pass for the basketball players).
    /// </summary>
    public sealed class MenuFigure
    {
        public enum Mode { Idle, Juggle }

        public RectTransform Root { get; private set; }
        /// <summary>Canvas-local position of the ball centre (for the kick that starts the game).</summary>
        public Vector2 BallLocal { get; private set; }
        public bool BallVisible = true;
        public RectTransform BallRect => ball;

        Image farHand, farFore, farUpper, farShin, farThigh, farBoot, farGlow, neck, pelvis, nearShin, nearThigh, nearBoot, nearGlow, torso, tuft, head, nearHand, nearFore, nearUpper;
        RectTransform ball, ballSpin;
        Image ballShadow, ballPattern;
        PlayerLook look;
        CharacterDef def;
        PlayerBody body = PlayerBody.Soccer;
        bool hoops, hiRes;
        float scale, spin, phase;
        float kickT = -1f;

        const float A = PlayerDims.AnkleHeight;

        public void Build(Transform parent, Vector2 feet, float unitScale, PlayerLook body, CharacterDef character, bool hiResBall = false)
        {
            scale = unitScale;
            hiRes = hiResBall;
            Root = UiKit.Node("Figure", parent, feet, Vector2.zero);
            ballShadow = UiKit.Img("BallShadow", Root, UiArt.Glow, new Color(0f, 0f, 0.05f, 0.35f), Vector2.zero, new Vector2(0.5f, 0.14f) * scale);
            UiKit.Img("Shadow", Root, UiArt.Glow, new Color(0f, 0f, 0.05f, 0.45f), new Vector2(0f, 0.01f * scale), new Vector2(0.95f, 0.2f) * scale);
            farHand = Part("FarHand"); farFore = Part("FarForearm"); farUpper = Part("FarUpperArm");
            farShin = Part("FarShin"); farThigh = Part("FarThigh"); farBoot = Part("FarBoot"); farGlow = Part("FarBootGlow");
            neck = Part("Neck"); pelvis = Part("Pelvis");
            nearShin = Part("NearShin"); nearThigh = Part("NearThigh"); nearBoot = Part("NearBoot"); nearGlow = Part("NearBootGlow");
            torso = Part("Torso"); tuft = Part("HairTuft"); head = Part("Head");
            nearHand = Part("NearHand"); nearFore = Part("NearForearm"); nearUpper = Part("NearUpperArm");

            float bs = Art.BallRadius * 2.24f * scale;
            ball = UiKit.Node("Ball", Root, Vector2.zero, Vector2.one * bs);
            ballSpin = UiKit.Node("Spin", ball, Vector2.zero, Vector2.one * bs);
            // the big title figure takes the hi-res ball of the kick-off transition
            bool hi = hiResBall && MenuScenery.HeroBall != null;
            ballPattern = UiKit.Img("Pattern", ballSpin, hi ? MenuScenery.HeroBall : Art.BallPattern, Color.white, Vector2.zero, Vector2.one * bs);
            UiKit.Img("Shade", ball, hi ? MenuScenery.HeroShade : Art.BallShade, Color.white, Vector2.zero, Vector2.one * bs);
            UiKit.Img("Hi", ball, hi ? MenuScenery.HeroHighlight : Art.BallHighlight, Color.white.WithAlpha(0.85f), Vector2.zero, Vector2.one * bs);
            SetLook(body, character);
            phase = Random.value;
        }

        Image Part(string name)
        {
            var img = UiKit.Img(name, Root, null, Color.white, Vector2.zero, Vector2.one);
            img.material = Art.UiFigureMat;   // blends the soft joint edges of overlapping parts without a seam
            return img;
        }

        public void SetLook(PlayerLook body, CharacterDef character)
        {
            look = body;
            def = character;
            this.body = body.Body ?? PlayerBody.Soccer;
            hoops = body.Sport == Sport.Basketball;
            Color back = Palette.BackLimbTint;
            Bind(farHand, look.Hand, back); Bind(farFore, look.Forearm, back); Bind(farUpper, look.UpperArm, back);
            Bind(farShin, look.Shin, back); Bind(farThigh, look.Thigh, back); Bind(farBoot, look.Boot, back);
            Bind(farGlow, look.BootGlow, def.Kit.Neon.WithAlpha(0.3f));
            Bind(neck, look.Neck, Color.white); Bind(pelvis, look.Pelvis, Color.white);
            Bind(nearShin, look.Shin, Color.white); Bind(nearThigh, look.Thigh, Color.white); Bind(nearBoot, look.Boot, Color.white);
            Bind(nearGlow, look.BootGlow, def.Kit.Neon.WithAlpha(0.7f));
            Bind(torso, look.Torso, Color.white); Bind(tuft, look.HairTuft, Color.white); Bind(head, look.Head, Color.white);
            Bind(nearHand, look.Hand, Color.white);
            Bind(nearFore, PlayerArt.SpriteOf(look, PlayerPart.ForearmNear), Color.white);
            Bind(nearUpper, PlayerArt.SpriteOf(look, PlayerPart.UpperArmNear), Color.white);
            if (ballPattern != null)
                ballPattern.sprite = hoops ? (hiRes && MenuScenery.HeroHoop != null ? MenuScenery.HeroHoop : Art.HoopPattern)
                                           : (hiRes && MenuScenery.HeroBall != null ? MenuScenery.HeroBall : Art.BallPattern);
        }

        void Bind(Image img, Sprite sprite, Color tint)
        {
            img.sprite = sprite;
            img.color = tint;
            img.enabled = sprite != null;   // an Image without a sprite would draw a white box (short hair has no swinging part)
            if (sprite == null) return;
            var rt = img.rectTransform;
            rt.pivot = new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);
            rt.sizeDelta = sprite.rect.size / sprite.pixelsPerUnit * scale;
        }

        void Place(Image img, Vector2 units, float rot)
        {
            var rt = img.rectTransform;
            rt.anchoredPosition = units * scale;
            rt.localRotation = Quaternion.Euler(0f, 0f, rot);
        }

        /// <summary>Starts the big strike; the ball leaves the foot (or the hands) at KickContact.</summary>
        public void Kick() => kickT = 0f;
        public const float KickContact = 0.28f;
        public bool Kicking => kickT >= 0f;

        public void SetAlpha(float a)
        {
            var g = Root.GetComponent<CanvasGroup>();
            if (g == null) g = Root.gameObject.AddComponent<CanvasGroup>();
            g.alpha = a;
        }

        /// <param name="lookAt">-1..1: where the pointer is, the head turns a little towards it</param>
        public void Update(float dt, Mode mode, Vector2 lookAt)
        {
            if (look == null) return;
            if (kickT >= 0f) kickT += dt;
            phase += dt;
            if (hoops) UpdateHoops(dt, mode, lookAt);
            else UpdateSoccer(dt, mode, lookAt);
            if (kickT > 1.4f) kickT = -1f;
        }

        void UpdateSoccer(float dt, Mode mode, Vector2 lookAt)
        {
            float t = phase;

            // ---- keep-ups: the ball rises and falls, the near foot meets it at the bottom
            const float period = 0.95f;
            float u = Mathf.Repeat(t / period, 1f);
            bool juggling = mode == Mode.Juggle && kickT < 0f;
            float contact = juggling ? Mathf.Pow(Mathf.Clamp01(1f - Mathf.Min(u, 1f - u) / 0.2f), 2f) : 0f;
            float breathe = Mathf.Sin(t * 2.1f);

            float kick = kickT >= 0f ? kickT : -1f;
            float windup = kick >= 0f ? MathUtil.Smooth01(kick / 0.2f) * (1f - MathUtil.Smooth01((kick - 0.2f) / 0.08f)) : 0f;
            float strike = kick >= 0f ? MathUtil.Smooth01((kick - 0.2f) / 0.1f) * (1f - MathUtil.Smooth01((kick - 0.7f) / 0.4f)) : 0f;

            Vector2 hip = new Vector2(0f, body.StandHip - 0.015f - 0.012f * (breathe * 0.5f + 0.5f) - 0.03f * contact - 0.05f * windup);
            float lean = -3f + 1.2f * breathe - 4f * contact + 10f * windup - 14f * strike;

            // legs
            Vector2 nearRest = new Vector2(0.14f, A);
            Vector2 nearUp = new Vector2(0.27f, 0.27f);
            Vector2 nearAnkleTarget = Vector2.Lerp(nearRest, nearUp, contact);
            nearAnkleTarget = Vector2.Lerp(nearAnkleTarget, new Vector2(-0.3f, 0.34f), windup);
            nearAnkleTarget = Vector2.Lerp(nearAnkleTarget, new Vector2(0.48f, 0.62f), strike);
            Vector2 farAnkleTarget = new Vector2(-0.2f + 0.04f * windup, A);
            float nearBootRot = contact * 18f + strike * 30f - windup * 20f;
            PoseBody(hip, lean, nearAnkleTarget, farAnkleTarget, nearBootRot, lookAt, contact, t, mode == Mode.Juggle ? 1f : 0f);

            // arms out for balance while juggling, swinging with the kick
            float armOut = juggling ? 1f : 0f;
            float sway = Mathf.Sin(t * 2f * Mathf.PI / period) * 6f;
            float farShDeg = Mathf.Lerp(26f, 58f, armOut) + sway - windup * 40f + strike * 70f;
            float nearShDeg = Mathf.Lerp(-30f, -50f, armOut) - sway + windup * 50f - strike * 60f;
            Arm(farHand, farFore, farUpper, FarShoulder(hip, lean), farShDeg, 30f);
            Arm(nearHand, nearFore, nearUpper, NearShoulder(hip, lean), nearShDeg, 36f);

            // ---- the ball
            Vector2 b;
            if (juggling)
            {
                float height = 4f * 0.72f * u * (1f - u);
                Vector2 contactPoint = new Vector2(0.37f, 0.27f + 0.07f + Art.BallRadius);
                b = contactPoint + new Vector2(Mathf.Sin(u * Mathf.PI * 2f) * 0.03f, height);
                spin -= dt * 420f;
            }
            else
            {
                b = new Vector2(0.44f, Art.BallRadius);
                if (kick >= 0f && kick < 0.2f) b = Vector2.Lerp(b, new Vector2(0.4f, Art.BallRadius), kick / 0.2f);
                spin = Mathf.Lerp(spin, 0f, dt * 2f);
            }
            PlaceBall(b, kick);
        }

        /// <summary>
        /// A basketball player: dribbles with the near hand (quicker and lower when showing off),
        /// the free arm guarding; the strike is a two-handed chest pass straight at the camera.
        /// </summary>
        void UpdateHoops(float dt, Mode mode, Vector2 lookAt)
        {
            float t = phase;
            float R = Art.BallRadius;
            float kick = kickT >= 0f ? kickT : -1f;
            float windup = kick >= 0f ? MathUtil.Smooth01(kick / 0.2f) * (1f - MathUtil.Smooth01((kick - 0.2f) / 0.08f)) : 0f;
            float strike = kick >= 0f ? MathUtil.Smooth01((kick - 0.2f) / 0.08f) * (1f - MathUtil.Smooth01((kick - 0.7f) / 0.4f)) : 0f;
            bool show = mode == Mode.Juggle && kick < 0f;

            // the dribble: a bounce per beat, faster and lower in the show-off
            float bps = show ? 2.3f : 1.5f;
            float u = Mathf.Repeat(t * bps, 1f);
            float bounce = Mathf.Abs(Mathf.Cos(Mathf.PI * u));
            float breathe = Mathf.Sin(t * 2.1f);
            // the dribbling stance: knees bent, chest over the ball, front foot drawn back so the ball bounces
            // clear in front of the shoe (its tip is measured per character)
            float knees = show ? 0.06f : 0.04f;
            Vector2 hip = new Vector2(0f, body.StandHip - 0.02f - knees - 0.012f * (breathe * 0.5f + 0.5f) - 0.015f * (1f - bounce) - 0.07f * windup + 0.03f * strike);
            float lean = -9f - (show ? 5f : 0f) + 1.2f * breathe + 6f * windup - 12f * strike;

            Vector2 nearAnkle = new Vector2(-0.02f + 0.18f * strike, A);
            Vector2 farAnkle = new Vector2(-0.22f, A);
            PoseBody(hip, lean, nearAnkle, farAnkle, 0f, lookAt, 0f, t, 0f);

            Vector2 nearSh = NearShoulder(hip, lean), farSh = FarShoulder(hip, lean);
            float apex = hip.y - (show ? 0.2f : 0.1f);
            Vector2 dribble = new Vector2(body.Toe + 0.14f, R + (apex - R) * bounce);
            Vector2 chest = new Vector2(0.22f, hip.y + body.Shoulder.y * 0.72f);
            Vector2 pass = new Vector2(0.5f, hip.y + body.Shoulder.y * 0.78f);
            Vector2 b = dribble;
            if (kick >= 0f)
            {
                b = Vector2.Lerp(dribble, chest, MathUtil.EaseOutCubic(Mathf.Clamp01(kick / 0.2f)));
                b = Vector2.Lerp(b, pass, MathUtil.EaseInQuad(Mathf.Clamp01((kick - 0.2f) / 0.08f)));
            }

            if (kick < 0f)
            {
                // the fist rides the ball down part of the way, then waits for it on top; the far arm guards
                Vector2 riding = Palm(b, new Vector2(-0.12f, 1f));
                float waitY = Palm(new Vector2(b.x, apex), new Vector2(-0.12f, 1f)).y - 0.15f;
                Vector2 wrist = riding.y > waitY ? riding : new Vector2(riding.x, waitY);
                float onBall = 1f - MathUtil.Smooth01((wrist.y - riding.y) / 0.08f);
                ArmTo(nearHand, nearFore, nearUpper, nearSh, wrist, 0f, Vector2.Lerp(wrist + Vector2.down, b, onBall), 1f);
                Arm(farHand, farFore, farUpper, farSh, 24f + breathe * 3f, 58f);
            }
            else
            {
                // both hands on the ball, then thrown out straight after it
                float release = MathUtil.Smooth01((kick - KickContact) / 0.06f);
                Vector2 nw = Vector2.Lerp(Palm(b, new Vector2(0.1f, -1f)), nearSh + new Vector2(0.52f, 0.02f), release);
                Vector2 fw = Vector2.Lerp(Palm(b, new Vector2(-1f, 0.2f)), farSh + new Vector2(0.52f, 0.06f), release);
                ArmTo(nearHand, nearFore, nearUpper, nearSh, nw, 60f, b, 1f - release);
                ArmTo(farHand, farFore, farUpper, farSh, fw, 60f, b, 1f - release);
            }
            spin = kick >= 0f ? spin - dt * 200f : spin - dt * 60f * (show ? 1.5f : 1f);
            PlaceBall(b, kick);
        }

        Vector2 NearShoulder(Vector2 hip, float lean) => hip + MathUtil.Rotate(body.Shoulder, lean);
        Vector2 FarShoulder(Vector2 hip, float lean) => hip + MathUtil.Rotate(body.Shoulder + new Vector2(-0.05f, 0.02f), lean);

        /// <summary>Legs, pelvis, torso, neck, head and hair for a hip, a lean and two ankle targets.</summary>
        void PoseBody(Vector2 hip, float lean, Vector2 nearAnkleTarget, Vector2 farAnkleTarget, float nearBootRot, Vector2 lookAt, float contact, float t, float armOut)
        {
            Vector2 nearHip = hip + new Vector2(0.02f, 0f), farHip = hip + new Vector2(-0.025f, 0f);
            Vector2 nearKnee = MathUtil.SolveTwoBone(nearHip, nearAnkleTarget, body.ThighLen, body.ShinLen, 1f, out Vector2 nAnkle);
            Vector2 farKnee = MathUtil.SolveTwoBone(farHip, farAnkleTarget, body.ThighLen, body.ShinLen, 1f, out Vector2 fAnkle);

            Vector2 neckBase = hip + MathUtil.Rotate(body.Neck, lean);
            float headRot = lean * 0.6f + Mathf.Clamp(lookAt.y, -1f, 1f) * 5f - contact * 6f;
            Vector2 headPos = neckBase + MathUtil.Rotate(body.Head, lean * 0.6f);

            Leg(farShin, farThigh, farBoot, farGlow, farHip, farKnee, fAnkle, 0f);
            Place(neck, neckBase, lean * 0.6f);
            Place(pelvis, hip, lean * 0.35f);
            Leg(nearShin, nearThigh, nearBoot, nearGlow, nearHip, nearKnee, nAnkle, nearBootRot);
            Place(torso, hip, lean);
            Place(tuft, headPos + MathUtil.Rotate(body.Tuft, headRot), headRot + (6f * Mathf.Sin(t * 1.7f) + Mathf.Sin(t * 5f) * 3f * armOut) * body.TuftFlex);
            Place(head, headPos, headRot);
        }

        void PlaceBall(Vector2 b, float kick)
        {
            BallLocal = Root.anchoredPosition + b * scale * Root.localScale.x;
            ball.anchoredPosition = b * scale;
            ballSpin.localRotation = Quaternion.Euler(0f, 0f, spin);
            bool showBall = BallVisible && !(kick >= KickContact);
            if (ball.gameObject.activeSelf != showBall) ball.gameObject.SetActive(showBall);
            // the ball's shadow shrinks as it rises
            float air = Mathf.Clamp01((b.y - Art.BallRadius) / 1f);
            ballShadow.rectTransform.anchoredPosition = new Vector2(b.x * scale, 0.01f * scale);
            ballShadow.rectTransform.sizeDelta = new Vector2(0.5f, 0.14f) * scale * Mathf.Lerp(1f, 0.5f, air);
            ballShadow.color = new Color(0f, 0f, 0.05f, showBall ? Mathf.Lerp(0.35f, 0.12f, air) : 0f);
        }

        void Leg(Image shin, Image thigh, Image boot, Image glow, Vector2 hip, Vector2 knee, Vector2 ankle, float bootRot)
        {
            Place(shin, knee, MathUtil.DownAngle(ankle - knee));
            Place(thigh, hip, MathUtil.DownAngle(knee - hip));
            Place(boot, ankle, bootRot);
            Place(glow, ankle, bootRot);
        }

        void Arm(Image hand, Image fore, Image upper, Vector2 shoulder, float shoulderDeg, float elbowDeg, float wristDeg = 0f)
        {
            Vector2 dirU = MathUtil.Rotate(Vector2.down, shoulderDeg);
            Vector2 elbow = shoulder + dirU * body.UpperArmLen;
            Vector2 dirF = MathUtil.Rotate(dirU, elbowDeg);
            Vector2 wrist = elbow + dirF * body.ForearmLen;
            Place(hand, wrist, MathUtil.DownAngle(dirF) + wristDeg);
            Place(fore, elbow, MathUtil.DownAngle(dirF));
            Place(upper, shoulder, MathUtil.DownAngle(dirU));
        }

        /// <summary>The arm with its wrist on target (elbow down and back), the hand bent by wristDeg, or with gripW
        /// turned to point at grip (a held ball's centre: the knuckles rest on it).</summary>
        void ArmTo(Image hand, Image fore, Image upper, Vector2 shoulder, Vector2 wristTarget, float wristDeg, Vector2 grip = default, float gripW = 0f)
        {
            Vector2 elbow = MathUtil.SolveTwoBone(shoulder, wristTarget, body.UpperArmLen, body.ForearmLen, -1f, out Vector2 wrist);
            Place(upper, shoulder, MathUtil.DownAngle(elbow - shoulder));
            Place(fore, elbow, MathUtil.DownAngle(wrist - elbow));
            float handRot = MathUtil.DownAngle(wrist - elbow) + wristDeg;
            if (gripW > 0.001f && (grip - wrist).sqrMagnitude > 1e-4f) handRot = Mathf.LerpAngle(handRot, MathUtil.DownAngle(grip - wrist), gripW);
            Place(hand, wrist, handRot);
        }

        /// <summary>Wrist position for a fist resting on the ball from a side (the fist is as long as the character's).</summary>
        Vector2 Palm(Vector2 ball, Vector2 side) => ball + side.normalized * (Art.BallRadius + body.HandLen * 0.72f);
    }
}
