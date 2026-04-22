using JeffopolyDeal;
using JeffopolyDeal.Models;

namespace JeffopolyDeal.BotAI.Tests
{
    public class CardEvaluatorExtendedTests
    {
        // ── Rent scoring ────────────────────────────────────────────────

        [Fact]
        public void PlayScore_RentScoreIncludesPropertyCountBonus()
        {
            var bot = CreatePlayer("bot-1");
            var other = CreatePlayer("p1");

            // 1 Brown property → rent = 1 → score = 50 + 1*5 = 55
            var set1 = new PropertySet { Color = PropertyColor.Brown };
            set1.Cards.Add(new Card { Id = 10, CardType = CardType.Property, Color = PropertyColor.Brown, MoneyValue = 1 });
            bot.PropertySets.Add(set1);

            var rent = new Card
            {
                Id = 1,
                CardType = CardType.Rent,
                RentColors = new List<PropertyColor> { PropertyColor.Brown, PropertyColor.LightBlue },
                MoneyValue = 1
            };
            var allPlayers = new List<Player> { bot, other };

            var score1 = CardEvaluator.PlayScore(bot, rent, allPlayers, 3);

            // Add second Brown → rent = 2 → score = 50 + 2*5 = 60
            set1.Cards.Add(new Card { Id = 11, CardType = CardType.Property, Color = PropertyColor.Brown, MoneyValue = 1 });

            var score2 = CardEvaluator.PlayScore(bot, rent, allPlayers, 3);

            Assert.NotNull(score1);
            Assert.NotNull(score2);
            Assert.True(score2 > score1);
        }

        [Fact]
        public void PlayScore_RentWithZeroPropertiesScoresLow()
        {
            var bot = CreatePlayer("bot-1");
            var other = CreatePlayer("p1");

            // No properties of matching colors
            var rent = new Card
            {
                Id = 1,
                CardType = CardType.Rent,
                RentColors = new List<PropertyColor> { PropertyColor.Green, PropertyColor.DarkBlue },
                MoneyValue = 1
            };
            var allPlayers = new List<Player> { bot, other };

            var score = CardEvaluator.PlayScore(bot, rent, allPlayers, 3);
            Assert.NotNull(score);
            Assert.Equal(10, score); // base score for 0 rent
        }

        // ── Action card scoring ─────────────────────────────────────────

        [Fact]
        public void PlayScore_SlyDealScoresHighWhenTargetsAvailable()
        {
            var bot = CreatePlayer("bot-1");
            var opponent = CreatePlayer("p1");

            var oppSet = new PropertySet { Color = PropertyColor.Red };
            oppSet.Cards.Add(new Card { Id = 10, CardType = CardType.Property, Color = PropertyColor.Red, MoneyValue = 3 });
            opponent.PropertySets.Add(oppSet);

            var slyDeal = new Card { Id = 1, CardType = CardType.Action, ActionKind = ActionType.SlyDeal, MoneyValue = 3 };
            var allPlayers = new List<Player> { bot, opponent };

            var score = CardEvaluator.PlayScore(bot, slyDeal, allPlayers, 3);
            Assert.NotNull(score);
            Assert.Equal(60, score);
        }

        [Fact]
        public void PlayScore_SlyDealScoresLowWhenNoTargets()
        {
            var bot = CreatePlayer("bot-1");
            var opponent = CreatePlayer("p1");
            // Opponent only has complete sets (not stealable)
            var completeSet = new PropertySet { Color = PropertyColor.Brown };
            completeSet.Cards.Add(new Card { Id = 10, CardType = CardType.Property, Color = PropertyColor.Brown, MoneyValue = 1 });
            completeSet.Cards.Add(new Card { Id = 11, CardType = CardType.Property, Color = PropertyColor.Brown, MoneyValue = 1 });
            opponent.PropertySets.Add(completeSet);

            var slyDeal = new Card { Id = 1, CardType = CardType.Action, ActionKind = ActionType.SlyDeal, MoneyValue = 3 };
            var allPlayers = new List<Player> { bot, opponent };

            var score = CardEvaluator.PlayScore(bot, slyDeal, allPlayers, 3);
            Assert.NotNull(score);
            Assert.Equal(10, score);
        }

        [Fact]
        public void PlayScore_ForceDealScoresHighWhenBothHaveProperties()
        {
            var bot = CreatePlayer("bot-1");
            var opponent = CreatePlayer("p1");

            // Bot has stealable property
            var botSet = new PropertySet { Color = PropertyColor.Brown };
            botSet.Cards.Add(new Card { Id = 10, CardType = CardType.Property, Color = PropertyColor.Brown, MoneyValue = 1 });
            bot.PropertySets.Add(botSet);

            // Opponent has stealable property
            var oppSet = new PropertySet { Color = PropertyColor.Red };
            oppSet.Cards.Add(new Card { Id = 11, CardType = CardType.Property, Color = PropertyColor.Red, MoneyValue = 3 });
            opponent.PropertySets.Add(oppSet);

            var forceDeal = new Card { Id = 1, CardType = CardType.Action, ActionKind = ActionType.ForceDeal, MoneyValue = 3 };
            var allPlayers = new List<Player> { bot, opponent };

            var score = CardEvaluator.PlayScore(bot, forceDeal, allPlayers, 3);
            Assert.NotNull(score);
            Assert.Equal(55, score);
        }

