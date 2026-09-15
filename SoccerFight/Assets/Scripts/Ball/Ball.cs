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
        public enum State { Held, Scripted, Shot, Rainbow, Loose, Returning }

        public State St { get; private set; } = State.Held;
        public Vector2 Pos;
        public Vector2 Vel;
        public bool IsHeld => St == State.Held || St == State.Scripted;
        public bool IsHeldFree => St == State.Held;
        public bool IsDangerous => St == State.Shot || St == State.Rainbow || (St == State.Returning && Vel.magnitude > 9f);
        public bool IsRainbow => St == State.Rainbow;
        /// <summary>Scripted by the player's keep-ups: drawn in front and spun by each touch.</summary>
        public bool JuggleMode;
        const float R = Art.BallRadius;

        Transform root, stretch, spinNode;
        SpriteRenderer pattern, shade, highlight, glow, shadow, core;
        TrailRenderer shotTrail, rainbowTrail;
        float spin, spinVel;
        float stateTime;
        float squash, squashVel;
        Vector2 prevPos;
        int order = PlayerRig.BallOrderFront;
        float hueT;

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

            shotTrail = MakeTrail("ShotTrail", Art.TrailShotMat, 0.17f, R * 1.8f, order - 3);
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Palette.ShotCyan, 0.35f), new GradientColorKey(new Color(0.3f, 0.5f, 1f), 1f) },
                new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.6f, 0.4f), new GradientAlphaKey(0f, 1f) });
            shotTrail.colorGradient = g;
            shotTrail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.5f, 0.55f), new Keyframe(1f, 0f));

            rainbowTrail = MakeTrail("RainbowTrail", Art.TrailRainbowMat, 0.55f, 0.46f, order - 4);
            var rg = new Gradient();
            rg.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.5f), new GradientAlphaKey(0f, 1f) });
            rainbowTrail.colorGradient = rg;
            rainbowTrail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(0.12f, 1f), new Keyframe(1f, 0.75f));
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
            shotTrail.Clear();
            rainbowTrail.Clear();
            shotTrail.emitting = rainbowTrail.emitting = false;
            root.position = p;
        }

        void Enter(State s)
        {
            St = s;
            stateTime = 0f;
        }

        // ------------------------------------------------------------------ commands

        public void Kick(Vector2 velocity)
        {
            Enter(State.Shot);
            Vel = velocity;
            hitIds.Clear();
            FlightId++;
            squashVel += 6f;
        }

        public void BeginScripted() { Enter(State.Scripted); }

        public void Release() { Enter(State.Loose); Vel = new Vector2(0f, 3f); }

        /// <summary>A missed keep-up: the ball carries on under normal physics and rolls home.</summary>
        public void Drop(Vector2 velocity) { JuggleMode = false; Enter(State.Loose); Vel = velocity; }

        public void OnJuggleTouch(float spinDegPerSec) { spinVel = spinDegPerSec; squashVel -= 5f; }

        public void StartRainbow(Vector2 from, Vector2 target, int facing)
        {
            Enter(State.Rainbow);
            hitIds.Clear();
            FlightId++;
            flickFacing = facing;
            float dist = Mathf.Abs(target.x - from.x);
            float apex = 3.4f + dist * 0.13f;
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

        public void Update(float dt, Player player)
        {
            stateTime += dt;
            prevPos = Pos;
            var rig = player.Rig;

            switch (St)
            {
                case State.Held:
                {
                    Vector2 target = rig.BallHold;
                    MathUtil.Spring(ref Pos, ref Vel, target, 7.5f, 0.9f, dt);
                    if (Pos.y < R) Pos.y = R;
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
                    Vel.y -= 4f * dt;
                    Pos += Vel * dt;
                    CollideWorld(0.55f);
                    if (stateTime > 0.4f || Vel.sqrMagnitude < 36f) Enter(State.Returning);
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
                    Vel.y -= 26f * dt;
                    Vel.x *= Mathf.Exp(-0.6f * dt);
                    Pos += Vel * dt;
                    CollideWorld(0.6f);
                    if (stateTime > 0.4f) Enter(State.Returning);
                    break;
                }
                case State.Returning:
                {
                    Vector2 target = rig.BallHold + new Vector2(0f, 0.15f);
                    Vector2 to = target - Pos;
                    float dist = to.magnitude;
                    float ramp = MathUtil.EaseOutQuad(stateTime / 0.25f);
                    float speed = Mathf.Lerp(5f, 30f, ramp) + dist * 1.5f;
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
            if (Pos.y < R)
            {
                Pos.y = R;
                if (Vel.y < -2f)
                {
                    FxSystem.I.Dust(new Vector2(Pos.x, 0f), new Vector2(Vel.x * 0.1f, 0f), 2, 1.2f, 0.25f, 0.25f);
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
            Vector2 p = new Vector2(Pos.x, R);
            Pos = p;
            game.Waves.RainbowImpact(p);

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
            bool onGround = Pos.y <= R + 0.02f;

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
            stretch.localScale = new Vector3(1f + st + sq, 1f - st * 0.55f - sq * 0.7f, 1f);
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
                default: glowCol = Palette.ShotCyan; glowA = 0.06f + 0.03f * Mathf.Sin(Time.time * 3f); glowSize = 0.85f; break;
            }
            glow.color = Color.Lerp(glow.color, glowCol.WithAlpha(glowA), 1f - Mathf.Exp(-14f * dt));
            glow.transform.localScale = Vector3.one * Mathf.Lerp(glow.transform.localScale.x, glowSize, 1f - Mathf.Exp(-12f * dt));
            core.color = Color.Lerp(core.color, Color.white.WithAlpha(coreA), 1f - Mathf.Exp(-14f * dt));

            shotTrail.emitting = St == State.Shot || (St == State.Returning && speed > 7f) || (St == State.Loose && speed > 7f);
            rainbowTrail.emitting = St == State.Rainbow;

            // sorting: tuck between the legs while the flick rolls it up the calf
            SetOrder(St == State.Scripted && !JuggleMode ? PlayerRig.BallOrderBetweenLegs : PlayerRig.BallOrderFront);

            // ground shadow
            float h = Mathf.Max(0f, Pos.y - R);
            float s = Mathf.Lerp(0.5f, 0.2f, Mathf.Clamp01(h / 4f));
            shadow.transform.position = new Vector3(Pos.x, 0.02f, 0f);
            shadow.transform.localScale = new Vector3(s, s * 0.9f, 1f);
            shadow.color = new Color(0f, 0f, 0f, Mathf.Lerp(0.45f, 0.08f, Mathf.Clamp01(h / 4f)));

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
