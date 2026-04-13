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
        // Brown (2 cards) — Duncan, BC area / rural Vancouver Island
        public static readonly PropertyDef[] Brown = {
            new("brown1", "Cowichan Station"),
            new("brown2", "Glenora General Store"),
        };

        // LightBlue (3 cards) — Honeymoon Bay / Lake Cowichan area
        public static readonly PropertyDef[] LightBlue = {
            new("lightblue1", "Honeymoon Bay"),
            new("lightblue2", "Lake Cowichan"),
            new("lightblue3", "Mesachie Lake"),
        };

        // Pink (3 cards) — Duncan / Cowichan Valley
        public static readonly PropertyDef[] Pink = {
            new("pink1", "Duncan"),
            new("pink2", "Maple Bay"),
            new("pink3", "Genoa Bay"),
        };

        // Orange (3 cards) — Vancouver / Burnaby
        public static readonly PropertyDef[] Orange = {
            new("orange1", "Gastown"),
            new("orange2", "Kitsilano"),
            new("orange3", "Commercial Drive"),
        };

        // Red (3 cards) — East Side / Bellevue
        public static readonly PropertyDef[] Red = {
            new("red1", "Bellevue Square"),
            new("red2", "Crossroads Mall"),
            new("red3", "Factoria"),
        };

        // Yellow (3 cards) — Lake Sammamish / Issaquah
        public static readonly PropertyDef[] Yellow = {
            new("yellow1", "Pine Lake"),
            new("yellow2", "Issaquah Highlands"),
            new("yellow3", "Sammamish Landing"),
        };

        // Green (3 cards) — Seattle proper / premium areas
        public static readonly PropertyDef[] Green = {
            new("green1", "Capitol Hill"),
            new("green2", "Queen Anne"),
            new("green3", "Fremont"),
        };

        // DarkBlue (2 cards) — Waterfront / flagship locations
        public static readonly PropertyDef[] DarkBlue = {
            new("darkblue1", "Alki Beach"),
            new("darkblue2", "Pike Place Market"),
        };

        // Railroad → Sports Stadiums (4 cards) — Seattle & Vancouver
        public static readonly PropertyDef[] Stadium = {
            new("railroad1", "Lumen Field"),
            new("railroad2", "T-Mobile Park"),
            new("railroad3", "Climate Pledge Arena"),
            new("railroad4", "Rogers Arena"),
        };

        // Utility (2 cards)
        public static readonly PropertyDef[] Utility = {
            new("utility1", "Xfinity"),
            new("utility2", "Puget Sound Energy"),
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
