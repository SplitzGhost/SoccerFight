using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SoccerFight
{
    /// <summary>
    /// F3 developer panel: cheats, game speed, stage jumps, wave skip, boss call, spawns, reward
    /// screens and every upgrade card on a click. Freezes the game while open, like the pause menu.
    /// </summary>
    public sealed class DevPanel
    {
        sealed class UpRow
        {
            public UpgradeDef def;
            public Image back;
            public TextMeshProUGUI stacks;
            public UiAnim anim;
        }

        Canvas canvas;
        CanvasGroup rootGroup;
        RectTransform root, panel;
        TextMeshProUGUI stageValue, status, buildInfo;
        readonly List<UpRow> upRows = new List<UpRow>();
        Selectable first;
        float openT, openVel, statusT = 99f;
        int stageSel = 1;

        public bool IsOpen { get; private set; }

        static RunDirector Director => Game.I.Director;
        static RunState Run => Game.I.Run;

        // ------------------------------------------------------------------ build

        public void Build(Transform parent, Camera cam, bool renderWithCamera)
        {
            ButtonSkin.Build();
            var go = new GameObject("Dev Panel", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            canvas = go.AddComponent<Canvas>();
            if (renderWithCamera)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 0.92f;
                canvas.sortingOrder = 1080;
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 48;
            }
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            rootGroup = go.AddComponent<CanvasGroup>();
            root = (RectTransform)go.transform;

            var dim = UiKit.Img("Dim", root, null, new Color(0.01f, 0.02f, 0.05f, 0.55f), Vector2.zero, Vector2.zero);
            dim.rectTransform.anchorMin = Vector2.zero;
            dim.rectTransform.anchorMax = Vector2.one;
            dim.rectTransform.sizeDelta = Vector2.zero;
            dim.raycastTarget = true;

            const float k = 1920f / 1672f;
            Vector2 size = new Vector2(1344f, 755f) * k;
            panel = UiKit.Node("Panel", root, new Vector2(0f, -19.5f), size);
            UiKit.Img("Originalrahmen", panel, ExactButtonArt.Get("dev-panel"), Color.white, Vector2.zero, size);

            BuildCheats(new Vector2(-430f, 0f));
            BuildRun(new Vector2(0f, 0f));
            BuildUpgrades(new Vector2(527f, 0f));

            status = UiKit.Label("Status", panel, "", 14f, Palette.Gold, TextAlignmentOptions.Center, new Vector2(0f, -407f), new Vector2(1200f, 22f), true, 4f);
            canvas.gameObject.SetActive(false);
        }

        const float ColW = 420f;

        void StepBtn(Vector2 pos, string glyph, System.Action action) => PlainBtn(pos, new Vector2(56f, 42f), glyph, 22f, 0f, action);

        /// <summary>Compact button without the accent bar (the pill buttons' bar would crowd short labels).</summary>
        void PlainBtn(Vector2 pos, Vector2 size, string text, float fontSize, float spacing, System.Action action)
        {
            OriginalButton(text, action);

        }

        Button Btn(Vector2 col, float y, string text, System.Action action, bool primary = false, float width = ColW)
            => OriginalButton(text, action, primary);

        static Vector2 OriginalPosition(Rect r) => new Vector2(r.center.x - 837f, 487.5f - r.center.y) * (1920f / 1672f);

        Button OriginalButton(string text, System.Action action, bool primary = false)
        {
            var r = ExactButtonArt.ReferenceRect("action-" + text);
            return UiKit.MakeButton(panel, text, OriginalPosition(r), r.size * (1920f / 1672f), action, primary);
        }

        Button OriginalToggle(string text, System.Func<bool> get, System.Action<bool> set)
        {
            var r = ExactButtonArt.ReferenceRect("toggle-row-" + text);
            return UiKit.MakeToggle(panel, text, OriginalPosition(r), r.width * (1920f / 1672f), get, set, r.height * (1920f / 1672f));
        }

        void BuildCheats(Vector2 c)
        {
            first = OriginalToggle("UNVERWUNDBAR", () => DevMode.God, v => { DevMode.God = v; Cheat(v ? "Unverwundbar an" : "Unverwundbar aus"); });
            OriginalToggle("KEINE ABKLINGZEITEN", () => DevMode.NoCooldowns, v => { DevMode.NoCooldowns = v; Cheat(v ? "Keine Abklingzeiten an" : "Abklingzeiten normal"); });
            OriginalToggle("EIN-TREFFER-KILLS", () => DevMode.OneHit, v => { DevMode.OneHit = v; Cheat(v ? "Jeder Treffer tötet" : "Schaden normal"); });
            OriginalToggle("INFO-ANZEIGE", () => DevMode.ShowInfo, v => { DevMode.ShowInfo = v; Say(v ? "Info-Anzeige oben rechts" : "Info-Anzeige aus"); });
            UiKit.MakeSlider(panel, "SPIELTEMPO", OriginalPosition(ExactButtonArt.ReferenceRect("slider-row-SPIELTEMPO")), 422f * (1920f / 1672f), 0.25f, 2f,
                () => DevMode.Speed, v => { DevMode.Speed = Mathf.Round(v * 4f) / 4f; if (!Mathf.Approximately(DevMode.Speed, 1f)) DevMode.MarkRun(); },
                v => "×" + (Mathf.Round(v * 4f) / 4f).ToString("0.00"), 100f * (1920f / 1672f));

            Btn(c, -100f, "VOLLE HEILUNG", () => { Game.I.Player.Heal(9999f, true); DevMode.MarkRun(); Say("Voll geheilt"); });
            Btn(c, -152f, "ALLE FÄHIGKEITEN FREISCHALTEN", () => { Director.DevUnlockAll(); Say("Alle Fähigkeiten frei"); });
            Btn(c, -204f, "+5 ZUFÄLLIGE UPGRADES", () => { Director.DevAddRandomUpgrades(5); RefreshRows(); Say("5 zufällige Upgrades genommen"); });
            Btn(c, -256f, "BUILD LEEREN", () => { Director.DevClearBuild(); RefreshRows(); Say("Alle Upgrades entfernt"); });
            // meta progression: coins for testing the shop, and a way back to the first launch
            float hw = (ColW - 8f) * 0.5f;
            PlainBtn(c + new Vector2(-hw * 0.5f - 4f, -308f), new Vector2(hw, 44f), "+500 MÜNZEN", 13f, 3f, () =>
            {
                Wallet.Add(Currencies.Coins, 500);
                Profile.Save();
                Say("+500 Münzen · jetzt " + Currencies.Format(Wallet.Get(Currencies.Coins)));
            });
            PlainBtn(c + new Vector2(hw * 0.5f + 4f, -308f), new Vector2(hw, 44f), "PROFIL LÖSCHEN", 13f, 3f, () =>
            {
                Profile.Reset();
                Characters.Reload();
                Say("Profil gelöscht · das Hauptmenü startet mit der Starterwahl");
            });
            buildInfo = UiKit.Label("BuildInfo", panel, "", 12f, Palette.UiMuted, TextAlignmentOptions.Center, c + new Vector2(0f, -395f), new Vector2(ColW, 20f), true, 3f);
        }

        void BuildRun(Vector2 c)
        {
            // stage stepper
            UiKit.Img("Stage", panel, ExactButtonArt.Get("dev-stage"), Color.white, new Vector2(46f, 198f), new Vector2(250f, 77f));
            StepBtn(c + new Vector2(30f, 222f), "−", () => SetStage(stageSel - 1));
            stageValue = UiKit.Label("StageValue", panel, "1", 28f, Color.white, TextAlignmentOptions.Center, c + new Vector2(114f, 198f), new Vector2(60f, 34f), true, 0f);
            StepBtn(c + new Vector2(160f, 222f), "+", () => SetStage(stageSel + 1));
            Btn(c, 170f, "ZU STAGE SPRINGEN", () => { Director.DevGoToStage(stageSel); Close(); }, true);
            Btn(c, 118f, "WELLE ÜBERSPRINGEN", () => { Director.DevSkipWave(); Game.I.Hud.ShowToast("DEV  ·  WELLE ÜBERSPRUNGEN"); Close(); });
            Btn(c, 66f, "BOSS RUFEN", () => { Director.DevCallBoss(); Close(); });
            Btn(c, 14f, "ALLE GEGNER BESIEGEN", () => { Director.DevKillAll(); Say("Alle Gegner besiegt"); });

            float w = (ColW - 16f) / 3f;
            PlainBtn(c + new Vector2(-w - 8f, -100f), new Vector2(w, 44f), "NORMAL", 13f, 4f, () => { Director.DevSpawn(Rank.Normal); Say("Gegner gerufen"); });
            PlainBtn(c + new Vector2(0f, -100f), new Vector2(w, 44f), "ELITE", 13f, 4f, () => { Director.DevSpawn(Rank.Elite); Say("Elite gerufen"); });
            PlainBtn(c + new Vector2(w + 8f, -100f), new Vector2(w, 44f), "MINIBOSS", 13f, 4f, () => { Director.DevSpawn(Rank.MiniBoss); Say("Miniboss gerufen"); });

            Btn(c, -214f, "UPGRADE-KARTEN", () => { Close(); Director.DevOfferCards(false); });
            Btn(c, -266f, "BOSS-KARTEN (SELTEN+)", () => { Close(); Director.DevOfferCards(true); });
            Btn(c, -318f, "FÄHIGKEIT WÄHLEN", () => { Close(); Director.DevOfferAbility(); });
        }

        void BuildUpgrades(Vector2 c)
        {
            const float viewH = 629f;
            var view = UiKit.Img("Upgrade List", panel, UiArt.Panel, new Color(0.02f, 0.035f, 0.06f, 0.6f), c + new Vector2(0f, 249f - viewH * 0.5f), new Vector2(ColW, viewH), Image.Type.Sliced, true);
            view.gameObject.AddComponent<RectMask2D>();
            var content = UiKit.Node("Content", view.rectTransform, Vector2.zero, new Vector2(ColW, 0f));
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;

            const float rowH = 45f, gap = 4f;
            float y = -8f - rowH * 0.5f;
            foreach (var u in UpgradeDb.All)
            {
                var def = u;
                Color rc = Rarities.Of(u.Rarity);
                var back = UiKit.Img(u.Id, content, ExactButtonArt.Get("upgrade-row"), Color.white, new Vector2(0f, y), new Vector2(ColW - 22f, rowH), Image.Type.Sliced, true);
                back.rectTransform.anchorMin = back.rectTransform.anchorMax = new Vector2(0.5f, 1f);   // rows hang from the top of the list
                UiKit.Img("Symbol", back.transform, UpgradeIcons.Get(u.Icon), Color.white, new Vector2(-ColW * 0.5f + 43f, 0f), new Vector2(35f, 35f)).preserveAspect = true;
                UiKit.Img("Diamond", back.transform, UiArt.Diamond, rc, new Vector2(ColW * .3f, 0f), new Vector2(14f, 14f));
                MenuArt.Label("Name", back.transform, u.Name, 15f, Color.white, new Vector2(-4f, 0f), new Vector2(ColW - 150f, rowH), TextAlignmentOptions.Left, 0f);
                var stacks = UiKit.Label("Stacks", back.transform, "", 12f, Palette.UiMuted, TextAlignmentOptions.Right, new Vector2(ColW * 0.5f - 60f, 0f), new Vector2(70f, rowH), true, 0f);
                var row = new UpRow { def = def, back = back, stacks = stacks };
                var button = back.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => AddUpgrade(row));
                row.anim = back.gameObject.AddComponent<UiAnim>();
                row.anim.Apply = a =>
                {
                    float s = 1f + a.Hover * 0.015f - a.Press * 0.03f;
                    back.rectTransform.localScale = new Vector3(s, s, 1f);
                    back.color = Color.white;
                };
                upRows.Add(row);
                y -= rowH + gap;
            }
            content.sizeDelta = new Vector2(ColW, -y - rowH * 0.5f + 8f);

            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = view.rectTransform;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 36f;
            scroll.inertia = true;
        }

        // ------------------------------------------------------------------ actions

        void Say(string text)
        {
            status.text = text.ToUpperInvariant();
            statusT = 0f;
        }

        void Cheat(string text)
        {
            if (DevMode.AnyCheat) DevMode.MarkRun();
            Say(text);
        }

        void SetStage(int s)
        {
            stageSel = Mathf.Clamp(s, 1, 24);
            stageValue.text = stageSel.ToString();
        }

        void AddUpgrade(UpRow row)
        {
            if (Director.DevAddUpgrade(row.def)) Say(row.def.Name + " · Stufe " + Run.Stacks(row.def.Id) + " / " + row.def.Max);
            else Say(row.def.Name + " ist schon auf der höchsten Stufe");
            RefreshRows();
        }

        void RefreshRows()
        {
            foreach (var r in upRows)
            {
                int n = Run.Stacks(r.def.Id);
                r.stacks.text = n + " / " + r.def.Max;
                r.stacks.color = n >= r.def.Max ? Palette.Gold : n > 0 ? Palette.UiText : Palette.UiMuted;
            }
            buildInfo.text = Run.PickOrder.Count + " UPGRADES  ·  " + Run.Unlocked.Count + " FÄHIGKEITEN";
        }

        // ------------------------------------------------------------------ open / close

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            SetStage(Run.Stage);
            RefreshRows();
            canvas.gameObject.SetActive(true);
            var es = EventSystem.current;
            if (es != null && first != null) es.SetSelectedGameObject(first.gameObject);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            var es = EventSystem.current;
            if (es != null) es.SetSelectedGameObject(null);
        }

        public void Update(float udt)
        {
            MathUtil.Spring(ref openT, ref openVel, IsOpen ? 1f : 0f, 3.4f, 0.9f, udt);
            float o = Mathf.Clamp01(openT);
            if (!IsOpen && o < 0.01f && canvas.gameObject.activeSelf) canvas.gameObject.SetActive(false);
            if (!canvas.gameObject.activeSelf) return;
            rootGroup.alpha = o;
            rootGroup.interactable = rootGroup.blocksRaycasts = IsOpen;
            float s = Mathf.Lerp(0.95f, 1f, o);
            panel.localScale = new Vector3(s, s, 1f);
            statusT += udt;
            status.alpha = statusT < 2.5f ? 1f - MathUtil.Smooth01((statusT - 2f) / 0.5f) : 0f;
        }
    }
}
