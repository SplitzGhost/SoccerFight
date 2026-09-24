using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Everything a stage looks like beyond the colour grade: its platform layout (with art) and its
    /// monsters' bodies. Prepared ahead of time (in the background, or while a reward screen has the
    /// game frozen) and swapped in while the next stage's ability choice covers the arena.
    /// Stage 1 always gets the same layout; later stages roll theirs from the run's seed. The whole scenery
    /// (backdrop, ground, props) is swapped along with the platforms.
    /// </summary>
    public static class StageArt
    {
        static int preparedStage = -1, preparedSeed = -1, appliedStage = -1, appliedSeed = -1;
        static Level.Platform[] layout;
        static PlatformLook[] looks;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { preparedStage = preparedSeed = appliedStage = appliedSeed = -1; layout = null; looks = null; }

        static int SeedFor(int stage, int runSeed) => stage <= 1 ? 0 : runSeed;

        public static bool IsApplied(int stage, int runSeed) => appliedStage == stage && appliedSeed == SeedFor(stage, runSeed);

        /// <summary>Queue the art for a stage (cheap to call again).</summary>
        public static void Prepare(int stage, int runSeed)
        {
            int seed = SeedFor(stage, runSeed);
            if (preparedStage == stage && preparedSeed == seed) return;
            if (appliedStage == stage && appliedSeed == seed) return;
            if (looks != null && preparedStage >= 0)
            {
                // a different stage was prepared but never shown (developer jumps): drop its art
                ArtQueue.Flush();
                PlatformArt.Release(looks);
            }
            preparedStage = stage;
            preparedSeed = seed;
            var theme = StageThemes.For(stage);
            // jede Stage ordnet ihre eigenen Plattform-Bilder an; Stage 1 immer gleich (fester Seed)
            layout = Level.Generate(StageKit.For(theme), seed * 31 + stage * 7919);
            looks = System.Array.ConvertAll(layout, p => new PlatformLook { P = p });
            MonsterArt.Prepare(theme);
            ArtQueue.Kick();
        }

        /// <summary>Make the stage's layout the live one. Returns true if the arena changed.</summary>
        public static bool Apply(int stage, int runSeed)
        {
            if (IsApplied(stage, runSeed)) return false;
            Prepare(stage, runSeed);
            ArtQueue.Flush();
            appliedStage = preparedStage;
            appliedSeed = preparedSeed;
            preparedStage = preparedSeed = -1;
            Level.Set(layout);
            var theme = StageThemes.For(stage);
            Game.I.Environment.ApplyStage(theme);
            Game.I.Environment.Platforms.Show(looks, StageKit.For(theme));
            MonsterArt.Trim(StageThemes.For(stage), StageThemes.For(Mathf.Max(1, stage - 1)));
            return true;
        }
    }
}
