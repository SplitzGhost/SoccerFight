using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    public enum StatusKind : byte { Stun, Freeze, Chill, Ignite, Expose }

    /// <summary>One monster as the host describes it in a world snapshot.</summary>
    public struct MonsterNet
    {
        public int Id;
        public Vector2 Pos;
        public float Face, Charge, Lunge, Primed, Fade, Hp;
        public int Flags;

        public static MonsterNet Read(NetReader r) => new MonsterNet
        {
            Id = r.Int(), Pos = r.Pos(), Face = r.SByte() / 100f, Flags = r.Byte(),
            Charge = r.Unit(), Lunge = r.Unit(), Primed = r.Unit(), Fade = r.Unit(), Hp = r.Float(),
        };
    }

    /// <summary>
    /// Duo. On the host every monster is simulated as always, and each one goes into the world
    /// snapshot. On the partner's screen monsters are ghosts: the same bodies with the same
    /// animation, but they move where the host says (played back a moment in the past, blended
    /// between snapshots) and their health belongs to the host. A hit on a ghost shows at once and
    /// is sent to the host, which applies it for real.
    /// </summary>
    public sealed partial class Monster
    {
        /// <summary>This monster lives on the host; here it only shows what the host reports.</summary>
        public bool Ghost { get; private set; }
        /// <summary>Host: the hit being applied right now came from the partner.</summary>
        public static bool ApplyingRemote;
        /// <summary>Host: whom the AI chases (0 = the host's player, 1 = the partner).</summary>
        public int TargetSide;
        public float SizeMul => sizeMul;
        public bool Detonated => detonated;
        bool GhostKilledHere;

        const int FGrounded = 1, FBurn = 2, FSlow = 4, FFreeze = 8, FStun = 16, FExpose = 32;

        struct NetSample { public float T; public Vector2 P; }
        const int NetSize = 8;
        readonly NetSample[] netBuf = new NetSample[NetSize];
        int netHead, netCount;
        float netFace = 1f, netCharge, netLunge, netPrimed, netFade = 1f;
        int netFlags;

        // ------------------------------------------------------------------ host side

        public void WriteNet(NetWriter w)
        {
            Acting(out float charge, out float lunge, out float primed);
            w.Int(Id);
            w.Pos(Pos);
            w.SByte((sbyte)Mathf.RoundToInt(Mathf.Clamp(faceT, -1f, 1f) * 100f));
            int f = (grounded ? FGrounded : 0) | (BurnTime > 0f ? FBurn : 0) | (SlowTime > 0f ? FSlow : 0)
                  | (FreezeTime > 0f ? FFreeze : 0) | (StunTime > 0f ? FStun : 0) | (ExposeTime > 0f ? FExpose : 0);
            w.Byte((byte)f);
            w.Unit(charge); w.Unit(lunge); w.Unit(primed); w.Unit(fade);
            w.Float(Hp);
        }

        /// <summary>A hit the partner landed on this monster (damage already scaled by the partner's build).</summary>
        public void ApplyRemoteHit(float dmg, Vector2 dir, float knock, bool big, bool crit, bool boosted)
        {
            ApplyingRemote = true;
            try { Hit(dmg, dir, knock, big, crit, boosted); }
            finally { ApplyingRemote = false; }
        }

        public void ApplyRemoteStatus(StatusKind kind, float a, float b)
        {
            ApplyingRemote = true;
            try
            {
                switch (kind)
                {
                    case StatusKind.Stun: Stun(a); break;
                    case StatusKind.Freeze: Freeze(a); break;
                    case StatusKind.Chill: Chill(a, b); break;
                    case StatusKind.Ignite: Ignite(a, b); break;
                    case StatusKind.Expose: Expose(a); break;
                }
            }
            finally { ApplyingRemote = false; }
        }

        // ------------------------------------------------------------------ partner side

        /// <summary>Wear the host's monster: same id, size, affixes and health as over there.</summary>
        public void SpawnGhost(in SpawnSpec s, int id, float size, List<EliteAffix> affixes, float maxHp)
        {
            Spawn(s);
            Ghost = true;
            Id = id;
            sizeMul = size;
            Radius = (K == Kind.Blob ? 0.42f : 0.34f) * sizeMul;
            Affixes.Clear();
            Affixes.AddRange(affixes);
            MaxHp = Hp = maxHp;
            netHead = netCount = 0;
            netFace = faceT;
            netCharge = netLunge = netPrimed = 0f;
            netFade = 1f;
            netFlags = 0;
            GhostKilledHere = false;
            ApplyLook();   // the aura follows the affixes
        }

        public void ApplyNet(in MonsterNet s, float hostTime)
        {
            if (netCount > 0 && hostTime <= netBuf[(netHead - 1 + NetSize) % NetSize].T) return;
            netBuf[netHead] = new NetSample { T = hostTime, P = s.Pos };
            netHead = (netHead + 1) % NetSize;
            if (netCount < NetSize) netCount++;
            netFace = s.Face;
            netFlags = s.Flags;
            netCharge = s.Charge; netLunge = s.Lunge; netPrimed = s.Primed; netFade = s.Fade;
            // the partner's hits lower the bar too: show it when health drops
            if (s.Hp < Hp - 0.01f) hpShow = Mathf.Max(hpShow, 1.6f);
            Hp = s.Hp;
        }

        Vector2 SampleNet(float t)
        {
            var newer = netBuf[(netHead - 1 + NetSize) % NetSize];
            if (t >= newer.T || netCount == 1) return newer.P;
            for (int i = 1; i < netCount; i++)
            {
                var older = netBuf[(netHead - 1 - i + NetSize * 2) % NetSize];
                if (older.T <= t)
                {
                    float span = newer.T - older.T;
                    // a teleport (shade blink, boss teleport) snaps instead of sliding across the arena
                    if ((newer.P - older.P).sqrMagnitude > 16f) return t - older.T < span * 0.5f ? older.P : newer.P;
                    return Vector2.Lerp(older.P, newer.P, span > 1e-4f ? (t - older.T) / span : 1f);
                }
                newer = older;
            }
            return newer.P;
        }

        void UpdateGhost(float dt, Player player)
        {
            t += dt;
            spawnT = Mathf.Min(1f, spawnT + dt / (Rank == Rank.Boss ? 0.9f : 0.45f));
            scaleNow = MathUtil.EaseOutBack(spawnT, 2.2f) * sizeMul;
            if (netCount > 0)
            {
                Vector2 np = SampleNet(Coop.WorldRenderTime);
                if (dt > 1e-5f) Vel = Vector2.Lerp(Vel, (np - Pos) / dt, 0.4f);
                Pos = np;
            }
            grounded = (netFlags & FGrounded) != 0;
            BurnTime = (netFlags & FBurn) != 0 ? 1f : 0f;
            SlowTime = (netFlags & FSlow) != 0 ? 1f : 0f;
            SlowAmount = SlowTime > 0f ? 0.35f : 0f;
            FreezeTime = (netFlags & FFreeze) != 0 ? 1f : 0f;
            StunTime = (netFlags & FStun) != 0 ? 1f : 0f;
            ExposeTime = (netFlags & FExpose) != 0 ? 1f : 0f;
            fade = netFade;
            if (BurnTime > 0f && Random.value < dt * 14f)
                FxSystem.I.Sparks(Center + Random.insideUnitCircle * Radius * 0.7f, Vector2.up, 40f, 1, 1.5f, 3.5f, Palette.BlastOrange, 2.4f, 0.04f, 0.3f, -4f);

            // a boss roars into its next phase here too (its reinforcements come from the host)
            if (Rank == Rank.Boss)
            {
                int phase = BossPhase;
                if (phase > lastPhase)
                {
                    lastPhase = phase;
                    FxSystem.I.Ring(FxLayer.Front, Center, 0.4f, 5f, 0.5f, 0.03f, 0.6f, Color.white, theme.Accent.WithAlpha(0f), 2.6f);
                    Game.I.Cam.AddTrauma(0.5f);
                    Game.I.Post.Impact(0.6f);
                    Game.I.Hud.ShowToast(DisplayName + " WIRD WÜTEND");
                }
            }

            focus = Decoys.Focus(player.Pos);
            Vector2 toPlayer = focus + new Vector2(0f, 0.8f) - Center;
            faceT = Mathf.MoveTowards(faceT, netFace, dt / (Rank == Rank.Boss ? 0.25f : 0.12f));
            UpdateVisuals(dt, toPlayer);
        }

        /// <summary>A hit on a ghost: the flash, the sparks and the number now — the host does the rest.</summary>
        void GhostHit(float dmg, Vector2 dir, float knock, bool big, bool crit, bool boosted)
        {
            HitCount++;
            flash = big ? 0.1f : 0.07f;
            hpShow = 1.6f;
            squashVel += big ? 14f : 9f;
            var fx = FxSystem.I;
            Vector2 c = Center;
            fx.Flash(c, (big ? 2.2f : 1.4f) * Mathf.Sqrt(sizeMul), Color.white, 0.12f, 2.6f);
            fx.Sparks(c, dir, 110f, big ? 14 : 8, 5f, 12f, look.Glow, 2.6f, 0.05f, 0.25f, 2f);
            Game.I.Hud.DamageNumber(c + new Vector2(0f, Radius + 0.3f), dmg, big, crit, boosted);
            if (Rank != Rank.Boss) TimeFx.HitStop(big ? 0.05f : 0.03f, 0.06f);
            Coop.SendHit(this, dmg, dir, knock, big, crit, boosted);
        }

        /// <summary>The host reports this monster dead.</summary>
        public void GhostDie(bool detonatedThere, bool killedHere)
        {
            if (!Alive) return;
            GhostKilledHere = killedHere;
            // a bomber that blew itself up over there blows up here too — it can catch this player as well
            if (detonatedThere && !detonated) { Explode(1.9f * Mathf.Sqrt(sizeMul), ContactDamage * 1.4f); detonated = true; }
            Hp = 0f;
            Die(Vector2.up);
        }

        void Acting(out float charge, out float lunge, out float primed)
        {
            if (Ghost) { charge = netCharge; lunge = netLunge; primed = netPrimed; return; }
            charge = Mathf.Clamp01(Mathf.Max(windup * 2f, attackWind > 0f ? 1f : 0f, moveActive && moveT < 0.7f ? 1f : 0f));
            lunge = diveTime > 0f || chargeTime > 0f || (moveActive && moveT >= 0.7f && (move == BossMove.Charge || move == BossMove.LeapSlam)) ? 1f : 0f;
            primed = primeTime > 0f ? 1f : 0f;
        }
    }
}
