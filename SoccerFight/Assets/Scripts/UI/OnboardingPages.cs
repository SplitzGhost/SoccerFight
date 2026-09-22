using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// The first launch, in two pages the title screen opens on until they are done:
    /// 1. WÄHLE DEINEN SPIELER — one free starter per class, each card showing the class, its talent
    ///    and strengths; WEITER takes the pick to
    /// 2. WÄHLE 3 FÄHIGKEITEN — the skills this class can use; three free picks become the owned,
    ///    equipped starting loadout. LOS GEHT'S commits both choices at once, so leaving halfway
    ///    never hands out a second starter.
    /// </summary>
    public sealed class OnboardingPages
    {
        const int FreeSkills = 3;

        MenuNav nav;
        SubPage starterPage, skillsPage;
        readonly List<CharacterCard> cards = new List<CharacterCard>();
        readonly List<SkillTile> tiles = new List<SkillTile>();
        readonly List<Ability> picks = new List<Ability>();
        CharacterDef starter;
        ChunkButton next, go;
        MenuTarget nextTarget, goTarget;
        TextMeshProUGUI counter, skillsHint;
        float time;

        public SubPage StarterPage => starterPage;
        public SubPage SkillsPage => skillsPage;

        public void Build(RectTransform parent, MenuNav menu)
        {
            nav = menu;
            BuildStarter(parent);
            BuildSkills(parent);
        }

        // ------------------------------------------------------------------ step 1: the starter

        void BuildStarter(RectTransform parent)
        {
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

            next = new ChunkButton(content, "Next", new Vector2(0f, -392f), new Vector2(400f, 84f), MetaUi.Gold, "WEITER", 34f, MenuArt.IconPlay, 30f, true, true);
            next.IconLeft(60f);
            nextTarget = new MenuTarget { Id = "starterNext", Root = next.Root, Size = next.Size, Page = MenuPage.Starter, Button = next, Action = ToSkills, Accent = MetaUi.Gold };
            nav.Register(nextTarget);
            MetaUi.Text(content, "Hint", "Die anderen beiden gibt es später für Münzen im Shop.", 16f, MetaUi.Muted, new Vector2(0f, -452f), new Vector2(900f, 26f));
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

        void ToSkills()
        {
            if (starter == null)
            {
                Sfx.Play(Sound.Denied, 0.7f);
                nav.Say("WÄHLE ERST EINEN SPIELER", next.Root);
                return;
            }
            picks.Clear();
            LayoutSkills();
            nav.Open(MenuPage.StarterSkills);
        }

        // ------------------------------------------------------------------ step 2: three free skills

        void BuildSkills(RectTransform parent)
        {
            skillsPage = new SubPage(parent, MenuPage.StarterSkills, "WÄHLE 3 FÄHIGKEITEN", "GRATIS ZUM START  ·  WEITERE GIBT ES IM SHOP", MetaUi.Gold, nav.Register,
                () => nav.Open(MenuPage.Starter));
            var content = skillsPage.Content;
            foreach (var s in SkillCatalog.All)
            {
                var tile = new SkillTile(content, s, Vector2.zero);
                tiles.Add(tile);
                nav.Register(new MenuTarget
                {
                    Id = "start_" + s.Id, Root = tile.Root, Size = new Vector2(SkillTile.W, SkillTile.H), Page = MenuPage.StarterSkills,
                    Action = () => ToggleSkill(tile), Draw = t => StyleSkill(tile, t), Accent = s.Accent,
                    Visible = () => tile.Root.gameObject.activeSelf ? 1f : 0f,
                });
            }
            counter = MenuArt.Label("Counter", content, "", 22f, Color.white, new Vector2(0f, -330f), new Vector2(600f, 32f), TextAlignmentOptions.Center, 6f);
            go = new ChunkButton(content, "Go", new Vector2(0f, -400f), new Vector2(430f, 88f), MetaUi.Gold, "LOS GEHT'S", 36f, MenuArt.IconPlay, 32f, true, true);
            go.IconLeft(60f);
            goTarget = new MenuTarget { Id = "starterGo", Root = go.Root, Size = go.Size, Page = MenuPage.StarterSkills, Button = go, Action = Finish, Accent = MetaUi.Gold };
            nav.Register(goTarget);
            skillsHint = MetaUi.Text(content, "Hint", "", 15f, MetaUi.Muted, new Vector2(0f, -462f), new Vector2(1000f, 24f));
        }

        /// <summary>Shows the skills the chosen class can use, four to a row.</summary>
        void LayoutSkills()
        {
            var visible = new List<SkillTile>();
            foreach (var t in tiles)
            {
                bool on = starter != null && t.Skill.UsableBy(starter.Class);
                t.Root.gameObject.SetActive(on);
                if (on) visible.Add(t);
            }
            const int perRow = 4;
            const float gx = SkillTile.W + 22f, gy = SkillTile.H + 20f;
            int rows = (visible.Count + perRow - 1) / perRow;
            for (int i = 0; i < visible.Count; i++)
            {
                int row = i / perRow, col = i % perRow;
                int inRow = Mathf.Min(perRow, visible.Count - row * perRow);
                float x = (col - (inRow - 1) * 0.5f) * gx;
                float y = 262f - row * gy + (3 - rows) * gy * 0.5f;
                visible[i].Home = new Vector2(x, y);
                visible[i].Root.anchoredPosition = visible[i].Home;
            }
            skillsHint.text = starter != null && starter.Class == CharacterClass.Defender
                ? "Nur Verteidiger können den KOPFBALL spielen. Jede Fähigkeit liegt später auf einer eigenen Taste."
                : "Jede Fähigkeit liegt auf einer eigenen Taste. Du kannst sie später jederzeit tauschen.";
        }

        void ToggleSkill(SkillTile tile)
        {
            var a = tile.Skill.Ability;
            if (picks.Contains(a))
            {
                picks.Remove(a);
                Sfx.Play(Sound.Unequip, 0.7f);
                return;
            }
            if (!tile.Skill.StarterPick)
            {
                tile.Jiggle = 1f;
                Sfx.Play(Sound.Denied, 0.7f);
                nav.Say("GIBT ES NUR IM SHOP", tile.Root);
                return;
            }
            if (picks.Count >= FreeSkills)
            {
                tile.Jiggle = 1f;
                Sfx.Play(Sound.Denied, 0.7f);
                nav.Say("HÖCHSTENS " + FreeSkills + "  ·  NIMM ERST EINE RAUS", tile.Root);
                return;
            }
            picks.Add(a);
            tile.Jiggle = 0.6f;
            Sfx.Play(Sound.Equip, 0.75f);
        }

        void StyleSkill(SkillTile tile, MenuTarget t)
        {
            var s = tile.Skill;
            int slot = picks.IndexOf(s.Ability);
            tile.IsSelected = slot >= 0;
            tile.SlotNumber = slot;
            tile.Locked = !s.StarterPick;
            tile.Dimmed = !s.StarterPick || (slot < 0 && picks.Count >= FreeSkills);
            if (slot >= 0) tile.SetStatus("GEWÄHLT  ·  PLATZ " + (slot + 1), MetaUi.Gold);
            else if (!s.StarterPick) tile.SetStatus("NUR IM SHOP", MetaUi.Muted);
            else tile.SetStatus("GRATIS", Color.Lerp(MetaUi.Muted, Color.white, 0.4f));
            if (starter != null) SkillPage.Corner(tile, starter);
            tile.Style(t, TimeFx.UiDelta);
        }

        void Finish()
        {
            if (starter == null) { nav.Open(MenuPage.Starter); return; }
            if (picks.Count < FreeSkills)
            {
                Sfx.Play(Sound.Denied, 0.7f);
                int left = FreeSkills - picks.Count;
                nav.Say(left == 1 ? "WÄHLE NOCH EINE FÄHIGKEIT" : "WÄHLE NOCH " + left + " FÄHIGKEITEN", go.Root);
                return;
            }
            Profile.ChooseStarter(starter);
            Characters.Select(Characters.IndexOf(starter));
            Profile.ChooseStartSkills(picks);
            Sfx.Play(Sound.Purchase, 0.8f);
            nav.Open(MenuPage.Main);
            nav.Say("WILLKOMMEN, " + starter.Name + "!  ·  SCHIESS AUF SPIELEN", null);
        }

        // ------------------------------------------------------------------ update

        public void Update(float udt, Vector2 aim)
        {
            time += udt;
            starterPage.Update(udt);
            skillsPage.Update(udt);
            if (starterPage.T > 0.01f)
            {
                PlayerArt.Pump();
                foreach (var c in cards) c.Update(udt, aim, Mathf.Clamp01(starterPage.T));
                next.Disabled = starter == null;
            }
            if (skillsPage.T > 0.01f)
            {
                counter.text = picks.Count + " / " + FreeSkills + "  GEWÄHLT";
                counter.color = picks.Count >= FreeSkills ? MetaUi.Gold : Color.white;
                go.Disabled = picks.Count < FreeSkills;
            }
        }

        /// <summary>Opening the first page again (e.g. after the profile was wiped) starts clean.</summary>
        public void Reset()
        {
            starter = null;
            picks.Clear();
        }
    }
}
