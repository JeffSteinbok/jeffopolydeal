using System;
using System.Collections.Generic;
using System.Linq;
using JeffopolyDeal.Models;

namespace JeffopolyDeal.ISMCTS
{
    // =========================================================================
    // GameSimulator.cs — Synchronous game engine for ISMCTS rollouts
    // =========================================================================
    //
    // This is a stripped-down, synchronous re-implementation of the core game
    // logic from Game.cs. It operates on SimulationState instead of real game
    // objects, has no SignalR/async/locks, and is designed to be called
    // thousands of times during Monte Carlo simulations.
    //
    // Key differences from the real Game:
    //   - No networking or UI notifications
    //   - No connection tracking or reconnection logic
    //   - Discard is automatic (lowest value cards discarded first)
    //   - Pending actions are resolved immediately using a policy function
    //   - No game action log / recent actions tracking
    //
    // The simulator handles all card types:
    //   Money, Property, PropertyWildcard, Rent (+ DoubleTheRent), PassGo,
    //   DebtCollector, Birthday, SlyDeal, ForceDeal, DealBreaker, House, Hotel
    //
    // Just Say No is handled by the response policy — the simulator asks the
    // policy whether to play JSN and processes the chain accordingly.
    // =========================================================================

    /// <summary>
    /// A move that can be played in the simulation. Represents a single card play
    /// along with all the targeting/configuration needed to execute it.
    /// </summary>
    public class SimMove
    {
        /// <summary>
        /// Special sentinel: the player chooses to end their turn early
        /// (stop playing cards even though they have plays remaining).
        /// </summary>
        public bool IsEndTurn { get; set; }

        /// <summary>The card being played from hand.</summary>
        public Card? Card { get; set; }

        /// <summary>If true, the card is banked as money instead of played for its effect.</summary>
        public bool PlayAsMoney { get; set; }

        /// <summary>For rent cards: which color to charge rent for.</summary>
        public PropertyColor? RentColor { get; set; }

        /// <summary>For wild rent and targeted actions: index of the target player.</summary>
        public int TargetPlayerIndex { get; set; } = -1;

        /// <summary>For Sly Deal / Force Deal: the card ID to steal from the target.</summary>
        public int? TargetCardId { get; set; }

        /// <summary>For Force Deal: the card ID the bot is offering in exchange.</summary>
        public int? OfferedCardId { get; set; }

        /// <summary>For Deal Breaker: the color of the complete set to steal.</summary>
        public PropertyColor? TargetSetColor { get; set; }

        /// <summary>For PropertyWildcard: which color to assign the wildcard to.</summary>
        public PropertyColor? WildcardColor { get; set; }

        /// <summary>
        /// For rent plays: list of DoubleTheRent card IDs to attach.
        /// Each DTR doubles the rent and consumes one additional play.
        /// </summary>
        public List<int>? DoubleRentCardIds { get; set; }

        /// <summary>
        /// Creates a human-readable description for debugging.
        /// </summary>
        public override string ToString()
        {
            if (IsEndTurn) return "EndTurn";
            if (Card == null) return "NoOp";
            if (PlayAsMoney) return $"Bank({Card.Name} ${Card.MoneyValue})";
            return $"Play({Card.Name} -> {Card.CardType})";
        }
    }

    /// <summary>
    /// Synchronous game engine that processes moves on a SimulationState.
    /// All methods mutate the state in-place for performance.
    /// </summary>
    public static class GameSimulator
    {
        // =====================================================================
        // Main entry point: simulate a full game to completion
        // =====================================================================

