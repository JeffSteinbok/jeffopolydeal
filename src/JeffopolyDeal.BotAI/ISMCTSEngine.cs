using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using JeffopolyDeal.Models;

namespace JeffopolyDeal.ISMCTS
{
    // =========================================================================
    // ISMCTSEngine.cs — Information Set Monte Carlo Tree Search
    // =========================================================================
    //
    // This is the core search algorithm. ISMCTS extends standard MCTS to handle
    // games with hidden information (like card games where you can't see
    // opponents' hands).
    //
    // Standard MCTS builds a tree over game STATES. ISMCTS builds a tree over
    // INFORMATION SETS — groups of states that look the same from the bot's
    // perspective. The key difference is that each iteration starts with a
    // fresh "determinization" (a random guess about hidden cards), and the
    // tree is shared across all determinizations.
    //
    // Algorithm (per iteration):
    //   1. DETERMINIZE: Sample a plausible game state (random opponent hands)
    //   2. SELECT: Walk down the tree using UCB1, only considering moves
    //      that are legal in this particular determinization
    //   3. EXPAND: Add a child node for one untried legal move
    //   4. ROLLOUT: Simulate the game to end using RolloutPolicy (heuristic)
    //   5. BACKPROPAGATE: Update win/visit counts up the tree
    //
    // After all iterations, return the root child with the most visits.
    //
    // Key ISMCTS detail: not all moves are legal in every determinization
    // (because opponent hands differ). We track "availability" counts per
    // child and use them in UCB1 to avoid selection bias.
    //
    // References:
    //   Cowling, Powley, Whitehouse (2012) — "Information Set Monte Carlo
    //   Tree Search" — the foundational paper for this algorithm.
    // =========================================================================

    /// <summary>
    /// Configuration for the ISMCTS engine. Controls search budget and behavior.
    /// </summary>
    public class ISMCTSConfig
    {
        /// <summary>
        /// Number of MCTS iterations to run. More iterations = better decisions
        /// but more computation time. 500-1000 is a good range for this game.
        /// </summary>
        public int Iterations { get; set; } = 500;

        /// <summary>
        /// UCB1 exploration constant. Higher values encourage exploring less-visited
        /// moves; lower values exploit known-good moves. sqrt(2) ≈ 1.414 is the
        /// theoretical default; 0.7-1.0 often works better in practice.
        /// </summary>
        public double ExplorationConstant { get; set; } = 1.0;

        /// <summary>
        /// Maximum number of turns to simulate during rollout before stopping
        /// and using a heuristic evaluation. Prevents slow rollouts in long games.
        /// </summary>
        public int MaxRolloutTurns { get; set; } = 20;

        /// <summary>
        /// Hard time limit in milliseconds. If exceeded, stop early even if
        /// we haven't completed all iterations. 0 = no time limit.
        /// </summary>
        public int TimeLimitMs { get; set; } = 200;

        /// <summary>
        /// A config that forces the heuristic fallback path (Iterations = 0).
        /// Useful for unit tests that need deterministic, predictable bot behavior.
        /// </summary>
        public static ISMCTSConfig Heuristic => new ISMCTSConfig { Iterations = 0 };
    }

    /// <summary>
    /// A node in the ISMCTS search tree. Represents a decision point from the
    /// bot's perspective (an information set).
    /// 
    /// Unlike standard MCTS where each node corresponds to a unique game state,
    /// an ISMCTS node may correspond to MANY concrete states (all the ones that
    /// look the same to the bot). This is handled by sharing the tree across
    /// determinizations.
    /// </summary>
    public class MCTSNode
    {
        /// <summary>The move that was taken to reach this node from its parent.</summary>
        public SimMove? Move { get; set; }

        /// <summary>Number of rollouts that passed through this node and resulted in a win.</summary>
        public double Wins { get; set; }

        /// <summary>Total number of rollouts that passed through this node.</summary>
        public int Visits { get; set; }

        /// <summary>
        /// Number of determinizations in which this move was AVAILABLE (legal).
        /// Used for corrected UCB1 in ISMCTS — we need this because not every
        /// move is legal in every determinization.
        /// </summary>
        public int AvailabilityCount { get; set; }

        /// <summary>Parent node (null for root).</summary>
        public MCTSNode? Parent { get; set; }

        /// <summary>
        /// Child nodes, keyed by a string representation of the move.
        /// We use string keys rather than SimMove objects because the same
        /// logical move may be represented by different SimMove instances
        /// across determinizations.
        /// </summary>
        public Dictionary<string, MCTSNode> Children { get; set; } = new();

