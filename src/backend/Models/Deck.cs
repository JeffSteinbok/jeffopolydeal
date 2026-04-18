using System;
using System.Collections.Generic;
using System.Linq;

namespace JeffopolyDeal.Models
{
    /// <summary>
    /// Builds and manages the 106-card playable deck (110 minus 4 rule cards).
    /// </summary>
    public class Deck
    {
        private readonly List<Card> _drawPile = new();
        private readonly List<Card> _discardPile = new();
        private readonly Random _rng = new();
        private int _nextId = 1;

        public int DrawPileCount => _drawPile.Count;
        public int DiscardPileCount => _discardPile.Count;
        public Card? TopDiscard => _discardPile.Count > 0 ? _discardPile[^1] : null;

        /// <summary>Returns a snapshot of all cards in the draw pile (top of pile last).</summary>
        public List<Card> GetDrawPileSnapshot() => _drawPile.ToList();

        /// <summary>Returns a snapshot of all cards in the discard pile.</summary>
        public List<Card> GetDiscardPileSnapshot() => _discardPile.ToList();

        /// <summary>Create a specific card for test injection. Internal for testing.</summary>
        internal Card CreateCard(CardType type, int moneyValue = 0, string name = "Test Card",
            PropertyColor? color = null, PropertyColor? altColor = null,
            ActionType? actionKind = null, List<PropertyColor>? rentColors = null,
            bool isWildRent = false, bool isMulticolorWild = false, string? cardId = null)
        {
            return new Card
            {
                Id = _nextId++,
                CardId = cardId ?? $"test{_nextId}",
                CardType = type,
                MoneyValue = moneyValue,
                Name = name,
                Color = color,
                AltColor = altColor,
                ActiveColor = color,
                ActionKind = actionKind,
                RentColors = rentColors,
                IsWildRent = isWildRent,
                IsMulticolorWild = isMulticolorWild,
            };
        }

        /// <summary>Place a card on top of the draw pile. Internal for testing.</summary>
        internal void PlaceOnTop(Card card) => _drawPile.Add(card);

        // Counters for generating stable CardIds per type
        private readonly Dictionary<string, int> _cardIdCounters = new();

        private string NextCardId(string prefix)
        {
            if (!_cardIdCounters.ContainsKey(prefix))
                _cardIdCounters[prefix] = 0;
            _cardIdCounters[prefix]++;
            return $"{prefix}{_cardIdCounters[prefix]}";
        }

        public Deck()
        {
            BuildDeck();
            Shuffle();
        }

        /// <summary>Returns the full deck in build order (unshuffled). For test/debug pages.</summary>
        public static List<Card> GetOrderedDeck()
        {
            var deck = new Deck();
            // deck was shuffled in ctor, but _drawPile was built in order before shuffle
            // Rebuild without shuffle
            var ordered = new Deck(skipShuffle: true);
            return ordered._drawPile.ToList();
        }

        private Deck(bool skipShuffle)
        {
            BuildDeck();
            if (!skipShuffle) Shuffle();
        }

        public List<Card> Draw(int count)
        {
            var drawn = new List<Card>();
            for (int i = 0; i < count; i++)
            {
                if (_drawPile.Count == 0)
                {
                    ReshuffleDiscard();
                    if (_drawPile.Count == 0)
                        break; // No cards left anywhere
                }
                var card = _drawPile[^1];
                _drawPile.RemoveAt(_drawPile.Count - 1);
                drawn.Add(card);
            }
            return drawn;
        }

        public void Discard(Card card)
        {
            _discardPile.Add(card);
        }

        public void Shuffle()
        {
            // Fisher-Yates shuffle
            for (int i = _drawPile.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (_drawPile[i], _drawPile[j]) = (_drawPile[j], _drawPile[i]);
            }
        }

        private void ReshuffleDiscard()
        {
            _drawPile.AddRange(_discardPile);
            _discardPile.Clear();
            Shuffle();
        }

