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
        /// </summary>
        public static List<Card> FindOptimalPayment(Player bot, int amountOwed)
        {
            var payable = bot.GetPayableCards()
                .Where(c => !c.IsMulticolorWild)
                .ToList();

            int totalAssets = payable.Sum(c => c.MoneyValue);

            // If insolvent, must pay everything
            if (totalAssets <= amountOwed)
                return payable;

            var scored = payable.Select(c => new ScoredCard
            {
                Card = c,
                StrategicValue = CardStrategicValue(bot, c)
            }).ToList();

            if (payable.Count <= 15)
                return ExactMinOverpay(scored, amountOwed);
            else
                return GreedyPayment(scored, amountOwed);
        }

        /// <summary>
        /// Strategic value of a card (higher = more worth keeping).
        /// </summary>
        internal static int CardStrategicValue(Player bot, Card card)
        {
            // Bank money: always sacrifice first
            if (bot.Bank.Contains(card)) return 0;

            var set = bot.PropertySets.FirstOrDefault(s => s.Cards.Contains(card));
            if (set == null) return 5;
            if (set.IsComplete) return 50 + set.CalculateRent();
            if (set.Size >= set.RequiredSize - 1) return 30;
            return 10 + set.Size;
        }

        private static List<Card> ExactMinOverpay(List<ScoredCard> scored, int amountOwed)
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

            return bestCombo ?? scored.Select(s => s.Card).ToList();
        }

        private static List<Card> GreedyPayment(List<ScoredCard> scored, int amountOwed)
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
