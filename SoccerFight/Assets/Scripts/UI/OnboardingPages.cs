using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// The first launch: WÄHLE DEINEN SPIELER — any one player of any sport is free. A tab per
    /// sport shows its three players (one per class), each card showing the class, its talent, its
    /// right-click move and the character's own perk. LOS GEHT'S commits the pick. Skills need no
    /// choosing: every player has all of their sport's and picks them up after boss fights.
    /// </summary>
    public sealed class OnboardingPages
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

        MenuNav nav;
        SubPage starterPage;
        readonly List<Group> groups = new List<Group>();
        CharacterDef starter;
        ChunkButton go;
        TMPro.TextMeshProUGUI line;
        int tab;
        float time;

        public SubPage StarterPage => starterPage;

        public void Build(RectTransform parent, MenuNav menu)
        {
            nav = menu;
            starterPage = new SubPage(parent, MenuPage.Starter, "WÄHLE DEINEN SPIELER", "DEIN ERSTER SPIELER IST GRATIS  ·  FUSSBALL ODER BASKETBALL", MetaUi.Gold, nav.Register, null);
            var content = starterPage.Content;
            float step = CharacterCard.W + 50f;
            int n = Characters.Sports.Length;
            for (int g = 0; g < n; g++)
            {
                var sport = Characters.Sports[g];
                var grp = new Group { Sport = sport };
                int index = g;
                Color accent = CharacterPage.SportAccent(sport);
                grp.Tab = new ChunkButton(content, "Tab" + sport, new Vector2((g - (n - 1) * 0.5f) * 330f, 392f), new Vector2(300f, 58f), accent,
                    Characters.SportName(sport), 22f, MenuArt.SportIcon(sport), 28f);
                grp.Tab.IconLeft(30f);
                nav.Register(new MenuTarget
                {
                    Id = "starterSport" + g, Root = grp.Tab.Root, Size = grp.Tab.Size, Page = MenuPage.Starter,
                    Action = () => tab = index, Draw = t => { grp.Tab.Filled = tab == index; grp.Tab.Style(Mathf.Clamp01(t.Hover), t.Hit, t.Punch, t.Fade, time); },
                    Accent = accent,
                });
                grp.Root = UiKit.Node("Starters " + sport, content, Vector2.zero, new Vector2(1600f, 700f));
                grp.Alpha = grp.Root.gameObject.AddComponent<CanvasGroup>();
                int i = 0;
                foreach (var cls in Classes.ForSport(sport))
                {
                    var def = Characters.Of(sport, cls.Class);
                    if (def == null || !def.Starter) continue;
                    var card = new CharacterCard(grp.Root, def, new Vector2((i - 1) * step, -18f), CharacterCard.Mode.Starter);
                    grp.Cards.Add(card);
                    nav.Register(new MenuTarget
                    {
                        Id = "starter_" + def.Id, Root = card.Root, Size = new Vector2(CharacterCard.W, CharacterCard.H), Page = MenuPage.Starter,
                        Action = () => PickStarter(card), Draw = t => StyleStarter(card, t), Accent = def.Accent,
                        Visible = () => Mathf.Clamp01(grp.Vis),
                    });
                    i++;
                }
                groups.Add(grp);
            }
            line = MenuArt.Label("Sport", content, "", 16f, Color.white, new Vector2(0f, 344f), new Vector2(1500f, 24f), TMPro.TextAlignmentOptions.Center, 3f, MenuArt.TextHeavySoft);
            line.enableAutoSizing = true;
            line.fontSizeMin = 11f;
            line.fontSizeMax = 16f;

            go = new ChunkButton(content, "Go", new Vector2(0f, -410f), new Vector2(430f, 76f), MetaUi.Gold, "LOS GEHT'S", 32f, MenuArt.IconPlay, 28f, true, true);
            go.IconLeft(60f);
            nav.Register(new MenuTarget { Id = "starterGo", Root = go.Root, Size = go.Size, Page = MenuPage.Starter, Button = go, Action = Finish, Accent = MetaUi.Gold });
            MetaUi.Text(content, "Hint", "Die anderen gibt es später für Münzen im Shop.  ·  Fähigkeiten hast du alle: nach jedem Boss wählst du eine neue, höchstens " + RunState.MaxSkills + " pro Lauf.",
                15f, MetaUi.Muted, new Vector2(0f, -466f), new Vector2(1500f, 24f));
        }

        void PickStarter(CharacterCard card)
        {
            starter = card.Def;
            card.Jiggle = 1f;
            // the title screen's player follows the pick
            Characters.Preview(Characters.IndexOf(card.Def));
        }

        void StyleStarter(CharacterCard card, MenuTarget t)
        {
            card.IsChosen = starter == card.Def;
            card.Style(t, TimeFx.UiDelta, card.IsChosen ? "GEWÄHLT" : "WÄHLEN", card.IsChosen, card.IsChosen ? MetaUi.Gold : card.Def.Accent, card.IsChosen);
        }

        void Finish()
        {
            if (starter == null)
            {
                nav.Say("WÄHLE ERST EINEN SPIELER", go.Root);
                return;
            }
            Profile.ChooseStarter(starter);
            Characters.Select(Characters.IndexOf(starter));
            nav.Open(MenuPage.Main);
            nav.Say("WILLKOMMEN, " + starter.Name + "!  ·  SCHIESS AUF SPIELEN", null);
        }

        public void Update(float udt, Vector2 aim)
        {
            time += udt;
            starterPage.Update(udt);
            if (starterPage.T < 0.01f) return;
            PlayerArt.Pump();
            var sport = groups[tab].Sport;
            line.text = CharacterPage.SportLine(sport);
            line.color = MetaUi.Soft(CharacterPage.SportAccent(sport));
            for (int g = 0; g < groups.Count; g++)
            {
                var grp = groups[g];
                MathUtil.Spring(ref grp.Vis, ref grp.VisVel, g == tab ? 1f : 0f, 5f, 0.9f, udt);
                float v = Mathf.Clamp01(grp.Vis);
                grp.Alpha.alpha = v;
                grp.Root.anchoredPosition = new Vector2((g - tab) * 140f * (1f - v), 0f);
                bool on = v > 0.01f;
                if (grp.Root.gameObject.activeSelf != on) grp.Root.gameObject.SetActive(on);
                if (on) foreach (var c in grp.Cards) c.Update(udt, aim, v * Mathf.Clamp01(starterPage.T));
            }
            go.Disabled = starter == null;
        }

        /// <summary>Opening the page again (e.g. after the profile was wiped) starts clean.</summary>
        public void Reset()
        {
            starter = null;
            tab = 0;
            for (int g = 0; g < groups.Count; g++) groups[g].Vis = g == 0 ? 1f : 0f;
        }
    }
}