        private void BuildDeck()
        {
            // Money cards (20)
            AddMoney(1, 6);
            AddMoney(2, 5);
            AddMoney(3, 3);
            AddMoney(4, 3);
            AddMoney(5, 2);
            AddMoney(10, 1);

            // Property cards (28) — names come from PropertyNames registry
            foreach (var (color, defs) in PropertyNames.ByColor)
            {
                AddProperties(color, defs);
            }

            // Property wildcards (11)
            AddPropertyWildcard(PropertyColor.DarkBlue, PropertyColor.Green, 4, 1);
            AddPropertyWildcard(PropertyColor.Green, PropertyColor.Railroad, 4, 1);
            AddPropertyWildcard(PropertyColor.Utility, PropertyColor.Railroad, 2, 1);
            AddPropertyWildcard(PropertyColor.LightBlue, PropertyColor.Railroad, 4, 1);
            AddPropertyWildcard(PropertyColor.LightBlue, PropertyColor.Brown, 1, 1);
            AddPropertyWildcard(PropertyColor.Pink, PropertyColor.Orange, 2, 2);
            AddPropertyWildcard(PropertyColor.Red, PropertyColor.Yellow, 3, 2);
            // Multi-color wildcards (no monetary value)
            for (int i = 0; i < 2; i++)
            {
                _drawPile.Add(new Card
                {
                    Id = _nextId++,
                    CardId = NextCardId("wildall"),
                    CardType = CardType.PropertyWildcard,
                    MoneyValue = 0,
                    Name = "Multi-color Wildcard",
                    IsMulticolorWild = true,
                });
            }

            // Rent cards (13)
            AddRent(new[] { PropertyColor.DarkBlue, PropertyColor.Green }, 2);
            AddRent(new[] { PropertyColor.Red, PropertyColor.Yellow }, 2);
            AddRent(new[] { PropertyColor.Pink, PropertyColor.Orange }, 2);
            AddRent(new[] { PropertyColor.LightBlue, PropertyColor.Brown }, 2);
            AddRent(new[] { PropertyColor.Railroad, PropertyColor.Utility }, 2);
            // Wild rent (any color, targets one player)
            for (int i = 0; i < 3; i++)
            {
                _drawPile.Add(new Card
                {
                    Id = _nextId++,
                    CardId = NextCardId("wildrent"),
                    CardType = CardType.Rent,
                    MoneyValue = 3,
                    Name = "Wild Rent",
                    IsWildRent = true,
                });
            }

            // Action cards (34)
            AddAction(ActionType.PassGo, "Pass Go", 1, 10);
            AddAction(ActionType.DebtCollector, "Debt Collector", 3, 3);
            AddAction(ActionType.ItsMyBirthday, "It's My Birthday", 2, 3);
            AddAction(ActionType.SlyDeal, "Sly Deal", 3, 3);
            AddAction(ActionType.ForceDeal, "Force Deal", 3, 3);
            AddAction(ActionType.DealBreaker, "Deal Breaker", 5, 2);
            AddAction(ActionType.JustSayNo, "Just Say No", 4, 3);
            AddAction(ActionType.DoubleTheRent, "Double the Rent", 1, 2);
            AddAction(ActionType.House, "House", 3, 3);
            AddAction(ActionType.Hotel, "Hotel", 4, 2);
        }

        private void AddMoney(int value, int count)
        {
            for (int i = 0; i < count; i++)
            {
                _drawPile.Add(new Card
                {
                    Id = _nextId++,
                    CardId = NextCardId($"money{value}m"),
                    CardType = CardType.Money,
                    MoneyValue = value,
                    Name = $"{value}M",
                });
            }
        }

        private void AddProperties(PropertyColor color, PropertyDef[] defs)
        {
            int moneyValue = GameConfig.PropertyValue.TryGetValue(color, out var v) ? v : 0;
            foreach (var def in defs)
            {
                _drawPile.Add(new Card
                {
                    Id = _nextId++,
                    CardId = def.CardId,
                    CardType = CardType.Property,
                    MoneyValue = moneyValue,
                    Name = def.DisplayName,
                    Color = color,
                });
            }
        }

        private void AddPropertyWildcard(PropertyColor color1, PropertyColor color2, int moneyValue, int count)
        {
            var prefix = $"wild{color1.ToString().ToLower()}{color2.ToString().ToLower()}";
            for (int i = 0; i < count; i++)
            {
                _drawPile.Add(new Card
                {
                    Id = _nextId++,
                    CardId = NextCardId(prefix),
                    CardType = CardType.PropertyWildcard,
                    MoneyValue = moneyValue,
                    Name = $"{color1.DisplayName()}/{color2.DisplayName()} Wildcard",
                    Color = color1,
                    AltColor = color2,
                    ActiveColor = color1,
                });
            }
        }

        private void AddRent(PropertyColor[] colors, int count)
        {
            var prefix = $"rent{colors[0].ToString().ToLower()}{colors[1].ToString().ToLower()}";
            for (int i = 0; i < count; i++)
            {
                _drawPile.Add(new Card
                {
                    Id = _nextId++,
                    CardId = NextCardId(prefix),
                    CardType = CardType.Rent,
                    MoneyValue = 1,
                    Name = $"{colors[0].DisplayName()}/{colors[1].DisplayName()} Rent",
                    RentColors = colors.ToList(),
                });
            }
        }

        private void AddAction(ActionType actionType, string name, int moneyValue, int count)
        {
            var prefix = actionType.ToString().ToLower();
            for (int i = 0; i < count; i++)
            {
                _drawPile.Add(new Card
                {
                    Id = _nextId++,
                    CardId = NextCardId(prefix),
                    CardType = CardType.Action,
                    MoneyValue = moneyValue,
                    Name = name,
                    ActionKind = actionType,
                });
            }
        }
    }
}
