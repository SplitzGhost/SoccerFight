using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// The run's state machine: stage intro → waves (budget-planned spawns, elites, mini-bosses) →
    /// an upgrade choice after every second round (waves and boss fights count) → boss → ability
    /// choice → next stage in a new theme with a new platform layout. Reward screens freeze the
    /// game; everything else plays out live.
    /// </summary>
    public sealed partial class RunDirector
    {
        public enum Phase { Idle, StageIntro, WaveIntro, Fighting, WaveCleared, Reward, AbilityPick, BossIntro, StageCleared, RunOver }
        /// <summary>How many skills a boss offers to pick one from.</summary>
        const int AbilityChoices = 2;

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
        public int Remaining => Coop.IsClient ? NetRemaining : (plan.Count - planIndex) + (bossPending ? 1 : 0) + waves.AliveCount;
        public int WaveTotal { get; private set; }
        public bool Fighting => P == Phase.Fighting;
        public int PlanIndex => planIndex;
        public int PlanCount => plan.Count;

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
        Vector2 lastBossPos;

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
            // a duo run plays both arenas from the host's seed
            if (Coop.S != null && Coop.S.InRun) run.Seed = Coop.S.RunSeed;
            CoinRewards.ResetRun();
            Combat.Reset();
            player.ApplyStats(true);
            waves.Restart();
            BeginStage(true);
        }

        /// <summary>Parks the run: no waves, no timers. The world the title screen plays in front of.</summary>
        public void Idle()
        {
            plan.Clear();
            planIndex = 0;
            bossPending = bossSeen = false;
            Enter(Phase.Idle);
        }

        void BeginStage(bool instant)
        {
            run.Wave = 0;
            run.Rebuild();                         // stage mechanic stats (ice, low gravity)
            player.ApplyStats(false);
            // usually the arena was already rebuilt behind the ability choice; if not, now
            if (StageArt.Apply(run.Stage, run.Seed) && !instant) Game.I.Environment.Platforms.Flourish();
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
            // duo: a downed player comes back after a while; the run only ends when both are down
            if (Coop.Active) { Coop.OnLocalDown(); return; }
            EndRun();
        }

        /// <summary>The run is over (solo death, or both duo players down).</summary>
        public void EndRun()
        {
            if (P == Phase.RunOver) return;
            StageMechanics.I.SetRunning(false);
            int reached = run.Stage;
            if (!DevMode.UsedThisRun) RunState.RecordStage(reached);
            Profile.OnRunFinished();
            Enter(Phase.RunOver);
        }

        public void OnMonsterDied(Monster m)
        {
            if (m.Rank == Rank.Boss) lastBossPos = m.Center;
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
            float budget = Difficulty.Budget(level) * (Coop.IsHost ? Difficulty.DuoBudget : 1f);
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
            if (s.Rank == Rank.MiniBoss) { Game.I.Hud.ShowToast(s.Name + "  NAHT"); Coop.SendToast(s.Name + "  NAHT"); }
        }

        // ------------------------------------------------------------------ update

        public void Update(float dt)
        {
            if (Coop.IsClient) { UpdateFollower(dt); return; }
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
                    if (t > 1.5f)
                    {
                        if (run.UpgradeDue) { Coop.SendRewards(false); OpenReward(false); }
                        else StartWave(run.Wave + 1);   // past the last regular wave this becomes the boss
                    }
                    break;

                case Phase.BossIntro:
                    if (bossPending && t > 0.9f) SpawnBoss();
                    if (t > 2.6f) { Enter(Phase.Fighting); StageMechanics.I.SetRunning(true); escortTimer = 6f / Difficulty.BossAdds; }
                    break;

                case Phase.StageCleared:
                    if (!stageHealed && t > 0.8f)
                    {
                        stageHealed = true;
                        float heal = player.MaxHp * 0.5f;
                        player.Heal(heal, true);
                    }
                    if (t > 2.8f)
                    {
                        Coop.SendRewards(true);
                        if (run.UpgradeDue) OpenReward(true);
                        else AfterReward(true);
                    }
                    break;
            }
        }

        void StartWave(int wave)
        {
            run.Wave = wave;
            Coop.SendWave(wave);
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
            Coop.SendWaveDone();
            StageMechanics.I.SetRunning(false);
            EnemyProjectiles.I.Clear();
            var s = run.Stats;
            float heal = player.MaxHp * 0.15f + s.HealOnWave;
            player.Heal(heal, true);
            run.RoundsCleared++;
            Game.I.Hud.OnWaveCleared(run.Wave, run.WavesInStage, run.UpgradeDue);
            Profile.SaveIfDirty();
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
            lastBossPos = at;
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
                    escortTimer = Difficulty.BossEscortInterval(boss.BossPhase);
                    if (waves.AliveCount < Difficulty.BossEscortCap) waves.SpawnFromPortal(escortPool[Random.Range(0, escortPool.Count)], run.Level - 1f);
                }
            }
            if (bossSeen && waves.Boss == null && P == Phase.Fighting) BossDefeated();
        }

        void BossDefeated()
        {
            if (P != Phase.Fighting) return;
            Coop.SendBossDown(lastBossPos);
            StageMechanics.I.SetRunning(false);
            EnemyProjectiles.I.Clear();
            // the boss's court dissolves with it
            foreach (var m in waves.Monsters)
            {
                if (!m.Alive) continue;
                FxSystem.I.Burst(m.Center, Color.white, run.Theme.Glow, 0.7f);
                m.Deactivate();
            }
            if (!DevMode.UsedThisRun) RunState.RecordStage(run.Stage + 1);
            run.RoundsCleared++;
            Game.I.Hud.OnStageCleared(run.Stage, run.Theme);
            // the stage pays out: a shower of coins where the boss fell
            CoinDrops.I?.Shower(lastBossPos, CoinRewards.ForStageClear(run.Stage));
            Profile.SaveIfDirty();
            // start drawing the next arena and its monsters now (threads) or behind the reward screens (WebGL)
            StageArt.Prepare(run.Stage + 1, run.Seed);
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
                if (bonus) FinishRewards(true);
                else AfterReward(boss);
            }, bonus ? RebuildArena : (System.Action)null);
        }

        /// <summary>The next stage's arena appears while a reward screen covers the pitch.</summary>
        void RebuildArena() => StageArt.Apply(run.Stage + 1, run.Seed);

        void AfterReward(bool boss)
        {
            if (!boss)
            {
                FinishRewards(false);
                return;
            }
            var locked = run.LockedAbilities();
            if (locked.Count == 0)
            {
                // every ability owned: a second boss reward takes its place
                OpenReward(true, true);
                return;
            }
            // two random skills; the one not taken goes back into the pool
            var choice = new List<Ability>();
            while (choice.Count < Mathf.Min(AbilityChoices, locked.Count))
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
                FinishRewards(true);
            }, RebuildArena);
        }

        void NextStage()
        {
            run.Stage++;
            Coop.SendStage(run.Stage);
            BeginStage(false);
        }

        // ------------------------------------------------------------------ developer mode

        /// <summary>Removes everything on the pitch without kill credit (no splitters, no kill effects).</summary>
        void ClearPitch()
        {
            foreach (var m in waves.Monsters)
            {
                if (!m.Alive) continue;
                FxSystem.I.Burst(m.Center, Color.white, run.Theme.Glow, 0.6f);
                m.Deactivate();
            }
            EnemyProjectiles.I.Clear();
        }

        /// <summary>Jump to a stage, keeping the build and the unlocked abilities.</summary>
        public void DevGoToStage(int stage)
        {
            DevMode.MarkRun();
            StageMechanics.I.SetRunning(false);
            ClearPitch();
            plan.Clear(); planIndex = 0; bossPending = false;
            run.Stage = Mathf.Max(1, stage);
            BeginStage(false);
        }

        /// <summary>End the current wave as if it had been cleared (the boss wave counts as won).</summary>
        public void DevSkipWave()
        {
            DevMode.MarkRun();
            switch (P)
            {
                case Phase.StageIntro:
                case Phase.WaveIntro:
                    t = 99f;   // fast-forward the intro
                    return;
                case Phase.BossIntro:
                    bossPending = false;
                    bossSeen = true;
                    ClearPitch();
                    Enter(Phase.Fighting);
                    BossDefeated();
                    return;
                case Phase.Fighting:
                    plan.Clear(); planIndex = 0;
                    if (run.IsBossWave) { bossPending = false; ClearPitch(); BossDefeated(); }
                    else ClearPitch();   // UpdateWave sees an empty pitch and finishes the wave
                    return;
            }
        }

        /// <summary>Straight to this stage's boss.</summary>
        public void DevCallBoss()
        {
            DevMode.MarkRun();
            if (P == Phase.Reward || P == Phase.AbilityPick || P == Phase.RunOver) return;
            StageMechanics.I.SetRunning(false);
            ClearPitch();
            StartWave(run.WavesInStage + 1);
        }

        /// <summary>Defeat everything on the pitch (kill effects and splitters included).</summary>
        public void DevKillAll()
        {
            DevMode.MarkRun();
            for (int pass = 0; pass < 3; pass++)
            {
                bool any = false;
                var list = waves.Monsters;
                for (int i = 0; i < list.Count; i++)
                {
                    var m = list[i];
                    if (!m.Alive || m.Rank == Rank.Boss && P != Phase.Fighting) continue;
                    any = true;
                    Combat.Hit(m, m.Hp + 1f, Vector2.up, 3f, Src.Hazard, big: true);
                }
                if (!any) break;
            }
        }

        /// <summary>A monster of the current stage out of the next portal.</summary>
        public void DevSpawn(Rank rank)
        {
            DevMode.MarkRun();
            var theme = run.Theme;
            int wave = Mathf.Max(1, run.IsBossWave ? run.WavesInStage : run.Wave);
            var options = new List<RosterEntry>();
            foreach (var e in theme.Roster) if (e.FromWave <= wave) options.Add(e);
            if (options.Count == 0) options.Add(theme.Roster[0]);
            var pick = options[Random.Range(0, options.Count)];
            int affixes = Difficulty.EliteAffixes(run.Stage);
            if (rank == Rank.MiniBoss)
            {
                waves.SpawnFromPortal(MiniBossType(theme), run.Level, Rank.MiniBoss, Mathf.Max(1, affixes - 1), theme.MiniBossName);
                Game.I.Hud.ShowToast(theme.MiniBossName + "  NAHT");
            }
            else waves.SpawnFromPortal(pick.Type, run.Level, rank, rank == Rank.Elite ? affixes : 0, pick.Name);
        }

        /// <summary>Every skill that isn't in a slot yet (developer tools).</summary>
        List<Ability> DevPool() => run.LockedAbilities();

        void DevAfterPick()
        {
            player.ApplyStats(false);
            Game.I.Hud.OnUpgradeTaken(null);
        }

        /// <summary>Offer cards without touching the wave flow (the fight resumes after the pick).</summary>
        public void DevOfferCards(bool boss)
        {
            DevMode.MarkRun();
            var offer = UpgradeRoller.Offer(run, 3, boss);
            rewards.ShowUpgrades(offer, boss, run, u => { run.Take(u, player); player.ApplyStats(false); Game.I.Hud.OnUpgradeTaken(u); });
        }

        public void DevOfferAbility()
        {
            DevMode.MarkRun();
            var locked = DevPool();
            if (locked.Count == 0) { Game.I.Hud.ShowToast("ALLE VIER PLÄTZE SIND BELEGT"); return; }
            var choice = new List<Ability>();
            while (choice.Count < Mathf.Min(AbilityChoices, locked.Count))
            {
                var a = locked[Random.Range(0, locked.Count)];
                if (!choice.Contains(a)) choice.Add(a);
            }
            rewards.ShowAbilities(choice, run.Stage, a => { run.Unlock(a); player.ApplyStats(false); Game.I.Hud.OnAbilityUnlocked(a); });
        }

        /// <summary>+1 stack of a specific card. Returns false at the stack limit.</summary>
        public bool DevAddUpgrade(UpgradeDef u)
        {
            if (run.Stacks(u.Id) >= u.Max) return false;
            DevMode.MarkRun();
            run.Take(u, player);
            player.ApplyStats(false);
            Game.I.Hud.OnUpgradeTaken(u);
            return true;
        }

        public void DevAddRandomUpgrades(int count)
        {
            DevMode.MarkRun();
            for (int i = 0; i < count; i++)
            {
                var offer = UpgradeRoller.Offer(run, 1, false);
                if (offer.Count > 0) run.Take(offer[0], player);
            }
            DevAfterPick();
        }

        public void DevUnlockAll()
        {
            DevMode.MarkRun();
            foreach (var a in DevPool()) run.Unlock(a);
            player.ApplyStats(false);
            foreach (var a in Abilities.UnlockableFor(run.Sport)) Game.I.Hud.OnAbilityUnlocked(a);
            Game.I.Hud.ShowToast("FÄHIGKEITS-PLÄTZE GEFÜLLT");
        }

        public void DevClearBuild()
        {
            DevMode.MarkRun();
            run.Owned.Clear();
            run.PickOrder.Clear();
            run.Rebuild();
            DevAfterPick();
        }

        /// <summary>Capture/debug: open the reward screens directly.</summary>
        public void DebugOpenReward(bool boss) => OpenReward(boss);
        public void DebugOpenAbilities() => AfterReward(true);

        /// <summary>Capture/debug: jump straight to a stage and wave with a few random upgrades.</summary>
        public void DebugJump(int stage, int wave, int upgrades, bool spawn)
        {
            run.Reset();
            CoinRewards.ResetRun();
            for (int s = 2; s <= stage; s++)
            {
                var locked = DevPool();
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
