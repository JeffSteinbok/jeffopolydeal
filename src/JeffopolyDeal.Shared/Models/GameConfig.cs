using System.Collections.Generic;
using System.Linq;

namespace JeffopolyDeal.Models
{
    /// <summary>
    /// Serializable subset of game configuration sent to clients.
    /// </summary>
    public class GameConfigData
    {
        public Dictionary<string, int> SetSize { get; set; } = new();
        public Dictionary<string, int[]> RentTable { get; set; } = new();

        /// <summary>Build from the static GameConfig dictionaries.</summary>
        public static GameConfigData FromStatic() => new()
        {
            SetSize = GameConfig.SetSize.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
            RentTable = GameConfig.RentTable.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
        };
    }

    /// <summary>
    /// Static game configuration: rent tables, set sizes, card values.
    /// </summary>
    public static class GameConfig
    {
        /// <summary>
        /// Number of property cards needed to complete a set of each color.
        /// </summary>
        public static readonly Dictionary<PropertyColor, int> SetSize = new()
        {
            { PropertyColor.Brown, 2 },
            { PropertyColor.LightBlue, 3 },
            { PropertyColor.Pink, 3 },
            { PropertyColor.Orange, 3 },
            { PropertyColor.Red, 3 },
            { PropertyColor.Yellow, 3 },
            { PropertyColor.Green, 3 },
            { PropertyColor.DarkBlue, 2 },
            { PropertyColor.Railroad, 4 },
            { PropertyColor.Utility, 2 },
        };

        /// <summary>
        /// Rent values indexed by color and number of properties owned (1-based).
        /// E.g., RentTable[Brown][1] = 1, RentTable[Brown][2] = 2.
        /// </summary>
        public static readonly Dictionary<PropertyColor, int[]> RentTable = new()
        {
            { PropertyColor.Brown,     new[] { 0, 1, 2 } },
            { PropertyColor.LightBlue, new[] { 0, 1, 2, 3 } },
            { PropertyColor.Pink,      new[] { 0, 1, 2, 4 } },
            { PropertyColor.Orange,    new[] { 0, 1, 3, 5 } },
            { PropertyColor.Red,       new[] { 0, 2, 3, 6 } },
            { PropertyColor.Yellow,    new[] { 0, 2, 4, 6 } },
            { PropertyColor.Green,     new[] { 0, 2, 4, 7 } },
            { PropertyColor.DarkBlue,  new[] { 0, 3, 8 } },
            { PropertyColor.Railroad,  new[] { 0, 1, 2, 3, 4 } },
            { PropertyColor.Utility,   new[] { 0, 1, 2 } },
        };

        public const int HouseRentBonus = 3;
        public const int HotelRentBonus = 4;

        public const int InitialHandSize = 5;
        public const int DrawPerTurn = 2;
        public const int DrawWhenEmpty = 5;
        public const int MaxPlaysPerTurn = 3;
        public const int MaxHandSize = 7;
        public const int SetsToWin = 3;

        public const int DebtCollectorAmount = 5;
        public const int BirthdayAmount = 2;

        /// <summary>
        /// Monetary value of each property card by color (used when banked or paying).
        /// </summary>
        public static readonly Dictionary<PropertyColor, int> PropertyValue = new()
        {
            { PropertyColor.Brown, 1 },
            { PropertyColor.LightBlue, 1 },
            { PropertyColor.Pink, 2 },
            { PropertyColor.Orange, 2 },
            { PropertyColor.Red, 3 },
            { PropertyColor.Yellow, 3 },
            { PropertyColor.Green, 4 },
            { PropertyColor.DarkBlue, 4 },
            { PropertyColor.Railroad, 2 },
            { PropertyColor.Utility, 2 },
        };
    }
}
