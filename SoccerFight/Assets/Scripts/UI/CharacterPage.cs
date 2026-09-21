using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoccerFight
{
    /// <summary>
    /// Character select as a row of player cards in the in-game card style: dark glass with a
    /// hairline frame in the player's colour, a class diamond and the record in the header, a tall
    /// portrait where the live figure stands in its own coloured light (it starts juggling when you
    /// aim at it), the name over the portrait, three class bars and a WÄHLEN / GEWÄHLT button.
    /// Cards are picked like every other menu button — by kicking the ball.
    /// </summary>
    public sealed class CharacterPage
    {
        public const float CardW = 440f, CardH = 660f, CardGap = 50f;

        sealed class Card
        {
            public RectTransform Root, Portrait;
            public Image Frame, FrameGlow, Wash, Spot;
            public TextMeshProUGUI Best, Flavour;
            public ChunkButton Pick;
            public MenuFigure Figure;
            public CharacterDef Def;
            public int Index;
            public float Chosen, ChosenVel, Jiggle;
        }

        static readonly Color Gold = new Color(1f, 0.8f, 0.4f);

        readonly Card[] cards = new Card[3];
        SubPage page;
        bool figuresBuilt;
        float time;

        public SubPage Page => page;

        public void Build(RectTransform parent, System.Action<MenuTarget> register, System.Action back)
        {
            page = new SubPage(parent, MenuPage.Characters, "SPIELER", "3 SPIELER · 3 KLASSEN", MenuArt.Accent, register, back);
            var content = page.Content;

            float step = CardW + CardGap;
            for (int i = 0; i < cards.Length; i++)
            {
                var card = BuildCard(content, Characters.All[i], i, new Vector2((i - 1) * step, -30f));
                cards[i] = card;
                int index = i;
                register(new MenuTarget
                {
                    Id = "card" + i, Root = card.Root, Size = new Vector2(CardW, CardH), Page = MenuPage.Characters,
                    Action = () => { Characters.Select(index); card.Jiggle = 1f; },
                    Draw = t => Style(card, t), Accent = card.Def.Accent,
                });
            }
        }

        static Color Soft(Color c) => Color.Lerp(c, Color.white, 0.35f);

        Card BuildCard(RectTransform parent, CharacterDef def, int index, Vector2 pos)
        {
            var c = new Card { Def = def, Index = index };
            var size = new Vector2(CardW, CardH);
            c.Root = UiKit.Node(def.Name, parent, pos, size);
            c.FrameGlow = UiKit.Img("FrameGlow", c.Root, UiArt.Glow, Gold.WithAlpha(0f), Vector2.zero, size + new Vector2(240f, 240f));
            MenuUi.Plate(c.Root, "Card", Vector2.zero, size, def.Accent, 0f);
            c.Frame = UiKit.Img("Frame", c.Root, MenuArt.Frame, Soft(def.Accent).WithAlpha(0.4f), Vector2.zero, size + new Vector2(4f, 4f), Image.Type.Sliced);

            // portrait: the player's colour as light in the dark, a pool of light on the floor
            const float portraitTop = CardH * 0.5f - 72f, portraitBottom = -66f;
            float ph = portraitTop - portraitBottom;
            float pw = CardW - 20f;
            c.Portrait = UiKit.Node("Portrait", c.Root, new Vector2(0f, (portraitTop + portraitBottom) * 0.5f), new Vector2(pw, ph));
            c.Portrait.gameObject.AddComponent<RectMask2D>();
            UiKit.Img("Back", c.Portrait, null, Color.Lerp(new Color(0.02f, 0.05f, 0.08f), def.Accent, 0.07f), Vector2.zero, new Vector2(pw, ph));
            c.Wash = UiKit.Img("Light", c.Portrait, UiArt.Glow, def.Accent.WithAlpha(0.3f), new Vector2(0f, 30f), new Vector2(560f, 560f));
            UiKit.Img("Moon", c.Portrait, UiArt.Glow, new Color(0.75f, 0.95f, 1f, 0.12f), new Vector2(90f, ph * 0.5f - 40f), new Vector2(300f, 300f));
            c.Spot = UiKit.Img("Floor", c.Portrait, UiArt.Glow, Soft(def.Accent).WithAlpha(0.35f), new Vector2(-10f, -ph * 0.5f + 34f), new Vector2(300f, 70f));
            UiKit.Img("TopShade", c.Portrait, UiArt.LineFade, new Color(0.01f, 0.03f, 0.05f, 0.6f), new Vector2(0f, ph * 0.5f), new Vector2(pw * 1.6f, 60f));
            // the figure is added on first open (EnsureFigures); name and shade sit on top of it
            var nameShade = UiKit.Img("NameShade", c.Portrait, UiArt.LineFade, new Color(0.01f, 0.03f, 0.05f, 0.75f), new Vector2(0f, -ph * 0.5f + 30f), new Vector2(pw * 1.6f, 90f));
            nameShade.transform.SetAsLastSibling();
            var name = MenuArt.Label("Name", c.Portrait, def.Name, 58f, Color.white, new Vector2(-pw * 0.5f + 24f + 200f, -ph * 0.5f + 40f), new Vector2(400f, 80f), TextAlignmentOptions.Left, 12f);
            name.transform.SetAsLastSibling();
            UiKit.Img("PortraitFrame", c.Root, MenuArt.Frame, Color.white.WithAlpha(0.08f), c.Portrait.anchoredPosition, new Vector2(pw + 2f, ph + 2f), Image.Type.Sliced);

            // header: class diamond, class name, record
            float hy = CardH * 0.5f - 38f;
            var badge = UiKit.Img("Badge", c.Root, MenuArt.Badge, Color.Lerp(def.Accent, new Color(0.05f, 0.09f, 0.14f), 0.35f), new Vector2(-CardW * 0.5f + 40f, hy), new Vector2(58f, 58f));
            var icon = UiKit.Img("ClassIcon", badge.transform, MenuArt.ClassIcon(def.Class), Color.white, Vector2.zero, new Vector2(30f, 30f));
            icon.preserveAspect = true;
            var role = MenuArt.Label("Role", c.Root, def.Role, 24f, Soft(def.Accent), new Vector2(-CardW * 0.5f + 80f + 110f, hy), new Vector2(220f, 40f), TextAlignmentOptions.Left, 6f);
            role.enableAutoSizing = true;
            role.fontSizeMin = 16f;
            role.fontSizeMax = 24f;
            UiKit.Img("Trophy", c.Root, MenuArt.IconTrophy, Gold, new Vector2(CardW * 0.5f - 96f, hy), new Vector2(30f, 30f));
            c.Best = MenuArt.Label("Best", c.Root, "0", 26f, Gold, new Vector2(CardW * 0.5f - 46f, hy), new Vector2(70f, 40f), TextAlignmentOptions.Center, 0f);

            // flavour and the three class bars
            c.Flavour = MenuArt.Label("Flavour", c.Root, def.Flavour, 18f, new Color(0.75f, 0.84f, 0.9f), new Vector2(0f, -104f), new Vector2(CardW - 50f, 50f), TextAlignmentOptions.Center, 0.5f, MenuArt.TextHeavySoft);
            c.Flavour.fontStyle = FontStyles.Normal;
            c.Flavour.textWrappingMode = TextWrappingModes.Normal;
            string[] names = { "ANGRIFF", "ABWEHR", "TECHNIK" };
            int[] values = { def.Attack, def.Defence, def.Tech };
            Color[] cols = { new Color(1f, 0.45f, 0.42f), new Color(0.45f, 0.66f, 1f), new Color(0.8f, 0.55f, 1f) };
            Sprite[] icons = { MenuArt.IconStriker, MenuArt.IconDefender, MenuArt.IconSkiller };
            for (int r = 0; r < 3; r++)
            {
                float y = -150f - r * 34f;
                var si = UiKit.Img("StatIcon", c.Root, icons[r], Soft(cols[r]), new Vector2(-CardW * 0.5f + 40f, y), new Vector2(24f, 24f));
                si.preserveAspect = true;
                MenuArt.Label(names[r], c.Root, names[r], 17f, new Color(0.85f, 0.9f, 0.94f), new Vector2(-CardW * 0.5f + 62f + 70f, y), new Vector2(140f, 30f), TextAlignmentOptions.Left, 4f, MenuArt.TextHeavySoft);
                for (int s = 0; s < 5; s++)
                {
                    float x = -CardW * 0.5f + 226f + s * 40f;
                    bool on = s < values[r];
                    UiKit.Img("Seg", c.Root, UiArt.Pill, on ? cols[r] : new Color(0.12f, 0.18f, 0.24f, 0.9f), new Vector2(x, y), new Vector2(34f, 10f), Image.Type.Sliced);
                    if (on) UiKit.Img("SegGlow", c.Root, UiArt.Glow, cols[r].WithAlpha(0.18f), new Vector2(x, y), new Vector2(60f, 30f));
                }
            }

            c.Pick = new ChunkButton(c.Root, "Pick", new Vector2(0f, -CardH * 0.5f + 52f), new Vector2(CardW - 70f, 70f),
                def.Accent, "WÄHLEN", 28f, MenuArt.IconCheck, 30f);
            c.Pick.IconLeft(92f);
            return c;
        }

        /// <summary>Builds the three live figures the first time the page is shown.</summary>
        public void EnsureFigures()
        {
            if (figuresBuilt) return;
            figuresBuilt = true;
            for (int i = 0; i < cards.Length; i++)
            {
                var c = cards[i];
                float ph = c.Portrait.sizeDelta.y;
                c.Figure = new MenuFigure();
                c.Figure.Build(c.Portrait, new Vector2(-20f, -ph * 0.5f - 138f), 230f, PlayerArt.Get(i), c.Def);
                // name and its shade stay in front of the figure
                c.Portrait.Find("NameShade").SetAsLastSibling();
                c.Portrait.Find("Name").SetAsLastSibling();
                MenuUi.SetLayer(c.Portrait, c.Root.gameObject.layer);
            }
        }

        public void RefreshRecords()
        {
            foreach (var c in cards) c.Best.text = RunState.BestStageOf(c.Index).ToString();
        }

        public void Update(float udt, Vector2 aim)
        {
            time += udt;
            page.Update(udt);
            if (!figuresBuilt || page.T < 0.01f) return;
            foreach (var c in cards)
            {
                bool lively = c.Chosen > 0.5f || c.Jiggle > 0f;
                c.Figure.Update(udt, lively ? MenuFigure.Mode.Juggle : MenuFigure.Mode.Idle, aim);
            }
        }

        void Style(Card c, MenuTarget t)
        {
            float udt = TimeFx.UiDelta;
            bool chosen = Characters.Index == c.Index;
            MathUtil.Spring(ref c.Chosen, ref c.ChosenVel, chosen ? 1f : 0f, 5f, 0.6f, udt);
            c.Jiggle = Mathf.Max(0f, c.Jiggle - udt * 1.5f);
            float pick = Mathf.Clamp01(c.Chosen);
            float h = Mathf.Clamp01(t.Hover);
            float fade = t.Fade;

            float lift = h * 10f + pick * 8f;
            float wob = Mathf.Sin(time * 20f) * c.Jiggle * 2f;
            c.Root.anchoredPosition = new Vector2((c.Index - 1) * (CardW + CardGap), -30f + lift);
            c.Root.localRotation = Quaternion.Euler(0f, 0f, wob);
            float s = 1f + h * 0.02f + t.Punch * 0.04f;
            c.Root.localScale = new Vector3(s + t.Punch * 0.015f, s - t.Punch * 0.04f, 1f);

            float pulse = 0.75f + 0.25f * Mathf.Sin(time * 2.4f);
            c.Frame.color = Color.Lerp(Soft(c.Def.Accent), Gold, pick).WithAlpha(Mathf.Lerp(0.4f + 0.4f * h, 0.95f, pick));
            c.FrameGlow.color = Gold.WithAlpha(pick * 0.22f * pulse + t.Hit * 0.35f);
            c.Wash.color = c.Def.Accent.WithAlpha(0.2f + 0.1f * h + 0.1f * pick);
            c.Spot.color = Soft(c.Def.Accent).WithAlpha(0.28f + 0.2f * pick);

            c.Pick.Filled = chosen;
            c.Pick.Color = chosen ? Gold : c.Def.Accent;
            string label = chosen ? "GEWÄHLT" : "WÄHLEN";
            if (c.Pick.Label.text != label) c.Pick.Label.text = label;
            c.Pick.Icon.enabled = chosen;
            c.Pick.Style(h, t.Hit, 0f, fade, time);
        }
    }
}
