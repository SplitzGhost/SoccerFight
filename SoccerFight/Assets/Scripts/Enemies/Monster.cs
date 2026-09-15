using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Two enemy types sharing one class: the hopping Gloom blob and the floating Wisp.
    /// All motion is spring-based squash & stretch; hits flash white, knock back and pop a damage number.
    /// </summary>
    public sealed class Monster
    {
        public enum Kind { Blob, Wisp }

        static int nextId;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { nextId = 0; }

        public int Id { get; private set; }
        public Kind K { get; private set; }
        public Vector2 Pos, Vel;
        public float Radius;
        public float Hp, MaxHp;
        public bool Alive;
        public float ContactDamage;
        public Vector2 Center => K == Kind.Blob ? Pos + new Vector2(0f, 0.36f * scaleNow) : Pos;

        Transform root, body;
        SpriteRenderer bodySr, glow, shadow, hpBack, hpFill;
        SpriteRenderer[] eyes, pupils, extras;
        Color[] extraColors;
        Transform[] tail;
        Vector2[] tailPos;
        float spawnT;
        float scaleNow = 1f;
        float flash;
        float hpShow;
        float hpDisplay = 1f;
        float squash, squashVel;
        float faceT = -1f;
        float blinkTimer, blink;
        float hopTimer;
        bool grounded;
        int standing = Level.None;     // platform under a grounded blob
        bool leapPlanned;              // the next hop is a big leap up to a platform (longer crouch)
        float t;
        float diveTimer, windup, diveTime;
        float sizeMul = 1f;

        public void Build(Transform parent, Kind kind)
        {
            K = kind;
            root = new GameObject(kind.ToString()).transform;
            root.SetParent(parent, false);
            body = new GameObject("Body").transform;
            body.SetParent(root, false);
            shadow = Art.MakeSprite("Shadow", root, Art.Shadow, -43, Art.SpriteMat, new Color(0, 0, 0, 0.4f));

            if (kind == Kind.Blob)
            {
                glow = Art.MakeSprite("Glow", body, Art.SoftGlow, 58, Art.SpriteGlowMat, Palette.MonsterGlow.WithAlpha(0.18f));
                glow.transform.localPosition = new Vector3(0f, 0.35f, 0f);
                glow.transform.localScale = Vector3.one * 1.9f;
                var footL = Art.MakeSprite("FootL", body, MonsterArt.BlobFoot, 59);
                footL.transform.localPosition = new Vector3(-0.2f, 0.03f, 0f);
                var footR = Art.MakeSprite("FootR", body, MonsterArt.BlobFoot, 59);
                footR.transform.localPosition = new Vector3(0.22f, 0.03f, 0f);
                var hornL = Art.MakeSprite("HornL", body, MonsterArt.BlobHorn, 59);
                hornL.transform.localPosition = new Vector3(-0.2f, 0.66f, 0f);
                hornL.transform.localRotation = Quaternion.Euler(0, 0, 22f);
                var hornR = Art.MakeSprite("HornR", body, MonsterArt.BlobHorn, 59);
                hornR.transform.localPosition = new Vector3(0.18f, 0.68f, 0f);
                hornR.transform.localRotation = Quaternion.Euler(0, 0, -16f);
                hornR.transform.localScale = new Vector3(-1f, 1f, 1f);
                bodySr = Art.MakeSprite("Body", body, MonsterArt.BlobBody, 60);
                eyes = new SpriteRenderer[2];
                pupils = new SpriteRenderer[2];
                for (int i = 0; i < 2; i++)
                {
                    var eg = Art.MakeSprite("EyeGlow", body, Art.SoftGlow, 61, Art.SpriteGlowMat, Palette.MonsterEye.WithAlpha(0.5f));
                    eg.transform.localPosition = new Vector3(i == 0 ? 0.02f : 0.22f, 0.44f, 0f);
                    eg.transform.localScale = Vector3.one * 0.32f;
                    eyes[i] = Art.MakeSprite("Eye", body, MonsterArt.Eye, 62, Art.SpriteEmissiveMat, Palette.MonsterEye);
                    eyes[i].transform.localPosition = new Vector3(i == 0 ? 0.02f : 0.22f, 0.44f, 0f);
                    pupils[i] = Art.MakeSprite("Pupil", eyes[i].transform, MonsterArt.Pupil, 63);
                }
                extras = new[] { footL, footR, hornL, hornR };
                Radius = 0.42f;
            }
            else
            {
                glow = Art.MakeSprite("Glow", body, Art.SoftGlow, 58, Art.SpriteGlowMat, Palette.WispGlow.WithAlpha(0.35f));
                glow.transform.localScale = Vector3.one * 1.7f;
                tail = new Transform[3];
                tailPos = new Vector2[3];
                var tailSrs = new SpriteRenderer[3];
                for (int i = 0; i < 3; i++)
                {
                    tailSrs[i] = Art.MakeSprite("Tail" + i, root, MonsterArt.WispTail, 57 - i);
                    tail[i] = tailSrs[i].transform;
                    float s = 0.9f - i * 0.22f;
                    tail[i].localScale = Vector3.one * s;
                }
                bodySr = Art.MakeSprite("Body", body, MonsterArt.WispBody, 60);
                eyes = new SpriteRenderer[1];
                pupils = new SpriteRenderer[0];
                var eg = Art.MakeSprite("EyeGlow", body, Art.SoftGlow, 61, Art.SpriteGlowMat, new Color(1f, 0.95f, 0.8f, 0.45f));
                eg.transform.localPosition = new Vector3(0.06f, 0.04f, 0f);
                eg.transform.localScale = Vector3.one * 0.5f;
                eyes[0] = Art.MakeSprite("Eye", body, MonsterArt.WispEye, 62, Art.SpriteEmissiveMat, Color.white);
                eyes[0].transform.localPosition = new Vector3(0.06f, 0.04f, 0f);
                extras = tailSrs;
                Radius = 0.34f;
            }
            extraColors = new Color[extras.Length];
            for (int i = 0; i < extras.Length; i++) extraColors[i] = extras[i].color;

            hpBack = Art.MakeSprite("HpBack", root, Art.Pill, 70, Art.SpriteMat, new Color(0.02f, 0.03f, 0.08f, 0f));
            hpBack.drawMode = SpriteDrawMode.Sliced;
            hpBack.size = new Vector2(0.7f, 0.1f);
            hpFill = Art.MakeSprite("HpFill", root, Art.Pill, 71, Art.SpriteMat, Palette.HpA.WithAlpha(0f));
            hpFill.drawMode = SpriteDrawMode.Sliced;
            hpFill.size = new Vector2(0.66f, 0.07f);
            root.gameObject.SetActive(false);
        }

        public void Spawn(Vector2 at, Vector2 vel, float hpMul)
        {
            Id = ++nextId;
            Pos = at;
            Vel = vel;
            sizeMul = K == Kind.Blob ? Random.Range(0.92f, 1.12f) : Random.Range(0.95f, 1.08f);
            MaxHp = Hp = (K == Kind.Blob ? 36f : 24f) * hpMul;
            ContactDamage = K == Kind.Blob ? 12f : 10f;
            Alive = true;
            spawnT = 0f;
            flash = 0f;
            hpShow = 0f;
            hpDisplay = 1f;
            squash = squashVel = 0f;
            grounded = false;
            standing = Level.None;
            leapPlanned = false;
            hopTimer = Random.Range(0.2f, 0.5f);
            diveTimer = Random.Range(2.5f, 4.5f);
            windup = diveTime = 0f;
            t = Random.value * 10f;
            faceT = vel.x >= 0f ? 1f : -1f;
            if (tailPos != null) for (int i = 0; i < tailPos.Length; i++) tailPos[i] = at;
            root.gameObject.SetActive(true);
            SetBodyMaterial(false);
        }

        public void Deactivate()
        {
            Alive = false;
            root.gameObject.SetActive(false);
        }

        void SetBodyMaterial(bool white)
        {
            var mat = white ? Art.SpriteSolidMat : Art.SpriteMat;
            bodySr.sharedMaterial = mat;
            bodySr.color = Color.white;
            for (int i = 0; i < extras.Length; i++)
            {
                extras[i].sharedMaterial = mat;
                extras[i].color = white ? Color.white : extraColors[i];
            }
        }

        // ------------------------------------------------------------------ damage

        public void Hit(float dmg, Vector2 dir, float knock, bool big)
        {
            if (!Alive) return;
            Hp -= dmg;
            flash = big ? 0.1f : 0.07f;
            hpShow = 1.6f;
            Vel += dir.normalized * knock + Vector2.up * (K == Kind.Blob ? knock * 0.45f : 0f);
            if (K == Kind.Blob && Vel.y > 0.5f) { grounded = false; standing = Level.None; leapPlanned = false; }
            squashVel += big ? 14f : 9f;
            windup = 0f;
            diveTime = 0f;
            var fx = FxSystem.I;
            Vector2 c = Center;
            fx.Flash(c, big ? 2.2f : 1.4f, Color.white, 0.12f, 2.6f);
            fx.Sparks(c, dir, 110f, big ? 14 : 8, 5f, 12f, K == Kind.Blob ? Palette.MonsterGlow : Palette.WispGlow, 2.6f, 0.05f, 0.25f, 2f);
            Game.I.Hud.DamageNumber(c + new Vector2(0f, Radius + 0.3f), dmg, big);
            if (Hp <= 0f) Die(dir);
            else TimeFx.HitStop(big ? 0.05f : 0.03f, 0.06f);
        }

        void Die(Vector2 dir)
        {
            Alive = false;
            var fx = FxSystem.I;
            Vector2 c = Center;
            if (K == Kind.Blob) fx.Burst(c, Palette.MonsterTop, Palette.MonsterGlow, sizeMul);
            else fx.Burst(c, Palette.WispTop, Palette.WispGlow, 0.85f);
            fx.Motes(c, Vector2.up * 2f, Palette.MonsterEye, 6, 0.3f);
            var game = Game.I;
            game.Cam.AddTrauma(0.22f);
            game.Post.Impact(0.3f);
            TimeFx.HitStop(0.06f, 0.05f);
            root.gameObject.SetActive(false);
            game.Hud.OnMonsterKilled();
        }

        // ------------------------------------------------------------------ update

        public void Update(float dt, Player player)
        {
            t += dt;
            spawnT = Mathf.Min(1f, spawnT + dt / 0.45f);
            scaleNow = MathUtil.EaseOutBack(spawnT, 2.2f) * sizeMul;
            Vector2 toPlayer = player.Pos + new Vector2(0f, 0.8f) - Center;
            float targetFace = Mathf.Abs(toPlayer.x) > 0.2f ? Mathf.Sign(toPlayer.x) : faceT;

            if (K == Kind.Blob) UpdateBlob(dt, player, toPlayer);
            else UpdateWisp(dt, player, toPlayer);

            // monsters may emerge from the goal nets but are always nudged back onto the pitch
            if (Mathf.Abs(Pos.x) > Player.ArenaHalf) Vel.x -= Mathf.Sign(Pos.x) * 10f * dt;
            const float wall = 18.2f;
            if (Pos.x < -wall) { Pos.x = -wall; Vel.x = Mathf.Abs(Vel.x) * 0.5f; }
            if (Pos.x > wall) { Pos.x = wall; Vel.x = -Mathf.Abs(Vel.x) * 0.5f; }

            // face turn (smooth flip)
            faceT = Mathf.MoveTowards(faceT, targetFace, dt / 0.12f);
            MathUtil.Spring(ref squash, ref squashVel, 0f, 3.2f, 0.32f, dt);
            float sq = Mathf.Clamp(squash * 0.05f, -0.35f, 0.35f);
            float flipScale = Mathf.Sin(faceT * Mathf.PI * 0.5f);
            body.localScale = new Vector3(flipScale * (1f - sq) * scaleNow, (1f + sq) * scaleNow, 1f);
            root.position = new Vector3(Pos.x, Pos.y, 0f);

            // eyes: look at the player, blink now and then
            blinkTimer -= dt;
            if (blinkTimer <= 0f) { blinkTimer = Random.Range(1.8f, 4.5f); blink = 1f; }
            blink = Mathf.Max(0f, blink - dt / 0.12f);
            float eyeY = 1f - MathUtil.Bump(1f - blink) * 0.9f;
            Vector2 look = toPlayer.normalized;
            for (int i = 0; i < eyes.Length; i++)
            {
                float baseScale = K == Kind.Blob ? (i == 0 ? 0.9f : 1f) : 1f;
                eyes[i].transform.localScale = new Vector3(baseScale, baseScale * Mathf.Max(0.1f, eyeY), 1f);
            }
            for (int i = 0; i < pupils.Length; i++)
                pupils[i].transform.localPosition = new Vector3(Mathf.Abs(look.x) * 0.018f + 0.004f, look.y * 0.022f, 0f);

            // flash + glow
            if (flash > 0f)
            {
                flash -= Time.unscaledDeltaTime;
                SetBodyMaterial(flash > 0f);
            }
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 3.4f);
            Color gc = K == Kind.Blob ? Palette.MonsterGlow : Palette.WispGlow;
            float ga = (K == Kind.Blob ? 0.14f + 0.08f * pulse : 0.28f + 0.1f * pulse) + windup * 0.6f;
            glow.color = gc.WithAlpha(ga);

            // shadow on the surface below
            float floor = K == Kind.Blob && grounded ? Pos.y : Level.FloorBelow(Pos.x, Pos.y + 0.02f, 0.15f);
            float h = Mathf.Max(0f, Pos.y - floor - (K == Kind.Wisp ? 0.4f : 0f));
            float s = Mathf.Lerp(0.95f, 0.35f, Mathf.Clamp01(h / 4f)) * scaleNow;
            shadow.transform.position = new Vector3(Pos.x, floor + 0.02f, 0f);
            shadow.transform.localScale = new Vector3(s, s, 1f);
            shadow.color = new Color(0, 0, 0, Mathf.Lerp(0.42f, 0.08f, Mathf.Clamp01(h / 4f)));

            // tiny health bar that only appears after taking damage
            hpShow = Mathf.Max(0f, hpShow - dt);
            hpDisplay = MathUtil.Damp(hpDisplay, Mathf.Clamp01(Hp / MaxHp), 10f, dt);
            float ha = Mathf.Clamp01(hpShow * 3f);
            float topY = (K == Kind.Blob ? 1.0f : 0.62f) * scaleNow;
            Vector2 hp = new Vector2(K == Kind.Blob ? 0f : 0f, topY);
            hpBack.transform.localPosition = hp;
            hpBack.color = new Color(0.02f, 0.03f, 0.08f, 0.7f * ha);
            float w = Mathf.Max(0.07f, 0.66f * hpDisplay);
            hpFill.size = new Vector2(w, 0.07f);
            hpFill.transform.localPosition = hp + new Vector2(-(0.66f - w) * 0.5f, 0f);
            hpFill.color = Color.Lerp(Palette.HpA, Palette.HpB, hpDisplay).WithAlpha(ha);
        }

        const float BlobGravity = 24f;
        const float LeapCrouch = 0.34f, HopCrouch = 0.16f;
        const float MaxLeapRise = 2.7f, MaxLeapReach = 4.6f;

        void UpdateBlob(float dt, Player player, Vector2 toPlayer)
        {
            if (grounded)
            {
                Vel.x = Mathf.MoveTowards(Vel.x, 0f, 14f * dt);
                float before = hopTimer;
                hopTimer -= dt;
                // decide early whether the next hop is a leap, so the crouch can telegraph it
                if (before >= LeapCrouch && hopTimer < LeapCrouch) leapPlanned = PlanLeap(player, out _, out _);
                float crouch = leapPlanned ? LeapCrouch : HopCrouch;
                if (hopTimer < crouch && hopTimer > 0f) squashVel -= (leapPlanned ? 70f : 55f) * dt; // crouch anticipation
                if (hopTimer <= 0f && spawnT >= 1f) Hop(player, toPlayer);
            }
            else
            {
                Vel.y -= BlobGravity * dt;
            }

            float prevY = Pos.y;
            Pos += Vel * dt;
            float half = 0.2f * sizeMul;
            if (grounded)
            {
                // slid or got shoved off an edge
                if (Level.FloorBelow(Pos.x, Pos.y + 0.02f, half) < Pos.y - 0.01f) { grounded = false; standing = Level.None; }
            }
            else if (Vel.y <= 0f)
            {
                float floor = Level.FloorBelow(Pos.x, prevY + 0.02f, half, Level.None, out int index);
                if (Pos.y <= floor)
                {
                    float impact = -Vel.y;
                    squashVel -= Mathf.Clamp(impact * 1.6f, 4f, 18f);
                    if (impact > 3f) FxSystem.I.Dust(new Vector2(Pos.x, floor), new Vector2(Mathf.Sign(Vel.x), 0f), 3, 1.2f, 0.32f, 0.26f);
                    Pos.y = floor;
                    Vel.y = 0f;
                    grounded = true;
                    standing = index;
                }
            }
        }

        void Hop(Player player, Vector2 toPlayer)
        {
            hopTimer = Random.Range(0.45f, 0.85f);
            bool planned = leapPlanned;
            leapPlanned = false;
            float dir = Mathf.Sign(toPlayer.x == 0f ? 1f : toPlayer.x);

            // the player is up on a higher level: leap after them (via a lower step if needed)
            if (PlanLeap(player, out float top, out float landX))
            {
                if (planned) { Leap(top, landX); return; }
                leapPlanned = true;        // not telegraphed yet: crouch properly first
                hopTimer = LeapCrouch;
                return;
            }
            if (player.GroundY > Pos.y + 0.6f)
            {
                // nothing reachable from here: move towards the closest way up
                if (NearestStep(player, out float stepX)) dir = Mathf.Sign(stepX - Pos.x);
            }
            else if (player.GroundY < Pos.y - 0.6f && standing != Level.None)
            {
                // the player is below: bounce to the nearest edge and drop after them
                var p = Level.Platforms[standing];
                if (player.Pos.x > p.X0 - 0.6f && player.Pos.x < p.X1 + 0.6f) dir = Pos.x - p.X0 < p.X1 - Pos.x ? -1f : 1f;
            }
            Vel = new Vector2(dir * Random.Range(2.2f, 3.4f), Random.Range(5.8f, 7.6f));
            grounded = false;
            standing = Level.None;
            squashVel += 12f;
        }

        /// <summary>Is there a platform one leap up that brings the blob closer to a player standing higher?</summary>
        bool PlanLeap(Player player, out float top, out float landX)
        {
            top = 0f;
            landX = Pos.x;
            float playerFloor = player.GroundY;
            if (!grounded || playerFloor < Pos.y + 0.6f) return false;
            int playerPlatform = player.Grounded ? player.OnPlatform : Level.None;
            int best = Level.None;
            float bestScore = float.MaxValue, bestX = 0f;
            var plats = Level.Platforms;
            for (int i = 0; i < plats.Length; i++)
            {
                var p = plats[i];
                if (p.Y < Pos.y + 0.5f || p.Y > Pos.y + MaxLeapRise || p.Y > playerFloor + 0.1f) continue;
                // land a little inside, on the side facing the player
                float lx = Level.ClampOnto(p, Mathf.Lerp(Pos.x, player.Pos.x, 0.35f), 0.45f);
                float reach = Mathf.Abs(lx - Pos.x);
                if (reach > MaxLeapReach) continue;
                float score = reach + Mathf.Abs(Level.ClampOnto(p, player.Pos.x, 0f) - player.Pos.x) * 0.7f - (i == playerPlatform ? 2.5f : 0f);
                if (score < bestScore) { bestScore = score; best = i; bestX = lx; }
            }
            if (best == Level.None) return false;
            top = plats[best].Y;
            landX = bestX;
            return true;
        }

        /// <summary>x of the closest platform a leap could start under, for walking towards it.</summary>
        bool NearestStep(Player player, out float x)
        {
            x = Pos.x;
            float bestD = float.MaxValue;
            foreach (var p in Level.Platforms)
            {
                if (p.Y < Pos.y + 0.5f || p.Y > Pos.y + MaxLeapRise || p.Y > player.GroundY + 0.1f) continue;
                float cx = Level.ClampOnto(p, Pos.x, 0.45f);
                float d = Mathf.Abs(cx - Pos.x) + Mathf.Abs(p.Center - player.Pos.x) * 0.5f;
                if (d < bestD) { bestD = d; x = cx; }
            }
            return bestD < float.MaxValue;
        }

        void Leap(float top, float landX)
        {
            const float clearance = 0.6f;
            float apex = top - Pos.y + clearance;
            float vy = Mathf.Sqrt(2f * BlobGravity * apex);
            float flight = vy / BlobGravity + Mathf.Sqrt(2f * clearance / BlobGravity);
            Vel = new Vector2((landX - Pos.x) / flight, vy);
            grounded = false;
            standing = Level.None;
            squashVel += 18f;
            FxSystem.I.Dust(Pos, new Vector2(-Mathf.Sign(Vel.x + 0.001f), 0.2f), 4, 1.4f, 0.34f, 0.28f);
        }

        void UpdateWisp(float dt, Player player, Vector2 toPlayer)
        {
            float side = Pos.x > player.Pos.x ? 1f : -1f;
            Vector2 hover = player.Pos + new Vector2(side * 2.6f, 2.4f + Mathf.Sin(t * 1.3f) * 0.45f);

            if (windup > 0f)
            {
                windup += dt;
                Vel = MathUtil.Damp(Vel, -toPlayer.normalized * 0.8f, 6f, dt);
                if (windup > 0.5f) { windup = 0f; diveTime = 0.55f; Vel = toPlayer.normalized * 10.5f; squashVel += 10f; }
            }
            else if (diveTime > 0f)
            {
                diveTime -= dt;
                Vel = MathUtil.Damp(Vel, Vel.normalized * 10.5f, 2f, dt);
                if (Random.value < dt * 30f)
                    FxSystem.I.Spawn(FxLayer.Back, true, Art.CellGlow, Pos, -Vel * 0.05f, 0.35f, 0.3f, 0f, Palette.WispGlow, Palette.WispGlow.WithAlpha(0f), 2f);
            }
            else
            {
                Vector2 desired = Vector2.ClampMagnitude((hover - Pos) * 1.5f, 4.2f);
                Vel = MathUtil.Damp(Vel, desired, 2.6f, dt);
                diveTimer -= dt;
                if (diveTimer <= 0f && spawnT >= 1f) { diveTimer = Random.Range(3f, 5f); windup = 0.0001f; }
            }
            Pos += Vel * dt;
            if (Pos.y < 0.5f) { Pos.y = 0.5f; Vel.y = Mathf.Abs(Vel.y) * 0.5f; }
            if (Pos.y > 9.2f) { Pos.y = 9.2f; Vel.y = -Mathf.Abs(Vel.y) * 0.5f; }

            // tail: each segment chases the previous one (smooth trailing motion)
            Vector2 anchor = Pos + new Vector2(-Mathf.Sign(faceT) * 0.18f, -0.12f);
            for (int i = 0; i < tail.Length; i++)
            {
                Vector2 target = i == 0 ? anchor : tailPos[i - 1] + new Vector2(0f, -0.06f);
                tailPos[i] = MathUtil.Damp(tailPos[i], target, 14f - i * 3f, dt);
                Vector2 d = tailPos[i] - target;
                float maxD = 0.16f + i * 0.02f;
                if (d.magnitude > maxD) tailPos[i] = target + d.normalized * maxD;
                tail[i].position = new Vector3(tailPos[i].x, tailPos[i].y + Mathf.Sin(t * 5f + i) * 0.02f, 0f);
                tail[i].localScale = Vector3.one * (0.9f - i * 0.22f) * scaleNow;
            }
        }
    }
}
