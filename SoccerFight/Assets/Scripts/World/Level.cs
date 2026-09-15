using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Walkable geometry: the pitch at y = 0 plus one-way platforms (a ruined terrace on each side,
    /// broken column capitals and floating mossy rocks). Everything that stands, lands or bounces —
    /// player, blobs, ball, shadows, grass interaction — asks here instead of assuming a flat floor.
    /// Platforms are jumped through from below and dropped through with the down key.
    /// Pure data, so the art workers can read it too.
    /// </summary>
    public static class Level
    {
        public enum Style { Terrace, Capital, Rock }

        public readonly struct Platform
        {
            public readonly float X0, X1, Y;
            public readonly Style Kind;
            public readonly int Seed;

            public Platform(float x0, float x1, float y, Style kind, int seed)
            {
                X0 = x0; X1 = x1; Y = y; Kind = kind; Seed = seed;
            }

            public float Center => (X0 + X1) * 0.5f;
            public float Width => X1 - X0;
        }

        // low levels (~2.2) are one jump from the pitch, the rocks (~4.1) one jump from a low level
        public static readonly Platform[] Platforms =
        {
            new Platform(-14.8f, -10.0f, 2.1f, Style.Terrace, 11),
            new Platform(-8.9f, -6.2f, 4.1f, Style.Rock, 23),
            new Platform(-4.75f, -2.45f, 2.25f, Style.Capital, 37),
            new Platform(-0.95f, 1.95f, 4.25f, Style.Rock, 41),
            new Platform(3.3f, 5.6f, 2.2f, Style.Capital, 53),
            new Platform(7.3f, 10.1f, 4.05f, Style.Rock, 67),
            new Platform(10.1f, 14.9f, 2.1f, Style.Terrace, 79),
        };

        public const float Ground = 0f;
        public const int None = -1;
        /// <summary>Pass as "ignore" to fall through every platform (down key held in the air).</summary>
        public const int All = -2;

        /// <summary>
        /// The highest surface under x whose top is at or below y (the pitch if nothing else).
        /// halfW widens the probe (feet), ignore skips one platform (dropping through it) or all.
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
                if (p.Y <= y && p.Y > best) { best = p.Y; index = i; }
            }
            return best;
        }

        public static float FloorBelow(float x, float y, float halfW = 0f) => FloorBelow(x, y, halfW, None, out _);

        /// <summary>Nearest standing spot on a platform to x, kept a little inside its edges.</summary>
        public static float ClampOnto(in Platform p, float x, float margin = 0.35f)
            => Mathf.Clamp(x, p.X0 + margin, p.X1 - margin);
    }
}
