using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoccerFight
{
    /// <summary>
    /// FÄHIGKEITEN: the four-slot loadout on top (each slot with its key), every skill as a tile
    /// underneath. Kick an owned skill to equip it into the next free slot, kick it again — or its
    /// slot — to take it off. Skills the player doesn't own show their price and lead to the shop;
    /// skills the current class can't use are dimmed. Tiles whose category the class talent boosts
    /// say so in the corner, so the class and the loadout read as one decision.
    /// </summary>
    public sealed class SkillPage
    {
        sealed class Slot
        {
            public RectTransform Root;
            public Image Disc, Ring, Icon, Glow, Empty;
            public TextMeshProUGUI Key, Name;
            public Ability Shown = Ability.None;
            public float Pop, PopVel;
        }

        MenuNav nav;
        SubPage page;
        readonly Slot[] slots = new Slot[RunState.MaxSkills];
        readonly List<SkillTile> tiles = new List<SkillTile>();
        TextMeshProUGUI classLine;
        List<Ability> loadout = new List<Ability>();
        WalletChip wallet;
        float time;

        /// <summary>Hit a locked skill: the page opens the shop on this item.</summary>
        public System.Action<ShopItem> ShowInShop;

        public SubPage Page => page;

        public void Build(RectTransform parent, MenuNav menu)
        {
            nav = menu;
            page = new SubPage(parent, MenuPage.Skills, "FÄHIGKEITEN", "4 PLÄTZE  ·  AUSRÜSTEN UND ABLEGEN", MetaUi.Gold, nav.Register, nav.Back);
            var content = page.Content;
            // the coins sit in the top bar now; the chip stays as the page's own counter but hidden
            wallet = new WalletChip(page.Root, new Vector2(1f, 1f), new Vector2(-150f, -78f));
            wallet.Root.gameObject.SetActive(false);

            // the loadout: four slots in a row, like the skill bar in the game
            var bar = UiKit.Node("Loadout", content, new Vector2(0f, 312f), new Vector2(1000f, 150f));
            for (int i = 0; i < slots.Length; i++)
            {
                var s = new Slot();
                s.Root = UiKit.Node("Slot" + i, bar, new Vector2((i - 1.5f) * 200f, 10f), new Vector2(120f, 150f));
                s.Glow = UiKit.Img("Glow", s.Root, UiArt.Glow, MetaUi.Gold.WithAlpha(0f), new Vector2(0f, 14f), new Vector2(220f, 220f));
                s.Disc = UiKit.Img("Disc", s.Root, MenuArt.Round, new Color(0.02f, 0.05f, 0.08f, 0.9f), new Vector2(0f, 14f), new Vector2(104f, 104f));
                s.Empty = UiKit.Img("Empty", s.Root, UiArt.RingThin, Color.white.WithAlpha(0.25f), new Vector2(0f, 14f), new Vector2(84f, 84f));
                s.Ring = UiKit.Img("Ring", s.Root, UiArt.RingThick, MetaUi.Gold, new Vector2(0f, 14f), new Vector2(108f, 108f));
                s.Icon = UiKit.Img("Icon", s.Root, null, Color.white, new Vector2(0f, 14f), new Vector2(70f, 70f));
                s.Icon.preserveAspect = true;
                var key = UiKit.Img("KeyPill", s.Root, UiArt.Pill, new Color(0.08f, 0.14f, 0.2f, 0.95f), new Vector2(0f, -46f), new Vector2(54f, 30f), Image.Type.Sliced);
                s.Key = MenuArt.Label("Key", key.transform, "E", 17f, new Color(0.85f, 0.95f, 1f), new Vector2(0f, 1f), new Vector2(54f, 30f), TextAlignmentOptions.Center, 1f, MenuArt.TextHeavySoft);
                s.Name = MenuArt.Label("Name", s.Root, "", 15f, Color.white, new Vector2(0f, -76f), new Vector2(200f, 24f), TextAlignmentOptions.Center, 3f, MenuArt.TextHeavySoft);
                slots[i] = s;
                int index = i;
                nav.Register(new MenuTarget
                {
                    Id = "slot" + i, Root = s.Root, Size = new Vector2(130f, 130f), Page = MenuPage.Skills,
                    Action = () => HitSlot(index), Draw = t => StyleSlot(slots[index], t), Accent = MetaUi.Gold,
                });
            }
            classLine = MenuArt.Label("Class", content, "", 16f, MetaUi.Muted, new Vector2(0f, 206f), new Vector2(1400f, 26f), TextAlignmentOptions.Center, 3f, MenuArt.TextHeavySoft);

            // every skill, four to a row
            const int perRow = 4;
            const float gx = SkillTile.W + 22f, gy = SkillTile.H + 20f;
            int count = SkillCatalog.All.Count;
            for (int i = 0; i < count; i++)
            {
                var s = SkillCatalog.All[i];
                int row = i / perRow, col = i % perRow;
                int inRow = Mathf.Min(perRow, count - row * perRow);
                var tile = new SkillTile(content, s, new Vector2((col - (inRow - 1) * 0.5f) * gx, 88f - row * gy));
                tiles.Add(tile);
                nav.Register(new MenuTarget
                {
                    Id = "skill_" + s.Id, Root = tile.Root, Size = new Vector2(SkillTile.W, SkillTile.H), Page = MenuPage.Skills,
                    Action = () => HitTile(tile), Draw = t => StyleTile(tile, t), Accent = s.Accent,
                });
            }
            MetaUi.Text(content, "Hint", "Kick eine Fähigkeit zum Ausrüsten, noch einmal zum Ablegen. Neue Fähigkeiten gibt es im Shop.",
                15f, MetaUi.Muted, new Vector2(0f, -440f), new Vector2(1200f, 24f));
        }

        public void Refresh()
        {
            var ch = Characters.Current;
            var cls = ch.ClassDef;
            classLine.text = ch.Name + "  ·  " + cls.Name + "  ·  TALENT " + cls.TraitName + ":  " + cls.Trait.Text;
            classLine.color = MetaUi.Soft(ch.Accent);
        }

        // ------------------------------------------------------------------ actions

        void HitSlot(int i)
        {
            loadout = Profile.Loadout();
            if (i >= loadout.Count)
            {
                Sfx.Play(Sound.Denied, 0.6f);
                nav.Say("PLATZ " + (i + 1) + " IST LEER  ·  KICKE UNTEN EINE FÄHIGKEIT", slots[i].Root);
                return;
            }
            var a = loadout[i];
            Profile.Unequip(a);
            Profile.Save();
            Sfx.Play(Sound.Unequip, 0.8f);
            foreach (var s in slots) s.PopVel -= 4f;
        }

        void HitTile(SkillTile tile)
        {
            var s = tile.Skill;
            var ch = Characters.Current;
            if (!s.UsableBy(ch.Class))
            {
                tile.Jiggle = 1f;
                Sfx.Play(Sound.Denied, 0.7f);
                nav.Say("NUR FÜR " + Classes.Of(s.ClassLock.Value).Name + "  ·  " + ch.Name + " IST " + ch.Role, tile.Root);
                return;
            }
            if (!Profile.OwnsSkill(s.Ability))
            {
                Sfx.Play(Sound.Select, 0.6f);
                ShowInShop?.Invoke(Shop.ForSkill(s.Ability));
                return;
            }
            var r = Profile.Toggle(s.Ability);
            switch (r)
            {
                case Profile.EquipResult.Equipped:
                    Profile.Save();
                    Sfx.Play(Sound.Equip, 0.85f);
                    tile.Jiggle = 0.6f;
                    int slot = Profile.SlotOf(s.Ability);
                    if (slot >= 0) slots[slot].PopVel += 14f;
                    break;
                case Profile.EquipResult.Unequipped:
                    Profile.Save();
                    Sfx.Play(Sound.Unequip, 0.8f);
                    break;
                case Profile.EquipResult.Full:
                    tile.Jiggle = 1f;
                    Sfx.Play(Sound.Denied, 0.7f);
                    nav.Say("ALLE 4 PLÄTZE BELEGT  ·  LEGE ERST EINE AB", tile.Root);
                    foreach (var sl in slots) sl.PopVel += 6f;
                    break;
            }
        }

        // ------------------------------------------------------------------ drawing

        void StyleSlot(Slot s, MenuTarget t)
        {
            float udt = TimeFx.UiDelta;
            MathUtil.Spring(ref s.Pop, ref s.PopVel, 0f, 5f, 0.35f, udt);
            int i = System.Array.IndexOf(slots, s);

            Ability a = i < loadout.Count ? loadout[i] : Ability.None;
            if (a != s.Shown)
            {
                s.Shown = a;
                s.Icon.sprite = a != Ability.None ? Abilities.Icon(a) : null;
                s.Name.text = a != Ability.None ? Abilities.Name(a) : "LEER";
            }
            bool full = a != Ability.None;
            float h = Mathf.Clamp01(t.Hover);
            float sc = 1f + s.Pop * 0.12f + h * 0.05f + t.Punch * 0.05f;
            s.Root.localScale = new Vector3(sc, sc, 1f);
            Color accent = full ? Abilities.Accent(a) : Color.white;
            s.Icon.enabled = full;
            s.Empty.enabled = !full;
            s.Empty.color = Color.white.WithAlpha(0.18f + 0.12f * Mathf.Sin(time * 3f + i));
            s.Ring.color = full ? Color.Lerp(accent, MetaUi.Gold, 0.35f).WithAlpha(0.9f) : Color.white.WithAlpha(0.1f + 0.2f * h);
            s.Glow.color = (full ? accent : Color.white).WithAlpha((full ? 0.18f : 0.04f) + 0.12f * h + t.Hit * 0.3f);
            s.Name.color = full ? Color.white : MetaUi.Muted;
            string key = KeyBindings.ShortName((GameAction)((int)GameAction.Skill1 + i));
            if (s.Key.text != key) s.Key.text = key;
        }

        void StyleTile(SkillTile tile, MenuTarget t)
        {
            var s = tile.Skill;
            var ch = Characters.Current;
            bool usable = s.UsableBy(ch.Class);
            bool owned = Profile.OwnsSkill(s.Ability);
            int slot = Profile.SlotOf(s.Ability);
            tile.IsSelected = slot >= 0;
            tile.SlotNumber = slot;
            tile.Locked = !owned;
            tile.Dimmed = !usable;
            if (!usable) tile.SetStatus("NUR " + Classes.Of(s.ClassLock.Value).Name, MetaUi.Muted);
            else if (slot >= 0) tile.SetStatus("AUSGERÜSTET  ·  PLATZ " + (slot + 1), MetaUi.Gold);
            else if (owned) tile.SetStatus(Profile.LoadoutFull ? "IM BESITZ" : "AUSRÜSTEN", Profile.LoadoutFull ? MetaUi.Muted : Color.white);
            else tile.SetPrice(s.Cost);
            // the class talent that boosts this kind of skill
            Corner(tile, ch);

            tile.Style(t, TimeFx.UiDelta);
        }

        /// <summary>The tile corner: the talent that boosts this skill for the character, or who may use a class skill.</summary>
        public static void Corner(SkillTile tile, CharacterDef ch)
        {
            var s = tile.Skill;
            string note = s.UsableBy(ch.Class) ? TalentNote(ch.ClassDef, s.Category) : "";
            if (note.Length > 0) tile.SetBoost(note, MetaUi.Soft(ch.Accent));
            else if (s.ClassLock != null) tile.SetBoost("NUR " + Classes.Of(s.ClassLock.Value).Name, MetaUi.Soft(Classes.Of(s.ClassLock.Value).Accent));
            else tile.SetBoost("", Color.white);
        }

        /// <summary>"+30 %" on the skills the class talent makes stronger.</summary>
        public static string TalentNote(ClassDef cls, SkillCategory cat)
        {
            if (cls.Class == CharacterClass.Striker && cat == SkillCategory.Shot) return "TALENT +" + Mathf.RoundToInt(ClassTuning.StrikerShotBonus * 100f) + " %";
            if (cls.Class == CharacterClass.Skiller && cat == SkillCategory.Technique) return "TALENT +" + Mathf.RoundToInt(ClassTuning.SkillerTechBonus * 100f) + " %";
            if (cls.Class == CharacterClass.Defender && cat == SkillCategory.Header) return "TALENT +" + Mathf.RoundToInt(ClassTuning.DefenderHeaderBonus * 100f) + " %";
            return "";
        }

        public void Update(float udt)
        {
            time += udt;
            page.Update(udt);
            if (page.T < 0.01f) return;
            wallet.Update(udt);
            loadout = Profile.Loadout();
        }
    }
}
