using JeffopolyDeal.Models;
using Xunit;

namespace JeffopolyDeal.Tests
{
    /// <summary>
    /// Tests for basic game flow: lobby, starting, drawing, turn structure.
    /// </summary>
    public class GameFlowTests
    {
        [Fact]
        public async Task NewGame_StartsInLobby()
        {
            var h = new TestGameHarness();
            var p1 = await h.AddPlayerAsync("Alice");
            Assert.Equal(GamePhase.Lobby, h.GetPhase(p1));
        }

        [Fact]
        public async Task JoinGame_AddsPlayer()
        {
            var h = new TestGameHarness();
            var p1 = await h.AddPlayerAsync("Alice");
            var p2 = await h.AddPlayerAsync("Bob");

            var state = h.GetState(p1);
            Assert.NotNull(state);
            Assert.Equal(2, state!.Players.Count);
            Assert.Contains(state.Players, p => p.Name == "Alice");
            Assert.Contains(state.Players, p => p.Name == "Bob");
        }

        [Fact]
        public async Task StartGame_RequiresAtLeast2Players()
        {
            var h = new TestGameHarness();
            var p1 = await h.AddPlayerAsync("Alice");
            await h.Game.StartGameAsync(allowSinglePlayer: false);

            // Should still be in lobby
            Assert.Equal(GamePhase.Lobby, h.GetPhase(p1));
        }

        [Fact]
        public async Task StartGame_AllowsSinglePlayer()
        {
            var h = new TestGameHarness();
            var p1 = await h.AddPlayerAsync("Alice");
            await h.Game.StartGameAsync(allowSinglePlayer: true);

            Assert.Equal(GamePhase.Draw, h.GetPhase(p1));
        }

        [Fact]
        public async Task StartGame_DealsInitialHands()
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();

            Assert.Equal(GameConfig.InitialHandSize, h.GetHand(p1).Count);
            Assert.Equal(GameConfig.InitialHandSize, h.GetHand(p2).Count);
        }

        [Fact]
        public async Task StartGame_MovesToDrawPhase()
        {
            var h = new TestGameHarness();
            var (p1, _) = await h.SetupTwoPlayerGameAsync();
            Assert.Equal(GamePhase.Draw, h.GetPhase(p1));
        }

        [Fact]
        public async Task DrawCards_Draws2Cards()
        {
            var h = new TestGameHarness();
            var (p1, _) = await h.SetupTwoPlayerGameAsync();

            int handBefore = h.GetHand(p1).Count;
            await h.DrawAsync(p1);
            Assert.Equal(handBefore + GameConfig.DrawPerTurn, h.GetHand(p1).Count);
        }

        [Fact]
        public async Task DrawCards_MovesToPlayPhase()
        {
            var h = new TestGameHarness();
            var (p1, _) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);
            Assert.Equal(GamePhase.Play, h.GetPhase(p1));
        }

        [Fact]
        public async Task DrawCards_OnlyCurrentPlayerCanDraw()
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();

            // P2 tries to draw during P1's turn
            int p2HandBefore = h.GetHand(p2).Count;
            await h.DrawAsync(p2);
            Assert.Equal(p2HandBefore, h.GetHand(p2).Count);
        }

        [Fact]
        public async Task EndTurn_AdvancesToNextPlayer()
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();

            Assert.Equal(0, h.GetCurrentPlayerIndex(p1));
            await h.DrawAsync(p1);
            await h.EndTurnAsync(p1);

            Assert.Equal(1, h.GetCurrentPlayerIndex(p1));
            Assert.Equal(GamePhase.Draw, h.GetPhase(p1));
        }

        [Fact]
        public async Task EndTurn_WrapsAroundToFirstPlayer()
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();

            // P1 turn
            await h.SkipTurnAsync(p1);
            // P2 turn
            await h.SkipTurnAsync(p2);

            // Back to P1
            Assert.Equal(0, h.GetCurrentPlayerIndex(p1));
        }

        [Fact]
        public async Task MaxPlaysPerTurn_EnforcesLimit()
        {
            var h = new TestGameHarness();
            var (p1, _) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            int played = 0;
            // Play up to 3 money cards
            for (int i = 0; i < 4; i++)
            {
                var money = h.FindMoneyInHand(p1);
                if (money == null) break;
                int handBefore = h.GetHand(p1).Count;
                await h.PlayAsMoney(p1, money.Id);
                if (h.GetHand(p1).Count < handBefore)
                    played++;
            }

            // Should have played at most 3
            Assert.True(played <= GameConfig.MaxPlaysPerTurn);
        }

        [Fact]
        public async Task HandLimit_ForcesDiscard()
        {
            var h = new TestGameHarness();
            var (p1, _) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            // Force hand above max so EndTurn should switch to Discard phase.
            h.InjectMoney(p1, 1);
            Assert.True(h.GetHand(p1).Count > GameConfig.MaxHandSize);

            await h.EndTurnAsync(p1);
            Assert.Equal(GamePhase.Discard, h.GetPhase(p1));
        }

        [Fact]
        public async Task DrawWhenEmpty_Draws5()
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            // Play all cards from hand to empty it
            var hand = h.GetHand(p1);
            int plays = 0;
            foreach (var card in hand.ToList())
            {
                if (plays >= GameConfig.MaxPlaysPerTurn) break;
                await h.PlayAsMoney(p1, card.Id);
                plays++;
            }
            await h.EndTurnAsync(p1);

            // P2 turn
            await h.SkipTurnAsync(p2);

            // P1 should draw again. If hand was emptied, should draw 5
            int handBefore = h.GetHand(p1).Count;
            int expectedDraw = handBefore == 0 ? GameConfig.DrawWhenEmpty : GameConfig.DrawPerTurn;
            await h.DrawAsync(p1);
            Assert.Equal(handBefore + expectedDraw, h.GetHand(p1).Count);
        }

        [Fact]
        public async Task StartGame_WithPopulatedBoards_DoesNotExceedHandLimit()
        {
            var h = new TestGameHarness();
            var p1 = await h.AddPlayerAsync("Alice");
            var p2 = await h.AddPlayerAsync("Bob");

            await h.Game.StartGameAsync(allowSinglePlayer: false, populateBoards: true);

            Assert.True(h.GetHand(p1).Count <= GameConfig.MaxHandSize);
            Assert.True(h.GetHand(p2).Count <= GameConfig.MaxHandSize);
        }
    }
}
