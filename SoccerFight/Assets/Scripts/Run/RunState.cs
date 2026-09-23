using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>Everything a single run remembers: position in the stage/wave ladder, the build and a few counters.</summary>
    public sealed class RunState
    {
        public int Stage = 1;
        public int Wave;                 // 1..WavesInStage, WavesInStage + 1 = boss
        public int Kills;
        public float Time;
        /// <summary>Waves and boss fights won this run — every second one ends in an upgrade choice.</summary>
        public int RoundsCleared;
        public int OffersSinceEpic;
        public int RevivesUsed;
        /// <summary>Rolls the platform layouts of this run's stages.</summary>
        public int Seed;

        public const int RoundsPerUpgrade = 2;
        public bool UpgradeDue => RoundsCleared > 0 && RoundsCleared % RoundsPerUpgrade == 0;
        public int RoundsToUpgrade => RoundsPerUpgrade - RoundsCleared % RoundsPerUpgrade;

        public readonly Dictionary<string, int> Owned = new Dictionary<string, int>();
        public readonly List<string> PickOrder = new List<string>();
        public readonly HashSet<Ability> Unlocked = new HashSet<Ability>();
        /// <summary>Unlocked abilities in the order they were gained (the skill bar grows leftwards in this order).</summary>
        public readonly List<Ability> UnlockOrder = new List<Ability>();
        /// <summary>
        /// The four skill slots. The shot and the class move are always there on the mouse buttons;
        /// every boss fight adds one pick to the next free slot and each is played with that slot's key.
        /// </summary>
        public readonly List<Ability> Skills = new List<Ability>();
        public const int MaxSkills = 4;
        public bool CanUnlockMore => Skills.Count < MaxSkills;
        public int SkillCount => Skills.Count;
        public Ability SkillAt(int slot) => slot >= 0 && slot < Skills.Count ? Skills[slot] : Ability.None;
        public int SlotOf(Ability a) => Skills.IndexOf(a);
        /// <summary>Which key plays this ability right now.</summary>
        public GameAction ActionFor(Ability a)
        {
            if (a == Primary) return GameAction.PowerShot;
            int i = SlotOf(a);
            return i < 0 ? GameAction.Shoot : (GameAction)((int)GameAction.Skill1 + i);
        }
        public static bool IsSkill(Ability a) => a != Ability.None && a != Ability.Shot && a != Ability.AirKick && !Abilities.IsClassMove(a);
        /// <summary>The class move on the right mouse button (power shot, dash or header).</summary>
        public Ability Primary = Ability.Power;
        /// <summary>The sport of the player of this run: which moves, skills and upgrade cards exist.</summary>
        public Sport Sport => Characters.Current.Sport;
        public readonly PlayerStats Stats = new PlayerStats();

        public StageTheme Theme => StageThemes.For(Stage);
        public int WavesInStage => Difficulty.WavesInStage(Stage);
        public bool IsBossWave => Wave > WavesInStage;
        public float Level => IsBossWave ? Difficulty.BossLevel(Stage) : Difficulty.Level(Stage, Mathf.Max(1, Wave));

        public static int BestStage
        {
            get => PlayerPrefs.GetInt("sf_best_stage", 0);
            set { PlayerPrefs.SetInt("sf_best_stage", value); PlayerPrefs.Save(); }
        }

        static string BestKey(CharacterDef c) => "sf_best_" + c.Id;

        /// <summary>Best stage reached with one character (shown on the character cards).</summary>
        public static int BestStageOf(CharacterDef c)
        {
            int best = PlayerPrefs.GetInt(BestKey(c), 0);
            // records from before the character ids: the three starters were stored by position
            int index = Characters.IndexOf(c);
            if (c.Starter && index >= 0 && index < 3) best = Mathf.Max(best, PlayerPrefs.GetInt("sf_best_stage_" + index, 0));
            return best;
        }

        /// <summary>A run got this far: keeps the overall and the per-character record.</summary>
        public static void RecordStage(int reached)
        {
            if (Profile.IsTransient) return;   // captures play on a throwaway profile
            if (reached > BestStage) BestStage = reached;
            var c = Characters.Current;
            if (reached > BestStageOf(c)) { PlayerPrefs.SetInt(BestKey(c), reached); PlayerPrefs.Save(); }
        }

        public void Reset()
        {
            Stage = 1; Wave = 0; Kills = 0; Time = 0f; RoundsCleared = 0; OffersSinceEpic = 0; RevivesUsed = 0;
            Seed = Random.Range(1, 1 << 20);
            Owned.Clear(); PickOrder.Clear();
            Unlocked.Clear();
            UnlockOrder.Clear();
            Skills.Clear();
            // the shot and the class move are there from the first second; skills come from the bosses
            Primary = Characters.Current.ClassDef.Primary;
            Unlock(Ability.Shot);
            Unlock(Primary);
        }

        public bool Has(Ability a) => a == Ability.None || Unlocked.Contains(a);
        public int Stacks(string id) => Owned.TryGetValue(id, out int n) ? n : 0;

        /// <summary>
        /// What a boss can add to a free slot: every skill not in a slot yet — all of them belong to
        /// every player. Nothing once all four slots are taken — the boss then pays a second card.
        /// </summary>
        public List<Ability> LockedAbilities()
        {
            var list = new List<Ability>();
            if (!CanUnlockMore) return list;
            foreach (var s in SkillCatalog.ForSport(Sport))
                if (!Unlocked.Contains(s.Ability)) list.Add(s.Ability);
            return list;
        }

        public void Take(UpgradeDef u, Player player)
        {
            Owned[u.Id] = Stacks(u.Id) + 1;
            PickOrder.Add(u.Id);
            Rebuild();
            u.OnPick?.Invoke(player);
        }

        public void Unlock(Ability a)
        {
            // the four slots are the hard limit: a full build takes nothing else
            if (IsSkill(a) && !CanUnlockMore && !Unlocked.Contains(a)) return;
            if (Unlocked.Add(a))
            {
                UnlockOrder.Add(a);
                if (IsSkill(a)) Skills.Add(a);
            }
            Rebuild();
        }

        /// <summary>
        /// Base stats, then the class trait and character perk, then every owned upgrade in database
        /// order (so results never depend on pick order).
        /// </summary>
        public void Rebuild()
        {
            Stats.Reset();
            MetaPassives.Apply(Stats, Characters.Current);
            foreach (var u in UpgradeDb.All)
            {
                int n = Stacks(u.Id);
                if (n > 0) u.Apply(Stats, n);
            }
            Stats.Revives = Mathf.Max(0, Stats.Revives - RevivesUsed);
            var mech = Theme.Mechanic;
            Stats.Slippery = mech == StageMechanic.Ice;
            Stats.GravityMul = mech == StageMechanic.LowGravity ? 0.72f : 1f;
        }
    }
}
