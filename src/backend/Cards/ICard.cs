using JeffopolyDeal.Models;
using System.Collections.Generic;
using System.Linq;

namespace JeffopolyDeal.Cards
{
    internal sealed class CardPlayabilityContext
    {
        public required Player Player { get; init; }
        public required IReadOnlyList<Player> Players { get; init; }
    }

    internal interface ICard
    {
        bool IsPlayable(CardPlayabilityContext context);
    }

    internal static class CardFactory
    {
        public static ICard Create(Card card)
        {
            return card.CardType switch
            {
                CardType.Money => new MoneyCard(),
                CardType.Property => new PropertyCard(),
                CardType.PropertyWildcard => new PropertyWildcardCard(),
                CardType.Rent => new RentCard(card),
                CardType.Action => new ActionCard(card),
                _ => new UnplayableCard(),
            };
        }
    }

    internal sealed class MoneyCard : ICard
    {
        public bool IsPlayable(CardPlayabilityContext context) => true;
    }

    internal sealed class PropertyCard : ICard
    {
        public bool IsPlayable(CardPlayabilityContext context) => true;
    }

    internal sealed class PropertyWildcardCard : ICard
    {
        public bool IsPlayable(CardPlayabilityContext context) => true;
    }

    internal sealed class RentCard : ICard
    {
        private readonly Card _card;

        public RentCard(Card card)
        {
            _card = card;
        }

        public bool IsPlayable(CardPlayabilityContext context)
        {
            var myColors = context.Player.PropertySets
                .Where(s => s.Cards.Count > 0)
                .Select(s => s.Color)
                .ToList();

            if (_card.IsWildRent) return myColors.Count > 0;
            return myColors.Any(c => _card.RentColors?.Contains(c) == true);
        }
    }

    internal sealed class ActionCard : ICard
    {
        private readonly Card _card;

        public ActionCard(Card card)
        {
            _card = card;
        }

        public bool IsPlayable(CardPlayabilityContext context)
        {
            if (_card.ActionKind == null) return false;

            var otherPlayers = context.Players
                .Where(p => p.ConnectionId != context.Player.ConnectionId)
                .ToList();

            switch (_card.ActionKind)
            {
                case ActionType.PassGo:
                    return true;
                case ActionType.ItsMyBirthday:
                case ActionType.DebtCollector:
                    return otherPlayers.Count > 0;
                case ActionType.SlyDeal:
                    return otherPlayers.Any(p => p.GetStealableProperties().Count > 0);
                case ActionType.ForceDeal:
                    return context.Player.GetStealableProperties().Count > 0 &&
                           otherPlayers.Any(p => p.GetStealableProperties().Count > 0);
                case ActionType.DealBreaker:
                    return otherPlayers.Any(p => p.GetCompletePropertySets().Count > 0);
                case ActionType.House:
                    return context.Player.PropertySets.Any(s =>
                        IsEligibleForBuilding(s) &&
                        !s.HasHouse &&
                        !s.HasHotel);
                case ActionType.Hotel:
                    return context.Player.PropertySets.Any(s =>
                        IsEligibleForBuilding(s) &&
                        s.HasHouse &&
                        !s.HasHotel);
                case ActionType.JustSayNo:
                case ActionType.DoubleTheRent:
                    return false;
                default:
                    return false;
            }
        }

        private static bool IsEligibleForBuilding(PropertySet set)
        {
            return set.IsComplete &&
                   set.Color != PropertyColor.Railroad &&
                   set.Color != PropertyColor.Utility;
        }
    }

    internal sealed class UnplayableCard : ICard
    {
        public bool IsPlayable(CardPlayabilityContext context) => false;
    }
}
