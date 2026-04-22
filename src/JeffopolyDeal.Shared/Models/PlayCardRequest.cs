using JeffopolyDeal.Models;

namespace JeffopolyDeal
{
    /// <summary>
    /// Request data for playing a card.
    /// </summary>
    public class PlayCardRequest
    {
        public bool PlayAsMoney { get; set; }
        public PropertyColor? WildcardColor { get; set; }
        public PropertyColor? RentColor { get; set; }
        public string? TargetPlayerId { get; set; }
        public int? TargetCardId { get; set; }
        public int? OfferedCardId { get; set; }
        public PropertyColor? TargetSetColor { get; set; }
        public List<int>? DoubleRentCardIds { get; set; }
    }

    /// <summary>
    /// Response data from a player being targeted by an action.
    /// </summary>
    public class ActionResponse
    {
        public bool PlayJustSayNo { get; set; }
        public List<int>? PaymentCardIds { get; set; }
    }
}
