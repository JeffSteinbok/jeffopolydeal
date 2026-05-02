using System.Collections.Generic;
using System.Linq;
using JeffopolyDeal.Models;

namespace JeffopolyDeal.ISMCTS
{
    // =========================================================================
    // MoveGenerator.cs — Legal Move Enumeration for ISMCTS
    // =========================================================================
    //
    // Given a simulation state and a player index, generates ALL legal moves
    // the player can make. This is used in two places:
    //
    //   1. ISMCTS expansion: to know which child nodes to create
    //   2. Rollout policy: to know which moves are available for selection
    //
    // A "move" represents a single card play. The move space includes:
    //   - Playing each hand card for its intended effect
    //   - Playing each hand card as money (banking it)
    //   - Playing rent with each valid color + optional DoubleTheRent combos
    //   - Playing targeted actions against each valid target
    //   - Ending the turn early (choosing not to play more cards)
    //
    // Cards that should NOT be played proactively (Just Say No, DoubleTheRent)
    // are excluded — JSN is reactive-only and DTR is bundled with rent moves.
    //
    // Performance note: this is called many times during MCTS, so we avoid
    // allocating excessively. In practice the move count per position is small
    // (typically 5-20 moves) so this isn't a bottleneck.
    // =========================================================================

    /// <summary>
    /// Generates all legal moves for a player in a given simulation state.
    /// </summary>
    public static class MoveGenerator
    {
        /// <summary>
        /// Enumerate all legal moves for the specified player.
        /// 
        /// The result always includes an "end turn" move (the player can
        /// choose to stop playing even if they have plays remaining).
        /// 
        /// Returns an empty list only if the player has no hand cards AND
        /// no plays remaining (which shouldn't happen during normal play).
        /// </summary>
        public static List<SimMove> GetLegalMoves(SimulationState state, int playerIndex)
        {
            var moves = new List<SimMove>();
            var player = state.Players[playerIndex];

            // Can always choose to end the turn
            moves.Add(new SimMove { IsEndTurn = true });

            // No plays left or no cards in hand — can only end turn
            if (state.PlaysRemaining <= 0 || player.Hand.Count == 0)
                return moves;

            // Enumerate moves for each card in hand
            foreach (var card in player.Hand)
            {
                // JSN and DTR are reactive-only — cannot be played for their effect.
                // But they CAN still be banked as money (all cards can be banked).
                if (card.ActionKind == ActionType.JustSayNo ||
                    card.ActionKind == ActionType.DoubleTheRent)
                {
                    moves.Add(new SimMove { Card = card, PlayAsMoney = true });
                    continue; // no effect-based moves for these
                }

                // Every other card can be played as money
                moves.Add(new SimMove { Card = card, PlayAsMoney = true });

                // Generate card-specific moves for playing the card for its effect
                switch (card.CardType)
                {
                    case CardType.Money:
                        // Money cards can ONLY be banked — already added above
                        break;

                    case CardType.Property:
                        GeneratePropertyMoves(moves, card);
                        break;

                    case CardType.PropertyWildcard:
                        GenerateWildcardMoves(moves, player, card);
                        break;

                    case CardType.Rent:
                        GenerateRentMoves(moves, state, playerIndex, card);
                        break;

                    case CardType.Action:
                        GenerateActionMoves(moves, state, playerIndex, card);
                        break;
                }
            }

            return moves;
        }

        // =====================================================================
        // Card-type-specific move generators
        // =====================================================================

        /// <summary>
        /// Property cards have exactly one move: play as a property onto the board.
        /// The color is determined by the card itself.
        /// </summary>
        private static void GeneratePropertyMoves(List<SimMove> moves, Card card)
        {
            moves.Add(new SimMove { Card = card, PlayAsMoney = false });
        }

        /// <summary>
        /// Wildcard properties can be played as one of their valid colors.
        /// 
        /// Multi-color wilds (the rainbow 10-color card) can be ANY color,
        /// but we limit choices to colors the player has or could benefit from.
        /// 
        /// Dual-color wilds offer exactly two choices (primary and alt color).
        /// </summary>
        private static void GenerateWildcardMoves(List<SimMove> moves, SimPlayer player, Card card)
        {
            if (card.IsMulticolorWild)
            {
                // Multi-color wild: generate a move for each color that the player
                // has at least one card in (or any incomplete set).
                // To keep the branching factor reasonable, we only consider colors
                // where the player already has cards, plus any color with the smallest
                // set size (Brown, DarkBlue, Utility = 2 cards needed).
                var candidateColors = new HashSet<PropertyColor>();

                // Colors the player already has property cards in
                foreach (var set in player.PropertySets.Where(s => !s.IsComplete))
                    candidateColors.Add(set.Color);

                // If no existing sets, offer the cheapest colors to complete
                if (candidateColors.Count == 0)
                {
                    candidateColors.Add(PropertyColor.Brown);
                    candidateColors.Add(PropertyColor.DarkBlue);
                    candidateColors.Add(PropertyColor.Utility);
                }

                foreach (var color in candidateColors)
                {
                    moves.Add(new SimMove
                    {
                        Card = card,
                        PlayAsMoney = false,
                        WildcardColor = color,
                    });
                }
            }
            else
            {
                // Dual-color wild: can be played as either color
                if (card.Color.HasValue)
                {
                    moves.Add(new SimMove
                    {
                        Card = card,
                        PlayAsMoney = false,
                        WildcardColor = card.Color.Value,
                    });
                }
                if (card.AltColor.HasValue)
                {
                    moves.Add(new SimMove
                    {
                        Card = card,
                        PlayAsMoney = false,
                        WildcardColor = card.AltColor.Value,
                    });
                }
            }
        }

