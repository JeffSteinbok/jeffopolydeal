using JeffopolyDeal.ISMCTS;
using JeffopolyDeal.Models;

namespace JeffopolyDeal
{
    /// <summary>
    /// Smart bot AI powered by ISMCTS (Information Set Monte Carlo Tree Search).
    /// 
    /// For PROACTIVE decisions (what card to play on your turn), the bot uses
    /// ISMCTS to search across many possible game states and find the move with
    /// the highest win probability.
    /// 
    /// For REACTIVE decisions (how to respond to an opponent's action — paying
    /// rent, playing Just Say No, etc.), the bot uses fast heuristic logic since
    /// these decisions don't benefit as much from tree search.
    /// </summary>
    public static class SmartBotAI
    {
        private static readonly Random _rng = new();

        /// <summary>
        /// Default ISMCTS configuration. Can be overridden for difficulty levels
        /// or performance tuning.
        /// </summary>
        private static readonly ISMCTSConfig _defaultConfig = new ISMCTSConfig
        {
            Iterations = 500,
            ExplorationConstant = 1.0,
            MaxRolloutTurns = 20,
            TimeLimitMs = 200,
        };

        public static bool IsBot(string connectionId) => connectionId.StartsWith("bot-");

        /// <summary>
        /// Play a full bot turn using ISMCTS for move selection.
        /// 
        /// For each card play, the bot:
        ///   1. Snapshots the game state into a SimulationState
        ///   2. Runs ISMCTS to find the best move
        ///   3. Converts the SimMove back to a PlayCardRequest
        ///   4. Plays the card through the real game engine
        ///   5. Repeats until out of plays or ISMCTS says to end turn
        /// 
        /// Falls back to the old heuristic (CardEvaluator) if ISMCTS produces
        /// no results (e.g., too few iterations completed).
        /// </summary>
        public static void PlayTurn(Player bot, List<Player> allPlayers, Deck deck,
            Func<Player, Card, PlayCardRequest, bool> playCard, int maxPlays,
            ISMCTSConfig? config = null)
        {
            config ??= _defaultConfig;

            // Collect all cards in the game (reused across all ISMCTS calls this turn).
            // This pool is needed by the Determinizer to sample opponent hands.
            var allCards = Determinizer.CollectAllCards(bot, allPlayers, deck);

            // If the card pool is too small for meaningful ISMCTS (e.g., in unit tests
            // with minimal game state), fall back to the heuristic-based approach.
            // ISMCTS needs a reasonable unknown card pool to produce good determinizations.
            bool useISMCTS = allCards.Count >= 20 && config.Iterations > 0;

            int plays = 0;
            while (plays < maxPlays && bot.Hand.Count > 0)
            {
                int playsRemaining = maxPlays - plays;

                if (useISMCTS)
                {
                    // --- ISMCTS path: search across simulated futures ---

                    // Record opponent hand sizes (Determinizer needs to deal the right count)
                    var handSizes = allPlayers.Select(p => p.Hand.Count).ToArray();

                    // Snapshot the real game state for ISMCTS
                    var simState = SimulationState.FromGame(bot, allPlayers, deck, playsRemaining);

                    int botIndex = allPlayers.FindIndex(p => p.ConnectionId == bot.ConnectionId);
                    if (botIndex < 0) break;

                    // Run ISMCTS to find the best move
                    var bestMove = ISMCTSEngine.FindBestMove(
                        simState, botIndex, allCards, handSizes, config);

                    if (bestMove.IsEndTurn || bestMove.Card == null) break;

                    var card = bot.Hand.FirstOrDefault(c => c.Id == bestMove.Card.Id);
                    if (card == null) break;

                    PlayCardRequest request;
                    if (bestMove.PlayAsMoney)
                    {
                        request = new PlayCardRequest { PlayAsMoney = true };
                    }
                    else
                    {
                        request = ConvertMoveToRequest(bot, card, bestMove, allPlayers);
                    }

                    // Attach DoubleTheRent cards if the ISMCTS move included them
                    if (bestMove.DoubleRentCardIds != null && bestMove.DoubleRentCardIds.Count > 0)
                        request.DoubleRentCardIds = bestMove.DoubleRentCardIds;

                    bool shouldContinue = playCard(bot, card, request);
                    plays++;

                    if (request.DoubleRentCardIds != null)
                        plays += request.DoubleRentCardIds.Count;

                    if (!shouldContinue) break;
                }
                else
                {
                    // --- Heuristic fallback path: CardEvaluator-based scoring ---
                    // Used when the game state is too minimal for ISMCTS (e.g., tests)
                    // or when ISMCTS is explicitly disabled (Iterations = 0).

                    var discardSnapshot = deck.GetDiscardPileSnapshot();
                    var candidates = bot.Hand
                        .Select(c => new { Card = c, Score = CardEvaluator.PlayScore(bot, c, allPlayers, playsRemaining, discardSnapshot) })
                        .Where(x => x.Score.HasValue)
                        .OrderByDescending(x => x.Score)
                        .ToList();

                    if (candidates.Count == 0) break;

                    // Small randomness among top choices (within 10% of best score)
                    var bestScore = candidates[0].Score!.Value;
                    var topTier = candidates.Where(x => x.Score >= bestScore * 0.9).ToList();
                    var pick = topTier.Count > 1 ? topTier[_rng.Next(topTier.Count)] : topTier[0];

                    var card = pick.Card;
                    var request = BuildRequest(bot, card, allPlayers);
                    if (request == null)
                        request = new PlayCardRequest { PlayAsMoney = true };

                    // Check for DoubleTheRent opportunity when playing rent
                    if (card.CardType == CardType.Rent && !request.PlayAsMoney && plays + 1 < maxPlays)
                    {
                        var dtr = bot.Hand.FirstOrDefault(c => c.ActionKind == ActionType.DoubleTheRent);
                        if (dtr != null)
                        {
                            request.DoubleRentCardIds = new List<int> { dtr.Id };
                            if (plays + 2 < maxPlays)
                            {
                                var dtr2 = bot.Hand.FirstOrDefault(c =>
                                    c.ActionKind == ActionType.DoubleTheRent && c.Id != dtr.Id);
                                if (dtr2 != null)
                                    request.DoubleRentCardIds.Add(dtr2.Id);
                            }
                        }
                    }

                    bool shouldContinue = playCard(bot, card, request);
                    plays++;

                    if (request.DoubleRentCardIds != null)
                        plays += request.DoubleRentCardIds.Count;

                    if (!shouldContinue) break;
                }
            }
        }

