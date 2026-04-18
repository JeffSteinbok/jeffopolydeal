# Jeffopoly Deal 🎩🃏

[![CI](https://github.com/JeffSteinbok/jeffopolydeal/actions/workflows/ci.yml/badge.svg)](https://github.com/JeffSteinbok/jeffopolydeal/actions/workflows/ci.yml)
[![Deploy](https://github.com/JeffSteinbok/jeffopolydeal/actions/workflows/deploy.yml/badge.svg)](https://github.com/JeffSteinbok/jeffopolydeal/actions/workflows/deploy.yml)
[![Health Check](https://github.com/JeffSteinbok/jeffopolydeal/actions/workflows/health-check.yml/badge.svg)](https://github.com/JeffSteinbok/jeffopolydeal/actions/workflows/health-check.yml)

<p align="center">
  <img src="src/web/assets/JeffopolyDeal.png" alt="Jeffopoly Deal" width="500" />
</p>

A real-time multiplayer **Monopoly Deal** card game built with React, ASP.NET Core, and SignalR. Play with friends in your browser — no installs needed.

> **Live at:** [jeffopolydeal.azurewebsites.net](https://jeffopolydeal.azurewebsites.net)

---

## Features

- **Real-time multiplayer** — 2–4 players via SignalR websockets
- **AI opponents** — play against bots with built-in AI
- **Full Monopoly Deal rules** — rent, action cards, property sets, houses/hotels, Just Say No chains
- **Responsive UI** — desktop and mobile layouts
- **Reconnection support** — drop and rejoin mid-game without losing your place
- **Spectator mode** — inspect other players' boards

### Implemented Cards

| Category | Cards |
|---|---|
| **Money** | 1M, 2M, 3M, 4M, 5M, 10M |
| **Properties** | 10 color sets (Brown → Utility), dual-color wildcards, rainbow wildcard |
| **Actions** | Pass Go, Debt Collector, It's My Birthday, Sly Deal, Force Deal, Deal Breaker, Just Say No, Double the Rent, House, Hotel |
| **Rent** | Single-color and multi-color rent cards |

### Game Rules

- Draw **2 cards** per turn (5 if your hand is empty)
- Play up to **3 cards** per turn
- Hand limit of **7** — discard down at end of turn
- Collect **3 complete property sets** to win
- Any card can be banked as money
- Houses (+3M rent) and Hotels (+4M rent) on complete sets

---

## Tech Stack

| Layer | Technology |
|---|---|
| **Frontend** | React 19, TypeScript 5.9, Vite 8 |
| **Backend** | ASP.NET Core (.NET 10), SignalR |
| **Testing** | Vitest + jsdom (frontend), xUnit (backend) |
| **CI/CD** | GitHub Actions → Azure Web App |

---

## Project Structure

```
├── src/
│   ├── backend/              # ASP.NET Core game engine & SignalR hub
│   │   ├── Hubs/             # SignalR GameHub
│   │   ├── Models/           # Game state, cards, deck, config
│   │   ├── Game.cs           # Core game logic
│   │   ├── BotAI.cs          # AI player logic
│   │   └── GameCache.cs      # In-memory game store
│   ├── backend.Tests/        # xUnit tests
│   └── web/                  # React frontend
│       ├── pages/            # Game, lobby, home pages
│       ├── components/       # Shared UI components
│       ├── utilities/        # Debug, logging, helpers
│       └── Types.ts          # TypeScript type definitions
├── public/                   # Static assets
├── wwwroot/                  # Vite build output (served by backend)
├── .github/workflows/        # CI, deploy, health-check
├── vite.config.ts            # Vite config (proxies /hub, /api to backend)
└── package.json
```

---

## Getting Started

See [**DEVELOPMENT.md**](DEVELOPMENT.md) for full build, run, debug, and testing instructions.

**Quick start:**

```bash
# Terminal 1 — Backend
dotnet run --project src/backend/JeffopolyDeal.csproj

# Terminal 2 — Frontend (dev server with hot reload)
npm install && npm run dev
```

Then open `http://localhost:5173` in your browser.

---

## Deployment

Deployment is fully automated via GitHub Actions:

1. **CI** (`.github/workflows/ci.yml`) — builds and tests both frontend and backend on every push/PR to `main`
2. **Deploy** (`.github/workflows/deploy.yml`) — builds, publishes, and deploys to Azure Web App
3. **Health Check** (`.github/workflows/health-check.yml`) — scheduled weekly + post-deploy verification

> ⚠️ Never deploy manually. All deployments go through GitHub Actions CI/CD.

---

## Debug Mode

Append `?debug=<hexFlags>` to the URL to enable debug features. Flags are combined via bitwise OR.

**Example:** `http://localhost:5173/?debug=1006`

### Debug Flags

All flags are defined in [`src/web/utilities/Debug.ts`](src/web/utilities/Debug.ts).

| Flag | Hex | Description |
|---|---:|---|
| `VerboseLogging` | `0x001` | Extra console logging |
| `FixedGameCode` | `0x002` | Forces new game code to `TEST` |
| `SkipLobby` | `0x004` | Auto-start game immediately after creation |
| `ForcedHand` | `0x008` | *(reserved)* |
| `ShowAllHands` | `0x010` | *(reserved)* |
| `UnlimitedPlays` | `0x020` | *(reserved)* |
| `NoHandLimit` | `0x040` | *(reserved)* |
| `RichStart` | `0x080` | *(reserved)* |
| `InstantWin` | `0x100` | *(reserved)* |
| `SkipDraw` | `0x200` | *(reserved)* |
| `ShowDeck` | `0x400` | Shows in-game debug deck viewer |
| `PopulatedBoards` | `0x800` | Auto-starts with 3 AI bots and pre-populated boards |
| `PlayVsAi` | `0x1000` | Adds 3 AI bots for a normal game |

### Helpful Combinations

| Code | Flags | Use Case |
|---:|---|---|
| `0x006` | SkipLobby + FixedGameCode | Quick solo test |
| `0x406` | SkipLobby + FixedGameCode + ShowDeck | Test with deck viewer |
| `0x806` | SkipLobby + FixedGameCode + PopulatedBoards | Jump into pre-built boards |
| `0x1004` | SkipLobby + PlayVsAi | Fast game vs AI bots |
| `0x1006` | SkipLobby + FixedGameCode + PlayVsAi | Fast fixed-code game vs AI |
| `0x1FFF` | All flags | Everything enabled |

### Special Routes

| URL | Description |
|---|---|
| `?page=deck` | Deck test page — renders all 106 cards |
| `/api/deck` | API endpoint — returns full deck JSON |

---

## Game Configuration

Core game constants are in [`src/backend/Models/GameConfig.cs`](src/backend/Models/GameConfig.cs):

| Setting | Value |
|---|---|
| Initial hand size | 5 |
| Cards drawn per turn | 2 |
| Cards drawn when hand empty | 5 |
| Max plays per turn | 3 |
| Max hand size | 7 |
| Sets to win | 3 |
| Debt Collector amount | 5M |
| Birthday amount | 2M |
| House rent bonus | +3M |
| Hotel rent bonus | +4M |

---

## License

This project is licensed under the MIT License. See [LICENSE.md](LICENSE.md) for details.
