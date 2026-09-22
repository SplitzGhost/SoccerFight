using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoccerFight
{
    /// <summary>
    /// A title-screen button in the language of the in-game cards: a dark glass body lit from the
    /// top, a hairline frame and an under-glow in the button's accent colour, a white icon and a
    /// tracked label; a thin dark lip underneath that the body sinks into when the ball hits it.
    /// Filled buttons (SPIELEN) are solid accent with dark print. Drawn by the menu every frame
    /// through Style(hover, hit, punch, fade) — buttons are hit by the kicked ball, not by uGUI,
    /// so they carry no Button component.
    /// </summary>
    public sealed class ChunkButton
    {
        public RectTransform Root, Face;
        public Image Glow, Shadow, Lip, Body, Wash, Gloss, Rim, Flash, Icon, Ring;
        public TextMeshProUGUI Label, Sub;
        public Color Color;
        public Vector2 Size;
        public bool Disabled;
        public bool Filled;
        RectTransform shine;
        Image shineImg;
        float shineT;
        readonly float lip;

        public const float LipHeight = 6f;

        public ChunkButton(Transform parent, string name, Vector2 pos, Vector2 size, Color color, string label, float fontSize,
            Sprite icon = null, float iconSize = 0f, bool shiny = false, bool filled = false)
        {
            Size = size;
            Color = color;
            Filled = filled;
            lip = LipHeight * Mathf.Clamp(size.y / 110f, 0.7f, 1.2f);
            Root = UiKit.Node(name, parent, pos, size);
            Glow = UiKit.Img("Glow", Root, UiArt.Glow, color.WithAlpha(0f), new Vector2(0f, -4f), size + new Vector2(150f, 130f));
            Shadow = UiKit.Img("Shadow", Root, UiArt.Glow, new Color(0f, 0.01f, 0.03f, 0.5f), new Vector2(0f, -14f), size * 1.1f + new Vector2(40f, 50f));
            Lip = UiKit.Img("Lip", Root, MenuArt.Body, LipColor(color), new Vector2(0f, -lip * 0.5f), size + new Vector2(0f, lip), Image.Type.Sliced);
            Face = UiKit.Node("Face", Root, Vector2.zero, size);
            Body = UiKit.Img("Body", Face, MenuArt.Body, BodyColor(color), Vector2.zero, size, Image.Type.Sliced);
            // the accent light welling up from the bottom of the glass
            Wash = UiKit.Img("Wash", Face, MenuArt.Gloss, color.WithAlpha(0f), new Vector2(0f, -size.y * 0.25f), new Vector2(size.x - 4f, size.y * 0.5f), Image.Type.Sliced);
            Wash.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);
            Gloss = UiKit.Img("Gloss", Face, MenuArt.Gloss, Color.white.WithAlpha(0f), new Vector2(0f, size.y * 0.25f), new Vector2(size.x - 4f, size.y * 0.5f), Image.Type.Sliced);
            if (shiny)
            {
                // a glint sweeps over the body every few seconds (clipped to the body's shape)
                var maskImg = UiKit.Img("ShineMask", Face, MenuArt.Body, Color.white, Vector2.zero, size, Image.Type.Sliced);
                var mask = maskImg.gameObject.AddComponent<Mask>();
                mask.showMaskGraphic = false;
                shineImg = UiKit.Img("Shine", maskImg.transform, MenuArt.Shine, Color.white.WithAlpha(0.4f), new Vector2(-size.x, 0f), new Vector2(size.y * 0.6f, size.y * 1.6f));
                shine = shineImg.rectTransform;
            }
            Rim = UiKit.Img("Rim", Face, MenuArt.Frame, color.WithAlpha(0.5f), Vector2.zero, size + new Vector2(2f, 2f), Image.Type.Sliced);
            if (icon != null)
            {
                Icon = UiKit.Img("Icon", Face, icon, Color.white, Vector2.zero, Vector2.one * iconSize);
                Icon.preserveAspect = true;
            }
            if (!string.IsNullOrEmpty(label))
            {
                Label = MenuArt.Label("Label", Face, label, fontSize, Color.white, Vector2.zero, size, TextAlignmentOptions.Center, fontSize * 0.14f,
                    filled ? MenuArt.TextPlate : MenuArt.TextHeavy);
            }
            Flash = UiKit.Img("Flash", Face, MenuArt.Body, Color.white.WithAlpha(0f), Vector2.zero, size, Image.Type.Sliced);
        }

        Color BodyColor(Color c) => Filled ? c : Color.Lerp(MenuArt.Glass, new Color(c.r, c.g, c.b, MenuArt.Glass.a), 0.015f);

        Color LipColor(Color c) => Filled ? new Color(c.r * 0.45f, c.g * 0.35f, c.b * 0.25f, 1f) : new Color(0.01f, 0.025f, 0.04f, 0.95f);

        /// <summary>Icon on the left, label to the right of it.</summary>
        public ChunkButton IconLeft(float pad = 18f)
        {
            if (Icon == null || Label == null) return this;
            float iconW = Icon.rectTransform.sizeDelta.x;
            Icon.rectTransform.anchoredPosition = new Vector2(-Size.x * 0.5f + pad + iconW * 0.5f, 0f);
            Label.rectTransform.anchoredPosition = new Vector2(iconW * 0.5f + pad * 0.3f, 0f);
            Label.rectTransform.sizeDelta = new Vector2(Size.x - iconW - pad * 2f, Size.y);
            return this;
        }

        /// <summary>Icon on top in a lit ring, label underneath (the big feature tiles).</summary>
        public ChunkButton IconTop(string sub = null)
        {
            if (Icon == null || Label == null) return this;
            float iconH = Icon.rectTransform.sizeDelta.y;
            Vector2 at = new Vector2(0f, Size.y * 0.5f - iconH * 0.5f - 34f);
            Icon.rectTransform.anchoredPosition = at;
            Icon.rectTransform.sizeDelta = Vector2.one * iconH * 0.62f;
            Ring = UiKit.Img("Ring", Face, MenuArt.RoundFrame, Color.WithAlpha(0.7f), at, Vector2.one * iconH);
            var disc = UiKit.Img("Disc", Face, MenuArt.Round, new Color(0.02f, 0.05f, 0.08f, 0.6f), at, Vector2.one * (iconH - 6f));
            disc.transform.SetSiblingIndex(Ring.transform.GetSiblingIndex());
            Icon.transform.SetAsLastSibling();
            Flash.transform.SetAsLastSibling();
            float labelY = -Size.y * 0.5f + Label.fontSize * 0.8f + (sub != null ? 36f : 18f);
            Label.rectTransform.anchoredPosition = new Vector2(0f, labelY);
            if (sub != null)
            {
                Sub = MenuArt.Label("Sub", Face, sub, 18f, new Color(0.7f, 0.8f, 0.86f), new Vector2(0f, labelY - Label.fontSize * 0.5f - 18f), new Vector2(Size.x - 30f, 26f),
                    TextAlignmentOptions.Center, 2f, MenuArt.TextHeavySoft);
            }
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
            Face.anchoredPosition = new Vector2(0f, -lip * press + h * 2f);
            float sx = 1f + h * 0.03f + punch * 0.06f, sy = 1f + h * 0.03f - punch * 0.08f;
            Root.localScale = new Vector3(sx, sy, 1f);
            Color c = Disabled ? Color.Lerp(Color, new Color(0.45f, 0.52f, 0.58f), 0.6f) : Color;
            Color bright = Color.Lerp(c, Color.white, 0.45f);

            Body.color = (Filled ? Color.Lerp(c, Color.white, h * 0.12f) : Color.Lerp(BodyColor(c), new Color(0.1f, 0.17f, 0.24f, 0.96f), h * 0.5f)).WithAlpha((Filled ? 1f : 0.94f) * fade);
            Lip.color = LipColor(c).WithAlpha(fade);
            Shadow.color = new Color(0f, 0.01f, 0.03f, 0.5f * fade);
            Wash.color = (Filled ? Color.white : c).WithAlpha(fade * (Filled ? 0f : 0.012f + 0.06f * h));
            Gloss.color = Color.white.WithAlpha(fade * (Filled ? 0.3f : 0.05f + 0.04f * h));
            Rim.color = (Filled ? Color.Lerp(c, Color.white, 0.6f) : bright).WithAlpha(fade * (Filled ? 0.7f : 0.35f + 0.55f * h));
            Glow.color = c.WithAlpha(fade * (0.1f * h + 0.45f * hit + (Filled ? 0.08f : 0f)));
            Flash.color = Color.white.WithAlpha(fade * hit * 0.5f);
            if (Icon != null) Icon.color = (Filled ? MenuArt.Ink : Color.Lerp(bright, Color.white, 0.25f + 0.5f * h)).WithAlpha(fade * (Disabled ? 0.7f : 1f));
            if (Ring != null) Ring.color = bright.WithAlpha(fade * (0.45f + 0.4f * h));
            if (Label != null)
            {
                var mat = Filled ? MenuArt.TextPlate : MenuArt.TextHeavy;
                if (mat != null && Label.fontSharedMaterial != mat) Label.fontSharedMaterial = mat;
            }
            if (Label != null) Label.color = (Filled ? MenuArt.Ink : Disabled ? new Color(0.72f, 0.78f, 0.84f) : Color.white).WithAlpha(fade);
            if (Sub != null) Sub.color = Sub.color.WithAlpha(fade);
            if (shine != null)
            {
                shineT += Time.unscaledDeltaTime;
                const float period = 3.8f, sweep = 0.7f;
                if (shineT > period) shineT -= period;
                float u = Mathf.Clamp01(shineT / sweep);
                shine.anchoredPosition = new Vector2(Mathf.Lerp(-Size.x * 0.7f, Size.x * 0.7f, MathUtil.EaseInOutSine(u)), 0f);
                shineImg.color = Color.white.WithAlpha(fade * 0.35f * (u < 1f ? 1f : 0f));
            }
        }
    }

    /// <summary>Small helpers shared by the menu pages.</summary>
    public static class MenuUi
    {
        /// <summary>A dark glass panel with a soft shadow, a hairline frame and a lit top edge in the accent colour.</summary>
        public static Image Plate(Transform parent, string name, Vector2 pos, Vector2 size, Color accent, float rim = 0.3f)
        {
            var rt = UiKit.Node(name, parent, pos, size);
            UiKit.Img("Shadow", rt, UiArt.Glow, new Color(0f, 0.01f, 0.03f, 0.45f), new Vector2(0f, -12f), size * 1.08f + new Vector2(60f, 60f));
            var body = UiKit.Img("Body", rt, MenuArt.CardBody, MenuArt.Glass, Vector2.zero, size, Image.Type.Sliced);
            UiKit.Img("Frame", rt, MenuArt.Frame, Color.Lerp(accent, Color.white, 0.3f).WithAlpha(rim), Vector2.zero, size + new Vector2(2f, 2f), Image.Type.Sliced);
            UiKit.Img("TopLight", rt, UiArt.LineFade, accent.WithAlpha(0.55f), new Vector2(0f, size.y * 0.5f - 1f), new Vector2(size.x * 0.7f, 2f));
            return body;
        }

        /// <summary>A heading between two thin lines that fade out (like the in-game stage titles).</summary>
        public static TextMeshProUGUI Banner(Transform parent, string name, string text, Vector2 pos, float width, Color color, float fontSize = 24f)
        {
            var rt = UiKit.Node(name, parent, pos, new Vector2(width, fontSize * 1.6f));
            var t = MenuArt.Label("Text", rt, text, fontSize, color, Vector2.zero, new Vector2(width, fontSize * 1.6f), TextAlignmentOptions.Center, fontSize * 0.35f, MenuArt.TextHeavySoft);
            float tw = t.GetPreferredValues(text).x;
            float lineW = Mathf.Max(20f, (width - tw) * 0.5f - 26f);
            var l = UiKit.Img("LineL", rt, UiArt.LineFade, color.WithAlpha(0.45f), new Vector2(-tw * 0.5f - 18f - lineW * 0.5f, 0f), new Vector2(lineW, 2f));
            UiKit.Img("LineR", rt, UiArt.LineFade, color.WithAlpha(0.45f), new Vector2(tw * 0.5f + 18f + lineW * 0.5f, 0f), new Vector2(lineW, 2f));
            l.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);
            UiKit.Img("DotL", rt, UiArt.Diamond, color.WithAlpha(0.8f), new Vector2(-tw * 0.5f - 12f, 0f), new Vector2(9f, 9f));
            UiKit.Img("DotR", rt, UiArt.Diamond, color.WithAlpha(0.8f), new Vector2(tw * 0.5f + 12f, 0f), new Vector2(9f, 9f));
            return t;
        }

        /// <summary>Small tag ("NEU", "BALD") in the corner of a tile.</summary>
        public static RectTransform Tag(Transform parent, string text, Vector2 pos, Color color, float fontSize = 17f)
        {
            float w = fontSize * (text.Length * 0.9f) + 30f;
            var rt = UiKit.Node("Tag", parent, pos, new Vector2(w, fontSize * 1.75f));
            UiKit.Img("Glow", rt, UiArt.Glow, color.WithAlpha(0.3f), Vector2.zero, new Vector2(w + 50f, fontSize * 3.5f));
            UiKit.Img("Body", rt, UiArt.Pill, color, Vector2.zero, new Vector2(w, fontSize * 1.75f), Image.Type.Sliced);
            MenuArt.Label("Text", rt, text, fontSize, MenuArt.Ink, new Vector2(0f, 0.5f), new Vector2(w, fontSize * 1.75f), TextAlignmentOptions.Center, fontSize * 0.2f, MenuArt.TextPlate);
            return rt;
        }

        /// <summary>Round glass medallion (rank, avatar) with a hairline ring in the accent colour.</summary>
        public static Image Medallion(Transform parent, string name, Vector2 pos, float size, Color accent)
        {
            var rt = UiKit.Node(name, parent, pos, Vector2.one * size);
            var body = UiKit.Img("Body", rt, MenuArt.Round, Color.Lerp(MenuArt.Glass, accent, 0.2f).WithAlpha(0.95f), Vector2.zero, Vector2.one * size);
            UiKit.Img("Ring", rt, MenuArt.RoundFrame, accent.WithAlpha(0.9f), Vector2.zero, Vector2.one * (size + 4f));
            return body;
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

        /// <summary>Puts a UI tree on one layer (the title screen's camera only draws its own layers).</summary>
        public static void SetLayer(Transform t, int layer) => MenuVista.SetLayer(t, layer);
    }
}


namespace SoccerFight
{
    /// <summary>Pages of the title screen.</summary>
    public static class MenuPage
    {
        public const int Main = 0, Characters = 1, Settings = 2, Shop = 3, Ranking = 4, Friends = 5, Events = 6, Info = 7,
            Skills = 8, Starter = 9, StarterSkills = 10, Count = 11;

        /// <summary>The first-launch pages: no way back to the title screen until they are done.</summary>
        public static bool IsOnboarding(int page) => page == Starter || page == StarterSkills;
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
        /// <summary>Extra visibility inside the page (a tab that is not shown): multiplied onto the page fade.</summary>
        public System.Func<float> Visible;
        /// <summary>Visibility the page (or an entrance animation) gives this target.</summary>
        public float Fade = 1f;
    }
}
