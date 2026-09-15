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
        public int Rerolls;
        public int OffersSinceEpic;
        public int RevivesUsed;

        public readonly Dictionary<string, int> Owned = new Dictionary<string, int>();
        public readonly List<string> PickOrder = new List<string>();
        public readonly HashSet<Ability> Unlocked = new HashSet<Ability>();
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

        public void Reset()
        {
            Stage = 1; Wave = 0; Kills = 0; Time = 0f; Rerolls = 1; OffersSinceEpic = 0; RevivesUsed = 0;
            Owned.Clear(); PickOrder.Clear();
            Unlocked.Clear();
            Unlocked.Add(Ability.Shot);
            Unlocked.Add(Ability.Power);
            Rebuild();
        }

        public bool Has(Ability a) => a == Ability.None || Unlocked.Contains(a);
        public int Stacks(string id) => Owned.TryGetValue(id, out int n) ? n : 0;

        public List<Ability> LockedAbilities()
        {
            var list = new List<Ability>();
            foreach (var a in Abilities.Unlockable) if (!Unlocked.Contains(a)) list.Add(a);
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
            Unlocked.Add(a);
            Rebuild();
        }

        /// <summary>Base stats, then every owned upgrade in database order (so results never depend on pick order).</summary>
        public void Rebuild()
        {
            Stats.Reset();
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
