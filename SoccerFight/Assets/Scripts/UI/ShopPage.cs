using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// SHOP: every character as a compact card, one row per class, with portrait, class, perk,
    /// description and price. Skills are not sold — every player has all of them and picks them up
    /// after boss fights. Buying takes two kicks — the first turns the button into a pulsing gold
    /// KAUFEN?, the second (within a few seconds) buys — so a stray ball never spends coins. Not
    /// enough coins: the card shakes and says how many are missing. Bought characters can be
    /// selected right here.
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
            page = new SubPage(parent, MenuPage.Shop, "SHOP", "SPIELER  ·  BEZAHLT MIT MÜNZEN", MetaUi.Gold, nav.Register, nav.Back);
            var content = page.Content;
            // the coins sit in the top bar now; the chip stays as the page's own counter but hidden
            wallet = new WalletChip(page.Root, new Vector2(1f, 1f), new Vector2(-150f, -78f));
            wallet.Root.gameObject.SetActive(false);

            // one row per class
            int row = 0;
            foreach (var cls in Classes.All)
            {
                int col = 0;
                foreach (var def in Characters.OfClass(cls.Class))
                {
                    var item = Shop.ForCharacter(def);
                    var card = new ShopCharacterCard(content, item, new Vector2((col - 1) * (ShopCharacterCard.W + 22f), 236f - row * (ShopCharacterCard.H + 20f)));
                    cards.Add(card);
                    nav.Register(new MenuTarget
                    {
                        Id = "shop_" + item.Id, Root = card.Root, Size = new Vector2(ShopCharacterCard.W, ShopCharacterCard.H), Page = MenuPage.Shop,
                        Action = () => HitCharacter(card), Draw = t => StyleCharacter(card, t), Accent = def.Accent,
                    });
                    col++;
                }
                row++;
            }
            MetaUi.Text(content, "Hint", "Erster Treffer wählt aus, zweiter Treffer kauft.  ·  Münzen lassen besiegte Monster fallen, Bosse besonders viele.  ·  Fähigkeiten hast du alle: nach jedem Boss wählst du eine.",
                15f, MetaUi.Muted, new Vector2(0f, -452f), new Vector2(1600f, 24f));
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
                Sfx.Play(Sound.Denied, 0.7f);
                nav.Say(item.Blocked(), at);
                return false;
            }
            if (check == Shop.Result.TooExpensive)
            {
                jiggle(1f);
                pending = null;
                Sfx.Play(Sound.Denied, 0.7f);
                nav.Say("DIR FEHLEN " + Currencies.Format(Wallet.Missing(item.Price)) + " MÜNZEN", at);
                return false;
            }
            if (pending != item)
            {
                pending = item;
                pendingT = 0f;
                jiggle(0.5f);
                Sfx.Play(Sound.Select, 0.6f);
                nav.Say("NOCH EINMAL SCHIESSEN ZUM KAUFEN", at);
                return false;
            }
            pending = null;
            if (Shop.Buy(item) != Shop.Result.Bought) return false;
            Sfx.Play(Sound.Purchase, 0.9f);
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
                Sfx.Play(Sound.Select, 0.8f);
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
            bool confirm = pending == item;
            string label; bool coin, filled; Color color;
            if (item.Owned())
            {
                bool current = Characters.Current == def;
                label = current ? "GEWÄHLT" : "AUSWÄHLEN";
                coin = false; filled = current; color = current ? MetaUi.Gold : def.Accent;
            }
            else if (confirm) { label = "KAUFEN?  " + item.Price; coin = true; filled = true; color = MetaUi.Gold; }
            else { label = item.Price.ToString(); coin = true; filled = false; color = Wallet.CanAfford(item.Price) ? def.Accent : MetaUi.Danger; }
            card.Style(t, TimeFx.UiDelta, label, coin, filled, color, confirm);
        }

        public void Update(float udt, Vector2 aim)
        {
            page.Update(udt);
            if (page.T < 0.01f) return;
            wallet.Update(udt);
            if (pending != null && (pendingT += udt) > ConfirmTime) pending = null;
            PlayerArt.Pump();
            foreach (var card in cards) card.Update(udt, aim, Mathf.Clamp01(page.T));
        }
    }
}
