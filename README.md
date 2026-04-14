# Jeffopoly Deal

Use `?debug=<hexFlags>` in the game URL to enable web debug flags.

Example:

- `http://localhost:5173/?debug=6` (`SkipLobby + FixedGameCode`)

## Debug flags

All flags are defined in `/src/web/utilities/Debug.ts`.

| Flag | Hex | Description |
|---|---:|---|
| `VerboseLogging` | `0x001` | Extra console logging. |
| `FixedGameCode` | `0x002` | Forces newly-created game code to `TEST`. |
| `SkipLobby` | `0x004` | Auto-start a game immediately after creation. |
| `ForcedHand` | `0x008` | Reserved debug flag (currently not used in gameplay flow). |
| `ShowAllHands` | `0x010` | Reserved debug flag (currently not used in gameplay flow). |
| `UnlimitedPlays` | `0x020` | Reserved debug flag (currently not used in gameplay flow). |
| `NoHandLimit` | `0x040` | Reserved debug flag (currently not used in gameplay flow). |
| `RichStart` | `0x080` | Reserved debug flag (currently not used in gameplay flow). |
| `InstantWin` | `0x100` | Reserved debug flag (currently not used in gameplay flow). |
| `SkipDraw` | `0x200` | Reserved debug flag (currently not used in gameplay flow). |
| `ShowDeck` | `0x400` | Shows the in-game debug deck viewer. |
| `PopulatedBoards` | `0x800` | Auto-starts with 3 AI bots and pre-populated boards (debug setup). |

### Helpful combinations

- `0x006` = `SkipLobby + FixedGameCode`
- `0x406` = `SkipLobby + FixedGameCode + ShowDeck`
- `0x806` = `SkipLobby + FixedGameCode + PopulatedBoards`
- `0xFFF` = all current flags
