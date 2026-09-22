using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// One currency. Adding a currency is one more entry in Currencies: the wallet, the save file and
    /// every price work with any of them. The icon is looked up late because the menu art is built
    /// after the data layer.
    /// </summary>
    public sealed class CurrencyDef
    {
        /// <summary>Stable save key — never rename.</summary>
        public string Id;
        public string Name;
        public Color Color;
        public System.Func<Sprite> Icon;
        /// <summary>What a fresh profile starts with.</summary>
        public int Start;
    }

    /// <summary>An amount of one currency.</summary>
    public readonly struct Price
    {
        public readonly CurrencyDef Currency;
        public readonly int Amount;

        public Price(CurrencyDef currency, int amount) { Currency = currency; Amount = amount; }

        public static Price Coins(int amount) => new Price(Currencies.Coins, amount);
        public bool Free => Amount <= 0;
        public override string ToString() => Currencies.Format(Amount);
    }

    public static class Currencies
    {
        /// <summary>Earned in every run: monsters drop them, bosses drop a lot.</summary>
        public static readonly CurrencyDef Coins = new CurrencyDef
        {
            Id = "coins", Name = "MÜNZEN", Color = new Color(1f, 0.8f, 0.36f), Icon = () => MenuArt.IconCoin, Start = 0,
        };

        /// <summary>The rare currency (shown in the menu, nothing costs gems yet).</summary>
        public static readonly CurrencyDef Gems = new CurrencyDef
        {
            Id = "gems", Name = "KRISTALLE", Color = new Color(0.45f, 0.9f, 1f), Icon = () => MenuArt.IconGem, Start = 0,
        };

        public static readonly IReadOnlyList<CurrencyDef> All = new[] { Coins, Gems };

        public static CurrencyDef Get(string id)
        {
            foreach (var c in All) if (c.Id == id) return c;
            return null;
        }

        /// <summary>1.234 — German thousands separator (built by hand: WebGL ships without culture data).</summary>
        public static string Format(int amount)
        {
            string digits = Mathf.Abs(amount).ToString();
            var sb = new System.Text.StringBuilder(digits.Length + 4);
            if (amount < 0) sb.Append('-');
            for (int i = 0; i < digits.Length; i++)
            {
                if (i > 0 && (digits.Length - i) % 3 == 0) sb.Append('.');
                sb.Append(digits[i]);
            }
            return sb.ToString();
        }
    }
}
