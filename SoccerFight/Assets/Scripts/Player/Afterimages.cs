using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>Pooled silhouette snapshots of the player rig that fade out (rainbow ghost trail).</summary>
    public sealed class Afterimages
    {
        sealed class Ghost
        {
            public Transform root;
            public SpriteRenderer[] parts;
            public float age, life, alpha;
            public Color color;
            public bool active;
        }

        readonly List<Ghost> pool = new List<Ghost>();
        readonly Transform parent;
        readonly PlayerRig rig;
        readonly List<SpriteRenderer> sourceParts = new List<SpriteRenderer>();

        public Afterimages(Transform parent, PlayerRig rig)
        {
            this.parent = new GameObject("Afterimages").transform;
            this.parent.SetParent(parent, false);
            this.rig = rig;
        }

        Ghost Create()
        {
            if (sourceParts.Count == 0)
                foreach (var p in rig.Parts)
                    if (p.sharedMaterial != Art.SpriteGlowMat) sourceParts.Add(p);

            var g = new Ghost { root = new GameObject("Ghost").transform, parts = new SpriteRenderer[sourceParts.Count] };
            g.root.SetParent(parent, false);
            for (int i = 0; i < sourceParts.Count; i++)
            {
                var sr = Art.MakeSprite("G", g.root, sourceParts[i].sprite, 80 + i, Art.SpriteSolidMat);
                g.parts[i] = sr;
            }
            pool.Add(g);
            return g;
        }

        public void Spawn(Color color, float life, float alpha = 0.42f)
        {
            Ghost g = null;
            for (int i = 0; i < pool.Count; i++) if (!pool[i].active) { g = pool[i]; break; }
            if (g == null) g = Create();

            g.active = true;
            g.age = 0f;
            g.life = life;
            g.alpha = alpha;
            g.color = color;
            g.root.gameObject.SetActive(true);
            for (int i = 0; i < g.parts.Length; i++)
            {
                var src = sourceParts[i].transform;
                var dst = g.parts[i].transform;
                dst.SetPositionAndRotation(src.position, src.rotation);
                dst.localScale = src.lossyScale;
                g.parts[i].color = color.WithAlpha(alpha);
            }
        }

        public void Update(float dt)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                var g = pool[i];
                if (!g.active) continue;
                g.age += dt;
                float t = g.age / g.life;
                if (t >= 1f)
                {
                    g.active = false;
                    g.root.gameObject.SetActive(false);
                    continue;
                }
                float a = g.alpha * (1f - MathUtil.EaseOutQuad(t));
                Color c = g.color.WithAlpha(a);
                for (int k = 0; k < g.parts.Length; k++) g.parts[k].color = c;
            }
        }

        public void Clear()
        {
            foreach (var g in pool) { g.active = false; g.root.gameObject.SetActive(false); }
        }
    }
}
