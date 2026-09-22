using UnityEngine;
using UnityEngine.UI;

namespace SoccerFight
{
    /// <summary>
    /// The player as a UI puppet: the rig's own body sprites placed as images (each rotates around
    /// its sprite pivot, which is the joint) and posed every frame with the same two-bone maths the
    /// rig uses. Three moods: standing with the ball at the foot (cards), keep-ups (the title
    /// screen), and one big kick (the way into the game).
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
        Image ballShadow;
        PlayerLook look;
        CharacterDef def;
        float scale, spin, phase;
        float kickT = -1f;

        const float A = PlayerDims.AnkleHeight;

        public void Build(Transform parent, Vector2 feet, float unitScale, PlayerLook body, CharacterDef character, bool hiResBall = false)
        {
            scale = unitScale;
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
            UiKit.Img("Pattern", ballSpin, hi ? MenuScenery.HeroBall : Art.BallPattern, Color.white, Vector2.zero, Vector2.one * bs);
            UiKit.Img("Shade", ball, hi ? MenuScenery.HeroShade : Art.BallShade, Color.white, Vector2.zero, Vector2.one * bs);
            UiKit.Img("Hi", ball, hi ? MenuScenery.HeroHighlight : Art.BallHighlight, Color.white.WithAlpha(0.85f), Vector2.zero, Vector2.one * bs);
            SetLook(body, character);
            phase = Random.value;
        }

        Image Part(string name)
        {
            var img = UiKit.Img(name, Root, null, Color.white, Vector2.zero, Vector2.one);
            return img;
        }

        public void SetLook(PlayerLook body, CharacterDef character)
        {
            look = body;
            def = character;
            Color back = Palette.BackLimbTint;
            Bind(farHand, look.Hand, back); Bind(farFore, look.Forearm, back); Bind(farUpper, look.UpperArm, back);
            Bind(farShin, look.Shin, back); Bind(farThigh, look.Thigh, back); Bind(farBoot, look.Boot, back);
            Bind(farGlow, look.BootGlow, def.Kit.Neon.WithAlpha(0.3f));
            Bind(neck, look.Neck, Color.white); Bind(pelvis, look.Pelvis, Color.white);
            Bind(nearShin, look.Shin, Color.white); Bind(nearThigh, look.Thigh, Color.white); Bind(nearBoot, look.Boot, Color.white);
            Bind(nearGlow, look.BootGlow, def.Kit.Neon.WithAlpha(0.7f));
            Bind(torso, look.Torso, Color.white); Bind(tuft, look.HairTuft, Color.white); Bind(head, look.Head, Color.white);
            Bind(nearHand, look.Hand, Color.white); Bind(nearFore, look.Forearm, Color.white); Bind(nearUpper, look.UpperArm, Color.white);
        }

        void Bind(Image img, Sprite sprite, Color tint)
        {
            img.sprite = sprite;
            img.color = tint;
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

        /// <summary>Starts the big kick; the ball leaves the foot at KickContact.</summary>
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

            Vector2 hip = new Vector2(0f, PlayerDims.StandHip - 0.015f - 0.012f * (breathe * 0.5f + 0.5f) - 0.03f * contact - 0.05f * windup);
            float lean = -3f + 1.2f * breathe - 4f * contact + 10f * windup - 14f * strike;

            // legs
            Vector2 nearRest = new Vector2(0.14f, A);
            Vector2 nearUp = new Vector2(0.27f, 0.27f);
            Vector2 nearAnkleTarget = Vector2.Lerp(nearRest, nearUp, contact);
            nearAnkleTarget = Vector2.Lerp(nearAnkleTarget, new Vector2(-0.3f, 0.34f), windup);
            nearAnkleTarget = Vector2.Lerp(nearAnkleTarget, new Vector2(0.48f, 0.62f), strike);
            Vector2 farAnkleTarget = new Vector2(-0.2f + 0.04f * windup, A);
            Vector2 nearHip = hip + new Vector2(0.02f, 0f), farHip = hip + new Vector2(-0.025f, 0f);
            Vector2 nearKnee = MathUtil.SolveTwoBone(nearHip, nearAnkleTarget, PlayerDims.ThighLen, PlayerDims.ShinLen, 1f, out Vector2 nAnkle);
            Vector2 farKnee = MathUtil.SolveTwoBone(farHip, farAnkleTarget, PlayerDims.ThighLen, PlayerDims.ShinLen, 1f, out Vector2 fAnkle);
            float nearBootRot = contact * 18f + strike * 30f - windup * 20f;

            // upper body
            Vector2 shoulder = hip + MathUtil.Rotate(new Vector2(0f, 0.465f), lean);
            Vector2 neckBase = hip + MathUtil.Rotate(new Vector2(0.03f, 0.525f), lean);
            float headRot = lean * 0.6f + Mathf.Clamp(lookAt.y, -1f, 1f) * 5f - contact * 6f;
            Vector2 headPos = neckBase + MathUtil.Rotate(new Vector2(0f, 0.065f), lean * 0.6f);
            Vector2 nearSh = shoulder + MathUtil.Rotate(new Vector2(0.015f, -0.01f), lean);
            Vector2 farSh = shoulder + MathUtil.Rotate(new Vector2(-0.035f, 0.01f), lean);
            // arms out for balance while juggling, swinging with the kick
            float armOut = juggling ? 1f : 0f;
            float sway = Mathf.Sin(t * 2f * Mathf.PI / period) * 6f;
            float farShDeg = Mathf.Lerp(26f, 58f, armOut) + sway - windup * 40f + strike * 70f;
            float nearShDeg = Mathf.Lerp(-30f, -50f, armOut) - sway + windup * 50f - strike * 60f;

            Arm(farHand, farFore, farUpper, farSh, farShDeg, 30f);
            Leg(farShin, farThigh, farBoot, farGlow, farHip, farKnee, fAnkle, 0f);
            Place(neck, neckBase, lean * 0.6f);
            Place(pelvis, hip, lean * 0.35f);
            Leg(nearShin, nearThigh, nearBoot, nearGlow, nearHip, nearKnee, nAnkle, nearBootRot);
            Place(torso, hip, lean);
            Place(tuft, headPos + MathUtil.Rotate(new Vector2(0.03f, 0.35f), headRot), headRot + 6f + Mathf.Sin(t * 5f) * 3f * armOut);
            Place(head, headPos, headRot);
            Arm(nearHand, nearFore, nearUpper, nearSh, nearShDeg, 36f);

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
            if (kickT > 1.4f) kickT = -1f;
        }

        void Leg(Image shin, Image thigh, Image boot, Image glow, Vector2 hip, Vector2 knee, Vector2 ankle, float bootRot)
        {
            Place(shin, knee, MathUtil.DownAngle(ankle - knee));
            Place(thigh, hip, MathUtil.DownAngle(knee - hip));
            Place(boot, ankle, bootRot);
            Place(glow, ankle, bootRot);
        }

        void Arm(Image hand, Image fore, Image upper, Vector2 shoulder, float shoulderDeg, float elbowDeg)
        {
            Vector2 dirU = MathUtil.Rotate(Vector2.down, shoulderDeg);
            Vector2 elbow = shoulder + dirU * PlayerDims.UpperArmLen;
            Vector2 dirF = MathUtil.Rotate(dirU, elbowDeg);
            Vector2 wrist = elbow + dirF * PlayerDims.ForearmLen;
            Place(hand, wrist, MathUtil.DownAngle(dirF));
            Place(fore, elbow, MathUtil.DownAngle(dirF));
            Place(upper, shoulder, MathUtil.DownAngle(dirU));
        }
    }
}
