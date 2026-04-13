using System.Collections.Generic;

namespace JeffopolyDeal.Models
{
    /// <summary>
    /// Central registry of all property names. Change these to re-theme the game.
    /// Sorted by price tier (Brown = cheapest, DarkBlue = most expensive).
    /// </summary>
    public static class PropertyNames
    {
        // Brown (2 cards) — Duncan, BC area / rural Vancouver Island
        public static readonly string[] Brown = {
            "Cowichan Station",
            "Glenora General Store",
        };

        // LightBlue (3 cards) — Honeymoon Bay / Lake Cowichan area
        public static readonly string[] LightBlue = {
            "Honeymoon Bay",
            "Lake Cowichan",
            "Mesachie Lake",
        };

        // Pink (3 cards) — Duncan / Cowichan Valley
        public static readonly string[] Pink = {
            "Duncan",
            "Maple Bay",
            "Genoa Bay",
        };

        // Orange (3 cards) — Vancouver / Burnaby
        public static readonly string[] Orange = {
            "Gastown",
            "Kitsilano",
            "Commercial Drive",
        };

        // Red (3 cards) — East Side / Bellevue
        public static readonly string[] Red = {
            "Bellevue Square",
            "Crossroads Mall",
            "Factoria",
        };

        // Yellow (3 cards) — Lake Sammamish / Issaquah
        public static readonly string[] Yellow = {
            "Pine Lake",
            "Issaquah Highlands",
            "Sammamish Landing",
        };

        // Green (3 cards) — Seattle proper / premium areas
        public static readonly string[] Green = {
            "Capitol Hill",
            "Queen Anne",
            "Fremont",
        };

        // DarkBlue (2 cards) — Waterfront / flagship locations
        public static readonly string[] DarkBlue = {
            "Alki Beach",
            "Pike Place Market",
        };

        // Railroad → Sports Stadiums (4 cards) — Seattle & Vancouver
        public static readonly string[] Stadium = {
            "Lumen Field",
            "T-Mobile Park",
            "Climate Pledge Arena",
            "Rogers Arena",
        };

        // Utility (2 cards)
        public static readonly string[] Utility = {
            "Xfinity",
            "Puget Sound Energy",
        };

        /// <summary>
        /// Lookup by PropertyColor for use in deck building.
        /// </summary>
        public static readonly Dictionary<PropertyColor, string[]> ByColor = new()
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
    }
}
