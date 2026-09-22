using System.Collections;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Duo test: two game instances on one machine (one started with -sfNetHost &lt;port&gt;, the other
    /// with -sfNetJoin &lt;port&gt;) meet through the real title screen — DUO, RAUM ERSTELLEN / code typed
    /// in, BEITRETEN, DUO STARTEN — and then bots play a real run together: waves, reward picks on
    /// both sides, a boss, a downed player coming back after the wait, and the host ending the run.
    /// Both sides log "[Duo]" lines and take screenshots. Runs in real time (no fixed capture step)
    /// so the two games keep the same pace.
    /// </summary>
    public sealed partial class CaptureDriver
    {
        static float Now => Time.realtimeSinceStartup;

        static IEnumerator Real(float seconds)
        {
            float end = Now + seconds;
            while (Now < end) yield return null;
        }

        string role;
        float duoStart;

        void DuoLog(string text) => Debug.Log($"[Duo] {role} t={Now - duoStart,6:F1} {text}");

        string DuoState()
        {
            var s = Coop.S;
            var d = G.Director;
            int alive = 0, ghosts = 0;
            foreach (var m in G.Waves.Monsters) if (m.Alive) { alive++; if (m.Ghost) ghosts++; }
            var r = G.Remote;
            string link = s == null ? "no room" : $"{s.Link.St} sent={s.Link.SentPackets}/{s.Link.SentBytes / 1024}k recv={s.Link.ReceivedPackets}/{s.Link.ReceivedBytes / 1024}k";
            return $"stage={G.Run.Stage} wave={G.Run.Wave} {d.P} remaining={d.Remaining} alive={alive} ghosts={ghosts} " +
                   $"me=({P.Pos.x:F1},{P.Pos.y:F1}) hp={P.Hp:F0}/{P.MaxHp:F0} dead={P.Dead} kills={G.Run.Kills} " +
                   $"partner={(r.Present ? $"({r.P.Pos.x:F1},{r.P.Pos.y:F1}) hp={r.P.Hp:F0} {r.P.CurrentAction}" : "-")} " +
                   $"localDown={s?.LocalDown} remoteDown={s?.RemoteDown} waiting={s?.WaitingForPartner} build={G.Run.PickOrder.Count} skills={G.Run.SkillCount} | {link}";
        }

        IEnumerator Duo()
        {
            Time.captureDeltaTime = 0f;
            bool host = Arg("-sfNetHost") != null || Arg("-sfDuoRole") == "host";
            string codeFile = Arg("-sfCodeFile");
            role = host ? "HOST" : "JOIN";
            duoStart = Now;
            SeedProfile();
            if (!host) Characters.Select(Characters.IndexOf(Characters.Get("mira")));
            G.ToMenu();
            yield return Real(2.5f);

            // ---- through the real menu
            yield return Kick("mode");
            yield return Real(1.2f);
            yield return Shot(role + "_01_duo_page");
            if (host)
            {
                yield return Kick("duoCreate");
                // over the relay the code takes a moment; a test partner reads it from a file
                float wait = Now + 40f;
                while ((Coop.S == null || Coop.S.Link.JoinCode == null) && Now < wait) yield return null;
                DuoLog("room code " + (Coop.S?.Link.JoinCode ?? "(none)") + " " + (Coop.S?.Link.Error ?? ""));
                if (codeFile != null && Coop.S?.Link.JoinCode != null) System.IO.File.WriteAllText(codeFile, Coop.S.Link.JoinCode);
                yield return Real(1.5f);
                yield return Shot("HOST_02_code");
            }
            else
            {
                yield return Real(5f);   // the host opens the room first
                string code = "ABCDEF";
                if (codeFile != null)
                {
                    float wait = Now + 90f;
                    while (!(System.IO.File.Exists(codeFile) && new System.IO.FileInfo(codeFile).Length > 0) && Now < wait) yield return null;
                    if (System.IO.File.Exists(codeFile)) code = System.IO.File.ReadAllText(codeFile).Trim();
                    DuoLog("joining room " + code);
                }
                G.Menu.DebugDuoType(code);
                yield return Real(0.6f);
                yield return Shot("JOIN_02_typed");
                yield return Kick("duoJoin");
            }
            float until = Now + 90f;
            while (!(Coop.S != null && Coop.S.Link.Connected && Coop.S.PartnerHello) && Now < until) yield return null;
            if (Coop.S == null || !Coop.S.Link.Connected) { DuoLog("FAILED to connect: " + (Coop.S?.Link.Error ?? "no room")); yield break; }
            DuoLog("connected, partner " + Coop.S.PartnerName + " (character " + Coop.S.PartnerCharacter + ")");
            yield return Real(2.5f);
            yield return Shot(role + "_03_lobby");
            if (host) yield return Kick("duoStart", false);

            until = Now + 30f;
            while (!Coop.Active && Now < until) yield return null;
            if (!Coop.Active) { DuoLog("FAILED: the run did not start"); yield break; }
            DuoLog("run started, seed " + Coop.S.RunSeed + " / run seed " + G.Run.Seed);
            G.Waves.Enabled = true;

            // ---- the run
            float runStart = Now, nextLog = 0f, nextKill = 12f, rewardT = 0f;
            bool downDone = false, sawPartnerDown = false, rewardShot = false, waitShot = false, bossShot = false;
            int shotIndex = 0;
            var lastPhase = (RunDirector.Phase)(-1);
            int frame = 0;
            while (Now - runStart < 230f)
            {
                frame++;
                float t = Now - runStart;
                if (!Coop.Active) { DuoLog("duo ended: " + (Coop.S?.Notice ?? "") + " | " + DuoState()); break; }
                var d = G.Director;
                if (d.P != lastPhase) { lastPhase = d.P; DuoLog("phase → " + DuoState()); }
                if (t >= nextLog) { nextLog = t + 4f; DuoLog(DuoState()); }
                if (d.P == RunDirector.Phase.RunOver) { DuoLog("RUN OVER"); yield return Real(2f); yield return Shot(role + "_run_over"); break; }

                // keep the bots alive, except for the planned knock-out of the partner
                bool testingDown = !host && t > 70f && !downDone;
                if (!P.Dead && P.Hp < 60f && !testingDown) P.Hp = Mathf.Min(60f, P.MaxHp);
                if (testingDown && !P.Dead)
                {
                    downDone = true;
                    P.Hp = 1f; P.InvulnTimer = 0f; P.DodgeTime = 0f; P.Shield = 0; G.Run.Stats.Revives = 0;
                    P.TakeDamage(80f, P.Pos + Vector2.right);
                    DuoLog("knocked out on purpose → " + DuoState());
                }
                if (!host && downDone && P.Dead && Coop.S.DownLeft < 27f && Coop.S.DownLeft > 26.9f) yield return Shot("JOIN_05_down");
                if (host && Coop.S.RemoteDown && !sawPartnerDown)
                {
                    sawPartnerDown = true;
                    DuoLog("partner is down → " + DuoState());
                    yield return Real(1.5f);
                    yield return Shot("HOST_05_partner_down");
                }

                if (G.Rewards.IsOpen)
                {
                    rewardT += Time.unscaledDeltaTime;
                    if (!rewardShot && rewardT > 1.2f) { rewardShot = true; yield return Shot(role + "_04_reward"); }
                    // the partner takes longer, so the host's "waiting" banner shows
                    if (rewardT > (host ? 1.5f : 5f)) { rewardT = 0f; G.Rewards.DebugPick(0); DuoLog("picked a card → " + DuoState()); }
                    yield return null;
                    continue;
                }
                rewardT = 0f;
                if (host && Coop.S.WaitingForPartner && !waitShot) { waitShot = true; yield return Real(0.8f); yield return Shot("HOST_04b_waiting"); }

                // the fight
                var m = Combat.NearestTo(P.Pos + Vector2.up, 40f);
                if (m != null && d.Fighting && !P.Dead)
                {
                    GameInput.AimWorld = m.Center;
                    float dx = m.Center.x - P.Pos.x, adx = Mathf.Abs(dx);
                    Move(adx > 7f ? Mathf.Sign(dx) : adx < 3f ? -Mathf.Sign(dx) : 0f);
                    if (Mathf.Abs(P.Pos.x) > Player.ArenaHalf - 1f) Move(-Mathf.Sign(P.Pos.x));
                    if (frame % 16 == 0) GameInput.ShootPressed = true;
                    if (frame % 200 == 100) GameInput.PowerPressed = true;
                    if (adx < 2.2f && P.Grounded && frame % 30 == 0) GameInput.JumpPressed = true;
                    for (int i = 0; i < G.Run.SkillCount; i++) if (frame % 240 == 60 * i) GameInput.SkillPressed[i] = true;
                    if (G.Run.IsBossWave && G.Waves.Boss != null && !bossShot && d.PhaseTime > 3f) { bossShot = true; yield return Shot(role + "_06_boss"); }
                }
                else Move(0f);

                // the host speeds the waves up (kills go through the normal death path, so both screens see them)
                if (host && d.Fighting && t > nextKill) { nextKill = t + 9f; G.Director.DevKillAll(); DuoLog("dev: killed the pitch → " + DuoState()); }

                if (shotIndex == 0 && t > 14f) { shotIndex++; yield return Shot(role + "_10_fight"); }
                if (shotIndex == 1 && t > 45f) { shotIndex++; yield return Shot(role + "_11_fight"); }
                if (shotIndex == 2 && t > 110f) { shotIndex++; yield return Shot(role + "_12_fight"); }
                if (shotIndex == 3 && t > 170f) { shotIndex++; yield return Shot(role + "_13_fight"); }
                yield return null;
            }
            Move(0f);
            DuoLog("end of play → " + DuoState());

            // the host ends the run: both go back to the room
            if (host) { G.ToMenu(); DuoLog("host back to menu"); }
            until = Now + 15f;
            while (Now < until && (Coop.S == null || Coop.S.InRun)) yield return null;
            DuoLog("after run: inRun=" + (Coop.S?.InRun) + " connected=" + (Coop.S?.Link.Connected) + " menu=" + G.Menu.IsOpen);
            yield return Real(2.5f);
            yield return Shot(role + "_99_after");
            yield return Real(3f);
            Coop.Leave();
        }
    }
}
