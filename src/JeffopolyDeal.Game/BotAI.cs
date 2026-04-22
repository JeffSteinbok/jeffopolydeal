using JeffopolyDeal.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JeffopolyDeal
{
    /// <summary>
    /// Simple bot AI that makes random valid moves. Not smart — just keeps the game moving.
    /// </summary>
    public static class BotAI
    {
        private static readonly Random _rng = new();

        public static bool IsBot(string connectionId) => connectionId.StartsWith("bot-");

        /// <summary>
        /// Plays a full bot turn: draw, play up to 3 cards, discard, end turn.
        /// Mutates game state directly (caller holds the lock).
        /// Returns the list of actions taken for logging.
        /// </summary>
        public static void PlayTurn(Player bot, List<Player> allPlayers, Deck deck, Func<Player, Card, PlayCardRequest, bool> playCard, int maxPlays)
        {
            // Play up to maxPlays cards
            int plays = 0;
            while (plays < maxPlays && bot.Hand.Count > 0)
            {
                var card = PickCardToPlay(bot, allPlayers);
                if (card == null) break;

                var request = BuildRequest(bot, card, allPlayers);
                if (request == null)
                {
                    // Can't build a valid request — try banking it
                    request = new PlayCardRequest { PlayAsMoney = true };
                }

                bool shouldContinue = playCard(bot, card, request);
                plays++;
                if (!shouldContinue) break;
            }
        }

        /// <summary>
        /// Pick a card to play, preferring simple plays.
        /// </summary>
        private static Card? PickCardToPlay(Player bot, List<Player> allPlayers)
        {
            // Priority: Money > Property > PassGo > Rent > other actions
            var money = bot.Hand.FirstOrDefault(c => c.CardType == CardType.Money);
            if (money != null) return money;

            var prop = bot.Hand.FirstOrDefault(c => c.CardType == CardType.Property);
            if (prop != null) return prop;

            var wildcard = bot.Hand.FirstOrDefault(c => c.CardType == CardType.PropertyWildcard);
            if (wildcard != null) return wildcard;

            var passGo = bot.Hand.FirstOrDefault(c => c.ActionKind == ActionType.PassGo);
            if (passGo != null) return passGo;

            var rent = bot.Hand.FirstOrDefault(c => c.CardType == CardType.Rent && HasPropertiesForRent(bot, c));
            if (rent != null) return rent;

            // Try action cards (skip JSN and DTR — they're reactive)
            var action = bot.Hand.FirstOrDefault(c =>
                c.CardType == CardType.Action &&
                c.ActionKind != ActionType.JustSayNo &&
                c.ActionKind != ActionType.DoubleTheRent &&
                c.ActionKind != ActionType.House &&
                c.ActionKind != ActionType.Hotel);
            if (action != null) return action;

            // House/Hotel if we have a complete set
            var house = bot.Hand.FirstOrDefault(c => c.ActionKind == ActionType.House);
            if (house != null && bot.PropertySets.Any(s => s.IsComplete && !s.HasHouse
                && s.Color != PropertyColor.Railroad && s.Color != PropertyColor.Utility))
                return house;

            var hotel = bot.Hand.FirstOrDefault(c => c.ActionKind == ActionType.Hotel);
            if (hotel != null && bot.PropertySets.Any(s => s.IsComplete && s.HasHouse && !s.HasHotel
                && s.Color != PropertyColor.Railroad && s.Color != PropertyColor.Utility))
                return hotel;

            // Bank anything remaining
            return bot.Hand.FirstOrDefault(c =>
                c.ActionKind != ActionType.JustSayNo); // keep JSN for defense
        }

        private static bool HasPropertiesForRent(Player bot, Card rentCard)
        {
            if (rentCard.IsWildRent)
                return bot.PropertySets.Any(s => s.Cards.Count > 0);

            if (rentCard.RentColors == null) return false;
            return rentCard.RentColors.Any(color =>
                bot.PropertySets.Any(s => s.Color == color && s.Cards.Count > 0));
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
                        // Pick a color we have properties in, or random
                        var color = bot.PropertySets.FirstOrDefault()?.Color ?? PropertyColor.Brown;
                        return new PlayCardRequest { PlayAsMoney = false, WildcardColor = color };
                    }
                    return new PlayCardRequest { PlayAsMoney = false, WildcardColor = card.Color };

                case CardType.Rent:
                    return BuildRentRequest(bot, card, others);

                case CardType.Action:
                    return BuildActionRequest(bot, card, others);

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
                    .OrderByDescending(s => s.CalculateRent())
                    .FirstOrDefault();
                if (bestSet == null) return new PlayCardRequest { PlayAsMoney = true };
                rentColor = bestSet.Color;
                var target = others[_rng.Next(others.Count)];
                return new PlayCardRequest
                {
                    PlayAsMoney = false,
                    RentColor = rentColor,
                    TargetPlayerId = target.ConnectionId,
                };
            }

            // Standard rent — pick the color we own
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

        private static PlayCardRequest? BuildActionRequest(Player bot, Card card, List<Player> others)
        {
            if (others.Count == 0) return new PlayCardRequest { PlayAsMoney = true };

            switch (card.ActionKind)
            {
                case ActionType.PassGo:
                    return new PlayCardRequest { PlayAsMoney = false };

                case ActionType.DebtCollector:
                {
                    var target = PickRichestTarget(others);
                    return new PlayCardRequest { PlayAsMoney = false, TargetPlayerId = target.ConnectionId };
                }

                case ActionType.ItsMyBirthday:
                    return new PlayCardRequest { PlayAsMoney = false };

                case ActionType.SlyDeal:
                {
                    // Find a target with stealable properties
                    foreach (var target in others.OrderBy(_ => _rng.Next()))
                    {
                        var stealable = target.GetStealableProperties();
                        if (stealable.Count > 0)
                        {
                            var steal = stealable[_rng.Next(stealable.Count)];
                            return new PlayCardRequest
                            {
                                PlayAsMoney = false,
                                TargetPlayerId = target.ConnectionId,
                                TargetCardId = steal.Id,
                            };
                        }
                    }
                    return new PlayCardRequest { PlayAsMoney = true }; // no valid target
                }

                case ActionType.ForceDeal:
                {
                    var myStealable = bot.GetStealableProperties();
                    if (myStealable.Count == 0) return new PlayCardRequest { PlayAsMoney = true };

                    foreach (var target in others.OrderBy(_ => _rng.Next()))
                    {
                        var theirStealable = target.GetStealableProperties();
                        if (theirStealable.Count > 0)
                        {
                            // Offer our cheapest, take their best
                            var offer = myStealable.OrderBy(c => c.MoneyValue).First();
                            var take = theirStealable[_rng.Next(theirStealable.Count)];
                            return new PlayCardRequest
                            {
                                PlayAsMoney = false,
                                TargetPlayerId = target.ConnectionId,
                                TargetCardId = take.Id,
                                OfferedCardId = offer.Id,
                            };
                        }
                    }
                    return new PlayCardRequest { PlayAsMoney = true };
                }

                case ActionType.DealBreaker:
                {
                    foreach (var target in others.OrderBy(_ => _rng.Next()))
                    {
                        var complete = target.GetCompletePropertySets();
                        if (complete.Count > 0)
                        {
                            var set = complete[_rng.Next(complete.Count)];
                            return new PlayCardRequest
                            {
                                PlayAsMoney = false,
                                TargetPlayerId = target.ConnectionId,
                                TargetSetColor = set.Color,
                            };
                        }
                    }
                    return new PlayCardRequest { PlayAsMoney = true };
                }

                case ActionType.House:
                {
                    var set = bot.PropertySets.FirstOrDefault(s => s.IsComplete && !s.HasHouse
                        && s.Color != PropertyColor.Railroad && s.Color != PropertyColor.Utility);
                    if (set == null) return new PlayCardRequest { PlayAsMoney = true };
                    return new PlayCardRequest { PlayAsMoney = false, TargetSetColor = set.Color };
                }

                case ActionType.Hotel:
                {
                    var set = bot.PropertySets.FirstOrDefault(s => s.IsComplete && s.HasHouse && !s.HasHotel
                        && s.Color != PropertyColor.Railroad && s.Color != PropertyColor.Utility);
                    if (set == null) return new PlayCardRequest { PlayAsMoney = true };
                    return new PlayCardRequest { PlayAsMoney = false, TargetSetColor = set.Color };
                }

                default:
                    return new PlayCardRequest { PlayAsMoney = true };
            }
        }

        /// <summary>
        /// Auto-respond to a pending action (pay or JSN).
        /// </summary>
        public static ActionResponse BuildResponse(Player bot)
        {
            // 30% chance to play JSN if we have one
            var jsn = bot.Hand.FirstOrDefault(c => c.ActionKind == ActionType.JustSayNo);
            if (jsn != null && _rng.Next(100) < 30)
            {
                return new ActionResponse { PlayJustSayNo = true };
            }

            // Pay with cheapest cards from bank first, then properties
            var payable = bot.GetPayableCards()
                .Where(c => !c.IsMulticolorWild) // can't pay with multi-color wild
                .OrderBy(c => c.MoneyValue)
                .ToList();

            // Just pay with whatever we have (up to a few cards)
            var payment = payable.Take(Math.Min(payable.Count, 5)).Select(c => c.Id).ToList();
            return new ActionResponse { PlayJustSayNo = false, PaymentCardIds = payment };
        }

        /// <summary>
        /// Pick cards to discard down to max hand size.
        /// </summary>
        public static List<int> PickDiscards(Player bot, int maxHandSize)
        {
            int excess = bot.Hand.Count - maxHandSize;
            if (excess <= 0) return new List<int>();

            // Discard lowest-value cards first, keep JSN and rent
            return bot.Hand
                .OrderBy(c => c.ActionKind == ActionType.JustSayNo ? 100 : 0)
                .ThenBy(c => c.CardType == CardType.Rent ? 50 : 0)
                .ThenBy(c => c.MoneyValue)
                .Take(excess)
                .Select(c => c.Id)
                .ToList();
        }

        private static Player PickRichestTarget(List<Player> others)
        {
            return others.OrderByDescending(p => p.BankTotal + p.PropertySets.Sum(s => s.Cards.Count)).First();
        }
    }
}
