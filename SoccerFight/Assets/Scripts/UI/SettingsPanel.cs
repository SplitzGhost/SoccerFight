using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoccerFight
{
    /// <summary>
    /// The options page: display, effects and rebindable controls. Built once per owner — the pause
    /// menu and the title screen each place it in their own canvas and animate the returned root.
    /// </summary>
    public sealed class SettingsPanel
    {
        public static readonly Vector2 Size = new Vector2(1060f, 840f);

        public RectTransform Root { get; private set; }
        public CanvasGroup Group { get; private set; }
        /// <summary>Widget that takes keyboard focus when the page opens.</summary>
        public Selectable First { get; private set; }
        public bool IsCapturing => capturing;

        public event System.Action BackRequested;

        readonly Dictionary<GameAction, UiKit.KeyRow> keyRows = new Dictionary<GameAction, UiKit.KeyRow>();
        bool capturing;
        GameAction captureAction;
        int captureFrame;
        float lastCaptureEnd = -10f;

        public void Build(Transform parent, bool withBackButton)
        {
            // tall enough for ten key rows next to the display/effects column
            Root = UiKit.Panel(parent, "Settings", Size, out var group);
            Group = group;
            UiKit.Label("Title", Root, "EINSTELLUNGEN", 34f, Color.white, TextAlignmentOptions.Center, new Vector2(0f, 366f), new Vector2(1000f, 50f), true, 14f);
            UiKit.Img("Divider", Root, UiArt.LineFade, Color.white.WithAlpha(0.1f), new Vector2(0f, 20f), new Vector2(600f, 2f)).rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            const float colW = 400f;
            float lx = -255f, rx = 255f;

            UiKit.Section(Root, "ANZEIGE", new Vector2(lx, 294f), colW);
            First = UiKit.MakeToggle(Root, "VOLLBILD", new Vector2(lx, 246f), colW,
                () => GameSettings.Fullscreen, v => { GameSettings.Fullscreen = v; Apply(); });
            UiKit.MakeToggle(Root, "VSYNC", new Vector2(lx, 198f), colW,
                () => GameSettings.VSync, v => { GameSettings.VSync = v; Apply(); });
            UiKit.MakeToggle(Root, "FPS ANZEIGEN", new Vector2(lx, 150f), colW,
                () => GameSettings.ShowFps, v => { GameSettings.ShowFps = v; Apply(); });

            UiKit.Section(Root, "EFFEKTE", new Vector2(lx, 86f), colW);
            UiKit.MakeSlider(Root, "BILDSCHIRMWACKELN", new Vector2(lx, 32f), colW, 0f, 1.5f,
                () => GameSettings.ScreenShake, v => { GameSettings.ScreenShake = v; GameSettings.Save(); }, v => Mathf.RoundToInt(v * 100f) + "%");
            UiKit.MakeSlider(Root, "LEUCHTEN (BLOOM)", new Vector2(lx, -38f), colW, 0f, 1.5f,
                () => GameSettings.Bloom, v => { GameSettings.Bloom = v; GameSettings.Save(); }, v => Mathf.RoundToInt(v * 100f) + "%");
            UiKit.MakeToggle(Root, "FARBSAUM-EFFEKT", new Vector2(lx, -100f), colW,
                () => GameSettings.ChromaticAberration, v => { GameSettings.ChromaticAberration = v; Apply(); });

            UiKit.Section(Root, "TON", new Vector2(lx, -164f), colW);
            UiKit.MakeSlider(Root, "LAUTSTÄRKE", new Vector2(lx, -218f), colW, 0f, 1f,
                () => GameSettings.Volume, v => { GameSettings.Volume = v; GameSettings.Save(); }, v => Mathf.RoundToInt(v * 100f) + "%");

            UiKit.Section(Root, "STEUERUNG", new Vector2(rx, 294f), colW);
            float y = 248f;
            foreach (var a in KeyBindings.All)
            {
                var action = a;
                keyRows[a] = UiKit.MakeKeyRow(Root, KeyBindings.ActionName(a), new Vector2(rx, y), colW, () => BeginCapture(action));
                y -= 44f;
            }
            UiKit.MakeButton(Root, "STANDARD WIEDERHERSTELLEN", new Vector2(rx, y - 31f), new Vector2(305f, 83f), KeyBindings.ResetDefaults, false, 14f);
            UiKit.Label("KeyHint", Root, "TASTE ANKLICKEN, DANN NEUE TASTE DRÜCKEN", 11f, Palette.UiMuted, TextAlignmentOptions.Center, new Vector2(rx, y - 94f), new Vector2(colW, 20f), true, 4f);
            UiKit.Label("Pause", Root, "ESC  PAUSE  ·  F1  FPS  ·  F2  VSYNC  ·  F3  DEVELOPER", 11f, Palette.UiMuted, TextAlignmentOptions.Center, new Vector2(rx, y - 116f), new Vector2(colW, 20f), true, 4f);

            if (withBackButton)
                UiKit.MakeButton(Root, "ZURÜCK", new Vector2(0f, -354f), new Vector2(354f, 91f), () => BackRequested?.Invoke(), true);

            KeyBindings.Changed += RefreshKeys;
            RefreshKeys();
        }

        static void Apply()
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

        public void CancelCapture()
        {
            if (!capturing) return;
            capturing = false;
            lastCaptureEnd = Time.unscaledTime;
            RefreshKeys();
        }

        public void Update(float udt)
        {
            if (capturing && Time.frameCount > captureFrame)
            {
                if (KeyBindings.TryCapture(out var b, out bool cancelled))
                {
                    KeyBindings.Set(captureAction, b);
                    CancelCapture();
                }
                else if (cancelled) CancelCapture();
            }

            // key rows: pulse while waiting for input, hover highlight otherwise
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);
            foreach (var kv in keyRows)
            {
                var row = kv.Value;
                bool waiting = capturing && kv.Key == captureAction;
                row.Background.color = Color.white;
                row.Rim.color = waiting ? Palette.ShotCyan.WithAlpha(.3f + .3f * pulse) : Color.clear;
                row.Key.color = waiting ? Color.white : Color.Lerp(Palette.UiText, Palette.ShotCyan, row.Anim.Hover * 0.6f);
            }
        }
    }
}