        /// <summary>
        /// Calculate the UCB1 score for this node, adjusted for ISMCTS move
        /// availability. This balances exploitation (high win rate) with
        /// exploration (less-visited nodes).
        /// 
        /// Formula: wins/visits + C * sqrt(ln(availability) / visits)
        /// 
        /// Using AvailabilityCount instead of parent visits is the key ISMCTS
        /// modification — it corrects for the fact that this move wasn't
        /// available in all parent's determinizations.
        /// </summary>
        public double UCB1Score(double explorationConstant)
        {
            if (Visits == 0) return double.MaxValue; // unvisited = infinite priority

            double exploitation = Wins / Visits;
            double exploration = explorationConstant *
                Math.Sqrt(Math.Log(AvailabilityCount) / Visits);

            return exploitation + exploration;
        }
    }

    /// <summary>
    /// The ISMCTS search engine. Call FindBestMove() with the current game state
    /// to get the best move for the bot.
    /// </summary>
    public static class ISMCTSEngine
    {
        /// <summary>
        /// Run ISMCTS from the given state and return the best move for the bot.
        /// 
        /// This is the main entry point. It:
        ///   1. Collects all cards in the game (for determinization)
        ///   2. Records opponent hand sizes (for determinization)
        ///   3. Runs the configured number of MCTS iterations
        ///   4. Returns the move with the highest visit count
        /// 
        /// Parameters:
        ///   rootState   — current game state snapshot (from SimulationState.FromGame)
        ///   botIndex    — which player in the state is the bot
        ///   allCards    — complete list of all 106 cards (from Determinizer.CollectAllCards)
        ///   opponentHandSizes — hand count for each player (bot's entry is ignored)
        ///   config      — search configuration (iterations, time limit, etc.)
        /// </summary>
        public static SimMove FindBestMove(
            SimulationState rootState,
            int botIndex,
            List<Card> allCards,
            int[] opponentHandSizes,
            ISMCTSConfig? config = null)
        {
            config ??= new ISMCTSConfig();
            var root = new MCTSNode();
            var rng = new Random();
            var stopwatch = Stopwatch.StartNew();

            // --- Main MCTS loop ---
            for (int iteration = 0; iteration < config.Iterations; iteration++)
            {
                // Check time limit
                if (config.TimeLimitMs > 0 && stopwatch.ElapsedMilliseconds >= config.TimeLimitMs)
                    break;

                // Step 1: DETERMINIZE — create a plausible concrete game state
                // by sampling random opponent hands from the unknown card pool
                var detState = rootState.Clone();
                Determinizer.Determinize(detState, botIndex, opponentHandSizes, allCards, rng);

                // Step 2: SELECT — walk down the tree using UCB1
                var node = root;
                var simState = detState.Clone(); // we'll mutate this during selection

                // Get legal moves for the current position
                var legalMoves = MoveGenerator.GetLegalMoves(simState, botIndex);
                var legalKeys = new HashSet<string>(legalMoves.Select(MoveKey));

                // Update availability counts for all children that are legal here
                foreach (var child in node.Children.Values)
                {
                    if (child.Move != null && legalKeys.Contains(MoveKey(child.Move)))
                        child.AvailabilityCount++;
                }

                // Select: keep descending while we have children and the node is
                // fully expanded (all legal moves have been tried)
                while (node.Children.Count > 0)
                {
                    // Find which legal moves have children and which don't
                    var triedKeys = new HashSet<string>(node.Children.Keys);
                    var untried = legalMoves.Where(m => !triedKeys.Contains(MoveKey(m))).ToList();

                    if (untried.Count > 0)
                        break; // not fully expanded — go to expand phase

                    // All legal moves tried — select best child by UCB1
                    // ISMCTS: only select among moves legal in THIS determinization
                    MCTSNode? bestChild = null;
                    double bestScore = double.MinValue;

                    foreach (var kvp in node.Children)
                    {
                        if (!legalKeys.Contains(kvp.Key)) continue; // not legal here
                        var score = kvp.Value.UCB1Score(config.ExplorationConstant);
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestChild = kvp.Value;
                        }
                    }

                    if (bestChild == null) break; // no legal children (shouldn't happen)

                    // Apply this move to advance the simulation state
                    if (bestChild.Move != null && !bestChild.Move.IsEndTurn)
                    {
                        GameSimulator.ExecuteMove(simState, botIndex, bestChild.Move,
                            RolloutPolicy.BuildResponse);
                    }
                    else
                    {
                        break; // end-turn node — stop descending
                    }

                    node = bestChild;

                    // Check if game is over
                    if (simState.Phase == SimPhase.GameOver) break;

                    // Get legal moves at new depth
                    if (simState.PlaysRemaining > 0 && simState.CurrentPlayerIndex == botIndex)
                    {
                        legalMoves = MoveGenerator.GetLegalMoves(simState, botIndex);
                        legalKeys = new HashSet<string>(legalMoves.Select(MoveKey));

                        // Update availability for children at this level
                        foreach (var child in node.Children.Values)
                        {
                            if (child.Move != null && legalKeys.Contains(MoveKey(child.Move)))
                                child.AvailabilityCount++;
                        }
                    }
                    else
                    {
                        break; // not bot's turn anymore
                    }
                }

                // Step 3: EXPAND — add a child for one untried legal move
                if (simState.Phase != SimPhase.GameOver
                    && simState.PlaysRemaining > 0
                    && simState.CurrentPlayerIndex == botIndex)
                {
                    legalMoves = MoveGenerator.GetLegalMoves(simState, botIndex);
                    var triedKeys = new HashSet<string>(node.Children.Keys);
                    var untried = legalMoves.Where(m => !triedKeys.Contains(MoveKey(m))).ToList();

                    if (untried.Count > 0)
                    {
                        // Pick a random untried move to expand
                        var expandMove = untried[rng.Next(untried.Count)];
                        var expandKey = MoveKey(expandMove);

                        var childNode = new MCTSNode
                        {
                            Move = expandMove,
                            Parent = node,
                            AvailabilityCount = 1,
                        };
                        node.Children[expandKey] = childNode;
                        node = childNode;

                        // Apply the expanded move
                        if (!expandMove.IsEndTurn)
                        {
                            GameSimulator.ExecuteMove(simState, botIndex, expandMove,
                                RolloutPolicy.BuildResponse);
                        }
                    }
                }

                // Step 4: ROLLOUT — simulate the game to the end (or horizon)
                // using the heuristic rollout policy
                double result = Rollout(simState, botIndex, config.MaxRolloutTurns);

                // Step 5: BACKPROPAGATE — update win/visit counts up the tree
                var backpropNode = node;
                while (backpropNode != null)
                {
                    backpropNode.Visits++;
                    backpropNode.Wins += result;
                    backpropNode = backpropNode.Parent;
                }
            }

