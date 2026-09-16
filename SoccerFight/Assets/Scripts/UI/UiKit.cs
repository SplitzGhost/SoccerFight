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
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            MathUtil.Spring(ref Hover, ref hoverVel, hovered || selected ? 1f : 0f, 4.5f, 0.85f, dt);
            MathUtil.Spring(ref Press, ref pressVel, pressed ? 1f : 0f, 7f, 0.7f, dt);
            Apply?.Invoke(this);
        }
    }

    /// <summary>Builders for the clean glass-style menu widgets.</summary>
    public static class UiKit
    {
        public static readonly Color ButtonBase = new Color(0.08f, 0.13f, 0.21f, 0.96f);
        public static readonly Color ButtonHover = new Color(0.12f, 0.2f, 0.32f, 1f);
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

        /// <summary>Pill button with an accent bar that grows on hover and a soft press squash.</summary>
        public static Button MakeButton(Transform parent, string text, Vector2 pos, Vector2 size, System.Action onClick, bool primary = false, float fontSize = 19f)
        {
            Color accent = Palette.ShotCyan;
            var glow = Img(text + " Glow", parent, UiArt.Glow, accent.WithAlpha(0f), pos, size + new Vector2(90f, 70f));
            // the rim sits behind the button so only a 1px border shows (UI blends in linear space)
            var rim = Img(text + " Rim", parent, UiArt.Pill, Color.white.WithAlpha(primary ? 0.3f : 0.14f), pos, size + new Vector2(2f, 2f), Image.Type.Sliced);
            var bg = Img(text, parent, UiArt.Pill, ButtonBase, pos, size, Image.Type.Sliced, true);
            var bar = Img("Accent", bg.transform, UiArt.Pill, accent, new Vector2(-size.x * 0.5f + 16f, 0f), new Vector2(6f, size.y * 0.42f), Image.Type.Sliced);
            var label = Label("Label", bg.transform, text, fontSize, Palette.UiText, TextAlignmentOptions.Center, Vector2.zero, size, true, 6f);
            var button = bg.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => onClick?.Invoke());
            var anim = bg.gameObject.AddComponent<UiAnim>();
            anim.Apply = a =>
            {
                float h = Mathf.Clamp01(a.Hover);
                float s = 1f + h * 0.03f - a.Press * 0.04f;
                bg.rectTransform.localScale = new Vector3(s, s, 1f);
                rim.rectTransform.localScale = new Vector3(s, s, 1f);
                glow.rectTransform.localScale = new Vector3(s, s, 1f);
                rim.color = Color.Lerp(Color.white.WithAlpha(primary ? 0.3f : 0.14f), accent.WithAlpha(0.7f), h);
                bg.color = Color.Lerp(primary ? new Color(0.1f, 0.2f, 0.3f, 0.98f) : ButtonBase, ButtonHover, h);
                glow.color = accent.WithAlpha(h * 0.16f + (primary ? 0.05f : 0f));
                bar.color = accent.WithAlpha(primary ? 1f : 0.35f + 0.65f * h);
                bar.rectTransform.sizeDelta = new Vector2(6f, size.y * (0.3f + 0.25f * h));
                label.color = Color.Lerp(Palette.UiText, Color.Lerp(accent, Color.white, 0.55f), h);
            };
            anim.Apply(anim);
            return button;
        }

        public static TextMeshProUGUI Section(Transform parent, string text, Vector2 pos, float width)
        {
            var t = Label(text, parent, text, 14f, Palette.ShotCyan, TextAlignmentOptions.Left, pos, new Vector2(width, 22f), true, 8f);
            Img("Line", parent, UiArt.LineFade, Palette.ShotCyan.WithAlpha(0.25f), pos + new Vector2(0f, -18f), new Vector2(width, 2f));
            return t;
        }

        /// <summary>Label on the left, animated switch on the right; the whole row is clickable.</summary>
        public static Button MakeToggle(Transform parent, string text, Vector2 pos, float width, System.Func<bool> get, System.Action<bool> set)
        {
            var row = Img(text + " Row", parent, UiArt.Pill, Color.white.WithAlpha(0f), pos, new Vector2(width, 44f), Image.Type.Sliced, true);
            var label = Label("Label", row.transform, text, 16f, Palette.UiText, TextAlignmentOptions.Left, new Vector2(-width * 0.5f + width * 0.35f + 14f, 0f), new Vector2(width * 0.7f, 30f), true, 3f);
            var track = Img("Track", row.transform, UiArt.Pill, Track, new Vector2(width * 0.5f - 40f, 0f), new Vector2(60f, 30f), Image.Type.Sliced);
            var knobGlow = Img("KnobGlow", track.transform, UiArt.Glow, Palette.ShotCyan.WithAlpha(0f), Vector2.zero, new Vector2(56f, 56f));
            var knob = Img("Knob", track.transform, UiArt.Circle, Color.white, Vector2.zero, new Vector2(22f, 22f));
            float value = get() ? 1f : 0f, vel = 0f;
            var button = row.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => set(!get()));
            var anim = row.gameObject.AddComponent<UiAnim>();
            anim.Apply = a =>
            {
                MathUtil.Spring(ref value, ref vel, get() ? 1f : 0f, 5f, 0.75f, Mathf.Min(Time.unscaledDeltaTime, 0.05f));
                float v = Mathf.Clamp01(value);
                knob.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-15f, 15f, value), 0f);
                knobGlow.rectTransform.anchoredPosition = knob.rectTransform.anchoredPosition;
                knobGlow.color = Palette.ShotCyan.WithAlpha(0.35f * v);
                track.color = Color.Lerp(Track, Palette.ShotCyan * 0.85f, v);
                row.color = new Color(0.16f, 0.3f, 0.45f, 0.35f * a.Hover);
                label.color = Color.Lerp(Palette.UiText, Color.white, a.Hover);
            };
            return button;
        }

        /// <summary>Label + value on top, pill slider below.</summary>
        public static Slider MakeSlider(Transform parent, string text, Vector2 pos, float width, float min, float max,
            System.Func<float> get, System.Action<float> set, System.Func<float, string> format)
        {
            var root = Node(text + " Slider", parent, pos, new Vector2(width, 60f));
            Label("Label", root, text, 16f, Palette.UiText, TextAlignmentOptions.Left, new Vector2(-width * 0.5f + width * 0.35f, 12f), new Vector2(width * 0.7f, 26f), true, 3f);
            var valueText = Label("Value", root, format(get()), 16f, Palette.ShotCyan, TextAlignmentOptions.Right, new Vector2(width * 0.5f - 60f, 12f), new Vector2(120f, 26f), true, 2f);

            var sliderRt = Node("Slider", root, new Vector2(0f, -14f), new Vector2(width, 26f));
            var bg = Img("Background", sliderRt, UiArt.Pill, Track, Vector2.zero, new Vector2(width, 8f), Image.Type.Sliced, true);
            var fillArea = Node("Fill Area", sliderRt, Vector2.zero, Vector2.zero);
            fillArea.anchorMin = new Vector2(0f, 0.5f); fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.sizeDelta = new Vector2(-8f, 8f);
            var fill = Img("Fill", fillArea, UiArt.Pill, Palette.ShotCyan, Vector2.zero, Vector2.zero, Image.Type.Sliced);
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.sizeDelta = new Vector2(8f, 0f);
            var handleArea = Node("Handle Area", sliderRt, Vector2.zero, Vector2.zero);
            handleArea.anchorMin = Vector2.zero; handleArea.anchorMax = Vector2.one;
            handleArea.sizeDelta = new Vector2(-22f, 0f);
            var handle = Img("Handle", handleArea, UiArt.Circle, Color.white, Vector2.zero, new Vector2(22f, 22f), Image.Type.Simple, true);
            handle.rectTransform.anchorMin = new Vector2(0f, 0.5f); handle.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            var handleGlow = Img("Glow", handle.transform, UiArt.Glow, Palette.ShotCyan.WithAlpha(0.25f), Vector2.zero, new Vector2(60f, 60f));
            handleGlow.transform.SetAsFirstSibling();

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
            var anim = sliderRt.gameObject.AddComponent<UiAnim>();
            anim.Apply = a =>
            {
                float s = 1f + a.Hover * 0.15f + a.Press * 0.1f;
                handle.rectTransform.localScale = new Vector3(s, s, 1f);
                handleGlow.color = Palette.ShotCyan.WithAlpha(0.2f + 0.25f * a.Hover);
            };
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
            var row = Node(action + " Row", parent, pos, new Vector2(width, 46f));
            Label("Action", row, action, 16f, Palette.UiText, TextAlignmentOptions.Left, new Vector2(-width * 0.5f + width * 0.3f, 0f), new Vector2(width * 0.6f, 30f), true, 3f);
            var rim = Img("Rim", row, UiArt.Pill, Color.white.WithAlpha(0.14f), new Vector2(width * 0.5f - 95f, 0f), new Vector2(192f, 42f), Image.Type.Sliced);
            var bg = Img("Key", row, UiArt.Pill, ButtonBase, new Vector2(width * 0.5f - 95f, 0f), new Vector2(190f, 40f), Image.Type.Sliced, true);
            var key = Label("KeyLabel", bg.transform, "", 15f, Palette.UiText, TextAlignmentOptions.Center, Vector2.zero, new Vector2(186f, 36f), true, 3f);
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

        /// <summary>Glass card with a soft drop shadow, a hairline border and a lit top edge.</summary>
        public static RectTransform Panel(Transform parent, string name, Vector2 size, out CanvasGroup group)
        {
            var rt = Node(name, parent, Vector2.zero, size);
            group = rt.gameObject.AddComponent<CanvasGroup>();
            Img("Shadow", rt, UiArt.Glow, new Color(0f, 0f, 0.02f, 0.55f), new Vector2(0f, -20f), size * 1.35f);
            Img("Border", rt, UiArt.Panel, Color.white.WithAlpha(0.1f), Vector2.zero, size + new Vector2(3f, 3f), Image.Type.Sliced);
            Img("Glass", rt, UiArt.Panel, new Color(0.045f, 0.075f, 0.12f, 0.995f), Vector2.zero, size, Image.Type.Sliced, true);
            Img("Top Light", rt, UiArt.LineFade, Palette.ShotCyan.WithAlpha(0.35f), new Vector2(0f, size.y * 0.5f - 1f), new Vector2(size.x * 0.7f, 2f));
            return rt;
        }
    }
}
