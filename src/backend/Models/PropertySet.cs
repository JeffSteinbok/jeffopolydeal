using System.Collections.Generic;
using System.Linq;

namespace JeffopolyDeal.Models
{
    /// <summary>
    /// Tracks a group of property cards of the same color belonging to a player.
    /// </summary>
    public class PropertySet
    {
        public PropertyColor Color { get; set; }
        public List<Card> Cards { get; set; } = new();
        public bool HasHouse { get; set; }
        public bool HasHotel { get; set; }

        public int Size => Cards.Count;
        public int RequiredSize => GameConfig.SetSize[Color];
        public bool IsComplete => Size >= RequiredSize;

        /// <summary>
        /// Calculates the rent for this property set based on number of cards.
        /// </summary>
        public int CalculateRent()
        {
            var rentTable = GameConfig.RentTable[Color];
            int propertyCount = System.Math.Min(Size, rentTable.Length - 1);
            int rent = rentTable[propertyCount];

            if (IsComplete)
            {
                if (HasHouse) rent += GameConfig.HouseRentBonus;
                if (HasHotel) rent += GameConfig.HotelRentBonus;
            }

            return rent;
        }
    }
}
