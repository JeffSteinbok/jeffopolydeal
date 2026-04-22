export enum DebugFlags {
    None              = 0,
    VerboseLogging    = 1 << 0,  // 0x01 — Extra [DEBUG] console logging
    FixedGameCode     = 1 << 1,  // 0x02 — Force game code to TEST
    SkipLobby         = 1 << 2,  // 0x04 — Auto-start, bypass lobby
    ShowDeck          = 1 << 3,  // 0x08 — Show debug deck viewer
    PopulatedBoards   = 1 << 4,  // 0x10 — 3 AI bots with pre-populated boards
    PlayVsAi          = 1 << 5,  // 0x20 — 3 AI bots, normal game flow
    SkipToGameOver    = 1 << 6,  // 0x40 — Jump to Game Over screen
}

// Helpful combos (hex):
// SkipLobby + FixedGameCode: 0x06
// FixedGameCode + SkipLobby + ShowDeck: 0x0E
// SkipLobby + PopulatedBoards: 0x14
// FixedGameCode + SkipLobby + PopulatedBoards: 0x16
// SkipLobby + PlayVsAi: 0x24
// FixedGameCode + SkipLobby + PlayVsAi: 0x26
// All flags: 0x7F

export class Debug {
    static flags = DebugFlags.None;

    public static setFlags(debugFlags: DebugFlags): void {
        Debug.flags = debugFlags;
        if (Debug.isFlagSet(DebugFlags.VerboseLogging)) {
            console.log("Debug flags set:", debugFlags.toString(16));
        }
    }

    public static isFlagSet(flag: DebugFlags): boolean {
        return (Debug.flags & flag) === flag;
    }

    public static initFromUrl(): void {
        const params = new URLSearchParams(window.location.search);
        const debugParam = params.get("debug");
        if (debugParam) {
            const flags = parseInt(debugParam, 16);
            if (!isNaN(flags)) {
                Debug.setFlags(flags);
            }
        }
    }
}
