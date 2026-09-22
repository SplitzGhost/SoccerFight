using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// The stage's special rule: wind gusts, telegraphed lightning, darkness, lava geysers, ice,
    /// low gravity or eclipse pulses. Strikes and geysers are also the weapons of some bosses.
    /// Hazards hurt monsters too, so reading them turns into positioning play.
    /// </summary>
    public sealed class StageMechanics
    {
        public static StageMechanics I { get; private set; }

        /// <summary>Global enemy boosts (eclipse pulses).</summary>
        public static float EnemySpeedBoost = 1f, EnemyDamageBoost = 1f;

        /// <summary>Signed wind force for this frame (units/s² at full gust).</summary>
        public float Wind { get; private set; }
        public bool EclipseActive => eclipseT > 0f;
        public StageMechanic Mechanic => mech;

        enum HazardKind { Strike, Geyser }

        sealed class Hazard
        {
            public HazardKind kind;
            public float x, floor, delay, t, dmg, erupt;
            public bool fired, active;
            public Transform root;
            public SpriteRenderer marker, ring, column;
        }

        readonly List<Hazard> hazards = new List<Hazard>();
        Transform parent;
        StageMechanic mech;
        float timer, gustT, gustDur, gustDir, eclipseT;
        bool running;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { I = null; EnemySpeedBoost = EnemyDamageBoost = 1f; }

        public void Build(Transform root)
        {
            I = this;
            parent = new GameObject("Stage Hazards").transform;
            parent.SetParent(root, false);
        }

        public void Begin(StageTheme theme)
        {
            mech = theme.Mechanic;
            timer = 4f;
            gustT = 0f;
            eclipseT = 0f;
            Wind = 0f;
            EnemySpeedBoost = EnemyDamageBoost = 1f;
        }

        /// <summary>Hazards only run while a wave is being fought.</summary>
        public void SetRunning(bool value)
        {
            running = value;
            if (!value) { Clear(); gustT = 0f; eclipseT = 0f; Wind = 0f; EnemySpeedBoost = EnemyDamageBoost = 1f; }
        }

        public void Clear()
        {
            foreach (var h in hazards) { h.active = false; h.root.gameObject.SetActive(false); }
        }

        Hazard Get()
        {
            foreach (var h in hazards) if (!h.active) return h;
            var root = new GameObject("Hazard").transform;
            root.SetParent(parent, false);
            var n = new Hazard
            {
                root = root,
                marker = Art.MakeSprite("Marker", root, Art.SoftGlow, -30, Art.SpriteGlowMat, Color.clear),
                ring = Art.MakeSprite("Ring", root, Art.Ring, -29, Art.SpriteGlowMat, Color.clear),
                column = Art.MakeSprite("Column", root, Art.LightRay, 125, Art.SpriteGlowMat, Color.clear),
            };
            hazards.Add(n);
            return n;
        }

        void Add(HazardKind kind, float x, float dmg, float delay)
        {
            var h = Get();
            h.kind = kind;
            h.x = Mathf.Clamp(x, -Player.ArenaHalf + 0.5f, Player.ArenaHalf - 0.5f);
            h.floor = Level.FloorBelow(h.x, 20f);
            h.delay = delay; h.t = 0f; h.dmg = dmg; h.erupt = 0f; h.fired = false; h.active = true;
            h.root.gameObject.SetActive(true);
            h.root.position = new Vector3(h.x, h.floor + 0.05f, 0f);
        }

        public void Strike(float x, float dmg, float delay = 1.1f) => Add(HazardKind.Strike, x, dmg, delay);
        public void Geyser(float x, float dmg, float delay = 1.1f) => Add(HazardKind.Geyser, x, dmg, delay);

        public void Update(float dt, Player player, Ball ball, WaveDirector waves)
        {
            Wind = 0f;
            if (running && !player.Dead) Tick(dt, player);
            UpdateHazards(dt, player, waves);
            // wind pushes everything that isn't nailed down
            if (Wind != 0f)
            {
                if (!player.IsDashing) player.Vel.x += Wind * 0.85f * dt;
                if (!ball.IsHeld) ball.Vel.x += Wind * 1.1f * dt;
                waves.Push(Wind * 0.7f * dt);
            }
        }

        void Tick(float dt, Player player)
        {
            var theme = Game.I.Run.Theme;
            switch (mech)
            {
                case StageMechanic.Wind:
                    if (gustT > 0f)
                    {
                        gustT -= dt;
                        float k = Mathf.Sin(Mathf.PI * Mathf.Clamp01(1f - gustT / gustDur));
                        Wind = gustDir * 11f * k;
                        if (Random.value < dt * 40f * k)
                        {
                            Vector2 cam = Game.I.Cam.Cam.transform.position;
                            Vector2 p = cam + new Vector2(-gustDir * 11f + Random.Range(-2f, 6f) * gustDir, Random.Range(-3f, 5f));
                            FxSystem.I.Streak(FxLayer.Front, p, new Vector2(gustDir * Random.Range(14f, 22f), Random.Range(-0.5f, 0.5f)), Random.Range(0.4f, 0.7f),
                                0.025f, 0.08f, new Color(1f, 0.92f, 0.8f, 0.35f), new Color(1f, 0.85f, 0.6f, 0f), 1.2f, 0.5f);
                        }
                    }
                    else if ((timer -= dt) <= 0f)
                    {
                        timer = Random.Range(7f, 10f);
                        gustDur = gustT = 3.6f;
                        gustDir = Random.value < 0.5f ? -1f : 1f;
                        Game.I.Hud.ShowToast(gustDir < 0f ? "←  WINDBÖE" : "WINDBÖE  →");
                    }
                    break;
                case StageMechanic.Lightning:
                    if ((timer -= dt) <= 0f)
                    {
                        timer = Random.Range(4.5f, 7f);
                        int n = Random.value < 0.35f ? 2 : 1;
                        for (int i = 0; i < n; i++) Strike(player.Pos.x + Random.Range(-4f, 4f), 18f * Difficulty.DamageMul(Game.I.Run.Level), 1.2f + i * 0.3f);
                    }
                    break;
                case StageMechanic.Geysers:
                    if ((timer -= dt) <= 0f)
                    {
                        timer = Random.Range(4f, 6f);
                        Geyser(player.Pos.x + Random.Range(-3.5f, 3.5f), 16f * Difficulty.DamageMul(Game.I.Run.Level), 1.1f);
                        if (Random.value < 0.5f) Geyser(Random.Range(-Player.ArenaHalf, Player.ArenaHalf), 16f * Difficulty.DamageMul(Game.I.Run.Level), 1.4f);
                    }
                    break;
                case StageMechanic.Eclipse:
                    if (eclipseT > 0f)
                    {
                        eclipseT -= dt;
                        float k = Mathf.Clamp01(Mathf.Min(eclipseT, 4.5f - eclipseT) / 0.6f);
                        EnemySpeedBoost = 1f + 0.3f * k;
                        EnemyDamageBoost = 1f + 0.25f * k;
                        Game.I.Post.SetEclipse(k);
                        if (eclipseT <= 0f) { EnemySpeedBoost = EnemyDamageBoost = 1f; Game.I.Post.SetEclipse(0f); }
                    }
                    else if ((timer -= dt) <= 0f)
                    {
                        timer = Random.Range(11f, 14f);
                        eclipseT = 4.5f;
                        Game.I.Hud.ShowToast("EKLIPSE  ·  GEGNER GESTÄRKT");
                        FxSystem.I.Ring(FxLayer.Front, player.Pos + new Vector2(0f, 1f), 1f, 14f, 0.6f, 0.05f, 1.2f, theme.Accent, theme.Accent.WithAlpha(0f), 2f);
                    }
                    break;
            }
        }

        void UpdateHazards(float dt, Player player, WaveDirector waves)
        {
            var fx = FxSystem.I;
            foreach (var h in hazards)
            {
                if (!h.active) continue;
                h.t += dt;
                bool strike = h.kind == HazardKind.Strike;
                Color c = strike ? new Color(0.7f, 0.9f, 1f) : Palette.BlastOrange;
                if (!h.fired)
                {
                    // telegraph: a pulsing mark on the ground that tightens as it charges
                    float k = Mathf.Clamp01(h.t / h.delay);
                    float pulse = 0.6f + 0.4f * Mathf.Sin(h.t * (10f + 20f * k));
                    h.marker.color = c.WithAlpha((0.25f + 0.5f * k) * pulse);
                    h.marker.transform.localScale = new Vector3(2.4f, 0.7f, 1f);
                    h.ring.color = c.WithAlpha(0.8f * k);
                    h.ring.transform.localScale = new Vector3(Mathf.Lerp(1.4f, 0.5f, k), Mathf.Lerp(1.4f, 0.5f, k) * 2.6f, 1f);
                    h.column.color = c.WithAlpha(0.12f * k * pulse);
                    h.column.transform.localScale = new Vector3(0.6f, -0.875f, 1f);   // the ray sprite hangs from its pivot: flip it to rise from the ground
                    if (!strike && Random.value < dt * 20f * k)
                        fx.Sparks(new Vector2(h.x + Random.Range(-0.4f, 0.4f), h.floor), Vector2.up, 30f, 1, 2f, 5f, Palette.BlastOrange, 2.4f, 0.04f, 0.3f, 4f);
                    if (h.t >= h.delay) { h.fired = true; h.erupt = 0f; Fire(h, player, waves); }
                }
                else
                {
                    h.erupt += dt;
                    float life = strike ? 0.25f : 0.8f;
                    float k = 1f - Mathf.Clamp01(h.erupt / life);
                    h.marker.color = c.WithAlpha(0.8f * k);
                    h.ring.color = Color.clear;
                    h.column.color = (strike ? Color.white : c).WithAlpha((strike ? 0.9f : 0.75f) * k);
                    h.column.transform.localScale = new Vector3(strike ? 0.9f : 1.4f, -0.875f, 1f);
                    if (!strike)
                    {
                        if (Random.value < dt * 60f)
                            fx.Streak(FxLayer.Front, new Vector2(h.x + Random.Range(-0.5f, 0.5f), h.floor), new Vector2(Random.Range(-1f, 1f), Random.Range(12f, 20f)),
                                Random.Range(0.3f, 0.55f), 0.07f, 0.05f, new Color(1f, 0.9f, 0.5f), Palette.BlastOrange.WithAlpha(0f), 2.6f, 1f, 14f);
                        // the column keeps hurting while it stands
                        if (h.erupt < 0.6f) HitColumn(h, player, waves, false);
                    }
                    if (h.erupt >= life) { h.active = false; h.root.gameObject.SetActive(false); }
                }
            }
        }

        void Fire(Hazard h, Player player, WaveDirector waves)
        {
            var fx = FxSystem.I;
            var game = Game.I;
            Vector2 ground = new Vector2(h.x, h.floor);
            if (h.kind == HazardKind.Strike)
            {
                Lightning.I.Bolt(ground + new Vector2(Random.Range(-1.5f, 1.5f), 12f), ground, new Color(0.6f, 0.85f, 1f), 0.12f, 0.3f, 0.55f);
                Lightning.I.Bolt(ground + new Vector2(Random.Range(-1f, 1f), 7f), ground, new Color(0.6f, 0.85f, 1f), 0.06f, 0.22f, 0.4f);
                fx.Flash(ground + Vector2.up * 0.4f, 4f, new Color(0.75f, 0.9f, 1f), 0.2f, 3f);
                fx.Ring(FxLayer.Front, ground + Vector2.up * 0.1f, 0.2f, 2f, 0.3f, 0.02f, 0.35f, Color.white, new Color(0.6f, 0.85f, 1f, 0f), 2.4f);
                fx.Sparks(ground, Vector2.up, 150f, 14, 4f, 10f, new Color(0.8f, 0.95f, 1f), 2.6f, 0.04f, 0.3f, 10f);
                game.Cam.AddTrauma(0.35f);
                game.Post.Impact(0.5f);
                HitColumn(h, player, waves, true);
            }
            else
            {
                fx.Flash(ground + Vector2.up * 0.6f, 3f, Palette.BlastOrange, 0.25f, 2.6f);
                fx.Dust(ground, Vector2.right, 6, 2.6f, 0.5f, 0.4f);
                fx.Dust(ground, Vector2.left, 6, 2.6f, 0.5f, 0.4f);
                game.Cam.AddTrauma(0.3f);
                HitColumn(h, player, waves, true);
            }
        }

        void HitColumn(Hazard h, Player player, WaveDirector waves, bool first)
        {
            float half = h.kind == HazardKind.Strike ? 1.1f : 0.8f;
            // the strike stops on the surface it lands on: whoever stands below that platform is sheltered
            float above = player.Pos.y - h.floor;
            if (!player.Dead && Mathf.Abs(player.Pos.x - h.x) < half && above > -0.5f && above < 4f)
            {
                player.TakeDamage(h.dmg, new Vector2(h.x, h.floor));
                if (h.kind == HazardKind.Geyser) player.Launch(14f);
            }
            if (first) waves.HazardHit(new Vector2(h.x, h.floor + 1f), half + 0.4f, h.kind == HazardKind.Strike ? 60f : 45f, h.kind == HazardKind.Geyser);
        }
    }
}
