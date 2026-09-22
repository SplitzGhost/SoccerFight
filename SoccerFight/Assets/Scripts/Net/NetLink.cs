using System;
using System.Threading.Tasks;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Networking.Transport.Utilities;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// One connection to one partner. Online games go through Unity Relay over secure WebSockets —
    /// the only way two browsers can reach each other, and the host needs no open port: both sides
    /// dial out to the relay, the host's allocation is found by its six-letter join code. For local
    /// tests two game instances can also connect directly (UDP on localhost).
    ///
    /// Two lanes: a reliable, ordered one for events (spawns, deaths, hits, the run's flow) and an
    /// unreliable one for the ~30 Hz state updates, where only the newest packet matters. Both
    /// fragment, so a big snapshot never hits the MTU.
    /// </summary>
    public sealed class NetLink : IDisposable
    {
        public enum State { Idle, Starting, WaitingForPartner, Connecting, Connected, Failed, Closed }

        public State St { get; private set; } = State.Idle;
        public bool IsHost { get; private set; }
        public string JoinCode { get; private set; }
        public string Error { get; private set; }
        public bool Connected => St == State.Connected;
        /// <summary>Traffic counters (tests and the dev overlay).</summary>
        public int SentPackets, ReceivedPackets;
        public long SentBytes, ReceivedBytes;

        /// <summary>A partner arrived (host) or the connection stood (client).</summary>
        public event Action OnConnected;
        /// <summary>The partner left or the connection broke.</summary>
        public event Action OnDisconnected;
        /// <summary>One packet: buffer and length (valid only during the call).</summary>
        public event Action<byte[], int> OnData;

        NetworkDriver driver;
        NetworkPipeline reliable, unreliable;
        NetworkConnection conn;
        byte[] recv = new byte[16 * 1024];
        int generation;
        float connectT;
        float waitLogT;

        const int MaxPayload = 48 * 1024;

        // ------------------------------------------------------------------ services

        static Task servicesTask;

        /// <summary>Unity Gaming Services and an anonymous player id (each browser gets its own).</summary>
        static Task EnsureServices()
        {
            if (servicesTask == null || servicesTask.IsFaulted || servicesTask.IsCanceled) servicesTask = InitServices();
            return servicesTask;
        }

        static async Task InitServices()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                var options = new InitializationOptions();
                string profile = TestProfile();
                if (!string.IsNullOrEmpty(profile)) options.SetProfile(profile);
                await UnityServices.InitializeAsync(options);
            }
            if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        /// <summary>
        /// Two test instances on one machine would share one anonymous player: an editor gets its own with
        /// -sfNetProfile, a browser tab with ?profile=name in the address.
        /// </summary>
        static string TestProfile()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-sfNetProfile");
            if (i >= 0 && i + 1 < args.Length) return args[i + 1];
            string url = Application.absoluteURL ?? "";
            int q = url.IndexOf("profile=", StringComparison.Ordinal);
            if (q < 0) return null;
            var sb = new System.Text.StringBuilder();
            for (int k = q + 8; k < url.Length && sb.Length < 30 && char.IsLetterOrDigit(url[k]); k++) sb.Append(url[k]);
            return sb.Length > 0 ? sb.ToString() : null;
        }

        // ------------------------------------------------------------------ start

        NetworkSettings Settings()
        {
            var s = new NetworkSettings();
            // a browser tab in the background runs slowly: be patient before calling a partner gone
            s.WithNetworkConfigParameters(connectTimeoutMS: 1000, maxConnectAttempts: 30, disconnectTimeoutMS: 20000, heartbeatTimeoutMS: 1000);
            s.WithFragmentationStageParameters(payloadCapacity: MaxPayload);
            s.WithReliableStageParameters(windowSize: 128);
            return s;
        }

        void Create(NetworkSettings settings, bool webSocket)
        {
            driver = webSocket ? NetworkDriver.Create(new WebSocketNetworkInterface(), settings) : NetworkDriver.Create(settings);
            reliable = driver.CreatePipeline(typeof(FragmentationPipelineStage), typeof(ReliableSequencedPipelineStage));
            unreliable = driver.CreatePipeline(typeof(FragmentationPipelineStage), typeof(UnreliableSequencedPipelineStage));
        }

        /// <summary>Opens a room on the relay and fetches its join code.</summary>
        public async void HostRelay()
        {
            int gen = Begin(true);
            try
            {
                await EnsureServices();
                if (gen != generation) return;
                Allocation alloc = await RelayService.Instance.CreateAllocationAsync(1);
                if (gen != generation) return;
                string code = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);
                if (gen != generation) return;
                var data = alloc.ToRelayServerData(RelayProtocol);
                var settings = Settings();
                settings.WithRelayParameters(ref data);
                Create(settings, RelayProtocol == "wss");
                if (driver.Bind(NetworkEndpoint.AnyIpv4) != 0 || driver.Listen() != 0) { Fail("Der Raum konnte nicht geöffnet werden."); return; }
                JoinCode = code.ToUpperInvariant();
                St = State.WaitingForPartner;
            }
            catch (Exception e) { if (gen == generation) Fail(Describe(e)); }
        }

        /// <summary>Joins the room behind a join code.</summary>
        public async void JoinRelay(string code)
        {
            int gen = Begin(false);
            try
            {
                await EnsureServices();
                if (gen != generation) return;
                JoinAllocation alloc = await RelayService.Instance.JoinAllocationAsync(code.Trim().ToUpperInvariant());
                if (gen != generation) return;
                var data = alloc.ToRelayServerData(RelayProtocol);
                var settings = Settings();
                settings.WithRelayParameters(ref data);
                Create(settings, RelayProtocol == "wss");
                conn = driver.Connect();
                St = State.Connecting;
                connectT = 0f;
                JoinCode = code.Trim().ToUpperInvariant();
            }
            catch (Exception e) { if (gen == generation) Fail(Describe(e)); }
        }

        /// <summary>Local test: listen on a UDP port without the relay.</summary>
        public void HostDirect(ushort port)
        {
            Begin(true);
            Create(Settings(), false);
            int bind = driver.Bind(NetworkEndpoint.AnyIpv4.WithPort(port));
            int listen = bind == 0 ? driver.Listen() : -1;
            Debug.Log("[Net] direct host: bind " + bind + ", listen " + listen + ", port " + port);
            if (bind != 0 || listen != 0) { Fail("Port " + port + " ist belegt."); return; }
            JoinCode = "LOCAL";
            St = State.WaitingForPartner;
        }

        /// <summary>Local test: connect straight to a host on this machine.</summary>
        public void JoinDirect(string address, ushort port)
        {
            Begin(false);
            Create(Settings(), false);
            conn = driver.Connect(NetworkEndpoint.Parse(address, port));
            Debug.Log("[Net] direct join: connecting to " + address + ":" + port + " → " + driver.GetConnectionState(conn));
            JoinCode = "LOCAL";
            St = State.Connecting;
            connectT = 0f;
        }

        int Begin(bool host)
        {
            Close();
            IsHost = host;
            Error = null;
            JoinCode = null;
            St = State.Starting;
            return ++generation;
        }

        void Fail(string message)
        {
            Debug.LogWarning("[Net] " + message);
            DisposeDriver();
            Error = message;
            St = State.Failed;
        }

        /// <summary>Relay protocol: secure WebSockets (the only one a browser has); tests may pick "udp".</summary>
        static string RelayProtocol
        {
            get
            {
                var args = Environment.GetCommandLineArgs();
                int i = Array.IndexOf(args, "-sfRelayProto");
                return i >= 0 && i + 1 < args.Length ? args[i + 1] : "wss";
            }
        }

        static string Describe(Exception e)
        {
            string m = e.Message ?? "";
            Debug.LogWarning("[Net] " + e);
            if (e is RelayServiceException rex)
            {
                if (rex.Reason == RelayExceptionReason.JoinCodeNotFound || rex.Reason == RelayExceptionReason.InvalidRequest) return "Diesen Raum-Code gibt es nicht (mehr).";
                if (rex.Reason == RelayExceptionReason.AllocationNotFound) return "Der Raum ist nicht mehr offen.";
            }
            if (e is RequestFailedException) return "Keine Verbindung zu den Unity-Servern.";
            if (m.IndexOf("join code", StringComparison.OrdinalIgnoreCase) >= 0) return "Diesen Raum-Code gibt es nicht (mehr).";
            return "Verbindung fehlgeschlagen.";
        }

        // ------------------------------------------------------------------ stop

        /// <summary>Leaves the room (the partner sees a disconnect).</summary>
        public void Close()
        {
            generation++;
            if (driver.IsCreated && conn.IsCreated)
            {
                driver.Disconnect(conn);
                driver.ScheduleUpdate().Complete();   // push the goodbye out before the driver goes
            }
            DisposeDriver();
            if (St != State.Idle) St = State.Closed;
        }

        void DisposeDriver()
        {
            conn = default;
            if (driver.IsCreated) driver.Dispose();
            driver = default;
        }

        public void Dispose() => Close();

        // ------------------------------------------------------------------ pump

        /// <summary>Runs the transport, accepts the partner, delivers packets. Call once per frame.</summary>
        public void Update(float dt)
        {
            if (!driver.IsCreated) return;
            driver.ScheduleUpdate().Complete();

            if (IsHost)
            {
                NetworkConnection c;
                while ((c = driver.Accept()) != default)
                {
                    // a duo: whoever comes second is turned away
                    if (conn.IsCreated) { driver.Disconnect(c); continue; }
                    conn = c;
                    St = State.Connected;
                    OnConnected?.Invoke();
                }
                if (driver.GetRelayConnectionStatus() == RelayConnectionStatus.AllocationInvalid) { Fail("Der Raum ist abgelaufen. Erstelle einen neuen."); return; }
                if (St == State.WaitingForPartner)
                {
                    waitLogT += dt;
                    if (waitLogT > 20f) { waitLogT = 0f; Debug.Log("[Net] host waiting: relay " + driver.GetRelayConnectionStatus() + ", bound " + driver.Bound + ", listening " + driver.Listening); }
                }
            }
            else if (St == State.Connecting)
            {
                connectT += dt;
                if ((int)(connectT - dt) != (int)connectT) Debug.Log("[Net] connecting " + connectT.ToString("F0") + " s: " + driver.GetConnectionState(conn) + ", relay " + driver.GetRelayConnectionStatus());
                if (driver.GetRelayConnectionStatus() == RelayConnectionStatus.AllocationInvalid) { Fail("Der Raum ist nicht mehr offen."); return; }
                if (connectT > 25f) { Fail("Der Host antwortet nicht."); return; }
            }

            if (!conn.IsCreated) return;
            NetworkEvent.Type cmd;
            while (driver.IsCreated && conn.IsCreated && (cmd = driver.PopEventForConnection(conn, out var stream)) != NetworkEvent.Type.Empty)
            {
                switch (cmd)
                {
                    case NetworkEvent.Type.Connect:
                        St = State.Connected;
                        OnConnected?.Invoke();
                        break;
                    case NetworkEvent.Type.Data:
                    {
                        int n = stream.Length;
                        if (n > recv.Length) recv = new byte[Mathf.NextPowerOfTwo(n)];
                        stream.ReadBytes(new Span<byte>(recv, 0, n));
                        ReceivedPackets++;
                        ReceivedBytes += n;
                        OnData?.Invoke(recv, n);
                        break;
                    }
                    case NetworkEvent.Type.Disconnect:
                        conn = default;
                        if (IsHost) St = State.WaitingForPartner;   // the room stays open for a rejoin
                        else { DisposeDriver(); St = State.Closed; }
                        OnDisconnected?.Invoke();
                        return;
                }
            }
        }

        /// <summary>Sends one packet on the reliable (events) or unreliable (state) lane.</summary>
        public void Send(NetWriter w, bool reliableLane)
        {
            if (!driver.IsCreated || !conn.IsCreated || St != State.Connected || w.Length == 0) return;
            if (w.Length > MaxPayload) { Debug.LogWarning("[Net] packet too big: " + w.Length); return; }
            int status = driver.BeginSend(reliableLane ? reliable : unreliable, conn, out var writer, w.Length);
            if (status < 0) { Debug.LogWarning("[Net] BeginSend failed: " + status); return; }
            writer.WriteBytes(new Span<byte>(w.Data, 0, w.Length));
            if (writer.HasFailedWrites) { driver.AbortSend(writer); Debug.LogWarning("[Net] packet did not fit"); return; }
            status = driver.EndSend(writer);
            SentPackets++;
            SentBytes += w.Length;
            if (status < 0) Debug.LogWarning("[Net] EndSend failed: " + status);
        }
    }
}
