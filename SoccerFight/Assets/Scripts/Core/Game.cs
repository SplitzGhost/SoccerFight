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
        public WaveDirector Waves { get; private set; }
        public Player Player { get; private set; }
        public Ball Ball { get; private set; }
        public WorldEnvironment Environment { get; private set; }
        public bool CaptureMode { get; private set; }

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
            TimeFx.ResetAll();

            // The backdrop generates on worker threads while the main thread builds everything else.
            EnvironmentArt.Begin();
            Art.Build();
            UiArt.Build();

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
            BuildTimer.Mark("env objects");
            FxSystem.Create(transform);
            Ball = new Ball();
            Ball.Build(transform);
            Player = new Player();
            Player.Build(transform, Ball);
            Waves = new WaveDirector();
            Waves.Build(transform, Environment);
            BuildTimer.Mark("actors");
            Hud = new Hud();
            Hud.Build(transform, Cam.Cam, Player, Waves, CaptureMode);
            Pause = new PauseMenu();
            Pause.Build(transform, Cam.Cam, CaptureMode);
            Pause.RestartRequested += () => { Pause.Close(); Restart(); };
            BuildTimer.Mark("hud");

            Restart();

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

        public void Restart()
        {
            TimeFx.ResetAll();
            FxSystem.I.Clear();
            Player.Respawn();
            Ball.ResetTo(Player.Pos + new Vector2(0.5f, Art.BallRadius));
            Waves.Restart();
            Hud.ResetState();
            Cam.SetZoom(1f);
            Cam.Snap(Player.Pos);
        }

        void Update()
        {
            if (RecoverFromReload()) return;
            float udt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);

            GameInput.Blocked = Pause.IsOpen;
            GameInput.Poll(Cam.Cam);

            if (GameInput.PausePressed)
            {
                if (Pause.IsOpen) Pause.HandleEscape();
                else if (!CaptureMode) Pause.Open();
            }
            bool paused = Pause.IsOpen;
            TimeFx.Paused = paused;
            TimeFx.Update(udt);
            Hud.SetPaused(paused);
            Cursor.visible = paused || Player.Dead;

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
                if (GameInput.Scripted) GameInput.ClearEdges();
                return;
            }

            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            if (Player.Dead)
            {
                Player.DeadTime += udt;
                if (GameInput.RestartPressed && Player.DeadTime > 0.9f) Restart();
            }

            Player.Update(dt);
            Player.Rig.Update(dt);
            Ball.Update(dt, Player);
            Player.LateVisuals(dt);
            Waves.Update(dt, Player, Ball);

            if (GameInput.Scripted) GameInput.ClearEdges();
        }

        void LateUpdate()
        {
            if (Cam == null || Player == null) return;
            float udt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            bool paused = Pause.IsOpen;

            Vector2 aimOffset = Vector2.ClampMagnitude((GameInput.AimWorld - (Player.Pos + Vector2.up)) * 0.09f, 1.1f);
            Vector2 look = new Vector2(Player.Facing * 0.8f + aimOffset.x, aimOffset.y * 0.35f);
            Cam.Update(Player.Pos, Player.Grounded, Player.Pos.y, look, dt, paused ? 0f : udt);

            envTime += dt;
            Environment.Update(dt, envTime, Player, Ball);
            Hud.Update(paused ? 0f : udt);
            Pause.Update(udt);
            Post.Update(udt);
        }
    }
}
