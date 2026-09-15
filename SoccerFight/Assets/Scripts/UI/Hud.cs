using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoccerFight
{
    /// <summary>
    /// Clean HUD: health bar with delayed damage ghost (top-left), two ability slots with radial
    /// cooldown sweeps (bottom-right), crosshair with shot-cooldown arc, wave banner, damage numbers.
    /// All animation runs on unscaled time so it stays smooth during hit-stop and slow motion.
    /// </summary>
    public sealed class Hud
    {
        sealed class Slot
        {
            public RectTransform root, iconRt;
            public Image icon, overlay, ring, dot, flashRing, glow;
            public TextMeshProUGUI timer;
            public int shownTenths = -1;
            public float size;
            public float prevRemaining;
            public float pop, popVel;
            public float flashT = 99f;
            public Color accent;
            public GameAction action;
            public Image badge, badgeRim, mouseIcon;
            public TextMeshProUGUI keyText;
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

        Canvas canvas;
        RectTransform canvasRect;
        Camera cam;
        Player player;
        WaveDirector waves;

        // health
        RectTransform hpGroup;
        Image hpFill, hpGhost, hpGlow, emblemRing, emblemGlow;
        RectTransform hpFillRt, hpGhostRt;
        TextMeshProUGUI hpText;
        float hpDisplay = 1f, hpGhostV = 1f, ghostDelay, hpShake;
        int shownHp = -1;
        const float BarW = 300f, BarH = 26f, BarInset = 5f;

        Slot shotSlot, flickSlot, powerSlot, stepSlot, bikeSlot;
        Slot[] slots;

        RectTransform cross;
        Image crossArc;
        float crossPunch, crossPunchVel;

        TextMeshProUGUI waveText, enemiesText;
        RectTransform banner;
        CanvasGroup bannerGroup;
        TextMeshProUGUI bannerText, bannerSub;
        RectTransform bannerLineL, bannerLineR;
        float bannerT = 99f;
        int shownEnemies = -1;
        int shownWave = -1;
        bool deathTextSet;
        Image fade;
        float fadeT;

        TextMeshProUGUI fpsText, hintText;
        bool paused;
        float fpsTimer;
        int fpsFrames;

        TextMeshProUGUI toast;
        float toastT = 99f;

        Number[] numbers;
        int numberCursor;

        CanvasGroup deathGroup;
        TextMeshProUGUI deathSub;

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

            BuildHealth();
            // skill bar, right to left: rainbow flick (the big one), shot, power shot, step-over, bicycle kick
            flickSlot = BuildSlot("Flick", new Vector2(-82f, 92f), 104f, UiArt.IconFlick, UiArt.RingRainbow, Color.white, GameAction.Flick);
            shotSlot = BuildSlot("Shot", new Vector2(-198f, 84f), 84f, UiArt.IconShot, UiArt.RingThick, Palette.ShotCyan, GameAction.Shoot);
            powerSlot = BuildSlot("Power", new Vector2(-296f, 82f), 78f, UiArt.IconPower, UiArt.RingThick, Palette.PowerGold, GameAction.PowerShot);
            stepSlot = BuildSlot("StepOver", new Vector2(-388f, 82f), 78f, UiArt.IconStepOver, UiArt.RingThick, Palette.DashMint, GameAction.StepOver);
            bikeSlot = BuildSlot("Bicycle", new Vector2(-480f, 82f), 78f, UiArt.IconBicycle, UiArt.RingThick, Palette.BlastOrange, GameAction.Bicycle);
            slots = new[] { shotSlot, powerSlot, stepSlot, bikeSlot, flickSlot };
            BuildCrosshair();
            BuildWave();
            BuildJuggle();
            BuildMisc();
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
            Img("EmblemBase", hpGroup, UiArt.Circle, glass, new Vector2(38f, -38f), new Vector2(76f, 76f));
            emblemRing = Img("EmblemRing", hpGroup, UiArt.RingThick, Palette.HpA, new Vector2(38f, -38f), new Vector2(76f, 76f));
            Img("EmblemRim", hpGroup, UiArt.RingThin, Color.white.WithAlpha(0.18f), new Vector2(38f, -38f), new Vector2(92f, 92f));
            Img("Heart", hpGroup, UiArt.Heart, Color.white, new Vector2(38f, -37f), new Vector2(32f, 32f));

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
        }

        Slot BuildSlot(string name, Vector2 pos, float size, Sprite icon, Sprite ringSprite, Color accent, GameAction action)
        {
            var s = new Slot { size = size, accent = accent };
            s.root = Node(name, canvasRect, new Vector2(1f, 0f), pos, new Vector2(size, size));
            Color glass = Palette.UiGlass.WithAlpha(0.88f);
            s.glow = Img("Glow", s.root, UiArt.Glow, accent.WithAlpha(0.0f), Vector2.zero, Vector2.one * size * 2f);
            Img("Shadow", s.root, UiArt.Glow, new Color(0f, 0f, 0.02f, 0.45f), new Vector2(0f, -4f), Vector2.one * size * 1.45f);
            Img("Base", s.root, UiArt.Circle, glass, Vector2.zero, Vector2.one * size);
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
                bool mouse = b.Mouse && b.Button <= 1;   // left or right button: draw the mouse
                s.mouseIcon.gameObject.SetActive(mouse);
                s.mouseIcon.sprite = b.Button == 1 ? UiArt.IconMouseRight : UiArt.IconMouse;
                s.keyText.gameObject.SetActive(!mouse);
                string label = KeyBindings.ShortName(s.action);
                s.keyText.text = label;
                float w = mouse ? 34f : Mathf.Max(30f, 16f + label.Length * 11f);
                s.badge.rectTransform.sizeDelta = new Vector2(w, 26f);
                s.badgeRim.rectTransform.sizeDelta = new Vector2(w + 2f, 28f);
            }
            if (hintText != null)
                hintText.text = KeyBindings.DisplayName(GameAction.Left) + " / " + KeyBindings.DisplayName(GameAction.Right) + "  LAUFEN    "
                    + KeyBindings.DisplayName(GameAction.Jump) + "  SPRINGEN    " + KeyBindings.DisplayName(GameAction.Down) + "  RUNTER    "
                    + KeyBindings.DisplayName(GameAction.Shoot) + "  SCHUSS    " + KeyBindings.DisplayName(GameAction.Juggle) + "  HOCHHALTEN    ESC  PAUSE\n"
                    + KeyBindings.DisplayName(GameAction.PowerShot) + "  POWER-SCHUSS    " + KeyBindings.DisplayName(GameAction.StepOver) + "  ÜBERSTEIGER    "
                    + KeyBindings.DisplayName(GameAction.Bicycle) + "  FALLRÜCKZIEHER (IN DER LUFT)    " + KeyBindings.DisplayName(GameAction.Flick) + "  RAINBOW FLICK";
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
            var g = Node("Wave", canvasRect, new Vector2(0.5f, 1f), new Vector2(0f, -40f), Vector2.zero);
            waveText = Text("WaveText", g, "", 22f, Palette.UiText, TextAlignmentOptions.Center, Vector2.zero, new Vector2(400f, 30f), true, true, 10f);
            var line = Img("Line", g, UiArt.LineFade, Color.white.WithAlpha(0.25f), new Vector2(0f, -20f), new Vector2(220f, 2f));
            enemiesText = Text("Enemies", g, "", 14f, Palette.UiMuted, TextAlignmentOptions.Center, new Vector2(0f, -38f), new Vector2(400f, 20f), true, true, 4f);

            banner = Node("Banner", canvasRect, new Vector2(0.5f, 0.5f), new Vector2(0f, 190f), new Vector2(900f, 160f));
            bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.alpha = 0f;
            bannerText = Text("Title", banner, "", 78f, Color.white, TextAlignmentOptions.Center, new Vector2(0f, 10f), new Vector2(900f, 100f), true, true, 18f);
            bannerSub = Text("Sub", banner, "", 20f, Palette.UiMuted, TextAlignmentOptions.Center, new Vector2(0f, -52f), new Vector2(900f, 30f), false, true, 6f);
            bannerLineL = Img("LineL", banner, UiArt.LineFade, Color.white.WithAlpha(0.5f), new Vector2(-300f, 10f), new Vector2(200f, 2f)).rectTransform;
            bannerLineR = Img("LineR", banner, UiArt.LineFade, Color.white.WithAlpha(0.5f), new Vector2(300f, 10f), new Vector2(200f, 2f)).rectTransform;
        }

        void BuildMisc()
        {
            fpsText = Text("FPS", canvasRect, "", 15f, Palette.UiMuted, TextAlignmentOptions.Right, Vector2.zero, new Vector2(200f, 22f), true, true, 2f);
            var fr = fpsText.rectTransform;
            fr.anchorMin = fr.anchorMax = new Vector2(1f, 1f);
            fr.anchoredPosition = new Vector2(-124f, -30f);

            toast = Text("Toast", canvasRect, "", 18f, Palette.UiText, TextAlignmentOptions.Center, new Vector2(0f, 300f), new Vector2(600f, 30f), true, true, 4f);
            toast.alpha = 0f;

            var numRoot = Node("DamageNumbers", canvasRect, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            numbers = new Number[24];
            for (int i = 0; i < numbers.Length; i++)
            {
                var t = Text("Num", numRoot, "", 34f, Color.white, TextAlignmentOptions.Center, Vector2.zero, new Vector2(160f, 60f));
                t.gameObject.SetActive(false);
                numbers[i] = new Number { rt = t.rectTransform, text = t };
            }

            var death = Node("Death", canvasRect, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            death.anchorMin = Vector2.zero; death.anchorMax = Vector2.one; death.sizeDelta = Vector2.zero;
            deathGroup = death.gameObject.AddComponent<CanvasGroup>();
            deathGroup.alpha = 0f;
            var dim = Img("Dim", death, UiArt.Circle, new Color(0.01f, 0.02f, 0.05f, 0.6f), Vector2.zero, Vector2.zero);
            dim.sprite = null;
            dim.rectTransform.anchorMin = Vector2.zero; dim.rectTransform.anchorMax = Vector2.one; dim.rectTransform.sizeDelta = Vector2.zero;
            Text("Title", death, "BESIEGT", 84f, Color.white, TextAlignmentOptions.Center, new Vector2(0f, 40f), new Vector2(900f, 110f), true, true, 20f);
            deathSub = Text("Sub", death, "", 22f, Palette.UiMuted, TextAlignmentOptions.Center, new Vector2(0f, -30f), new Vector2(900f, 30f), false, true, 4f);
            Text("Restart", death, "[ ENTER ]  NEUSTART", 18f, Palette.ShotCyan, TextAlignmentOptions.Center, new Vector2(0f, -86f), new Vector2(900f, 30f), true, true, 6f);

            // two lines (moves, then skills), shifted left so they clear the skill bar
            var hint = Node("Hint", canvasRect, new Vector2(0.5f, 0f), new Vector2(-150f, 58f), new Vector2(1300f, 56f));
            hintGroup = hint.gameObject.AddComponent<CanvasGroup>();
            hintText = Text("HintText", hint, "", 14f, Palette.UiMuted, TextAlignmentOptions.Center, Vector2.zero, new Vector2(1300f, 56f), true, true, 2.5f);
            RefreshBindings();
            KeyBindings.Changed += RefreshBindings;

            fade = Img("Fade", canvasRect, null, new Color(0.01f, 0.02f, 0.05f, 1f), Vector2.zero, Vector2.zero);
            fade.rectTransform.anchorMin = Vector2.zero;
            fade.rectTransform.anchorMax = Vector2.one;
            fade.rectTransform.sizeDelta = Vector2.zero;
        }

        // ------------------------------------------------------------------ events

        public void OnShotUsed() { shotSlot.popVel -= 6f; crossPunchVel += 9f; }
        public void OnFlickUsed() { flickSlot.popVel -= 7f; }

        public void OnSkillUsed(GameAction action)
        {
            foreach (var s in slots) if (s.action == action) s.popVel -= 7f;
            if (action == GameAction.PowerShot) crossPunchVel += 12f;
        }

        public void OnPlayerDamaged(float amount)
        {
            ghostDelay = 0.45f;
            hpShake = 1f;
        }

        public void OnMonsterKilled() { }

        public void ShowWaveBanner(int wave)
        {
            bannerT = 0f;
            bannerText.text = "WELLE " + wave;
            bannerSub.text = wave == 1 ? "DIE MONSTER KOMMEN" : "SIE WERDEN STÄRKER";
        }

        public void OnWaveCleared(int wave)
        {
            bannerT = 0f;
            bannerText.text = "GESCHAFFT";
            bannerSub.text = "WELLE " + wave + " ÜBERSTANDEN";
        }

        public void ShowToast(string text) { toast.text = text; toastT = 0f; }

        public void SetVisible(bool visible) => canvas.enabled = visible;

        public void DamageNumber(Vector2 world, float amount, bool big)
            => Popup(world, Mathf.RoundToInt(amount).ToString(), big ? Palette.Gold : Color.white, big ? 46f : 32f, big);

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
            shownHp = -1;
            bannerT = 99f;
            fadeT = 0f;
            deathGroup.alpha = 0f;
            foreach (var n in numbers) { n.age = 99f; n.rt.gameObject.SetActive(false); }
            jugEndT = 99f;
            jugGroup.alpha = 0f;
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

            UpdateHealth(dt);
            bool juggling = player.CurrentAction == Player.Action.Juggle;
            bool withBall = player.Ball.IsHeld && !player.Dead && !juggling;
            UpdateSlot(shotSlot, player.ShotCd, Player.ShotCooldown, withBall, dt, false);
            UpdateSlot(powerSlot, player.PowerCd, Player.PowerCooldown, withBall && player.Grounded, dt, false);
            UpdateSlot(stepSlot, player.StepOverCd, Player.StepOverCooldown, player.Grounded && !player.Dead && !juggling, dt, false);
            UpdateSlot(bikeSlot, player.BicycleCd, Player.BicycleCooldown, withBall && !player.Grounded, dt, false);
            UpdateSlot(flickSlot, player.FlickCd, Player.FlickCooldown, withBall && player.Grounded, dt, true);
            UpdateCrosshair(dt);
            UpdateWave(dt);
            UpdateJuggle(dt);
            UpdateNumbers(dt);
            UpdateMisc(dt);
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

            int hp = Mathf.CeilToInt(player.Hp);
            if (hp != shownHp)
            {
                shownHp = hp;
                hpText.text = hp + "<size=70%><color=#8FA3B8> / " + Mathf.RoundToInt(player.MaxHp) + "</color></size>";
            }
        }

        void UpdateSlot(Slot s, float remaining, float total, bool available, float dt, bool rainbow)
        {
            float frac = Mathf.Clamp01(remaining / total);
            s.overlay.fillAmount = frac;
            s.ring.fillAmount = 1f - frac;

            bool ready = remaining <= 0f;
            if (ready && s.prevRemaining > 0f)
            {
                s.flashT = 0f;
                s.popVel += 9f;
            }
            s.prevRemaining = remaining;

            MathUtil.Spring(ref s.pop, ref s.popVel, 0f, 3.2f, 0.35f, dt);
            float scale = 1f + s.pop * 0.05f;
            s.root.localScale = new Vector3(scale, scale, 1f);

            // leading edge dot of the charge ring
            if (!ready)
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

            s.flashT += dt;
            float ft = s.flashT / 0.45f;
            if (ft < 1f)
            {
                float fs = Mathf.Lerp(1f, 1.55f, MathUtil.EaseOutCubic(ft));
                s.flashRing.rectTransform.localScale = new Vector3(fs, fs, 1f);
                s.flashRing.color = (rainbow ? Color.white : s.accent).WithAlpha(0.8f * (1f - ft));
            }
            else s.flashRing.color = Color.clear;

            float iconAlpha = available ? 1f : 0.42f;
            s.icon.color = new Color(1f, 1f, 1f, MathUtil.Damp(s.icon.color.a, ready ? iconAlpha : iconAlpha * 0.6f, 12f, dt));

            // only touch the text when the displayed value changes (no per-frame string garbage)
            int tenths = !ready && total > 1f ? Mathf.CeilToInt(remaining * 10f) : -1;
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
            float frac = Mathf.Clamp01(player.ShotCd / Player.ShotCooldown);
            crossArc.fillAmount = frac > 0f ? 1f - frac : 0f;
            crossArc.color = Palette.ShotCyan.WithAlpha(frac > 0f ? 0.9f : 0f);
        }

        void UpdateWave(float dt)
        {
            if (waves.Wave != shownWave)
            {
                shownWave = waves.Wave;
                shownEnemies = -1;
                waveText.text = shownWave > 0 ? "WELLE " + shownWave : "BEREIT MACHEN";
                if (shownWave == 0) enemiesText.text = "";
            }
            if (shownWave > 0)
            {
                int remaining = waves.RemainingInWave;
                if (remaining != shownEnemies)
                {
                    shownEnemies = remaining;
                    enemiesText.text = remaining > 0 ? remaining + " GEGNER" : "PAUSE";
                }
            }

            bannerT += dt;
            const float dur = 2.3f;
            if (bannerT < dur)
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
                bannerLineL.anchoredPosition = new Vector2(-250f - lw * 0.5f, 10f);
                bannerLineR.anchoredPosition = new Vector2(250f + lw * 0.5f, 10f);
            }
            else bannerGroup.alpha = 0f;
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

        void UpdateMisc(float dt)
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
            toast.alpha = toastT < 1.6f ? 1f - MathUtil.Smooth01((toastT - 1.1f) / 0.5f) : 0f;

            float deathTarget = player.Dead && player.DeadTime > 0.9f ? 1f : 0f;
            deathGroup.alpha = MathUtil.Damp(deathGroup.alpha, deathTarget, 5f, dt);
            if (player.Dead && !deathTextSet)
            {
                deathTextSet = true;
                int survived = Mathf.Max(0, waves.Wave - 1);
                deathSub.text = "DU HAST " + survived + (survived == 1 ? " WELLE" : " WELLEN") + " ÜBERLEBT";
            }
            else if (!player.Dead) deathTextSet = false;

            hintGroup.alpha = 1f - MathUtil.Smooth01((time - 7f) / 1.5f);

            // fade in from black on start / restart (also hides first-frame shader warm-up)
            fadeT += dt;
            float fa = 1f - MathUtil.EaseOutQuad(fadeT / 0.8f);
            fade.color = new Color(0.01f, 0.02f, 0.05f, fa);
            fade.enabled = fa > 0.001f;
        }
    }
}
