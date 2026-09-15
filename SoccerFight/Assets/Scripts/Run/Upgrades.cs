using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    public enum Rarity { Common, Rare, Epic, Legendary }

    /// <summary>Glyph drawn on the card (see UpgradeIcons).</summary>
    public enum UpIcon
    {
        Damage, Speed, AttackSpeed, Crit, CritDamage, Heart, Knockback, Area, Cooldown, BallSpeed, Regen, Heal,
        Leech, Armor, Magnet, Power, Rainbow, Jump, Dodge, Luck, Juggle, Blast, Echo, Ricochet, Chain, Fire,
        Frost, Explode, Shockwave, Shield, Adrenaline, AirKick, Dash, Boomerang, Trident, Fan, Storm, Nova,
        Time, Frenzy, Cyclone, GoldenBoot, TwinSun, Phoenix, BlackHole, Infinity, Maestro, Synergy
    }

    public sealed class UpgradeDef
    {
        public string Id, Name, Tag;
        public Rarity Rarity;
        public UpIcon Icon;
        public int Max = 1;
        public string[] Requires;              // other upgrade ids (synergies)
        public Ability NeedsAbility = Ability.None;
        public Func<int, string> Describe;      // level the card would give
        public Action<PlayerStats, int> Apply;  // stacks owned
        public Action<Player> OnPick;
        public bool IsSynergy => Requires != null && Requires.Length > 0;
    }

    public static class Rarities
    {
        public static readonly Color Common = new Color(0.72f, 0.8f, 0.86f);
        public static readonly Color Rare = new Color(0.33f, 0.72f, 1f);
        public static readonly Color Epic = new Color(0.78f, 0.45f, 1f);
        public static readonly Color Legendary = new Color(1f, 0.74f, 0.25f);

        public static Color Of(Rarity r) => r == Rarity.Legendary ? Legendary : r == Rarity.Epic ? Epic : r == Rarity.Rare ? Rare : Common;

        public static string Name(Rarity r) => r == Rarity.Legendary ? "LEGENDÄR" : r == Rarity.Epic ? "EPISCH" : r == Rarity.Rare ? "SELTEN" : "GEWÖHNLICH";
    }

    /// <summary>
    /// The upgrade pool. Commons are stackable stat boosts, rares add mechanics, epics define builds,
    /// legendaries change how the game plays. Synergy cards only appear once their parts are owned.
    /// </summary>
    public static class UpgradeDb
    {
        public static readonly List<UpgradeDef> All = new List<UpgradeDef>();
        static readonly Dictionary<string, UpgradeDef> byId = new Dictionary<string, UpgradeDef>();

        public static UpgradeDef Get(string id) => byId.TryGetValue(id, out var u) ? u : null;

        static string V(string s) => "<color=#FFD98A>" + s + "</color>";
        static string P(float f) => V(Mathf.RoundToInt(f * 100f) + "%");
        static string N(float f) => V(f % 1f == 0f ? ((int)f).ToString() : f.ToString("0.#"));

        static UpgradeDef U(string id, string name, Rarity r, UpIcon icon, int max, Func<int, string> desc,
            Action<PlayerStats, int> apply, string tag = null, string[] req = null, Ability ab = Ability.None, Action<Player> onPick = null)
        {
            var u = new UpgradeDef { Id = id, Name = name, Rarity = r, Icon = icon, Max = max, Describe = desc, Apply = apply,
                Tag = tag, Requires = req, NeedsAbility = ab, OnPick = onPick };
            All.Add(u);
            byId[id] = u;
            return u;
        }

        static UpgradeDb()
        {
            const Rarity C = Rarity.Common, R = Rarity.Rare, E = Rarity.Epic, L = Rarity.Legendary;

            // ------------------------------------------------------------------ common
            U("kick_power", "SCHUSSKRAFT", C, UpIcon.Damage, 10, n => "+" + P(0.12f) + " Schaden für alle Ballangriffe.", (s, n) => s.DamageMul += 0.12f * n);
            U("quick_feet", "SCHNELLE FÜSSE", C, UpIcon.Speed, 5, n => "+" + P(0.08f) + " Laufgeschwindigkeit.", (s, n) => s.MoveSpeedMul += 0.08f * n);
            U("short_backswing", "KURZES AUSHOLEN", C, UpIcon.AttackSpeed, 6, n => "Schuss-Abklingzeit " + V("−8%") + ".", (s, n) => s.ShotCooldownMul *= Mathf.Pow(0.92f, n));
            U("sharp_studs", "SCHARFE STOLLEN", C, UpIcon.Crit, 8, n => "+" + P(0.05f) + " Chance auf kritische Treffer.", (s, n) => s.CritChance += 0.05f * n);
            U("precision", "PRÄZISION", C, UpIcon.CritDamage, 6, n => "+" + P(0.25f) + " kritischer Schaden.", (s, n) => s.CritMul += 0.25f * n);
            U("stamina", "KONDITION", C, UpIcon.Heart, 10, n => "+" + N(15) + " maximales Leben, heilt sofort " + N(15) + ".", (s, n) => s.MaxHpBonus += 15f * n,
                onPick: p => p.Heal(15f));
            U("heft", "WUCHT", C, UpIcon.Knockback, 5, n => "+" + P(0.25f) + " Rückstoß auf Gegner.", (s, n) => s.KnockbackMul += 0.25f * n);
            U("wide_ripples", "WEITE WELLEN", C, UpIcon.Area, 6, n => "+" + P(0.1f) + " Größe aller Flächeneffekte.", (s, n) => s.AreaMul += 0.1f * n);
            U("training_plan", "TRAININGSPLAN", C, UpIcon.Cooldown, 6, n => "Abklingzeit aller Fähigkeiten " + V("−6%") + ".", (s, n) => s.CooldownMul *= Mathf.Pow(0.94f, n));
            U("hot_shot", "HEISSER SCHUSS", C, UpIcon.BallSpeed, 5, n => "+" + P(0.15f) + " Ballgeschwindigkeit und +" + P(0.04f) + " Schaden.",
                (s, n) => { s.BallSpeedMul += 0.15f * n; s.DamageMul += 0.04f * n; });
            U("second_wind", "ZWEITE LUFT", C, UpIcon.Regen, 5, n => "Regeneriert " + N(0.4f) + " Leben pro Sekunde.", (s, n) => s.RegenPerSec += 0.4f * n);
            U("victory_cheer", "SIEGESJUBEL", C, UpIcon.Heal, 5, n => "Heilt " + N(10) + " Leben nach jeder geschafften Welle.", (s, n) => s.HealOnWave += 10f * n);
            U("absorb", "ENERGIERAUB", C, UpIcon.Leech, 5, n => "Heilt " + N(1) + " Leben pro besiegtem Gegner.", (s, n) => s.LifeOnKill += 1f * n);
            U("shin_guards", "SCHIENBEINSCHONER", C, UpIcon.Armor, 6, n => V("−4%") + " erlittener Schaden.", (s, n) => s.Armor += 0.04f * n);
            U("magnet_soles", "MAGNETSOHLEN", C, UpIcon.Magnet, 4, n => "Ball kehrt " + P(0.25f) + " schneller zurück, One-Touch-Radius +" + P(0.2f) + ".",
                (s, n) => { s.ReturnSpeedMul += 0.25f * n; s.CatchRadiusMul += 0.2f * n; });
            U("power_training", "KRAFTTRAINING", C, UpIcon.Power, 5, n => "Power-Schuss +" + P(0.15f) + " Schaden.", (s, n) => s.PowerDamageMul += 0.15f * n);
            U("rainbow_bloom", "REGENBOGENPRACHT", C, UpIcon.Rainbow, 5, n => "Rainbow Flick +" + P(0.15f) + " Schaden und +" + P(0.08f) + " Radius.",
                (s, n) => { s.FlickDamageMul += 0.15f * n; s.FlickRadiusMul += 0.08f * n; }, ab: Ability.Flick);
            U("spring_legs", "SPRUNGFEDERN", C, UpIcon.Jump, 4, n => "+" + P(0.07f) + " Sprunghöhe.", (s, n) => s.JumpMul += 0.07f * n);
            U("evasion", "AUSWEICHTRAINING", C, UpIcon.Dodge, 4, n => "+" + N(0.2f) + " s Unverwundbarkeit nach einem Treffer.", (s, n) => s.InvulnBonus += 0.2f * n);
            U("clover", "GLÜCKSKLEE", C, UpIcon.Luck, 3, n => "Seltene, epische und legendäre Karten erscheinen häufiger.", (s, n) => s.Luck += n);
            U("dribbler", "BALLGEFÜHL", C, UpIcon.Juggle, 3, n => "Hochhalten heilt +" + P(0.5f) + ", Zeitfenster +" + P(0.1f) + ".",
                (s, n) => { s.JuggleHealMul += 0.5f * n; s.JuggleWindowMul += 0.1f * n; }, ab: Ability.Juggle);
            U("demolition", "SPRENGMEISTER", C, UpIcon.Blast, 5, n => "Fallrückzieher-Explosion +" + P(0.15f) + " Schaden und +" + P(0.06f) + " Radius.",
                (s, n) => { s.BlastDamageMul += 0.15f * n; s.BlastRadiusMul += 0.06f * n; }, ab: Ability.Bicycle);

            // ------------------------------------------------------------------ rare
            U("one_two", "DOPPELPASS", R, UpIcon.Echo, 3, n => "Jeder Schuss feuert " + V("+1") + " Echo-Ball (" + P(0.55f) + " Schaden).", (s, n) => s.EchoBalls += n, tag: "echo");
            U("ricochet", "QUERSCHLÄGER", R, UpIcon.Ricochet, 3, n => "Schüsse prallen zum nächsten Gegner ab (" + V("+1") + " Abpraller).", (s, n) => s.Ricochets += n, tag: "ricochet");
            U("chain_spark", "KETTENFUNKE", R, UpIcon.Chain, 3, n => "Treffer springen auf " + N(1 + n) + " nahe Gegner über (" + P(0.35f + 0.1f * (n - 1)) + " Schaden).",
                (s, n) => { s.ChainTargets = 1 + n; s.ChainFrac = Mathf.Max(s.ChainFrac, 0.35f + 0.1f * (n - 1)); }, tag: "chain");
            U("blazing_boot", "FEUERSCHUH", R, UpIcon.Fire, 3, n => "Treffer setzen Gegner in Brand: " + P(0.3f + 0.15f * (n - 1)) + " des Treffers pro Sekunde, 3 s.",
                (s, n) => s.BurnFrac = 0.3f + 0.15f * (n - 1), tag: "fire");
            U("frost_ball", "FROSTBALL", R, UpIcon.Frost, 2, n => "Treffer verlangsamen Gegner um " + P(0.35f + 0.15f * (n - 1)) + " für 2 s.",
                (s, n) => s.SlowAmount = 0.35f + 0.15f * (n - 1), tag: "frost");
            U("chain_reaction", "KETTENREAKTION", R, UpIcon.Explode, 3, n => "Besiegte Gegner explodieren: " + P(0.35f + 0.15f * (n - 1)) + " ihres max. Lebens als Flächenschaden.",
                (s, n) => s.KillExplodeFrac = 0.35f + 0.15f * (n - 1), tag: "explode");
            U("counter_stomp", "KONTER-STAMPFER", R, UpIcon.Shockwave, 2, n => "Wirst du getroffen, entlädt sich eine Schockwelle: " + N(40 * n) + " Schaden, starker Rückstoß.",
                (s, n) => s.CounterStomp = 40f * n);
            U("overcharge", "ÜBERLADUNG", R, UpIcon.Power, 2, n => "Power-Schuss holt " + P(0.3f) + " schneller aus und macht +" + P(0.2f) + " Schaden.",
                (s, n) => { s.PowerChargeMul *= Mathf.Pow(0.7f, n); s.PowerDamageMul += 0.2f * n; });
            U("captain_shield", "KAPITÄNSSCHILD", R, UpIcon.Shield, 2, n => "Ein Schild blockt einen Treffer und lädt alle " + N(16 - 4 * (n - 1)) + " s neu.",
                (s, n) => { s.ShieldCharges = 1; s.ShieldRecharge = 16f - 4f * (n - 1); });
            U("adrenaline", "ADRENALIN", R, UpIcon.Adrenaline, 2, n => "Nach jedem Sieg: +" + P(0.2f) + " Tempo und Schussrate für " + N(2.5f * n) + " s.",
                (s, n) => s.AdrenalineTime = 2.5f * n);
            U("air_acrobat", "LUFTAKROBAT", R, UpIcon.AirKick, 2, n => V("+1") + " Luft-Rückstoß pro Sprung.", (s, n) => s.AirBoosts += n, ab: Ability.AirKick);
            U("sharpshooter", "SCHARFSCHÜTZE", R, UpIcon.CritDamage, 2, n => "Kritische Treffer machen +" + P(0.6f) + " Schaden und schleudern Gegner weg.",
                (s, n) => s.SharpshooterBonus += 0.6f * n);
            U("afterburner", "NACHBRENNER", R, UpIcon.Dash, 2, n => "Übersteiger-Dash +" + P(0.4f) + " Weite und trifft durchquerte Gegner (" + P(0.8f * n) + " Schaden).",
                (s, n) => { s.DashDistanceMul += 0.4f * n; s.DashDamageFrac = 0.8f * n; }, ab: Ability.StepOver);
            U("big_heart", "GROSSES HERZ", R, UpIcon.Heart, 2, n => "+" + N(35) + " maximales Leben und volle Heilung.", (s, n) => s.MaxHpBonus += 35f * n,
                onPick: p => p.Heal(9999f));
            U("boomerang", "BUMERANG", R, UpIcon.Boomerang, 1, n => "Der zurückkehrende Ball macht vollen Schaden und fliegt durch Gegner hindurch.", (s, n) => s.Boomerang = true);
            U("double_rainbow", "DOPPELTER REGENBOGEN", R, UpIcon.Rainbow, 1, n => "Rainbow Flick schlägt kurz darauf ein zweites Mal ein (" + P(0.6f) + " Schaden).",
                (s, n) => s.DoubleRainbow = true, ab: Ability.Flick);
            U("run_up", "ANLAUF", R, UpIcon.Speed, 2, n => "Schüsse aus vollem Lauf machen +" + P(0.3f) + " Schaden.", (s, n) => s.RunUpBonus += 0.3f * n);
            U("fire_chain", "FUNKENFLUG", R, UpIcon.Synergy, 1, n => "Synergie: Kettenfunken setzen jedes Ziel in Brand.", (s, n) => s.ChainIgnites = true,
                req: new[] { "chain_spark", "blazing_boot" });
            U("echo_ricochet", "ECHO-QUERPASS", R, UpIcon.Synergy, 1, n => "Synergie: Echo-Bälle prallen ebenfalls zum nächsten Gegner ab.", (s, n) => s.EchoRicochet = true,
                req: new[] { "one_two", "ricochet" });

            // ------------------------------------------------------------------ epic
            U("trident", "DREIZACK", E, UpIcon.Trident, 1, n => "Der Power-Schuss teilt sich in " + V("3") + " durchschlagende Schüsse.", (s, n) => s.Trident = true);
            U("fan_volley", "FÄCHERSCHUSS", E, UpIcon.Fan, 1, n => "Jeder Schuss feuert " + V("+2") + " Echo-Bälle im Fächer. Echo-Schaden " + P(0.7f) + ".",
                (s, n) => { s.EchoBalls += 2; s.EchoDamageFrac = Mathf.Max(s.EchoDamageFrac, 0.7f); s.EchoSpread = 12f; }, tag: "echo");
            U("storm_chain", "GEWITTERKETTE", E, UpIcon.Storm, 1, n => "Kettenfunken springen bis zu " + V("5-mal") + " weiter, können kritisch treffen und machen " + P(0.6f) + " Schaden.",
                (s, n) => { s.ChainJumps = 5; s.ChainCanCrit = true; s.ChainFrac = Mathf.Max(s.ChainFrac, 0.6f); }, req: new[] { "chain_spark" });
            U("nova", "NOVA", E, UpIcon.Nova, 2, n => "Jeder " + N(n == 1 ? 12 : 9) + ". Treffer löst eine Nova um dich aus (" + N(60) + " Schaden).",
                (s, n) => s.NovaEvery = n == 1 ? 12 : 9);
            U("wildfire", "LAUFFEUER", E, UpIcon.Fire, 1, n => "Brand +" + P(0.5f) + ". Brennende Gegner entzünden beim Tod alle in der Nähe.",
                (s, n) => { s.BurnSpread = true; s.BurnBoost += 0.5f; s.BurnTime = 4f; }, req: new[] { "blazing_boot" }, tag: "fire");
            U("frost_core", "FROSTKERN", E, UpIcon.Frost, 1, n => "Verlangsamte Gegner erleiden +" + P(0.35f) + " Schaden, jeder 3. Treffer friert sie ein.",
                (s, n) => { s.FrostVuln = 0.35f; s.FreezeOnThird = true; }, req: new[] { "frost_ball" }, tag: "frost");
            U("cannoneer", "KANONIER", E, UpIcon.Explode, 1, n => "Schüsse explodieren beim Treffer (" + P(0.5f) + " Schaden als Fläche).", (s, n) => s.Cannoneer = true, tag: "explode");
            U("bullet_time", "ZEITLUPE", E, UpIcon.Time, 1, n => "+" + P(0.1f) + " Krit-Chance. Krits und perfekte Ballberührungen verlangsamen kurz die Zeit.",
                (s, n) => { s.BulletTime = true; s.CritChance += 0.1f; });
            U("blood_frenzy", "BLUTRAUSCH", E, UpIcon.Frenzy, 1, n => "Jeder Sieg: +" + P(0.06f) + " Schaden für 4 s, bis zu " + V("10-fach") + ".", (s, n) => s.BloodFrenzy = true);
            U("echo_flip", "ECHO-SALTO", E, UpIcon.Blast, 1, n => "Der Fallrückzieher feuert " + V("2") + " zusätzliche explodierende Bälle im Fächer.",
                (s, n) => s.EchoFlip = true, ab: Ability.Bicycle);
            U("cyclone_step", "WIRBELSTURM", E, UpIcon.Cyclone, 1, n => "Der Übersteiger hinterlässt einen Wirbel, der Gegner 2 s einsaugt und trifft.",
                (s, n) => s.CycloneStep = true, ab: Ability.StepOver);
            U("thermal_shock", "THERMOSCHOCK", E, UpIcon.Synergy, 1, n => "Synergie: Treffer auf brennende UND verlangsamte Gegner lösen eine Frostexplosion aus (" + P(1.5f) + ").",
                (s, n) => s.ThermalShock = true, req: new[] { "blazing_boot", "frost_ball" });

            // ------------------------------------------------------------------ legendary
            U("golden_boot", "GOLDENER SCHUH", L, UpIcon.GoldenBoot, 1, n => "Jeder " + V("3.") + " Schuss ist garantiert kritisch, explodiert und verkettet Funken.", (s, n) => s.GoldenBoot = true);
            U("twin_sun", "ZWILLINGSSONNE", L, UpIcon.TwinSun, 1, n => "Ein Geisterball umkreist dich und schießt alle " + N(1.2f) + " s auf den nächsten Gegner. Erbt alle Treffer-Effekte.",
                (s, n) => s.TwinSun = true);
            U("phoenix", "PHÖNIX", L, UpIcon.Phoenix, 1, n => "Einmal pro Lauf: Tödlicher Schaden belebt dich mit " + P(0.5f) + " Leben in einer Feuerexplosion wieder.",
                (s, n) => s.Revives += 1);
            U("singularity", "SINGULARITÄT", L, UpIcon.BlackHole, 1, n => "Der Power-Schuss hinterlässt ein schwarzes Loch, das Gegner " + N(2.5f) + " s einsaugt und dann implodiert.",
                (s, n) => s.Singularity = true);
            U("perpetual", "PERPETUUM MOBILE", L, UpIcon.Infinity, 1, n => "Jeder Sieg verkürzt alle Abklingzeiten um " + N(0.5f) + " s, jeder Krit um " + N(0.2f) + " s.",
                (s, n) => s.Perpetual = true);
            U("maestro", "MAESTRO", L, UpIcon.Maestro, 1, n => "Jede Fähigkeit macht dich " + N(1) + " s unverwundbar und feuert eine Echo-Salve auf nahe Gegner.",
                (s, n) => s.Maestro = true);
        }

        public static int Count(Rarity r) { int c = 0; foreach (var u in All) if (u.Rarity == r) c++; return c; }
    }

    /// <summary>
    /// Offers three cards. Rarity weights climb with the stage and with luck; boss rewards skip commons;
    /// after four offers without an epic or better, one card is guaranteed epic+ (pity). Cards whose
    /// synergy partners are owned, and upgrades the build already uses, are weighted up so builds form.
    /// </summary>
    public static class UpgradeRoller
    {
        static readonly List<UpgradeDef> candidates = new List<UpgradeDef>();

        public static void Weights(RunState run, bool boss, out float c, out float r, out float e, out float l)
        {
            int s = run.Stage - 1;
            float luck = run.Stats.Luck;
            if (boss) { c = 0f; r = 55f; e = 33f; l = 12f; }
            else
            {
                c = Mathf.Max(30f, 62f - 3f * s);
                r = Mathf.Min(38f, 27f + 1.5f * s);
                e = Mathf.Min(22f, 9f + 1.1f * s);
                l = Mathf.Min(8f, 2f + 0.35f * s);
            }
            c = Mathf.Max(0f, c - 6f * luck);
            r += 2.5f * luck; e += 2.5f * luck; l += 1f * luck;
        }

        static Rarity Roll(float c, float r, float e, float l)
        {
            float x = UnityEngine.Random.value * (c + r + e + l);
            if ((x -= l) < 0f) return Rarity.Legendary;
            if ((x -= e) < 0f) return Rarity.Epic;
            if ((x -= r) < 0f) return Rarity.Rare;
            return Rarity.Common;
        }

        public static bool Eligible(RunState run, UpgradeDef u)
        {
            if (run.Stacks(u.Id) >= u.Max) return false;
            if (u.NeedsAbility != Ability.None && !run.Has(u.NeedsAbility)) return false;
            if (u.Requires != null) foreach (var req in u.Requires) if (run.Stacks(req) == 0) return false;
            return true;
        }

        static UpgradeDef Pick(RunState run, Rarity rarity, List<UpgradeDef> exclude)
        {
            candidates.Clear();
            float total = 0f;
            foreach (var u in UpgradeDb.All)
            {
                if (u.Rarity != rarity || exclude.Contains(u) || !Eligible(run, u)) continue;
                candidates.Add(u);
                total += Weight(run, u);
            }
            if (candidates.Count == 0) return null;
            float x = UnityEngine.Random.value * total;
            foreach (var u in candidates) { x -= Weight(run, u); if (x <= 0f) return u; }
            return candidates[candidates.Count - 1];
        }

        static float Weight(RunState run, UpgradeDef u)
        {
            float w = 1f;
            if (u.IsSynergy) w = 2.5f;                       // the game points at the combo you just made
            else if (run.Stacks(u.Id) > 0) w = 1.4f;          // deepen what the build already uses
            return w;
        }

        public static List<UpgradeDef> Offer(RunState run, int count, bool boss)
        {
            Weights(run, boss, out float c, out float r, out float e, out float l);
            var offer = new List<UpgradeDef>();
            bool pity = run.OffersSinceEpic >= 4;
            for (int i = 0; i < count; i++)
            {
                Rarity rarity = pity && i == 0 ? (UnityEngine.Random.value < 0.85f ? Rarity.Epic : Rarity.Legendary) : Roll(c, r, e, l);
                UpgradeDef u = Pick(run, rarity, offer);
                // pool for that rarity exhausted: step down, then up
                for (int k = (int)rarity - 1; u == null && k >= 0; k--) u = Pick(run, (Rarity)k, offer);
                for (int k = (int)rarity + 1; u == null && k <= (int)Rarity.Legendary; k++) u = Pick(run, (Rarity)k, offer);
                if (u != null) offer.Add(u);
            }
            bool epicPlus = false;
            foreach (var u in offer) if (u.Rarity >= Rarity.Epic) epicPlus = true;
            run.OffersSinceEpic = epicPlus ? 0 : run.OffersSinceEpic + 1;
            return offer;
        }
    }
}
