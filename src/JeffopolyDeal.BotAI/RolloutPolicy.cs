using System;
using System.Collections.Generic;
using System.Linq;
using JeffopolyDeal.Models;

namespace JeffopolyDeal.ISMCTS
{
    // =========================================================================
    // RolloutPolicy.cs — Fast playout strategy for ISMCTS rollouts
    // =========================================================================
    //
    // During the rollout phase of MCTS, we need a fast heuristic to simulate
    // the rest of the game. Pure random play would work but produces low-quality
    // signals (random play doesn't resemble real play, so win/loss stats from
    // random rollouts are noisy).
    //
    // Instead, we reuse the existing CardEvaluator heuristic — the same scoring
    // logic that the old SmartBotAI used. This gives us rollouts that play
    // reasonably well without the computational cost of tree search.
    //
    // The rollout policy also handles response decisions (paying rent, playing
    // JSN) using the same heuristic logic from the existing PaymentSolver and
    // SmartBotAI.ShouldPlayJSN-style logic.
    // =========================================================================

    /// <summary>
    /// Heuristic-based policy for fast ISMCTS rollouts. Uses the existing
    /// CardEvaluator scoring to pick moves, and heuristic logic for responses.
    /// 
    /// All methods are static and stateless — they only depend on the
    /// SimulationState passed to them.
    /// </summary>
    public static class RolloutPolicy
    {
        private static readonly Random _rng = new();

        /// <summary>
        /// Target bank balance for rent protection. Mirrors the constant
        /// in CardEvaluator — the first RentBufferTarget units of money
        /// provide critical defense against rent charges.
        /// </summary>
        private const int RentBufferTarget = 5;

        // =====================================================================
        // Move selection — used during the "play cards" phase
        // =====================================================================

        /// <summary>
        /// Pick a move for the specified player during a rollout simulation.
        /// 
        /// Strategy:
        ///   1. Get all legal moves from MoveGenerator
        ///   2. Score each move using CardEvaluator-based heuristics
        ///   3. Pick the highest-scoring move (with small randomness for variety)
        ///   4. If all moves score poorly, end the turn
        /// 
        /// This mirrors the old SmartBotAI.PlayTurn() logic but operates on
        /// SimulationState instead of real game objects.
        /// </summary>
        public static SimMove PickMove(SimulationState state, int playerIndex,
            BotPersonality? personality = null)
        {
            var moves = MoveGenerator.GetLegalMoves(state, playerIndex);

            // If only "end turn" is available, end the turn
            if (moves.Count <= 1) return moves[0];

            // Score each non-end-turn move
            var scored = new List<(SimMove Move, int Score)>();
            var player = state.Players[playerIndex];

            foreach (var move in moves)
            {
                if (move.IsEndTurn) continue;

                int? score = ScoreMove(state, playerIndex, move, personality);
                if (score.HasValue)
                    scored.Add((move, score.Value));
            }

            // If nothing scored well, end the turn
            if (scored.Count == 0)
                return new SimMove { IsEndTurn = true };

            // Sort by score descending
            scored.Sort((a, b) => b.Score.CompareTo(a.Score));

            // Pick from the top tier (within 10% of best) for some variety
            int bestScore = scored[0].Score;
            var topTier = scored.Where(s => s.Score >= bestScore * 0.9).ToList();
            var pick = topTier.Count > 1 ? topTier[_rng.Next(topTier.Count)] : topTier[0];

            return pick.Move;
        }

