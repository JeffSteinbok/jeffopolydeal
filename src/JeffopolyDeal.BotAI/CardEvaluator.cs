using JeffopolyDeal.ISMCTS;
using JeffopolyDeal.Models;

namespace JeffopolyDeal
{
    /// <summary>
    /// Scores cards in hand for play priority.
    /// Higher = play first. Returns null if card shouldn't be played.
    /// 
    /// Defensive awareness: The scorer considers the bot's bank balance when
    /// deciding between properties and money. A thin bank means rent charges
    /// will strip properties off the board, so banking money becomes more
    /// valuable and playing non-completing properties becomes less attractive.
    ///
    /// Discard-pile awareness: When a discard pile snapshot is provided, the
    /// scorer factors in how many action cards (e.g. Just Say No) remain in
    /// play when scoring attack cards — boosting steal/rent plays when
    /// opponents are less likely to be holding counters.
    ///
    /// Personality awareness: When a BotPersonality is provided, feature-level
    /// parameters (attack weight, steal weight, rent buffer target, etc.)
    /// are used instead of defaults, producing different play styles.
    /// </summary>
    public static class CardEvaluator
    {
        /// <summary>
        /// Default target bank balance for rent buffer (used when no personality).
        /// Typical rent charges are 1-8M, so 5M covers most single charges.
        /// </summary>
        private const int DefaultRentBufferTarget = 5;

        public static int? PlayScore(Player bot, Card card, List<Player> allPlayers, int playsRemaining,
            List<Card>? discardPile = null, BotPersonality? personality = null)
        {
            // Cards that should never be proactively played
            if (card.ActionKind == ActionType.JustSayNo) return null;
            if (card.ActionKind == ActionType.DoubleTheRent) return null;

            int bankTotal = bot.BankTotal;

            // Use personality's rent buffer target, or default
            int rentBufferTarget = personality?.RentBufferTarget ?? DefaultRentBufferTarget;
            double propertyWeight = personality?.PropertyWeight ?? 1.0;
            double setCompletionWeight = personality?.SetCompletionWeight ?? 1.0;

            // How exposed are we? If bank is below the rent buffer target,
            // we're vulnerable — money is worth more, loose properties worth less.
            bool bankIsLow = bankTotal < rentBufferTarget;

            // Estimate opponent rent threat: max rent any opponent could charge us
            int maxOpponentRent = EstimateMaxRent(bot, allPlayers);

            switch (card.CardType)
            {
                case CardType.Money:
                    // Base score 20, but up to 45 when bank is dangerously low.
                    // The less bank we have relative to the buffer, the more we
                    // want to add cash. Once we have enough buffer, money drops
                    // back to baseline.
                    if (bankIsLow)
                    {
                        int deficit = rentBufferTarget - bankTotal;
                        return 20 + Math.Min(25, deficit * 5);
                    }
                    return 20;

                case CardType.Property:
                {
                    int propScore = (int)(30 * propertyWeight);
                    var targetSet = bot.PropertySets.FirstOrDefault(s =>
                        s.Color == card.Color && !s.IsComplete);
                    if (targetSet != null && targetSet.Size >= targetSet.RequiredSize - 1)
                    {
                        // Completing a set is always high priority — complete sets
                        // can't be stolen with Sly Deal and are worth the risk
                        propScore += (int)(40 * setCompletionWeight);
                    }
                    else if (bankIsLow && maxOpponentRent > bankTotal)
                    {
                        // Non-completing property with a thin bank: this property
                        // is exposed. Rent will eat through our empty bank and
                        // we'll lose it anyway. Reduce priority below money.
                        propScore = 15;
                    }
                    return propScore;
                }

                case CardType.PropertyWildcard:
                {
                    // Wildcards follow similar logic — check if they'd complete a set
                    bool completesASet = bot.PropertySets.Any(s =>
                        !s.IsComplete && s.Size >= s.RequiredSize - 1);
                    if (completesASet) return (int)(70 * setCompletionWeight);
                    if (bankIsLow && maxOpponentRent > bankTotal) return 15;
                    return (int)(35 * propertyWeight);
                }

                case CardType.Rent:
                {
                    int rentAmount = GetBestRentAmount(bot, card);
                    if (rentAmount == 0) return 10;
                    double attackWeight = personality?.AttackWeight ?? 1.0;
                    return (int)((50 + rentAmount * 5) * attackWeight);
                }

                case CardType.Action:
                    return ScoreAction(bot, card, allPlayers, playsRemaining, discardPile, personality);
            }

            return 10;
        }

