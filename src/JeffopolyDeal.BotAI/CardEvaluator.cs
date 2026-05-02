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
    /// </summary>
    public static class CardEvaluator
    {
        /// <summary>
        /// Target bank balance that provides a comfortable buffer against rent.
        /// Typical rent charges are 1-8M, so 5M covers most single charges.
        /// </summary>
        private const int RentBufferTarget = 5;

        public static int? PlayScore(Player bot, Card card, List<Player> allPlayers, int playsRemaining)
        {
            // Cards that should never be proactively played
            if (card.ActionKind == ActionType.JustSayNo) return null;
            if (card.ActionKind == ActionType.DoubleTheRent) return null;

            int bankTotal = bot.BankTotal;

            // How exposed are we? If bank is below the rent buffer target,
            // we're vulnerable — money is worth more, loose properties worth less.
            bool bankIsLow = bankTotal < RentBufferTarget;

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
                        int deficit = RentBufferTarget - bankTotal;
                        return 20 + Math.Min(25, deficit * 5);
                    }
                    return 20;

                case CardType.Property:
                {
                    int propScore = 30;
                    var targetSet = bot.PropertySets.FirstOrDefault(s =>
                        s.Color == card.Color && !s.IsComplete);
                    if (targetSet != null && targetSet.Size >= targetSet.RequiredSize - 1)
                    {
                        // Completing a set is always high priority — complete sets
                        // can't be stolen with Sly Deal and are worth the risk
                        propScore += 40;
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
                    if (completesASet) return 70;
                    if (bankIsLow && maxOpponentRent > bankTotal) return 15;
                    return 35;
                }

                case CardType.Rent:
                {
                    int rentAmount = GetBestRentAmount(bot, card);
                    if (rentAmount == 0) return 10;
                    return 50 + rentAmount * 5;
                }

                case CardType.Action:
                    return ScoreAction(bot, card, allPlayers, playsRemaining);
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

        private static int ScoreAction(Player bot, Card card, List<Player> allPlayers, int playsRemaining)
        {
            var others = allPlayers.Where(p => p.ConnectionId != bot.ConnectionId).ToList();

            switch (card.ActionKind)
            {
                case ActionType.PassGo:
                    return playsRemaining >= 2 ? 80 : 40;

                case ActionType.DealBreaker:
                    if (others.Any(p => p.GetCompletePropertySets().Count > 0))
                    {
                        if (BoardAnalyzer.OpponentNearWin(bot, allPlayers))
                            return 200;
                        return 90;
                    }
                    return 10;

                case ActionType.DebtCollector:
                    return 55;

                case ActionType.ItsMyBirthday:
                    return 45;

                case ActionType.SlyDeal:
                    if (others.Any(p => p.GetStealableProperties().Count > 0))
                        return 60;
                    return 10;

                case ActionType.ForceDeal:
                    if (bot.GetStealableProperties().Count > 0 &&
                        others.Any(p => p.GetStealableProperties().Count > 0))
                        return 55;
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
    }
}
