using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Walkable geometry: the pitch at y = 0 plus one-way platforms. Stage 1 always uses the classic
    /// ruin layout; every later stage generates its own — sometimes sparse, sometimes crowded, small or
    /// wide, still or gliding back and forth / up and down — in the styles its theme prefers. Everything
    /// that stands, lands or bounces (player, blobs, ball, shadows, grass) asks here instead of assuming
    /// a flat floor, and riders of moving platforms are carried by <see cref="Platform.Delta"/>.
    /// Platforms are jumped through from below and dropped through with the down key.
    /// </summary>
    public static class Level
    {
        public enum Style { Terrace, Capital, Rock, Crystal, Block, Plank, Mushroom }
        public enum Motion { None, Horizontal, Vertical }

        public sealed class Platform
        {
            public float X0, X1, Y;                       // current extent (moves with the motion)
            public readonly float BaseX0, BaseX1, BaseY;  // where the art was drawn
            public readonly Style Kind;
            public readonly int Seed;
            public Motion Move;
            public float Amp, Period = 6f, Phase;
            /// <summary>How far the platform moved during the last step (riders get carried by this).</summary>
            public Vector2 Delta;

            public Platform(float x0, float x1, float y, Style kind, int seed)
            {
                X0 = BaseX0 = x0; X1 = BaseX1 = x1; Y = BaseY = y; Kind = kind; Seed = seed;
            }

            public float Center => (X0 + X1) * 0.5f;
            public float Width => X1 - X0;
            public bool Moving => Move != Motion.None;
            public Vector2 Offset => new Vector2(X0 - BaseX0, Y - BaseY);
            /// <summary>Stone slabs and mushrooms stand on supports down to the pitch — they never move.</summary>
            public bool Grounded => Kind == Style.Terrace || Kind == Style.Capital || Kind == Style.Mushroom;

            internal void Step(float time)
            {
                float off = Move == Motion.None ? 0f : Amp * Mathf.Sin(time * MathUtil.Tau / Period + Phase);
                float nx0 = BaseX0 + (Move == Motion.Horizontal ? off : 0f);
                float ny = BaseY + (Move == Motion.Vertical ? off : 0f);
                Delta = new Vector2(nx0 - X0, ny - Y);
                X1 = nx0 + (BaseX1 - BaseX0);
                X0 = nx0;
                Y = ny;
            }

            /// <summary>Extent including the whole horizontal swing.</summary>
            internal float Reach0 => BaseX0 - (Move == Motion.Horizontal ? Amp : 0f);
            internal float Reach1 => BaseX1 + (Move == Motion.Horizontal ? Amp : 0f);
        }

        public static Platform[] Platforms = Classic();
        static float time;

        public const float Ground = 0f;
        public const int None = -1;
        /// <summary>Pass as "ignore" to fall through every platform (down key held in the air).</summary>
        public const int All = -2;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Platforms = Classic(); time = 0f; }

        /// <summary>low levels (~2.2) are one jump from the pitch, the rocks (~4.1) one jump from a low level.</summary>
        public static Platform[] Classic() => new[]
        {
            new Platform(-14.8f, -10.0f, 2.1f, Style.Terrace, 11),
            new Platform(-8.9f, -6.2f, 4.1f, Style.Rock, 23),
            new Platform(-4.75f, -2.45f, 2.25f, Style.Capital, 37),
            new Platform(-0.95f, 1.95f, 4.25f, Style.Rock, 41),
            new Platform(3.3f, 5.6f, 2.2f, Style.Capital, 53),
            new Platform(7.3f, 10.1f, 4.05f, Style.Rock, 67),
            new Platform(10.1f, 14.9f, 2.1f, Style.Terrace, 79),
        };

        /// <summary>Bumped whenever the layout changes, so stale platform indices can be recognised.</summary>
        public static int Version { get; private set; }

        public static void Set(Platform[] layout)
        {
            Platforms = layout;
            Version++;
            foreach (var p in Platforms) { p.Step(time); p.Delta = Vector2.zero; }
        }

        /// <summary>Advance the moving platforms (game time: they freeze with the game).</summary>
        public static void Update(float dt)
        {
            time += dt;
            foreach (var p in Platforms) p.Step(time);
        }

        public static Vector2 DeltaOf(int index) => index >= 0 && index < Platforms.Length ? Platforms[index].Delta : Vector2.zero;

        /// <summary>
        /// The highest surface under x whose top is at or below y (the pitch if nothing else).
        /// halfW widens the probe (feet), ignore skips one platform (dropping through it) or all.
        /// A platform that rose this step still counts if it was below y before the step.
        /// </summary>
        public static float FloorBelow(float x, float y, float halfW, int ignore, out int index)
        {
            float best = Ground;
            index = None;
            if (ignore == All) return best;
            for (int i = 0; i < Platforms.Length; i++)
            {
                if (i == ignore) continue;
                var p = Platforms[i];
                if (x + halfW < p.X0 || x - halfW > p.X1) continue;
                if (p.Y - Mathf.Max(0f, p.Delta.y) <= y && p.Y > best) { best = p.Y; index = i; }
            }
            return best;
        }

        public static float FloorBelow(float x, float y, float halfW = 0f) => FloorBelow(x, y, halfW, None, out _);

        /// <summary>Nearest standing spot on a platform to x, kept a little inside its edges.</summary>
        public static float ClampOnto(Platform p, float x, float margin = 0.35f)
            => Mathf.Clamp(x, p.X0 + margin, p.X1 - margin);

        // ------------------------------------------------------------------ generated layouts

        const float Edge = 14.8f;          // platforms stay inside the arena walls
        const float MaxStep = 2.15f;       // highest rise between two levels a plain jump always makes

        static bool Floating(Style s) => s == Style.Rock || s == Style.Crystal || s == Style.Block || s == Style.Plank;

        /// <summary>
        /// A layout for one stage: a profile (sparse / medium / crowded, small / mixed / wide, still /
        /// some moving / many moving) is rolled first, then low levels are spread over the pitch, higher
        /// ones placed beside them within one jump, and floating pieces get their motion.
        /// </summary>
        public static Platform[] Generate(Style[] styles, int seed)
        {
            var r = new System.Random(seed);
            float R() => (float)r.NextDouble();
            float Range(float a, float b) => a + (b - a) * R();

            if (styles == null || styles.Length == 0) styles = new[] { Style.Terrace, Style.Capital, Style.Rock };
            var floating = new List<Style>();
            foreach (var s in styles) if (Floating(s)) floating.Add(s);
            if (floating.Count == 0) floating.Add(Style.Rock);

            int density = r.Next(3);       // 0 sparse, 1 medium, 2 crowded
            int size = r.Next(3);          // 0 small, 1 mixed, 2 wide
            int motion = r.Next(3);        // 0 still, 1 some, 2 many
            float Width()
            {
                switch (size)
                {
                    case 0: return Range(1.7f, 2.6f);
                    case 2: return Range(3.4f, 5.4f);
                    default: return R() < 0.5f ? Range(1.8f, 2.8f) : Range(3.2f, 4.6f);
                }
            }

            var low = new List<Platform>();
            var all = new List<Platform>();
            var supportY = new Dictionary<Platform, float>();

            // low levels: spread across the pitch, one per slot (a crowded stage sometimes leaves a hole)
            int lowCount = density == 0 ? 2 : density == 1 ? 3 : 4;
            float slot = Edge * 2f / lowCount;
            int skip = density >= 1 && R() < 0.3f ? r.Next(lowCount) : -1;
            for (int i = 0; i < lowCount; i++)
            {
                if (i == skip) continue;
                float w = Mathf.Min(Width(), slot - 1.9f);
                float room = slot - w - 1.9f;
                float cx = -Edge + slot * (i + 0.5f) + (R() - 0.5f) * room;
                float y = Range(1.9f, 2.35f);
                var st = styles[r.Next(styles.Length)];
                var p = new Platform(cx - w * 0.5f, cx + w * 0.5f, y, st, r.Next(1, 9999));
                low.Add(p); all.Add(p); supportY[p] = 0f;
            }

            // higher levels: beside a lower one, never more than one jump up and one step across
            Platform PlaceAbove(List<Platform> anchors, float minY, float maxY)
            {
                for (int attempt = 0; attempt < 24; attempt++)
                {
                    var a = anchors[r.Next(anchors.Count)];
                    float w = Mathf.Clamp(Width() * 0.9f, 1.7f, 4.8f);
                    float side = R() < 0.5f ? -1f : 1f;
                    float cx = a.Center + side * (a.Width * 0.5f + w * 0.5f + Range(-0.9f, 1.1f));
                    cx = Mathf.Clamp(cx, -Edge + w * 0.5f, Edge - w * 0.5f);
                    // keep within a step across of the anchor (clamping at the wall may have pulled it)
                    float gap = Mathf.Abs(cx - a.Center) - (a.Width + w) * 0.5f;
                    if (gap > 1.3f) continue;
                    float y = a.Y + Range(1.8f, MaxStep);
                    if (y < minY || y > maxY) continue;
                    bool clash = false;
                    foreach (var o in all)
                    {
                        if (Mathf.Abs(o.Y - y) > 1.5f) continue;
                        if (cx + w * 0.5f + 1.3f > o.Reach0 && cx - w * 0.5f - 1.3f < o.Reach1) { clash = true; break; }
                    }
                    if (clash) continue;
                    var p = new Platform(cx - w * 0.5f, cx + w * 0.5f, y, floating[r.Next(floating.Count)], r.Next(1, 9999));
                    supportY[p] = a.Y;
                    all.Add(p);
                    return p;
                }
                return null;
            }

            var high = new List<Platform>();
            int highCount = density == 0 ? 1 + r.Next(2) : density == 1 ? 2 + r.Next(2) : 3 + r.Next(2);
            for (int i = 0; i < highCount && low.Count > 0; i++)
            {
                var p = PlaceAbove(low, 3.6f, 4.6f);
                if (p != null) high.Add(p);
            }
            if (high.Count > 0 && (density == 2 ? R() < 0.6f : density == 1 && R() < 0.25f))
                PlaceAbove(high, 5.5f, 6.5f);

            // motion: only floating pieces move; every moving stage has at least one mover
            float chance = motion == 0 ? 0f : motion == 1 ? 0.35f : 0.75f;
            bool any = false;
            var order = new List<Platform>(all);
            for (int i = order.Count - 1; i > 0; i--) { int j = r.Next(i + 1); (order[i], order[j]) = (order[j], order[i]); }
            foreach (var p in order)
            {
                if (!Floating(p.Kind)) continue;
                if (!(R() < chance || (motion > 0 && !any))) continue;
                bool vertical = p.Kind == Style.Plank ? R() < 0.6f : R() < 0.35f;
                if (!vertical && TrySwing(p, all, Range(0.9f, 2.3f), Range(5f, 9f), R() * MathUtil.Tau)) { any = true; continue; }
                var lift = Lift(p, supportY[p], all, Range(0.5f, 1.2f), Range(5f, 8f), R() * MathUtil.Tau);
                if (lift != null)
                {
                    all[all.IndexOf(p)] = lift;
                    supportY[lift] = supportY[p];
                    any = true;
                }
                else if (vertical && TrySwing(p, all, Range(0.9f, 2f), Range(5f, 9f), R() * MathUtil.Tau)) any = true;
            }
            all.Sort((a, b) => a.BaseY != b.BaseY ? a.BaseY.CompareTo(b.BaseY) : a.BaseX0.CompareTo(b.BaseX0));
            return all.ToArray();
        }

        /// <summary>Glide sideways as far as the neighbours on the same level allow.</summary>
        static bool TrySwing(Platform p, List<Platform> all, float amp, float period, float phase)
        {
            float room = amp;
            foreach (var o in all)
            {
                if (o == p || Mathf.Abs(o.BaseY - p.BaseY) > 1.5f) continue;
                if (o.BaseX0 >= p.BaseX1) room = Mathf.Min(room, o.Reach0 - p.BaseX1 - 1.1f);
                else if (o.BaseX1 <= p.BaseX0) room = Mathf.Min(room, p.BaseX0 - o.Reach1 - 1.1f);
                else return false;
            }
            room = Mathf.Min(room, Mathf.Min(p.BaseX0 + Edge, Edge - p.BaseX1));
            if (room < 0.7f) return false;
            p.Move = Motion.Horizontal; p.Amp = room; p.Period = period; p.Phase = phase;
            return true;
        }

        /// <summary>
        /// A lift: at its lowest point it is one jump above the level it is reached from (with head
        /// room underneath), from there it rides up — at most until it would meet a platform above.
        /// Returns the moving replacement, or null if there is no room.
        /// </summary>
        static Platform Lift(Platform p, float below, List<Platform> all, float amp, float period, float phase)
        {
            float low = Mathf.Clamp(p.BaseY - amp, below + 1.7f, below + 2.2f);
            float high = Mathf.Min(low + 2f * amp, 6.6f);
            foreach (var o in all)
            {
                if (o == p || o.Reach1 + 0.5f < p.BaseX0 || o.Reach0 - 0.5f > p.BaseX1) continue;
                if (o.BaseY > low + 0.3f) high = Mathf.Min(high, o.BaseY - (o.Moving ? o.Amp : 0f) - 1.6f);
            }
            amp = (high - low) * 0.5f;
            if (amp < 0.3f) return null;
            return new Platform(p.BaseX0, p.BaseX1, low + amp, p.Kind, p.Seed) { Move = Motion.Vertical, Amp = amp, Period = period, Phase = phase };
        }
    }
}
