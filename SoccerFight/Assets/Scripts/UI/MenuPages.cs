using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoccerFight
{
    /// <summary>
    /// Frame every title-screen sub page shares. Pages reached from the top bar are named by their
    /// tab, so they only carry a small accent overline under the bar; the scene behind is dimmed to
    /// night blue. The first-launch pages have no bar: they keep a big tracked title instead.
    /// </summary>
    public sealed class SubPage
    {
        /// <summary>Height of the title screen's top bar and bottom strip (canvas px) — pages stay between them.</summary>
        public const float TopBar = 108f, BottomBar = 46f;
        /// <summary>So weit wächst eine Seite höchstens über ihre 16:9-Größe, wenn der Bildschirm höher ist.</summary>
        const float MaxGrow = 1.3f;

        public readonly int Id;
        public readonly RectTransform Root, Content;
        public readonly CanvasGroup Group;
        public readonly bool InBar;
        public float T, Vel;
        readonly RectTransform title;
        public Vector2 ArtworkSize;

        /// <param name="back">null for the first-launch screens: they stand alone, without the top bar.</param>
        public SubPage(RectTransform parent, int id, string heading, string overline, Color accent, System.Action<MenuTarget> register, System.Action back, bool artwork = false)
        {
            Id = id;
            InBar = back != null;
            Root = UiKit.Node("Page " + heading, parent, Vector2.zero, Vector2.zero);
            MenuUi.Stretch(Root);
            Group = Root.gameObject.AddComponent<CanvasGroup>();

            if (artwork)
            {
                // Originalansicht ohne den gemeinsamen dunklen Seitenrahmen.
                ArtworkSize = new Vector2(1672f, 941f);
                Content = UiKit.Node("Originalansicht", Root, Vector2.zero, ArtworkSize);
                title = null;
                return;
            }

            MenuUi.Stretch(UiKit.Img("Dim", Root, null, new Color(0.01f, 0.03f, 0.05f, InBar ? 0.66f : 0.72f), Vector2.zero, Vector2.zero).rectTransform);
            var vignette = UiKit.Img("Vignette", Root, MenuArt.Vignette, new Color(0f, 0f, 0f, 0.55f), Vector2.zero, Vector2.zero);
            MenuUi.Stretch(vignette.rectTransform);

            title = UiKit.Node("Heading", Root, Vector2.zero, new Vector2(1200f, 150f));
            if (InBar)
            {
                MenuUi.Pin(title, new Vector2(0.5f, 1f), new Vector2(0f, -TopBar - 34f));
                MenuArt.Label("Overline", title, overline, 19f, accent, new Vector2(0f, 0f), new Vector2(1100f, 30f), TextAlignmentOptions.Center, 8f, MenuArt.TextHeavySoft);
                UiKit.Img("Line", title, UiArt.LineFade, accent.WithAlpha(0.4f), new Vector2(0f, -22f), new Vector2(620f, 2f));
            }
            else
            {
                MenuUi.Pin(title, new Vector2(0.5f, 0.5f), new Vector2(0f, 448f));
                MenuArt.Label("Overline", title, overline, 22f, accent, new Vector2(0f, 46f), new Vector2(1000f, 32f), TextAlignmentOptions.Center, 9f, MenuArt.TextHeavySoft);
                MenuArt.Label("Title", title, heading, 60f, Color.white, new Vector2(0f, -4f), new Vector2(1200f, 80f), TextAlignmentOptions.Center, 18f);
                UiKit.Img("Line", title, UiArt.LineFade, accent.WithAlpha(0.5f), new Vector2(0f, -54f), new Vector2(760f, 2f));
            }

            Content = UiKit.Node("Content", Root, new Vector2(0f, -40f), new Vector2(1600f, 900f));
        }

        public void Update(float udt)
        {
            Rect r = Root.rect;
            if (ArtworkSize != Vector2.zero)
            {
                float scale = Mathf.Min(r.width / ArtworkSize.x, r.height / ArtworkSize.y);
                Content.localScale = Vector3.one * scale;
                Content.anchoredPosition = Vector2.zero;
                return;
            }
            if (InBar)
            {
                // the page content (its top edge sits ~400 above its centre, the bottom ~470 below)
                // fits between the overline and the bottom strip
                // Auf hohen Bildschirmen (4:3, Hochkant) wächst die Seite mit und steht mittig im freien Platz.
                float room = r.height - TopBar - 62f - BottomBar - 12f;
                float s = Mathf.Min(MaxGrow, Mathf.Min(r.width / 1700f, room / 870f));
                float spare = Mathf.Max(0f, room - 870f * s) * 0.5f;
                float top = r.height * 0.5f - TopBar - 62f - spare;
                Content.localScale = new Vector3(s, s, 1f);
                Content.anchoredPosition = new Vector2(0f, top - 400f * s);
                title.localScale = Vector3.one * Mathf.Min(r.width / 1300f, Mathf.Clamp(s, 1f, MaxGrow));
                title.anchoredPosition = new Vector2(0f, -TopBar - 34f - spare);
                return;
            }
            // the whole page fits the window: it shrinks on small canvases, grows on tall ones,
            // and its heading stays right above it
            float k = Mathf.Min(MaxGrow, Mathf.Min(r.width / 1700f, (r.height - 60f) / 1000f));
            Content.localScale = new Vector3(k, k, 1f);
            Content.anchoredPosition = new Vector2(0f, -40f * k);
            title.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, T) * k;
            title.anchoredPosition = new Vector2(0f, 448f * k);
        }
    }

    /// <summary>The placeholder pages (ranking, friends, events), the info page and the settings page.</summary>
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
            BuildRanking(parent);
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
            var b = new ChunkButton(parent, id, pos, size, color, label, font, MenuArt.IconLock, size.y * 0.36f) { PlainPlate = true };
            b.IconLeft(46f);
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

        // ------------------------------------------------------------------ ranking

        void BuildRanking(RectTransform parent)
        {
            var page = NewPage(parent, MenuPage.Ranking, "RANGLISTE", "BESTE STAGES", Gold);
            var panel = UiKit.Node("Panel", page.Content, new Vector2(0f, 0f), new Vector2(960f, 640f));
            MenuUi.Plate(panel, "Back", Vector2.zero, new Vector2(960f, 640f), Gold, 0.3f);
            Emblem(panel, MenuArt.IconTrophy, new Vector2(0f, 236f), 110f, Gold, true);
            for (int i = 0; i < 6; i++)
            {
                float y = 136f - i * 66f;
                bool you = i == 0;
                var row = UiKit.Node("Row" + i, panel, new Vector2(0f, y), new Vector2(860f, 58f));
                UiKit.Img("Frame", row, UiArt.Pill, (you ? Gold : Muted).WithAlpha(you ? 0.55f : 0.14f), Vector2.zero, new Vector2(862f, 60f), Image.Type.Sliced);
                UiKit.Img("Plate", row, UiArt.Pill, you ? new Color(0.13f, 0.11f, 0.07f, 0.95f) : new Color(0.03f, 0.06f, 0.09f, 0.9f), Vector2.zero, new Vector2(858f, 56f), Image.Type.Sliced);
                Color medal = i == 0 ? Gold : i == 1 ? new Color(0.8f, 0.87f, 0.92f) : i == 2 ? new Color(0.9f, 0.6f, 0.4f) : Muted;
                MenuUi.Medallion(row, "Rank", new Vector2(-380f, 0f), 42f, medal);
                MenuArt.Label("N", row, (i + 1).ToString(), 22f, Color.white, new Vector2(-380f, 0f), new Vector2(42f, 42f), TextAlignmentOptions.Center, 0f, MenuArt.TextHeavySoft);
                var name = MenuArt.Label("Name", row, you ? "DU" : "? ? ?", 26f, you ? Color.white : Muted.WithAlpha(0.7f), new Vector2(-120f, 0f), new Vector2(420f, 44f), TextAlignmentOptions.Left, 4f);
                var value = MenuArt.Label("Value", row, you ? "STAGE 0" : "—", 26f, you ? Gold : Muted.WithAlpha(0.7f), new Vector2(300f, 0f), new Vector2(240f, 44f), TextAlignmentOptions.Right, 4f);
                if (you) { rankYou = name; rankValue = value; }
            }
            Body(panel, "Die Online-Rangliste kommt später — bis dahin jagst du hier deinen eigenen Rekord.", new Vector2(0f, -258f), new Vector2(820f, 40f), 19f);
        }

        // ------------------------------------------------------------------ friends

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
                Emblem(card, icons[i], new Vector2(0f, 120f), 170f, cols[i], true);
                MenuArt.Label("Name", card, names[i], 34f, Color.white, new Vector2(0f, -20f), new Vector2(520f, 50f), TextAlignmentOptions.Center, 6f);
                Body(card, lines[i], new Vector2(0f, -84f), new Vector2(460f, 70f));
                var timer = UiKit.Node("Timer", card, new Vector2(0f, -148f), new Vector2(240f, 40f));
                UiKit.Img("Pill", timer, UiArt.Pill, new Color(0.02f, 0.05f, 0.08f, 0.85f), Vector2.zero, new Vector2(240f, 40f), Image.Type.Sliced);
                MenuArt.Label("T", timer, "STARTET BALD", 18f, Gold, Vector2.zero, new Vector2(240f, 40f), TextAlignmentOptions.Center, 4f, MenuArt.TextHeavySoft);
                SoonButton(card, MenuPage.Events, "event" + i, new Vector2(0f, -218f), new Vector2(320f, 72f), cols[i], "MITMACHEN", 26f, "EVENTS STARTEN BALD");
            }
        }

        // ------------------------------------------------------------------ info

        void BuildInfo(RectTransform parent)
        {
            var page = NewPage(parent, MenuPage.Info, "INFO", "STEUERUNG UND TIPPS", MenuArt.Accent);
            var left = UiKit.Node("Controls", page.Content, new Vector2(-370f, 0f), new Vector2(680f, 700f));
            MenuUi.Plate(left, "Back", Vector2.zero, new Vector2(680f, 700f), MenuArt.Accent, 0.3f);
            MenuArt.Label("Head", left, "STEUERUNG", 30f, MenuArt.Accent, new Vector2(0f, 284f), new Vector2(600f, 44f), TextAlignmentOptions.Center, 10f);
            float y = 236f;
            foreach (var a in KeyBindings.All)
            {
                MenuArt.Label("Action", left, KeyBindings.ActionName(a), 20f, Color.white, new Vector2(-116f, y), new Vector2(360f, 40f), TextAlignmentOptions.Left, 3f, MenuArt.TextHeavySoft);
                var plate = UiKit.Img("Key", left, UiArt.Pill, new Color(0.08f, 0.14f, 0.2f, 0.95f), new Vector2(180f, y), new Vector2(210f, 38f), Image.Type.Sliced);
                UiKit.Img("KeyRim", left, UiArt.Pill, Color.white.WithAlpha(0.12f), new Vector2(180f, y), new Vector2(212f, 40f), Image.Type.Sliced).transform.SetSiblingIndex(plate.transform.GetSiblingIndex());
                var key = MenuArt.Label("K", plate.transform, "", 18f, new Color(0.85f, 0.95f, 1f), Vector2.zero, new Vector2(200f, 38f), TextAlignmentOptions.Center, 3f, MenuArt.TextHeavySoft);
                keyRows.Add((key, a));
                y -= 50f;
            }
            Body(left, "LINKSKLICK HALTEN = DAUERFEUER  ·  ESC PAUSE", new Vector2(0f, -292f), new Vector2(600f, 32f), 17f);

            var right = UiKit.Node("About", page.Content, new Vector2(370f, 0f), new Vector2(680f, 700f));
            MenuUi.Plate(right, "Back", Vector2.zero, new Vector2(680f, 700f), Gold, 0.3f);
            MenuArt.Label("Head", right, "TIPPS", 30f, Gold, new Vector2(0f, 284f), new Vector2(600f, 44f), TextAlignmentOptions.Center, 10f);
            string[] tips =
            {
                "Schieß dich mit dem Ball durch Wellen von Monstern — jede Stage endet mit einem Boss.",
                "Nach jeder zweiten Runde wählst du ein Upgrade. Deine vier Fähigkeiten rüstest du im Menü aus.",
                "Besiegte Monster lassen Münzen fallen — damit kaufst du im Shop neue Spieler und Fähigkeiten.",
                "Jede Klasse hat ein Talent: Stürmer schießen härter, Skiller tricksen schneller, Verteidiger halten mehr aus.",
                "Rote Markierungen am Boden zeigen, wohin ein Boss springt oder stürmt.",
            };
            Sprite[] icons = { MenuArt.IconStriker, MenuArt.IconStar, MenuArt.IconCoin, MenuArt.IconSkiller, MenuArt.IconDefender };
            float ty = 208f;
            for (int i = 0; i < tips.Length; i++)
            {
                UiKit.Img("TipDisc", right, MenuArt.Round, new Color(0.02f, 0.05f, 0.08f, 0.85f), new Vector2(-246f, ty), new Vector2(58f, 58f));
                UiKit.Img("TipRing", right, MenuArt.RoundFrame, Gold.WithAlpha(0.5f), new Vector2(-246f, ty), new Vector2(60f, 60f));
                var ic = UiKit.Img("TipIcon", right, icons[i], Color.white, new Vector2(-246f, ty), new Vector2(36f, 36f));
                ic.preserveAspect = true;
                var tip = Body(right, tips[i], new Vector2(42f, ty), new Vector2(480f, 84f), 19f, TextAlignmentOptions.Left);
                tip.enableAutoSizing = true;
                tip.fontSizeMin = 14f;
                tip.fontSizeMax = 19f;
                ty -= 100f;
            }
            Body(right, "SPORTFIGHTER  ·  F1 FPS  ·  F2 VSYNC  ·  F3 DEVELOPER", new Vector2(0f, -292f), new Vector2(600f, 30f), 15f);
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
            Vector2 cardSize = new Vector2(SettingsPanel.Size.x, 730f), cardPos = new Vector2(0f, -8f);
            Settings = new SettingsPanel();
            Settings.Build(holder, false);
            Recolor(Settings.Root, "Glass", Color.white, cardPos, cardSize);
            Recolor(Settings.Root, "Shadow", new Color(0f, 0.01f, 0.03f, 0.45f), cardPos + new Vector2(0f, -12f), cardSize * 1.1f);
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
                MenuArt.SetText(rankYou, "DU  (" + Characters.Current.Name + ")");
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
