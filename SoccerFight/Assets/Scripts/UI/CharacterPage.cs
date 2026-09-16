using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoccerFight
{
    /// <summary>
    /// The character select of the title screen: one card per player, each with the figure built
    /// from that character's own body art, the class, a flavour line and three class bars. The card
    /// is picked the same way as every other menu button — by kicking a ball at it.
    /// </summary>
    public sealed class CharacterPage
    {
        public const float CardW = 430f, CardH = 664f, CardGap = 40f;

        sealed class Card
        {
            public RectTransform Root, FigureRoot;
            public Image Glow, Rim, Back, Wash, Plate, Flash, Check;
            public Image[] Brackets;
            public TextMeshProUGUI Name, Role, Flavour, Picked;
            public Image[,] Bars;
            public CharacterDef Def;
            public int Index;
            public float Pick, PickVel;
        }

        RectTransform root;
        CanvasGroup group;
        readonly Card[] cards = new Card[3];
        TextMeshProUGUI hint;

        public RectTransform Root => root;
        public CanvasGroup Group => group;

        /// <summary>Builds the page. register hooks every card up as a shootable menu target.</summary>
        public void Build(RectTransform parent, System.Action<RectTransform, Vector2, Vector2, System.Action, System.Action<float, float, float>> register)
        {
            root = UiKit.Node("Characters", parent, Vector2.zero, Vector2.zero);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.sizeDelta = Vector2.zero;
            group = root.gameObject.AddComponent<CanvasGroup>();

            UiKit.Label("Title", root, "SPIELER WÄHLEN", 40f, Color.white, TextAlignmentOptions.Center, new Vector2(0f, 420f), new Vector2(1200f, 56f), true, 16f);
            UiKit.Img("TitleLine", root, UiArt.LineFade, Palette.ShotCyan.WithAlpha(0.35f), new Vector2(0f, 392f), new Vector2(560f, 2f));
            hint = UiKit.Label("Hint", root, "SCHIESS AUF EINE KARTE  ·  DIE KLASSEN-BONI KOMMEN SPÄTER, HEUTE ENTSCHEIDET DIE WAHL DAS AUSSEHEN",
                14f, Palette.UiMuted, TextAlignmentOptions.Center, new Vector2(0f, 364f), new Vector2(1500f, 24f), true, 6f);

            float step = CardW + CardGap;
            for (int i = 0; i < cards.Length; i++)
            {
                var def = Characters.All[i];
                var pos = new Vector2((i - 1) * step, -40f);
                var card = BuildCard(def, i, pos);
                cards[i] = card;
                int index = i;
                register(card.Root, pos, new Vector2(CardW, CardH), () => Characters.Select(index),
                    (hover, hit, fade) => Style(card, hover, hit, fade));
            }
        }

        Card BuildCard(CharacterDef def, int index, Vector2 pos)
        {
            var card = new Card { Def = def, Index = index };
            card.Root = UiKit.Node(def.Name, root, pos, new Vector2(CardW, CardH));
            var size = new Vector2(CardW, CardH);

            card.Glow = UiKit.Img("Glow", card.Root, UiArt.Glow, def.Accent.WithAlpha(0f), Vector2.zero, size + new Vector2(220f, 220f));
            UiKit.Img("Shadow", card.Root, UiArt.Glow, new Color(0f, 0f, 0.02f, 0.5f), new Vector2(0f, -22f), size * 1.25f);
            card.Rim = UiKit.Img("Rim", card.Root, UiArt.Panel, Color.white.WithAlpha(0.14f), Vector2.zero, size + new Vector2(5f, 5f), Image.Type.Sliced);
            card.Back = UiKit.Img("Back", card.Root, UiArt.Panel, new Color(0.05f, 0.08f, 0.13f, 0.99f), Vector2.zero, size, Image.Type.Sliced);
            // colour wash behind the figure, like the portrait plate of the reference cards
            card.Wash = UiKit.Img("Wash", card.Root, UiArt.Glow, def.Accent.WithAlpha(0.22f), new Vector2(0f, 96f), new Vector2(CardW * 1.1f, 430f));
            UiKit.Img("Field", card.Root, UiArt.LineFade, Color.white.WithAlpha(0.07f), new Vector2(0f, -104f), new Vector2(CardW - 60f, 2f));

            card.FigureRoot = UiKit.Node("Figure", card.Root, new Vector2(0f, -70f), new Vector2(CardW, 400f));

            // name plate
            card.Plate = UiKit.Img("Plate", card.Root, UiArt.Pill, new Color(0.02f, 0.04f, 0.07f, 0.92f), new Vector2(0f, -128f), new Vector2(CardW - 44f, 62f), Image.Type.Sliced);
            card.Name = UiKit.Label("Name", card.Plate.rectTransform, def.Name, 34f, Color.white, TextAlignmentOptions.Center, new Vector2(0f, 2f), new Vector2(CardW - 60f, 40f), true, 12f);
            card.Role = UiKit.Label("Role", card.Root, def.Role, 16f, def.Accent, TextAlignmentOptions.Center, new Vector2(0f, -170f), new Vector2(CardW - 60f, 24f), true, 10f);
            card.Flavour = UiKit.Label("Flavour", card.Root, def.Flavour, 14f, Palette.UiMuted, TextAlignmentOptions.Center, new Vector2(0f, -200f), new Vector2(CardW - 56f, 22f), false, 2f);
            card.Flavour.textWrappingMode = TextWrappingModes.Normal;
            card.Flavour.rectTransform.sizeDelta = new Vector2(CardW - 56f, 44f);

            // three class bars, five segments each
            card.Bars = new Image[3, 5];
            string[] names = { "ANGRIFF", "ABWEHR", "TECHNIK" };
            int[] values = { def.Attack, def.Defence, def.Tech };
            Color[] cols = { Palette.Hurt, Palette.Guard, Palette.Trick };
            for (int r = 0; r < 3; r++)
            {
                float y = -246f - r * 32f;
                UiKit.Label(names[r], card.Root, names[r], 13f, Palette.UiMuted, TextAlignmentOptions.Left, new Vector2(-CardW * 0.5f + 92f, y), new Vector2(150f, 20f), true, 5f);
                for (int s = 0; s < 5; s++)
                {
                    float x = -CardW * 0.5f + 186f + s * 38f;
                    bool on = s < values[r];
                    card.Bars[r, s] = UiKit.Img("Seg", card.Root, UiArt.Pill,
                        on ? cols[r] : new Color(1f, 1f, 1f, 0.09f), new Vector2(x, y), new Vector2(30f, 14f), Image.Type.Sliced);
                }
            }

            card.Flash = UiKit.Img("Flash", card.Root, UiArt.Panel, Color.white.WithAlpha(0f), Vector2.zero, size, Image.Type.Sliced);
            card.Picked = UiKit.Label("Picked", card.Root, "GEWÄHLT", 15f, Color.white, TextAlignmentOptions.Center, new Vector2(0f, CardH * 0.5f - 26f), new Vector2(220f, 26f), true, 9f);
            card.Check = UiKit.Img("CheckBg", card.Root, UiArt.Pill, def.Accent.WithAlpha(0f), new Vector2(0f, CardH * 0.5f - 26f), new Vector2(160f, 34f), Image.Type.Sliced);
            card.Picked.transform.SetAsLastSibling();

            card.Brackets = new Image[4];
            for (int k = 0; k < 4; k++)
            {
                card.Brackets[k] = UiKit.Img("Bracket" + k, card.Root, MenuArt.Bracket, def.Accent.WithAlpha(0f), Vector2.zero, new Vector2(34f, 34f));
                card.Brackets[k].rectTransform.localRotation = Quaternion.Euler(0f, 0f, k * -90f);
            }
            return card;
        }

        bool figuresBuilt;

        /// <summary>
        /// Draws the three bodies the first time the page is opened. Two of the three characters are
        /// rasterised here, so the game starts without paying for looks nobody has asked to see.
        /// </summary>
        public void EnsureFigures()
        {
            if (figuresBuilt) return;
            figuresBuilt = true;
            for (int i = 0; i < cards.Length; i++)
                BuildFigure(cards[i].FigureRoot, PlayerArt.Get(i), cards[i].Def, 192f);
        }

        // ------------------------------------------------------------------ the figure

        /// <summary>
        /// The same skeleton the rig animates, frozen in a standing pose: the sprites pivot at their
        /// joints, so the UI only has to mirror the rig's own maths.
        /// </summary>
        static void BuildFigure(RectTransform parent, PlayerLook look, CharacterDef def, float scale)
        {
            const float A = PlayerDims.AnkleHeight;
            Vector2 hip = new Vector2(0f, PlayerDims.StandHip);
            float lean = -4f;

            // legs: near foot forward, far foot back
            Vector2 nearAnkle = new Vector2(0.17f, A), farAnkle = new Vector2(-0.2f, A);
            Vector2 nearKnee = MathUtil.SolveTwoBone(hip, nearAnkle, PlayerDims.ThighLen, PlayerDims.ShinLen, 1f, out Vector2 nAnkle);
            Vector2 farKnee = MathUtil.SolveTwoBone(hip, farAnkle, PlayerDims.ThighLen, PlayerDims.ShinLen, 1f, out Vector2 fAnkle);

            float torsoRot = lean;
            Vector2 shoulder = hip + MathUtil.Rotate(new Vector2(0f, 0.465f), torsoRot);
            Vector2 neckBase = hip + MathUtil.Rotate(new Vector2(0.03f, 0.525f), torsoRot);
            Vector2 headPos = neckBase + MathUtil.Rotate(new Vector2(0f, 0.065f), torsoRot * 0.6f);
            Vector2 nearSh = shoulder + MathUtil.Rotate(new Vector2(0.015f, -0.01f), torsoRot);
            Vector2 farSh = shoulder + MathUtil.Rotate(new Vector2(-0.035f, 0.01f), torsoRot);

            Color back = Palette.BackLimbTint;
            // back to front, exactly like the rig
            Arm(parent, look, scale, farSh, 26f, 30f, back);
            Leg(parent, look, def, scale, hip + new Vector2(-0.025f, 0f), farKnee, fAnkle, back, 0.35f);
            Place(parent, look.Neck, scale, neckBase, torsoRot * 0.6f, Color.white);
            Place(parent, look.Pelvis, scale, hip, torsoRot * 0.35f, Color.white);
            Leg(parent, look, def, scale, hip + new Vector2(0.02f, 0f), nearKnee, nAnkle, Color.white, 0.8f);
            Place(parent, look.Torso, scale, hip, torsoRot, Color.white);
            Place(parent, look.HairTuft, scale, headPos + MathUtil.Rotate(new Vector2(0.03f, 0.35f), torsoRot), torsoRot + 6f, Color.white);
            Place(parent, look.Head, scale, headPos, torsoRot, Color.white);
            Arm(parent, look, scale, nearSh, -30f, 36f, Color.white);

            // the ball rests at the front foot
            var ball = UiKit.Img("Ball", parent, Art.BallPattern, Color.white, (nAnkle + new Vector2(0.3f, Art.BallRadius - A)) * scale, Vector2.one * (Art.BallRadius * 2.2f * scale));
            UiKit.Img("BallShade", ball.rectTransform, Art.BallShade, Color.white, Vector2.zero, ball.rectTransform.sizeDelta);
        }

        static void Leg(RectTransform parent, PlayerLook look, CharacterDef def, float scale, Vector2 hip, Vector2 knee, Vector2 ankle, Color tint, float glow)
        {
            Place(parent, look.Shin, scale, knee, MathUtil.DownAngle(ankle - knee), tint);
            Place(parent, look.Thigh, scale, hip, MathUtil.DownAngle(knee - hip), tint);
            Place(parent, look.Boot, scale, ankle, 0f, tint);
            Place(parent, look.BootGlow, scale, ankle, 0f, def.Kit.Neon.WithAlpha(glow * 0.7f));
        }

        static void Arm(RectTransform parent, PlayerLook look, float scale, Vector2 shoulder, float shoulderDeg, float elbowDeg, Color tint)
        {
            Vector2 dirU = MathUtil.Rotate(Vector2.down, shoulderDeg);
            Vector2 elbow = shoulder + dirU * PlayerDims.UpperArmLen;
            Vector2 dirF = MathUtil.Rotate(dirU, elbowDeg);
            Vector2 wrist = elbow + dirF * PlayerDims.ForearmLen;
            Place(parent, look.Hand, scale, wrist, MathUtil.DownAngle(dirF), tint);
            Place(parent, look.Forearm, scale, elbow, MathUtil.DownAngle(dirF), tint);
            Place(parent, look.UpperArm, scale, shoulder, MathUtil.DownAngle(dirU), tint);
        }

        /// <summary>One body part as a UI image, rotating around the sprite's own joint pivot.</summary>
        static void Place(RectTransform parent, Sprite sprite, float scale, Vector2 posUnits, float rotDeg, Color tint)
        {
            var img = UiKit.Img(sprite.name, parent, sprite, tint, Vector2.zero, Vector2.one);
            var rt = img.rectTransform;
            rt.pivot = new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);
            rt.sizeDelta = sprite.rect.size / sprite.pixelsPerUnit * scale;
            rt.anchoredPosition = posUnits * scale;
            rt.localRotation = Quaternion.Euler(0f, 0f, rotDeg);
        }

        // ------------------------------------------------------------------ update

        /// <summary>Called by the menu for the card's own look (hover, impact, entrance).</summary>
        void Style(Card c, float hover, float hit, float fade)
        {
            bool chosen = Characters.Index == c.Index;
            MathUtil.Spring(ref c.Pick, ref c.PickVel, chosen ? 1f : 0f, 5f, 0.7f, Mathf.Min(Time.unscaledDeltaTime, 0.05f));
            float pick = Mathf.Clamp01(c.Pick);
            float h = Mathf.Clamp01(hover);
            float lift = h * 10f + pick * 14f;
            float s = 1f + h * 0.025f + pick * 0.03f - hit * 0.04f;

            c.Root.anchoredPosition = new Vector2((c.Index - 1) * (CardW + CardGap), -40f + lift);
            c.Root.localScale = new Vector3(s, s, 1f);
            c.Glow.color = c.Def.Accent.WithAlpha(fade * (0.05f + 0.2f * h + 0.25f * pick + 0.5f * hit));
            c.Rim.color = Color.Lerp(Color.white.WithAlpha(0.14f), c.Def.Accent, Mathf.Max(h * 0.8f, pick)).WithAlpha(fade * (0.18f + 0.5f * h + 0.6f * pick));
            c.Back.color = new Color(0.05f, 0.08f, 0.13f, fade * Mathf.Lerp(0.9f, 0.99f, pick));
            c.Wash.color = c.Def.Accent.WithAlpha(fade * (0.2f + 0.12f * h + 0.16f * pick));
            c.Plate.color = new Color(0.02f, 0.04f, 0.07f, fade * 0.92f);
            c.Name.color = Color.Lerp(Palette.UiText, Color.white, Mathf.Max(h, pick)).WithAlpha(fade);
            c.Role.color = c.Def.Accent.WithAlpha(fade * (0.7f + 0.3f * Mathf.Max(h, pick)));
            c.Flavour.color = Palette.UiMuted.WithAlpha(fade * (0.75f + 0.25f * h));
            c.Flash.color = Color.white.WithAlpha(hit * 0.5f * fade);
            c.FigureRoot.localScale = Vector3.one * (1f + pick * 0.04f);
            c.Check.color = c.Def.Accent.WithAlpha(fade * pick * 0.9f);
            c.Picked.color = Color.white.WithAlpha(fade * pick);
            float pad = Mathf.Lerp(20f, 6f, h) - hit * 6f;
            for (int k = 0; k < 4; k++)
            {
                float sx = k == 0 || k == 3 ? -1f : 1f;
                float sy = k <= 1 ? 1f : -1f;
                c.Brackets[k].rectTransform.anchoredPosition = new Vector2(sx * (CardW * 0.5f + pad), sy * (CardH * 0.5f + pad));
                c.Brackets[k].color = c.Def.Accent.WithAlpha(fade * (h * 0.9f + hit * 0.6f));
            }
        }
    }
}
