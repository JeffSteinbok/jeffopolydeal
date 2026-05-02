using System;
using System.Collections.Generic;
using System.Linq;
using JeffopolyDeal.Models;

namespace JeffopolyDeal.ISMCTS
{
    // =========================================================================
    // SimulationState.cs — Lightweight, cloneable game state for ISMCTS rollouts
    // =========================================================================
    //
    // The real Game class is tightly coupled to SignalR, async I/O, and locks.
    // None of that belongs in a Monte Carlo simulation that must clone state
    // thousands of times per decision.
    //
    // This file defines a parallel model hierarchy:
    //   SimPropertySet  — a group of property cards of one color
    //   SimPlayer        — a player's hand, bank, and property sets
    //   SimPendingAction — an action waiting for a response (rent, steal, etc.)
    //   SimDeck          — draw pile + discard pile
    //   SimulationState  — the full game snapshot
    //
    // Every class has a Clone() method for deep copying. Cards themselves are
    // treated as immutable value objects — we copy list references but never
    // mutate a Card's fields during simulation (except ActiveColor on wildcards,
    // which we handle by copying into a new Card when needed).
    // =========================================================================

    /// <summary>
    /// Lightweight property set for simulation. Mirrors PropertySet but with
    /// no static ID counter or external dependencies.
    /// </summary>
    public class SimPropertySet
    {
        public PropertyColor Color { get; set; }
        public List<Card> Cards { get; set; } = new();
        public bool HasHouse { get; set; }
        public bool HasHotel { get; set; }

        // --- Computed properties (mirror the real PropertySet) ---

        /// <summary>Number of property cards in this set.</summary>
        public int Size => Cards.Count;

        /// <summary>How many cards are needed to complete this color set.</summary>
        public int RequiredSize => GameConfig.SetSize[Color];

        /// <summary>Whether this set has enough cards to be considered complete.</summary>
        public bool IsComplete => Size >= RequiredSize;

        /// <summary>
        /// Calculates rent using the same rent table as the real game.
        /// Includes house (+3) and hotel (+4) bonuses when the set is complete.
        /// </summary>
        public int CalculateRent()
        {
            var rentTable = GameConfig.RentTable[Color];
            // Clamp to the max index in the rent table
            int propertyCount = Math.Min(Size, rentTable.Length - 1);
            int rent = rentTable[propertyCount];

            // House and hotel bonuses only apply to complete sets
            if (IsComplete)
            {
                if (HasHouse) rent += GameConfig.HouseRentBonus;
                if (HasHotel) rent += GameConfig.HotelRentBonus;
            }

            return rent;
        }

        /// <summary>
        /// Deep clone this property set. Cards are shallow-copied since we treat
        /// them as immutable during simulation.
        /// </summary>
        public SimPropertySet Clone() => new SimPropertySet
        {
            Color = Color,
            Cards = new List<Card>(Cards),  // shallow copy of card references
            HasHouse = HasHouse,
            HasHotel = HasHotel,
        };
    }

    /// <summary>
    /// Lightweight player state for simulation. Contains the same logical
    /// fields as Player but without connection tracking or SignalR concerns.
    /// </summary>
    public class SimPlayer
    {
        /// <summary>
        /// Stable identifier matching the real Player.ConnectionId.
        /// Used to correlate simulation players with real players.
        /// </summary>
        public string PlayerId { get; set; } = "";

        /// <summary>Cards in hand (hidden from other players in the real game).</summary>
        public List<Card> Hand { get; set; } = new();

        /// <summary>Money cards in the bank (visible to all).</summary>
        public List<Card> Bank { get; set; } = new();

        /// <summary>Property sets on the table (visible to all).</summary>
        public List<SimPropertySet> PropertySets { get; set; } = new();

        /// <summary>Multi-color wildcards not yet assigned to a set.</summary>
        public List<Card> UnboundWilds { get; set; } = new();

        // --- Computed properties (mirror the real Player) ---

        /// <summary>Total money value in the bank.</summary>
        public int BankTotal => Bank.Sum(c => c.MoneyValue);

        /// <summary>Number of completed property sets (may include duplicates).</summary>
        public int CompletedSetCount => PropertySets.Count(s => s.IsComplete);