        /// <summary>
        /// Convert an ISMCTS SimMove (which uses player indices) to a PlayCardRequest
        /// (which uses ConnectionId strings) for the real game engine.
        /// </summary>
        private static PlayCardRequest ConvertMoveToRequest(
            Player bot, Card card, SimMove move, List<Player> allPlayers)
        {
            var request = new PlayCardRequest { PlayAsMoney = false };

            // Target player
            if (move.TargetPlayerIndex >= 0 && move.TargetPlayerIndex < allPlayers.Count)
                request.TargetPlayerId = allPlayers[move.TargetPlayerIndex].ConnectionId;

            // Card-type-specific fields
            request.RentColor = move.RentColor;
            request.WildcardColor = move.WildcardColor;
            request.TargetCardId = move.TargetCardId;
            request.OfferedCardId = move.OfferedCardId;
            request.TargetSetColor = move.TargetSetColor;

            return request;
        }

        /// <summary>
        /// Respond to a pending action (pay, JSN, etc).
        /// </summary>
        public static ActionResponse BuildResponse(Player bot, PendingAction pending, List<Player> allPlayers,
            List<Card>? discardPile = null)
        {
            var effectiveType = pending.OriginalActionType ?? pending.Type;

            // JSN decision
            var jsn = bot.Hand.FirstOrDefault(c => c.ActionKind == ActionType.JustSayNo);
            if (jsn != null && ShouldPlayJSN(bot, pending, effectiveType, allPlayers, discardPile))
            {
                return new ActionResponse { PlayJustSayNo = true };
            }

            // For steal/swap actions, no payment needed
            if (effectiveType == PendingActionType.RespondToSlyDeal ||
                effectiveType == PendingActionType.RespondToForceDeal ||
                effectiveType == PendingActionType.RespondToDealBreaker)
            {
                return new ActionResponse { PlayJustSayNo = false, PaymentCardIds = new List<int>() };
            }

            // Payment actions — use PaymentSolver, passing the receiver so it avoids helping them win
            var receiver = allPlayers.FirstOrDefault(p => p.ConnectionId == pending.SourcePlayerId);
            var optimalPayment = PaymentSolver.FindOptimalPayment(bot, pending.Amount, receiver);
            return new ActionResponse
            {
                PlayJustSayNo = false,
                PaymentCardIds = optimalPayment.Select(c => c.Id).ToList()
            };
        }

