using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// SHOP wie in der Vorlage (Inspiration/Shop UI): alle Spieler als gemalte Karten in einer Reihe – hohe Karten
    /// einzeln, kleine zu zweit übereinander (Reihenfolge und Format aus ShopArt.Cards). In der Namensleiste steht
    /// der Preis, bei gekauften Spielern der Knopf AUSWÄHLEN bzw. grün GEWÄHLT. Skills are not sold — every player
    /// has all of them and picks them up after boss fights. Buying takes two kicks — the first turns the price into
    /// a pulsing KAUFEN?, the second (within a few seconds) buys — so a stray ball never spends coins. Not
    /// enough coins: the card shakes and says how many are missing.
    /// </summary>
    public sealed class ShopPage
    {
        const float ConfirmTime = 4f;

        MenuNav nav;
        SubPage page;
        readonly List<ShopCharacterCard> cards = new List<ShopCharacterCard>();
        WalletChip wallet;
        ShopItem pending;
        float pendingT;

        /// <summary>Canvas-space burst when something is bought.</summary>
        public System.Action<Vector2, Color> Burst;

        public SubPage Page => page;

        public void Build(RectTransform parent, MenuNav menu)
        {
            nav = menu;
            // die Karten sind farbig gemalt: die Szene dahinter bleibt hell wie in der Vorlage
            page = new SubPage(parent, MenuPage.Shop, "SHOP", "SHOP  ·  SPIELER KAUFEN", MetaUi.Gold, nav.Register, nav.Back, dim: 0.3f);
            var content = page.Content;
            // the coins sit in the top bar now; the chip stays as the page's own counter but hidden
            wallet = new WalletChip(page.Root, new Vector2(1f, 1f), new Vector2(-150f, -78f));
            wallet.Root.gameObject.SetActive(false);

            // Spalten: eine hohe Karte oder zwei kleine übereinander
            var columns = new List<List<(ShopItem item, bool small)>>();
            foreach (var e in ShopArt.Cards)
            {
                var def = Characters.Get(e.id);
                var item = def != null ? Shop.ForCharacter(def) : null;
                if (item == null || ShopArt.Get(e.id) == null) continue;
                var last = columns.Count > 0 ? columns[columns.Count - 1] : null;
                if (e.small && last != null && last.Count == 1 && last[0].small) last.Add((item, true));
                else columns.Add(new List<(ShopItem, bool)> { (item, e.small) });
            }
            const float step = ShopCharacterCard.W + ShopCharacterCard.Gap;
            float width = columns.Count * step - ShopCharacterCard.Gap;
            var row = UiKit.Node("Karten", content, new Vector2(0f, -28f), Vector2.zero);
            // mehr Spieler als Platz: die Reihe wird als Ganzes kleiner
            row.localScale = Vector3.one * Mathf.Min(1f, 1664f / Mathf.Max(1f, width));
            for (int c = 0; c < columns.Count; c++)
            {
                float x = (c - (columns.Count - 1) * 0.5f) * step;
                for (int i = 0; i < columns[c].Count; i++)
                {
                    var (item, small) = columns[c][i];
                    float y = small ? (0.5f - i) * (ShopCharacterCard.SmallH + ShopCharacterCard.Gap) : 0f;
                    var card = new ShopCharacterCard(row, item, new Vector2(x, y), small);
                    cards.Add(card);
                    nav.Register(new MenuTarget
                    {
                        Id = "shop_" + item.Id, Root = card.Root, Size = card.Size, Page = MenuPage.Shop,
                        Action = () => HitCharacter(card), Draw = t => StyleCharacter(card, t), Accent = item.Accent,
                    });
                }
            }
            // die Szene ist hier hell: ein weicher dunkler Streifen hält den Hinweis lesbar
            UiKit.Img("HintShade", content, UiArt.Glow, new Color(0.01f, 0.03f, 0.05f, 0.6f), new Vector2(0f, -452f), new Vector2(1700f, 84f));
            MetaUi.Text(content, "Hint", "Erster Treffer wählt aus, zweiter Treffer kauft.  ·  Münzen lassen besiegte Monster fallen, Bosse besonders viele.  ·  Fähigkeiten hast du alle: nach jedem Boss wählst du eine.",
                15f, MetaUi.Body, new Vector2(0f, -452f), new Vector2(1600f, 24f));
        }

        /// <summary>Opens the shop straight on an item (a locked character).</summary>
        public void Focus(ShopItem item)
        {
            if (item == null) return;
            foreach (var c in cards) if (c.Item == item) c.Jiggle = 1f;
        }

        public void Refresh() => pending = null;

        // ------------------------------------------------------------------ buying

        /// <summary>First kick asks, the second buys. Returns true when something was bought.</summary>
        bool TryBuy(ShopItem item, RectTransform at, System.Action<float> jiggle)
        {
            var check = Shop.Check(item);
            if (check == Shop.Result.Blocked)
            {
                jiggle(1f);
                nav.Say(item.Blocked(), at);
                return false;
            }
            if (check == Shop.Result.TooExpensive)
            {
                jiggle(1f);
                pending = null;
                nav.Say("DIR FEHLEN " + Currencies.Format(Wallet.Missing(item.Price)) + " MÜNZEN", at);
                return false;
            }
            if (pending != item)
            {
                pending = item;
                pendingT = 0f;
                jiggle(0.5f);
                nav.Say("NOCH EINMAL SCHIESSEN ZUM KAUFEN", at);
                return false;
            }
            pending = null;
            if (Shop.Buy(item) != Shop.Result.Bought) return false;
            Burst?.Invoke(nav.CanvasPos(at), item.Accent);
            jiggle(0.8f);
            return true;
        }

        void HitCharacter(ShopCharacterCard card)
        {
            var item = card.Item;
            var def = item.Character;
            if (item.Owned())
            {
                if (Characters.Current == def) { nav.Say(def.Name + " SPIELT SCHON", card.Root); return; }
                Characters.Select(Characters.IndexOf(def));
                card.Jiggle = 0.6f;
                nav.Say(def.Name + " IST DEIN SPIELER", card.Root);
                return;
            }
            if (TryBuy(item, card.Root, j => card.Jiggle = j))
                nav.Say(def.Name + " GEHÖRT DIR!  ·  NOCH EINMAL SCHIESSEN ZUM AUSWÄHLEN", card.Root);
        }

        // ------------------------------------------------------------------ drawing

        void StyleCharacter(ShopCharacterCard card, MenuTarget t)
        {
            var item = card.Item;
            var def = item.Character;
            var state = item.Owned()
                ? (Characters.Current == def ? ShopCharacterCard.State.Chosen : ShopCharacterCard.State.Select)
                : pending == item ? ShopCharacterCard.State.Confirm : ShopCharacterCard.State.Price;
            card.Style(t, TimeFx.UiDelta, state, Wallet.CanAfford(item.Price), item.Price.ToString());
        }

        public void Update(float udt, Vector2 aim)
        {
            page.Update(udt);
            if (page.T < 0.01f) return;
            wallet.Update(udt);
            if (pending != null && (pendingT += udt) > ConfirmTime) pending = null;
        }
    }
}
