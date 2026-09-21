using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoccerFight
{
    /// <summary>
    /// Frame every title-screen sub page shares, laid out like the in-game upgrade screen: the scene
    /// dimmed to night blue, a small accent overline, a big tracked title with a fading line under
    /// it, and a glass back button in the top-left corner.
    /// </summary>
    public sealed class SubPage
    {
        public readonly int Id;
        public readonly RectTransform Root, Content;
        public readonly CanvasGroup Group;
        public readonly MenuTarget Back;
        public float T, Vel;
        readonly RectTransform title;

        public SubPage(RectTransform parent, int id, string heading, string overline, Color accent, System.Action<MenuTarget> register, System.Action back)
        {
            Id = id;
            Root = UiKit.Node("Page " + heading, parent, Vector2.zero, Vector2.zero);
            MenuUi.Stretch(Root);
            Group = Root.gameObject.AddComponent<CanvasGroup>();

            MenuUi.Stretch(UiKit.Img("Dim", Root, null, new Color(0.01f, 0.03f, 0.05f, 0.72f), Vector2.zero, Vector2.zero).rectTransform);
            var vignette = UiKit.Img("Vignette", Root, MenuArt.Vignette, new Color(0f, 0f, 0f, 0.55f), Vector2.zero, Vector2.zero);
            MenuUi.Stretch(vignette.rectTransform);

            title = UiKit.Node("Heading", Root, Vector2.zero, new Vector2(1200f, 150f));
            MenuUi.Pin(title, new Vector2(0.5f, 1f), new Vector2(0f, -92f));
            MenuArt.Label("Overline", title, overline, 22f, accent, new Vector2(0f, 46f), new Vector2(1000f, 32f), TextAlignmentOptions.Center, 9f, MenuArt.TextHeavySoft);
            MenuArt.Label("Title", title, heading, 60f, Color.white, new Vector2(0f, -4f), new Vector2(1200f, 80f), TextAlignmentOptions.Center, 18f);
            UiKit.Img("Line", title, UiArt.LineFade, accent.WithAlpha(0.5f), new Vector2(0f, -54f), new Vector2(760f, 2f));

            var holder = UiKit.Node("BackHolder", Root, Vector2.zero, new Vector2(110f, 96f));
            MenuUi.Pin(holder, new Vector2(0f, 1f), new Vector2(96f, -78f));
            var button = new ChunkButton(holder, "Back", Vector2.zero, new Vector2(96f, 84f), MenuArt.Accent, null, 0f, MenuArt.IconBack, 46f);
            Back = new MenuTarget { Id = "back" + id, Root = button.Root, Size = button.Size, Page = id, Action = back, Button = button, Accent = MenuArt.Accent };
            register(Back);

            Content = UiKit.Node("Content", Root, new Vector2(0f, -40f), new Vector2(1600f, 900f));
        }

        public void Update(float udt)
        {
            Rect r = Root.rect;
            // the whole page fits the window: content shrinks on small canvases
            float s = Mathf.Min(1f, Mathf.Min(r.width / 1700f, (r.height - 60f) / 1000f));
            Content.localScale = new Vector3(s, s, 1f);
            Content.anchoredPosition = new Vector2(0f, -40f * s);
            title.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, T) * Mathf.Min(1f, r.width / 1300f);
        }
    }

    /// <summary>The placeholder pages (shop, ranking, friends, events) and the info page.</summary>
    public sealed class MenuPages
    {
        public readonly List<SubPage> Pages = new List<SubPage>();
        readonly List<RectTransform> bobbers = new List<RectTransform>();
        readonly List<(Image img, Color color, float phase)> glows = new List<(Image, Color, float)>();
        TextMeshProUGUI rankYou, rankValue;
        readonly List<(TextMeshProUGUI key, GameAction action)> keyRows = new List<(TextMeshProUGUI, GameAction)>();
        float time;

        public SettingsPanel Settings { get; private set; }

        System.Action<MenuTarget> register;
        System.Action back;

        static readonly Color Gold = new Color(1f, 0.8f, 0.4f);
        static readonly Color Muted = new Color(0.62f, 0.72f, 0.8f);

        public void Build(RectTransform parent, System.Action<MenuTarget> reg, System.Action goBack)
        {
            register = reg;
            back = goBack;
            BuildShop(parent);
            BuildRanking(parent);
            BuildFriends(parent);
            BuildEvents(parent);
            BuildInfo(parent);
            BuildSettings(parent);
            KeyBindings.Changed += RefreshKeys;
        }

        SubPage NewPage(RectTransform parent, int id, string title, string overline, Color accent)
        {
            var p = new SubPage(parent, id, title, overline, accent, register, back);
            Pages.Add(p);
            return p;
        }

        ChunkButton SoonButton(Transform parent, int page, string id, Vector2 pos, Vector2 size, Color color, string label, float font, string soon)
        {
            var b = new ChunkButton(parent, id, pos, size, color, label, font, MenuArt.IconLock, size.y * 0.42f);
            b.IconLeft(24f);
            b.Disabled = true;
            register(new MenuTarget { Id = id, Root = b.Root, Size = size, Page = page, Button = b, Soon = soon, Accent = color });
            return b;
        }

        TextMeshProUGUI Body(Transform parent, string text, Vector2 pos, Vector2 box, float size = 22f, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var t = MenuArt.Label("Text", parent, text, size, new Color(0.8f, 0.87f, 0.92f), pos, box, align, 0.5f, MenuArt.TextHeavySoft);
            t.fontStyle = FontStyles.Normal;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        /// <summary>A glyph in a lit ring, the way the upgrade cards show their icons.</summary>
        Image Emblem(Transform parent, Sprite icon, Vector2 pos, float size, Color accent, bool colored = false)
        {
            var glow = UiKit.Img("EmblemGlow", parent, UiArt.Glow, accent.WithAlpha(0.22f), pos, Vector2.one * size * 2f);
            glows.Add((glow, accent, Random.value * 10f));
            UiKit.Img("EmblemDisc", parent, MenuArt.Round, new Color(0.02f, 0.05f, 0.08f, 0.8f), pos, Vector2.one * size);
            UiKit.Img("EmblemRing", parent, MenuArt.RoundFrame, Color.Lerp(accent, Color.white, 0.3f).WithAlpha(0.85f), pos, Vector2.one * (size + 4f));
            var img = UiKit.Img("Emblem", parent, icon, colored ? Color.white : Color.Lerp(accent, Color.white, 0.5f), pos, Vector2.one * size * 0.56f);
            img.preserveAspect = true;
            bobbers.Add(img.rectTransform);
            return img;
        }

        // ------------------------------------------------------------------ shop

        void BuildShop(RectTransform parent)
        {
            var page = NewPage(parent, MenuPage.Shop, "SHOP", "TRIKOTS · BÄLLE · KRISTALLE", Gold);
            string[] names = { "STARTERPAKET", "GOLDENER BALL", "TRIKOT-SET", "KRISTALLE" };
            string[] lines = { "Ein Schwung Münzen und ein Trikot für den Anfang.", "Ein Ball, der golden leuchtet und Funken zieht.", "Neue Farben für alle drei Spieler.", "Die seltene Währung aus den Ruinen." };
            Sprite[] icons = { MenuArt.IconShop, MenuArt.IconCoin, MenuArt.IconStar, MenuArt.IconGem };
            Color[] cols = { MenuArt.Accent, Gold, new Color(1f, 0.45f, 0.42f), new Color(0.45f, 0.9f, 1f) };
            for (int i = 0; i < names.Length; i++)
            {
                var tile = UiKit.Node("Offer" + i, page.Content, new Vector2((i - 1.5f) * 345f, 30f), new Vector2(310f, 470f));
                MenuUi.Plate(tile, "Card", Vector2.zero, new Vector2(310f, 470f), cols[i], 0.35f);
                Emblem(tile, icons[i], new Vector2(0f, 95f), 150f, cols[i], i == 1 || i == 3);
                MenuArt.Label("Name", tile, names[i], 26f, Color.white, new Vector2(0f, -38f), new Vector2(300f, 40f), TextAlignmentOptions.Center, 4f);
                UiKit.Img("Line", tile, UiArt.LineFade, cols[i].WithAlpha(0.4f), new Vector2(0f, -66f), new Vector2(220f, 2f));
                Body(tile, lines[i], new Vector2(0f, -108f), new Vector2(260f, 60f), 19f);
                SoonButton(tile, MenuPage.Shop, "buy" + i, new Vector2(0f, -186f), new Vector2(240f, 66f), cols[i], "BALD", 26f, "DER SHOP ÖFFNET BALD");
                if (i == 0) MenuUi.Tag(tile, "NEU", new Vector2(110f, 214f), MenuArt.Accent);
            }
            MenuUi.Banner(page.Content, "Soon", "DER SHOP ÖFFNET IN EINEM SPÄTEREN UPDATE", new Vector2(0f, -300f), 1100f, Muted, 22f);
        }

        // ------------------------------------------------------------------ ranking

        void BuildRanking(RectTransform parent)
        {
            var page = NewPage(parent, MenuPage.Ranking, "RANGLISTE", "BESTE STAGES", Gold);
            var panel = UiKit.Node("Panel", page.Content, new Vector2(0f, 0f), new Vector2(960f, 640f));
            MenuUi.Plate(panel, "Back", Vector2.zero, new Vector2(960f, 640f), Gold, 0.3f);
            Emblem(panel, MenuArt.IconTrophy, new Vector2(0f, 240f), 110f, Gold);
            for (int i = 0; i < 6; i++)
            {
                float y = 130f - i * 70f;
                bool you = i == 0;
                var row = UiKit.Node("Row" + i, panel, new Vector2(0f, y), new Vector2(860f, 58f));
                UiKit.Img("Plate", row, MenuArt.CardBody, you ? new Color(0.16f, 0.14f, 0.09f, 0.9f) : new Color(0.03f, 0.06f, 0.09f, 0.7f), Vector2.zero, new Vector2(860f, 58f), Image.Type.Sliced);
                UiKit.Img("Frame", row, MenuArt.Frame, (you ? Gold : Muted).WithAlpha(you ? 0.7f : 0.15f), Vector2.zero, new Vector2(862f, 60f), Image.Type.Sliced);
                Color medal = i == 0 ? Gold : i == 1 ? new Color(0.8f, 0.87f, 0.92f) : i == 2 ? new Color(0.9f, 0.6f, 0.4f) : Muted;
                MenuUi.Medallion(row, "Rank", new Vector2(-380f, 0f), 42f, medal);
                MenuArt.Label("N", row, (i + 1).ToString(), 22f, Color.white, new Vector2(-380f, 0f), new Vector2(42f, 42f), TextAlignmentOptions.Center, 0f, MenuArt.TextHeavySoft);
                var name = MenuArt.Label("Name", row, you ? "DU" : "? ? ?", 26f, you ? Color.white : Muted.WithAlpha(0.7f), new Vector2(-120f, 0f), new Vector2(420f, 44f), TextAlignmentOptions.Left, 4f);
                var value = MenuArt.Label("Value", row, you ? "STAGE 0" : "—", 26f, you ? Gold : Muted.WithAlpha(0.7f), new Vector2(300f, 0f), new Vector2(240f, 44f), TextAlignmentOptions.Right, 4f);
                if (you) { rankYou = name; rankValue = value; }
            }
            Body(panel, "Die Online-Rangliste kommt später — bis dahin jagst du hier deinen eigenen Rekord.", new Vector2(0f, -280f), new Vector2(820f, 60f), 20f);
        }

        // ------------------------------------------------------------------ friends

        void BuildFriends(RectTransform parent)
        {
            var page = NewPage(parent, MenuPage.Friends, "FREUNDE", "GEMEINSAM SPIELEN", Palette.DashMint);
            var panel = UiKit.Node("Panel", page.Content, new Vector2(0f, 10f), new Vector2(820f, 580f));
            MenuUi.Plate(panel, "Back", Vector2.zero, new Vector2(820f, 580f), Palette.DashMint, 0.3f);
            Emblem(panel, MenuArt.IconFriends, new Vector2(0f, 130f), 180f, Palette.DashMint);
            MenuArt.Label("Empty", panel, "NOCH KEINE FREUNDE", 40f, Color.white, new Vector2(0f, -20f), new Vector2(760f, 56f), TextAlignmentOptions.Center, 8f);
            Body(panel, "Freundesliste, Einladungen und gemeinsame Läufe kommen in einem späteren Update.", new Vector2(0f, -88f), new Vector2(640f, 70f));
            SoonButton(panel, MenuPage.Friends, "invite", new Vector2(0f, -196f), new Vector2(400f, 80f), Palette.DashMint, "EINLADEN", 30f, "ONLINE-FUNKTIONEN KOMMEN BALD");
        }

        // ------------------------------------------------------------------ events

        void BuildEvents(RectTransform parent)
        {
            var page = NewPage(parent, MenuPage.Events, "EVENTS", "BESONDERE LÄUFE", Palette.MonsterGlow);
            string[] names = { "WOCHEN-CHALLENGE", "BOSS-RUSH" };
            string[] lines =
            {
                "Jede Woche ein fester Lauf für alle — wer schafft die meisten Stages?",
                "Alle acht Bosse hintereinander, ohne Wellen dazwischen.",
            };
            Sprite[] icons = { MenuArt.IconEvents, MenuArt.IconStriker };
            Color[] cols = { new Color(0.78f, 0.55f, 1f), Palette.MonsterGlow };
            for (int i = 0; i < 2; i++)
            {
                var card = UiKit.Node("Event" + i, page.Content, new Vector2((i - 0.5f) * 600f, 20f), new Vector2(540f, 560f));
                MenuUi.Plate(card, "Card", Vector2.zero, new Vector2(540f, 560f), cols[i], 0.35f);
                Emblem(card, icons[i], new Vector2(0f, 120f), 170f, cols[i]);
                MenuArt.Label("Name", card, names[i], 34f, Color.white, new Vector2(0f, -20f), new Vector2(520f, 50f), TextAlignmentOptions.Center, 6f);
                Body(card, lines[i], new Vector2(0f, -84f), new Vector2(460f, 70f));
                var timer = UiKit.Node("Timer", card, new Vector2(0f, -148f), new Vector2(240f, 40f));
                UiKit.Img("Pill", timer, UiArt.Pill, new Color(0.02f, 0.05f, 0.08f, 0.85f), Vector2.zero, new Vector2(240f, 40f), Image.Type.Sliced);
                MenuArt.Label("T", timer, "STARTET BALD", 18f, Gold, Vector2.zero, new Vector2(240f, 40f), TextAlignmentOptions.Center, 4f, MenuArt.TextHeavySoft);
                SoonButton(card, MenuPage.Events, "event" + i, new Vector2(0f, -218f), new Vector2(320f, 72f), cols[i], "MITMACHEN", 26f, "EVENTS STARTEN BALD");
                MenuUi.Tag(card, "BALD", new Vector2(208f, 250f), Gold);
            }
        }

        // ------------------------------------------------------------------ info

        void BuildInfo(RectTransform parent)
        {
            var page = NewPage(parent, MenuPage.Info, "INFO", "STEUERUNG UND TIPPS", MenuArt.Accent);
            var left = UiKit.Node("Controls", page.Content, new Vector2(-370f, 0f), new Vector2(680f, 700f));
            MenuUi.Plate(left, "Back", Vector2.zero, new Vector2(680f, 700f), MenuArt.Accent, 0.3f);
            MenuArt.Label("Head", left, "STEUERUNG", 30f, MenuArt.Accent, new Vector2(0f, 300f), new Vector2(600f, 44f), TextAlignmentOptions.Center, 10f);
            float y = 232f;
            foreach (var a in KeyBindings.All)
            {
                MenuArt.Label("Action", left, KeyBindings.ActionName(a), 20f, Color.white, new Vector2(-120f, y), new Vector2(360f, 40f), TextAlignmentOptions.Left, 3f, MenuArt.TextHeavySoft);
                var plate = UiKit.Img("Key", left, UiArt.Pill, new Color(0.08f, 0.14f, 0.2f, 0.95f), new Vector2(190f, y), new Vector2(210f, 38f), Image.Type.Sliced);
                UiKit.Img("KeyRim", left, UiArt.Pill, Color.white.WithAlpha(0.12f), new Vector2(190f, y), new Vector2(212f, 40f), Image.Type.Sliced).transform.SetSiblingIndex(plate.transform.GetSiblingIndex());
                var key = MenuArt.Label("K", plate.transform, "", 18f, new Color(0.85f, 0.95f, 1f), Vector2.zero, new Vector2(200f, 38f), TextAlignmentOptions.Center, 3f, MenuArt.TextHeavySoft);
                keyRows.Add((key, a));
                y -= 46f;
            }
            Body(left, "LINKSKLICK HALTEN = DAUERFEUER  ·  ESC PAUSE", new Vector2(0f, -306f), new Vector2(620f, 36f), 18f);

            var right = UiKit.Node("About", page.Content, new Vector2(370f, 0f), new Vector2(680f, 700f));
            MenuUi.Plate(right, "Back", Vector2.zero, new Vector2(680f, 700f), Gold, 0.3f);
            MenuArt.Label("Head", right, "SO GEHT'S", 30f, Gold, new Vector2(0f, 300f), new Vector2(600f, 44f), TextAlignmentOptions.Center, 10f);
            string[] tips =
            {
                "Schieß dich mit dem Ball durch Wellen von Monstern — jede Stage endet mit einem Boss.",
                "Nach jeder zweiten Runde wählst du ein Upgrade, nach jedem Boss eine neue Fähigkeit (höchstens vier).",
                "Deine Upgrades siehst du am Ball: mehr Schaden macht ihn größer, Feuer, Frost und Blitze färben ihn.",
                "Rote Markierungen am Boden zeigen, wohin ein Boss springt oder stürmt.",
                "Hochhalten heilt dich — aber nur, wenn du den Ball im richtigen Takt triffst.",
            };
            Sprite[] icons = { MenuArt.IconStriker, MenuArt.IconStar, MenuArt.IconCoin, MenuArt.IconDefender, MenuArt.IconSkiller };
            float ty = 206f;
            for (int i = 0; i < tips.Length; i++)
            {
                bool coin = icons[i] == MenuArt.IconCoin;
                UiKit.Img("TipRing", right, MenuArt.RoundFrame, Gold.WithAlpha(0.5f), new Vector2(-282f, ty), new Vector2(56f, 56f));
                var ic = UiKit.Img("TipIcon", right, icons[i], coin ? Color.white : Color.Lerp(Gold, Color.white, 0.5f), new Vector2(-282f, ty), new Vector2(32f, 32f));
                ic.preserveAspect = true;
                Body(right, tips[i], new Vector2(40f, ty), new Vector2(540f, 90f), 20f, TextAlignmentOptions.Left);
                ty -= 100f;
            }
            Body(right, "SOCCERFIGHT  ·  Grafik und Animation komplett im Code erzeugt  ·  F1 FPS  ·  F2 VSYNC  ·  F3 DEV", new Vector2(0f, -312f), new Vector2(620f, 40f), 15f);
            RefreshKeys();
        }

        void RefreshKeys()
        {
            foreach (var (key, action) in keyRows) key.text = KeyBindings.DisplayName(action);
        }

        // ------------------------------------------------------------------ settings

        void BuildSettings(RectTransform parent)
        {
            var page = NewPage(parent, MenuPage.Settings, "OPTIONEN", "ANZEIGE · EFFEKTE · TASTEN", MenuArt.Accent);
            var holder = UiKit.Node("Holder", page.Content, new Vector2(0f, -30f), SettingsPanel.Size);
            // the card is cut down to the rows it holds (its own title is replaced by the page heading)
            Vector2 cardSize = new Vector2(SettingsPanel.Size.x, 660f), cardPos = new Vector2(0f, 22f);
            Settings = new SettingsPanel();
            Settings.Build(holder, false);
            Recolor(Settings.Root, "Glass", MenuArt.Glass, cardPos, cardSize);
            Recolor(Settings.Root, "Border", MenuArt.Accent.WithAlpha(0.3f), cardPos, cardSize + new Vector2(3f, 3f));
            Recolor(Settings.Root, "Shadow", new Color(0f, 0.01f, 0.03f, 0.45f), cardPos + new Vector2(0f, -12f), cardSize * 1.1f);
            Recolor(Settings.Root, "Top Light", MenuArt.Accent.WithAlpha(0.55f), cardPos + new Vector2(0f, cardSize.y * 0.5f - 1f), new Vector2(cardSize.x * 0.7f, 2f));
            var title = Settings.Root.Find("Title");
            if (title != null) title.gameObject.SetActive(false);   // the page already has a heading
        }

        static void Recolor(Transform root, string child, Color c, Vector2 pos, Vector2 size)
        {
            var t = root.Find(child);
            if (t == null || !t.TryGetComponent<Image>(out var img)) return;
            img.color = c;
            img.rectTransform.anchoredPosition = pos;
            img.rectTransform.sizeDelta = size;
        }

        // ------------------------------------------------------------------ update

        public void Refresh()
        {
            if (rankYou != null)
            {
                rankYou.text = "DU  (" + Characters.Current.Name + ")";
                int best = RunState.BestStage;
                rankValue.text = best > 0 ? "STAGE " + best : "—";
            }
            RefreshKeys();
        }

        public void Update(float udt)
        {
            time += udt;
            foreach (var p in Pages) p.Update(udt);
            for (int i = 0; i < bobbers.Count; i++)
            {
                var b = bobbers[i];
                b.localScale = Vector3.one * (1f + 0.025f * Mathf.Sin(time * 1.8f + i * 1.3f));
            }
            foreach (var (img, color, phase) in glows)
                img.color = color.WithAlpha(0.16f + 0.08f * Mathf.Sin(time * 1.4f + phase));
            Settings.Update(udt);
        }
    }
}
