using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    public enum ShopKind { Character }

    /// <summary>
    /// Something that can be bought once and then belongs to the player. The shop knows nothing
    /// about characters beyond this: a new kind of item (a ball skin, a character upgrade level, a
    /// bundle) is a new ShopKind plus the three delegates. Skills are never sold — every player has
    /// all of them and picks them up after boss fights.
    /// </summary>
    public sealed class ShopItem
    {
        public string Id;
        public ShopKind Kind;
        public Price Price;
        public string Name, Description;
        public Color Accent;
        public System.Func<bool> Owned;
        public System.Action Grant;
        /// <summary>Why it can't be bought right now (null: it can).</summary>
        public System.Func<string> Blocked;
        public CharacterDef Character;
    }

    /// <summary>
    /// The catalogue and the checkout. Buying spends from the wallet, grants the item, saves the
    /// profile at once (a purchase must never be lost) and raises Purchased for the effects.
    /// </summary>
    public static class Shop
    {
        public enum Result { Bought, Owned, TooExpensive, Blocked }

        public static event System.Action<ShopItem> Purchased;

        static List<ShopItem> items;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { items = null; Purchased = null; }

        public static IReadOnlyList<ShopItem> All
        {
            get
            {
                if (items == null) Build();
                return items;
            }
        }

        static void Build()
        {
            items = new List<ShopItem>();
            foreach (var c in Characters.All)
            {
                var def = c;
                items.Add(new ShopItem
                {
                    Id = "char_" + def.Id, Kind = ShopKind.Character, Price = def.Cost, Name = def.Name,
                    Description = def.Flavour, Accent = def.Accent, Character = def,
                    Owned = () => Profile.OwnsCharacter(def.Id),
                    Grant = () => Profile.GrantCharacter(def.Id),
                });
            }
        }

        public static ShopItem Find(string id)
        {
            foreach (var i in All) if (i.Id == id) return i;
            return null;
        }

        public static ShopItem ForCharacter(CharacterDef c) => Find("char_" + c.Id);

        public static IEnumerable<ShopItem> OfKind(ShopKind kind)
        {
            foreach (var i in All) if (i.Kind == kind) yield return i;
        }

        public static Result Check(ShopItem item)
        {
            if (item.Owned()) return Result.Owned;
            if (item.Blocked?.Invoke() != null) return Result.Blocked;
            if (!Wallet.CanAfford(item.Price)) return Result.TooExpensive;
            return Result.Bought;
        }

        public static Result Buy(ShopItem item)
        {
            var r = Check(item);
            if (r != Result.Bought) return r;
            if (!Wallet.TrySpend(item.Price)) return Result.TooExpensive;
            item.Grant();
            Profile.Save();
            Purchased?.Invoke(item);
            return Result.Bought;
        }
    }
}
