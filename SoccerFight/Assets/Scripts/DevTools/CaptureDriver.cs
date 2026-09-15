using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace SoccerFight
{
    /// <summary>
    /// Development tool: when Unity is started with "-sfCapture &lt;name&gt; -sfOut &lt;dir&gt;", this plays a
    /// scripted scenario at a fixed 60 fps timestep and writes screenshots plus animation contact
    /// sheets to disk. Used to review visuals and animation without a human at the keyboard.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class CaptureDriver : MonoBehaviour
    {
        public static bool Finished { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Finished = false; }

        public static bool IsRequested() => Arg("-sfCapture") != null;

        static string Arg(string name)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return null;
        }

        const int W = 1920, H = 1080, Cell = 420;

        string outDir;
        RenderTexture rt, cellRt;
        Texture2D frame;
        string pendingShot;
        bool pendingCell;
        Texture2D sheet;
        int sheetCols, sheetRows, sheetIndex;

        Game G => Game.I;
        Player P => Game.I.Player;
        bool running;
        bool juggleBot;

        // Started from Update (not Start) so the scenario restarts cleanly after a script reload.
        void Update()
        {
            if (Game.I == null || Game.I.Player == null) return;
            // keep-up bot: taps so the press lands on the contact frame (runs after Game.Update,
            // so the edge is consumed by the next gameplay step)
            if (juggleBot && P.CurrentAction == Player.Action.Juggle && !P.JuggleDropped && P.JuggleTimeToContact - 1f / 60f <= 0.008f)
                GameInput.JugglePressed = true;
            if (running || Finished) return;
            running = true;
            StartCoroutine(Main());
        }

        IEnumerator Main()
        {
            outDir = Arg("-sfOut") ?? Path.Combine(Application.dataPath, "../Captures");
            Directory.CreateDirectory(outDir);
            Time.captureDeltaTime = 1f / 60f;
            GameInput.Scripted = true;
            rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cellRt = new RenderTexture(Cell, Cell, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            frame = new Texture2D(W, H, TextureFormat.RGBA32, false);
            G.Cam.Cam.targetTexture = rt;
            G.Waves.Enabled = false;
            Debug.Log("[Capture] started → " + outDir);

            string scenario = Arg("-sfCapture");
            if (scenario != "run" && scenario != "quick" && scenario != "sim" && scenario != "themes" && scenario != "dev")
            {
                // the older scenarios show every move: skip the run intro and unlock everything
                G.Director.DebugJump(1, 1, 0, false);
                foreach (var a in Abilities.Unlockable) G.Run.Unlock(a);
                P.ApplyStats(true);
            }
            yield return Frames(5);
            if (scenario == "quick") yield return Quick();
            else if (scenario == "run") yield return RunTour();
            else if (scenario == "sim") yield return Simulate();
            else if (scenario == "themes") { G.Waves.Enabled = true; G.Restart(); yield return ThemeTour(); }
            else if (scenario == "dev") yield return DevTour();
            else if (scenario == "moves") yield return Moves();
            else if (scenario == "portrait") yield return Portrait();
            else if (scenario == "platforms") yield return Platforms();
            else if (scenario == "skills") yield return Skills();
            else if (scenario == "bestiary") yield return Bestiary();
            else if (scenario == "layouts") yield return Layouts();
            else if (scenario == "blackhole") yield return BlackHole();
            else yield return All();

            Debug.Log("[Capture] finished");
            Time.captureDeltaTime = 0f;
            Finished = true;
        }

        // ------------------------------------------------------------------ scenario helpers

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        static IEnumerator Seconds(float s) => Frames(Mathf.RoundToInt(s * 60f));

        void Move(float x) => GameInput.MoveX = x;
        void Aim(Vector2 offsetFromPlayer) => GameInput.AimWorld = P.Pos + offsetFromPlayer;

        IEnumerator Shot(string name) { pendingShot = name; yield return null; }

        void BeginSheet(int cols, int rows)
        {
            sheetCols = cols; sheetRows = rows; sheetIndex = 0;
            sheet = new Texture2D(cols * Cell, rows * Cell, TextureFormat.RGBA32, false);
            var fill = new Color32[sheet.width * sheet.height];
            for (int i = 0; i < fill.Length; i++) fill[i] = new Color32(10, 14, 22, 255);
            sheet.SetPixels32(fill);
            G.Hud.SetVisible(false);
        }

        IEnumerator SheetCell(Vector2 offset, float size)
        {
            G.Cam.SetOverride(P.Pos + offset, size);
            pendingCell = true;
            yield return null;
        }

        void EndSheet(string name)
        {
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(outDir, name + ".png"), sheet.EncodeToPNG());
            Destroy(sheet);
            G.Cam.ClearOverride();
            G.Hud.SetVisible(true);
            Debug.Log("[Capture] sheet " + name);
        }

        // ------------------------------------------------------------------ scenarios

        IEnumerator Quick()
        {
            Aim(new Vector2(5f, 1.5f));
            yield return Seconds(1.5f);
            yield return Shot("quick_idle");
        }

        /// <summary>A bot that keeps shooting the nearest monster (and stays alive for the pictures).</summary>
        IEnumerator AutoFight(float seconds)
        {
            int frames = Mathf.RoundToInt(seconds * 60f);
            for (int f = 0; f < frames; f++)
            {
                P.Hp = P.MaxHp;
                var m = Combat.NearestTo(P.Pos + Vector2.up, 40f);
                if (m != null)
                {
                    GameInput.AimWorld = m.Center;
                    float dx = m.Center.x - P.Pos.x;
                    Move(Mathf.Abs(dx) > 6f ? Mathf.Sign(dx) : Mathf.Abs(dx) < 2f ? -Mathf.Sign(dx) : 0f);
                    if (f % 18 == 0) GameInput.ShootPressed = true;
                    if (f % 150 == 75) GameInput.PowerPressed = true;
                }
                yield return null;
            }
            Move(0f);
        }

        void SpawnSpec(EnemyType type, Vector2 at, Rank rank, int affixes, string name)
        {
            var run = G.Run;
            G.Waves.Spawn(new Monster.SpawnSpec { Type = type, At = at, Level = run.Level, Rank = rank, Affixes = affixes, Theme = run.Theme, Name = name });
        }

        /// <summary>Developer panel and the live info lines.</summary>
        IEnumerator DevTour()
        {
            G.Waves.Enabled = true;
            G.Restart();
            Aim(new Vector2(5f, 1.5f));
            yield return Seconds(6f);
            DevMode.ShowInfo = true;
            DevMode.God = true;
            G.Dev.Open();
            yield return Seconds(0.8f);
            yield return Shot("d01_panel");
            G.Dev.Close();
            G.Director.DevSpawn(Rank.Elite);
            yield return Seconds(2f);
            yield return Shot("d02_info");
        }

        /// <summary>Every stage theme with a few of its monsters (the player is untouchable for the pictures).</summary>
        IEnumerator ThemeTour()
        {
            // every theme with a few of its monsters
            for (int s = 1; s <= StageThemes.All.Length; s++)
            {
                G.Director.DebugJump(s, 2, 0, false);
                FxSystem.I.Clear();
                var th = G.Run.Theme;
                P.Pos = new Vector2(-2f, 0f); P.Vel = Vector2.zero;
                G.Ball.ResetTo(P.Pos + new Vector2(0.5f, Art.BallRadius));
                G.Cam.Snap(P.Pos);
                SpawnSpec(th.Roster[0].Type, new Vector2(3.5f, 0.5f), Rank.Normal, 0, th.Roster[0].Name);
                SpawnSpec(th.Roster[1].Type, new Vector2(5.5f, 2.2f), Rank.Normal, 0, th.Roster[1].Name);
                SpawnSpec(th.Roster[2].Type, new Vector2(-6f, 0.5f), Rank.Elite, 1, th.Roster[2].Name);
                if (s >= 3) G.Mechanics.SetRunning(true);
                for (int i = 0; i < 230; i++) { P.Hp = P.MaxHp; P.DodgeTime = 0.5f; yield return null; }
                yield return Shot("t" + s + "_" + th.Name.ToLowerInvariant());
                G.Mechanics.SetRunning(false);
            }

        }

        /// <summary>The roguelite loop: stage card, a wave, reward cards, ability pick, a boss, all eight themes, the run summary.</summary>
        IEnumerator RunTour()
        {
            G.Waves.Enabled = true;
            G.Restart();
            Aim(new Vector2(5f, 1.5f));
            yield return Seconds(1.1f);
            yield return Shot("r01_stage_card");

            // mid-run: stage 2, wave 3 with a small build
            G.Director.DebugJump(2, 3, 5, true);
            yield return Seconds(0.9f);
            yield return Shot("r02_wave_banner");
            yield return AutoFight(6f);
            yield return Shot("r03_fight");
            var theme = G.Run.Theme;
            SpawnSpec(EnemyType.Splitter, P.Pos + new Vector2(5f, 0.5f), Rank.Elite, 2, theme.Roster[1].Name);
            SpawnSpec(EnemyType.Brute, P.Pos + new Vector2(-6f, 0.5f), Rank.MiniBoss, 1, theme.MiniBossName);
            yield return Seconds(1.2f);
            yield return Shot("r04_elites");

            // wave reward → pick the middle card
            G.Director.DebugOpenReward(false);
            yield return Seconds(1.3f);
            yield return Shot("r05_reward");
            G.Rewards.DebugPick(1);
            yield return Seconds(1f);

            // boss reward, then the ability choice opens by itself
            G.Director.DebugOpenReward(true);
            yield return Seconds(1.3f);
            yield return Shot("r06_boss_reward");
            G.Rewards.DebugPick(0);
            yield return Seconds(1.4f);
            yield return Shot("r07_ability_pick");
            G.Rewards.DebugPick(0);
            yield return Seconds(1.4f);
            yield return Shot("r08_next_stage");

            // boss of stage 3
            G.Director.DebugJump(3, Difficulty.WavesInStage(3) + 1, 8, true);
            yield return Seconds(1.3f);
            yield return Shot("r09_boss_intro");
            yield return Seconds(1.6f);
            yield return AutoFight(4f);
            yield return Shot("r10_boss_fight");

            yield return ThemeTour();

            // the run summary
            G.Director.DebugJump(4, 2, 10, false);
            P.Hp = 1f;
            P.InvulnTimer = 0f;
            P.Shield = 0;
            G.Run.Stats.Revives = 0;
            P.TakeDamage(50f, P.Pos + Vector2.right);
            yield return Seconds(2.2f);
            yield return Shot("r11_death");
        }

        /// <summary>
        /// Plays the real run with a simple bot (keeps distance, shoots, uses unlocked skills, juggles
        /// to heal) and always takes the first card. Logs every phase so flow and pacing can be checked.
        /// </summary>
        IEnumerator Simulate()
        {
            G.Waves.Enabled = true;
            G.Restart();
            var d = G.Director;
            var lastPhase = (RunDirector.Phase)(-1);
            int frame = 0, shots = 0, rewardFrames = 0;
            float damageTaken = 0f, lastHp = P.Hp;
            bool bossShot = false, rewardShot = false, stage2Shot = false;
            const int maxFrames = 60 * 60 * 9;
            while (frame++ < maxFrames)
            {
                if (P.Hp < lastHp) damageTaken += lastHp - P.Hp;
                // flow test: the bot can't die (damage taken is logged as the difficulty signal)
                if (!P.Dead && P.Hp < 60f) P.Hp = Mathf.Min(60f, P.MaxHp);
                lastHp = P.Hp;
                if (d.P != lastPhase)
                {
                    lastPhase = d.P;
                    Debug.Log($"[Sim] t={G.Run.Time,6:F1}s stage={G.Run.Stage} wave={G.Run.Wave}/{G.Run.WavesInStage} {d.P,-12} hp={P.Hp:F0}/{P.MaxHp:F0} kills={G.Run.Kills} dmgTaken={damageTaken:F0} L={G.Run.Level:F2} build={string.Join(",", G.Run.PickOrder)} abil={string.Join(",", G.Run.Unlocked)}");
                }
                if (P.Dead) { Debug.Log($"[Sim] DIED at stage {G.Run.Stage} wave {G.Run.Wave} after {G.Run.Time:F0}s"); yield return Seconds(1.5f); yield return Shot("s9_death"); break; }
                if (G.Run.Stage >= 5) { Debug.Log("[Sim] reached stage 5"); break; }

                if (G.Rewards.IsOpen)
                {
                    if (++rewardFrames % 70 == 50)
                    {
                        if (!rewardShot) { rewardShot = true; yield return Shot("s2_reward"); }
                        G.Rewards.DebugPick(0);
                    }
                    yield return null;
                    continue;
                }
                rewardFrames = 0;

                var m = Combat.NearestTo(P.Pos + Vector2.up, 40f);
                if (m != null && d.Fighting)
                {
                    GameInput.AimWorld = m.Center;
                    float dx = m.Center.x - P.Pos.x, adx = Mathf.Abs(dx);
                    Move(adx > 7f ? Mathf.Sign(dx) : adx < 3f ? -Mathf.Sign(dx) : 0f);
                    if (Mathf.Abs(P.Pos.x) > Player.ArenaHalf - 1f) Move(-Mathf.Sign(P.Pos.x));
                    if (frame % 16 == 0) { GameInput.ShootPressed = true; shots++; }
                    if (frame % 200 == 100) GameInput.PowerPressed = true;
                    if (adx < 1.8f && G.Run.Has(Ability.StepOver) && P.StepOverCd <= 0f) GameInput.StepOverPressed = true;
                    else if (adx < 2.2f && P.Grounded && frame % 30 == 0) GameInput.JumpPressed = true;
                    // a human would hop over a charging heavyweight
                    if (m.Rank >= Rank.MiniBoss && adx < 4.5f && Mathf.Abs(m.Vel.x) > 6f && Mathf.Sign(m.Vel.x) == -Mathf.Sign(dx) && P.Grounded) { GameInput.JumpPressed = true; GameInput.JumpHeld = true; }
                    else if (P.Grounded) GameInput.JumpHeld = false;
                    if (G.Run.Has(Ability.Flick) && P.FlickCd <= 0f && frame % 45 == 0) GameInput.FlickPressed = true;
                    if (!P.Grounded && G.Run.Has(Ability.Bicycle) && P.BicycleCd <= 0f) GameInput.BicyclePressed = true;
                    if (G.Run.IsBossWave && !bossShot && G.Waves.Boss != null && d.PhaseTime > 4f) { bossShot = true; yield return Shot("s3_boss"); }
                    if (G.Run.Stage == 2 && G.Run.Wave == 2 && !stage2Shot && d.PhaseTime > 6f) { stage2Shot = true; yield return Shot("s4_stage2"); }
                }
                else Move(0f);
                if (frame == 60 * 20) yield return Shot("s1_first_wave");
                yield return null;
            }
            Move(0f);
            Debug.Log($"[Sim] end: stage={G.Run.Stage} wave={G.Run.Wave} time={G.Run.Time:F0}s kills={G.Run.Kills} shots={shots} dmgTaken={damageTaken:F0}");
        }

        IEnumerator WaitBallHome()
        {
            for (int i = 0; i < 240 && !(P.Ball.IsHeldFree && P.Grounded && P.CurrentAction == Player.Action.None); i++) yield return null;
        }

        /// <summary>Quick art check: the player up close and in the scene.</summary>
        IEnumerator Portrait()
        {
            Aim(new Vector2(5f, 1.6f));
            yield return Seconds(1.2f);
            G.Hud.SetVisible(false);
            G.Cam.SetOverride(P.Pos + new Vector2(0.15f, 0.95f), 1.1f);
            yield return Frames(3);
            yield return Shot("p01_portrait");
            G.Cam.ClearOverride();
            yield return Frames(3);
            yield return Shot("p02_scene");
        }

        /// <summary>Player art, turning, keep-ups and the air-kick recoil.</summary>
        IEnumerator Moves()
        {
            Aim(new Vector2(5f, 1.6f));
            yield return Seconds(1.2f);

            // A — the kit up close and in the scene
            G.Hud.SetVisible(false);
            G.Cam.SetOverride(P.Pos + new Vector2(0.15f, 0.95f), 1.1f);
            yield return Frames(3);
            yield return Shot("m01_portrait");
            G.Cam.ClearOverride();
            G.Hud.SetVisible(true);
            yield return Frames(3);
            yield return Shot("m01b_scene");

            // B — full-speed turn: skid, flip, carry through
            Move(1f);
            yield return Seconds(0.9f);
            Move(-1f);
            BeginSheet(6, 2);
            for (int i = 0; i < 12; i++) { yield return SheetCell(new Vector2(0f, 0.9f), 1.3f); yield return Frames(1); }
            EndSheet("m02_turn_sheet");
            Move(0f);
            yield return Seconds(0.8f);
            yield return WaitBallHome();

            // C — keep-ups with a bot that taps on the beat
            juggleBot = true;
            GameInput.JugglePressed = true;
            yield return Frames(2);
            BeginSheet(6, 3);
            for (int i = 0; i < 18; i++) { yield return SheetCell(new Vector2(0.1f, 1.3f), 1.9f); yield return Frames(4); }
            EndSheet("m03_juggle_sheet");
            for (int i = 0; i < 300 && !(P.JuggleTimeToContact > 0.1f && P.JuggleTimeToContact < 0.13f); i++) yield return null;
            yield return Shot("m04_juggle_approach");
            yield return Seconds(1.6f);
            yield return Shot("m05_juggle_count");

            // D — tapping early swings through air and the ball drops
            for (int i = 0; i < 300 && !(P.JuggleTimeToContact > 0.35f && P.JuggleTimeToContact < 0.45f); i++) yield return null;
            juggleBot = false;
            GameInput.JugglePressed = true;
            yield return Frames(10);
            yield return Shot("m06_juggle_early");
            yield return Seconds(1f);
            yield return WaitBallHome();

            // E — air kick straight down at the apex: the recoil is a second jump
            GameInput.JumpPressed = true;
            GameInput.JumpHeld = true;
            yield return Seconds(0.32f);
            GameInput.JumpHeld = false;
            Aim(new Vector2(0.3f, -4f));
            GameInput.ShootPressed = true;
            BeginSheet(6, 2);
            for (int i = 0; i < 12; i++) { yield return SheetCell(new Vector2(0f, 1.4f), 2.4f); yield return Frames(2); }
            EndSheet("m07_airboost_sheet");
            yield return Seconds(1.2f);
            yield return WaitBallHome();

            // F — sideways air kick: dash the other way
            GameInput.JumpPressed = true;
            GameInput.JumpHeld = true;
            yield return Seconds(0.25f);
            Aim(new Vector2(6f, 0.3f));
            GameInput.ShootPressed = true;
            yield return Frames(12);
            GameInput.JumpHeld = false;
            yield return Shot("m08_airdash");
            yield return Seconds(1f);
        }

        void PlaceOn(int platform, float offset = 0f)
        {
            var p = Level.Platforms[platform];
            P.Pos = new Vector2(p.Center + offset, p.Y);
            P.Vel = Vector2.zero;
            P.Grounded = true;
            P.OnPlatform = platform;
            G.Ball.ResetTo(P.Pos + new Vector2(0.5f * P.Facing, Art.BallRadius));
            G.Cam.Snap(P.Pos);
        }

        void LogPlayer(string what) =>
            Debug.Log($"[Capture] {what}: grounded={P.Grounded} platform={P.OnPlatform} pos=({P.Pos.x:F2}, {P.Pos.y:F2})");

        /// <summary>Depth layers, platforms, blobs leaping after the player, dropping through, flicking from above.</summary>
        IEnumerator Platforms()
        {
            Aim(new Vector2(5f, 1.6f));
            yield return Seconds(1.2f);
            G.Hud.SetVisible(false);
            yield return Shot("pl01_scene");

            // the whole arena at once, then close-ups of each platform type
            G.Cam.SetOverride(new Vector2(0f, 3.6f), 8.8f);
            yield return Frames(3);
            yield return Shot("pl02_overview");
            var rock = Level.Platforms[3];
            G.Cam.SetOverride(new Vector2(rock.Center, rock.Y - 0.4f), 2.1f);
            yield return Frames(3);
            yield return Shot("pl03_rock");
            var terrace = Level.Platforms[0];
            G.Cam.SetOverride(new Vector2(terrace.Center, terrace.Y - 0.7f), 2.6f);
            yield return Frames(3);
            yield return Shot("pl04_terrace");
            var capital = Level.Platforms[4];
            G.Cam.SetOverride(new Vector2(capital.Center, capital.Y - 0.8f), 2.1f);
            yield return Frames(3);
            yield return Shot("pl05_capital");
            // the far layers, zoomed: peaks + castle, forest + stadium, aqueduct
            G.Cam.SetOverride(new Vector2(4f, 5.2f), 2.6f);
            yield return Frames(3);
            yield return Shot("pl05b_far_detail");
            G.Cam.SetOverride(new Vector2(-6.5f, 3.4f), 3f);
            yield return Frames(3);
            yield return Shot("pl05c_mid_detail");
            G.Cam.ClearOverride();
            G.Hud.SetVisible(true);
            yield return Frames(3);

            // a real jump onto the capital to the left of the spawn
            Move(-1f);
            GameInput.JumpPressed = true;
            GameInput.JumpHeld = true;
            yield return Seconds(0.22f);
            Move(0f);
            yield return Seconds(0.3f);
            GameInput.JumpHeld = false;
            yield return Seconds(0.8f);
            LogPlayer("jump onto capital");
            yield return Shot("pl06_on_capital");

            // up on a floating rock: the camera frames the level, blobs leap up after the player
            PlaceOn(3);
            yield return Seconds(1f);
            yield return Shot("pl07_high");
            var b1 = G.Waves.SpawnAt(Monster.Kind.Blob, new Vector2(rock.X0 - 2.2f, 0f));
            var b2 = G.Waves.SpawnAt(Monster.Kind.Blob, new Vector2(rock.X1 + 1.6f, 0f));
            G.Waves.SpawnAt(Monster.Kind.Wisp, new Vector2(rock.X1 + 2.5f, 6f));
            for (int i = 0; i < 6; i++)
            {
                yield return Seconds(0.5f);
                Debug.Log($"[Capture] blobs t={0.5f * (i + 1):F1}s: ({b1.Pos.x:F2}, {b1.Pos.y:F2}) ({b2.Pos.x:F2}, {b2.Pos.y:F2})");
                if (i == 2) yield return Shot("pl08_blobs_climb");
            }
            yield return Shot("pl09_blobs_up");

            // drop back down through the rock
            G.Waves.Restart(999f);
            yield return Frames(2);
            PlaceOn(3);
            yield return Seconds(0.4f);
            GameInput.DownPressed = true;
            GameInput.DownHeld = true;
            yield return Seconds(0.3f);
            GameInput.DownHeld = false;
            yield return Seconds(0.9f);
            LogPlayer("after drop-through");
            yield return Shot("pl10_dropped");

            // rainbow flick from the terrace down onto the pitch
            PlaceOn(0, 1.2f);
            yield return Seconds(0.8f);
            Aim(new Vector2(6f, -1f));
            GameInput.FlickPressed = true;
            for (int i = 0; i < 200 && G.Ball.St != Ball.State.Rainbow; i++) yield return null;
            yield return Frames(14);
            yield return Shot("pl11_flick_from_terrace");
            for (int i = 0; i < 200 && G.Ball.St == Ball.State.Rainbow; i++) yield return null;
            yield return Frames(2);
            yield return Shot("pl12_flick_impact");
            yield return Seconds(1.2f);

            // parallax: the same layers seen from both ends of the arena and from up high
            G.Hud.SetVisible(false);
            G.Cam.SetOverride(new Vector2(-9.5f, 3f), 4.9f);
            yield return Frames(3);
            yield return Shot("pl13_left");
            G.Cam.SetOverride(new Vector2(9.5f, 3f), 4.9f);
            yield return Frames(3);
            yield return Shot("pl14_right");
            G.Cam.SetOverride(new Vector2(9.5f, 4.8f), 5.2f);
            yield return Frames(3);
            yield return Shot("pl15_right_high");
            G.Cam.ClearOverride();
            G.Hud.SetVisible(true);
        }

        /// <summary>Power shot through a line of monsters, step-over dash through them, bicycle-kick blast.</summary>
        IEnumerator Skills()
        {
            Aim(new Vector2(5f, 0.8f));
            yield return Seconds(1f);

            // A — power shot pierces three blobs
            var p1 = G.Waves.SpawnAt(Monster.Kind.Blob, P.Pos + new Vector2(3f, 0f));
            var p2 = G.Waves.SpawnAt(Monster.Kind.Blob, P.Pos + new Vector2(4.4f, 0f));
            var p3 = G.Waves.SpawnAt(Monster.Kind.Blob, P.Pos + new Vector2(5.8f, 0f));
            yield return Frames(12);
            Aim(new Vector2(9f, 0.35f));
            GameInput.PowerPressed = true;
            BeginSheet(6, 2);
            for (int i = 0; i < 12; i++) { yield return SheetCell(new Vector2(0.4f, 0.9f), 1.3f); yield return Frames(1); }
            EndSheet("s01_power_sheet");
            yield return Shot("s02_power_pierce");
            Debug.Log($"[Capture] power shot hp: {p1.Hp:0} {p2.Hp:0} {p3.Hp:0}");
            yield return Seconds(1.2f);

            // B — step-over, then an invulnerable dash through two blobs
            G.Waves.Restart(999f);
            yield return WaitBallHome();
            G.Waves.SpawnAt(Monster.Kind.Blob, P.Pos + new Vector2(2.3f, 0f));
            G.Waves.SpawnAt(Monster.Kind.Blob, P.Pos + new Vector2(3.5f, 0f));
            yield return Frames(6);
            float hpBefore = P.Hp;
            Move(1f);
            GameInput.StepOverPressed = true;
            BeginSheet(6, 3);
            for (int i = 0; i < 18; i++) { yield return SheetCell(new Vector2(0.6f, 0.9f), 1.6f); yield return Frames(1); }
            EndSheet("s03_stepover_sheet");
            Move(0f);
            Debug.Log($"[Capture] step-over hp: {hpBefore:0} -> {P.Hp:0}");
            yield return Shot("s04_stepover_after");
            yield return Seconds(0.8f);

            // C — bicycle kick at the top of a jump; the ball blows up on the blobs ahead. Start on open
            // pitch (between the low capitals, under the high rock) so it lands on the grass.
            G.Waves.Restart(999f);
            P.Pos = new Vector2(-2.2f, 0f);
            P.Vel = Vector2.zero;
            P.Facing = 1;
            G.Ball.ResetTo(P.Pos + new Vector2(0.5f, Art.BallRadius));
            yield return Frames(20);
            var k1 = G.Waves.SpawnAt(Monster.Kind.Blob, P.Pos + new Vector2(3.3f, 0f));
            var k2 = G.Waves.SpawnAt(Monster.Kind.Blob, P.Pos + new Vector2(4.3f, 0f));
            yield return Frames(6);
            GameInput.JumpPressed = true;
            GameInput.JumpHeld = true;
            yield return Seconds(0.24f);
            Aim(new Vector2(3.6f, -2.8f));
            GameInput.BicyclePressed = true;
            BeginSheet(6, 3);
            for (int i = 0; i < 18; i++) { yield return SheetCell(new Vector2(0f, 1.5f), 1.9f); yield return Frames(1); }
            EndSheet("s05_bicycle_sheet");
            GameInput.JumpHeld = false;
            for (int i = 0; i < 120 && G.Ball.St == Ball.State.Blast; i++) yield return null;
            yield return Frames(3);
            yield return Shot("s06_bicycle_blast");
            Debug.Log($"[Capture] bicycle blast at {G.Ball.Pos}, blob hp: {k1.Hp:0} {k2.Hp:0}");
            yield return Seconds(1.2f);
            yield return Shot("s07_hud");

            // D — settings page with all ten bindings
            G.Pause.Open();
            yield return Frames(40);
            G.Pause.OpenSettings();
            yield return Frames(40);
            yield return Shot("s08_settings");
            G.Pause.Close();
            yield return Frames(20);
        }

        void PlaceAway()
        {
            P.Pos = new Vector2(-14.5f, 0f);
            P.Vel = Vector2.zero;
            P.DodgeTime = 999f;
            G.Ball.ResetTo(P.Pos + new Vector2(0.5f, Art.BallRadius));
        }

        void SpawnBoss(StageTheme th, Vector2 at)
        {
            var b = th.Boss;
            G.Waves.Spawn(new Monster.SpawnSpec
            {
                Type = b.Body == Monster.Kind.Wisp ? EnemyType.Lantern : EnemyType.Brute, At = at, Level = G.Run.Level,
                Rank = Rank.Boss, Theme = th, Name = b.Name, Boss = b,
            });
        }

        /// <summary>Every monster body: the nine archetypes in four stage colour schemes, then the eight bosses.</summary>
        IEnumerator Bestiary()
        {
            G.Hud.SetVisible(false);
            Monster.Hold = true;
            EnemyType[] ground = { EnemyType.Hopper, EnemyType.Spawnling, EnemyType.Splitter, EnemyType.Spitter, EnemyType.Brute, EnemyType.Bomber };
            EnemyType[] air = { EnemyType.Diver, EnemyType.Shade, EnemyType.Lantern };
            float[] gx = { -8.2f, -5.5f, -3.7f, -1f, 1.7f, 5.1f };
            foreach (int stage in new[] { 1, 4, 5, 6 })
            {
                G.Director.DebugJump(stage, 1, 0, false);
                yield return Frames(2);
                FxSystem.I.Clear();
                PlaceAway();
                for (int i = 0; i < ground.Length; i++) SpawnSpec(ground[i], new Vector2(gx[i], 0f), Rank.Normal, 0, ground[i].ToString());
                for (int i = 0; i < air.Length; i++) SpawnSpec(air[i], new Vector2(-5f + i * 5f, 3.6f), Rank.Normal, 0, air[i].ToString());
                yield return Seconds(1.2f);
                G.Cam.SetOverride(new Vector2(0f, 2.2f), 5f);
                yield return Frames(3);
                yield return Shot("b" + stage + "_lineup");
                G.Cam.SetOverride(new Vector2(-6.85f, 0.6f), 1.5f); yield return Frames(3); yield return Shot("b" + stage + "_close_a");
                G.Cam.SetOverride(new Vector2(-2.35f, 0.65f), 1.5f); yield return Frames(3); yield return Shot("b" + stage + "_close_b");
                G.Cam.SetOverride(new Vector2(3.4f, 0.85f), 1.75f); yield return Frames(3); yield return Shot("b" + stage + "_close_c");
                G.Cam.SetOverride(new Vector2(0f, 3.6f), 3.2f); yield return Frames(3); yield return Shot("b" + stage + "_close_air");
                G.Cam.ClearOverride();
                G.Waves.Restart();
            }

            // the eight bosses, each in its own stage's colours
            G.Director.DebugJump(1, 1, 0, false);
            yield return Frames(2);
            FxSystem.I.Clear();
            PlaceAway();
            var blobs = new System.Collections.Generic.List<StageTheme>();
            var wisps = new System.Collections.Generic.List<StageTheme>();
            foreach (var th in StageThemes.All) (th.Boss.Body == Monster.Kind.Wisp ? wisps : blobs).Add(th);
            var at = new System.Collections.Generic.List<Vector2>();
            for (int i = 0; i < blobs.Count; i++) { var p = new Vector2(-8f + i * 4f, 0f); SpawnBoss(blobs[i], p); at.Add(p + new Vector2(0f, 1f)); }
            for (int i = 0; i < wisps.Count; i++) { var p = new Vector2(-6f + i * 6f, 5.4f); SpawnBoss(wisps[i], p); at.Add(p); }
            yield return Seconds(1.6f);
            G.Cam.SetOverride(new Vector2(0f, 3.1f), 6.3f);
            yield return Frames(3);
            yield return Shot("b9_bosses");
            for (int i = 0; i < at.Count; i++)
            {
                G.Cam.SetOverride(at[i], 2.1f);
                yield return Frames(3);
                yield return Shot("b9_boss_" + i);
            }
            G.Cam.ClearOverride();
            Monster.Hold = false;
            G.Waves.Restart();
            G.Hud.SetVisible(true);
        }

        static string Describe(Level.Platform p) =>
            $"{p.Kind}@({p.BaseX0:F1}..{p.BaseX1:F1}, {p.BaseY:F2}){(p.Moving ? " " + p.Move + " ±" + p.Amp.ToString("F1") : "")}";

        /// <summary>Every stage's generated arena from afar and one detail each, then a ride on a gliding platform.</summary>
        IEnumerator Layouts()
        {
            G.Hud.SetVisible(false);
            for (int stage = 1; stage <= 8; stage++)
            {
                G.Director.DebugJump(stage, 1, 0, false);
                FxSystem.I.Clear();
                P.Pos = new Vector2(0f, 0f); P.Vel = Vector2.zero;
                G.Ball.ResetTo(P.Pos + new Vector2(0.5f, Art.BallRadius));
                yield return Seconds(0.6f);
                var plats = Level.Platforms;
                Debug.Log($"[Capture] stage {stage} layout ({plats.Length}): " + string.Join(" | ", System.Array.ConvertAll(plats, Describe)));
                G.Cam.SetOverride(new Vector2(0f, 3.4f), 8.6f);
                yield return Frames(3);
                yield return Shot("l" + stage + "_overview");
                Level.Platform pick = plats.Length > 0 ? plats[0] : null;
                foreach (var p in plats) if (p.Moving || (p.Kind != Level.Style.Terrace && p.Kind != Level.Style.Capital && p.Kind != Level.Style.Rock)) { pick = p; break; }
                if (pick != null)
                {
                    G.Cam.SetOverride(new Vector2(pick.Center, pick.Y - 0.35f), 2.4f);
                    yield return Frames(3);
                    yield return Shot("l" + stage + "_detail");
                }
                G.Cam.ClearOverride();
            }

            // ride a gliding platform: the player must stay put on it
            int idx = -1;
            for (int tries = 0; tries < 20 && idx < 0; tries++)
            {
                G.Director.DebugJump(2 + tries % 7, 1, 0, false);
                var plats = Level.Platforms;
                for (int i = 0; i < plats.Length; i++) if (plats[i].Move == Level.Motion.Horizontal) { idx = i; break; }
            }
            if (idx >= 0)
            {
                var p = Level.Platforms[idx];
                PlaceOn(idx);
                yield return Seconds(0.3f);
                float rel0 = P.Pos.x - p.Center, off0 = p.Offset.x;
                G.Hud.SetVisible(true);
                yield return Seconds(2.4f);
                LogPlayer("riding");
                Debug.Log($"[Capture] ride: platform moved {p.Offset.x - off0:F2}, player offset on it {rel0:F2} -> {P.Pos.x - p.Center:F2}, grounded={P.Grounded} on={P.OnPlatform} ({Describe(p)})");
                yield return Shot("l9_riding");
            }
            else Debug.Log("[Capture] ride: no gliding platform found");
            G.Hud.SetVisible(true);
        }

        /// <summary>Singularity: the black hole must open where the cursor was when the power shot left.</summary>
        IEnumerator BlackHole()
        {
            var sing = UpgradeDb.All.Find(u => u.Id == "singularity");
            G.Run.Take(sing, P);
            P.ApplyStats(false);
            P.Pos = new Vector2(-4f, 0f); P.Vel = Vector2.zero; P.Facing = 1;
            G.Ball.ResetTo(P.Pos + new Vector2(0.5f, Art.BallRadius));
            yield return Seconds(0.8f);
            Vector2[] targets = { new Vector2(3f, 2.8f), new Vector2(6.5f, 1.2f), new Vector2(1.5f, 5.2f) };
            for (int k = 0; k < targets.Length; k++)
            {
                Vector2 target = targets[k];
                G.Waves.SpawnAt(Monster.Kind.Blob, target + new Vector2(1.2f, -target.y));
                G.Waves.SpawnAt(Monster.Kind.Wisp, target + new Vector2(-1f, 0.6f));
                yield return WaitBallHome();
                P.PowerCd = 0f;
                GameInput.AimWorld = target;
                int before = Vortices.I.Spawned;
                GameInput.PowerPressed = true;
                // the cursor wanders off right after the shot: the hole must still open at the aim point
                for (int i = 0; i < 120 && Vortices.I.Spawned == before; i++) { if (i > 30) GameInput.AimWorld = P.Pos + new Vector2(-3f, 1f); yield return null; }
                Debug.Log($"[Capture] singularity {k}: aimed at {target} opened at {Vortices.I.LastPos} (spawned={Vortices.I.Spawned > before})");
                yield return Frames(20);
                yield return Shot("h" + k + "_singularity");
                yield return Seconds(2.6f);
                G.Waves.Restart();
            }
        }

        IEnumerator All()
        {
            // 1 — idle (foot rests on the ball after a moment)
            Aim(new Vector2(5f, 1.6f));
            yield return Seconds(1.8f);
            yield return Shot("01_idle");

            // 1b — background detail close-ups (plants, ruins, trees)
            G.Hud.SetVisible(false);
            G.Cam.SetOverride(P.Pos + new Vector2(4.5f, 1.6f), 2.3f);
            yield return Frames(3);
            yield return Shot("01b_detail_near");
            G.Cam.SetOverride(P.Pos + new Vector2(-3.5f, 3.6f), 3.2f);
            yield return Frames(3);
            yield return Shot("01c_detail_ruins");
            G.Cam.ClearOverride();
            G.Hud.SetVisible(true);
            yield return Frames(3);

            // 2 — run cycle close-up
            Move(1f);
            yield return Seconds(0.9f);
            BeginSheet(6, 2);
            for (int i = 0; i < 12; i++) { Aim(new Vector2(5f, 1.6f)); yield return SheetCell(new Vector2(0.35f, 0.85f), 1.2f); yield return Frames(1); }
            EndSheet("02_run_sheet");
            // grass bending around the running player's legs
            BeginSheet(4, 2);
            for (int i = 0; i < 8; i++) { yield return SheetCell(new Vector2(0.1f, 0.45f), 0.8f); yield return Frames(3); }
            EndSheet("02b_grass_push");
            yield return Frames(10);
            yield return Shot("03_run_full");

            // 3 — stop, then kick close-up
            Move(0f);
            yield return Seconds(1.0f);
            Aim(new Vector2(7f, 1.4f));
            GameInput.ShootPressed = true;
            BeginSheet(6, 2);
            for (int i = 0; i < 12; i++) { yield return SheetCell(new Vector2(0.3f, 0.8f), 1.25f); yield return Frames(1); }
            EndSheet("04_kick_sheet");

            // 4 — shot in flight (full view)
            yield return Seconds(1.2f);
            Aim(new Vector2(8f, 2.6f));
            GameInput.ShootPressed = true;
            yield return Frames(14);
            yield return Shot("05_shot_full");
            yield return Seconds(1.4f);

            // 5 — rainbow flick close-up
            Aim(new Vector2(6f, 0.5f));
            GameInput.FlickPressed = true;
            BeginSheet(6, 3);
            for (int i = 0; i < 18; i++) { yield return SheetCell(new Vector2(0.1f, 1.0f), 1.45f); yield return Frames(2); }
            EndSheet("06_flick_sheet");

            // 6 — rainbow arc mid-air and the impact (full view)
            for (int i = 0; i < 200 && G.Ball.St != Ball.State.Rainbow; i++) yield return null;
            yield return Frames(12);
            yield return Shot("07_flick_arc");
            for (int i = 0; i < 200 && G.Ball.St == Ball.State.Rainbow; i++) yield return null;
            yield return Frames(2);
            yield return Shot("08_flick_impact");
            yield return Seconds(1.5f);

            // 7 — combat: monsters, hit reactions, damage numbers, HUD cooldowns
            G.Waves.SpawnAt(Monster.Kind.Blob, P.Pos + new Vector2(4.5f, 0f));
            G.Waves.SpawnAt(Monster.Kind.Wisp, P.Pos + new Vector2(3.2f, 2.6f));
            G.Waves.SpawnAt(Monster.Kind.Blob, P.Pos + new Vector2(-4.8f, 0f));
            yield return Seconds(0.9f);
            Aim(new Vector2(4.5f, 0.45f));
            GameInput.ShootPressed = true;
            yield return Frames(15);
            yield return Shot("09_combat_hit");
            yield return Seconds(0.5f);
            yield return Shot("10_combat_hud");

            // 8 — let a monster reach the player to see the damage feedback
            yield return Seconds(2.5f);
            yield return Shot("11_after_damage");

            // 9 — jump close-up
            G.Waves.Restart(999f);
            yield return Seconds(1.2f);
            GameInput.JumpPressed = true;
            GameInput.JumpHeld = true;
            BeginSheet(6, 2);
            for (int i = 0; i < 12; i++) { yield return SheetCell(new Vector2(0.2f, 1.6f), 1.9f); yield return Frames(3); }
            GameInput.JumpHeld = false;
            EndSheet("12_jump_sheet");

            // 10 — regular wave start with banner
            G.Waves.Enabled = true;
            G.Waves.Restart(0.2f);
            yield return Seconds(0.75f);
            yield return Shot("13_wave_banner");
            yield return Seconds(2.6f);
            yield return Shot("14_wave_fight");

            // 11 — pause menu and settings
            G.Pause.Open();
            yield return Frames(60);
            yield return Shot("15_pause");
            G.Pause.OpenSettings();
            yield return Frames(60);
            yield return Shot("16_settings");
            G.Pause.Close();
            yield return Frames(30);
        }

        // ------------------------------------------------------------------ rendering

        void Render()
        {
            Canvas.ForceUpdateCanvases();
            var req = new RenderPipeline.StandardRequest { destination = rt };
            RenderPipeline.SubmitRenderRequest(G.Cam.Cam, req);
        }

        void LateUpdate()
        {
            if (!running || rt == null || Game.I == null || Game.I.Cam == null) return;
            if (pendingShot != null)
            {
                Render();
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                frame.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                frame.Apply();
                RenderTexture.active = prev;
                File.WriteAllBytes(Path.Combine(outDir, pendingShot + ".png"), frame.EncodeToPNG());
                Debug.Log("[Capture] shot " + pendingShot);
                pendingShot = null;
            }

            if (pendingCell && sheet != null)
            {
                Render();
                float sx = (float)H / W;
                Graphics.Blit(rt, cellRt, new Vector2(sx, 1f), new Vector2((1f - sx) * 0.5f, 0f));
                var prev = RenderTexture.active;
                RenderTexture.active = cellRt;
                int col = sheetIndex % sheetCols, row = sheetIndex / sheetCols;
                if (row < sheetRows) sheet.ReadPixels(new Rect(0, 0, Cell, Cell), col * Cell, (sheetRows - 1 - row) * Cell);
                RenderTexture.active = prev;
                sheetIndex++;
                pendingCell = false;
            }
        }
    }
}
