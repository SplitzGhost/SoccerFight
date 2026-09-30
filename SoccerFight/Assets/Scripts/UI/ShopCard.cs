using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoccerFight
{
    /// <summary>
    /// Die aus der Shop-Vorlage geschnittenen Bilder (tools/newdesign/shop.js → Resources/Menu/Shop): pro Spieler
    /// eine fertige Karte (Bild, Rahmen, Namensleiste) und die Teile der Leiste (Münze, gelber Knopf, „GEWÄHLT“).
    /// cards.json nennt Reihenfolge und Format (kleine Karten stehen zu zweit übereinander).
    /// </summary>
    public static class ShopArt
    {
        [System.Serializable] public sealed class Entry { public string id; public bool small; }
        [System.Serializable] sealed class Layout { public Entry[] cards; }

        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        static Entry[] cards;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { sprites.Clear(); cards = null; }

        public static Entry[] Cards
        {
            get
            {
                if (cards != null) return cards;
                var source = Resources.Load<TextAsset>("Menu/Shop/cards");
                if (source == null) Debug.LogError("Shop-Karten fehlen (tools/newdesign/shop.js)");
                cards = source != null ? JsonUtility.FromJson<Layout>(source.text).cards : new Entry[0];
                return cards;
            }
        }

        public static Sprite Get(string name)
        {
            if (sprites.TryGetValue(name, out var s) && s != null) return s;
            var tex = Resources.Load<Texture2D>("Menu/Shop/" + name);
            if (tex == null) return null;
            // die Textur ist auf Zweierpotenzen gestreckt (Crunch): die Größe gibt immer die Karte vor
            s = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            s.name = "Shop " + name;
            sprites[name] = s;
            return s;
        }
    }

    /// <summary>
    /// Eine Shop-Karte wie in der Vorlage: das gemalte Kartenbild mit Namensleiste, rechts in der Leiste je nach
    /// Zustand der Preis mit Münze, der gelbe Knopf (AUSWÄHLEN / KAUFEN?) oder grün „GEWÄHLT“. Unter dem Zeiger hebt
    /// sich die Karte an: ihr Schatten bleibt liegen und wird größer, der Knopf kommt noch ein Stück weiter heraus.
    /// Die Trefferfläche (Root) bewegt sich dabei nicht, sonst würde die Karte am Rand unter dem Zeiger flattern.
    /// </summary>
    public sealed class ShopCharacterCard
    {
        public enum State { Price, Confirm, Select, Chosen }

        /// <summary>Kartenmaße in Menüeinheiten (halbe Pixelgröße der Bilder); zwei kleine Karten sind so hoch wie eine große.</summary>
        public const float W = 318f, TallH = 550f, SmallH = 268f, Gap = 14f;
        // Rahmen, Fuß (Farblinie + Leiste) und Linie wie in tools/newdesign/shop.js (FRAME, FOOT), halbiert
        const float Edge = 3f, Foot = 63.5f, Stripe = 6.5f;
        static readonly Color Chosen = new Color(0.05f, 0.97f, 0.3f);
        static readonly Color PriceWhite = new Color(0.9f, 0.96f, 1f);

        public readonly ShopItem Item;
        public readonly RectTransform Root;
        public readonly Vector2 Size;
        public float Jiggle;

        readonly RectTransform lift, button;
        readonly Image shadow, glow, picture, stripe, sheen, coin, select, plate, chosen;
        readonly TextMeshProUGUI amount, confirm;
        readonly Vector2 buttonHome;
        float time, aPrice, aConfirm, aSelect, aChosen;
        bool first = true;

        public ShopCharacterCard(Transform parent, ShopItem item, Vector2 pos, bool small)
        {
            Item = item;
            var def = item.Character;
            Size = new Vector2(W, small ? SmallH : TallH);
            Root = UiKit.Node(def.Name, parent, pos, Size);
            shadow = UiKit.Img("Shadow", Root, UiArt.Glow, Color.clear, Vector2.zero, Size);
            lift = UiKit.Node("Lift", Root, Vector2.zero, Size);
            glow = UiKit.Img("Glow", lift, UiArt.Glow, Color.clear, Vector2.zero, Size + new Vector2(170f, 170f));
            picture = UiKit.Img("Card", lift, ShopArt.Get(def.Id), Color.white, Vector2.zero, Size);

            float bottom = -Size.y * 0.5f, right = W * 0.5f - Edge;
            float barY = bottom + Edge + (Foot - Stripe - Edge) * 0.5f;
            // die gemalte Farblinie über der Leiste wird grün, wenn der Spieler gewählt ist
            stripe = UiKit.Img("Stripe", lift, null, Color.clear, new Vector2(0f, bottom + Foot - Stripe * 0.5f), new Vector2(W - Edge * 2f, Stripe));
            // Licht von oben auf dem Bild (Hover, Treffer)
            float artH = Size.y - Foot - Edge;
            sheen = UiKit.Img("Sheen", lift, MenuUi.FadeDown, Color.clear, new Vector2(0f, bottom + Foot + artH * 0.5f), new Vector2(W - Edge * 2f, artH));

            coin = UiKit.Img("Coin", lift, ShopArt.Get("coin"), Color.white, new Vector2(right - 34f, barY), new Vector2(38f, 40f));
            amount = UiKit.Label("Price", lift, "", 30f, PriceWhite, TextAlignmentOptions.Right, new Vector2(right - 62f - 70f, barY), new Vector2(140f, 44f));
            amount.fontStyle = FontStyles.Bold | FontStyles.Italic;
            if (CurrencyBar.Numbers != null) amount.fontSharedMaterial = CurrencyBar.Numbers;

            buttonHome = new Vector2(right - 8f - 77f, barY);
            button = UiKit.Node("Button", lift, buttonHome, new Vector2(154f, 40f));
            select = UiKit.Img("Select", button, ShopArt.Get("select"), Color.white, Vector2.zero, new Vector2(154f, 40f));
            plate = UiKit.Img("Plate", button, ShopArt.Get("plate"), Color.white, Vector2.zero, new Vector2(154f, 40f));
            // normale Menüschrift: die gemalte kennt kein Fragezeichen
            confirm = UiKit.Label("Label", button, "KAUFEN?", 23f, MenuArt.Ink, TextAlignmentOptions.Center, new Vector2(0f, 1f), new Vector2(150f, 40f), true, 2f);
            confirm.fontStyle = FontStyles.Bold;
            if (UiArt.FontBold != null) confirm.fontSharedMaterial = UiArt.FontBold.material;
            chosen = UiKit.Img("Chosen", lift, ShopArt.Get("chosen"), Color.white, new Vector2(right - 14f - 49.5f, barY), new Vector2(99f, 28.7f));
        }

        public void Style(MenuTarget t, float udt, State state, bool affordable, string price)
        {
            time += udt;
            Jiggle = Mathf.Max(0f, Jiggle - udt * 2f);
            float h = Mathf.Clamp01(t.Hover), hit = Mathf.Clamp01(t.Hit), fade = t.Fade;

            // anheben: die Karte kommt dem Betrachter entgegen, der Schatten bleibt am Boden und wird weiter und blasser
            float wob = Mathf.Sin(time * 24f) * Jiggle * 5f;
            lift.anchoredPosition = new Vector2(wob, h * 13f - hit * 4f);
            float s = 1f + h * 0.028f + t.Punch * 0.04f;
            lift.localScale = new Vector3(s + t.Punch * 0.015f, s - t.Punch * 0.04f, 1f);
            shadow.rectTransform.anchoredPosition = new Vector2(0f, -10f - h * 9f);
            shadow.rectTransform.sizeDelta = Size + Vector2.one * (70f + h * 46f);
            shadow.color = new Color(0f, 0.01f, 0.03f, (0.55f - 0.13f * h) * fade);

            // die angehobene Karte liegt über ihren Nachbarn
            if (h > 0.3f && Root.GetSiblingIndex() != Root.parent.childCount - 1) Root.SetAsLastSibling();

            float step = first ? 1f : udt * 9f;
            first = false;
            aPrice = Mathf.MoveTowards(aPrice, state == State.Price ? 1f : 0f, step);
            aConfirm = Mathf.MoveTowards(aConfirm, state == State.Confirm ? 1f : 0f, step);
            aSelect = Mathf.MoveTowards(aSelect, state == State.Select ? 1f : 0f, step);
            aChosen = Mathf.MoveTowards(aChosen, state == State.Chosen ? 1f : 0f, step);

            // goldener Schein, solange ein Kauf auf den zweiten Treffer wartet
            float pulse = 0.5f + 0.5f * Mathf.Sin(time * 9f);
            glow.color = MetaUi.Gold.WithAlpha((hit * 0.3f + h * 0.07f + aConfirm * (0.2f + 0.14f * pulse)) * fade);
            picture.color = Color.white.WithAlpha(fade);
            stripe.color = Chosen.WithAlpha(aChosen * fade);
            sheen.color = Color.white.WithAlpha((0.05f * h + 0.2f * hit) * fade);

            if (amount.text != price) amount.text = price;
            amount.color = (affordable ? PriceWhite : MetaUi.Danger).WithAlpha(aPrice * fade);
            coin.color = Color.white.WithAlpha(aPrice * fade);
            coin.rectTransform.localScale = Vector3.one * (1f + 0.08f * h);
            select.color = Color.white.WithAlpha(aSelect * fade);
            plate.color = Color.white.WithAlpha(aConfirm * fade);
            confirm.color = MenuArt.Ink.WithAlpha(aConfirm * fade);
            chosen.color = Color.white.WithAlpha(aChosen * fade);

            // der Knopf hebt sich auf der Karte noch einmal ab
            button.anchoredPosition = buttonHome + new Vector2(0f, h * 2.5f - hit * 3f);
            button.localScale = Vector3.one * (1f + 0.06f * h + 0.035f * pulse * aConfirm);
        }
    }
}
