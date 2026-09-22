namespace SoccerFight
{
    /// <summary>
    /// Every number an upgrade can touch. Rebuilt from scratch (base values + all owned upgrades)
    /// whenever the build changes, so stacking order never matters and nothing drifts.
    /// </summary>
    public sealed class PlayerStats
    {
        // ---- offence
        public float DamageMul, ShotDamageMul, PowerDamageMul, FlickDamageMul, BlastDamageMul;
        public float ShotCooldownMul, CooldownMul, BallSpeedMul, KnockbackMul, AreaMul;
        public float CritChance, CritMul, SharpshooterBonus;
        public float RunUpBonus, PowerChargeMul;

        // projectiles and chain reactions
        public int EchoBalls;
        public float EchoDamageFrac, EchoSpread;
        public bool EchoRicochet;
        public int Ricochets;
        public int ChainTargets, ChainJumps;
        public float ChainFrac;
        public bool ChainCanCrit, ChainIgnites;
        public float BurnFrac, BurnTime, BurnBoost;
        public bool BurnSpread;
        public float SlowAmount, SlowTime, FrostVuln;
        public bool FreezeOnThird, ThermalShock;
        public float KillExplodeFrac, KillExplodeRadius;
        public bool Cannoneer;
        public int NovaEvery;
        public bool BloodFrenzy, GoldenBoot, TwinSun, Singularity, Trident, EchoFlip, CycloneStep;
        public bool Boomerang, DoubleRainbow, BulletTime, Perpetual, Maestro;

        // ---- mobility
        public float MoveSpeedMul, JumpMul, DashDistanceMul, DashDamageFrac;
        public int AirBoosts;
        public float AdrenalineTime;
        public bool Slippery;          // ice stage
        public float GravityMul;       // astral stage

        // ---- defence
        public float MaxHpBonus, Armor, RegenPerSec, HealOnWave, LifeOnKill, InvulnBonus;
        public int ShieldCharges;
        public float ShieldRecharge;
        public float CounterStomp;
        public int Revives;

        // ---- utility
        public float ReturnSpeedMul, CatchRadiusMul, JuggleHealMul, JuggleWindowMul, Luck;
        public float FlickRadiusMul, BlastRadiusMul;

        // ---- the later moves
        public bool TackleWave;        // Bodenwelle: shockwave at the end of a slide
        public float TackleHeal;       // Blutgrätsche: heal per swept monster
        public int PuntExtra;          // Hagel: extra meteors
        public bool PuntFire;          // Brandsatz: the crater keeps burning
        public float WallLifeBonus;    // Stabile Mauer
        public bool WallBounce;        // Abpraller: the ball leaves the wall faster and harder
        public bool NutmegSpread;      // Demütigung: the mark jumps to a neighbour
        public float NutmegRefund;     // Straßenfußball: every nutmeg shaves cooldowns
        public float WhistleBonus;     // Nachspielzeit
        public bool RedCard;           // Rote Karte: one normal monster is sent off
        public int DecoyCount;         // Doppelgänger: extra decoys
        public bool DecoyBlast;        // Ablenkungsmanöver: the decoy bursts hard

        // ---- the header (defender only)
        public float HeaderStunBonus;  // Kopfnuss
        public int HeaderPierce;       // Flugkopfball: monsters the header passes through before it bounces

        // ---- class traits and character perks (MetaPassives)
        /// <summary>Damage multiplier per SkillCategory, on top of DamageMul.</summary>
        public readonly float[] CategoryDamage = new float[System.Enum.GetValues(typeof(SkillCategory)).Length];
        /// <summary>Speed at which trick moves play out (1 = normal).</summary>
        public float TechniqueHaste;
        /// <summary>Extra run speed (fraction) for RushTime seconds after a trick.</summary>
        public float RushSpeed, RushTime;
        /// <summary>Multiplier on every hit the player takes (after armour).</summary>
        public float DamageTaken;
        /// <summary>Boosted shots land with the striker's impact star.</summary>
        public bool ShotImpactFx;

        public float CategoryMul(SkillCategory c) => CategoryDamage[(int)c];

        public void Reset()
        {
            DamageMul = ShotDamageMul = PowerDamageMul = FlickDamageMul = BlastDamageMul = 1f;
            ShotCooldownMul = CooldownMul = BallSpeedMul = KnockbackMul = AreaMul = 1f;
            CritChance = 0.05f; CritMul = 1.75f; SharpshooterBonus = 0f;
            RunUpBonus = 0f; PowerChargeMul = 1f;

            EchoBalls = 0; EchoDamageFrac = 0.55f; EchoSpread = 9f; EchoRicochet = false;
            Ricochets = 0;
            ChainTargets = 0; ChainJumps = 1; ChainFrac = 0.35f; ChainCanCrit = false; ChainIgnites = false;
            BurnFrac = 0f; BurnTime = 3f; BurnBoost = 0f; BurnSpread = false;
            SlowAmount = 0f; SlowTime = 2f; FrostVuln = 0f; FreezeOnThird = false; ThermalShock = false;
            KillExplodeFrac = 0f; KillExplodeRadius = 1.6f;
            Cannoneer = false; NovaEvery = 0;
            BloodFrenzy = GoldenBoot = TwinSun = Singularity = Trident = EchoFlip = CycloneStep = false;
            Boomerang = DoubleRainbow = BulletTime = Perpetual = Maestro = false;

            MoveSpeedMul = JumpMul = DashDistanceMul = 1f; DashDamageFrac = 0f;
            AirBoosts = 1; AdrenalineTime = 0f; Slippery = false; GravityMul = 1f;

            MaxHpBonus = 0f; Armor = 0f; RegenPerSec = 0f; HealOnWave = 0f; LifeOnKill = 0f; InvulnBonus = 0f;
            ShieldCharges = 0; ShieldRecharge = 16f; CounterStomp = 0f; Revives = 0;

            ReturnSpeedMul = CatchRadiusMul = JuggleHealMul = JuggleWindowMul = 1f; Luck = 0f;
            FlickRadiusMul = BlastRadiusMul = 1f;

            TackleWave = false; TackleHeal = 0f; PuntExtra = 0; PuntFire = false;
            WallLifeBonus = 0f; WallBounce = false; NutmegSpread = false; NutmegRefund = 0f;
            WhistleBonus = 0f; RedCard = false; DecoyCount = 0; DecoyBlast = false;
            HeaderStunBonus = 0f; HeaderPierce = 0;

            for (int i = 0; i < CategoryDamage.Length; i++) CategoryDamage[i] = 1f;
            TechniqueHaste = 1f;
            RushSpeed = RushTime = 0f;
            DamageTaken = 1f;
            ShotImpactFx = false;
        }
    }
}