        /// <summary>
        /// Run the game from the current state until someone wins or we hit
        /// the turn limit (to prevent infinite games in degenerate cases).
        /// 
        /// The movePolicy function is called each time a player needs to
        /// choose a move. During ISMCTS rollouts, this is typically the
        /// RolloutPolicy (CardEvaluator-based heuristic).
        /// 
        /// The responsePolicy function is called when a player must respond
        /// to a pending action (pay rent, play JSN, etc.).
        /// 
        /// Returns the index of the winning player, or -1 if the turn limit
        /// was reached without a winner.
        /// </summary>
        /// <param name="state">The game state to simulate forward. MUTATED in place.</param>
        /// <param name="movePolicy">Given state and player index, returns the move to play.</param>
        /// <param name="responsePolicy">Given state and responding player index, returns whether to JSN and payment cards.</param>
        /// <param name="maxTurns">Safety limit to prevent infinite simulation loops.</param>
        public static int SimulateToEnd(
            SimulationState state,
            Func<SimulationState, int, SimMove> movePolicy,
            Func<SimulationState, int, SimActionResponse> responsePolicy,
            int maxTurns = 50)
        {
            int turnCount = 0;

            while (state.Phase != SimPhase.GameOver && turnCount < maxTurns)
            {
                // If there's a pending action, resolve it first
                if (state.Phase == SimPhase.AwaitingResponse && state.PendingAction != null)
                {
                    ResolvePendingAction(state, responsePolicy);
                    continue; // re-check phase after resolution
                }

                // Current player takes their turn
                PlayFullTurn(state, movePolicy, responsePolicy);
                turnCount++;

                if (state.Phase == SimPhase.GameOver)
                    break;
            }

            return state.WinnerIndex;
        }

        // =====================================================================
        // Turn structure
        // =====================================================================

        /// <summary>
        /// Play a full turn for the current player:
        ///   1. Draw cards (2 normally, 5 if hand is empty)
        ///   2. Play up to 3 cards using the move policy
        ///   3. Discard down to 7 cards (auto-selects lowest value)
        ///   4. Advance to the next player
        /// </summary>
        private static void PlayFullTurn(
            SimulationState state,
            Func<SimulationState, int, SimMove> movePolicy,
            Func<SimulationState, int, SimActionResponse> responsePolicy)
        {
            var player = state.CurrentPlayer;

            // --- Phase 1: Draw ---
            int drawCount = player.Hand.Count == 0
                ? GameConfig.DrawWhenEmpty  // draw 5 if hand is empty
                : GameConfig.DrawPerTurn;   // draw 2 normally
            var drawn = state.Deck.Draw(drawCount);
            player.Hand.AddRange(drawn);

            // --- Phase 2: Play up to 3 cards ---
            state.PlaysRemaining = GameConfig.MaxPlaysPerTurn;
            state.Phase = SimPhase.Playing;

            while (state.PlaysRemaining > 0 && player.Hand.Count > 0
                   && state.Phase == SimPhase.Playing)
            {
                var move = movePolicy(state, state.CurrentPlayerIndex);

                // Player chose to stop playing early
                if (move.IsEndTurn) break;

                // Execute the move
                ExecuteMove(state, state.CurrentPlayerIndex, move, responsePolicy);

                // Check for win after each play
                if (CheckWin(state)) return;
            }

            // --- Phase 3: Auto-discard down to hand limit ---
            AutoDiscard(state, state.CurrentPlayerIndex);

            // --- Phase 4: Advance to next player ---
            AdvanceTurn(state);
        }

        /// <summary>
        /// Move to the next player's turn. Wraps around to player 0
        /// after the last player.
        /// </summary>
        private static void AdvanceTurn(SimulationState state)
        {
            state.CurrentPlayerIndex = (state.CurrentPlayerIndex + 1) % state.PlayerCount;
            state.PlaysRemaining = GameConfig.MaxPlaysPerTurn;
            state.Phase = SimPhase.Playing;
        }

        /// <summary>
        /// Auto-discard cards until the player is at or below the hand limit (7).
        /// Discards the lowest money-value cards first (least strategic loss).
        /// </summary>
        private static void AutoDiscard(SimulationState state, int playerIndex)
        {
            var player = state.Players[playerIndex];
            while (player.Hand.Count > GameConfig.MaxHandSize)
            {
                // Find the card with the lowest money value to discard
                var worst = player.Hand.OrderBy(c => c.MoneyValue).First();
                player.Hand.Remove(worst);
                state.Deck.Discard(worst);
            }
        }

