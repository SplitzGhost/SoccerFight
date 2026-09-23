using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// SPIELER: the roster, one tab per sport (the sports still to come sit at the end as "kommt
    /// bald" tabs). Each tab shows the sport's three players — striker, defender, skiller — as tall
    /// cards in the in-game card style (portrait with the live figure, class, talent, personal
    /// perk, bars); the line above says what the sport plays like. Owned characters are picked by
    /// kicking the card; locked ones sit behind a veil with their price and lead to the shop.
    /// </summary>
    public sealed class CharacterPage
    {
        sealed class Group
        {
            public Sport Sport;
            public RectTransform Root;
            public CanvasGroup Alpha;
            public ChunkButton Tab;
            public readonly List<CharacterCard> Cards = new List<CharacterCard>();
            public float Vis, VisVel;
        }

        /// <summary>What each sport plays like (the line under the tabs).</summary>
        public static string SportLine(Sport s) => s == Sport.Basketball
            ? "BASKETBALL  ·  WÜRFE, CROSSOVER UND DUNKS  ·  DER BALL WIRD GEDRIBBELT UND KOMMT NACH JEDEM WURF ZURÜCK"
            : "FUSSBALL  ·  SCHÜSSE, TRICKS UND KOPFBÄLLE  ·  DER BALL KLEBT AM FUSS UND KOMMT NACH JEDEM SCHUSS ZURÜCK";

        MenuNav nav;
        SubPage page;
        readonly List<Group> groups = new List<Group>();
        readonly List<ChunkButton> soonTabs = new List<ChunkButton>();
        TextMeshProUGUI talent, owned;
        int tab;
        float time;

        /// <summary>Kick a locked character: the menu opens the shop on it.</summary>
        public System.Action<ShopItem> ShowInShop;

        public SubPage Page => page;

        public void Build(RectTransform parent, MenuNav menu)
        {
            nav = menu;
            page = new SubPage(parent, MenuPage.Characters, "SPIELER",
                Characters.All.Length + " SPIELER  ·  " + Characters.Sports.Length + " SPORTARTEN  ·  3 KLASSEN", MenuArt.Accent, nav.Register, nav.Back);
            var content = page.Content;

            // two real sports, two coming soon
            string[] soonNames = { "BOXEN", "TENNIS" };
            System.Func<Sprite>[] soonIcons = { () => MenuArt.IconBoxing, () => MenuArt.IconTennis };
            int tabCount = Characters.Sports.Length + soonNames.Length;
            const float tabW = 290f, tabStep = 310f;
            for (int g = 0; g < Characters.Sports.Length; g++)
            {
                var sport = Characters.Sports[g];
                var grp = new Group { Sport = sport };
                int index = g;
                Color accent = SportAccent(sport);
                grp.Tab = new ChunkButton(content, "Tab" + sport, new Vector2((g - (tabCount - 1) * 0.5f) * tabStep, 372f), new Vector2(tabW, 60f), accent,
                    Characters.SportName(sport), 22f, MenuArt.SportIcon(sport), 28f);
                grp.Tab.IconLeft(30f);
                nav.Register(new MenuTarget
                {
                    Id = "sport" + g, Root = grp.Tab.Root, Size = grp.Tab.Size, Page = MenuPage.Characters,
                    Action = () => SetTab(index), Draw = t => StyleTab(grp, t, index), Accent = accent,
                });

                grp.Root = UiKit.Node("Sport " + sport, content, Vector2.zero, new Vector2(1600f, 700f));
                grp.Alpha = grp.Root.gameObject.AddComponent<CanvasGroup>();
                int i = 0;
                foreach (var cls in Classes.ForSport(sport))
                {
                    var def = Characters.Of(sport, cls.Class);
                    if (def == null) continue;
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
            for (int s = 0; s < soonNames.Length; s++)
            {
                int g = Characters.Sports.Length + s;
                var b = new ChunkButton(content, "Tab" + soonNames[s], new Vector2((g - (tabCount - 1) * 0.5f) * tabStep, 372f), new Vector2(tabW, 60f),
                    MetaUi.Muted, soonNames[s], 22f, soonIcons[s](), 28f);
                b.IconLeft(30f);
                soonTabs.Add(b);
                nav.Register(new MenuTarget
                {
                    Id = "sportSoon" + s, Root = b.Root, Size = b.Size, Page = MenuPage.Characters, Button = b, Accent = MetaUi.Muted,
                    Soon = soonNames[s] + " KOMMT BALD",
                });
            }
            talent = MenuArt.Label("Talent", content, "", 17f, Color.white, new Vector2(0f, 322f), new Vector2(1500f, 26f), TextAlignmentOptions.Center, 3f, MenuArt.TextHeavySoft);
            talent.enableAutoSizing = true;
            talent.fontSizeMin = 12f;
            talent.fontSizeMax = 17f;
            owned = MenuArt.Label("Owned", page.Root, "", 16f, MetaUi.Muted, Vector2.zero, new Vector2(400f, 24f), TextAlignmentOptions.Right, 3f, MenuArt.TextHeavySoft);
            MenuUi.Pin(owned.rectTransform, new Vector2(1f, 1f), new Vector2(-250f, -SubPage.TopBar - 34f));
        }

        /// <summary>The colour a sport is shown in (tabs, the sport line).</summary>
        public static Color SportAccent(Sport s) => s == Sport.Basketball ? Palette.HoopOrange : MenuArt.Accent;

        void SetTab(int g)
        {
            tab = g;
        }

        /// <summary>Called when the page opens: the tab of the current player, fresh records.</summary>
        public void Open()
        {
            var sport = Characters.Current.Sport;
            for (int g = 0; g < groups.Count; g++)
                if (groups[g].Sport == sport) { tab = g; groups[g].Vis = 1f; } else groups[g].Vis = 0f;
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
                ShowInShop?.Invoke(Shop.ForCharacter(def));
                return;
            }
            card.Jiggle = 1f;
            if (Characters.Current == def) return;
            Characters.Select(Characters.IndexOf(def));
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
            var sport = groups[tab].Sport;
            talent.text = SportLine(sport);
            talent.color = MetaUi.Soft(SportAccent(sport));
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
