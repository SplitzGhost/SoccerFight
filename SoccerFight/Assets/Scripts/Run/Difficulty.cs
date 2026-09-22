using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// One continuous difficulty level L drives every enemy number. Waves inside a stage add 1 each;
    /// a new stage starts a little below where the previous one ended (StageOverlap), so the last
    /// wave of stage N is slightly harder than the first wave of stage N+1 and the curve never jumps.
    /// Health grows linear × gently exponential, damage linearly, speed saturates, spawn budget
    /// sub-linearly — which keeps it sane for dozens of stages while player builds snowball.
    /// </summary>
    public static class Difficulty
    {
        public const float WaveStep = 1f;
        public const float StageOverlap = 0.35f;

        /// <summary>Regular waves per stage (the boss fight comes on top).</summary>
        public static int WavesInStage(int stage) => stage <= 1 ? 3 : stage == 2 ? 4 : 5;

        /// <summary>Level of the first wave of a stage.</summary>
        public static float StageBase(int stage)
        {
            float b = 0f;
            for (int s = 1; s < stage; s++) b += (WavesInStage(s) - 1) * WaveStep - StageOverlap;
            return b;
        }

        public static float Level(int stage, int wave) => StageBase(stage) + (wave - 1) * WaveStep;

        /// <summary>The boss sits half a step above the stage's last wave.</summary>
        public static float BossLevel(int stage) => Level(stage, WavesInStage(stage)) + 0.5f;

        public static float HealthMul(float level) => (1f + 0.1f * level) * Mathf.Pow(1.016f, level);
        public static float DamageMul(float level) => 1f + 0.045f * level;
        public static float SpeedMul(float level) => 1f + 0.3f * (1f - Mathf.Exp(-level / 14f));

        /// <summary>Threat points to spend on a wave (a basic hopper costs 1).</summary>
        public static float Budget(float level) => 5f + 2f * Mathf.Pow(level, 0.9f);

        public static int MaxAlive(float level) => Mathf.Min(14, 4 + Mathf.RoundToInt(level * 0.45f));
        public static float SpawnInterval(float level) => Mathf.Max(0.45f, 1.4f - 0.04f * level);

        /// <summary>Chance that a spawned regular enemy is an elite (from stage 1, wave 3).</summary>
        public static float EliteChance(int stage, int wave, float level)
            => stage == 1 && wave < 3 ? 0f : Mathf.Min(0.3f, 0.015f + 0.011f * level);

        public static int EliteAffixes(int stage) => stage >= 7 ? 3 : stage >= 4 ? 2 : 1;

        /// <summary>Mini-bosses in the last regular wave from stage 2, a second one from stage 5.</summary>
        public static int MiniBosses(int stage, int wave)
        {
            int last = WavesInStage(stage);
            if (stage == 1) return 0;
            if (wave == last) return 1;
            if (stage >= 5 && wave == last - 1) return 1;
            return 0;
        }

        // multipliers on top of the level scaling
        public const float EliteHealth = 2.8f, EliteDamage = 1.3f, EliteSize = 1.3f, EliteCost = 3f;
        // mini-bosses and bosses use fixed bases (not their archetype's numbers) so every stage's
        // big fights weigh the same at the same level
        public const float MiniBaseHealth = 220f, MiniBaseDamage = 11f, MiniDamage = 1.35f, MiniSize = 1.8f, MiniCost = 10f;
        public const float BossBaseHealth = 380f, BossBaseDamage = 13f, BossDamage = 1f;

        /// <summary>
        /// Share of the adds a boss brings along: its summons, the reinforcements at each phase change
        /// and the portal trickle. The first boss drowned stage 1 in monsters, so every boss now fields
        /// this share — one factor for all of them, so later bosses still build on the earlier ones.
        /// </summary>
        public const float BossAdds = 0.5f;

        /// <summary>Duo: monster health, boss health and wave budget next to a solo run.</summary>
        public const float DuoHealth = 1.4f, DuoBossHealth = 1.6f, DuoBudget = 1.25f;

        /// <summary>Monsters per summon (and per phase change) at boss phase 0..2 — was 2 / 3 / 4.</summary>
        public static int BossSummons(int phase) => Mathf.Max(1, Mathf.RoundToInt((2 + phase) * BossAdds));

        /// <summary>Seconds between two escorts out of the portals (the trickle thins by the same factor).</summary>
        public static float BossEscortInterval(int phase) => Mathf.Lerp(7f, 4.5f, phase / 2f) / BossAdds;

        /// <summary>No escort comes while this many monsters (boss included) are already up.</summary>
        public static int BossEscortCap => Mathf.CeilToInt(5 * BossAdds);
    }
}
