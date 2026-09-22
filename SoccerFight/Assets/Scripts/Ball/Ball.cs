using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// The ball: dribbled at the feet, kicked as a projectile, flicked along a rainbow arc and
    /// magnetically recalled. Visual stack: stretch node (aligned to velocity) → spin node (pattern)
    /// + a non-rotating shading/highlight pass so it always reads as a lit sphere.
    /// </summary>
    public sealed class Ball
    {
        // Pierce: power shot that flies straight through every monster. Blast: bicycle kick that
        // explodes on the first thing it touches.
        public enum State { Held, Scripted, Shot, Rainbow, Loose, Returning, Pierce, Blast, Meteor, Header }

        public State St { get; private set; } = State.Held;
        public Vector2 Pos;
        public Vector2 Vel;
        public bool IsHeld => St == State.Held || St == State.Scripted;
        public bool IsHeldFree => St == State.Held;
        public bool IsDangerous => St == State.Shot || St == State.Rainbow || St == State.Pierce || St == State.Blast || St == State.Header
                                   || (St == State.Returning && Vel.magnitude > (Game.I.Run.Stats.Boomerang ? 4f : 9f));
        public bool IsRainbow => St == State.Rainbow;
        /// <summary>Scripted by the player's keep-ups: drawn in front and spun by each touch.</summary>
        public bool JuggleMode;
        /// <summary>Power-shot wind-up (0..1): the held ball gathers a golden glow.</summary>
        public float Charge;
        /// <summary>Set by the kick: run-up damage bonus, golden-boot crit, ricochets left.</summary>
        public float ShotMul = 1f;
        public bool GoldenShot;
        public int RicochetsLeft;
        /// <summary>Visual and hit size (the damage upgrades make the ball a little bigger).</summary>
        public float SizeMul = 1f;
        public float Radius => Art.BallRadius * SizeMul;
        float R => Art.BallRadius * SizeMul;
        float rainbowEcho = -1f;
        Vector2 rainbowEchoAt;

        Transform root, stretch, spinNode;
        SpriteRenderer pattern, shade, highlight, glow, shadow, core;
        SpriteRenderer markerRing, markerGlow;
        TrailRenderer shotTrail, rainbowTrail, heavyTrail;
        Gradient pierceGradient, blastGradient, shotGradient, headerGradient;
        /// <summary>Header: monsters it may still pass through before it pops up.</summary>
        public int HeaderPierceLeft;
        float shotTrailTime = 0.17f;
        float spin, spinVel;
        float stateTime;
        float squash, squashVel;
        Vector2 prevPos;
        int order = PlayerRig.BallOrderFront;
        float hueT;

        // a shot fired downwards from a platform passes through that platform for a moment
        int passPlatform = Level.None;
        float passTimer;

        // rainbow path
        Vector2 b0, b1, b2, b3;
        readonly float[] arcTable = new float[65];
        float arcLen, arcS, arcSpeed0, arcGravity, arcY0;
        int flickFacing;

        // damage bookkeeping: one hit per monster per flight
        readonly System.Collections.Generic.HashSet<int> hitIds = new System.Collections.Generic.HashSet<int>();
        public int FlightId { get; private set; }

        public void Build(Transform parent)
        {
            root = new GameObject("Ball").transform;
            root.SetParent(parent, false);

            shadow = Art.MakeSprite("Shadow", parent, Art.Shadow, -44, Art.SpriteMat, new Color(0, 0, 0, 0.45f));

            glow = Art.MakeSprite("Glow", root, Art.SoftGlow, order - 2, Art.SpriteGlowMat, Palette.ShotCyan.WithAlpha(0f));
            core = Art.MakeSprite("Core", root, Art.SoftGlow, order + 3, Art.SpriteGlowMat, Color.white.WithAlpha(0f));
            core.transform.localScale = Vector3.one * 0.55f;

            stretch = new GameObject("Stretch").transform;
            stretch.SetParent(root, false);
            spinNode = new GameObject("Spin").transform;
            spinNode.SetParent(stretch, false);
            pattern = Art.MakeSprite("Pattern", spinNode, Art.BallPattern, order);
            shade = Art.MakeSprite("Shade", stretch, Art.BallShade, order + 1);
            highlight = Art.MakeSprite("Highlight", stretch, Art.BallHighlight, order + 2, Art.SpriteAddMat, new Color(1, 1, 1, 0.9f));

            // goal-kick marker: a ring on the ground that tightens while the ball is out of frame
            markerGlow = Art.MakeSprite("MeteorGlow", parent, Art.SoftGlow, -43, Art.SpriteGlowMat, Color.clear);
            markerRing = Art.MakeSprite("MeteorRing", parent, Art.MarkRing, -42, Art.SpriteGlowMat, Color.clear);

            shotTrail = MakeTrail("ShotTrail", Art.TrailShotMat, 0.17f, R * 1.8f, order - 3);
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Palette.ShotCyan, 0.35f), new GradientColorKey(new Color(0.3f, 0.5f, 1f), 1f) },
                new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.6f, 0.4f), new GradientAlphaKey(0f, 1f) });
            shotTrail.colorGradient = shotGradient = g;
            shotTrail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.5f, 0.55f), new Keyframe(1f, 0f));

            rainbowTrail = MakeTrail("RainbowTrail", Art.TrailRainbowMat, 0.55f, 0.46f, order - 4);
            var rg = new Gradient();
            rg.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.5f), new GradientAlphaKey(0f, 1f) });
            rainbowTrail.colorGradient = rg;
            rainbowTrail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(0.12f, 1f), new Keyframe(1f, 0.75f));

            // one wide trail for the heavy shots, recoloured per flight
            heavyTrail = MakeTrail("HeavyTrail", Art.TrailShotMat, 0.3f, R * 2.6f, order - 3);
            heavyTrail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.35f, 0.7f), new Keyframe(1f, 0f));
            pierceGradient = TrailGradient(Color.white, Palette.PowerGold, new Color(1f, 0.45f, 0.2f));
            blastGradient = TrailGradient(new Color(1f, 0.95f, 0.8f), Palette.BlastOrange, new Color(0.75f, 0.15f, 0.2f));
            headerGradient = TrailGradient(Color.white, Palette.Header, new Color(0.2f, 0.35f, 0.9f));
        }

        /// <summary>The normal shot's trail colour and length (elemental upgrades recolour it).</summary>
        public void SetShotTrail(Gradient g, float time)
        {
            if (g != null && g != shotGradient) { shotGradient = g; shotTrail.colorGradient = g; }
            shotTrailTime = time;
        }

        public static Gradient TrailGradient(Color head, Color mid, Color tail)
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(head, 0f), new GradientColorKey(mid, 0.3f), new GradientColorKey(tail, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.75f, 0.35f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        TrailRenderer MakeTrail(string name, Material mat, float time, float width, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var tr = go.AddComponent<TrailRenderer>();
            tr.sharedMaterial = mat;
            tr.time = time;
            tr.widthMultiplier = width;
            tr.minVertexDistance = 0.025f;
            tr.numCapVertices = 4;
            tr.numCornerVertices = 2;
            tr.textureMode = LineTextureMode.Stretch;
            tr.alignment = LineAlignment.View;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.receiveShadows = false;
            tr.sortingOrder = sortingOrder;
            tr.emitting = false;
            return tr;
        }

        public void SetOrder(int o)
        {
            if (o == order) return;
            order = o;
            glow.sortingOrder = o - 2;
            pattern.sortingOrder = o;
            shade.sortingOrder = o + 1;
            highlight.sortingOrder = o + 2;
            core.sortingOrder = o + 3;
        }

        public void ResetTo(Vector2 p)
        {
            St = State.Held;
            Pos = prevPos = p;
            Vel = Vector2.zero;
            stateTime = 0f;
            JuggleMode = false;
            Charge = 0f;
            HideMarker();
            shotTrail.Clear();
            rainbowTrail.Clear();
            heavyTrail.Clear();
            shotTrail.emitting = rainbowTrail.emitting = heavyTrail.emitting = false;
            root.position = p;
        }

        void Enter(State s)
        {
            St = s;
            stateTime = 0f;
        }

        // ------------------------------------------------------------------ commands

        /// <summary>fromPlatform: the platform the kicker stands on (the shot may pass through it).</summary>
        public void Kick(Vector2 velocity, int fromPlatform = Level.None)
        {
            Enter(State.Shot);
            Vel = velocity;
            passPlatform = fromPlatform;
            passTimer = fromPlatform != Level.None && velocity.y < 0f ? 0.3f : 0f;
            hitIds.Clear();
            FlightId++;
            squashVel += 6f;
        }

        /// <summary>
        /// Power shot: dead straight and fast, passes through monsters (each is hit once). target is
        /// where the player aimed — the singularity opens there once the ball arrives (or the shot ends).
        /// </summary>
        public void Pierce(Vector2 velocity, int fromPlatform, Vector2 target)
        {
            Kick(velocity, fromPlatform);
            Enter(State.Pierce);
            heavyTrail.colorGradient = pierceGradient;
            heavyTrail.Clear();
            squashVel += 4f;
            pierceTarget = target;
            pierceTargetDist = (target - Pos).magnitude;
            pierceTravel = 0f;
            pierceMarked = false;
        }

        Vector2 pierceTarget;
        float pierceTargetDist, pierceTravel;
        bool pierceMarked;

        void MarkPierceTarget()
        {
            if (pierceMarked) return;
            pierceMarked = true;
            Combat.OnPierceTarget(pierceTarget);
        }

        // ---- goal kick: the ball leaves the frame and comes back down on the marked spot
        Vector2 meteorTarget;
        float meteorDelay;
        bool meteorFalling;

        /// <summary>Punted out of the frame; it returns as a meteor on target after delay seconds.</summary>
        public void Punt(Vector2 target, float delay)
        {
            Enter(State.Meteor);
            hitIds.Clear();
            FlightId++;
            meteorTarget = target;
            meteorDelay = delay;
            meteorFalling = false;
            Vel = new Vector2(Random.Range(-1.5f, 1.5f), 26f);
            JuggleMode = false;
            heavyTrail.colorGradient = blastGradient;
            heavyTrail.Clear();
            squashVel += 7f;
        }

        void MeteorImpact()
        {
            var game = Game.I;
            var fx = FxSystem.I;
            var st = game.Run.Stats;
            Vector2 p = meteorTarget;
            float radius = Player.PuntRadius * st.AreaMul;
            game.Waves.Blast(p, radius, Player.PuntDamage, Src.Punt);
            // Hagel: a couple of smaller craters left and right
            for (int i = 0; i < st.PuntExtra; i++)
            {
                float off = (i % 2 == 0 ? 1f : -1f) * (2.1f + i * 0.5f);
                Vector2 q = new Vector2(Mathf.Clamp(p.x + off, -Player.ArenaHalf, Player.ArenaHalf), p.y);
                game.Waves.Blast(q, radius * 0.7f, Player.PuntDamage * 0.5f, Src.Punt);
                fx.Ring(FxLayer.Front, q, 0.2f, radius * 0.8f, 0.3f, 0.02f, 0.4f, Color.white, Palette.Amber.WithAlpha(0f), 2.4f);
                fx.Dust(q, Vector2.up, 6, 2.6f, 0.5f, 0.4f);
            }
            if (st.PuntFire)
            {
                foreach (var m in game.Waves.Monsters)
                    if (m.Alive && (m.Center - p).sqrMagnitude < (radius + m.Radius) * (radius + m.Radius))
                        m.Ignite(Mathf.Max(6f, Player.PuntDamage * 0.12f), 4f);
                for (int i = 0; i < 10; i++)
                    fx.Streak(FxLayer.Front, p + new Vector2(Random.Range(-radius, radius), 0.1f), new Vector2(Random.Range(-1f, 1f), Random.Range(3f, 7f)),
                        Random.Range(0.4f, 0.8f), 0.09f, 0.05f, new Color(1f, 0.85f, 0.45f), Palette.BlastOrange.WithAlpha(0f), 2.6f, 1.4f, 4f);
            }

            fx.Flash(p, 6f, Palette.Amber, 0.26f, 2.8f);
            fx.Flash(p, 2.6f, Color.white, 0.1f, 3.4f);
            fx.Ring(FxLayer.Front, p, 0.3f, radius * 1.25f, 0.6f, 0.03f, 0.5f, Color.white, Palette.Amber.WithAlpha(0f), 2.8f);
            fx.Ring(FxLayer.Front, p, 0.15f, radius * 0.7f, 0.3f, 0.02f, 0.3f, Palette.Gold, Palette.BlastOrange.WithAlpha(0f), 2.4f);
            fx.Sparks(p, Vector2.up, 150f, 26, 7f, 18f, Palette.Amber, 2.8f, 0.06f, 0.45f, 13f);
            fx.Dust(p, Vector2.right, 10, 4f, 0.7f, 0.45f);
            fx.Dust(p, Vector2.left, 10, 4f, 0.7f, 0.45f);
            fx.Sparkles(p + Vector2.up * 0.5f, 1.6f, 12, Palette.Gold, 3f, 0.8f);

            game.Cam.AddTrauma(0.7f);
            game.Cam.Kick(new Vector2(0f, -0.4f));
            game.Post.Impact(0.9f);
            TimeFx.HitStop(0.09f, 0.04f);

            Pos = p + new Vector2(0f, R);
            Vel = new Vector2(Random.Range(-2f, 2f), 7f);
            Enter(State.Loose);
            squashVel -= 12f;
            HideMarker();
        }

        void HideMarker()
        {
            if (markerRing != null) markerRing.color = Color.clear;
            if (markerGlow != null) markerGlow.color = Color.clear;
        }

        /// <summary>Ground mark under the falling ball: tightens and beats faster as it closes in.</summary>
        void UpdateMarker(float dt)
        {
            float k = Mathf.Clamp01(stateTime / (meteorDelay + 0.35f));
            float pulse = 0.55f + 0.45f * Mathf.Sin(stateTime * (9f + 26f * k));
            float radius = Player.PuntRadius * Game.I.Run.Stats.AreaMul;
            float r = Mathf.Lerp(radius * 1.05f, radius * 0.5f, MathUtil.EaseInQuad(k));
            markerRing.transform.position = new Vector3(meteorTarget.x, meteorTarget.y + 0.05f, 0f);
            markerRing.transform.localScale = new Vector3(r * 2.15f, r * 0.85f, 1f);
            markerRing.color = Palette.Amber.WithAlpha((0.35f + 0.45f * k) * pulse);
            markerGlow.transform.position = markerRing.transform.position;
            markerGlow.transform.localScale = new Vector3(r * 2.4f, r * 0.9f, 1f);
            markerGlow.color = Palette.Amber.WithAlpha(0.06f + 0.12f * k * pulse);
            if (Random.value < dt * 12f * k)
                FxSystem.I.Sparks(new Vector2(meteorTarget.x + Random.Range(-r, r), meteorTarget.y + 0.05f), Vector2.up, 26f, 1, 2f, 5f, Palette.Amber, 2.4f, 0.04f, 0.3f, 5f);
        }

        /// <summary>Bicycle kick: a heavy shot that explodes on the first surface or monster it meets.</summary>
        public void Blast(Vector2 velocity)
        {
            Kick(velocity);
            Enter(State.Blast);
            heavyTrail.colorGradient = blastGradient;
            heavyTrail.Clear();
        }

        /// <summary>Header: a heavy, flat ball. It stuns what it meets and then pops up into the air (Waves.BallHit).</summary>
        public void Header(Vector2 velocity, int pierce)
        {
            Kick(velocity);
            Enter(State.Header);
            HeaderPierceLeft = pierce;
            heavyTrail.colorGradient = headerGradient;
            heavyTrail.Clear();
            squashVel += 5f;
        }

        /// <summary>The header met a monster: straight up off the head of the victim, then home.</summary>
        public void HeaderPop()
        {
            Vel = new Vector2(Mathf.Sign(Vel.x) * -1.2f, 11f);
            Enter(State.Loose);
            squashVel -= 9f;
        }

        public void BeginScripted() { Enter(State.Scripted); }

        /// <summary>Back to the feet after a move that carried the ball.</summary>
        public void EndScripted()
        {
            if (St != State.Scripted) return;
            JuggleMode = false;
            Enter(State.Held);
        }

        public void Release() { Enter(State.Loose); Vel = new Vector2(0f, 3f); }

        /// <summary>A missed keep-up: the ball carries on under normal physics and rolls home.</summary>
        public void Drop(Vector2 velocity) { JuggleMode = false; Enter(State.Loose); Vel = velocity; }

        public void OnJuggleTouch(float spinDegPerSec) { spinVel = spinDegPerSec; squashVel -= 5f; }

        /// <summary>groundY: the surface the player flicks from — the arc peaks the same height above it.</summary>
        public void StartRainbow(Vector2 from, Vector2 target, int facing, float groundY)
        {
            Enter(State.Rainbow);
            hitIds.Clear();
            FlightId++;
            flickFacing = facing;
            float dist = Mathf.Abs(target.x - from.x);
            float apex = groundY + 3.4f + dist * 0.13f;
            float ctrlY = (8f * apex - from.y - target.y) / 6f;
            b0 = from;
            b1 = new Vector2(from.x - facing * 0.75f, ctrlY);
            b2 = new Vector2(target.x - facing * dist * 0.12f, ctrlY * 0.96f);
            b3 = target;

            arcTable[0] = 0f;
            Vector2 prev = b0;
            for (int i = 1; i < arcTable.Length; i++)
            {
                Vector2 p = MathUtil.Bezier(b0, b1, b2, b3, i / (float)(arcTable.Length - 1));
                arcTable[i] = arcTable[i - 1] + (p - prev).magnitude;
                prev = p;
            }
            arcLen = arcTable[arcTable.Length - 1];
            arcS = 0f;
            arcY0 = from.y;
            arcGravity = 30f;
            float vApex = Mathf.Max(5.5f, dist * 0.95f);
            arcSpeed0 = Mathf.Sqrt(vApex * vApex + 2f * arcGravity * (apex - from.y));
            rainbowTrail.Clear();
            squashVel += 5f;
        }

        float ArcU(float s)
        {
            if (s <= 0f) return 0f;
            if (s >= arcLen) return 1f;
            int lo = 0, hi = arcTable.Length - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (arcTable[mid] < s) lo = mid; else hi = mid;
            }
            float seg = arcTable[hi] - arcTable[lo];
            float f = seg > 1e-6f ? (s - arcTable[lo]) / seg : 0f;
            return (lo + f) / (arcTable.Length - 1);
        }

        /// <summary>A returning ball close to the player can be taken first-time (one-touch shot).</summary>
        public bool IsCatchable(Vector2 holdPoint, float range)
            => St == State.Returning && (Pos - holdPoint).sqrMagnitude < range * range;

        public void ForceCatch(Player player) => Catch(player);

        /// <summary>Was this monster already hit during the current flight?</summary>
        public bool TryRegisterHit(int monsterId) => hitIds.Add(monsterId);

        /// <summary>Bicycle-kick blast: area damage and a big burst where the ball comes down.</summary>
        public void Explode()
        {
            if (St != State.Blast) return;
            var game = Game.I;
            var fx = FxSystem.I;
            Vector2 p = Pos;
            var st = game.Run.Stats;
            float radius = Player.BlastRadius * st.BlastRadiusMul * st.AreaMul;
            game.Waves.Blast(p, radius, Player.BlastDamage);

            fx.Flash(p, 5f, Palette.BlastOrange, 0.24f, 2.6f);
            fx.Flash(p, 2.4f, Color.white, 0.1f, 3.2f);
            fx.Ring(FxLayer.Front, p, 0.3f, radius * 1.1f, 0.55f, 0.03f, 0.45f, Color.white, Palette.BlastOrange.WithAlpha(0f), 2.6f);
            fx.Ring(FxLayer.Front, p, 0.15f, radius * 0.65f, 0.3f, 0.02f, 0.3f, Palette.Gold, Palette.Hurt.WithAlpha(0f), 2.4f);
            fx.Sparks(p, Vector2.up, 200f, 26, 6f, 16f, Palette.BlastOrange, 2.8f, 0.06f, 0.42f, 14f);
            for (int i = 0; i < 18; i++)
            {
                float ang = Random.Range(8f, 172f);
                Color c = Color.Lerp(Palette.Gold, Palette.BlastOrange, Random.value);
                fx.Streak(FxLayer.Front, p, MathUtil.Dir(ang) * Random.Range(7f, 16f), Random.Range(0.25f, 0.5f), 0.08f, 0.045f,
                    Color.Lerp(c, Color.white, 0.35f), c.WithAlpha(0f), 2.8f, 3.5f, 10f);
            }
            fx.Dust(p, Vector2.right, 9, 3.6f, 0.65f, 0.42f);
            fx.Dust(p, Vector2.left, 9, 3.6f, 0.65f, 0.42f);
            fx.Sparkles(p + Vector2.up * 0.5f, 1.4f, 12, Palette.Gold, 3f, 0.8f);

            game.Cam.AddTrauma(0.6f);
            game.Cam.Kick(new Vector2(0f, -0.35f));
            game.Post.Impact(0.8f);
            TimeFx.HitStop(0.08f, 0.04f);

            Vel = new Vector2(-Mathf.Sign(Vel.x) * 1.5f, 8f);
            Enter(State.Loose);
            squashVel -= 10f;
        }

        public void BounceOff(Vector2 normal)
        {
            // deflect off a monster, lose energy, then come home
            Vector2 v = Vector2.Reflect(Vel, normal);
            v = v * 0.42f + Vector2.up * 5f;
            Vel = v;
            Enter(State.Loose);
            squashVel -= 8f;
        }

        // ------------------------------------------------------------------ update

        static float Gravity => Game.I.Run.Stats.GravityMul;

        void StartReturn()
        {
            // boomerang: the way home is a second pass through everything
            if (Game.I.Run.Stats.Boomerang && (St == State.Shot || St == State.Pierce)) hitIds.Clear();
            Enter(State.Returning);
        }

        /// <summary>Ricochet: redirect a shot that just hit into the nearest other monster.</summary>
        public bool TryRicochet(Monster from)
        {
            if (RicochetsLeft <= 0 || St != State.Shot) return false;
            var next = Combat.NearestTo(Pos, 7.5f, from);
            if (next == null || hitIds.Contains(next.Id)) return false;
            RicochetsLeft--;
            Vel = (next.Center - Pos).normalized * Mathf.Max(Vel.magnitude, 20f);
            stateTime = Mathf.Min(stateTime, 0.15f);
            squashVel -= 6f;
            var fx = FxSystem.I;
            fx.Ring(FxLayer.Front, Pos, 0.05f, 0.6f, 0.1f, 0.01f, 0.18f, Color.white, Palette.ShotCyan.WithAlpha(0f), 2.2f);
            fx.Sparks(Pos, Vel, 40f, 5, 5f, 10f, Palette.ShotCyan, 2.4f, 0.04f, 0.18f);
            return true;
        }

        public void Update(float dt, Player player)
        {
            stateTime += dt;
            if (rainbowEcho >= 0f && (rainbowEcho -= dt) < 0f) RainbowEcho();
            passTimer = Mathf.Max(0f, passTimer - dt);
            prevPos = Pos;
            var rig = player.Rig;

            switch (St)
            {
                case State.Held:
                {
                    Vector2 target = rig.BallHold;
                    MathUtil.Spring(ref Pos, ref Vel, target, 7.5f, 0.9f, dt);
                    // never sink into the surface — but only surfaces at the player's feet or lower,
                    // so the ball follows when the player drops through a platform
                    float floor = Level.FloorBelow(Pos.x, Mathf.Min(Pos.y - R, player.Pos.y) + 0.02f);
                    if (Pos.y < floor + R) Pos.y = floor + R;
                    break;
                }
                case State.Scripted:
                {
                    Vector2 target = rig.BallHold;
                    Vel = dt > 0f ? (target - Pos) / dt : Vector2.zero;
                    Pos = target;
                    break;
                }
                case State.Shot:
                {
                    Vel.y -= 4f * Gravity * dt;
                    Pos += Vel * dt;
                    CollideWorld(0.55f);
                    if (stateTime > 0.4f || Vel.sqrMagnitude < 36f) StartReturn();
                    break;
                }
                case State.Pierce:
                {
                    // no gravity and no bounce off monsters; floors deflect it, the arena wall ends it
                    Pos += Vel * dt;
                    pierceTravel += Vel.magnitude * dt;
                    if (pierceTravel >= pierceTargetDist) MarkPierceTarget();
                    CollideWorld(0.85f);
                    if (stateTime > 0.75f || Mathf.Abs(Pos.x) >= Player.ArenaHalf + 0.55f) { MarkPierceTarget(); StartReturn(); }
                    break;
                }
                case State.Meteor:
                {
                    // out of the frame, a beat of nothing but the marker, then straight back down
                    if (!meteorFalling)
                    {
                        Pos += Vel * dt;
                        Vel.y = Mathf.Max(6f, Vel.y - 14f * dt);
                        if (stateTime >= meteorDelay)
                        {
                            meteorFalling = true;
                            Pos = meteorTarget + new Vector2(0f, 16f);
                            Vel = new Vector2(0f, -46f);
                            heavyTrail.Clear();
                            squashVel += 6f;
                        }
                    }
                    else
                    {
                        Pos += Vel * dt;
                        if (Random.value < dt * 40f)
                            FxSystem.I.Streak(FxLayer.Front, Pos + new Vector2(Random.Range(-0.3f, 0.3f), 0.4f), new Vector2(0f, 14f),
                                0.2f, 0.06f, 0.05f, new Color(1f, 0.9f, 0.6f), Palette.Amber.WithAlpha(0f), 2.6f, 2f);
                        if (Pos.y <= meteorTarget.y + R) MeteorImpact();
                    }
                    UpdateMarker(dt);
                    break;
                }
                case State.Header:
                {
                    // heavier than a kick: dips a little over its flight, bounces off floors, then comes home
                    Vel.y -= 7f * Gravity * dt;
                    Pos += Vel * dt;
                    CollideWorld(0.5f);
                    if (stateTime > 0.55f || Vel.sqrMagnitude < 36f) StartReturn();
                    break;
                }
                case State.Blast:
                {
                    Vel.y -= 20f * Gravity * dt;
                    Pos += Vel * dt;
                    float floor = Level.FloorBelow(Pos.x, prevPos.y - R + 0.02f);
                    if (Pos.y <= floor + R) { Pos.y = floor + R; Explode(); }
                    else if (Mathf.Abs(Pos.x) >= Player.ArenaHalf + 0.6f || Pos.y > 12f || stateTime > 1.6f) Explode();
                    break;
                }
                case State.Rainbow:
                {
                    float u0 = ArcU(arcS);
                    float y = MathUtil.Bezier(b0, b1, b2, b3, u0).y;
                    float speed = Mathf.Sqrt(Mathf.Max(16f, arcSpeed0 * arcSpeed0 - 2f * arcGravity * (y - arcY0)));
                    arcS += speed * dt;
                    float u = ArcU(arcS);
                    Vector2 np = MathUtil.Bezier(b0, b1, b2, b3, u);
                    Vel = dt > 0f ? (np - Pos) / dt : Vel;
                    Pos = np;
                    if (arcS >= arcLen) Impact();
                    break;
                }
                case State.Loose:
                {
                    Vel.y -= 26f * Gravity * dt;
                    Vel.x *= Mathf.Exp(-0.6f * dt);
                    Pos += Vel * dt;
                    CollideWorld(0.6f);
                    if (stateTime > 0.28f / Mathf.Sqrt(Gravity)) StartReturn();
                    break;
                }
                case State.Returning:
                {
                    Vector2 target = rig.BallHold + new Vector2(0f, 0.15f);
                    Vector2 to = target - Pos;
                    float dist = to.magnitude;
                    float ramp = MathUtil.EaseOutQuad(stateTime / 0.25f);
                    float speed = (Mathf.Lerp(7f, 34f, ramp) + dist * 1.6f) * Game.I.Run.Stats.ReturnSpeedMul;
                    Vector2 desired = dist > 1e-4f ? to / dist * speed : Vector2.zero;
                    Vel = MathUtil.Damp(Vel, desired, 7f + ramp * 8f, dt);
                    Pos += Vel * dt;
                    if (Pos.y < R) { Pos.y = R; Vel.y = Mathf.Abs(Vel.y) * 0.3f; }
                    if (dist < 0.4f + Vel.magnitude * dt || stateTime > 2.2f) Catch(player);
                    break;
                }
            }

            UpdateVisuals(dt, player);
        }

        void CollideWorld(float restitution)
        {
            // the free-kick wall bounces the ball back into play (a pierce shot goes straight through)
            if (St != State.Pierce && Barrier.I != null && Barrier.I.Deflect(prevPos, ref Pos, ref Vel, R)) squashVel -= 6f;

            // one-way platforms: only a ball that was above a surface last step can land on it
            float floor = Level.FloorBelow(Pos.x, prevPos.y - R + 0.02f, 0f, passTimer > 0f ? passPlatform : Level.None, out int under);
            if (Pos.y < floor + R)
            {
                Pos.y = floor + R;
                Pos.x += Level.DeltaOf(under).x;   // resting on a gliding platform: ride along
                if (Vel.y < -2f)
                {
                    FxSystem.I.Dust(new Vector2(Pos.x, floor), new Vector2(Vel.x * 0.1f, 0f), 2, 1.2f, 0.25f, 0.25f);
                    squashVel -= Mathf.Min(8f, -Vel.y * 0.5f);
                }
                Vel.y = -Vel.y * restitution;
                Vel.x *= 0.88f;
            }
            float wall = Player.ArenaHalf + 0.6f;
            if (Pos.x < -wall) { Pos.x = -wall; Vel.x = Mathf.Abs(Vel.x) * 0.6f; }
            if (Pos.x > wall) { Pos.x = wall; Vel.x = -Mathf.Abs(Vel.x) * 0.6f; }
            if (Pos.y > 12f) { Pos.y = 12f; Vel.y = -Mathf.Abs(Vel.y) * 0.5f; }
        }

        void Impact()
        {
            var game = Game.I;
            var fx = FxSystem.I;
            Vector2 p = new Vector2(Pos.x, b3.y);   // the target sits on the pitch or a platform
            Pos = p;
            game.Waves.RainbowImpact(p);
            if (game.Run.Stats.DoubleRainbow) { rainbowEcho = 0.38f; rainbowEchoAt = p; }

            fx.Flash(p + Vector2.up * 0.3f, 3.2f, Color.white, 0.2f, 2.4f);
            fx.Ring(FxLayer.Front, p, 0.2f, 3.1f, 0.6f, 0.04f, 0.5f, Color.white, Color.white.WithAlpha(0f), 2.2f, true);
            fx.Ring(FxLayer.Front, p, 0.1f, 1.8f, 0.22f, 0.02f, 0.3f, Color.white, Palette.Gold.WithAlpha(0f), 2.4f);
            for (int i = 0; i < 28; i++)
            {
                Color c = Art.Rainbow(Random.value);
                float ang = Random.Range(15f, 165f);
                fx.Streak(FxLayer.Front, p, MathUtil.Dir(ang) * Random.Range(6f, 15f), Random.Range(0.3f, 0.55f), 0.07f, 0.04f,
                    Color.Lerp(c, Color.white, 0.3f), c.WithAlpha(0f), 2.8f, 3.5f, 9f);
            }
            fx.Sparkles(p + Vector2.up * 0.4f, 1.2f, 16, Palette.Gold, 3f, 0.9f);
            fx.Dust(p, Vector2.right, 7, 3f, 0.55f, 0.35f);
            fx.Dust(p, Vector2.left, 7, 3f, 0.55f, 0.35f);

            game.Cam.AddTrauma(0.5f);
            game.Cam.Kick(new Vector2(0f, -0.3f));
            game.Post.Impact(0.7f);
            TimeFx.HitStop(0.075f, 0.04f);

            Vel = new Vector2(flickFacing * 2.2f, 8.5f);
            Enter(State.Loose);
            squashVel -= 10f;
        }

        /// <summary>Double rainbow: a second, smaller impact right after the first.</summary>
        void RainbowEcho()
        {
            var game = Game.I;
            var fx = FxSystem.I;
            Vector2 p = rainbowEchoAt;
            game.Waves.RainbowImpact(p, 0.6f);
            fx.Flash(p + Vector2.up * 0.3f, 2.4f, Color.white, 0.16f, 2.2f);
            fx.Ring(FxLayer.Front, p, 0.2f, 2.4f, 0.45f, 0.03f, 0.42f, Color.white, Color.white.WithAlpha(0f), 2.2f, true);
            fx.Sparkles(p + Vector2.up * 0.4f, 1f, 10, Palette.Gold, 3f, 0.7f);
            game.Cam.AddTrauma(0.25f);
        }

        void Catch(Player player)
        {
            Enter(State.Held);
            Vel *= 0.15f;
            player.Rig.OnBallReceived();
            var fx = FxSystem.I;
            fx.Ring(FxLayer.Front, Pos, 0.05f, 0.5f, 0.08f, 0.01f, 0.18f, Palette.ShotCore, Palette.ShotCyan.WithAlpha(0f), 2f);
            fx.Motes(Pos, Vector2.up * 0.5f, Palette.ShotCyan, 4, 0.1f);
            squashVel -= 4f;
        }

        // ------------------------------------------------------------------ visuals

        void UpdateVisuals(float dt, Player player)
        {
            Vector2 delta = Pos - prevPos;
            float speed = dt > 0f ? delta.magnitude / dt : 0f;
            float floor = Level.FloorBelow(Pos.x, Pos.y - R + 0.03f);
            bool onGround = Pos.y <= floor + R + 0.02f;

            // spin: pure rolling on the ground, flicked spin in the air
            if (JuggleMode && St == State.Scripted) { spin += spinVel * dt; spinVel *= Mathf.Exp(-0.8f * dt); }
            else if (onGround || St == State.Held) spin -= delta.x / R * Mathf.Rad2Deg;
            else if (St == State.Rainbow) spin += -flickFacing * 900f * dt;
            else spin -= Mathf.Sign(Vel.x) * Mathf.Min(speed, 30f) * 18f * dt;

            MathUtil.Spring(ref squash, ref squashVel, 0f, 4f, 0.35f, dt);

            root.position = new Vector3(Pos.x, Pos.y, 0f);
            float velAng = speed > 0.5f ? MathUtil.Angle(delta) : 0f;
            float st = (St == State.Held || St == State.Scripted) ? 0f : Mathf.Clamp(speed / 36f, 0f, 0.32f);
            float sq = Mathf.Clamp(squash * 0.06f, -0.25f, 0.25f);
            stretch.localRotation = Quaternion.Euler(0f, 0f, velAng);
            stretch.localScale = new Vector3((1f + st + sq) * SizeMul, (1f - st * 0.55f - sq * 0.7f) * SizeMul, 1f);
            spinNode.localRotation = Quaternion.Euler(0f, 0f, spin - velAng);
            shade.transform.localRotation = Quaternion.Euler(0f, 0f, -velAng);
            highlight.transform.localRotation = Quaternion.Euler(0f, 0f, -velAng);

            // glow + trails
            hueT += dt * 2.2f;
            Color glowCol;
            float glowA, glowSize, coreA = 0f;
            switch (St)
            {
                case State.Shot: glowCol = Palette.ShotCyan; glowA = 0.7f; glowSize = 1.35f; coreA = 0.5f; break;
                case State.Rainbow: glowCol = Art.Rainbow(Mathf.PingPong(hueT, 1f)); glowA = 0.55f; glowSize = 1.15f; coreA = 0.5f; break;
                case State.Returning: glowCol = Palette.ShotCyan; glowA = 0.38f; glowSize = 1.1f; coreA = 0.2f; break;
                case State.Loose: glowCol = Palette.ShotCyan; glowA = 0.28f; glowSize = 1f; break;
                case State.Pierce: glowCol = Palette.PowerGold; glowA = 0.85f; glowSize = 1.6f; coreA = 0.7f; break;
                case State.Blast: glowCol = Palette.BlastOrange; glowA = 0.8f; glowSize = 1.45f; coreA = 0.55f; break;
                case State.Header: glowCol = Palette.Header; glowA = 0.8f; glowSize = 1.45f; coreA = 0.55f; break;
                case State.Meteor: glowCol = Palette.Amber; glowA = 0.9f; glowSize = 1.7f; coreA = 0.75f; break;
                default: glowCol = Palette.ShotCyan; glowA = 0.06f + 0.03f * Mathf.Sin(Time.time * 3f); glowSize = 0.85f; break;
            }
            if (Charge > 0f && IsHeld)
            {
                // wind-up of the power shot: gold glow swells and flickers faster as it fills
                float c = Charge * (0.85f + 0.15f * Mathf.Sin(Time.time * 40f));
                glowCol = Palette.PowerGold; glowA = 0.15f + 0.65f * c; glowSize = 0.9f + 0.7f * c; coreA = 0.55f * c;
            }
            glow.color = Color.Lerp(glow.color, glowCol.WithAlpha(glowA), 1f - Mathf.Exp(-14f * dt));
            glow.transform.localScale = Vector3.one * Mathf.Lerp(glow.transform.localScale.x, glowSize * SizeMul, 1f - Mathf.Exp(-12f * dt));
            core.transform.localScale = Vector3.one * 0.55f * SizeMul;
            shotTrail.widthMultiplier = R * 1.8f;
            shotTrail.time = shotTrailTime;
            heavyTrail.widthMultiplier = R * 2.6f;
            core.color = Color.Lerp(core.color, Color.white.WithAlpha(coreA), 1f - Mathf.Exp(-14f * dt));

            if (St != State.Meteor) HideMarker();
            shotTrail.emitting = St == State.Shot || (St == State.Returning && speed > 7f) || (St == State.Loose && speed > 7f);
            rainbowTrail.emitting = St == State.Rainbow;
            heavyTrail.emitting = St == State.Pierce || St == State.Blast || St == State.Meteor || St == State.Header;

            // heavy shots shed embers along their path
            if ((St == State.Pierce || St == State.Blast || St == State.Meteor || St == State.Header) && Random.value < dt * 45f && speed > 1f)
            {
                Color c = St == State.Pierce ? Palette.PowerGold : St == State.Meteor ? Palette.Amber : St == State.Header ? Palette.Header : Palette.BlastOrange;
                FxSystem.I.Sparks(Pos, -delta.normalized, 35f, 1, 1.5f, 4.5f, c, 2.4f, 0.035f, 0.2f, St == State.Blast ? 6f : 0f);
            }

            // sorting: tuck between the legs while the flick rolls it up the calf
            SetOrder(St == State.Scripted && !JuggleMode ? PlayerRig.BallOrderBetweenLegs : PlayerRig.BallOrderFront);

            // shadow on the surface below
            float h = Mathf.Max(0f, Pos.y - R - floor);
            float s = Mathf.Lerp(0.5f, 0.2f, Mathf.Clamp01(h / 4f)) * SizeMul;
            shadow.transform.position = new Vector3(Pos.x, floor + 0.02f, 0f);
            shadow.transform.localScale = new Vector3(s, s * 0.9f, 1f);
            shadow.color = new Color(0f, 0f, 0f, St == State.Meteor && !meteorFalling ? 0f : Mathf.Lerp(0.45f, 0.08f, Mathf.Clamp01(h / 4f)));

            // rainbow flight sheds sparkles
            if (St == State.Rainbow && Random.value < dt * 40f)
            {
                Color c = Art.Rainbow(Random.value);
                FxSystem.I.Spawn(FxLayer.Front, true, Art.CellSparkle, Pos + Random.insideUnitCircle * 0.25f, Random.insideUnitCircle * 0.6f,
                    0.5f, 0.2f, 0f, c, c.WithAlpha(0f), 2.8f, 1f, 0.5f, Random.Range(0f, 90f), 120f);
            }
        }
    }
}