        /// <summary>
        /// Number of completed sets of DIFFERENT colors. This is the win condition:
        /// first player to reach GameConfig.SetsToWin (3) unique completed sets wins.
        /// </summary>
        public int UniqueCompletedSetCount => PropertySets
            .Where(s => s.IsComplete)
            .Select(s => s.Color)
            .Distinct()
            .Count();

        /// <summary>
        /// Gets or creates an incomplete property set for the given color.
        /// If all sets of this color are full, creates a new one.
        /// Mirrors Player.GetOrCreatePropertySet().
        /// </summary>
        public SimPropertySet GetOrCreatePropertySet(PropertyColor color)
        {
            // Prefer the incomplete set with the most cards (fill up before creating new)
            var set = PropertySets
                .Where(s => s.Color == color && !s.IsComplete)
                .OrderByDescending(s => s.Cards.Count)
                .FirstOrDefault();

            if (set == null)
            {
                set = new SimPropertySet { Color = color };
                PropertySets.Add(set);
            }
            return set;
        }

        /// <summary>
        /// All property cards on the table that are NOT part of complete sets.
        /// These are valid targets for Sly Deal and Force Deal.
        /// </summary>
        public List<Card> GetStealableProperties()
        {
            var cards = PropertySets
                .Where(s => !s.IsComplete)
                .SelectMany(s => s.Cards)
                .ToList();
            cards.AddRange(UnboundWilds);
            return cards;
        }

        /// <summary>All completed property sets. Valid targets for Deal Breaker.</summary>
        public List<SimPropertySet> GetCompletePropertySets()
        {
            return PropertySets.Where(s => s.IsComplete).ToList();
        }

        /// <summary>
        /// All cards on the table that can be used for payment: bank cards + property cards.
        /// Mirrors Player.GetPayableCards().
        /// </summary>
        public List<Card> GetPayableCards()
        {
            var cards = new List<Card>(Bank);
            foreach (var set in PropertySets)
                cards.AddRange(set.Cards);
            return cards;
        }

        /// <summary>
        /// Deep clone this player. Hand, bank, and property sets are all deeply copied.
        /// Cards themselves are treated as immutable references.
        /// </summary>
        public SimPlayer Clone() => new SimPlayer
        {
            PlayerId = PlayerId,
            Hand = new List<Card>(Hand),
            Bank = new List<Card>(Bank),
            PropertySets = PropertySets.Select(s => s.Clone()).ToList(),
            UnboundWilds = new List<Card>(UnboundWilds),
        };
    }

    /// <summary>
    /// Tracks a pending action in simulation — e.g., rent that opponents must pay,
    /// or a steal that the target must respond to. Mirrors the real PendingAction
    /// but with only the fields needed for simulation logic.
    /// </summary>
    public class SimPendingAction
    {
        /// <summary>What kind of action is pending (PayRent, RespondToSlyDeal, etc.).</summary>
        public PendingActionType Type { get; set; }

        /// <summary>Index of the player who initiated this action.</summary>
        public int SourcePlayerIndex { get; set; }

        /// <summary>Indices of players who still need to respond.</summary>
        public List<int> TargetPlayerIndices { get; set; } = new();

        /// <summary>Amount to pay (for rent, debt collector, birthday).</summary>
        public int Amount { get; set; }

        /// <summary>Card ID being stolen (for Sly Deal / Force Deal).</summary>
        public int? TargetCardId { get; set; }

        /// <summary>Card ID being offered in exchange (for Force Deal).</summary>
        public int? OfferedCardId { get; set; }

        /// <summary>Color of the set being taken (for Deal Breaker).</summary>
        public PropertyColor? TargetSetColor { get; set; }

        /// <summary>
        /// Deep clone. Target indices list is copied so mutations don't bleed
        /// across cloned states.
        /// </summary>
        public SimPendingAction Clone() => new SimPendingAction
        {
            Type = Type,
            SourcePlayerIndex = SourcePlayerIndex,
            TargetPlayerIndices = new List<int>(TargetPlayerIndices),
            Amount = Amount,
            TargetCardId = TargetCardId,
            OfferedCardId = OfferedCardId,
            TargetSetColor = TargetSetColor,
        };
    }