        [Fact]
        public void PlayScore_ForceDealScoresLowWhenBotHasNoProperties()
        {
            var bot = CreatePlayer("bot-1");
            var opponent = CreatePlayer("p1");

            var oppSet = new PropertySet { Color = PropertyColor.Red };
            oppSet.Cards.Add(new Card { Id = 10, CardType = CardType.Property, Color = PropertyColor.Red, MoneyValue = 3 });
            opponent.PropertySets.Add(oppSet);

            var forceDeal = new Card { Id = 1, CardType = CardType.Action, ActionKind = ActionType.ForceDeal, MoneyValue = 3 };
            var allPlayers = new List<Player> { bot, opponent };

            var score = CardEvaluator.PlayScore(bot, forceDeal, allPlayers, 3);
            Assert.NotNull(score);
            Assert.Equal(10, score);
        }

        // ── House/Hotel scoring ─────────────────────────────────────────

        [Fact]
        public void PlayScore_HouseScoresHighWithCompleteSet()
        {
            var bot = CreatePlayer("bot-1");
            var other = CreatePlayer("p1");

            var set = new PropertySet { Color = PropertyColor.Red };
            for (int i = 0; i < 3; i++)
                set.Cards.Add(new Card { Id = 10 + i, CardType = CardType.Property, Color = PropertyColor.Red, MoneyValue = 3 });
            bot.PropertySets.Add(set);

            var house = new Card { Id = 1, CardType = CardType.Action, ActionKind = ActionType.House, MoneyValue = 3 };
            var allPlayers = new List<Player> { bot, other };

            var score = CardEvaluator.PlayScore(bot, house, allPlayers, 3);
            Assert.NotNull(score);
            Assert.Equal(45, score);
        }

        [Fact]
        public void PlayScore_HouseScoresLowWithNoCompleteSet()
        {
            var bot = CreatePlayer("bot-1");
            var other = CreatePlayer("p1");

            bot.PropertySets.Add(CreatePartialSet(PropertyColor.Red, 1, 3));

            var house = new Card { Id = 1, CardType = CardType.Action, ActionKind = ActionType.House, MoneyValue = 3 };
            var allPlayers = new List<Player> { bot, other };

            var score = CardEvaluator.PlayScore(bot, house, allPlayers, 3);
            Assert.NotNull(score);
            Assert.Equal(10, score);
        }

        [Fact]
        public void PlayScore_HotelScoresHighWithHousedCompleteSet()
        {
            var bot = CreatePlayer("bot-1");
            var other = CreatePlayer("p1");

            var set = new PropertySet { Color = PropertyColor.Red, HasHouse = true };
            for (int i = 0; i < 3; i++)
                set.Cards.Add(new Card { Id = 10 + i, CardType = CardType.Property, Color = PropertyColor.Red, MoneyValue = 3 });
            bot.PropertySets.Add(set);

            var hotel = new Card { Id = 1, CardType = CardType.Action, ActionKind = ActionType.Hotel, MoneyValue = 4 };
            var allPlayers = new List<Player> { bot, other };

            var score = CardEvaluator.PlayScore(bot, hotel, allPlayers, 3);
            Assert.NotNull(score);
            Assert.Equal(45, score);
        }

        [Fact]
        public void PlayScore_HotelScoresLowWithoutHouse()
        {
            var bot = CreatePlayer("bot-1");
            var other = CreatePlayer("p1");

            // Complete set but no house
            var set = new PropertySet { Color = PropertyColor.Red };
            for (int i = 0; i < 3; i++)
                set.Cards.Add(new Card { Id = 10 + i, CardType = CardType.Property, Color = PropertyColor.Red, MoneyValue = 3 });
            bot.PropertySets.Add(set);

            var hotel = new Card { Id = 1, CardType = CardType.Action, ActionKind = ActionType.Hotel, MoneyValue = 4 };
            var allPlayers = new List<Player> { bot, other };

            var score = CardEvaluator.PlayScore(bot, hotel, allPlayers, 3);
            Assert.NotNull(score);
            Assert.Equal(10, score);
        }

        // ── DealBreaker scoring with OpponentNearWin ────────────────────

        [Fact]
        public void PlayScore_DealBreakerScores200WhenOpponentNearWin()
        {
            var bot = CreatePlayer("bot-1");
            var opponent = CreatePlayer("p1");

            // Opponent has 2 complete sets (near win)
            opponent.PropertySets.Add(CreateCompleteSet(PropertyColor.Brown, 2));
            opponent.PropertySets.Add(CreateCompleteSet(PropertyColor.DarkBlue, 2));
            // And a 3rd one that's incomplete
            opponent.PropertySets.Add(CreatePartialSet(PropertyColor.Red, 1, 3));

            var dealBreaker = new Card { Id = 1, CardType = CardType.Action, ActionKind = ActionType.DealBreaker, MoneyValue = 5 };
            var allPlayers = new List<Player> { bot, opponent };

            var score = CardEvaluator.PlayScore(bot, dealBreaker, allPlayers, 3);
            Assert.NotNull(score);
            Assert.Equal(200, score);
        }

