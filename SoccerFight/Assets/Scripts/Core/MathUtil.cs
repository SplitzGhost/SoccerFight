using UnityEngine;

namespace SoccerFight
{
    /// <summary>Springs, easing curves, IK and curve helpers used by all animation code.</summary>
    public static class MathUtil
    {
        public const float Tau = Mathf.PI * 2f;

        // ---------------------------------------------------------------- springs

        /// <summary>
        /// Implicit-Euler damped spring. Unconditionally stable for any dt, so motion stays smooth
        /// at every frame rate. freq in Hz, zeta = damping ratio (1 = critically damped).
        /// </summary>
        public static void Spring(ref float x, ref float v, float target, float freq, float zeta, float dt)
        {
            float omega = Tau * freq;
            float f = 1f + 2f * dt * zeta * omega;
            float oo = omega * omega;
            float hoo = dt * oo;
            float hhoo = dt * hoo;
            float detInv = 1f / (f + hhoo);
            float detX = f * x + dt * v + hhoo * target;
            float detV = v + hoo * (target - x);
            x = detX * detInv;
            v = detV * detInv;
        }

        public static void Spring(ref Vector2 x, ref Vector2 v, Vector2 target, float freq, float zeta, float dt)
        {
            Spring(ref x.x, ref v.x, target.x, freq, zeta, dt);
            Spring(ref x.y, ref v.y, target.y, freq, zeta, dt);
        }

        /// <summary>Frame-rate independent exponential smoothing.</summary>
        public static float Damp(float current, float target, float lambda, float dt)
            => Mathf.Lerp(current, target, 1f - Mathf.Exp(-lambda * dt));

        public static Vector2 Damp(Vector2 current, Vector2 target, float lambda, float dt)
            => Vector2.Lerp(current, target, 1f - Mathf.Exp(-lambda * dt));

        public static float DampAngle(float current, float target, float lambda, float dt)
            => Mathf.LerpAngle(current, target, 1f - Mathf.Exp(-lambda * dt));

        // ---------------------------------------------------------------- easing

        public static float Smooth01(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
        public static float Smoother01(float t) { t = Mathf.Clamp01(t); return t * t * t * (t * (6f * t - 15f) + 10f); }
        public static float EaseOutCubic(float t) { t = 1f - Mathf.Clamp01(t); return 1f - t * t * t; }
        public static float EaseInCubic(float t) { t = Mathf.Clamp01(t); return t * t * t; }
        public static float EaseOutQuad(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t); }
        public static float EaseInQuad(float t) { t = Mathf.Clamp01(t); return t * t; }
        public static float EaseOutQuart(float t) { t = 1f - Mathf.Clamp01(t); return 1f - t * t * t * t; }
        public static float EaseOutExpo(float t) { t = Mathf.Clamp01(t); return t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t); }

        public static float EaseInOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
        }

        public static float EaseInOutSine(float t) => -(Mathf.Cos(Mathf.PI * Mathf.Clamp01(t)) - 1f) * 0.5f;

        public static float EaseOutBack(float t, float overshoot = 1.70158f)
        {
            t = Mathf.Clamp01(t) - 1f;
            return 1f + (overshoot + 1f) * t * t * t + overshoot * t * t;
        }

        public static float EaseOutElastic(float t)
        {
            t = Mathf.Clamp01(t);
            if (t <= 0f || t >= 1f) return t;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * (Tau / 3f)) + 1f;
        }

        /// <summary>0 → 1 → 0 bump over t in [0,1].</summary>
        public static float Bump(float t) { t = Mathf.Clamp01(t); return Mathf.Sin(t * Mathf.PI); }

        public static float Remap01(float v, float a, float b) => Mathf.Clamp01((v - a) / (b - a));

        // ---------------------------------------------------------------- curves

        public static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        public static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
        {
            float u = 1f - t;
            return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
        }

        public static Vector2 BezierTangent(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
        {
            float u = 1f - t;
            return 3f * u * u * (b - a) + 6f * u * t * (c - b) + 3f * t * t * (d - c);
        }

        // ---------------------------------------------------------------- geometry

        public static Vector2 Dir(float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
        }

        public static float Angle(Vector2 v) => Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;

        public static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        /// <summary>
        /// Zieht ein Arm-Ziel weich vor die volle Streckung: Bis 88 % der Armlänge bleibt es, darüber nähert es sich
        /// sanft 98 %. Ein Ziel an oder hinter der Grenze ließ den Ellbogen zwischen gebeugt und gestreckt zittern.
        /// </summary>
        public static Vector2 SoftReach(Vector2 root, Vector2 target, float reach)
        {
            Vector2 d = target - root;
            float dist = d.magnitude, knee = reach * 0.88f, room = reach * 0.1f;
            if (dist <= knee) return target;
            return root + d / dist * (knee + room * (float)System.Math.Tanh((dist - knee) / room));
        }

        /// <summary>
        /// Analytic two-bone IK. Returns the middle joint (knee/elbow). bendSign picks which side the
        /// joint bends to (+1 = counter-clockwise from root→target).
        /// </summary>
        public static Vector2 SolveTwoBone(Vector2 root, Vector2 target, float lenA, float lenB, float bendSign, out Vector2 end)
        {
            Vector2 d = target - root;
            float dist = d.magnitude;
            float maxReach = (lenA + lenB) * 0.9995f;
            float minReach = Mathf.Abs(lenA - lenB) + 0.001f;
            float clamped = Mathf.Clamp(dist, minReach, maxReach);
            Vector2 dir = dist > 1e-5f ? d / dist : Vector2.down;
            end = root + dir * clamped;

            float cosA = (lenA * lenA + clamped * clamped - lenB * lenB) / (2f * lenA * clamped);
            float a = Mathf.Acos(Mathf.Clamp(cosA, -1f, 1f));
            float baseAngle = Mathf.Atan2(dir.y, dir.x);
            float jointAngle = baseAngle + bendSign * a;
            return root + new Vector2(Mathf.Cos(jointAngle), Mathf.Sin(jointAngle)) * lenA;
        }

        /// <summary>Z rotation (degrees) that points a sprite's local -Y axis along dir.</summary>
        public static float DownAngle(Vector2 dir) => Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f;

        public static float Hash(int n)
        {
            n = (n << 13) ^ n;
            return 1f - ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 1073741824f;
        }

        public static Color WithAlpha(this Color c, float a) { c.a = a; return c; }

        public static Color Hsv(float h, float s, float v, float a = 1f)
        {
            Color c = Color.HSVToRGB(Mathf.Repeat(h, 1f), s, v);
            c.a = a;
            return c;
        }
    }
}
