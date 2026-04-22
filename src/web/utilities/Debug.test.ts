import { Debug, DebugFlags } from "./Debug";

beforeEach(() => {
    Debug.setFlags(DebugFlags.None);
});

describe("DebugFlags enum", () => {
    it("None is 0", () => {
        expect(DebugFlags.None).toBe(0);
    });

    it("flags are powers of 2", () => {
        expect(DebugFlags.VerboseLogging).toBe(1);
        expect(DebugFlags.FixedGameCode).toBe(2);
        expect(DebugFlags.SkipLobby).toBe(4);
        expect(DebugFlags.ForcedHand).toBe(8);
        expect(DebugFlags.ShowAllHands).toBe(16);
        expect(DebugFlags.UnlimitedPlays).toBe(32);
        expect(DebugFlags.NoHandLimit).toBe(64);
        expect(DebugFlags.RichStart).toBe(128);
        expect(DebugFlags.InstantWin).toBe(256);
        expect(DebugFlags.SkipDraw).toBe(512);
        expect(DebugFlags.ShowDeck).toBe(1024);
        expect(DebugFlags.PopulatedBoards).toBe(2048);
        expect(DebugFlags.PlayVsAi).toBe(4096);
        expect(DebugFlags.SkipToGameOver).toBe(8192);
    });
});

describe("Debug.setFlags / isFlagSet", () => {
    it("setFlags sets flags correctly", () => {
        Debug.setFlags(DebugFlags.VerboseLogging);
        expect(Debug.flags).toBe(DebugFlags.VerboseLogging);
    });

    it("isFlagSet returns true for set flag", () => {
        Debug.setFlags(DebugFlags.SkipLobby);
        expect(Debug.isFlagSet(DebugFlags.SkipLobby)).toBe(true);
    });

    it("isFlagSet returns false for unset flag", () => {
        Debug.setFlags(DebugFlags.SkipLobby);
        expect(Debug.isFlagSet(DebugFlags.VerboseLogging)).toBe(false);
    });

    it("isFlagSet(None) returns true when no flags set", () => {
        expect(Debug.isFlagSet(DebugFlags.None)).toBe(true);
    });

    it("combined flags: both set, others not", () => {
        Debug.setFlags(DebugFlags.VerboseLogging | DebugFlags.FixedGameCode);
        expect(Debug.isFlagSet(DebugFlags.VerboseLogging)).toBe(true);
        expect(Debug.isFlagSet(DebugFlags.FixedGameCode)).toBe(true);
        expect(Debug.isFlagSet(DebugFlags.SkipLobby)).toBe(false);
        expect(Debug.isFlagSet(DebugFlags.ForcedHand)).toBe(false);
    });
});

describe("Debug.initFromUrl", () => {
    const originalLocation = window.location;

    afterEach(() => {
        Object.defineProperty(window, "location", {
            value: originalLocation,
            writable: true,
        });
    });

    it("parses hex debug param from URL", () => {
        Object.defineProperty(window, "location", {
            value: { search: "?debug=3" },
            writable: true,
        });
        Debug.initFromUrl();
        expect(Debug.isFlagSet(DebugFlags.VerboseLogging)).toBe(true);
        expect(Debug.isFlagSet(DebugFlags.FixedGameCode)).toBe(true);
        expect(Debug.isFlagSet(DebugFlags.SkipLobby)).toBe(false);
    });

    it("does nothing when no debug param", () => {
        Object.defineProperty(window, "location", {
            value: { search: "" },
            writable: true,
        });
        Debug.initFromUrl();
        expect(Debug.flags).toBe(DebugFlags.None);
    });
});
