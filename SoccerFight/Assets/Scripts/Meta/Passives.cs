using System.Collections.Generic;

namespace SoccerFight
{
    /// <summary>
    /// A permanent stat change from outside the run: a class trait, a character's personal perk,
    /// and later bought character or skill upgrades (anything with a level in the profile). Works
    /// like an upgrade card — Apply gets the stats and the level — so a passive can use every stat
    /// an upgrade can.
    /// </summary>
    public sealed class PassiveDef
    {
        /// <summary>Stable save key for leveled passives — never rename.</summary>
        public string Id;
        public string Name;
        /// <summary>One line for cards and tooltips.</summary>
        public string Text;
        /// <summary>Highest level a leveled passive can be bought to (1 for traits and perks).</summary>
        public int MaxLevel = 1;
        public System.Action<PlayerStats, int> Apply;
    }

    /// <summary>
    /// Everything outside the run that shapes the stats, applied in a fixed order before the run's
    /// upgrade cards: class trait → character perk → bought levels. RunState.Rebuild calls this, so
    /// a new source only has to be listed here.
    /// </summary>
    public static class MetaPassives
    {
        /// <summary>
        /// Passives the player can level up with currency (character upgrades, skill upgrades).
        /// Empty for now: add a PassiveDef with MaxLevel &gt; 1 here and a shop item that raises
        /// Profile.Level(id), and the stats follow automatically.
        /// </summary>
        public static readonly List<PassiveDef> Leveled = new List<PassiveDef>();

        public static void Apply(PlayerStats s, CharacterDef character)
        {
            if (character == null) return;
            character.ClassDef.Trait.Apply(s, 1);
            character.Perk?.Apply(s, 1);
            foreach (var p in Leveled)
            {
                int level = Profile.Level(p.Id);
                if (level > 0) p.Apply(s, UnityEngine.Mathf.Min(level, p.MaxLevel));
            }
        }
    }
}
