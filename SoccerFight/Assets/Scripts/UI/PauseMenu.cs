using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace SoccerFight
{
    /// <summary>
    /// Esc pause menu: resume / settings / restart / main menu / quit, with the shared settings page
    /// sliding in from the right. Animates on unscaled time while the game is frozen.
    /// </summary>
    public sealed class PauseMenu
    {
        Canvas canvas;
        CanvasGroup rootGroup, mainGroup;
        RectTransform mainPanel;
        Image dim;
        Button firstMain;
        SettingsPanel settings;

        public bool IsOpen { get; private set; }
        public bool IsCapturing => settings.IsCapturing;
        bool onSettings;
        float openT, openVel, pageT, pageVel;

        public event System.Action RestartRequested;
        public event System.Action DevRequested;
        public event System.Action MenuRequested;

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

            dim = UiKit.Img("Dim", root, null, new Color(0.06f, 0.12f, 0.18f, 0.72f), Vector2.zero, Vector2.zero);
            dim.rectTransform.anchorMin = Vector2.zero;
            dim.rectTransform.anchorMax = Vector2.one;
            dim.rectTransform.sizeDelta = Vector2.zero;
            dim.raycastTarget = true;
            UiKit.Img("Aura", root, UiArt.Glow, Palette.ShotCyan.WithAlpha(0.05f), Vector2.zero, new Vector2(1400f, 1000f));

            BuildMain(root);
            settings = new SettingsPanel();
            settings.Build(root, true);
            settings.BackRequested += () => ShowSettings(false);
            canvas.gameObject.SetActive(false);
        }

        void BuildMain(RectTransform root)
        {
            mainPanel = UiKit.Panel(root, "Main", new Vector2(460f, 700f), out mainGroup);
            UiKit.Label("Title", mainPanel, "PAUSE", 58f, Color.white, TextAlignmentOptions.Center, new Vector2(0f, 266f), new Vector2(440f, 80f), true, 22f);
            UiKit.Label("Sub", mainPanel, "SPIEL ANGEHALTEN", 14f, Palette.UiMuted, TextAlignmentOptions.Center, new Vector2(0f, 216f), new Vector2(440f, 24f), true, 7f);
            Vector2 size = new Vector2(340f, 58f);
            float y = 128f;
            firstMain = UiKit.MakeButton(mainPanel, "WEITER", new Vector2(0f, y), size, Close, true);
            UiKit.MakeButton(mainPanel, "EINSTELLUNGEN", new Vector2(0f, y -= 72f), size, () => ShowSettings(true));
            UiKit.MakeButton(mainPanel, "NEU STARTEN", new Vector2(0f, y -= 72f), size, () => RestartRequested?.Invoke());
            UiKit.MakeButton(mainPanel, "HAUPTMENÜ", new Vector2(0f, y -= 72f), size, () => MenuRequested?.Invoke());
            UiKit.MakeButton(mainPanel, "DEVELOPER-MODUS", new Vector2(0f, y -= 72f), size, () => DevRequested?.Invoke());
#if !UNITY_WEBGL || UNITY_EDITOR
            UiKit.MakeButton(mainPanel, "BEENDEN", new Vector2(0f, y -= 72f), size, Quit);   // a browser tab can't be quit
#endif
            UiKit.Label("Hint", mainPanel, "ESC  ZURÜCK ZUM SPIEL  ·  F3  DEVELOPER", 12f, Palette.UiMuted, TextAlignmentOptions.Center, new Vector2(0f, -302f), new Vector2(440f, 20f), true, 5f);
        }

        // ------------------------------------------------------------------ actions

        void ShowSettings(bool show)
        {
            settings.CancelCapture();
            onSettings = show;
            Select(show ? settings.First : firstMain);
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
            settings.CancelCapture();
            IsOpen = false;
            var es = EventSystem.current;
            if (es != null) es.SetSelectedGameObject(null);
        }

        /// <summary>Esc: cancel a rebind, leave the settings page, or close the menu.</summary>
        public void HandleEscape()
        {
            if (settings.IsCapturing) settings.CancelCapture();
            else if (onSettings) ShowSettings(false);
            else Close();
        }

        // ------------------------------------------------------------------ update

        public void Update(float udt)
        {
            MathUtil.Spring(ref openT, ref openVel, IsOpen ? 1f : 0f, 3.2f, 0.9f, udt);
            MathUtil.Spring(ref pageT, ref pageVel, onSettings ? 1f : 0f, 3.4f, 0.95f, udt);
            float o = Mathf.Clamp01(openT);
            if (!IsOpen && o < 0.01f && canvas.gameObject.activeSelf) canvas.gameObject.SetActive(false);
            if (!canvas.gameObject.activeSelf) return;

            settings.Update(udt);

            rootGroup.alpha = o;
            rootGroup.interactable = rootGroup.blocksRaycasts = IsOpen;
            float p = Mathf.Clamp01(pageT);
            float s = Mathf.Lerp(0.94f, 1f, o);
            mainPanel.localScale = new Vector3(s, s, 1f);
            mainPanel.anchoredPosition = new Vector2(-60f * p, 0f);
            mainGroup.alpha = 1f - p;
            mainGroup.interactable = mainGroup.blocksRaycasts = p < 0.5f;
            settings.Root.localScale = new Vector3(s, s, 1f);
            settings.Root.anchoredPosition = new Vector2(60f * (1f - p), 0f);
            settings.Group.alpha = p;
            settings.Group.interactable = settings.Group.blocksRaycasts = p >= 0.5f;
        }
    }
}
