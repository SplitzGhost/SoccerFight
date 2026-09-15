using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SoccerFight
{
    /// <summary>
    /// Esc pause menu: resume / settings / restart / quit, and a settings page with display and
    /// effect options plus rebindable controls. Animates on unscaled time while the game is frozen.
    /// </summary>
    public sealed class PauseMenu
    {
        Canvas canvas;
        CanvasGroup rootGroup, mainGroup, settingsGroup;
        RectTransform mainPanel, settingsPanel;
        Image dim;
        Button firstMain, firstSettings;
        readonly Dictionary<GameAction, UiKit.KeyRow> keyRows = new Dictionary<GameAction, UiKit.KeyRow>();

        public bool IsOpen { get; private set; }
        public bool IsCapturing => capturing;
        bool onSettings;
        float openT, openVel, pageT, pageVel;
        bool capturing;
        GameAction captureAction;
        int captureFrame;
        float lastCaptureEnd = -10f;

        public event System.Action RestartRequested;

        // ------------------------------------------------------------------ build

        public void Build(Transform parent, Camera cam, bool renderWithCamera)
        {
            var go = new GameObject("Pause Menu", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            canvas = go.AddComponent<Canvas>();
            if (renderWithCamera)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 0.9f;
                canvas.sortingOrder = 1100;
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 50;
            }
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            rootGroup = go.AddComponent<CanvasGroup>();
            var root = (RectTransform)go.transform;

            dim = UiKit.Img("Dim", root, null, new Color(0.01f, 0.02f, 0.05f, 0.72f), Vector2.zero, Vector2.zero);
            dim.rectTransform.anchorMin = Vector2.zero;
            dim.rectTransform.anchorMax = Vector2.one;
            dim.rectTransform.sizeDelta = Vector2.zero;
            dim.raycastTarget = true;
            UiKit.Img("Aura", root, UiArt.Glow, Palette.ShotCyan.WithAlpha(0.05f), Vector2.zero, new Vector2(1400f, 1000f));

            BuildMain(root);
            BuildSettings(root);
            canvas.gameObject.SetActive(false);
            KeyBindings.Changed += RefreshKeys;
        }

        RectTransform Panel(Transform root, string name, Vector2 size, out CanvasGroup group)
        {
            var rt = UiKit.Node(name, root, Vector2.zero, size);
            group = rt.gameObject.AddComponent<CanvasGroup>();
            UiKit.Img("Shadow", rt, UiArt.Glow, new Color(0f, 0f, 0.02f, 0.55f), new Vector2(0f, -20f), size * 1.35f);
            UiKit.Img("Border", rt, UiArt.Panel, Color.white.WithAlpha(0.1f), Vector2.zero, size + new Vector2(3f, 3f), Image.Type.Sliced);
            UiKit.Img("Glass", rt, UiArt.Panel, new Color(0.045f, 0.075f, 0.12f, 0.995f), Vector2.zero, size, Image.Type.Sliced);
            UiKit.Img("Top Light", rt, UiArt.LineFade, Palette.ShotCyan.WithAlpha(0.35f), new Vector2(0f, size.y * 0.5f - 1f), new Vector2(size.x * 0.7f, 2f));
            return rt;
        }

        void BuildMain(RectTransform root)
        {
            mainPanel = Panel(root, "Main", new Vector2(460f, 560f), out mainGroup);
            UiKit.Label("Title", mainPanel, "PAUSE", 58f, Color.white, TextAlignmentOptions.Center, new Vector2(0f, 196f), new Vector2(440f, 80f), true, 22f);
            UiKit.Label("Sub", mainPanel, "SPIEL ANGEHALTEN", 14f, Palette.UiMuted, TextAlignmentOptions.Center, new Vector2(0f, 146f), new Vector2(440f, 24f), true, 7f);
            Vector2 size = new Vector2(340f, 58f);
            firstMain = UiKit.MakeButton(mainPanel, "WEITER", new Vector2(0f, 64f), size, Close, true);
            UiKit.MakeButton(mainPanel, "EINSTELLUNGEN", new Vector2(0f, -8f), size, () => ShowSettings(true));
            UiKit.MakeButton(mainPanel, "NEU STARTEN", new Vector2(0f, -80f), size, () => RestartRequested?.Invoke());
#if !UNITY_WEBGL || UNITY_EDITOR
            UiKit.MakeButton(mainPanel, "BEENDEN", new Vector2(0f, -152f), size, Quit);   // a browser tab can't be quit
#endif
            UiKit.Label("Hint", mainPanel, "ESC  ZURÜCK ZUM SPIEL", 12f, Palette.UiMuted, TextAlignmentOptions.Center, new Vector2(0f, -232f), new Vector2(440f, 20f), true, 5f);
        }

        void BuildSettings(RectTransform root)
        {
            settingsPanel = Panel(root, "Settings", new Vector2(1060f, 680f), out settingsGroup);
            UiKit.Label("Title", settingsPanel, "EINSTELLUNGEN", 34f, Color.white, TextAlignmentOptions.Center, new Vector2(0f, 290f), new Vector2(1000f, 50f), true, 14f);
            UiKit.Img("Divider", settingsPanel, UiArt.LineFade, Color.white.WithAlpha(0.1f), new Vector2(0f, -10f), new Vector2(440f, 2f)).rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            const float colW = 400f;
            float lx = -255f, rx = 255f;

            UiKit.Section(settingsPanel, "ANZEIGE", new Vector2(lx, 214f), colW);
            firstSettings = UiKit.MakeToggle(settingsPanel, "VOLLBILD", new Vector2(lx, 166f), colW,
                () => GameSettings.Fullscreen, v => { GameSettings.Fullscreen = v; ApplySettings(); });
            UiKit.MakeToggle(settingsPanel, "VSYNC", new Vector2(lx, 118f), colW,
                () => GameSettings.VSync, v => { GameSettings.VSync = v; ApplySettings(); });
            UiKit.MakeToggle(settingsPanel, "FPS ANZEIGEN", new Vector2(lx, 70f), colW,
                () => GameSettings.ShowFps, v => { GameSettings.ShowFps = v; ApplySettings(); });

            UiKit.Section(settingsPanel, "EFFEKTE", new Vector2(lx, 6f), colW);
            UiKit.MakeSlider(settingsPanel, "BILDSCHIRMWACKELN", new Vector2(lx, -48f), colW, 0f, 1.5f,
                () => GameSettings.ScreenShake, v => { GameSettings.ScreenShake = v; GameSettings.Save(); }, v => Mathf.RoundToInt(v * 100f) + "%");
            UiKit.MakeSlider(settingsPanel, "LEUCHTEN (BLOOM)", new Vector2(lx, -118f), colW, 0f, 1.5f,
                () => GameSettings.Bloom, v => { GameSettings.Bloom = v; GameSettings.Save(); }, v => Mathf.RoundToInt(v * 100f) + "%");
            UiKit.MakeToggle(settingsPanel, "FARBSAUM-EFFEKT", new Vector2(lx, -180f), colW,
                () => GameSettings.ChromaticAberration, v => { GameSettings.ChromaticAberration = v; ApplySettings(); });

            UiKit.Section(settingsPanel, "STEUERUNG", new Vector2(rx, 214f), colW);
            float y = 166f;
            foreach (var a in KeyBindings.All)
            {
                var action = a;
                keyRows[a] = UiKit.MakeKeyRow(settingsPanel, KeyBindings.ActionName(a), new Vector2(rx, y), colW, () => BeginCapture(action));
                y -= 46f;
            }
            UiKit.MakeButton(settingsPanel, "STANDARD WIEDERHERSTELLEN", new Vector2(rx, y - 16f), new Vector2(colW, 46f), KeyBindings.ResetDefaults, false, 14f);
            UiKit.Label("KeyHint", settingsPanel, "TASTE ANKLICKEN, DANN NEUE TASTE DRÜCKEN", 11f, Palette.UiMuted, TextAlignmentOptions.Center, new Vector2(rx, y - 60f), new Vector2(colW, 20f), true, 4f);
            UiKit.Label("Pause", settingsPanel, "ESC  PAUSE  ·  F1  FPS  ·  F2  VSYNC", 11f, Palette.UiMuted, TextAlignmentOptions.Center, new Vector2(rx, y - 84f), new Vector2(colW, 20f), true, 4f);

            UiKit.MakeButton(settingsPanel, "ZURÜCK", new Vector2(0f, -284f), new Vector2(280f, 54f), () => ShowSettings(false), true);
            RefreshKeys();
        }

        // ------------------------------------------------------------------ actions

        static void ApplySettings()
        {
            GameSettings.Apply();
            GameSettings.Save();
        }

        void RefreshKeys()
        {
            foreach (var kv in keyRows) kv.Value.Key.text = KeyBindings.DisplayName(kv.Key);
        }

        void BeginCapture(GameAction a)
        {
            if (Time.unscaledTime - lastCaptureEnd < 0.3f) return;
            capturing = true;
            captureAction = a;
            captureFrame = Time.frameCount;
            keyRows[a].Key.text = "TASTE DRÜCKEN…";
        }

        void EndCapture()
        {
            capturing = false;
            lastCaptureEnd = Time.unscaledTime;
            RefreshKeys();
        }

        void ShowSettings(bool show)
        {
            if (capturing) EndCapture();
            onSettings = show;
            Select(show ? firstSettings : firstMain);
        }

        static void Select(Selectable s)
        {
            var es = EventSystem.current;
            if (es != null && s != null) es.SetSelectedGameObject(s.gameObject);
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            onSettings = false;
            pageT = 0f;
            canvas.gameObject.SetActive(true);
            Select(firstMain);
        }

        public void OpenSettings()
        {
            Open();
            ShowSettings(true);
        }

        public void Close()
        {
            if (!IsOpen) return;
            if (capturing) EndCapture();
            IsOpen = false;
            var es = EventSystem.current;
            if (es != null) es.SetSelectedGameObject(null);
        }

        /// <summary>Esc: cancel a rebind, leave the settings page, or close the menu.</summary>
        public void HandleEscape()
        {
            if (capturing) EndCapture();
            else if (onSettings) ShowSettings(false);
            else Close();
        }

        // ------------------------------------------------------------------ update

        public void Update(float udt)
        {
            if (capturing && Time.frameCount > captureFrame)
            {
                if (KeyBindings.TryCapture(out var b, out bool cancelled))
                {
                    KeyBindings.Set(captureAction, b);
                    EndCapture();
                }
                else if (cancelled) EndCapture();
            }

            MathUtil.Spring(ref openT, ref openVel, IsOpen ? 1f : 0f, 3.2f, 0.9f, udt);
            MathUtil.Spring(ref pageT, ref pageVel, onSettings ? 1f : 0f, 3.4f, 0.95f, udt);
            float o = Mathf.Clamp01(openT);
            if (!IsOpen && o < 0.01f && canvas.gameObject.activeSelf) canvas.gameObject.SetActive(false);
            if (!canvas.gameObject.activeSelf) return;

            rootGroup.alpha = o;
            rootGroup.interactable = rootGroup.blocksRaycasts = IsOpen;
            float p = Mathf.Clamp01(pageT);
            float s = Mathf.Lerp(0.94f, 1f, o);
            mainPanel.localScale = new Vector3(s, s, 1f);
            mainPanel.anchoredPosition = new Vector2(-60f * p, 0f);
            mainGroup.alpha = 1f - p;
            mainGroup.interactable = mainGroup.blocksRaycasts = p < 0.5f;
            settingsPanel.localScale = new Vector3(s, s, 1f);
            settingsPanel.anchoredPosition = new Vector2(60f * (1f - p), 0f);
            settingsGroup.alpha = p;
            settingsGroup.interactable = settingsGroup.blocksRaycasts = p >= 0.5f;

            // key rows: pulse while waiting for input, hover highlight otherwise
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);
            foreach (var kv in keyRows)
            {
                var row = kv.Value;
                bool waiting = capturing && kv.Key == captureAction;
                row.Background.color = waiting
                    ? Color.Lerp(UiKit.ButtonHover, Palette.ShotCyan * 0.55f, pulse * 0.6f)
                    : Color.Lerp(UiKit.ButtonBase, UiKit.ButtonHover, row.Anim.Hover);
                row.Rim.color = waiting ? Palette.ShotCyan.WithAlpha(0.6f + 0.4f * pulse) : Color.white.WithAlpha(0.14f + 0.2f * row.Anim.Hover);
                row.Key.color = waiting ? Color.white : Color.Lerp(Palette.UiText, Palette.ShotCyan, row.Anim.Hover * 0.6f);
            }
        }
    }
}
