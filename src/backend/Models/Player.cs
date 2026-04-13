using System.Collections.Generic;
using System.Linq;

namespace JeffopolyDeal.Models
{
    /// <summary>
    /// Represents a player in the game.
    /// </summary>
    public class Player
    {
        public string ConnectionId { get; set; } = "";
        public string Name { get; set; } = "";

        /// <summary>Cards in the player's hand (hidden from other players).</summary>
        public List<Card> Hand { get; set; } = new();

        /// <summary>Money cards in the player's bank (visible to all).</summary>
        public List<Card> Bank { get; set; } = new();

        /// <summary>Property sets on the table (visible to all).</summary>
        public List<PropertySet> PropertySets { get; set; } = new();

        /// <summary>Number of completed property sets.</summary>
        public int CompletedSetCount => PropertySets.Count(s => s.IsComplete);

        /// <summary>Total bank value.</summary>
        public int BankTotal => Bank.Sum(c => c.MoneyValue);

        /// <summary>
        /// Gets or creates a property set for the given color.
        /// </summary>
        public PropertySet GetOrCreatePropertySet(PropertyColor color)
        {
            var set = PropertySets.FirstOrDefault(s => s.Color == color);
            if (set == null)
            {
                set = new PropertySet { Color = color };
                PropertySets.Add(set);
            }
            return set;
        }

        /// <summary>
        /// Gets all property cards on the table (across all sets) that are not part of complete sets.
        /// Used for Sly Deal / Force Deal targeting.
        /// </summary>
        public List<Card> GetStealableProperties()
        {
            return PropertySets
                .Where(s => !s.IsComplete)
                .SelectMany(s => s.Cards)
                .ToList();
        }

        /// <summary>
        /// Gets all complete property sets. Used for Deal Breaker targeting.
        /// </summary>
        public List<PropertySet> GetCompletePropertySets()
        {
            return PropertySets.Where(s => s.IsComplete).ToList();
        }

        /// <summary>
        /// Gets all cards on the table that can be used for payment (bank + properties + houses/hotels).
        /// </summary>
        public List<Card> GetPayableCards()
        {
            var cards = new List<Card>(Bank);
            foreach (var set in PropertySets)
            {
                cards.AddRange(set.Cards);
            }
            return cards;
        }
    }
}
