namespace SoccerFight
{
    /// <summary>Behaviour archetypes. Each rides on one of the two bodies (blob or wisp) with its own skin.</summary>
    public enum EnemyType { Hopper, Diver, Splitter, Spawnling, Spitter, Brute, Shade, Lantern, Bomber }

    /// <summary>Elite modifiers, shown as an aura colour and a name tag.</summary>
    public enum EliteAffix { Swift, Armored, Regenerating, Volatile, Frenzied }

    public enum Rank { Normal, Elite, MiniBoss, Boss }

    public struct EnemyDef
    {
        public Monster.Kind Body;
        public float Hp, Damage, Cost, Size;

        public static EnemyDef Get(EnemyType t)
        {
            switch (t)
            {
                case EnemyType.Diver: return new EnemyDef { Body = Monster.Kind.Wisp, Hp = 24f, Damage = 7f, Cost = 1.3f, Size = 1f };
                case EnemyType.Splitter: return new EnemyDef { Body = Monster.Kind.Blob, Hp = 46f, Damage = 9f, Cost = 1.8f, Size = 1.15f };
                case EnemyType.Spawnling: return new EnemyDef { Body = Monster.Kind.Blob, Hp = 12f, Damage = 4f, Cost = 0.4f, Size = 0.6f };
                case EnemyType.Spitter: return new EnemyDef { Body = Monster.Kind.Blob, Hp = 30f, Damage = 7f, Cost = 2f, Size = 0.95f };
                case EnemyType.Brute: return new EnemyDef { Body = Monster.Kind.Blob, Hp = 120f, Damage = 14f, Cost = 4f, Size = 1.6f };
                case EnemyType.Shade: return new EnemyDef { Body = Monster.Kind.Wisp, Hp = 26f, Damage = 8f, Cost = 2f, Size = 1f };
                case EnemyType.Lantern: return new EnemyDef { Body = Monster.Kind.Wisp, Hp = 28f, Damage = 6f, Cost = 2f, Size = 1.05f };
                case EnemyType.Bomber: return new EnemyDef { Body = Monster.Kind.Blob, Hp = 30f, Damage = 11f, Cost = 1.6f, Size = 0.9f };
                default: return new EnemyDef { Body = Monster.Kind.Blob, Hp = 36f, Damage = 8f, Cost = 1f, Size = 1f };
            }
        }

        public static string AffixName(EliteAffix a)
        {
            switch (a)
            {
                case EliteAffix.Swift: return "FLINK";
                case EliteAffix.Armored: return "GEPANZERT";
                case EliteAffix.Regenerating: return "REGENERIEREND";
                case EliteAffix.Volatile: return "INSTABIL";
                default: return "RASEND";
            }
        }

        public static UnityEngine.Color AffixColor(EliteAffix a)
        {
            switch (a)
            {
                case EliteAffix.Swift: return new UnityEngine.Color(0.45f, 1f, 0.8f);
                case EliteAffix.Armored: return new UnityEngine.Color(0.75f, 0.82f, 0.95f);
                case EliteAffix.Regenerating: return new UnityEngine.Color(0.45f, 1f, 0.45f);
                case EliteAffix.Volatile: return new UnityEngine.Color(1f, 0.55f, 0.2f);
                default: return new UnityEngine.Color(1f, 0.3f, 0.35f);
            }
        }
    }

    /// <summary>What a boss can do. Each stage boss picks a handful and cycles through them.</summary>
    public enum BossMove { LeapSlam, Charge, Volley, RadialBurst, Summon, Teleport, LightningCall, GeyserCall }
}