            // --- Choose the best move: highest visit count ---
            // Visit count is more stable than win rate for move selection
            // because it naturally handles exploration/exploitation balance.
            if (root.Children.Count == 0)
            {
                // No children expanded — fallback to heuristic
                return RolloutPolicy.PickMove(rootState, botIndex);
            }

            var bestMove = root.Children.Values
                .OrderByDescending(c => c.Visits)
                .First();

            return bestMove.Move ?? new SimMove { IsEndTurn = true };
        }

        // =====================================================================
        // Rollout: simulate the rest of the game using heuristic policy
        // =====================================================================

        /// <summary>
        /// Simulate the game from the current state using RolloutPolicy for all
        /// players' decisions. Returns a score for the bot:
        ///   1.0 = bot wins
        ///   0.0 = bot loses
        ///   0.5 = game didn't finish within the turn limit (use heuristic eval)
        /// 
        /// When the turn limit is reached, we use a heuristic evaluation based
        /// on how close each player is to winning (set completion progress).
        /// This is much faster than simulating to terminal and produces better
        /// signals than a flat 0.5.
        /// </summary>
        private static double Rollout(SimulationState state, int botIndex, int maxTurns)
        {
            // If game is already over, return immediately
            if (state.Phase == SimPhase.GameOver)
                return state.WinnerIndex == botIndex ? 1.0 : 0.0;

            // Run the simulation
            int winner = GameSimulator.SimulateToEnd(
                state,
                RolloutPolicy.PickMove,
                RolloutPolicy.BuildResponse,
                maxTurns);

            if (winner >= 0)
            {
                // Game finished — clear win or loss
                return winner == botIndex ? 1.0 : 0.0;
            }

            // Game didn't finish — use heuristic evaluation
            return HeuristicEval(state, botIndex);
        }

