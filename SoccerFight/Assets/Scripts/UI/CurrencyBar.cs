using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoccerFight
{
    /// <summary>
    /// Kristall- oder Münzleiste im Stil der freigegebenen Charakteransicht (Symbol + Steinleiste, weiße Zahl).
    /// Überall im Menü dieselbe Grafik (<c>tools/newdesign/currency.js</c> schneidet sie aus dem Probebild).
    /// Die Zahl zählt zum neuen Stand, die Leiste hüpft kurz und leuchtet in ihrer Farbe.
    /// Maße in Pixeln des 1672×941-Probebilds mal <c>unit</c>.
    /// </summary>
    public sealed class CurrencyBar
    {
        public readonly RectTransform Root;
        public readonly CurrencyDef Currency;
        public Vector2 Size { get; }
        readonly RectTransform body;
        readonly TextMeshProUGUI amount;
        readonly Image glow, shine;
        readonly Color accent;
        /// <summary>Ruheposition (die Leiste hebt sich beim Zeigen etwas darüber).</summary>
        public Vector2 Home;
        float shown = -1f, pop, popVel, flash = 99f, time;

        static Sprite gems, coins;
        static Material numbers;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { gems = coins = null; numbers = null; }

        /// <summary>Weiße, kräftige Zahl mit dunkler Kante – dieselbe wie auf den Leisten.</summary>
        public static Material Numbers
        {
            get
            {
                if (numbers != null) return numbers;
                var src = UiArt.FontBold != null ? UiArt.FontBold.material : null;
                if (src == null) return null;
                numbers = new Material(src) { name = "Währungszahl" };
                numbers.SetFloat("_FaceDilate", 0.18f);
                numbers.EnableKeyword("OUTLINE_ON");
                numbers.SetColor("_OutlineColor", new Color(0.05f, 0.07f, 0.1f, 0.9f));
                numbers.SetFloat("_OutlineWidth", 0.16f);
                numbers.EnableKeyword("UNDERLAY_ON");
                numbers.SetColor("_UnderlayColor", new Color(0f, 0.02f, 0.05f, 0.55f));
                numbers.SetFloat("_UnderlayOffsetY", -0.7f);
                numbers.SetFloat("_UnderlayDilate", 0.2f);
                numbers.SetFloat("_UnderlaySoftness", 0.4f);
                return numbers;
            }
        }

        static Sprite Art(bool gem)
        {
            ref Sprite s = ref gem ? ref gems : ref coins;
            if (s != null) return s;
            var tex = Resources.Load<Texture2D>("Menu/Currency/" + (gem ? "gems" : "coins"));
            if (tex == null) return null;
            s = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            return s;
        }

        /// <param name="currency">Currencies.Gems oder Currencies.Coins</param>
        /// <param name="unit">Bildschirmpixel pro Probebild-Pixel (1 = so groß wie in der Charakteransicht)</param>
        public CurrencyBar(Transform parent, CurrencyDef currency, Vector2 pos, float unit)
        {
            Currency = currency;
            bool gem = currency == Currencies.Gems;
            // Größe, Zahlenfeld: vermessen im Probebild (Leiste rechts vom Symbol)
            Size = (gem ? new Vector2(123.7f, 57f) : new Vector2(135.3f, 52.7f)) * unit;
            accent = gem ? currency.Color : MetaUi.Gold;
            Home = pos;
            Root = UiKit.Node(gem ? "Kristalle" : "Münzen", parent, pos, Size);
            body = UiKit.Node("Leiste", Root, Vector2.zero, Size);
            glow = UiKit.Img("Licht", body, UiArt.Glow, Color.clear, new Vector2(-Size.x * 0.32f, 0f), new Vector2(Size.y * 2.4f, Size.y * 2.4f));
            var art = UiKit.Img("Bild", body, Art(gem), Color.white, Vector2.zero, Size);
            art.preserveAspect = true;
            // ein schmaler Lichtstreif, der beim Zeigen über die Leiste gleitet
            shine = UiKit.Img("Glanz", body, UiArt.Glow, Color.clear, Vector2.zero, new Vector2(Size.y * 0.5f, Size.y * 0.8f));
            shine.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -18f);
            float cx = (gem ? 0.131f : 0.12f) * Size.x;
            float w = (gem ? 0.58f : 0.66f) * Size.x;
            amount = UiKit.Label("Zahl", body, "0", 25f * unit, Color.white, TextAlignmentOptions.Center,
                new Vector2(cx, 0.03f * Size.y), new Vector2(w, Size.y * 0.62f));
            if (UiArt.FontBold != null) amount.font = UiArt.FontBold;
            if (Numbers != null) amount.fontSharedMaterial = Numbers;
            amount.fontStyle = FontStyles.Bold;
            amount.enableAutoSizing = true;
            amount.fontSizeMin = 11f * unit;
            amount.fontSizeMax = 25f * unit;
        }

        /// <summary>Beim nächsten Update sofort den echten Stand zeigen (Menü öffnet neu).</summary>
        public void Snap() => shown = -1f;

        /// <param name="hover">0..1 (Zeiger darüber)</param>
        public void Update(float udt, float hover = 0f, float punch = 0f)
        {
            time += udt;
            int target = Wallet.Get(Currency);
            if (shown < 0f) shown = target;
            if (Mathf.Abs(shown - target) > 0.5f)
            {
                shown = Mathf.MoveTowards(shown, target, Mathf.Max(30f, Mathf.Abs(target - shown) * 6f) * udt);
                if (Mathf.Abs(shown - target) <= 0.5f) { shown = target; popVel += 8f; flash = 0f; }
            }
            string s = Currencies.Format(Mathf.RoundToInt(shown));
            if (amount.text != s) amount.text = s;
            MathUtil.Spring(ref pop, ref popVel, 0f, 5f, 0.3f, udt);
            flash += udt;
            float f = Mathf.Clamp01(1f - flash / 0.45f);
            hover = Mathf.Clamp01(hover);
            amount.color = Color.Lerp(Color.white, Color.Lerp(Color.white, accent, 0.6f), f);
            glow.color = accent.WithAlpha(0.05f * hover + 0.16f * f);
            float sc = 1f + pop * 0.07f + hover * 0.04f + punch * 0.05f;
            body.localScale = new Vector3(sc + punch * 0.02f, sc - punch * 0.03f, 1f);
            Root.anchoredPosition = Home + new Vector2(0f, hover * 2f);
            // Glanz: gleitet beim Zeigen einmal pro Sekunde über die Zahl
            float u = Mathf.Repeat(time * 0.9f, 1f);
            shine.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-0.2f, 0.5f, u) * Size.x, 0f);
            shine.color = Color.white.WithAlpha(hover * 0.1f * Mathf.Sin(u * Mathf.PI));
        }
    }
}
