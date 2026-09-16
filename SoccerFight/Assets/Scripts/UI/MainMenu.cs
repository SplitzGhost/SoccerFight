using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoccerFight
{
    /// <summary>
    /// Title screen. The arena keeps playing behind it (no monsters, the player idling), dimmed and
    /// vignetted, so the menu sits inside the game instead of in front of a still image.
    ///
    /// Buttons are not pressed with the pointer: the mouse is the game's crosshair and a click kicks
    /// a ball from the player's point of view into the screen — it starts big at the bottom edge,
    /// shrinks as it flies away and hits exactly where you aimed. A button only fires when the ball
    /// lands on it; the ball then rebounds towards the camera and falls out of frame under gravity
    /// while the page behind it changes. Shots into empty space do nothing but fly and fall.
    /// </summary>
    public sealed class MainMenu
    {
        const string Word = "SOCCERFIGHT";

        enum State { Menu, Starting, Quitting }

        /// <summary>A menu button. Hit detection runs against Home/Size, never through uGUI.</summary>
        sealed class Btn
        {
            public RectTransform Root;
            public Image Glow, Rim, Bg, Bar, Flash, Chevron;
            public Image[] Brackets;
            public TextMeshProUGUI Label;
            public Vector2 Home, Size;
            public Color Accent;
            public System.Action Action;
            public float Hover, HoverVel, Punch, PunchVel, Hit;
            public bool Primary;
        }

        /// <summary>One kicked ball: flies along its path, then falls freely once it has landed.</summary>
        sealed class Shot
        {
            public RectTransform Root, Spin;
            public Image Glow;
            public Image[] Ghosts;
            public Vector2 From, To, Pos, Vel;
            public float T, Dur, Scale, Angle, SpinRate;
            public Btn Target;
            public bool Live, Flying;
        }

        /// <summary>Pooled impact piece: flash, shockwave ring or spark.</summary>
        sealed class Bit
        {
            public RectTransform Rt;
            public Image Img;
            public Vector2 Pos, Vel, Size0, Size1;
            public Color Tint;
            public float Life, Age, Grav, Spin, Angle, Fade;
            public bool Align, Live;
        }

        Canvas canvas;
        Camera cam;
        RectTransform root, pageRoot, logoRoot, wordRoot, fxRoot, cursorRoot, logoSpin, shineMask;
        CanvasGroup rootGroup, menuGroup, pageGroup;
        SettingsPanel settings;
        Image logoBurst, curRing, curDot, curGlow;
        Image[] curBrackets;
        TextMeshProUGUI shine, bestLabel;
        readonly List<Btn> buttons = new List<Btn>();
        readonly List<Shot> shots = new List<Shot>();
        readonly List<Bit> bits = new List<Bit>();

        float openT, openVel, pageT, pageVel, time, shake, shakeVel;
        float curPunch, curPunchVel, lockT, lockVel, logoAngle, logoSpinRate = 34f, shineT;
        Vector2 aimLocal;
        bool onSettings;
        State state = State.Menu;
        float stateT;
        bool playFired;
        Btn hovered;

        public bool IsOpen { get; private set; }
        /// <summary>Fires when the play ball lands: the run starts while the menu fades out.</summary>
        public event System.Action PlayRequested;

        // ------------------------------------------------------------------ build

        public void Build(Transform parent, Camera camera, bool renderWithCamera)
        {
            cam = camera;
            UiArt.Build();
            MenuArt.Build();

            var go = new GameObject("Main Menu", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            canvas = go.AddComponent<Canvas>();
            if (renderWithCamera)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 0.85f;
                canvas.sortingOrder = 1200;
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 60;
            }
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            rootGroup = go.AddComponent<CanvasGroup>();
            root = (RectTransform)go.transform;

            // everything that fades when the menu opens or leaves; the ball layer above must not
            var menu = UiKit.Node("Menu", root, Vector2.zero, Vector2.zero);
            Stretch(menu);
            menuGroup = menu.gameObject.AddComponent<CanvasGroup>();

            BuildBackdrop(menu);
            pageRoot = UiKit.Node("Page", menu, Vector2.zero, Vector2.zero);
            pageGroup = pageRoot.gameObject.AddComponent<CanvasGroup>();
            BuildLogo(pageRoot);
            BuildButtons();
            BuildFooter(pageRoot);

            settings = new SettingsPanel();
            settings.Build(menu, true);
            settings.BackRequested += () => onSettings = false;

            fxRoot = UiKit.Node("Shots", root, Vector2.zero, Vector2.zero);
            BuildCursor();
            canvas.gameObject.SetActive(false);
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
        }

        void BuildBackdrop(RectTransform parent)
        {
            Stretch(UiKit.Img("Dim", parent, null, new Color(0.015f, 0.035f, 0.065f, 0.58f), Vector2.zero, Vector2.zero).rectTransform);
            Stretch(UiKit.Img("Vignette", parent, MenuArt.Vignette, new Color(0f, 0f, 0f, 0.9f), Vector2.zero, Vector2.zero).rectTransform);

            var brt = UiKit.Img("BottomFade", parent, MenuArt.FadeUp, new Color(0.01f, 0.025f, 0.05f, 0.85f), Vector2.zero, Vector2.zero).rectTransform;
            brt.anchorMin = new Vector2(0f, 0f);
            brt.anchorMax = new Vector2(1f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.anchoredPosition = Vector2.zero;
            brt.sizeDelta = new Vector2(0f, 320f);
        }

        void BuildLogo(RectTransform parent)
        {
            logoRoot = UiKit.Node("Logo", parent, new Vector2(0f, 238f), new Vector2(1600f, 320f));
            UiKit.Img("Aura", logoRoot, UiArt.Glow, Palette.ShotCyan.WithAlpha(0.13f), new Vector2(0f, -4f), new Vector2(1560f, 560f));
            UiKit.Img("AuraWarm", logoRoot, UiArt.Glow, Palette.Gold.WithAlpha(0.07f), new Vector2(180f, -30f), new Vector2(880f, 320f));

            UiKit.Label("Kicker", logoRoot, "ROGUELITE · FUSSBALL · MONSTER", 15f, Palette.ShotCyan.WithAlpha(0.8f),
                TextAlignmentOptions.Center, new Vector2(0f, 126f), new Vector2(900f, 24f), true, 13f);
            UiKit.Img("KickL", logoRoot, UiArt.LineFade, Palette.ShotCyan.WithAlpha(0.28f), new Vector2(-310f, 126f), new Vector2(260f, 2f));
            UiKit.Img("KickR", logoRoot, UiArt.LineFade, Palette.ShotCyan.WithAlpha(0.28f), new Vector2(310f, 126f), new Vector2(260f, 2f));

            // the word: five dark copies stacked down-right build the slab, the lit face sits on top
            const float fs = 132f, tr = 9f;
            wordRoot = UiKit.Node("Word", logoRoot, new Vector2(-54f, 4f), new Vector2(1500f, 210f));
            for (int i = 5; i >= 1; i--)
            {
                var d = UiKit.Label("Deep" + i, wordRoot, Word, fs, new Color(0.02f, 0.075f, 0.13f, 1f),
                    TextAlignmentOptions.Center, new Vector2(i * 1.7f, -i * 3.6f), new Vector2(1500f, 210f), true, tr);
                if (MenuArt.LogoDeep != null) d.fontSharedMaterial = MenuArt.LogoDeep;
            }
            var face = UiKit.Label("Face", wordRoot, Word, fs, Color.white, TextAlignmentOptions.Center, Vector2.zero, new Vector2(1500f, 210f), true, tr);
            if (MenuArt.LogoFace != null) face.fontSharedMaterial = MenuArt.LogoFace;
            face.enableVertexGradient = true;
            face.colorGradient = new VertexGradient(Color.white, Color.white, new Color(0.55f, 0.86f, 1f), new Color(0.55f, 0.86f, 1f));

            // gloss: a narrow window travels across a white copy of the word
            shineMask = UiKit.Node("ShineMask", wordRoot, new Vector2(-900f, 0f), new Vector2(200f, 230f));
            shineMask.gameObject.AddComponent<RectMask2D>();
            shine = UiKit.Label("Shine", shineMask, Word, fs, Color.white.WithAlpha(0f), TextAlignmentOptions.Center, Vector2.zero, new Vector2(1500f, 210f), true, tr);
            if (MenuArt.LogoFace != null) shine.fontSharedMaterial = MenuArt.LogoFace;

            // a ball has just come in from the upper right and smashed into the end of the word
            float half = face.GetPreferredValues(Word).x * 0.5f;
            var ballRoot = UiKit.Node("LogoBall", logoRoot, new Vector2(wordRoot.anchoredPosition.x + half + 36f, 54f), new Vector2(128f, 128f));
            for (int i = 0; i < 3; i++)
            {
                var line = UiKit.Img("Speed" + i, ballRoot, MenuArt.Spark, Palette.ShotCyan.WithAlpha(0.34f - i * 0.08f),
                    new Vector2(116f + i * 44f, 34f - i * 30f), new Vector2(132f - i * 26f, 14f));
                line.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 194f);
            }
            UiKit.Img("BallGlow", ballRoot, UiArt.Glow, Palette.ShotCyan.WithAlpha(0.3f), Vector2.zero, new Vector2(320f, 320f));
            logoBurst = UiKit.Img("Burst", ballRoot, MenuArt.Burst, Color.white.WithAlpha(0.2f), Vector2.zero, new Vector2(300f, 300f));
            logoSpin = UiKit.Node("Spin", ballRoot, Vector2.zero, new Vector2(124f, 124f));
            UiKit.Img("Pattern", logoSpin, Art.BallPattern, Color.white, Vector2.zero, new Vector2(124f, 124f));
            UiKit.Img("Shade", ballRoot, Art.BallShade, Color.white, Vector2.zero, new Vector2(124f, 124f));
            UiKit.Img("Hi", ballRoot, Art.BallHighlight, Color.white.WithAlpha(0.9f), Vector2.zero, new Vector2(124f, 124f));

            UiKit.Img("DivL", logoRoot, UiArt.LineFade, Color.white.WithAlpha(0.2f), new Vector2(-215f, -104f), new Vector2(320f, 2f));
            UiKit.Img("DivR", logoRoot, UiArt.LineFade, Color.white.WithAlpha(0.2f), new Vector2(215f, -104f), new Vector2(320f, 2f));
            UiKit.Img("Diamond", logoRoot, UiArt.Diamond, Palette.ShotCyan.WithAlpha(0.75f), new Vector2(0f, -104f), new Vector2(15f, 15f));
            UiKit.Label("Tagline", logoRoot, "SCHIESS DIR DEN WEG FREI", 17f, Palette.UiMuted, TextAlignmentOptions.Center,
                new Vector2(0f, -136f), new Vector2(900f, 26f), true, 11f);
        }

        void BuildButtons()
        {
            Vector2 size = new Vector2(430f, 78f);
            float y = -48f;
            MakeButton("SPIELEN", new Vector2(0f, y), size, Palette.ShotCyan, true, Play);
            MakeButton("EINSTELLUNGEN", new Vector2(0f, y -= 94f), size, Palette.ShotCyan, false, () => onSettings = true);
#if !UNITY_WEBGL || UNITY_EDITOR
            MakeButton("BEENDEN", new Vector2(0f, y -= 94f), size, Palette.Hurt, false, Quit);   // a browser tab can't be quit
#endif
        }

        void MakeButton(string text, Vector2 pos, Vector2 size, Color accent, bool primary, System.Action action)
        {
            var b = new Btn { Home = pos, Size = size, Action = action, Accent = accent, Primary = primary };
            b.Root = UiKit.Node(text, pageRoot, pos, size);
            b.Glow = UiKit.Img("Glow", b.Root, UiArt.Glow, accent.WithAlpha(0f), Vector2.zero, size + new Vector2(200f, 130f));
            b.Rim = UiKit.Img("Rim", b.Root, UiArt.Pill, Color.white.WithAlpha(0.16f), Vector2.zero, size + new Vector2(2f, 2f), Image.Type.Sliced);
            b.Bg = UiKit.Img("Bg", b.Root, UiArt.Pill, UiKit.ButtonBase, Vector2.zero, size, Image.Type.Sliced);
            UiKit.Img("Sheen", b.Bg.transform, UiArt.LineFade, Color.white.WithAlpha(0.1f), new Vector2(0f, size.y * 0.5f - 3f), new Vector2(size.x * 0.55f, 2f));
            b.Bar = UiKit.Img("Accent", b.Bg.transform, UiArt.Pill, accent, new Vector2(-size.x * 0.5f + 18f, 0f), new Vector2(6f, size.y * 0.34f), Image.Type.Sliced);
            b.Label = UiKit.Label("Label", b.Bg.transform, text, primary ? 25f : 21f, Palette.UiText, TextAlignmentOptions.Center, Vector2.zero, size, true, 9f);
            b.Chevron = UiKit.Img("Chevron", b.Bg.transform, MenuArt.Chevron, accent.WithAlpha(0.6f), new Vector2(size.x * 0.5f - 34f, 0f), new Vector2(26f, 26f));
            b.Flash = UiKit.Img("Flash", b.Root, UiArt.Pill, Color.white.WithAlpha(0f), Vector2.zero, size, Image.Type.Sliced);
            b.Brackets = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                b.Brackets[i] = UiKit.Img("Bracket" + i, b.Root, MenuArt.Bracket, accent.WithAlpha(0f), Vector2.zero, new Vector2(30f, 30f));
                b.Brackets[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, i * -90f);
            }
            buttons.Add(b);
        }

        void BuildFooter(RectTransform parent)
        {
            bestLabel = UiKit.Label("Best", parent, BestText(), 14f, Palette.UiMuted,
                TextAlignmentOptions.Left, new Vector2(-660f, -458f), new Vector2(560f, 22f), true, 6f);
            UiKit.Label("Hint", parent, "LINKSKLICK SCHIESST DEN BALL  ·  TRIFF EINEN KNOPF", 14f, Palette.ShotCyan.WithAlpha(0.75f),
                TextAlignmentOptions.Center, new Vector2(0f, -458f), new Vector2(900f, 22f), true, 8f);
            UiKit.Label("Keys", parent, "F1 FPS  ·  F2 VSYNC  ·  F3 DEVELOPER", 14f, Palette.UiMuted,
                TextAlignmentOptions.Right, new Vector2(660f, -458f), new Vector2(560f, 22f), true, 6f);
        }

        static string BestText()
        {
            int best = RunState.BestStage;
            return best > 0 ? "BESTE STAGE  " + best : "NOCH KEIN LAUF BEENDET";
        }

        void BuildCursor()
        {
            cursorRoot = UiKit.Node("Crosshair", root, Vector2.zero, new Vector2(64f, 64f));
            curGlow = UiKit.Img("Glow", cursorRoot, UiArt.Glow, Palette.ShotCyan.WithAlpha(0.14f), Vector2.zero, new Vector2(78f, 78f));
            curBrackets = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                curBrackets[i] = UiKit.Img("Bracket" + i, cursorRoot, MenuArt.Bracket, Color.white.WithAlpha(0f), Vector2.zero, new Vector2(26f, 26f));
                curBrackets[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, i * -90f);
            }
            curRing = UiKit.Img("Ring", cursorRoot, UiArt.RingThin, Color.white.WithAlpha(0.85f), Vector2.zero, new Vector2(26f, 26f));
            curDot = UiKit.Img("Dot", cursorRoot, UiArt.Circle, Color.white, Vector2.zero, new Vector2(5f, 5f));
        }

        // ------------------------------------------------------------------ open / close

        public void Open()
        {
            IsOpen = true;
            state = State.Menu;
            stateT = 0f;
            onSettings = false;
            pageT = pageVel = 0f;
            openT = openVel = 0f;
            hovered = null;
            playFired = false;
            logoSpinRate = 34f;
            if (bestLabel != null) bestLabel.text = BestText();
            foreach (var b in buttons) b.Hover = b.HoverVel = b.Punch = b.PunchVel = b.Hit = 0f;
            canvas.gameObject.SetActive(true);
        }

        void Play()
        {
            if (state != State.Menu) return;
            state = State.Starting;
            stateT = 0f;
            playFired = false;      // the run starts a beat later, once the impact has been seen
            IsOpen = false;
        }

        void Quit()
        {
            if (state != State.Menu) return;
            state = State.Quitting;
            stateT = 0f;
            IsOpen = false;
        }

        /// <summary>Esc: cancel a rebind or leave the settings page.</summary>
        public void HandleEscape()
        {
            if (settings.IsCapturing) settings.CancelCapture();
            else if (onSettings) onSettings = false;
        }

        // ------------------------------------------------------------------ update

        public void Update(float udt)
        {
            if (canvas == null || !canvas.gameObject.activeSelf) return;
            time += udt;
            stateT += udt;
            // opens with a soft rise, leaves quickly so the stage card behind it is not muddied
            MathUtil.Spring(ref openT, ref openVel, IsOpen ? 1f : 0f, IsOpen ? 3.4f : 5.5f, 0.9f, udt);
            MathUtil.Spring(ref pageT, ref pageVel, onSettings ? 1f : 0f, 3.6f, 0.95f, udt);
            MathUtil.Spring(ref shake, ref shakeVel, 0f, 7f, 0.35f, udt);

            if (IsOpen) Aim();
            settings.Update(udt);
            UpdateLayout();
            UpdateButtons(udt);
            UpdateLogo(udt);
            UpdateShots(udt);
            UpdateBits(udt);
            UpdateCursor(udt);

            // push the view aside (with a slow drift) so the player keeps the stage to themselves;
            // it glides back onto them as the menu leaves
            if (Game.I != null)
            {
                // released the moment play is hit, so the camera is back on the player when the run starts
                float framing = state == State.Starting ? 0f : Mathf.Clamp01(openT);
                Game.I.Cam.Offset = new Vector2(3.3f + Mathf.Sin(time * 0.13f) * 0.75f, 0.35f + Mathf.Sin(time * 0.09f) * 0.2f) * framing;
            }

            if (state == State.Starting && !playFired && stateT > 0.2f)
            {
                playFired = true;
                PlayRequested?.Invoke();
            }
            if (state == State.Quitting && stateT > 0.55f) QuitNow();
            if (!IsOpen && openT < 0.01f && !AnythingLive())
            {
                if (Game.I != null) Game.I.Cam.Offset = Vector2.zero;
                canvas.gameObject.SetActive(false);
            }
        }

        bool AnythingLive()
        {
            foreach (var s in shots) if (s.Live) return true;
            foreach (var b in bits) if (b.Live) return true;
            return false;
        }

        static void QuitNow()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>Pointer position in canvas space, hover state, and the click that kicks a ball.</summary>
        void Aim()
        {
            var uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cam;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(root, GameInput.AimScreen, uiCam, out var local))
                aimLocal = local;

            bool interactive = state == State.Menu && !onSettings && openT > 0.8f && pageT < 0.2f;
            hovered = interactive ? ButtonAt(aimLocal) : null;

            if (!GameInput.ClickPressed) return;
            // the settings card is an ordinary UI surface — only the open screen is a shooting range
            bool inPanel = pageT > 0.4f && Mathf.Abs(aimLocal.x) < SettingsPanel.Size.x * 0.5f + 12f
                                       && Mathf.Abs(aimLocal.y) < SettingsPanel.Size.y * 0.5f + 12f;
            if (!inPanel) Shoot(aimLocal, hovered);
        }

        /// <summary>Screen position of a button (0 = play) — the screenshot driver aims with these.</summary>
        public Vector2 ButtonScreen(int index)
        {
            var uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cam;
            if (index < 0 || index >= buttons.Count) return RectTransformUtility.WorldToScreenPoint(uiCam, root.position);
            return RectTransformUtility.WorldToScreenPoint(uiCam, buttons[index].Root.position);
        }

        /// <summary>Screen position of a point in canvas space (1920x1080, centre origin).</summary>
        public Vector2 ScreenOf(Vector2 canvasLocal)
        {
            var uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cam;
            return RectTransformUtility.WorldToScreenPoint(uiCam, root.TransformPoint(canvasLocal));
        }

        Btn ButtonAt(Vector2 local)
        {
            foreach (var b in buttons)
            {
                Vector2 d = local - b.Home;
                if (Mathf.Abs(d.x) <= b.Size.x * 0.5f + 8f && Mathf.Abs(d.y) <= b.Size.y * 0.5f + 8f) return b;
            }
            return null;
        }

        void UpdateLayout()
        {
            float o = Mathf.Clamp01(openT);
            float p = Mathf.Clamp01(pageT);
            rootGroup.blocksRaycasts = rootGroup.interactable = IsOpen;
            menuGroup.alpha = o;
            pageGroup.alpha = 1f - p;
            settings.Group.alpha = p;
            settings.Group.interactable = settings.Group.blocksRaycasts = p >= 0.5f && IsOpen;
            settings.Root.anchoredPosition = new Vector2(70f * (1f - p), 0f);
            float ss = Mathf.Lerp(0.95f, 1f, o);
            settings.Root.localScale = new Vector3(ss, ss, 1f);

            // menu-wide jolt from an impact, plus a slight parallax lean towards the pointer
            Vector2 lean = new Vector2(aimLocal.x * 0.012f, aimLocal.y * 0.008f) * o;
            Vector2 jolt = new Vector2(Mathf.Sin(time * 71f), Mathf.Cos(time * 63f)) * shake * 16f;
            pageRoot.anchoredPosition = new Vector2(-70f * p, 0f) + jolt + lean;

            logoRoot.anchoredPosition = new Vector2(0f, 238f + (1f - o) * 46f + Mathf.Sin(time * 0.9f) * 3.5f);
            float ls = Mathf.Lerp(0.94f, 1f, o) + (state == State.Starting ? Mathf.Min(0.1f, stateT * 0.22f) : 0f);
            logoRoot.localScale = new Vector3(ls, ls, 1f);
        }

        void UpdateButtons(float udt)
        {
            for (int i = 0; i < buttons.Count; i++)
            {
                var b = buttons[i];
                // staggered entrance: each button follows the one above it
                float delay = 0.13f * (i + 1);
                float o = Mathf.Clamp01((openT - delay) / Mathf.Max(0.05f, 1f - delay));
                MathUtil.Spring(ref b.Hover, ref b.HoverVel, hovered == b ? 1f : 0f, 5.5f, 0.85f, udt);
                MathUtil.Spring(ref b.Punch, ref b.PunchVel, 0f, 6f, 0.32f, udt);
                b.Hit = Mathf.Max(0f, b.Hit - udt * 3f);

                float h = Mathf.Clamp01(b.Hover);
                float leave = state == State.Starting ? Mathf.Clamp01(stateT * 2.6f) : 0f;
                float fade = o * (1f - leave);
                bool visible = fade > 0.002f;
                if (b.Root.gameObject.activeSelf != visible) b.Root.gameObject.SetActive(visible);
                if (!visible) continue;

                b.Root.anchoredPosition = b.Home + new Vector2(0f, (1f - o) * -34f - leave * 90f);
                b.Root.localScale = new Vector3(1f + h * 0.02f + b.Punch * 0.09f, 1f + h * 0.02f - b.Punch * 0.16f, 1f);
                b.Bg.color = Color.Lerp(b.Primary ? new Color(0.1f, 0.19f, 0.29f, 1f) : UiKit.ButtonBase, UiKit.ButtonHover, h).WithAlpha(fade * 0.96f);
                b.Rim.color = Color.Lerp(Color.white, b.Accent, h).WithAlpha(fade * Mathf.Lerp(b.Primary ? 0.3f : 0.16f, 0.85f, h));
                b.Glow.color = b.Accent.WithAlpha(fade * (0.04f + 0.2f * h + 0.55f * b.Hit));
                b.Bar.color = b.Accent.WithAlpha(fade * (b.Primary ? 1f : 0.4f + 0.6f * h));
                b.Bar.rectTransform.sizeDelta = new Vector2(6f, b.Size.y * (0.3f + 0.28f * h));
                b.Label.color = Color.Lerp(Palette.UiText, Color.Lerp(b.Accent, Color.white, 0.5f), h).WithAlpha(fade);
                b.Chevron.color = b.Accent.WithAlpha(fade * (b.Primary ? 0.55f + 0.45f * h : 0.3f + 0.5f * h));
                b.Chevron.rectTransform.anchoredPosition = new Vector2(b.Size.x * 0.5f - 34f + h * 6f, 0f);
                b.Flash.color = Color.white.WithAlpha(b.Hit * 0.55f * fade);

                float pad = Mathf.Lerp(20f, 4f, h) - b.Hit * 6f;
                for (int k = 0; k < 4; k++)
                {
                    float sx = k == 0 || k == 3 ? -1f : 1f;
                    float sy = k <= 1 ? 1f : -1f;
                    var rt = b.Brackets[k].rectTransform;
                    rt.anchoredPosition = new Vector2(sx * (b.Size.x * 0.5f + pad), sy * (b.Size.y * 0.5f + pad));
                    b.Brackets[k].color = b.Accent.WithAlpha(fade * (h * 0.9f + b.Hit * 0.6f));
                }
            }
        }

        void UpdateLogo(float udt)
        {
            logoSpinRate = Mathf.Lerp(logoSpinRate, 34f, udt * 1.2f);
            logoAngle -= logoSpinRate * udt;
            logoSpin.localRotation = Quaternion.Euler(0f, 0f, logoAngle);
            logoBurst.color = Color.white.WithAlpha(0.16f + 0.06f * Mathf.Sin(time * 2.3f) + Mathf.Clamp01((logoSpinRate - 34f) / 400f) * 0.4f);

            // gloss pass every few seconds
            shineT += udt;
            const float period = 5.5f, sweep = 0.75f;
            if (shineT > period) shineT -= period;
            if (shineT < sweep)
            {
                float u = shineT / sweep;
                float x = Mathf.Lerp(-860f, 860f, MathUtil.EaseInOutSine(u));
                shineMask.anchoredPosition = new Vector2(x, 0f);
                shine.rectTransform.anchoredPosition = new Vector2(-x, 0f);
                shine.color = Color.white.WithAlpha(0.5f * MathUtil.Bump(u));
            }
            else if (shine.color.a > 0f) shine.color = Color.white.WithAlpha(0f);
        }

        // ------------------------------------------------------------------ the ball

        void Shoot(Vector2 at, Btn target)
        {
            var s = TakeShot();
            s.Live = true;
            s.Flying = true;
            s.Target = target;
            s.T = 0f;
            // kicked from the player's point of view: just below the frame, roughly under the target
            s.From = new Vector2(at.x * 0.28f + Random.Range(-50f, 50f), -700f);
            s.To = at;
            s.Dur = 0.17f + Vector2.Distance(s.From, s.To) / 9000f;
            s.Scale = 1f;
            s.Pos = s.From;
            s.Vel = Vector2.zero;
            s.Angle = Random.Range(0f, 360f);
            s.SpinRate = Random.Range(950f, 1500f) * (at.x < s.From.x ? -1f : 1f);
            curPunch = 1f;
            if (Game.I != null) Game.I.Cam.AddTrauma(0.04f);
        }

        static Vector2 PathAt(Shot s, float e)
        {
            e = Mathf.Clamp01(e);
            Vector2 p = Vector2.Lerp(s.From, s.To, e);
            p.y += Mathf.Sin(e * Mathf.PI) * 34f;      // a kicked ball rises a little on its way out
            return p;
        }

        void UpdateShots(float udt)
        {
            foreach (var s in shots)
            {
                if (!s.Live) continue;
                if (s.Flying)
                {
                    s.T += udt;
                    float u = Mathf.Clamp01(s.T / s.Dur);
                    float e = MathUtil.EaseOutQuad(u);   // perspective: quick at first, slower far away
                    s.Pos = PathAt(s, e);
                    s.Scale = Mathf.Lerp(1f, 0.42f, e);
                    s.Angle += s.SpinRate * udt;
                    for (int k = 0; k < s.Ghosts.Length; k++)
                    {
                        float ge = e - (k + 1) * 0.055f;
                        var g = s.Ghosts[k];
                        if (ge <= 0f) { g.color = Color.white.WithAlpha(0f); continue; }
                        g.rectTransform.anchoredPosition = PathAt(s, ge);
                        float gs = Mathf.Lerp(1f, 0.42f, ge) * (1f - k * 0.13f);
                        g.rectTransform.localScale = new Vector3(gs, gs, 1f);
                        g.color = Color.Lerp(Color.white, Palette.ShotCyan, k * 0.22f).WithAlpha(0.3f * (1f - k / (float)s.Ghosts.Length));
                    }
                    if (u >= 1f) Land(s);
                }
                else
                {
                    // free fall back towards the camera and out of frame
                    s.Vel.y -= 2600f * udt;
                    s.Vel.x = Mathf.Lerp(s.Vel.x, 0f, udt * 0.9f);
                    s.Pos += s.Vel * udt;
                    s.Scale = Mathf.Lerp(s.Scale, 1.05f, udt * 2.2f);
                    s.SpinRate = Mathf.Lerp(s.SpinRate, 260f * Mathf.Sign(s.SpinRate), udt * 1.4f);
                    s.Angle += s.SpinRate * udt;
                    foreach (var g in s.Ghosts) if (g.color.a > 0f) g.color = g.color.WithAlpha(Mathf.Max(0f, g.color.a - udt * 4f));
                    if (s.Pos.y < -780f) { Retire(s); continue; }
                }

                s.Root.anchoredPosition = s.Pos;
                s.Root.localScale = new Vector3(s.Scale, s.Scale, 1f);
                s.Spin.localRotation = Quaternion.Euler(0f, 0f, s.Angle);
                s.Glow.color = Palette.ShotCyan.WithAlpha(s.Flying ? 0.34f : 0.1f);
            }
        }

        void Land(Shot s)
        {
            s.Flying = false;
            var target = s.Target;
            s.Target = null;
            bool hit = target != null;
            Vector2 at = s.To;
            Color tint = hit ? target.Accent : new Color(0.72f, 0.85f, 0.95f);

            // bounces back towards the camera: a short hop, then gravity takes it out of frame
            float away = at.x < s.From.x ? -1f : 1f;
            s.Vel = new Vector2(away * Random.Range(40f, 150f), Random.Range(300f, 430f));

            Bump(MenuArt.Burst, at, 70f, hit ? 460f : 260f, Color.white.WithAlpha(hit ? 0.95f : 0.45f), hit ? 0.3f : 0.2f, 2.2f, Random.Range(-70f, 70f));
            Bump(MenuArt.Shock, at, 60f, hit ? 430f : 250f, tint.WithAlpha(hit ? 0.8f : 0.35f), hit ? 0.45f : 0.3f, 1.6f, 0f);
            if (hit) Bump(MenuArt.Shock, at, 40f, 250f, Color.white.WithAlpha(0.5f), 0.26f, 1.4f, 0f);

            int n = hit ? 14 : 6;
            for (int i = 0; i < n; i++)
            {
                var b = Take();
                b.Img.sprite = MenuArt.Spark;
                b.Pos = at;
                b.Vel = MathUtil.Dir(Random.Range(0f, 360f)) * Random.Range(320f, hit ? 1000f : 520f);
                b.Size0 = new Vector2(Random.Range(26f, 48f), Random.Range(5f, 8f));
                b.Size1 = b.Size0 * 0.3f;
                b.Tint = i % 3 == 0 ? Color.white : tint;
                b.Life = Random.Range(0.3f, 0.6f);
                b.Age = 0f;
                b.Grav = 1500f;
                b.Fade = 1.3f;
                b.Align = true;
                b.Spin = 0f;
            }

            if (hit)
            {
                target.PunchVel += 15f;
                target.Hit = 1f;
                logoSpinRate = 430f;
                shake = 1f;
                shakeVel = 0f;
                if (Game.I != null) Game.I.Cam.AddTrauma(0.16f);
                target.Action?.Invoke();
            }
            else
            {
                shake = 0.3f;
                if (Game.I != null) Game.I.Cam.AddTrauma(0.05f);
            }
        }

        void Bump(Sprite sprite, Vector2 at, float from, float to, Color tint, float life, float fade, float spin)
        {
            var b = Take();
            b.Img.sprite = sprite;
            b.Pos = at;
            b.Vel = Vector2.zero;
            b.Size0 = new Vector2(from, from);
            b.Size1 = new Vector2(to, to);
            b.Tint = tint;
            b.Life = life;
            b.Age = 0f;
            b.Grav = 0f;
            b.Fade = fade;
            b.Align = false;
            b.Spin = spin;
            b.Angle = Random.Range(0f, 90f);
        }

        void UpdateBits(float udt)
        {
            foreach (var b in bits)
            {
                if (!b.Live) continue;
                b.Age += udt;
                float u = b.Age / b.Life;
                if (u >= 1f)
                {
                    b.Live = false;
                    b.Rt.gameObject.SetActive(false);
                    continue;
                }
                b.Vel.y -= b.Grav * udt;
                b.Pos += b.Vel * udt;
                b.Rt.anchoredPosition = b.Pos;
                b.Rt.sizeDelta = Vector2.Lerp(b.Size0, b.Size1, MathUtil.EaseOutCubic(u));
                b.Angle = b.Align ? MathUtil.Angle(b.Vel) + 180f : b.Angle + b.Spin * udt;
                b.Rt.localRotation = Quaternion.Euler(0f, 0f, b.Angle);
                b.Img.color = b.Tint.WithAlpha(b.Tint.a * Mathf.Pow(1f - u, b.Fade));
            }
        }

        void UpdateCursor(float udt)
        {
            bool show = IsOpen && openT > 0.15f;
            if (cursorRoot.gameObject.activeSelf != show) cursorRoot.gameObject.SetActive(show);
            if (!show) return;

            MathUtil.Spring(ref curPunch, ref curPunchVel, 0f, 5f, 0.4f, udt);
            MathUtil.Spring(ref lockT, ref lockVel, hovered != null ? 1f : 0f, 6f, 0.8f, udt);
            cursorRoot.anchoredPosition = aimLocal;
            float s = 1f + curPunch * 0.45f;
            cursorRoot.localScale = new Vector3(s, s, 1f);

            Color accent = hovered != null ? Color.Lerp(Palette.ShotCyan, Palette.Gold, 0.85f) : Palette.ShotCyan;
            curRing.color = Color.Lerp(Color.white.WithAlpha(0.85f), accent, lockT * 0.8f);
            curDot.color = Color.Lerp(Color.white, accent, lockT * 0.6f);
            curGlow.color = accent.WithAlpha(0.12f + 0.16f * lockT + 0.25f * curPunch);
            float pad = Mathf.Lerp(22f, 12f, lockT);
            float bs = Mathf.Lerp(0.7f, 1f, lockT);
            for (int k = 0; k < 4; k++)
            {
                float sx = k == 0 || k == 3 ? -1f : 1f;
                float sy = k <= 1 ? 1f : -1f;
                var rt = curBrackets[k].rectTransform;
                rt.anchoredPosition = new Vector2(sx * pad, sy * pad);
                rt.localScale = new Vector3(bs, bs, 1f);
                curBrackets[k].color = accent.WithAlpha(lockT * 0.9f);
            }
        }

        // ------------------------------------------------------------------ pools

        Shot TakeShot()
        {
            foreach (var s in shots) if (!s.Live) return s;
            // the tail is soft light, not copies of the ball: reads as motion blur, never as a chain
            var n = new Shot { Ghosts = new Image[5] };
            for (int i = n.Ghosts.Length - 1; i >= 0; i--)
                n.Ghosts[i] = UiKit.Img("Trail", fxRoot, UiArt.Glow, Color.white.WithAlpha(0f), Vector2.zero, new Vector2(150f, 150f));
            n.Root = UiKit.Node("Shot", fxRoot, Vector2.zero, new Vector2(118f, 118f));
            n.Glow = UiKit.Img("Glow", n.Root, UiArt.Glow, Palette.ShotCyan.WithAlpha(0.34f), Vector2.zero, new Vector2(290f, 290f));
            n.Spin = UiKit.Node("Spin", n.Root, Vector2.zero, new Vector2(118f, 118f));
            UiKit.Img("Pattern", n.Spin, Art.BallPattern, Color.white, Vector2.zero, new Vector2(118f, 118f));
            UiKit.Img("Shade", n.Root, Art.BallShade, Color.white, Vector2.zero, new Vector2(118f, 118f));
            UiKit.Img("Hi", n.Root, Art.BallHighlight, Color.white.WithAlpha(0.9f), Vector2.zero, new Vector2(118f, 118f));
            shots.Add(n);
            return n;
        }

        void Retire(Shot s)
        {
            s.Live = false;
            s.Root.anchoredPosition = new Vector2(0f, -3000f);
            foreach (var g in s.Ghosts) g.color = Color.white.WithAlpha(0f);
        }

        Bit Take()
        {
            foreach (var b in bits)
                if (!b.Live) { b.Live = true; b.Rt.gameObject.SetActive(true); return b; }
            var n = new Bit();
            n.Img = UiKit.Img("Bit", fxRoot, MenuArt.Spark, Color.white, Vector2.zero, new Vector2(32f, 8f));
            n.Rt = n.Img.rectTransform;
            n.Live = true;
            bits.Add(n);
            return n;
        }
    }
}
