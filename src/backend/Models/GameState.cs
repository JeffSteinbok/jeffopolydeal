using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Collections.Generic;

namespace JeffopolyDeal.Models
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum GamePhase
    {
        /// <summary>Waiting in lobby for players to join.</summary>
        Lobby,
        /// <summary>Active player must draw cards.</summary>
        Draw,
        /// <summary>Active player can play up to 3 cards.</summary>
        Play,
        /// <summary>Waiting for a targeted player to respond (pay rent, Just Say No, etc.).</summary>
        AwaitingResponse,
        /// <summary>Active player must discard down to 7 cards.</summary>
        Discard,
        /// <summary>Game is over — someone won.</summary>
        GameOver
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum PendingActionType
    {
        PayRent,
        PayDebtCollector,
        PayBirthday,
        RespondToSlyDeal,
        RespondToForceDeal,
        RespondToDealBreaker,
        JustSayNoChain
    }

    /// <summary>
    /// Tracks pending actions that require responses from other players.
    /// </summary>
    public class PendingAction
    {
        public PendingActionType Type { get; set; }

        /// <summary>The player who initiated the action.</summary>
        public string SourcePlayerId { get; set; } = "";

        /// <summary>Display name of the source player (for UI).</summary>
        public string SourcePlayerName { get; set; } = "";

        /// <summary>Players who still need to respond.</summary>
        public List<string> TargetPlayerIds { get; set; } = new();

        /// <summary>Amount to pay (for rent/debt/birthday).</summary>
        public int Amount { get; set; }

        /// <summary>Card being stolen (for Sly Deal / Force Deal).</summary>
        public int? TargetCardId { get; set; }

        /// <summary>Display name of the card being stolen (for UI).</summary>
        public string? TargetCardName { get; set; }

        /// <summary>Card being offered in exchange (for Force Deal).</summary>
        public int? OfferedCardId { get; set; }

        /// <summary>Display name of the card being offered (for UI).</summary>
        public string? OfferedCardName { get; set; }

        /// <summary>Property set color being taken (for Deal Breaker).</summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public PropertyColor? TargetSetColor { get; set; }

        /// <summary>
        /// The player currently being asked to respond in a Just Say No chain.
        /// </summary>
        public string? JustSayNoResponderId { get; set; }

        /// <summary>The original action source player before any Just Say No chain.</summary>
        public string? OriginalSourcePlayerId { get; set; }

        /// <summary>The original action type before the Just Say No chain changed it.</summary>
        public PendingActionType? OriginalActionType { get; set; }

        /// <summary>The original target player IDs before the Just Say No chain changed them.</summary>
        public List<string>? OriginalTargetPlayerIds { get; set; }
    }

    /// <summary>
    /// Represents a single player action for the activity log.
    /// </summary>
    public class GameAction
    {
        public string PlayerName { get; set; } = "";
        public string Text { get; set; } = "";
    }

    /// <summary>
    /// Complete snapshot of game state, sent to clients.
    /// </summary>
    public class GameState
    {
        public GamePhase Phase { get; set; } = GamePhase.Lobby;
        public string GameCode { get; set; } = "";
        public List<PlayerState> Players { get; set; } = new();

        /// <summary>Index into Players for whose turn it is.</summary>
        public int CurrentPlayerIndex { get; set; }

        /// <summary>Number of cards played so far this turn.</summary>
        public int PlaysUsed { get; set; }

        public int DrawPileCount { get; set; }
        public int DiscardPileCount { get; set; }
        public Card? TopDiscard { get; set; }

        public PendingAction? PendingAction { get; set; }

        public string? WinnerId { get; set; }
        public string? WinnerName { get; set; }

        /// <summary>Error message for the last payment attempt (if rejected).</summary>
        public string? PaymentError { get; set; }

        /// <summary>Recent player actions (newest last), up to 5 entries.</summary>
        public List<GameAction> RecentActions { get; set; } = new();
    }

    /// <summary>
    /// Per-player state as seen by a specific client.
    /// Hand is only populated for the requesting player.
    /// </summary>
    public class PlayerState
    {
        public string PlayerId { get; set; } = "";
        public string ConnectionId { get; set; } = "";
        public string Name { get; set; } = "";
        public int HandCount { get; set; }

        /// <summary>Only populated for the requesting player.</summary>
        public List<Card>? Hand { get; set; }

        public List<Card> Bank { get; set; } = new();
        public List<PropertySetState> PropertySets { get; set; } = new();
        public List<Card> UnboundWilds { get; set; } = new();
        public int CompletedSetCount { get; set; }
        public int UniqueCompletedSetCount { get; set; }
    }

    public class PropertySetState
    {
        public int SetId { get; set; }
        [JsonConverter(typeof(StringEnumConverter))]
        public PropertyColor Color { get; set; }
        public List<Card> Cards { get; set; } = new();
        public bool IsComplete { get; set; }
        public bool HasHouse { get; set; }
        public bool HasHotel { get; set; }
        public int Rent { get; set; }
        public int RequiredSize { get; set; }
    }
}