        // =====================================================================
        // Move execution — routes to the appropriate handler by card type
        // =====================================================================

        /// <summary>
        /// Execute a single move: remove the card from hand and process its effect.
        /// Decrements PlaysRemaining. If the card creates a pending action (rent,
        /// steal, etc.), resolves it immediately using the response policy.
        /// </summary>
        public static void ExecuteMove(
            SimulationState state,
            int playerIndex,
            SimMove move,
            Func<SimulationState, int, SimActionResponse> responsePolicy)
        {
            if (move.IsEndTurn || move.Card == null) return;

            var player = state.Players[playerIndex];
            var card = move.Card;

            // Remove card from hand
            player.Hand.Remove(card);
            state.PlaysRemaining--;

            // If playing as money, just bank it
            if (move.PlayAsMoney)
            {
                player.Bank.Add(card);
                return;
            }

            // Route by card type
            switch (card.CardType)
            {
                case CardType.Money:
                    // Money cards are always banked
                    player.Bank.Add(card);
                    break;

                case CardType.Property:
                    PlayProperty(state, playerIndex, card);
                    break;

                case CardType.PropertyWildcard:
                    PlayWildcard(state, playerIndex, card, move.WildcardColor);
                    break;

                case CardType.Rent:
                    PlayRent(state, playerIndex, card, move, responsePolicy);
                    break;

                case CardType.Action:
                    PlayAction(state, playerIndex, card, move, responsePolicy);
                    break;
            }
        }

        // =====================================================================
        // Card type handlers
        // =====================================================================

        /// <summary>
        /// Play a property card: add it to the appropriate color set.
        /// </summary>
        private static void PlayProperty(SimulationState state, int playerIndex, Card card)
        {
            var player = state.Players[playerIndex];
            var color = card.Color ?? PropertyColor.Brown; // fallback shouldn't happen
            var set = player.GetOrCreatePropertySet(color);
            card.ActiveColor = color;
            set.Cards.Add(card);
        }

        /// <summary>
        /// Play a wildcard property: assign it to the specified color
        /// (or its primary color if no choice specified).
        /// Multi-color wilds go to UnboundWilds if no color specified.
        /// </summary>
        private static void PlayWildcard(
            SimulationState state, int playerIndex, Card card, PropertyColor? chosenColor)
        {
            var player = state.Players[playerIndex];

            if (card.IsMulticolorWild)
            {
                // Multi-color wild: needs a color assignment
                if (chosenColor.HasValue)
                {
                    card.ActiveColor = chosenColor.Value;
                    var set = player.GetOrCreatePropertySet(chosenColor.Value);
                    set.Cards.Add(card);
                }
                else
                {
                    // No color chosen — park in unbound wilds
                    player.UnboundWilds.Add(card);
                }
            }
            else
            {
                // Dual-color wild: use the chosen color or default to primary
                var color = chosenColor ?? card.Color ?? PropertyColor.Brown;
                card.ActiveColor = color;
                var set = player.GetOrCreatePropertySet(color);
                set.Cards.Add(card);
            }
        }

