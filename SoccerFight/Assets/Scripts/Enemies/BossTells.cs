using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Small red warnings for the big boss moves: where a leap comes down, which way a charge will
    /// run, when a burst goes off. Deliberately quiet — a thin glow that fills up while the boss winds
    /// up and flares for a moment when the move fires.
    ///
    /// Immediate mode: a boss calls Spot/Lane/Ring every frame it wants a warning shown (keyed by
    /// monster and slot). Warnings nobody asks for any more fade out on their own.
    /// </summary>
    public sealed class BossTells
    {
        public static BossTells I { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { I = null; }

        enum Kind { Spot, Lane, Ring }

        sealed class Tell
        {
            public int Key;
            public Kind K;
            public Transform Root;
            public SpriteRenderer Glow, RingA, RingB, Dot, Line;
            public readonly List<SpriteRenderer> Chevrons = new List<SpriteRenderer>();
            public Vector2 Pos, Dir;
            public float Size, Length, Progress, Alpha, Flare, Age;
            public bool Touched, Live, Fired;
        }

        public static readonly Color Red = new Color(1f, 0.23f, 0.27f);
        const float ChevronStep = 0.85f;
        const int MaxChevrons = 16;

        readonly List<Tell> tells = new List<Tell>();
        Transform parent;
        Sprite chevron;
        float time;

        public void Build(Transform root)
        {
            I = this;
            parent = new GameObject("Boss Tells").transform;
            parent.SetParent(root, false);

            // a flat arrow head, drawn pointing right
            var c = new SdfCanvas(new Rect(-0.25f, -0.25f, 0.5f, 0.5f), 192f);
            c.Fill(p => Sdf.Union(Sdf.Capsule(p, new Vector2(-0.12f, 0.16f), new Vector2(0.1f, 0f), 0.045f),
                                  Sdf.Capsule(p, new Vector2(0.1f, 0f), new Vector2(-0.12f, -0.16f), 0.045f)), Color.white);
            chevron = c.ToSprite("TellChevron", Vector2.zero);
        }

        public void Clear()
        {
            foreach (var t in tells) Retire(t);
        }

        // ------------------------------------------------------------------ requests

        /// <summary>Landing mark on the ground. progress 0..1 fills while the move winds up.</summary>
        public void Spot(int key, Vector2 ground, float radius, float progress)
        {
            var t = Get(key, Kind.Spot);
            t.Pos = ground;
            t.Size = radius;
            Touch(t, progress);
        }

        /// <summary>Run of arrows from a point along a direction (a charge, a volley's line).</summary>
        public void Lane(int key, Vector2 from, Vector2 dir, float length, float progress)
        {
            var t = Get(key, Kind.Lane);
            t.Pos = from;
            t.Dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.right;
            t.Length = Mathf.Max(0.5f, length);
            Touch(t, progress);
        }

        /// <summary>Thin ring that tightens around a point (a burst about to go off).</summary>
        public void Ring(int key, Vector2 center, float radius, float progress)
        {
            var t = Get(key, Kind.Ring);
            t.Pos = center;
            t.Size = radius;
            Touch(t, progress);
        }

        static void Touch(Tell t, float progress)
        {
            progress = Mathf.Clamp01(progress);
            // the moment the wind-up completes: a short flare
            if (progress >= 1f && !t.Fired) { t.Fired = true; t.Flare = 1f; }
            if (progress < 0.98f) t.Fired = false;
            t.Progress = progress;
            t.Touched = true;
        }

        Tell Get(int key, Kind kind)
        {
            Tell free = null;
            foreach (var t in tells)
            {
                if (t.Live && t.Key == key && t.K == kind) return t;
                if (!t.Live && t.K == kind && free == null) free = t;
            }
            var n = free ?? Create(kind);
            n.Live = true;
            n.Key = key;
            n.Alpha = 0f;
            n.Flare = 0f;
            n.Age = 0f;
            n.Fired = false;
            n.Root.gameObject.SetActive(true);
            return n;
        }

        Tell Create(Kind kind)
        {
            var t = new Tell { K = kind };
            t.Root = new GameObject("Tell " + kind).transform;
            t.Root.SetParent(parent, false);
            switch (kind)
            {
                case Kind.Spot:
                    t.Glow = Art.MakeSprite("Glow", t.Root, Art.SoftGlow, -41, Art.SpriteGlowMat, Color.clear);
                    t.RingA = Art.MakeSprite("Ring", t.Root, Art.MarkRing, -40, Art.SpriteGlowMat, Color.clear);
                    t.RingB = Art.MakeSprite("Inner", t.Root, Art.MarkRing, -40, Art.SpriteGlowMat, Color.clear);
                    t.Dot = Art.MakeSprite("Dot", t.Root, Art.SoftGlow, -40, Art.SpriteGlowMat, Color.clear);
                    break;
                case Kind.Lane:
                    t.Line = Art.MakeSprite("Line", t.Root, Art.SoftGlow, -41, Art.SpriteGlowMat, Color.clear);
                    for (int i = 0; i < MaxChevrons; i++)
                        t.Chevrons.Add(Art.MakeSprite("Chevron" + i, t.Root, chevron, -40, Art.SpriteGlowMat, Color.clear));
                    break;
                case Kind.Ring:
                    t.Glow = Art.MakeSprite("Glow", t.Root, Art.SoftGlow, 57, Art.SpriteGlowMat, Color.clear);
                    t.RingA = Art.MakeSprite("Ring", t.Root, Art.MarkRing, 58, Art.SpriteGlowMat, Color.clear);
                    break;
            }
            tells.Add(t);
            return t;
        }

        void Retire(Tell t)
        {
            t.Live = false;
            t.Touched = false;
            t.Root.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ drawing

        public void Update(float dt)
        {
            time += dt;
            foreach (var t in tells)
            {
                if (!t.Live) continue;
                // fade in quickly while requested, out a little slower once the move is under way
                t.Alpha = Mathf.MoveTowards(t.Alpha, t.Touched ? 1f : 0f, dt * (t.Touched ? 6f : 3.5f));
                t.Flare = Mathf.Max(0f, t.Flare - dt * 3.2f);
                t.Age += dt;
                if (!t.Touched && t.Alpha <= 0f) { Retire(t); continue; }
                t.Touched = false;

                float k = MathUtil.EaseOutQuad(t.Progress);
                float pulse = 0.75f + 0.25f * Mathf.Sin(time * (9f + 14f * k));
                float a = t.Alpha;
                switch (t.K)
                {
                    case Kind.Spot: DrawSpot(t, k, pulse, a); break;
                    case Kind.Lane: DrawLane(t, k, pulse, a); break;
                    case Kind.Ring: DrawRing(t, k, pulse, a); break;
                }
            }
        }

        void DrawSpot(Tell t, float k, float pulse, float a)
        {
            float r = t.Size;
            t.Root.position = new Vector3(t.Pos.x, t.Pos.y + 0.03f, 0f);
            t.Glow.transform.localScale = new Vector3(r * 2.6f, r * 0.7f, 1f);
            t.Glow.color = Red.WithAlpha(a * (0.16f + 0.2f * k + 0.35f * t.Flare) * pulse);
            // the outer ring closes in on the mark as the boss gets ready
            float rr = Mathf.Lerp(r * 1.7f, r, k);
            t.RingA.transform.localScale = new Vector3(rr * 2f, rr * 0.56f, 1f);
            t.RingA.color = Red.WithAlpha(a * (0.35f + 0.4f * k + 0.3f * t.Flare));
            t.RingB.transform.localScale = new Vector3(r * 0.9f, r * 0.25f, 1f);
            t.RingB.color = Red.WithAlpha(a * 0.35f * k * pulse);
            t.Dot.transform.localScale = new Vector3(0.28f, 0.1f, 1f) * (1f + 0.6f * t.Flare);
            t.Dot.color = Color.Lerp(Red, Color.white, 0.35f).WithAlpha(a * (0.3f + 0.5f * k));
        }

        void DrawLane(Tell t, float k, float pulse, float a)
        {
            t.Root.position = new Vector3(t.Pos.x, t.Pos.y, 0f);
            float ang = MathUtil.Angle(t.Dir);
            t.Root.rotation = Quaternion.Euler(0f, 0f, ang);
            int count = Mathf.Clamp(Mathf.FloorToInt(t.Length / ChevronStep), 1, MaxChevrons);
            float len = count * ChevronStep;

            t.Line.transform.localPosition = new Vector3(len * 0.5f + 0.3f, 0f, 0f);
            t.Line.transform.localScale = new Vector3(len + 0.8f, 0.16f, 1f);
            t.Line.color = Red.WithAlpha(a * (0.1f + 0.14f * k + 0.25f * t.Flare));

            for (int i = 0; i < t.Chevrons.Count; i++)
            {
                var sr = t.Chevrons[i];
                if (i >= count) { sr.color = Color.clear; continue; }
                float u = (i + 1f) / count;
                // arrows light up from the boss outwards as the wind-up fills, then a pulse runs along
                float lit = Mathf.Clamp01((k * 1.15f - u * 0.95f) / 0.2f);
                float run = Mathf.Max(0f, Mathf.Sin((time * 7f - i * 0.8f)));
                sr.transform.localPosition = new Vector3(0.55f + i * ChevronStep, 0f, 0f);
                float s = 0.62f + 0.14f * run * k;
                sr.transform.localScale = new Vector3(s, s, 1f);
                sr.color = Red.WithAlpha(a * (0.12f + 0.55f * lit * (0.75f + 0.25f * run) + 0.3f * t.Flare) * (1f - 0.35f * u));
            }
        }

        void DrawRing(Tell t, float k, float pulse, float a)
        {
            t.Root.position = new Vector3(t.Pos.x, t.Pos.y, 0f);
            float r = Mathf.Lerp(t.Size * 1.45f, t.Size, k) * (1f + 0.25f * t.Flare);
            t.RingA.transform.localScale = new Vector3(r * 2f, r * 2f, 1f);
            t.RingA.color = Red.WithAlpha(a * (0.25f + 0.35f * k + 0.35f * t.Flare) * pulse);
            t.Glow.transform.localScale = Vector3.one * r * 2.3f;
            t.Glow.color = Red.WithAlpha(a * (0.05f + 0.08f * k + 0.18f * t.Flare));
        }
    }
}
