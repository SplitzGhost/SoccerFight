using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SoccerFight
{
    /// <summary>
    /// Smooth follow with look-ahead, trauma-based Perlin shake (smooth, never jittery),
    /// directional recoil kicks and zoom punches.
    /// </summary>
    public sealed class CameraRig
    {
        public Camera Cam { get; private set; }
        public float BaseSize = 4.9f;
        public float MinX = -9.5f, MaxX = 9.5f;
        public float BaseY = 3.0f;

        Vector2 pos, vel;
        float zoom = 1f, zoomVel, zoomTarget = 1f;
        float trauma;
        Vector2 kick, kickVel;
        float noiseT;

        // debug / capture override
        bool overrideActive;
        Vector2 overridePos;
        float overrideSize;

        public void Init(Transform parent)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.SetParent(parent, false);
            Cam = go.AddComponent<Camera>();
            Cam.orthographic = true;
            Cam.orthographicSize = BaseSize;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = Palette.SkyTop;
            Cam.nearClipPlane = 0.1f;
            Cam.farClipPlane = 100f;
            Cam.allowHDR = true;
            Cam.allowMSAA = false;
            var data = Cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.None;
            go.AddComponent<AudioListener>();
            pos = new Vector2(0f, BaseY);
            Apply(Vector2.zero, 0f);
        }

        public void Snap(Vector2 target) { pos = Target(target, Vector2.zero); vel = Vector2.zero; }

        public void AddTrauma(float amount) => trauma = Mathf.Clamp01(trauma + amount);

        public void Kick(Vector2 impulse) => kickVel += impulse;

        /// <summary>Negative = zoom in. Springs back automatically.</summary>
        public void ZoomPunch(float amount) => zoomVel += amount * 12f;

        public void SetZoom(float z) => zoomTarget = z;

        public void SetOverride(Vector2 center, float size) { overrideActive = true; overridePos = center; overrideSize = size; }
        public void ClearOverride() => overrideActive = false;

        Vector2 Target(Vector2 player, Vector2 lookAhead)
        {
            Vector2 t = new Vector2(player.x, BaseY + Mathf.Max(0f, player.y - 1.2f) * 0.45f) + lookAhead;
            t.x = Mathf.Clamp(t.x, MinX, MaxX);
            return t;
        }

        public void Update(Vector2 player, Vector2 lookAhead, float dt, float unscaledDt)
        {
            Vector2 target = Target(player, lookAhead);
            pos.x = Mathf.SmoothDamp(pos.x, target.x, ref vel.x, 0.16f, Mathf.Infinity, dt);
            pos.y = Mathf.SmoothDamp(pos.y, target.y, ref vel.y, 0.28f, Mathf.Infinity, dt);

            MathUtil.Spring(ref zoom, ref zoomVel, zoomTarget, 2.2f, 0.75f, unscaledDt);
            MathUtil.Spring(ref kick, ref kickVel, Vector2.zero, 5f, 0.55f, unscaledDt);

            trauma = Mathf.Max(0f, trauma - unscaledDt * 1.5f);
            noiseT += unscaledDt;
            Apply(pos, unscaledDt);
        }

        void Apply(Vector2 p, float dt)
        {
            float shake = trauma * trauma * GameSettings.ScreenShake;
            const float freq = 22f;
            Vector2 shakeOffset = new Vector2(
                (Mathf.PerlinNoise(noiseT * freq, 0.3f) - 0.5f) * 2f,
                (Mathf.PerlinNoise(0.7f, noiseT * freq) - 0.5f) * 2f) * (0.42f * shake);
            float rot = (Mathf.PerlinNoise(noiseT * freq * 0.8f, 5.1f) - 0.5f) * 2f * 1.6f * shake;

            Vector2 final = p + shakeOffset + kick * Mathf.Min(1f, GameSettings.ScreenShake);
            float size = BaseSize * zoom;
            if (overrideActive) { final = overridePos; size = overrideSize; rot = 0f; }

            Cam.transform.position = new Vector3(final.x, final.y, -20f);
            Cam.transform.rotation = Quaternion.Euler(0f, 0f, rot);
            Cam.orthographicSize = size;
        }

        public Vector2 Position => Cam.transform.position;

        public Rect ViewRect
        {
            get
            {
                float h = Cam.orthographicSize, w = h * Cam.aspect;
                Vector2 c = Cam.transform.position;
                return new Rect(c.x - w, c.y - h, w * 2f, h * 2f);
            }
        }
    }
}
