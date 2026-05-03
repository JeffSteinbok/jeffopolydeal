using JeffopolyDeal.ISMCTS;
using JeffopolyDeal.Models;

namespace JeffopolyDeal
{
    // =========================================================================
    // ThreatProfile — Snapshot of remaining threat cards in the unknown pool
    // =========================================================================
    //
    // Tracks how many of each dangerous action card are unaccounted for
    // (not in the discard pile and not in the bot's own hand). These cards
    // exist somewhere in opponent hands or the draw pile and could be used
    // against the bot at any time.
    //
    // The profile supports probability queries: "what is the chance that
    // opponent X holds at least one Deal Breaker?" using the hypergeometric
    // distribution (same math as EstimateHeldProbability).
    //
    // Usage:
    //   var profile = BoardAnalyzer.BuildThreatProfile(bot, allPlayers, discardPile);
    //   double dbRisk = profile.ProbabilityOpponentHolds(
    //       profile.DealBreakersRemaining, opponentHandSize);
    // =========================================================================

    /// <summary>
    /// Counts of threat cards remaining in the unknown pool (opponent hands +
    /// draw pile). Built from discard-pile and bot-hand analysis.
    /// </summary>
    public class ThreatProfile
    {
        // --- Card counts in the full Monopoly Deal deck ---
        // These are the total number of each card type across all 106 cards.
        public const int TotalDealBreakers = 2;
        public const int TotalSlyDeals = 3;
        public const int TotalForceDeals = 3;
        public const int TotalDebtCollectors = 3;
        public const int TotalBirthdays = 3;
        public const int TotalJsn = 3;
        public const int TotalDtr = 2;
        public const int TotalWildRent = 3;
        public const int TotalCards = 106;

        // --- Remaining counts in the unknown pool ---

        /// <summary>Deal Breakers not in discard or bot hand.</summary>
        public int DealBreakersRemaining { get; set; }

        /// <summary>Sly Deals not in discard or bot hand.</summary>
        public int SlyDealsRemaining { get; set; }

        /// <summary>Forced Deals not in discard or bot hand.</summary>
        public int ForceDealsRemaining { get; set; }

        /// <summary>Debt Collectors not in discard or bot hand.</summary>
        public int DebtCollectorsRemaining { get; set; }

        /// <summary>Birthdays not in discard or bot hand.</summary>
        public int BirthdaysRemaining { get; set; }

        /// <summary>Just Say No cards not in discard or bot hand.</summary>
        public int JsnRemaining { get; set; }

        /// <summary>Double The Rent cards not in discard or bot hand.</summary>
        public int DtrRemaining { get; set; }

        /// <summary>Wild Rent cards not in discard or bot hand.</summary>
        public int WildRentRemaining { get; set; }

        /// <summary>Total unknown cards (opponent hands + draw pile).</summary>
        public int UnknownPoolSize { get; set; }

        // --- Derived aggregates ---

        /// <summary>
        /// Total steal threats: Deal Breaker + Sly Deal + Forced Deal.
        /// These can take properties directly from the bot.
        /// </summary>
        public int StealThreatsRemaining =>
            DealBreakersRemaining + SlyDealsRemaining + ForceDealsRemaining;

        /// <summary>
        /// Total payment threats: Debt Collector + Birthday + Wild Rent.
        /// These force the bot to pay money or lose assets.
        /// </summary>
        public int PaymentThreatsRemaining =>
            DebtCollectorsRemaining + BirthdaysRemaining + WildRentRemaining;

        /// <summary>
        /// Probability that a specific opponent (with the given hand size) holds
        /// at least one of the specified threat cards. Uses hypergeometric model.
        /// </summary>
        public double ProbabilityOpponentHolds(int threatsRemaining, int opponentHandSize)
        {
            return BoardAnalyzer.EstimateHeldProbability(opponentHandSize, threatsRemaining, UnknownPoolSize);
        }

