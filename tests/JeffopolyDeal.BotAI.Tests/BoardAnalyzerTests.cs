using JeffopolyDeal;
using JeffopolyDeal.Models;

namespace JeffopolyDeal.BotAI.Tests
{
    public class BoardAnalyzerTests
    {
        [Fact]
        public void ThreatScore_PlayerWithCompleteSetScoresHigher()
        {
            var p1 = CreatePlayer("p1");
            var p2 = CreatePlayer("p2");

            // p1 has 2 complete sets
            p1.PropertySets.Add(CreateCompleteSet(PropertyColor.Brown, 2));
            p1.PropertySets.Add(CreateCompleteSet(PropertyColor.DarkBlue, 2));

            // p2 has 1 complete set
            p2.PropertySets.Add(CreateCompleteSet(PropertyColor.Brown, 2));

            Assert.True(BoardAnalyzer.ThreatScore(p1) > BoardAnalyzer.ThreatScore(p2));
        }

        [Fact]
        public void ThreatScore_MorePropertiesMeansHigherScore()
        {
            var p1 = CreatePlayer("p1");
            var p2 = CreatePlayer("p2");

            p1.PropertySets.Add(CreatePartialSet(PropertyColor.Red, 2, 3));
            p1.PropertySets.Add(CreatePartialSet(PropertyColor.Green, 1, 3));

            p2.PropertySets.Add(CreatePartialSet(PropertyColor.Red, 1, 3));

            Assert.True(BoardAnalyzer.ThreatScore(p1) > BoardAnalyzer.ThreatScore(p2));
        }

        [Fact]
        public void ThreatScore_NearCompleteSetScoresHigherThanPartial()
        {
            var p1 = CreatePlayer("p1");
            var p2 = CreatePlayer("p2");

            // p1 has a set that's one card away from complete (2 of 3)
            p1.PropertySets.Add(CreatePartialSet(PropertyColor.Red, 2, 3));

            // p2 has a set that's far from complete (1 of 3)
            p2.PropertySets.Add(CreatePartialSet(PropertyColor.Red, 1, 3));

            Assert.True(BoardAnalyzer.ThreatScore(p1) > BoardAnalyzer.ThreatScore(p2));
        }

        [Fact]
        public void BiggestThreat_ReturnsPlayerClosestToWinning()
        {
            var bot = CreatePlayer("bot-1");
            var weak = CreatePlayer("p1");
            var strong = CreatePlayer("p2");

            strong.PropertySets.Add(CreateCompleteSet(PropertyColor.Brown, 2));
            strong.PropertySets.Add(CreateCompleteSet(PropertyColor.DarkBlue, 2));
            weak.PropertySets.Add(CreatePartialSet(PropertyColor.Red, 1, 3));

            var allPlayers = new List<Player> { bot, weak, strong };
            var threat = BoardAnalyzer.BiggestThreat(bot, allPlayers);

            Assert.Equal("p2", threat?.ConnectionId);
        }

        [Fact]
        public void RichestOpponent_ReturnsPlayerWithMostAssets()
        {
            var bot = CreatePlayer("bot-1");
            var poor = CreatePlayer("p1");
            var rich = CreatePlayer("p2");

            rich.Bank.Add(CreateMoney(1, 5));
            rich.Bank.Add(CreateMoney(2, 5));
            poor.Bank.Add(CreateMoney(3, 1));

            var allPlayers = new List<Player> { bot, poor, rich };
            var richest = BoardAnalyzer.RichestOpponent(bot, allPlayers);

            Assert.Equal("p2", richest?.ConnectionId);
        }

        [Fact]
        public void OpponentNearWin_TrueWhenOpponentHasTwoCompleteSets()
        {
            var bot = CreatePlayer("bot-1");
            var opponent = CreatePlayer("p1");

            opponent.PropertySets.Add(CreateCompleteSet(PropertyColor.Brown, 2));
            opponent.PropertySets.Add(CreateCompleteSet(PropertyColor.DarkBlue, 2));

            var allPlayers = new List<Player> { bot, opponent };
            Assert.True(BoardAnalyzer.OpponentNearWin(bot, allPlayers));
        }