        internal static int GetBestRentAmount(Player bot, Card card)
        {
            if (card.IsWildRent)
            {
                return bot.PropertySets
                    .Where(s => s.Cards.Count > 0)
                    .Select(s => s.CalculateRent())
                    .DefaultIfEmpty(0)
                    .Max();
            }

            if (card.RentColors == null) return 0;

            return card.RentColors
                .Select(c => bot.PropertySets
                    .Where(s => s.Color == c && s.Cards.Count > 0)
                    .Select(s => s.CalculateRent())
                    .DefaultIfEmpty(0)
                    .Max())
                .DefaultIfEmpty(0)
                .Max();
        }

        private static int ScoreAction(Player bot, Card card, List<Player> allPlayers, int playsRemaining,
            List<Card>? discardPile, BotPersonality? personality = null)
        {
            var others = allPlayers.Where(p => p.ConnectionId != bot.ConnectionId).ToList();

            // Compute continuous JSN risk discount using personality sensitivity.
            // jsnRiskDiscount is 0.0 (no JSN risk, attack freely) to 1.0 (max risk).
            double jsnRiskSensitivity = personality?.JsnRiskSensitivity ?? 0.5;
            double jsnProb = EstimateJsnProbability(bot, allPlayers, discardPile);
            // Discount factor: 1.0 = no discount, lower = attacks are riskier
            double jsnRiskDiscount = 1.0 - (jsnProb * jsnRiskSensitivity);

            double attackWeight = personality?.AttackWeight ?? 1.0;
            double stealWeight = personality?.StealWeight ?? 1.0;

            switch (card.ActionKind)
            {
                case ActionType.PassGo:
                    return playsRemaining >= 2 ? 80 : 40;

                case ActionType.DealBreaker:
                    if (others.Any(p => p.GetCompletePropertySets().Count > 0))
                    {
                        if (BoardAnalyzer.OpponentNearWin(bot, allPlayers))
                            return (int)(200 * stealWeight);
                        return (int)(90 * stealWeight * jsnRiskDiscount);
                    }
                    return 10;

                case ActionType.DebtCollector:
                    return (int)(55 * attackWeight * jsnRiskDiscount);

                case ActionType.ItsMyBirthday:
                    return (int)(45 * attackWeight * jsnRiskDiscount);

                case ActionType.SlyDeal:
                    if (others.Any(p => p.GetStealableProperties().Count > 0))
                        return (int)(60 * stealWeight * jsnRiskDiscount);
                    return 10;

                case ActionType.ForceDeal:
                    if (bot.GetStealableProperties().Count > 0 &&
                        others.Any(p => p.GetStealableProperties().Count > 0))
                        return (int)(55 * stealWeight * jsnRiskDiscount);
                    return 10;

                case ActionType.House:
                {
                    var houseSet = bot.PropertySets.FirstOrDefault(s =>
                        s.IsComplete && !s.HasHouse &&
                        s.Color != PropertyColor.Railroad && s.Color != PropertyColor.Utility);
                    return houseSet != null ? 45 : 10;
                }

                case ActionType.Hotel:
                {
                    var hotelSet = bot.PropertySets.FirstOrDefault(s =>
                        s.IsComplete && s.HasHouse && !s.HasHotel &&
                        s.Color != PropertyColor.Railroad && s.Color != PropertyColor.Utility);
                    return hotelSet != null ? 45 : 10;
                }

                default:
                    return 10;
            }
        }