        /// <summary>
        /// Probability that ANY opponent holds at least one of the specified
        /// threat cards. Computed as 1 - product(1 - P(each opponent holds one)).
        /// </summary>
        public double ProbabilityAnyOpponentHolds(int threatsRemaining, IEnumerable<int> opponentHandSizes)
        {
            double probNone = 1.0;
            foreach (int handSize in opponentHandSizes)
            {
                probNone *= 1.0 - ProbabilityOpponentHolds(threatsRemaining, handSize);
            }
            return Math.Max(0.0, Math.Min(1.0, 1.0 - probNone));
        }
    }

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
        /// Estimates the probability that a specific opponent holds at least one
        /// card of a given type, using the hypergeometric distribution.
        /// 
        /// This is the generalized version — works for any card type, not just JSN.
        ///   P(≥1 in hand) = 1 − P(0 in hand)
        ///   P(0) = C(unknownCards − threatsInUnknown, opponentHandSize) / C(unknownCards, opponentHandSize)
        /// 
        /// Returns 0 when threatsInUnknown is 0 (certain: no threats possible).
        /// Returns 1 when the opponent's hand is as large as the unknown pool.
        /// </summary>
        public static double EstimateHeldProbability(int opponentHandSize, int threatsInUnknown, int unknownCards)
        {
            if (threatsInUnknown <= 0 || unknownCards <= 0 || opponentHandSize <= 0)
                return 0.0;
            if (opponentHandSize >= unknownCards)
                return threatsInUnknown > 0 ? 1.0 : 0.0;

            // P(0 threats in hand of size k, drawn from pool of N with T threats)
            // = product_{i=0}^{k-1} (N - T - i) / (N - i)
            double probNone = 1.0;
            for (int i = 0; i < opponentHandSize; i++)
            {
                double denom = unknownCards - i;
                if (denom <= 0) break;
                double numerator = Math.Max(0.0, unknownCards - threatsInUnknown - i);
                probNone *= numerator / denom;
            }
            return Math.Max(0.0, Math.Min(1.0, 1.0 - probNone));
        }

        /// <summary>
        /// Estimates the probability that a specific opponent holds at least one
        /// Just Say No card. Wraps the generalized EstimateHeldProbability for
        /// backward compatibility.
        /// </summary>
        public static double EstimateJsnHeldProbability(int opponentHandSize, int jsnInUnknown, int unknownCards)
        {
            return EstimateHeldProbability(opponentHandSize, jsnInUnknown, unknownCards);
        }

        // =====================================================================
        // ThreatProfile builders
        // =====================================================================

        /// <summary>
        /// Build a ThreatProfile from real game objects. Counts cards accounted
        /// for in the discard pile and bot's hand, subtracts from deck totals
        /// to determine how many threats remain in the unknown pool.
        /// </summary>
        public static ThreatProfile BuildThreatProfile(Player bot, List<Player> allPlayers, List<Card>? discardPile)
        {
            var profile = new ThreatProfile();

            // Count cards we can see: bot's hand + discard pile
            var accountedFor = new List<Card>(bot.Hand);
            if (discardPile != null)
                accountedFor.AddRange(discardPile);

            // Subtract accounted-for cards from deck totals
            profile.DealBreakersRemaining = ThreatProfile.TotalDealBreakers
                - accountedFor.Count(c => c.ActionKind == ActionType.DealBreaker);
            profile.SlyDealsRemaining = ThreatProfile.TotalSlyDeals
                - accountedFor.Count(c => c.ActionKind == ActionType.SlyDeal);
            profile.ForceDealsRemaining = ThreatProfile.TotalForceDeals
                - accountedFor.Count(c => c.ActionKind == ActionType.ForceDeal);
            profile.DebtCollectorsRemaining = ThreatProfile.TotalDebtCollectors
                - accountedFor.Count(c => c.ActionKind == ActionType.DebtCollector);
            profile.BirthdaysRemaining = ThreatProfile.TotalBirthdays
                - accountedFor.Count(c => c.ActionKind == ActionType.ItsMyBirthday);
            profile.JsnRemaining = ThreatProfile.TotalJsn
                - accountedFor.Count(c => c.ActionKind == ActionType.JustSayNo);
            profile.DtrRemaining = ThreatProfile.TotalDtr
                - accountedFor.Count(c => c.ActionKind == ActionType.DoubleTheRent);
            profile.WildRentRemaining = ThreatProfile.TotalWildRent
                - accountedFor.Count(c => c.IsWildRent);

            // Clamp to zero (shouldn't go negative but be safe)
            profile.DealBreakersRemaining = Math.Max(0, profile.DealBreakersRemaining);
            profile.SlyDealsRemaining = Math.Max(0, profile.SlyDealsRemaining);
            profile.ForceDealsRemaining = Math.Max(0, profile.ForceDealsRemaining);
            profile.DebtCollectorsRemaining = Math.Max(0, profile.DebtCollectorsRemaining);
            profile.BirthdaysRemaining = Math.Max(0, profile.BirthdaysRemaining);
            profile.JsnRemaining = Math.Max(0, profile.JsnRemaining);
            profile.DtrRemaining = Math.Max(0, profile.DtrRemaining);
            profile.WildRentRemaining = Math.Max(0, profile.WildRentRemaining);

            // Unknown pool = total cards minus everything visible
            int totalVisible = bot.Hand.Count
                + allPlayers.SelectMany(p => p.Bank).Count()
                + allPlayers.SelectMany(p => p.PropertySets).SelectMany(s => s.Cards).Count()
                + allPlayers.SelectMany(p => p.UnboundWilds).Count()
                + (discardPile?.Count ?? 0);
            profile.UnknownPoolSize = Math.Max(1, ThreatProfile.TotalCards - totalVisible);

            return profile;
        }

