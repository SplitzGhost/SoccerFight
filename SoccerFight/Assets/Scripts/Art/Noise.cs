using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Pure C# 2D Perlin noise (range ~0..1, like Mathf.PerlinNoise). Thread-safe and much cheaper
    /// than the native call when evaluated millions of times during texture generation.
    /// </summary>
    public static class Noise
    {
        static readonly int[] Perm = BuildPerm();

        static int[] BuildPerm()
        {
            var p = new int[512];
            var rng = new System.Random(1337);
            var src = new int[256];
            for (int i = 0; i < 256; i++) src[i] = i;
            for (int i = 255; i > 0; i--) { int j = rng.Next(i + 1); (src[i], src[j]) = (src[j], src[i]); }
            for (int i = 0; i < 512; i++) p[i] = src[i & 255];
            return p;
        }

        static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        static float Grad(int hash, float x, float y)
        {
            switch (hash & 7)
            {
                case 0: return x + y;
                case 1: return -x + y;
                case 2: return x - y;
                case 3: return -x - y;
                case 4: return x;
                case 5: return -x;
                case 6: return y;
                default: return -y;
            }
        }

        public static float Perlin(float x, float y)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = x - xi, yf = y - yi;
            xi &= 255; yi &= 255;
            float u = Fade(xf), v = Fade(yf);
            int aa = Perm[Perm[xi] + yi], ab = Perm[Perm[xi] + yi + 1];
            int ba = Perm[Perm[xi + 1] + yi], bb = Perm[Perm[xi + 1] + yi + 1];
            float x1 = Mathf.Lerp(Grad(aa, xf, yf), Grad(ba, xf - 1f, yf), u);
            float x2 = Mathf.Lerp(Grad(ab, xf, yf - 1f), Grad(bb, xf - 1f, yf - 1f), u);
            return Mathf.Clamp01(Mathf.Lerp(x1, x2, v) * 0.5f + 0.5f);
        }
    }
}
