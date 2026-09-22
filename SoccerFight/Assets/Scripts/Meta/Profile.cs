using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Everything the game remembers between sessions, as one JSON document in PlayerPrefs (which is
    /// IndexedDB in the browser build). Ids are strings so new characters, skills, currencies and
    /// upgrade levels never need a new format — only Version bumps when a migration is required.
    /// </summary>
    [Serializable]
    public sealed class ProfileData
    {
        [Serializable]
        public struct Entry
        {
            public string Id;
            public int Value;
        }

        public int Version = Profile.CurrentVersion;
        /// <summary>Selected character id.</summary>
        public string Character = "";
        public List<string> Characters = new List<string>();
        public List<string> Skills = new List<string>();
        /// <summary>Equipped skill ids in slot order (slot 1 = first key).</summary>
        public List<string> Loadout = new List<string>();
        public List<Entry> Wallet = new List<Entry>();
        /// <summary>Levels of anything that can be leveled (character upgrades, skill upgrades).</summary>
        public List<Entry> Levels = new List<Entry>();
        /// <summary>One-time steps that are done ("starter", "skills").</summary>
        public List<string> Flags = new List<string>();
        public int CoinsEarned;
        public int Runs;
    }

    /// <summary>
    /// The persistent profile: onboarding, owned characters and skills, the four-slot loadout, the
    /// selected character and the wallet. Writes are batched — call Save at natural checkpoints
    /// (a purchase, the end of a round, leaving to the menu); SaveIfDirty is cheap to call often.
    /// Captures run on a transient profile so they never touch the player's save.
    /// </summary>
    public static class Profile
    {
        public const int CurrentVersion = 1;
        const string Key = "sf_profile";
        public const string FlagStarter = "starter", FlagSkills = "skills";

        static ProfileData data;
        static bool transient, dirty;

        /// <summary>Anything in the profile changed (ownership, loadout, selection, wallet).</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { data = null; transient = false; dirty = false; Changed = null; Wallet.ResetStatics(); }

        public static ProfileData Data { get { Load(); return data; } }
        public static bool IsTransient => transient;

        public static void Load()
        {
            if (data != null) return;
            string json = PlayerPrefs.GetString(Key, "");
            if (!string.IsNullOrEmpty(json))
            {
                try { data = JsonUtility.FromJson<ProfileData>(json); }
                catch (Exception e) { Debug.LogWarning("[SoccerFight] Profile unreadable, starting fresh: " + e.Message); }
            }
            if (data == null) data = Fresh();
            Migrate(data);
        }

        /// <summary>An in-memory profile (captures, tests): nothing is ever written to disk.</summary>
        public static void UseTransient(ProfileData seed = null)
        {
            transient = true;
            data = seed ?? Fresh();
            Migrate(data);
            Changed?.Invoke();
        }

        static ProfileData Fresh()
        {
            var d = new ProfileData();
            foreach (var c in Currencies.All) if (c.Start != 0) d.Wallet.Add(new ProfileData.Entry { Id = c.Id, Value = c.Start });
            return d;
        }

        /// <summary>Fills gaps (lists missing in older saves) and drops ids the game no longer knows.</summary>
        static void Migrate(ProfileData d)
        {
            if (d.Characters == null) d.Characters = new List<string>();
            if (d.Skills == null) d.Skills = new List<string>();
            if (d.Loadout == null) d.Loadout = new List<string>();
            if (d.Wallet == null) d.Wallet = new List<ProfileData.Entry>();
            if (d.Levels == null) d.Levels = new List<ProfileData.Entry>();
            if (d.Flags == null) d.Flags = new List<string>();
            d.Characters.RemoveAll(id => Characters.Get(id) == null);
            d.Skills.RemoveAll(id => SkillCatalog.Get(id) == null);
            d.Loadout.RemoveAll(id => SkillCatalog.Get(id) == null || !d.Skills.Contains(id));
            if (d.Loadout.Count > RunState.MaxSkills) d.Loadout.RemoveRange(RunState.MaxSkills, d.Loadout.Count - RunState.MaxSkills);
            if (Characters.Get(d.Character) == null || !d.Characters.Contains(d.Character))
                d.Character = d.Characters.Count > 0 ? d.Characters[0] : "";
            d.Version = CurrentVersion;
        }

        public static void MarkDirty() { dirty = true; Changed?.Invoke(); }

        public static void Save()
        {
            dirty = false;
            if (transient || data == null) return;
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        public static void SaveIfDirty() { if (dirty) Save(); }

        /// <summary>Wipes the profile (developer panel): the next title screen starts the onboarding.</summary>
        public static void Reset()
        {
            data = Fresh();
            Save();
            Changed?.Invoke();
        }

        // ------------------------------------------------------------------ onboarding

        public static bool HasFlag(string flag) => Data.Flags.Contains(flag);

        public static void SetFlag(string flag)
        {
            if (Data.Flags.Contains(flag)) return;
            data.Flags.Add(flag);
            MarkDirty();
        }

        public static bool HasStarter => HasFlag(FlagStarter) && Data.Characters.Count > 0;
        public static bool HasStartSkills => HasFlag(FlagSkills);
        /// <summary>Starter character and the three free skills are chosen.</summary>
        public static bool Onboarded => HasStarter && HasStartSkills;

        /// <summary>First-launch step 1: the free starter becomes the first owned (and selected) character.</summary>
        public static void ChooseStarter(CharacterDef c)
        {
            GrantCharacter(c.Id);
            SelectCharacter(c.Id);
            SetFlag(FlagStarter);
            Save();
        }

        /// <summary>First-launch step 2: the free skills are owned and equipped in the order they were picked.</summary>
        public static void ChooseStartSkills(IList<Ability> skills)
        {
            data.Loadout.Clear();
            foreach (var a in skills)
            {
                GrantSkill(a);
                Equip(a);
            }
            SetFlag(FlagSkills);
            Save();
        }

        // ------------------------------------------------------------------ characters

        public static bool OwnsCharacter(string id) => Data.Characters.Contains(id);

        public static void GrantCharacter(string id)
        {
            if (Characters.Get(id) == null || OwnsCharacter(id)) return;
            data.Characters.Add(id);
            MarkDirty();
        }

        public static string SelectedCharacter => Data.Character;

        public static bool SelectCharacter(string id)
        {
            if (!OwnsCharacter(id)) return false;
            if (data.Character == id) return true;
            data.Character = id;
            // a class-locked skill the new character can't use leaves the loadout
            var def = Characters.Get(id);
            data.Loadout.RemoveAll(s => !SkillCatalog.Get(s).UsableBy(def.Class));
            MarkDirty();
            return true;
        }

        // ------------------------------------------------------------------ skills and loadout

        public static bool OwnsSkill(Ability a)
        {
            var s = SkillCatalog.Get(a);
            return s != null && Data.Skills.Contains(s.Id);
        }

        public static void GrantSkill(Ability a)
        {
            var s = SkillCatalog.Get(a);
            if (s == null || OwnsSkill(a)) return;
            data.Skills.Add(s.Id);
            MarkDirty();
        }

        /// <summary>The equipped skills in slot order.</summary>
        public static List<Ability> Loadout()
        {
            var list = new List<Ability>();
            foreach (var id in Data.Loadout)
            {
                var s = SkillCatalog.Get(id);
                if (s != null) list.Add(s.Ability);
            }
            return list;
        }

        public static bool IsEquipped(Ability a)
        {
            var s = SkillCatalog.Get(a);
            return s != null && Data.Loadout.Contains(s.Id);
        }

        public static int SlotOf(Ability a)
        {
            var s = SkillCatalog.Get(a);
            return s == null ? -1 : Data.Loadout.IndexOf(s.Id);
        }

        public static bool LoadoutFull => Data.Loadout.Count >= RunState.MaxSkills;

        public enum EquipResult { Equipped, Unequipped, NotOwned, Full, WrongClass }

        public static EquipResult Equip(Ability a)
        {
            var s = SkillCatalog.Get(a);
            if (s == null || !OwnsSkill(a)) return EquipResult.NotOwned;
            if (IsEquipped(a)) return EquipResult.Equipped;
            var ch = Characters.Get(data.Character);
            if (ch != null && !s.UsableBy(ch.Class)) return EquipResult.WrongClass;
            if (LoadoutFull) return EquipResult.Full;
            data.Loadout.Add(s.Id);
            MarkDirty();
            return EquipResult.Equipped;
        }

        public static EquipResult Unequip(Ability a)
        {
            var s = SkillCatalog.Get(a);
            if (s != null && data.Loadout.Remove(s.Id)) MarkDirty();
            return EquipResult.Unequipped;
        }

        public static EquipResult Toggle(Ability a) => IsEquipped(a) ? Unequip(a) : Equip(a);

        // ------------------------------------------------------------------ levels (future upgrades)

        public static int Level(string id)
        {
            foreach (var e in Data.Levels) if (e.Id == id) return e.Value;
            return 0;
        }

        public static void SetLevel(string id, int level)
        {
            var list = Data.Levels;
            for (int i = 0; i < list.Count; i++)
                if (list[i].Id == id) { list[i] = new ProfileData.Entry { Id = id, Value = level }; MarkDirty(); return; }
            list.Add(new ProfileData.Entry { Id = id, Value = level });
            MarkDirty();
        }

        // ------------------------------------------------------------------ stats

        public static void OnRunFinished()
        {
            Data.Runs++;
            Save();
        }

    }

    /// <summary>
    /// Balances of every currency, stored in the profile. Changed fires with the old and the new
    /// amount so counters can animate the difference.
    /// </summary>
    public static class Wallet
    {
        public static event Action<CurrencyDef, int, int> Changed;

        internal static void ResetStatics() => Changed = null;

        public static int Get(CurrencyDef c)
        {
            foreach (var e in Profile.Data.Wallet) if (e.Id == c.Id) return e.Value;
            return 0;
        }

        static void Set(CurrencyDef c, int value)
        {
            int old = Get(c);
            if (old == value) return;
            var list = Profile.Data.Wallet;
            bool found = false;
            for (int i = 0; i < list.Count; i++)
                if (list[i].Id == c.Id) { list[i] = new ProfileData.Entry { Id = c.Id, Value = value }; found = true; break; }
            if (!found) list.Add(new ProfileData.Entry { Id = c.Id, Value = value });
            Profile.MarkDirty();
            Changed?.Invoke(c, old, value);
        }

        public static void Add(CurrencyDef c, int amount)
        {
            if (amount <= 0) return;
            if (c == Currencies.Coins) Profile.Data.CoinsEarned += amount;
            Set(c, Get(c) + amount);
        }

        public static bool CanAfford(Price p) => p.Free || Get(p.Currency) >= p.Amount;

        /// <summary>How much is missing for a price (0 when affordable).</summary>
        public static int Missing(Price p) => Mathf.Max(0, p.Amount - Get(p.Currency));

        public static bool TrySpend(Price p)
        {
            if (p.Free) return true;
            if (!CanAfford(p)) return false;
            Set(p.Currency, Get(p.Currency) - p.Amount);
            return true;
        }
    }
}
