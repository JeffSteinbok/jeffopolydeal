using JeffopolyDeal.Models;
using Xunit;

namespace JeffopolyDeal.Tests
{
    /// <summary>
    /// Tests for property card rearrangement: moving cards between sets and flipping wildcards.
    /// Covers:
    ///   - Issue #94: Cards must not disappear when dragged to the same set they came from.
    ///   - Issue #95: Win condition must be evaluated after every property rearrangement.
    /// </summary>
    public class PropertyRearrangementTests
    {
        // ═══════════════════════════════════════════════════════════════════
        // Issue #94 — Cards must never disappear on property moves
        // ═══════════════════════════════════════════════════════════════════

        [Fact]
        public async Task MoveProperty_ToSameSet_SingleCard_CardDoesNotDisappear()
        {
            // Regression: moving the only card in a set back to that same set used to delete
            // the set (count → 0) and then orphan the card in the removed set reference.
            var h = new TestGameHarness();
            var (p1, _) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            var set = h.PlacePropertyOnBoard(p1, PropertyColor.Green, 1);
            var card = set.Cards[0];

            // Drag the card back to the same set it is already in
            await h.Game.MovePropertyAsync(p1, card.Id, set.SetId, null);

            var p1State = h.GetPlayerState(p1, p1);

            // The card must still be on the board
            bool cardFound = p1State!.PropertySets.Any(s => s.Cards.Any(c => c.Id == card.Id));
            Assert.True(cardFound, "Card should still be on the board after a no-op same-set move.");
        }

        [Fact]
        public async Task MoveProperty_ToSameSet_MultiCard_CardCountUnchanged()
        {
            // Moving any card within its own set should leave the set size unchanged.
            var h = new TestGameHarness();
            var (p1, _) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            var set = h.PlacePropertyOnBoard(p1, PropertyColor.Red, 2);
            var card = set.Cards[0];
            int originalCount = set.Cards.Count;

            await h.Game.MovePropertyAsync(p1, card.Id, set.SetId, null);

            var p1State = h.GetPlayerState(p1, p1);
            var updatedSet = p1State!.PropertySets.FirstOrDefault(s => s.SetId == set.SetId);
            Assert.NotNull(updatedSet);
            Assert.Equal(originalCount, updatedSet!.Cards.Count);
        }

        [Fact]
        public async Task MoveProperty_ToNewSet_CardMovedCorrectly()
        {
            // Sanity check: a legitimate move to a new set must still work.
            var h = new TestGameHarness();
            var (p1, _) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            var set1 = h.PlacePropertyOnBoard(p1, PropertyColor.Green, 1);
            var cardToMove = set1.Cards[0];

            await h.Game.MovePropertyAsync(p1, cardToMove.Id, 0, PropertyColor.Green);

            var p1State = h.GetPlayerState(p1, p1);
            // The card must be on the board — exact set ID may differ (new set created)
            bool cardFound = p1State!.PropertySets.Any(s => s.Color == PropertyColor.Green
                && s.Cards.Any(c => c.Id == cardToMove.Id));
            Assert.True(cardFound, "Card should be in a Green set after moving to a new Green set.");
        }

        [Fact]
        public async Task MoveProperty_InvalidMove_CardStaysInPlace()
        {
            // A move to a different-color existing set must be rejected and the card must stay.
            var h = new TestGameHarness();
            var (p1, _) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            var greenSet = h.PlacePropertyOnBoard(p1, PropertyColor.Green, 1);
            var redSet = h.PlacePropertyOnBoard(p1, PropertyColor.Red, 1);
            var greenCard = greenSet.Cards[0]; // green property card

            // Try to move a Green property to the Red set — must be rejected
            await h.Game.MovePropertyAsync(p1, greenCard.Id, redSet.SetId, null);

            var p1State = h.GetPlayerState(p1, p1);
            bool cardInGreen = p1State!.PropertySets.Any(s => s.Color == PropertyColor.Green
                && s.Cards.Any(c => c.Id == greenCard.Id));
            bool cardInRed = p1State.PropertySets.Any(s => s.Color == PropertyColor.Red
                && s.Cards.Any(c => c.Id == greenCard.Id));

            Assert.True(cardInGreen, "Green card should remain in Green set after invalid move.");
            Assert.False(cardInRed, "Green card must not appear in Red set.");
        }