        /// <summary>
        /// Play a rent card: calculate rent amount, apply DoubleTheRent multiplier,
        /// then create and immediately resolve a pending action for payment.
        /// 
        /// Standard rent hits ALL opponents. Wild rent targets ONE player.
        /// </summary>
        private static void PlayRent(
            SimulationState state, int playerIndex, Card card, SimMove move,
            Func<SimulationState, int, SimActionResponse> responsePolicy)
        {
            var player = state.Players[playerIndex];

            // Determine which color to charge rent for
            var rentColor = move.RentColor;
            if (rentColor == null) return; // shouldn't happen if move is valid

            // Find the property set matching the rent color
            var rentSet = player.PropertySets
                .Where(s => s.Color == rentColor.Value && s.Cards.Count > 0)
                .OrderByDescending(s => s.CalculateRent())
                .FirstOrDefault();

            if (rentSet == null) return; // no properties of this color

            // Calculate base rent from the rent table
            int rentAmount = rentSet.CalculateRent();

            // Apply DoubleTheRent cards (each one doubles the rent)
            int dtrCount = 0;
            if (move.DoubleRentCardIds != null)
            {
                foreach (var dtrId in move.DoubleRentCardIds)
                {
                    var dtr = player.Hand.FirstOrDefault(c => c.Id == dtrId);
                    if (dtr != null)
                    {
                        player.Hand.Remove(dtr);
                        state.Deck.Discard(dtr);
                        rentAmount *= 2;
                        dtrCount++;
                    }
                }
                // Each DTR card used costs an additional play
                state.PlaysRemaining -= dtrCount;
            }

            // Discard the rent card itself
            state.Deck.Discard(card);

            // Determine targets: wild rent = one player, standard = all opponents
            var targets = new List<int>();
            if (card.IsWildRent && move.TargetPlayerIndex >= 0)
            {
                targets.Add(move.TargetPlayerIndex);
            }
            else
            {
                // Standard rent: all other players
                for (int i = 0; i < state.PlayerCount; i++)
                {
                    if (i != playerIndex) targets.Add(i);
                }
            }

            // Create pending action and resolve it immediately
            var pending = new SimPendingAction
            {
                Type = PendingActionType.PayRent,
                SourcePlayerIndex = playerIndex,
                TargetPlayerIndices = targets,
                Amount = rentAmount,
            };

            ResolvePaymentAction(state, pending, responsePolicy);
        }

        /// <summary>
        /// Play an action card: route to the specific action handler.
        /// The card is discarded after use (except PassGo which is also discarded).
        /// </summary>
        private static void PlayAction(
            SimulationState state, int playerIndex, Card card, SimMove move,
            Func<SimulationState, int, SimActionResponse> responsePolicy)
        {
            // Discard the action card (it's used up)
            state.Deck.Discard(card);

            switch (card.ActionKind)
            {
                case ActionType.PassGo:
                    // Draw 2 additional cards
                    var drawn = state.Deck.Draw(2);
                    state.Players[playerIndex].Hand.AddRange(drawn);
                    break;

                case ActionType.DebtCollector:
                    PlayDebtCollector(state, playerIndex, move, responsePolicy);
                    break;

                case ActionType.ItsMyBirthday:
                    PlayBirthday(state, playerIndex, responsePolicy);
                    break;

                case ActionType.SlyDeal:
                    PlaySlyDeal(state, playerIndex, move, responsePolicy);
                    break;

                case ActionType.ForceDeal:
                    PlayForceDeal(state, playerIndex, move, responsePolicy);
                    break;

                case ActionType.DealBreaker:
                    PlayDealBreaker(state, playerIndex, move, responsePolicy);
                    break;

                case ActionType.House:
                    PlayHouseOrHotel(state, playerIndex, move, isHotel: false);
                    break;

                case ActionType.Hotel:
                    PlayHouseOrHotel(state, playerIndex, move, isHotel: true);
                    break;

                // JustSayNo and DoubleTheRent are not played as standalone actions
                // from the move generator — JSN is reactive, DTR is bundled with rent
                default:
                    break;
            }
        }

        // =====================================================================
        // Specific action handlers
        // =====================================================================

        /// <summary>
        /// Debt Collector: target ONE player, they owe $5.
        /// </summary>
        private static void PlayDebtCollector(
            SimulationState state, int playerIndex, SimMove move,
            Func<SimulationState, int, SimActionResponse> responsePolicy)
        {
            if (move.TargetPlayerIndex < 0) return;

            var pending = new SimPendingAction
            {
                Type = PendingActionType.PayDebtCollector,
                SourcePlayerIndex = playerIndex,
                TargetPlayerIndices = new List<int> { move.TargetPlayerIndex },
                Amount = GameConfig.DebtCollectorAmount,
            };

            ResolvePaymentAction(state, pending, responsePolicy);
        }