        /// <summary>
        /// Build a ThreatProfile from SimulationState (for use during ISMCTS).
        /// Same logic as BuildThreatProfile but reads from sim objects.
        /// </summary>
        public static ThreatProfile BuildThreatProfileSim(SimulationState state, int botIndex)
        {
            var profile = new ThreatProfile();
            var botPlayer = state.Players[botIndex];

            // Cards accounted for: bot hand + discard pile in sim deck
            var accountedFor = new List<Card>(botPlayer.Hand);
            accountedFor.AddRange(state.Deck.DiscardPile);

            profile.DealBreakersRemaining = ThreatProfile.TotalDealBreakers
                - accountedFor.Count(c => c.ActionKind == ActionType.DealBreaker);
            profile.SlyDealsRemaining = ThreatProfile.TotalSlyDeals
                - accountedFor.Count(c => c.ActionKind == ActionType.SlyDeal);
            profile.ForceDealsRemaining = ThreatProfile.TotalForceDeals
                - accountedFor.Count(c => c.ActionKind == ActionType.ForceDeal);
            profile.DebtCollectorsRemaining = ThreatProfile.TotalDebtCollectors
                - accountedFor.Count(c => c.ActionKind == ActionType.DebtCollector);
            profile.BirthdaysRemaining = ThreatProfile.TotalBirthdays
                - accountedFor.Count(c => c.ActionKind == ActionType.ItsMyBirthday);
            profile.JsnRemaining = ThreatProfile.TotalJsn
                - accountedFor.Count(c => c.ActionKind == ActionType.JustSayNo);
            profile.DtrRemaining = ThreatProfile.TotalDtr
                - accountedFor.Count(c => c.ActionKind == ActionType.DoubleTheRent);
            profile.WildRentRemaining = ThreatProfile.TotalWildRent
                - accountedFor.Count(c => c.IsWildRent);

            // Clamp
            profile.DealBreakersRemaining = Math.Max(0, profile.DealBreakersRemaining);
            profile.SlyDealsRemaining = Math.Max(0, profile.SlyDealsRemaining);
            profile.ForceDealsRemaining = Math.Max(0, profile.ForceDealsRemaining);
            profile.DebtCollectorsRemaining = Math.Max(0, profile.DebtCollectorsRemaining);
            profile.BirthdaysRemaining = Math.Max(0, profile.BirthdaysRemaining);
            profile.JsnRemaining = Math.Max(0, profile.JsnRemaining);
            profile.DtrRemaining = Math.Max(0, profile.DtrRemaining);
            profile.WildRentRemaining = Math.Max(0, profile.WildRentRemaining);

            // Unknown pool
            int totalVisible = botPlayer.Hand.Count
                + state.Players.Sum(p => p.Bank.Count)
                + state.Players.Sum(p => p.PropertySets.Sum(s => s.Cards.Count))
                + state.Deck.DiscardPile.Count;
            profile.UnknownPoolSize = Math.Max(1, ThreatProfile.TotalCards - totalVisible);

            return profile;
        }
    }
}