        /// <summary>
        /// Estimate the probability that ANY opponent holds a Just Say No card.
        /// Returns a continuous value 0.0 (no risk) to ~1.0 (high risk).
        /// Used by personality-aware scoring for continuous JSN risk discount.
        /// </summary>
        private static double EstimateJsnProbability(Player bot, List<Player> allPlayers, List<Card>? discardPile)
        {
            if (discardPile == null || allPlayers.Count <= 1)
                return 0.0; // no discard info = no discount (preserves old behavior)

            int jsnInUnknown = BoardAnalyzer.JsnRemainingInUnknown(bot, allPlayers, discardPile);
            if (jsnInUnknown == 0) return 0.0;

            int totalVisible = bot.Hand.Count
                + allPlayers.SelectMany(p => p.Bank).Count()
                + allPlayers.SelectMany(p => p.PropertySets).SelectMany(s => s.Cards).Count()
                + allPlayers.SelectMany(p => p.UnboundWilds).Count()
                + discardPile.Count;
            int unknownCards = Math.Max(1, 106 - totalVisible);

            var opponents = allPlayers.Where(p => p.ConnectionId != bot.ConnectionId).ToList();
            if (opponents.Count == 0) return 0.0;
            int avgHandSize = opponents.Sum(p => p.Hand.Count) / opponents.Count;

            return BoardAnalyzer.EstimateJsnHeldProbability(avgHandSize, jsnInUnknown, unknownCards);
        }

        /// <summary>
        /// Returns true when the estimated probability that ANY opponent holds a Just Say No
        /// is low enough that it is safe to treat attack cards as essentially unblockable.
        ///
        /// Uses the discard pile to compute how many JSN remain in the unknown pool.
        /// If no discard information is provided the risk is assumed to be normal.
        /// 
        /// NOTE: Kept for backward compatibility. New code should use the continuous
        /// EstimateJsnProbability + JsnRiskSensitivity approach via personality.
        /// </summary>
        private static bool IsLowJsnRisk(Player bot, List<Player> allPlayers, List<Card>? discardPile)
        {
            return EstimateJsnProbability(bot, allPlayers, discardPile) < 0.10;
        }

        /// <summary>
        /// Estimate the maximum rent any opponent could charge this turn.
        /// Used to gauge how "exposed" the bot is with a thin bank.
        /// 
        /// We look at each opponent's property sets and take the highest rent
        /// value across all of them. This is a rough upper bound — in practice
        /// the opponent also needs a matching rent card, but planning for the
        /// worst case makes the bot more resilient.
        /// </summary>
        internal static int EstimateMaxRent(Player bot, List<Player> allPlayers)
        {
            int maxRent = 0;
            foreach (var p in allPlayers)
            {
                if (p.ConnectionId == bot.ConnectionId) continue;
                foreach (var set in p.PropertySets)
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

        // =====================================================================
        // Banking decision helper
        // =====================================================================

        /// <summary>
        /// Determines whether a card should be banked as money. High-value action
        /// cards (Pass Go, Rent, Steal cards, etc.) should NOT be banked unless:
        ///   1. Banking empties the hand → triggers 5-card draw next turn
        ///   2. Hand is over limit and card would be discarded for nothing
        /// 
        /// Pass Go should NEVER be banked (it's always playable and draws 2 cards).
        /// </summary>
        public static bool ShouldBankCard(Card card, int handCount, int playsRemaining)
        {
            // Pass Go: never bank — always play it to draw 2 cards
            if (card.ActionKind == ActionType.PassGo) return false;

            // Money cards: always bank
            if (card.CardType == CardType.Money) return true;

            // Properties: always play as property
            if (card.CardType == CardType.Property || card.CardType == CardType.PropertyWildcard) return true;

            // JSN and DTR: never bank (handled elsewhere, shouldn't reach here)
            if (card.ActionKind == ActionType.JustSayNo) return false;
            if (card.ActionKind == ActionType.DoubleTheRent) return false;

            // High-value action/rent cards: only bank to empty hand for 5-card draw
            // or if hand-limit pressure would force a discard anyway
            bool isHighValue = card.CardType == CardType.Rent ||
                card.ActionKind == ActionType.DealBreaker ||
                card.ActionKind == ActionType.SlyDeal ||
                card.ActionKind == ActionType.ForceDeal ||
                card.ActionKind == ActionType.DebtCollector ||
                card.ActionKind == ActionType.ItsMyBirthday;

            if (isHighValue)
            {
                // Would banking this card empty our hand? 
                // handCount includes this card. After banking, we'd have handCount - 1.
                // If remaining plays could clear the rest, it's worth it for 5-card draw.
                int cardsAfter = handCount - 1;
                bool emptiesHand = cardsAfter == 0 || cardsAfter <= playsRemaining - 1;

                // Also allow banking under hand-limit pressure (over 7 cards)
                bool handLimitPressure = handCount > GameConfig.MaxHandSize;

                return emptiesHand || handLimitPressure;
            }

            // Everything else: OK to bank
            return true;
        }
    }
}
