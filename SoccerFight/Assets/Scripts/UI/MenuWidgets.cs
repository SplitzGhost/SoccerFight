using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoccerFight
{
    /// <summary>
    /// A chunky title-screen button: dark keyline, a darker lip underneath that the body sinks into
    /// when it is hit, a lit body with a glossy top, an optional icon and a heavy label. Drawn by the
    /// menu every frame through Style(hover, hit, punch, fade) — buttons are hit by the kicked ball,
    /// not by uGUI, so they carry no Button component.
    /// </summary>
    public sealed class ChunkButton
    {
        public RectTransform Root, Face;
        public Image Glow, Keyline, Lip, Body, Gloss, Flash, Icon;
        public TextMeshProUGUI Label, Sub;
        public Color Color;
        public Vector2 Size;
        public bool Disabled;
        RectTransform shine;
        Image shineImg;
        float shineT;
        readonly float lip;

        public const float LipHeight = 10f;

        public ChunkButton(Transform parent, string name, Vector2 pos, Vector2 size, Color color, string label, float fontSize,
            Sprite icon = null, float iconSize = 0f, bool shiny = false, float radiusScale = 1f)
        {
            Size = size;
            Color = color;
            lip = LipHeight * Mathf.Clamp(size.y / 110f, 0.6f, 1.2f);
            Root = UiKit.Node(name, parent, pos, size);
            Glow = UiKit.Img("Glow", Root, UiArt.Glow, color.WithAlpha(0f), new Vector2(0f, -4f), size + new Vector2(170f, 150f));
            Keyline = UiKit.Img("Keyline", Root, MenuArt.Edge, MenuArt.Ink, new Vector2(0f, -lip * 0.5f), size + new Vector2(10f, 10f + lip), Image.Type.Sliced);
            Lip = UiKit.Img("Lip", Root, MenuArt.Edge, Darker(color), new Vector2(0f, -lip * 0.5f), size + new Vector2(0f, lip), Image.Type.Sliced);
            Face = UiKit.Node("Face", Root, Vector2.zero, size);
            Body = UiKit.Img("Body", Face, MenuArt.Body, color, Vector2.zero, size, Image.Type.Sliced);
            Gloss = UiKit.Img("Gloss", Face, MenuArt.Gloss, Color.white.WithAlpha(0.28f), new Vector2(0f, size.y * 0.2f), new Vector2(size.x - 16f, size.y * 0.46f), Image.Type.Sliced);
            if (shiny)
            {
                // a glint sweeps over the body every few seconds (clipped to the body's shape)
                var maskImg = UiKit.Img("ShineMask", Face, MenuArt.Body, Color.white, Vector2.zero, size, Image.Type.Sliced);
                var mask = maskImg.gameObject.AddComponent<Mask>();
                mask.showMaskGraphic = false;
                shineImg = UiKit.Img("Shine", maskImg.transform, MenuArt.Shine, Color.white.WithAlpha(0.55f), new Vector2(-size.x, 0f), new Vector2(size.y * 0.7f, size.y * 1.6f));
                shine = shineImg.rectTransform;
            }
            if (icon != null)
            {
                Icon = UiKit.Img("Icon", Face, icon, Color.white, Vector2.zero, Vector2.one * iconSize);
                Icon.preserveAspect = true;
            }
            if (!string.IsNullOrEmpty(label))
                Label = MenuArt.Label("Label", Face, label, fontSize, Color.white, Vector2.zero, size, TextAlignmentOptions.Center, 1.5f);
            Flash = UiKit.Img("Flash", Face, MenuArt.Body, Color.white.WithAlpha(0f), Vector2.zero, size, Image.Type.Sliced);
        }

        public static Color Darker(Color c) => new Color(c.r * 0.55f, c.g * 0.5f, c.b * 0.62f, 1f);

        /// <summary>Icon on the left, label to the right of it.</summary>
        public ChunkButton IconLeft(float pad = 18f)
        {
            if (Icon == null || Label == null) return this;
            float iconW = Icon.rectTransform.sizeDelta.x;
            Icon.rectTransform.anchoredPosition = new Vector2(-Size.x * 0.5f + pad + iconW * 0.5f, 2f);
            Label.rectTransform.anchoredPosition = new Vector2(iconW * 0.5f + pad * 0.3f, 0f);
            Label.rectTransform.sizeDelta = new Vector2(Size.x - iconW - pad * 2f, Size.y);
            return this;
        }

        /// <summary>Icon on top, label underneath (the big feature tiles).</summary>
        public ChunkButton IconTop()
        {
            if (Icon == null || Label == null) return this;
            float iconH = Icon.rectTransform.sizeDelta.y;
            Icon.rectTransform.anchoredPosition = new Vector2(0f, Size.y * 0.5f - iconH * 0.5f - 12f);
            Label.rectTransform.anchoredPosition = new Vector2(0f, -Size.y * 0.5f + Label.fontSize * 0.75f + 10f);
            return this;
        }

        /// <param name="hover">0..1 aim on the button</param>
        /// <param name="hit">1 right after a ball landed on it, decays</param>
        /// <param name="punch">spring value of the impact squash</param>
        /// <param name="fade">page / entrance visibility</param>
        public void Style(float hover, float hit, float punch, float fade, float time)
        {
            float h = Disabled ? hover * 0.4f : hover;
            float press = Mathf.Clamp01(hit * 1.4f);
            Face.anchoredPosition = new Vector2(0f, -lip * press);
            float sx = 1f + h * 0.035f + punch * 0.07f, sy = 1f + h * 0.035f - punch * 0.1f;
            Root.localScale = new Vector3(sx, sy, 1f);
            Color c = Disabled ? Color.Lerp(Color, new Color(0.5f, 0.5f, 0.6f), 0.55f) : Color;
            Body.color = Color.Lerp(c, Color.white, h * 0.14f).WithAlpha(fade);
            Lip.color = Darker(c).WithAlpha(fade);
            Keyline.color = MenuArt.Ink.WithAlpha(fade);
            Gloss.color = Color.white.WithAlpha(fade * (0.24f + 0.1f * h));
            Glow.color = Color.Lerp(c, Color.white, 0.3f).WithAlpha(fade * (0.08f * h + 0.5f * hit));
            Flash.color = Color.white.WithAlpha(fade * hit * 0.6f);
            if (Icon != null) Icon.color = Color.white.WithAlpha(fade * (Disabled ? 0.75f : 1f));
            if (Label != null) Label.color = (Disabled ? new Color(0.88f, 0.88f, 0.95f) : Color.white).WithAlpha(fade);
            if (Sub != null) Sub.color = Sub.color.WithAlpha(fade);
            if (shine != null)
            {
                shineT += Time.unscaledDeltaTime;
                const float period = 3.4f, sweep = 0.6f;
                if (shineT > period) shineT -= period;
                float u = Mathf.Clamp01(shineT / sweep);
                shine.anchoredPosition = new Vector2(Mathf.Lerp(-Size.x * 0.7f, Size.x * 0.7f, MathUtil.EaseInOutSine(u)), 0f);
                shineImg.color = Color.white.WithAlpha(fade * 0.5f * (u < 1f ? 1f : 0f));
            }
        }
    }

    /// <summary>Small helpers shared by the menu pages.</summary>
    public static class MenuUi
    {
        /// <summary>A dark rounded plate with a keyline (info boxes, list rows).</summary>
        public static Image Plate(Transform parent, string name, Vector2 pos, Vector2 size, Color color, float keyline = 6f)
        {
            var rt = UiKit.Node(name, parent, pos, size);
            UiKit.Img("Keyline", rt, MenuArt.Edge, MenuArt.Ink, new Vector2(0f, -3f), size + new Vector2(keyline * 2f, keyline * 2f + 6f), Image.Type.Sliced);
            var body = UiKit.Img("Body", rt, MenuArt.CardBody, color, Vector2.zero, size, Image.Type.Sliced);
            return body;
        }

        /// <summary>Ribbon with a label across it.</summary>
        public static TextMeshProUGUI Ribbon(Transform parent, string name, string text, Vector2 pos, float width, Color color, float fontSize = 30f)
        {
            var rt = UiKit.Node(name, parent, pos, new Vector2(width, 64f));
            UiKit.Img("Keyline", rt, MenuArt.Ribbon, MenuArt.Ink, new Vector2(0f, -3f), new Vector2(width + 14f, 76f), Image.Type.Sliced);
            UiKit.Img("Body", rt, MenuArt.Ribbon, color, Vector2.zero, new Vector2(width, 60f), Image.Type.Sliced);
            return MenuArt.Label("Text", rt, text, fontSize, Color.white, new Vector2(0f, 2f), new Vector2(width - 60f, 60f));
        }

        /// <summary>Sticker burst ("NEU", "BALD") that wobbles on its own.</summary>
        public static RectTransform Sticker(Transform parent, string text, Vector2 pos, float size, Color color)
        {
            var rt = UiKit.Node("Sticker", parent, pos, Vector2.one * size);
            UiKit.Img("Keyline", rt, MenuArt.Sticker, MenuArt.Ink, Vector2.zero, Vector2.one * (size + 12f));
            UiKit.Img("Body", rt, MenuArt.Sticker, color, Vector2.zero, Vector2.one * size);
            var t = MenuArt.Label("Text", rt, text, size * 0.27f, Color.white, new Vector2(0f, 1f), Vector2.one * size, TextAlignmentOptions.Center, 0f, MenuArt.TextHeavySoft);
            t.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 12f);
            return rt;
        }

        /// <summary>Round icon medallion (profile picture, currency) with a keyline.</summary>
        public static Image Medallion(Transform parent, string name, Vector2 pos, float size, Color color)
        {
            var rt = UiKit.Node(name, parent, pos, Vector2.one * size);
            UiKit.Img("Keyline", rt, MenuArt.RoundEdge, MenuArt.Ink, new Vector2(0f, -2f), Vector2.one * (size + 12f));
            return UiKit.Img("Body", rt, MenuArt.Round, color, Vector2.zero, Vector2.one * size);
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
        }

        /// <summary>Pins a node to a screen edge / corner (anchor in 0..1) at an offset.</summary>
        public static void Pin(RectTransform rt, Vector2 anchor, Vector2 offset)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = offset;
        }
    }
}

namespace SoccerFight
{
    /// <summary>Pages of the title screen.</summary>
    public static class MenuPage
    {
        public const int Main = 0, Characters = 1, Settings = 2, Shop = 3, Ranking = 4, Friends = 5, Events = 6, Info = 7, Count = 8;
    }

    /// <summary>
    /// Something the kicked ball can hit. The hit box is the Root's rect (Size, in Root's own
    /// units), so targets inside moving or scaled containers stay accurate.
    /// </summary>
    public sealed class MenuTarget
    {
        public string Id;
        public RectTransform Root;
        public Vector2 Size;
        public int Page;
        public System.Action Action;
        /// <summary>Standard look; otherwise Draw is called.</summary>
        public ChunkButton Button;
        public System.Action<MenuTarget> Draw;
        public Color Accent = Color.white;
        /// <summary>Placeholder: the hit bounces off with a "coming soon" note instead of acting.</summary>
        public string Soon;
        public float Hover, HoverVel, Punch, PunchVel, Hit;
        /// <summary>Visibility the page (or an entrance animation) gives this target.</summary>
        public float Fade = 1f;
    }
}
