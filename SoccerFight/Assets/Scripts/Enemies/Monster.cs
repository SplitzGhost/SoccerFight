using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Every enemy: two ways to move (hopping blob, floating wisp) carrying nine behaviour archetypes,
    /// elite affixes, mini-boss specials and stage bosses — and a body of its own for each archetype
    /// and boss (see <see cref="MonsterArt"/>): parts that sway, flap, swing and glow, eyes that blink
    /// and follow the player, chains that trail behind. All motion is spring-based squash & stretch;
    /// hits flash white, knock back and pop a damage number. Stats come from the difficulty level.
    /// </summary>
    public sealed partial class Monster
    {
        public enum Kind { Blob, Wisp }

        /// <summary>The body a spawn will wear.</summary>
        public static Look LookFor(in SpawnSpec s) => s.Rank == Rank.Boss && s.Boss != null ? s.Boss.Look : MonsterArt.LookOf(s.Type);

        public struct SpawnSpec
        {
            public EnemyType Type;
            public Vector2 At, Vel;
            public float Level;
            public Rank Rank;
            public int Affixes;
            public StageTheme Theme;
            public string Name;
            public BossDef Boss;
        }

        static int nextId;
        static Sprite iceShard;
        static readonly List<EliteAffix> affixPool = new List<EliteAffix>();
        /// <summary>Capture tool: monsters stay where they were placed and only animate.</summary>
        public static bool Hold;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { nextId = 0; Hold = false; iceShard = null; }

        // ---- identity and stats
        public int Id { get; private set; }
        public Kind K { get; private set; }
        public EnemyType Type { get; private set; }
        public Rank Rank { get; private set; }
        public readonly List<EliteAffix> Affixes = new List<EliteAffix>();
        public string DisplayName { get; private set; }
        public BossDef Boss { get; private set; }
        public Vector2 Pos, Vel;
        public float Radius;
        public float Hp, MaxHp;
        public bool Alive;
        public float ContactDamage;
        public float DifficultyLevel { get; private set; }
        public Vector2 Center => K == Kind.Blob ? Pos + new Vector2(0f, 0.36f * scaleNow) : Pos;
        public bool Named => Rank != Rank.Normal;
        public bool Has(EliteAffix a) => Affixes.Contains(a);
        public float DamageTakenMul => (Has(EliteAffix.Armored) ? 0.65f : 1f) * (ExposeTime > 0f ? 1.4f : 1f);
        public bool Halted => FreezeTime > 0f || StunTime > 0f;
        public float TopY => Center.y + Radius + 0.15f;
        public int BossPhase => Rank != Rank.Boss ? 0 : Hp > MaxHp * 0.66f ? 0 : Hp > MaxHp * 0.33f ? 1 : 2;

        // ---- status effects
        public float BurnTime, BurnDps, SlowTime, SlowAmount, FreezeTime;
        /// <summary>Knocked over (slide tackle, nutmeg) or held by the whistle: no AI, only gravity.</summary>
        public float StunTime;
        /// <summary>Nutmegged: takes more damage from everything for a while.</summary>
        public float ExposeTime;
        public int HitCount;
        public bool Burning => BurnTime > 0f;
        public bool Slowed => SlowTime > 0f || FreezeTime > 0f;
        public float PullStrength;       // cyclone / singularity pull
        public Vector2 PullTo;

        // ---- rendering
        sealed class PartRt { public PartDef D; public SpriteRenderer Sr; public Transform T; public float GlowK = 1f; }
        sealed class EyeRt { public EyeDef D; public SpriteRenderer Sr, Pupil; public Transform T; }
        sealed class ChainRt { public ChainDef D; public SpriteRenderer[] Srs; public Vector2[] Pos; }

        Transform root, body;
        SpriteRenderer bodySr, glow, shadow, hpBack, hpFill, aura, auraRing;
        SpriteRenderer frost, heat;
        SpriteRenderer[] ice;
        float frostK, heatK;
        static readonly float[] IceAngles = { -52f, -18f, 20f, 58f };
        static readonly float[] IceSizes = { 0.85f, 1.15f, 0.95f, 0.75f };
        PartRt[] parts;
        EyeRt[] eyes;
        ChainRt[] chains;
        public Look BuiltLook { get; private set; }
        LookDef look;
        StageTheme theme;
        float spawnT, scaleNow = 1f, flash, hpShow, hpDisplay = 1f, squash, squashVel, faceT = -1f, blinkTimer, blink, t, tilt;
        float sizeMul = 1f, speedMul = 1f, fade = 1f;
        Color auraColor;
        int standVersion;

        // ---- behaviour
        float hopTimer;
        bool grounded;
        int standing = Level.None;     // platform under a grounded blob
        bool leapPlanned;              // the next hop is a big leap up to a platform (longer crouch)
        Vector2 focus;                  // what the AI currently believes is the player
        float diveTimer, windup, diveTime;
        float attackTimer, attackWind;  // spitter / lantern / brute charge / mini special
        float chargeTime;
        int burst;
        float burstTimer;
        float regenDelay;
        float primeTime;               // bomber fuse
        bool detonated;                // blew itself up (no second death blast)
        float teleportT = -1f;

        // ---- boss brain
        int moveIndex;
        BossMove move;
        float moveT, moveCd;
        bool moveActive;
        int lastPhase;

        // ---- warnings (BossTells): where the current leap lands / where a teleport ends up
        Vector2 tellPos;
        float leapT, leapAirTime;
        int TellKey(int slot) => Id * 8 + slot;

        /// <summary>Creates the renderers for one body (parts, eyes, chains). The monster is then pooled for that body.</summary>
        public void Build(Transform parent, LookDef def)
        {
            BuiltLook = def.Look;
            K = def.Wisp ? Kind.Wisp : Kind.Blob;
            root = new GameObject(def.Look.ToString()).transform;
            root.SetParent(parent, false);
            // each monster sorts as one piece, so overlapping monsters never interleave their parts
            root.gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>().sortingOrder = 60;
            body = new GameObject("Body").transform;
            body.SetParent(root, false);
            // the shadow lies on the ground under everything, outside the sorting group
            shadow = Art.MakeSprite("Shadow", parent, Art.Shadow, -43, Art.SpriteMat, new Color(0, 0, 0, 0.4f));
            aura = Art.MakeSprite("Aura", body, Art.SoftGlow, 55, Art.SpriteGlowMat, Color.clear);
            auraRing = Art.MakeSprite("AuraRing", body, Art.Ring, 56, Art.SpriteGlowMat, Color.clear);
            glow = Art.MakeSprite("Glow", body, Art.SoftGlow, 57, Art.SpriteGlowMat, Color.clear);
            bodySr = Art.MakeSprite("Body", body, def.Body, 60);
            // status dressing: a frosty glow with ice shards (slowed / frozen), a warm glow (burning)
            frost = Art.MakeSprite("Frost", body, Art.SoftGlow, 59, Art.SpriteAddMat, Color.clear);
            heat = Art.MakeSprite("Heat", body, Art.SoftGlow, 59, Art.SpriteAddMat, Color.clear);
            ice = new SpriteRenderer[IceAngles.Length];
            for (int i = 0; i < ice.Length; i++) ice[i] = Art.MakeSprite("Ice" + i, body, IceShard, 69, Art.SpriteMat, Color.clear);

            parts = new PartRt[def.Parts.Count];
            for (int i = 0; i < parts.Length; i++)
            {
                var d = def.Parts[i];
                Transform at = d.Parent >= 0 && d.Parent < i ? parts[d.Parent].T : body;
                var sr = Art.MakeSprite(d.Name, at, d.Sprite, d.Order);
                parts[i] = new PartRt { D = d, Sr = sr, T = sr.transform };
            }
            eyes = new EyeRt[def.Eyes.Count];
            for (int i = 0; i < eyes.Length; i++)
            {
                var d = def.Eyes[i];
                Transform at = d.Parent >= 0 ? parts[d.Parent].T : body;
                int order = Mathf.Max(62, d.Parent >= 0 ? def.Parts[d.Parent].Order + 1 : 62);
                var sr = Art.MakeSprite("Eye", at, d.Wide ? MonsterArt.WispEye : MonsterArt.Eye, order, Art.SpriteEmissiveMat, Color.white);
                var e = new EyeRt { D = d, Sr = sr, T = sr.transform };
                if (!d.Wide) { e.Pupil = Art.MakeSprite("Pupil", sr.transform, MonsterArt.Pupil, order + 1); }
                eyes[i] = e;
            }
            chains = new ChainRt[def.Chains.Count];
            for (int i = 0; i < chains.Length; i++)
            {
                var d = def.Chains[i];
                var c = new ChainRt { D = d, Srs = new SpriteRenderer[d.Count], Pos = new Vector2[d.Count] };
                for (int k = 0; k < d.Count; k++) c.Srs[k] = Art.MakeSprite(def.Look + " Chain" + i + "." + k, root, d.Sprite, d.Order - k);
                chains[i] = c;
            }

            hpBack = Art.MakeSprite("HpBack", root, Art.Pill, 70, Art.SpriteMat, new Color(0.02f, 0.03f, 0.08f, 0f));
            hpBack.drawMode = SpriteDrawMode.Sliced;
            hpBack.size = new Vector2(0.7f, 0.1f);
            hpFill = Art.MakeSprite("HpFill", root, Art.Pill, 71, Art.SpriteMat, Palette.HpA.WithAlpha(0f));
            hpFill.drawMode = SpriteDrawMode.Sliced;
            hpFill.size = new Vector2(0.66f, 0.07f);
            Show(false);
        }

        // ------------------------------------------------------------------ spawn

        public void Spawn(in SpawnSpec s)
        {
            Id = ++nextId;
            Ghost = false;
            netCount = 0;
            Type = s.Type;
            Rank = s.Rank;
            Boss = s.Boss;
            DifficultyLevel = s.Level;
            theme = s.Theme ?? StageThemes.All[0];
            look = MonsterArt.Get(theme, LookFor(s));
            Pos = s.At;
            Vel = s.Vel;
            var def = EnemyDef.Get(Type);
            K = def.Body;

            float rankHp = 1f, rankDmg = 1f, rankSize = 1f;
            Affixes.Clear();
            if (Rank == Rank.Elite) { rankHp = Difficulty.EliteHealth; rankDmg = Difficulty.EliteDamage; rankSize = Difficulty.EliteSize; }
            else if (Rank == Rank.MiniBoss) { rankDmg = Difficulty.MiniDamage; rankSize = Difficulty.MiniSize; }
            else if (Rank == Rank.Boss) { rankDmg = Difficulty.BossDamage; rankSize = K == Kind.Blob ? 1.95f : 2.1f; }
            if (Rank == Rank.Elite || Rank == Rank.MiniBoss) RollAffixes(s.Affixes);

            float jitter = Rank == Rank.Normal ? (K == Kind.Blob ? Random.Range(0.92f, 1.12f) : Random.Range(0.95f, 1.08f)) : 1f;
            sizeMul = def.Size * rankSize * jitter;
            float baseHp = Rank == Rank.Boss ? Difficulty.BossBaseHealth : Rank == Rank.MiniBoss ? Difficulty.MiniBaseHealth : def.Hp * rankHp;
            MaxHp = baseHp * Difficulty.HealthMul(DifficultyLevel);
            // two players hit twice as often: a duo's monsters are tougher (the partner's ghost takes the host's number)
            if (Coop.IsHost) MaxHp *= Rank == Rank.Boss ? Difficulty.DuoBossHealth : Difficulty.DuoHealth;
            Hp = MaxHp;
            float baseDmg = Rank == Rank.Boss ? Difficulty.BossBaseDamage : Rank == Rank.MiniBoss ? Difficulty.MiniBaseDamage : def.Damage;
            ContactDamage = baseDmg * Difficulty.DamageMul(DifficultyLevel) * rankDmg;
            speedMul = Difficulty.SpeedMul(DifficultyLevel) * (Has(EliteAffix.Swift) ? 1.35f : 1f) * (Has(EliteAffix.Frenzied) ? 1.15f : 1f);
            Radius = (K == Kind.Blob ? 0.42f : 0.34f) * sizeMul;
            DisplayName = s.Name;

            Alive = true;
            spawnT = 0f; flash = 0f; hpShow = 0f; hpDisplay = 1f; fade = 1f;
            squash = squashVel = 0f;
            grounded = false; standing = Level.None; leapPlanned = false; tilt = 0f;
            hopTimer = Random.Range(0.2f, 0.5f);
            diveTimer = Random.Range(2.5f, 4.5f);
            windup = diveTime = 0f;
            attackTimer = Random.Range(1.8f, 3f); attackWind = 0f; chargeTime = 0f; burst = 0; burstTimer = 0f;
            primeTime = 0f; detonated = false; slamPending = false; teleportT = -1f; regenDelay = 0f;
            BurnTime = BurnDps = SlowTime = SlowAmount = FreezeTime = 0f; HitCount = 0; PullStrength = 0f;
            StunTime = ExposeTime = 0f;
            frostK = heatK = 0f;
            moveIndex = 0; moveT = 0f; moveCd = 2.2f; moveActive = false; lastPhase = 0;
            t = Random.value * 10f;
            faceT = s.Vel.x >= 0f ? 1f : -1f;
            ApplyLook();
            ResetChains();
            // stays hidden until its first UpdateVisuals: spawned after the monsters' update, it would
            // otherwise show for a frame wherever the pooled transform was left (or mid-pitch when new)
            Show(false);
            SetBodyMaterial(false);
        }

        void ResetChains()
        {
            foreach (var c in chains) for (int k = 0; k < c.Pos.Length; k++) c.Pos[k] = Pos;
        }

        void RollAffixes(int count)
        {
            affixPool.Clear();
            affixPool.Add(EliteAffix.Swift); affixPool.Add(EliteAffix.Armored); affixPool.Add(EliteAffix.Regenerating);
            affixPool.Add(EliteAffix.Volatile); affixPool.Add(EliteAffix.Frenzied);
            for (int i = 0; i < count && affixPool.Count > 0; i++)
            {
                int k = Random.Range(0, affixPool.Count);
                Affixes.Add(affixPool[k]);
                affixPool.RemoveAt(k);
            }
        }

        static Material MatOf(PartMat m) => m == PartMat.Glow ? Art.SpriteGlowMat : m == PartMat.Emissive ? Art.SpriteEmissiveMat : Art.SpriteMat;

        /// <summary>Put on the body in this stage's colours (the renderers were built for the same body).</summary>
        void ApplyLook()
        {
            bodySr.sprite = look.Body;
            for (int i = 0; i < parts.Length; i++)
            {
                var p = parts[i];
                p.D = look.Parts[i];
                p.Sr.sprite = p.D.Sprite;
                p.Sr.sortingOrder = p.D.Order;
                p.T.localPosition = p.D.Pos;
                p.T.localRotation = Quaternion.Euler(0f, 0f, p.D.Rot);
                p.T.localScale = new Vector3(p.D.Scale.x, p.D.Scale.y, 1f);
                p.GlowK = 1f;
            }
            for (int i = 0; i < eyes.Length; i++)
            {
                var e = eyes[i];
                e.D = look.Eyes[i];
                e.T.localPosition = e.D.Pos;
                e.Sr.color = e.D.Wide ? Color.white : e.D.Color ?? look.Eye;
                if (e.Pupil != null) e.Pupil.enabled = e.D.Pupil;
            }
            for (int i = 0; i < chains.Length; i++)
            {
                var c = chains[i];
                c.D = look.Chains[i];
                foreach (var sr in c.Srs) sr.sprite = c.D.Sprite;
            }
            glow.transform.localPosition = look.GlowPos;
            glow.transform.localScale = Vector3.one * look.GlowSize;

            // rank dressing: affix aura for elites and mini-bosses (bosses wear their own body instead)
            auraColor = Affixes.Count > 0 && Rank != Rank.Boss ? EnemyDef.AffixColor(Affixes[0]) : Rank != Rank.Normal ? theme.Accent : Color.clear;
            aura.transform.localPosition = look.AuraPos;
            auraRing.transform.localPosition = look.AuraPos;
        }

        void Show(bool on)
        {
            root.gameObject.SetActive(on);
            shadow.gameObject.SetActive(on);
        }

        public void Deactivate()
        {
            Alive = false;
            Show(false);
        }

        void SetBodyMaterial(bool white)
        {
            bodySr.sharedMaterial = white ? Art.SpriteSolidMat : Art.SpriteMat;
            bodySr.color = Color.white;
            foreach (var p in parts)
            {
                bool glowing = p.D.Mat == PartMat.Glow;
                p.Sr.sharedMaterial = white && !glowing ? Art.SpriteSolidMat : MatOf(p.D.Mat);
                if (white && !glowing) p.Sr.color = Color.white;
            }
            foreach (var c in chains)
            {
                bool glowing = c.D.Mat == PartMat.Glow;
                foreach (var sr in c.Srs)
                {
                    sr.sharedMaterial = white && !glowing ? Art.SpriteSolidMat : MatOf(c.D.Mat);
                    if (white && !glowing) sr.color = Color.white;
                }
            }
        }

        // ------------------------------------------------------------------ damage and status

        /// <summary>Applies damage (already scaled by the caller). Returns true if this killed the monster.</summary>
        public bool Hit(float dmg, Vector2 dir, float knock, bool big, bool crit = false, bool boosted = false)
        {
            if (!Alive) return false;
            // duo partner's screen: show the hit now, the host decides what it does
            if (Ghost) { GhostHit(dmg, dir, knock, big, crit, boosted); return false; }
            Hp -= dmg;
            HitCount++;
            regenDelay = 1.2f;
            flash = big ? 0.1f : 0.07f;
            hpShow = 1.6f;
            float knockMul = Rank == Rank.Boss ? 0.12f : Rank == Rank.MiniBoss ? 0.35f : Rank == Rank.Elite ? 0.7f : 1f;
            if (Has(EliteAffix.Armored)) knockMul *= 0.6f;
            Vel += dir.normalized * knock * knockMul + Vector2.up * (K == Kind.Blob ? knock * 0.45f * knockMul : 0f);
            if (K == Kind.Blob && Vel.y > 0.5f && Rank != Rank.Boss) { grounded = false; standing = Level.None; leapPlanned = false; }
            squashVel += big ? 14f : 9f;
            if (Rank == Rank.Normal) { windup = 0f; diveTime = 0f; attackWind = 0f; }
            var fx = FxSystem.I;
            Vector2 c = Center;
            fx.Flash(c, (big ? 2.2f : 1.4f) * Mathf.Sqrt(sizeMul), Color.white, 0.12f, 2.6f);
            Color spark = look.Glow;
            fx.Sparks(c, dir, 110f, big ? 14 : 8, 5f, 12f, spark, 2.6f, 0.05f, 0.25f, 2f);
            Game.I.Hud.DamageNumber(c + new Vector2(0f, Radius + 0.3f), dmg, big, crit, boosted);
            if (Hp <= 0f) { Die(dir); return true; }
            // the partner's hits don't freeze this screen
            if (Rank != Rank.Boss && !ApplyingRemote) TimeFx.HitStop(big ? 0.05f : 0.03f, 0.06f);
            return false;
        }

        public void Ignite(float dps, float time)
        {
            if (!Alive) return;
            if (Ghost) Coop.SendStatus(this, StatusKind.Ignite, dps, time);
            BurnDps = Mathf.Max(BurnDps, dps);
            BurnTime = Mathf.Max(BurnTime, time);
        }

        public void Chill(float amount, float time)
        {
            if (!Alive) return;
            if (Ghost) Coop.SendStatus(this, StatusKind.Chill, amount, time);
            SlowAmount = Mathf.Max(SlowAmount, amount);
            SlowTime = Mathf.Max(SlowTime, time);
        }

        public void Freeze(float time)
        {
            if (!Alive || Rank == Rank.Boss) return;
            if (Ghost) Coop.SendStatus(this, StatusKind.Freeze, time, 0f);
            FreezeTime = Mathf.Max(FreezeTime, Rank == Rank.MiniBoss ? time * 0.4f : time);
            FxSystem.I.Ring(FxLayer.Front, Center, 0.1f, Radius * 2.4f, 0.12f, 0.01f, 0.25f, Color.white, new Color(0.6f, 0.9f, 1f, 0f), 2f);
        }

        /// <summary>Knocked over or held: no attacks, no walking, just gravity. Bosses only ever briefly.</summary>
        public void Stun(float time)
        {
            if (!Alive) return;
            if (Ghost) Coop.SendStatus(this, StatusKind.Stun, time, 0f);
            if (Rank == Rank.Boss) time = Mathf.Min(time, 1.2f);
            StunTime = Mathf.Max(StunTime, time);
            windup = 0f;
            diveTime = 0f;
            attackWind = 0f;
            squashVel += 8f;
            FxSystem.I.Sparkles(Center + new Vector2(0f, Radius * 0.9f), Radius * 0.9f, 4, new Color(1f, 0.95f, 0.7f), 2.4f, 0.5f);
        }

        /// <summary>Nutmegged: everything hits harder while the mark lasts.</summary>
        public void Expose(float time)
        {
            if (!Alive) return;
            if (Ghost) Coop.SendStatus(this, StatusKind.Expose, time, 0f);
            ExposeTime = Mathf.Max(ExposeTime, time);
        }

        void Die(Vector2 dir)
        {
            Alive = false;
            var fx = FxSystem.I;
            Vector2 c = Center;
            Color top = look.Top;
            Color g = look.Glow;
            fx.Burst(c, top, g, K == Kind.Blob ? sizeMul : 0.85f * sizeMul);
            fx.Motes(c, Vector2.up * 2f, look.Eye, 6, 0.3f);
            if (Named)
            {
                fx.Ring(FxLayer.Front, c, 0.2f, 2.6f * Mathf.Sqrt(sizeMul), 0.3f, 0.02f, 0.5f, Color.white, auraColor.WithAlpha(0f), 2.4f);
                fx.Sparkles(c, 0.8f * sizeMul, Rank == Rank.Boss ? 30 : 12, Palette.Gold, 2.8f, 0.9f);
            }
            var game = Game.I;
            // the partner's kills land softer on this screen (a boss always shakes everything)
            bool mine = Ghost ? GhostKilledHere : !ApplyingRemote;
            float feel = mine || Rank == Rank.Boss ? 1f : 0.35f;
            game.Cam.AddTrauma((Rank == Rank.Boss ? 0.8f : Named ? 0.4f : 0.22f) * feel);
            game.Post.Impact((Rank == Rank.Boss ? 1f : 0.3f) * feel);
            if (mine || Rank == Rank.Boss) TimeFx.HitStop(Rank == Rank.Boss ? 0.14f : 0.06f, 0.05f);
            if (Rank == Rank.Boss) TimeFx.SlowMo(0.25f, 0.5f, 0.8f);
            Show(false);
            game.Waves.OnDied(this);
        }

        /// <summary>Bomber fuse or volatile affix: a blast that hurts the player.</summary>
        void Explode(float radius, float damage)
        {
            Vector2 c = Center;
            var fx = FxSystem.I;
            fx.Flash(c, radius * 1.6f, Palette.BlastOrange, 0.2f, 2.6f);
            fx.Ring(FxLayer.Front, c, 0.2f, radius, 0.35f, 0.02f, 0.35f, Color.white, Palette.BlastOrange.WithAlpha(0f), 2.4f);
            fx.Sparks(c, Vector2.up, 200f, 16, 5f, 12f, Palette.BlastOrange, 2.6f, 0.05f, 0.35f, 10f);
            fx.Dust(c, Vector2.right, 5, 2.4f, 0.5f, 0.35f);
            fx.Dust(c, Vector2.left, 5, 2.4f, 0.5f, 0.35f);
            Game.I.Cam.AddTrauma(0.3f);
            var p = Game.I.Player;
            if (!p.Dead && (p.Pos + new Vector2(0f, 0.8f) - c).magnitude < radius) p.TakeDamage(damage, c);
        }

        // ------------------------------------------------------------------ update

        public void Update(float dt, Player player)
        {
            if (Ghost) { UpdateGhost(dt, player); return; }
            t += dt;
            spawnT = Mathf.Min(1f, spawnT + dt / (Rank == Rank.Boss ? 0.9f : 0.45f));
            scaleNow = MathUtil.EaseOutBack(spawnT, 2.2f) * sizeMul;
            // the decoy takes the attention while it lasts: every chase and every shot aims here
            focus = Decoys.Focus(player.Pos);
            Vector2 toPlayer = focus + new Vector2(0f, 0.8f) - Center;
            float targetFace = Mathf.Abs(toPlayer.x) > 0.2f ? Mathf.Sign(toPlayer.x) : faceT;

            UpdateStatus(dt);
            if (!Alive) return;
            if (Hold)
            {
                grounded = K == Kind.Blob;
                faceT = Mathf.MoveTowards(faceT, targetFace, dt / 0.12f);
                UpdateVisuals(dt, toPlayer);
                return;
            }

            // riding a moving platform (a new layout drops the old index)
            if (K == Kind.Blob && grounded && standing != Level.None)
            {
                if (standVersion == Level.Version) Pos += Level.DeltaOf(standing);
                else { grounded = false; standing = Level.None; }
            }

            float slow = Halted ? 0f : 1f - SlowAmount * (SlowTime > 0f ? 1f : 0f);
            float spd = speedMul * slow * StageMechanics.EnemySpeedBoost;
            if (Halted)
            {
                // frozen or knocked over: only gravity and knockback move it
                if (K == Kind.Blob && !grounded) Vel.y -= BlobGravity * dt;
                Vel *= Mathf.Exp(-6f * dt);
                Vector2 prev = Pos;
                Pos += Vel * dt;
                if (K == Kind.Blob) LandCheck(prev.y);
            }
            else if (Rank == Rank.Boss) UpdateBoss(dt, player, toPlayer, spd);
            else if (K == Kind.Blob) UpdateBlob(dt, player, toPlayer, spd);
            else UpdateWisp(dt, player, toPlayer, spd);

            // cyclone / black-hole pull
            if (PullStrength > 0f && Rank != Rank.Boss)
            {
                Vector2 d = PullTo - Center;
                Vel = MathUtil.Damp(Vel, d * PullStrength, 6f, dt);
                if (K == Kind.Blob && d.y > 0.3f) { grounded = false; standing = Level.None; }
                PullStrength = 0f;
            }

            // monsters may emerge from the goal nets but are always nudged back onto the pitch
            if (Mathf.Abs(Pos.x) > Player.ArenaHalf) Vel.x -= Mathf.Sign(Pos.x) * 10f * dt;
            const float wall = 18.2f;
            if (Pos.x < -wall) { Pos.x = -wall; Vel.x = Mathf.Abs(Vel.x) * 0.5f; }
            if (Pos.x > wall) { Pos.x = wall; Vel.x = -Mathf.Abs(Vel.x) * 0.5f; }

            faceT = Mathf.MoveTowards(faceT, targetFace, dt / (Rank == Rank.Boss ? 0.25f : 0.12f));
            UpdateVisuals(dt, toPlayer);
        }

        void UpdateStatus(float dt)
        {
            if (BurnTime > 0f)
            {
                BurnTime -= dt;
                float d = BurnDps * dt;
                Hp -= d;
                hpShow = Mathf.Max(hpShow, 0.6f);
                if (Random.value < dt * 14f)
                    FxSystem.I.Sparks(Center + Random.insideUnitCircle * Radius * 0.7f, Vector2.up, 40f, 1, 1.5f, 3.5f, Palette.BlastOrange, 2.4f, 0.04f, 0.3f, -4f);
                if (Hp <= 0f) { Die(Vector2.up); return; }
            }
            if (SlowTime > 0f) SlowTime -= dt; else SlowAmount = 0f;
            if (FreezeTime > 0f) FreezeTime -= dt;
            if (StunTime > 0f) StunTime -= dt;
            if (ExposeTime > 0f) ExposeTime -= dt;
            if (Has(EliteAffix.Regenerating))
            {
                regenDelay -= dt;
                if (regenDelay <= 0f && Hp < MaxHp) Hp = Mathf.Min(MaxHp, Hp + MaxHp * 0.025f * dt);
            }
        }

        // ------------------------------------------------------------------ blob body

        const float BlobGravity = 24f;
        const float LeapCrouch = 0.34f, HopCrouch = 0.16f;
        const float MaxLeapRise = 2.7f, MaxLeapReach = 4.6f;

        void UpdateBlob(float dt, Player player, Vector2 toPlayer, float spd)
        {
            bool frenzied = Has(EliteAffix.Frenzied);
            if (Type == EnemyType.Bomber && primeTime > 0f)
            {
                // fuse lit: sizzle, swell, pop
                primeTime -= dt;
                Vel.x = Mathf.MoveTowards(Vel.x, 0f, 20f * dt);
                squashVel += Mathf.Sin(t * 60f) * 30f * dt;
                if (Random.value < dt * 30f) FxSystem.I.Sparks(Center + Vector2.up * Radius, Vector2.up, 60f, 1, 2f, 4f, Palette.Gold, 2.6f, 0.04f, 0.2f);
                if (primeTime <= 0f) { Explode(1.9f * Mathf.Sqrt(sizeMul), ContactDamage * 1.4f); detonated = true; Hp = 0f; Die(Vector2.up); return; }
            }
            else if (Type == EnemyType.Brute && (attackWind > 0f || chargeTime > 0f))
            {
                UpdateBruteCharge(dt, player, toPlayer, spd);
            }
            else if (grounded)
            {
                Vel.x = Mathf.MoveTowards(Vel.x, 0f, 14f * dt);
                if (attackWind > 0f) UpdateSpitterWindup(dt, player);
                else
                {
                    float before = hopTimer;
                    hopTimer -= dt * spd * (frenzied ? 1.4f : 1f);
                    // decide early whether the next hop is a leap, so the crouch can telegraph it
                    if (before >= LeapCrouch && hopTimer < LeapCrouch) leapPlanned = PlanLeap(player, out _, out _);
                    float crouch = leapPlanned ? LeapCrouch : HopCrouch;
                    if (hopTimer < crouch && hopTimer > 0f) squashVel -= (leapPlanned ? 70f : 55f) * dt; // crouch anticipation
                    if (hopTimer <= 0f && spawnT >= 1f) Hop(player, toPlayer, spd);
                    UpdateBlobAttacks(dt, player, toPlayer);
                }
            }
            else
            {
                Vel.y -= BlobGravity * dt;
            }

            float prevY = Pos.y;
            Pos += Vel * dt;
            if (!Alive) return;
            // mini-boss slam: the landing spot glows while it is in the air
            if (slamPending && !grounded) TellLanding(dt);
            LandCheck(prevY);
        }

        /// <summary>Red mark where the current leap comes down; it flares shortly before touchdown.</summary>
        void TellLanding(float dt, float from = 0.6f)
        {
            leapT += dt;
            float k = from + (1f - from) * Mathf.Clamp01(leapT / Mathf.Max(0.1f, leapAirTime * 0.85f));
            BossTells.I?.Spot(TellKey(0), tellPos, Radius * 0.95f, k);
        }

        /// <summary>Where a jump with this launch velocity lands (same gravity and one-way floors as LandCheck).</summary>
        Vector2 PredictLanding(Vector2 from, Vector2 vel)
        {
            const float step = 1f / 30f;
            float half = 0.2f * sizeMul;
            Vector2 p = from, v = vel;
            for (int i = 0; i < 150; i++)
            {
                float prevY = p.y;
                v.y -= BlobGravity * step;
                p += v * step;
                if (v.y > 0f) continue;
                float floor = Level.FloorBelow(p.x, prevY + 0.02f, half);
                if (p.y <= floor) return new Vector2(p.x, floor);
            }
            return new Vector2(p.x, Level.FloorBelow(p.x, p.y + 0.02f, half));
        }

        /// <summary>How far a charge at this speed runs before it stops or meets the arena wall.</summary>
        float ChargeReach(float dir, float speed, float time)
        {
            float wall = dir > 0f ? Player.ArenaHalf - 0.5f - Pos.x : Pos.x + Player.ArenaHalf - 0.5f;
            return Mathf.Clamp(speed * time + 1f, 1f, Mathf.Max(1f, wall));
        }

        void LandCheck(float prevY)
        {
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
                    standVersion = Level.Version;
                    OnLanded(floor, impact);
                }
            }
        }

        /// <summary>Per-type attacks that start while standing still.</summary>
        void UpdateBlobAttacks(float dt, Player player, Vector2 toPlayer)
        {
            float rate = Has(EliteAffix.Frenzied) ? 1.6f : 1f;
            switch (Type)
            {
                case EnemyType.Spitter:
                    attackTimer -= dt * rate;
                    if (attackTimer <= 0f && spawnT >= 1f && Mathf.Abs(toPlayer.x) < 11f) { attackWind = 0.0001f; attackTimer = Random.Range(2.4f, 3.2f); }
                    break;
                case EnemyType.Brute:
                    attackTimer -= dt * rate;
                    if (attackTimer <= 0f && spawnT >= 1f && Mathf.Abs(toPlayer.x) < 9f && Mathf.Abs(toPlayer.y) < 2f)
                    { attackWind = 0.0001f; attackTimer = Random.Range(3.5f, 4.5f); }
                    break;
                case EnemyType.Bomber:
                    if (toPlayer.magnitude < 1.5f && spawnT >= 1f) primeTime = 0.6f;
                    break;
            }
            if (Rank == Rank.MiniBoss)
            {
                // mini-boss special: a high leap that ends in a two-way shockwave
                moveCd -= dt * rate;
                if (moveCd <= 0f && spawnT >= 1f) { moveCd = Random.Range(5f, 6.5f); LeapAt(focus.x, 1.4f); slamPending = true; }
            }
        }

        bool slamPending;

        void OnLanded(float floor, float impact)
        {
            if (slamPending || (Rank == Rank.Boss && move == BossMove.LeapSlam && moveActive && burst == 1))
            {
                slamPending = false;
                float dmg = ContactDamage * 0.8f;
                var ep = EnemyProjectiles.I;
                ep.Shockwave(new Vector2(Pos.x, floor), 1f, dmg, theme.Accent);
                ep.Shockwave(new Vector2(Pos.x, floor), -1f, dmg, theme.Accent);
                var fx = FxSystem.I;
                fx.Dust(new Vector2(Pos.x, floor), Vector2.right, 8, 3.2f, 0.6f, 0.4f);
                fx.Dust(new Vector2(Pos.x, floor), Vector2.left, 8, 3.2f, 0.6f, 0.4f);
                fx.Ring(FxLayer.Front, new Vector2(Pos.x, floor + 0.2f), 0.3f, 2.4f, 0.3f, 0.02f, 0.35f, Color.white, theme.Accent.WithAlpha(0f), 2.2f);
                Game.I.Cam.AddTrauma(Rank == Rank.Boss ? 0.5f : 0.32f);
                if (moveActive) moveT = 99f;
            }
        }

        void UpdateSpitterWindup(float dt, Player player)
        {
            attackWind += dt;
            squashVel -= 40f * dt;
            if (attackWind >= 0.55f)
            {
                attackWind = 0f;
                Vector2 from = Center + new Vector2(faceT * Radius * 0.6f, Radius * 0.5f);
                EnemyProjectiles.I.Lob(from, focus + new Vector2(Random.Range(-0.4f, 0.4f), 0.2f), ContactDamage * 0.85f, look.Glow);
                squashVel += 16f;
                if (Rank != Rank.Normal)
                {
                    EnemyProjectiles.I.Lob(from, focus + new Vector2(-1.6f, 0.2f), ContactDamage * 0.85f, look.Glow);
                    EnemyProjectiles.I.Lob(from, focus + new Vector2(1.6f, 0.2f), ContactDamage * 0.85f, look.Glow);
                }
            }
        }

        void UpdateBruteCharge(float dt, Player player, Vector2 toPlayer, float spd)
        {
            if (attackWind > 0f)
            {
                attackWind += dt;
                Vel.x = Mathf.MoveTowards(Vel.x, -faceT * 1.2f, 10f * dt);   // rear back
                squashVel -= 30f * dt;
                if (Random.value < dt * 20f) FxSystem.I.Dust(Pos, new Vector2(-faceT, 0.2f), 1, 1.4f, 0.35f, 0.28f);
                if (Rank >= Rank.MiniBoss)
                {
                    float dir = Mathf.Sign(toPlayer.x);
                    BossTells.I?.Lane(TellKey(1), Pos + new Vector2(dir * Radius * 0.5f, 0.05f), new Vector2(dir, 0f), ChargeReach(dir, 11f * spd, 0.65f), attackWind / 0.7f);
                }
                if (attackWind >= 0.7f) { attackWind = 0f; chargeTime = 0.65f; Vel = new Vector2(Mathf.Sign(toPlayer.x) * 11f * spd, 1.2f); squashVel += 14f; }
            }
            else
            {
                chargeTime -= dt;
                if (Random.value < dt * 30f) FxSystem.I.Dust(Pos, new Vector2(-Mathf.Sign(Vel.x), 0.2f), 1, 1.8f, 0.4f, 0.3f);
                if (!grounded) Vel.y -= BlobGravity * dt;
                if (chargeTime <= 0f) Vel.x *= 0.3f;
            }
        }

        void Hop(Player player, Vector2 toPlayer, float spd)
        {
            hopTimer = Random.Range(0.45f, 0.85f) * (Type == EnemyType.Spawnling || Type == EnemyType.Bomber ? 0.65f : 1f);
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
                if (focus.x > p.X0 - 0.6f && focus.x < p.X1 + 0.6f) dir = Pos.x - p.X0 < p.X1 - Pos.x ? -1f : 1f;
            }
            else if (Type == EnemyType.Spitter && Mathf.Abs(toPlayer.x) < 5f) dir = -dir;   // keep lobbing distance
            float hx = Random.Range(2.2f, 3.4f), hy = Random.Range(5.8f, 7.6f);
            if (Type == EnemyType.Brute) { hx = 1.8f; hy = 4.2f; }
            Vel = new Vector2(dir * hx * spd, hy);
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
                float lx = Level.ClampOnto(p, Mathf.Lerp(Pos.x, focus.x, 0.35f), 0.45f);
                float reach = Mathf.Abs(lx - Pos.x);
                if (reach > MaxLeapReach) continue;
                float score = reach + Mathf.Abs(Level.ClampOnto(p, focus.x, 0f) - focus.x) * 0.7f - (i == playerPlatform ? 2.5f : 0f);
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
                float d = Mathf.Abs(cx - Pos.x) + Mathf.Abs(p.Center - focus.x) * 0.5f;
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

        /// <summary>High arcing leap onto an x on the current floor (mini-boss and boss slams).</summary>
        void LeapAt(float x, float airTime)
        {
            Vel = LeapVelocity(x, airTime);
            tellPos = PredictLanding(Pos, Vel);
            leapT = 0f;
            leapAirTime = airTime;
            grounded = false;
            standing = Level.None;
            squashVel += 20f;
            FxSystem.I.Dust(Pos, Vector2.up, 6, 2f, 0.45f, 0.32f);
        }

        Vector2 LeapVelocity(float x, float airTime)
        {
            x = Mathf.Clamp(x, -Player.ArenaHalf + 1f, Player.ArenaHalf - 1f);
            return new Vector2((x - Pos.x) / airTime, BlobGravity * airTime * 0.5f);
        }

        // ------------------------------------------------------------------ wisp body

        void UpdateWisp(float dt, Player player, Vector2 toPlayer, float spd)
        {
            float side = Pos.x > focus.x ? 1f : -1f;
            bool lantern = Type == EnemyType.Lantern;
            float dist = lantern ? 4.6f : 2.6f, height = lantern ? 3.2f : 2.4f;
            Vector2 hover = focus + new Vector2(side * dist, height + Mathf.Sin(t * 1.3f) * 0.45f);
            float rate = Has(EliteAffix.Frenzied) ? 1.6f : 1f;

            if (teleportT >= 0f)
            {
                // shade blink: fade out, reappear behind the player, then dive
                teleportT += dt;
                fade = teleportT < 0.3f ? 1f - teleportT / 0.3f : Mathf.Clamp01((teleportT - 0.45f) / 0.25f);
                Vel *= Mathf.Exp(-8f * dt);
                if (teleportT >= 0.3f && teleportT - dt < 0.3f)
                {
                    FxSystem.I.Burst(Center, look.Top, look.Glow, 0.5f);
                    Pos = focus + new Vector2(-player.Facing * 2.4f, 2.2f);
                    ResetChains();
                    FxSystem.I.Ring(FxLayer.Front, Pos, 0.1f, 1.2f, 0.12f, 0.01f, 0.25f, look.Glow, look.Glow.WithAlpha(0f), 2.2f);
                }
                if (teleportT >= 0.7f) { teleportT = -1f; fade = 1f; windup = 0.0001f; }
            }
            else if (windup > 0f)
            {
                windup += dt;
                Vel = MathUtil.Damp(Vel, -toPlayer.normalized * 0.8f, 6f, dt);
                if (windup > 0.5f) { windup = 0f; diveTime = 0.55f; Vel = toPlayer.normalized * 10.5f * spd; squashVel += 10f; }
            }
            else if (diveTime > 0f)
            {
                diveTime -= dt;
                Vel = MathUtil.Damp(Vel, Vel.normalized * 10.5f * spd, 2f, dt);
                if (Random.value < dt * 30f)
                    FxSystem.I.Spawn(FxLayer.Back, true, Art.CellGlow, Pos, -Vel * 0.05f, 0.35f, 0.3f, 0f, look.Glow, look.Glow.WithAlpha(0f), 2f);
            }
            else if (attackWind > 0f)
            {
                // lantern: glow up, then a burst of bolts
                attackWind += dt;
                Vel = MathUtil.Damp(Vel, Vector2.zero, 5f, dt);
                if (attackWind >= 0.5f)
                {
                    burstTimer -= dt;
                    if (burstTimer <= 0f && burst > 0)
                    {
                        burst--;
                        burstTimer = 0.14f;
                        Vector2 dir = (focus + new Vector2(0f, 0.8f) - Center).normalized;
                        EnemyProjectiles.I.Bolt(Center + dir * Radius, dir, 9f * Mathf.Min(spd, 1.3f), ContactDamage * 0.8f, look.Glow);
                        squashVel += 6f;
                    }
                    if (burst <= 0) attackWind = 0f;
                }
            }
            else
            {
                Vector2 desired = Vector2.ClampMagnitude((hover - Pos) * 1.5f, 4.2f * spd);
                Vel = MathUtil.Damp(Vel, desired, 2.6f, dt);
                diveTimer -= dt * rate;
                if (diveTimer <= 0f && spawnT >= 1f)
                {
                    diveTimer = Random.Range(3f, 5f);
                    if (Type == EnemyType.Shade) teleportT = 0f;
                    else if (lantern) { attackWind = 0.0001f; burst = Rank != Rank.Normal ? 5 : 3; burstTimer = 0f; diveTimer = Random.Range(2.4f, 3.2f); }
                    else windup = 0.0001f;
                }
                if (Rank == Rank.MiniBoss)
                {
                    moveCd -= dt * rate;
                    if (moveCd <= 0f && spawnT >= 1f) { moveCd = Random.Range(4.5f, 6f); RadialBurst(10, ContactDamage * 0.7f); }
                }
            }
            Pos += Vel * dt;
            if (Pos.y < 0.5f) { Pos.y = 0.5f; Vel.y = Mathf.Abs(Vel.y) * 0.5f; }
            if (Pos.y > 9.2f) { Pos.y = 9.2f; Vel.y = -Mathf.Abs(Vel.y) * 0.5f; }
        }

        void RadialBurst(int count, float damage)
        {
            float off = Random.value * 360f;
            for (int i = 0; i < count; i++)
            {
                Vector2 dir = MathUtil.Dir(off + i * 360f / count);
                EnemyProjectiles.I.Bolt(Center + dir * Radius, dir, 7f, damage, look.Glow);
            }
            FxSystem.I.Ring(FxLayer.Front, Center, 0.2f, Radius * 3f, 0.2f, 0.01f, 0.3f, Color.white, look.Glow.WithAlpha(0f), 2.4f);
            squashVel += 14f;
        }

        // ------------------------------------------------------------------ boss brain

        void UpdateBoss(float dt, Player player, Vector2 toPlayer, float spd)
        {
            int phase = BossPhase;
            if (phase != lastPhase)
            {
                // phase change: roar, shake, reinforcements
                lastPhase = phase;
                var fx = FxSystem.I;
                fx.Ring(FxLayer.Front, Center, 0.4f, 5f, 0.5f, 0.03f, 0.6f, Color.white, theme.Accent.WithAlpha(0f), 2.6f);
                Game.I.Cam.AddTrauma(0.5f);
                Game.I.Post.Impact(0.6f);
                Game.I.Hud.ShowToast(DisplayName + " WIRD WÜTEND");
                int adds = Difficulty.BossSummons(phase);
                for (int i = 0; i < adds; i++) Game.I.Waves.SpawnMinion(Boss.Minion, Center + new Vector2((i - (adds - 1) * 0.5f) * 1.5f, 0.5f), this);
                moveCd = Mathf.Min(moveCd, 0.8f);
            }
            float tempo = 1f + 0.35f * phase;

            if (!moveActive)
            {
                // idle locomotion between moves
                if (K == Kind.Blob)
                {
                    if (grounded)
                    {
                        Vel.x = Mathf.MoveTowards(Vel.x, 0f, 10f * dt);
                        hopTimer -= dt * tempo;
                        if (hopTimer <= 0f && spawnT >= 1f)
                        {
                            // stalk from a distance: close in when far, back off when crowding the player
                            hopTimer = Random.Range(1f, 1.4f);
                            float adx = Mathf.Abs(toPlayer.x);
                            float dir = adx > 5f ? Mathf.Sign(toPlayer.x) : adx < 3f ? -Mathf.Sign(toPlayer.x) : 0f;
                            Vel = new Vector2(dir * 2.6f * spd, 5.5f);
                            grounded = false; standing = Level.None; squashVel += 12f;
                        }
                    }
                    else Vel.y -= BlobGravity * dt;
                }
                else
                {
                    float side = Pos.x > focus.x ? 1f : -1f;
                    Vector2 hover = new Vector2(focus.x + side * 4.5f, 4.2f + Mathf.Sin(t * 0.9f) * 0.6f);
                    Vel = MathUtil.Damp(Vel, Vector2.ClampMagnitude((hover - Pos) * 1.2f, 4f), 2f, dt);
                }
                moveCd -= dt * tempo;
                if (moveCd <= 0f && spawnT >= 1f) BeginMove();
            }
            else RunMove(dt, player, toPlayer, phase, spd);

            float prevY = Pos.y;
            Pos += Vel * dt;
            if (K == Kind.Blob) LandCheck(prevY);
            else
            {
                if (Pos.y < 1.2f) { Pos.y = 1.2f; Vel.y = Mathf.Abs(Vel.y) * 0.5f; }
                if (Pos.y > 8.5f) { Pos.y = 8.5f; Vel.y = -Mathf.Abs(Vel.y) * 0.5f; }
            }
        }

        void BeginMove()
        {
            var moves = Boss.Moves;
            move = moves[moveIndex % moves.Length];
            moveIndex += Random.value < 0.25f ? 2 : 1;
            // blob-only / wisp-only moves fall back to something the body can do
            if (K == Kind.Wisp && (move == BossMove.LeapSlam || move == BossMove.Charge)) move = BossMove.RadialBurst;
            if (K == Kind.Blob && move == BossMove.Teleport) move = BossMove.LeapSlam;
            moveActive = true;
            moveT = 0f;
            burst = 0;
            if (move == BossMove.Teleport)
                tellPos = new Vector2(Mathf.Clamp(focus.x + (Random.value < 0.5f ? -4f : 4f), -13f, 13f), 4f);
        }

        void EndMove(float cooldown)
        {
            moveActive = false;
            moveCd = cooldown;
        }

        void RunMove(float dt, Player player, Vector2 toPlayer, int phase, float spd)
        {
            moveT += dt;
            float tell = 0.7f - 0.1f * phase;
            bool telegraph = moveT < tell;
            if (telegraph) { squashVel -= 25f * dt; if (K == Kind.Blob && grounded) Vel.x = Mathf.MoveTowards(Vel.x, 0f, 12f * dt); }
            if (K == Kind.Blob && !grounded) Vel.y -= BlobGravity * dt;
            if (K == Kind.Wisp) Vel = MathUtil.Damp(Vel, Vector2.zero, 3f, dt);
            var ep = EnemyProjectiles.I;
            var tells = BossTells.I;
            float wind = moveT / tell;

            switch (move)
            {
                case BossMove.LeapSlam:
                {
                    float air = 1.15f - 0.1f * phase;
                    if (!telegraph && burst == 0 && grounded) { burst = 1; LeapAt(focus.x, air); }
                    // the mark follows the player while the boss crouches, then stays where it will land
                    if (burst == 0 && grounded) tells?.Spot(TellKey(0), PredictLanding(Pos, LeapVelocity(focus.x, air)), Radius * 0.95f, wind * 0.6f);
                    else if (burst == 1 && !grounded) TellLanding(dt);
                    if (moveT > 99f || (burst == 1 && moveT > 3.5f)) EndMove(2.2f);
                    break;
                }
                case BossMove.Charge:
                    if (burst == 0 || moveT < tell + 0.12f)
                    {
                        float dir = burst == 0 ? Mathf.Sign(toPlayer.x) : Mathf.Sign(Vel.x);
                        tells?.Lane(TellKey(1), new Vector2(Pos.x + dir * Radius * 0.5f, Pos.y + 0.05f), new Vector2(dir, 0f), ChargeReach(dir, 13f * spd, 0.9f), wind);
                    }
                    if (!telegraph && burst == 0) { burst = 1; chargeTime = 0.9f; Vel = new Vector2(Mathf.Sign(toPlayer.x) * 13f * spd, 1.5f); squashVel += 16f; }
                    if (burst == 1)
                    {
                        chargeTime -= dt;
                        if (Random.value < dt * 40f) FxSystem.I.Dust(Pos, new Vector2(-Mathf.Sign(Vel.x), 0.2f), 2, 2.4f, 0.5f, 0.35f);
                        if (chargeTime <= 0f || Mathf.Abs(Pos.x) > Player.ArenaHalf - 0.5f) { Vel.x *= 0.2f; EndMove(2f); }
                    }
                    break;
                case BossMove.Volley:
                    if (telegraph)
                    {
                        // lobs rain down around the player, bolts fly straight at them
                        int count = 5 + 2 * phase;
                        if (K == Kind.Blob) tells?.Spot(TellKey(2), new Vector2(focus.x, Level.FloorBelow(focus.x, focus.y + 0.3f)), (count - 1) * 0.45f + 0.5f, wind);
                        else
                        {
                            Vector2 from = Center + new Vector2(0f, Radius * 0.6f);
                            Vector2 to = focus + new Vector2(0f, 0.8f) - from;
                            tells?.Lane(TellKey(2), from + to.normalized * Radius, to, to.magnitude - Radius, wind);
                        }
                    }
                    if (!telegraph)
                    {
                        burstTimer -= dt;
                        int total = 5 + 2 * phase;
                        if (burstTimer <= 0f && burst < total)
                        {
                            burstTimer = K == Kind.Blob ? 0.12f : 0.1f;
                            float spread = (burst - (total - 1) * 0.5f) * 0.9f;
                            Vector2 from = Center + new Vector2(0f, Radius * 0.6f);
                            if (K == Kind.Blob) ep.Lob(from, focus + new Vector2(spread, 0.2f), ContactDamage * 0.55f, look.Glow);
                            else
                            {
                                Vector2 dir = MathUtil.Rotate((focus + new Vector2(0f, 0.8f) - from).normalized, spread * 7f);
                                ep.Bolt(from, dir, 9.5f, ContactDamage * 0.5f, look.Glow);
                            }
                            burst++;
                            squashVel += 5f;
                        }
                        if (burst >= total) EndMove(1.8f);
                    }
                    break;
                case BossMove.RadialBurst:
                    if (telegraph) tells?.Ring(TellKey(3), Center, Radius * 1.5f, wind);
                    else if (burst == 1 && phase > 0) tells?.Ring(TellKey(3), Center, Radius * 1.5f, (moveT - tell) / 0.5f);
                    if (!telegraph && burst == 0) { burst = 1; RadialBurst(12 + 4 * phase, ContactDamage * 0.5f); }
                    if (burst == 1 && moveT > tell + 0.5f && phase > 0) { burst = 2; RadialBurst(12 + 4 * phase, ContactDamage * 0.5f); }
                    if (moveT > tell + 1f) EndMove(2f);
                    break;
                case BossMove.Summon:
                    if (!telegraph && burst == 0)
                    {
                        burst = 1;
                        int n = Difficulty.BossSummons(phase);
                        for (int i = 0; i < n; i++) Game.I.Waves.SpawnMinion(Boss.Minion, Center + new Vector2((i - (n - 1) * 0.5f) * 1.4f, 0.6f), this);
                        FxSystem.I.Ring(FxLayer.Front, Center, 0.3f, 3f, 0.3f, 0.02f, 0.45f, theme.Accent, theme.Accent.WithAlpha(0f), 2.4f);
                    }
                    if (moveT > tell + 0.8f) EndMove(3f);
                    break;
                case BossMove.Teleport:
                    if (burst == 0) tells?.Ring(TellKey(4), tellPos, Radius * 1.1f, moveT / 0.3f);
                    if (burst == 0 && moveT > 0.3f)
                    {
                        burst = 1;
                        FxSystem.I.Burst(Center, look.Top, look.Glow, 1.2f);
                        Pos = tellPos;
                        ResetChains();
                        FxSystem.I.Ring(FxLayer.Front, Pos, 0.2f, 2.4f, 0.2f, 0.01f, 0.35f, look.Glow, look.Glow.WithAlpha(0f), 2.4f);
                    }
                    fade = moveT < 0.3f ? 1f - moveT / 0.3f : Mathf.Clamp01((moveT - 0.3f) / 0.25f);
                    if (moveT > 0.8f) { fade = 1f; move = BossMove.Volley; moveT = tell; burst = 0; }
                    break;
                case BossMove.LightningCall:
                case BossMove.GeyserCall:
                    // the hazards mark their own spots; the boss only glows while it calls them
                    if (telegraph) tells?.Ring(TellKey(3), Center, Radius * 1.3f, wind);
                    if (!telegraph && burst == 0)
                    {
                        burst = 1;
                        int n = 3 + phase;
                        for (int i = 0; i < n; i++)
                        {
                            float x = focus.x + (i - (n - 1) * 0.5f) * 2.2f + Random.Range(-0.4f, 0.4f);
                            if (move == BossMove.LightningCall) StageMechanics.I.Strike(x, ContactDamage * 0.9f, 0.9f + i * 0.12f);
                            else StageMechanics.I.Geyser(x, ContactDamage * 0.9f, 0.9f + i * 0.12f);
                        }
                    }
                    if (moveT > tell + 1.4f) EndMove(2.4f);
                    break;
            }
        }

        // ------------------------------------------------------------------ visuals

        void UpdateVisuals(float dt, Vector2 toPlayer)
        {
            MathUtil.Spring(ref squash, ref squashVel, 0f, 3.2f, 0.32f, dt);
            float sq = Mathf.Clamp(squash * 0.05f, -0.35f, 0.35f);
            float flipScale = Mathf.Sin(faceT * Mathf.PI * 0.5f);
            float side = flipScale >= 0f ? 1f : -1f;
            Acting(out float charge, out float lunge, out float primed);
            float shiver = primed > 0f ? 1f + 0.05f * Mathf.Sin(t * 70f) : 1f;
            body.localScale = new Vector3(flipScale * (1f - sq) * scaleNow * shiver, (1f + sq) * scaleNow * shiver, 1f);
            // flyers lean into their flight (nose up when rising, down when diving)
            float lean = look.Tilt > 0f ? Mathf.Clamp(Mathf.Atan2(Vel.y, Mathf.Abs(Vel.x) + 0.6f) * Mathf.Rad2Deg, -55f, 55f) * look.Tilt * side : 0f;
            tilt = MathUtil.Damp(tilt, lean, 7f, dt);
            body.localRotation = Quaternion.Euler(0f, 0f, tilt);
            root.position = new Vector3(Pos.x, Pos.y, 0f);

            // what the body is doing: winding up, lunging, airborne, fuse lit
            float speed01 = Mathf.Clamp01(Vel.magnitude / 8f);
            float faceVx = Vel.x * side;
            bool air = K == Kind.Blob && !grounded;
            foreach (var p in parts) AnimatePart(p, charge, lunge, speed01, faceVx, air, primed);

            // eyes: look at the player, blink now and then
            blinkTimer -= dt;
            if (blinkTimer <= 0f) { blinkTimer = Random.Range(1.8f, 4.5f); blink = 1f; }
            blink = Mathf.Max(0f, blink - dt / 0.12f);
            float eyeY = 1f - MathUtil.Bump(1f - blink) * 0.9f;
            Vector2 dir = toPlayer.normalized;
            foreach (var e in eyes)
            {
                Vector2 st = e.D.Stretch * e.D.Size;
                e.T.localScale = new Vector3(st.x, st.y * Mathf.Max(0.1f, eyeY), 1f);
                if (e.Pupil != null && e.Pupil.enabled)
                    e.Pupil.transform.localPosition = new Vector3(Mathf.Abs(dir.x) * 0.018f + 0.004f, dir.y * 0.022f, 0f);
                Color ec = e.Sr.color; ec.a = fade; e.Sr.color = ec;
            }

            // flash, frost tint and fade (shade blink)
            if (flash > 0f)
            {
                flash -= Time.unscaledDeltaTime;
                SetBodyMaterial(flash > 0f);
            }
            Color tint = Color.white;
            if (FreezeTime > 0f) tint = new Color(0.62f, 0.85f, 1f);
            else if (StunTime > 0f) tint = Color.Lerp(Color.white, new Color(1f, 0.93f, 0.62f), 0.45f + 0.2f * Mathf.Sin(t * 16f));
            else if (SlowTime > 0f) tint = Color.Lerp(Color.white, new Color(0.7f, 0.88f, 1f), 0.6f);
            else if (BurnTime > 0f) tint = Color.Lerp(Color.white, new Color(1f, 0.72f, 0.55f), 0.35f + 0.15f * Mathf.Sin(t * 20f));
            if (ExposeTime > 0f) tint = Color.Lerp(tint, Palette.Showboat, 0.35f + 0.12f * Mathf.Sin(t * 9f));
            bool white = flash > 0f;
            if (!white) bodySr.color = tint.WithAlpha(fade);
            foreach (var p in parts)
            {
                if (p.D.Mat == PartMat.Glow) p.Sr.color = p.D.Tint.WithAlpha(Mathf.Clamp01(0.7f * p.GlowK) * fade);
                else if (!white) p.Sr.color = (p.D.Tint * tint).WithAlpha(fade * p.D.Tint.a);
            }
            UpdateChains(dt, flipScale, side, white ? Color.white : tint, white);
            StatusLook(dt);

            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 3.4f);
            float ga = look.GlowAlpha + (look.Wisp ? 0.1f : 0.08f) * pulse + Mathf.Max(charge, primed) * 0.5f;
            glow.color = look.Glow.WithAlpha(ga * fade);

            // elite / mini-boss aura; bosses are recognisable by their own bodies (no ring around them)
            if (Named)
            {
                float ap = 0.5f + 0.5f * Mathf.Sin(t * 4f);
                aura.color = auraColor.WithAlpha((Rank == Rank.Boss ? 0.2f : 0.22f + 0.08f * ap) * fade);
                aura.transform.localScale = Vector3.one * look.AuraSize * (look.Wisp ? 0.9f : 1f) * (1f + 0.06f * ap);
                bool ring = Rank == Rank.Elite;
                auraRing.color = ring ? auraColor.WithAlpha(0.35f * fade) : Color.clear;
                auraRing.transform.localScale = Vector3.one * (look.Wisp ? 1.1f : 1.35f) * (1f + 0.08f * Mathf.Sin(t * 2.3f));
                auraRing.transform.localRotation = Quaternion.Euler(0f, 0f, t * 40f);
            }
            else { aura.color = Color.clear; auraRing.color = Color.clear; }

            // shadow on the surface below
            float floor = K == Kind.Blob && grounded ? Pos.y : Level.FloorBelow(Pos.x, Pos.y + 0.02f, 0.15f);
            float h = Mathf.Max(0f, Pos.y - floor - (K == Kind.Wisp ? 0.4f : 0f));
            float s = Mathf.Lerp(0.95f, 0.35f, Mathf.Clamp01(h / 4f)) * scaleNow;
            shadow.transform.position = new Vector3(Pos.x, floor + 0.02f, 0f);
            shadow.transform.localScale = new Vector3(s, s, 1f);
            shadow.color = new Color(0, 0, 0, Mathf.Lerp(0.42f, 0.08f, Mathf.Clamp01(h / 4f)) * fade);

            BodyEffects(dt);

            // small health bar after damage (named enemies use the HUD instead)
            hpShow = Mathf.Max(0f, hpShow - dt);
            hpDisplay = MathUtil.Damp(hpDisplay, Mathf.Clamp01(Hp / MaxHp), 10f, dt);
            float ha = Rank == Rank.Boss ? 0f : Mathf.Clamp01(hpShow * 3f) * fade;
            Vector2 hp = new Vector2(0f, look.HpY * scaleNow);
            float barW = Rank == Rank.MiniBoss ? 1.2f : Rank == Rank.Elite ? 0.9f : 0.66f;
            hpBack.transform.localPosition = hp;
            hpBack.size = new Vector2(barW + 0.04f, 0.1f);
            hpBack.color = new Color(0.02f, 0.03f, 0.08f, 0.7f * ha);
            float w = Mathf.Max(0.07f, barW * hpDisplay);
            hpFill.size = new Vector2(w, 0.07f);
            hpFill.transform.localPosition = hp + new Vector2(-(barW - w) * 0.5f, 0f);
            hpFill.color = Color.Lerp(Palette.HpA, Palette.HpB, hpDisplay).WithAlpha(ha);

            // a fresh spawn becomes visible only once its transforms are posed at the portal
            if (!root.gameObject.activeSelf) Show(true);
        }

        /// <summary>A small faceted ice crystal, pivot at its base (drawn once, shared by every monster).</summary>
        static Sprite IceShard
        {
            get
            {
                if (iceShard != null) return iceShard;
                var c = new SdfCanvas(new Rect(-0.07f, -0.03f, 0.14f, 0.3f), 256f);
                Vector2 b = new Vector2(0f, -0.01f), l = new Vector2(-0.045f, 0.085f), rr = new Vector2(0.05f, 0.075f), tip = new Vector2(0.004f, 0.245f);
                c.Fill(p => Mathf.Min(Sdf.Triangle(p, b, rr, tip), Sdf.Triangle(p, b, tip, l)) - 0.006f, new Color(0.2f, 0.42f, 0.62f));
                c.Fill(p => Sdf.Triangle(p, b, rr, tip), new Color(0.56f, 0.8f, 0.96f));
                c.Fill(p => Sdf.Triangle(p, b, tip, l), new Color(0.86f, 0.97f, 1f));
                c.Fill(p => Sdf.Capsule(p, new Vector2(-0.012f, 0.06f), new Vector2(-0.004f, 0.19f), 0.006f), new Color(1f, 1f, 1f, 0.9f));
                iceShard = c.ToSprite("IceShard", Vector2.zero);
                return iceShard;
            }
        }

        /// <summary>
        /// Slowed monsters frost over (a cold glow, ice shards growing out of the top, falling glints),
        /// frozen ones more so; burning ones glow warm and lick flames.
        /// </summary>
        void StatusLook(float dt)
        {
            bool frozen = FreezeTime > 0f;
            float want = frozen ? 1f : SlowTime > 0f ? Mathf.Lerp(0.55f, 0.85f, Mathf.Clamp01(SlowAmount * 1.5f)) : 0f;
            frostK = Mathf.MoveTowards(frostK, want, dt * (want > frostK ? 6f : 2.5f));
            Vector2 c = K == Kind.Blob ? new Vector2(0f, 0.36f) : Vector2.zero;
            float r = K == Kind.Blob ? 0.42f : 0.34f;
            frost.transform.localPosition = c;
            frost.transform.localScale = Vector3.one * r * 3.4f;
            frost.color = new Color(0.6f, 0.88f, 1f, (frozen ? 0.2f : 0.11f) * frostK * fade);
            for (int i = 0; i < ice.Length; i++)
            {
                float a = IceAngles[i];
                ice[i].transform.localPosition = c + MathUtil.Dir(90f - a) * r * 0.86f;
                ice[i].transform.localRotation = Quaternion.Euler(0f, 0f, -a);
                // the shards grow in one after another
                float grow = Mathf.Clamp01(frostK * 1.7f - i * 0.22f);
                float sc = MathUtil.EaseOutBack(grow, 1.6f) * (frozen ? 1.35f : 1f) * IceSizes[i];
                ice[i].transform.localScale = new Vector3(sc, sc, 1f);
                ice[i].color = Color.white.WithAlpha(Mathf.Clamp01(grow * 2f) * fade * 0.95f);
            }
            var fx = FxSystem.I;
            if (frostK > 0.3f && Random.value < dt * 5f * frostK)
                fx.Spawn(FxLayer.Front, true, Art.CellSparkle, Center + Random.insideUnitCircle * Radius, new Vector2(Random.Range(-0.2f, 0.2f), -0.3f),
                    0.6f, 0.1f, 0f, Color.white, UpgradeVisuals.Frost.WithAlpha(0f), 2f, 1.5f, 0.8f, Random.Range(0f, 90f), 40f);

            heatK = Mathf.MoveTowards(heatK, BurnTime > 0f ? 1f : 0f, dt * 4f);
            heat.transform.localPosition = c;
            heat.transform.localScale = Vector3.one * r * 3f;
            heat.color = UpgradeVisuals.Fire.WithAlpha(0.16f * heatK * fade * (0.8f + 0.2f * Mathf.PerlinNoise(t * 9f, Id * 0.37f)));
            if (heatK > 0.5f && Random.value < dt * 16f)
            {
                Vector2 top = Center + new Vector2(Random.Range(-0.6f, 0.6f) * Radius, Radius * Random.Range(0.3f, 0.8f));
                fx.Spawn(FxLayer.Front, true, Art.CellGlow, top, new Vector2(Random.Range(-0.2f, 0.2f), Random.Range(1.2f, 2.2f)), Random.Range(0.25f, 0.45f),
                    Radius * Random.Range(0.35f, 0.55f), 0f, new Color(1f, 0.75f, 0.3f), new Color(1f, 0.2f, 0.1f, 0f), 2.6f, 1f, -1f);
            }
        }

        /// <summary>One part's little life: breathing, swaying, flapping, swinging its fists, gaping, orbiting.</summary>
        void AnimatePart(PartRt p, float charge, float lunge, float speed01, float faceVx, bool air, float primed)
        {
            var d = p.D;
            Vector2 pos = d.Pos;
            float rot = d.Rot;
            Vector2 sc = d.Scale;
            float glowK = 1f;
            float ph = t * d.Freq + d.Phase;
            switch (d.Anim)
            {
                case PartAnim.Bob:
                    pos.y += d.Amp * Mathf.Sin(ph) + d.Charge * charge;
                    break;
                case PartAnim.Sway:
                    rot += d.Amp * Mathf.Sin(ph) + d.Charge * charge - Mathf.Clamp(faceVx * 2f, -10f, 10f) * Mathf.Min(1f, d.Amp / 8f);
                    break;
                case PartAnim.Flap:
                {
                    // faster wing beats when flying fast, folded back while winding up or diving
                    float beat = d.Amp * Mathf.Sin(t * d.Freq * (1f + 0.6f * speed01) + d.Phase);
                    rot += Mathf.Lerp(beat, d.Charge, Mathf.Max(charge, lunge));
                    break;
                }
                case PartAnim.Swing:
                    // fists trail behind the motion, rear back on the wind-up and thrust forward on the lunge
                    rot += Mathf.Clamp(faceVx * 4f, -d.Amp, d.Amp) + Mathf.Sin(ph) * d.Amp * 0.25f + d.Charge * charge - d.Charge * 1.2f * lunge;
                    break;
                case PartAnim.Pulse:
                    sc *= 1f + d.Amp * Mathf.Sin(ph) + d.Charge * charge;
                    break;
                case PartAnim.Foot:
                    if (air) { pos.y += 0.035f; rot += Mathf.Clamp(Vel.y * 2.5f, -20f, 20f); }
                    break;
                case PartAnim.Orbit:
                {
                    float s = Mathf.Sin(ph), c = Mathf.Cos(ph);
                    pos += new Vector2(c * d.Amp, s * d.Amp * 0.38f);
                    p.Sr.sortingOrder = s > 0f ? d.Order - 7 : d.Order;   // passes behind on the far half
                    rot += t * 90f;
                    break;
                }
                case PartAnim.Flicker:
                    glowK = 0.7f + 0.3f * Mathf.PerlinNoise(t * d.Freq, d.Phase * 3f) + primed * 0.8f;
                    sc *= 0.85f + 0.3f * Mathf.PerlinNoise(t * d.Freq * 0.7f, 5f) + primed * 0.5f;
                    break;
                case PartAnim.Breathe:
                {
                    float boost = Mathf.Max(charge, primed);
                    glowK = 1f - d.Amp + d.Amp * (0.5f + 0.5f * Mathf.Sin(ph)) + d.Charge * boost;
                    sc *= 1f + 0.12f * boost;
                    break;
                }
                case PartAnim.Jaw:
                    rot += d.Amp * Mathf.Abs(Mathf.Sin(ph)) + d.Charge * Mathf.Max(charge, lunge);
                    break;
            }
            p.T.localPosition = pos;
            p.T.localRotation = Quaternion.Euler(0f, 0f, rot);
            p.T.localScale = new Vector3(sc.x, sc.y, 1f);
            p.GlowK = glowK;
        }

        /// <summary>Tails, tentacles, tendrils and the wyrm's body: each segment chases the one before it.</summary>
        void UpdateChains(float dt, float flipScale, float side, Color tint, bool white)
        {
            foreach (var c in chains)
            {
                var d = c.D;
                Vector2 anchor = body.TransformPoint(d.Anchor);
                Vector2 hang = new Vector2(d.Hang.x * side, d.Hang.y) * scaleNow;
                bool glowing = d.Mat == PartMat.Glow;
                for (int k = 0; k < d.Count; k++)
                {
                    Vector2 from = k == 0 ? anchor : c.Pos[k - 1];
                    Vector2 target = from + (k == 0 ? hang * 0.5f : hang);
                    c.Pos[k] = MathUtil.Damp(c.Pos[k], target, Mathf.Max(3f, d.Stiff * (1f - 0.1f * k)), dt);
                    Vector2 delta = c.Pos[k] - from;
                    float maxD = d.Spacing * scaleNow;
                    if (delta.sqrMagnitude > maxD * maxD) c.Pos[k] = from + delta.normalized * maxD;
                    delta = c.Pos[k] - from;
                    Vector2 along = delta.sqrMagnitude > 1e-6f ? delta.normalized : Vector2.down;
                    // a travelling wave along the chain, growing towards the tip
                    float wave = Mathf.Sin(t * d.WaveFreq - k * 0.9f + d.Phase) * d.Wave * scaleNow * (k + 1f) / d.Count;
                    Vector2 draw = c.Pos[k] + new Vector2(-along.y, along.x) * wave;
                    var sr = c.Srs[k];
                    sr.transform.position = new Vector3(draw.x, draw.y, 0f);
                    float s = Mathf.Lerp(d.Scale0, d.Scale1, d.Count > 1 ? k / (d.Count - 1f) : 0f) * scaleNow;
                    if (d.Align)
                    {
                        sr.transform.rotation = Quaternion.Euler(0f, 0f, MathUtil.DownAngle(along));
                        sr.transform.localScale = new Vector3(s * flipScale, s, 1f);
                    }
                    else
                    {
                        sr.transform.rotation = Quaternion.identity;
                        sr.transform.localScale = new Vector3(s, s, 1f);
                    }
                    if (glowing) sr.color = d.Tint.WithAlpha(0.55f * fade * (1f - 0.5f * k / d.Count));
                    else if (!white) sr.color = (d.Tint * tint).WithAlpha(fade);
                }
            }
        }

        /// <summary>A few bodies give off something of their own: storm arcs, lava embers, void motes.</summary>
        void BodyEffects(float dt)
        {
            var fx = FxSystem.I;
            if (fx == null || fade < 0.5f) return;
            switch (look.Look)
            {
                case Look.StormLantern:
                    if (Random.value < dt * 3f)
                    {
                        Vector2 a = Center + Random.insideUnitCircle * 0.3f * sizeMul, b = Center + Random.insideUnitCircle * 0.45f * sizeMul;
                        Lightning.I.Bolt(a, b, look.Glow, 0.03f, 0.1f, 0.12f);
                    }
                    break;
                case Look.MagmaColossus:
                    if (Random.value < dt * 14f)
                    {
                        Vector2 vent = body.TransformPoint(Random.value < 0.5f ? new Vector2(-0.3f, 0.9f) : new Vector2(0.05f, 0.96f));
                        Color ec = Color.Lerp(look.Glow, Palette.Gold, Random.value * 0.5f);
                        fx.Spawn(FxLayer.Back, true, Art.CellDot, vent, new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(1.2f, 2.4f)),
                            Random.Range(0.8f, 1.4f), Random.Range(0.05f, 0.09f), 0.01f, ec, ec.WithAlpha(0f), 2.8f, 0.4f, -0.3f);
                    }
                    break;
                case Look.CometOracle:
                case Look.VoidLord:
                    if (Random.value < dt * 6f)
                        fx.Motes(Center + Random.insideUnitCircle * Radius, Vector2.up * 0.4f, look.Glow, 1, 0.1f);
                    break;
            }
        }

        /// <summary>Volatile elites burst when they die (the fight-ending pop you have to respect).</summary>
        public void OnDeathEffects()
        {
            if (detonated) return;
            if (Has(EliteAffix.Volatile)) Explode(2.2f, ContactDamage * 1.2f);
            else if (Type == EnemyType.Bomber) Explode(1.6f, ContactDamage);
        }
    }
}