        /// <summary>
        /// Heuristic evaluation of a non-terminal game state. Returns a value
        /// between 0.0 and 1.0 representing the bot's estimated winning chances.
        /// 
        /// Factors considered:
        ///   - Number of unique completed sets (most important — this IS the win condition)
        ///   - Progress toward completing sets (near-complete sets are valuable)
        ///   - Bank total with diminishing returns (first 5M is a critical rent
        ///     buffer; additional money is less important)
        ///   - Defensive exposure: incomplete property sets are discounted when
        ///     the player's bank can't absorb a rent charge
        ///   - Relative position vs opponents
        /// 
        /// The evaluation normalizes the bot's score relative to all players
        /// so that the return value reflects competitive position, not just
        /// absolute progress.
        /// </summary>
        private static double HeuristicEval(SimulationState state, int botIndex)
        {
            // Target bank balance for rent protection. The first RentBuffer
            // units of money are worth significantly more than money above it.
            const double RentBuffer = 5.0;

            // Score each player
            var scores = new double[state.PlayerCount];
            for (int i = 0; i < state.PlayerCount; i++)
            {
                var player = state.Players[i];

                // Completed sets are worth the most (100 points each)
                double score = player.UniqueCompletedSetCount * 100.0;

                // Bank value uses diminishing returns curve:
                //   First 5M of bank → worth 3.0 per M (total 15.0 for full buffer)
                //   Money above 5M  → worth 0.3 per M  (nice to have, not critical)
                //
                // This models the defensive reality: a 5M bank absorbs most rent
                // charges, protecting your properties. An empty bank means every
                // rent card strips your board.
                double bank = player.BankTotal;
                double bufferPortion = Math.Min(bank, RentBuffer);
                double excessPortion = Math.Max(0, bank - RentBuffer);
                score += bufferPortion * 3.0 + excessPortion * 0.3;

                // Property set evaluation — defensive-aware
                bool bankCanAbsorbRent = bank >= RentBuffer;
                foreach (var set in player.PropertySets)
                {
                    if (set.IsComplete)
                    {
                        // Complete sets are always good — can't be stolen with
                        // Sly Deal and they count toward the win condition
                        // (already counted above in UniqueCompletedSetCount)
                    }
                    else if (set.Size >= set.RequiredSize - 1)
                    {
                        // Near-complete sets: high value, but slightly reduced
                        // if bank is thin (they're still worth pursuing)
                        score += bankCanAbsorbRent ? 30.0 : 22.0;
                    }
                    else if (set.Cards.Count > 0)
                    {
                        // Partial sets: worth less when exposed to rent with no
                        // bank buffer — these are the first to go when charged
                        double perCard = bankCanAbsorbRent ? 5.0 : 2.0;
                        score += set.Cards.Count * perCard;
                    }
                }

                // Hand size is slightly valuable (more options)
                score += player.Hand.Count * 0.5;

                scores[i] = score;
            }

            // Normalize: bot's score relative to sum of all scores
            double totalScore = scores.Sum();
            if (totalScore <= 0) return 0.5; // degenerate case

            // Return bot's share, but map it so that equal scores = 0.5
            double botShare = scores[botIndex] / totalScore;

            // Scale to [0, 1] range with 0.5 as the neutral point
            // botShare of 1/N (equal share) maps to 0.5
            double expectedShare = 1.0 / state.PlayerCount;
            if (botShare >= expectedShare)
            {
                // Bot is doing better than average
                return 0.5 + 0.5 * (botShare - expectedShare) / (1.0 - expectedShare);
            }
            else
            {
                // Bot is doing worse than average
                return 0.5 * botShare / expectedShare;
            }
        }

        // =====================================================================
        // Move key generation — used to index tree nodes
        // =====================================================================

        /// <summary>
        /// Generate a string key for a move. This key is used to match moves
        /// across different determinizations in the shared ISMCTS tree.
        /// 
        /// The key must capture the OBSERVABLE aspects of the move:
        ///   - Which card (by ID) — bot's own cards have stable IDs
        ///   - Whether it's played as money
        ///   - Target player, target card, offered card (all public IDs)
        ///   - Rent color, wildcard color
        ///   - DoubleTheRent card IDs
        ///
        /// The key must NOT depend on hidden information (opponent hand contents).
        /// </summary>
        public static string MoveKey(SimMove move)
        {
            if (move.IsEndTurn) return "END";

            var parts = new List<string>();

            // Card identity
            parts.Add(move.Card?.Id.ToString() ?? "?");

            // Play mode
            parts.Add(move.PlayAsMoney ? "M" : "P");

            // Targeting
            if (move.TargetPlayerIndex >= 0)
                parts.Add($"T{move.TargetPlayerIndex}");
            if (move.TargetCardId.HasValue)
                parts.Add($"TC{move.TargetCardId}");
            if (move.OfferedCardId.HasValue)
                parts.Add($"OC{move.OfferedCardId}");
            if (move.TargetSetColor.HasValue)
                parts.Add($"SC{move.TargetSetColor}");
            if (move.RentColor.HasValue)
                parts.Add($"RC{move.RentColor}");
            if (move.WildcardColor.HasValue)
                parts.Add($"WC{move.WildcardColor}");

            // DoubleTheRent IDs
            if (move.DoubleRentCardIds != null && move.DoubleRentCardIds.Count > 0)
                parts.Add($"DTR{string.Join(",", move.DoubleRentCardIds.OrderBy(x => x))}");

            return string.Join("|", parts);
        }
    }
}
