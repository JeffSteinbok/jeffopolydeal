export enum DebugFlags {
    None              = 0,
    VerboseLogging    = 1 << 0,
    FixedGameCode     = 1 << 1,
    SkipLobby         = 1 << 2,
    ForcedHand        = 1 << 3,
    ShowAllHands      = 1 << 4,
    UnlimitedPlays    = 1 << 5,
    NoHandLimit       = 1 << 6,
    RichStart         = 1 << 7,
    InstantWin        = 1 << 8,
    SkipDraw          = 1 << 9,
    ShowDeck          = 1 << 10,
}

// Helpful combos:
// SkipLobby + FixedGameCode: 6
// FixedGameCode + SkipLobby + ShowDeck: 406
// All debug shortcuts: 0x7FF

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
