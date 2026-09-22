using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Everything the game remembers between sessions, as one JSON document in PlayerPrefs (which is
    /// IndexedDB in the browser build). Ids are strings so new characters, currencies and upgrade
    /// levels never need a new format — only Version bumps when a migration is required.
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
        public List<Entry> Wallet = new List<Entry>();
        /// <summary>Levels of anything that can be leveled (character upgrades, skill upgrades).</summary>
        public List<Entry> Levels = new List<Entry>();
        /// <summary>One-time steps that are done ("starter").</summary>
        public List<string> Flags = new List<string>();
        public int CoinsEarned;
        public int Runs;
    }

    /// <summary>
    /// The persistent profile: onboarding, owned characters, the selected character and the wallet.
    /// Skills are not stored: every player has all of them and picks them up during a run. Writes are
    /// batched — call Save at natural checkpoints (a purchase, the end of a round, leaving to the
    /// menu); SaveIfDirty is cheap to call often. Captures run on a transient profile so they never
    /// touch the player's save.
    /// </summary>
    public static class Profile
    {
        public const int CurrentVersion = 1;
        const string Key = "sf_profile";
        public const string FlagStarter = "starter";

        static ProfileData data;
        static bool transient, dirty;

        /// <summary>Anything in the profile changed (ownership, selection, wallet).</summary>
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
            if (d.Wallet == null) d.Wallet = new List<ProfileData.Entry>();
            if (d.Levels == null) d.Levels = new List<ProfileData.Entry>();
            if (d.Flags == null) d.Flags = new List<string>();
            d.Characters.RemoveAll(id => Characters.Get(id) == null);
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
        /// <summary>The first launch is done: the free starter is chosen.</summary>
        public static bool Onboarded => HasStarter;

        /// <summary>The first launch: the free starter becomes the first owned (and selected) character.</summary>
        public static void ChooseStarter(CharacterDef c)
        {
            GrantCharacter(c.Id);
            SelectCharacter(c.Id);
            SetFlag(FlagStarter);
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
            MarkDirty();
            return true;
        }

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
