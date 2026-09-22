using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// The first launch: WÄHLE DEINEN SPIELER — one free starter per class, each card showing the
    /// class, its talent, its right-click move and the character's own perk. LOS GEHT'S commits the
    /// pick. Skills need no choosing: every player has all of them and picks them up after boss
    /// fights.
    /// </summary>
    public sealed class OnboardingPages
    {
        MenuNav nav;
        SubPage starterPage;
        readonly List<CharacterCard> cards = new List<CharacterCard>();
        CharacterDef starter;
        ChunkButton go;

        public SubPage StarterPage => starterPage;

        public void Build(RectTransform parent, MenuNav menu)
        {
            nav = menu;
            starterPage = new SubPage(parent, MenuPage.Starter, "WÄHLE DEINEN SPIELER", "DEIN ERSTER SPIELER IST GRATIS  ·  EINER PRO KLASSE", MetaUi.Gold, nav.Register, null);
            var content = starterPage.Content;
            float step = CharacterCard.W + 50f;
            int i = 0;
            foreach (var def in Characters.Starters)
            {
                var card = new CharacterCard(content, def, new Vector2((i - 1) * step, 22f), CharacterCard.Mode.Starter);
                cards.Add(card);
                nav.Register(new MenuTarget
                {
                    Id = "starter" + i, Root = card.Root, Size = new Vector2(CharacterCard.W, CharacterCard.H), Page = MenuPage.Starter,
                    Action = () => PickStarter(card), Draw = t => StyleStarter(card, t), Accent = def.Accent,
                });
                i++;
            }

            go = new ChunkButton(content, "Go", new Vector2(0f, -392f), new Vector2(430f, 84f), MetaUi.Gold, "LOS GEHT'S", 34f, MenuArt.IconPlay, 30f, true, true);
            go.IconLeft(60f);
            nav.Register(new MenuTarget { Id = "starterGo", Root = go.Root, Size = go.Size, Page = MenuPage.Starter, Button = go, Action = Finish, Accent = MetaUi.Gold });
            MetaUi.Text(content, "Hint", "Die anderen gibt es später für Münzen im Shop.  ·  Fähigkeiten hast du alle: nach jedem Boss wählst du eine neue, höchstens " + RunState.MaxSkills + " pro Lauf.",
                16f, MetaUi.Muted, new Vector2(0f, -452f), new Vector2(1500f, 26f));
        }

        void PickStarter(CharacterCard card)
        {
            bool changed = starter != card.Def;
            starter = card.Def;
            card.Jiggle = 1f;
            // the title screen's player follows the pick
            Characters.Preview(Characters.IndexOf(card.Def));
            if (changed) Sfx.Play(Sound.Select, 0.8f);
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
                Sfx.Play(Sound.Denied, 0.7f);
                nav.Say("WÄHLE ERST EINEN SPIELER", go.Root);
                return;
            }
            Profile.ChooseStarter(starter);
            Characters.Select(Characters.IndexOf(starter));
            Sfx.Play(Sound.Purchase, 0.8f);
            nav.Open(MenuPage.Main);
            nav.Say("WILLKOMMEN, " + starter.Name + "!  ·  SCHIESS AUF SPIELEN", null);
        }

        public void Update(float udt, Vector2 aim)
        {
            starterPage.Update(udt);
            if (starterPage.T < 0.01f) return;
            PlayerArt.Pump();
            foreach (var c in cards) c.Update(udt, aim, Mathf.Clamp01(starterPage.T));
            go.Disabled = starter == null;
        }

        /// <summary>Opening the page again (e.g. after the profile was wiped) starts clean.</summary>
        public void Reset() => starter = null;
    }
}
