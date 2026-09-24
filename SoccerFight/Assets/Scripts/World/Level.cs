using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Walkable geometry: the pitch at y = 0 plus one-way platforms. Every stage generates its layout from
    /// its own platform pictures — sometimes sparse, sometimes crowded, small or large, still or gliding back
    /// and forth / up and down (stage 1 always the same). Everything
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
            /// <summary>Which picture of the stage kit draws it (index into its platform list) and at what size.</summary>
            public int Piece = -1;
            public float Scale = 1f;
            public bool Flip;
            /// <summary>A ledge inside another platform's picture (the plank in a swing frame): drawn by its owner.</summary>
            public Platform Owner;
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
                if (Owner != null) { Delta = Owner.Delta; X0 = BaseX0 + Owner.Offset.x; X1 = BaseX1 + Owner.Offset.x; Y = BaseY + Owner.Offset.y; return; }
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
        static float drift;

        /// <summary>The platforms' clock (duo: the partner's screen follows the host's).</summary>
        public static float Clock => time;

        /// <summary>Shift the clock on the next update (small steps, so riders are carried along).</summary>
        public static void Nudge(float seconds) => drift += seconds;

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
            time += dt + drift;
            drift = 0f;
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

        /// <summary>
        /// A layout for one stage, built from the stage's own platform pictures: a profile (sparse / medium /
        /// crowded, small / mixed / large pieces, still / some moving / many moving) is rolled first, then low
        /// levels are spread over the pitch — standing pieces (pedestals, frames, stumps) scaled so their top is
        /// one jump high, or low floating ones —, higher ones placed beside them within one jump, and floating
        /// pieces get their motion. Pictures are only ever scaled evenly, so a platform's size follows its art.
        /// </summary>
        public static Platform[] Generate(StageKit kit, int seed)
        {
            var r = new System.Random(seed);
            float R() => (float)r.NextDouble();
            float Range(float a, float b) => a + (b - a) * R();

            var pieces = kit.Platforms;
            var stands = new List<int>();
            var floats = new List<int>();
            for (int i = 0; i < pieces.Count; i++) (pieces[i].Role == StageKit.Role.Stand ? stands : floats).Add(i);
            if (pieces.Count == 0) return Classic();

            int density = r.Next(3);       // 0 sparse, 1 medium, 2 crowded
            int size = r.Next(3);          // 0 small, 1 mixed, 2 large
            int motion = r.Next(3);        // 0 still, 1 some, 2 many
            float FloatScale()
            {
                switch (size)
                {
                    case 0: return Range(0.78f, 0.95f);
                    case 2: return Range(1.05f, 1.25f);
                    default: return Range(0.82f, 1.2f);
                }
            }

            var all = new List<Platform>();
            var supportY = new Dictionary<Platform, float>();
            // horizontal room a platform's picture needs (walk span plus overhang)
            float Half(StageKit.Piece pc, float s) => Mathf.Max(pc.WalkWidth, pc.Width * 0.85f) * 0.5f * s;
            var halfOf = new Dictionary<Platform, float>();

            Platform Make(int pi, float s, float cx, float y)
            {
                var pc = pieces[pi];
                float w = pc.WalkWidth * s;
                var p = new Platform(cx - w * 0.5f, cx + w * 0.5f, y, pc.Role == StageKit.Role.Stand ? Style.Capital : Style.Rock, r.Next(1, 9999))
                    { Piece = pi, Scale = s, Flip = !pc.Hangs && r.Next(2) == 0 };
                halfOf[p] = Half(pc, s);
                return p;
            }
            void AddLedges(Platform p)
            {
                var pc = pieces[p.Piece];
                foreach (var l in pc.Ledges)
                {
                    float a = l.X0 * p.Scale, b = l.X1 * p.Scale;
                    if (p.Flip) { float t = -a; a = -b; b = t; }
                    var q = new Platform(p.Center + a, p.Center + b, p.BaseY + l.Y * p.Scale, p.Kind, p.Seed + 1) { Owner = p, Piece = p.Piece, Scale = p.Scale, Flip = p.Flip };
                    halfOf[q] = (b - a) * 0.5f;
                    all.Add(q); supportY[q] = 0f;
                }
            }
            bool Clear(float cx, float half, float y, float gap, float band = 1.5f)
            {
                foreach (var o in all)
                {
                    if (Mathf.Abs(o.BaseY - y) > band) continue;
                    float oh = halfOf.TryGetValue(o, out var h) ? h : o.Width * 0.5f;
                    float sw = o.Move == Motion.Horizontal ? o.Amp : 0f;
                    if (cx + half + gap > o.Center - oh - sw && cx - half - gap < o.Center + oh + sw) return false;
                }
                return true;
            }

            // low levels: spread across the pitch, one per slot (a crowded stage sometimes leaves a hole)
            int lowCount = density == 0 ? 2 : density == 1 ? 3 : 4;
            float slot = Edge * 2f / lowCount;
            int skip = density >= 1 && R() < 0.3f ? r.Next(lowCount) : -1;
            for (int i = 0; i < lowCount; i++)
            {
                if (i == skip) continue;
                for (int attempt = 0; attempt < 40; attempt++)
                {
                    bool stand = stands.Count > 0 && (floats.Count == 0 || R() < 0.72f);
                    int pi = stand ? stands[r.Next(stands.Count)] : floats[r.Next(floats.Count)];
                    var pc = pieces[pi];
                    float s, y;
                    if (stand)
                    {
                        if (pc.Ledges.Length > 0)
                        {
                            // a frame: the inner ledge is the low level, the top one jump above it
                            float ledgeH = pc.Height + pc.Ledges[0].Y;
                            s = Range(1.85f, 2.3f) / ledgeH;
                            y = pc.Height * s;
                            if (s < 0.5f || s > 1.3f || y < 3.6f || y > 4.8f) continue;
                        }
                        else
                        {
                            s = Mathf.Clamp(Range(1.85f, 2.35f) / pc.Height, 0.6f, 1.3f);
                            y = pc.Height * s;
                            if (y < 1.7f || y > 2.45f) continue;
                        }
                    }
                    else
                    {
                        // low floating pieces are drawn smaller when their underside (crystals, rebar, ropes)
                        // would otherwise reach down into the pitch
                        y = Range(1.95f, 2.35f);
                        s = Mathf.Min(FloatScale(), (y - 0.6f) / Mathf.Max(0.1f, pc.BottomH));
                        if (s < 0.7f) continue;
                    }
                    float half = Half(pc, s);
                    if (half * 2f > slot - 1.7f) continue;
                    float room = slot - half * 2f - 1.7f;
                    float cx = -Edge + slot * (i + 0.5f) + (R() - 0.5f) * room;
                    if (!Clear(cx, half, y, 1.2f)) continue;
                    var p = Make(pi, s, cx, y);
                    all.Add(p); supportY[p] = 0f;
                    if (pc.Ledges.Length > 0) AddLedges(p);
                    break;
                }
            }
            var low = new List<Platform>();
            foreach (var p in all) if (p.BaseY < 3f) low.Add(p);

            // higher levels: beside a lower one, never more than one jump up and one step across
            Platform PlaceAbove(List<Platform> anchors, float minY, float maxY, bool allowStand)
            {
                if (anchors.Count == 0) return null;
                for (int attempt = 0; attempt < 30; attempt++)
                {
                    var a = anchors[r.Next(anchors.Count)];
                    bool stand = allowStand && stands.Count > 0 && (floats.Count == 0 || R() < 0.3f);
                    if (!stand && floats.Count == 0) return null;
                    int pi = stand ? stands[r.Next(stands.Count)] : floats[r.Next(floats.Count)];
                    var pc = pieces[pi];
                    if (stand && pc.Ledges.Length > 0) continue;
                    float y = a.BaseY + Range(1.8f, MaxStep);
                    if (y < minY || y > maxY) continue;
                    float s = stand ? y / pc.Height : FloatScale();
                    if (stand && (s < 0.6f || s > 1.35f)) continue;
                    float half = Half(pc, s), ah = halfOf.TryGetValue(a, out var hh) ? hh : a.Width * 0.5f;
                    float side = R() < 0.5f ? -1f : 1f;
                    float cx = a.Center + side * (ah + half + Range(-0.9f, 1.1f));
                    cx = Mathf.Clamp(cx, -Edge + half, Edge - half);
                    float gap = Mathf.Abs(cx - a.Center) - (a.Width + pc.WalkWidth * s) * 0.5f;
                    if (gap > 1.3f) continue;
                    if (!Clear(cx, half, y, 1.3f)) continue;
                    // a standing piece reaches down to the pitch: nothing may stand in its way
                    if (stand && !Clear(cx, half, y * 0.5f, 0.4f, y * 0.5f + 0.2f)) continue;
                    var p = Make(pi, s, cx, y);
                    supportY[p] = a.BaseY;
                    all.Add(p);
                    return p;
                }
                return null;
            }

            var high = new List<Platform>();
            foreach (var p in all) if (p.BaseY >= 3f) high.Add(p);   // frame tops
            int highCount = density == 0 ? 1 + r.Next(2) : density == 1 ? 2 + r.Next(2) : 3 + r.Next(2);
            for (int i = 0; i < highCount && low.Count > 0; i++)
            {
                var p = PlaceAbove(low, 3.6f, 4.6f, true);
                if (p != null) high.Add(p);
            }
            if (high.Count > 0 && (density == 2 ? R() < 0.6f : density == 1 && R() < 0.25f))
                PlaceAbove(high, 5.5f, 6.5f, false);

            // motion: only floating pieces move; every moving stage has at least one mover
            float chance = motion == 0 ? 0f : motion == 1 ? 0.35f : 0.75f;
            bool any = false;
            var order = new List<Platform>(all);
            for (int i = order.Count - 1; i > 0; i--) { int j = r.Next(i + 1); (order[i], order[j]) = (order[j], order[i]); }
            foreach (var p in order)
            {
                if (p.Owner != null || pieces[p.Piece].Role != StageKit.Role.Float) continue;
                if (!(R() < chance || (motion > 0 && !any))) continue;
                bool vertical = pieces[p.Piece].Hangs ? R() < 0.6f : R() < 0.35f;
                if (!vertical && TrySwing(p, all, Range(0.9f, 2.3f), Range(5f, 9f), R() * MathUtil.Tau)) { any = true; continue; }
                var lift = Lift(p, supportY[p], all, Range(0.5f, 1.2f), Range(5f, 8f), R() * MathUtil.Tau);
                if (lift != null)
                {
                    all[all.IndexOf(p)] = lift;
                    supportY[lift] = supportY[p];
                    halfOf[lift] = halfOf[p];
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
            return new Platform(p.BaseX0, p.BaseX1, low + amp, p.Kind, p.Seed) { Move = Motion.Vertical, Amp = amp, Period = period, Phase = phase, Piece = p.Piece, Scale = p.Scale, Flip = p.Flip };
        }
    }
}
