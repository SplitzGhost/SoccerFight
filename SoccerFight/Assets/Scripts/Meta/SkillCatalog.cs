using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// What kind of move a skill is. The class traits hang off these: the striker hits harder and
    /// recharges faster with every Shot, the skiller plays Technique faster and harder, the
    /// defender's header is its own kind.
    /// </summary>
    public enum SkillCategory { Shot, Technique, Defense, Header, Utility }

    /// <summary>
    /// A skill of the boss pool: the ability it plays and its category. Every player has all of
    /// them — nothing is bought — and a run picks up at most four, one after each boss fight.
    /// Name, text, icon and colour come from Abilities so the run and the menus always agree.
    /// </summary>
    public sealed class SkillDef
    {
        public Ability Ability;
        /// <summary>Stable key — never rename.</summary>
        public string Id;
        public SkillCategory Category;

        public string Name => Abilities.Name(Ability);
        public string Description => Abilities.Description(Ability);
        public Sprite Icon => Abilities.Icon(Ability);
        public Color Accent => Abilities.Accent(Ability);
    }

    public static class SkillCatalog
    {
        static SkillDef S(Ability a, SkillCategory cat)
            => new SkillDef { Ability = a, Id = a.ToString().ToLowerInvariant(), Category = cat };

        /// <summary>Every skill a boss can offer. The class moves on the right mouse button are not in here.</summary>
        public static readonly IReadOnlyList<SkillDef> All = new[]
        {
            S(Ability.Tackle,   SkillCategory.Defense),
            S(Ability.Juggle,   SkillCategory.Utility),     // heals, no damage: nothing for a talent to boost
            S(Ability.Decoy,    SkillCategory.Technique),
            S(Ability.StepOver, SkillCategory.Technique),
            S(Ability.Nutmeg,   SkillCategory.Technique),
            S(Ability.Flick,    SkillCategory.Technique),
            S(Ability.Wall,     SkillCategory.Defense),
            S(Ability.Bicycle,  SkillCategory.Shot),
            S(Ability.Punt,     SkillCategory.Shot),
            S(Ability.Whistle,  SkillCategory.Utility),
        };

        /// <summary>The basketball boss pool.</summary>
        public static readonly IReadOnlyList<SkillDef> Hoops = new[]
        {
            S(Ability.AlleyOop,  SkillCategory.Shot),
            S(Ability.Block,     SkillCategory.Defense),
            S(Ability.FastBreak, SkillCategory.Technique),
        };

        public static IReadOnlyList<SkillDef> ForSport(Sport s) => s == Sport.Basketball ? Hoops : All;

        static bool IsHoops => Characters.Current.Sport == Sport.Basketball;

        static readonly Dictionary<Ability, SkillDef> byAbility = new Dictionary<Ability, SkillDef>();
        static readonly Dictionary<string, SkillDef> byId = new Dictionary<string, SkillDef>();

        static SkillCatalog()
        {
            foreach (var s in All) { byAbility[s.Ability] = s; byId[s.Id] = s; }
            foreach (var s in Hoops) { byAbility[s.Ability] = s; byId[s.Id] = s; }
        }

        public static SkillDef Get(Ability a) => byAbility.TryGetValue(a, out var s) ? s : null;
        public static SkillDef Get(string id) => id != null && byId.TryGetValue(id, out var s) ? s : null;

        /// <summary>The category of any ability, class moves included (null: none, e.g. the air kick).</summary>
        public static SkillCategory? CategoryOf(Ability a)
        {
            switch (a)
            {
                case Ability.Shot: case Ability.Power: case Ability.Three: return SkillCategory.Shot;
                case Ability.Dash: case Ability.Crossover: return SkillCategory.Technique;
                case Ability.Header: case Ability.Dunk: return SkillCategory.Header;
                default:
                    var s = Get(a);
                    return s != null ? s.Category : (SkillCategory?)null;
            }
        }

        public static string CategoryName(SkillCategory c)
        {
            switch (c)
            {
                case SkillCategory.Shot: return IsHoops ? "WURF" : "SCHUSS";
                case SkillCategory.Technique: return "TRICK";
                case SkillCategory.Defense: return "ABWEHR";
                case SkillCategory.Header: return IsHoops ? "DUNK" : "KOPFBALL";
                default: return "SPEZIAL";
            }
        }

        public static Color CategoryColor(SkillCategory c)
        {
            switch (c)
            {
                case SkillCategory.Shot: return new Color(1f, 0.5f, 0.42f);
                case SkillCategory.Technique: return new Color(0.8f, 0.56f, 1f);
                case SkillCategory.Defense: return new Color(0.48f, 0.7f, 1f);
                case SkillCategory.Header: return new Color(0.5f, 0.86f, 1f);
                default: return new Color(0.8f, 0.9f, 0.96f);
            }
        }

        /// <summary>
        /// The category a hit belongs to (for the class damage bonuses). Only moves the player makes
        /// directly count — chains, explosions and burns carry damage that was already scaled.
        /// </summary>
        public static SkillCategory? CategoryOf(Src src)
        {
            switch (src)
            {
                case Src.Shot: case Src.Returning: case Src.Echo: case Src.TwinSun:
                case Src.Power: case Src.Blast: case Src.Punt: case Src.Three: case Src.AlleyOop:
                    return SkillCategory.Shot;
                case Src.Rainbow: case Src.RainbowPass: case Src.Dash: case Src.Nutmeg: case Src.Decoy: case Src.FastBreak:
                    return SkillCategory.Technique;
                case Src.Tackle: case Src.Block:
                    return SkillCategory.Defense;
                case Src.Header: case Src.Dunk:
                    return SkillCategory.Header;
                default:
                    return null;
            }
        }

        /// <summary>The category of the action the player is in the middle of (for the skiller's haste).</summary>
        public static SkillCategory? CategoryOf(Player.Action a)
        {
            switch (a)
            {
                case Player.Action.Flick: case Player.Action.StepOver: case Player.Action.Nutmeg: case Player.Action.Decoy:
                case Player.Action.Dash: case Player.Action.Crossover: case Player.Action.FastBreak:
                    return SkillCategory.Technique;
                case Player.Action.Bicycle: case Player.Action.Punt: case Player.Action.AlleyOop:
                    return SkillCategory.Shot;
                case Player.Action.Tackle: case Player.Action.Wall: case Player.Action.Block:
                    return SkillCategory.Defense;
                case Player.Action.Header: case Player.Action.Dunk:
                    return SkillCategory.Header;
                default:
                    return null;
            }
        }
    }
}
