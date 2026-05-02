# SmartBotAI 🤖🧠

The SmartBotAI library powers the computer opponents in Jeffopoly Deal. It uses **ISMCTS (Information Set Monte Carlo Tree Search)** — the gold standard for AI in card games with hidden information — to search thousands of possible game futures and pick the move with the highest win probability.

## How It Works — Overview

The AI combines two approaches:

- **ISMCTS for proactive decisions** (what card to play on your turn): The bot simulates hundreds of possible games by guessing what opponents might hold, plays each game to the end, and picks the move that wins most often.
- **Heuristics for reactive decisions** (responding to rent, playing Just Say No): Fast rule-based logic handles time-sensitive responses where tree search isn't needed.

## ISMCTS — The Search Engine

When the bot needs to choose a card to play, here's what happens behind the scenes:

### 1. Snapshot the Game — `SimulationState`

The bot takes a lightweight snapshot of the current game: its own hand, everyone's board (properties, bank), the discard pile, and the deck size. This snapshot can be cloned thousands of times without the overhead of the real game engine (no SignalR, no async, no locks).

### 2. Guess Opponent Hands — `Determinizer`

The bot knows its own hand and everything on the table, but NOT what opponents hold. The unknown cards (opponent hands + draw pile) are pooled together and randomly dealt to create a **plausible concrete game state**. Each run produces a different "possible world."

Example: "I have 5 cards, I see 30 on the table and 8 in the discard pile. The remaining 63 cards are split somehow between 3 opponents (7, 6, 5 cards) and the deck (45 cards). Let me shuffle and deal a random split."

### 3. Search the Tree — `ISMCTSEngine`

The engine runs ~500 iterations. Each iteration:

1. **Determinize**: Create a fresh random world (step 2)
2. **Select**: Walk down the search tree picking the most promising moves using **UCB1** (balances trying new moves vs. exploiting known-good ones)
3. **Expand**: Add one new untried move as a tree node
4. **Rollout**: Simulate the rest of the game using fast heuristics (`RolloutPolicy`)
5. **Backpropagate**: Update win/loss statistics up the tree

After all iterations, pick the move with the most visits (most statistically reliable).

**Key ISMCTS detail**: Different random worlds may produce different sets of legal moves. The engine tracks **move availability** — how many worlds each move was legal in — and uses this in the UCB1 formula to avoid statistical bias.

### 4. Enumerate Legal Moves — `MoveGenerator`

Before searching, the engine needs to know what's legal. The move generator produces all valid options:

- Play each hand card for its effect OR as money
- Rent cards: choose color, optional DoubleTheRent attachments
- Targeted actions: each valid target player × target card
- End turn early (sometimes the best play is to stop)

Compound moves like **Rent + DoubleTheRent** are generated as single moves to keep the search space manageable.

### 5. Simulate Games — `GameSimulator`

A stripped-down, synchronous game engine that can play a full game to completion in microseconds. It handles all card types: rent, steal, birthday, pass go, houses, hotels, Just Say No chains, and win detection. No networking, no UI — just pure game logic.

### 6. Fast Heuristic Playouts — `RolloutPolicy`

During simulated games (rollouts), all players use the same CardEvaluator-based heuristics that the old bot used. This produces much better signal than random play — the simulated games resemble real games.

**Horizon cutoff**: If a rollout doesn't finish within 20 turns, a heuristic evaluation scores each player by completed sets, near-complete sets, bank value, and hand size.

## Heuristic Components (Still Used)

### Reading the Board — `BoardAnalyzer`

Evaluates game state: threat scoring, win detection, richest opponent, best wildcard placement. Used by both ISMCTS rollouts and targeting logic.

### Card Priority Scoring — `CardEvaluator`

Each card in hand gets a priority score. Used by the RolloutPolicy during ISMCTS simulated games to play cards realistically. Higher score = play first.