    /// <summary>
    /// The simulation's draw and discard piles. Cards are drawn from the end
    /// of the DrawPile list (top of deck = last element).
    /// </summary>
    public class SimDeck
    {
        /// <summary>Cards available to draw. Top of deck = last element.</summary>
        public List<Card> DrawPile { get; set; } = new();

        /// <summary>Discarded cards. Not reshuffled in simulation for simplicity.</summary>
        public List<Card> DiscardPile { get; set; } = new();

        /// <summary>Number of cards remaining in the draw pile.</summary>
        public int DrawPileCount => DrawPile.Count;

        /// <summary>
        /// Draw up to 'count' cards from the top of the deck.
        /// If the deck runs out, reshuffles the discard pile into the draw pile.
        /// Returns however many cards were actually drawn (may be less than requested
        /// if both piles are empty).
        /// </summary>
        public List<Card> Draw(int count)
        {
            var drawn = new List<Card>();
            for (int i = 0; i < count; i++)
            {
                // If draw pile is empty, reshuffle discards
                if (DrawPile.Count == 0)
                {
                    if (DiscardPile.Count == 0) break; // no cards left anywhere
                    DrawPile.AddRange(DiscardPile);
                    DiscardPile.Clear();
                    // Shuffle using Fisher-Yates
                    var rng = new Random();
                    for (int j = DrawPile.Count - 1; j > 0; j--)
                    {
                        int k = rng.Next(j + 1);
                        (DrawPile[j], DrawPile[k]) = (DrawPile[k], DrawPile[j]);
                    }
                }

                // Draw from top (last element)
                var card = DrawPile[^1];
                DrawPile.RemoveAt(DrawPile.Count - 1);
                drawn.Add(card);
            }
            return drawn;
        }

        /// <summary>Add a card to the discard pile.</summary>
        public void Discard(Card card) => DiscardPile.Add(card);

        /// <summary>
        /// Deep clone. Both piles are shallow-copied (card refs are immutable).
        /// </summary>
        public SimDeck Clone() => new SimDeck
        {
            DrawPile = new List<Card>(DrawPile),
            DiscardPile = new List<Card>(DiscardPile),
        };
    }

    /// <summary>
    /// Phases the simulation can be in. Simplified from the real GamePhase:
    /// we don't need Lobby or Draw since simulation starts mid-game.
    /// </summary>
    public enum SimPhase
    {
        /// <summary>The current player is choosing cards to play.</summary>
        Playing,

        /// <summary>Waiting for a target player to respond to an action.</summary>
        AwaitingResponse,

        /// <summary>The game is over — someone has won.</summary>
        GameOver
    }

    /// <summary>
    /// Complete simulation state snapshot. This is the top-level object that
    /// gets cloned at each ISMCTS node. Contains everything needed to simulate
    /// the game forward from this point.
    /// </summary>
    public class SimulationState
    {
        /// <summary>All players in turn order. Index 0 goes first.</summary>
        public List<SimPlayer> Players { get; set; } = new();

        /// <summary>The draw and discard piles.</summary>
        public SimDeck Deck { get; set; } = new();

        /// <summary>Index into Players of whose turn it currently is.</summary>
        public int CurrentPlayerIndex { get; set; }

        /// <summary>
        /// How many card plays the current player has remaining this turn.
        /// Starts at MaxPlaysPerTurn (3) and decrements with each play.
        /// DoubleTheRent consumes an extra play per DTR card attached.
        /// </summary>
        public int PlaysRemaining { get; set; }

        /// <summary>Current phase of the simulation.</summary>
        public SimPhase Phase { get; set; }

        /// <summary>
        /// The pending action waiting for a response, if any.
        /// Null when Phase != AwaitingResponse.
        /// </summary>
        public SimPendingAction? PendingAction { get; set; }

        /// <summary>
        /// Index of the winning player, or -1 if no winner yet.
        /// Set when Phase transitions to GameOver.
        /// </summary>
        public int WinnerIndex { get; set; } = -1;

        // --- Convenience accessors ---

        /// <summary>The player whose turn it currently is.</summary>
        public SimPlayer CurrentPlayer => Players[CurrentPlayerIndex];

        /// <summary>Total number of players in the game.</summary>
        public int PlayerCount => Players.Count;

