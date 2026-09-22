using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoccerFight
{
    /// <summary>
    /// Title screen. Its backdrop is a place in the game world (MenuVista, filmed by the game camera
    /// with the game's own look), the stone logo sits on top, the chosen player juggles on the
    /// floating centre circle in the middle (shoot it to change player), shop / ranking / friends on
    /// the left, events / settings / info on the right, and a big SPIELEN button underneath. The UI
    /// speaks the language of the in-game cards and HUD: dark glass, hairline frames, accent light.
    ///
    /// Buttons are not pressed with the pointer: the mouse is the game's crosshair and a click kicks
    /// a ball from the player's point of view into the screen — it starts big at the bottom edge,
    /// shrinks as it flies away and hits exactly where you aimed. A button only fires when the ball
    /// lands on it; the ball then rebounds towards the camera and falls out of frame under gravity.
    /// Shots into empty space do nothing but fly and fall.
    ///
    /// SPIELEN: the menu clears away, the player on the pedestal kicks the ball straight at the
    /// camera, it fills the screen, and the flash behind it opens onto the arena.
    /// </summary>
    public sealed class MainMenu
    {
        enum State { Menu, Starting, Quitting }

        /// <summary>One kicked ball: flies along its path, then falls freely once it has landed.</summary>
        sealed class Shot
        {
            public RectTransform Root, Spin;
            public Image Glow;
            public Image[] Ghosts;
            public Vector2 From, To, Pos, Vel;
            public float T, Dur, Scale, Angle, SpinRate;
            public MenuTarget Target;
            public bool Live, Flying;
        }

        /// <summary>Pooled particle: flash, shockwave ring, spark or sparkle.</summary>
        sealed class Bit
        {
            public RectTransform Rt;
            public Image Img;
            public Vector2 Pos, Vel, Size0, Size1;
            public Color Tint;
            public float Life, Age, Grav, Spin, Angle, Fade;
            public bool Align, Live, Bump;
        }

        /// <summary>A main-page element that flies in when the menu opens and out when a page or the run takes over.</summary>
        sealed class Mover
        {
            public RectTransform Rt;
            public Vector2 Out;
            public float Delay;
            public CanvasGroup Group;
            public MenuTarget Target;
        }

        Canvas canvas;
        Camera cam;
        RectTransform root, content, main, stack, pagesRoot, fxRoot, cursorRoot;
        CanvasGroup contentGroup, mainGroup;
        MenuVista vista;
        Image veil;
        MenuFigure figure;
        CharacterPage characters;
        SkillPage skills;
        ShopPage shop;
        OnboardingPages onboarding;
        MenuNav nav;
        TextMeshProUGUI coinAmount, gemAmount;
        RectTransform coinPill;
        float coinShown = -1f, coinPop, coinPopVel;
        MenuPages pages;
        readonly SubPage[] subPages = new SubPage[MenuPage.Count];
        readonly List<MenuTarget> targets = new List<MenuTarget>();
        readonly List<Shot> shots = new List<Shot>();
        readonly List<Bit> bits = new List<Bit>();
        readonly List<Mover> movers = new List<Mover>();
        readonly List<RectTransform> stickers = new List<RectTransform>();

        // main page
        RectTransform logoRoot, logoBall, logoSpin, logoShine, leftCol, rightCol, topLeft, topRight, tagRoot;
        Image logoShineImg, pedestalGlow, avatarHead, avatarBody, avatarRing, tagBadge, tagIcon, playHalo;
        Image[] logoSparkles;
        Image toastShadow, toastFrame, toastLight;
        TextMeshProUGUI profileName, profileBest, tagName, tagRole;
        ChunkButton play;
        MenuTarget figureTarget, tagTarget, profileTarget;
        const float FeetY = -250f, FigureScale = 190f, LogoUnit = 125f, LogoY = 330f;

        // cursor
        Image curRing, curDot, curGlow;
        Image[] curBrackets;

        // play transition
        RectTransform hero, heroSpin;
        CanvasGroup heroGroup;
        Image heroGlow, flash;
        float heroT = -1f, flashA, heroA;
        Image[] speedLines;
        Vector2 heroFrom;

        // "coming soon" note
        TextMeshProUGUI toast;
        RectTransform toastRoot;
        Image toastPlate;
        CanvasGroup toastGroup;
        float toastT = 99f;
        Vector2 toastPos;

        float openT, openVel, time, stateT, shake, shakeVel, curPunch, curPunchVel, lockT, lockVel, logoAngle, logoSpinRate = 60f, shineT, swapFlash;
        Vector2 aimLocal;
        int page = MenuPage.Main;
        int shownCharacter = -1;
        State state = State.Menu;
        bool playFired, culled;
        int savedMask;
        MenuTarget hovered;

        public bool IsOpen { get; private set; }
        public int Page => page;
        /// <summary>Fires when the kicked ball fills the screen: the run starts behind the flash.</summary>
        public event System.Action PlayRequested;

        // ------------------------------------------------------------------ build

        public void Build(Transform parent, Camera camera, bool renderWithCamera)
        {
            cam = camera;
            UiArt.Build();
            MenuArt.Build();
            MenuScenery.End();

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
            root = (RectTransform)go.transform;

            // the scene the menu stands in is part of the world; the camera switches to it while the menu is up
            vista = new MenuVista();
            vista.Build(parent);
            // night falls over the switch between arena and scene
            veil = UiKit.Img("Veil", root, null, new Color(0.01f, 0.03f, 0.05f, 1f), Vector2.zero, Vector2.zero);
            MenuUi.Stretch(veil.rectTransform);

            // everything that fades with the menu; the flying balls and the transition sit above it
            content = UiKit.Node("Content", root, Vector2.zero, Vector2.zero);
            MenuUi.Stretch(content);
            contentGroup = content.gameObject.AddComponent<CanvasGroup>();

            main = UiKit.Node("Main", content, Vector2.zero, Vector2.zero);
            MenuUi.Stretch(main);
            mainGroup = main.gameObject.AddComponent<CanvasGroup>();
            stack = UiKit.Node("Stack", main, Vector2.zero, new Vector2(1920f, 1080f));
            BuildCenter();
            BuildLogo();
            BuildPlay();
            BuildColumns();
            BuildTopBars();

            pagesRoot = UiKit.Node("Pages", content, Vector2.zero, Vector2.zero);
            MenuUi.Stretch(pagesRoot);
            nav = new MenuNav
            {
                Register = Register, Open = Open, Back = Back, Toast = ShowToast,
                CanvasPos = rt => root.InverseTransformPoint(rt.TransformPoint(Vector3.zero)),
            };
            characters = new CharacterPage();
            characters.Build(pagesRoot, nav);
            characters.ShowInShop = OpenShop;
            subPages[MenuPage.Characters] = characters.Page;
            pages = new MenuPages();
            pages.Build(pagesRoot, Register, Back);
            foreach (var p in pages.Pages) subPages[p.Id] = p;
            skills = new SkillPage();
            skills.Build(pagesRoot, nav);
            skills.ShowInShop = OpenShop;
            subPages[MenuPage.Skills] = skills.Page;
            shop = new ShopPage();
            shop.Build(pagesRoot, nav);
            shop.Burst = PurchaseBurst;
            subPages[MenuPage.Shop] = shop.Page;
            onboarding = new OnboardingPages();
            onboarding.Build(pagesRoot, nav);
            subPages[MenuPage.Starter] = onboarding.StarterPage;
            subPages[MenuPage.StarterSkills] = onboarding.SkillsPage;
            foreach (var p in subPages) if (p != null) p.Root.gameObject.SetActive(false);

            fxRoot = UiKit.Node("Shots", root, Vector2.zero, Vector2.zero);
            BuildTransition();
            toastRoot = UiKit.Node("Toast", root, Vector2.zero, new Vector2(600f, 70f));
            toastGroup = toastRoot.gameObject.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0f;
            toastPlate = MenuUi.Plate(toastRoot, "Plate", Vector2.zero, new Vector2(600f, 60f), Gold, 0.55f);
            toastPlate.color = MenuArt.Glass.WithAlpha(1f);
            var plate = toastPlate.transform.parent;
            toastShadow = plate.Find("Shadow").GetComponent<Image>();
            toastFrame = plate.Find("Frame").GetComponent<Image>();
            toastLight = plate.Find("TopLight").GetComponent<Image>();
            toast = MenuArt.Label("Text", toastRoot, "", 24f, Gold, new Vector2(0f, 1f), new Vector2(900f, 56f), TextAlignmentOptions.Center, 7f);
            BuildCursor();
            // in captures the canvas is drawn by the game camera, which then only draws these layers
            MenuUi.SetLayer(root, UiLayer);
            canvas.gameObject.SetActive(false);
        }

        static readonly Color Gold = new Color(1f, 0.8f, 0.4f);
        static readonly Color Cool = new Color(0.8f, 0.95f, 1f);
        const int UiLayer = 5;

        void Register(MenuTarget t) => targets.Add(t);

        Mover Move(RectTransform rt, Vector2 outOffset, float delay, MenuTarget target = null)
        {
            var m = new Mover { Rt = rt, Out = outOffset, Delay = delay, Target = target };
            if (!rt.TryGetComponent(out m.Group)) m.Group = rt.gameObject.AddComponent<CanvasGroup>();
            movers.Add(m);
            return m;
        }

        MenuTarget Button(ChunkButton b, string id, System.Action action, string soon = null)
        {
            var t = new MenuTarget { Id = id, Root = b.Root, Size = b.Size, Page = MenuPage.Main, Button = b, Action = action, Soon = soon, Accent = b.Color };
            Register(t);
            return t;
        }

        void BuildLogo()
        {
            logoRoot = UiKit.Node("Logo", stack, new Vector2(0f, LogoY), LogoArt.Area.size * LogoUnit);
            Move(logoRoot, new Vector2(0f, 420f), 0f);
            if (MenuScenery.Logo == null)
            {
                MenuArt.Label("Word", logoRoot, "SOCCERFIGHT", 120f, Palette.Gold, Vector2.zero, new Vector2(1200f, 200f));
                return;
            }
            Vector2 size = LogoArt.Area.size * LogoUnit;
            Vector2 centre = LogoArt.Area.center;
            UiKit.Img("Glow", logoRoot, UiArt.Glow, new Color(0.45f, 0.85f, 1f, 0.2f), new Vector2(0f, -10f), size * 1.35f);
            UiKit.Img("Art", logoRoot, MenuScenery.Logo, Color.white, Vector2.zero, size);

            // the ball that sits in the O
            Vector2 bp = (LogoArt.BallCenter - centre) * LogoUnit;
            float bs = LogoArt.BallRadius * LogoUnit * 2f * 1.12f;
            logoBall = UiKit.Node("Ball", logoRoot, bp, Vector2.one * bs);
            logoSpin = UiKit.Node("Spin", logoBall, Vector2.zero, Vector2.one * bs);
            UiKit.Img("Pattern", logoSpin, Art.BallPattern, Color.white, Vector2.zero, Vector2.one * bs);
            UiKit.Img("Shade", logoBall, Art.BallShade, Color.white, Vector2.zero, Vector2.one * bs);
            UiKit.Img("Hi", logoBall, Art.BallHighlight, Color.white.WithAlpha(0.9f), Vector2.zero, Vector2.one * bs);

            MenuUi.Banner(logoRoot, "Tagline", "DER BALL IST DEINE WAFFE", (LogoArt.TaglineCenter - centre) * LogoUnit, 4.2f * LogoUnit,
                new Color(0.62f, 0.93f, 1f), 21f);

            // a glint sweeps across the whole logo now and then
            var maskImg = UiKit.Img("ShineMask", logoRoot, MenuScenery.Logo, Color.white, Vector2.zero, size);
            maskImg.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            logoShineImg = UiKit.Img("Shine", maskImg.transform, MenuArt.Shine, Color.white.WithAlpha(0f), new Vector2(-size.x, 0f), new Vector2(200f, size.y * 1.3f));
            logoShine = logoShineImg.rectTransform;

            logoSparkles = new Image[4];
            for (int i = 0; i < logoSparkles.Length; i++)
                logoSparkles[i] = UiKit.Img("Sparkle" + i, logoRoot, MenuArt.Sparkle, Color.clear, Vector2.zero, Vector2.one * 40f);
        }

        void BuildCenter()
        {
            // the player stands on the floating centre circle of the scene (MenuVista); a soft pool of
            // floodlight around the feet ties the two together
            pedestalGlow = UiKit.Img("FeetLight", stack, UiArt.Glow, new Color(0.75f, 0.95f, 1f, 0.2f), new Vector2(-10f, FeetY + 6f), new Vector2(380f, 90f));

            figure = new MenuFigure();
            figure.Build(stack, new Vector2(-18f, FeetY), FigureScale, PlayerArt.Get(Characters.Index), Characters.Current);

            var hit = UiKit.Node("FigureHit", stack, new Vector2(0f, FeetY + 175f), new Vector2(300f, 400f));
            figureTarget = new MenuTarget { Id = "figure", Root = hit, Size = hit.sizeDelta, Page = MenuPage.Main, Action = () => Open(MenuPage.Characters), Draw = DrawFigure, Accent = MenuArt.Accent };
            Register(figureTarget);

            // name tag next to the player
            tagRoot = UiKit.Node("Tag", stack, new Vector2(300f, -30f), new Vector2(270f, 124f));
            MenuUi.Plate(tagRoot, "Plate", Vector2.zero, new Vector2(270f, 124f), MenuArt.Accent, 0.3f);
            tagBadge = UiKit.Img("Badge", tagRoot, MenuArt.Badge, Color.white, new Vector2(-94f, 16f), new Vector2(62f, 62f));
            tagIcon = UiKit.Img("Icon", tagBadge.transform, MenuArt.IconStriker, Color.white, Vector2.zero, new Vector2(32f, 32f));
            tagIcon.preserveAspect = true;
            tagName = MenuArt.Label("Name", tagRoot, "", 34f, Color.white, new Vector2(40f, 30f), new Vector2(170f, 44f), TextAlignmentOptions.Left, 7f);
            tagRole = MenuArt.Label("Role", tagRoot, "", 17f, Color.white, new Vector2(40f, 0f), new Vector2(170f, 26f), TextAlignmentOptions.Left, 4f, MenuArt.TextHeavySoft);
            var swap = UiKit.Node("Swap", tagRoot, new Vector2(0f, -40f), new Vector2(236f, 34f));
            UiKit.Img("Pill", swap, UiArt.Pill, new Color(0.36f, 0.92f, 1f, 0.14f), Vector2.zero, new Vector2(236f, 34f), Image.Type.Sliced);
            UiKit.Img("SwapIcon", swap, MenuArt.IconSwap, MenuArt.Accent, new Vector2(-80f, 0f), new Vector2(22f, 22f));
            MenuArt.Label("SwapText", swap, "WECHSELN", 16f, new Color(0.7f, 0.95f, 1f), new Vector2(14f, 0f), new Vector2(170f, 30f), TextAlignmentOptions.Center, 6f, MenuArt.TextHeavySoft);
            tagTarget = new MenuTarget { Id = "tag", Root = tagRoot, Size = tagRoot.sizeDelta, Page = MenuPage.Main, Action = () => Open(MenuPage.Characters), Draw = DrawTag, Accent = MenuArt.Accent };
            Register(tagTarget);
            Move(tagRoot, new Vector2(500f, 0f), 0.35f);
        }

        void BuildPlay()
        {
            // the one warm, solid thing on the screen: lantern gold, like the power shot's ring
            playHalo = UiKit.Img("PlayHalo", stack, UiArt.Glow, Gold.WithAlpha(0.3f), new Vector2(0f, -398f), new Vector2(720f, 260f));
            play = new ChunkButton(stack, "Play", new Vector2(0f, -398f), new Vector2(430f, 108f), Gold, "SPIELEN", 48f, MenuArt.IconPlay, 40f, true, true);
            play.IconLeft(70f);
            var t = Button(play, "play", Play);
            Move(play.Root, new Vector2(0f, -400f), 0.45f, t);
            var hint = MenuArt.Label("Hint", stack, "LINKSKLICK  ·  SCHIESS DEN BALL AUF EINEN KNOPF", 16f, new Color(0.7f, 0.82f, 0.9f, 0.85f), new Vector2(0f, -484f), new Vector2(900f, 28f), TextAlignmentOptions.Center, 6f, MenuArt.TextHeavySoft);
            Move(hint.rectTransform, new Vector2(0f, -200f), 0.55f);
        }

        void BuildColumns()
        {
            leftCol = UiKit.Node("Left", main, Vector2.zero, new Vector2(380f, 560f));
            rightCol = UiKit.Node("Right", main, Vector2.zero, new Vector2(380f, 560f));

            // one accent per button, all on the same dark glass: the colours of the game's own lights
            var shop = new ChunkButton(leftCol, "Shop", new Vector2(0f, 105f), new Vector2(340f, 220f), Gold, "SHOP", 34f, MenuArt.IconShop, 100f).IconTop("SPIELER · FÄHIGKEITEN");
            stickers.Add(MenuUi.Tag(shop.Face, "NEU", new Vector2(128f, 88f), MenuArt.Accent));
            Move(shop.Root, new Vector2(-600f, 0f), 0.15f, Button(shop, "shop", () => Open(MenuPage.Shop)));
            var rank = new ChunkButton(leftCol, "Ranking", new Vector2(0f, -72f), new Vector2(340f, 92f), new Color(1f, 0.62f, 0.32f), "RANGLISTE", 26f, MenuArt.IconTrophy, 40f).IconLeft(30f);
            Move(rank.Root, new Vector2(-600f, 0f), 0.22f, Button(rank, "ranking", () => Open(MenuPage.Ranking)));
            var friends = new ChunkButton(leftCol, "Friends", new Vector2(0f, -182f), new Vector2(340f, 92f), Palette.DashMint, "FREUNDE", 26f, MenuArt.IconFriends, 40f).IconLeft(30f);
            Move(friends.Root, new Vector2(-600f, 0f), 0.29f, Button(friends, "friends", () => Open(MenuPage.Friends)));

            // the loadout is part of every run, so it gets the big tile; events are still a preview
            var skillTile = new ChunkButton(rightCol, "Skills", new Vector2(0f, 105f), new Vector2(340f, 220f), new Color(0.82f, 0.5f, 1f), "FÄHIGKEITEN", 32f, MenuArt.IconSkills, 100f).IconTop("4 PLÄTZE · AUSRÜSTEN");
            stickers.Add(MenuUi.Tag(skillTile.Face, "NEU", new Vector2(-122f, 88f), MenuArt.Accent));
            Move(skillTile.Root, new Vector2(600f, 0f), 0.15f, Button(skillTile, "skills", () => Open(MenuPage.Skills)));
            var events = new ChunkButton(rightCol, "Events", new Vector2(0f, -72f), new Vector2(340f, 92f), Palette.MonsterGlow, "EVENTS", 26f, MenuArt.IconEvents, 40f).IconLeft(30f);
            stickers.Add(MenuUi.Tag(events.Face, "BALD", new Vector2(132f, 36f), Gold, 14f));
            Move(events.Root, new Vector2(600f, 0f), 0.22f, Button(events, "events", () => Open(MenuPage.Events)));
            var settings = new ChunkButton(rightCol, "Settings", new Vector2(0f, -182f), new Vector2(340f, 92f), MenuArt.Accent, "OPTIONEN", 26f, MenuArt.IconGear, 40f).IconLeft(30f);
            Move(settings.Root, new Vector2(600f, 0f), 0.29f, Button(settings, "settings", () => Open(MenuPage.Settings)));
        }

        void BuildTopBars()
        {
            // profile: avatar, name, record
            topLeft = UiKit.Node("Profile", main, Vector2.zero, new Vector2(430f, 110f));
            topLeft.pivot = new Vector2(0f, 1f);
            var tl = Inner(topLeft, new Vector2(0f, 0.5f));
            MenuUi.Plate(tl, "Plate", new Vector2(240f, 0f), new Vector2(330f, 76f), MenuArt.Accent, 0.25f);
            // avatar in a ring, like the health ring of the HUD
            var avatar = UiKit.Node("Avatar", tl, new Vector2(60f, 0f), new Vector2(100f, 100f));
            UiKit.Img("Glow", avatar, UiArt.Glow, MenuArt.Accent.WithAlpha(0.18f), Vector2.zero, Vector2.one * 170f);
            avatarBody = UiKit.Img("Body", avatar, MenuArt.Round, Color.white, Vector2.zero, Vector2.one * 92f);
            var mask = UiKit.Img("Mask", avatar, MenuArt.RoundEdge, Color.white, Vector2.zero, Vector2.one * 88f);
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            avatarHead = UiKit.Img("Head", mask.transform, null, Color.white, Vector2.zero, Vector2.one);
            avatarRing = UiKit.Img("Ring", avatar, MenuArt.RoundFrame, MenuArt.Accent, Vector2.zero, Vector2.one * 102f);
            profileName = MenuArt.Label("Name", tl, "", 28f, Color.white, new Vector2(250f, 14f), new Vector2(240f, 40f), TextAlignmentOptions.Left, 8f);
            UiKit.Img("Trophy", tl, MenuArt.IconTrophy, Gold, new Vector2(144f, -18f), new Vector2(22f, 22f));
            profileBest = MenuArt.Label("Best", tl, "", 16f, Gold, new Vector2(274f, -18f), new Vector2(240f, 28f), TextAlignmentOptions.Left, 4f, MenuArt.TextHeavySoft);
            var hit = UiKit.Node("Hit", tl, new Vector2(215f, 0f), new Vector2(430f, 110f));
            profileTarget = new MenuTarget { Id = "profile", Root = hit, Size = hit.sizeDelta, Page = MenuPage.Main, Action = () => Open(MenuPage.Characters), Draw = DrawProfile };
            Register(profileTarget);
            Move(topLeft, new Vector2(0f, 220f), 0.1f);

            // wallet: coins and gems (placeholders that lead to the shop), quit on desktop
            topRight = UiKit.Node("Wallet", main, Vector2.zero, new Vector2(10f, 110f));
            topRight.pivot = new Vector2(1f, 1f);
            wallet = Inner(topRight, new Vector2(1f, 0.5f));
            float x = 0f;
#if !UNITY_WEBGL || UNITY_EDITOR
            var quit = new ChunkButton(wallet, "Quit", new Vector2(-46f, 0f), new Vector2(84f, 76f), new Color(1f, 0.42f, 0.45f), null, 0f, MenuArt.IconPower, 36f);
            Button(quit, "quit", Quit);
            x = -120f;
#endif
            var info = new ChunkButton(wallet, "Info", new Vector2(x - 46f, 0f), new Vector2(84f, 76f), new Color(0.5f, 0.72f, 1f), null, 0f, MenuArt.IconInfo, 36f);
            Button(info, "info", () => Open(MenuPage.Info));
            x -= 100f;
            gemAmount = Currency("gems", MenuArt.IconGem, new Vector2(x - 110f, 0f));
            coinAmount = Currency("coins", MenuArt.IconCoin, new Vector2(x - 330f, 0f));
            coinPill = (RectTransform)coinAmount.transform.parent;
            Move(topRight, new Vector2(0f, 220f), 0.1f);
        }

        RectTransform wallet;

        /// <summary>Child origin at an edge point of a pinned bar (children are laid out from there).</summary>
        static RectTransform Inner(RectTransform parent, Vector2 anchor)
        {
            var rt = UiKit.Node("Inner", parent, Vector2.zero, Vector2.zero);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        TextMeshProUGUI Currency(string id, Sprite icon, Vector2 pos)
        {
            var pill = UiKit.Node(id, wallet, pos, new Vector2(200f, 70f));
            Color accent = id == "coins" ? Gold : MenuArt.Accent;
            UiKit.Img("Rim", pill, UiArt.Pill, accent.WithAlpha(0.3f), new Vector2(10f, 0f), new Vector2(172f, 52f), Image.Type.Sliced);
            UiKit.Img("Body", pill, UiArt.Pill, MenuArt.Glass, new Vector2(10f, 0f), new Vector2(170f, 50f), Image.Type.Sliced);
            UiKit.Img("Glow", pill, UiArt.Glow, accent.WithAlpha(0.2f), new Vector2(-70f, 0f), new Vector2(110f, 110f));
            UiKit.Img("Icon", pill, icon, Color.white, new Vector2(-70f, 1f), new Vector2(52f, 52f));
            var amount = MenuArt.Label("Amount", pill, "0", 24f, Color.white, new Vector2(8f, 0f), new Vector2(90f, 44f), TextAlignmentOptions.Center, 3f);
            amount.enableAutoSizing = true;
            amount.fontSizeMin = 14f;
            amount.fontSizeMax = 24f;
            var plus = new ChunkButton(pill, "Plus", new Vector2(76f, 0f), new Vector2(40f, 40f), accent, null, 0f, MenuArt.IconPlus, 18f);
            var t = new MenuTarget { Id = id, Root = pill, Size = pill.sizeDelta, Page = MenuPage.Main, Action = () => Open(MenuPage.Shop), Accent = plus.Color };
            t.Draw = m => plus.Style(m.Hover, m.Hit, m.Punch, m.Fade, time);
            Register(t);
            return amount;
        }

        /// <summary>The wallet pills show the real balances; the coin count rolls and pops when it changes.</summary>
        void UpdateWallet(float udt)
        {
            int coins = Wallet.Get(Currencies.Coins);
            if (coinShown < 0f) coinShown = coins;
            if (Mathf.Abs(coinShown - coins) > 0.5f)
            {
                coinShown = Mathf.MoveTowards(coinShown, coins, Mathf.Max(40f, Mathf.Abs(coins - coinShown) * 6f) * udt);
                if (Mathf.Abs(coinShown - coins) <= 0.5f) { coinShown = coins; coinPopVel += 8f; }
            }
            coinAmount.text = Currencies.Format(Mathf.RoundToInt(coinShown));
            gemAmount.text = Currencies.Format(Wallet.Get(Currencies.Gems));
            MathUtil.Spring(ref coinPop, ref coinPopVel, 0f, 5f, 0.3f, udt);
            coinPill.localScale = Vector3.one * (1f + coinPop * 0.08f);
        }

        void OpenShop(ShopItem item)
        {
            Open(MenuPage.Shop);
            shop.Focus(item);
        }

        /// <summary>Something was bought: a burst of light and coins where the card sits.</summary>
        void PurchaseBurst(Vector2 at, Color accent)
        {
            Bump(MenuArt.Burst, at, 80f, 620f, Color.white.WithAlpha(0.9f), 0.35f, 2f, 40f);
            Bump(MenuArt.Shock, at, 60f, 560f, Gold.WithAlpha(0.85f), 0.5f, 1.6f, 0f);
            Bump(MenuArt.Shock, at, 40f, 380f, accent.WithAlpha(0.7f), 0.4f, 1.4f, 0f);
            for (int i = 0; i < 22; i++)
            {
                var b = Take();
                bool star = i % 3 == 0;
                b.Img.sprite = star ? MenuArt.Sparkle : CoinArt.Ui;
                b.Pos = at;
                b.Vel = MathUtil.Dir(Random.Range(20f, 160f)) * Random.Range(300f, 900f);
                float sz = star ? Random.Range(30f, 48f) : Random.Range(26f, 40f);
                b.Size0 = new Vector2(sz, sz);
                b.Size1 = b.Size0 * (star ? 0.2f : 0.8f);
                b.Tint = Color.white;
                b.Life = Random.Range(0.6f, 1f);
                b.Age = 0f;
                b.Grav = 1800f;
                b.Fade = 1.5f;
                b.Align = false;
                b.Spin = Random.Range(-360f, 360f);
                b.Bump = false;
            }
            shake = 0.8f;
            shakeVel = 0f;
            if (Game.I != null) Game.I.Cam.AddTrauma(0.12f);
        }

        void BuildTransition()
        {
            var ft = UiKit.Img("Flash", root, null, new Color(0.92f, 0.98f, 1f, 0f), Vector2.zero, Vector2.zero);
            MenuUi.Stretch(ft.rectTransform);
            flash = ft;
            hero = UiKit.Node("HeroBall", root, Vector2.zero, Vector2.one * 100f);
            heroGlow = UiKit.Img("Glow", hero, UiArt.Glow, new Color(0.7f, 0.95f, 1f, 0f), Vector2.zero, Vector2.one * 260f);
            heroSpin = UiKit.Node("Spin", hero, Vector2.zero, Vector2.one * 100f);
            UiKit.Img("Pattern", heroSpin, MenuScenery.HeroBall != null ? MenuScenery.HeroBall : Art.BallPattern, Color.white, Vector2.zero, Vector2.one * 100f);
            UiKit.Img("Shade", hero, MenuScenery.HeroShade != null ? MenuScenery.HeroShade : Art.BallShade, Color.white, Vector2.zero, Vector2.one * 100f);
            UiKit.Img("Hi", hero, MenuScenery.HeroHighlight != null ? MenuScenery.HeroHighlight : Art.BallHighlight, Color.white.WithAlpha(0.9f), Vector2.zero, Vector2.one * 100f);
            // speed lines streaming out from behind the ball
            speedLines = new Image[14];
            for (int i = 0; i < speedLines.Length; i++)
            {
                speedLines[i] = UiKit.Img("Speed" + i, root, MenuArt.Spark, Color.clear, Vector2.zero, new Vector2(220f, 14f));
                speedLines[i].gameObject.SetActive(false);
            }
            heroGroup = hero.gameObject.AddComponent<CanvasGroup>();
            hero.gameObject.SetActive(false);
            flash.transform.SetAsLastSibling();
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
            curRing = UiKit.Img("Ring", cursorRoot, UiArt.RingThin, Color.white.WithAlpha(0.9f), Vector2.zero, new Vector2(26f, 26f));
            curDot = UiKit.Img("Dot", cursorRoot, UiArt.Circle, Color.white, Vector2.zero, new Vector2(5f, 5f));
        }

        // ------------------------------------------------------------------ open / close

        public void Open()
        {
            IsOpen = true;
            state = State.Menu;
            stateT = 0f;
            // the first launch opens on the starter pick until a starter and three skills are chosen
            page = Profile.Onboarded ? MenuPage.Main : MenuPage.Starter;
            if (page == MenuPage.Starter) onboarding.Reset();
            coinShown = -1f;
            foreach (var p in subPages) if (p != null) { p.T = p.Vel = 0f; p.Root.gameObject.SetActive(false); }
            openT = openVel = 0f;
            hovered = null;
            playFired = false;
            heroT = -1f;
            flashA = heroA = 0f;
            hero.gameObject.SetActive(false);
            logoSpinRate = 60f;
            foreach (var t in targets) t.Hover = t.HoverVel = t.Punch = t.PunchVel = t.Hit = 0f;
            RefreshCharacter(true);
            canvas.gameObject.SetActive(true);
        }

        void Open(int id)
        {
            if (state != State.Menu) return;
            page = id;
            if (id == MenuPage.Characters) characters.Open();
            else if (id == MenuPage.Skills) skills.Refresh();
            else if (id == MenuPage.Shop) shop.Refresh();
            else pages.Refresh();
        }

        void Back()
        {
            if (state != State.Menu) return;
            if (page == MenuPage.Settings && pages.Settings.IsCapturing) pages.Settings.CancelCapture();
            if (MenuPage.IsOnboarding(page)) return;
            page = MenuPage.Main;
        }

        void Play()
        {
            if (state != State.Menu) return;
            if (!Profile.Onboarded) { Open(MenuPage.Starter); return; }
            state = State.Starting;
            stateT = 0f;
            playFired = false;
            heroT = -1f;
            IsOpen = false;
            figure.Kick();
        }

        void Quit()
        {
            if (state != State.Menu) return;
            state = State.Quitting;
            stateT = 0f;
            IsOpen = false;
        }

        /// <summary>Closes the title screen without the play animation (dev tools, restart).</summary>
        public void Dismiss()
        {
            if (!IsOpen) return;
            IsOpen = false;
            state = State.Starting;
            playFired = true;
            heroT = -1f;
            flashA = 0f;
            stateT = 10f;
            SetWorldHidden(false);
        }

        /// <summary>Esc: cancel a rebind, or go back to the main page.</summary>
        public void HandleEscape()
        {
            if (pages.Settings.IsCapturing) pages.Settings.CancelCapture();
            else if (page == MenuPage.StarterSkills) page = MenuPage.Starter;
            else if (MenuPage.IsOnboarding(page)) { }   // the first launch has to be finished
            else if (page != MenuPage.Main) page = MenuPage.Main;
        }

        static void QuitNow()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>
        /// While the menu is up the camera films the menu's own scene instead of the arena: only the
        /// scene's layer and the UI layer (for a canvas the camera draws itself) are rendered.
        /// </summary>
        void SetWorldHidden(bool hide)
        {
            if (Game.I == null) return;
            var c = Game.I.Cam.Cam;
            if (hide && !culled) { savedMask = c.cullingMask; c.cullingMask = MenuVista.Mask | (1 << UiLayer); culled = true; }
            else if (!hide && culled) { c.cullingMask = savedMask; culled = false; }
            vista.SetVisible(culled);
        }

        // ------------------------------------------------------------------ update

        public void Update(float udt)
        {
            if (canvas == null || !canvas.gameObject.activeSelf) return;
            time += udt;
            stateT += udt;
            MathUtil.Spring(ref openT, ref openVel, IsOpen || state == State.Starting ? 1f : 0f, IsOpen ? 2.6f : 5.5f, 0.95f, udt);
            MathUtil.Spring(ref shake, ref shakeVel, 0f, 7f, 0.35f, udt);
            if (Game.I != null) Game.I.Cam.Offset = Vector2.zero;

            if (IsOpen) Aim();
            RefreshCharacter(false);
            UpdatePages(udt);
            UpdateLayout(udt);
            UpdateTargets(udt);
            UpdateCenter(udt);
            UpdateLogo(udt);
            UpdateShots(udt);
            UpdateBits(udt);
            UpdateToast(udt);
            UpdateTransition(udt);
            UpdateCursor(udt);

            // the scene replaces the arena until the kicked ball has filled the screen
            SetWorldHidden(IsOpen || state == State.Quitting || state == State.Starting && !playFired);

            if (state == State.Quitting && stateT > 0.55f) QuitNow();
            bool transitionDone = state != State.Starting || (playFired && flashA <= 0.001f && heroA <= 0.001f);
            if (!IsOpen && transitionDone && (state == State.Starting || openT < 0.01f) && !AnythingLive())
            {
                SetWorldHidden(false);
                canvas.gameObject.SetActive(false);
            }
        }

        bool AnythingLive()
        {
            foreach (var s in shots) if (s.Live) return true;
            foreach (var b in bits) if (b.Live) return true;
            return false;
        }

        void RefreshCharacter(bool force)
        {
            int idx = Characters.Index;
            if (!force && idx == shownCharacter) return;
            bool changed = shownCharacter >= 0 && idx != shownCharacter;
            shownCharacter = idx;
            var def = Characters.Current;
            var look = PlayerArt.Get(idx);
            figure.SetLook(look, def);
            tagName.text = def.Name;
            tagRole.text = def.Role;
            tagRole.color = Color.Lerp(def.Accent, Color.white, 0.4f);
            tagBadge.color = Color.Lerp(def.Accent, new Color(0.05f, 0.09f, 0.14f), 0.35f);
            tagIcon.sprite = MenuArt.ClassIcon(def.Class);
            profileName.text = def.Name;
            int best = RunState.BestStage;
            profileBest.text = best > 0 ? "BESTE STAGE " + best : "NOCH KEIN LAUF";
            avatarBody.color = Color.Lerp(def.Accent, new Color(0.05f, 0.09f, 0.14f), 0.45f);
            avatarRing.color = Color.Lerp(def.Accent, Color.white, 0.3f);
            var head = look.Head;
            if (head != null)
            {
                avatarHead.sprite = head;
                var rt = avatarHead.rectTransform;
                rt.pivot = new Vector2(head.pivot.x / head.rect.width, head.pivot.y / head.rect.height);
                rt.sizeDelta = head.rect.size / head.pixelsPerUnit * 250f;
                rt.anchoredPosition = new Vector2(-8f, -44f);
            }
            if (changed) swapFlash = 1f;
        }

        /// <summary>Pointer position in canvas space, hover state, and the click that kicks a ball.</summary>
        void Aim()
        {
            var uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cam;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(root, GameInput.AimScreen, uiCam, out var local))
                aimLocal = local;

            bool settled = page == MenuPage.Main ? MaxSubT() < 0.2f : subPages[page].T > 0.75f;
            bool interactive = state == State.Menu && openT > 0.8f && settled;
            hovered = interactive ? TargetAt(aimLocal) : null;

            if (!GameInput.ClickPressed) return;
            // the settings card is an ordinary UI surface — only the rest of the screen is a shooting range
            if (page == MenuPage.Settings && subPages[page].T > 0.4f && Inside(pages.Settings.Root, SettingsPanel.Size, aimLocal, 12f)) return;
            Shoot(aimLocal, hovered);
        }

        float MaxSubT()
        {
            float m = 0f;
            foreach (var p in subPages) if (p != null) m = Mathf.Max(m, p.T);
            return m;
        }

        MenuTarget TargetAt(Vector2 local)
        {
            for (int i = targets.Count - 1; i >= 0; i--)
            {
                var t = targets[i];
                if (t.Page != page || t.Fade < 0.5f) continue;
                if (Inside(t.Root, t.Size, local, 6f)) return t;
            }
            return null;
        }

        bool Inside(RectTransform rt, Vector2 size, Vector2 local, float pad)
        {
            if (rt == null || !rt.gameObject.activeInHierarchy) return false;
            Vector3 c3 = root.InverseTransformPoint(rt.TransformPoint(Vector3.zero));
            Vector3 ls = rt.lossyScale;
            float rs = Mathf.Max(1e-5f, root.lossyScale.x);
            Vector2 half = new Vector2(size.x * Mathf.Abs(ls.x), size.y * Mathf.Abs(ls.y)) / rs * 0.5f + Vector2.one * pad;
            Vector2 d = local - (Vector2)c3;
            return Mathf.Abs(d.x) <= half.x && Mathf.Abs(d.y) <= half.y;
        }

        /// <summary>Screen position of a named target — the screenshot driver aims with these.</summary>
        public Vector2 TargetScreen(string id)
        {
            var uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cam;
            foreach (var t in targets)
                if (t.Id == id) return RectTransformUtility.WorldToScreenPoint(uiCam, t.Root.TransformPoint(Vector3.zero));
            return RectTransformUtility.WorldToScreenPoint(uiCam, root.position);
        }

        /// <summary>Screen position of a point in canvas space (centre origin).</summary>
        public Vector2 ScreenOf(Vector2 canvasLocal)
        {
            var uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cam;
            return RectTransformUtility.WorldToScreenPoint(uiCam, root.TransformPoint(canvasLocal));
        }

        void UpdatePages(float udt)
        {
            for (int i = 0; i < subPages.Length; i++)
            {
                var p = subPages[i];
                if (p == null) continue;
                bool on = page == i && state == State.Menu;
                MathUtil.Spring(ref p.T, ref p.Vel, on ? 1f : 0f, 4.2f, 0.9f, udt);
                float t = Mathf.Clamp01(p.T);
                bool show = t > 0.004f;
                if (p.Root.gameObject.activeSelf != show) p.Root.gameObject.SetActive(show);
                if (!show) continue;
                p.Group.alpha = t;
                p.Group.interactable = p.Group.blocksRaycasts = t > 0.5f && IsOpen;
                p.Content.localRotation = Quaternion.identity;
                p.Content.anchoredPosition += new Vector2((1f - t) * 160f, 0f);
            }
            pages.Update(udt);
            var aim = AimNorm();
            characters.Update(udt, aim);
            skills.Update(udt);
            shop.Update(udt, aim);
            onboarding.Update(udt, aim);
            UpdateWallet(udt);
        }

        Vector2 AimNorm()
        {
            Rect r = root.rect;
            return new Vector2(Mathf.Clamp(aimLocal.x / (r.width * 0.5f), -1f, 1f), Mathf.Clamp(aimLocal.y / (r.height * 0.5f), -1f, 1f));
        }

        void UpdateLayout(float udt)
        {
            Rect r = root.rect;
            float w = r.width, h = r.height;
            float sub = MaxSubT();
            float leave = state == State.Starting ? MathUtil.Smooth01(stateT / 0.35f) : state == State.Quitting ? MathUtil.Smooth01(stateT / 0.3f) : 0f;
            float away = Mathf.Max(Mathf.Clamp01(sub), leave);   // 1 = main page elements gone

            contentGroup.alpha = state == State.Starting ? (playFired ? 0f : 1f) : Mathf.Clamp01(openT);
            contentGroup.blocksRaycasts = contentGroup.interactable = IsOpen;
            mainGroup.alpha = 1f;
            main.gameObject.SetActive(away < 0.999f || state == State.Starting);

            // the centre stack scales with the window height; the columns hug the side edges
            float k = Mathf.Clamp(Mathf.Min(h / 1080f, w / 1500f), 0.7f, 1.1f);
            Vector2 jolt = new Vector2(Mathf.Sin(time * 71f), Mathf.Cos(time * 63f)) * shake * 14f;
            stack.localScale = new Vector3(k, k, 1f);
            stack.anchoredPosition = jolt;
            float s = Mathf.Clamp(Mathf.Min((w - 980f) / 940f, h / 1080f), 0.62f, 1.08f);
            float colX = w * 0.5f - 36f - 190f * s;
            leftCol.localScale = rightCol.localScale = new Vector3(s, s, 1f);
            leftCol.anchoredPosition = new Vector2(-colX, -20f * s) + jolt;
            rightCol.anchoredPosition = new Vector2(colX, -20f * s) + jolt;
            float ts = Mathf.Clamp(Mathf.Min(w / 1920f, h / 1080f), 0.7f, 1.1f);
            topLeft.localScale = topRight.localScale = new Vector3(ts, ts, 1f);
            MenuUi.Pin(topLeft, new Vector2(0f, 1f), new Vector2(26f, -22f));
            MenuUi.Pin(topRight, new Vector2(1f, 1f), new Vector2(-26f, -24f));

            // entrance and exit of each main element
            foreach (var m in movers)
            {
                float e = Mathf.Clamp01((openT - m.Delay * 0.6f) / Mathf.Max(0.05f, 1f - m.Delay * 0.6f));
                float enter = MathUtil.EaseOutBack(e, 1.3f);
                float gone = MathUtil.Smooth01(away);
                float vis = Mathf.Clamp01(e * 1.6f) * (1f - gone);
                var home = MoverHome(m);
                m.Rt.anchoredPosition = home + m.Out * ((1f - enter) + gone);
                m.Group.alpha = vis;
                if (m.Target != null) m.Target.Fade = vis;
            }
            // targets that don't move on their own follow the page
            figureTarget.Fade = tagTarget.Fade = profileTarget.Fade = (1f - away) * Mathf.Clamp01(openT * 1.4f);
            foreach (var t in targets)
                if (t.Page == MenuPage.Main && t.Id != null && (t.Id == "coins" || t.Id == "gems" || t.Id == "quit" || t.Id == "info")) t.Fade = (1f - away) * Mathf.Clamp01(openT * 1.4f);
            foreach (var t in targets)
                if (t.Page != MenuPage.Main && subPages[t.Page] != null)
                    t.Fade = Mathf.Clamp01(subPages[t.Page].T) * (t.Visible != null ? Mathf.Clamp01(t.Visible()) : 1f);

            // the centre (figure, pedestal) drops away a little when a page opens
            float centreFade = (1f - away) * Mathf.Clamp01(openT * 1.5f);
            if (state == State.Starting && !playFired) centreFade = 1f;
            figure.SetAlpha(centreFade);
            pedestalGlow.color = pedestalGlow.color.WithAlpha((0.2f + 0.05f * Mathf.Sin(time * 2f)) * centreFade);
            playHalo.color = Gold.WithAlpha((0.2f + 0.08f * Mathf.Sin(time * 2.2f)) * (1f - away) * Mathf.Clamp01(openT * 1.4f));

            for (int i = 0; i < stickers.Count; i++)
            {
                float st = 1f + 0.04f * Mathf.Sin(time * 3f + i * 2f);
                stickers[i].localScale = new Vector3(st, st, 1f);
            }

            // night falls over the switch from the arena to the scene, and over quitting
            float veilA = state == State.Quitting ? MathUtil.Smooth01(stateT / 0.45f) : state == State.Starting ? 0f : 1f - MathUtil.Smooth01(openT * 1.7f);
            veil.color = veil.color.WithAlpha(veilA);
            veil.enabled = veilA > 0.002f;

            // the scene pushes in when the menu opens, on sub pages and as the ball flies at the camera
            float zoom = 1f + 0.06f * (1f - MathUtil.EaseOutCubic(Mathf.Clamp01(openT))) + 0.04f * Mathf.Clamp01(sub);
            if (state == State.Starting) zoom += 0.12f * MathUtil.EaseInCubic(Mathf.Clamp01(stateT / 0.8f));
            if (Game.I != null && culled)
            {
                var rig = Game.I.Cam;
                float wind = Game.I.Environment != null ? Game.I.Environment.Wind : 0f;
                vista.Update(udt, rig.Cam, rig.Center, AimNorm(), zoom, new Vector2(0f, 0.5f * Mathf.Clamp01(sub)), wind);
                // the floating centre circle stays right under the player's boots
                Vector2 feet = CanvasToWorld(StackToCanvas(new Vector2(-10f, FeetY)));
                float worldPerPx = rig.Cam.orthographicSize * 2f / Mathf.Max(1f, r.height);
                vista.PlacePedestal(feet, k * worldPerPx * 1080f / 9.8f, centreFade);
            }
        }

        /// <summary>World position (on the game camera) of a point in canvas space.</summary>
        Vector2 CanvasToWorld(Vector2 canvasLocal)
        {
            var c = Game.I.Cam.Cam;
            Vector2 screen = ScreenOf(canvasLocal);
            Vector3 w = c.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -c.transform.position.z));
            return new Vector2(w.x, w.y);
        }

        Vector2 MoverHome(Mover m)
        {
            if (m.Rt == topLeft) return new Vector2(26f, -22f);
            if (m.Rt == topRight) return new Vector2(-26f, -24f);
            if (m.Rt == logoRoot) return new Vector2(0f, LogoY + Mathf.Sin(time * 0.9f) * 5f);
            return HomeOf(m);
        }

        readonly Dictionary<RectTransform, Vector2> homes = new Dictionary<RectTransform, Vector2>();

        Vector2 HomeOf(Mover m)
        {
            if (!homes.TryGetValue(m.Rt, out var h)) { h = m.Rt.anchoredPosition; homes[m.Rt] = h; }
            return h;
        }

        void UpdateTargets(float udt)
        {
            foreach (var t in targets)
            {
                MathUtil.Spring(ref t.Hover, ref t.HoverVel, hovered == t ? 1f : 0f, 5.5f, 0.85f, udt);
                MathUtil.Spring(ref t.Punch, ref t.PunchVel, 0f, 6f, 0.32f, udt);
                t.Hit = Mathf.Max(0f, t.Hit - udt * 3f);
                float h = Mathf.Clamp01(t.Hover);
                if (t.Button != null) t.Button.Style(h, t.Hit, t.Punch, t.Fade, time);
                else t.Draw?.Invoke(t);
            }
        }

        void DrawFigure(MenuTarget t)
        {
            float h = Mathf.Clamp01(t.Hover);
            float s = 1f + h * 0.03f + t.Punch * 0.04f;
            figure.Root.localScale = new Vector3(s, s, 1f);
            pedestalGlow.rectTransform.localScale = Vector3.one * (1f + h * 0.15f + swapFlash * 0.4f);
        }

        void DrawTag(MenuTarget t)
        {
            float h = Mathf.Max(Mathf.Clamp01(t.Hover), Mathf.Clamp01(figureTarget.Hover));
            float s = 1f + h * 0.04f + t.Punch * 0.06f;
            tagRoot.localScale = new Vector3(s, s, 1f);
        }

        void DrawProfile(MenuTarget t)
        {
            float h = Mathf.Clamp01(t.Hover);
            float s = 1f + h * 0.04f + t.Punch * 0.05f;
            topLeft.localScale = topRight.localScale * s;
        }

        void UpdateCenter(float udt)
        {
            swapFlash = Mathf.Max(0f, swapFlash - udt * 1.5f);
            figure.BallVisible = true;
            figure.Update(udt, MenuFigure.Mode.Juggle, AimNorm());
            if (swapFlash > 0.95f)
            {
                Vector2 at = StackToCanvas(new Vector2(0f, FeetY + 170f));
                Bump(MenuArt.Burst, at, 80f, 520f, Color.white.WithAlpha(0.8f), 0.35f, 2f, 30f);
                Bump(MenuArt.Shock, at, 60f, 480f, Palette.ShotCyan.WithAlpha(0.7f), 0.45f, 1.6f, 0f);
                swapFlash = 0.94f;
            }
            // sparkles drift up out of the pedestal
            if (IsOpen && page == MenuPage.Main && Random.value < udt * 5f)
            {
                var b = Take();
                b.Img.sprite = MenuArt.Sparkle;
                b.Pos = StackToCanvas(new Vector2(Random.Range(-150f, 150f), FeetY + Random.Range(-10f, 20f)));
                b.Vel = new Vector2(Random.Range(-6f, 6f), Random.Range(40f, 90f));
                float sz = Random.Range(12f, 22f);
                b.Size0 = b.Size1 = new Vector2(sz, sz);
                b.Tint = Color.Lerp(Palette.Crystal, Color.white, Random.value * 0.6f).WithAlpha(0.7f);
                b.Life = Random.Range(1.2f, 2.2f);
                b.Age = 0f; b.Grav = 0f; b.Fade = 1f; b.Align = false; b.Spin = 60f; b.Bump = true;
                b.Rt.SetAsFirstSibling();
            }
        }

        Vector2 StackToCanvas(Vector2 p) => stack.anchoredPosition + p * stack.localScale.x;

        void UpdateLogo(float udt)
        {
            if (logoSpin == null) return;
            logoSpinRate = Mathf.Lerp(logoSpinRate, 60f, udt * 1.2f);
            logoAngle -= logoSpinRate * udt;
            logoSpin.localRotation = Quaternion.Euler(0f, 0f, logoAngle);
            float breathe = 1f + 0.012f * Mathf.Sin(time * 1.7f);
            logoRoot.localScale = new Vector3(breathe, breathe, 1f);

            shineT += udt;
            const float period = 4.5f, sweep = 0.9f;
            if (shineT > period) shineT -= period;
            float w = logoRoot.sizeDelta.x;
            float u = Mathf.Clamp01(shineT / sweep);
            logoShine.anchoredPosition = new Vector2(Mathf.Lerp(-w * 0.6f, w * 0.6f, MathUtil.EaseInOutSine(u)), 0f);
            logoShineImg.color = Color.white.WithAlpha(shineT < sweep ? 0.6f : 0f);

            for (int i = 0; i < logoSparkles.Length; i++)
            {
                float cyc = time * 0.7f + i * 0.61f;
                float k = Mathf.Repeat(cyc, 1f);
                int seed = Mathf.FloorToInt(cyc) * 7 + i * 13;
                var rt = logoSparkles[i].rectTransform;
                if (k < 0.02f || rt.anchoredPosition == Vector2.zero)
                    rt.anchoredPosition = new Vector2((MathUtil.Hash(seed) * 0.8f) * w * 0.45f, 20f + MathUtil.Hash(seed + 3) * 120f);
                float a = MathUtil.Bump(Mathf.Clamp01(k / 0.35f));
                rt.sizeDelta = Vector2.one * (30f + 30f * a);
                rt.localRotation = Quaternion.Euler(0f, 0f, k * 90f);
                logoSparkles[i].color = Color.white.WithAlpha(a * 0.9f);
            }
        }

        // ------------------------------------------------------------------ the ball

        void Shoot(Vector2 at, MenuTarget target)
        {
            var s = TakeShot();
            s.Live = true;
            s.Flying = true;
            s.Target = target;
            s.T = 0f;
            // kicked from the player's point of view: just below the frame, roughly under the target
            s.From = new Vector2(at.x * 0.28f + Random.Range(-50f, 50f), -BelowFrame);
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

        /// <summary>Just outside the bottom edge — the canvas is taller than 1080 on narrow windows.</summary>
        float BelowFrame => root.rect.height * 0.5f + 170f;

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
                        g.color = Color.Lerp(Color.white, Palette.ShotCyan, k * 0.25f).WithAlpha(0.3f * (1f - k / (float)s.Ghosts.Length));
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
                    if (s.Pos.y < -BelowFrame - 90f) { Retire(s); continue; }
                }

                s.Root.anchoredPosition = s.Pos;
                s.Root.localScale = new Vector3(s.Scale, s.Scale, 1f);
                s.Spin.localRotation = Quaternion.Euler(0f, 0f, s.Angle);
                s.Glow.color = new Color(0.7f, 0.95f, 1f, s.Flying ? 0.3f : 0.08f);
            }
        }

        void Land(Shot s)
        {
            s.Flying = false;
            var target = s.Target;
            s.Target = null;
            // a page change while the ball was in the air: it only hits what is still there
            bool hit = target != null && target.Page == page && target.Fade > 0.5f && (state == State.Menu);
            bool soon = hit && target.Soon != null;
            Vector2 at = s.To;
            Color tint = hit ? Color.Lerp(target.Accent, Color.white, 0.2f) : Cool;

            // bounces back towards the camera: a short hop, then gravity takes it out of frame
            float away = at.x < s.From.x ? -1f : 1f;
            s.Vel = new Vector2(away * Random.Range(40f, 150f), Random.Range(300f, 430f));

            Bump(MenuArt.Burst, at, 70f, hit ? 460f : 260f, Color.white.WithAlpha(hit ? 0.95f : 0.45f), hit ? 0.3f : 0.2f, 2.2f, Random.Range(-70f, 70f));
            Bump(MenuArt.Shock, at, 60f, hit ? 430f : 250f, tint.WithAlpha(hit ? 0.8f : 0.35f), hit ? 0.45f : 0.3f, 1.6f, 0f);
            if (hit) Bump(MenuArt.Shock, at, 40f, 250f, Color.white.WithAlpha(0.5f), 0.26f, 1.4f, 0f);

            int n = hit ? 16 : 6;
            for (int i = 0; i < n; i++)
            {
                var b = Take();
                b.Img.sprite = i % 4 == 0 && hit ? MenuArt.Sparkle : MenuArt.Spark;
                b.Pos = at;
                b.Vel = MathUtil.Dir(Random.Range(0f, 360f)) * Random.Range(320f, hit ? 1000f : 520f);
                bool star = b.Img.sprite == MenuArt.Sparkle;
                b.Size0 = star ? Vector2.one * Random.Range(30f, 46f) : new Vector2(Random.Range(26f, 48f), Random.Range(5f, 8f));
                b.Size1 = b.Size0 * 0.3f;
                b.Tint = i % 3 == 0 ? Color.white : tint;
                b.Life = Random.Range(0.3f, 0.6f);
                b.Age = 0f;
                b.Grav = 1500f;
                b.Fade = 1.3f;
                b.Align = !star;
                b.Spin = star ? 200f : 0f;
                b.Bump = false;
            }

            if (hit)
            {
                target.PunchVel += 15f;
                target.Hit = 1f;
                logoSpinRate = 520f;
                shake = soon ? 0.5f : 1f;
                shakeVel = 0f;
                if (Game.I != null) Game.I.Cam.AddTrauma(0.16f);
                if (soon) ShowToast(target.Soon, at);
                else target.Action?.Invoke();
            }
            else
            {
                shake = 0.3f;
                if (Game.I != null) Game.I.Cam.AddTrauma(0.05f);
            }
        }

        void ShowToast(string text, Vector2 at)
        {
            toast.text = text;
            toastT = 0f;
            float w = toast.GetPreferredValues(text).x + 80f;
            ((RectTransform)toastPlate.transform.parent).sizeDelta = new Vector2(w, 60f);
            toastPlate.rectTransform.sizeDelta = new Vector2(w, 60f);
            toastFrame.rectTransform.sizeDelta = new Vector2(w + 2f, 62f);
            toastShadow.rectTransform.sizeDelta = new Vector2(w * 1.08f + 60f, 125f);
            toastLight.rectTransform.sizeDelta = new Vector2(w * 0.7f, 2f);
            Rect r = root.rect;
            float half = w * 0.5f + 20f;
            toastPos = new Vector2(Mathf.Clamp(at.x, -r.width * 0.5f + half, r.width * 0.5f - half), Mathf.Clamp(at.y + 110f, -r.height * 0.5f + 60f, r.height * 0.5f - 60f));
            toastRoot.SetAsLastSibling();
        }

        void UpdateToast(float udt)
        {
            toastT += udt;
            if (toastT > 2f) { toastGroup.alpha = 0f; return; }
            toastGroup.alpha = Mathf.Clamp01(toastT * 8f) * (1f - MathUtil.Smooth01((toastT - 1.4f) / 0.6f));
            toastRoot.anchoredPosition = toastPos + new Vector2(0f, MathUtil.EaseOutCubic(toastT / 0.5f) * 30f);
            float s = 1f + 0.3f * (1f - MathUtil.EaseOutBack(Mathf.Clamp01(toastT / 0.25f), 2f));
            toastRoot.localScale = new Vector3(s, s, 1f);
            toastRoot.localRotation = Quaternion.Euler(0f, 0f, 2f * Mathf.Sin(toastT * 6f) * (1f - Mathf.Clamp01(toastT)));
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
            b.Bump = false;
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
                float a = b.Bump ? MathUtil.Bump(u) : Mathf.Pow(1f - u, b.Fade);
                b.Img.color = b.Tint.WithAlpha(b.Tint.a * a);
            }
        }

        // ------------------------------------------------------------------ SPIELEN

        void UpdateTransition(float udt)
        {
            if (state == State.Starting && !playFired)
            {
                if (heroT < 0f && figure.Kicking && stateT >= MenuFigure.KickContact)
                {
                    heroT = 0f;
                    var ballRt = figure.BallRect;
                    heroFrom = root.InverseTransformPoint(ballRt.TransformPoint(Vector3.zero));
                    Vector2 at = heroFrom;
                    Bump(MenuArt.Burst, at, 60f, 520f, Color.white, 0.3f, 2f, 40f);
                    Bump(MenuArt.Shock, at, 40f, 420f, Palette.ShotCyan.WithAlpha(0.9f), 0.4f, 1.6f, 0f);
                    shake = 1.2f;
                    shakeVel = 0f;
                    hero.gameObject.SetActive(true);
                    hero.SetAsLastSibling();
                    flash.transform.SetAsLastSibling();
                }
                if (heroT >= 0f)
                {
                    heroT += udt;
                    const float dur = 0.55f;
                    float u = Mathf.Clamp01(heroT / dur);
                    Rect r = root.rect;
                    float start = Art.BallRadius * 2.24f * FigureScale * stack.localScale.x;
                    float end = Mathf.Max(r.width, r.height) * 1.7f;
                    float grow = MathUtil.EaseInCubic(u);
                    hero.anchoredPosition = Vector2.Lerp(heroFrom, new Vector2(0f, -40f), MathUtil.EaseInOutSine(u)) + new Vector2(0f, Mathf.Sin(u * Mathf.PI) * 120f);
                    hero.sizeDelta = Vector2.one * 100f;
                    float sc = Mathf.Lerp(start, end, grow) / 100f;
                    hero.localScale = new Vector3(sc, sc, 1f);
                    heroSpin.localRotation = Quaternion.Euler(0f, 0f, -heroT * 900f);
                    heroA = 1f;
                    heroGlow.color = new Color(0.7f, 0.95f, 1f, 0.6f * (1f - u));
                    UpdateSpeedLines(u, sc * 50f);
                    flashA = MathUtil.Smooth01((u - 0.72f) / 0.28f);
                    if (u >= 1f)
                    {
                        playFired = true;
                        flashA = 1f;
                        SetWorldHidden(false);
                        PlayRequested?.Invoke();
                        if (Game.I != null) { Game.I.Cam.AddTrauma(0.35f); Game.I.Cam.ZoomPunch(0.08f); }
                    }
                }
            }
            else if (playFired)
            {
                heroA = Mathf.Max(0f, heroA - udt * 6f);
                flashA = Mathf.Max(0f, flashA - udt * 2.2f);
                if (heroA <= 0f && hero.gameObject.activeSelf) hero.gameObject.SetActive(false);
                foreach (var l in speedLines) if (l.gameObject.activeSelf) l.gameObject.SetActive(false);
            }
            if (hero.gameObject.activeSelf) heroGroup.alpha = heroA;
            flash.color = new Color(0.92f, 0.98f, 1f, flashA);
        }

        /// <summary>Streaks racing outwards from behind the ball as it comes at the camera.</summary>
        void UpdateSpeedLines(float u, float ballRadius)
        {
            Vector2 c = hero.anchoredPosition;
            float span = Mathf.Max(root.rect.width, root.rect.height);
            for (int i = 0; i < speedLines.Length; i++)
            {
                var l = speedLines[i];
                if (!l.gameObject.activeSelf) l.gameObject.SetActive(true);
                float ang = i * 360f / speedLines.Length + MathUtil.Hash(i * 31) * 12f;
                float travel = Mathf.Repeat(heroT * (1400f + 300f * MathUtil.Hash(i * 7)) + i * 97f, span * 0.6f);
                float r = ballRadius * 0.9f + travel;
                var rt = l.rectTransform;
                rt.anchoredPosition = c + MathUtil.Dir(ang) * r;
                rt.localRotation = Quaternion.Euler(0f, 0f, ang);
                rt.sizeDelta = new Vector2(160f + 260f * u, 10f + 8f * u);
                float fade = 1f - Mathf.Clamp01(travel / (span * 0.6f));
                l.color = new Color(0.8f, 0.95f, 1f, 0.5f * fade * Mathf.Clamp01(u * 4f) * (1f - flashA));
            }
        }

        // ------------------------------------------------------------------ cursor

        void UpdateCursor(float udt)
        {
            bool show = IsOpen && openT > 0.15f;
            if (cursorRoot.gameObject.activeSelf != show) cursorRoot.gameObject.SetActive(show);
            if (!show) return;
            cursorRoot.SetAsLastSibling();

            MathUtil.Spring(ref curPunch, ref curPunchVel, 0f, 5f, 0.4f, udt);
            MathUtil.Spring(ref lockT, ref lockVel, hovered != null ? 1f : 0f, 6f, 0.8f, udt);
            cursorRoot.anchoredPosition = aimLocal;
            float s = 1f + curPunch * 0.45f;
            cursorRoot.localScale = new Vector3(s, s, 1f);

            Color accent = hovered != null ? Color.Lerp(hovered.Accent, Color.white, 0.25f) : Color.white;
            curRing.color = Color.Lerp(Color.white.WithAlpha(0.9f), accent, lockT * 0.9f);
            curDot.color = Color.Lerp(Color.white, accent, lockT * 0.7f);
            curGlow.color = Color.Lerp(Palette.ShotCyan, hovered != null ? hovered.Accent : Palette.ShotCyan, lockT).WithAlpha(0.14f + 0.16f * lockT + 0.25f * curPunch);
            float pad = Mathf.Lerp(22f, 12f, lockT);
            float bs = Mathf.Lerp(0.7f, 1f, lockT);
            for (int k = 0; k < 4; k++)
            {
                float sx = k == 0 || k == 3 ? -1f : 1f;
                float sy = k <= 1 ? 1f : -1f;
                var rt = curBrackets[k].rectTransform;
                rt.anchoredPosition = new Vector2(sx * pad, sy * pad);
                rt.localScale = new Vector3(bs, bs, 1f);
                curBrackets[k].color = accent.WithAlpha(lockT * 0.95f);
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
            n.Glow = UiKit.Img("Glow", n.Root, UiArt.Glow, new Color(0.7f, 0.95f, 1f, 0.3f), Vector2.zero, new Vector2(290f, 290f));
            n.Spin = UiKit.Node("Spin", n.Root, Vector2.zero, new Vector2(118f, 118f));
            UiKit.Img("Pattern", n.Spin, Art.BallPattern, Color.white, Vector2.zero, new Vector2(118f, 118f));
            UiKit.Img("Shade", n.Root, Art.BallShade, Color.white, Vector2.zero, new Vector2(118f, 118f));
            UiKit.Img("Hi", n.Root, Art.BallHighlight, Color.white.WithAlpha(0.9f), Vector2.zero, new Vector2(118f, 118f));
            foreach (var g in n.Ghosts) g.gameObject.layer = UiLayer;
            MenuUi.SetLayer(n.Root, UiLayer);
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
            n.Rt.gameObject.layer = UiLayer;
            bits.Add(n);
            return n;
        }
    }
}