| Card Type | Priority | Why |
|---|---|---|
| **Set-completing property** | ⭐ Highest | Finishing a set is how you win |
| **Rent** (with properties) | 🔥 High | Collect money, especially with lots of properties |
| **Property wildcard** | 📋 Medium-high | Flexible, helps complete sets |
| **Pass Go** (plays remaining) | 📋 Medium-high | Draw 2 more cards when you can still play them |
| **Property** (regular) | 📋 Medium | Always useful to build toward sets |
| **Money** | 💰 Low-medium | Goes to bank, safe but boring |
| **Just Say No** | 🛡️ Never played proactively | Saved for defense |
| **Double the Rent** | 🛡️ Never played alone | Bundled with rent automatically |

### Paying Debts Wisely — `PaymentSolver`

When the bot owes money, it finds the optimal combination of cards to pay:

1. **Bank money first** — lowest strategic value
2. **Protect complete sets** — never break these if avoidable
3. **Minimize overpayment** — subset-sum algorithm finds the exact best combo
4. **Fallback greedy** — for large hands (>15 payable cards), uses fast greedy approach

### Defensive Decisions — Just Say No

| Attack | Response | Reasoning |
|---|---|---|
| **Deal Breaker** | ✅ Always blocks | Losing a complete set is devastating |
| **Sly/Force Deal** threatening near-complete set | 🤔 Blocks | Only if the stolen card was critical |
| **High rent ($5+)** requiring property sacrifice | ✅ Blocks | Protecting properties > saving a JSN |
| **Low rent** payable from bank | ❌ Doesn't block | Cheap to just pay |
| **Birthday** ($2) | ❌ Never blocks | Too cheap to waste JSN |
| **JSN chain** (bot was original attacker) | ✅ Counters | Protect the original action investment |

### Discarding Smartly

When the bot exceeds 7 cards: keeps JSN, set-completing properties, DealBreaker, wild rent; discards low-value money first.

## Architecture

```
src/JeffopolyDeal.BotAI/
├── SmartBotAI.cs          # Main entry — PlayTurn (ISMCTS), BuildResponse, PickDiscards
├── ISMCTSEngine.cs        # Core MCTS loop: select → expand → rollout → backpropagate
├── SimulationState.cs     # Lightweight cloneable game state for simulation
├── GameSimulator.cs       # Synchronous game engine for fast rollouts
├── Determinizer.cs        # Samples plausible opponent hands from unknown card pool
├── MoveGenerator.cs       # Enumerates all legal moves for a position
├── RolloutPolicy.cs       # Heuristic move/response policy for simulated games
├── BoardAnalyzer.cs       # Game state evaluation — threats, win detection, targeting
├── CardEvaluator.cs       # Card priority scoring (used by RolloutPolicy)
└── PaymentSolver.cs       # Optimal payment selection — subset-sum algorithm
```

**ISMCTS classes** (`ISMCTSEngine`, `SimulationState`, `GameSimulator`, `Determinizer`, `MoveGenerator`, `RolloutPolicy`) live in the `JeffopolyDeal.ISMCTS` namespace. They operate on lightweight `Sim*` models independent of the real game objects.

**Heuristic classes** (`BoardAnalyzer`, `CardEvaluator`, `PaymentSolver`) remain in the `JeffopolyDeal` namespace and are used both directly (reactive decisions) and indirectly (via RolloutPolicy during ISMCTS rollouts).

All classes are **static** and **stateless** — they evaluate the current game state each time they're called, with no memory between turns.

## Configuration

ISMCTS behavior is controlled by `ISMCTSConfig`:

| Parameter | Default | Description |
|---|---|---|
| `Iterations` | 500 | Number of MCTS iterations per decision |
| `ExplorationConstant` | 1.0 | UCB1 exploration vs exploitation tradeoff |
| `MaxRolloutTurns` | 20 | Horizon cutoff for simulated games |
| `TimeLimitMs` | 200 | Hard time limit per decision (ms) |

## Testing

```bash
dotnet test tests/JeffopolyDeal.BotAI.Tests/
```

Tests use deterministic deck stacking and controlled hand injection to ensure bot behavior is predictable and tests never flake due to random card draws.