        private static bool ShouldPlayJSN(Player bot, PendingAction pending, PendingActionType effectiveType,
            List<Player> allPlayers, List<Card>? discardPile)
        {
            // In a JSN chain where bot is the ATTACKER
            if (pending.Type == PendingActionType.JustSayNoChain)
            {
                if (pending.OriginalSourcePlayerId == bot.ConnectionId)
                {
                    // If the discard pile shows no JSN remain in unknown hands, our counter
                    // will be final — no risk of another JSN in reply. Always counter.
                    // If JSN remain in play (opponent might have one), still counter to protect
                    // our original action investment — the chain is worth fighting over.
                    return true;
                }
            }

            // Always JSN DealBreaker
            if (effectiveType == PendingActionType.RespondToDealBreaker)
                return true;

            // JSN SlyDeal/ForceDeal if it would break a near-complete set
            if (effectiveType == PendingActionType.RespondToSlyDeal ||
                effectiveType == PendingActionType.RespondToForceDeal)
            {
                var targetCard = bot.PropertySets
                    .SelectMany(s => s.Cards)
                    .FirstOrDefault(c => c.Id == pending.TargetCardId);
                if (targetCard != null)
                {
                    var set = bot.PropertySets.FirstOrDefault(s => s.Cards.Contains(targetCard));
                    if (set != null && set.Size >= set.RequiredSize - 1)
                        return true;
                }
                return false;
            }

            // Payment actions: JSN based on value at risk.
            // With discard-pile awareness: if there are few/no JSN remaining in unknown
            // hands our counter won't be blocked, making it safer to play.
            if (effectiveType == PendingActionType.PayRent ||
                effectiveType == PendingActionType.PayDebtCollector)
            {
                int amount = pending.Amount;
                int bankTotal = bot.BankTotal;

                if (bankTotal >= amount)
                    return false;

                // Count JSN cards that might be in the attacker's hand.
                // If none remain in the unknown pool, our JSN is guaranteed to succeed.
                int jsnInUnknown = discardPile != null
                    ? BoardAnalyzer.JsnRemainingInUnknown(bot, allPlayers, discardPile)
                    : int.MaxValue;

                // Play JSN when the charge is expensive AND bank can't cover it.
                // Lower the threshold when we know the opponent can't counter our JSN.
                int threshold = jsnInUnknown == 0 ? 4 : 5;
                if (amount >= threshold)
                    return true;

                return false;
            }

            // Birthday ($2) — never worth a JSN
            if (effectiveType == PendingActionType.PayBirthday)
                return false;

            return false;
        }

        private static PlayCardRequest? BuildRequest(Player bot, Card card, List<Player> allPlayers)
        {
            var others = allPlayers.Where(p => p.ConnectionId != bot.ConnectionId).ToList();

            switch (card.CardType)
            {
                case CardType.Money:
                    return new PlayCardRequest { PlayAsMoney = true };

                case CardType.Property:
                    return new PlayCardRequest { PlayAsMoney = false };

                case CardType.PropertyWildcard:
                    if (card.IsMulticolorWild)
                    {
                        var bestColor = BoardAnalyzer.BestWildcardColor(bot) ?? PropertyColor.Brown;
                        return new PlayCardRequest { PlayAsMoney = false, WildcardColor = bestColor };
                    }
                    // Dual-color wild: pick the color closer to completion
                    var color1Ratio = bot.PropertySets
                        .Where(s => s.Color == card.Color && !s.IsComplete)
                        .Select(s => BoardAnalyzer.SetCompletionRatio(s))
                        .DefaultIfEmpty(0)
                        .Max();
                    var color2Ratio = card.AltColor.HasValue ? bot.PropertySets
                        .Where(s => s.Color == card.AltColor && !s.IsComplete)
                        .Select(s => BoardAnalyzer.SetCompletionRatio(s))
                        .DefaultIfEmpty(0)
                        .Max() : 0;
                    var chosenColor = color2Ratio > color1Ratio ? card.AltColor!.Value : card.Color!.Value;
                    return new PlayCardRequest { PlayAsMoney = false, WildcardColor = chosenColor };

                case CardType.Rent:
                    return BuildRentRequest(bot, card, others);

                case CardType.Action:
                    return BuildActionRequest(bot, card, others, allPlayers);

                default:
                    return null;
            }
        }

