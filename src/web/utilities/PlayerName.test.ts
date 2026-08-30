/**
 * The name-memory rules from App.tsx. A native shell suggests the device name,
 * but that is only ever a first-run hint — an edited name has to survive.
 */
const PLAYER_NAME_KEY = "jeffopolydeal_playerName";

function loadPlayerName(): string | null {
    try {
        const name = localStorage.getItem(PLAYER_NAME_KEY)?.trim();
        return name ? name : null;
    } catch {
        return null;
    }
}

function savePlayerName(name: string) {
    const trimmed = name.trim().slice(0, 20);
    if (!trimmed) return;
    try {
        localStorage.setItem(PLAYER_NAME_KEY, trimmed);
    } catch { /* ignore */ }
}

function resolveHint(saved: string | null, deviceHint: string | null): string | undefined {
    return saved ?? deviceHint ?? undefined;
}

beforeEach(() => localStorage.clear());

describe("player name memory", () => {
    it("remembers an entered name", () => {
        savePlayerName("Charlie");
        expect(loadPlayerName()).toBe("Charlie");
    });

    it("prefers a remembered name over the device hint", () => {
        savePlayerName("Charlie");
        expect(resolveHint(loadPlayerName(), "Jeff")).toBe("Charlie");
    });

    it("falls back to the device hint when nothing is remembered", () => {
        expect(resolveHint(loadPlayerName(), "Jeff")).toBe("Jeff");
    });

    it("is undefined when there is neither", () => {
        expect(resolveHint(loadPlayerName(), null)).toBeUndefined();
    });

    it("trims and caps what it stores", () => {
        savePlayerName("   Bartholomew Featherstonehaugh   ");
        expect(loadPlayerName()).toBe("Bartholomew Feathers"); // 20 chars, matching the input maxLength
    });

    it("never stores a blank name over a good one", () => {
        savePlayerName("Charlie");
        savePlayerName("   ");
        expect(loadPlayerName()).toBe("Charlie");
    });
});
