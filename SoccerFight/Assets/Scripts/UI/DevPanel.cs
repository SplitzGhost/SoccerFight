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

            Vector2 size = new Vector2(1290f, 830f);
            panel = UiKit.Node("Panel", root, Vector2.zero, size);
            UiKit.Img("Shadow", panel, UiArt.Glow, new Color(0f, 0f, 0.02f, 0.55f), new Vector2(0f, -20f), size * 1.3f);
            UiKit.Img("Border", panel, ButtonSkin.Frame, ButtonSkin.Gold.WithAlpha(0.3f), Vector2.zero, size + new Vector2(3f, 3f), Image.Type.Sliced);
            UiKit.Img("Glass", panel, ButtonSkin.Panel, ButtonSkin.Slate, Vector2.zero, size, Image.Type.Sliced);
            UiKit.Img("Top Light", panel, UiArt.LineFade, Palette.Gold.WithAlpha(0.4f), new Vector2(0f, size.y * 0.5f - 1f), new Vector2(size.x * 0.7f, 2f));

            UiKit.Label("Title", panel, "DEVELOPER-MODUS", 32f, Color.white, TextAlignmentOptions.Center, new Vector2(0f, 366f), new Vector2(1000f, 46f), true, 14f);
            UiKit.Label("Sub", panel, "F3 ODER ESC SCHLIESSEN  ·  ÄNDERUNGEN WIRKEN SOFORT  ·  DEV-LÄUFE ZÄHLEN NICHT FÜR DEN REKORD",
                12f, Palette.UiMuted, TextAlignmentOptions.Center, new Vector2(0f, 330f), new Vector2(1200f, 20f), true, 4f);

            BuildCheats(new Vector2(-430f, 0f));
            BuildRun(new Vector2(0f, 0f));
            BuildUpgrades(new Vector2(430f, 0f));

            status = UiKit.Label("Status", panel, "", 14f, Palette.Gold, TextAlignmentOptions.Center, new Vector2(0f, -384f), new Vector2(1200f, 22f), true, 4f);
            canvas.gameObject.SetActive(false);
        }

        const float ColW = 370f;

        void StepBtn(Vector2 pos, string glyph, System.Action action) => PlainBtn(pos, new Vector2(56f, 42f), glyph, 22f, 0f, action);

        /// <summary>Compact button without the accent bar (the pill buttons' bar would crowd short labels).</summary>
        void PlainBtn(Vector2 pos, Vector2 size, string text, float fontSize, float spacing, System.Action action)
        {
            var rim = UiKit.Img(text + " Rim", panel, ButtonSkin.Frame, Color.white.WithAlpha(0.14f), pos, size + new Vector2(2f, 2f), Image.Type.Sliced);
            var bg = UiKit.Img(text, panel, ButtonSkin.Plate, UiKit.ButtonBase, pos, size, Image.Type.Sliced, true);
            var label = UiKit.Label("Label", bg.transform, text, fontSize, Palette.UiText, TextAlignmentOptions.Center, new Vector2(0f, 1f), size, true, spacing);
            var b = bg.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => action());
            var anim = bg.gameObject.AddComponent<UiAnim>();
            anim.Apply = a =>
            {
                float s = 1f + a.Hover * 0.04f - a.Press * 0.06f;
                bg.rectTransform.localScale = rim.rectTransform.localScale = new Vector3(s, s, 1f);
                bg.color = Color.Lerp(UiKit.ButtonBase, UiKit.ButtonHover, a.Hover);
                label.color = Color.Lerp(Palette.UiText, Palette.Gold, a.Hover);
            };
        }

        Button Btn(Vector2 col, float y, string text, System.Action action, bool primary = false, float width = ColW)
            => UiKit.MakeButton(panel, text, col + new Vector2(0f, y), new Vector2(width, 44f), action, primary, 14f);

        void BuildCheats(Vector2 c)
        {
            UiKit.Section(panel, "SCHUMMELN", c + new Vector2(0f, 270f), ColW);
            first = UiKit.MakeToggle(panel, "UNVERWUNDBAR", c + new Vector2(0f, 222f), ColW, () => DevMode.God, v => { DevMode.God = v; Cheat(v ? "Unverwundbar an" : "Unverwundbar aus"); });
            UiKit.MakeToggle(panel, "KEINE ABKLINGZEITEN", c + new Vector2(0f, 176f), ColW, () => DevMode.NoCooldowns, v => { DevMode.NoCooldowns = v; Cheat(v ? "Keine Abklingzeiten an" : "Abklingzeiten normal"); });
            UiKit.MakeToggle(panel, "EIN-TREFFER-KILLS", c + new Vector2(0f, 130f), ColW, () => DevMode.OneHit, v => { DevMode.OneHit = v; Cheat(v ? "Jeder Treffer tötet" : "Schaden normal"); });
            UiKit.MakeToggle(panel, "INFO-ANZEIGE", c + new Vector2(0f, 84f), ColW, () => DevMode.ShowInfo, v => { DevMode.ShowInfo = v; Say(v ? "Info-Anzeige oben rechts" : "Info-Anzeige aus"); });
            UiKit.MakeSlider(panel, "SPIELTEMPO", c + new Vector2(0f, 20f), ColW, 0.25f, 2f,
                () => DevMode.Speed, v => { DevMode.Speed = Mathf.Round(v * 4f) / 4f; if (!Mathf.Approximately(DevMode.Speed, 1f)) DevMode.MarkRun(); },
                v => "×" + (Mathf.Round(v * 4f) / 4f).ToString("0.00"));

            UiKit.Section(panel, "SPIELER", c + new Vector2(0f, -52f), ColW);
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
            buildInfo = UiKit.Label("BuildInfo", panel, "", 12f, Palette.UiMuted, TextAlignmentOptions.Center, c + new Vector2(0f, -350f), new Vector2(ColW, 20f), true, 3f);
        }

        void BuildRun(Vector2 c)
        {
            UiKit.Section(panel, "LAUF", c + new Vector2(0f, 270f), ColW);
            // stage stepper
            UiKit.Label("StageLabel", panel, "STAGE", 15f, Palette.UiText, TextAlignmentOptions.Left, c + new Vector2(-ColW * 0.5f + 60f, 222f), new Vector2(120f, 30f), true, 4f);
            StepBtn(c + new Vector2(30f, 222f), "−", () => SetStage(stageSel - 1));
            stageValue = UiKit.Label("StageValue", panel, "1", 22f, Color.white, TextAlignmentOptions.Center, c + new Vector2(96f, 222f), new Vector2(60f, 34f), true, 0f);
            StepBtn(c + new Vector2(160f, 222f), "+", () => SetStage(stageSel + 1));
            Btn(c, 170f, "ZU STAGE SPRINGEN", () => { Director.DevGoToStage(stageSel); Close(); }, true);
            Btn(c, 118f, "WELLE ÜBERSPRINGEN", () => { Director.DevSkipWave(); Game.I.Hud.ShowToast("DEV  ·  WELLE ÜBERSPRUNGEN"); Close(); });
            Btn(c, 66f, "BOSS RUFEN", () => { Director.DevCallBoss(); Close(); });
            Btn(c, 14f, "ALLE GEGNER BESIEGEN", () => { Director.DevKillAll(); Say("Alle Gegner besiegt"); });

            UiKit.Section(panel, "GEGNER RUFEN", c + new Vector2(0f, -52f), ColW);
            float w = (ColW - 16f) / 3f;
            PlainBtn(c + new Vector2(-w - 8f, -100f), new Vector2(w, 44f), "NORMAL", 13f, 4f, () => { Director.DevSpawn(Rank.Normal); Say("Gegner gerufen"); });
            PlainBtn(c + new Vector2(0f, -100f), new Vector2(w, 44f), "ELITE", 13f, 4f, () => { Director.DevSpawn(Rank.Elite); Say("Elite gerufen"); });
            PlainBtn(c + new Vector2(w + 8f, -100f), new Vector2(w, 44f), "MINIBOSS", 13f, 4f, () => { Director.DevSpawn(Rank.MiniBoss); Say("Miniboss gerufen"); });

            UiKit.Section(panel, "KARTEN ÖFFNEN", c + new Vector2(0f, -166f), ColW);
            Btn(c, -214f, "UPGRADE-KARTEN", () => { Close(); Director.DevOfferCards(false); });
            Btn(c, -266f, "BOSS-KARTEN (SELTEN+)", () => { Close(); Director.DevOfferCards(true); });
            Btn(c, -318f, "FÄHIGKEIT WÄHLEN", () => { Close(); Director.DevOfferAbility(); });
        }

        void BuildUpgrades(Vector2 c)
        {
            UiKit.Section(panel, "UPGRADES  ·  KLICK = +1 STUFE", c + new Vector2(0f, 270f), ColW);
            const float viewH = 590f;
            var view = UiKit.Img("Upgrade List", panel, UiArt.Panel, new Color(0.02f, 0.035f, 0.06f, 0.6f), c + new Vector2(0f, 240f - viewH * 0.5f), new Vector2(ColW, viewH), Image.Type.Sliced, true);
            view.gameObject.AddComponent<RectMask2D>();
            var content = UiKit.Node("Content", view.rectTransform, Vector2.zero, new Vector2(ColW, 0f));
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;

            const float rowH = 34f, gap = 4f;
            float y = -8f - rowH * 0.5f;
            foreach (var u in UpgradeDb.All)
            {
                var def = u;
                Color rc = Rarities.Of(u.Rarity);
                var back = UiKit.Img(u.Id, content, ButtonSkin.Plate, UiKit.ButtonBase, new Vector2(0f, y), new Vector2(ColW - 22f, rowH), Image.Type.Sliced, true);
                back.rectTransform.anchorMin = back.rectTransform.anchorMax = new Vector2(0.5f, 1f);   // rows hang from the top of the list
                UiKit.Img("Diamond", back.transform, UiArt.Diamond, rc, new Vector2(-ColW * 0.5f + 30f, 0f), new Vector2(11f, 11f));
                UiKit.Label("Name", back.transform, u.Name, 13f, Palette.UiText, TextAlignmentOptions.Left, new Vector2(-10f, 0f), new Vector2(ColW - 110f, rowH), true, 1.5f);
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
                    back.color = Color.Lerp(UiKit.ButtonBase, Color.Lerp(UiKit.ButtonHover, rc * 0.5f, 0.3f), a.Hover);
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