        // ── ItsMyBirthday scoring ───────────────────────────────────────

        [Fact]
        public void PlayScore_BirthdayScores45()
        {
            var bot = CreatePlayer("bot-1");
            var other = CreatePlayer("p1");

            var birthday = new Card { Id = 1, CardType = CardType.Action, ActionKind = ActionType.ItsMyBirthday, MoneyValue = 2 };
            var allPlayers = new List<Player> { bot, other };

            var score = CardEvaluator.PlayScore(bot, birthday, allPlayers, 3);
            Assert.NotNull(score);
            Assert.Equal(45, score);
        }

        // ── PropertyWildcard scoring ────────────────────────────────────

        [Fact]
        public void PlayScore_PropertyWildcardScores35()
        {
            var bot = CreatePlayer("bot-1");

            var wild = new Card
            {
                Id = 1,
                CardType = CardType.PropertyWildcard,
                Color = PropertyColor.Red,
                AltColor = PropertyColor.Yellow,
                MoneyValue = 3
            };
            var allPlayers = new List<Player> { bot };

            var score = CardEvaluator.PlayScore(bot, wild, allPlayers, 3);
            Assert.NotNull(score);
            Assert.Equal(35, score);
        }

        // ── GetBestRentAmount ───────────────────────────────────────────

        [Fact]
        public void GetBestRentAmount_WildRentReturnsHighestRent()
        {
            var bot = CreatePlayer("bot-1");

            // DarkBlue: 2 cards → rent = 8
            bot.PropertySets.Add(CreateCompleteSet(PropertyColor.DarkBlue, 2));
            // Brown: 1 card → rent = 1
            bot.PropertySets.Add(CreatePartialSet(PropertyColor.Brown, 1, 2));

            var wildRent = new Card { Id = 1, CardType = CardType.Rent, IsWildRent = true, MoneyValue = 3 };

            var rentAmount = CardEvaluator.GetBestRentAmount(bot, wildRent);
            Assert.Equal(8, rentAmount);
        }

        [Fact]
        public void GetBestRentAmount_StandardRentPicksBestMatchingColor()
        {
            var bot = CreatePlayer("bot-1");

            // Brown: 2 cards → rent = 2
            bot.PropertySets.Add(CreateCompleteSet(PropertyColor.Brown, 2));
            // LightBlue: 1 card → rent = 1
            bot.PropertySets.Add(CreatePartialSet(PropertyColor.LightBlue, 1, 3));

            var rent = new Card
            {
                Id = 1,
                CardType = CardType.Rent,
                RentColors = new List<PropertyColor> { PropertyColor.Brown, PropertyColor.LightBlue },
                MoneyValue = 1
            };

            var rentAmount = CardEvaluator.GetBestRentAmount(bot, rent);
            Assert.Equal(2, rentAmount);
        }

        [Fact]
        public void GetBestRentAmount_NoMatchingPropertiesReturns0()
        {
            var bot = CreatePlayer("bot-1");

            var rent = new Card
            {
                Id = 1,
                CardType = CardType.Rent,
                RentColors = new List<PropertyColor> { PropertyColor.Green, PropertyColor.DarkBlue },
                MoneyValue = 1
            };

            var rentAmount = CardEvaluator.GetBestRentAmount(bot, rent);
            Assert.Equal(0, rentAmount);
        }

        [Fact]
        public void GetBestRentAmount_NullRentColorsReturns0()
        {
            var bot = CreatePlayer("bot-1");

            var rent = new Card { Id = 1, CardType = CardType.Rent, RentColors = null, MoneyValue = 1 };

            var rentAmount = CardEvaluator.GetBestRentAmount(bot, rent);
            Assert.Equal(0, rentAmount);
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private static Player CreatePlayer(string id) => new()
        {
            ConnectionId = id,
            PlayerId = id,
            Name = id,
        };

        private static PropertySet CreateCompleteSet(PropertyColor color, int size)
        {
            var set = new PropertySet { Color = color };
            for (int i = 0; i < size; i++)
            {
                set.Cards.Add(new Card
                {
                    Id = 8000 + (int)color * 10 + i,
                    CardType = CardType.Property,
                    Color = color,
                    ActiveColor = color,
                    MoneyValue = GameConfig.PropertyValue.GetValueOrDefault(color, 1),
                    Name = $"{color} {i}",
                });
            }
            return set;
        }

        private static PropertySet CreatePartialSet(PropertyColor color, int count, int required)
        {
            var set = new PropertySet { Color = color };
            for (int i = 0; i < count; i++)
            {
                set.Cards.Add(new Card
                {
                    Id = 9000 + (int)color * 10 + i,
                    CardType = CardType.Property,
                    Color = color,
                    ActiveColor = color,
                    MoneyValue = GameConfig.PropertyValue.GetValueOrDefault(color, 1),
                    Name = $"{color} {i}",
                });
            }
            return set;
        }
    }
}
