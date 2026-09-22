using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// SPIELER: the roster, one tab per class. Each tab shows the class talent and its three
    /// characters as tall cards in the in-game card style (portrait with the live figure, class,
    /// talent, personal perk, bars); the line above names the class move on the right mouse button. Owned characters are picked by kicking the card; locked ones
    /// sit behind a veil with their price and lead to the shop.
    /// </summary>
    public sealed class CharacterPage
    {
        sealed class Group
        {
            public ClassDef Class;
            public RectTransform Root;
            public CanvasGroup Alpha;
            public ChunkButton Tab;
            public readonly List<CharacterCard> Cards = new List<CharacterCard>();
            public float Vis, VisVel;
        }

        MenuNav nav;
        SubPage page;
        readonly List<Group> groups = new List<Group>();
        TextMeshProUGUI talent, owned;
        int tab;
        float time;

        /// <summary>Kick a locked character: the menu opens the shop on it.</summary>
        public System.Action<ShopItem> ShowInShop;

        public SubPage Page => page;

        public void Build(RectTransform parent, MenuNav menu)
        {
            nav = menu;
            page = new SubPage(parent, MenuPage.Characters, "SPIELER", Characters.All.Length + " SPIELER  ·  3 KLASSEN", MenuArt.Accent, nav.Register, nav.Back);
            var content = page.Content;

            for (int g = 0; g < Classes.All.Count; g++)
            {
                var cls = Classes.All[g];
                var grp = new Group { Class = cls };
                int index = g;
                grp.Tab = new ChunkButton(content, "Tab" + cls.Name, new Vector2((g - 1) * 300f, 372f), new Vector2(280f, 60f), cls.Accent, cls.Name, 22f, cls.Icon(), 26f);
                grp.Tab.IconLeft(30f);
                nav.Register(new MenuTarget
                {
                    Id = "class" + g, Root = grp.Tab.Root, Size = grp.Tab.Size, Page = MenuPage.Characters,
                    Action = () => SetTab(index), Draw = t => StyleTab(grp, t, index), Accent = cls.Accent,
                });

                grp.Root = UiKit.Node("Class " + cls.Name, content, Vector2.zero, new Vector2(1600f, 700f));
                grp.Alpha = grp.Root.gameObject.AddComponent<CanvasGroup>();
                int i = 0;
                foreach (var def in Characters.OfClass(cls.Class))
                {
                    var card = new CharacterCard(grp.Root, def, new Vector2((i - 1) * (CharacterCard.W + 50f), -44f), CharacterCard.Mode.Roster);
                    grp.Cards.Add(card);
                    nav.Register(new MenuTarget
                    {
                        Id = "card_" + def.Id, Root = card.Root, Size = new Vector2(CharacterCard.W, CharacterCard.H), Page = MenuPage.Characters,
                        Action = () => HitCard(card), Draw = t => StyleCard(card, t), Accent = def.Accent,
                        Visible = () => Mathf.Clamp01(grp.Vis),
                    });
                    i++;
                }
                groups.Add(grp);
            }
            talent = MenuArt.Label("Talent", content, "", 17f, Color.white, new Vector2(0f, 322f), new Vector2(1500f, 26f), TextAlignmentOptions.Center, 3f, MenuArt.TextHeavySoft);
            talent.enableAutoSizing = true;
            talent.fontSizeMin = 12f;
            talent.fontSizeMax = 17f;
            owned = MenuArt.Label("Owned", page.Root, "", 16f, MetaUi.Muted, Vector2.zero, new Vector2(400f, 24f), TextAlignmentOptions.Right, 3f, MenuArt.TextHeavySoft);
            MenuUi.Pin(owned.rectTransform, new Vector2(1f, 1f), new Vector2(-250f, -SubPage.TopBar - 34f));
        }

        void SetTab(int g)
        {
            if (g != tab) Sfx.Play(Sound.Equip, 0.5f);
            tab = g;
        }

        /// <summary>Called when the page opens: the tab of the current player, fresh records.</summary>
        public void Open()
        {
            var cls = Characters.Current.Class;
            for (int g = 0; g < groups.Count; g++)
                if (groups[g].Class.Class == cls) { tab = g; groups[g].Vis = 1f; } else groups[g].Vis = 0f;
            foreach (var grp in groups) foreach (var c in grp.Cards) c.SetBest(RunState.BestStageOf(c.Def));
            int n = 0;
            foreach (var c in Characters.All) if (Profile.OwnsCharacter(c.Id)) n++;
            owned.text = "FREIGESCHALTET  " + n + " / " + Characters.All.Length;
        }

        void HitCard(CharacterCard card)
        {
            var def = card.Def;
            if (!Profile.OwnsCharacter(def.Id))
            {
                Sfx.Play(Sound.Select, 0.6f);
                ShowInShop?.Invoke(Shop.ForCharacter(def));
                return;
            }
            card.Jiggle = 1f;
            if (Characters.Current == def) return;
            Characters.Select(Characters.IndexOf(def));
            Sfx.Play(Sound.Select, 0.8f);
        }

        void StyleTab(Group grp, MenuTarget t, int index)
        {
            grp.Tab.Filled = tab == index;
            grp.Tab.Style(Mathf.Clamp01(t.Hover), t.Hit, t.Punch, t.Fade, time);
        }

        void StyleCard(CharacterCard card, MenuTarget t)
        {
            var def = card.Def;
            bool own = Profile.OwnsCharacter(def.Id);
            bool current = Characters.Current == def;
            card.IsChosen = current;
            card.Locked = !own;
            string label = current ? "GEWÄHLT" : own ? "WÄHLEN" : "ZUM SHOP";
            card.Style(t, TimeFx.UiDelta, label, current, current ? MetaUi.Gold : def.Accent, current);
        }

        public void Update(float udt, Vector2 aim)
        {
            time += udt;
            page.Update(udt);
            if (page.T < 0.01f) return;
            PlayerArt.Pump();
            var cls = groups[tab].Class;
            talent.text = "TALENT  " + cls.TraitName + "  ·  " + cls.Trait.Text + "  ·  RECHTSKLICK: " + Abilities.Name(cls.Primary);
            talent.color = MetaUi.Soft(cls.Accent);
            for (int g = 0; g < groups.Count; g++)
            {
                var grp = groups[g];
                MathUtil.Spring(ref grp.Vis, ref grp.VisVel, g == tab ? 1f : 0f, 5f, 0.9f, udt);
                float v = Mathf.Clamp01(grp.Vis);
                grp.Alpha.alpha = v;
                grp.Root.anchoredPosition = new Vector2((g - tab) * 140f * (1f - v), 0f);
                bool on = v > 0.01f;
                if (grp.Root.gameObject.activeSelf != on) grp.Root.gameObject.SetActive(on);
                if (on) foreach (var c in grp.Cards) c.Update(udt, aim, v * Mathf.Clamp01(page.T));
            }
        }
    }
}
