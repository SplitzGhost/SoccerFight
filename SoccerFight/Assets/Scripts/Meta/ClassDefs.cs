using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    public enum CharacterClass { Striker, Defender, Skiller }

    /// <summary>
    /// The numbers behind the three class traits, in one place so balancing never has to hunt
    /// through gameplay code. The card texts are built from these values. Every sport shares them;
    /// only the moves they boost differ.
    /// </summary>
    public static class ClassTuning
    {
        // Stürmer / Angreifer · Schussgewalt / Wurfgewalt
        public const float StrikerShotBonus = 0.30f;      // +30 % damage for every shot-type move
        public const float StrikerShotCooldown = 0.8f;    // shot skills recharge 20 % faster

        // Skiller · Technikmeister
        public const float SkillerHaste = 1.35f;          // trick moves play 35 % faster
        public const float SkillerTechBonus = 0.25f;      // +25 % damage for tricks
        public const float SkillerTechCooldown = 0.8f;    // trick skills (and the dash) recharge 20 % faster
        public const float SkillerRushSpeed = 0.30f;      // +30 % run speed ...
        public const float SkillerRushTime = 1.6f;        // ... for this long after every trick

        // Verteidiger · Bollwerk
        public const float DefenderHp = 40f;              // 120 → 160 max health
        public const float DefenderDamageTaken = 0.85f;   // takes 15 % less from every hit
        public const float DefenderDamageMul = 0.9f;      // deals 10 % less overall
        public const float DefenderHeaderBonus = 0.25f;   // +25 % header / dunk damage
    }

    /// <summary>
    /// One class of one sport: its identity on the cards (name, colour, icon, description,
    /// strengths), its move on the right mouse button and its trait, a passive that is always on
    /// while a character of this class plays.
    /// </summary>
    public sealed class ClassDef
    {
        public CharacterClass Class;
        public Sport Sport;
        public string Name;
        public string TraitName;
        public string Tagline;
        public string Description;
        /// <summary>Short lines for the cards ("+30 % SCHADEN MIT SCHÜSSEN").</summary>
        public string[] Strengths;
        /// <summary>The price of the strengths (empty when there is none).</summary>
        public string Drawback;
        public Color Accent;
        public System.Func<Sprite> Icon;
        /// <summary>The skill category the class is built around (boss offers mark it as boosted).</summary>
        public SkillCategory Specialty;
        /// <summary>The class move on the right mouse button, there from the first second of a run.</summary>
        public Ability Primary;
        public PassiveDef Trait;
        /// <summary>Card bars 1..5.</summary>
        public int Attack, Defence, Tech;
    }

    public static class Classes
    {
        static Color Hex(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
        static string Pct(float f) => Mathf.RoundToInt(f * 100f) + " %";

        // ------------------------------------------------------------------ the traits (shared by every sport)

        static PassiveDef StrikerTrait(string name, string shots) => new PassiveDef
        {
            Id = "trait_striker", Name = name,
            Text = "+" + Pct(ClassTuning.StrikerShotBonus) + " Schaden mit allen " + shots + ", " + shots + "-Fähigkeiten laden " + Pct(1f - ClassTuning.StrikerShotCooldown) + " schneller.",
            Apply = (s, n) =>
            {
                s.CategoryDamage[(int)SkillCategory.Shot] += ClassTuning.StrikerShotBonus;
                s.CategoryCooldown[(int)SkillCategory.Shot] *= ClassTuning.StrikerShotCooldown;
                s.ShotImpactFx = true;
            },
        };

        static readonly PassiveDef SkillerTrait = new PassiveDef
        {
            Id = "trait_skiller", Name = "TECHNIKMEISTER",
            Text = "Tricks laufen " + Pct(ClassTuning.SkillerHaste - 1f) + " schneller, laden " + Pct(1f - ClassTuning.SkillerTechCooldown) + " schneller, machen +" + Pct(ClassTuning.SkillerTechBonus) + " Schaden und geben einen Sprint.",
            Apply = (s, n) =>
            {
                s.CategoryDamage[(int)SkillCategory.Technique] += ClassTuning.SkillerTechBonus;
                s.CategoryCooldown[(int)SkillCategory.Technique] *= ClassTuning.SkillerTechCooldown;
                s.TechniqueHaste *= ClassTuning.SkillerHaste;
                s.RushSpeed = Mathf.Max(s.RushSpeed, ClassTuning.SkillerRushSpeed);
                s.RushTime = Mathf.Max(s.RushTime, ClassTuning.SkillerRushTime);
            },
        };

        static PassiveDef DefenderTrait(string heavy) => new PassiveDef
        {
            Id = "trait_defender", Name = "BOLLWERK",
            Text = "+" + Mathf.RoundToInt(ClassTuning.DefenderHp) + " Leben, " + Pct(1f - ClassTuning.DefenderDamageTaken) + " weniger erlittener Schaden, starker " + heavy + ". " + Pct(1f - ClassTuning.DefenderDamageMul) + " weniger Schaden.",
            Apply = (s, n) =>
            {
                s.MaxHpBonus += ClassTuning.DefenderHp;
                s.DamageTaken *= ClassTuning.DefenderDamageTaken;
                s.DamageMul *= ClassTuning.DefenderDamageMul;
                s.CategoryDamage[(int)SkillCategory.Header] += ClassTuning.DefenderHeaderBonus;
            },
        };

        // ------------------------------------------------------------------ soccer

        public static readonly ClassDef Striker = new ClassDef
        {
            Class = CharacterClass.Striker, Sport = Sport.Soccer, Name = "STÜRMER", TraitName = "SCHUSSGEWALT",
            Tagline = "Wuchtige Schüsse, hoher Schaden",
            Description = "Lebt vom Abschluss: Jeder Schuss trifft härter, Schuss-Fähigkeiten laden schneller, verstärkte Treffer schlagen mit einem Wuchtstern ein.",
            Strengths = new[]
            {
                "+" + Pct(ClassTuning.StrikerShotBonus) + " SCHADEN MIT SCHÜSSEN",
                "SCHUSS-FÄHIGKEITEN LADEN " + Pct(1f - ClassTuning.StrikerShotCooldown) + " SCHNELLER",
                "RECHTSKLICK: POWER-SCHUSS",
            },
            Drawback = "",
            Accent = Hex("#FF5A4A"), Icon = () => MenuArt.IconStriker, Specialty = SkillCategory.Shot, Primary = Ability.Power,
            Attack = 5, Defence = 2, Tech = 3,
            Trait = StrikerTrait("SCHUSSGEWALT", "Schüssen"),
        };

        public static readonly ClassDef Skiller = new ClassDef
        {
            Class = CharacterClass.Skiller, Sport = Sport.Soccer, Name = "SKILLER", TraitName = "TECHNIKMEISTER",
            Tagline = "Schnell, wendig, trickreich",
            Description = "Tricks statt Kraft: Trick-Fähigkeiten laufen schneller ab, laden schneller, treffen härter und geben nach jedem Einsatz einen kurzen Sprint.",
            Strengths = new[]
            {
                "+" + Pct(ClassTuning.SkillerTechBonus) + " SCHADEN MIT TRICKS",
                "TRICKS " + Pct(ClassTuning.SkillerHaste - 1f) + " SCHNELLER, LADEN " + Pct(1f - ClassTuning.SkillerTechCooldown) + " SCHNELLER",
                "RECHTSKLICK: ANTRITT (DASH)",
            },
            Drawback = "",
            Accent = Hex("#C77DFF"), Icon = () => MenuArt.IconSkiller, Specialty = SkillCategory.Technique, Primary = Ability.Dash,
            Attack = 2, Defence = 3, Tech = 5,
            Trait = SkillerTrait,
        };

        public static readonly ClassDef Defender = new ClassDef
        {
            Class = CharacterClass.Defender, Sport = Sport.Soccer, Name = "VERTEIDIGER", TraitName = "BOLLWERK",
            Tagline = "Zäh, standfest, kopfballstark",
            Description = "Stellt sich dazwischen: mehr Leben, weniger Schaden durch Treffer und ein wuchtiger Kopfball auf der rechten Maustaste.",
            Strengths = new[]
            {
                "+" + Mathf.RoundToInt(ClassTuning.DefenderHp) + " MAXIMALES LEBEN",
                Pct(1f - ClassTuning.DefenderDamageTaken) + " WENIGER ERLITTENER SCHADEN",
                "RECHTSKLICK: KOPFBALL (+" + Pct(ClassTuning.DefenderHeaderBonus) + ")",
            },
            Drawback = Pct(1f - ClassTuning.DefenderDamageMul) + " WENIGER SCHADEN",
            Accent = Hex("#5B8CFF"), Icon = () => MenuArt.IconDefender, Specialty = SkillCategory.Header, Primary = Ability.Header,
            Attack = 3, Defence = 5, Tech = 2,
            Trait = DefenderTrait("Kopfball"),
        };

        // ------------------------------------------------------------------ basketball

        public static readonly ClassDef HoopsStriker = new ClassDef
        {
            Class = CharacterClass.Striker, Sport = Sport.Basketball, Name = "ANGREIFER", TraitName = "WURFGEWALT",
            Tagline = "Treffsicher aus jeder Distanz",
            Description = "Lebt vom Wurf: Jeder Wurf trifft härter, Wurf-Fähigkeiten laden schneller, der Dreier landet mit einer Explosion genau am Fadenkreuz.",
            Strengths = new[]
            {
                "+" + Pct(ClassTuning.StrikerShotBonus) + " SCHADEN MIT WÜRFEN",
                "WURF-FÄHIGKEITEN LADEN " + Pct(1f - ClassTuning.StrikerShotCooldown) + " SCHNELLER",
                "RECHTSKLICK: DREIER MIT STEP-BACK",
            },
            Drawback = "",
            Accent = Striker.Accent, Icon = () => MenuArt.IconStriker, Specialty = SkillCategory.Shot, Primary = Ability.Three,
            Attack = 5, Defence = 2, Tech = 3,
            Trait = StrikerTrait("WURFGEWALT", "Würfen"),
        };

        public static readonly ClassDef HoopsSkiller = new ClassDef
        {
            Class = CharacterClass.Skiller, Sport = Sport.Basketball, Name = "SKILLER", TraitName = "TECHNIKMEISTER",
            Tagline = "Ballzauber und Tempo",
            Description = "Tricks statt Kraft: Nach dem Crossover durch die Beine wird er schnell und seine Würfe fliegen durch jeden Gegner.",
            Strengths = new[]
            {
                "+" + Pct(ClassTuning.SkillerTechBonus) + " SCHADEN MIT TRICKS",
                "TRICKS " + Pct(ClassTuning.SkillerHaste - 1f) + " SCHNELLER, LADEN " + Pct(1f - ClassTuning.SkillerTechCooldown) + " SCHNELLER",
                "RECHTSKLICK: CROSSOVER (TEMPO + DURCHSCHLAG)",
            },
            Drawback = "",
            Accent = Skiller.Accent, Icon = () => MenuArt.IconSkiller, Specialty = SkillCategory.Technique, Primary = Ability.Crossover,
            Attack = 2, Defence = 3, Tech = 5,
            Trait = SkillerTrait,
        };

        public static readonly ClassDef HoopsDefender = new ClassDef
        {
            Class = CharacterClass.Defender, Sport = Sport.Basketball, Name = "VERTEIDIGER", TraitName = "BOLLWERK",
            Tagline = "Zäh, stark, gehört unter den Korb",
            Description = "Stellt sich dazwischen: mehr Leben, weniger Schaden durch Treffer, und sein Dunk schickt Druckwellen in alle Richtungen.",
            Strengths = new[]
            {
                "+" + Mathf.RoundToInt(ClassTuning.DefenderHp) + " MAXIMALES LEBEN",
                Pct(1f - ClassTuning.DefenderDamageTaken) + " WENIGER ERLITTENER SCHADEN",
                "RECHTSKLICK: DUNK (+" + Pct(ClassTuning.DefenderHeaderBonus) + ")",
            },
            Drawback = Pct(1f - ClassTuning.DefenderDamageMul) + " WENIGER SCHADEN",
            Accent = Defender.Accent, Icon = () => MenuArt.IconDefender, Specialty = SkillCategory.Header, Primary = Ability.Dunk,
            Attack = 3, Defence = 5, Tech = 2,
            Trait = DefenderTrait("Dunk"),
        };

        /// <summary>The soccer classes (the order of the class tabs).</summary>
        public static readonly IReadOnlyList<ClassDef> All = new[] { Striker, Defender, Skiller };
        public static readonly IReadOnlyList<ClassDef> Hoops = new[] { HoopsStriker, HoopsDefender, HoopsSkiller };

        public static IReadOnlyList<ClassDef> ForSport(Sport sport) => sport == Sport.Basketball ? Hoops : All;

        public static ClassDef Of(CharacterClass c, Sport sport = Sport.Soccer)
        {
            foreach (var d in ForSport(sport)) if (d.Class == c) return d;
            return Striker;
        }
    }
}
