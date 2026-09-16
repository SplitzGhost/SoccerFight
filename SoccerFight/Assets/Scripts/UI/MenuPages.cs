using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoccerFight
{
    /// <summary>
    /// Frame every title-screen sub page shares: the landscape dimmed under a slowly scrolling
    /// ball pattern, a back button in the top-left corner and a big title.
    /// </summary>
    public sealed class SubPage
    {
        public readonly int Id;
        public readonly RectTransform Root, Content;
        public readonly CanvasGroup Group;
        public readonly MenuTarget Back;
        public float T, Vel;
        readonly RawImage pattern;
        readonly RectTransform title;

        public SubPage(RectTransform parent, int id, string heading, Color accent, System.Action<MenuTarget> register, System.Action back)
        {
            Id = id;
            Root = UiKit.Node("Page " + heading, parent, Vector2.zero, Vector2.zero);
            MenuUi.Stretch(Root);
            Group = Root.gameObject.AddComponent<CanvasGroup>();

            MenuUi.Stretch(UiKit.Img("Dim", Root, null, new Color(0.07f, 0.06f, 0.24f, 0.74f), Vector2.zero, Vector2.zero).rectTransform);
            pattern = new GameObject("Pattern", typeof(RectTransform)).AddComponent<RawImage>();
            pattern.rectTransform.SetParent(Root, false);
            MenuUi.Stretch(pattern.rectTransform);
            pattern.texture = MenuArt.BallPattern;
            pattern.color = Color.white.WithAlpha(0.05f);
            pattern.raycastTarget = false;

            var band = UiKit.Img("TopBand", Root, null, new Color(0.04f, 0.03f, 0.14f, 0.55f), Vector2.zero, Vector2.zero);
            band.rectTransform.anchorMin = new Vector2(0f, 1f);
            band.rectTransform.anchorMax = new Vector2(1f, 1f);
            band.rectTransform.pivot = new Vector2(0.5f, 1f);
            band.rectTransform.sizeDelta = new Vector2(0f, 150f);
            var bandLine = UiKit.Img("BandLine", Root, null, accent.WithAlpha(0.8f), Vector2.zero, Vector2.zero);
            bandLine.rectTransform.anchorMin = new Vector2(0f, 1f);
            bandLine.rectTransform.anchorMax = new Vector2(1f, 1f);
            bandLine.rectTransform.anchoredPosition = new Vector2(0f, -150f);
            bandLine.rectTransform.sizeDelta = new Vector2(0f, 5f);

            var titleText = MenuArt.Label("Title", Root, heading, 70f, Color.white, Vector2.zero, new Vector2(1000f, 100f), TextAlignmentOptions.Center, 4f);
            title = titleText.rectTransform;
            MenuUi.Pin(title, new Vector2(0.5f, 1f), new Vector2(0f, -76f));

            var holder = UiKit.Node("BackHolder", Root, Vector2.zero, new Vector2(130f, 100f));
            MenuUi.Pin(holder, new Vector2(0f, 1f), new Vector2(118f, -74f));
            var button = new ChunkButton(holder, "Back", Vector2.zero, new Vector2(124f, 96f), new Color(0.32f, 0.56f, 1f), null, 0f, MenuArt.IconBack, 62f);
            Back = new MenuTarget { Id = "back" + id, Root = button.Root, Size = button.Size, Page = id, Action = back, Button = button, Accent = accent };
            register(Back);

            Content = UiKit.Node("Content", Root, new Vector2(0f, -40f), new Vector2(1600f, 900f));
        }

        public void Update(float udt)
        {
            Rect r = Root.rect;
            var uv = pattern.uvRect;
            uv.width = r.width / 180f;
            uv.height = r.height / 180f;
            uv.x = Mathf.Repeat(uv.x + udt * 0.04f, 1f);
            uv.y = Mathf.Repeat(uv.y + udt * 0.025f, 1f);
            pattern.uvRect = uv;
            // the whole page fits the window: content shrinks on small canvases
            float s = Mathf.Min(1f, Mathf.Min(r.width / 1700f, (r.height - 60f) / 1000f));
            Content.localScale = new Vector3(s, s, 1f);
            Content.anchoredPosition = new Vector2(0f, -40f * s);
            title.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, T);
        }
    }

    /// <summary>The placeholder pages (shop, ranking, friends, events) and the info page.</summary>
    public sealed class MenuPages
    {
        public readonly List<SubPage> Pages = new List<SubPage>();
        readonly List<RectTransform> bobbers = new List<RectTransform>();
        readonly List<RectTransform> stickers = new List<RectTransform>();
        TextMeshProUGUI rankYou, rankValue;
        readonly List<(TextMeshProUGUI key, GameAction action)> keyRows = new List<(TextMeshProUGUI, GameAction)>();
        float time;

        public SettingsPanel Settings { get; private set; }

        System.Action<MenuTarget> register;
        System.Action back;

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

        SubPage NewPage(RectTransform parent, int id, string title, Color accent)
        {
            var p = new SubPage(parent, id, title, accent, register, back);
            Pages.Add(p);
            return p;
        }

        ChunkButton SoonButton(Transform parent, int page, string id, Vector2 pos, Vector2 size, Color color, string label, float font, string soon)
        {
            var b = new ChunkButton(parent, id, pos, size, color, label, font, MenuArt.IconLock, size.y * 0.55f);
            b.IconLeft(22f);
            register(new MenuTarget { Id = id, Root = b.Root, Size = size, Page = page, Button = b, Soon = soon, Accent = color });
            return b;
        }

        TextMeshProUGUI Body(Transform parent, string text, Vector2 pos, Vector2 box, float size = 24f, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var t = MenuArt.Label("Text", parent, text, size, new Color(0.88f, 0.9f, 1f), pos, box, align, 0.5f, MenuArt.TextHeavySoft);
            t.fontStyle = FontStyles.Normal;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        // ------------------------------------------------------------------ shop

        void BuildShop(RectTransform parent)
        {
            var page = NewPage(parent, MenuPage.Shop, "SHOP", new Color(1f, 0.4f, 0.75f));
            string[] names = { "STARTERPAKET", "GOLDENER BALL", "TRIKOT-SET", "EDELSTEINE" };
            string[] lines = { "Ein Schwung Münzen und ein Trikot für den Anfang.", "Ein Ball, der golden leuchtet und funkelt.", "Neue Farben für alle drei Spieler.", "Die seltene Währung für besondere Dinge." };
            Sprite[] icons = { MenuArt.IconShop, MenuArt.IconCoin, MenuArt.IconStar, MenuArt.IconGem };
            Color[] cols = { new Color(0.55f, 0.36f, 0.95f), new Color(1f, 0.62f, 0.18f), new Color(0.95f, 0.32f, 0.38f), new Color(0.98f, 0.36f, 0.72f) };
            for (int i = 0; i < names.Length; i++)
            {
                var tile = UiKit.Node("Offer" + i, page.Content, new Vector2((i - 1.5f) * 345f, 40f), new Vector2(310f, 470f));
                MenuUi.Plate(tile, "Card", Vector2.zero, new Vector2(310f, 470f), cols[i]);
                var stripes = new GameObject("Stripes", typeof(RectTransform)).AddComponent<RawImage>();
                stripes.rectTransform.SetParent(tile, false);
                stripes.rectTransform.anchoredPosition = new Vector2(0f, 90f);
                stripes.rectTransform.sizeDelta = new Vector2(290f, 250f);
                stripes.texture = MenuArt.Stripes;
                stripes.uvRect = new Rect(0f, 0f, 290f / 64f, 250f / 64f);
                stripes.color = Color.white.WithAlpha(0.12f);
                stripes.raycastTarget = false;
                UiKit.Img("Light", tile, UiArt.Glow, Color.white.WithAlpha(0.35f), new Vector2(0f, 90f), new Vector2(300f, 300f));
                var icon = UiKit.Img("Icon", tile, icons[i], Color.white, new Vector2(0f, 95f), new Vector2(170f, 170f));
                bobbers.Add(icon.rectTransform);
                MenuArt.Label("Name", tile, names[i], 30f, Color.white, new Vector2(0f, -60f), new Vector2(300f, 44f));
                Body(tile, lines[i], new Vector2(0f, -112f), new Vector2(270f, 60f), 19f);
                SoonButton(tile, MenuPage.Shop, "buy" + i, new Vector2(0f, -188f), new Vector2(250f, 72f), new Color(0.35f, 0.8f, 0.35f), "BALD", 32f, "DER SHOP ÖFFNET BALD");
                if (i == 0) stickers.Add(MenuUi.Sticker(tile, "NEU", new Vector2(120f, 215f), 96f, new Color(1f, 0.3f, 0.35f)));
            }
            MenuUi.Ribbon(page.Content, "Soon", "DER SHOP ÖFFNET IN EINEM SPÄTEREN UPDATE", new Vector2(0f, -305f), 900f, new Color(0.85f, 0.2f, 0.55f), 30f);
        }

        // ------------------------------------------------------------------ ranking

        void BuildRanking(RectTransform parent)
        {
            var page = NewPage(parent, MenuPage.Ranking, "RANGLISTE", new Color(1f, 0.75f, 0.2f));
            var panel = UiKit.Node("Panel", page.Content, new Vector2(0f, 10f), new Vector2(960f, 660f));
            MenuUi.Plate(panel, "Back", Vector2.zero, new Vector2(960f, 660f), new Color(0.2f, 0.25f, 0.6f));
            UiKit.Img("Trophy", panel, MenuArt.IconTrophy, Color.white, new Vector2(-250f, 260f), new Vector2(80f, 80f));
            MenuArt.Label("Head", panel, "BESTE STAGES", 46f, Palette.Gold, new Vector2(40f, 262f), new Vector2(520f, 60f));
            for (int i = 0; i < 6; i++)
            {
                float y = 170f - i * 76f;
                bool you = i == 0;
                var row = UiKit.Node("Row" + i, panel, new Vector2(0f, y), new Vector2(860f, 64f));
                MenuUi.Plate(row, "Plate", Vector2.zero, new Vector2(860f, 64f), you ? new Color(1f, 0.72f, 0.24f) : new Color(0.14f, 0.17f, 0.44f), 4f);
                var medal = MenuUi.Medallion(row, "Rank", new Vector2(-380f, 0f), 50f, i == 0 ? new Color(1f, 0.85f, 0.3f) : i == 1 ? new Color(0.8f, 0.85f, 0.95f) : i == 2 ? new Color(0.9f, 0.55f, 0.3f) : new Color(0.35f, 0.4f, 0.7f));
                MenuArt.Label("N", medal.transform, (i + 1).ToString(), 28f, Color.white, Vector2.zero, new Vector2(50f, 50f), TextAlignmentOptions.Center, 0f, MenuArt.TextHeavySoft);
                var name = MenuArt.Label("Name", row, you ? "DU" : "? ? ?", 30f, you ? Color.white : new Color(0.6f, 0.65f, 0.9f), new Vector2(-120f, 0f), new Vector2(420f, 50f), TextAlignmentOptions.Left, 2f);
                var value = MenuArt.Label("Value", row, you ? "STAGE 0" : "—", 30f, you ? Color.white : new Color(0.6f, 0.65f, 0.9f), new Vector2(300f, 0f), new Vector2(240f, 50f), TextAlignmentOptions.Right, 2f);
                if (you) { rankYou = name; rankValue = value; }
            }
            Body(panel, "Die Online-Rangliste kommt später — bis dahin jagst du hier deinen eigenen Rekord.", new Vector2(0f, -290f), new Vector2(820f, 60f), 21f);
        }

        // ------------------------------------------------------------------ friends

        void BuildFriends(RectTransform parent)
        {
            var page = NewPage(parent, MenuPage.Friends, "FREUNDE", new Color(0.35f, 0.9f, 0.5f));
            var panel = UiKit.Node("Panel", page.Content, new Vector2(0f, 20f), new Vector2(820f, 600f));
            MenuUi.Plate(panel, "Back", Vector2.zero, new Vector2(820f, 600f), new Color(0.16f, 0.46f, 0.42f));
            UiKit.Img("Light", panel, UiArt.Glow, Color.white.WithAlpha(0.25f), new Vector2(0f, 130f), new Vector2(420f, 420f));
            var icon = UiKit.Img("Icon", panel, MenuArt.IconFriends, Color.white, new Vector2(0f, 130f), new Vector2(210f, 210f));
            bobbers.Add(icon.rectTransform);
            MenuArt.Label("Empty", panel, "NOCH KEINE FREUNDE", 48f, Color.white, new Vector2(0f, -20f), new Vector2(760f, 60f));
            Body(panel, "Freundesliste, Einladungen und gemeinsame Läufe kommen in einem späteren Update.", new Vector2(0f, -90f), new Vector2(640f, 70f));
            SoonButton(panel, MenuPage.Friends, "invite", new Vector2(0f, -200f), new Vector2(420f, 88f), new Color(0.35f, 0.8f, 0.35f), "EINLADEN", 38f, "ONLINE-FUNKTIONEN KOMMEN BALD");
        }

        // ------------------------------------------------------------------ events

        void BuildEvents(RectTransform parent)
        {
            var page = NewPage(parent, MenuPage.Events, "EVENTS", new Color(0.75f, 0.5f, 1f));
            string[] names = { "WOCHEN-CHALLENGE", "BOSS-RUSH" };
            string[] lines =
            {
                "Jede Woche ein fester Lauf für alle — wer schafft die meisten Stages?",
                "Alle acht Bosse hintereinander, ohne Wellen dazwischen.",
            };
            Sprite[] icons = { MenuArt.IconEvents, MenuArt.IconStriker };
            Color[] cols = { new Color(0.52f, 0.34f, 0.92f), new Color(0.92f, 0.3f, 0.36f) };
            for (int i = 0; i < 2; i++)
            {
                var card = UiKit.Node("Event" + i, page.Content, new Vector2((i - 0.5f) * 600f, 30f), new Vector2(540f, 560f));
                MenuUi.Plate(card, "Card", Vector2.zero, new Vector2(540f, 560f), cols[i]);
                UiKit.Img("Light", card, UiArt.Glow, Color.white.WithAlpha(0.3f), new Vector2(0f, 110f), new Vector2(380f, 380f));
                var icon = UiKit.Img("Icon", card, icons[i], Color.white, new Vector2(0f, 120f), new Vector2(190f, 190f));
                bobbers.Add(icon.rectTransform);
                MenuArt.Label("Name", card, names[i], 40f, Color.white, new Vector2(0f, -20f), new Vector2(520f, 56f));
                Body(card, lines[i], new Vector2(0f, -85f), new Vector2(460f, 70f));
                var timer = MenuUi.Plate(card, "Timer", new Vector2(0f, -150f), new Vector2(260f, 46f), new Color(0.08f, 0.06f, 0.2f), 3f);
                MenuArt.Label("T", timer.transform, "STARTET BALD", 22f, Palette.Gold, Vector2.zero, new Vector2(260f, 46f), TextAlignmentOptions.Center, 1f, MenuArt.TextHeavySoft);
                SoonButton(card, MenuPage.Events, "event" + i, new Vector2(0f, -218f), new Vector2(320f, 76f), new Color(0.35f, 0.8f, 0.35f), "MITMACHEN", 30f, "EVENTS STARTEN BALD");
                stickers.Add(MenuUi.Sticker(card, "BALD", new Vector2(215f, 225f), 110f, new Color(1f, 0.55f, 0.15f)));
            }
        }

        // ------------------------------------------------------------------ info

        void BuildInfo(RectTransform parent)
        {
            var page = NewPage(parent, MenuPage.Info, "INFO", new Color(0.35f, 0.8f, 1f));
            var left = UiKit.Node("Controls", page.Content, new Vector2(-370f, 10f), new Vector2(680f, 700f));
            MenuUi.Plate(left, "Back", Vector2.zero, new Vector2(680f, 700f), new Color(0.18f, 0.28f, 0.62f));
            MenuArt.Label("Head", left, "STEUERUNG", 44f, new Color(0.6f, 0.9f, 1f), new Vector2(0f, 300f), new Vector2(600f, 60f));
            float y = 225f;
            foreach (var a in KeyBindings.All)
            {
                MenuArt.Label("Action", left, KeyBindings.ActionName(a), 22f, Color.white, new Vector2(-120f, y), new Vector2(360f, 40f), TextAlignmentOptions.Left, 1f, MenuArt.TextHeavySoft);
                var plate = MenuUi.Plate(left, "Key", new Vector2(190f, y), new Vector2(210f, 40f), new Color(0.9f, 0.92f, 1f), 3f);
                var key = MenuArt.Label("K", plate.transform, "", 20f, MenuArt.Ink, Vector2.zero, new Vector2(200f, 40f), TextAlignmentOptions.Center, 1f, MenuArt.TextPlate);
                keyRows.Add((key, a));
                y -= 46f;
            }
            Body(left, "LINKSKLICK HALTEN = DAUERFEUER  ·  ESC PAUSE", new Vector2(0f, -310f), new Vector2(620f, 36f), 20f);

            var right = UiKit.Node("About", page.Content, new Vector2(370f, 10f), new Vector2(680f, 700f));
            MenuUi.Plate(right, "Back", Vector2.zero, new Vector2(680f, 700f), new Color(0.35f, 0.22f, 0.6f));
            MenuArt.Label("Head", right, "SO GEHT'S", 44f, new Color(1f, 0.8f, 0.4f), new Vector2(0f, 300f), new Vector2(600f, 60f));
            string[] tips =
            {
                "Schieß dich mit dem Ball durch Wellen von Monstern — jede Stage endet mit einem Boss.",
                "Nach jeder zweiten Runde wählst du ein Upgrade, nach jedem Boss eine neue Fähigkeit (höchstens vier).",
                "Deine Upgrades siehst du am Ball: mehr Schaden macht ihn größer, Feuer, Frost und Blitze färben ihn.",
                "Rote Markierungen am Boden zeigen, wohin ein Boss springt oder stürmt.",
                "Hochhalten heilt dich — aber nur, wenn du den Ball im richtigen Takt triffst.",
            };
            Sprite[] icons = { MenuArt.IconStriker, MenuArt.IconStar, MenuArt.IconCoin, MenuArt.IconDefender, MenuArt.IconSkiller };
            float ty = 210f;
            for (int i = 0; i < tips.Length; i++)
            {
                UiKit.Img("TipIcon", right, icons[i], Color.white, new Vector2(-280f, ty), new Vector2(54f, 54f));
                Body(right, tips[i], new Vector2(40f, ty), new Vector2(540f, 90f), 22f, TextAlignmentOptions.Left);
                ty -= 102f;
            }
            Body(right, "SOCCERFIGHT  ·  Grafik und Animation komplett im Code erzeugt  ·  F1 FPS  ·  F2 VSYNC  ·  F3 DEV", new Vector2(0f, -320f), new Vector2(620f, 40f), 17f);
            RefreshKeys();
        }

        void RefreshKeys()
        {
            foreach (var (key, action) in keyRows) key.text = KeyBindings.DisplayName(action);
        }

        // ------------------------------------------------------------------ settings

        void BuildSettings(RectTransform parent)
        {
            var page = NewPage(parent, MenuPage.Settings, "OPTIONEN", new Color(0.4f, 0.65f, 1f));
            var holder = UiKit.Node("Holder", page.Content, new Vector2(0f, -30f), SettingsPanel.Size);
            // the card is cut down to the rows it holds (its own title is replaced by the page heading)
            Vector2 cardSize = new Vector2(SettingsPanel.Size.x, 660f), cardPos = new Vector2(0f, 22f);
            UiKit.Img("Keyline", holder, MenuArt.Edge, MenuArt.Ink, cardPos + new Vector2(0f, -4f), cardSize + new Vector2(16f, 24f), Image.Type.Sliced);
            Settings = new SettingsPanel();
            Settings.Build(holder, false);
            // the shared settings card, recoloured to the title screen's palette
            Recolor(Settings.Root, "Glass", new Color(0.16f, 0.14f, 0.38f, 1f), cardPos, cardSize);
            Recolor(Settings.Root, "Border", new Color(0.45f, 0.5f, 1f, 0.35f), cardPos, cardSize + new Vector2(3f, 3f));
            Recolor(Settings.Root, "Shadow", new Color(0f, 0f, 0f, 0f), cardPos, cardSize);
            Recolor(Settings.Root, "Top Light", new Color(0.5f, 0.7f, 1f, 0.5f), cardPos + new Vector2(0f, cardSize.y * 0.5f - 1f), new Vector2(cardSize.x * 0.7f, 2f));
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
                b.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 1.6f + i) * 4f);
                b.localScale = Vector3.one * (1f + 0.03f * Mathf.Sin(time * 2.4f + i * 1.3f));
            }
            for (int i = 0; i < stickers.Count; i++)
            {
                float s = 1f + 0.06f * Mathf.Sin(time * 4f + i);
                stickers[i].localScale = new Vector3(s, s, 1f);
                stickers[i].localRotation = Quaternion.Euler(0f, 0f, -8f + Mathf.Sin(time * 2f + i) * 5f);
            }
            Settings.Update(udt);
        }
    }
}