        /// <summary>
        /// It's My Birthday: ALL opponents owe $2.
        /// </summary>
        private static void PlayBirthday(
            SimulationState state, int playerIndex,
            Func<SimulationState, int, SimActionResponse> responsePolicy)
        {
            var targets = new List<int>();
            for (int i = 0; i < state.PlayerCount; i++)
            {
                if (i != playerIndex) targets.Add(i);
            }

            var pending = new SimPendingAction
            {
                Type = PendingActionType.PayBirthday,
                SourcePlayerIndex = playerIndex,
                TargetPlayerIndices = targets,
                Amount = GameConfig.BirthdayAmount,
            };

            ResolvePaymentAction(state, pending, responsePolicy);
        }

        /// <summary>
        /// Sly Deal: steal one property card from an opponent's INCOMPLETE set.
        /// Can be countered with Just Say No.
        /// </summary>
        private static void PlaySlyDeal(
            SimulationState state, int playerIndex, SimMove move,
            Func<SimulationState, int, SimActionResponse> responsePolicy)
        {
            if (move.TargetPlayerIndex < 0 || !move.TargetCardId.HasValue) return;

            // Check if target plays JSN
            var pending = new SimPendingAction
            {
                Type = PendingActionType.RespondToSlyDeal,
                SourcePlayerIndex = playerIndex,
                TargetPlayerIndices = new List<int> { move.TargetPlayerIndex },
                TargetCardId = move.TargetCardId,
            };

            if (TryJustSayNo(state, pending, move.TargetPlayerIndex, responsePolicy))
                return; // action was blocked

            // Execute the steal
            ExecuteSlyDeal(state, playerIndex, move.TargetPlayerIndex, move.TargetCardId.Value);
        }

        /// <summary>
        /// Force Deal: swap one of your properties for one of an opponent's.
        /// Both must be from INCOMPLETE sets. Can be countered with JSN.
        /// </summary>
        private static void PlayForceDeal(
            SimulationState state, int playerIndex, SimMove move,
            Func<SimulationState, int, SimActionResponse> responsePolicy)
        {
            if (move.TargetPlayerIndex < 0 || !move.TargetCardId.HasValue || !move.OfferedCardId.HasValue)
                return;

            var pending = new SimPendingAction
            {
                Type = PendingActionType.RespondToForceDeal,
                SourcePlayerIndex = playerIndex,
                TargetPlayerIndices = new List<int> { move.TargetPlayerIndex },
                TargetCardId = move.TargetCardId,
                OfferedCardId = move.OfferedCardId,
            };

            if (TryJustSayNo(state, pending, move.TargetPlayerIndex, responsePolicy))
                return; // action was blocked

            // Execute the swap
            ExecuteForceDeal(state, playerIndex, move.TargetPlayerIndex,
                move.TargetCardId.Value, move.OfferedCardId.Value);
        }

        /// <summary>
        /// Deal Breaker: steal an entire COMPLETE property set from an opponent.
        /// The most powerful action card — usually countered with JSN.
        /// </summary>
        private static void PlayDealBreaker(
            SimulationState state, int playerIndex, SimMove move,
            Func<SimulationState, int, SimActionResponse> responsePolicy)
        {
            if (move.TargetPlayerIndex < 0 || !move.TargetSetColor.HasValue) return;

            var pending = new SimPendingAction
            {
                Type = PendingActionType.RespondToDealBreaker,
                SourcePlayerIndex = playerIndex,
                TargetPlayerIndices = new List<int> { move.TargetPlayerIndex },
                TargetSetColor = move.TargetSetColor,
            };

            if (TryJustSayNo(state, pending, move.TargetPlayerIndex, responsePolicy))
                return; // action was blocked

            // Execute: move the entire set to the attacker
            ExecuteDealBreaker(state, playerIndex, move.TargetPlayerIndex, move.TargetSetColor.Value);
        }

