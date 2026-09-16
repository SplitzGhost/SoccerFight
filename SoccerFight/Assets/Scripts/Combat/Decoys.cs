using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// The decoy left behind by the sidestep: a frozen snapshot of the player's own rig that the
    /// monsters believe in. While one stands, every chase, leap and shot aims at it instead of at
    /// the player; when it runs out it bursts and shoves whatever gathered around it.
    /// </summary>
    public sealed class Decoys
    {
        public static Decoys I { get; private set; }

        sealed class Ghost
        {
            public Transform root;
            public SpriteRenderer[] parts;
            public SpriteRenderer ring;
            public Vector2 pos;
            public float age, life;
            public bool active;
        }

        readonly List<Ghost> pool = new List<Ghost>();
        readonly List<SpriteRenderer> source = new List<SpriteRenderer>();
        Transform parent;
        PlayerRig rig;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { I = null; }

        public void Build(Transform root, PlayerRig playerRig)
        {
            I = this;
            rig = playerRig;
            parent = new GameObject("Decoys").transform;
            parent.SetParent(root, false);
        }

        /// <summary>Where the monsters think the player is.</summary>
        public static Vector2 Focus(Vector2 playerPos)
        {
            if (I == null) return playerPos;
            Ghost best = null;
            float bestD = float.MaxValue;
            foreach (var g in I.pool)
            {
                if (!g.active) continue;
                float d = (g.pos - playerPos).sqrMagnitude;
                if (d < bestD) { bestD = d; best = g; }
            }
            return best != null ? best.pos : playerPos;
        }

        public bool Any
        {
            get { foreach (var g in pool) if (g.active) return true; return false; }
        }

        Ghost Create()
        {
            if (source.Count == 0)
                foreach (var p in rig.Parts)
                    if (p.sharedMaterial != Art.SpriteGlowMat) source.Add(p);

            var g = new Ghost { root = new GameObject("Decoy").transform, parts = new SpriteRenderer[source.Count] };
            g.root.SetParent(parent, false);
            for (int i = 0; i < source.Count; i++)
                g.parts[i] = Art.MakeSprite("D", g.root, source[i].sprite, 70 + i, Art.SpriteSolidMat);
            g.ring = Art.MakeSprite("Ring", g.root, Art.Ring, 69, Art.SpriteGlowMat, Color.clear);
            pool.Add(g);
            return g;
        }

        public void Spawn(Player player, float life, int extra)
        {
            int n = 1 + Mathf.Max(0, extra);
            for (int k = 0; k < n; k++)
            {
                Ghost g = null;
                foreach (var p in pool) if (!p.active) { g = p; break; }
                if (g == null) g = Create();

                g.active = true;
                g.age = 0f;
                g.life = life;
                g.pos = player.Pos + new Vector2(k == 0 ? 0f : (k % 2 == 0 ? 1.5f : -1.5f) * ((k + 1) / 2), 0f);
                g.pos.x = Mathf.Clamp(g.pos.x, -Player.ArenaHalf, Player.ArenaHalf);
                g.root.gameObject.SetActive(true);
                // snapshot the pose the player is standing in right now
                Vector2 offset = g.pos - player.Pos;
                for (int i = 0; i < g.parts.Length; i++)
                {
                    var src = source[i].transform;
                    var dst = g.parts[i].transform;
                    dst.SetPositionAndRotation(src.position + new Vector3(offset.x, offset.y, 0f), src.rotation);
                    dst.localScale = src.lossyScale;
                    g.parts[i].sprite = source[i].sprite;
                }
                g.ring.transform.position = new Vector3(g.pos.x, g.pos.y + 0.05f, 0f);
                g.ring.transform.localScale = Vector3.one * 1.6f;

                var fx = FxSystem.I;
                fx.Ring(FxLayer.Front, g.pos + new Vector2(0f, 0.9f), 0.2f, 1.6f, 0.16f, 0.01f, 0.28f, Color.white, Palette.Trick.WithAlpha(0f), 2.4f);
                fx.Motes(g.pos + new Vector2(0f, 0.9f), Vector2.up * 0.8f, Palette.Trick, 5, 0.5f);
            }
        }

        public void Update(float dt)
        {
            foreach (var g in pool)
            {
                if (!g.active) continue;
                g.age += dt;
                if (g.age >= g.life) { Burst(g); continue; }

                float t = g.age / g.life;
                float flicker = 0.62f + 0.18f * Mathf.Sin(g.age * 13f) * Mathf.Lerp(0.4f, 1f, t);
                float fade = Mathf.Min(1f, (1f - t) * 4f) * Mathf.Min(1f, g.age * 6f);
                Color c = Palette.Trick.WithAlpha(flicker * fade);
                for (int i = 0; i < g.parts.Length; i++) g.parts[i].color = c;
                g.ring.color = Palette.Trick.WithAlpha(0.3f * fade * (0.6f + 0.4f * Mathf.Sin(g.age * 6f)));
                g.ring.transform.localScale = Vector3.one * (1.5f + 0.2f * Mathf.Sin(g.age * 6f));
                if (Random.value < dt * 5f)
                    FxSystem.I.Motes(g.pos + new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(0.4f, 1.6f)), Vector2.up * 0.6f, Palette.Trick, 1, 0.2f);
            }
        }

        /// <summary>The ghost pops: a shove for everything that gathered around it.</summary>
        void Burst(Ghost g)
        {
            g.active = false;
            g.root.gameObject.SetActive(false);
            var s = Game.I.Run.Stats;
            Vector2 at = g.pos + new Vector2(0f, 0.9f);
            float radius = (s.DecoyBlast ? 3.2f : 2.2f) * s.AreaMul;
            Combat.Explosion(at, radius, s.DecoyBlast ? 45f : 18f, Palette.Trick, Src.Decoy);

            var fx = FxSystem.I;
            fx.Flash(at, s.DecoyBlast ? 3.2f : 2f, Palette.Trick, 0.18f, 3f);
            fx.Ring(FxLayer.Front, at, 0.2f, radius, 0.28f, 0.02f, 0.34f, Color.white, Palette.Trick.WithAlpha(0f), 2.6f);
            for (int i = 0; i < 12; i++)
            {
                float ang = Random.Range(0f, 360f);
                fx.Streak(FxLayer.Front, at, MathUtil.Dir(ang) * Random.Range(4f, 9f), Random.Range(0.2f, 0.36f), 0.05f, 0.05f,
                    Color.white.WithAlpha(0.9f), Palette.Trick.WithAlpha(0f), 2.4f, 4f);
            }
            Game.I.Cam.AddTrauma(s.DecoyBlast ? 0.24f : 0.12f);
        }

        public void Clear()
        {
            foreach (var g in pool) { g.active = false; g.root.gameObject.SetActive(false); }
        }
    }
}
