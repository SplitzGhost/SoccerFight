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
            public RectTransform rt, content;
            public CanvasGroup group;
            public Image glow, border, shine, medGlow;
            public Color color;
            public float delay, revealT, lift;
            public bool epic, legendary;
            public UiAnim anim;
        }

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
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
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
            BuildAbilityCards();
            Open(shown);
        }

        void Open(Action shown)
        {
            IsOpen = true;
            closing = false;
            chosen = -1;
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

        Card MakeCard(int index, int count, Vector2 size, Color color, bool epic, bool legendary)
        {
            float spacing = size.x + 44f;
            var c = new Card { color = color, epic = epic, legendary = legendary, delay = 0.12f + index * 0.09f };
            c.rt = UiKit.Node("Card " + (index + 1), cardRoot, new Vector2((index - (count - 1) * 0.5f) * spacing, 0f), size);
            c.group = c.rt.gameObject.AddComponent<CanvasGroup>();
            c.glow = UiKit.Img("Glow", c.rt, UiArt.Glow, color.WithAlpha(0.1f), new Vector2(0f, 10f), size * 1.5f);
            UiKit.Img("Shadow", c.rt, UiArt.Glow, new Color(0f, 0f, 0.02f, 0.6f), new Vector2(0f, -24f), size * 1.3f);
            var glass = UiKit.Img("Glass", c.rt, UiArt.Panel, new Color(0.04f, 0.066f, 0.108f, 0.995f), Vector2.zero, size, Image.Type.Sliced, true);
            c.content = UiKit.Node("Content", c.rt, Vector2.zero, size);
            c.border = UiKit.Img("Border", c.rt, UiArt.PanelRing, color.WithAlpha(0.45f), Vector2.zero, size + new Vector2(2f, 2f), Image.Type.Sliced);
            c.content.gameObject.AddComponent<RectMask2D>();
            // coloured light pooling in the top half of the card
            UiKit.Img("TopLight", c.content, UiArt.Glow, color.WithAlpha(legendary ? 0.26f : epic ? 0.2f : 0.14f), new Vector2(0f, size.y * 0.42f), new Vector2(size.x * 1.7f, size.y * 0.9f));
            UiKit.Img("Band", c.content, UiArt.LineFade, color.WithAlpha(0.9f), new Vector2(0f, size.y * 0.5f - 2f), new Vector2(size.x * 0.9f, 3f));
            if (epic || legendary)
            {
                c.shine = UiKit.Img("Shine", c.content, UiArt.LineFade, Color.white.WithAlpha(0f), Vector2.zero, new Vector2(size.y * 1.8f, legendary ? 90f : 60f));
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

        void Medallion(Card c, Transform parent, Vector2 pos, float size, Sprite icon, float iconScale)
        {
            c.medGlow = UiKit.Img("MedGlow", parent, UiArt.Glow, c.color.WithAlpha(0.35f), pos, Vector2.one * size * 2.2f);
            UiKit.Img("MedFill", parent, UiArt.Circle, Color.Lerp(new Color(0.05f, 0.08f, 0.13f), c.color, 0.3f), pos, Vector2.one * size);
            UiKit.Img("MedInner", parent, UiArt.Glow, c.color.WithAlpha(0.35f), pos + new Vector2(0f, size * 0.12f), Vector2.one * size * 0.9f);
            UiKit.Img("MedRing", parent, UiArt.RingThick, c.color, pos, Vector2.one * size);
            UiKit.Img("MedRim", parent, UiArt.RingThin, Color.white.WithAlpha(0.25f), pos, Vector2.one * (size + 14f));
            UiKit.Img("Icon", parent, icon, Color.white, pos, Vector2.one * size * iconScale);
        }

        void BuildUpgradeCards()
        {
            ClearCards();
            var size = new Vector2(340f, 470f);
            for (int i = 0; i < offer.Count; i++)
            {
                var u = offer[i];
                Color rc = Rarities.Of(u.Rarity);
                var c = MakeCard(i, offer.Count, size, rc, u.Rarity >= Rarity.Epic, u.Rarity == Rarity.Legendary);
                var ct = c.content;
                UiKit.Label("Rarity", ct, Rarities.Name(u.Rarity), 13f, rc, TextAlignmentOptions.Center, new Vector2(0f, 205f), new Vector2(300f, 20f), true, 9f);
                Medallion(c, ct, new Vector2(0f, 110f), 118f, UpgradeIcons.Get(u.Icon), 0.66f);
                var name = UiKit.Label("Name", ct, u.Name, 27f, Color.white, TextAlignmentOptions.Center, new Vector2(0f, 20f), new Vector2(310f, 36f), true, 3f);
                name.enableAutoSizing = true; name.fontSizeMin = 18f; name.fontSizeMax = 27f;
                string tag = u.IsSynergy ? "SYNERGIE" : u.NeedsAbility != Ability.None ? Abilities.Name(u.NeedsAbility) : u.Rarity == Rarity.Legendary ? "SPIELVERÄNDERND" : u.Rarity == Rarity.Epic ? "BUILD-KERN" : "";
                if (tag.Length > 0) UiKit.Label("Tag", ct, tag, 11f, u.IsSynergy ? Palette.Gold : Palette.UiMuted, TextAlignmentOptions.Center, new Vector2(0f, -10f), new Vector2(300f, 18f), true, 5f);
                UiKit.Img("Divider", ct, UiArt.LineFade, Color.white.WithAlpha(0.14f), new Vector2(0f, -30f), new Vector2(240f, 2f));
                int level = run.Stacks(u.Id) + 1;
                var desc = Wrap(UiKit.Label("Desc", ct, u.Describe(level), 18f, Palette.UiText, TextAlignmentOptions.Top, new Vector2(0f, -82f), new Vector2(290f, 92f), false, 0f));
                desc.richText = true;

                // stack level: pips for stackable cards, "EINMALIG" for uniques
                if (u.Max > 1)
                {
                    int n = Mathf.Min(u.Max, 10);
                    for (int k = 0; k < n; k++)
                    {
                        bool owned = k < level - 1, next = k == level - 1;
                        var pip = UiKit.Img("Pip", ct, UiArt.Diamond, owned ? rc : next ? Color.white : Color.white.WithAlpha(0.18f),
                            new Vector2((k - (n - 1) * 0.5f) * 18f, -150f), Vector2.one * (next ? 13f : 10f));
                        if (next) UiKit.Img("PipGlow", ct, UiArt.Glow, rc.WithAlpha(0.5f), pip.rectTransform.anchoredPosition, Vector2.one * 34f);
                    }
                    UiKit.Label("Level", ct, level == 1 ? "NEU" : "STUFE " + level + " / " + u.Max, 11f, level == 1 ? Palette.Heal : Palette.UiMuted,
                        TextAlignmentOptions.Center, new Vector2(0f, -170f), new Vector2(300f, 16f), true, 4f);
                }
                else UiKit.Label("Level", ct, "EINMALIG", 11f, Palette.UiMuted, TextAlignmentOptions.Center, new Vector2(0f, -162f), new Vector2(300f, 16f), true, 5f);

                var key = UiKit.Img("KeyBack", ct, UiArt.Pill, Color.white.WithAlpha(0.08f), new Vector2(0f, -204f), new Vector2(40f, 26f), Image.Type.Sliced);
                UiKit.Label("Key", key.transform, (i + 1).ToString(), 14f, Palette.UiText, TextAlignmentOptions.Center, Vector2.zero, new Vector2(40f, 26f), true, 0f);
                cards.Add(c);
            }
        }

        void BuildAbilityCards()
        {
            ClearCards();
            var size = new Vector2(440f, 540f);
            for (int i = 0; i < abilities.Count; i++)
            {
                var a = abilities[i];
                Color ac = Abilities.Accent(a);
                if (a == Ability.Flick) ac = new Color(1f, 0.85f, 0.55f);
                var c = MakeCard(i, abilities.Count, size, ac, true, false);
                var ct = c.content;
                // the category, and whether the class trait makes this one stronger
                var cat = SkillCatalog.CategoryOf(a);
                var cls = Characters.Current.ClassDef;
                bool classBonus = cat != null && cat.Value == cls.Specialty;
                string kind = cat != null ? SkillCatalog.CategoryName(cat.Value) : "FÄHIGKEIT";
                if (classBonus) kind += "  ·  " + cls.Name + "-BONUS";
                UiKit.Label("Kind", ct, kind, 13f, classBonus ? cls.Accent : ac, TextAlignmentOptions.Center, new Vector2(0f, 232f), new Vector2(400f, 20f), true, classBonus ? 6f : 10f);
                Medallion(c, ct, new Vector2(0f, 110f), 170f, Abilities.Icon(a), 0.74f);
                UiKit.Label("Name", ct, Abilities.Name(a), 34f, Color.white, TextAlignmentOptions.Center, new Vector2(0f, -8f), new Vector2(420f, 44f), true, 5f);
                UiKit.Img("Divider", ct, UiArt.LineFade, Color.white.WithAlpha(0.14f), new Vector2(0f, -42f), new Vector2(300f, 2f));
                Wrap(UiKit.Label("Desc", ct, Abilities.Description(a), 19f, Palette.UiText, TextAlignmentOptions.Top, new Vector2(0f, -100f), new Vector2(370f, 100f), false, 0f));

                // it lands in the next free slot, so that is the key it will answer to
                string keyName = a == Ability.AirKick ? "IN DER LUFT: " + KeyBindings.DisplayName(GameAction.Shoot)
                    : "PLATZ " + (Game.I.Run.SkillCount + 1) + ":  " + KeyBindings.DisplayName((GameAction)((int)GameAction.Skill1 + Mathf.Min(RunState.MaxSkills - 1, Game.I.Run.SkillCount)));
                var kb = UiKit.Img("KeyBack", ct, UiArt.Pill, Color.white.WithAlpha(0.08f), new Vector2(0f, -174f), new Vector2(260f, 30f), Image.Type.Sliced);
                UiKit.Label("KeyName", kb.transform, keyName, 13f, Palette.UiText, TextAlignmentOptions.Center, Vector2.zero, new Vector2(260f, 30f), true, 3f);
                int unlocks = 0;
                foreach (var u in UpgradeDb.All) if (u.NeedsAbility == a) unlocks++;
                if (unlocks > 0)
                    UiKit.Label("Unlocks", ct, "SCHALTET " + unlocks + " NEUE UPGRADES FREI", 12f, Palette.Gold, TextAlignmentOptions.Center, new Vector2(0f, -212f), new Vector2(380f, 18f), true, 4f);
                var key = UiKit.Img("Key", ct, UiArt.Pill, Color.white.WithAlpha(0.08f), new Vector2(0f, -246f), new Vector2(40f, 26f), Image.Type.Sliced);
                UiKit.Label("KeyNum", key.transform, (i + 1).ToString(), 14f, Palette.UiText, TextAlignmentOptions.Center, Vector2.zero, new Vector2(40f, 26f), true, 0f);
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

            for (int i = 0; i < cards.Count; i++) AnimateCard(cards[i], i, udt);
        }

        void AnimateCard(Card c, int index, float udt)
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

            float hover = closing ? 0f : Mathf.Clamp01(c.lift);
            float sx = Mathf.Max(0.02f, flip) * (1f + 0.035f * hover + (isChosen ? 0.08f * pick : 0f));
            float sy = (1f + 0.035f * hover + (isChosen ? 0.08f * pick : 0f));
            c.rt.localScale = new Vector3(sx, sy, 1f);
            float y = -90f * (1f - rise) + 16f * hover + (isChosen ? 24f * pick : 0f) - (other ? 60f * pick : 0f);
            var p = c.rt.anchoredPosition;
            c.rt.anchoredPosition = new Vector2(p.x, y);
            c.group.alpha = Mathf.Clamp01(t / 0.1f) * (other ? 1f - MathUtil.Smooth01(pickT / 0.18f) : 1f);

            float breathe = 0.5f + 0.5f * Mathf.Sin(openT * 2.6f + index);
            float rarityPulse = c.legendary ? 0.1f * breathe : c.epic ? 0.05f * breathe : 0f;
            c.border.color = c.color.WithAlpha(0.4f + 0.5f * hover + rarityPulse * 2f + (isChosen ? 0.6f * pick : 0f));
            c.glow.color = c.color.WithAlpha(0.08f + 0.14f * hover + rarityPulse + (isChosen ? 0.3f * pick : 0f));
            if (c.medGlow != null) c.medGlow.color = c.color.WithAlpha(0.3f + 0.2f * hover + rarityPulse);

            // a sheen sweeps across epic and legendary cards every few seconds
            if (c.shine != null)
            {
                float period = c.legendary ? 2.2f : 3.4f;
                float s = Mathf.Repeat(openT - c.delay - 0.3f, period) / 0.9f;
                float x = Mathf.Lerp(-420f, 420f, s);
                c.shine.rectTransform.anchoredPosition = new Vector2(x, 0f);
                c.shine.color = Color.Lerp(Color.white, c.color, 0.4f).WithAlpha(s < 1f ? (c.legendary ? 0.22f : 0.14f) * MathUtil.Bump(s) : 0f);
            }
        }
    }
}
