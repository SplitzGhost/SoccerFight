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
        Time, Frenzy, Cyclone, GoldenBoot, TwinSun, Phoenix, BlackHole, Infinity, Maestro, Synergy,
        // basketball
        Hoop, Swish, Slam, Crossover, Palm, Distance
    }

    public sealed class UpgradeDef
    {
        public string Id, Name, Tag;
        public Rarity Rarity;
        public UpIcon Icon;
        public int Max = 1;
        public string[] Requires;              // other upgrade ids (synergies)
        public Ability NeedsAbility = Ability.None;
        /// <summary>The sport whose players get this card.</summary>
        public Sport Sport = Sport.Soccer;
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
            U("power_training", "KRAFTTRAINING", C, UpIcon.Power, 5, n => "Power-Schuss +" + P(0.15f) + " Schaden.", (s, n) => s.PowerDamageMul += 0.15f * n, ab: Ability.Power);
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
                (s, n) => { s.PowerChargeMul *= Mathf.Pow(0.7f, n); s.PowerDamageMul += 0.2f * n; }, ab: Ability.Power);
            U("captain_shield", "KAPITÄNSSCHILD", R, UpIcon.Shield, 2, n => "Ein Schild blockt einen Treffer und lädt alle " + N(16 - 4 * (n - 1)) + " s neu.",
                (s, n) => { s.ShieldCharges = 1; s.ShieldRecharge = 16f - 4f * (n - 1); });
            U("adrenaline", "ADRENALIN", R, UpIcon.Adrenaline, 2, n => "Nach jedem Sieg: +" + P(0.2f) + " Tempo und Schussrate für " + N(2.5f * n) + " s.",
                (s, n) => s.AdrenalineTime = 2.5f * n);
            U("air_acrobat", "LUFTAKROBAT", R, UpIcon.AirKick, 2, n => V("+1") + " Luft-Rückstoß pro Sprung.", (s, n) => s.AirBoosts += n);
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
            U("trident", "DREIZACK", E, UpIcon.Trident, 1, n => "Der Power-Schuss teilt sich in " + V("3") + " durchschlagende Schüsse.", (s, n) => s.Trident = true, ab: Ability.Power);
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
            U("singularity", "SINGULARITÄT", L, UpIcon.BlackHole, 1, n => "Der Power-Schuss öffnet dort, wohin du zielst, ein schwarzes Loch, das Gegner " + N(2.5f) + " s einsaugt und dann implodiert.",
                (s, n) => s.Singularity = true, ab: Ability.Power);
            U("perpetual", "PERPETUUM MOBILE", L, UpIcon.Infinity, 1, n => "Jeder Sieg verkürzt alle Abklingzeiten um " + N(0.5f) + " s, jeder Krit um " + N(0.2f) + " s.",
                (s, n) => s.Perpetual = true);
            U("maestro", "MAESTRO", L, UpIcon.Maestro, 1, n => "Jede Fähigkeit macht dich " + N(1) + " s unverwundbar und feuert eine Echo-Salve auf nahe Gegner.",
                (s, n) => s.Maestro = true);

            // ------------------------------------------------------------------ the later moves
            U("blood_tackle", "BLUTGRÄTSCHE", C, UpIcon.Leech, 4, n => "Jeder von der Grätsche umgeworfene Gegner heilt " + N(3f * n) + ".",
                (s, n) => s.TackleHeal += 3f * n, ab: Ability.Tackle);
            U("ground_wave", "BODENWELLE", R, UpIcon.Shockwave, 1, n => "Am Ende der Grätsche rollt eine Schockwelle über den Boden (" + N(26) + " Flächenschaden).",
                (s, n) => s.TackleWave = true, ab: Ability.Tackle);
            U("firebomb", "BRANDSATZ", R, UpIcon.Fire, 1, n => "Der Abstoß-Krater brennt: Getroffene Gegner stehen " + N(4) + " s in Flammen.",
                (s, n) => s.PuntFire = true, ab: Ability.Punt);
            U("hailstorm", "HAGEL", E, UpIcon.Explode, 2, n => "Der Abstoß schlägt " + V(n.ToString()) + " mal zusätzlich neben dem Ziel ein (halber Schaden).",
                (s, n) => s.PuntExtra += n, ab: Ability.Punt);
            U("solid_wall", "STABILE MAUER", C, UpIcon.Shield, 3, n => "Die Mauer steht " + N(1.2f * n) + " s länger.",
                (s, n) => s.WallLifeBonus += 1.2f * n, ab: Ability.Wall);
            U("rebound", "ABPRALLER", R, UpIcon.Ricochet, 1, n => "Dein Ball kommt von der Mauer schneller zurück, als er ankam.",
                (s, n) => s.WallBounce = true, ab: Ability.Wall);
            U("street_ball", "STRASSENFUSSBALL", C, UpIcon.Cooldown, 3, n => "Jeder Tunnel verkürzt alle Abklingzeiten um " + N(0.6f * n) + " s.",
                (s, n) => s.NutmegRefund += 0.6f * n, ab: Ability.Nutmeg);
            U("humiliation", "DEMÜTIGUNG", R, UpIcon.Chain, 1, n => "Die Tunnel-Markierung springt auf einen zweiten Gegner in der Nähe über.",
                (s, n) => s.NutmegSpread = true, ab: Ability.Nutmeg);
            U("stoppage", "NACHSPIELZEIT", R, UpIcon.Time, 2, n => "Der Schlusspfiff hält " + N(0.5f * n) + " s länger an.",
                (s, n) => s.WhistleBonus += 0.5f * n, ab: Ability.Whistle);
            U("red_card", "ROTE KARTE", E, UpIcon.Crit, 1, n => "Beim Schlusspfiff fliegt ein einfacher Gegner sofort vom Platz.",
                (s, n) => s.RedCard = true, ab: Ability.Whistle);
            U("diversion", "ABLENKUNGSMANÖVER", R, UpIcon.Nova, 1, n => "Der Lockvogel platzt mit " + N(45) + " Flächenschaden statt " + N(18) + " und größerem Radius.",
                (s, n) => s.DecoyBlast = true, ab: Ability.Decoy);
            U("doppelganger", "DOPPELGÄNGER", E, UpIcon.Echo, 2, n => "Die Körpertäuschung lässt " + V(n.ToString()) + " zusätzliche Nachbilder stehen.",
                (s, n) => s.DecoyCount += n, ab: Ability.Decoy);

            // ------------------------------------------------------------------ the header (defender)
            U("air_power", "LUFTHOHEIT", C, UpIcon.Damage, 4, n => "Kopfball +" + P(0.15f) + " Schaden.",
                (s, n) => s.CategoryDamage[(int)SkillCategory.Header] += 0.15f * n, ab: Ability.Header);
            U("headbutt", "KOPFNUSS", R, UpIcon.Time, 2, n => "Der Kopfball betäubt " + N(0.8f) + " s länger.",
                (s, n) => s.HeaderStunBonus += 0.8f * n, ab: Ability.Header);
            U("diving_header", "FLUGKOPFBALL", E, UpIcon.Trident, 1, n => "Der Kopfball fliegt durch " + V("2") + " Gegner hindurch, bevor er abprallt.",
                (s, n) => s.HeaderPierce += 2, ab: Ability.Header);

            // ------------------------------------------------------------------ the dash (skiller)
            U("sprint_spikes", "SPRINTSPIKES", C, UpIcon.Dash, 3, n => "Antritt lädt " + V("−12%") + " schneller und reicht +" + P(0.1f) + " weiter.",
                (s, n) => { s.DashCooldownMul *= Mathf.Pow(0.88f, n); s.DashDistanceMul += 0.1f * n; }, ab: Ability.Dash);
            U("breakthrough", "DURCHBRUCH", R, UpIcon.Dash, 2, n => "Der Antritt trifft jeden durchquerten Gegner (" + P(0.6f * n) + " Schaden).",
                (s, n) => s.DashDamageFrac = Mathf.Max(s.DashDamageFrac, 0.6f * n), ab: Ability.Dash);
            U("zigzag", "ZICKZACK", E, UpIcon.Speed, 1, n => V("+1") + " Antritt in der Luft, und der Antritt lädt " + V("−25%") + " schneller.",
                (s, n) => { s.AirDashes += 1; s.DashCooldownMul *= 0.75f; }, ab: Ability.Dash);

            Hoops();
        }

        /// <summary>A basketball card: only players of that sport are offered it.</summary>
        static UpgradeDef B(string id, string name, Rarity r, UpIcon icon, int max, Func<int, string> desc,
            Action<PlayerStats, int> apply, string tag = null, string[] req = null, Ability ab = Ability.None, Action<Player> onPick = null)
        {
            var u = U(id, name, r, icon, max, desc, apply, tag, req, ab, onPick);
            u.Sport = Sport.Basketball;
            return u;
        }

        /// <summary>
        /// The basketball pool: the soccer cards that make sense for a thrower (renamed for the
        /// court), plus cards for the dribble, the throw and the six basketball moves.
        /// </summary>
        static void Hoops()
        {
            const Rarity C = Rarity.Common, R = Rarity.Rare, E = Rarity.Epic, L = Rarity.Legendary;

            // ------------------------------------------------------------------ common
            B("bb_power", "WURFKRAFT", C, UpIcon.Damage, 10, n => "+" + P(0.12f) + " Schaden für alle Ballangriffe.", (s, n) => s.DamageMul += 0.12f * n);
            B("bb_quick", "SCHNELLE BEINE", C, UpIcon.Speed, 5, n => "+" + P(0.08f) + " Laufgeschwindigkeit.", (s, n) => s.MoveSpeedMul += 0.08f * n);
            B("bb_release", "SCHNELLE HAND", C, UpIcon.AttackSpeed, 6, n => "Wurf-Abklingzeit " + V("−8%") + ".", (s, n) => s.ShotCooldownMul *= Mathf.Pow(0.92f, n));
            B("bb_touch", "FINGERSPITZENGEFÜHL", C, UpIcon.Crit, 8, n => "+" + P(0.05f) + " Chance auf kritische Treffer.", (s, n) => s.CritChance += 0.05f * n);
            B("bb_precision", "PRÄZISION", C, UpIcon.CritDamage, 6, n => "+" + P(0.25f) + " kritischer Schaden.", (s, n) => s.CritMul += 0.25f * n);
            B("bb_stamina", "AUSDAUER", C, UpIcon.Heart, 10, n => "+" + N(15) + " maximales Leben, heilt sofort " + N(15) + ".", (s, n) => s.MaxHpBonus += 15f * n,
                onPick: p => p.Heal(15f));
            B("bb_heft", "SCHULTERSTOSS", C, UpIcon.Knockback, 5, n => "+" + P(0.25f) + " Rückstoß auf Gegner.", (s, n) => s.KnockbackMul += 0.25f * n);
            B("bb_area", "SPIELRAUM", C, UpIcon.Area, 6, n => "+" + P(0.1f) + " Größe aller Flächeneffekte.", (s, n) => s.AreaMul += 0.1f * n);
            B("bb_camp", "TRAININGSCAMP", C, UpIcon.Cooldown, 6, n => "Abklingzeit aller Fähigkeiten " + V("−6%") + ".", (s, n) => s.CooldownMul *= Mathf.Pow(0.94f, n));
            B("bb_pass", "HARTER PASS", C, UpIcon.BallSpeed, 5, n => "+" + P(0.15f) + " Ballgeschwindigkeit und +" + P(0.04f) + " Schaden.",
                (s, n) => { s.BallSpeedMul += 0.15f * n; s.DamageMul += 0.04f * n; });
            B("bb_breath", "DURCHATMEN", C, UpIcon.Regen, 5, n => "Regeneriert " + N(0.4f) + " Leben pro Sekunde.", (s, n) => s.RegenPerSec += 0.4f * n);
            B("bb_timeout", "AUSZEIT", C, UpIcon.Heal, 5, n => "Heilt " + N(10) + " Leben nach jeder geschafften Welle.", (s, n) => s.HealOnWave += 10f * n);
            B("bb_steal", "BALLGEWINN", C, UpIcon.Leech, 5, n => "Heilt " + N(1) + " Leben pro besiegtem Gegner.", (s, n) => s.LifeOnKill += 1f * n);
            B("bb_pads", "KNIESCHONER", C, UpIcon.Armor, 6, n => V("−4%") + " erlittener Schaden.", (s, n) => s.Armor += 0.04f * n);
            B("bb_hands", "REBOUND-HÄNDE", C, UpIcon.Magnet, 4, n => "Ball kehrt " + P(0.25f) + " schneller zurück, Fang-Radius +" + P(0.2f) + ".",
                (s, n) => { s.ReturnSpeedMul += 0.25f * n; s.CatchRadiusMul += 0.2f * n; });
            B("bb_range", "DISTANZTRAINING", C, UpIcon.Hoop, 5, n => "Dreier +" + P(0.15f) + " Schaden.", (s, n) => s.ThreeDamageMul += 0.15f * n, ab: Ability.Three);
            B("bb_hops", "SPRUNGKRAFT", C, UpIcon.Jump, 4, n => "+" + P(0.07f) + " Sprunghöhe.", (s, n) => s.JumpMul += 0.07f * n);
            B("bb_footwork", "BEINARBEIT", C, UpIcon.Dodge, 4, n => "+" + N(0.2f) + " s Unverwundbarkeit nach einem Treffer.", (s, n) => s.InvulnBonus += 0.2f * n);
            B("bb_luck", "GLÜCKSWURF", C, UpIcon.Luck, 3, n => "Seltene, epische und legendäre Karten erscheinen häufiger.", (s, n) => s.Luck += n);
            B("bb_rim", "RINGKRAFT", C, UpIcon.Slam, 4, n => "Dunk +" + P(0.15f) + " Schaden.",
                (s, n) => s.CategoryDamage[(int)SkillCategory.Header] += 0.15f * n, ab: Ability.Dunk);
            B("bb_handles", "BALLHANDLING", C, UpIcon.Crossover, 3, n => "Der Crossover-Boost hält " + N(0.6f) + " s länger.", (s, n) => s.CrossTimeBonus += 0.6f * n, ab: Ability.Crossover);
            B("bb_quickstep", "QUICKSTEP", C, UpIcon.Cooldown, 3, n => "Crossover lädt " + V("−10%") + " schneller.", (s, n) => s.CrossCooldownMul *= Mathf.Pow(0.9f, n), ab: Ability.Crossover);
            B("bb_lob", "WEICHER LOB", C, UpIcon.Hoop, 4, n => "Alley-Oop +" + P(0.15f) + " Schaden.", (s, n) => s.OopDamageMul += 0.15f * n, ab: Ability.AlleyOop);
            B("bb_loopcd", "SCHNELLER LOB", C, UpIcon.Cooldown, 3, n => "Alley-Oop lädt " + V("−12%") + " schneller.", (s, n) => s.OopCooldownMul *= Mathf.Pow(0.88f, n), ab: Ability.AlleyOop);
            B("bb_arms", "LANGE ARME", C, UpIcon.Palm, 3, n => "Der Block hält " + N(0.4f) + " s länger.", (s, n) => s.BlockTimeBonus += 0.4f * n, ab: Ability.Block);
            B("bb_ready", "SPRUNGBEREIT", C, UpIcon.Cooldown, 3, n => "Block lädt " + V("−12%") + " schneller.", (s, n) => s.BlockCooldownMul *= Mathf.Pow(0.88f, n), ab: Ability.Block);
            B("bb_pace", "TEMPOMACHER", C, UpIcon.Dash, 3, n => "Fastbreak lädt " + V("−12%") + " schneller und reicht +" + P(0.1f) + " weiter.",
                (s, n) => { s.FastBreakCooldownMul *= Mathf.Pow(0.88f, n); s.FastBreakDistMul += 0.1f * n; }, ab: Ability.FastBreak);

            // ------------------------------------------------------------------ rare
            B("bb_nolook", "NO-LOOK-PASS", R, UpIcon.Echo, 3, n => "Jeder Wurf feuert " + V("+1") + " Echo-Ball (" + P(0.55f) + " Schaden).", (s, n) => s.EchoBalls += n, tag: "echo");
            B("bb_assist", "ZUSPIEL", R, UpIcon.Ricochet, 3, n => "Würfe prallen zum nächsten Gegner ab (" + V("+1") + " Abpraller).", (s, n) => s.Ricochets += n, tag: "ricochet");
            B("bb_bank", "BRETTWURF", R, UpIcon.Ricochet, 2, n => "Trifft ein Wurf Boden oder Plattform, springt er zum nächsten Gegner weiter (" + V("+1") + " pro Wurf).",
                (s, n) => s.BankShots += n);
            B("bb_spark", "KETTENFUNKE", R, UpIcon.Chain, 3, n => "Treffer springen auf " + N(1 + n) + " nahe Gegner über (" + P(0.35f + 0.1f * (n - 1)) + " Schaden).",
                (s, n) => { s.ChainTargets = 1 + n; s.ChainFrac = Mathf.Max(s.ChainFrac, 0.35f + 0.1f * (n - 1)); }, tag: "chain");
            B("bb_fire", "FEUERHAND", R, UpIcon.Fire, 3, n => "Treffer setzen Gegner in Brand: " + P(0.3f + 0.15f * (n - 1)) + " des Treffers pro Sekunde, 3 s.",
                (s, n) => s.BurnFrac = 0.3f + 0.15f * (n - 1), tag: "fire");
            B("bb_ice", "EISWURF", R, UpIcon.Frost, 2, n => "Treffer verlangsamen Gegner um " + P(0.35f + 0.15f * (n - 1)) + " für 2 s.",
                (s, n) => s.SlowAmount = 0.35f + 0.15f * (n - 1), tag: "frost");
            B("bb_boom", "KETTENREAKTION", R, UpIcon.Explode, 3, n => "Besiegte Gegner explodieren: " + P(0.35f + 0.15f * (n - 1)) + " ihres max. Lebens als Flächenschaden.",
                (s, n) => s.KillExplodeFrac = 0.35f + 0.15f * (n - 1), tag: "explode");
            B("bb_charge", "OFFENSIVFOUL", R, UpIcon.Shockwave, 2, n => "Wirst du getroffen, entlädt sich eine Schockwelle: " + N(40 * n) + " Schaden, starker Rückstoß.",
                (s, n) => s.CounterStomp = 40f * n);
            B("bb_wrist", "HANDGELENK", R, UpIcon.Hoop, 2, n => "Der Dreier lädt " + P(0.3f) + " schneller und macht +" + P(0.2f) + " Schaden.",
                (s, n) => { s.ThreeCooldownMul *= Mathf.Pow(0.7f, n); s.ThreeDamageMul += 0.2f * n; }, ab: Ability.Three);
            B("bb_captain", "TEAMKAPITÄN", R, UpIcon.Shield, 2, n => "Ein Schild blockt einen Treffer und lädt alle " + N(16 - 4 * (n - 1)) + " s neu.",
                (s, n) => { s.ShieldCharges = 1; s.ShieldRecharge = 16f - 4f * (n - 1); });
            B("bb_crowd", "HEIMSPIEL", R, UpIcon.Adrenaline, 2, n => "Nach jedem Sieg: +" + P(0.2f) + " Tempo und Wurfrate für " + N(2.5f * n) + " s.",
                (s, n) => s.AdrenalineTime = 2.5f * n);
            B("bb_hangtime", "HANG TIME", R, UpIcon.AirKick, 2, n => V("+1") + " Bodenpass pro Sprung (Wurf in der Luft stößt dich ab).", (s, n) => s.AirBoosts += n);
            B("bb_sniper", "SCHARFSCHÜTZE", R, UpIcon.CritDamage, 2, n => "Kritische Treffer machen +" + P(0.6f) + " Schaden und schleudern Gegner weg.",
                (s, n) => s.SharpshooterBonus += 0.6f * n);
            B("bb_heart", "GROSSES HERZ", R, UpIcon.Heart, 2, n => "+" + N(35) + " maximales Leben und volle Heilung.", (s, n) => s.MaxHpBonus += 35f * n,
                onPick: p => p.Heal(9999f));
            B("bb_outlet", "OUTLET-PASS", R, UpIcon.Boomerang, 1, n => "Der zurückkehrende Ball macht vollen Schaden und fliegt durch Gegner hindurch.", (s, n) => s.Boomerang = true);
            B("bb_transition", "WURF AUS DEM LAUF", R, UpIcon.Speed, 2, n => "Würfe aus vollem Lauf machen +" + P(0.3f) + " Schaden.", (s, n) => s.RunUpBonus += 0.3f * n);
            B("bb_floater", "FLOATER", R, UpIcon.AirKick, 2, n => "Würfe aus der Luft machen +" + P(0.2f) + " Schaden.", (s, n) => s.AirThrowBonus += 0.2f * n);
            B("bb_firechain", "FUNKENFLUG", R, UpIcon.Synergy, 1, n => "Synergie: Kettenfunken setzen jedes Ziel in Brand.", (s, n) => s.ChainIgnites = true,
                req: new[] { "bb_spark", "bb_fire" });
            B("bb_relay", "PASSSTAFETTE", R, UpIcon.Synergy, 1, n => "Synergie: Echo-Bälle prallen ebenfalls zum nächsten Gegner ab.", (s, n) => s.EchoRicochet = true,
                req: new[] { "bb_nolook", "bb_assist" });
            B("bb_swish", "SWISH", R, UpIcon.Swish, 2, n => "Dreier-Explosion +" + P(0.25f) + " Radius und +" + P(0.15f) + " Schaden.",
                (s, n) => { s.ThreeRadiusMul += 0.25f * n; s.ThreeDamageMul += 0.15f * n; }, ab: Ability.Three);
            B("bb_poster", "POSTERIZE", R, UpIcon.Slam, 2, n => "Die Dunk-Druckwellen betäuben Gegner " + N(0.8f * n) + " s lang.", (s, n) => s.DunkStun += 0.8f * n, ab: Ability.Dunk);
            B("bb_leap", "WEITSPRUNG", R, UpIcon.Jump, 2, n => "Dunk-Reichweite +" + P(0.2f) + ", lädt " + V("−10%") + " schneller.",
                (s, n) => { s.DunkRangeMul += 0.2f * n; s.DunkCooldownMul *= Mathf.Pow(0.9f, n); }, ab: Ability.Dunk);
            B("bb_ankles", "ANKLE BREAKER", R, UpIcon.Crossover, 1, n => "Beim Crossover stolpern alle Gegner in der Nähe und sind " + N(1) + " s betäubt.",
                (s, n) => s.CrossStunRadius = 3.5f, ab: Ability.Crossover);
            B("bb_rejection", "ZURÜCK ZUM ABSENDER", R, UpIcon.Palm, 2, n => "Zurückgeschlagene Geschosse +" + P(0.5f) + " Schaden, Block-Reichweite +" + P(0.3f) + ".",
                (s, n) => { s.BlockReflectMul += 0.5f * n; s.BlockRadiusMul += 0.3f * n; }, ab: Ability.Block);
            B("bb_counter", "TEMPOGEGENSTOSS", R, UpIcon.Dash, 2, n => "Fastbreak reicht +" + P(0.3f) + " weiter und heilt " + N(3) + " pro getroffenem Gegner.",
                (s, n) => { s.FastBreakDistMul += 0.3f * n; s.FastBreakHeal += 3f * n; }, ab: Ability.FastBreak);

            // ------------------------------------------------------------------ epic
            B("bb_fan", "FÄCHERPASS", E, UpIcon.Fan, 1, n => "Jeder Wurf feuert " + V("+2") + " Echo-Bälle im Fächer. Echo-Schaden " + P(0.7f) + ".",
                (s, n) => { s.EchoBalls += 2; s.EchoDamageFrac = Mathf.Max(s.EchoDamageFrac, 0.7f); s.EchoSpread = 12f; }, tag: "echo");
            B("bb_storm", "GEWITTERKETTE", E, UpIcon.Storm, 1, n => "Kettenfunken springen bis zu " + V("5-mal") + " weiter, können kritisch treffen und machen " + P(0.6f) + " Schaden.",
                (s, n) => { s.ChainJumps = 5; s.ChainCanCrit = true; s.ChainFrac = Mathf.Max(s.ChainFrac, 0.6f); }, req: new[] { "bb_spark" });
            B("bb_nova", "NOVA", E, UpIcon.Nova, 2, n => "Jeder " + N(n == 1 ? 12 : 9) + ". Treffer löst eine Nova um dich aus (" + N(60) + " Schaden).",
                (s, n) => s.NovaEvery = n == 1 ? 12 : 9);
            B("bb_wildfire", "LAUFFEUER", E, UpIcon.Fire, 1, n => "Brand +" + P(0.5f) + ". Brennende Gegner entzünden beim Tod alle in der Nähe.",
                (s, n) => { s.BurnSpread = true; s.BurnBoost += 0.5f; s.BurnTime = 4f; }, req: new[] { "bb_fire" }, tag: "fire");
            B("bb_frostcore", "FROSTKERN", E, UpIcon.Frost, 1, n => "Verlangsamte Gegner erleiden +" + P(0.35f) + " Schaden, jeder 3. Treffer friert sie ein.",
                (s, n) => { s.FrostVuln = 0.35f; s.FreezeOnThird = true; }, req: new[] { "bb_ice" }, tag: "frost");
            B("bb_cannon", "KANONIER", E, UpIcon.Explode, 1, n => "Würfe explodieren beim Treffer (" + P(0.5f) + " Schaden als Fläche).", (s, n) => s.Cannoneer = true, tag: "explode");
            B("bb_clutch", "CLUTCH", E, UpIcon.Time, 1, n => "+" + P(0.1f) + " Krit-Chance. Kritische Treffer verlangsamen kurz die Zeit.",
                (s, n) => { s.BulletTime = true; s.CritChance += 0.1f; });
            B("bb_run", "PUNKTESERIE", E, UpIcon.Frenzy, 1, n => "Jeder Sieg: +" + P(0.06f) + " Schaden für 4 s, bis zu " + V("10-fach") + ".", (s, n) => s.BloodFrenzy = true);
            B("bb_hothand", "HEISSE HAND", E, UpIcon.Fire, 1, n => "Nach " + V("3") + " Würfen in Folge, die treffen, fängt der Ball Feuer: " + N(5) + " s lang +" + P(0.4f) + " Schaden, Treffer setzen in Brand.",
                (s, n) => s.HotHand = true);
            B("bb_thermal", "THERMOSCHOCK", E, UpIcon.Synergy, 1, n => "Synergie: Treffer auf brennende UND verlangsamte Gegner lösen eine Frostexplosion aus (" + P(1.5f) + ").",
                (s, n) => s.ThermalShock = true, req: new[] { "bb_fire", "bb_ice" });
            B("bb_rain", "DREIER-REGEN", E, UpIcon.Swish, 1, n => "Der Dreier wirft " + V("2") + " weitere Bälle links und rechts vom Ziel (" + P(0.6f) + " Schaden).",
                (s, n) => s.ThreeSplit = 2, ab: Ability.Three);
            B("bb_quake", "ERDBEBEN", E, UpIcon.Shockwave, 1, n => "Der Dunk schickt " + V("eine Druckwelle mehr") + ", alle Wellen reichen " + P(0.25f) + " weiter.",
                (s, n) => { s.DunkExtraWaves += 1; s.DunkWaveMul += 0.25f; }, ab: Ability.Dunk);
            B("bb_mirror", "SPIEGELBILD", E, UpIcon.Echo, 1, n => "Während des Crossover-Boosts feuert jeder Wurf " + V("2") + " durchschlagende Echo-Bälle.",
                (s, n) => s.CrossMirror = true, ab: Ability.Crossover);
            B("bb_second", "ZWEITER KONTAKT", E, UpIcon.Ricochet, 1, n => "Nach dem Einschlag springt der Alley-Oop auf einen zweiten Gegner (" + P(0.6f) + " Schaden).",
                (s, n) => s.OopBounce = true, ab: Ability.AlleyOop);
            B("bb_lockdown", "LOCKDOWN", E, UpIcon.Palm, 1, n => "Der Block betäubt alle Gegner in der Nähe " + N(1.2f) + " s lang.", (s, n) => s.BlockStun = true, ab: Ability.Block);
            B("bb_trail", "FLAMMENSPUR", E, UpIcon.Fire, 1, n => "Der Fastbreak setzt jeden getroffenen Gegner " + N(3) + " s in Brand und zieht eine Feuerspur.",
                (s, n) => s.FastBreakFire = true, ab: Ability.FastBreak);
            B("bb_fade", "FADEAWAY", E, UpIcon.AirKick, 1, n => "Würfe aus der Luft machen +" + P(0.35f) + " Schaden, " + V("+1") + " Bodenpass pro Sprung.",
                (s, n) => { s.AirThrowBonus += 0.35f; s.AirBoosts += 1; });

            // ------------------------------------------------------------------ legendary
            B("bb_downtown", "DOWNTOWN", L, UpIcon.Distance, 1, n => "Je weiter ein Wurf fliegt, desto mehr Schaden: bis zu +" + P(0.6f) + ". Treffer aus über " + N(9) + " m sind immer kritisch.",
                (s, n) => s.Downtown = true);
            B("bb_golden", "GOLDENER BALL", L, UpIcon.GoldenBoot, 1, n => "Jeder " + V("3.") + " Wurf ist garantiert kritisch, explodiert und verkettet Funken.", (s, n) => s.GoldenBoot = true);
            B("bb_twin", "ZWILLINGSSONNE", L, UpIcon.TwinSun, 1, n => "Ein Geisterball umkreist dich und wirft alle " + N(1.2f) + " s auf den nächsten Gegner. Erbt alle Treffer-Effekte.",
                (s, n) => s.TwinSun = true);
            B("bb_phoenix", "PHÖNIX", L, UpIcon.Phoenix, 1, n => "Einmal pro Lauf: Tödlicher Schaden belebt dich mit " + P(0.5f) + " Leben in einer Feuerexplosion wieder.",
                (s, n) => s.Revives += 1);
            B("bb_perpetual", "PERPETUUM MOBILE", L, UpIcon.Infinity, 1, n => "Jeder Sieg verkürzt alle Abklingzeiten um " + N(0.5f) + " s, jeder Krit um " + N(0.2f) + " s.",
                (s, n) => s.Perpetual = true);
            B("bb_showtime", "SHOWTIME", L, UpIcon.Maestro, 1, n => "Jede Fähigkeit macht dich " + N(1) + " s unverwundbar und feuert eine Echo-Salve auf nahe Gegner.",
                (s, n) => s.Maestro = true);
            B("bb_skywalker", "SKYWALKER", L, UpIcon.Slam, 1, n => "Besiegt ein Dunk einen Gegner, ist er sofort wieder bereit.", (s, n) => s.DunkRefund = true, ab: Ability.Dunk);
            B("bb_splash", "SPLASH ZONE", L, UpIcon.Swish, 1, n => "Wo der Dreier einschlägt, brennt der Boden " + N(3) + " s und setzt jeden Gegner darauf in Brand.",
                (s, n) => s.ThreeBurn = true, ab: Ability.Three);
        }

        public static int Count(Rarity r) { int c = 0; foreach (var u in All) if (u.Rarity == r) c++; return c; }
    }

    /// <summary>
    /// Offers three cards. Rarity weights climb with the stage and with luck; boss rewards skip commons;
    /// after three offers without an epic or better, one card is guaranteed epic+ (pity). Cards whose
    /// synergy partners are owned, and upgrades the build already uses, are weighted up so builds form.
    /// </summary>
    public static class UpgradeRoller
    {
        static readonly List<UpgradeDef> candidates = new List<UpgradeDef>();

        public static void Weights(RunState run, bool boss, out float c, out float r, out float e, out float l)
        {
            int s = run.Stage - 1;
            float luck = run.Stats.Luck;
            // choices only come every second round, so each one leans a little richer
            if (boss) { c = 0f; r = 52f; e = 35f; l = 13f; }
            else
            {
                c = Mathf.Max(24f, 52f - 3f * s);
                r = Mathf.Min(40f, 31f + 1.5f * s);
                e = Mathf.Min(24f, 12f + 1.1f * s);
                l = Mathf.Min(9f, 3f + 0.35f * s);
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
            if (u.Sport != run.Sport) return false;
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
            bool pity = run.OffersSinceEpic >= 3;
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
