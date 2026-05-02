using JeffopolyDeal.Models;

namespace JeffopolyDeal
{
    /// <summary>
    /// Optimal payment selection using subset-sum approach.
    /// </summary>
    public static class PaymentSolver
    {
        /// <summary>
        /// Find the optimal set of cards to pay a debt.
        /// Minimizes: 1) overpayment 2) strategic loss 3) card count.
        /// Prefers bank cards over properties, protects near-complete sets.
        /// Also avoids paying property cards that would complete a winning set for the receiver.
        /// </summary>
        /// <param name="bot">The bot that is paying.</param>
        /// <param name="amountOwed">Amount that must be paid.</param>
        /// <param name="receiver">The player receiving the payment (used to avoid helping them win).</param>
        public static List<Card> FindOptimalPayment(Player bot, int amountOwed, Player? receiver = null)
        {
            var payable = bot.GetPayableCards()
                .Where(c => !c.IsMulticolorWild)
                .ToList();

            int totalAssets = payable.Sum(c => c.MoneyValue);

            // If insolvent, must pay everything — no choice even if it helps receiver
            if (totalAssets <= amountOwed)
                return payable;

            var scored = payable.Select(c => new ScoredCard
            {
                Card = c,
                StrategicValue = CardStrategicValue(bot, c, receiver)
            }).ToList();

            if (payable.Count <= 15)
                return ExactMinOverpay(scored, amountOwed);
            else
                return GreedyPayment(scored, amountOwed);
        }

        /// <summary>
        /// Strategic value of a card (higher = more worth keeping).
        /// Cards that would give the receiver a game-winning complete set are assigned
        /// maximum value so they are avoided unless there is no other choice.
        /// </summary>
        internal static int CardStrategicValue(Player bot, Card card, Player? receiver = null)
        {
            // Bank money: always sacrifice first
            if (bot.Bank.Contains(card)) return 0;

            // Never voluntarily pay a card that would complete a winning set for the receiver.
            // Assign it the highest possible strategic value so it is only paid as a last resort.
            if (receiver != null && WouldGiveReceiverWin(receiver, card))
                return 1000;

            var set = bot.PropertySets.FirstOrDefault(s => s.Cards.Contains(card));
            if (set == null) return 5;
            if (set.IsComplete) return 50 + set.CalculateRent();
            if (set.Size >= set.RequiredSize - 1) return 30;
            return 10 + set.Size;
        }

        /// <summary>
        /// Returns true if paying this property card to <paramref name="receiver"/> would complete
        /// a set and give them enough unique complete sets to win the game.
        ///
        /// The check considers that the receiver can rearrange their existing property wildcards:
        /// a dual-color wildcard currently on one side could be flipped to the other, so both
        /// colors of the wildcard are treated as potential contributors to each respective set.
        /// </summary>
        private static bool WouldGiveReceiverWin(Player receiver, Card card)
        {
            // Only a concern if receiver is one set away from winning
            if (receiver.UniqueCompletedSetCount < GameConfig.SetsToWin - 1)
                return false;

            // Determine which colors this card could be placed in
            foreach (var color in GetCardColors(card))
            {
                // Check if receiver already has a set of this color that needs exactly one more card
                foreach (var set in receiver.PropertySets)
                {
                    if (set.Color != color || set.IsComplete) continue;

                    // Count cards the receiver has for this color, including rearrangeable wildcards
                    int countForColor = CountCardsForColor(receiver, color);
                    if (countForColor >= set.RequiredSize - 1)
                        return true; // giving this card completes the set → receiver wins
                }
            }
            return false;
        }

        /// <summary>
        /// Returns the colors that a card can be placed in.
        /// For regular properties this is just the card's own color.
        /// For dual-color wildcards it is both sides.
        /// For multi-color wildcards all property colors are considered.
        /// </summary>
        private static IEnumerable<PropertyColor> GetCardColors(Card card)
        {
            if (card.CardType == CardType.Property && card.Color.HasValue)
                return new[] { card.Color.Value };

            if (card.CardType == CardType.PropertyWildcard)
            {
                if (card.IsMulticolorWild)
                    return System.Enum.GetValues<PropertyColor>();

                var colors = new List<PropertyColor>();
                if (card.Color.HasValue) colors.Add(card.Color.Value);
                if (card.AltColor.HasValue) colors.Add(card.AltColor.Value);
                return colors;
            }

            return System.Linq.Enumerable.Empty<PropertyColor>();
        }