        /// <summary>
        /// Score a single move using heuristics based on CardEvaluator logic.
        /// Returns null if the move shouldn't be played.
        /// 
        /// This reconstructs the scoring from CardEvaluator.PlayScore() and
        /// SmartBotAI.BuildRequest() but operates on SimPlayer/SimulationState.
        /// 
        /// Defensive awareness: When bank is below the rent buffer threshold,
        /// banking money scores higher and non-completing properties score lower.
        /// This prevents the rollout from over-investing in exposed properties.
        /// 
        /// Personality awareness: When provided, uses personality weights for
        /// attack/steal/property scoring and rent buffer target.
        /// </summary>
        private static int? ScoreMove(SimulationState state, int playerIndex, SimMove move,
            BotPersonality? personality = null)
        {
            if (move.Card == null) return null;
            var player = state.Players[playerIndex];
            var card = move.Card;

            int rentBufferTarget = personality?.RentBufferTarget ?? RentBufferTarget;
            double propertyWeight = personality?.PropertyWeight ?? 1.0;
            double setCompletionWeight = personality?.SetCompletionWeight ?? 1.0;
            double attackWeight = personality?.AttackWeight ?? 1.0;
            double stealWeight = personality?.StealWeight ?? 1.0;

            int bankTotal = player.BankTotal;
            bool bankIsLow = bankTotal < rentBufferTarget;

            // Estimate max rent any opponent could charge
            int maxOpponentRent = EstimateMaxRentSim(state, playerIndex);

            // Banking as money: check ShouldBankCard first, then score if allowed
            if (move.PlayAsMoney)
            {
                // Use the shared banking decision helper
                if (!CardEvaluator.ShouldBankCard(card, player.Hand.Count, state.PlaysRemaining))
                    return null; // don't bank this card

                if (bankIsLow)
                {
                    int deficit = rentBufferTarget - bankTotal;
                    return 20 + Math.Min(25, deficit * 5);
                }
                return 20;
            }

            switch (card.CardType)
            {
                case CardType.Money:
                    if (bankIsLow)
                    {
                        int deficit = RentBufferTarget - bankTotal;
                        return 20 + Math.Min(25, deficit * 5);
                    }
                    return 20;

                case CardType.Property:
                {
                    int propScore = (int)(30 * propertyWeight);
                    var targetSet = player.PropertySets.FirstOrDefault(s =>
                        s.Color == card.Color && !s.IsComplete);
                    if (targetSet != null && targetSet.Size >= targetSet.RequiredSize - 1)
                    {
                        propScore += (int)(40 * setCompletionWeight); // this card completes a set!
                    }
                    else if (bankIsLow && maxOpponentRent > bankTotal)
                    {
                        // Non-completing property with thin bank: exposed to rent
                        propScore = 15;
                    }
                    return propScore;
                }

                case CardType.PropertyWildcard:
                {
                    bool completesASet = player.PropertySets.Any(s =>
                        !s.IsComplete && s.Size >= s.RequiredSize - 1);
                    if (completesASet) return (int)(70 * setCompletionWeight);
                    if (bankIsLow && maxOpponentRent > bankTotal) return 15;
                    return (int)(35 * propertyWeight);
                }

                case CardType.Rent:
                {
                    // Score rent by how much money we'd collect
                    int rentAmount = GetRentAmount(player, card, move.RentColor);
                    if (rentAmount == 0) return 10;
                    int score = (int)((50 + rentAmount * 5) * attackWeight);
                    // Bonus for DoubleTheRent combos
                    if (move.DoubleRentCardIds != null)
                        score += move.DoubleRentCardIds.Count * 30;
                    return score;
                }

                case CardType.Action:
                    return ScoreAction(state, playerIndex, card, move, personality);
            }

            return 10;
        }

