using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SoccerFight
{
    /// <summary>
    /// Every second round: three upgrade cards flip in (rarity colour, icon, values, stack level) and
    /// the player takes exactly one — click or 1/2/3. After a boss: two big ability cards. The game
    /// is frozen while this is open; everything animates on unscaled time.
    /// </summary>
    public sealed class RewardScreen
    {
        sealed class Card
        {
            public RectTransform rt, content, emblem;
            public CanvasGroup group;
            public Image halo, shadow, shine, medGlow, veil;
            public Image[] motes, rays;
            public Vector3[] moteSeed;   // x, Geschwindigkeit, Phase
            public Color color, glowColor;
            public Vector2 size;
            public float delay, revealT, lift;
            /// <summary>0 gewöhnlich … 3 legendär. Fähigkeitskarten haben ihren eigenen, goldenen Auftritt.</summary>
            public int tier;
            public bool ability;
            public UiAnim anim;
        }

        // Wie stark jede Seltenheit auftritt: Rahmen eingefärbt, Schein um die Karte, Licht oben, Schein hinter dem Bild.
        static readonly float[] FrameTint = { 0f, 0.2f, 0.28f, 0.34f };
        static readonly float[] HaloBase = { 0f, 0.14f, 0.2f, 0.26f };
        static readonly float[] TopLight = { 0.015f, 0.035f, 0.05f, 0.07f };
        static readonly float[] EmblemGlow = { 0.035f, 0.08f, 0.11f, 0.14f };
        static readonly int[] MoteCount = { 0, 0, 6, 10 };

        Canvas canvas;
        CanvasGroup rootGroup;
        RectTransform root, cardRoot;
        Image dim, aura;
        TextMeshProUGUI kicker, title, sub, hint;
        RectTransform lineL, lineR;
        readonly List<Card> cards = new List<Card>();

        public bool IsOpen { get; private set; }
        /// <summary>The cards have landed: a good moment for background work (the game is frozen anyway).</summary>
        public bool Settled => IsOpen && !closing && openT > 1.1f;
        bool abilityMode, boss, closing;
        float openT, closeT, pickT;
        int chosen = -1;
        RunState run;
        List<UpgradeDef> offer;
        List<Ability> abilities;
        Action<UpgradeDef> onUpgrade;
        Action<Ability> onAbility;
        Action onShown;
        Color accent = Palette.ShotCyan;

        // ------------------------------------------------------------------ build

        public void Build(Transform parent, Camera cam, bool renderWithCamera)
        {
            var go = new GameObject("Reward Screen", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            canvas = go.AddComponent<Canvas>();
            if (renderWithCamera)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 0.95f;
                canvas.sortingOrder = 1050;
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 40;
            }
            UiKit.Scale(go);
            go.AddComponent<GraphicRaycaster>();
            rootGroup = go.AddComponent<CanvasGroup>();
            root = (RectTransform)go.transform;

            dim = UiKit.Img("Dim", root, null, new Color(0.01f, 0.02f, 0.05f, 0.78f), Vector2.zero, Vector2.zero);
            dim.rectTransform.anchorMin = Vector2.zero;
            dim.rectTransform.anchorMax = Vector2.one;
            dim.rectTransform.sizeDelta = Vector2.zero;
            dim.raycastTarget = true;
            aura = UiKit.Img("Aura", root, UiArt.Glow, Palette.ShotCyan.WithAlpha(0.06f), new Vector2(0f, -40f), new Vector2(1800f, 1100f));

            kicker = UiKit.Label("Kicker", root, "", 16f, Palette.ShotCyan, TextAlignmentOptions.Center, new Vector2(0f, 408f), new Vector2(1200f, 24f), true, 12f);
            title = UiKit.Label("Title", root, "", 56f, Color.white, TextAlignmentOptions.Center, new Vector2(0f, 356f), new Vector2(1400f, 70f), true, 16f);
            sub = UiKit.Label("Sub", root, "", 17f, Palette.UiMuted, TextAlignmentOptions.Center, new Vector2(0f, 310f), new Vector2(1400f, 26f), false, 3f);
            lineL = UiKit.Img("LineL", root, UiArt.LineFade, Color.white.WithAlpha(0.35f), new Vector2(-300f, 408f), new Vector2(200f, 2f)).rectTransform;
            lineR = UiKit.Img("LineR", root, UiArt.LineFade, Color.white.WithAlpha(0.35f), new Vector2(300f, 408f), new Vector2(200f, 2f)).rectTransform;
            cardRoot = UiKit.Node("Cards", root, new Vector2(0f, -10f), Vector2.zero);
            hint = UiKit.Label("Hint", root, "", 14f, Palette.UiMuted, TextAlignmentOptions.Center, new Vector2(0f, -372f), new Vector2(1400f, 24f), true, 5f);

            canvas.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ open

        /// <summary>shown: called once the screen has faded in and covers the arena.</summary>
        public void ShowUpgrades(List<UpgradeDef> cardsOffered, bool bossReward, RunState state, Action<UpgradeDef> pick, Action shown = null)
        {
            run = state;
            offer = cardsOffered;
            boss = bossReward;
            onUpgrade = pick;
            onAbility = null;
            abilityMode = false;
            var theme = run.Theme;
            accent = boss ? Palette.Gold : theme.Accent;
            kicker.text = boss ? "BOSS BESIEGT" : "WELLE " + run.Wave + " GESCHAFFT";
            title.text = boss ? "BOSS-BELOHNUNG" : "WÄHLE EIN UPGRADE";
            sub.text = boss ? "Nur seltene Karten oder besser – dein Build nimmt Form an."
                            : "STAGE " + run.Stage + "  ·  " + StageThemes.Title(run.Stage) + "  ·  als Nächstes: " + (run.Wave >= run.WavesInStage ? "BOSS" : "WELLE " + (run.Wave + 1));
            cardRoot.anchoredPosition = new Vector2(0f, -10f);
            BuildUpgradeCards();
            Open(shown);
        }

        public void ShowAbilities(List<Ability> choice, int stage, Action<Ability> pick, Action shown = null)
        {
            abilities = choice;
            onAbility = pick;
            onUpgrade = null;
            abilityMode = true;
            accent = Palette.Gold;
            kicker.text = "STAGE " + stage + " GESCHAFFT";
            title.text = "NEUE FÄHIGKEIT";
            sub.text = "Platz " + (Game.I.Run.SkillCount + 1) + " von " + RunState.MaxSkills + " ist frei: Nimm eine Fähigkeit für den Rest des Laufs mit.";
            cardRoot.anchoredPosition = new Vector2(0f, -28f);   // die Fähigkeitskarten sind höher
            BuildAbilityCards();
            Open(shown);
        }

        void Open(Action shown)
        {
            IsOpen = true;
            closing = false;
            chosen = -1;
            debugHover = -1;
            openT = 0f;
            closeT = 0f;
            onShown = shown;
            canvas.gameObject.SetActive(true);
            rootGroup.alpha = 0f;
            rootGroup.interactable = true;
            aura.color = accent.WithAlpha(0.07f);
            kicker.color = accent;
            int n = abilityMode ? abilities.Count : offer.Count;
            var keys = new System.Text.StringBuilder();
            for (int i = 1; i <= n; i++) keys.Append("[ ").Append(i).Append(" ]  ");
            hint.text = keys + "ODER KLICKEN";
        }

        // ------------------------------------------------------------------ cards

        void ClearCards()
        {
            foreach (var c in cards) UnityEngine.Object.Destroy(c.rt.gameObject);
            cards.Clear();
        }

        /// <summary>tier: Seltenheit 0–3. ability: Fähigkeitskarte – goldener Rahmen, goldener Schein und Lichtstrahlen.</summary>
        Card MakeCard(int index, int count, Vector2 size, Color color, int tier, bool ability, Vector2 emblemPos)
        {
            ButtonSkin.Build();
            float spacing = size.x + 44f;
            var c = new Card { color = color, glowColor = ability ? Palette.Gold : color, tier = tier, ability = ability, size = size, delay = 0.12f + index * 0.09f };
            c.rt = UiKit.Node("Card " + (index + 1), cardRoot, new Vector2((index - (count - 1) * 0.5f) * spacing, 0f), size);
            c.group = c.rt.gameObject.AddComponent<CanvasGroup>();
            c.shadow = UiKit.Img("Shadow", c.rt, UiArt.Glow, new Color(0f, 0f, 0.02f, 0.6f), new Vector2(0f, -24f), size * 1.3f);
            // Schein entlang der Kartenkante: seine glatte Mitte liegt 34 innerhalb der Karte, nach außen läuft er weich aus.
            c.halo = UiKit.Img("Halo", c.rt, CardEmblems.Halo, c.glowColor.WithAlpha(0f), Vector2.zero,
                size + Vector2.one * (2f * (CardEmblems.HaloReach - 34f)), Image.Type.Sliced);
            var frame = ExactButtonArt.Get("reward-frame");
            // Die Seltenheit färbt den gemalten Steinrahmen selbst ein – kein zweiter, anders geformter Rahmen darüber.
            Color frameColor = Color.Lerp(Color.white, c.glowColor, ability ? 0.36f : FrameTint[tier]);
            var glass = UiKit.Img("Glass", c.rt, frame, frameColor, Vector2.zero, size, Image.Type.Sliced, true);
            // der Inhalt bleibt in der Kartenform (Lasche, schräge Ecken): Licht und Glanz ragen nicht über den Rahmen hinaus.
            var clip = UiKit.Img("Content", c.rt, frame, Color.white, Vector2.zero, size, Image.Type.Sliced);
            clip.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            c.content = clip.rectTransform;
            // dunkelt die Karte ab, wenn eine andere unter dem Zeiger liegt (über allem, in der Kartenform)
            c.veil = UiKit.Img("Veil", c.rt, frame, new Color(0.01f, 0.015f, 0.04f, 0f), Vector2.zero, size, Image.Type.Sliced);
            // farbiges Licht in der oberen Kartenhälfte
            UiKit.Img("TopLight", c.content, UiArt.Glow, c.glowColor.WithAlpha(ability ? 0.06f : TopLight[tier]), new Vector2(0f, size.y * 0.42f), new Vector2(size.x * 1.7f, size.y * 0.9f));
            // legendäre Upgrades und Fähigkeiten: langsam kreisende Lichtstrahlen hinter dem Bild
            if (ability || tier == 3)
            {
                c.rays = new Image[3];
                for (int k = 0; k < c.rays.Length; k++)
                    c.rays[k] = UiKit.Img("Ray", c.content, UiArt.LineFade, c.glowColor.WithAlpha(0f), emblemPos, new Vector2(size.x * 1.15f, 5f));
            }
            // epische und legendäre Karten, Fähigkeiten: feine Lichtpunkte steigen auf
            int motes = ability ? 6 : MoteCount[tier];
            if (motes > 0)
            {
                c.motes = new Image[motes];
                c.moteSeed = new Vector3[motes];
                for (int k = 0; k < motes; k++)
                {
                    float r1 = Hash(index * 31 + k * 7), r2 = Hash(index * 17 + k * 13 + 5), r3 = Hash(k * 29 + index * 3 + 11);
                    c.moteSeed[k] = new Vector3((r1 - 0.5f) * size.x * 0.8f, 16f + r2 * 22f, r3);
                    c.motes[k] = UiKit.Img("Mote", c.content, UiArt.Glow, c.glowColor.WithAlpha(0f), Vector2.zero, Vector2.one * (9f + r2 * 8f));
                }
            }
            if (ability || tier >= 2)
            {
                c.shine = UiKit.Img("Shine", c.content, UiArt.LineFade, Color.white.WithAlpha(0f), Vector2.zero, new Vector2(size.y * 1.8f, tier == 3 ? 90f : 60f));
                c.shine.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 62f);
            }

            var button = glass.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            int idx = index;
            button.onClick.AddListener(() => Pick(idx));
            c.anim = glass.gameObject.AddComponent<UiAnim>();
            var card = c;
            c.anim.Apply = a => card.lift = a.Hover;
            return c;
        }

        static TextMeshProUGUI Wrap(TextMeshProUGUI t)
        {
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            t.lineSpacing = 4f;
            return t;
        }

        static float Hash(int n)
        {
            uint h = (uint)n * 2654435761u;
            h ^= h >> 13; h *= 1274126177u; h ^= h >> 16;
            return (h & 0xFFFF) / 65535f;
        }

        /// <summary>Das Kartenbild (Relief aus CardEmblems) mit einem Schein in Kartenfarbe dahinter.</summary>
        static void Emblem(Card c, Vector2 pos, float size, Sprite art)
        {
            c.medGlow = UiKit.Img("EmblemGlow", c.content, UiArt.Glow, c.color.WithAlpha(0f), pos, Vector2.one * size * 1.45f);
            var img = UiKit.Img("Emblem", c.content, art, Color.white, pos, Vector2.one * size);
            img.preserveAspect = true;
            c.emblem = img.rectTransform;
        }

        /// <summary>Innenkante des gemalten Kartenrahmens unten und Mitte der Namenslasche oben (vom Kartenrand gemessen).</summary>
        const float FrameBottom = 21f, TabCenter = 25f;
        /// <summary>Mitte des dunklen Ziffernfelds im WÄHLEN-Knopf, als Anteil der Knopfbreite.</summary>
        const float KeyCellX = 0.82f;

        /// <summary>
        /// Der WÄHLEN-Knopf steht auf der Innenkante des Rahmens, nie darüber. Alle Karten tragen denselben blauen
        /// Knopf (die Ziffer kommt als Text ins leere Feld), keine ist vorab gelb hervorgehoben.
        /// </summary>
        static void ChooseFooter(Transform parent, Vector2 cardSize, float width, int index)
        {
            float h = width * 82f / 295f;
            var plate = UiKit.Img("Wählen", parent, ExactButtonArt.Get("choose-blank"), Color.white,
                new Vector2(0f, -cardSize.y * 0.5f + FrameBottom + 3f + h * 0.5f), new Vector2(width, h));
            var key = UiKit.Label("Key", plate.transform, (index + 1).ToString(), h * 0.42f, new Color(0.9f, 0.95f, 1f), TextAlignmentOptions.Center,
                new Vector2(width * (KeyCellX - 0.5f), 1f), new Vector2(h * 0.7f, h * 0.7f), true, 0f);
            key.fontStyle = FontStyles.Bold;
        }

        void BuildUpgradeCards()
        {
            ClearCards();
            var size = new Vector2(340f, 470f);
            for (int i = 0; i < offer.Count; i++)
            {
                var u = offer[i];
                Color rc = Rarities.Of(u.Rarity);
                var emblemPos = new Vector2(0f, 110f);
                var c = MakeCard(i, offer.Count, size, rc, (int)u.Rarity, false, emblemPos);
                var ct = c.content;
                UiKit.Label("Rarity", ct, Rarities.Name(u.Rarity), 13f, rc, TextAlignmentOptions.Center, new Vector2(0f, size.y * 0.5f - TabCenter), new Vector2(200f, 20f), true, 9f);
                Emblem(c, emblemPos, 178f, CardEmblems.ForUpgrade(u.Icon));
                var name = UiKit.Label("Name", ct, u.Name, 27f, Color.white, TextAlignmentOptions.Center, new Vector2(0f, 20f), new Vector2(310f, 36f), true, 3f);
                name.enableAutoSizing = true; name.fontSizeMin = 18f; name.fontSizeMax = 27f;
                string tag = u.IsSynergy ? "SYNERGIE" : u.NeedsAbility != Ability.None ? Abilities.Name(u.NeedsAbility) : u.Rarity == Rarity.Legendary ? "SPIELVERÄNDERND" : u.Rarity == Rarity.Epic ? "BUILD-KERN" : "";
                if (tag.Length > 0) UiKit.Label("Tag", ct, tag, 11f, u.IsSynergy ? Palette.Gold : Palette.UiMuted, TextAlignmentOptions.Center, new Vector2(0f, -10f), new Vector2(300f, 18f), true, 5f);
                UiKit.Img("Divider", ct, UiArt.LineFade, Color.Lerp(Color.white, rc, u.Rarity == Rarity.Common ? 0f : 0.6f).WithAlpha(u.Rarity == Rarity.Common ? 0.14f : 0.24f), new Vector2(0f, -30f), new Vector2(240f, 2f));
                int level = run.Stacks(u.Id) + 1;
                var desc = Wrap(UiKit.Label("Desc", ct, u.Describe(level), 18f, Palette.UiText, TextAlignmentOptions.Top, new Vector2(0f, -74f), new Vector2(290f, 76f), false, 0f));
                desc.richText = true;
                // lange Texte werden kleiner, statt über Stufe und Knopf zu laufen
                desc.enableAutoSizing = true;
                desc.fontSizeMin = 14f;
                desc.fontSizeMax = 18f;

                // stack level: pips for stackable cards, "EINMALIG" for uniques
                if (u.Max > 1)
                {
                    int n = Mathf.Min(u.Max, 10);
                    for (int k = 0; k < n; k++)
                    {
                        bool owned = k < level - 1, next = k == level - 1;
                        var pip = UiKit.Img("Pip", ct, UiArt.Diamond, owned ? rc : next ? Color.white : Color.white.WithAlpha(0.18f),
                            new Vector2((k - (n - 1) * 0.5f) * 18f, -114f), Vector2.one * (next ? 13f : 10f));
                        if (next) UiKit.Img("PipGlow", ct, UiArt.Glow, rc.WithAlpha(0.5f), pip.rectTransform.anchoredPosition, Vector2.one * 34f);
                    }
                    UiKit.Label("Level", ct, level == 1 ? "NEU" : "STUFE " + level + " / " + u.Max, 11f, level == 1 ? Palette.Heal : Palette.UiMuted,
                        TextAlignmentOptions.Center, new Vector2(0f, -130f), new Vector2(300f, 16f), true, 4f);
                }
                else UiKit.Label("Level", ct, "EINMALIG", 11f, Palette.UiMuted, TextAlignmentOptions.Center, new Vector2(0f, -124f), new Vector2(300f, 16f), true, 5f);

                // Punkte und Stufe über dem Knopf, der Knopf auf der Innenkante des Rahmens
                ChooseFooter(ct, size, 250f, i);
                cards.Add(c);
            }
        }

        void BuildAbilityCards()
        {
            ClearCards();
            var size = new Vector2(440f, 580f);
            for (int i = 0; i < abilities.Count; i++)
            {
                var a = abilities[i];
                Color ac = Abilities.Accent(a);
                if (a == Ability.Flick) ac = new Color(1f, 0.85f, 0.55f);
                var emblemPos = new Vector2(0f, 130f);
                var c = MakeCard(i, abilities.Count, size, ac, 2, true, emblemPos);
                var ct = c.content;
                // the category, and whether the class trait makes this one stronger
                var cat = SkillCatalog.CategoryOf(a);
                var cls = Characters.Current.ClassDef;
                bool classBonus = cat != null && cat.Value == cls.Specialty;
                string kind = cat != null ? SkillCatalog.CategoryName(cat.Value) : "FÄHIGKEIT";
                if (classBonus) kind += "  ·  " + cls.Name + "-BONUS";
                // die Art sitzt mitten in der Namenslasche (die Lasche ist in der breiten Karte etwa 290 breit)
                var kindLabel = UiKit.Label("Kind", ct, kind, 13f, classBonus ? cls.Accent : ac, TextAlignmentOptions.Center, new Vector2(0f, size.y * 0.5f - TabCenter), new Vector2(240f, 20f), true, classBonus ? 6f : 10f);
                kindLabel.enableAutoSizing = true;
                kindLabel.fontSizeMin = 9f;
                kindLabel.fontSizeMax = 13f;
                Emblem(c, emblemPos, 204f, CardEmblems.ForAbility(a, ac));
                UiKit.Label("Name", ct, Abilities.Name(a), 34f, Color.white, TextAlignmentOptions.Center, new Vector2(0f, 16f), new Vector2(420f, 44f), true, 5f);
                UiKit.Img("Divider", ct, UiArt.LineFade, Palette.Gold.WithAlpha(0.3f), new Vector2(0f, -14f), new Vector2(300f, 2f));
                // lange Beschreibungen werden kleiner statt über den Knopf zu laufen
                var desc = Wrap(UiKit.Label("Desc", ct, Abilities.Description(a), 18f, Palette.UiText, TextAlignmentOptions.Top, new Vector2(0f, -78f), new Vector2(370f, 104f), false, 0f));
                desc.enableAutoSizing = true;
                desc.fontSizeMin = 14f;
                desc.fontSizeMax = 18f;

                // it lands in the next free slot, so that is the key it will answer to
                string keyName = a == Ability.AirKick ? "IN DER LUFT: " + KeyBindings.DisplayName(GameAction.Shoot)
                    : "PLATZ " + (Game.I.Run.SkillCount + 1) + ":  " + KeyBindings.DisplayName((GameAction)((int)GameAction.Skill1 + Mathf.Min(RunState.MaxSkills - 1, Game.I.Run.SkillCount)));
                var kb = UiKit.Img("KeyBack", ct, UiArt.Pill, Color.white.WithAlpha(0.05f), new Vector2(0f, -168f), new Vector2(260f, 28f), Image.Type.Sliced);
                UiKit.Label("KeyName", kb.transform, keyName, 13f, Palette.UiText, TextAlignmentOptions.Center, Vector2.zero, new Vector2(260f, 28f), true, 3f);
                int unlocks = 0;
                foreach (var u in UpgradeDb.All) if (u.NeedsAbility == a) unlocks++;
                if (unlocks > 0)
                    UiKit.Label("Unlocks", ct, "SCHALTET " + unlocks + " NEUE UPGRADES FREI", 12f, Palette.Gold, TextAlignmentOptions.Center, new Vector2(0f, -141f), new Vector2(380f, 18f), true, 4f);
                ChooseFooter(ct, size, 300f, i);
                cards.Add(c);
            }
        }

        // ------------------------------------------------------------------ choose

        void Pick(int index)
        {
            if (closing || index < 0 || index >= cards.Count) return;
            if (cards[index].revealT < 0.4f) return;   // no picking before the card has even turned
            chosen = index;
            closing = true;
            pickT = 0f;
            rootGroup.interactable = false;
        }

        /// <summary>Capture scripts have no keyboard: pick a card by index.</summary>
        public void DebugPick(int index) => Pick(index);

        /// <summary>Captures haben keine Maus: diese Karte so zeigen, als läge der Zeiger darauf (-1 = keine).</summary>
        public void DebugHover(int index) => debugHover = index;
        int debugHover = -1;
        float HoverOf(Card c, int index) => Mathf.Clamp01(Mathf.Max(c.lift, index == debugHover ? 1f : 0f));

        /// <summary>Restart from the pause menu: drop the screen without picking.</summary>
        public void Cancel()
        {
            if (!IsOpen) return;
            IsOpen = false;
            closing = false;
            onUpgrade = null;
            onAbility = null;
            onShown = null;
            canvas.gameObject.SetActive(false);
            ClearCards();
        }

        void Finish()
        {
            IsOpen = false;
            canvas.gameObject.SetActive(false);
            ClearCards();
            if (onShown != null) { var shown = onShown; onShown = null; shown(); }
            int idx = chosen;
            if (abilityMode) { var cb = onAbility; onAbility = null; cb?.Invoke(abilities[idx]); }
            else { var cb = onUpgrade; onUpgrade = null; cb?.Invoke(offer[idx]); }
        }

        // ------------------------------------------------------------------ update

        public void Update(float udt, bool inputAllowed)
        {
            if (!IsOpen) return;
            openT += udt;

            if (inputAllowed && !closing)
            {
                var kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) Pick(0);
                    else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) Pick(1);
                    else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) Pick(2);
                }
            }

            // fully faded in: whatever should happen behind the screen (a new arena) happens now
            if (onShown != null && openT > 0.4f)
            {
                var cb = onShown;
                onShown = null;
                cb();
            }

            float fadeIn = MathUtil.EaseOutCubic(openT / 0.35f);
            float fadeOut = 0f;
            if (closing)
            {
                pickT += udt;
                fadeOut = MathUtil.Smooth01((pickT - 0.45f) / 0.3f);
                if (pickT >= 0.75f) { Finish(); return; }
            }
            rootGroup.alpha = fadeIn * (1f - fadeOut);

            float th = MathUtil.EaseOutCubic((openT - 0.05f) / 0.5f);
            title.rectTransform.anchoredPosition = new Vector2(0f, 356f + 20f * (1f - th));
            title.characterSpacing = Mathf.Lerp(34f, 16f, MathUtil.EaseOutCubic(openT / 0.9f));
            title.alpha = Mathf.Clamp01(th);
            sub.alpha = MathUtil.Smooth01((openT - 0.25f) / 0.3f);
            float lw = 220f * MathUtil.EaseOutCubic((openT - 0.1f) / 0.6f);
            float half = Mathf.Max(120f, kicker.preferredWidth * 0.5f + 24f);
            lineL.sizeDelta = new Vector2(lw, 2f);
            lineR.sizeDelta = new Vector2(lw, 2f);
            lineL.anchoredPosition = new Vector2(-half - lw * 0.5f, 408f);
            lineR.anchoredPosition = new Vector2(half + lw * 0.5f, 408f);
            lineL.GetComponent<Image>().color = accent.WithAlpha(0.45f);
            lineR.GetComponent<Image>().color = accent.WithAlpha(0.45f);
            aura.color = accent.WithAlpha(0.06f + 0.015f * Mathf.Sin(openT * 2f));

            float anyHover = 0f;
            for (int i = 0; i < cards.Count; i++) anyHover = Mathf.Max(anyHover, HoverOf(cards[i], i));
            for (int i = 0; i < cards.Count; i++) AnimateCard(cards[i], i, anyHover);
        }

        void AnimateCard(Card c, int index, float anyHover)
        {
            // flip in: rises, turns from edge-on, then settles with a little overshoot
            float t = openT - c.delay;
            float k = Mathf.Clamp01(t / 0.55f);
            c.revealT = k;
            float flip = MathUtil.EaseOutBack(Mathf.Clamp01(t / 0.45f), 1.4f);
            float rise = MathUtil.EaseOutCubic(k);
            bool isChosen = closing && index == chosen;
            bool other = closing && index != chosen;
            float pick = closing ? MathUtil.EaseOutCubic(pickT / 0.3f) : 0f;

            // Hover: die Karte hebt sich ruhig heraus, die anderen treten ein wenig zurück
            float hover = closing ? 0f : HoverOf(c, index);
            float recede = closing ? 0f : Mathf.Clamp01(anyHover - hover);
            float grow = 1f + 0.045f * hover - 0.02f * recede + (isChosen ? 0.08f * pick : 0f);
            c.rt.localScale = new Vector3(Mathf.Max(0.02f, flip) * grow, grow, 1f);
            float y = -90f * (1f - rise) + 18f * hover + (isChosen ? 24f * pick : 0f) - (other ? 60f * pick : 0f);
            var p = c.rt.anchoredPosition;
            c.rt.anchoredPosition = new Vector2(p.x, y);
            float appear = Mathf.Clamp01(t / 0.6f);
            c.group.alpha = Mathf.Clamp01(t / 0.1f) * (other ? 1f - MathUtil.Smooth01(pickT / 0.18f) : 1f);
            c.veil.color = c.veil.color.WithAlpha(0.3f * recede);

            // Seltenheit: der Schein um die Karte atmet bei epischen und legendären Karten langsam
            float breathe = 0.5f + 0.5f * Mathf.Sin(openT * 2.2f + index * 1.3f);
            int tier = c.ability ? 2 : c.tier;
            float pulse = tier == 3 ? 0.08f * breathe : tier == 2 ? 0.05f * breathe : 0f;
            float haloBase = c.ability ? 0.2f : HaloBase[c.tier];
            c.halo.color = Color.Lerp(c.glowColor, Color.white, 0.2f * hover)
                .WithAlpha(appear * (haloBase + pulse + 0.2f * hover) + (isChosen ? 0.35f * pick : 0f));
            c.shadow.color = new Color(0f, 0f, 0.02f, 0.6f + 0.15f * hover);
            c.shadow.rectTransform.anchoredPosition = new Vector2(0f, -24f - 12f * hover);
            float eg = c.ability ? 0.1f : EmblemGlow[c.tier];
            c.medGlow.color = c.color.WithAlpha(appear * (eg + pulse * 0.4f + 0.05f * hover));
            float es = 1f + 0.035f * hover;
            c.emblem.localScale = new Vector3(es, es, 1f);

            // langsam kreisende Lichtstrahlen hinter dem Bild (legendär, Fähigkeiten)
            if (c.rays != null)
                for (int r = 0; r < c.rays.Length; r++)
                {
                    float ang = r * 60f + openT * (r % 2 == 0 ? 7f : -5f);
                    c.rays[r].rectTransform.localRotation = Quaternion.Euler(0f, 0f, ang);
                    c.rays[r].color = c.glowColor.WithAlpha(appear * (0.06f + 0.03f * breathe + 0.04f * hover));
                }

            // feine Lichtpunkte steigen mit leichtem Seitendrift auf und verglimmen oben
            if (c.motes != null)
                for (int m = 0; m < c.motes.Length; m++)
                {
                    var seed = c.moteSeed[m];
                    float span = c.size.y * 0.78f;
                    float life = Mathf.Repeat(openT * seed.y / span + seed.z, 1f);
                    float mx = seed.x + Mathf.Sin(openT * 1.3f + seed.z * 6.283f) * 6f;
                    c.motes[m].rectTransform.anchoredPosition = new Vector2(mx, -c.size.y * 0.36f + life * span);
                    c.motes[m].color = c.glowColor.WithAlpha(appear * 0.4f * MathUtil.Bump(life));
                }

            // ein Glanz zieht alle paar Sekunden über epische und legendäre Karten und Fähigkeiten
            if (c.shine != null)
            {
                float period = tier == 3 ? 2.4f : 3.6f;
                float s = Mathf.Repeat(openT - c.delay - 0.3f, period) / 0.9f;
                float x = Mathf.Lerp(-420f, 420f, s);
                c.shine.rectTransform.anchoredPosition = new Vector2(x, 0f);
                c.shine.color = Color.Lerp(Color.white, c.glowColor, 0.4f).WithAlpha(s < 1f ? (tier == 3 ? 0.07f : 0.045f) * MathUtil.Bump(s) : 0f);
            }
        }
    }
}
