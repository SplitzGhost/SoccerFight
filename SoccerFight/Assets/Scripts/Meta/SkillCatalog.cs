using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// What kind of move a skill is. The class traits hang off these: the striker hits harder with
    /// every Shot, the skiller plays Technique faster and harder, the defender owns the Header.
    /// </summary>
    public enum SkillCategory { Shot, Technique, Defense, Header, Utility }

    /// <summary>
    /// A skill as the meta game sees it: the ability it plays, its category, its shop price, whether
    /// a new player may take it for free, and which class may use it (null = everyone). Name, text,
    /// icon and colour come from Abilities so the run and the menus always agree.
    /// </summary>
    public sealed class SkillDef
    {
        public Ability Ability;
        /// <summary>Stable save key — never rename.</summary>
        public string Id;
        public SkillCategory Category;
        public Price Cost;
        /// <summary>One of the three free picks of a new player.</summary>
        public bool StarterPick = true;
        /// <summary>Only this class can equip it (null: every class).</summary>
        public CharacterClass? ClassLock;

        public string Name => Abilities.Name(Ability);
        public string Description => Abilities.Description(Ability);
        public Sprite Icon => Abilities.Icon(Ability);
        public Color Accent => Abilities.Accent(Ability);

        public bool UsableBy(CharacterClass c) => ClassLock == null || ClassLock.Value == c;
    }

    public static class SkillCatalog
    {
        static SkillDef S(Ability a, SkillCategory cat, int price, bool starter = true, CharacterClass? only = null)
            => new SkillDef { Ability = a, Id = a.ToString().ToLowerInvariant(), Category = cat, Cost = Price.Coins(price), StarterPick = starter, ClassLock = only };

        /// <summary>
        /// Every skill in menu order. Prices climb with how much a skill changes a fight: small tools
        /// first, the big finishers last, the whistle (an ultimate) is the most expensive and never free.
        /// </summary>
        public static readonly IReadOnlyList<SkillDef> All = new[]
        {
            S(Ability.Tackle,   SkillCategory.Defense,   150),
            S(Ability.Juggle,   SkillCategory.Technique, 150),
            S(Ability.Decoy,    SkillCategory.Technique, 200),
            S(Ability.StepOver, SkillCategory.Technique, 250),
            S(Ability.Nutmeg,   SkillCategory.Technique, 250),
            S(Ability.Flick,    SkillCategory.Technique, 300),
            S(Ability.Wall,     SkillCategory.Defense,   300),
            S(Ability.Header,   SkillCategory.Header,    300, true, CharacterClass.Defender),
            S(Ability.Bicycle,  SkillCategory.Shot,      400),
            S(Ability.Punt,     SkillCategory.Shot,      400),
            S(Ability.Whistle,  SkillCategory.Utility,   600, false),
        };

        static readonly Dictionary<Ability, SkillDef> byAbility = new Dictionary<Ability, SkillDef>();
        static readonly Dictionary<string, SkillDef> byId = new Dictionary<string, SkillDef>();

        static SkillCatalog()
        {
            foreach (var s in All) { byAbility[s.Ability] = s; byId[s.Id] = s; }
        }

        public static SkillDef Get(Ability a) => byAbility.TryGetValue(a, out var s) ? s : null;
        public static SkillDef Get(string id) => id != null && byId.TryGetValue(id, out var s) ? s : null;

        public static string CategoryName(SkillCategory c)
        {
            switch (c)
            {
                case SkillCategory.Shot: return "SCHUSS";
                case SkillCategory.Technique: return "TRICK";
                case SkillCategory.Defense: return "ABWEHR";
                case SkillCategory.Header: return "KOPFBALL";
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
                case Src.Power: case Src.Blast: case Src.Punt:
                    return SkillCategory.Shot;
                case Src.Rainbow: case Src.RainbowPass: case Src.Dash: case Src.Nutmeg: case Src.Decoy:
                    return SkillCategory.Technique;
                case Src.Tackle:
                    return SkillCategory.Defense;
                case Src.Header:
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
                    return SkillCategory.Technique;
                case Player.Action.Bicycle: case Player.Action.Punt:
                    return SkillCategory.Shot;
                case Player.Action.Tackle: case Player.Action.Wall:
                    return SkillCategory.Defense;
                case Player.Action.Header:
                    return SkillCategory.Header;
                default:
                    return null;
            }
        }
    }
}
