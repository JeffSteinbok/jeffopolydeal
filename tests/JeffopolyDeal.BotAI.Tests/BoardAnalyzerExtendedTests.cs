using JeffopolyDeal;
using JeffopolyDeal.Models;

namespace JeffopolyDeal.BotAI.Tests
{
    public class BoardAnalyzerExtendedTests
    {
        // ── CanWinThisTurn ───────────────────────────────────────────────

        [Fact]
        public void CanWinThisTurn_TrueWhenHandCompletesEnoughSets()
        {
            var bot = CreatePlayer("bot-1");

            // 2 complete sets already
            bot.PropertySets.Add(CreateCompleteSet(PropertyColor.Brown, 2));
            bot.PropertySets.Add(CreateCompleteSet(PropertyColor.DarkBlue, 2));

            // 1 incomplete set: Red 2/3, need 1 more
            bot.PropertySets.Add(CreatePartialSet(PropertyColor.Red, 2, 3));

            // Hand has a Red property to complete the 3rd set
            bot.Hand.Add(new Card
            {
                Id = 5000,
                CardType = CardType.Property,
                Color = PropertyColor.Red,
                MoneyValue = 3
            });

            Assert.True(BoardAnalyzer.CanWinThisTurn(bot, remainingPlays: 1));
        }

        [Fact]
        public void CanWinThisTurn_FalseWhenNotEnoughSets()
        {
            var bot = CreatePlayer("bot-1");

            // Only 1 complete set
            bot.PropertySets.Add(CreateCompleteSet(PropertyColor.Brown, 2));
            bot.PropertySets.Add(CreatePartialSet(PropertyColor.Red, 1, 3));

            Assert.False(BoardAnalyzer.CanWinThisTurn(bot, remainingPlays: 3));
        }

        [Fact]
        public void CanWinThisTurn_TrueWithMulticolorWild()
        {
            var bot = CreatePlayer("bot-1");

            bot.PropertySets.Add(CreateCompleteSet(PropertyColor.Brown, 2));
            bot.PropertySets.Add(CreateCompleteSet(PropertyColor.DarkBlue, 2));

            // Red: 2 of 3
            bot.PropertySets.Add(CreatePartialSet(PropertyColor.Red, 2, 3));

            // Multicolor wild can complete Red
            bot.Hand.Add(new Card
            {
                Id = 5001,
                CardType = CardType.PropertyWildcard,
                IsMulticolorWild = true,
                MoneyValue = 0
            });

            Assert.True(BoardAnalyzer.CanWinThisTurn(bot, remainingPlays: 1));
        }

        [Fact]
        public void CanWinThisTurn_FalseWhenNotEnoughPlaysRemaining()
        {
            var bot = CreatePlayer("bot-1");

            bot.PropertySets.Add(CreateCompleteSet(PropertyColor.Brown, 2));
            bot.PropertySets.Add(CreateCompleteSet(PropertyColor.DarkBlue, 2));

            // Red: 1 of 3, needs 2 more cards but only 1 play remaining
            bot.PropertySets.Add(CreatePartialSet(PropertyColor.Red, 1, 3));

            bot.Hand.Add(new Card { Id = 5002, CardType = CardType.Property, Color = PropertyColor.Red, MoneyValue = 3 });
            bot.Hand.Add(new Card { Id = 5003, CardType = CardType.Property, Color = PropertyColor.Red, MoneyValue = 3 });

            Assert.False(BoardAnalyzer.CanWinThisTurn(bot, remainingPlays: 1));
        }

        [Fact]
        public void CanWinThisTurn_WithDualColorWild()
        {
            var bot = CreatePlayer("bot-1");

            bot.PropertySets.Add(CreateCompleteSet(PropertyColor.Brown, 2));
            bot.PropertySets.Add(CreateCompleteSet(PropertyColor.DarkBlue, 2));

            // Red: 2 of 3
            bot.PropertySets.Add(CreatePartialSet(PropertyColor.Red, 2, 3));

            // Dual-color wild (Red/Yellow) can complete Red
            bot.Hand.Add(new Card
            {
                Id = 5004,
                CardType = CardType.PropertyWildcard,
                Color = PropertyColor.Red,
                AltColor = PropertyColor.Yellow,
                MoneyValue = 3
            });

            Assert.True(BoardAnalyzer.CanWinThisTurn(bot, remainingPlays: 1));
        }

        // ── OpponentNearWin ─────────────────────────────────────────────

        [Fact]
        public void OpponentNearWin_DetectsOpponentWith2CompleteSets()
        {
            var bot = CreatePlayer("bot-1");
            var opp = CreatePlayer("p1");

            opp.PropertySets.Add(CreateCompleteSet(PropertyColor.Brown, 2));
            opp.PropertySets.Add(CreateCompleteSet(PropertyColor.Red, 3));

            var allPlayers = new List<Player> { bot, opp };
            Assert.True(BoardAnalyzer.OpponentNearWin(bot, allPlayers));
        }

