using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    public enum CharacterClass { Striker, Defender, Skiller }

    /// <summary>
    /// The numbers behind the three class traits, in one place so balancing never has to hunt
    /// through gameplay code. The card texts are built from these values.
    /// </summary>
    public static class ClassTuning
    {
        // Stürmer · Schussgewalt
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
        public const float DefenderHeaderBonus = 0.25f;   // +25 % header damage
    }

    /// <summary>
    /// One class: its identity on the cards (name, colour, icon, description, strengths), its move
    /// on the right mouse button and its trait, a passive that is always on while a character of
    /// this class plays.
    /// </summary>
    public sealed class ClassDef
    {
        public CharacterClass Class;
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

        public static readonly ClassDef Striker = new ClassDef
        {
            Class = CharacterClass.Striker, Name = "STÜRMER", TraitName = "SCHUSSGEWALT",
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
            Trait = new PassiveDef
            {
                Id = "trait_striker", Name = "SCHUSSGEWALT",
                Text = "+" + Pct(ClassTuning.StrikerShotBonus) + " Schaden mit allen Schüssen, Schuss-Fähigkeiten laden " + Pct(1f - ClassTuning.StrikerShotCooldown) + " schneller.",
                Apply = (s, n) =>
                {
                    s.CategoryDamage[(int)SkillCategory.Shot] += ClassTuning.StrikerShotBonus;
                    s.CategoryCooldown[(int)SkillCategory.Shot] *= ClassTuning.StrikerShotCooldown;
                    s.ShotImpactFx = true;
                },
            },
        };

        public static readonly ClassDef Skiller = new ClassDef
        {
            Class = CharacterClass.Skiller, Name = "SKILLER", TraitName = "TECHNIKMEISTER",
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
            Trait = new PassiveDef
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
            },
        };

        public static readonly ClassDef Defender = new ClassDef
        {
            Class = CharacterClass.Defender, Name = "VERTEIDIGER", TraitName = "BOLLWERK",
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
            Trait = new PassiveDef
            {
                Id = "trait_defender", Name = "BOLLWERK",
                Text = "+" + Mathf.RoundToInt(ClassTuning.DefenderHp) + " Leben, " + Pct(1f - ClassTuning.DefenderDamageTaken) + " weniger erlittener Schaden, starker Kopfball. " + Pct(1f - ClassTuning.DefenderDamageMul) + " weniger Schaden.",
                Apply = (s, n) =>
                {
                    s.MaxHpBonus += ClassTuning.DefenderHp;
                    s.DamageTaken *= ClassTuning.DefenderDamageTaken;
                    s.DamageMul *= ClassTuning.DefenderDamageMul;
                    s.CategoryDamage[(int)SkillCategory.Header] += ClassTuning.DefenderHeaderBonus;
                },
            },
        };

        public static readonly IReadOnlyList<ClassDef> All = new[] { Striker, Defender, Skiller };

        public static ClassDef Of(CharacterClass c)
        {
            foreach (var d in All) if (d.Class == c) return d;
            return Striker;
        }
    }
}
