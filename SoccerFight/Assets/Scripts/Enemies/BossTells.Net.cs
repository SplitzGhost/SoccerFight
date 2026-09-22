using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Duo: the warnings are asked for every frame by the host's boss AI. The host lists what was
    /// asked for in each snapshot; the partner's screen asks for the same warnings every frame
    /// until the next snapshot says otherwise.
    /// </summary>
    public sealed partial class BossTells
    {
        struct TellNet
        {
            public int Key, Kind;
            public Vector2 Pos, Dir;
            public float Size, Length, Progress;
        }

        readonly List<TellNet> netOut = new List<TellNet>();
        readonly List<TellNet> netIn = new List<TellNet>();

        void Record(Tell t) => netOut.Add(new TellNet { Key = t.Key, Kind = (int)t.K, Pos = t.Pos, Dir = t.Dir, Size = t.Size, Length = t.Length, Progress = t.Progress });

        public void WriteSnapshot(NetWriter w)
        {
            w.Byte((byte)Mathf.Min(netOut.Count, 32));
            for (int i = 0; i < netOut.Count && i < 32; i++)
            {
                var t = netOut[i];
                w.Int(t.Key); w.Byte((byte)t.Kind);
                w.Pos(t.Pos); w.Pos(t.Dir);
                w.Float(t.Size); w.Float(t.Length); w.Unit(t.Progress);
            }
        }

        public void ApplySnapshot(NetReader r)
        {
            netIn.Clear();
            int n = r.Byte();
            for (int i = 0; i < n; i++)
                netIn.Add(new TellNet
                {
                    Key = r.Int(), Kind = r.Byte(), Pos = r.Pos(), Dir = r.Pos(),
                    Size = r.Float(), Length = r.Float(), Progress = r.Unit(),
                });
        }

        void ReplayNet()
        {
            foreach (var t in netIn)
            {
                switch ((Kind)t.Kind)
                {
                    case Kind.Spot: Spot(t.Key, t.Pos, t.Size, t.Progress); break;
                    case Kind.Lane: Lane(t.Key, t.Pos, t.Dir, t.Length, t.Progress); break;
                    case Kind.Ring: Ring(t.Key, t.Pos, t.Size, t.Progress); break;
                }
            }
        }

        public void ClearNet() => netIn.Clear();
    }
}
