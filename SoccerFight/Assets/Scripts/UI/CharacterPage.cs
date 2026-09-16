using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoccerFight
{
    /// <summary>
    /// Character select in the style of a brawler collection: one chunky card per player with a
    /// class badge and record in the header, a big striped portrait with the live figure (it starts
    /// juggling when you aim at it), the name printed over the portrait, three class bars and a
    /// WÄHLEN / GEWÄHLT button. Cards are picked like every other menu button — by kicking the ball.
    /// </summary>
    public sealed class CharacterPage
    {
        public const float CardW = 450f, CardH = 660f, CardGap = 44f;

        sealed class Card
        {
            public RectTransform Root, Portrait;
            public Image Frame, FrameGlow, Header, Wash, Bar;
            public RawImage Stripes;
            public TextMeshProUGUI Best, Flavour;
            public ChunkButton Pick;
            public MenuFigure Figure;
            public CharacterDef Def;
            public int Index;
            public float Chosen, ChosenVel, Jiggle;
        }

        readonly Card[] cards = new Card[3];
        SubPage page;
        bool figuresBuilt;
        float time;

        public SubPage Page => page;

        public void Build(RectTransform parent, System.Action<MenuTarget> register, System.Action back)
        {
            page = new SubPage(parent, MenuPage.Characters, "SPIELER", new Color(0.36f, 0.56f, 1f), register, back);
            var content = page.Content;
            MenuArt.Label("Count", content, "3 SPIELER  ·  3 KLASSEN", 24f, new Color(0.75f, 0.85f, 1f), new Vector2(0f, 385f), new Vector2(800f, 36f), TextAlignmentOptions.Center, 3f, MenuArt.TextHeavySoft);

            float step = CardW + CardGap;
            for (int i = 0; i < cards.Length; i++)
            {
                var card = BuildCard(content, Characters.All[i], i, new Vector2((i - 1) * step, -20f));
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

        Card BuildCard(RectTransform parent, CharacterDef def, int index, Vector2 pos)
        {
            var c = new Card { Def = def, Index = index };
            var size = new Vector2(CardW, CardH);
            c.Root = UiKit.Node(def.Name, parent, pos, size);
            c.FrameGlow = UiKit.Img("FrameGlow", c.Root, UiArt.Glow, Palette.Gold.WithAlpha(0f), Vector2.zero, size + new Vector2(260f, 260f));
            c.Frame = UiKit.Img("Frame", c.Root, MenuArt.Edge, Palette.Gold.WithAlpha(0f), new Vector2(0f, -4f), size + new Vector2(30f, 36f), Image.Type.Sliced);
            UiKit.Img("Keyline", c.Root, MenuArt.Edge, MenuArt.Ink, new Vector2(0f, -4f), size + new Vector2(14f, 22f), Image.Type.Sliced);
            UiKit.Img("Back", c.Root, MenuArt.CardBody, new Color(0.13f, 0.12f, 0.3f), Vector2.zero, size, Image.Type.Sliced);

            // portrait: the character's colour, diagonal stripes, a light behind the figure
            const float portraitTop = CardH * 0.5f - 76f, portraitBottom = -64f;
            float ph = portraitTop - portraitBottom;
            c.Portrait = UiKit.Node("Portrait", c.Root, new Vector2(0f, (portraitTop + portraitBottom) * 0.5f), new Vector2(CardW - 16f, ph));
            c.Portrait.gameObject.AddComponent<RectMask2D>();
            UiKit.Img("Colour", c.Portrait, null, def.Accent, Vector2.zero, new Vector2(CardW - 16f, ph));
            c.Stripes = new GameObject("Stripes", typeof(RectTransform)).AddComponent<RawImage>();
            c.Stripes.rectTransform.SetParent(c.Portrait, false);
            c.Stripes.rectTransform.sizeDelta = new Vector2(CardW - 16f, ph);
            c.Stripes.texture = MenuArt.Stripes;
            c.Stripes.uvRect = new Rect(0f, 0f, (CardW - 16f) / 64f, ph / 64f);
            c.Stripes.color = Color.white.WithAlpha(0.1f);
            c.Stripes.raycastTarget = false;
            c.Wash = UiKit.Img("Light", c.Portrait, UiArt.Glow, Color.Lerp(def.Accent, Color.white, 0.6f).WithAlpha(0.6f), new Vector2(0f, 20f), new Vector2(420f, 420f));
            UiKit.Img("Floor", c.Portrait, UiArt.Glow, new Color(0f, 0f, 0.1f, 0.35f), new Vector2(0f, -ph * 0.5f), new Vector2(CardW, 120f));
            // the figure is added on first open (EnsureFigures); name and shade sit on top of it
            var nameShade = UiKit.Img("NameShade", c.Portrait, UiArt.LineFade, new Color(0.05f, 0.03f, 0.15f, 0.55f), new Vector2(0f, -ph * 0.5f + 34f), new Vector2(CardW * 1.4f, 90f));
            nameShade.transform.SetAsLastSibling();
            var name = MenuArt.Label("Name", c.Portrait, def.Name, 70f, Color.white, new Vector2(-CardW * 0.5f + 26f + 200f, -ph * 0.5f + 44f), new Vector2(400f, 90f), TextAlignmentOptions.Left, 3f);
            name.transform.SetAsLastSibling();

            // header: class badge, class name, record
            c.Header = UiKit.Img("Header", c.Root, MenuArt.CardBody, new Color(0.09f, 0.08f, 0.22f), new Vector2(0f, CardH * 0.5f - 38f), new Vector2(CardW - 16f, 62f), Image.Type.Sliced);
            var badge = UiKit.Img("BadgeKey", c.Root, MenuArt.Badge, def.Accent, new Vector2(-CardW * 0.5f + 44f, CardH * 0.5f - 34f), new Vector2(76f, 86f));
            var icon = UiKit.Img("ClassIcon", badge.transform, MenuArt.ClassIcon(def.Class), Color.white, new Vector2(0f, 4f), new Vector2(54f, 54f));
            icon.preserveAspect = true;
            var role = MenuArt.Label("Role", c.Root, def.Role, 30f, Color.Lerp(def.Accent, Color.white, 0.55f), new Vector2(-CardW * 0.5f + 96f + 100f, CardH * 0.5f - 38f), new Vector2(200f, 50f), TextAlignmentOptions.Left, 2f);
            role.enableAutoSizing = true;
            role.fontSizeMin = 18f;
            role.fontSizeMax = 30f;
            UiKit.Img("Trophy", c.Root, MenuArt.IconTrophy, Color.white, new Vector2(CardW * 0.5f - 104f, CardH * 0.5f - 38f), new Vector2(42f, 42f));
            c.Best = MenuArt.Label("Best", c.Root, "0", 32f, Palette.Gold, new Vector2(CardW * 0.5f - 46f, CardH * 0.5f - 38f), new Vector2(80f, 50f), TextAlignmentOptions.Center, 0f);

            // flavour and the three class bars
            c.Flavour = MenuArt.Label("Flavour", c.Root, def.Flavour, 19f, new Color(0.82f, 0.86f, 1f), new Vector2(0f, -104f), new Vector2(CardW - 50f, 50f), TextAlignmentOptions.Center, 0f, MenuArt.TextHeavySoft);
            c.Flavour.fontStyle = FontStyles.Normal;
            c.Flavour.textWrappingMode = TextWrappingModes.Normal;
            string[] names = { "ANGRIFF", "ABWEHR", "TECHNIK" };
            int[] values = { def.Attack, def.Defence, def.Tech };
            Color[] cols = { new Color(1f, 0.36f, 0.4f), new Color(0.4f, 0.62f, 1f), new Color(0.8f, 0.5f, 1f) };
            Sprite[] icons = { MenuArt.IconStriker, MenuArt.IconDefender, MenuArt.IconSkiller };
            for (int r = 0; r < 3; r++)
            {
                float y = -152f - r * 36f;
                UiKit.Img("StatIcon", c.Root, icons[r], Color.white, new Vector2(-CardW * 0.5f + 40f, y), new Vector2(30f, 30f));
                MenuArt.Label(names[r], c.Root, names[r], 19f, Color.white, new Vector2(-CardW * 0.5f + 64f + 70f, y), new Vector2(140f, 30f), TextAlignmentOptions.Left, 1.5f, MenuArt.TextHeavySoft);
                for (int s = 0; s < 5; s++)
                {
                    float x = -CardW * 0.5f + 222f + s * 40f;
                    bool on = s < values[r];
                    UiKit.Img("SegKey", c.Root, MenuArt.Edge, MenuArt.Ink, new Vector2(x, y - 1f), new Vector2(38f, 24f), Image.Type.Sliced);
                    UiKit.Img("Seg", c.Root, MenuArt.Body, on ? cols[r] : new Color(0.25f, 0.24f, 0.42f), new Vector2(x, y + 1f), new Vector2(32f, 18f), Image.Type.Sliced);
                }
            }

            c.Pick = new ChunkButton(c.Root, "Pick", new Vector2(0f, -CardH * 0.5f + 52f), new Vector2(CardW - 70f, 76f),
                new Color(0.3f, 0.82f, 0.36f), "WÄHLEN", 36f, MenuArt.IconCheck, 44f);
            c.Pick.IconLeft(80f);
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
                // name, shade and floor shade stay in front of the figure
                c.Portrait.Find("NameShade").SetAsLastSibling();
                c.Portrait.Find("Name").SetAsLastSibling();
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
                var uv = c.Stripes.uvRect;
                uv.x = Mathf.Repeat(uv.x + udt * 0.15f, 1f);
                c.Stripes.uvRect = uv;
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

            float lift = h * 12f + pick * 10f;
            float wob = Mathf.Sin(time * 20f) * c.Jiggle * 3f;
            c.Root.anchoredPosition = new Vector2((c.Index - 1) * (CardW + CardGap), -20f + lift);
            c.Root.localRotation = Quaternion.Euler(0f, 0f, wob);
            float s = 1f + h * 0.025f + t.Punch * 0.05f;
            c.Root.localScale = new Vector3(s + t.Punch * 0.02f, s - t.Punch * 0.05f, 1f);

            float pulse = 0.75f + 0.25f * Mathf.Sin(time * 3f);
            c.Frame.color = Color.Lerp(Color.white, Palette.Gold, pick).WithAlpha(Mathf.Max(pick, h * 0.6f));
            c.FrameGlow.color = Palette.Gold.WithAlpha(pick * 0.3f * pulse + t.Hit * 0.4f);
            c.Wash.color = Color.Lerp(c.Def.Accent, Color.white, 0.6f).WithAlpha(0.45f + 0.25f * h + 0.2f * pick);

            c.Pick.Color = chosen ? new Color(1f, 0.74f, 0.2f) : new Color(0.3f, 0.82f, 0.36f);
            string label = chosen ? "GEWÄHLT" : "WÄHLEN";
            if (c.Pick.Label.text != label) c.Pick.Label.text = label;
            c.Pick.Icon.enabled = chosen;
            c.Pick.Style(h, t.Hit, 0f, fade, time);
        }
    }
}
