using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// The duo mode. Two players, one run: the host's game is the real one — it plans the waves,
    /// runs every monster, rolls the hazards — and the partner's game mirrors it. Each player
    /// plays their own body and ball for real on their own screen (no input delay) and tells the
    /// other where they are; hits on monsters go to the host, which applies them. Upgrades and
    /// skills are each player's own; the run only moves on when both have picked.
    ///
    /// Everything gameplay code needs is a static call here that does nothing outside a duo run,
    /// so single player stays exactly as it was.
    /// </summary>
    public static class Coop
    {
        public const ushort Protocol = 1;
        /// <summary>Seconds a downed player waits before coming back.</summary>
        public const float DownTime = 30f;

        public static CoopSession S { get; private set; }

        /// <summary>Connected to a partner (lobby or run).</summary>
        public static bool InSession => S != null;
        /// <summary>A duo run is being played with a connected partner.</summary>
        public static bool Active => S != null && S.InRun && S.Link.Connected;
        public static bool IsHost => Active && S.Link.IsHost;
        public static bool IsClient => Active && !S.Link.IsHost;
        public static RemotePlayer Remote => S?.Remote;

        /// <summary>Host time to draw the monsters at (partner's screen).</summary>
        public static float WorldRenderTime => S != null ? S.WorldClock.RenderTime : 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { S = null; }

        // ------------------------------------------------------------------ rooms

        /// <summary>Opens a room (relay, or a local port when the command line asks for a test).</summary>
        public static CoopSession HostRoom()
        {
            Leave();
            S = new CoopSession();
            ushort port = TestPort("-sfNetHost");
            if (port != 0) S.Link.HostDirect(port);
            else S.Link.HostRelay();
            return S;
        }

        public static CoopSession JoinRoom(string code)
        {
            Leave();
            S = new CoopSession();
            ushort port = TestPort("-sfNetJoin");
            if (port != 0) S.Link.JoinDirect("127.0.0.1", port);
            else S.Link.JoinRelay(code);
            return S;
        }

        public static void Leave()
        {
            if (S == null) return;
            S.Close();
            S = null;
        }

        static ushort TestPort(string flag)
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, flag);
            return i >= 0 && i + 1 < args.Length && ushort.TryParse(args[i + 1], out ushort p) ? p : (ushort)0;
        }

        // ------------------------------------------------------------------ frame

        public static void Update(float udt) => S?.Update(udt);
        public static void LateTick(float udt) => S?.LateTick(udt);

        // ------------------------------------------------------------------ monsters

        /// <summary>Host: whom this monster chases — the nearer living player, with some stickiness.</summary>
        public static Player TargetFor(Monster m, Player local)
        {
            var r = S.Remote;
            if (!r.Present || S.RemoteDown) { m.TargetSide = 0; return local; }
            if (local.Dead) { m.TargetSide = 1; return r.P; }
            float dl = Mathf.Abs(local.Pos.x - m.Center.x) + Mathf.Abs(local.Pos.y - m.Pos.y) * 0.5f;
            float dr = Mathf.Abs(r.P.Pos.x - m.Center.x) + Mathf.Abs(r.P.Pos.y - m.Pos.y) * 0.5f;
            if (m.TargetSide == 0 && dr < dl - 2.5f) m.TargetSide = 1;
            else if (m.TargetSide == 1 && dl < dr - 2.5f) m.TargetSide = 0;
            return m.TargetSide == 1 ? r.P : local;
        }

        /// <summary>Host: where a random hazard aims — at either player.</summary>
        public static Vector2 HazardTarget(Player local)
        {
            if (!IsHost || !S.Remote.Present || S.RemoteDown) return local.Pos;
            if (local.Dead || Random.value < 0.5f) return S.Remote.P.Pos;
            return local.Pos;
        }

        public static void SendSpawn(Monster m, in Monster.SpawnSpec spec, int portal)
        {
            var w = S.Event(Ev.MSpawn);
            w.Int(m.Id);
            w.Byte((byte)spec.Type);
            w.Byte((byte)spec.Rank);
            w.Byte((byte)System.Array.IndexOf(StageThemes.All, spec.Theme ?? StageThemes.All[0]));
            w.Bool(spec.Boss != null);
            w.Float(spec.Level);
            w.Pos(spec.At); w.Pos(spec.Vel);
            w.String(spec.Name);
            w.Float(m.SizeMul);
            w.Float(m.MaxHp);
            w.SByte((sbyte)portal);
            w.Byte((byte)m.Affixes.Count);
            foreach (var a in m.Affixes) w.Byte((byte)a);
        }

        public static void SendDeath(Monster m, bool byPartner)
        {
            var w = S.Event(Ev.MDie);
            w.Int(m.Id);
            w.Byte((byte)((m.Detonated ? 1 : 0) | (byPartner ? 2 : 0)));
        }

        public static void SendHit(Monster m, float dmg, Vector2 dir, float knock, bool big, bool crit, bool boosted)
        {
            if (!IsClient) return;
            var w = S.Event(Ev.MHit);
            w.Int(m.Id);
            w.Float(dmg);
            w.Pos(dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.up);
            w.Float(knock);
            w.Byte((byte)((big ? 1 : 0) | (crit ? 2 : 0) | (boosted ? 4 : 0)));
        }

        public static void SendStatus(Monster m, StatusKind kind, float a, float b)
        {
            if (!IsClient) return;
            var w = S.Event(Ev.MStatus);
            w.Int(m.Id); w.Byte((byte)kind); w.Float(a); w.Float(b);
        }

        public static void SendBounce(Monster m, float vx)
        {
            if (!IsClient) return;
            var w = S.Event(Ev.MBounce);
            w.Int(m.Id); w.Float(vx);
        }

        // ------------------------------------------------------------------ enemy shots, hazards

        public static void SendProjectile(int id, int kind, Vector2 pos, Vector2 vel, float dmg, Color c, float life, float floorY, float dir)
        {
            var w = S.Event(Ev.PSpawn);
            w.Int(id); w.Byte((byte)kind); w.Vec(pos); w.Vec(vel); w.Float(dmg); w.Color(c); w.Float(life); w.Float(floorY); w.Float(dir);
        }

        public static void SendProjectilePop(int id)
        {
            if (!Active) return;
            S.Event(Ev.PPop).Int(id);
        }

        public static void SendHazard(int kind, float x, float dmg, float delay)
        {
            var w = S.Event(Ev.Hazard);
            w.Byte((byte)kind); w.Float(x); w.Float(dmg); w.Float(delay);
        }

        public static void SendGust(float dir) => S.Event(Ev.Gust).Float(dir);
        public static void SendEclipse() => S.Event(Ev.Eclipse);

        public static void SendToast(string text)
        {
            if (!IsHost) return;
            S.Event(Ev.Toast).String(text);
        }

        // ------------------------------------------------------------------ the partner's moves that shape the fight

        public static void SendWall(float x, float floor, float life)
        {
            if (!Active) return;
            var w = S.Event(Ev.Wall);
            w.Float(x); w.Float(floor); w.Float(life);
        }

        public static void SendDecoy(float life, int extra)
        {
            if (!Active) return;
            var w = S.Event(Ev.Decoy);
            w.Float(life); w.Byte((byte)Mathf.Clamp(extra, 0, 9));
        }

        public static void SendEcho(Vector2 from, Vector2 dir, float speed, Color c, bool pierce, bool explosive)
        {
            if (!Active) return;
            var w = S.Event(Ev.Echo);
            w.Vec(from); w.Pos(dir.normalized); w.Float(speed); w.Color(c); w.Byte((byte)((pierce ? 1 : 0) | (explosive ? 2 : 0)));
        }

        public static void SendVortex(Vector2 at, float radius, float life, bool implode, Color c)
        {
            if (!Active) return;
            var w = S.Event(Ev.Vortex);
            w.Vec(at); w.Float(radius); w.Float(life); w.Bool(implode); w.Color(c);
        }

        // ------------------------------------------------------------------ run flow (host → partner)

        public static void SendStage(int stage)
        {
            if (!IsHost) return;
            S.Event(Ev.Stage).Int(stage);
        }

        public static void SendWave(int wave)
        {
            if (!IsHost) return;
            S.Event(Ev.Wave).Int(wave);
        }

        public static void SendWaveDone()
        {
            if (!IsHost) return;
            S.Event(Ev.WaveDone);
        }

        public static void SendBossDown(Vector2 at)
        {
            if (!IsHost) return;
            S.Event(Ev.BossDown).Vec(at);
        }

        /// <summary>Host: both players now pick their rewards; the run waits for both.</summary>
        public static void SendRewards(bool boss)
        {
            if (!IsHost) return;
            S.BeginRewards();
            S.Event(Ev.Rewards).Bool(boss);
        }

        /// <summary>This player's reward picks are done (the last pick of the chain).</summary>
        public static void LocalRewardsDone(bool boss) => S.LocalRewardsDone(boss);

        /// <summary>This player went down: wait, then come back — unless both are down.</summary>
        public static void OnLocalDown() => S.OnLocalDown();
    }

    /// <summary>What travels on the reliable lane, in order.</summary>
    public enum Ev : byte
    {
        Hello = 1, Launch, ToMenu,
        MSpawn = 10, MDie, MHit, MStatus, MBounce,
        PSpawn = 20, PPop,
        Hazard = 30, Gust, Eclipse, Toast,
        Stage = 40, Wave, WaveDone, BossDown, Rewards, RewardsDone, RunOver,
        Down = 60, Up,
        Wall = 70, Decoy, Vortex, Fx, Echo,
    }

    /// <summary>One room: the link, the partner, the run in common.</summary>
    public sealed class CoopSession
    {
        const byte PktEvents = 1, PktState = 2, PktWorld = 3;
        const float StateRate = 1f / 30f, WorldRate = 1f / 20f;

        public readonly NetLink Link = new NetLink();
        public readonly NetClock WorldClock = new NetClock();

        /// <summary>A duo run is on (it may continue alone if the partner drops).</summary>
        public bool InRun { get; private set; }
        public int RunSeed { get; private set; }
        public bool PartnerHello { get; private set; }
        public int PartnerCharacter { get; private set; } = -1;
        public string PartnerName { get; private set; } = "";
        /// <summary>Why the room ended or what went wrong, for the menu.</summary>
        public string Notice;
        public RemotePlayer Remote => Game.I.Remote;

        // down and back
        public bool LocalDown { get; private set; }
        public float DownLeft { get; private set; }
        public bool RemoteDown { get; private set; }

        // reward sync
        bool localRewardsDone, remoteRewardsDone, rewardsBoss;
        /// <summary>This player has picked and waits for the partner.</summary>
        public bool WaitingForPartner => InRun && Link.Connected && localRewardsDone && (Link.IsHost ? !remoteRewardsDone : true);

        readonly NetWriter events = new NetWriter();
        readonly NetWriter scratch = new NetWriter();
        readonly NetReader reader = new NetReader();
        readonly List<EliteAffix> affixes = new List<EliteAffix>();
        float stateT, worldT;
        bool helloSent;
        int helloCharacter = -1;

        public CoopSession()
        {
            events.Byte(PktEvents);
            Link.OnData += Receive;
            Link.OnConnected += () => { helloSent = false; PartnerHello = false; };
            Link.OnDisconnected += PartnerLost;
        }

        public void Close()
        {
            if (InRun) EndRunLocal();
            Link.Close();
        }

        // ------------------------------------------------------------------ sending

        /// <summary>Starts an event in this frame's reliable packet.</summary>
        public NetWriter Event(Ev e)
        {
            events.Byte((byte)e);
            return events;
        }

        void FlushEvents()
        {
            if (events.Length > 1 && Link.Connected) Link.Send(events, true);
            events.Clear();
            events.Byte(PktEvents);
        }

        void SendHello()
        {
            helloSent = true;
            helloCharacter = Characters.Index;
            var w = Event(Ev.Hello);
            w.UShort(Coop.Protocol);
            w.Byte((byte)Characters.Index);
            w.String(Characters.Current.Name);
            w.Bool(InRun);
        }

        // ------------------------------------------------------------------ frame

        public void Update(float udt)
        {
            Link.Update(udt);
            if (!Link.Connected) return;
            // introduce ourselves, and again whenever the player changes character in the menu
            if (!helloSent || helloCharacter != Characters.Index) SendHello();
            if (!InRun) return;

            var game = Game.I;
            // a downed player comes back after the wait, next to the partner if the partner stands
            if (LocalDown && !TimeFx.Paused)
            {
                DownLeft -= udt;
                if (DownLeft <= 0f) Revive();
            }
            if (Link.IsHost && LocalDown && RemoteDown && game.Director.P != RunDirector.Phase.RunOver) RunOver();
            if (Link.IsHost) TryFinishRewards();
        }

        public void LateTick(float udt)
        {
            if (!Link.Connected) { events.Clear(); events.Byte(PktEvents); return; }
            if (InRun)
            {
                stateT += udt;
                if (stateT >= StateRate)
                {
                    stateT = Mathf.Min(stateT - StateRate, StateRate);
                    scratch.Clear();
                    scratch.Byte(PktState);
                    Game.I.Player.ToNet(Characters.Index, LocalDown ? DownLeft : 0f).Write(scratch);
                    Link.Send(scratch, false);
                }
                if (Link.IsHost)
                {
                    worldT += udt;
                    if (worldT >= WorldRate)
                    {
                        worldT = Mathf.Min(worldT - WorldRate, WorldRate);
                        SendWorld();
                    }
                }
            }
            FlushEvents();
        }

        void SendWorld()
        {
            var game = Game.I;
            scratch.Clear();
            scratch.Byte(PktWorld);
            scratch.Float(Time.realtimeSinceStartup);
            scratch.Float(Level.Clock);
            scratch.Short((short)Mathf.Clamp(game.Director.Remaining, -1, 999));
            game.Waves.WriteSnapshot(scratch);
            BossTells.I.WriteSnapshot(scratch);
            Link.Send(scratch, false);
        }

        // ------------------------------------------------------------------ run start and end

        /// <summary>Host: a duo run starts now with this seed; the partner starts it too.</summary>
        public int StartRunAsHost()
        {
            RunSeed = Random.Range(1, 1 << 20);
            BeginRun();
            Event(Ev.Launch).Int(RunSeed);
            return RunSeed;
        }

        void BeginRun()
        {
            InRun = true;
            LocalDown = RemoteDown = false;
            DownLeft = 0f;
            localRewardsDone = remoteRewardsDone = false;
            WorldClock.Reset();
            Remote.Reset();
            Barrier.Partner?.Clear();
            BossTells.I?.ClearNet();
            stateT = worldT = 0f;
        }

        /// <summary>The run ends here (menu); the partner goes back to the room too.</summary>
        public void LeaveRun()
        {
            if (!InRun) return;
            if (Link.Connected) Event(Ev.ToMenu);
            EndRunLocal();
        }

        void EndRunLocal()
        {
            InRun = false;
            LocalDown = RemoteDown = false;
            Remote?.Reset();
            Barrier.Partner?.Clear();
            BossTells.I?.ClearNet();
        }

        void PartnerLost()
        {
            PartnerHello = false;
            PartnerCharacter = -1;
            if (!InRun) { Notice = "Dein Mitspieler hat den Raum verlassen."; return; }
            var game = Game.I;
            if (Link.IsHost)
            {
                // the host plays on alone
                RemoteDown = false;
                Remote.Reset();
                game.Hud.ShowToast("MITSPIELER GETRENNT  ·  DU SPIELST ALLEIN WEITER");
                if (LocalDown) { LocalDown = false; game.Director.EndRun(); }
                else if (localRewardsDone) TryFinishRewards(true);
            }
            else
            {
                Notice = "Die Verbindung zum Host ist abgebrochen.";
                EndRunLocal();
                game.ToMenu();
            }
        }

        // ------------------------------------------------------------------ down and back

        public void OnLocalDown()
        {
            LocalDown = true;
            DownLeft = Coop.DownTime;
            var p = Game.I.Player;
            p.Rig.SetVisible(false);
            p.Ball.SetVisible(false);
            Event(Ev.Down);
            Game.I.Hud.ShowToast("AUSGESCHALTET  ·  COMEBACK IN " + Mathf.RoundToInt(Coop.DownTime) + " S");
        }

        void Revive()
        {
            LocalDown = false;
            var game = Game.I;
            Vector2 at = Remote.Present && !RemoteDown ? Remote.P.Pos : game.Player.Pos;
            game.Player.CoopRevive(at);
            Event(Ev.Up);
            game.Hud.ShowToast("ZURÜCK IM SPIEL");
        }

        void RunOver()
        {
            Event(Ev.RunOver);
            LocalDown = RemoteDown = false;
            Game.I.Director.EndRun();
        }

        // ------------------------------------------------------------------ rewards

        public void BeginRewards()
        {
            localRewardsDone = remoteRewardsDone = false;
        }

        public void LocalRewardsDone(bool boss)
        {
            localRewardsDone = true;
            rewardsBoss = boss;
            if (!Link.IsHost) Event(Ev.RewardsDone);
            else TryFinishRewards();
        }

        void TryFinishRewards(bool force = false)
        {
            if (!localRewardsDone) return;
            if (!remoteRewardsDone && !force && Link.Connected) return;
            localRewardsDone = remoteRewardsDone = false;
            Game.I.Director.AfterBothRewards(rewardsBoss);
        }

        // ------------------------------------------------------------------ receiving

        void Receive(byte[] data, int length)
        {
            reader.Reset(data, length);
            byte kind = reader.Byte();
            switch (kind)
            {
                case PktEvents:
                    while (reader.More && !reader.Overrun) Handle((Ev)reader.Byte(), reader);
                    break;
                case PktState:
                {
                    var s = PlayerNet.Read(reader);
                    PartnerCharacter = s.Character;
                    if (InRun) Remote.Receive(s);
                    break;
                }
                case PktWorld:
                    if (InRun && !Link.IsHost) ApplyWorld(reader);
                    break;
            }
        }

        void ApplyWorld(NetReader r)
        {
            var game = Game.I;
            float hostTime = r.Float();
            float levelTime = r.Float();
            WorldClock.Observe(hostTime);
            // the platforms run on the host's clock, as far in the past as the monsters are drawn
            float want = levelTime + (WorldClock.RenderTime - hostTime);
            float err = want - Level.Clock;
            Level.Nudge(Mathf.Abs(err) > 1.5f ? err : err * 0.15f);
            game.Director.NetRemaining = r.Short();
            game.Waves.ApplySnapshot(r, hostTime);
            BossTells.I.ApplySnapshot(r);
        }

        void Handle(Ev e, NetReader r)
        {
            var game = Game.I;
            switch (e)
            {
                case Ev.Hello:
                {
                    ushort version = r.UShort();
                    PartnerCharacter = r.Byte();
                    PartnerName = r.String() ?? "";
                    r.Bool();
                    PartnerHello = true;
                    if (version != Coop.Protocol) Notice = "Dein Mitspieler hat eine andere Spielversion. Ladet beide die Seite neu.";
                    break;
                }
                case Ev.Launch:
                {
                    int seed = r.Int();
                    if (Link.IsHost) break;
                    RunSeed = seed;
                    BeginRun();
                    game.StartCoopRun();
                    break;
                }
                case Ev.ToMenu:
                    if (!InRun) break;
                    EndRunLocal();
                    Notice = "Dein Mitspieler hat den Lauf beendet.";
                    game.ToMenu();
                    break;

                // ---- monsters
                case Ev.MSpawn:
                {
                    int id = r.Int();
                    var type = (EnemyType)r.Byte();
                    var rank = (Rank)r.Byte();
                    int themeIndex = r.Byte();
                    bool boss = r.Bool();
                    float level = r.Float();
                    Vector2 at = r.Pos(), vel = r.Pos();
                    string name = r.String();
                    float size = r.Float();
                    float maxHp = r.Float();
                    int portal = r.SByte();
                    affixes.Clear();
                    int n = r.Byte();
                    for (int i = 0; i < n; i++) affixes.Add((EliteAffix)r.Byte());
                    if (!InRun || Link.IsHost) break;
                    var theme = StageThemes.All[Mathf.Clamp(themeIndex, 0, StageThemes.All.Length - 1)];
                    var spec = new Monster.SpawnSpec
                    {
                        Type = type, At = at, Vel = vel, Level = level, Rank = rank, Affixes = n,
                        Theme = theme, Name = name, Boss = boss ? theme.Boss : null,
                    };
                    game.Waves.SpawnGhost(spec, id, size, affixes, maxHp, portal);
                    break;
                }
                case Ev.MDie:
                {
                    int id = r.Int();
                    int flags = r.Byte();
                    if (!InRun || Link.IsHost) break;
                    var m = game.Waves.FindById(id);
                    if (m != null && m.Ghost) m.GhostDie((flags & 1) != 0, (flags & 2) != 0);
                    break;
                }
                case Ev.MHit:
                {
                    int id = r.Int();
                    float dmg = r.Float();
                    Vector2 dir = r.Pos();
                    float knock = r.Float();
                    int flags = r.Byte();
                    if (!InRun || !Link.IsHost) break;
                    var m = game.Waves.FindById(id);
                    if (m != null && !m.Ghost) m.ApplyRemoteHit(dmg, dir, knock, (flags & 1) != 0, (flags & 2) != 0, (flags & 4) != 0);
                    break;
                }
                case Ev.MStatus:
                {
                    int id = r.Int();
                    var kind = (StatusKind)r.Byte();
                    float a = r.Float(), b = r.Float();
                    if (!InRun || !Link.IsHost) break;
                    var m = game.Waves.FindById(id);
                    if (m != null && !m.Ghost) m.ApplyRemoteStatus(kind, a, b);
                    break;
                }
                case Ev.MBounce:
                {
                    int id = r.Int();
                    float vx = r.Float();
                    if (!InRun || !Link.IsHost) break;
                    var m = game.Waves.FindById(id);
                    if (m != null && !m.Ghost) m.Vel.x = vx;
                    break;
                }

                // ---- enemy shots and hazards
                case Ev.PSpawn:
                {
                    int id = r.Int();
                    int kind = r.Byte();
                    Vector2 pos = r.Vec(), vel = r.Vec();
                    float dmg = r.Float();
                    Color c = r.Color();
                    float life = r.Float(), floorY = r.Float(), dir = r.Float();
                    if (!InRun || Link.IsHost) break;
                    EnemyProjectiles.I.SpawnNet(id, kind, pos, vel, dmg, c, life, floorY, dir);
                    break;
                }
                case Ev.PPop:
                {
                    int id = r.Int();
                    if (InRun) EnemyProjectiles.I.PopById(id);
                    break;
                }
                case Ev.Hazard:
                {
                    int kind = r.Byte();
                    float x = r.Float(), dmg = r.Float(), delay = r.Float();
                    if (InRun && !Link.IsHost) StageMechanics.I.AddNet(kind, x, dmg, delay);
                    break;
                }
                case Ev.Gust:
                {
                    float dir = r.Float();
                    if (InRun && !Link.IsHost) StageMechanics.I.StartGust(dir);
                    break;
                }
                case Ev.Eclipse:
                    if (InRun && !Link.IsHost) StageMechanics.I.StartEclipse();
                    break;
                case Ev.Toast:
                {
                    string text = r.String();
                    if (InRun) game.Hud.ShowToast(text);
                    break;
                }

                // ---- run flow
                case Ev.Stage:
                {
                    int stage = r.Int();
                    if (InRun && !Link.IsHost) { localRewardsDone = false; game.Director.NetStage(stage); }
                    break;
                }
                case Ev.Wave:
                {
                    int wave = r.Int();
                    if (InRun && !Link.IsHost) { localRewardsDone = false; game.Director.NetWave(wave); }
                    break;
                }
                case Ev.WaveDone:
                    if (InRun && !Link.IsHost) game.Director.NetWaveDone();
                    break;
                case Ev.BossDown:
                {
                    Vector2 at = r.Vec();
                    if (InRun && !Link.IsHost) game.Director.NetBossDown(at);
                    break;
                }
                case Ev.Rewards:
                {
                    bool boss = r.Bool();
                    if (InRun && !Link.IsHost) { localRewardsDone = false; game.Director.NetRewards(boss); }
                    break;
                }
                case Ev.RewardsDone:
                    if (InRun && Link.IsHost) remoteRewardsDone = true;
                    break;
                case Ev.RunOver:
                    if (InRun && !Link.IsHost) { LocalDown = RemoteDown = false; game.Director.EndRun(); }
                    break;

                // ---- the partner
                case Ev.Down:
                    RemoteDown = true;
                    if (InRun) game.Hud.ShowToast(PartnerName + " IST AUSGESCHALTET  ·  HALTE DURCH");
                    break;
                case Ev.Up:
                    RemoteDown = false;
                    if (InRun) game.Hud.ShowToast(PartnerName + " IST ZURÜCK");
                    break;
                case Ev.Wall:
                {
                    float x = r.Float(), floor = r.Float(), life = r.Float();
                    if (InRun) Barrier.Partner?.Spawn(x, floor, life);
                    break;
                }
                case Ev.Decoy:
                {
                    float life = r.Float();
                    int extra = r.Byte();
                    if (InRun && Remote.Present) Decoys.I.Spawn(Remote.P, life, extra, true);
                    break;
                }
                case Ev.Vortex:
                {
                    Vector2 at = r.Vec();
                    float radius = r.Float(), life = r.Float();
                    bool implode = r.Bool();
                    Color c = r.Color();
                    if (InRun) Vortices.I.Spawn(at, radius, life, 0f, 0f, implode, c, true);
                    break;
                }
                case Ev.Fx:
                {
                    var kind = (CoopFx.Kind)r.Byte();
                    Vector2 at = r.Vec();
                    float radius = r.Float();
                    Color c = r.Color();
                    if (InRun) CoopFx.Play(kind, at, radius, c);
                    break;
                }
                case Ev.Echo:
                {
                    Vector2 from = r.Vec(), dir = r.Pos();
                    float speed = r.Float();
                    Color c = r.Color();
                    int flags = r.Byte();
                    if (InRun) EchoBalls.I.Fire(from, dir, speed, 0f, Src.Echo, c, (flags & 1) != 0, (flags & 2) != 0, false, true);
                    break;
                }
                default:
                    Debug.LogWarning("[Net] unknown event " + (int)e);
                    reader.Reset(null, 0);   // the rest of the packet can't be read any more
                    break;
            }
        }
    }
}
