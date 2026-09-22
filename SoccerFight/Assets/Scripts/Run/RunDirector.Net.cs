using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Duo. The host's director runs the run as always and reports each step. The partner's director
    /// follows: it plays the same banners, heals, stage changes and reward screens when the host
    /// says so, and only keeps the little timers that are pure presentation. After every reward
    /// chain both directors wait for each other before the run moves on.
    /// </summary>
    public sealed partial class RunDirector
    {
        /// <summary>Partner's screen: monsters left in the wave, as the host counts them.</summary>
        public int NetRemaining;

        /// <summary>The end of this player's reward picks: solo the run moves on, in a duo it waits for both.</summary>
        void FinishRewards(bool boss)
        {
            if (Coop.Active) { Coop.LocalRewardsDone(boss); return; }
            AfterBothRewards(boss);
        }

        /// <summary>Both players have picked: next wave (or the boss), or the next stage after a boss.</summary>
        public void AfterBothRewards(bool boss)
        {
            if (Coop.IsClient) return;
            if (boss) NextStage();
            else StartWave(run.Wave + 1);   // past the last regular wave this becomes the boss
        }

        void UpdateFollower(float dt)
        {
            t += dt;
            if (P != Phase.RunOver && P != Phase.Idle) run.Time += dt;
            switch (P)
            {
                case Phase.WaveIntro:
                    if (t > 1.5f) { Enter(Phase.Fighting); StageMechanics.I.SetRunning(true); }
                    break;
                case Phase.BossIntro:
                    if (t > 2.6f) { Enter(Phase.Fighting); StageMechanics.I.SetRunning(true); }
                    break;
                case Phase.StageCleared:
                    if (!stageHealed && t > 0.8f)
                    {
                        stageHealed = true;
                        player.Heal(player.MaxHp * 0.5f, true);
                    }
                    break;
            }
        }

        public void NetStage(int stage)
        {
            run.Stage = stage;
            BeginStage(false);
        }

        public void NetWave(int wave)
        {
            run.Wave = wave;
            plan.Clear();
            planIndex = 0;
            bossPending = false;
            if (run.IsBossWave)
            {
                var boss = run.Theme.Boss;
                Game.I.Hud.ShowBossIntro(boss.Name, boss.Title);
                Game.I.Cam.AddTrauma(0.2f);
                Enter(Phase.BossIntro);
                return;
            }
            Game.I.Hud.ShowWaveBanner(run.Wave, run.WavesInStage);
            Enter(Phase.WaveIntro);
        }

        public void NetWaveDone() => WaveDone();

        public void NetBossDown(Vector2 at)
        {
            lastBossPos = at;
            if (P != Phase.Fighting) Enter(Phase.Fighting);
            BossDefeated();
        }

        public void NetRewards(bool boss)
        {
            if (!boss) { OpenReward(false); return; }
            if (run.UpgradeDue) OpenReward(true);
            else AfterReward(true);
        }
    }
}
