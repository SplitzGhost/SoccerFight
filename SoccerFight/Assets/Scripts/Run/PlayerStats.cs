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
        }
    }
}