        /// <summary>
        /// Rent cards generate moves for each valid color the player can charge.
        /// 
        /// Standard rent: charges ALL opponents for a color matching the card.
        /// Wild rent: charges ONE target player for ANY color the player has.
        /// 
        /// For each rent move, we also check if the player has DoubleTheRent
        /// cards and enough plays remaining to attach them (compound move).
        /// </summary>
        private static void GenerateRentMoves(
            List<SimMove> moves, SimulationState state, int playerIndex, Card card)
        {
            var player = state.Players[playerIndex];

            // Determine valid rent colors
            List<PropertyColor> validColors;

            if (card.IsWildRent)
            {
                // Wild rent: can charge for any color where player has properties
                validColors = player.PropertySets
                    .Where(s => s.Cards.Count > 0)
                    .Select(s => s.Color)
                    .Distinct()
                    .ToList();
            }
            else if (card.RentColors != null)
            {
                // Standard rent: limited to the card's colors
                validColors = card.RentColors
                    .Where(c => player.PropertySets.Any(s => s.Color == c && s.Cards.Count > 0))
                    .ToList();
            }
            else
            {
                return; // no valid colors
            }

            if (validColors.Count == 0) return;

            // Find DoubleTheRent cards in hand for compound moves
            var dtrCards = player.Hand
                .Where(c => c.ActionKind == ActionType.DoubleTheRent)
                .ToList();

            foreach (var color in validColors)
            {
                if (card.IsWildRent)
                {
                    // Wild rent targets ONE player — generate a move per opponent
                    for (int i = 0; i < state.PlayerCount; i++)
                    {
                        if (i == playerIndex) continue;

                        // Base rent move (no DTR)
                        moves.Add(new SimMove
                        {
                            Card = card,
                            PlayAsMoney = false,
                            RentColor = color,
                            TargetPlayerIndex = i,
                        });

                        // Rent + DTR combos (only if enough plays remaining)
                        AddDTRCombos(moves, card, color, i, dtrCards, state.PlaysRemaining);
                    }
                }
                else
                {
                    // Standard rent hits all opponents — no target needed
                    moves.Add(new SimMove
                    {
                        Card = card,
                        PlayAsMoney = false,
                        RentColor = color,
                    });

                    // Rent + DTR combos
                    AddDTRCombos(moves, card, color, -1, dtrCards, state.PlaysRemaining);
                }
            }
        }

        /// <summary>
        /// Add DoubleTheRent combo moves for a rent play. Each DTR card costs
        /// one additional play, so we check PlaysRemaining.
        /// 
        /// Possible combos: Rent + 1 DTR, Rent + 2 DTR (if two DTR cards available).
        /// </summary>
        private static void AddDTRCombos(
            List<SimMove> moves, Card rentCard, PropertyColor color,
            int targetPlayerIndex, List<Card> dtrCards, int playsRemaining)
        {
            // Need at least 2 plays remaining to attach 1 DTR (1 for rent + 1 for DTR)
            if (dtrCards.Count >= 1 && playsRemaining >= 2)
            {
                moves.Add(new SimMove
                {
                    Card = rentCard,
                    PlayAsMoney = false,
                    RentColor = color,
                    TargetPlayerIndex = targetPlayerIndex,
                    DoubleRentCardIds = new List<int> { dtrCards[0].Id },
                });

                // Double DTR: need 3 plays remaining and 2 DTR cards
                if (dtrCards.Count >= 2 && playsRemaining >= 3)
                {
                    moves.Add(new SimMove
                    {
                        Card = rentCard,
                        PlayAsMoney = false,
                        RentColor = color,
                        TargetPlayerIndex = targetPlayerIndex,
                        DoubleRentCardIds = new List<int> { dtrCards[0].Id, dtrCards[1].Id },
                    });
                }
            }
        }

