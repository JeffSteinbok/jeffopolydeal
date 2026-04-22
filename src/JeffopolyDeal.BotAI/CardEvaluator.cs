using JeffopolyDeal.Models;

namespace JeffopolyDeal
{
    /// <summary>
    /// Scores cards in hand for play priority.
    /// Higher = play first. Returns null if card shouldn't be played.
    /// </summary>
    public static class CardEvaluator
    {
        public static int? PlayScore(Player bot, Card card, List<Player> allPlayers, int playsRemaining)
        {
            // Cards that should never be proactively played
            if (card.ActionKind == ActionType.JustSayNo) return null;
            if (card.ActionKind == ActionType.DoubleTheRent) return null;

            switch (card.CardType)
            {
                case CardType.Money:
                    return 20;

                case CardType.Property:
                {
                    int propScore = 30;
                    var targetSet = bot.PropertySets.FirstOrDefault(s =>
                        s.Color == card.Color && !s.IsComplete);
                    if (targetSet != null && targetSet.Size >= targetSet.RequiredSize - 1)
                        propScore += 40;
                    return propScore;
                }

                case CardType.PropertyWildcard:
                    return 35;

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
    }
}
