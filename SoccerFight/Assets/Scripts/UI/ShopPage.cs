using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// SHOP with two tabs. SPIELER: every character as a compact card, one row per class, with
    /// portrait, class, perk, description and price. FÄHIGKEITEN: every skill as a tile with its
    /// price. Buying takes two kicks — the first turns the button into a pulsing gold KAUFEN?, the
    /// second (within a few seconds) buys — so a stray ball never spends coins. Not enough coins:
    /// the card shakes and says how many are missing. Bought characters can be selected right
    /// here; a bought skill goes straight into a free loadout slot.
    /// </summary>
    public sealed class ShopPage
    {
        const float ConfirmTime = 4f;

        MenuNav nav;
        SubPage page;
        RectTransform charGroup, skillGroup;
        CanvasGroup charAlpha, skillAlpha;
        ChunkButton tabChars, tabSkills;
        readonly List<ShopCharacterCard> cards = new List<ShopCharacterCard>();
        readonly List<SkillTile> tiles = new List<SkillTile>();
        WalletChip wallet;
        int tab;
        float tabT, tabVel, time;
        ShopItem pending;
        float pendingT;

        /// <summary>Canvas-space burst when something is bought.</summary>
        public System.Action<Vector2, Color> Burst;

        public SubPage Page => page;

        public void Build(RectTransform parent, MenuNav menu)
        {
            nav = menu;
            page = new SubPage(parent, MenuPage.Shop, "SHOP", "SPIELER  ·  FÄHIGKEITEN  ·  BEZAHLT MIT MÜNZEN", MetaUi.Gold, nav.Register, nav.Back);
            var content = page.Content;
            // the coins sit in the top bar now; the chip stays as the page's own counter but hidden
            wallet = new WalletChip(page.Root, new Vector2(1f, 1f), new Vector2(-150f, -78f));
            wallet.Root.gameObject.SetActive(false);

            tabChars = new ChunkButton(content, "TabChars", new Vector2(-175f, 356f), new Vector2(330f, 62f), MetaUi.Gold, "SPIELER", 24f, MenuArt.IconFriends, 26f);
            tabChars.IconLeft(40f);
            nav.Register(new MenuTarget { Id = "tabChars", Root = tabChars.Root, Size = tabChars.Size, Page = MenuPage.Shop, Action = () => SetTab(0), Draw = t => StyleTab(tabChars, t, 0), Accent = MetaUi.Gold });
            tabSkills = new ChunkButton(content, "TabSkills", new Vector2(175f, 356f), new Vector2(330f, 62f), MetaUi.Gold, "FÄHIGKEITEN", 24f, MenuArt.IconSkills, 26f);
            tabSkills.IconLeft(40f);
            nav.Register(new MenuTarget { Id = "tabSkills", Root = tabSkills.Root, Size = tabSkills.Size, Page = MenuPage.Shop, Action = () => SetTab(1), Draw = t => StyleTab(tabSkills, t, 1), Accent = MetaUi.Gold });

            // ---- characters: one row per class
            charGroup = UiKit.Node("Characters", content, Vector2.zero, new Vector2(1600f, 800f));
            charAlpha = charGroup.gameObject.AddComponent<CanvasGroup>();
            int row = 0;
            foreach (var cls in Classes.All)
            {
                int col = 0;
                foreach (var def in Characters.OfClass(cls.Class))
                {
                    var item = Shop.ForCharacter(def);
                    var card = new ShopCharacterCard(charGroup, item, new Vector2((col - 1) * (ShopCharacterCard.W + 22f), 192f - row * (ShopCharacterCard.H + 20f)));
                    cards.Add(card);
                    nav.Register(new MenuTarget
                    {
                        Id = "shop_" + item.Id, Root = card.Root, Size = new Vector2(ShopCharacterCard.W, ShopCharacterCard.H), Page = MenuPage.Shop,
                        Action = () => HitCharacter(card), Draw = t => StyleCharacter(card, t), Accent = def.Accent,
                        Visible = () => 1f - tabT,
                    });
                    col++;
                }
                row++;
            }

            // ---- skills: the same tiles as the skill page, with prices
            skillGroup = UiKit.Node("Skills", content, Vector2.zero, new Vector2(1600f, 800f));
            skillAlpha = skillGroup.gameObject.AddComponent<CanvasGroup>();
            const int perRow = 4;
            const float gx = SkillTile.W + 22f, gy = SkillTile.H + 20f;
            int count = SkillCatalog.All.Count;
            for (int i = 0; i < count; i++)
            {
                var s = SkillCatalog.All[i];
                int r = i / perRow, c = i % perRow;
                int inRow = Mathf.Min(perRow, count - r * perRow);
                var tile = new SkillTile(skillGroup, s, new Vector2((c - (inRow - 1) * 0.5f) * gx, 206f - r * gy));
                tiles.Add(tile);
                var item = Shop.ForSkill(s.Ability);
                nav.Register(new MenuTarget
                {
                    Id = "shop_" + item.Id, Root = tile.Root, Size = new Vector2(SkillTile.W, SkillTile.H), Page = MenuPage.Shop,
                    Action = () => HitSkill(tile, item), Draw = t => StyleSkill(tile, item, t), Accent = s.Accent,
                    Visible = () => tabT,
                });
            }
            MetaUi.Text(content, "Hint", "Erster Treffer wählt aus, zweiter Treffer kauft.  ·  Münzen lassen besiegte Monster fallen, Bosse besonders viele.",
                15f, MetaUi.Muted, new Vector2(0f, -452f), new Vector2(1400f, 24f));
            SetTab(0, true);
        }

        void SetTab(int t, bool instant = false)
        {
            if (tab != t && !instant) Sfx.Play(Sound.Equip, 0.5f);
            tab = t;
            pending = null;
            if (instant) { tabT = t; tabVel = 0f; }
        }

        /// <summary>Opens the shop straight on an item (from the skill page or a locked character).</summary>
        public void Focus(ShopItem item)
        {
            if (item == null) return;
            SetTab(item.Kind == ShopKind.Character ? 0 : 1, true);
            foreach (var c in cards) if (c.Item == item) c.Jiggle = 1f;
            foreach (var t in tiles) if (t.Skill == item.Skill) t.Jiggle = 1f;
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
                nav.Say(item.Blocked() + "  ·  KAUFE ERST EINEN SPIELER DIESER KLASSE", at);
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
                bool hadHeader = Profile.IsEquipped(Ability.Header);
                Characters.Select(Characters.IndexOf(def));
                card.Jiggle = 0.6f;
                Sfx.Play(Sound.Select, 0.8f);
                nav.Say(def.Name + " IST DEIN SPIELER" + (hadHeader && !Profile.IsEquipped(Ability.Header) ? "  ·  KOPFBALL ABGELEGT (NUR VERTEIDIGER)" : ""), card.Root);
                return;
            }
            if (TryBuy(item, card.Root, j => card.Jiggle = j))
                nav.Say(def.Name + " GEHÖRT DIR!  ·  NOCH EINMAL SCHIESSEN ZUM AUSWÄHLEN", card.Root);
        }

        void HitSkill(SkillTile tile, ShopItem item)
        {
            if (item.Owned())
            {
                Sfx.Play(Sound.Select, 0.5f);
                nav.Open(MenuPage.Skills);
                return;
            }
            if (!TryBuy(item, tile.Root, j => tile.Jiggle = j)) return;
            // straight into a free slot if the class can use it
            var s = item.Skill;
            var r = Profile.Equip(s.Ability);
            Profile.Save();
            nav.Say(r == Profile.EquipResult.Equipped
                ? s.Name + " GEKAUFT  ·  AUSGERÜSTET AUF PLATZ " + (Profile.SlotOf(s.Ability) + 1)
                : s.Name + " GEKAUFT  ·  RÜSTE SIE UNTER FÄHIGKEITEN AUS", tile.Root);
        }

        // ------------------------------------------------------------------ drawing

        void StyleTab(ChunkButton b, MenuTarget t, int index)
        {
            b.Filled = tab == index;
            b.Style(Mathf.Clamp01(t.Hover), t.Hit, t.Punch, t.Fade, time);
        }

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

        void StyleSkill(SkillTile tile, ShopItem item, MenuTarget t)
        {
            var s = tile.Skill;
            bool owned = item.Owned();
            string blocked = owned ? null : item.Blocked?.Invoke();
            bool confirm = pending == item;
            tile.IsSelected = confirm;
            tile.SlotNumber = -1;
            tile.Locked = !owned;
            tile.Dimmed = blocked != null;
            if (owned) tile.SetStatus(Profile.IsEquipped(s.Ability) ? "IM BESITZ  ·  AUSGERÜSTET" : "IM BESITZ", MetaUi.Muted);
            else if (blocked != null) tile.SetStatus(blocked, MetaUi.Muted);
            else if (confirm) tile.SetStatus("NOCH EINMAL: KAUFEN FÜR " + item.Price, MetaUi.Gold);
            else tile.SetPrice(item.Price);
            SkillPage.Corner(tile, Characters.Current);
            tile.Style(t, TimeFx.UiDelta);
        }

        public void Update(float udt, Vector2 aim)
        {
            time += udt;
            page.Update(udt);
            if (page.T < 0.01f) return;
            wallet.Update(udt);
            if (pending != null && (pendingT += udt) > ConfirmTime) pending = null;

            MathUtil.Spring(ref tabT, ref tabVel, tab, 5f, 0.9f, udt);
            float c = Mathf.Clamp01(1f - tabT), s = Mathf.Clamp01(tabT);
            charAlpha.alpha = c;
            skillAlpha.alpha = s;
            charGroup.anchoredPosition = new Vector2(-tabT * 120f, 0f);
            skillGroup.anchoredPosition = new Vector2((1f - tabT) * 120f, 0f);
            if (charGroup.gameObject.activeSelf != c > 0.01f) charGroup.gameObject.SetActive(c > 0.01f);
            if (skillGroup.gameObject.activeSelf != s > 0.01f) skillGroup.gameObject.SetActive(s > 0.01f);

            if (c > 0.01f)
            {
                PlayerArt.Pump();
                foreach (var card in cards) card.Update(udt, aim, c * Mathf.Clamp01(page.T));
            }
        }
    }
}
