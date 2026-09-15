using UnityEngine;

namespace SoccerFight
{
    /// <summary>2D signed distance functions (negative inside). All inputs in world units.</summary>
    public static class Sdf
    {
        public static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;

        /// <summary>Approximate ellipse distance (good enough for anti-aliasing).</summary>
        public static float Ellipse(Vector2 p, Vector2 c, Vector2 radii)
        {
            Vector2 q = p - c;
            float k0 = new Vector2(q.x / radii.x, q.y / radii.y).magnitude;
            float k1 = new Vector2(q.x / (radii.x * radii.x), q.y / (radii.y * radii.y)).magnitude;
            if (k1 < 1e-6f) return -Mathf.Min(radii.x, radii.y);
            return k0 * (k0 - 1f) / k1;
        }

        public static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Mathf.Max(1e-8f, Vector2.Dot(ba, ba)));
            return (pa - ba * h).magnitude;
        }

        public static float Capsule(Vector2 p, Vector2 a, Vector2 b, float r) => Segment(p, a, b) - r;

        /// <summary>Capsule whose radius changes from ra (at a) to rb (at b).</summary>
        public static float Tapered(Vector2 p, Vector2 a, float ra, Vector2 b, float rb)
        {
            Vector2 ba = b - a;
            float h = ba.magnitude;
            if (h < 1e-5f) return (p - a).magnitude - Mathf.Max(ra, rb);
            Vector2 dir = ba / h;
            Vector2 q = p - a;
            float y = Vector2.Dot(q, dir);
            float x = Mathf.Abs(dir.x * q.y - dir.y * q.x);
            float bb = (ra - rb) / h;
            float aa = Mathf.Sqrt(Mathf.Max(0f, 1f - bb * bb));
            float k = -bb * x + aa * y;
            if (k < 0f) return Mathf.Sqrt(x * x + y * y) - ra;
            if (k > aa * h) return Mathf.Sqrt(x * x + (y - h) * (y - h)) - rb;
            return x * aa + y * bb - ra;
        }

        public static float Box(Vector2 p, Vector2 c, Vector2 half, float radius = 0f, float angleDeg = 0f)
        {
            Vector2 q = p - c;
            if (angleDeg != 0f) q = MathUtil.Rotate(q, -angleDeg);
            Vector2 h = half - new Vector2(radius, radius);
            Vector2 d = new Vector2(Mathf.Abs(q.x) - h.x, Mathf.Abs(q.y) - h.y);
            Vector2 dm = new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f));
            return dm.magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f) - radius;
        }

        public static float Ring(Vector2 p, Vector2 c, float r, float thickness)
            => Mathf.Abs((p - c).magnitude - r) - thickness * 0.5f;

        public static float Triangle(Vector2 p, Vector2 p0, Vector2 p1, Vector2 p2)
        {
            Vector2 e0 = p1 - p0, e1 = p2 - p1, e2 = p0 - p2;
            Vector2 v0 = p - p0, v1 = p - p1, v2 = p - p2;
            Vector2 pq0 = v0 - e0 * Mathf.Clamp01(Vector2.Dot(v0, e0) / Vector2.Dot(e0, e0));
            Vector2 pq1 = v1 - e1 * Mathf.Clamp01(Vector2.Dot(v1, e1) / Vector2.Dot(e1, e1));
            Vector2 pq2 = v2 - e2 * Mathf.Clamp01(Vector2.Dot(v2, e2) / Vector2.Dot(e2, e2));
            float s = Mathf.Sign(e0.x * e2.y - e0.y * e2.x);
            Vector2 d0 = new Vector2(Vector2.Dot(pq0, pq0), s * (v0.x * e0.y - v0.y * e0.x));
            Vector2 d1 = new Vector2(Vector2.Dot(pq1, pq1), s * (v1.x * e1.y - v1.y * e1.x));
            Vector2 d2 = new Vector2(Vector2.Dot(pq2, pq2), s * (v2.x * e2.y - v2.y * e2.x));
            Vector2 d = Vector2.Min(Vector2.Min(d0, d1), d2);
            return -Mathf.Sqrt(d.x) * Mathf.Sign(d.y);
        }

        /// <summary>Distance to a half plane: negative on the side the normal points away from.</summary>
        public static float HalfPlane(Vector2 p, Vector2 point, Vector2 normal) => Vector2.Dot(p - point, normal.normalized);

        public static float Union(float a, float b) => Mathf.Min(a, b);
        public static float Subtract(float a, float b) => Mathf.Max(a, -b);
        public static float Intersect(float a, float b) => Mathf.Max(a, b);

        public static float SmoothUnion(float a, float b, float k)
        {
            float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
            return Mathf.Lerp(b, a, h) - k * h * (1f - h);
        }

        public static float SmoothSubtract(float a, float b, float k)
        {
            float h = Mathf.Clamp01(0.5f - 0.5f * (a + b) / k);
            return Mathf.Lerp(a, -b, h) + k * h * (1f - h);
        }

        /// <summary>Four-pointed sparkle star.</summary>
        public static float Star4(Vector2 p, Vector2 c, float r, float thin)
        {
            Vector2 q = p - c;
            q = new Vector2(Mathf.Abs(q.x), Mathf.Abs(q.y));
            // Astroid-like concave star: |x|^k + |y|^k = r^k with small k
            float k = thin;
            float v = Mathf.Pow(q.x / r, k) + Mathf.Pow(q.y / r, k);
            return (Mathf.Pow(v, 1f / k) - 1f) * r * 0.35f;
        }
    }
}