        // ═══════════════════════════════════════════════════════════════════
        // Issue #95 — Win condition evaluated after every rearrangement
        // ═══════════════════════════════════════════════════════════════════

        [Fact]
        public async Task MoveProperty_CompletingThirdSet_TriggersWin()
        {
            // Player already has 2 complete sets; moving a wildcard completes the 3rd.
            var h = new TestGameHarness();
            var (p1, _) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            // Two complete sets (Brown: 2 cards, Utility: 2 cards)
            h.PlaceCompleteSet(p1, PropertyColor.Brown);
            h.PlaceCompleteSet(p1, PropertyColor.Utility);

            // Set up a DarkBlue set with 1 of 2 cards and an unbound multi-color wild
            h.PlacePropertyOnBoard(p1, PropertyColor.DarkBlue, 1);

            // Inject a multi-color wild and play it as unbound (goes to UnboundWilds)
            var wild = h.InjectMulticolorWild(p1);
            await h.PlayCardAsync(p1, wild.Id, new PlayCardRequest()); // plays to unbound

            // Now move the unbound wild to the DarkBlue set — that should complete it and trigger win
            var p1State = h.GetPlayerState(p1, p1);
            var darkBlueSet = p1State!.PropertySets.First(s => s.Color == PropertyColor.DarkBlue);
            await h.Game.MovePropertyAsync(p1, wild.Id, darkBlueSet.SetId, null);

            Assert.Equal(GamePhase.GameOver, h.GetPhase(p1));
        }

        [Fact]
        public async Task MoveProperty_NotCompletingWin_StaysInPlay()
        {
            // Moving a card that doesn't complete a 3rd set must not end the game.
            var h = new TestGameHarness();
            var (p1, _) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            h.PlaceCompleteSet(p1, PropertyColor.Brown);
            h.PlaceCompleteSet(p1, PropertyColor.Utility);

            var set1 = h.PlacePropertyOnBoard(p1, PropertyColor.Green, 1);
            var set2 = h.PlacePropertyOnBoard(p1, PropertyColor.Green, 1);
            var cardToMove = set2.Cards[0];

            await h.Game.MovePropertyAsync(p1, cardToMove.Id, set1.SetId, null);

            Assert.NotEqual(GamePhase.GameOver, h.GetPhase(p1));
        }

        [Fact]
        public async Task FlipWildcard_CompletingThirdSet_TriggersWin()
        {
            // Player has 2 complete sets plus a DarkBlue set that needs 1 more card.
            // A dual-color wild currently in the wrong color can be flipped to complete DarkBlue.
            var h = new TestGameHarness();
            var (p1, _) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            h.PlaceCompleteSet(p1, PropertyColor.Brown);
            h.PlaceCompleteSet(p1, PropertyColor.Utility);

            // 1 of 2 DarkBlue cards already on board
            h.PlacePropertyOnBoard(p1, PropertyColor.DarkBlue, 1);

            // Play a DarkBlue/Green wildcard as Green (wrong side for DarkBlue)
            var wild = h.InjectPropertyWildcard(p1, PropertyColor.DarkBlue, PropertyColor.Green, 4);
            await h.PlayCardAsync(p1, wild.Id, new PlayCardRequest { WildcardColor = PropertyColor.Green });

            // Game should not be over yet
            Assert.NotEqual(GamePhase.GameOver, h.GetPhase(p1));

            // Flip the wild to DarkBlue — this should complete the DarkBlue set → win
            await h.Game.FlipWildcardAsync(p1, wild.Id);

            Assert.Equal(GamePhase.GameOver, h.GetPhase(p1));
        }

        [Fact]
        public async Task FlipWildcard_NotCompletingWin_StaysInPlay()
        {
            // Flipping a wildcard that doesn't create a 3rd complete set must not end the game.
            var h = new TestGameHarness();
            var (p1, _) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            h.PlaceCompleteSet(p1, PropertyColor.Brown);

            var wild = h.InjectPropertyWildcard(p1, PropertyColor.Red, PropertyColor.Yellow, 3);
            await h.PlayCardAsync(p1, wild.Id, new PlayCardRequest { WildcardColor = PropertyColor.Red });

            await h.Game.FlipWildcardAsync(p1, wild.Id);

            Assert.NotEqual(GamePhase.GameOver, h.GetPhase(p1));
        }
    }
}
