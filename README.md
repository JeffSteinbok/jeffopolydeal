# Jeffopoly Deal 🎩🃏

[![CI](https://github.com/JeffSteinbok/jeffopolydeal/actions/workflows/ci.yml/badge.svg)](https://github.com/JeffSteinbok/jeffopolydeal/actions/workflows/ci.yml)
[![Deploy](https://github.com/JeffSteinbok/jeffopolydeal/actions/workflows/deploy.yml/badge.svg)](https://github.com/JeffSteinbok/jeffopolydeal/actions/workflows/deploy.yml)
[![Health Check](https://github.com/JeffSteinbok/jeffopolydeal/actions/workflows/health-check.yml/badge.svg)](https://github.com/JeffSteinbok/jeffopolydeal/actions/workflows/health-check.yml)

<p align="center">
  <img src="src/web/assets/JeffopolyDeal.png" alt="Jeffopoly Deal" width="500" />
</p>

A real-time multiplayer **Monopoly Deal** card game built with React, ASP.NET Core, and SignalR. Play with friends in your browser — no installs needed.


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
│   ├── JeffopolyDeal.Shared/    # Shared models & card definitions
│   │   ├── Models/              # Card, Player, PropertySet, Deck, GameConfig
│   │   └── Cards/               # Card playability logic
│   ├── JeffopolyDeal.Game/      # ASP.NET Core game engine & SignalR hub
│   │   ├── Hubs/                # SignalR GameHub
│   │   ├── Themes/              # Theme JSON files
│   │   ├── Game.cs              # Core game logic
│   │   ├── BotAI.cs             # AI player logic
│   │   └── GameCache.cs         # In-memory game store
│   └── web/                     # React frontend
│       ├── pages/               # Game, lobby, home pages
│       ├── components/          # Shared UI components
│       ├── utilities/           # Debug, logging, helpers
│       └── Types.ts             # TypeScript type definitions
├── tests/
│   └── JeffopolyDeal.Game.Tests/ # xUnit backend tests
├── public/                      # Static assets
├── wwwroot/                     # Vite build output (served by backend)
├── .github/workflows/           # CI, deploy, health-check
├── vite.config.ts               # Vite config (proxies /hub, /api to backend)
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

### Debug Console

When any debug flag is active, a debug console input appears in the game header (next to the Jeffopoly logo). Type a command and press Enter to execute it. The game state refreshes automatically after each command.

#### Commands

| Command | Description | Example |
|---|---|---|
| `give money <value>` | Add a money card to your hand | `give money 5` |
| `give rent <color>` | Add a rent card for that color pair | `give rent pink` |
| `give wildrent` | Add a wild rent card (any color) | `give wildrent` |
| `give house` | Add a House card | `give house` |
| `give hotel` | Add a Hotel card | `give hotel` |
| `give dealbreaker` / `give db` | Add a Deal Breaker card | `give db` |
| `give slydeal` / `give sly` | Add a Sly Deal card | `give sly` |
| `give forcedeal` / `give force` | Add a Forced Deal card | `give force` |
| `give jsn` / `give justsayno` | Add a Just Say No card | `give jsn` |
| `give passgo` / `give go` | Add a Pass Go card | `give go` |
| `give debt` / `give debtcollector` | Add a Debt Collector card | `give debt` |
| `give birthday` | Add an It's My Birthday card | `give birthday` |
| `give double` / `give doublerent` | Add a Double The Rent card | `give double` |
| `give wild` | Add a multicolor property wildcard | `give wild` |
| `give <color>` | Add a property card of that color | `give brown` |
| `bank <value>` | Add money directly to your bank | `bank 10` |
| `clear hand` | Remove all cards from your hand | `clear hand` |
| `clear bank` | Remove all cards from your bank | `clear bank` |
| `myturn` / `skip` | Skip to your turn immediately | `myturn` |
| `giveto <name> <card>` | Give a card to another player | `giveto Bot1 rent pink` |

#### Color Shortcuts

Colors can be specified as full names or abbreviations:

| Color | Accepted Values |
|---|---|
| Brown | `brown`, `brn` |
| Light Blue | `lightblue`, `lb`, `light` |
| Pink | `pink`, `pnk` |
| Orange | `orange`, `org` |
| Red | `red` |
| Yellow | `yellow`, `yel` |
| Green | `green`, `grn` |
| Dark Blue | `darkblue`, `db`, `dark` |
| Railroad | `railroad`, `rr`, `rail` |
| Utility | `utility`, `util` |

### Special Routes

| URL | Description |
|---|---|
| `?page=deck` | Deck test page — renders all 106 cards |
| `/api/deck` | API endpoint — returns full deck JSON |

### Themes

Property names and category labels (e.g., "Stadium" vs "Railroad") are defined in JSON theme files under `src/backend/Themes/`.

| Theme | File | Description |
|---|---|---|
| `jeffopoly` | `jeffopoly.json` | Default theme with custom property names |
| `classic` | `classic.json` | Classic Monopoly property names |

Use the `?theme=` query string parameter to select a theme:
- `?theme=classic` — uses classic Monopoly names
- `?theme=jeffopoly` or omitted — uses Jeffopoly names
- Any unrecognized value falls back to `jeffopoly`

The theme parameter works on both the main game and the deck test page (`?page=deck&theme=classic`).

To create a new theme, add a JSON file to `src/backend/Themes/` with this structure:

```json
{
    "name": "My Theme",
    "categoryNames": {
        "Railroad": "Railroad",
        "Utility": "Utility"
    },
    "properties": {
        "Brown": ["Name 1", "Name 2"],
        "LightBlue": ["Name 1", "Name 2", "Name 3"],
        ...
    }
}
```

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
