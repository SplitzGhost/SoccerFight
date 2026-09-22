using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// The duo partner on this screen: a full player body with its own ball, posed from the packets
    /// the partner sends. Packets are kept for a moment and played back a little in the past, so the
    /// body always blends between two real states instead of jumping from packet to packet.
    /// </summary>
    public sealed class RemotePlayer
    {
        public Player P { get; private set; }
        public Ball B { get; private set; }
        /// <summary>A packet arrived at least once since the run started.</summary>
        public bool Present => count > 0;
        public PlayerNet Last => count > 0 ? buf[(head - 1 + Size) % Size] : default;
        /// <summary>The partner is down and waiting to come back.</summary>
        public bool Down => Present && P.Dead;
        public float DownLeft { get; private set; }
        public int Character { get; private set; } = -1;

        const int Size = 16;
        readonly PlayerNet[] buf = new PlayerNet[Size];
        int head, count;
        readonly NetClock clock = new NetClock();
        bool shown;

        public void Build(Transform parent)
        {
            B = new Ball { OrderShift = -32 };
            B.Build(parent);
            P = new Player { Puppet = true };
            P.Build(parent, B, -32);
            Hide();
        }

        public void Reset()
        {
            head = count = 0;
            clock.Reset();
            Hide();
        }

        void Hide()
        {
            shown = false;
            P.Rig.SetVisible(false);
            B.SetVisible(false);
            P.Dead = true;
        }

        public void Receive(in PlayerNet s)
        {
            // drop stale or duplicated packets (the unreliable lane may reorder)
            if (count > 0 && s.T <= buf[(head - 1 + Size) % Size].T) return;
            clock.Observe(s.T);
            buf[head] = s;
            head = (head + 1) % Size;
            if (count < Size) count++;
        }

        PlayerNet Sample(float t)
        {
            // newest first: find the pair around t
            PlayerNet newer = buf[(head - 1 + Size) % Size];
            if (t >= newer.T || count == 1) return newer;
            for (int i = 1; i < count; i++)
            {
                PlayerNet older = buf[(head - 1 - i + Size * 2) % Size];
                if (older.T <= t)
                {
                    float span = newer.T - older.T;
                    return PlayerNet.Lerp(older, newer, span > 1e-4f ? (t - older.T) / span : 1f);
                }
                newer = older;
            }
            return newer;
        }

        public void Update(float dt)
        {
            if (count == 0) { if (shown) Hide(); return; }
            var s = Sample(clock.RenderTime);
            if (s.Character != Character)
            {
                Character = s.Character;
                P.Rig.ApplyLook(PlayerArt.Get(Character));
            }
            DownLeft = s.DownLeft;
            P.ApplyNet(s);
            bool body = !s.Dead;
            if (body != shown)
            {
                shown = body;
                P.Rig.SetVisible(body);
                B.SetVisible(body);
                if (body) P.Rig.ResetPose();
            }
            if (!body) return;
            P.Rig.Update(dt);
            B.UpdateNet(dt, P);
            P.LateVisuals(dt);
        }
    }

    /// <summary>
    /// Maps the sender's clock onto ours. Every packet carries the sender's time; the smallest
    /// offset seen lately is the one with the least delay in it. Playback runs a bit behind that
    /// (more when packets arrive unevenly), so there is nearly always a newer packet to blend to.
    /// </summary>
    public sealed class NetClock
    {
        float offset, jitter = 0.03f, lastLocal, lastRemote;
        bool known;

        public void Reset() { known = false; jitter = 0.03f; }

        public void Observe(float remoteTime)
        {
            float local = Time.realtimeSinceStartup;
            float o = remoteTime - local;
            if (!known) { offset = o; known = true; }
            else
            {
                // a packet that took longer than usual only nudges the offset; a faster one resets it
                if (o > offset) offset = o;
                else offset = Mathf.Lerp(offset, o, 0.02f);
                float gap = (local - lastLocal) - (remoteTime - lastRemote);
                jitter = Mathf.Lerp(jitter, Mathf.Abs(gap), 0.1f);
            }
            lastLocal = local;
            lastRemote = remoteTime;
        }

        /// <summary>Delay behind the newest state: two send intervals plus the measured unevenness.</summary>
        public float Delay => Mathf.Clamp(0.075f + jitter * 2f, 0.075f, 0.3f);

        /// <summary>The sender's time to draw right now.</summary>
        public float RenderTime => Time.realtimeSinceStartup + offset - Delay;

        /// <summary>The sender's current time (no playback delay).</summary>
        public float Now => Time.realtimeSinceStartup + offset;
    }
}