        /// <summary>
        /// House/Hotel: add to a complete property set to increase its rent.
        /// House must be added before hotel. Cannot be added to Railroad or Utility.
        /// </summary>
        private static void PlayHouseOrHotel(
            SimulationState state, int playerIndex, SimMove move, bool isHotel)
        {
            if (!move.TargetSetColor.HasValue) return;
            var player = state.Players[playerIndex];

            var set = player.PropertySets.FirstOrDefault(s =>
                s.Color == move.TargetSetColor.Value && s.IsComplete);
            if (set == null) return;

            if (isHotel)
                set.HasHotel = true;
            else
                set.HasHouse = true;
        }

        // =====================================================================
        // Steal / swap execution helpers
        // =====================================================================

        /// <summary>
        /// Execute a Sly Deal: move one card from target's incomplete set to attacker.
        /// </summary>
        private static void ExecuteSlyDeal(
            SimulationState state, int attackerIndex, int targetIndex, int cardId)
        {
            var target = state.Players[targetIndex];
            var attacker = state.Players[attackerIndex];

            // Find the card in target's property sets or unbound wilds
            Card? card = null;
            SimPropertySet? sourceSet = null;

            foreach (var set in target.PropertySets)
            {
                card = set.Cards.FirstOrDefault(c => c.Id == cardId);
                if (card != null) { sourceSet = set; break; }
            }

            if (card == null)
            {
                card = target.UnboundWilds.FirstOrDefault(c => c.Id == cardId);
                if (card != null) target.UnboundWilds.Remove(card);
            }
            else
            {
                sourceSet!.Cards.Remove(card);
                // Clean up empty sets
                if (sourceSet.Cards.Count == 0)
                    target.PropertySets.Remove(sourceSet);
            }

            if (card == null) return;

            // Add to attacker's property sets
            var color = card.ActiveColor ?? card.Color ?? PropertyColor.Brown;
            var destSet = attacker.GetOrCreatePropertySet(color);
            destSet.Cards.Add(card);
        }

        /// <summary>
        /// Execute a Force Deal: swap cards between two players.
        /// </summary>
        private static void ExecuteForceDeal(
            SimulationState state, int attackerIndex, int targetIndex,
            int takeCardId, int giveCardId)
        {
            var attacker = state.Players[attackerIndex];
            var target = state.Players[targetIndex];

            // Remove the taken card from target
            Card? takeCard = RemovePropertyCard(target, takeCardId);
            // Remove the given card from attacker
            Card? giveCard = RemovePropertyCard(attacker, giveCardId);

            if (takeCard == null || giveCard == null) return;

            // Add taken card to attacker
            var takeColor = takeCard.ActiveColor ?? takeCard.Color ?? PropertyColor.Brown;
            attacker.GetOrCreatePropertySet(takeColor).Cards.Add(takeCard);

            // Add given card to target
            var giveColor = giveCard.ActiveColor ?? giveCard.Color ?? PropertyColor.Brown;
            target.GetOrCreatePropertySet(giveColor).Cards.Add(giveCard);
        }

        /// <summary>
        /// Execute a Deal Breaker: move an entire complete set from target to attacker.
        /// House and hotel status transfer with the set.
        /// </summary>
        private static void ExecuteDealBreaker(
            SimulationState state, int attackerIndex, int targetIndex, PropertyColor setColor)
        {
            var target = state.Players[targetIndex];
            var attacker = state.Players[attackerIndex];

            // Find the complete set of the specified color
            var set = target.PropertySets.FirstOrDefault(s =>
                s.Color == setColor && s.IsComplete);
            if (set == null) return;

            // Remove from target
            target.PropertySets.Remove(set);

            // Add to attacker (transfer the whole set object)
            attacker.PropertySets.Add(set);
        }

        /// <summary>
        /// Helper: remove a property card from a player's sets or unbound wilds.
        /// Returns the removed card, or null if not found.
        /// </summary>
        private static Card? RemovePropertyCard(SimPlayer player, int cardId)
        {
            foreach (var set in player.PropertySets)
            {
                var card = set.Cards.FirstOrDefault(c => c.Id == cardId);
                if (card != null)
                {
                    set.Cards.Remove(card);
                    if (set.Cards.Count == 0)
                        player.PropertySets.Remove(set);
                    return card;
                }
            }

            var wild = player.UnboundWilds.FirstOrDefault(c => c.Id == cardId);
            if (wild != null)
            {
                player.UnboundWilds.Remove(wild);
                return wild;
            }

            return null;
        }