        /// <summary>
        /// Count how many of the receiver's current property cards can contribute to the given color.
        /// Fixed properties count only for their own color.
        /// Dual-color wildcards count for EITHER of their colors (receiver can flip them).
        /// Multi-color wildcards (unbound wilds) count for any color.
        /// </summary>
        private static int CountCardsForColor(Player receiver, PropertyColor color)
        {
            int count = 0;
            foreach (var set in receiver.PropertySets)
            {
                foreach (var c in set.Cards)
                {
                    if (c.CardType == CardType.Property && c.Color == color)
                        count++;
                    else if (c.CardType == CardType.PropertyWildcard && !c.IsMulticolorWild)
                    {
                        // Dual-color wild: receiver can flip to either side
                        if (c.Color == color || c.AltColor == color)
                            count++;
                    }
                }
            }
            // Unbound multi-color wilds can be placed anywhere
            count += receiver.UnboundWilds.Count;
            return count;
        }

        private static List<Card> ExactMinOverpay(List<ScoredCard> scored, int amountOwed)
        {
            // First pass: try to find a combination using only cards that would NOT give the
            // receiver a game-winning set (strategic value < 1000). Accepting slight overpayment
            // is better than handing the receiver a winning complete set.
            var nonCritical = scored.Where(s => s.StrategicValue < 1000).ToList();
            if (nonCritical.Sum(s => s.Card.MoneyValue) >= amountOwed)
            {
                var result = RunExactMinOverpay(nonCritical, amountOwed);
                if (result != null) return result;
            }

            // Fallback: we cannot cover the debt without game-winning cards — use all cards.
            return RunExactMinOverpay(scored, amountOwed) ?? scored.Select(s => s.Card).ToList();
        }

        private static List<Card>? RunExactMinOverpay(List<ScoredCard> scored, int amountOwed)
        {
            int n = scored.Count;
            List<Card>? bestCombo = null;
            int bestOverpay = int.MaxValue;
            int bestStrategicCost = int.MaxValue;
            int bestCardCount = int.MaxValue;

            // Bitmask iteration for small N (up to 2^15 = 32768)
            int limit = 1 << n;
            for (int mask = 1; mask < limit; mask++)
            {
                int total = 0;
                int strategicCost = 0;
                int cardCount = 0;

                for (int i = 0; i < n; i++)
                {
                    if ((mask & (1 << i)) != 0)
                    {
                        total += scored[i].Card.MoneyValue;
                        strategicCost += scored[i].StrategicValue;
                        cardCount++;
                    }
                }

                if (total < amountOwed) continue;

                int overpay = total - amountOwed;
                bool isBetter = overpay < bestOverpay ||
                    (overpay == bestOverpay && strategicCost < bestStrategicCost) ||
                    (overpay == bestOverpay && strategicCost == bestStrategicCost && cardCount < bestCardCount);

                if (isBetter)
                {
                    bestOverpay = overpay;
                    bestStrategicCost = strategicCost;
                    bestCardCount = cardCount;
                    bestCombo = new List<Card>();
                    for (int i = 0; i < n; i++)
                    {
                        if ((mask & (1 << i)) != 0)
                            bestCombo.Add(scored[i].Card);
                    }
                }
            }

            return bestCombo;
        }

        private static List<Card> GreedyPayment(List<ScoredCard> scored, int amountOwed)
        {
            // First try with non-critical cards (avoid handing receiver a winning set).
            var nonCritical = scored.Where(s => s.StrategicValue < 1000).ToList();
            if (nonCritical.Sum(s => s.Card.MoneyValue) >= amountOwed)
                return RunGreedy(nonCritical, amountOwed);

            // Must include game-winning cards — insolvent with respect to safe cards.
            return RunGreedy(scored, amountOwed);
        }

        private static List<Card> RunGreedy(List<ScoredCard> scored, int amountOwed)
        {
            // Sort ascending by strategic value (sacrifice cheap/bank first)
            var sorted = scored.OrderBy(s => s.StrategicValue).ThenBy(s => s.Card.MoneyValue).ToList();
            var result = new List<Card>();
            int running = 0;

            foreach (var s in sorted)
            {
                result.Add(s.Card);
                running += s.Card.MoneyValue;
                if (running >= amountOwed) break;
            }

            return result;
        }

        private class ScoredCard
        {
            public Card Card { get; set; } = null!;
            public int StrategicValue { get; set; }
        }
    }
}
