using System.Collections.Generic;
using System.Linq;

namespace JeffopolyDeal.Models
{
    /// <summary>
    /// A single property card definition: stable ID for code, display name for UI.
    /// </summary>
    public record PropertyDef(string CardId, string DisplayName);

    /// <summary>
    /// Central registry of all property cards. Change DisplayNames to re-theme the game.
    /// CardIds are stable identifiers (e.g. brown1, railroad3) — never change these.
    /// Sorted by price tier (Brown = cheapest, DarkBlue = most expensive).
    /// </summary>
    public static class PropertyNames
    {
        // Brown (2 cards)
        public static readonly PropertyDef[] Brown = {
            new("brown1", "Chan's Market"),
            new("brown2", "Wendy's"),
        };

        // LightBlue (3 cards)
        public static readonly PropertyDef[] LightBlue = {
            new("lightblue1", "Cowichan River"),
            new("lightblue2", "The Lot"),
            new("lightblue3", "Inner Harbour"),
        };

        // Pink (3 cards)
        public static readonly PropertyDef[] Pink = {
            new("pink1", "Carmel Drive"),
            new("pink2", "Doral Place"),
            new("pink3", "Hudson Street"),
        };

        // Orange (3 cards)
        public static readonly PropertyDef[] Orange = {
            new("orange1", "Duncan"),
            new("orange2", "Victoria"),
            new("orange3", "Vancouver"),
        };

        // Red (3 cards)
        public static readonly PropertyDef[] Red = {
            new("red1", "Sushi Me"),
            new("red2", "Din Tai Fung"),
            new("red3", "Prime Steakhouse"),
        };

        // Yellow (3 cards)
        public static readonly PropertyDef[] Yellow = {
            new("yellow1", "Bellevue"),
            new("yellow2", "Redmond"),
            new("yellow3", "Sammamish"),
        };

        // Green (3 cards)
        public static readonly PropertyDef[] Green = {
            new("green1", "Woodbridge"),
            new("green2", "Timberline"),
            new("green3", "Lake House"),
        };

        // DarkBlue (2 cards)
        public static readonly PropertyDef[] DarkBlue = {
            new("darkblue1", "False Creek"),
            new("darkblue2", "Lake Sammamish"),
        };

        // Railroad → Sports Stadiums (4 cards)
        public static readonly PropertyDef[] Stadium = {
            new("railroad1", "ESP Ball Fields"),
            new("railroad2", "Folsom Field"),
            new("railroad3", "Lumen Field"),
            new("railroad4", "T-Mobile Park"),
        };

        // Utility (2 cards)
        public static readonly PropertyDef[] Utility = {
            new("utility1", "Safeway"),
            new("utility2", "Whole Foods"),
        };

        /// <summary>
        /// Lookup by PropertyColor for use in deck building.
        /// </summary>
        public static readonly Dictionary<PropertyColor, PropertyDef[]> ByColor = new()
        {
            { PropertyColor.Brown, Brown },
            { PropertyColor.LightBlue, LightBlue },
            { PropertyColor.Pink, Pink },
            { PropertyColor.Orange, Orange },
            { PropertyColor.Red, Red },
            { PropertyColor.Yellow, Yellow },
            { PropertyColor.Green, Green },
            { PropertyColor.DarkBlue, DarkBlue },
            { PropertyColor.Railroad, Stadium },
            { PropertyColor.Utility, Utility },
        };

        /// <summary>
        /// Flat lookup: CardId → DisplayName for quick name resolution.
        /// </summary>
        public static readonly Dictionary<string, string> DisplayNameById =
            ByColor.Values
                .SelectMany(defs => defs)
                .ToDictionary(d => d.CardId, d => d.DisplayName);
    }
}