        // =====================================================================
        // Pending action resolution — handles payment and JSN chains
        // =====================================================================

        /// <summary>
        /// Resolve all pending payment actions: each target player either pays
        /// or plays Just Say No. The response policy decides for each player.
        /// </summary>
        private static void ResolvePaymentAction(
            SimulationState state,
            SimPendingAction pending,
            Func<SimulationState, int, SimActionResponse> responsePolicy)
        {
            // Process each target player
            foreach (var targetIndex in pending.TargetPlayerIndices.ToList())
            {
                // Check if target wants to play JSN
                if (TryJustSayNo(state, pending, targetIndex, responsePolicy))
                    continue; // this player blocked the action

                // Get payment from the target
                var target = state.Players[targetIndex];
                var source = state.Players[pending.SourcePlayerIndex];

                // Use the response policy to determine payment
                state.PendingAction = pending;
                var response = responsePolicy(state, targetIndex);
                state.PendingAction = null;

                // Transfer payment cards from target to source's bank
                if (response.PaymentCardIds != null)
                {
                    foreach (var cardId in response.PaymentCardIds)
                    {
                        var card = FindAndRemovePayableCard(target, cardId);
                        if (card != null)
                        {
                            source.Bank.Add(card);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Attempt to play Just Say No for the target player. Handles the full
        /// JSN chain: if target plays JSN, source can counter with their own JSN,
        /// and so on until someone doesn't have one or chooses not to play it.
        /// 
        /// Returns true if the action was ultimately blocked (target won the JSN chain).
        /// </summary>
        private static bool TryJustSayNo(
            SimulationState state,
            SimPendingAction pending,
            int targetIndex,
            Func<SimulationState, int, SimActionResponse> responsePolicy)
        {
            // Set up state so the response policy can see what's happening
            state.PendingAction = pending;
            state.Phase = SimPhase.AwaitingResponse;

            var response = responsePolicy(state, targetIndex);

            if (!response.PlayJustSayNo)
            {
                state.Phase = SimPhase.Playing;
                state.PendingAction = null;
                return false; // no JSN played
            }

            // Target plays JSN — remove it from their hand
            var target = state.Players[targetIndex];
            var jsn = target.Hand.FirstOrDefault(c => c.ActionKind == ActionType.JustSayNo);
            if (jsn == null)
            {
                // Tried to play JSN but doesn't have one — treat as no JSN
                state.Phase = SimPhase.Playing;
                state.PendingAction = null;
                return false;
            }
            target.Hand.Remove(jsn);
            state.Deck.Discard(jsn);

            // Now the source can counter with their own JSN
            int sourceIndex = pending.SourcePlayerIndex;
            var sourcePlayer = state.Players[sourceIndex];
            var sourceJsn = sourcePlayer.Hand.FirstOrDefault(c => c.ActionKind == ActionType.JustSayNo);

            if (sourceJsn != null)
            {
                // Ask the source if they want to counter
                var counterResponse = responsePolicy(state, sourceIndex);
                if (counterResponse.PlayJustSayNo)
                {
                    sourcePlayer.Hand.Remove(sourceJsn);
                    state.Deck.Discard(sourceJsn);

                    // Source countered — action goes through (recursion handles deeper chains)
                    state.Phase = SimPhase.Playing;
                    state.PendingAction = null;
                    return false;
                }
            }

            // JSN stands — action is blocked
            state.Phase = SimPhase.Playing;
            state.PendingAction = null;
            return true;
        }

        /// <summary>
        /// Resolve a pending action that was set before simulation started
        /// (e.g., the game was in AwaitingResponse phase when the bot needed
        /// to make a decision). Uses the response policy for all responders.
        /// </summary>
        private static void ResolvePendingAction(
            SimulationState state,
            Func<SimulationState, int, SimActionResponse> responsePolicy)
        {
            if (state.PendingAction == null) return;

            var pending = state.PendingAction;

            // Payment-type actions
            if (pending.Type == PendingActionType.PayRent ||
                pending.Type == PendingActionType.PayDebtCollector ||
                pending.Type == PendingActionType.PayBirthday)
            {
                ResolvePaymentAction(state, pending, responsePolicy);
            }
            // Steal-type actions — these are auto-resolved (no payment needed)
            else if (pending.Type == PendingActionType.RespondToSlyDeal)
            {
                foreach (var ti in pending.TargetPlayerIndices)
                {
                    if (!TryJustSayNo(state, pending, ti, responsePolicy) && pending.TargetCardId.HasValue)
                        ExecuteSlyDeal(state, pending.SourcePlayerIndex, ti, pending.TargetCardId.Value);
                }
            }
            else if (pending.Type == PendingActionType.RespondToForceDeal)
            {
                foreach (var ti in pending.TargetPlayerIndices)
                {
                    if (!TryJustSayNo(state, pending, ti, responsePolicy)
                        && pending.TargetCardId.HasValue && pending.OfferedCardId.HasValue)
                        ExecuteForceDeal(state, pending.SourcePlayerIndex, ti,
                            pending.TargetCardId.Value, pending.OfferedCardId.Value);
                }
            }
            else if (pending.Type == PendingActionType.RespondToDealBreaker)
            {
                foreach (var ti in pending.TargetPlayerIndices)
                {
                    if (!TryJustSayNo(state, pending, ti, responsePolicy) && pending.TargetSetColor.HasValue)
                        ExecuteDealBreaker(state, pending.SourcePlayerIndex, ti, pending.TargetSetColor.Value);
                }
            }

            // Clear the pending action — turn resumes
            state.PendingAction = null;
            state.Phase = SimPhase.Playing;
        }

        /// <summary>
        /// Find a card in a player's bank or property sets and remove it.
        /// Used for payment processing. Checks bank first, then property sets.
        /// </summary>
        private static Card? FindAndRemovePayableCard(SimPlayer player, int cardId)
        {
            // Check bank first
            var card = player.Bank.FirstOrDefault(c => c.Id == cardId);
            if (card != null)
            {
                player.Bank.Remove(card);
                return card;
            }

            // Check property sets
            foreach (var set in player.PropertySets.ToList())
            {
                card = set.Cards.FirstOrDefault(c => c.Id == cardId);
                if (card != null)
                {
                    set.Cards.Remove(card);
                    // If the set lost its house/hotel requirement, clear those flags
                    if (!set.IsComplete)
                    {
                        set.HasHouse = false;
                        set.HasHotel = false;
                    }
                    // Remove empty sets
                    if (set.Cards.Count == 0)
                        player.PropertySets.Remove(set);
                    return card;
                }
            }

            return null;
        }

        // =====================================================================
        // Win detection
        // =====================================================================

        /// <summary>
        /// Check if any player has won (3 unique completed property sets).
        /// If so, set the game phase to GameOver and record the winner.
        /// </summary>
        private static bool CheckWin(SimulationState state)
        {
            for (int i = 0; i < state.PlayerCount; i++)
            {
                if (state.Players[i].UniqueCompletedSetCount >= GameConfig.SetsToWin)
                {
                    state.WinnerIndex = i;
                    state.Phase = SimPhase.GameOver;
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Response to a pending action in simulation. The response policy
    /// returns this to indicate whether to play JSN and which cards to pay.
    /// </summary>
    public class SimActionResponse
    {
        /// <summary>Whether to play Just Say No to block the action.</summary>
        public bool PlayJustSayNo { get; set; }

        /// <summary>
        /// Card IDs to pay with (from bank and/or property sets).
        /// Only relevant for payment-type pending actions.
        /// </summary>
        public List<int>? PaymentCardIds { get; set; }
    }
}