        /// <summary>
        /// Deep clone the entire simulation state. This is called frequently
        /// during ISMCTS (once per iteration for determinization + rollout),
        /// so it should be as fast as possible.
        /// </summary>
        public SimulationState Clone() => new SimulationState
        {
            Players = Players.Select(p => p.Clone()).ToList(),
            Deck = Deck.Clone(),
            CurrentPlayerIndex = CurrentPlayerIndex,
            PlaysRemaining = PlaysRemaining,
            Phase = Phase,
            PendingAction = PendingAction?.Clone(),
            WinnerIndex = WinnerIndex,
        };

        // =====================================================================
        // Factory: create a SimulationState from the real game objects
        // =====================================================================

        /// <summary>
        /// Snapshot the real game state into a SimulationState.
        /// 
        /// This captures all PUBLIC information plus the bot's own hand.
        /// Opponent hands are NOT populated here — the Determinizer fills
        /// those in by sampling from the unknown card pool.
        /// 
        /// Parameters:
        ///   bot           — the bot player (we know their full hand)
        ///   allPlayers    — all players in the game, in turn order
        ///   deck          — the real deck (we snapshot draw/discard pile sizes)
        ///   playsRemaining — how many plays the bot has left this turn
        ///   pendingAction  — any pending action, or null
        /// </summary>
        public static SimulationState FromGame(
            Player bot,
            List<Player> allPlayers,
            Deck deck,
            int playsRemaining,
            PendingAction? pendingAction = null)
        {
            var state = new SimulationState
            {
                PlaysRemaining = playsRemaining,
                Phase = pendingAction != null ? SimPhase.AwaitingResponse : SimPhase.Playing,
            };

            // Convert each real player to a SimPlayer
            int botIndex = -1;
            for (int i = 0; i < allPlayers.Count; i++)
            {
                var real = allPlayers[i];
                var sim = new SimPlayer
                {
                    PlayerId = real.ConnectionId,
                    // Only populate the bot's hand — opponents' hands are unknown
                    // and will be filled by the Determinizer
                    Hand = real.ConnectionId == bot.ConnectionId
                        ? new List<Card>(real.Hand)
                        : new List<Card>(),
                    Bank = new List<Card>(real.Bank),
                    PropertySets = real.PropertySets.Select(ps => new SimPropertySet
                    {
                        Color = ps.Color,
                        Cards = new List<Card>(ps.Cards),
                        HasHouse = ps.HasHouse,
                        HasHotel = ps.HasHotel,
                    }).ToList(),
                    UnboundWilds = new List<Card>(real.UnboundWilds),
                };
                state.Players.Add(sim);

                if (real.ConnectionId == bot.ConnectionId)
                    botIndex = i;
            }

            // The bot is always the current player when we're deciding what to play
            state.CurrentPlayerIndex = botIndex >= 0 ? botIndex : 0;

            // Snapshot the deck — we only know sizes, not contents.
            // The Determinizer will populate the actual cards.
            // For now, copy the discard pile (it's public) and leave draw pile
            // to be filled by determinization.
            state.Deck = new SimDeck
            {
                DrawPile = new List<Card>(), // filled by Determinizer
                DiscardPile = deck.GetDiscardPileSnapshot(),
            };

            // Convert pending action if present
            if (pendingAction != null)
            {
                state.PendingAction = ConvertPendingAction(pendingAction, allPlayers);
            }

            return state;
        }

        /// <summary>
        /// Convert a real PendingAction (which uses ConnectionId strings) to a
        /// SimPendingAction (which uses player indices for fast lookup).
        /// </summary>
        private static SimPendingAction ConvertPendingAction(
            PendingAction real, List<Player> allPlayers)
        {
            // Helper to find player index by ConnectionId
            int IndexOf(string connectionId) =>
                allPlayers.FindIndex(p => p.ConnectionId == connectionId);

            return new SimPendingAction
            {
                Type = real.Type,
                SourcePlayerIndex = IndexOf(real.SourcePlayerId),
                TargetPlayerIndices = real.TargetPlayerIds
                    .Select(id => IndexOf(id))
                    .Where(i => i >= 0)  // filter out any not-found (shouldn't happen)
                    .ToList(),
                Amount = real.Amount,
                TargetCardId = real.TargetCardId,
                OfferedCardId = real.OfferedCardId,
                TargetSetColor = real.TargetSetColor,
            };
        }
    }
}