        [Fact]
        public void OpponentNearWin_FalseWithOnlyOneCompleteSet()
        {
            var bot = CreatePlayer("bot-1");
            var opp = CreatePlayer("p1");

            opp.PropertySets.Add(CreateCompleteSet(PropertyColor.Brown, 2));
            opp.PropertySets.Add(CreatePartialSet(PropertyColor.Red, 2, 3));

            var allPlayers = new List<Player> { bot, opp };
            Assert.False(BoardAnalyzer.OpponentNearWin(bot, allPlayers));
        }

        // ── BestWildcardColor ───────────────────────────────────────────

        [Fact]
        public void BestWildcardColor_PicksColorClosestToCompletion()
        {
            var bot = CreatePlayer("bot-1");

            // Brown: 1 of 2 = 50%
            bot.PropertySets.Add(CreatePartialSet(PropertyColor.Brown, 1, 2));
            // Red: 1 of 3 = 33%
            bot.PropertySets.Add(CreatePartialSet(PropertyColor.Red, 1, 3));

            var best = BoardAnalyzer.BestWildcardColor(bot);
            Assert.Equal(PropertyColor.Brown, best);
        }

        [Fact]
        public void BestWildcardColor_TiebreaksOnHigherRent()
        {
            var bot = CreatePlayer("bot-1");

            // Both at 1/2 = 50%, but DarkBlue has higher rent
            bot.PropertySets.Add(CreatePartialSet(PropertyColor.Brown, 1, 2));
            bot.PropertySets.Add(CreatePartialSet(PropertyColor.DarkBlue, 1, 2));

            var best = BoardAnalyzer.BestWildcardColor(bot);
            // DarkBlue rent at 1 card = 3, Brown rent at 1 card = 1
            Assert.Equal(PropertyColor.DarkBlue, best);
        }

        [Fact]
        public void BestWildcardColor_ReturnsNullWhenNoCandidates()
        {
            var bot = CreatePlayer("bot-1");
            // Only complete sets or empty
            bot.PropertySets.Add(CreateCompleteSet(PropertyColor.Brown, 2));

            var best = BoardAnalyzer.BestWildcardColor(bot);
            Assert.Null(best);
        }

        [Fact]
        public void BestWildcardColor_IgnoresEmptySets()
        {
            var bot = CreatePlayer("bot-1");

            // An empty set should not be considered
            var emptySet = new PropertySet { Color = PropertyColor.Green };
            bot.PropertySets.Add(emptySet);

            bot.PropertySets.Add(CreatePartialSet(PropertyColor.Red, 1, 3));

            var best = BoardAnalyzer.BestWildcardColor(bot);
            Assert.Equal(PropertyColor.Red, best);
        }

        // ── SetCompletionRatio ──────────────────────────────────────────

        [Fact]
        public void SetCompletionRatio_HalfComplete()
        {
            var set = CreatePartialSet(PropertyColor.DarkBlue, 1, 2);
            Assert.Equal(0.5, BoardAnalyzer.SetCompletionRatio(set), 4);
        }

        // ── TotalAssetValue ─────────────────────────────────────────────

        [Fact]
        public void TotalAssetValue_EmptyPlayer()
        {
            var player = CreatePlayer("p1");
            Assert.Equal(0, BoardAnalyzer.TotalAssetValue(player));
        }

        [Fact]
        public void TotalAssetValue_MultipleSets()
        {
            var player = CreatePlayer("p1");
            player.Bank.Add(CreateMoney(1, 10));

            // Red set: 2 cards × $3 = $6
            var redSet = CreatePartialSet(PropertyColor.Red, 2, 3);
            player.PropertySets.Add(redSet);

            // Brown set: 1 card × $1 = $1
            var brownSet = CreatePartialSet(PropertyColor.Brown, 1, 2);
            player.PropertySets.Add(brownSet);

            // 10 + 6 + 1 = 17
            Assert.Equal(17, BoardAnalyzer.TotalAssetValue(player));
        }

        // ── ThreatScore ─────────────────────────────────────────────────

        [Fact]
        public void ThreatScore_IncludesBankValue()
        {
            var player = CreatePlayer("p1");
            player.Bank.Add(CreateMoney(1, 5));

            Assert.True(BoardAnalyzer.ThreatScore(player) >= 5);
        }

        [Fact]
        public void ThreatScore_EmptyPlayer()
        {
            var player = CreatePlayer("p1");
            Assert.Equal(0, BoardAnalyzer.ThreatScore(player));
        }

        // ── BiggestThreat ───────────────────────────────────────────────

        [Fact]
        public void BiggestThreat_ReturnsNullForSoloGame()
        {
            var bot = CreatePlayer("bot-1");
            var allPlayers = new List<Player> { bot };
            Assert.Null(BoardAnalyzer.BiggestThreat(bot, allPlayers));
        }

        [Fact]
        public void RichestOpponent_ReturnsNullForSoloGame()
        {
            var bot = CreatePlayer("bot-1");
            var allPlayers = new List<Player> { bot };
            Assert.Null(BoardAnalyzer.RichestOpponent(bot, allPlayers));
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
                    Id = 6000 + (int)color * 10 + i,
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
                    Id = 7000 + (int)color * 10 + i,
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
