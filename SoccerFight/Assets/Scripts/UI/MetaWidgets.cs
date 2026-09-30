using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoccerFight
{
    /// <summary>Shared colours and small pieces of the progression screens (starter pick, roster, shop).</summary>
    public static class MetaUi
    {
        public static readonly Color Gold = new Color(1f, 0.8f, 0.4f);
        public static readonly Color Muted = new Color(0.62f, 0.72f, 0.8f);
        public static readonly Color Body = new Color(0.8f, 0.87f, 0.92f);
        public static readonly Color Danger = new Color(1f, 0.45f, 0.42f);

        public static Color Soft(Color c) => Color.Lerp(c, Color.white, 0.35f);

        /// <summary>Wrapping body text (normal weight, soft shadow).</summary>
        public static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color, Vector2 pos, Vector2 box,
            TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var t = MenuArt.Label(name, parent, text, size, color, pos, box, align, 0.3f, MenuArt.TextHeavySoft);
            t.fontStyle = FontStyles.Normal;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        /// <summary>A small tinted pill with a label ("TRICK", "NUR VERTEIDIGER").</summary>
        public static TextMeshProUGUI Chip(Transform parent, string name, string text, Color color, Vector2 pos, float fontSize, out RectTransform rt)
        {
            var t = MenuArt.Label("Text", null, text, fontSize, Color.Lerp(color, Color.white, 0.25f), Vector2.zero, new Vector2(400f, fontSize * 1.8f),
                TextAlignmentOptions.Center, fontSize * 0.22f, MenuArt.TextHeavySoft);
            float w = t.GetPreferredValues(text).x + fontSize * 1.6f;
            rt = UiKit.Node(name, parent, pos, new Vector2(w, fontSize * 1.75f));
            UiKit.Img("Body", rt, UiArt.Pill, color.WithAlpha(0.14f), Vector2.zero, new Vector2(w, fontSize * 1.75f), Image.Type.Sliced);
            UiKit.Img("Rim", rt, UiArt.Pill, color.WithAlpha(0.35f), Vector2.zero, new Vector2(w + 2f, fontSize * 1.75f + 2f), Image.Type.Sliced).transform.SetAsFirstSibling();
            t.transform.SetParent(rt, false);
            t.rectTransform.sizeDelta = new Vector2(w, fontSize * 1.75f);
            t.rectTransform.anchoredPosition = new Vector2(0f, 0.5f);
            return t;
        }

        /// <summary>A glyph in a lit ring (skill icons on tiles and slots).</summary>
        public static Image Emblem(Transform parent, Sprite icon, Vector2 pos, float size, Color accent, out Image ring)
        {
            UiKit.Img("EmblemGlow", parent, UiArt.Glow, accent.WithAlpha(0.2f), pos, Vector2.one * size * 1.9f);
            UiKit.Img("EmblemDisc", parent, MenuArt.Round, new Color(0.02f, 0.05f, 0.08f, 0.85f), pos, Vector2.one * size);
            ring = UiKit.Img("EmblemRing", parent, MenuArt.RoundFrame, Soft(accent).WithAlpha(0.85f), pos, Vector2.one * (size + 4f));
            var img = UiKit.Img("Emblem", parent, icon, Color.white, pos, Vector2.one * size * 0.66f);
            img.preserveAspect = true;
            return img;
        }
    }

    /// <summary>A coin icon with an amount; red when the wallet can't cover it.</summary>
    public sealed class PriceTag
    {
        public readonly RectTransform Root;
        readonly Image icon;
        readonly TextMeshProUGUI amount;
        readonly float size;

        public PriceTag(Transform parent, Vector2 pos, float fontSize)
        {
            size = fontSize;
            Root = UiKit.Node("Price", parent, pos, new Vector2(fontSize * 6f, fontSize * 1.6f));
            icon = UiKit.Img("Coin", Root, CoinArt.Ui, Color.white, Vector2.zero, Vector2.one * fontSize * 1.25f);
            amount = MenuArt.Label("Amount", Root, "", fontSize, Color.white, Vector2.zero, new Vector2(fontSize * 6f, fontSize * 1.5f), TextAlignmentOptions.Left, 1f);
        }

        public void Set(Price p, bool affordable)
        {
            amount.text = p.ToString();
            amount.color = affordable ? Color.white : MetaUi.Danger;
            // centre coin + number as one group
            float w = amount.GetPreferredValues(amount.text).x;
            float iconW = size * 1.25f;
            float total = iconW + 6f + w;
            icon.rectTransform.anchoredPosition = new Vector2(-total * 0.5f + iconW * 0.5f, 0f);
            amount.rectTransform.anchoredPosition = new Vector2(-total * 0.5f + iconW + 6f + size * 3f, 0f);
        }

        public void SetAlpha(float a)
        {
            icon.color = Color.white.WithAlpha(a);
            amount.alpha = a;
        }

        public void SetActive(bool on) { if (Root.gameObject.activeSelf != on) Root.gameObject.SetActive(on); }
    }

    /// <summary>
    /// The live player figure inside a card portrait. The body sprites are drawn on demand a
    /// quarter at a time (PlayerArt.Pump); until they exist a soft silhouette glow holds the place,
    /// then the figure fades in.
    /// </summary>
    public sealed class PortraitSlot
    {
        readonly RectTransform parent;
        readonly CharacterDef def;
        readonly int index;
        readonly Vector2 feet;
        readonly float scale;
        readonly Image placeholder;
        MenuFigure figure;
        float appear;
        public bool Ready => figure != null;

        public PortraitSlot(RectTransform parent, CharacterDef def, Vector2 feet, float scale)
        {
            this.parent = parent;
            this.def = def;
            index = Characters.IndexOf(def);
            this.feet = feet;
            this.scale = scale;
            placeholder = UiKit.Img("Placeholder", parent, UiArt.Glow, def.Accent.WithAlpha(0.2f), feet + new Vector2(0f, scale * 0.9f), new Vector2(scale * 0.8f, scale * 1.9f));
        }

        /// <summary>Call every frame while visible; returns the figure once built.</summary>
        public void Update(float udt, MenuFigure.Mode mode, Vector2 aim, float alpha)
        {
            if (figure == null)
            {
                var look = PlayerArt.Peek(index);
                placeholder.color = def.Accent.WithAlpha((0.14f + 0.06f * Mathf.Sin(Time.unscaledTime * 3f)) * alpha);
                if (look == null) return;
                figure = new MenuFigure();
                // taller bodies stand lower in the frame, so every head sits at the same height
                float taller = (def.Body.HeadTop - PlayerBody.Soccer.HeadTop) * scale;
                figure.Build(parent, feet - new Vector2(0f, taller), scale, look, def);
                MenuUi.SetLayer(parent, parent.gameObject.layer);
                BringFront();
            }
            appear = Mathf.Min(1f, appear + udt * 3f);
            placeholder.color = def.Accent.WithAlpha(0.2f * (1f - appear) * alpha);
            figure.SetAlpha(MathUtil.EaseOutCubic(appear) * alpha);
            figure.Update(udt, mode, aim);
        }

        void BringFront()
        {
            // children named Front* are kept above the figure (in their original order)
            var list = new System.Collections.Generic.List<Transform>();
            for (int i = 0; i < parent.childCount; i++) if (parent.GetChild(i).name.StartsWith("Front")) list.Add(parent.GetChild(i));
            foreach (var t in list) t.SetAsLastSibling();
        }
    }

    /// <summary>
    /// A player card in the language of the in-game cards: header with class diamond and the name, a tall
    /// portrait with the live figure standing in its own coloured light, and a body that either shows the
    /// class strengths (the starter pick) or flavour, talent, perk and bars (the roster). Everything stays
    /// inside the painted frame (MenuUi.PanelInset). Locked characters sit behind a dark veil with a lock;
    /// the button then shows their price. The owning page fills the button.
    /// </summary>
    public sealed class CharacterCard
    {
        public enum Mode { Roster, Starter }

        public const float W = 440f, H = 660f;

        public readonly CharacterDef Def;
        public readonly RectTransform Root;
        public readonly ChunkButton Button;
        public Vector2 Home;
        public float Chosen, ChosenVel, Jiggle;
        public bool IsChosen, Locked;

        readonly RectTransform portrait;
        readonly Image frameGlow, wash, spot, veil, lockIcon, coin;
        readonly TextMeshProUGUI best;
        readonly PortraitSlot figure;
        float time;

        public CharacterCard(Transform parent, CharacterDef def, Vector2 pos, Mode mode)
        {
            Def = def;
            Home = pos;
            var cls = def.ClassDef;
            var size = new Vector2(W, H);
            var inset = MenuUi.PanelInset;
            float left = -W * 0.5f + inset.x, right = W * 0.5f - inset.z, top = H * 0.5f - inset.w, bottom = -H * 0.5f + inset.y;
            Root = UiKit.Node(def.Name, parent, pos, size);
            frameGlow = UiKit.Img("FrameGlow", Root, UiArt.Glow, MetaUi.Gold.WithAlpha(0f), Vector2.zero, size + new Vector2(240f, 240f));
            MenuUi.Plate(Root, "Card", Vector2.zero, size, def.Accent, 0f);

            // header: class diamond, the name, the record
            float hy = top - 30f;
            var badge = UiKit.Img("Badge", Root, MenuArt.Badge, Color.Lerp(def.Accent, new Color(0.05f, 0.09f, 0.14f), 0.35f), new Vector2(left + 26f, hy), new Vector2(50f, 50f));
            var icon = UiKit.Img("ClassIcon", badge.transform, cls.Icon(), Color.white, Vector2.zero, new Vector2(26f, 26f));
            icon.preserveAspect = true;
            var name = MenuArt.Label("Name", Root, def.Name, 34f, Color.white, new Vector2(left + 62f + 110f, hy + 1f), new Vector2(220f, 44f), TextAlignmentOptions.Left, 6f);
            name.enableAutoSizing = true;
            name.fontSizeMin = 24f;
            name.fontSizeMax = 34f;
            if (mode == Mode.Roster)
            {
                UiKit.Img("Trophy", Root, MenuArt.IconTrophy, Color.white, new Vector2(right - 62f, hy), new Vector2(30f, 30f)).preserveAspect = true;
                best = MenuArt.Label("Best", Root, "0", 24f, MetaUi.Gold, new Vector2(right - 24f, hy), new Vector2(44f, 36f), TextAlignmentOptions.Center, 0f);
            }

            // portrait: the player's colour as light in the dark, a pool of light on the floor
            float pTop = hy - 32f, pBottom = -8f;
            float ph = pTop - pBottom, pw = right - left;
            portrait = UiKit.Node("Portrait", Root, new Vector2((left + right) * 0.5f, (pTop + pBottom) * 0.5f), new Vector2(pw, ph));
            portrait.gameObject.AddComponent<RectMask2D>();
            UiKit.Img("Back", portrait, null, Color.Lerp(new Color(0.02f, 0.05f, 0.08f), def.Accent, 0.07f), Vector2.zero, new Vector2(pw, ph));
            wash = UiKit.Img("Light", portrait, UiArt.Glow, def.Accent.WithAlpha(0.3f), new Vector2(0f, 20f), new Vector2(520f, 520f));
            UiKit.Img("Moon", portrait, UiArt.Glow, new Color(0.75f, 0.95f, 1f, 0.12f), new Vector2(90f, ph * 0.5f - 40f), new Vector2(300f, 300f));
            spot = UiKit.Img("Floor", portrait, UiArt.Glow, MetaUi.Soft(def.Accent).WithAlpha(0.35f), new Vector2(-10f, -ph * 0.5f + 30f), new Vector2(300f, 70f));
            UiKit.Img("TopShade", portrait, UiArt.LineFade, new Color(0.01f, 0.03f, 0.05f, 0.6f), new Vector2(0f, ph * 0.5f), new Vector2(pw * 1.6f, 50f));
            // head to about the knees, the head at the same height on every card
            figure = new PortraitSlot(portrait, def, new Vector2(-20f, -ph * 0.5f - 138f), 185f);
            UiKit.Img("FrontShade", portrait, UiArt.LineFade, new Color(0.01f, 0.03f, 0.05f, 0.7f), new Vector2(0f, -ph * 0.5f + 16f), new Vector2(pw * 1.6f, 56f));
            // locked: a dark veil over the portrait with a lock (the price is on the button)
            veil = UiKit.Img("FrontVeil", portrait, null, new Color(0.01f, 0.02f, 0.04f, 0.55f), Vector2.zero, new Vector2(pw, ph));
            lockIcon = UiKit.Img("FrontLock", portrait, MenuArt.IconLock, Color.white, new Vector2(0f, 6f), new Vector2(72f, 72f));
            lockIcon.preserveAspect = true;
            UiKit.Img("PortraitEdge", Root, UiArt.LineFade, MetaUi.Soft(def.Accent).WithAlpha(0.35f), new Vector2(portrait.anchoredPosition.x, pBottom), new Vector2(pw, 2f));

            // under the picture: class and sport in the class colour
            var role = MenuArt.Label("Role", Root, cls.Name + "  ·  " + Characters.SportName(def.Sport), 15f, MetaUi.Soft(def.Accent), new Vector2(0f, pBottom - 22f), new Vector2(W - 80f, 24f),
                TextAlignmentOptions.Center, 5f, MenuArt.TextHeavySoft);
            role.enableAutoSizing = true;
            role.fontSizeMin = 11f;
            role.fontSizeMax = 15f;

            if (mode == Mode.Starter) BuildStarterBody(cls, pBottom - 44f);
            else BuildRosterBody(cls, pBottom - 44f);

            // a plain painted plate as wide as the card: the finished button pictures would be fitted in tiny
            Button = new ChunkButton(Root, "Pick", new Vector2(0f, bottom + 30f), new Vector2(W - 84f, 56f), def.Accent, "WÄHLEN", 24f, MenuArt.IconCheck, 26f)
                { PlainPlate = true };
            Button.IconLeft(40f);
            coin = UiKit.Img("Coin", Button.Face, CoinArt.Ui, Color.white, Vector2.zero, new Vector2(30f, 30f));
            coin.transform.SetSiblingIndex(Button.Label.transform.GetSiblingIndex());
            MenuUi.FrameOnTop(Root, size);
        }

        void BuildStarterBody(ClassDef cls, float y)
        {
            MenuArt.Label("Tagline", Root, cls.Tagline.ToUpperInvariant(), 18f, Color.white, new Vector2(0f, y), new Vector2(W - 80f, 28f), TextAlignmentOptions.Center, 3f);
            MenuArt.Label("Trait", Root, "TALENT  ·  " + cls.TraitName, 15f, MetaUi.Soft(Def.Accent), new Vector2(0f, y - 24f), new Vector2(W - 80f, 24f), TextAlignmentOptions.Center, 5f, MenuArt.TextHeavySoft);
            y -= 48f;
            foreach (var s in cls.Strengths)
            {
                UiKit.Img("Check", Root, MenuArt.IconCheck, Color.white, new Vector2(-W * 0.5f + 52f, y), new Vector2(20f, 20f)).preserveAspect = true;
                var t = MenuArt.Label("Strength", Root, s, 15f, MetaUi.Body, new Vector2(18f, y), new Vector2(W - 120f, 24f), TextAlignmentOptions.Left, 1.5f, MenuArt.TextHeavySoft);
                t.enableAutoSizing = true;
                t.fontSizeMin = 10f;
                t.fontSizeMax = 15f;
                y -= 21f;
            }
            // the character's own perk on top of the class talent: its name, then what it does
            if (Def.Perk != null)
            {
                MenuArt.Label("Perk", Root, "PERK  ·  " + Def.Perk.Name, 13f, MetaUi.Gold, new Vector2(0f, y), new Vector2(W - 80f, 20f), TextAlignmentOptions.Center, 4f, MenuArt.TextHeavySoft);
                var perk = MenuArt.Label("PerkText", Root, Def.Perk.Text, 13f, MetaUi.Soft(MetaUi.Gold), new Vector2(0f, y - 18f), new Vector2(W - 80f, 20f), TextAlignmentOptions.Center, 1f, MenuArt.TextHeavySoft);
                perk.enableAutoSizing = true;
                perk.fontSizeMin = 10f;
                perk.fontSizeMax = 13f;
                y -= 40f;
            }
            if (!string.IsNullOrEmpty(cls.Drawback))
                MenuArt.Label("Drawback", Root, "DAFÜR: " + cls.Drawback, 13f, MetaUi.Danger, new Vector2(0f, y), new Vector2(W - 80f, 20f), TextAlignmentOptions.Center, 3f, MenuArt.TextHeavySoft);
        }

        void BuildRosterBody(ClassDef cls, float y)
        {
            var flavour = MetaUi.Text(Root, "Flavour", Def.Flavour, 16f, new Color(0.78f, 0.86f, 0.92f), new Vector2(0f, y - 8f), new Vector2(W - 84f, 42f));
            flavour.enableAutoSizing = true;
            flavour.fontSizeMin = 12f;
            flavour.fontSizeMax = 16f;
            MenuArt.Label("Trait", Root, "TALENT  ·  " + cls.TraitName, 15f, MetaUi.Soft(Def.Accent), new Vector2(0f, y - 40f), new Vector2(W - 80f, 24f), TextAlignmentOptions.Center, 4f, MenuArt.TextHeavySoft);
            var perk = MenuArt.Label("Perk", Root, Def.Perk != null ? "PERK  ·  " + Def.Perk.Name + ":  " + Def.Perk.Text : "NUR DAS KLASSEN-TALENT", 13f,
                Def.Perk != null ? MetaUi.Gold : MetaUi.Muted, new Vector2(0f, y - 67f), new Vector2(W - 84f, 30f), TextAlignmentOptions.Center, 1f, MenuArt.TextHeavySoft);
            perk.textWrappingMode = TextWrappingModes.Normal;
            perk.enableAutoSizing = true;
            perk.fontSizeMin = 10f;
            perk.fontSizeMax = 13f;

            string[] names = { "ANGRIFF", "ABWEHR", "TECHNIK" };
            int[] values = { Def.Attack, Def.Defence, Def.Tech };
            Color[] cols = { new Color(1f, 0.45f, 0.42f), new Color(0.45f, 0.66f, 1f), new Color(0.8f, 0.55f, 1f) };
            Sprite[] icons = { MenuArt.IconStriker, MenuArt.IconDefender, MenuArt.IconSkiller };
            float x0 = -W * 0.5f + MenuUi.PanelInset.x;
            for (int r = 0; r < 3; r++)
            {
                float ry = y - 100f - r * 24f;
                UiKit.Img("StatIcon", Root, icons[r], Color.white, new Vector2(x0 + 18f, ry), new Vector2(20f, 20f)).preserveAspect = true;
                MenuArt.Label(names[r], Root, names[r], 14f, new Color(0.85f, 0.9f, 0.94f), new Vector2(x0 + 40f + 60f, ry), new Vector2(120f, 24f), TextAlignmentOptions.Left, 4f, MenuArt.TextHeavySoft);
                for (int s = 0; s < 5; s++)
                {
                    float x = x0 + 184f + s * 38f;
                    bool on = s < values[r];
                    UiKit.Img("Seg", Root, UiArt.Pill, on ? cols[r] : new Color(0.12f, 0.18f, 0.24f, 0.9f), new Vector2(x, ry), new Vector2(32f, 9f), Image.Type.Sliced);
                    if (on) UiKit.Img("SegGlow", Root, UiArt.Glow, cols[r].WithAlpha(0.16f), new Vector2(x, ry), new Vector2(56f, 26f));
                }
            }
        }

        public void SetBest(int stage) { if (best != null) best.text = stage.ToString(); }

        /// <summary>Per frame while the card is on screen.</summary>
        public void Update(float udt, Vector2 aim, float alpha)
        {
            bool lively = Chosen > 0.5f || Jiggle > 0f;
            figure.Update(udt, lively ? MenuFigure.Mode.Juggle : MenuFigure.Mode.Idle, aim, alpha);
        }

        /// <param name="price">shown with a coin instead of the icon (locked characters)</param>
        public void Style(MenuTarget t, float udt, string label, bool filled, Color buttonColor, bool iconOn, bool price = false)
        {
            time += udt;
            MathUtil.Spring(ref Chosen, ref ChosenVel, IsChosen ? 1f : 0f, 5f, 0.6f, udt);
            Jiggle = Mathf.Max(0f, Jiggle - udt * 1.5f);
            float pick = Mathf.Clamp01(Chosen);
            float h = Mathf.Clamp01(t.Hover);
            float lift = h * 10f + pick * 8f;
            float wob = Mathf.Sin(time * 20f) * Jiggle * 2f;
            Root.anchoredPosition = Home + new Vector2(0f, lift);
            Root.localRotation = Quaternion.Euler(0f, 0f, wob);
            float s = 1f + h * 0.02f + t.Punch * 0.04f;
            Root.localScale = new Vector3(s + t.Punch * 0.015f, s - t.Punch * 0.04f, 1f);

            float pulse = 0.75f + 0.25f * Mathf.Sin(time * 2.4f);
            // the chosen card glows gold around its painted frame (a tinted frame would turn olive)
            frameGlow.color = MetaUi.Gold.WithAlpha(pick * 0.2f * pulse + t.Hit * 0.35f + h * 0.05f);
            wash.color = Def.Accent.WithAlpha((0.2f + 0.1f * h + 0.1f * pick) * (Locked ? 0.5f : 1f));
            spot.color = MetaUi.Soft(Def.Accent).WithAlpha(0.28f + 0.2f * pick);

            bool veiled = Locked;
            if (veil.gameObject.activeSelf != veiled)
            {
                veil.gameObject.SetActive(veiled);
                lockIcon.gameObject.SetActive(veiled);
            }
            if (veiled) lockIcon.rectTransform.localScale = Vector3.one * (1f + 0.04f * Mathf.Sin(time * 3f) + 0.08f * h);

            Button.Filled = filled;
            Button.Color = buttonColor;
            if (Button.Label.text != label) Button.Label.text = label;
            Button.Icon.enabled = iconOn && !price;
            // price: coin and number centred as one group; otherwise the label fills the plate beside the icon
            coin.enabled = price;
            if (price)
            {
                float lw = Button.Label.GetPreferredValues(label).x;
                float group = 36f + lw;
                coin.rectTransform.anchoredPosition = new Vector2(-group * 0.5f + 15f, 0f);
                Button.Label.rectTransform.anchoredPosition = new Vector2(18f, 0f);
                Button.Label.rectTransform.sizeDelta = new Vector2(Button.Size.x - 60f, Button.Size.y);
                coin.color = Color.white.WithAlpha(t.Fade);
            }
            else if (iconOn)
            {
                Button.Label.rectTransform.anchoredPosition = new Vector2(20f, 0f);
                Button.Label.rectTransform.sizeDelta = new Vector2(Button.Size.x - 110f, Button.Size.y);
            }
            else
            {
                Button.Label.rectTransform.anchoredPosition = Vector2.zero;
                Button.Label.rectTransform.sizeDelta = new Vector2(Button.Size.x - 60f, Button.Size.y);
            }
            Button.Style(h, t.Hit, 0f, t.Fade, time);
        }
    }

    /// <summary>The player's coins in a pinned pill (shop), counting up and popping on change.</summary>
    public sealed class WalletChip
    {
        public readonly RectTransform Root;
        readonly TextMeshProUGUI amount;
        readonly Image glow;
        float shown = -1f, pop, popVel, flash = 99f;

        public WalletChip(Transform parent, Vector2 anchor, Vector2 offset)
        {
            Root = UiKit.Node("Wallet", parent, Vector2.zero, new Vector2(220f, 64f));
            MenuUi.Pin(Root, anchor, offset);
            glow = UiKit.Img("Glow", Root, UiArt.Glow, MetaUi.Gold.WithAlpha(0.15f), new Vector2(-70f, 0f), new Vector2(150f, 150f));
            UiKit.Img("Rim", Root, UiArt.Pill, MetaUi.Gold.WithAlpha(0.3f), Vector2.zero, new Vector2(212f, 54f), Image.Type.Sliced);
            UiKit.Img("Body", Root, UiArt.Pill, MenuArt.Glass, Vector2.zero, new Vector2(210f, 52f), Image.Type.Sliced);
            UiKit.Img("Coin", Root, CoinArt.Ui, Color.white, new Vector2(-76f, 0f), new Vector2(44f, 44f));
            amount = MenuArt.Label("Amount", Root, "0", 26f, Color.white, new Vector2(18f, 0f), new Vector2(140f, 44f), TextAlignmentOptions.Center, 2f);
        }

        public void Update(float udt)
        {
            int target = Wallet.Get(Currencies.Coins);
            if (shown < 0f) shown = target;
            if (Mathf.Abs(shown - target) > 0.5f)
            {
                float step = Mathf.Max(40f, Mathf.Abs(target - shown) * 6f) * udt;
                shown = Mathf.MoveTowards(shown, target, step);
                if (Mathf.Abs(shown - target) <= 0.5f) { shown = target; popVel += 8f; flash = 0f; }
            }
            amount.text = Currencies.Format(Mathf.RoundToInt(shown));
            MathUtil.Spring(ref pop, ref popVel, 0f, 5f, 0.3f, udt);
            flash += udt;
            float f = Mathf.Clamp01(1f - flash / 0.4f);
            amount.color = Color.Lerp(Color.white, MetaUi.Gold, f);
            glow.color = MetaUi.Gold.WithAlpha(0.15f + 0.3f * f);
            Root.localScale = Vector3.one * (1f + pop * 0.08f);
        }
    }
}
