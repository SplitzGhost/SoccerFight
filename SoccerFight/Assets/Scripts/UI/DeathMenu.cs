using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SoccerFight
{
    /// <summary>
    /// Das Menü am Ende eines Laufs (Tod oder Sieg): gemalte Steintafel mit der Bilanz des Laufs und zwei
    /// Knöpfen – nochmal spielen oder zurück ins Hauptmenü. Die Welt läuft dahinter abgedunkelt weiter.
    /// </summary>
    public sealed class DeathMenu
    {
        sealed class Row
        {
            public RectTransform rt;
            public CanvasGroup group;
            public Vector2 home;
            public float delay;
        }

        Canvas canvas;
        CanvasGroup rootGroup;
        RectTransform panel;
        Image aura;
        TextMeshProUGUI label, title, sub, best, hint, againLabel;
        readonly TextMeshProUGUI[] statValue = new TextMeshProUGUI[4];
        readonly float[] statTarget = new float[4];
        RectTransform build;
        Button again;
        readonly List<Row> rows = new List<Row>();

        public bool IsOpen { get; private set; }
        float openT, openVel, age;
        int shownCount = -1;

        public event System.Action RestartRequested;
        public event System.Action MenuRequested;

        static readonly Vector2 Size = new Vector2(780f, 850f);
        const float CountStart = 0.45f, CountTime = 0.9f;

        // ------------------------------------------------------------------ build

        public void Build(Transform parent, Camera cam, bool renderWithCamera)
        {
            var go = new GameObject("Death Menu", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            canvas = go.AddComponent<Canvas>();
            if (renderWithCamera)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 0.97f;
                canvas.sortingOrder = 1040;
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 35;
            }
            UiKit.Scale(go);
            go.AddComponent<GraphicRaycaster>();
            rootGroup = go.AddComponent<CanvasGroup>();
            var root = (RectTransform)go.transform;

            var dim = UiKit.Img("Dim", root, null, new Color(0.01f, 0.02f, 0.05f, 0.74f), Vector2.zero, Vector2.zero);
            dim.rectTransform.anchorMin = Vector2.zero;
            dim.rectTransform.anchorMax = Vector2.one;
            dim.rectTransform.sizeDelta = Vector2.zero;
            dim.raycastTarget = true;
            aura = UiKit.Img("Aura", root, UiArt.Glow, Palette.Hurt.WithAlpha(0.07f), Vector2.zero, new Vector2(1700f, 1300f));

            panel = UiKit.Panel(root, "Tafel", Size, out _);

            // Kopf: kleine Zeile, großer Titel, wo der Lauf endete
            var head = AddRow("Kopf", 0.05f);
            label = UiKit.Label("Label", head, "LAUF BEENDET", 16f, Palette.Hurt, TextAlignmentOptions.Center, new Vector2(0f, 338f), new Vector2(680f, 24f), true, 14f);
            // Text kommt erst beim Öffnen: die gemalte Schrift ist beim Bau noch nicht geladen
            title = MenuArt.Label("Title", head, "", 92f, Color.white, new Vector2(0f, 270f), new Vector2(700f, 110f), TextAlignmentOptions.Center, 10f);
            UiKit.Img("Line", head, UiArt.LineFade, Palette.ShotCyan.WithAlpha(0.3f), new Vector2(-170f, 212f), new Vector2(300f, 2f))
                .rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);
            UiKit.Img("Line", head, UiArt.LineFade, Palette.ShotCyan.WithAlpha(0.3f), new Vector2(170f, 212f), new Vector2(300f, 2f));
            UiKit.Img("Dot", head, UiArt.Diamond, Palette.ShotCyan.WithAlpha(0.7f), new Vector2(0f, 212f), new Vector2(10f, 10f));
            sub = UiKit.Label("Sub", head, "", 20f, Palette.UiText, TextAlignmentOptions.Center, new Vector2(0f, 182f), new Vector2(680f, 28f), true, 4f);

            // Bilanz: vier Zahlen, die beim Öffnen hochzählen
            var stats = AddRow("Bilanz", 0.22f);
            string[] names = { "GEGNER BESIEGT", "ZEIT", "UPGRADES", "MÜNZEN" };
            for (int i = 0; i < 4; i++)
            {
                float x = (i - 1.5f) * 168f;
                statValue[i] = UiKit.Label("Wert", stats, "0", 44f, i == 3 ? Palette.PowerGold : Color.white, TextAlignmentOptions.Center, new Vector2(x, 104f), new Vector2(168f, 52f));
                UiKit.Label("Name", stats, names[i], 12.5f, Palette.UiMuted, TextAlignmentOptions.Center, new Vector2(x, 64f), new Vector2(168f, 18f), true, 3f);
                if (i > 0) UiKit.Img("Trenner", stats, null, Palette.ShotCyan.WithAlpha(0.14f), new Vector2(x - 84f, 88f), new Vector2(2f, 64f));
            }

            // Rekord und die gesammelten Upgrades
            var record = AddRow("Rekord", 0.36f);
            best = UiKit.Label("Best", record, "", 15f, Palette.Gold, TextAlignmentOptions.Center, new Vector2(0f, 4f), new Vector2(680f, 22f), true, 5f);
            build = UiKit.Node("Build", record, new Vector2(0f, -50f), Vector2.zero);

            // die beiden Wege weiter
            const float k = 1920f / 1672f;
            var buttonSize = new Vector2(378f, 87f) * k;
            var actions = AddRow("Knöpfe", 0.5f);
            again = UiKit.MakeButton(actions, "NOCHMAL SPIELEN", new Vector2(0f, -164f), buttonSize, () => RestartRequested?.Invoke(), true, 24f);
            againLabel = again.GetComponentInChildren<TextMeshProUGUI>();
            UiKit.MakeButton(actions, "HAUPTMENÜ", new Vector2(0f, -270f), buttonSize, () => MenuRequested?.Invoke());
            hint = UiKit.Label("Hint", actions, "", 13f, Palette.UiMuted, TextAlignmentOptions.Center, new Vector2(0f, -346f), new Vector2(680f, 20f), true, 4f);

            canvas.gameObject.SetActive(false);
        }

        RectTransform AddRow(string name, float delay)
        {
            var rt = UiKit.Node(name, panel, Vector2.zero, Vector2.zero);
            rows.Add(new Row { rt = rt, group = rt.gameObject.AddComponent<CanvasGroup>(), home = rt.anchoredPosition, delay = delay });
            return rt;
        }

        // ------------------------------------------------------------------ open / close

        public void Open(RunState run, bool won)
        {
            if (IsOpen) return;
            IsOpen = true;
            age = 0f;
            shownCount = -1;
            openT = openVel = 0f;

            Color mood = won ? Palette.Gold : Palette.Hurt;
            aura.color = mood.WithAlpha(0.07f);
            label.text = won ? "LEVEL " + run.Challenge.Number + " GESCHAFFT" : "LAUF BEENDET";
            label.color = mood;
            MenuArt.SetText(title, won ? "SIEG" : "BESIEGT");
            // der goldene Knopf in derselben gemalten Schrift wie der Hauptmenü-Knopf darunter
            if (againLabel.font != MenuArt.FontHeavy && MenuArt.FontHeavy != null && ExactMenuFont.Covers(againLabel.text))
            {
                againLabel.font = MenuArt.FontHeavy;
                if (MenuArt.TextPlate != null) againLabel.fontSharedMaterial = MenuArt.TextPlate;
                // dunkel eingefärbt: die eisblaue Schrift wäre auf Gold kaum lesbar
                againLabel.color = new Color(0.34f, 0.13f, 0.04f);
                againLabel.fontSize = 30f;
            }
            string wave = run.IsBossWave ? "BOSSKAMPF" : "WELLE " + Mathf.Max(1, run.Wave) + " / " + run.WavesInStage;
            sub.text = won ? run.Challenge.Name + "  ·  " + run.Challenge.Stages + " STAGES"
                : "STAGE " + run.Stage + "  ·  " + StageThemes.Title(run.Stage) + "  ·  " + wave;

            statTarget[0] = run.Kills;
            statTarget[1] = run.Time;
            statTarget[2] = run.PickOrder.Count;
            statTarget[3] = CoinRewards.Earned;

            int record = RunState.BestStage;
            best.text = won ? "LEVEL KANN JEDERZEIT ERNEUT GESPIELT WERDEN"
                : DevMode.UsedThisRun ? "DEV-LAUF  ·  ZÄHLT NICHT FÜR DEN REKORD"
                : run.Stage >= record ? "NEUER REKORD  ·  STAGE " + run.Stage : "BESTER LAUF  ·  STAGE " + record;

            // ein Medaillon je Upgrade, in der Reihenfolge, in der es zuerst gewählt wurde
            for (int i = build.childCount - 1; i >= 0; i--) Object.Destroy(build.GetChild(i).gameObject);
            var order = new List<string>();
            foreach (var id in run.PickOrder) if (!order.Contains(id)) order.Add(id);
            int n = Mathf.Min(order.Count, 15);
            for (int i = 0; i < n; i++)
                BuildIcon(UpgradeDb.Get(order[i]), run.Stacks(order[i]), new Vector2((i - (n - 1) * 0.5f) * 42f, 0f));
            if (n == 0)
                UiKit.Label("Leer", build, "OHNE UPGRADES", 13f, Palette.UiMuted.WithAlpha(0.6f), TextAlignmentOptions.Center, Vector2.zero, new Vector2(400f, 20f), true, 4f);

            canvas.gameObject.SetActive(true);
            Apply(0f);
        }

        void BuildIcon(UpgradeDef u, int level, Vector2 pos)
        {
            var rt = UiKit.Node(u.Id, build, pos, new Vector2(36f, 36f));
            Color rc = Rarities.Of(u.Rarity);
            UiKit.Img("Fill", rt, UiArt.Circle, Color.Lerp(Palette.UiGlass, rc, 0.28f).WithAlpha(0.95f), Vector2.zero, new Vector2(34f, 34f));
            UiKit.Img("Ring", rt, UiArt.RingThin, rc.WithAlpha(0.9f), Vector2.zero, new Vector2(36f, 36f));
            UiKit.Img("Icon", rt, UpgradeIcons.Get(u.Icon), Color.white, Vector2.zero, new Vector2(25f, 25f));
            if (level > 1) UiKit.Label("Count", rt, level.ToString(), 11f, Color.white, TextAlignmentOptions.Center, new Vector2(13f, -13f), new Vector2(30f, 16f));
        }

        /// <summary>Schließt ohne Ausblenden: der nächste Lauf oder das Hauptmenü übernimmt sofort.</summary>
        public void Close()
        {
            IsOpen = false;
            openT = openVel = 0f;
            if (canvas != null) canvas.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ update

        public void Update(float udt)
        {
            if (!IsOpen) return;
            age += udt;
            MathUtil.Spring(ref openT, ref openVel, 1f, 2.6f, 0.78f, udt);
            Apply(udt);
        }

        void Apply(float udt)
        {
            float o = Mathf.Clamp01(openT);
            rootGroup.alpha = o;
            rootGroup.interactable = rootGroup.blocksRaycasts = age > 0.25f;
            // die Tafel setzt sich mit etwas Gewicht: leicht über das Ziel und zurück
            float s = Mathf.LerpUnclamped(0.9f, 1f, openT);
            panel.localScale = new Vector3(s, s, 1f);
            panel.anchoredPosition = new Vector2(0f, Mathf.LerpUnclamped(-46f, 0f, openT));

            foreach (var r in rows)
            {
                float a = MathUtil.Smooth01((age - r.delay) / 0.32f);
                r.group.alpha = a;
                r.rt.anchoredPosition = r.home + new Vector2(0f, -16f * (1f - MathUtil.EaseOutCubic((age - r.delay) / 0.4f)));
            }

            // Zahlen zählen hoch (nur neu setzen, wenn sich der Stand ändert)
            float c = MathUtil.EaseOutCubic((age - CountStart) / CountTime);
            int step = Mathf.RoundToInt(c * 200f);
            if (step != shownCount)
            {
                shownCount = step;
                statValue[0].text = Mathf.RoundToInt(statTarget[0] * c).ToString();
                int sec = Mathf.FloorToInt(statTarget[1] * c);
                statValue[1].text = (sec / 60) + ":" + (sec % 60).ToString("00");
                statValue[2].text = Mathf.RoundToInt(statTarget[2] * c).ToString();
                statValue[3].text = "+" + Currencies.Format(Mathf.RoundToInt(statTarget[3] * c));
            }

            // im Duo startet nur der Host den nächsten Lauf
            bool host = !Coop.IsClient;
            if (again.gameObject.activeSelf != host) again.gameObject.SetActive(host);
            hint.text = host ? "[ ENTER ]  NOCHMAL SPIELEN" : "DER HOST STARTET DEN NÄCHSTEN LAUF";
        }
    }
}