        /// <summary>
        /// Score an action card. Logic mirrors CardEvaluator.ScoreAction() but
        /// adapted for SimulationState. Uses personality weights when provided.
        /// </summary>
        private static int? ScoreAction(
            SimulationState state, int playerIndex, Card card, SimMove move,
            BotPersonality? personality = null)
        {
            var player = state.Players[playerIndex];
            double attackWeight = personality?.AttackWeight ?? 1.0;
            double stealWeight = personality?.StealWeight ?? 1.0;

            switch (card.ActionKind)
            {
                case ActionType.PassGo:
                    // High priority if we have plays remaining to use the drawn cards
                    return state.PlaysRemaining >= 2 ? 80 : 40;

                case ActionType.DealBreaker:
                {
                    // Extremely high value if it can win us the game or stop an opponent
                    if (move.TargetPlayerIndex >= 0)
                    {
                        var target = state.Players[move.TargetPlayerIndex];
                        // Does this DealBreaker win us the game?
                        if (player.UniqueCompletedSetCount >= GameConfig.SetsToWin - 1)
                            return (int)(200 * stealWeight);
                        // Is the target about to win?
                        if (target.UniqueCompletedSetCount >= GameConfig.SetsToWin - 1)
                            return (int)(200 * stealWeight);
                        return (int)(90 * stealWeight);
                    }
                    return 10; // no valid target
                }

                case ActionType.DebtCollector:
                    return (int)(55 * attackWeight);

                case ActionType.ItsMyBirthday:
                    return (int)(45 * attackWeight);

                case ActionType.SlyDeal:
                {
                    if (move.TargetCardId.HasValue)
                    {
                        // Bonus if the stolen card completes one of our sets
                        int score = (int)(60 * stealWeight);
                        // Try to find the target card to check color
                        var targetPlayer = move.TargetPlayerIndex >= 0
                            ? state.Players[move.TargetPlayerIndex] : null;
                        if (targetPlayer != null)
                        {
                            var targetCard = targetPlayer.PropertySets
                                .SelectMany(s => s.Cards)
                                .FirstOrDefault(c => c.Id == move.TargetCardId);
                            if (targetCard != null)
                            {
                                var color = targetCard.ActiveColor ?? targetCard.Color;
                                if (color.HasValue)
                                {
                                    var ourSet = player.PropertySets.FirstOrDefault(s =>
                                        s.Color == color.Value && !s.IsComplete);
                                    if (ourSet != null && ourSet.Size >= ourSet.RequiredSize - 1)
                                        score += 40; // completes our set!
                                }
                            }
                        }
                        return score;
                    }
                    return 10;
                }

                case ActionType.ForceDeal:
                    return move.TargetCardId.HasValue ? (int)(55 * stealWeight) : 10;

                case ActionType.House:
                {
                    // Only valuable if we have a complete set to put it on
                    var houseSet = player.PropertySets.FirstOrDefault(s =>
                        s.IsComplete && !s.HasHouse &&
                        s.Color != PropertyColor.Railroad && s.Color != PropertyColor.Utility);
                    return houseSet != null ? 45 : 10;
                }

                case ActionType.Hotel:
                {
                    var hotelSet = player.PropertySets.FirstOrDefault(s =>
                        s.IsComplete && s.HasHouse && !s.HasHotel &&
                        s.Color != PropertyColor.Railroad && s.Color != PropertyColor.Utility);
                    return hotelSet != null ? 45 : 10;
                }

                default:
                    return 10;
            }
        }

        /// <summary>
        /// Calculate the rent amount for a given color from a player's property sets.
        /// </summary>
        private static int GetRentAmount(SimPlayer player, Card card, PropertyColor? rentColor)
        {
            if (!rentColor.HasValue) return 0;

            var set = player.PropertySets
                .Where(s => s.Color == rentColor.Value && s.Cards.Count > 0)
                .OrderByDescending(s => s.CalculateRent())
                .FirstOrDefault();

            return set?.CalculateRent() ?? 0;
        }

        // =====================================================================
        // Response policy — used when a player must respond to an action
        // =====================================================================

        /// <summary>
        /// Decide how to respond to a pending action during a rollout.
        /// Handles: Just Say No decisions and payment card selection.
        /// 
        /// This mirrors SmartBotAI.BuildResponse() + PaymentSolver but
        /// adapted for SimulationState.
        /// </summary>
        public static SimActionResponse BuildResponse(SimulationState state, int playerIndex)
        {
            var pending = state.PendingAction;
            if (pending == null)
                return new SimActionResponse { PlayJustSayNo = false, PaymentCardIds = new List<int>() };

            var player = state.Players[playerIndex];

            // Check if we should play Just Say No
            bool hasJSN = player.Hand.Any(c => c.ActionKind == ActionType.JustSayNo);
            if (hasJSN && ShouldPlayJSN(player, pending))
            {
                return new SimActionResponse { PlayJustSayNo = true };
            }

            // For steal/swap actions, no payment needed (JSN was our only defense)
            if (pending.Type == PendingActionType.RespondToSlyDeal ||
                pending.Type == PendingActionType.RespondToForceDeal ||
                pending.Type == PendingActionType.RespondToDealBreaker)
            {
                return new SimActionResponse { PlayJustSayNo = false, PaymentCardIds = new List<int>() };
            }

            // Payment actions — use simplified payment solver
            var paymentCards = FindOptimalPayment(player, pending.Amount);
            return new SimActionResponse
            {
                PlayJustSayNo = false,
                PaymentCardIds = paymentCards.Select(c => c.Id).ToList(),
            };
        }