        private static PlayCardRequest? BuildRentRequest(Player bot, Card card, List<Player> others)
        {
            if (others.Count == 0) return new PlayCardRequest { PlayAsMoney = true };

            PropertyColor? rentColor = null;

            if (card.IsWildRent)
            {
                var bestSet = bot.PropertySets
                    .Where(s => s.Cards.Count > 0)
                    .OrderByDescending(s => s.CalculateRent())
                    .FirstOrDefault();
                if (bestSet == null) return new PlayCardRequest { PlayAsMoney = true };
                rentColor = bestSet.Color;

                var allWithBot = others.Concat(new[] { bot }).ToList();
                var target = BoardAnalyzer.RichestOpponent(bot, allWithBot) ?? others[0];
                return new PlayCardRequest
                {
                    PlayAsMoney = false,
                    RentColor = rentColor,
                    TargetPlayerId = target.ConnectionId,
                };
            }

            // Standard rent — pick color with highest rent
            if (card.RentColors != null)
            {
                rentColor = card.RentColors
                    .Where(c => bot.PropertySets.Any(s => s.Color == c && s.Cards.Count > 0))
                    .OrderByDescending(c => bot.PropertySets.FirstOrDefault(s => s.Color == c)?.CalculateRent() ?? 0)
                    .FirstOrDefault();
            }

            if (rentColor == null) return new PlayCardRequest { PlayAsMoney = true };

            return new PlayCardRequest
            {
                PlayAsMoney = false,
                RentColor = rentColor,
            };
        }

        private static PlayCardRequest? BuildActionRequest(Player bot, Card card, List<Player> others,
            List<Player> allPlayers)
        {
            if (others.Count == 0) return new PlayCardRequest { PlayAsMoney = true };

            switch (card.ActionKind)
            {
                case ActionType.PassGo:
                    return new PlayCardRequest { PlayAsMoney = false };

                case ActionType.DebtCollector:
                {
                    var target = BoardAnalyzer.RichestOpponent(bot, allPlayers) ?? others[0];
                    return new PlayCardRequest { PlayAsMoney = false, TargetPlayerId = target.ConnectionId };
                }

                case ActionType.ItsMyBirthday:
                    return new PlayCardRequest { PlayAsMoney = false };

                case ActionType.SlyDeal:
                {
                    Card? bestSteal = null;
                    Player? bestTarget = null;
                    int bestScore = -1;

                    foreach (var target in others)
                    {
                        foreach (var stealable in target.GetStealableProperties())
                        {
                            int score = 0;
                            var color = stealable.ActiveColor ?? stealable.Color;
                            if (color.HasValue)
                            {
                                var ourSet = bot.PropertySets.FirstOrDefault(s =>
                                    s.Color == color.Value && !s.IsComplete);
                                if (ourSet != null && ourSet.Size >= ourSet.RequiredSize - 1)
                                    score += 100;
                                else if (ourSet != null)
                                    score += 30;

                                var theirSet = target.PropertySets.FirstOrDefault(s => s.Cards.Contains(stealable));
                                if (theirSet != null && theirSet.Size >= theirSet.RequiredSize - 1)
                                    score += 50;
                            }
                            score += stealable.MoneyValue;

                            if (score > bestScore)
                            {
                                bestScore = score;
                                bestSteal = stealable;
                                bestTarget = target;
                            }
                        }
                    }

                    if (bestSteal == null || bestTarget == null)
                        return new PlayCardRequest { PlayAsMoney = true };

                    return new PlayCardRequest
                    {
                        PlayAsMoney = false,
                        TargetPlayerId = bestTarget.ConnectionId,
                        TargetCardId = bestSteal.Id,
                    };
                }

                case ActionType.ForceDeal:
                {
                    var myStealable = bot.GetStealableProperties();
                    if (myStealable.Count == 0) return new PlayCardRequest { PlayAsMoney = true };

                    var offer = myStealable.OrderBy(c => c.MoneyValue).First();

                    Card? bestTake = null;
                    Player? bestTarget = null;
                    int bestScore = -1;

                    foreach (var target in others)
                    {
                        foreach (var stealable in target.GetStealableProperties())
                        {
                            int score = stealable.MoneyValue;
                            var color = stealable.ActiveColor ?? stealable.Color;
                            if (color.HasValue)
                            {
                                var ourSet = bot.PropertySets.FirstOrDefault(s =>
                                    s.Color == color.Value && !s.IsComplete);
                                if (ourSet != null && ourSet.Size >= ourSet.RequiredSize - 1)
                                    score += 100;
                                else if (ourSet != null)
                                    score += 30;
                            }
                            if (score > bestScore)
                            {
                                bestScore = score;
                                bestTake = stealable;
                                bestTarget = target;
                            }
                        }
                    }

                    if (bestTake == null || bestTarget == null)
                        return new PlayCardRequest { PlayAsMoney = true };

                    return new PlayCardRequest
                    {
                        PlayAsMoney = false,
                        TargetPlayerId = bestTarget.ConnectionId,
                        TargetCardId = bestTake.Id,
                        OfferedCardId = offer.Id,
                    };
                }

                case ActionType.DealBreaker:
                {
                    PropertySet? bestSet = null;
                    Player? bestTarget = null;
                    int bestRent = -1;

                    foreach (var target in others)
                    {
                        foreach (var set in target.GetCompletePropertySets())
                        {
                            int score = set.CalculateRent();
                            if (bot.UniqueCompletedSetCount >= GameConfig.SetsToWin - 1)
                                score += 1000;
                            if (target.UniqueCompletedSetCount >= GameConfig.SetsToWin - 1)
                                score += 500;
                            if (score > bestRent)
                            {
                                bestRent = score;
                                bestSet = set;
                                bestTarget = target;
                            }
                        }
                    }

                    if (bestSet == null || bestTarget == null)
                        return new PlayCardRequest { PlayAsMoney = true };

                    return new PlayCardRequest
                    {
                        PlayAsMoney = false,
                        TargetPlayerId = bestTarget.ConnectionId,
                        TargetSetColor = bestSet.Color,
                    };
                }

                case ActionType.House:
                {
                    var set = bot.PropertySets
                        .Where(s => s.IsComplete && !s.HasHouse &&
                            s.Color != PropertyColor.Railroad && s.Color != PropertyColor.Utility)
                        .OrderByDescending(s => s.CalculateRent())
                        .FirstOrDefault();
                    if (set == null) return new PlayCardRequest { PlayAsMoney = true };
                    return new PlayCardRequest { PlayAsMoney = false, TargetSetColor = set.Color };
                }

                case ActionType.Hotel:
                {
                    var set = bot.PropertySets
                        .Where(s => s.IsComplete && s.HasHouse && !s.HasHotel &&
                            s.Color != PropertyColor.Railroad && s.Color != PropertyColor.Utility)
                        .OrderByDescending(s => s.CalculateRent())
                        .FirstOrDefault();
                    if (set == null) return new PlayCardRequest { PlayAsMoney = true };
                    return new PlayCardRequest { PlayAsMoney = false, TargetSetColor = set.Color };
                }

                default:
                    return new PlayCardRequest { PlayAsMoney = true };
            }
        }

