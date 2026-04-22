# SmartBotAI 🤖🧠

The SmartBotAI library powers the computer opponents in Jeffopoly Deal. Instead of making random choices, the bot analyzes the full game state — its hand, the board, what opponents have — and makes strategic decisions like an experienced player would.

## How It Works

The AI breaks every decision into four parts, each handled by a dedicated component:

### 1. Reading the Board — `BoardAnalyzer`

Before doing anything, the bot scans the entire table. It answers questions like:

- **Who's the biggest threat?** Each opponent gets a "danger score" based on how close they are to winning (complete property sets = very dangerous), how much money they have, and how many properties they control.
- **Can I win this turn?** The bot checks if it has enough property cards in hand to complete the sets it needs for victory.
- **Is anyone about to win?** If an opponent is one set away from winning, the bot switches to aggressive mode — stealing properties, playing DealBreakers, and blocking with Just Say No.
- **Where should wildcards go?** When placing a multi-color wildcard, the bot picks whichever color set is closest to being completed.

### 2. Choosing What to Play — `CardEvaluator`

Each card in the bot's hand gets a priority score. Higher score = play it first. Here's roughly how it thinks:

| Card Type | Priority | Why |
|---|---|---|
| **Set-completing property** | ⭐ Highest | Finishing a set is how you win |
| **Rent** (with properties) | 🔥 High | Collect money, especially with lots of properties in that color |
| **Property wildcard** | 📋 Medium-high | Flexible, helps complete sets |
| **Property** (regular) | 📋 Medium | Always useful to build toward sets |
| **Pass Go** | 📋 Medium | Draw 2 more cards = more options |
| **Money** | 💰 Low-medium | Goes to bank, safe but boring |
| **Just Say No** | 🛡️ Never played proactively | Saved as a defensive card for when opponents attack |
| **Double the Rent** | 🛡️ Never played alone | Only played alongside a rent card to double the charge |

The bot also adds a small amount of randomness among its top choices (within 10% of the best score) so it doesn't always play identically — this makes it more fun and less predictable.

#### Smart Combos

- **Rent + Double the Rent**: If the bot plays a rent card and has a Double the Rent in hand, it automatically stacks them together to charge double (or even quadruple with two DTRs).
- **Properties before rent**: The bot knows to play properties *first* so that when it plays rent, the charge is higher.

### 3. Paying Debts Wisely — `PaymentSolver`

When the bot owes money (from rent, Debt Collector, etc.), it doesn't just grab random cards. It uses an algorithm to find the best combination of cards to pay with:

**The goal**: Pay what you owe while losing as little strategic value as possible.

**How it decides what to give up**:
1. **Bank money first** — Cash in the bank has no strategic value beyond its dollar amount. Pay with this first.
2. **Protect complete sets** — Cards in a complete (or nearly complete) property set are worth far more than their face value. The bot avoids breaking these up.
3. **Minimize overpayment** — If you owe $3, paying with a $3 bill is better than paying with a $5 bill. The bot uses a technique called "subset-sum" to find the exact combination of cards that gets closest to the amount owed without going way over.
4. **Fallback for large hands** — If a player somehow has more than 15 payable cards, the bot switches to a faster "greedy" approach (pay with lowest-value cards first) since checking every possible combination would be too slow.

### 4. Defensive Decisions — Just Say No

The bot doesn't blindly block every attack. It evaluates whether using a Just Say No card is worth it:

| Attack | Bot's Response | Reasoning |
|---|---|---|
| **Deal Breaker** (steal a complete set) | ✅ Always blocks | Losing a complete set is devastating |
| **Sly Deal / Force Deal** (steal a property) | 🤔 Blocks if it would break a near-complete set | Only worth a JSN if the stolen card was critical |
| **High rent ($5+)** that would require giving up properties | ✅ Blocks | Protecting properties > saving a JSN card |
| **Low rent** that can be paid from bank | ❌ Doesn't block | Cheap to just pay, save the JSN for something important |
| **Birthday** ($2 from everyone) | ❌ Never blocks | Too cheap to waste a JSN on |
| **JSN chain** (opponent JSN'd the bot's attack) | ✅ Blocks if bot was the original attacker | Protect the investment of the original action card |

### 5. Discarding Smartly

When the bot has more than 7 cards at end of turn, it must discard down. It keeps the most valuable cards:

1. 🛡️ **Just Say No** — Always kept (best defensive card)
2. 🏆 **Set-completing properties** — One card away from a complete set? Keep it!
3. 💣 **Deal Breaker** — Too powerful to throw away
4. 🎨 **Wild Rent** — Flexible rent card, always useful
5. 🔧 **Double the Rent** — Kept if bot has rent cards, discarded otherwise
6. 🏠 **Regular properties/rent** — Kept based on how close their set is to completion
7. 💵 **Low-value money** — First to go (a $1 bill is the least useful card)

## Architecture

```
src/JeffopolyDeal.BotAI/
├── SmartBotAI.cs       # Main entry point — PlayTurn, BuildResponse, PickDiscards
├── BoardAnalyzer.cs    # Game state evaluation — threats, win detection, targeting
├── CardEvaluator.cs    # Card priority scoring — what to play and when
└── PaymentSolver.cs    # Optimal payment selection — subset-sum algorithm
```

All classes are **static** and **stateless** — they evaluate the current game state each time they're called, with no memory between turns. This keeps things simple and avoids bugs from stale state.

## Testing

```bash
dotnet test tests/JeffopolyDeal.BotAI.Tests/
```

Tests use deterministic deck stacking (`StackDeckWithMoney`) and controlled hand injection to ensure bot behavior is predictable and tests never flake due to random card draws.
