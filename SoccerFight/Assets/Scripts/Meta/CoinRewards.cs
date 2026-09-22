using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// How many coins the run pays and when. Every kill drops coins by rank, later stages pay a bit
    /// more per kill, and clearing a stage drops a bonus shower at the boss. The numbers aim at
    /// roughly 100 coins for a first run that dies in stage 2 and 400+ for a run to stage 4 — so the
    /// first bought skill (150–300) is one or two runs away and a new character (750+) a goal for a
    /// handful of good runs.
    /// </summary>
    public static class CoinRewards
    {
        public const int Normal = 1, Elite = 4, MiniBoss = 12, Boss = 30;
        /// <summary>Small fry (splitter offspring) only pay now and then.</summary>
        public const float SpawnlingChance = 0.35f;
        /// <summary>+12 % coins per kill for every stage past the first.</summary>
        public const float PerStage = 0.12f;
        /// <summary>Stage clear bonus: this × stage number.</summary>
        public const int StageClear = 10;

        /// <summary>Coins dropped this run (the death screen shows it).</summary>
        public static int Earned { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Earned = 0;

        public static void ResetRun() => Earned = 0;

        public static void Note(int amount) => Earned += Mathf.Max(0, amount);

        public static float StageMul(int stage) => 1f + PerStage * Mathf.Max(0, stage - 1);

        public static int ForKill(Monster m, int stage)
        {
            float v;
            switch (m.Rank)
            {
                case Rank.Boss: v = Boss; break;
                case Rank.MiniBoss: v = MiniBoss; break;
                case Rank.Elite: v = Elite; break;
                default: v = m.Type == EnemyType.Spawnling ? (Random.value < SpawnlingChance ? Normal : 0) : Normal; break;
            }
            return RoundRandom(v * StageMul(stage));
        }

        public static int ForStageClear(int stage) => StageClear * Mathf.Max(1, stage);

        /// <summary>1.3 pays 1 most of the time and 2 now and then — fair on average, never fractional.</summary>
        static int RoundRandom(float v)
        {
            int n = Mathf.FloorToInt(v);
            return n + (Random.value < v - n ? 1 : 0);
        }
    }
}
