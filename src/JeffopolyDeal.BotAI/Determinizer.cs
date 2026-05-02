using System;
using System.Collections.Generic;
using System.Linq;
using JeffopolyDeal.Models;

namespace JeffopolyDeal.ISMCTS
{
    // =========================================================================
    // Determinizer.cs — Information Set Sampling for ISMCTS
    // =========================================================================
    //
    // In ISMCTS, the bot knows its own hand and all public information (board
    // state, bank cards, discard pile, hand sizes) but does NOT know what cards
    // opponents hold or what's in the draw pile. The total card pool is known
    // (106 cards), so by subtracting all visible cards we get the "unknown pool."
    //
    // Determinization creates a plausible concrete game state by:
    //   1. Computing the unknown pool (all cards not visible to the bot)
    //   2. Shuffling the unknown pool randomly
    //   3. Dealing cards to each opponent to match their known hand size
    //   4. Placing remaining cards as the draw pile
    //
    // Each call to Determinize() produces a different possible world, and
    // ISMCTS averages results across many such worlds to handle uncertainty.
    //
    // IMPORTANT: The determinizer operates on a CLONED state. It modifies
    // the clone's opponent hands and deck in place.
    // =========================================================================

    /// <summary>
    /// Creates plausible concrete game states from the bot's partial information.
    /// Each determinization samples a different possible distribution of hidden cards.
    /// </summary>
    public static class Determinizer
    {
        /// <summary>
        /// Determinize a simulation state: fill in opponent hands and the draw pile
        /// by sampling from the unknown card pool.
        /// 
        /// Preconditions:
        ///   - state.Players[botIndex].Hand contains the bot's actual hand
        ///   - Opponent hands may be empty (FromGame leaves them empty)
        ///   - state.Deck.DiscardPile contains the actual discard pile
        ///   - state.Deck.DrawPile may be empty (to be filled here)
        ///
        /// The method needs the original opponent hand sizes to know how many
        /// cards to deal to each opponent.
        /// 
        /// Parameters:
        ///   state              — the simulation state to fill in (MUTATED)
        ///   botIndex           — which player is the bot (their hand is known)
        ///   opponentHandSizes  — hand size for each player index (bot's entry is ignored)
        ///   allKnownCards      — complete list of all 106 cards in the game
        ///   rng                — random number generator for shuffling
        /// </summary>
        public static void Determinize(
            SimulationState state,
            int botIndex,
            int[] opponentHandSizes,
            List<Card> allKnownCards,
            Random rng)
        {
            // Step 1: Collect ALL cards that the bot can see (known cards).
            // These cards are NOT in the unknown pool.
            var visibleCardIds = new HashSet<int>();

            // Bot's own hand — bot knows exactly what they have
            foreach (var card in state.Players[botIndex].Hand)
                visibleCardIds.Add(card.Id);

            // All players' banks — visible to everyone
            foreach (var player in state.Players)
                foreach (var card in player.Bank)
                    visibleCardIds.Add(card.Id);

            // All players' property sets — visible to everyone
            foreach (var player in state.Players)
                foreach (var set in player.PropertySets)
                    foreach (var card in set.Cards)
                        visibleCardIds.Add(card.Id);

            // All players' unbound wilds — visible to everyone
            foreach (var player in state.Players)
                foreach (var card in player.UnboundWilds)
                    visibleCardIds.Add(card.Id);

            // Discard pile — public knowledge
            foreach (var card in state.Deck.DiscardPile)
                visibleCardIds.Add(card.Id);

            // Step 2: Build the unknown pool — all cards NOT visible to the bot.
            // This pool contains opponent hand cards + draw pile cards (indistinguishable).
            var unknownPool = allKnownCards
                .Where(c => !visibleCardIds.Contains(c.Id))
                .ToList();

            // Step 3: Shuffle the unknown pool (Fisher-Yates).
            // This randomization is the core of determinization — each shuffle
            // produces a different possible world.
            Shuffle(unknownPool, rng);

            // Step 4: Deal cards to opponents to match their hand sizes.
            // The bot's hand is already populated; we skip it.
            int dealIndex = 0;
            for (int i = 0; i < state.PlayerCount; i++)
            {
                if (i == botIndex) continue; // bot's hand is already set

                int handSize = opponentHandSizes[i];
                var hand = new List<Card>();

                // Deal 'handSize' cards from the shuffled unknown pool
                for (int j = 0; j < handSize && dealIndex < unknownPool.Count; j++)
                {
                    hand.Add(unknownPool[dealIndex++]);
                }

                state.Players[i].Hand = hand;
            }

            // Step 5: Remaining unknown cards become the draw pile.
            // These are the cards no one is holding — they're in the deck.
            state.Deck.DrawPile = unknownPool.Skip(dealIndex).ToList();
        }

        /// <summary>
        /// Fisher-Yates shuffle — the standard O(n) unbiased shuffle algorithm.
        /// Shuffles the list in place.
        /// </summary>
        private static void Shuffle<T>(List<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>
        /// Build the complete list of all cards in the game. This is called once
        /// at the start of ISMCTS and reused across all determinizations.
        /// 
        /// Collects cards from:
        ///   - All players' hands, banks, property sets, unbound wilds
        ///   - Draw pile and discard pile
        /// 
        /// This should total 106 cards (the playable Monopoly Deal deck).
        /// </summary>
        public static List<Card> CollectAllCards(
            Player bot,
            List<Player> allPlayers,
            Deck deck)
        {
            var allCards = new HashSet<int>(); // track by ID to avoid duplicates
            var result = new List<Card>();

            void Add(Card c)
            {
                if (allCards.Add(c.Id))
                    result.Add(c);
            }

            // All players' cards
            foreach (var player in allPlayers)
            {
                foreach (var c in player.Hand) Add(c);
                foreach (var c in player.Bank) Add(c);
                foreach (var set in player.PropertySets)
                    foreach (var c in set.Cards) Add(c);
                foreach (var c in player.UnboundWilds) Add(c);
            }

            // Deck cards
            foreach (var c in deck.GetDrawPileSnapshot()) Add(c);
            foreach (var c in deck.GetDiscardPileSnapshot()) Add(c);

            return result;
        }
    }
}