        /// <summary>
        /// Decide whether to play Just Say No. Logic mirrors
        /// SmartBotAI.ShouldPlayJSN():
        ///   - Always JSN a DealBreaker (stealing a complete set is devastating)
        ///   - JSN SlyDeal/ForceDeal if it threatens a near-complete set
        ///   - JSN high-value rent ($5+) if we can't pay from bank alone
        ///   - Don't waste JSN on Birthday ($2)
        /// </summary>
        private static bool ShouldPlayJSN(SimPlayer player, SimPendingAction pending)
        {
            // Always counter DealBreaker
            if (pending.Type == PendingActionType.RespondToDealBreaker)
                return true;

            // Counter steal/swap if it threatens a near-complete set
            if (pending.Type == PendingActionType.RespondToSlyDeal ||
                pending.Type == PendingActionType.RespondToForceDeal)
            {
                if (pending.TargetCardId.HasValue)
                {
                    // Check if the targeted card is in a near-complete set
                    foreach (var set in player.PropertySets)
                    {
                        if (set.Cards.Any(c => c.Id == pending.TargetCardId.Value))
                        {
                            if (set.Size >= set.RequiredSize - 1)
                                return true; // protect near-complete sets
                        }
                    }
                }
                return false;
            }

            // Payment actions: JSN if rent is high and we'd lose property
            if (pending.Type == PendingActionType.PayRent ||
                pending.Type == PendingActionType.PayDebtCollector)
            {
                int amount = pending.Amount;
                int bankTotal = player.BankTotal;

                // If we can pay entirely from bank, no need to JSN
                if (bankTotal >= amount) return false;

                // High rent and we'd lose property — JSN it
                if (amount >= 5) return true;

                return false;
            }

            // Birthday ($2) — never worth a JSN
            return false;
        }

        /// <summary>
        /// Simplified payment solver for rollouts. Pays from bank first,
        /// then sacrifices lowest-value properties.
        /// 
        /// This is a greedy approximation of PaymentSolver.FindOptimalPayment().
        /// For rollout speed we don't need the full bitmask optimization.
        /// </summary>
        private static List<Card> FindOptimalPayment(SimPlayer player, int amountOwed)
        {
            var payable = player.GetPayableCards()
                .Where(c => !c.IsMulticolorWild)  // never pay with rainbow wild
                .ToList();

            int totalAssets = payable.Sum(c => c.MoneyValue);

            // If insolvent, must pay everything
            if (totalAssets <= amountOwed)
                return payable;

            // Greedy: pay with bank cards first (lowest strategic value),
            // then properties from smallest/least-complete sets
            var sorted = payable
                .OrderBy(c => CardStrategicValue(player, c))
                .ThenBy(c => c.MoneyValue)
                .ToList();

            var result = new List<Card>();
            int running = 0;
            foreach (var card in sorted)
            {
                result.Add(card);
                running += card.MoneyValue;
                if (running >= amountOwed) break;
            }

            return result;
        }

        /// <summary>
        /// Strategic value of a card for payment purposes.
        /// Lower = more willing to sacrifice.
        ///   0  = bank money (sacrifice first)
        ///   5  = orphaned property (not in a set)
        ///   10 = property in a small set
        ///   30 = property in a near-complete set
        ///   50 = property in a complete set (sacrifice last)
        /// </summary>
        private static int CardStrategicValue(SimPlayer player, Card card)
        {
            if (player.Bank.Contains(card)) return 0;

            var set = player.PropertySets.FirstOrDefault(s => s.Cards.Contains(card));
            if (set == null) return 5;
            if (set.IsComplete) return 50 + set.CalculateRent();
            if (set.Size >= set.RequiredSize - 1) return 30;
            return 10 + set.Size;
        }

        /// <summary>
        /// Estimate the maximum rent any opponent could charge this player.
        /// Mirrors CardEvaluator.EstimateMaxRent() but for SimulationState.
        /// </summary>
        private static int EstimateMaxRentSim(SimulationState state, int playerIndex)
        {
            int maxRent = 0;
            for (int i = 0; i < state.PlayerCount; i++)
            {
                if (i == playerIndex) continue;
                foreach (var set in state.Players[i].PropertySets)
                {
                    if (set.Cards.Count > 0)
                    {
                        int rent = set.CalculateRent();
                        if (rent > maxRent) maxRent = rent;
                    }
                }
            }
            return maxRent;
        }
    }
}
