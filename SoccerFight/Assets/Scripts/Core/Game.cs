using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace SoccerFight
{
    /// <summary>
    /// Entry point. Drop this on an empty GameObject (the Game scene already has one) and press Play:
    /// everything — camera, post-processing, art, world, player, monsters, HUD — is built in code.
    /// A single update loop in a fixed order keeps motion perfectly in sync (no script-order jitter).
    /// </summary>
    public sealed class Game : MonoBehaviour
    {
        public static Game I { get; private set; }

        public CameraRig Cam { get; private set; }
        public PostFx Post { get; private set; }
        public Hud Hud { get; private set; }
        public PauseMenu Pause { get; private set; }
        public MainMenu Menu { get; private set; }
        public WaveDirector Waves { get; private set; }
        public Player Player { get; private set; }
        public Ball Ball { get; private set; }
        /// <summary>The duo partner's body and ball (hidden outside a duo run).</summary>
        public RemotePlayer Remote { get; private set; }
        public WorldEnvironment Environment { get; private set; }
        public bool CaptureMode { get; private set; }

        // roguelite run
        public RunState Run { get; private set; }
        public RunDirector Director { get; private set; }
        public RewardScreen Rewards { get; private set; }
        public ThemeGrade Grade { get; private set; }
        public StageMechanics Mechanics { get; private set; }
        public DevPanel Dev { get; private set; }
        EnemyProjectiles enemyShots;
        Barrier barrier;
        Barrier partnerBarrier;
        Decoys decoys;
        Lightning lightning;
        EchoBalls echoes;
        Vortices vortices;
        Court court;
        TwinSun twinSun;
        BossTells bossTells;
        UpgradeVisuals upgradeLook;
        CoinDrops coins;

        float envTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { I = null; }

        void Awake() => Build();

        /// <summary>
        /// Plain C# systems don't survive a script hot-reload during play mode. If that happens,
        /// tear down the generated hierarchy and build a fresh game instead of spamming errors.
        /// </summary>
        bool RecoverFromReload()
        {
            if (Cam != null && Player != null) return false;
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
            Build();
            return true;
        }

        void Build()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            BuildTimer.Begin();
            I = this;
            CaptureMode = CaptureDriver.IsRequested();

            GameSettings.Load();
            GameSettings.Apply();
            // the art jobs run nested Parallel.For loops: with only the default pool size they would
            // wait for the thread pool to grow, one thread every half second
            if (Par.Threads)
            {
                System.Threading.ThreadPool.GetMinThreads(out int workers, out int io);
                System.Threading.ThreadPool.SetMinThreads(Mathf.Max(workers, System.Environment.ProcessorCount * 3), io);
            }
            TimeFx.ResetAll();
            Combat.Reset();
            // the profile first: the selected character, its class and the loadout shape the run
            if (CaptureMode) Profile.UseTransient();
            else Profile.Load();
            Characters.Load();
            Run = new RunState();
            Run.Reset();

            // The backdrop, the first arena and its monsters generate on worker threads while the main
            // thread builds everything else.
            EnvironmentArt.Begin();
            StageArt.Prepare(1, Run.Seed);
            Art.Build();
            UiArt.Build();
            // the title screen's art starts once the main thread's own heavy drawing is done, so the
            // two never fight over the thread pool; Menu.Build collects it
            MenuScenery.Begin();
            MenuArt.Begin();

            Cam = new CameraRig();
            Cam.Init(transform);
            Post = new PostFx();
            Post.Init(transform);
            EnsureEventSystem();
            BuildTimer.Mark("camera+post");

            EnvironmentArt.End();
            BuildTimer.Mark("wait for backdrop");
            Environment = new WorldEnvironment();
            Environment.Build(transform, Cam);
            StageArt.Apply(1, Run.Seed);
            BuildTimer.Mark("arena");
            Grade = new ThemeGrade();
            Grade.Build(Environment, Cam, transform);
            BuildTimer.Mark("env objects");
            FxSystem.Create(transform);
            lightning = new Lightning();
            lightning.Build(transform);
            Ball = new Ball();
            Ball.Build(transform);
            Player = new Player();
            Player.Build(transform, Ball);
            upgradeLook = new UpgradeVisuals();
            upgradeLook.Build(transform, Player, Ball);
            Waves = new WaveDirector();
            Waves.Build(transform, Environment);
            enemyShots = new EnemyProjectiles();
            enemyShots.Build(transform);
            bossTells = new BossTells();
            bossTells.Build(transform);
            Mechanics = new StageMechanics();
            Mechanics.Build(transform);
            echoes = new EchoBalls();
            echoes.Build(transform);
            vortices = new Vortices();
            vortices.Build(transform);
            court = new Court();
            court.Build(transform);
            barrier = new Barrier();
            barrier.Build(transform);
            partnerBarrier = new Barrier();
            partnerBarrier.Build(transform, true);
            Remote = new RemotePlayer();
            Remote.Build(transform);
            decoys = new Decoys();
            decoys.Build(transform, Player.Rig);
            twinSun = new TwinSun();
            twinSun.Build(transform);
            coins = new CoinDrops();
            coins.Build(transform);
            BuildTimer.Mark("actors");
            Hud = new Hud();
            Hud.Build(transform, Cam.Cam, Player, Waves, CaptureMode);
            Rewards = new RewardScreen();
            Rewards.Build(transform, Cam.Cam, CaptureMode);
            Pause = new PauseMenu();
            Pause.Build(transform, Cam.Cam, CaptureMode);
            Pause.RestartRequested += () => { Pause.Close(); Restart(); };
            Dev = new DevPanel();
            Dev.Build(transform, Cam.Cam, CaptureMode);
            // not while a card choice is open: the dev actions would pull the run out from under it
            Pause.DevRequested += () => { if (Rewards.IsOpen) return; Pause.Close(); Dev.Open(); };
            Director = new RunDirector();
            Director.Build(Run, Waves, Player, Rewards);
            Menu = new MainMenu();
            Menu.Build(transform, Cam.Cam, CaptureMode);
            Menu.PlayRequested += Restart;
            Pause.MenuRequested += () => { Pause.Close(); ToMenu(); };
            BuildTimer.Mark("hud");

            // a capture drives the game itself; a player starts at the title screen
            if (CaptureMode) Restart();
            else ToMenu();

            if (CaptureMode && GetComponent<CaptureDriver>() == null) gameObject.AddComponent<CaptureDriver>();
            BuildTimer.Mark("rest");
            Debug.Log($"[SoccerFight] World generated in {sw.ElapsedMilliseconds} ms  ({BuildTimer.Report()})");
        }

        void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            es.transform.SetParent(transform, false);
        }

        void OnDisable()
        {
            Cursor.visible = true;
            Time.timeScale = 1f;
        }

        // coins that arrived since the last checkpoint are not lost when the game closes
        void OnApplicationQuit() => Profile.SaveIfDirty();
        void OnApplicationPause(bool paused) { if (paused) Profile.SaveIfDirty(); }

        /// <summary>A fresh run from stage 1.</summary>
        public void Restart()
        {
            // in a duo the host starts every run, for both
            if (Coop.IsClient) return;
            if (Coop.S != null && Coop.S.Link.IsHost && Coop.S.Link.Connected && Coop.S.PartnerHello) Coop.S.StartRunAsHost();
            else Coop.S?.LeaveRun();
            if (Menu != null) Menu.Dismiss();
            Hud.SetVisible(true);
            Begin(true);
        }

        /// <summary>Duo partner: the host started a run — it starts here too, on the host's seed.</summary>
        public void StartCoopRun()
        {
            if (Menu != null) Menu.Dismiss();
            Hud.SetVisible(true);
            Begin(true);
        }

        /// <summary>
        /// Back to the title screen. The arena keeps running behind it — the player idles, the world
        /// breathes — but the run itself is parked, so no wave ever starts while the menu is up.
        /// </summary>
        public void ToMenu()
        {
            Coop.S?.LeaveRun();
            Hud.SetVisible(false);
            Begin(false);
            Menu.Open();
        }

        void Begin(bool startRun)
        {
            TimeFx.ResetAll();
            // whatever the last run dropped and didn't deliver yet is paid out now
            coins.Flush();
            Profile.Save();
            FxSystem.I.Clear();
            Rewards.Cancel();
            enemyShots.Clear();
            bossTells.Clear();
            echoes.Clear();
            vortices.Clear();
            court.Clear();
            barrier.Clear();
            decoys.Clear();
            partnerBarrier.Clear();
            lightning.Clear();
            Mechanics.SetRunning(false);
            Run.Reset();
            Combat.Reset();
            DevMode.OnRunStart();
            Player.Respawn();
            Ball.ResetTo(Player.Pos + new Vector2(0.5f, Art.BallRadius));
            Waves.Restart();
            if (startRun) Director.StartRun();
            else { Player.ApplyStats(true); Director.Idle(); }
            Hud.ResetState();
            if (startRun) Hud.ShowStageCard(Run.Stage, Run.Theme);
            Cam.SetZoom(1f);
            Cam.Snap(Player.Pos);
        }

        void Update()
        {
            if (RecoverFromReload()) return;
            float udt = TimeFx.UiDelta;
            Coop.Update(udt);

            GameInput.Blocked = Pause.IsOpen || Rewards.IsOpen || Dev.IsOpen || Menu.IsOpen;
            GameInput.Poll(Cam.Cam);

            if (GameInput.DevPressed && !CaptureMode && !Menu.IsOpen && !Coop.Active)
            {
                if (Dev.IsOpen) Dev.Close();
                else if (!Rewards.IsOpen) { Pause.Close(); Dev.Open(); }
            }
            if (GameInput.PausePressed)
            {
                if (Menu.IsOpen) Menu.HandleEscape();
                else if (Dev.IsOpen) Dev.Close();
                else if (Pause.IsOpen) Pause.HandleEscape();
                else if (!CaptureMode) Pause.Open();
            }
            // reward screens and the dev panel freeze the fight exactly like the pause menu (a duo's pause menu doesn't: the partner plays on)
            bool paused = (Pause.IsOpen && !Coop.Active) || Rewards.IsOpen || Dev.IsOpen;
            TimeFx.Paused = paused;
            TimeFx.Update(udt);
            Hud.SetPaused(paused);
            // the title screen draws the game's crosshair instead of the system pointer
            Cursor.visible = (paused || Player.Dead) && !Menu.IsOpen;

            if (GameInput.ToggleFps)
            {
                GameSettings.ShowFps = !GameSettings.ShowFps;
                GameSettings.Save();
            }
            if (GameInput.ToggleVsync)
            {
                GameSettings.VSync = !GameSettings.VSync;
                GameSettings.Apply();
                GameSettings.Save();
                Hud.ShowToast(GameSettings.VSync ? "VSYNC AN" : "VSYNC AUS  ·  UNBEGRENZTE FPS");
            }

            if (paused)
            {
                // a reward screen has the game frozen: the next stage's art can generate meanwhile
                if (Rewards.Settled) ArtQueue.Pump(12f);
                if (GameInput.Scripted) GameInput.ClearEdges();
                Coop.LateTick(udt);
                return;
            }

            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            if (Player.Dead || Director.P == RunDirector.Phase.RunOver)
            {
                if (Player.Dead) Player.DeadTime += udt;
                // a duo run is over only when both are down, and only the host starts the next one
                bool over = !Coop.Active || Director.P == RunDirector.Phase.RunOver;
                float overTime = Player.Dead ? Player.DeadTime : Director.PhaseTime;
                if (GameInput.RestartPressed && overTime > 0.9f && over && !Coop.IsClient) Restart();
            }

            Level.Update(dt);
            Player.Update(dt);
            Player.Rig.Update(dt);
            Ball.Update(dt, Player);
            Player.LateVisuals(dt);
            if (Coop.Active) Remote.Update(dt);
            upgradeLook.Update(dt);
            Waves.Update(dt, Player, Ball);
            bossTells.Update(dt);
            echoes.Update(dt);
            vortices.Update(dt);
            court.Update(dt);
            barrier.Update(dt);
            partnerBarrier.Update(dt);
            decoys.Update(dt);
            twinSun.Update(dt, Player, Director.Fighting);
            enemyShots.Update(dt, Player);
            Mechanics.Update(dt, Player, Ball, Waves);
            lightning.Update(dt);
            Combat.Update(dt);
            coins.Update(dt);
            Director.Update(dt);

            if (GameInput.Scripted) GameInput.ClearEdges();
            Coop.LateTick(udt);
        }

        void LateUpdate()
        {
            if (Cam == null || Player == null) return;
            float udt = TimeFx.UiDelta;
            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            bool paused = (Pause.IsOpen && !Coop.Active) || Rewards.IsOpen || Dev.IsOpen;

            // a downed duo player watches the partner until coming back
            var follow = Player;
            if (Player.Dead && Coop.Active && Remote.Present && !Remote.Down) follow = Remote.P;
            Vector2 aimOffset = follow == Player ? Vector2.ClampMagnitude((GameInput.AimWorld - (Player.Pos + Vector2.up)) * 0.09f, 1.1f) : Vector2.zero;
            Vector2 look = new Vector2(follow.Facing * 0.8f + aimOffset.x, aimOffset.y * 0.35f);
            Cam.Update(follow.Pos, follow.Grounded, follow.Pos.y, look, dt, paused ? 0f : udt);

            envTime += dt;
            Environment.Update(dt, envTime, Player, Ball);
            Grade.Update(dt, Player, Ball);
            Hud.Update(paused ? 0f : udt);
            Rewards.Update(udt, !Pause.IsOpen);
            Pause.Update(udt);
            Menu.Update(udt);
            Dev.Update(udt);
            Post.Update(udt);
        }
    }
}
