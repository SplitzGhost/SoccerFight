using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>Duo: the host's monster roster goes out in snapshots, the partner's screen mirrors it as ghosts.</summary>
    public sealed partial class WaveDirector
    {
        /// <summary>Portal the spawn in progress comes out of (0 left, 1 right, -1 none).</summary>
        int pendingPortal = -1;

        public Monster FindById(int id)
        {
            for (int i = 0; i < monsters.Count; i++)
                if (monsters[i].Alive && monsters[i].Id == id) return monsters[i];
            return null;
        }

        /// <summary>Partner's screen: a monster the host just spawned.</summary>
        public void SpawnGhost(in Monster.SpawnSpec spec, int id, float size, List<EliteAffix> affixes, float maxHp, int portal)
        {
            if (FindById(id) != null) return;
            var m = Get(spec);
            m.SpawnGhost(spec, id, size, affixes, maxHp);
            AliveCount++;
            if (spec.Rank == Rank.Boss) Boss = m;
            if (portal < 0) return;

            // the same portal flash as on the host
            bool left = portal == 0;
            Vector2 at = (left ? env.LeftPortal : env.RightPortal).position;
            if (left) leftCharge = 1.4f; else rightCharge = 1.4f;
            var fx = FxSystem.I;
            float big = spec.Rank >= Rank.MiniBoss ? 1.8f : spec.Rank == Rank.Elite ? 1.3f : 1f;
            fx.Flash(at, 2.6f * big, portalColor, 0.25f, 2.6f);
            fx.Ring(FxLayer.Front, at, 0.3f, 1.4f * big, 0.25f, 0.02f, 0.4f, portalColor, portalColor.WithAlpha(0f), 2.2f);
            fx.Sparks(at, new Vector2(left ? 1f : -1f, 0.3f), 90f, Mathf.RoundToInt(8 * big), 3f, 8f, portalColor, 2.4f, 0.05f, 0.3f);
            if (spec.Rank >= Rank.MiniBoss) Game.I.Cam.AddTrauma(0.25f);
        }

        public void WriteSnapshot(NetWriter w)
        {
            w.Unit(Mathf.Clamp01(leftCharge));
            w.Unit(Mathf.Clamp01(rightCharge));
            int count = 0;
            foreach (var m in monsters) if (m.Alive) count++;
            w.Byte((byte)Mathf.Min(count, 255));
            int written = 0;
            foreach (var m in monsters)
            {
                if (!m.Alive || written >= 255) continue;
                m.WriteNet(w);
                written++;
            }
        }

        public void ApplySnapshot(NetReader r, float hostTime)
        {
            leftCharge = Mathf.Max(leftCharge, r.Unit());
            rightCharge = Mathf.Max(rightCharge, r.Unit());
            int count = r.Byte();
            for (int i = 0; i < count; i++)
            {
                var s = MonsterNet.Read(r);
                var m = FindById(s.Id);
                if (m != null && m.Ghost) m.ApplyNet(s, hostTime);
            }
        }
    }
}
