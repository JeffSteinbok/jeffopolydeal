# Development Guide

Full guide for building, running, debugging, and testing Jeffopoly Deal locally.

---

## Prerequisites

- [Node.js](https://nodejs.org/) v18+ (v20 recommended)
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A modern browser (Chrome, Edge, Firefox, Safari)

---

## Architecture Overview

```
Browser (React SPA)  ←—SignalR WebSocket—→  ASP.NET Core Backend
       ↓                                           ↓
   Vite Dev Server                           Game Engine
   (port 5173)                              (port 5011)
       ↓ proxy /hub, /api ─────────────────────→ ↑
```

- **Frontend** — React 19 + TypeScript, bundled by Vite. In dev mode, Vite proxies `/hub` (SignalR) and `/api` requests to the .NET backend.
- **Backend** — ASP.NET Core with SignalR for real-time game state. All game logic runs server-side. The `Game.cs` engine manages turns, actions, payments, and win conditions.
- **Production** — Vite builds to `wwwroot/`, which the .NET backend serves as static files alongside the SignalR hub.

---

## Running Locally

You need **two terminals** — one for the backend, one for the frontend.

### 1. Backend

```bash
dotnet restore src/backend/JeffopolyDeal.csproj
dotnet run --project src/backend/JeffopolyDeal.csproj
```

The backend starts on:
- `https://localhost:5011` (HTTPS)
- `http://localhost:5010` (HTTP)

### 2. Frontend

```bash
npm install
npm run dev
```

The Vite dev server starts on `http://localhost:5173` and proxies SignalR/API calls to the backend automatically.

### 3. Open in Browser

Navigate to **`http://localhost:5173`** to play.

---

## Building for Production

```bash
# Build frontend → wwwroot/
npm run build

# Build and publish backend (includes wwwroot/ static files)
dotnet publish src/backend/JeffopolyDeal.csproj -c Release -o publish/
```

To preview the production build locally:

```bash
cd publish/
dotnet JeffopolyDeal.dll
```

Then open `https://localhost:5011`.

---

## Testing

### Frontend Tests (Vitest + jsdom)

```bash
npm run test            # Single run
npm run test:watch      # Watch mode
```

### Backend Tests (xUnit)

```bash
dotnet test src/backend.Tests
```

### CI

Both test suites run automatically on every push/PR to `main` via GitHub Actions (`.github/workflows/ci.yml`).

---

## Debugging

### Debug Flags (Frontend)

Append `?debug=<hexFlags>` to any URL to enable debug features. Flags are combined via bitwise OR and parsed as hexadecimal.

**Example:** `http://localhost:5173/?debug=1006` (SkipLobby + FixedGameCode + PlayVsAi)

| Flag | Hex | Description |
|---|---:|---|
| `VerboseLogging` | `0x001` | Extra console logging via `Logger.debug` |
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

#### Recommended Combos

| Code | What it does |
|---:|---|
| `0x006` | Quick solo test (SkipLobby + FixedGameCode) |
| `0x406` | Solo test with deck viewer |
| `0x806` | Jump into pre-built boards with bots |
| `0x1004` | Fast game vs AI (random game code) |
| `0x1006` | Fast game vs AI (fixed code `TEST`) |
| `0x1FFF` | Everything enabled |

All flags are defined in [`src/web/utilities/Debug.ts`](src/web/utilities/Debug.ts).

### Special Routes

| URL | Description |
|---|---|
| `?page=deck` | Deck test page — renders all 106 cards visually |
| `/api/deck` | REST endpoint — returns the full deck as JSON |

### Backend Debugging

- **Visual Studio / VS Code:** Open `JeffopolyDeal.sln` or the `src/backend/` folder. Launch profiles are in `src/backend/Properties/launchSettings.json`.
- **Hot reload:** Use `dotnet watch` for automatic rebuild on changes:
  ```bash
  dotnet watch run --project src/backend/JeffopolyDeal.csproj
  ```
- **SignalR debug endpoint:** The `GetDebugDeckInfo` hub method returns the draw pile, discard pile, and all player hands for the current game.

### Frontend Debugging

- **Browser DevTools:** React DevTools and the browser console work normally. Enable `VerboseLogging` (`?debug=1`) for extra game state logging.
- **Hot Module Replacement:** Vite provides instant HMR — save a file and see changes immediately in the browser.
- **Source maps:** Enabled in both dev and production builds (`vite.config.ts: sourcemap: true`).

---

## Project Structure

```
├── src/
│   ├── backend/                  # ASP.NET Core game server
│   │   ├── Hubs/GameHub.cs       # SignalR hub — client ↔ server messaging
│   │   ├── Models/
│   │   │   ├── Card.cs           # Card types, actions, properties
│   │   │   ├── Deck.cs           # 106-card deck composition
│   │   │   ├── GameConfig.cs     # Game constants (rent tables, set sizes, limits)
│   │   │   ├── GameState.cs      # State DTOs sent to clients
│   │   │   ├── Player.cs         # Player model (hand, bank, properties)
│   │   │   └── PropertySet.cs    # Property set logic (completion, rent calc)
│   │   ├── Game.cs               # Core game engine (turns, actions, payments)
│   │   ├── GameCache.cs          # In-memory game store
│   │   ├── BotAI.cs              # AI player decision logic
│   │   └── Program.cs            # App startup, routing, middleware
│   │
│   ├── backend.Tests/            # xUnit backend tests
│   │
│   └── web/                      # React frontend
│       ├── pages/
│       │   ├── startPage/        # Home screen (Create/Join)
│       │   └── gamePage/         # Lobby, game board, action modals
│       ├── components/           # Shared UI (cards, buttons, toasts)
│       ├── utilities/
│       │   ├── Debug.ts          # Debug flag system
│       │   └── Logger.ts         # Conditional logging
│       └── Types.ts              # TypeScript type definitions
│
├── public/                       # Static assets (favicon, rent images)
├── wwwroot/                      # Vite build output (served by .NET)
├── .github/workflows/            # CI, deploy, health-check pipelines
├── vite.config.ts                # Vite config (proxy, build output)
├── tsconfig.json                 # TypeScript config
└── package.json                  # Frontend dependencies and scripts
```

---

## Key Concepts

### Game Phases

The game flows through these phases: **Lobby** → **Draw** → **Play** → **AwaitingResponse** → **Discard** → **GameOver**

- **Lobby** — players join, host starts the game
- **Draw** — active player draws 2 cards (5 if hand was empty)
- **Play** — active player plays up to 3 cards (properties, actions, money)
- **AwaitingResponse** — waiting for target player(s) to respond to an action
- **Discard** — active player discards down to 7 cards if over hand limit
- **GameOver** — a player has completed 3 property sets

### Player Identity

Players have a stable `PlayerId` (UUID) stored in `localStorage` that survives browser refreshes and reconnections. The `ConnectionId` is a transient SignalR transport identifier that changes on every reconnect.

### SignalR Communication

All game actions flow through the `GameHub`:
- **Client → Server:** `CreateGame`, `JoinGame`, `StartGame`, `PlayCard`, `RespondToAction`, `EndTurn`, etc.
- **Server → Client:** `ReceiveGameState` broadcasts personalized game state to each player after every action.

---

## Deployment

Deployment is fully automated — **never deploy manually**.

1. **CI** — builds and tests on every push/PR to `main`
2. **Deploy** — on push to `main`, builds frontend + backend and deploys to Azure Web App
3. **Health Check** — runs weekly + after every deploy to verify the site is up

All workflows live in `.github/workflows/`.

---

## npm Scripts Reference

| Script | Command | Description |
|---|---|---|
| `npm run dev` | `vite` | Start Vite dev server with HMR |
| `npm run build` | `vite build` | Production build → `wwwroot/` |
| `npm run preview` | `vite preview` | Preview production build locally |
| `npm run test` | `vitest run` | Run frontend tests once |
| `npm run test:watch` | `vitest` | Run frontend tests in watch mode |

## .NET Commands Reference

| Command | Description |
|---|---|
| `dotnet restore` | Restore NuGet packages |
| `dotnet build` | Build the backend |
| `dotnet run --project src/backend/JeffopolyDeal.csproj` | Run the backend |
| `dotnet watch run --project src/backend/JeffopolyDeal.csproj` | Run with hot reload |
| `dotnet test src/backend.Tests` | Run backend tests |
| `dotnet publish src/backend/JeffopolyDeal.csproj -c Release -o publish/` | Publish for deployment |