        /// <summary>
        /// Generate moves for action cards. Each action type has different
        /// targeting requirements.
        /// </summary>
        private static void GenerateActionMoves(
            List<SimMove> moves, SimulationState state, int playerIndex, Card card)
        {
            var player = state.Players[playerIndex];

            switch (card.ActionKind)
            {
                case ActionType.PassGo:
                    // No targeting needed — just play it
                    moves.Add(new SimMove { Card = card, PlayAsMoney = false });
                    break;

                case ActionType.DebtCollector:
                    // Target one opponent
                    for (int i = 0; i < state.PlayerCount; i++)
                    {
                        if (i == playerIndex) continue;
                        moves.Add(new SimMove
                        {
                            Card = card,
                            PlayAsMoney = false,
                            TargetPlayerIndex = i,
                        });
                    }
                    break;

                case ActionType.ItsMyBirthday:
                    // Targets all opponents automatically — no choice needed
                    moves.Add(new SimMove { Card = card, PlayAsMoney = false });
                    break;

                case ActionType.SlyDeal:
                    GenerateSlyDealMoves(moves, state, playerIndex, card);
                    break;

                case ActionType.ForceDeal:
                    GenerateForceDealMoves(moves, state, playerIndex, card);
                    break;

                case ActionType.DealBreaker:
                    GenerateDealBreakerMoves(moves, state, playerIndex, card);
                    break;

                case ActionType.House:
                    GenerateHouseHotelMoves(moves, state, playerIndex, card, isHotel: false);
                    break;

                case ActionType.Hotel:
                    GenerateHouseHotelMoves(moves, state, playerIndex, card, isHotel: true);
                    break;

                // JSN and DTR are handled elsewhere (reactive / compound)
                default:
                    break;
            }
        }

        /// <summary>
        /// Sly Deal: steal one property from an opponent's INCOMPLETE set.
        /// Generate one move per stealable card per opponent.
        /// </summary>
        private static void GenerateSlyDealMoves(
            List<SimMove> moves, SimulationState state, int playerIndex, Card card)
        {
            for (int i = 0; i < state.PlayerCount; i++)
            {
                if (i == playerIndex) continue;
                var stealable = state.Players[i].GetStealableProperties();
                foreach (var target in stealable)
                {
                    moves.Add(new SimMove
                    {
                        Card = card,
                        PlayAsMoney = false,
                        TargetPlayerIndex = i,
                        TargetCardId = target.Id,
                    });
                }
            }
        }

        /// <summary>
        /// Force Deal: swap one of your stealable properties for one of theirs.
        /// Generate one move per (your card, their card) pair.
        /// To keep branching factor manageable, we limit to the 3 lowest-value
        /// cards we could offer.
        /// </summary>
        private static void GenerateForceDealMoves(
            List<SimMove> moves, SimulationState state, int playerIndex, Card card)
        {
            var player = state.Players[playerIndex];
            var myStealable = player.GetStealableProperties();
            if (myStealable.Count == 0) return;

            // Limit our offered cards to the 3 lowest value (to reduce branching)
            var myOffers = myStealable.OrderBy(c => c.MoneyValue).Take(3).ToList();

            for (int i = 0; i < state.PlayerCount; i++)
            {
                if (i == playerIndex) continue;
                var theirStealable = state.Players[i].GetStealableProperties();

                foreach (var theirCard in theirStealable)
                {
                    foreach (var myCard in myOffers)
                    {
                        moves.Add(new SimMove
                        {
                            Card = card,
                            PlayAsMoney = false,
                            TargetPlayerIndex = i,
                            TargetCardId = theirCard.Id,
                            OfferedCardId = myCard.Id,
                        });
                    }
                }
            }
        }

        /// <summary>
        /// Deal Breaker: steal an entire COMPLETE set from an opponent.
        /// Generate one move per complete set per opponent.
        /// </summary>
        private static void GenerateDealBreakerMoves(
            List<SimMove> moves, SimulationState state, int playerIndex, Card card)
        {
            for (int i = 0; i < state.PlayerCount; i++)
            {
                if (i == playerIndex) continue;
                var completeSets = state.Players[i].GetCompletePropertySets();
                foreach (var set in completeSets)
                {
                    moves.Add(new SimMove
                    {
                        Card = card,
                        PlayAsMoney = false,
                        TargetPlayerIndex = i,
                        TargetSetColor = set.Color,
                    });
                }
            }
        }

        /// <summary>
        /// House/Hotel: add to one of the player's COMPLETE sets (not Railroad/Utility).
        /// House: set must not already have a house.
        /// Hotel: set must have a house but not a hotel.
        /// </summary>
        private static void GenerateHouseHotelMoves(
            List<SimMove> moves, SimulationState state, int playerIndex, Card card, bool isHotel)
        {
            var player = state.Players[playerIndex];

            foreach (var set in player.PropertySets)
            {
                if (!set.IsComplete) continue;
                // Can't add house/hotel to railroads or utilities
                if (set.Color == PropertyColor.Railroad || set.Color == PropertyColor.Utility)
                    continue;

                if (isHotel)
                {
                    // Hotel requires existing house and no existing hotel
                    if (set.HasHouse && !set.HasHotel)
                    {
                        moves.Add(new SimMove
                        {
                            Card = card,
                            PlayAsMoney = false,
                            TargetSetColor = set.Color,
                        });
                    }
                }
                else
                {
                    // House requires no existing house
                    if (!set.HasHouse)
                    {
                        moves.Add(new SimMove
                        {
                            Card = card,
                            PlayAsMoney = false,
                            TargetSetColor = set.Color,
                        });
                    }
                }
            }
        }
    }
}
