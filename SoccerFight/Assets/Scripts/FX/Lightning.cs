using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>Jagged electric arcs (chain sparks, storm strikes): a white core over a coloured glow, fading fast.</summary>
    public sealed class Lightning
    {
        public static Lightning I { get; private set; }

        sealed class Arc { public LineRenderer core, glow; public float age, life; public Color color; public bool active; }

        readonly List<Arc> pool = new List<Arc>();
        readonly List<Vector3> points = new List<Vector3>();
        Transform parent;
        Material mat;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { I = null; }

        public void Build(Transform root)
        {
            I = this;
            parent = new GameObject("Lightning").transform;
            parent.SetParent(root, false);
            mat = Art.MakeMeshMaterial("SF Lightning", Texture2D.whiteTexture, 2.6f, true, false);
        }

        LineRenderer Line(string name, Transform p, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(p, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = mat;
            lr.useWorldSpace = true;
            lr.numCapVertices = 2;
            lr.numCornerVertices = 1;
            lr.sortingOrder = order;
            lr.alignment = LineAlignment.View;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return lr;
        }

        Arc Get()
        {
            foreach (var a in pool) if (!a.active) return a;
            var root = new GameObject("Arc").transform;
            root.SetParent(parent, false);
            var n = new Arc { glow = Line("Glow", root, 140), core = Line("Core", root, 141) };
            pool.Add(n);
            return n;
        }

        /// <summary>An arc from a to b. Segment count and jitter scale with the distance.</summary>
        public void Bolt(Vector2 a, Vector2 b, Color c, float width = 0.07f, float life = 0.16f, float jitter = 0.28f)
        {
            var arc = Get();
            arc.active = true; arc.age = 0f; arc.life = life; arc.color = c;
            arc.core.gameObject.SetActive(true);
            arc.glow.gameObject.SetActive(true);
            points.Clear();
            Vector2 d = b - a;
            float len = d.magnitude;
            int n = Mathf.Clamp(Mathf.CeilToInt(len / 0.35f), 3, 40);
            Vector2 side = len > 1e-4f ? new Vector2(-d.y, d.x) / len : Vector2.up;
            for (int i = 0; i <= n; i++)
            {
                float u = i / (float)n;
                float off = (i == 0 || i == n) ? 0f : Random.Range(-jitter, jitter) * Mathf.Sin(u * Mathf.PI);
                Vector2 p = a + d * u + side * off;
                points.Add(new Vector3(p.x, p.y, 0f));
            }
            foreach (var lr in new[] { arc.core, arc.glow })
            {
                lr.positionCount = points.Count;
                for (int i = 0; i < points.Count; i++) lr.SetPosition(i, points[i]);
            }
            arc.core.widthMultiplier = width;
            arc.glow.widthMultiplier = width * 4f;
        }

        public void Update(float dt)
        {
            foreach (var a in pool)
            {
                if (!a.active) continue;
                a.age += dt;
                float k = a.age / a.life;
                if (k >= 1f) { a.active = false; a.core.gameObject.SetActive(false); a.glow.gameObject.SetActive(false); continue; }
                float fade = 1f - k * k;
                a.core.startColor = a.core.endColor = Color.white.WithAlpha(fade);
                a.glow.startColor = a.glow.endColor = a.color.WithAlpha(0.45f * fade);
            }
        }

        public void Clear()
        {
            foreach (var a in pool) { a.active = false; a.core.gameObject.SetActive(false); a.glow.gameObject.SetActive(false); }
        }
    }
}
