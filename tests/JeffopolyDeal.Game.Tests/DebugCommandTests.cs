using JeffopolyDeal.Models;
using Xunit;

namespace JeffopolyDeal.Tests
{
    /// <summary>
    /// Tests for the debug/cheat command system (Game.DebugCommandAsync).
    /// </summary>
    public class DebugCommandTests
    {
        #region give money

        [Fact]
        public async Task GiveMoney_DefaultValue_AddsMoney1ToHand()
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            int handBefore = h.GetHand(p1).Count;
            var result = await h.Game.DebugCommandAsync(p1, "give money");

            Assert.Contains("Gave", result);
            Assert.Equal(handBefore + 1, h.GetHand(p1).Count);
            var added = h.GetHand(p1).Last();
            Assert.Equal(CardType.Money, added.CardType);
            Assert.Equal(1, added.MoneyValue);
        }

        [Fact]
        public async Task GiveMoney_SpecificValue_AddsCorrectAmount()
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            var result = await h.Game.DebugCommandAsync(p1, "give money 5");

            Assert.Contains("Gave", result);
            var added = h.GetHand(p1).Last();
            Assert.Equal(CardType.Money, added.CardType);
            Assert.Equal(5, added.MoneyValue);
        }

        #endregion

        #region give rent

        [Fact]
        public async Task GiveRent_WithColor_AddsRentCardToHand()
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            var result = await h.Game.DebugCommandAsync(p1, "give rent pink");

            Assert.Contains("Gave", result);
            var added = h.GetHand(p1).Last();
            Assert.Equal(CardType.Rent, added.CardType);
            Assert.NotNull(added.RentColors);
            Assert.Contains(PropertyColor.Pink, added.RentColors!);
        }

        #endregion

        #region give wildrent

        [Fact]
        public async Task GiveWildRent_AddsWildRentToHand()
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            var result = await h.Game.DebugCommandAsync(p1, "give wildrent");

            Assert.Contains("Gave", result);
            var added = h.GetHand(p1).Last();
            Assert.Equal(CardType.Rent, added.CardType);
            Assert.True(added.IsWildRent);
        }

        #endregion

        #region give action cards

        [Theory]
        [InlineData("jsn", ActionType.JustSayNo)]
        [InlineData("justsayno", ActionType.JustSayNo)]
        [InlineData("house", ActionType.House)]
        [InlineData("hotel", ActionType.Hotel)]
        [InlineData("dealbreaker", ActionType.DealBreaker)]
        [InlineData("db", ActionType.DealBreaker)]
        [InlineData("doublerent", ActionType.DoubleTheRent)]
        [InlineData("double", ActionType.DoubleTheRent)]
        [InlineData("passgo", ActionType.PassGo)]
        [InlineData("go", ActionType.PassGo)]
        [InlineData("slydeal", ActionType.SlyDeal)]
        [InlineData("sly", ActionType.SlyDeal)]
        [InlineData("forcedeal", ActionType.ForceDeal)]
        [InlineData("force", ActionType.ForceDeal)]
        [InlineData("debtcollector", ActionType.DebtCollector)]
        [InlineData("debt", ActionType.DebtCollector)]
        [InlineData("birthday", ActionType.ItsMyBirthday)]
        public async Task GiveAction_AddsCorrectActionCard(string command, ActionType expected)
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            var result = await h.Game.DebugCommandAsync(p1, $"give {command}");

            Assert.Contains("Gave", result);
            var added = h.GetHand(p1).Last();
            Assert.Equal(CardType.Action, added.CardType);
            Assert.Equal(expected, added.ActionKind);
        }

        #endregion

        #region give property

        [Fact]
        public async Task GiveProperty_WithColorKeyword_AddsPropertyToHand()
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            var result = await h.Game.DebugCommandAsync(p1, "give property red");

            Assert.Contains("Gave", result);
            var added = h.GetHand(p1).Last();
            Assert.Equal(CardType.Property, added.CardType);
            Assert.Equal(PropertyColor.Red, added.Color);
        }

        [Fact]
        public async Task GiveProperty_BareColorName_AddsPropertyToHand()
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            var result = await h.Game.DebugCommandAsync(p1, "give green");

            Assert.Contains("Gave", result);
            var added = h.GetHand(p1).Last();
            Assert.Equal(CardType.Property, added.CardType);
            Assert.Equal(PropertyColor.Green, added.Color);
        }

        #endregion

        #region give unknown

        [Fact]
        public async Task GiveUnknown_FallsBackToPropertyByColor()
        {
            // Unknown card types fall through to ParseColor, which defaults to Brown,
            // so the command succeeds by creating a Brown property card.
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            int handBefore = h.GetHand(p1).Count;
            var result = await h.Game.DebugCommandAsync(p1, "give xyzzy");

            Assert.Contains("Gave", result);
            Assert.Equal(handBefore + 1, h.GetHand(p1).Count);
            var added = h.GetHand(p1).Last();
            Assert.Equal(CardType.Property, added.CardType);
        }

        #endregion

        #region bank

        [Fact]
        public async Task Bank_AddsMoneyToBankNotHand()
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            int handBefore = h.GetHand(p1).Count;
            var player = h.Game.GetPlayer(p1)!;
            int bankBefore = player.Bank.Count;

            var result = await h.Game.DebugCommandAsync(p1, "bank 3");

            Assert.Contains("bank", result);
            Assert.Equal(handBefore, h.GetHand(p1).Count);
            Assert.Equal(bankBefore + 1, player.Bank.Count);
            Assert.Equal(3, player.Bank.Last().MoneyValue);
        }

        #endregion

        #region clear hand

        [Fact]
        public async Task ClearHand_EmptiesHandAndDiscardsCards()
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            int handCount = h.GetHand(p1).Count;
            Assert.True(handCount > 0);

            int discardBefore = h.Game.GetDeck().DiscardPileCount;
            var result = await h.Game.DebugCommandAsync(p1, "clear hand");

            Assert.Contains("Discarded", result);
            Assert.Empty(h.GetHand(p1));
            Assert.Equal(discardBefore + handCount, h.Game.GetDeck().DiscardPileCount);
        }

        #endregion

        #region clear bank

        [Fact]
        public async Task ClearBank_EmptiesBankAndDiscardsCards()
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            // Put some money in bank first
            await h.Game.DebugCommandAsync(p1, "bank 2");
            await h.Game.DebugCommandAsync(p1, "bank 5");
            var player = h.Game.GetPlayer(p1)!;
            Assert.Equal(2, player.Bank.Count);

            int discardBefore = h.Game.GetDeck().DiscardPileCount;
            var result = await h.Game.DebugCommandAsync(p1, "clear bank");

            Assert.Contains("Discarded", result);
            Assert.Empty(player.Bank);
            Assert.Equal(discardBefore + 2, h.Game.GetDeck().DiscardPileCount);
        }

        #endregion

        #region myturn / skip

        [Fact]
        public async Task MyTurn_SkipsToPlayerTurnAndSetsPlayPhase()
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            // It's p1's turn, use myturn for p2 to skip to their turn
            var result = await h.Game.DebugCommandAsync(p2, "myturn");

            Assert.Contains("Skipped", result);
            Assert.Equal(GamePhase.Play, h.Game.Phase);
        }

        [Fact]
        public async Task Skip_AlsoSkipsToPlayerTurn()
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            var result = await h.Game.DebugCommandAsync(p2, "skip");

            Assert.Contains("Skipped", result);
            Assert.Equal(GamePhase.Play, h.Game.Phase);
        }

        #endregion

        #region giveto

        [Fact]
        public async Task GiveTo_GivesCardToNamedPlayer()
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            int p2HandBefore = h.GetHand(p2).Count;
            var result = await h.Game.DebugCommandAsync(p1, "giveto Bob money 3");

            Assert.Contains("Gave", result);
            Assert.Contains("Bob", result);
            Assert.Equal(p2HandBefore + 1, h.GetHand(p2).Count);
            var added = h.GetHand(p2).Last();
            Assert.Equal(CardType.Money, added.CardType);
            Assert.Equal(3, added.MoneyValue);
        }

        [Fact]
        public async Task GiveTo_UnknownPlayer_ReturnsError()
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            var result = await h.Game.DebugCommandAsync(p1, "giveto Nobody money 1");

            Assert.Contains("not found", result);
        }

        #endregion

        #region clearto

        [Fact]
        public async Task ClearTo_ClearsTargetPlayerHand()
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            Assert.True(h.GetHand(p2).Count > 0);
            var result = await h.Game.DebugCommandAsync(p1, "clearto Bob hand");

            Assert.Contains("Discarded", result);
            Assert.Empty(h.GetHand(p2));
        }

        [Fact]
        public async Task ClearTo_ClearsTargetPlayerBank()
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            await h.Game.DebugCommandAsync(p1, "giveto Bob money 5");
            // Move the money into Bob's bank via the bank debug command on Bob
            await h.Game.DebugCommandAsync(p2, "bank 5");
            var p2Player = h.Game.GetPlayer(p2)!;
            Assert.True(p2Player.Bank.Count > 0);

            var result = await h.Game.DebugCommandAsync(p1, "clearto Bob bank");

            Assert.Contains("Discarded", result);
            Assert.Empty(p2Player.Bank);
        }

        #endregion

        #region unknown / empty commands

        [Fact]
        public async Task UnknownCommand_ReturnsError()
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            var result = await h.Game.DebugCommandAsync(p1, "foobar");

            Assert.Contains("Unknown command", result);
        }

        [Fact]
        public async Task EmptyCommand_ReturnsError()
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            var result = await h.Game.DebugCommandAsync(p1, "   ");

            Assert.Contains("Error", result);
        }

        #endregion
    }
}