        [Fact]
        public void OpponentNearWin_FalseWhenNoOpponentCloseToWinning()
        {
            var bot = CreatePlayer("bot-1");
            var opponent = CreatePlayer("p1");

            opponent.PropertySets.Add(CreateCompleteSet(PropertyColor.Brown, 2));

            var allPlayers = new List<Player> { bot, opponent };
            Assert.False(BoardAnalyzer.OpponentNearWin(bot, allPlayers));
        }

        [Fact]
        public void BestWildcardColor_PicksColorClosestToCompletion()
        {
            var bot = CreatePlayer("bot-1");

            // Red: 2 of 3 (closer to complete)
            bot.PropertySets.Add(CreatePartialSet(PropertyColor.Red, 2, 3));
            // Green: 1 of 3
            bot.PropertySets.Add(CreatePartialSet(PropertyColor.Green, 1, 3));

            var best = BoardAnalyzer.BestWildcardColor(bot);
            Assert.Equal(PropertyColor.Red, best);
        }

        [Fact]
        public void BestWildcardColor_SkipsAlreadyCompleteSets()
        {
            var bot = CreatePlayer("bot-1");

            bot.PropertySets.Add(CreateCompleteSet(PropertyColor.Brown, 2));
            bot.PropertySets.Add(CreatePartialSet(PropertyColor.Green, 1, 3));

            var best = BoardAnalyzer.BestWildcardColor(bot);
            Assert.Equal(PropertyColor.Green, best);
        }

        [Fact]
        public void SetCompletionRatio_CorrectForVariousSizes()
        {
            var full = CreateCompleteSet(PropertyColor.Brown, 2);
            Assert.Equal(1.0, BoardAnalyzer.SetCompletionRatio(full));

            var half = CreatePartialSet(PropertyColor.Red, 1, 3);
            Assert.Equal(1.0 / 3.0, BoardAnalyzer.SetCompletionRatio(half), 4);

            var twoThirds = CreatePartialSet(PropertyColor.Red, 2, 3);
            Assert.Equal(2.0 / 3.0, BoardAnalyzer.SetCompletionRatio(twoThirds), 4);
        }

        [Fact]
        public void TotalAssetValue_IncludesBankAndProperties()
        {
            var player = CreatePlayer("p1");
            player.Bank.Add(CreateMoney(1, 5));
            player.Bank.Add(CreateMoney(2, 3));

            var set = new PropertySet { Color = PropertyColor.Brown };
            set.Cards.Add(new Card { Id = 10, CardType = CardType.Property, MoneyValue = 1, Color = PropertyColor.Brown });
            player.PropertySets.Add(set);

            Assert.Equal(9, BoardAnalyzer.TotalAssetValue(player));
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private static Player CreatePlayer(string id) => new()
        {
            ConnectionId = id,
            PlayerId = id,
            Name = id,
        };

        private static Card CreateMoney(int id, int value) => new()
        {
            Id = id,
            CardType = CardType.Money,
            MoneyValue = value,
            Name = $"{value}M",
        };

        private static PropertySet CreateCompleteSet(PropertyColor color, int size)
        {
            var set = new PropertySet { Color = color };
            for (int i = 0; i < size; i++)
            {
                set.Cards.Add(new Card
                {
                    Id = 1000 + (int)color * 10 + i,
                    CardType = CardType.Property,
                    Color = color,
                    MoneyValue = 1,
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
                    Id = 2000 + (int)color * 10 + i,
                    CardType = CardType.Property,
                    Color = color,
                    MoneyValue = 1,
                    Name = $"{color} {i}",
                });
            }
            return set;
        }
    }
}
