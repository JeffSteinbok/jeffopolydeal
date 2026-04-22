using JeffopolyDeal.Models;

namespace JeffopolyDeal
{
    /// <summary>
    /// Evaluates the game state. All methods are static and pure.
    /// </summary>
    public static class BoardAnalyzer
    {
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
    }
}
