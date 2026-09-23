using UnityEngine;

namespace SoccerFight
{
    /// <summary>Persistent level, crystal price and permanent stat growth of one character.</summary>
    public static class CharacterProgression
    {
        public const int MaxLevel = 10;

        static string Key(CharacterDef c) => "character:" + c.Id;

        public static int Level(CharacterDef c)
            => c != null && Profile.OwnsCharacter(c.Id) ? Mathf.Clamp(Profile.Level(Key(c)), 1, MaxLevel) : 0;

        /// <summary>Price for the next level. The larger gains near level 10 deliberately cost much more.</summary>
        public static int UpgradeCost(int currentLevel)
        {
            int n = Mathf.Clamp(currentLevel, 1, MaxLevel - 1);
            return Mathf.RoundToInt(6f + 4f * n + 1.5f * n * n);
        }

        public static bool TryUpgrade(CharacterDef c)
        {
            int level = Level(c);
            if (level <= 0 || level >= MaxLevel) return false;
            var price = new Price(Currencies.Gems, UpgradeCost(level));
            if (!Wallet.TrySpend(price)) return false;
            Profile.SetLevel(Key(c), level + 1);
            Profile.Save();
            return true;
        }

        /// <summary>Every level improves offence, defence and abilities; level 10 is roughly +36 % power.</summary>
        public static void Apply(PlayerStats s, CharacterDef c)
        {
            int steps = Mathf.Max(0, Level(c) - 1);
            if (steps == 0) return;
            float power = 0.04f * steps;
            s.DamageMul += power;
            s.MaxHpBonus += 8f * steps;
            s.DamageTaken *= Mathf.Max(0.72f, 1f - 0.025f * steps);
            s.CooldownMul *= Mathf.Pow(0.98f, steps);
            s.AreaMul += 0.025f * steps;
            s.MoveSpeedMul += 0.01f * steps;
        }
    }

    public sealed class ChallengeDef
    {
        public int Number, RequiredCharacterLevel, Stages;
        public float DifficultyOffset, CrystalMultiplier;
        public string Name;
    }

    /// <summary>The five finite level routes. Selection is saved globally; access depends on the current character.</summary>
    public static class ChallengeLevels
    {
        const string SelectionKey = "selected_challenge";

        public static readonly ChallengeDef[] All =
        {
            new ChallengeDef { Number = 1, Name = "ERSTE SCHRITTE", RequiredCharacterLevel = 1, Stages = 3, DifficultyOffset = 0f, CrystalMultiplier = 1f },
            new ChallengeDef { Number = 2, Name = "WILDE HAINE", RequiredCharacterLevel = 3, Stages = 4, DifficultyOffset = 3f, CrystalMultiplier = 1.5f },
            new ChallengeDef { Number = 3, Name = "STURMFRONT", RequiredCharacterLevel = 5, Stages = 5, DifficultyOffset = 7f, CrystalMultiplier = 2.1f },
            new ChallengeDef { Number = 4, Name = "GLUTPROBE", RequiredCharacterLevel = 7, Stages = 6, DifficultyOffset = 12f, CrystalMultiplier = 2.9f },
            new ChallengeDef { Number = 5, Name = "FINALE EKLIPSE", RequiredCharacterLevel = 9, Stages = 8, DifficultyOffset = 18f, CrystalMultiplier = 4f },
        };

        public static int Selected
        {
            get
            {
                int saved = Profile.Level(SelectionKey);
                return Mathf.Clamp(saved == 0 ? 1 : saved, 1, All.Length);
            }
        }

        public static ChallengeDef Current => All[Selected - 1];
        public static bool IsUnlocked(ChallengeDef challenge, CharacterDef character)
            => CharacterProgression.Level(character) >= challenge.RequiredCharacterLevel;

        public static void Select(int number)
        {
            Profile.SetLevel(SelectionKey, Mathf.Clamp(number, 1, All.Length));
            Profile.Save();
        }

        public static int WaveCrystals(ChallengeDef challenge, int stage)
            => Mathf.Max(1, Mathf.RoundToInt((1f + 0.35f * (stage - 1)) * challenge.CrystalMultiplier));

        public static int StageCrystals(ChallengeDef challenge, int stage)
            => Mathf.Max(2, Mathf.RoundToInt((3f + 1.5f * stage) * challenge.CrystalMultiplier));
    }
}
