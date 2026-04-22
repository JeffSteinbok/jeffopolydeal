using JeffopolyDeal;
using JeffopolyDeal.Models;

namespace JeffopolyDeal.BotAI.Tests
{
    public class CardEvaluatorTests
    {
        [Fact]
        public void PlayScore_JustSayNoReturnsNull()
        {
            var bot = CreatePlayer("bot-1");
            var card = new Card { Id = 1, CardType = CardType.Action, ActionKind = ActionType.JustSayNo };
            var allPlayers = new List<Player> { bot };

            Assert.Null(CardEvaluator.PlayScore(bot, card, allPlayers, 3));
        }

        [Fact]
        public void PlayScore_DoubleTheRentReturnsNull()
        {
            var bot = CreatePlayer("bot-1");
            var card = new Card { Id = 1, CardType = CardType.Action, ActionKind = ActionType.DoubleTheRent };
            var allPlayers = new List<Player> { bot };

            Assert.Null(CardEvaluator.PlayScore(bot, card, allPlayers, 3));
        }

        [Fact]
        public void PlayScore_PassGoScoresHighEarlyInTurn()
        {
            var bot = CreatePlayer("bot-1");
            var other = CreatePlayer("p1");
            var card = new Card { Id = 1, CardType = CardType.Action, ActionKind = ActionType.PassGo };
            var allPlayers = new List<Player> { bot, other };

            var earlyScore = CardEvaluator.PlayScore(bot, card, allPlayers, 3);
            var lateScore = CardEvaluator.PlayScore(bot, card, allPlayers, 1);

            Assert.NotNull(earlyScore);
            Assert.NotNull(lateScore);
            Assert.True(earlyScore > lateScore);
        }

        [Fact]
        public void PlayScore_SetCompletingPropertyScoresVeryHigh()
        {
            var bot = CreatePlayer("bot-1");
            var other = CreatePlayer("p1");

            // Bot has 1 of 2 Brown properties — adding another completes the set
            var set = new PropertySet { Color = PropertyColor.Brown };
            set.Cards.Add(new Card { Id = 10, CardType = CardType.Property, Color = PropertyColor.Brown, MoneyValue = 1 });
            bot.PropertySets.Add(set);

            var completingCard = new Card { Id = 2, CardType = CardType.Property, Color = PropertyColor.Brown, MoneyValue = 1 };
            var regularCard = new Card { Id = 3, CardType = CardType.Property, Color = PropertyColor.Green, MoneyValue = 2 };

            var allPlayers = new List<Player> { bot, other };

            var completingScore = CardEvaluator.PlayScore(bot, completingCard, allPlayers, 3);
            var regularScore = CardEvaluator.PlayScore(bot, regularCard, allPlayers, 3);

            Assert.NotNull(completingScore);
            Assert.NotNull(regularScore);
            Assert.True(completingScore > regularScore);
        }

        [Fact]
        public void PlayScore_DealBreakerScoresHighWhenOpponentHasCompleteSet()
        {
            var bot = CreatePlayer("bot-1");
            var opponent = CreatePlayer("p1");

            var set = new PropertySet { Color = PropertyColor.Brown };
            set.Cards.Add(new Card { Id = 10, CardType = CardType.Property, Color = PropertyColor.Brown, MoneyValue = 1 });
            set.Cards.Add(new Card { Id = 11, CardType = CardType.Property, Color = PropertyColor.Brown, MoneyValue = 1 });
            opponent.PropertySets.Add(set);

            var dealBreaker = new Card { Id = 1, CardType = CardType.Action, ActionKind = ActionType.DealBreaker, MoneyValue = 5 };
            var allPlayers = new List<Player> { bot, opponent };

            var score = CardEvaluator.PlayScore(bot, dealBreaker, allPlayers, 3);

            Assert.NotNull(score);
            Assert.True(score >= 90);
        }

        [Fact]
        public void PlayScore_DealBreakerScoresLowWhenNoCompleteOpponentSets()
        {
            var bot = CreatePlayer("bot-1");
            var opponent = CreatePlayer("p1");
            // Opponent has no complete sets
            var allPlayers = new List<Player> { bot, opponent };

            var dealBreaker = new Card { Id = 1, CardType = CardType.Action, ActionKind = ActionType.DealBreaker, MoneyValue = 5 };
            var score = CardEvaluator.PlayScore(bot, dealBreaker, allPlayers, 3);

            Assert.NotNull(score);
            Assert.True(score <= 20);
        }

        [Fact]
        public void PlayScore_MoneyHasModerateScore()
        {
            var bot = CreatePlayer("bot-1");
            var card = new Card { Id = 1, CardType = CardType.Money, MoneyValue = 5 };
            var allPlayers = new List<Player> { bot };

            var score = CardEvaluator.PlayScore(bot, card, allPlayers, 3);

            Assert.NotNull(score);
            Assert.Equal(20, score);
        }

        [Fact]
        public void PlayScore_RentWithPropertiesScoresHigh()
        {
            var bot = CreatePlayer("bot-1");
            var other = CreatePlayer("p1");

            var set = new PropertySet { Color = PropertyColor.Brown };
            set.Cards.Add(new Card { Id = 10, CardType = CardType.Property, Color = PropertyColor.Brown, MoneyValue = 1 });
            bot.PropertySets.Add(set);

            var rent = new Card
            {
                Id = 1,
                CardType = CardType.Rent,
                RentColors = new List<PropertyColor> { PropertyColor.Brown, PropertyColor.LightBlue },
                MoneyValue = 1,
            };
            var allPlayers = new List<Player> { bot, other };

            var score = CardEvaluator.PlayScore(bot, rent, allPlayers, 3);

            Assert.NotNull(score);
            Assert.True(score >= 50);
        }

        [Fact]
        public void PlayScore_DebtCollectorAlwaysDecent()
        {
            var bot = CreatePlayer("bot-1");
            var other = CreatePlayer("p1");
            var card = new Card { Id = 1, CardType = CardType.Action, ActionKind = ActionType.DebtCollector, MoneyValue = 3 };
            var allPlayers = new List<Player> { bot, other };

            var score = CardEvaluator.PlayScore(bot, card, allPlayers, 3);

            Assert.NotNull(score);
            Assert.Equal(55, score);
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private static Player CreatePlayer(string id) => new()
        {
            ConnectionId = id,
            PlayerId = id,
            Name = id,
        };
    }
}
