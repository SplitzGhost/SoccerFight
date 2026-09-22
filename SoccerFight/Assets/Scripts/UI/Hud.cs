using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoccerFight
{
    /// <summary>
    /// Clean HUD: health bar with delayed damage ghost and shield pips (top-left) and the build strip
    /// under it, stage/wave tracker with pips (top centre), boss bar, name tags over elites, the skill
    /// bar with radial cooldown sweeps and locks (bottom-right), crosshair, banners, stage and boss
    /// intro cards, damage numbers and the run summary. Animates on unscaled time.
    /// </summary>
    public sealed partial class Hud
    {
        sealed class Slot
        {
            public RectTransform root, iconRt;
            public Image icon, overlay, ring, dot, flashRing, glow, lockImg, baseImg;
            public TextMeshProUGUI timer;
            public int shownTenths = -1;
            public float size;
            public float prevRemaining;
            public float pop, popVel;
            public float flashT = 99f, shakeT = 99f, unlockT = 99f;
            public Color accent;
            public GameAction action;
            public Ability ability;
            public string fixedLabel;
            public Image badge, badgeRim, mouseIcon;
            public TextMeshProUGUI keyText;
            public Vector2 home;
            public bool placed;
        }

        sealed class Number
        {
            public RectTransform rt;
            public TextMeshProUGUI text;
            public Vector2 world;
            public float age = 99f;
            public bool big;
            public float drift;
        }

        sealed class Tag
        {
            public RectTransform rt;
            public TextMeshProUGUI name, affix;
            public Image back, line;
            public Monster target;
            public float alpha;
        }

        sealed class BuildIcon
        {
            public RectTransform rt;
            public Image ring, fill, icon;
            public TextMeshProUGUI count;
            public string id;
            public float pop, popVel;
        }

        Canvas canvas;
        RectTransform canvasRect;
        Camera cam;
        Player player;
        WaveDirector waves;

        // health
        RectTransform hpGroup;
        Image hpFill, hpGhost, hpGlow, emblemRing, emblemGlow, shieldRing, shieldGlow;
        RectTransform hpFillRt, hpGhostRt;
        TextMeshProUGUI hpText, shieldText;
        float hpDisplay = 1f, hpGhostV = 1f, ghostDelay, hpShake;
        int shownHp = -1, shownMaxHp = -1, shownShield = -1;
        const float BarW = 300f, BarH = 26f, BarInset = 5f;

        // build strip
        RectTransform buildRoot;
        readonly List<BuildIcon> buildIcons = new List<BuildIcon>();

        Slot shotSlot, flickSlot, powerSlot, stepSlot, bikeSlot, jugSlot;
        Slot tackleSlot, puntSlot, wallSlot, nutmegSlot, decoySlot, whistleSlot, headerSlot, dashSlot;
        const float ShotSlotSize = 94f, SkillSlotSize = 76f, SlotGap = 18f, SlotRight = 44f, SlotBottom = 46f;
        Slot[] slots;

        RectTransform cross;
        Image crossArc;
        float crossPunch, crossPunchVel;

        // stage / wave tracker
        TextMeshProUGUI stageText, waveText, enemiesText;
        RectTransform pipRoot;
        readonly List<Image> pips = new List<Image>();
        readonly List<Image> pipGlows = new List<Image>();
        int shownStage = -1, shownWave = -1, shownEnemies = -1, shownWaves = -1;

        // banner
        RectTransform banner;
        CanvasGroup bannerGroup;
        TextMeshProUGUI bannerText, bannerSub;
        RectTransform bannerLineL, bannerLineR;
        float bannerT = 99f;

        // stage card
        CanvasGroup stageGroup;
        RectTransform stageCard;
        TextMeshProUGUI stageNum, stageName, stageTag, stageMechName, stageMechText;
        RectTransform stageLineL, stageLineR;
        Image stageMechBack, stageMechDot, stageAura;
        float stageT = 99f;

        // boss intro + bar
        CanvasGroup bossIntroGroup;
        RectTransform barTop, barBottom;
        TextMeshProUGUI bossIntroName, bossIntroTitle, bossIntroLabel;
        float bossIntroT = 99f;
        CanvasGroup bossBarGroup;
        TextMeshProUGUI bossName, bossPhase;
        Image bossFill, bossGhost, bossGlow;
        RectTransform bossFillRt, bossGhostRt;
        float bossDisplay = 1f, bossGhostV = 1f, bossGhostDelay, bossBarA;
        Monster shownBoss;
        const float BossW = 720f, BossH = 16f;

        // name tags
        readonly List<Tag> tags = new List<Tag>();

        bool deathTextSet;
        Image fade;
        float fadeT;

        TextMeshProUGUI fpsText, hintText;
        bool paused;
        float fpsTimer;
        int fpsFrames;

        TextMeshProUGUI toast;
        float toastT = 99f;

        // developer mode: badge with the active cheats, optional live numbers
        TextMeshProUGUI devBadge, devInfo;
        Image devInfoBack;
        float devTimer;

        Number[] numbers;

        /// <summary>The coin counter (top right) and the coins flying into it.</summary>
        public CoinCounter Coins { get; private set; }
        int numberCursor;

        CanvasGroup deathGroup;
        TextMeshProUGUI deathSub, deathStats, deathBest;
        RectTransform deathBuild;

        CanvasGroup hintGroup;
        float time;

        // keep-ups: an approach ring closes on the touch point, the streak counts behind the player
        CanvasGroup jugGroup;
        Image jugTarget, jugApproach, jugGlow;
        TextMeshProUGUI jugCount, jugLabel, jugJudge;
        float jugPop, jugPopVel, jugJudgeT = 99f, jugEndT = 99f;

        // ------------------------------------------------------------------ building helpers

        static RectTransform Node(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        static Image Img(string name, Transform parent, Sprite sprite, Color color, Vector2 pos, Vector2 size, Image.Type type = Image.Type.Simple)
        {
            var rt = Node(name, parent, new Vector2(0.5f, 0.5f), pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.type = type;
            img.raycastTarget = false;
            return img;
        }

        static TextMeshProUGUI Text(string name, Transform parent, string text, float size, Color color,
            TextAlignmentOptions align, Vector2 pos, Vector2 box, bool bold = true, bool shadow = true, float spacing = 0f)
        {
            var rt = Node(name, parent, new Vector2(0.5f, 0.5f), pos, box);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = bold ? UiArt.FontBold : UiArt.FontRegular;
            var mat = bold ? UiArt.FontBoldShadow : UiArt.FontRegularShadow;
            if (shadow && mat != null) t.fontSharedMaterial = mat;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.characterSpacing = spacing;
            t.raycastTarget = false;
            t.text = text;
            return t;
        }

        static Image Stretch(Image img)
        {
            img.rectTransform.anchorMin = Vector2.zero;
            img.rectTransform.anchorMax = Vector2.one;
            img.rectTransform.sizeDelta = Vector2.zero;
            return img;
        }

        // ------------------------------------------------------------------ build

        public void Build(Transform parent, Camera camera, Player p, WaveDirector w, bool renderWithCamera)
        {
            UiArt.Build();
            cam = camera;
            player = p;
            waves = w;

            var go = new GameObject("HUD", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            canvas = go.AddComponent<Canvas>();
            if (renderWithCamera)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                canvas.sortingOrder = 1000;
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 10;
            }
            canvas.pixelPerfect = false;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasRect = (RectTransform)go.transform;

            BuildTags();
            BuildHealth();
            // skill bar: the normal shot (a little bigger) always far right, every other ability the same
            // size, shown only once unlocked and lined up leftwards in the order they were gained (LayoutSlots)
            shotSlot = BuildSlot("Shot", Vector2.zero, ShotSlotSize, UiArt.IconShot, UiArt.RingThick, Palette.ShotCyan, GameAction.Shoot, Ability.Shot);
            // the class move on the right mouse button: only the one of the current class is shown
            powerSlot = BuildSlot("Power", Vector2.zero, SkillSlotSize, UiArt.IconPower, UiArt.RingThick, Palette.PowerGold, GameAction.PowerShot, Ability.Power);
            headerSlot = BuildSlot("Header", Vector2.zero, SkillSlotSize, UiArt.IconHeader, UiArt.RingThick, Palette.Header, GameAction.PowerShot, Ability.Header);
            dashSlot = BuildSlot("Dash", Vector2.zero, SkillSlotSize, UiArt.IconDash, UiArt.RingThick, Palette.Trick, GameAction.PowerShot, Ability.Dash);
            // one slot object per unlockable ability; only the four a run picks up are ever shown,
            // and each takes the key of the slot it landed in (LayoutSlots assigns the action)
            flickSlot = BuildSlot("Flick", Vector2.zero, SkillSlotSize, UiArt.IconFlick, UiArt.RingRainbow, Color.white, GameAction.Skill1, Ability.Flick);
            stepSlot = BuildSlot("StepOver", Vector2.zero, SkillSlotSize, UiArt.IconStepOver, UiArt.RingThick, Palette.DashMint, GameAction.Skill1, Ability.StepOver);
            bikeSlot = BuildSlot("Bicycle", Vector2.zero, SkillSlotSize, UiArt.IconBicycle, UiArt.RingThick, Palette.BlastOrange, GameAction.Skill1, Ability.Bicycle);
            jugSlot = BuildSlot("Juggle", Vector2.zero, SkillSlotSize, UiArt.IconJuggle, UiArt.RingThick, Palette.Heal, GameAction.Skill1, Ability.Juggle);
            tackleSlot = BuildSlot("Tackle", Vector2.zero, SkillSlotSize, UiArt.IconTackle, UiArt.RingThick, Palette.Turf, GameAction.Skill1, Ability.Tackle);
            puntSlot = BuildSlot("Punt", Vector2.zero, SkillSlotSize, UiArt.IconPunt, UiArt.RingThick, Palette.Amber, GameAction.Skill1, Ability.Punt);
            wallSlot = BuildSlot("Wall", Vector2.zero, SkillSlotSize, UiArt.IconWall, UiArt.RingThick, Palette.Guard, GameAction.Skill1, Ability.Wall);
            nutmegSlot = BuildSlot("Nutmeg", Vector2.zero, SkillSlotSize, UiArt.IconNutmeg, UiArt.RingThick, Palette.Showboat, GameAction.Skill1, Ability.Nutmeg);
            decoySlot = BuildSlot("Decoy", Vector2.zero, SkillSlotSize, UiArt.IconDecoy, UiArt.RingThick, Palette.Trick, GameAction.Skill1, Ability.Decoy);
            whistleSlot = BuildSlot("Whistle", Vector2.zero, SkillSlotSize, UiArt.IconWhistle, UiArt.RingThick, Palette.Silver, GameAction.Skill1, Ability.Whistle);
            slots = new[] { shotSlot, powerSlot, flickSlot, stepSlot, bikeSlot, jugSlot,
                            tackleSlot, puntSlot, wallSlot, nutmegSlot, decoySlot, whistleSlot, headerSlot, dashSlot };
            foreach (var s in slots) s.root.gameObject.SetActive(false);   // LayoutSlots shows the unlocked ones
            BuildCrosshair();
            BuildWave();
            BuildBoss();
            BuildStageCard();
            BuildJuggle();
            BuildMisc();
            Coins = new CoinCounter();
            Coins.Build(canvasRect, WorldToCanvas);
            BuildCoop();
        }

        void BuildJuggle()
        {
            var root = Node("Juggle", canvasRect, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            jugGroup = root.gameObject.AddComponent<CanvasGroup>();
            jugGroup.alpha = 0f;
            jugGlow = Img("Glow", root, UiArt.Glow, Palette.ShotCyan.WithAlpha(0f), Vector2.zero, new Vector2(120f, 120f));
            jugTarget = Img("Target", root, UiArt.RingThin, Color.white.WithAlpha(0f), Vector2.zero, new Vector2(60f, 60f));
            jugApproach = Img("Approach", root, UiArt.RingThin, Color.white.WithAlpha(0f), Vector2.zero, new Vector2(60f, 60f));
            jugCount = Text("Count", root, "", 44f, Color.white, TextAlignmentOptions.Center, Vector2.zero, new Vector2(200f, 56f), true, true, 2f);
            jugLabel = Text("Label", root, "HOCHGEHALTEN", 12f, Palette.UiMuted, TextAlignmentOptions.Center, Vector2.zero, new Vector2(240f, 20f), true, true, 5f);
            jugJudge = Text("Judge", root, "", 17f, Palette.Gold, TextAlignmentOptions.Center, Vector2.zero, new Vector2(240f, 26f), true, true, 5f);
        }

        void BuildHealth()
        {
            hpGroup = Node("Health", canvasRect, new Vector2(0f, 1f), new Vector2(44f, -44f), Vector2.zero);
            Color glass = Palette.UiGlass.WithAlpha(0.86f);

            emblemGlow = Img("EmblemGlow", hpGroup, UiArt.Glow, Palette.HpA.WithAlpha(0.22f), new Vector2(38f, -38f), new Vector2(150f, 150f));
            shieldGlow = Img("ShieldGlow", hpGroup, UiArt.Glow, Palette.ShotCyan.WithAlpha(0f), new Vector2(38f, -38f), new Vector2(170f, 170f));
            Img("EmblemBase", hpGroup, UiArt.Circle, glass, new Vector2(38f, -38f), new Vector2(76f, 76f));
            emblemRing = Img("EmblemRing", hpGroup, UiArt.RingThick, Palette.HpA, new Vector2(38f, -38f), new Vector2(76f, 76f));
            Img("EmblemRim", hpGroup, UiArt.RingThin, Color.white.WithAlpha(0.18f), new Vector2(38f, -38f), new Vector2(92f, 92f));
            shieldRing = Img("ShieldRing", hpGroup, UiArt.RingThin, Palette.ShotCyan.WithAlpha(0f), new Vector2(38f, -38f), new Vector2(104f, 104f));
            Img("Heart", hpGroup, UiArt.Heart, Color.white, new Vector2(38f, -37f), new Vector2(32f, 32f));
            shieldText = Text("Shield", hpGroup, "", 13f, Palette.ShotCyan, TextAlignmentOptions.Center, new Vector2(38f, -94f), new Vector2(120f, 20f), true, true, 3f);

            float barX = 92f, barY = -30f;
            hpGlow = Img("BarGlow", hpGroup, UiArt.Glow, Palette.HpA.WithAlpha(0f), new Vector2(barX + BarW * 0.5f, barY), new Vector2(BarW + 80f, 70f));
            Img("BarBack", hpGroup, UiArt.Pill, glass, new Vector2(barX + BarW * 0.5f, barY), new Vector2(BarW, BarH), Image.Type.Sliced);
            Img("BarRim", hpGroup, UiArt.Pill, Color.white.WithAlpha(0.06f), new Vector2(barX + BarW * 0.5f, barY + 1f), new Vector2(BarW - 2f, BarH - 4f), Image.Type.Sliced);

            float innerH = BarH - BarInset * 2f;
            hpGhost = Img("Ghost", hpGroup, UiArt.Pill, Color.white.WithAlpha(0.85f), Vector2.zero, new Vector2(BarW, innerH), Image.Type.Sliced);
            hpGhostRt = hpGhost.rectTransform;
            hpGhostRt.pivot = new Vector2(0f, 0.5f);
            hpGhostRt.anchoredPosition = new Vector2(barX + BarInset, barY);
            hpFill = Img("Fill", hpGroup, UiArt.BarFill, Palette.HpB, Vector2.zero, new Vector2(BarW, innerH), Image.Type.Sliced);
            hpFillRt = hpFill.rectTransform;
            hpFillRt.pivot = new Vector2(0f, 0.5f);
            hpFillRt.anchoredPosition = new Vector2(barX + BarInset, barY);
            var shine = Img("Shine", hpFillRt, UiArt.Pill, Color.white.WithAlpha(0.22f), Vector2.zero, new Vector2(0f, 4f), Image.Type.Sliced);
            shine.rectTransform.anchorMin = new Vector2(0f, 1f);
            shine.rectTransform.anchorMax = new Vector2(1f, 1f);
            shine.rectTransform.offsetMin = new Vector2(6f, -7f);
            shine.rectTransform.offsetMax = new Vector2(-6f, -3f);

            for (int i = 1; i < 10; i++)
            {
                float x = barX + BarInset + (BarW - BarInset * 2f) * i / 10f;
                Img("Tick", hpGroup, UiArt.Pill, Palette.UiGlass.WithAlpha(0.45f), new Vector2(x, barY), new Vector2(2f, innerH), Image.Type.Sliced);
            }

            Text("Label", hpGroup, "LEBEN", 14f, Palette.UiMuted, TextAlignmentOptions.Left, new Vector2(barX + 60f, barY - 26f), new Vector2(120f, 20f), true, true, 6f);
            hpText = Text("Value", hpGroup, "100", 20f, Palette.UiText, TextAlignmentOptions.Right, new Vector2(barX + BarW - 60f, barY - 27f), new Vector2(120f, 24f));

            // the build: one medallion per upgrade, in pick order
            buildRoot = Node("Build", hpGroup, new Vector2(0.5f, 0.5f), new Vector2(92f, -92f), Vector2.zero);
        }

        Slot BuildSlot(string name, Vector2 pos, float size, Sprite icon, Sprite ringSprite, Color accent, GameAction action, Ability ability, string fixedLabel = null)
        {
            var s = new Slot { size = size, accent = accent, ability = ability, fixedLabel = fixedLabel, home = pos };
            s.root = Node(name, canvasRect, new Vector2(1f, 0f), pos, new Vector2(size, size));
            Color glass = Palette.UiGlass.WithAlpha(0.88f);
            s.glow = Img("Glow", s.root, UiArt.Glow, accent.WithAlpha(0.0f), Vector2.zero, Vector2.one * size * 2f);
            Img("Shadow", s.root, UiArt.Glow, new Color(0f, 0f, 0.02f, 0.45f), new Vector2(0f, -4f), Vector2.one * size * 1.45f);
            s.baseImg = Img("Base", s.root, UiArt.Circle, glass, Vector2.zero, Vector2.one * size);
            s.icon = Img("Icon", s.root, icon, Color.white, Vector2.zero, Vector2.one * size * 0.72f);
            s.iconRt = s.icon.rectTransform;
            s.overlay = Img("Cooldown", s.root, UiArt.Circle, new Color(0.01f, 0.02f, 0.05f, 0.62f), Vector2.zero, Vector2.one * (size - 2f), Image.Type.Filled);
            s.overlay.fillMethod = Image.FillMethod.Radial360;
            s.overlay.fillOrigin = (int)Image.Origin360.Top;
            s.overlay.fillClockwise = false;
            s.overlay.fillAmount = 0f;
            Img("Rim", s.root, UiArt.RingThin, Color.white.WithAlpha(0.16f), Vector2.zero, Vector2.one * (size + 10f));
            s.ring = Img("Progress", s.root, ringSprite, accent, Vector2.zero, Vector2.one * size, Image.Type.Filled);
            s.ring.fillMethod = Image.FillMethod.Radial360;
            s.ring.fillOrigin = (int)Image.Origin360.Top;
            s.ring.fillClockwise = true;
            s.ring.fillAmount = 1f;
            s.dot = Img("Dot", s.root, UiArt.Glow, Color.white.WithAlpha(0f), Vector2.zero, new Vector2(22f, 22f));
            s.flashRing = Img("Flash", s.root, UiArt.RingThick, Color.white.WithAlpha(0f), Vector2.zero, Vector2.one * size);
            s.timer = Text("Timer", s.root, "", size * 0.3f, Palette.UiText, TextAlignmentOptions.Center, new Vector2(0f, 1f), new Vector2(size, size * 0.5f));
            s.lockImg = Img("Lock", s.root, UiArt.IconLock, Color.white.WithAlpha(0f), new Vector2(0f, 1f), Vector2.one * size * 0.36f);

            // key badge (follows the current key binding)
            s.action = action;
            Vector2 badgePos = new Vector2(0f, -size * 0.5f - 4f);
            s.badgeRim = Img("BadgeRim", s.root, UiArt.Pill, Color.white.WithAlpha(0.16f), badgePos, new Vector2(36f, 28f), Image.Type.Sliced);
            s.badge = Img("Badge", s.root, UiArt.Pill, Palette.UiGlass.WithAlpha(0.97f), badgePos, new Vector2(34f, 26f), Image.Type.Sliced);
            s.mouseIcon = Img("Mouse", s.badge.rectTransform, UiArt.IconMouse, Color.white, Vector2.zero, new Vector2(14f, 18f));
            s.keyText = Text("Key", s.badge.rectTransform, "", 15f, Palette.UiText, TextAlignmentOptions.Center, Vector2.zero, new Vector2(120f, 26f), true, false, 1f);
            return s;
        }

        void RefreshBindings()
        {
            foreach (var s in slots)
            {
                var b = KeyBindings.Get(s.action);
                bool mouse = s.fixedLabel == null && b.Mouse && b.Button <= 1;   // left or right button: draw the mouse
                s.mouseIcon.gameObject.SetActive(mouse);
                s.mouseIcon.sprite = b.Button == 1 ? UiArt.IconMouseRight : UiArt.IconMouse;
                s.keyText.gameObject.SetActive(!mouse);
                string label = s.fixedLabel ?? KeyBindings.ShortName(s.action);
                s.keyText.text = label;
                s.keyText.fontSize = s.fixedLabel != null ? 12f : 15f;
                float w = mouse ? 34f : Mathf.Max(30f, 16f + label.Length * (s.fixedLabel != null ? 9f : 11f));
                s.badge.rectTransform.sizeDelta = new Vector2(w, 26f);
                s.badgeRim.rectTransform.sizeDelta = new Vector2(w + 2f, 28f);
            }
            if (hintText != null)
                hintText.text = KeyBindings.DisplayName(GameAction.Left) + " / " + KeyBindings.DisplayName(GameAction.Right) + "  LAUFEN    "
                    + KeyBindings.DisplayName(GameAction.Jump) + "  SPRINGEN    " + KeyBindings.DisplayName(GameAction.Down) + "  RUNTER    "
                    + KeyBindings.DisplayName(GameAction.Shoot) + "  SCHUSS    " + KeyBindings.DisplayName(GameAction.PowerShot) + "  " + Abilities.Name(Game.I != null && Game.I.Run != null ? Game.I.Run.Primary : Ability.Power) + "    ESC  PAUSE\n"
                    + "FÄHIGKEITEN  " + KeyBindings.DisplayName(GameAction.Skill1) + "  " + KeyBindings.DisplayName(GameAction.Skill2)
                    + "  " + KeyBindings.DisplayName(GameAction.Skill3) + "  " + KeyBindings.DisplayName(GameAction.Skill4)
                    + "   ·   EINE NEUE NACH JEDEM BOSS, HÖCHSTENS " + RunState.MaxSkills;
        }

        public void SetPaused(bool value) => paused = value;

        void BuildCrosshair()
        {
            cross = Node("Crosshair", canvasRect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(48f, 48f));
            Img("Glow", cross, UiArt.Glow, Palette.ShotCyan.WithAlpha(0.12f), Vector2.zero, new Vector2(70f, 70f));
            Img("Ring", cross, UiArt.RingThin, Color.white.WithAlpha(0.85f), Vector2.zero, new Vector2(26f, 26f));
            Img("Dot", cross, UiArt.Circle, Color.white, Vector2.zero, new Vector2(5f, 5f));
            crossArc = Img("Arc", cross, UiArt.RingThick, Palette.ShotCyan.WithAlpha(0.9f), Vector2.zero, new Vector2(40f, 40f), Image.Type.Filled);
            crossArc.fillMethod = Image.FillMethod.Radial360;
            crossArc.fillOrigin = (int)Image.Origin360.Top;
            crossArc.fillClockwise = true;
        }

        void BuildWave()
        {
            var g = Node("Wave", canvasRect, new Vector2(0.5f, 1f), new Vector2(0f, -34f), Vector2.zero);
            stageText = Text("Stage", g, "", 13f, Palette.UiMuted, TextAlignmentOptions.Center, new Vector2(0f, 0f), new Vector2(600f, 20f), true, true, 6f);
            pipRoot = Node("Pips", g, new Vector2(0.5f, 0.5f), new Vector2(0f, -28f), Vector2.zero);
            waveText = Text("WaveText", g, "", 20f, Palette.UiText, TextAlignmentOptions.Center, new Vector2(0f, -58f), new Vector2(500f, 28f), true, true, 8f);
            enemiesText = Text("Enemies", g, "", 13f, Palette.UiMuted, TextAlignmentOptions.Center, new Vector2(0f, -82f), new Vector2(400f, 20f), true, true, 4f);

            banner = Node("Banner", canvasRect, new Vector2(0.5f, 0.5f), new Vector2(0f, 190f), new Vector2(900f, 160f));
            bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.alpha = 0f;
            bannerText = Text("Title", banner, "", 78f, Color.white, TextAlignmentOptions.Center, new Vector2(0f, 10f), new Vector2(900f, 100f), true, true, 18f);
            bannerSub = Text("Sub", banner, "", 20f, Palette.UiMuted, TextAlignmentOptions.Center, new Vector2(0f, -52f), new Vector2(900f, 30f), false, true, 6f);
            bannerLineL = Img("LineL", banner, UiArt.LineFade, Color.white.WithAlpha(0.5f), new Vector2(-300f, 10f), new Vector2(200f, 2f)).rectTransform;
            bannerLineR = Img("LineR", banner, UiArt.LineFade, Color.white.WithAlpha(0.5f), new Vector2(300f, 10f), new Vector2(200f, 2f)).rectTransform;
        }

        void RebuildPips(int waveCount)
        {
            foreach (var p in pips) Object.Destroy(p.gameObject);
            foreach (var p in pipGlows) Object.Destroy(p.gameObject);
            pips.Clear(); pipGlows.Clear();
            int n = waveCount + 1;
            float spacing = 26f;
            for (int i = 0; i < n; i++)
            {
                bool boss = i == n - 1;
                float x = (i - (n - 1) * 0.5f) * spacing;
                pipGlows.Add(Img("PipGlow", pipRoot, UiArt.Glow, Color.clear, new Vector2(x, 0f), Vector2.one * (boss ? 52f : 40f)));
                if (boss) Img("BossRing", pipRoot, UiArt.RingThin, Palette.Hurt.WithAlpha(0.6f), new Vector2(x, 0f), Vector2.one * 24f).transform.SetParent(pipGlows[i].transform, true);
                pips.Add(Img(boss ? "BossPip" : "Pip", pipRoot, UiArt.Diamond, Color.white, new Vector2(x, 0f), Vector2.one * (boss ? 17f : 13f)));
            }
        }

        void BuildBoss()
        {
            var g = Node("Boss Bar", canvasRect, new Vector2(0.5f, 1f), new Vector2(0f, -160f), Vector2.zero);
            bossBarGroup = g.gameObject.AddComponent<CanvasGroup>();
            bossBarGroup.alpha = 0f;
            bossGlow = Img("Glow", g, UiArt.Glow, Palette.Hurt.WithAlpha(0.12f), Vector2.zero, new Vector2(BossW + 160f, 90f));
            bossName = Text("Name", g, "", 22f, Color.white, TextAlignmentOptions.Center, new Vector2(0f, 26f), new Vector2(BossW, 30f), true, true, 10f);
            bossPhase = Text("Phase", g, "", 12f, Palette.UiMuted, TextAlignmentOptions.Right, new Vector2(BossW * 0.5f - 60f, 26f), new Vector2(120f, 20f), true, true, 4f);
            Img("Back", g, UiArt.Pill, Palette.UiGlass.WithAlpha(0.9f), Vector2.zero, new Vector2(BossW + 10f, BossH + 10f), Image.Type.Sliced);
            bossGhost = Img("Ghost", g, UiArt.Pill, Color.white.WithAlpha(0.8f), Vector2.zero, new Vector2(BossW, BossH), Image.Type.Sliced);
            bossGhostRt = bossGhost.rectTransform;
            bossGhostRt.pivot = new Vector2(0f, 0.5f);
            bossGhostRt.anchoredPosition = new Vector2(-BossW * 0.5f, 0f);
            bossFill = Img("Fill", g, UiArt.BarFill, Palette.Hurt, Vector2.zero, new Vector2(BossW, BossH), Image.Type.Sliced);
            bossFillRt = bossFill.rectTransform;
            bossFillRt.pivot = new Vector2(0f, 0.5f);
            bossFillRt.anchoredPosition = new Vector2(-BossW * 0.5f, 0f);
            for (int i = 1; i <= 2; i++)
            {
                float x = -BossW * 0.5f + BossW * i / 3f;
                Img("Phase" + i, g, UiArt.Pill, Palette.UiGlass, new Vector2(x, 0f), new Vector2(3f, BossH + 4f), Image.Type.Sliced);
            }

            var intro = Node("Boss Intro", canvasRect, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            intro.anchorMin = Vector2.zero; intro.anchorMax = Vector2.one; intro.sizeDelta = Vector2.zero;
            bossIntroGroup = intro.gameObject.AddComponent<CanvasGroup>();
            bossIntroGroup.alpha = 0f;
            barTop = Img("BarTop", intro, null, new Color(0f, 0.005f, 0.015f, 0.92f), Vector2.zero, Vector2.zero).rectTransform;
            barTop.anchorMin = new Vector2(0f, 1f); barTop.anchorMax = new Vector2(1f, 1f); barTop.pivot = new Vector2(0.5f, 1f); barTop.sizeDelta = new Vector2(0f, 120f);
            barBottom = Img("BarBottom", intro, null, new Color(0f, 0.005f, 0.015f, 0.92f), Vector2.zero, Vector2.zero).rectTransform;
            barBottom.anchorMin = new Vector2(0f, 0f); barBottom.anchorMax = new Vector2(1f, 0f); barBottom.pivot = new Vector2(0.5f, 0f); barBottom.sizeDelta = new Vector2(0f, 120f);
            bossIntroLabel = Text("Label", intro, "STAGE-BOSS", 16f, Palette.Hurt, TextAlignmentOptions.Center, new Vector2(0f, 250f), new Vector2(900f, 24f), true, true, 14f);
            bossIntroName = Text("Name", intro, "", 96f, Color.white, TextAlignmentOptions.Center, new Vector2(0f, 190f), new Vector2(1400f, 120f), true, true, 16f);
            bossIntroTitle = Text("Title", intro, "", 22f, Palette.UiMuted, TextAlignmentOptions.Center, new Vector2(0f, 122f), new Vector2(900f, 30f), false, true, 6f);
        }

        void BuildStageCard()
        {
            stageCard = Node("Stage Card", canvasRect, new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(1200f, 400f));
            stageGroup = stageCard.gameObject.AddComponent<CanvasGroup>();
            stageGroup.alpha = 0f;
            stageAura = Img("Aura", stageCard, UiArt.Glow, Color.clear, new Vector2(0f, 20f), new Vector2(1300f, 420f));
            stageNum = Text("Number", stageCard, "", 18f, Palette.ShotCyan, TextAlignmentOptions.Center, new Vector2(0f, 104f), new Vector2(800f, 26f), true, true, 16f);
            stageName = Text("Name", stageCard, "", 86f, Color.white, TextAlignmentOptions.Center, new Vector2(0f, 44f), new Vector2(1400f, 110f), true, true, 18f);
            stageTag = Text("Tagline", stageCard, "", 22f, Palette.UiMuted, TextAlignmentOptions.Center, new Vector2(0f, -16f), new Vector2(1000f, 32f), false, true, 6f);
            stageLineL = Img("LineL", stageCard, UiArt.LineFade, Color.white.WithAlpha(0.45f), new Vector2(-300f, 104f), new Vector2(200f, 2f)).rectTransform;
            stageLineR = Img("LineR", stageCard, UiArt.LineFade, Color.white.WithAlpha(0.45f), new Vector2(300f, 104f), new Vector2(200f, 2f)).rectTransform;
            stageMechBack = Img("MechBack", stageCard, UiArt.Pill, Palette.UiGlass.WithAlpha(0.9f), new Vector2(0f, -74f), new Vector2(760f, 58f), Image.Type.Sliced);
            stageMechDot = Img("MechDot", stageMechBack.rectTransform, UiArt.Diamond, Color.white, new Vector2(-352f, 0f), new Vector2(16f, 16f));
            stageMechName = Text("MechName", stageMechBack.rectTransform, "", 15f, Color.white, TextAlignmentOptions.Left, new Vector2(-196f, 11f), new Vector2(280f, 22f), true, true, 5f);
            stageMechText = Text("MechText", stageMechBack.rectTransform, "", 16f, Palette.UiText, TextAlignmentOptions.Left, new Vector2(14f, -11f), new Vector2(700f, 24f), false, true, 0.5f);
        }

        void BuildTags()
        {
            var root = Node("Name Tags", canvasRect, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            for (int i = 0; i < 8; i++)
            {
                var rt = Node("Tag", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 50f));
                var t = new Tag { rt = rt };
                t.back = Img("Back", rt, UiArt.Pill, Palette.UiGlass.WithAlpha(0.7f), new Vector2(0f, 2f), new Vector2(160f, 24f), Image.Type.Sliced);
                t.line = Img("Line", rt, UiArt.LineFade, Color.white, new Vector2(0f, -12f), new Vector2(120f, 2f));
                t.name = Text("Name", rt, "", 14f, Color.white, TextAlignmentOptions.Center, new Vector2(0f, 2f), new Vector2(300f, 22f), true, true, 4f);
                t.affix = Text("Affix", rt, "", 11f, Palette.UiMuted, TextAlignmentOptions.Center, new Vector2(0f, -22f), new Vector2(300f, 18f), true, true, 3f);
                rt.gameObject.SetActive(false);
                tags.Add(t);
            }
        }

        void BuildMisc()
        {
            fpsText = Text("FPS", canvasRect, "", 15f, Palette.UiMuted, TextAlignmentOptions.Right, Vector2.zero, new Vector2(200f, 22f), true, true, 2f);
            var fr = fpsText.rectTransform;
            fr.anchorMin = fr.anchorMax = new Vector2(1f, 1f);
            fr.anchoredPosition = new Vector2(-364f, -46f);   // left of the coin counter

            devBadge = Text("DevBadge", canvasRect, "", 13f, Palette.Gold, TextAlignmentOptions.Right, Vector2.zero, new Vector2(1000f, 20f), true, true, 3f);
            devBadge.rectTransform.anchorMin = devBadge.rectTransform.anchorMax = new Vector2(1f, 1f);
            devBadge.rectTransform.anchoredPosition = new Vector2(-524f, -114f);
            // the info block sits on dark glass: the moon behind the top-right corner would swallow white text
            devInfoBack = Img("DevInfoBack", canvasRect, UiArt.Pill, Palette.UiGlass.WithAlpha(0.82f), Vector2.zero, new Vector2(560f, 84f), Image.Type.Sliced);
            var bRt = devInfoBack.rectTransform;
            bRt.anchorMin = bRt.anchorMax = bRt.pivot = new Vector2(1f, 1f);
            bRt.anchoredPosition = new Vector2(-20f, -130f);
            devInfoBack.enabled = false;
            devInfo = Text("DevInfo", canvasRect, "", 13f, Palette.UiText, TextAlignmentOptions.TopRight, Vector2.zero, new Vector2(760f, 72f), false, false, 0.5f);
            var iRt = devInfo.rectTransform;
            iRt.anchorMin = iRt.anchorMax = iRt.pivot = new Vector2(1f, 1f);
            iRt.anchoredPosition = new Vector2(-40f, -140f);
            devInfo.lineSpacing = 6f;

            toast = Text("Toast", canvasRect, "", 18f, Palette.UiText, TextAlignmentOptions.Center, new Vector2(0f, 300f), new Vector2(900f, 30f), true, true, 4f);
            toast.alpha = 0f;

            var numRoot = Node("DamageNumbers", canvasRect, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            numbers = new Number[32];
            for (int i = 0; i < numbers.Length; i++)
            {
                var t = Text("Num", numRoot, "", 34f, Color.white, TextAlignmentOptions.Center, Vector2.zero, new Vector2(200f, 60f));
                t.gameObject.SetActive(false);
                numbers[i] = new Number { rt = t.rectTransform, text = t };
            }

            var death = Node("Death", canvasRect, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            death.anchorMin = Vector2.zero; death.anchorMax = Vector2.one; death.sizeDelta = Vector2.zero;
            deathGroup = death.gameObject.AddComponent<CanvasGroup>();
            deathGroup.alpha = 0f;
            Stretch(Img("Dim", death, null, new Color(0.01f, 0.02f, 0.05f, 0.72f), Vector2.zero, Vector2.zero));
            Img("Aura", death, UiArt.Glow, Palette.Hurt.WithAlpha(0.08f), new Vector2(0f, 60f), new Vector2(1500f, 700f));
            Text("Label", death, "LAUF BEENDET", 16f, Palette.Hurt, TextAlignmentOptions.Center, new Vector2(0f, 190f), new Vector2(900f, 24f), true, true, 14f);
            Text("Title", death, "BESIEGT", 96f, Color.white, TextAlignmentOptions.Center, new Vector2(0f, 120f), new Vector2(1000f, 120f), true, true, 22f);
            deathSub = Text("Sub", death, "", 22f, Palette.UiText, TextAlignmentOptions.Center, new Vector2(0f, 46f), new Vector2(1200f, 30f), true, true, 5f);
            deathStats = Text("Stats", death, "", 17f, Palette.UiMuted, TextAlignmentOptions.Center, new Vector2(0f, 6f), new Vector2(1200f, 26f), true, true, 4f);
            deathBuild = Node("Build", death, new Vector2(0.5f, 0.5f), new Vector2(0f, -54f), Vector2.zero);
            deathBest = Text("Best", death, "", 15f, Palette.Gold, TextAlignmentOptions.Center, new Vector2(0f, -120f), new Vector2(900f, 24f), true, true, 5f);
            Text("Restart", death, "[ ENTER ]  NEUER LAUF", 18f, Palette.ShotCyan, TextAlignmentOptions.Center, new Vector2(0f, -170f), new Vector2(900f, 30f), true, true, 6f);

            // two lines (moves, then skills), shifted left so they clear the skill bar
            var hint = Node("Hint", canvasRect, new Vector2(0.5f, 0f), new Vector2(-230f, 58f), new Vector2(1200f, 56f));
            hintGroup = hint.gameObject.AddComponent<CanvasGroup>();
            hintText = Text("HintText", hint, "", 14f, Palette.UiMuted, TextAlignmentOptions.Center, Vector2.zero, new Vector2(1200f, 56f), true, true, 2.5f);
            RefreshBindings();
            KeyBindings.Changed += RefreshBindings;

            fade = Img("Fade", canvasRect, null, new Color(0.01f, 0.02f, 0.05f, 1f), Vector2.zero, Vector2.zero);
            Stretch(fade);
        }

        // ------------------------------------------------------------------ events

        public void OnShotUsed() { shotSlot.popVel -= 6f; crossPunchVel += 9f; }
        public void OnFlickUsed() { flickSlot.popVel -= 7f; }

        public void OnSkillUsed(Ability ability)
        {
            var s = SlotFor(ability);
            if (s != null) s.popVel -= 7f;
            if (ability == Ability.Power) crossPunchVel += 12f;
        }

        /// <summary>A skill key was pressed while its slot is still empty.</summary>
        public void OnEmptySlot(int slot)
        {
            var run = Game.I.Run;
            ShowToast(run.CanUnlockMore
                ? "PLATZ " + (slot + 1) + " IST LEER  ·  NACH JEDEM BOSS WÄHLST DU EINE FÄHIGKEIT"
                : "PLATZ " + (slot + 1) + " BLEIBT LEER  ·  " + RunState.MaxSkills + " FÄHIGKEITEN SIND DAS MAXIMUM");
        }

        /// <summary>The whistle just filled up.</summary>
        public void OnWhistleReady()
        {
            var s = SlotFor(Ability.Whistle);
            if (s != null) { s.popVel += 14f; s.flashT = 0f; }
            ShowToast("SCHLUSSPFIFF BEREIT");
        }

        public void OnPlayerDamaged(float amount)
        {
            ghostDelay = 0.45f;
            hpShake = 1f;
        }

        public void OnMonsterKilled() { }

        public void ShowWaveBanner(int wave, int total)
        {
            bannerT = 0f;
            bannerText.text = "WELLE " + wave;
            bannerText.color = Color.white;
            bannerSub.text = wave == total ? "LETZTE WELLE VOR DEM BOSS" : wave == 1 ? "DIE MONSTER KOMMEN" : "SIE WERDEN STÄRKER";
        }

        public void OnWaveCleared(int wave, int total, bool upgrade)
        {
            bannerT = 0f;
            bannerText.text = "GESCHAFFT";
            bannerText.color = Color.white;
            bannerSub.text = "WELLE " + wave + " VON " + total + " ÜBERSTANDEN  ·  " + (upgrade ? "ZEIT FÜR EIN UPGRADE" : "UPGRADE NACH DER NÄCHSTEN RUNDE");
        }

        public void OnStageCleared(int stage, StageTheme theme)
        {
            bannerT = -0.6f;
            bannerText.text = "STAGE GESCHAFFT";
            bannerText.color = Palette.Gold;
            bannerSub.text = StageThemes.Title(stage) + " BEFREIT";
            bossBarA = 0f;
        }

        public void ShowStageCard(int stage, StageTheme theme)
        {
            stageT = 0f;
            bannerT = 99f;
            stageNum.text = "STAGE " + stage;
            stageNum.color = theme.Accent;
            stageName.text = StageThemes.Title(stage);
            stageTag.text = theme.Tagline.ToUpperInvariant();
            bool mech = theme.Mechanic != StageMechanic.None;
            stageMechName.text = mech ? theme.MechanicName : "ERSTER STAGE";
            stageMechName.color = theme.Accent;
            stageMechText.text = theme.MechanicText;
            stageMechDot.color = theme.Accent;
            stageAura.color = theme.Accent.WithAlpha(0.08f);
            shownStage = -1;
        }

        public void HideStageCard() { stageT = 99f; stageGroup.alpha = 0f; }

        public void ShowBossIntro(string name, string title)
        {
            bossIntroT = 0f;
            bannerT = 99f;
            bossIntroName.text = name;
            bossIntroTitle.text = title.ToUpperInvariant();
            bossIntroName.color = Color.Lerp(Color.white, Game.I.Run.Theme.Accent, 0.25f);
        }

        public void OnUpgradeTaken(UpgradeDef u)
        {
            SyncBuild();
            if (u != null) foreach (var b in buildIcons) if (b.id == u.Id) b.popVel += 14f;
        }

        public void OnAbilityUnlocked(Ability a)
        {
            foreach (var s in slots) if (s.ability == a) { s.unlockT = 0f; s.popVel += 16f; }
            ShowToast("NEUE FÄHIGKEIT  ·  " + Abilities.Name(a));
        }

        public void ShowToast(string text) { toast.text = text; toastT = 0f; }

        public void SetVisible(bool visible) => canvas.enabled = visible;

        public void DamageNumber(Vector2 world, float amount, bool big) => DamageNumber(world, amount, big, false);

        public void DamageNumber(Vector2 world, float amount, bool big, bool crit, bool boosted = false)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt(amount));
            if (crit) Popup(world, n + "!", new Color(1f, 0.55f, 0.25f), boosted ? 58f : 52f, true);
            // the striker's boosted shots: hot red numbers, a size up, always the punchy pop
            else if (boosted) Popup(world, n.ToString(), Color.Lerp(Classes.Striker.Accent, Color.white, 0.25f), big ? 48f : 40f, true);
            else Popup(world, n.ToString(), big ? Palette.Gold : Color.white, big ? 44f : 32f, big);
        }

        /// <summary>Floating text in world space (damage, healing).</summary>
        public void Popup(Vector2 world, string text, Color color, float size, bool big = false)
        {
            var n = numbers[numberCursor];
            numberCursor = (numberCursor + 1) % numbers.Length;
            n.world = world + new Vector2(Random.Range(-0.2f, 0.2f), 0f);
            n.age = 0f;
            n.big = big;
            n.drift = Random.Range(-0.35f, 0.35f);
            n.text.text = text;
            n.text.fontSize = size;
            n.text.color = color;
            n.rt.gameObject.SetActive(true);
        }

        public void OnJuggleStart()
        {
            jugEndT = 99f;
            jugCount.text = "0";
            jugCount.color = Color.white;
            jugJudge.text = "";
            jugJudgeT = 99f;
        }

        public void OnJuggleTouch(int count, bool perfect, float healed, Vector2 world)
        {
            jugCount.text = count.ToString();
            jugCount.color = perfect ? Palette.Gold : Color.white;
            jugPopVel += perfect ? 9f : 6f;
            jugJudge.text = perfect ? "PERFEKT" : "GUT";
            jugJudge.color = perfect ? Palette.Gold : Palette.ShotCyan;
            jugJudgeT = 0f;
            bool streak = count % 10 == 0;
            if (healed > 0.01f) Popup(player.Pos + new Vector2(0f, 2.15f), "+" + Mathf.RoundToInt(healed), Palette.Heal, streak ? 38f : 26f, streak);
            if (streak) ShowToast(count + "ER SERIE  ·  BONUS-HEILUNG");
        }

        public void OnJuggleEnd(int count, bool early)
        {
            jugEndT = 0f;
            jugJudge.text = early ? "ZU FRÜH" : "ZU SPÄT";
            jugJudge.color = Palette.Hurt;
            jugJudgeT = 0f;
        }

        public void ResetState()
        {
            hpDisplay = hpGhostV = 1f;
            ghostDelay = 0f;
            shownHp = shownMaxHp = shownShield = -1;
            bannerT = 99f;
            stageT = 99f;
            bossIntroT = 99f;
            bossBarA = 0f;
            shownBoss = null;
            fadeT = 0f;
            deathGroup.alpha = 0f;
            deathTextSet = false;
            shownStage = shownWave = shownEnemies = shownWaves = -1;
            foreach (var n in numbers) { n.age = 99f; n.rt.gameObject.SetActive(false); }
            foreach (var t in tags) { t.target = null; t.alpha = 0f; t.rt.gameObject.SetActive(false); }
            jugEndT = 99f;
            jugGroup.alpha = 0f;
            Coins?.Reset();
            RefreshBindings();   // the right mouse button names the class move of this run's player
            SyncBuild();
        }

        // ------------------------------------------------------------------ build strip

        BuildIcon MakeBuildIcon(Transform parent, UpgradeDef u)
        {
            var rt = Node(u.Id, parent, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34f, 34f));
            Color rc = Rarities.Of(u.Rarity);
            var b = new BuildIcon { rt = rt, id = u.Id };
            b.fill = Img("Fill", rt, UiArt.Circle, Color.Lerp(Palette.UiGlass, rc, 0.28f).WithAlpha(0.95f), Vector2.zero, new Vector2(32f, 32f));
            b.ring = Img("Ring", rt, UiArt.RingThin, rc.WithAlpha(0.9f), Vector2.zero, new Vector2(34f, 34f));
            b.icon = Img("Icon", rt, UpgradeIcons.Get(u.Icon), Color.white, Vector2.zero, new Vector2(24f, 24f));
            b.count = Text("Count", rt, "", 11f, Color.white, TextAlignmentOptions.Center, new Vector2(12f, -12f), new Vector2(30f, 16f), true, true, 0f);
            return b;
        }

        void SyncBuild()
        {
            var run = Game.I != null ? Game.I.Run : null;
            if (run == null) return;
            // one medallion per distinct upgrade, in the order they were first taken
            var order = new List<string>();
            foreach (var id in run.PickOrder) if (!order.Contains(id)) order.Add(id);
            for (int i = buildIcons.Count - 1; i >= 0; i--)
                if (!order.Contains(buildIcons[i].id)) { Object.Destroy(buildIcons[i].rt.gameObject); buildIcons.RemoveAt(i); }
            for (int i = 0; i < order.Count; i++)
            {
                var id = order[i];
                var b = buildIcons.Find(x => x.id == id);
                if (b == null) { b = MakeBuildIcon(buildRoot, UpgradeDb.Get(id)); buildIcons.Add(b); }
                int lvl = run.Stacks(id);
                b.count.text = lvl > 1 ? lvl.ToString() : "";
                int col = i % 10, row = i / 10;
                b.rt.anchoredPosition = new Vector2(17f + col * 38f, -row * 38f);
            }
        }

        // ------------------------------------------------------------------ update

        Vector2 WorldToCanvas(Vector2 world)
        {
            Vector3 vp = cam.WorldToViewportPoint(new Vector3(world.x, world.y, 0f));
            Vector2 size = canvasRect.rect.size;
            return new Vector2((vp.x - 0.5f) * size.x, (vp.y - 0.5f) * size.y);
        }

        public void Update(float dt)
        {
            time += dt;
            var run = Game.I.Run;

            UpdateHealth(dt);
            bool juggling = player.CurrentAction == Player.Action.Juggle;
            bool withBall = player.Ball.IsHeld && !player.Dead && !juggling;
            LayoutSlots(run, dt);
            bool free = !player.Dead && !juggling;
            UpdateSlot(shotSlot, player.ShotCd, player.ShotCooldownTotal, withBall, dt, false, true);
            UpdateSlot(powerSlot, player.PowerCd, player.PowerCooldownTotal, withBall && player.Grounded, dt, false, true);
            UpdateSlot(stepSlot, player.StepOverCd, player.StepOverCooldownTotal, player.Grounded && free, dt, false, true);
            UpdateSlot(bikeSlot, player.BicycleCd, player.BicycleCooldownTotal, withBall && !player.Grounded, dt, false, true);
            UpdateSlot(flickSlot, player.FlickCd, player.FlickCooldownTotal, withBall && player.Grounded, dt, true, true);
            UpdateSlot(jugSlot, 0f, 1f, player.Ball.IsHeldFree && player.Grounded && !player.Dead, dt, false, true);
            UpdateSlot(tackleSlot, player.TackleCd, player.TackleCooldownTotal, player.Grounded && free, dt, false, true);
            UpdateSlot(puntSlot, player.PuntCd, player.PuntCooldownTotal, withBall && player.Grounded, dt, false, true);
            UpdateSlot(wallSlot, player.WallCd, player.WallCooldownTotal, player.Grounded && free, dt, false, true);
            UpdateSlot(nutmegSlot, player.NutmegCd, player.NutmegCooldownTotal, player.Grounded && free, dt, false, true);
            UpdateSlot(decoySlot, player.DecoyCd, player.DecoyCooldownTotal, free, dt, false, true);
            // the whistle has no timer: its ring is the charge that kills build up
            UpdateSlot(whistleSlot, 1f - player.Ultimate, 1f, free, dt, false, true);
            UpdateSlot(headerSlot, player.HeaderCd, player.HeaderCooldownTotal, withBall, dt, false, true);
            UpdateSlot(dashSlot, player.DashCd, player.DashCooldownTotal, free, dt, false, true);
            UpdateCrosshair(dt);
            UpdateWave(dt, run);
            UpdateBoss(dt);
            UpdateStageCard(dt);
            UpdateTags(dt);
            UpdateBuild(dt);
            UpdateJuggle(dt);
            UpdateNumbers(dt);
            UpdateMisc(dt, run);
            Coins.Update(dt);
            if (paused) bannerGroup.alpha = 0f;
        }

        void UpdateHealth(float dt)
        {
            float frac = Mathf.Clamp01(player.Hp / player.MaxHp);
            hpDisplay = MathUtil.Damp(hpDisplay, frac, 16f, dt);
            if (frac > hpGhostV) hpGhostV = frac;
            ghostDelay -= dt;
            if (ghostDelay <= 0f) hpGhostV = MathUtil.Damp(hpGhostV, frac, 5f, dt);

            float innerW = BarW - BarInset * 2f, innerH = BarH - BarInset * 2f;
            float fw = innerW * hpDisplay;
            hpFill.enabled = fw > 0.5f;
            hpFillRt.sizeDelta = new Vector2(Mathf.Max(innerH, fw), innerH);
            float gw = innerW * hpGhostV;
            hpGhost.enabled = gw > fw + 0.5f;
            hpGhostRt.sizeDelta = new Vector2(Mathf.Max(innerH, gw), innerH);
            hpGhost.color = Color.Lerp(Palette.HpA, Color.white, 0.75f).WithAlpha(0.9f);

            float low = frac < 0.3f && !player.Dead ? 1f - frac / 0.3f : 0f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(time * 7f);
            Color fillCol = Color.Lerp(Palette.HpA, Palette.HpB, Mathf.Clamp01((frac - 0.2f) / 0.8f));
            hpFill.color = Color.Lerp(fillCol, Color.white, low * pulse * 0.3f);
            hpGlow.color = Palette.HpA.WithAlpha(low * (0.25f + 0.25f * pulse));
            emblemRing.color = fillCol;
            emblemGlow.color = fillCol.WithAlpha(0.18f + low * pulse * 0.3f);
            Game.I.Post.SetLowHealth(low);

            hpShake = Mathf.Max(0f, hpShake - dt * 3.5f);
            float shake = Mathf.Sin(time * 70f) * 7f * hpShake * hpShake;
            hpGroup.anchoredPosition = new Vector2(44f + shake, -44f);

            int hp = Mathf.CeilToInt(player.Hp), max = Mathf.RoundToInt(player.MaxHp);
            if (hp != shownHp || max != shownMaxHp)
            {
                shownHp = hp;
                shownMaxHp = max;
                hpText.text = hp + "<size=70%><color=#8FA3B8> / " + max + "</color></size>";
            }

            // captain's shield: a cyan ring around the emblem, pips for extra charges
            int shield = player.Shield;
            float sA = shield > 0 ? 0.85f + 0.15f * Mathf.Sin(time * 3f) : 0f;
            shieldRing.color = Palette.ShotCyan.WithAlpha(sA);
            shieldGlow.color = Palette.ShotCyan.WithAlpha(sA * 0.14f);
            if (shield != shownShield)
            {
                shownShield = shield;
                shieldText.text = shield > 0 ? "SCHILD" + (shield > 1 ? " ×" + shield : "") : "";
            }
        }

        Slot SlotFor(Ability a)
        {
            foreach (var s in slots) if (s.ability == a) return s;
            return null;
        }

        /// <summary>
        /// Shot far right, then every unlocked ability leftwards in unlock order; locked ones are hidden.
        /// Slots glide to a new place instead of jumping; a freshly unlocked one appears in place.
        /// </summary>
        void LayoutSlots(RunState run, float dt)
        {
            float x = -SlotRight;
            Place(shotSlot, ref x, dt);
            var primary = SlotFor(run.Primary);
            if (primary != null) Place(primary, ref x, dt);
            bool rebind = false;
            for (int i = 0; i < run.Skills.Count; i++)
            {
                var s = SlotFor(run.Skills[i]);
                if (s == null) continue;
                Place(s, ref x, dt);
                // the ability answers to the key of the slot it landed in
                var action = (GameAction)((int)GameAction.Skill1 + i);
                if (s.action != action) { s.action = action; rebind = true; }
            }
            if (rebind) RefreshBindings();
            foreach (var s in slots)
            {
                bool show = s.ability == Ability.Shot || s.ability == run.Primary ? run.Has(s.ability) : run.SlotOf(s.ability) >= 0;
                if (s.root.gameObject.activeSelf != show) s.root.gameObject.SetActive(show);
                if (!show) s.placed = false;
            }
        }

        void Place(Slot s, ref float x, float dt)
        {
            x -= s.size * 0.5f;
            var target = new Vector2(x, SlotBottom + s.size * 0.5f);   // bottoms line up, the shot rises a little higher
            s.home = s.placed ? Vector2.Lerp(s.home, target, 1f - Mathf.Exp(-14f * dt)) : target;
            s.placed = true;
            x -= s.size * 0.5f + SlotGap;
        }

        void UpdateSlot(Slot s, float remaining, float total, bool available, float dt, bool rainbow, bool unlocked)
        {
            if (!s.root.gameObject.activeSelf) return;
            float frac = unlocked ? Mathf.Clamp01(remaining / Mathf.Max(0.01f, total)) : 1f;
            s.overlay.fillAmount = unlocked ? frac : 0f;
            s.ring.fillAmount = unlocked ? 1f - frac : 0f;

            bool ready = unlocked && remaining <= 0f;
            if (ready && s.prevRemaining > 0f)
            {
                s.flashT = 0f;
                s.popVel += 9f;
            }
            s.prevRemaining = unlocked ? remaining : 0f;

            MathUtil.Spring(ref s.pop, ref s.popVel, 0f, 3.2f, 0.35f, dt);
            float scale = (1f + s.pop * 0.05f) * (unlocked ? 1f : 0.9f);
            s.root.localScale = new Vector3(scale, scale, 1f);
            s.shakeT += dt;
            float shake = s.shakeT < 0.4f ? Mathf.Sin(s.shakeT * 60f) * 8f * (1f - s.shakeT / 0.4f) : 0f;
            s.root.anchoredPosition = s.home + new Vector2(shake, 0f);

            // leading edge dot of the charge ring
            if (unlocked && !ready)
            {
                float ang = (90f - (1f - frac) * 360f) * Mathf.Deg2Rad;
                float r = s.size * 0.5f - 3.3f;
                s.dot.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                Color dc = rainbow ? Color.HSVToRGB(Mathf.Lerp(0f, 0.8f, 1f - frac), 0.6f, 1f) : s.accent;
                s.dot.color = dc.WithAlpha(0.95f);
            }
            else s.dot.color = Color.clear;

            float breathe = 0.5f + 0.5f * Mathf.Sin(time * 2.4f);
            s.ring.color = (rainbow ? Color.white : s.accent).WithAlpha(ready ? 0.85f + 0.15f * breathe : 0.9f);
            s.glow.color = (rainbow ? Color.white : s.accent).WithAlpha(ready && available ? 0.1f + 0.06f * breathe : 0f);

            // unlock: a bright burst, the lock falls away
            s.unlockT += dt;
            s.flashT += dt;
            float ut = s.unlockT / 0.9f;
            float ft = s.flashT / 0.45f;
            if (ut < 1f)
            {
                float fs = Mathf.Lerp(1f, 2.2f, MathUtil.EaseOutCubic(ut));
                s.flashRing.rectTransform.localScale = new Vector3(fs, fs, 1f);
                s.flashRing.color = (rainbow ? Color.white : s.accent).WithAlpha(1f - ut);
                s.glow.color = (rainbow ? Color.white : s.accent).WithAlpha(0.5f * (1f - ut));
            }
            else if (ft < 1f)
            {
                float fs = Mathf.Lerp(1f, 1.55f, MathUtil.EaseOutCubic(ft));
                s.flashRing.rectTransform.localScale = new Vector3(fs, fs, 1f);
                s.flashRing.color = (rainbow ? Color.white : s.accent).WithAlpha(0.8f * (1f - ft));
            }
            else s.flashRing.color = Color.clear;

            float lockA = unlocked ? (ut < 1f ? 1f - MathUtil.Smooth01(ut * 3f) : 0f) : 0.85f;
            s.lockImg.color = Color.white.WithAlpha(lockA);
            s.lockImg.rectTransform.anchoredPosition = new Vector2(0f, 1f - (unlocked && ut < 1f ? ut * 30f : 0f));
            s.baseImg.color = Palette.UiGlass.WithAlpha(unlocked ? 0.88f : 0.6f);

            float iconAlpha = !unlocked ? 0.16f : available ? 1f : 0.42f;
            s.icon.color = new Color(1f, 1f, 1f, MathUtil.Damp(s.icon.color.a, ready || !unlocked ? iconAlpha : iconAlpha * 0.6f, 12f, dt));
            s.badge.color = Palette.UiGlass.WithAlpha(unlocked ? 0.97f : 0.6f);
            s.keyText.alpha = unlocked ? 1f : 0.4f;

            // only touch the text when the displayed value changes (no per-frame string garbage)
            int tenths = unlocked && !ready && total > 1f ? Mathf.CeilToInt(remaining * 10f) : -1;
            if (tenths != s.shownTenths)
            {
                s.shownTenths = tenths;
                s.timer.text = tenths >= 0 ? (tenths / 10f).ToString("0.0") : "";
            }
        }

        void UpdateCrosshair(float dt)
        {
            bool visible = !player.Dead && !paused;
            cross.gameObject.SetActive(visible);
            if (!visible) return;
            cross.anchoredPosition = WorldToCanvas(GameInput.AimWorld);
            MathUtil.Spring(ref crossPunch, ref crossPunchVel, 0f, 4f, 0.4f, dt);
            float s = 1f + crossPunch * 0.06f;
            cross.localScale = new Vector3(s, s, 1f);
            float frac = Mathf.Clamp01(player.ShotCd / Mathf.Max(0.01f, player.ShotCooldownTotal));
            crossArc.fillAmount = frac > 0f ? 1f - frac : 0f;
            crossArc.color = Palette.ShotCyan.WithAlpha(frac > 0f ? 0.9f : 0f);
        }

        void UpdateWave(float dt, RunState run)
        {
            var director = Game.I.Director;
            int waveCount = run.WavesInStage;
            if (waveCount != shownWaves) { shownWaves = waveCount; RebuildPips(waveCount); shownWave = -1; }
            if (run.Stage != shownStage)
            {
                shownStage = run.Stage;
                stageText.text = "STAGE " + run.Stage + "  ·  " + StageThemes.Title(run.Stage);
            }
            if (run.Wave != shownWave)
            {
                shownWave = run.Wave;
                shownEnemies = -1;
                waveText.text = run.Wave <= 0 ? "BEREIT MACHEN" : run.IsBossWave ? "BOSSKAMPF" : "WELLE " + run.Wave + " / " + waveCount;
                waveText.color = run.IsBossWave ? Palette.Hurt : Palette.UiText;
            }
            bool fighting = director != null && (director.P == RunDirector.Phase.Fighting || director.P == RunDirector.Phase.WaveIntro);
            int remaining = fighting && director != null && !run.IsBossWave ? director.Remaining : -2;
            if (remaining != shownEnemies)
            {
                shownEnemies = remaining;
                enemiesText.text = remaining >= 0 ? (remaining == 1 ? "1 GEGNER" : remaining + " GEGNER") : "";
            }

            // pips: done = filled, current = glowing and breathing, future = hollow; the last one is the boss
            var theme = run.Theme;
            for (int i = 0; i < pips.Count; i++)
            {
                int wave = i + 1;
                bool boss = i == pips.Count - 1;
                var ph = director != null ? director.P : RunDirector.Phase.Idle;
                bool cleared = ph == RunDirector.Phase.WaveCleared || ph == RunDirector.Phase.Reward || ph == RunDirector.Phase.StageCleared || ph == RunDirector.Phase.AbilityPick;
                bool done = run.Wave > wave || (run.Wave == wave && cleared);
                bool current = run.Wave == wave && !done;
                Color c = boss ? Palette.Hurt : theme.Accent;
                float breathe = 0.5f + 0.5f * Mathf.Sin(time * 4f);
                pips[i].color = done ? c : current ? Color.Lerp(c, Color.white, 0.4f * breathe) : Color.white.WithAlpha(0.22f);
                float s = current ? 1.25f + 0.1f * breathe : 1f;
                pips[i].rectTransform.localScale = new Vector3(s, s, 1f);
                pipGlows[i].color = c.WithAlpha(current ? 0.35f + 0.15f * breathe : done ? 0.12f : 0f);
            }

            bannerT += dt;
            const float dur = 2.3f;
            if (bannerT >= 0f && bannerT < dur)
            {
                float tIn = MathUtil.EaseOutCubic(bannerT / 0.4f);
                float tOut = MathUtil.Smooth01((bannerT - (dur - 0.5f)) / 0.5f);
                bannerGroup.alpha = tIn * (1f - tOut);
                float s = Mathf.Lerp(1.25f, 1f, tIn) + tOut * 0.05f;
                banner.localScale = new Vector3(s, s, 1f);
                banner.anchoredPosition = new Vector2(0f, 190f + tOut * 30f);
                float lw = 220f * MathUtil.EaseOutCubic((bannerT - 0.1f) / 0.6f);
                bannerLineL.sizeDelta = new Vector2(lw, 2f);
                bannerLineR.sizeDelta = new Vector2(lw, 2f);
                float half = Mathf.Max(250f, bannerText.preferredWidth * 0.5f + 30f);
                bannerLineL.anchoredPosition = new Vector2(-half - lw * 0.5f, 10f);
                bannerLineR.anchoredPosition = new Vector2(half + lw * 0.5f, 10f);
            }
            else bannerGroup.alpha = 0f;
        }

        void UpdateBoss(float dt)
        {
            // intro card with letterbox bars
            bossIntroT += dt;
            const float introDur = 2.6f;
            if (bossIntroT < introDur)
            {
                float tIn = MathUtil.EaseOutCubic(bossIntroT / 0.45f);
                float tOut = MathUtil.Smooth01((bossIntroT - (introDur - 0.5f)) / 0.5f);
                bossIntroGroup.alpha = 1f - tOut;
                barTop.anchoredPosition = new Vector2(0f, 120f * (1f - tIn) + 120f * tOut);
                barBottom.anchoredPosition = new Vector2(0f, -120f * (1f - tIn) - 120f * tOut);
                float ns = Mathf.Lerp(1.3f, 1f, MathUtil.EaseOutCubic((bossIntroT - 0.2f) / 0.5f));
                bossIntroName.rectTransform.localScale = new Vector3(ns, ns, 1f);
                bossIntroName.alpha = MathUtil.Smooth01((bossIntroT - 0.2f) / 0.3f);
                bossIntroName.characterSpacing = Mathf.Lerp(40f, 16f, MathUtil.EaseOutCubic((bossIntroT - 0.2f) / 1.2f));
                bossIntroTitle.alpha = MathUtil.Smooth01((bossIntroT - 0.6f) / 0.4f);
                bossIntroLabel.alpha = MathUtil.Smooth01((bossIntroT - 0.1f) / 0.3f);
            }
            else bossIntroGroup.alpha = 0f;

            var boss = waves.Boss;
            if (boss != null && boss != shownBoss)
            {
                shownBoss = boss;
                bossDisplay = bossGhostV = 1f;
                bossName.text = boss.DisplayName;
                bossFill.color = Color.Lerp(Palette.Hurt, Game.I.Run.Theme.Accent, 0.25f);
            }
            bossBarA = MathUtil.Damp(bossBarA, boss != null ? 1f : 0f, 6f, dt);
            bossBarGroup.alpha = bossBarA;
            if (boss == null) return;
            float frac = Mathf.Clamp01(boss.Hp / boss.MaxHp);
            bossDisplay = MathUtil.Damp(bossDisplay, frac, 14f, dt);
            if (frac < bossGhostV - 0.001f && bossGhostDelay <= 0f) bossGhostDelay = 0.5f;
            bossGhostDelay -= dt;
            if (bossGhostDelay <= 0f) bossGhostV = MathUtil.Damp(bossGhostV, frac, 4f, dt);
            bossFillRt.sizeDelta = new Vector2(Mathf.Max(BossH, BossW * bossDisplay), BossH);
            bossFill.enabled = bossDisplay > 0.005f;
            bossGhostRt.sizeDelta = new Vector2(Mathf.Max(BossH, BossW * bossGhostV), BossH);
            bossGhost.enabled = bossGhostV > bossDisplay + 0.003f;
            int phase = boss.BossPhase;
            bossPhase.text = "PHASE " + (phase + 1);
            bossGlow.color = Palette.Hurt.WithAlpha(0.08f + 0.06f * phase + 0.04f * Mathf.Sin(time * 5f));
        }

        void UpdateStageCard(float dt)
        {
            stageT += dt;
            const float dur = 3.4f;
            if (stageT >= dur) { stageGroup.alpha = 0f; return; }
            float tIn = MathUtil.EaseOutCubic(stageT / 0.6f);
            float tOut = MathUtil.Smooth01((stageT - (dur - 0.6f)) / 0.6f);
            stageGroup.alpha = tIn * (1f - tOut);
            stageName.characterSpacing = Mathf.Lerp(46f, 18f, MathUtil.EaseOutCubic(stageT / 1.6f));
            float s = Mathf.Lerp(1.08f, 1f, tIn);
            stageCard.localScale = new Vector3(s, s, 1f);
            stageCard.anchoredPosition = new Vector2(0f, 150f + tOut * 24f);
            float lw = 240f * MathUtil.EaseOutCubic((stageT - 0.2f) / 0.8f);
            stageLineL.sizeDelta = new Vector2(lw, 2f);
            stageLineR.sizeDelta = new Vector2(lw, 2f);
            stageLineL.anchoredPosition = new Vector2(-130f - lw * 0.5f, 104f);
            stageLineR.anchoredPosition = new Vector2(130f + lw * 0.5f, 104f);
            float mech = MathUtil.EaseOutCubic((stageT - 0.7f) / 0.5f);
            stageMechBack.rectTransform.localScale = new Vector3(Mathf.Lerp(0.85f, 1f, mech), 1f, 1f);
            stageMechBack.color = Palette.UiGlass.WithAlpha(0.9f * Mathf.Clamp01(mech));
            stageMechName.alpha = stageMechText.alpha = Mathf.Clamp01(mech);
            stageMechDot.color = stageMechDot.color.WithAlpha(Mathf.Clamp01(mech));
            stageTag.alpha = MathUtil.Smooth01((stageT - 0.4f) / 0.4f);
        }

        void UpdateTags(float dt)
        {
            // assign tags to named monsters that don't have one yet
            foreach (var m in waves.Monsters)
            {
                if (!m.Alive || (m.Rank != Rank.Elite && m.Rank != Rank.MiniBoss)) continue;
                bool has = false;
                foreach (var t in tags) if (t.target == m) { has = true; break; }
                if (has) continue;
                foreach (var t in tags)
                {
                    if (t.target != null && t.target.Alive) continue;
                    t.target = m;
                    t.alpha = 0f;
                    bool mini = m.Rank == Rank.MiniBoss;
                    t.name.text = m.DisplayName ?? "";
                    t.name.fontSize = mini ? 17f : 13f;
                    t.name.color = mini ? Palette.Gold : Color.white;
                    string affix = "";
                    for (int i = 0; i < m.Affixes.Count; i++)
                    {
                        if (i > 0) affix += "  ·  ";
                        affix += "<color=#" + ColorUtility.ToHtmlStringRGB(EnemyDef.AffixColor(m.Affixes[i])) + ">" + EnemyDef.AffixName(m.Affixes[i]) + "</color>";
                    }
                    t.affix.text = (mini ? "MINIBOSS" + (affix.Length > 0 ? "  ·  " : "") : "") + affix;
                    float w = Mathf.Max(t.name.preferredWidth, t.affix.preferredWidth) + 30f;
                    t.back.rectTransform.sizeDelta = new Vector2(w, mini ? 28f : 24f);
                    t.line.color = (m.Affixes.Count > 0 ? EnemyDef.AffixColor(m.Affixes[0]) : Palette.Gold).WithAlpha(0.8f);
                    t.line.rectTransform.sizeDelta = new Vector2(w * 0.8f, 2f);
                    break;
                }
            }
            foreach (var t in tags)
            {
                bool live = t.target != null && t.target.Alive && !paused;
                t.alpha = MathUtil.Damp(t.alpha, live ? 1f : 0f, 10f, dt);
                if (!live && t.alpha < 0.01f) { t.target = live ? t.target : null; t.rt.gameObject.SetActive(false); continue; }
                t.rt.gameObject.SetActive(true);
                if (t.target != null && t.target.Alive) t.rt.anchoredPosition = WorldToCanvas(new Vector2(t.target.Center.x, t.target.TopY + 0.35f));
                float a = t.alpha;
                t.name.alpha = a;
                t.affix.alpha = a;
                t.back.color = Palette.UiGlass.WithAlpha(0.7f * a);
                t.line.color = t.line.color.WithAlpha(0.8f * a);
            }
        }

        void UpdateBuild(float dt)
        {
            foreach (var b in buildIcons)
            {
                MathUtil.Spring(ref b.pop, ref b.popVel, 0f, 4f, 0.4f, dt);
                float s = 1f + b.pop * 0.05f;
                b.rt.localScale = new Vector3(s, s, 1f);
            }
        }

        void UpdateJuggle(float dt)
        {
            bool juggling = player.CurrentAction == Player.Action.Juggle && !player.JuggleDropped;
            jugEndT += dt;
            float targetA = juggling ? 1f : 1f - MathUtil.Smooth01((jugEndT - 0.55f) / 0.4f);
            jugGroup.alpha = MathUtil.Damp(jugGroup.alpha, targetA, 14f, dt);
            if (jugGroup.alpha < 0.002f && !juggling) return;

            Vector2 contactW = player.Pos + new Vector2(player.JuggleContactLocal.x * player.Facing, player.JuggleContactLocal.y);
            Vector2 c = WorldToCanvas(contactW);
            float ppu = (WorldToCanvas(contactW + Vector2.right) - c).x;
            float d = (Art.BallRadius * 2f + 0.14f) * ppu;

            // the approach ring meets the target ring exactly at the contact moment
            float ttc = player.JuggleTimeToContact, win = player.JuggleWindow;
            float k = Mathf.Clamp01(ttc / 0.5f);
            bool inWindow = juggling && Mathf.Abs(ttc) <= win;
            bool perfect = juggling && Mathf.Abs(ttc) <= win * 0.38f;
            Color ring = perfect ? Palette.Gold : inWindow ? Palette.ShotCyan : Color.white;

            jugTarget.rectTransform.anchoredPosition = c;
            jugTarget.rectTransform.sizeDelta = new Vector2(d, d);
            jugTarget.color = ring.WithAlpha(juggling ? (inWindow ? 0.95f : 0.3f) : 0f);
            float s = 1f + 1.7f * k;
            jugApproach.rectTransform.anchoredPosition = c;
            jugApproach.rectTransform.sizeDelta = new Vector2(d * s, d * s);
            jugApproach.color = ring.WithAlpha(juggling && ttc > -win ? Mathf.Lerp(0.9f, 0.12f, k) : 0f);
            jugGlow.rectTransform.anchoredPosition = c;
            jugGlow.rectTransform.sizeDelta = new Vector2(d * 2.4f, d * 2.4f);
            jugGlow.color = ring.WithAlpha(inWindow ? 0.22f : 0f);

            // streak counter floats behind the player's head, clear of the ball
            MathUtil.Spring(ref jugPop, ref jugPopVel, 0f, 4f, 0.4f, dt);
            Vector2 counter = WorldToCanvas(player.Pos + new Vector2(-player.Facing * 1.0f, 1.75f));
            float ps = 1f + Mathf.Max(-0.2f, jugPop) * 0.35f;
            jugCount.rectTransform.anchoredPosition = counter;
            jugCount.rectTransform.localScale = new Vector3(ps, ps, 1f);
            jugLabel.rectTransform.anchoredPosition = counter + new Vector2(0f, -32f);

            // judgement under the counter, away from the ball's flight
            jugJudgeT += dt;
            float rise = MathUtil.EaseOutCubic(Mathf.Clamp01(jugJudgeT / 0.35f));
            jugJudge.rectTransform.anchoredPosition = counter + new Vector2(0f, -66f + 10f * rise);
            jugJudge.alpha = rise * (1f - MathUtil.Smooth01((jugJudgeT - 0.45f) / 0.3f));
        }

        void UpdateNumbers(float dt)
        {
            for (int i = 0; i < numbers.Length; i++)
            {
                var n = numbers[i];
                if (n.age > 5f) continue;
                n.age += dt;
                const float life = 0.85f;
                if (n.age >= life) { n.age = 99f; n.rt.gameObject.SetActive(false); continue; }
                float t = n.age / life;
                float rise = MathUtil.EaseOutCubic(t) * (n.big ? 0.9f : 0.7f);
                Vector2 w = n.world + new Vector2(n.drift * t, rise);
                n.rt.anchoredPosition = WorldToCanvas(w);
                float pop = n.age < 0.14f ? MathUtil.EaseOutBack(n.age / 0.14f, 3f) : 1f;
                float s = pop * (n.big ? 1.15f : 1f) * (1f - 0.15f * t);
                n.rt.localScale = new Vector3(s, s, 1f);
                n.text.alpha = 1f - MathUtil.Smooth01((t - 0.55f) / 0.45f);
            }
        }

        static string PhaseName(RunDirector.Phase p)
        {
            switch (p)
            {
                case RunDirector.Phase.StageIntro: return "STAGE-INTRO";
                case RunDirector.Phase.WaveIntro: return "WELLEN-INTRO";
                case RunDirector.Phase.Fighting: return "KAMPF";
                case RunDirector.Phase.WaveCleared: return "WELLE GESCHAFFT";
                case RunDirector.Phase.Reward: return "KARTEN";
                case RunDirector.Phase.AbilityPick: return "FÄHIGKEIT";
                case RunDirector.Phase.BossIntro: return "BOSS-INTRO";
                case RunDirector.Phase.StageCleared: return "STAGE GESCHAFFT";
                case RunDirector.Phase.RunOver: return "LAUF VORBEI";
                default: return "LEERLAUF";
            }
        }

        /// <summary>Developer badge (always when a cheat touched the run) and the live numbers of the info toggle.</summary>
        void UpdateDev(float dt, RunState run)
        {
            devTimer -= dt;
            if (devTimer > 0f && dt > 0f) return;   // 5× per second while playing, every frame while paused
            devTimer = 0.2f;
            bool dev = DevMode.UsedThisRun || DevMode.AnyCheat;
            devBadge.text = dev ? DevMode.Badge() : "";
            devInfoBack.enabled = DevMode.ShowInfo;
            if (!DevMode.ShowInfo) { devInfo.text = ""; return; }
            var d = Game.I.Director;
            float l = run.Level;
            int wave = Mathf.Max(1, run.IsBossWave ? run.WavesInStage : run.Wave);
            devInfo.text =
                "STUFE " + l.ToString("0.00") + "     LEBEN ×" + Difficulty.HealthMul(l).ToString("0.00") + "     SCHADEN ×" + Difficulty.DamageMul(l).ToString("0.00")
                + "     TEMPO ×" + Difficulty.SpeedMul(l).ToString("0.00") + "\n"
                + PhaseName(d.P) + "     PLAN " + d.PlanIndex + " / " + d.PlanCount + "     FELD " + waves.AliveCount + " / " + Difficulty.MaxAlive(l)
                + "     ELITE " + Mathf.RoundToInt(Difficulty.EliteChance(run.Stage, wave, l) * 100f) + " %\n"
                + "BUDGET " + Difficulty.Budget(l).ToString("0.0") + "     SPAWN " + Difficulty.SpawnInterval(l).ToString("0.00") + " s     SIEGE " + run.Kills
                + "     ZEIT " + Clock(run.Time) + "     LEBEN " + Mathf.CeilToInt(player.Hp) + " / " + Mathf.RoundToInt(player.MaxHp);
        }

        static string Clock(float seconds)
        {
            int s = Mathf.FloorToInt(seconds);
            return (s / 60) + ":" + (s % 60).ToString("00");
        }

        void UpdateMisc(float dt, RunState run)
        {
            fpsFrames++;
            fpsTimer += dt;
            if (fpsTimer >= 0.5f)
            {
                float fps = fpsFrames / fpsTimer;
                fpsText.text = GameSettings.ShowFps ? Mathf.RoundToInt(fps) + " FPS" : "";
                fpsFrames = 0;
                fpsTimer = 0f;
            }

            toastT += dt;
            toast.alpha = toastT < 1.8f && !paused ? 1f - MathUtil.Smooth01((toastT - 1.3f) / 0.5f) : 0f;

            // a duo run is lost only when both players are down
            bool runLost = Coop.Active ? Game.I.Director.P == RunDirector.Phase.RunOver : player.Dead;
            float deathTarget = runLost && player.DeadTime > 0.9f ? 1f : 0f;
            deathGroup.alpha = MathUtil.Damp(deathGroup.alpha, deathTarget, 5f, dt);
            if (player.Dead && !deathTextSet)
            {
                deathTextSet = true;
                string wave = run.IsBossWave ? "BOSSKAMPF" : "WELLE " + Mathf.Max(1, run.Wave) + " / " + run.WavesInStage;
                deathSub.text = "STAGE " + run.Stage + "  ·  " + StageThemes.Title(run.Stage) + "  ·  " + wave;
                int ups = run.PickOrder.Count;
                deathStats.text = "GEGNER BESIEGT  " + run.Kills + "      ZEIT  " + Clock(run.Time) + "      UPGRADES  " + ups + "      <color=#FFCC5C>MÜNZEN  +" + Currencies.Format(CoinRewards.Earned) + "</color>";
                int best = RunState.BestStage;
                deathBest.text = DevMode.UsedThisRun ? "DEV-LAUF  ·  ZÄHLT NICHT FÜR DEN REKORD"
                    : run.Stage >= best ? "NEUER REKORD  ·  STAGE " + run.Stage : "BESTER LAUF  ·  STAGE " + best;
                for (int i = deathBuild.childCount - 1; i >= 0; i--) Object.Destroy(deathBuild.GetChild(i).gameObject);
                var order = new List<string>();
                foreach (var id in run.PickOrder) if (!order.Contains(id)) order.Add(id);
                int n = Mathf.Min(order.Count, 18);
                for (int i = 0; i < n; i++)
                {
                    var b = MakeBuildIcon(deathBuild, UpgradeDb.Get(order[i]));
                    int lvl = run.Stacks(order[i]);
                    b.count.text = lvl > 1 ? lvl.ToString() : "";
                    b.rt.anchoredPosition = new Vector2((i - (n - 1) * 0.5f) * 40f, 0f);
                }
            }
            else if (!player.Dead) deathTextSet = false;

            hintGroup.alpha = 1f - MathUtil.Smooth01((time - 9f) / 1.5f);
            UpdateDev(dt, run);

            UpdateCoop(dt);

            // fade in from black on start / restart (also hides first-frame shader warm-up)
            fadeT += dt;
            float fa = 1f - MathUtil.EaseOutQuad(fadeT / 0.8f);
            fade.color = new Color(0.01f, 0.02f, 0.05f, fa);
            fade.enabled = fa > 0.001f;
        }
    }
}
