using JeffopolyDeal.Models;

namespace JeffopolyDeal
{
    /// <summary>
    /// Evaluates the game state. All methods are static and pure.
    /// </summary>
    public static class BoardAnalyzer
    {
        // Total number of Just Say No cards in the full deck.
        private const int TotalJsnCards = 3;

        /// <summary>
        /// Threat score for a player (higher = more dangerous).
        /// </summary>
        public static int ThreatScore(Player player)
        {
            int score = 0;

            foreach (var set in player.PropertySets)
            {
                if (set.IsComplete)
                    score += 100;
                else if (set.Size >= set.RequiredSize - 1)
                    score += 30;
            }

            score += player.BankTotal;
            score += player.PropertySets.Sum(s => s.Cards.Count) * 2;

            return score;
        }

        /// <summary>
        /// Find the most dangerous opponent (closest to winning).
        /// </summary>
        public static Player? BiggestThreat(Player bot, List<Player> allPlayers)
        {
            return allPlayers
                .Where(p => p.ConnectionId != bot.ConnectionId)
                .OrderByDescending(ThreatScore)
                .FirstOrDefault();
        }

        /// <summary>
        /// Find the richest opponent (most total assets for payment targeting).
        /// </summary>
        public static Player? RichestOpponent(Player bot, List<Player> allPlayers)
        {
            return allPlayers
                .Where(p => p.ConnectionId != bot.ConnectionId)
                .OrderByDescending(TotalAssetValue)
                .FirstOrDefault();
        }

        /// <summary>
        /// Can the bot win this turn with remaining plays?
        /// </summary>
        public static bool CanWinThisTurn(Player bot, int remainingPlays)
        {
            int completeSets = bot.UniqueCompletedSetCount;

            // Count sets in hand that could be completed
            int completable = 0;
            foreach (var set in bot.PropertySets.Where(s => !s.IsComplete))
            {
                int needed = set.RequiredSize - set.Size;
                int cardsForColor = bot.Hand.Count(c =>
                    (c.CardType == CardType.Property || c.CardType == CardType.PropertyWildcard) &&
                    (c.Color == set.Color || c.AltColor == set.Color || c.IsMulticolorWild));
                if (cardsForColor >= needed && needed <= remainingPlays)
                    completable++;
            }

            return completeSets + completable >= GameConfig.SetsToWin;
        }

        /// <summary>
        /// Is any opponent one set away from winning?
        /// </summary>
        public static bool OpponentNearWin(Player bot, List<Player> allPlayers)
        {
            return allPlayers
                .Where(p => p.ConnectionId != bot.ConnectionId)
                .Any(p => p.UniqueCompletedSetCount >= GameConfig.SetsToWin - 1);
        }

        /// <summary>
        /// Score a property set by how close to completion (0.0 to 1.0).
        /// </summary>
        public static double SetCompletionRatio(PropertySet set)
        {
            return set.Size / (double)set.RequiredSize;
        }

        /// <summary>
        /// Find the best color for a wildcard placement.
        /// Prefer color closest to completion (but not already complete).
        /// Tiebreak: higher max rent value.
        /// </summary>
        public static PropertyColor? BestWildcardColor(Player bot)
        {
            return bot.PropertySets
                .Where(s => !s.IsComplete && s.Cards.Count > 0)
                .OrderByDescending(s => SetCompletionRatio(s))
                .ThenByDescending(s => s.CalculateRent())
                .Select(s => (PropertyColor?)s.Color)
                .FirstOrDefault();
        }

        /// <summary>
        /// Calculate total asset value of a player.
        /// </summary>
        public static int TotalAssetValue(Player player)
        {
            int bankValue = player.Bank.Sum(c => c.MoneyValue);
            int propertyValue = player.PropertySets
                .SelectMany(s => s.Cards)
                .Sum(c => c.MoneyValue);
            return bankValue + propertyValue;
        }

        // =====================================================================
        // Discard-pile awareness (issue #97)
        // =====================================================================

        /// <summary>
        /// Count how many cards of a given action type are in the discard pile.
        /// This tells the bot how many such cards have already been played and
        /// are no longer available to any player.
        /// </summary>
        public static int CountDiscarded(List<Card> discardPile, ActionType actionType)
        {
            return discardPile.Count(c => c.ActionKind == actionType);
        }

        /// <summary>
        /// Returns the number of Just Say No cards that are NOT in the discard pile
        /// and NOT in the bot's own hand.  These cards are somewhere in the unknown
        /// pool (opponent hands or the draw pile) and could therefore be played against
        /// the bot or used by an opponent to counter the bot's JSN.
        ///
        /// A return value of 0 means the bot knows for certain that no opponent holds
        /// a JSN — e.g. all three have been discarded or the bot holds them all.
        /// </summary>
        public static int JsnRemainingInUnknown(Player bot, List<Player> allPlayers, List<Card> discardPile)
        {
            int discarded = CountDiscarded(discardPile, ActionType.JustSayNo);
            int inBotHand = bot.Hand.Count(c => c.ActionKind == ActionType.JustSayNo);
            int remaining = TotalJsnCards - discarded - inBotHand;
            return System.Math.Max(0, remaining);
        }

        /// <summary>
        /// Estimates the probability that a specific opponent (with <paramref name="opponentHandSize"/>
        /// unknown cards) holds at least one Just Say No card, given how many JSN are left
        /// in the unknown card pool (<paramref name="jsnInUnknown"/>) out of
        /// <paramref name="unknownCards"/> total unseen cards.
        ///
        /// Uses the hypergeometric probability model:
        ///   P(≥1 JSN in hand) = 1 − P(0 JSN in hand)
        ///   P(0) = C(unknownCards − jsnInUnknown, opponentHandSize) / C(unknownCards, opponentHandSize)
        ///
        /// Returns 0 when <paramref name="jsnInUnknown"/> is 0 (certain: no JSN possible).
        /// Returns 1 when the opponent's hand is as large as the unknown pool (certain: must have one).
        /// </summary>
        public static double EstimateJsnHeldProbability(int opponentHandSize, int jsnInUnknown, int unknownCards)
        {
            if (jsnInUnknown <= 0 || unknownCards <= 0 || opponentHandSize <= 0)
                return 0.0;
            if (opponentHandSize >= unknownCards)
                return jsnInUnknown > 0 ? 1.0 : 0.0;

            // P(0 JSN in hand of size k, drawn from pool of N with J JSN)
            // = product_{i=0}^{k-1} (N - J - i) / (N - i)
            double probNone = 1.0;
            for (int i = 0; i < opponentHandSize; i++)
            {
                double denom = unknownCards - i;
                if (denom <= 0) break;
                double numerator = System.Math.Max(0.0, unknownCards - jsnInUnknown - i);
                probNone *= numerator / denom;
            }
            return System.Math.Max(0.0, System.Math.Min(1.0, 1.0 - probNone));
        }
    }
}
