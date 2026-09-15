using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// The run's state machine: stage intro → waves (budget-planned spawns, elites, mini-bosses) →
    /// upgrade card after each wave → boss → boss reward → ability choice → next stage in a new theme.
    /// Reward screens freeze the game; everything else plays out live.
    /// </summary>
    public sealed class RunDirector
    {
        public enum Phase { Idle, StageIntro, WaveIntro, Fighting, WaveCleared, Reward, AbilityPick, BossIntro, StageCleared, RunOver }

        struct PlannedSpawn
        {
            public EnemyType Type;
            public Rank Rank;
            public int Affixes;
            public string Name;
        }

        public Phase P { get; private set; } = Phase.Idle;
        public float PhaseTime => t;
        /// <summary>Monsters still to come in this wave plus those alive.</summary>
        public int Remaining => (plan.Count - planIndex) + (bossPending ? 1 : 0) + waves.AliveCount;
        public int WaveTotal { get; private set; }
        public bool Fighting => P == Phase.Fighting;

        readonly List<PlannedSpawn> plan = new List<PlannedSpawn>();
        readonly List<EnemyType> escortPool = new List<EnemyType>();
        RunState run;
        WaveDirector waves;
        Player player;
        RewardScreen rewards;
        float t, spawnTimer, escortTimer;
        int planIndex;
        bool bossPending, bossSeen, rewardDone;
        bool stageHealed;

        public void Build(RunState state, WaveDirector w, Player p, RewardScreen r)
        {
            run = state;
            waves = w;
            player = p;
            rewards = r;
        }

        void Enter(Phase p) { P = p; t = 0f; }

        // ------------------------------------------------------------------ run control

        public void StartRun()
        {
            run.Reset();
            Combat.Reset();
            player.ApplyStats(true);
            waves.Restart();
            BeginStage(true);
        }

        void BeginStage(bool instant)
        {
            run.Wave = 0;
            run.Rerolls = Mathf.Max(run.Rerolls, 1);
            run.Rebuild();                         // stage mechanic stats (ice, low gravity)
            player.ApplyStats(false);
            var theme = run.Theme;
            waves.SetTheme(theme);
            StageMechanics.I.Begin(theme);
            StageMechanics.I.SetRunning(false);
            Game.I.Grade.Transition(theme, instant ? 0f : 2.2f);
            Game.I.Hud.ShowStageCard(run.Stage, theme);
            stageHealed = false;
            Enter(Phase.StageIntro);
        }

        public void OnPlayerDied()
        {
            if (P == Phase.RunOver) return;
            StageMechanics.I.SetRunning(false);
            int reached = run.Stage;
            if (reached > RunState.BestStage) RunState.BestStage = reached;
            Enter(Phase.RunOver);
        }

        public void OnMonsterDied(Monster m)
        {
            if (m.Rank == Rank.Boss && P == Phase.Fighting && run.IsBossWave) BossDefeated();
        }

        // ------------------------------------------------------------------ wave planning

        /// <summary>
        /// Spend the wave's threat budget on the stage roster. Elites cost triple; mini-bosses are
        /// pre-placed at 60 % of the queue so they arrive once the wave is in full swing.
        /// </summary>
        void PlanWave()
        {
            plan.Clear();
            planIndex = 0;
            var theme = run.Theme;
            float level = run.Level;
            float budget = Difficulty.Budget(level);
            float eliteChance = Difficulty.EliteChance(run.Stage, run.Wave, level);
            int affixes = Difficulty.EliteAffixes(run.Stage);

            float totalW = 0f;
            foreach (var e in theme.Roster) if (e.FromWave <= run.Wave) totalW += e.Weight;
            int guard = 0;
            while (budget > 0.2f && guard++ < 200)
            {
                float x = Random.value * totalW;
                RosterEntry pick = theme.Roster[0];
                foreach (var e in theme.Roster)
                {
                    if (e.FromWave > run.Wave) continue;
                    if ((x -= e.Weight) <= 0f) { pick = e; break; }
                }
                float cost = EnemyDef.Get(pick.Type).Cost;
                bool elite = Random.value < eliteChance && budget >= cost * Difficulty.EliteCost;
                if (elite) cost *= Difficulty.EliteCost;
                budget -= cost;
                plan.Add(new PlannedSpawn { Type = pick.Type, Rank = elite ? Rank.Elite : Rank.Normal, Affixes = elite ? affixes : 0, Name = pick.Name });
            }
            // the very first wave of a run never opens with an elite
            if (run.Stage == 1 && run.Wave == 1)
                for (int i = 0; i < plan.Count; i++) { var s = plan[i]; s.Rank = Rank.Normal; s.Affixes = 0; plan[i] = s; }

            int minis = Difficulty.MiniBosses(run.Stage, run.Wave);
            for (int i = 0; i < minis; i++)
            {
                var mb = new PlannedSpawn { Type = MiniBossType(theme), Rank = Rank.MiniBoss, Affixes = Mathf.Max(1, affixes - 1), Name = theme.MiniBossName };
                int at = Mathf.Clamp(Mathf.RoundToInt(plan.Count * (0.55f + 0.2f * i)), 1, plan.Count);
                plan.Insert(at, mb);
            }
            WaveTotal = plan.Count;
            spawnTimer = 0.9f;
        }

        /// <summary>The heaviest archetype the stage fields so far.</summary>
        EnemyType MiniBossType(StageTheme theme)
        {
            EnemyType best = EnemyType.Hopper;
            float cost = -1f;
            foreach (var e in theme.Roster)
            {
                if (e.FromWave > run.Wave || e.Type == EnemyType.Spawnling || e.Type == EnemyType.Bomber) continue;
                float c = EnemyDef.Get(e.Type).Cost;
                if (c > cost) { cost = c; best = e.Type; }
            }
            return best;
        }

        void SpawnNext()
        {
            var s = plan[planIndex++];
            waves.SpawnFromPortal(s.Type, run.Level, s.Rank, s.Affixes, s.Name);
            if (s.Rank == Rank.MiniBoss) Game.I.Hud.ShowToast(s.Name + "  NAHT");
        }

        // ------------------------------------------------------------------ update

        public void Update(float dt)
        {
            t += dt;
            if (P != Phase.RunOver && P != Phase.Idle) run.Time += dt;
            switch (P)
            {
                case Phase.StageIntro:
                    if (t > 3.1f) StartWave(1);
                    break;

                case Phase.WaveIntro:
                    if (t > 1.5f) { Enter(Phase.Fighting); StageMechanics.I.SetRunning(true); }
                    break;

                case Phase.Fighting:
                    if (!waves.Enabled) break;
                    if (run.IsBossWave) UpdateBossFight(dt);
                    else UpdateWave(dt);
                    break;

                case Phase.WaveCleared:
                    if (t > 1.5f) OpenReward(false);
                    break;

                case Phase.BossIntro:
                    if (bossPending && t > 0.9f) SpawnBoss();
                    if (t > 2.6f) { Enter(Phase.Fighting); StageMechanics.I.SetRunning(true); escortTimer = 6f; }
                    break;

                case Phase.StageCleared:
                    if (!stageHealed && t > 0.8f)
                    {
                        stageHealed = true;
                        float heal = player.MaxHp * 0.3f;
                        player.Heal(heal, true);
                    }
                    if (t > 2.8f) OpenReward(true);
                    break;
            }
        }

        void StartWave(int wave)
        {
            run.Wave = wave;
            if (run.IsBossWave)
            {
                bossPending = true;
                bossSeen = false;
                plan.Clear(); planIndex = 0; WaveTotal = 1;
                var boss = run.Theme.Boss;
                escortPool.Clear();
                foreach (var e in run.Theme.Roster) escortPool.Add(e.Type);
                Game.I.Hud.ShowBossIntro(boss.Name, boss.Title);
                Game.I.Cam.AddTrauma(0.2f);
                Enter(Phase.BossIntro);
                return;
            }
            PlanWave();
            Game.I.Hud.ShowWaveBanner(run.Wave, run.WavesInStage);
            Enter(Phase.WaveIntro);
        }

        void UpdateWave(float dt)
        {
            float level = run.Level;
            if (planIndex < plan.Count)
            {
                spawnTimer -= dt;
                // telegraph: the next portal starts glowing before it spits a monster out
                if (spawnTimer < 0.6f) waves.ChargeNextPortal(1f - spawnTimer / 0.6f);
                if (spawnTimer <= 0f)
                {
                    if (waves.AliveCount < Difficulty.MaxAlive(level))
                    {
                        SpawnNext();
                        spawnTimer = Difficulty.SpawnInterval(level) * Random.Range(0.8f, 1.2f);
                        // bursts: sometimes two come out back to back
                        if (Random.value < 0.18f) spawnTimer *= 0.35f;
                    }
                    else spawnTimer = 0.25f;
                }
            }
            else if (waves.AliveCount == 0) WaveDone();
        }

        void WaveDone()
        {
            StageMechanics.I.SetRunning(false);
            EnemyProjectiles.I.Clear();
            var s = run.Stats;
            float heal = player.MaxHp * 0.08f + s.HealOnWave;
            player.Heal(heal, true);
            Game.I.Hud.OnWaveCleared(run.Wave, run.WavesInStage);
            Enter(Phase.WaveCleared);
        }

        void SpawnBoss()
        {
            bossPending = false;
            bossSeen = true;
            var theme = run.Theme;
            var boss = theme.Boss;
            EnemyType type = boss.Body == Monster.Kind.Wisp ? EnemyType.Lantern : EnemyType.Brute;
            float side = player.Pos.x > 0f ? -1f : 1f;
            Vector2 at = new Vector2(side * 7f, boss.Body == Monster.Kind.Wisp ? 5.5f : 8f);
            waves.Spawn(new Monster.SpawnSpec
            {
                Type = type, At = at, Vel = new Vector2(-side * 1.5f, boss.Body == Monster.Kind.Wisp ? 0f : -2f),
                Level = run.Level, Rank = Rank.Boss, Theme = theme, Name = boss.Name, Boss = boss,
            });
            var fx = FxSystem.I;
            fx.Flash(at, 7f, theme.Glow, 0.4f, 2.8f);
            fx.Ring(FxLayer.Front, at, 0.5f, 5f, 0.6f, 0.03f, 0.6f, Color.white, theme.Glow.WithAlpha(0f), 2.6f);
            fx.Sparks(at, Vector2.down, 300f, 30, 4f, 14f, theme.Glow, 2.6f, 0.06f, 0.5f);
            Game.I.Cam.AddTrauma(0.5f);
            Game.I.Post.Impact(0.8f);
        }

        void UpdateBossFight(float dt)
        {
            if (bossPending) return;
            // a light trickle of the stage's regulars keeps the arena busy (the boss summons its own)
            var boss = waves.Boss;
            if (boss != null && escortPool.Count > 0)
            {
                escortTimer -= dt;
                if (escortTimer < 0.6f) waves.ChargeNextPortal(1f - escortTimer / 0.6f);
                if (escortTimer <= 0f)
                {
                    escortTimer = Mathf.Lerp(7f, 4.5f, boss.BossPhase / 2f);
                    if (waves.AliveCount < 5) waves.SpawnFromPortal(escortPool[Random.Range(0, escortPool.Count)], run.Level - 1f);
                }
            }
            if (bossSeen && waves.Boss == null && P == Phase.Fighting) BossDefeated();
        }

        void BossDefeated()
        {
            if (P != Phase.Fighting) return;
            StageMechanics.I.SetRunning(false);
            EnemyProjectiles.I.Clear();
            // the boss's court dissolves with it
            foreach (var m in waves.Monsters)
            {
                if (!m.Alive) continue;
                FxSystem.I.Burst(m.Center, Color.white, run.Theme.Glow, 0.7f);
                m.Deactivate();
            }
            if (run.Stage + 1 > RunState.BestStage) RunState.BestStage = run.Stage + 1;
            Game.I.Hud.OnStageCleared(run.Stage, run.Theme);
            Enter(Phase.StageCleared);
        }

        // ------------------------------------------------------------------ rewards

        void OpenReward(bool boss, bool bonus = false)
        {
            var offer = UpgradeRoller.Offer(run, 3, boss);
            Enter(Phase.Reward);
            rewardDone = false;
            rewards.ShowUpgrades(offer, boss, run, u =>
            {
                if (rewardDone) return;
                rewardDone = true;
                run.Take(u, player);
                player.ApplyStats(false);
                Game.I.Hud.OnUpgradeTaken(u);
                if (bonus) NextStage();
                else AfterReward(boss);
            });
        }

        void AfterReward(bool boss)
        {
            if (!boss)
            {
                StartWave(run.Wave + 1);   // past the last regular wave this becomes the boss
                return;
            }
            var locked = run.LockedAbilities();
            if (locked.Count == 0)
            {
                // every ability owned: a second boss reward takes its place
                OpenReward(true, true);
                return;
            }
            // two random abilities; the one not taken goes back into the pool
            var choice = new List<Ability>();
            while (choice.Count < Mathf.Min(2, locked.Count))
            {
                var a = locked[Random.Range(0, locked.Count)];
                if (!choice.Contains(a)) choice.Add(a);
            }
            Enter(Phase.AbilityPick);
            rewards.ShowAbilities(choice, run.Stage, a =>
            {
                run.Unlock(a);
                player.ApplyStats(false);
                Game.I.Hud.OnAbilityUnlocked(a);
                NextStage();
            });
        }

        void NextStage()
        {
            run.Stage++;
            run.Rerolls++;
            BeginStage(false);
        }

        /// <summary>Capture/debug: open the reward screens directly.</summary>
        public void DebugOpenReward(bool boss) => OpenReward(boss);
        public void DebugOpenAbilities() => AfterReward(true);

        /// <summary>Capture/debug: jump straight to a stage and wave with a few random upgrades.</summary>
        public void DebugJump(int stage, int wave, int upgrades, bool spawn)
        {
            run.Reset();
            for (int s = 2; s <= stage; s++)
            {
                var locked = run.LockedAbilities();
                if (locked.Count > 0) run.Unlock(locked[Random.Range(0, locked.Count)]);
            }
            run.Stage = stage;
            for (int i = 0; i < upgrades; i++)
            {
                var offer = UpgradeRoller.Offer(run, 1, i % 3 == 0);
                if (offer.Count > 0) run.Take(offer[0], player);
            }
            waves.Restart();
            BeginStage(true);
            Game.I.Hud.HideStageCard();
            if (spawn) StartWave(wave);
            else { run.Wave = wave; Enter(Phase.Idle); }
        }
    }
}
