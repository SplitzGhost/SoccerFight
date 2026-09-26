using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SoccerFight
{
    /// <summary>Hover / press / keyboard-selection springs for a widget, running on unscaled time.</summary>
    public sealed class UiAnim : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        public float Hover, Press;
        public System.Action<UiAnim> Apply;
        bool hovered, pressed, selected;
        float hoverVel, pressVel;

        public void OnPointerEnter(PointerEventData e) => hovered = true;
        public void OnPointerExit(PointerEventData e) { hovered = false; pressed = false; }
        public void OnPointerDown(PointerEventData e) => pressed = true;
        public void OnPointerUp(PointerEventData e) => pressed = false;
        public void OnSelect(BaseEventData e) => selected = true;
        public void OnDeselect(BaseEventData e) => selected = false;

        void OnDisable()
        {
            hovered = pressed = selected = false;
            Hover = Press = hoverVel = pressVel = 0f;
            Apply?.Invoke(this);
        }

        void Update()
        {
            float dt = Mathf.Min(TimeFx.UiDelta, 0.05f);
            MathUtil.Spring(ref Hover, ref hoverVel, hovered || selected ? 1f : 0f, 4.5f, 0.85f, dt);
            MathUtil.Spring(ref Press, ref pressVel, pressed ? 1f : 0f, 7f, 0.7f, dt);
            Apply?.Invoke(this);
        }
    }

    /// <summary>Gemeinsame Erzeuger für plastische Buttons, Schalter, Regler und Menüplatten.</summary>
    public static class UiKit
    {
        public static readonly Color ButtonBase = new Color(0.36f, 0.59f, 0.67f, 1f);
        public static readonly Color ButtonHover = new Color(0.46f, 0.70f, 0.77f, 1f);
        public static readonly Color Track = new Color(0.14f, 0.2f, 0.3f, 1f);

        public static RectTransform Node(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image Img(string name, Transform parent, Sprite sprite, Color color, Vector2 pos, Vector2 size,
            Image.Type type = Image.Type.Simple, bool raycast = false)
        {
            var rt = Node(name, parent, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = type;
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static TextMeshProUGUI Label(string name, Transform parent, string text, float size, Color color,
            TextAlignmentOptions align, Vector2 pos, Vector2 box, bool bold = true, float spacing = 0f)
        {
            var rt = Node(name, parent, pos, box);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = bold ? UiArt.FontBold : UiArt.FontRegular;
            var mat = bold ? UiArt.FontBoldShadow : UiArt.FontRegularShadow;
            if (mat != null) t.fontSharedMaterial = mat;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.characterSpacing = spacing;
            t.raycastTarget = false;
            t.text = text;
            return t;
        }

        /// <summary>Abgeschrägte Platte mit ruhigem Hover und sichtbarer Unterkante beim Drücken.</summary>
        public static Button MakeButton(Transform parent, string text, Vector2 pos, Vector2 size, System.Action onClick, bool primary = false, float fontSize = 19f)
        {
            var sprite = ExactButtonArt.Action(text);
            var bg = Img(text, parent, sprite != null ? sprite : ExactButtonArt.Plate(primary, text.Contains("LÖSCHEN") || text == "BEENDEN"),
                Color.white, pos, size, sprite != null ? Image.Type.Simple : Image.Type.Sliced, true);
            bg.preserveAspect = sprite != null;
            if (sprite == null)
                Label("Label", bg.transform, text, fontSize, primary ? MenuArt.Ink : Color.white,
                    TextAlignmentOptions.Center, Vector2.zero, size - new Vector2(24f, 0f), true, 1f);
            var button = bg.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => onClick?.Invoke());
            var anim = bg.gameObject.AddComponent<UiAnim>();
            anim.Apply = a => bg.rectTransform.anchoredPosition = pos + new Vector2(0f, -a.Press * 3f);
            return button;
        }

        public static TextMeshProUGUI Section(Transform parent, string text, Vector2 pos, float width)
        {
            var t = Label(text, parent, text, 14f, Palette.ShotCyan, TextAlignmentOptions.Left, pos, new Vector2(width, 22f), true, 8f);
            Img("Line", parent, UiArt.LineFade, Palette.ShotCyan.WithAlpha(0.25f), pos + new Vector2(0f, -18f), new Vector2(width, 2f));
            return t;
        }

        /// <summary>Label on the left, animated switch on the right; the whole row is clickable.</summary>
        public static Button MakeToggle(Transform parent, string text, Vector2 pos, float width, System.Func<bool> get, System.Action<bool> set, float height = 44f)
        {
            ButtonSkin.Build();
            var artwork = ExactButtonArt.Get("toggle-row-" + text);
            var row = Img(text + " Row", parent, artwork != null ? artwork : ButtonSkin.Plate, Color.white, pos, new Vector2(width, height), artwork != null ? Image.Type.Simple : Image.Type.Sliced, true);
            if (artwork == null) Label("Label", row.transform, text, 16f, Palette.UiText, TextAlignmentOptions.Left,
                new Vector2(-width * .15f, 0f), new Vector2(width * .7f, 30f), true, 3f);
            bool dev = artwork != null && artwork.rect.width < 300f;
            string on = dev ? "dev-toggle-on" : "toggle-on", off = dev ? "dev-toggle-off" : "toggle-off";
            var toggle = Img("Schalter", row.transform, ExactButtonArt.Get(get() ? on : off), Color.white,
                new Vector2(width * (dev ? -.285f : .212f), 0f), new Vector2(width * (dev ? .285f : .397f), height * (dev ? .632f : .68f)));
            var button = row.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => set(!get()));
            var anim = row.gameObject.AddComponent<UiAnim>();
            anim.Apply = a => toggle.sprite = ExactButtonArt.Get(get() ? on : off);
            return button;
        }

        /// <summary>Beschriftung und Wert über dem eingelassenen Regler.</summary>
        public static Slider MakeSlider(Transform parent, string text, Vector2 pos, float width, float min, float max,
            System.Func<float> get, System.Action<float> set, System.Func<float, string> format, float height = 60f)
        {
            ButtonSkin.Build();
            bool speed = text == "SPIELTEMPO";
            var root = Node(text + " Slider", parent, pos, new Vector2(width, height));
            var artwork = ExactButtonArt.Get("slider-row-" + text);
            Img("Originalzeile", root, artwork != null ? artwork : ButtonSkin.Plate, Color.white, Vector2.zero, new Vector2(width, speed ? height : 44f), artwork != null ? Image.Type.Simple : Image.Type.Sliced);
            if (artwork == null) Label("Label", root, text, 15f, Color.white, TextAlignmentOptions.Left,
                new Vector2(-width * .27f, 0f), new Vector2(width * .45f, 30f), true, 1f);
            var valueText = Label("Value", root, format(get()), 14f, Color.white, TextAlignmentOptions.Center,
                new Vector2(width * .4f, speed ? -height * .12f : 0f), new Vector2(width * .13f, 26f), true, 1f);
            float trackWidth = width * (speed ? .59f : .375f);
            var sliderRt = Node("Slider", root, new Vector2(width * (speed ? -.02f : .16f), speed ? -height * .12f : 0f), new Vector2(trackWidth, 30f));
            var bg = Img("Background", sliderRt, ExactButtonArt.Get(speed ? "dev-slider-track" : "slider-track"), Color.white, Vector2.zero,
                new Vector2(trackWidth, 26f), Image.Type.Sliced, true);
            var fillArea = Node("Fill Area", sliderRt, Vector2.zero, Vector2.zero);
            fillArea.anchorMin = new Vector2(0f, .5f); fillArea.anchorMax = new Vector2(1f, .5f);
            fillArea.sizeDelta = new Vector2(-8f, 12f);
            var fill = Img("Fill", fillArea, ExactButtonArt.Get("slider-fill"), Color.white, Vector2.zero, Vector2.zero, Image.Type.Sliced);
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.sizeDelta = new Vector2(8f, 0f);
            var handleArea = Node("Handle Area", sliderRt, Vector2.zero, Vector2.zero);
            handleArea.anchorMin = Vector2.zero; handleArea.anchorMax = Vector2.one;
            handleArea.sizeDelta = new Vector2(-22f, 0f);
            var handle = Img("Handle", handleArea, ExactButtonArt.Get(speed ? "dev-slider-thumb" : "slider-thumb"), Color.white, Vector2.zero,
                speed ? new Vector2(46f, 45f) : new Vector2(30f, 32f), Image.Type.Simple, true);
            handle.rectTransform.anchorMin = handle.rectTransform.anchorMax = new Vector2(0f, .5f);
            var slider = sliderRt.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.transition = Selectable.Transition.None;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = get();
            slider.onValueChanged.AddListener(v => { set(v); valueText.text = format(v); });
            return slider;
        }

        public sealed class KeyRow
        {
            public TextMeshProUGUI Key;
            public Image Background, Rim;
            public UiAnim Anim;
        }

        /// <summary>Action name on the left, a key button on the right showing the current binding.</summary>
        public static KeyRow MakeKeyRow(Transform parent, string action, Vector2 pos, float width, System.Action onClick)
        {
            ButtonSkin.Build();
            var row = Node(action + " Row", parent, pos, new Vector2(width, 46f));
            var artwork = ExactButtonArt.Get("key-row-" + action);
            Img("Originalzeile", row, artwork != null ? artwork : ButtonSkin.Plate, Color.white, Vector2.zero, new Vector2(width, 44f), artwork != null ? Image.Type.Simple : Image.Type.Sliced);
            if (artwork == null) Label("Action", row, action, 16f, Palette.UiText, TextAlignmentOptions.Left,
                new Vector2(-width * .2f, 0f), new Vector2(width * .6f, 30f), true, 3f);
            var rim = Img("Rim", row, ButtonSkin.Frame, Color.clear, new Vector2(width * .29f, 0f), new Vector2(width * .34f, 38f), Image.Type.Sliced);
            var bg = Img("Key", row, ExactButtonArt.Get("key-face"), Color.white, new Vector2(width * .29f, 0f),
                new Vector2(width * .34f, 36f), Image.Type.Sliced, true);
            var key = Label("KeyLabel", bg.transform, "", 15f, Palette.UiText, TextAlignmentOptions.Center, Vector2.zero, new Vector2(width * .33f, 32f), true, 1f);
            key.font = ExactMenuFont.Get();
            var button = bg.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => onClick?.Invoke());
            var anim = bg.gameObject.AddComponent<UiAnim>();
            anim.Apply = a =>
            {
                float s = 1f + a.Hover * 0.03f - a.Press * 0.04f;
                bg.rectTransform.localScale = new Vector3(s, s, 1f);
                rim.rectTransform.localScale = new Vector3(s, s, 1f);
            };
            return new KeyRow { Key = key, Background = bg, Rim = rim, Anim = anim };
        }

        /// <summary>Dunkle Menüplatte mit Facettenrahmen und gedämpfter Lichtkante.</summary>
        public static RectTransform Panel(Transform parent, string name, Vector2 size, out CanvasGroup group)
        {
            ButtonSkin.Build();
            var rt = Node(name, parent, Vector2.zero, size);
            group = rt.gameObject.AddComponent<CanvasGroup>();
            Img("Shadow", rt, UiArt.Glow, new Color(0f, 0f, 0.02f, 0.55f), new Vector2(0f, -20f), size * 1.35f);
            Img("Border", rt, ButtonSkin.Frame, ButtonSkin.Slate.WithAlpha(0.6f), Vector2.zero, size + new Vector2(3f, 3f), Image.Type.Sliced);
            Img("Glass", rt, ButtonSkin.Panel, Color.white, Vector2.zero, size, Image.Type.Sliced, true);
            Img("Top Light", rt, UiArt.LineFade, Palette.ShotCyan.WithAlpha(0.12f), new Vector2(0f, size.y * 0.5f - 1f), new Vector2(size.x * 0.7f, 2f));
            return rt;
        }
    }
}
