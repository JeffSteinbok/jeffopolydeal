using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace JeffopolyDeal.Models
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum CardType
    {
        Money,
        Property,
        PropertyWildcard,
        Rent,
        Action
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ActionType
    {
        PassGo,
        DebtCollector,
        ItsMyBirthday,
        SlyDeal,
        ForceDeal,
        DealBreaker,
        JustSayNo,
        DoubleTheRent,
        House,
        Hotel
    }

    /// <summary>
    /// Represents a single card in the Monopoly Deal deck.
    /// All card types are represented by this one class; the CardType discriminator
    /// determines which fields are relevant.
    /// </summary>
    public class Card
    {
        /// <summary>Unique numeric identifier for this card instance in the deck.</summary>
        public int Id { get; set; }

        /// <summary>Stable string identifier for the card definition (e.g. "brown1", "passgo1").
        /// Used for code references and tests — independent of display name.</summary>
        public string CardId { get; set; } = "";

        public CardType CardType { get; set; }

        /// <summary>Monetary value (shown in corner of card). Used when banked or paying.</summary>
        public int MoneyValue { get; set; }

        /// <summary>Display name for the card (shown in UI). Change freely to re-theme.</summary>
        public string Name { get; set; } = "";

        // -- Property fields --

        /// <summary>Primary color (for Property and PropertyWildcard cards).</summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public PropertyColor? Color { get; set; }

        /// <summary>Secondary color (for dual-color PropertyWildcard cards).</summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public PropertyColor? AltColor { get; set; }

        /// <summary>True if this is the 10-color wildcard (can be any color).</summary>
        public bool IsMulticolorWild { get; set; }

        // -- Rent fields --

        /// <summary>Colors this rent card can charge for (for Rent cards).</summary>
        public List<PropertyColor>? RentColors { get; set; }

        /// <summary>True if this is a wild rent card (any color, one player).</summary>
        public bool IsWildRent { get; set; }

        // -- Action fields --

        /// <summary>The action type (for Action cards).</summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public ActionType? ActionKind { get; set; }

        /// <summary>
        /// The current color assignment for a wildcard property on the board.
        /// Not part of deck definition — set during gameplay.
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public PropertyColor? ActiveColor { get; set; }

        /// <summary>
        /// Whether this card can currently be played for its card effect.
        /// Computed server-side when game state is sent to a player.
        /// </summary>
        public bool IsPlayable { get; set; } = true;
    }
}
