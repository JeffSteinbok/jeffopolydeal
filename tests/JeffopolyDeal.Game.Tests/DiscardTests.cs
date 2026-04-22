using JeffopolyDeal.Models;
using Xunit;

namespace JeffopolyDeal.Tests
{
    /// <summary>
    /// Tests for the discard phase: triggered when a player ends their turn
    /// with more than MaxHandSize (7) cards in hand.
    /// </summary>
    public class DiscardTests
    {
        /// <summary>Helper: get p1 into Discard phase with the specified hand size.</summary>
        private async Task<(TestGameHarness h, string p1, string p2)> SetupDiscardPhaseAsync(int extraCards = 1)
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            // Inject extra money cards so hand exceeds MaxHandSize
            for (int i = 0; i < extraCards; i++)
                h.InjectMoney(p1, 1);

            Assert.True(h.GetHand(p1).Count > GameConfig.MaxHandSize);
            await h.EndTurnAsync(p1);
            Assert.Equal(GamePhase.Discard, h.GetPhase(p1));

            return (h, p1, p2);
        }

        [Fact]
        public async Task EndTurn_WithMoreThan7Cards_EntersDiscardPhase()
        {
            var h = new TestGameHarness();
            var (p1, _) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            h.InjectMoney(p1, 1);
            Assert.True(h.GetHand(p1).Count > GameConfig.MaxHandSize);

            await h.EndTurnAsync(p1);
            Assert.Equal(GamePhase.Discard, h.GetPhase(p1));
        }

        [Fact]
        public async Task Discard_RemovesCardFromHand()
        {
            var (h, p1, _) = await SetupDiscardPhaseAsync();

            var hand = h.GetHand(p1);
            int handBefore = hand.Count;
            var cardToDiscard = hand[0];

            await h.DiscardAsync(p1, cardToDiscard.Id);

            Assert.Equal(handBefore - 1, h.GetHand(p1).Count);
            Assert.DoesNotContain(h.GetHand(p1), c => c.Id == cardToDiscard.Id);
        }

        [Fact]
        public async Task Discard_DownTo7Cards_AdvancesTurnToNextPlayer()
        {
            var (h, p1, _) = await SetupDiscardPhaseAsync(extraCards: 1);

            // Hand is MaxHandSize + 1, discard one to reach 7
            var card = h.GetHand(p1)[0];
            await h.DiscardAsync(p1, card.Id);

            Assert.Equal(GamePhase.Draw, h.GetPhase(p1));
            Assert.Equal(1, h.GetCurrentPlayerIndex(p1));
        }

        [Fact]
        public async Task Discard_MultipleCardsNeeded_StaysInDiscardUntilAtLimit()
        {
            var (h, p1, _) = await SetupDiscardPhaseAsync(extraCards: 2);

            // Need to discard 2 cards. After first discard, still in Discard phase
            var firstCard = h.GetHand(p1)[0];
            await h.DiscardAsync(p1, firstCard.Id);
            Assert.Equal(GamePhase.Discard, h.GetPhase(p1));
            Assert.Equal(0, h.GetCurrentPlayerIndex(p1)); // Still p1's turn

            // Second discard brings hand to MaxHandSize, advances turn
            var secondCard = h.GetHand(p1)[0];
            await h.DiscardAsync(p1, secondCard.Id);
            Assert.Equal(GamePhase.Draw, h.GetPhase(p1));
            Assert.Equal(1, h.GetCurrentPlayerIndex(p1));
        }

        [Fact]
        public async Task Discard_WhenNotInDiscardPhase_IsIgnored()
        {
            var h = new TestGameHarness();
            var (p1, _) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            // In Play phase, not Discard
            Assert.Equal(GamePhase.Play, h.GetPhase(p1));
            int handBefore = h.GetHand(p1).Count;
            var card = h.GetHand(p1)[0];

            await h.DiscardAsync(p1, card.Id);

            Assert.Equal(handBefore, h.GetHand(p1).Count);
        }

        [Fact]
        public async Task Discard_NonCurrentPlayer_IsIgnored()
        {
            var (h, p1, p2) = await SetupDiscardPhaseAsync();

            // p2 tries to discard during p1's discard phase
            int p2HandBefore = h.GetHand(p2).Count;
            var p2Card = h.GetHand(p2)[0];

            await h.DiscardAsync(p2, p2Card.Id);

            Assert.Equal(p2HandBefore, h.GetHand(p2).Count);
            Assert.Equal(GamePhase.Discard, h.GetPhase(p1)); // Phase unchanged
        }

        [Fact]
        public async Task Discard_InvalidCardId_IsIgnored()
        {
            var (h, p1, _) = await SetupDiscardPhaseAsync();

            int handBefore = h.GetHand(p1).Count;
            await h.DiscardAsync(p1, -9999);

            Assert.Equal(handBefore, h.GetHand(p1).Count);
            Assert.Equal(GamePhase.Discard, h.GetPhase(p1));
        }

        [Fact]
        public async Task Discard_WhenAtOrBelowMaxHandSize_IsIgnored()
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            // Hand should be <= MaxHandSize after normal draw (initial 5 + 2 = 7)
            Assert.True(h.GetHand(p1).Count <= GameConfig.MaxHandSize);

            // Force into discard phase by injecting one extra card then ending turn
            h.InjectMoney(p1, 1);
            await h.EndTurnAsync(p1);
            Assert.Equal(GamePhase.Discard, h.GetPhase(p1));

            // Discard one to get to exactly MaxHandSize
            var card = h.GetHand(p1)[0];
            await h.DiscardAsync(p1, card.Id);

            // Now at MaxHandSize, turn should have advanced. Trying to discard again
            // should be ignored (we're no longer in Discard phase).
            Assert.Equal(GamePhase.Draw, h.GetPhase(p1));
        }

        [Fact]
        public async Task Discard_BatchedAction_CreatesSingleActionWithMultipleCards()
        {
            var (h, p1, _) = await SetupDiscardPhaseAsync(extraCards: 2);

            // Discard 2 cards to reach MaxHandSize
            var firstCard = h.GetHand(p1)[0];
            await h.DiscardAsync(p1, firstCard.Id);

            var secondCard = h.GetHand(p1)[0];
            await h.DiscardAsync(p1, secondCard.Id);

            // Turn should have advanced
            Assert.Equal(GamePhase.Draw, h.GetPhase(p1));

            // Check that a single "Discarded cards" action was logged with both cards
            var state = h.GetState(p1);
            Assert.NotNull(state);
            var discardAction = state!.RecentActions
                .LastOrDefault(a => a.Text == "Discarded cards");
            Assert.NotNull(discardAction);
            Assert.NotNull(discardAction!.SourceCards);
            Assert.Equal(2, discardAction.SourceCards!.Count);
            Assert.Contains(discardAction.SourceCards, c => c.Id == firstCard.Id);
            Assert.Contains(discardAction.SourceCards, c => c.Id == secondCard.Id);
        }
    }
}
