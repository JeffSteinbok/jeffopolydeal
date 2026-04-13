using System.Collections.Generic;
using System.Linq;

namespace JeffopolyDeal.Models
{
    /// <summary>
    /// Represents a player in the game.
    /// </summary>
    public class Player
    {
        /// <summary>Stable identity that survives reconnections. Set once at join.</summary>
        public string PlayerId { get; set; } = "";
        public string ConnectionId { get; set; } = "";
        public string Name { get; set; } = "";

        /// <summary>Cards in the player's hand (hidden from other players).</summary>
        public List<Card> Hand { get; set; } = new();

        /// <summary>Money cards in the player's bank (visible to all).</summary>
        public List<Card> Bank { get; set; } = new();

        /// <summary>Property sets on the table (visible to all).</summary>
        public List<PropertySet> PropertySets { get; set; } = new();

        /// <summary>Multi-color wildcards not yet assigned to any set.</summary>
        public List<Card> UnboundWilds { get; set; } = new();

        /// <summary>Number of completed property sets.</summary>
        public int CompletedSetCount => PropertySets.Count(s => s.IsComplete);

        /// <summary>Number of complete sets of DIFFERENT colors (win condition).</summary>
        public int UniqueCompletedSetCount => PropertySets
            .Where(s => s.IsComplete)
            .Select(s => s.Color)
            .Distinct()
            .Count();

        /// <summary>Total bank value.</summary>
        public int BankTotal => Bank.Sum(c => c.MoneyValue);

        /// <summary>
        /// Gets or creates a property set for the given color.
        /// If all existing sets of this color are full, creates a new one.
        /// </summary>
        public PropertySet GetOrCreatePropertySet(PropertyColor color)
        {
            // Find an incomplete set of this color, or create new
            var set = PropertySets.FirstOrDefault(s => s.Color == color && !s.IsComplete);
            if (set == null)
            {
                set = new PropertySet { Color = color };
                PropertySets.Add(set);
            }
            return set;
        }

        /// <summary>
        /// Creates a new property set with this card (used when receiving cards from others).
        /// Never merges into existing sets — player arranges on their turn.
        /// </summary>
        public PropertySet CreateNewPropertySet(PropertyColor color, Card card)
        {
            card.ActiveColor = color;
            var set = new PropertySet { Color = color };
            set.Cards.Add(card);
            PropertySets.Add(set);
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