        /// <summary>
        /// Pick cards to discard. Keep set-completing cards and JSN.
        /// </summary>
        public static List<int> PickDiscards(Player bot, int maxHandSize)
        {
            int excess = bot.Hand.Count - maxHandSize;
            if (excess <= 0) return new List<int>();

            return bot.Hand
                .OrderBy(c => KeepPriority(bot, c))
                .Take(excess)
                .Select(c => c.Id)
                .ToList();
        }

        private static int KeepPriority(Player bot, Card card)
        {
            if (card.ActionKind == ActionType.JustSayNo) return 100;

            if (card.CardType == CardType.Property || card.CardType == CardType.PropertyWildcard)
            {
                var color = card.Color;
                if (color.HasValue)
                {
                    var set = bot.PropertySets.FirstOrDefault(s => s.Color == color.Value && !s.IsComplete);
                    if (set != null && set.Size >= set.RequiredSize - 1) return 90;
                    if (set != null) return 50 + set.Size * 5;
                }
                return 40;
            }

            if (card.CardType == CardType.Rent)
            {
                if (card.IsWildRent) return 60;
                return 45;
            }

            if (card.ActionKind == ActionType.DealBreaker) return 70;
            if (card.ActionKind == ActionType.PassGo) return 35;

            if (card.ActionKind == ActionType.DoubleTheRent)
            {
                bool hasRent = bot.Hand.Any(c => c.CardType == CardType.Rent);
                return hasRent ? 55 : 5;
            }

            if (card.CardType == CardType.Money) return card.MoneyValue;

            return 20;
        }
    }
}
