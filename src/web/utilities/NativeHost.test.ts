import {
    readNativeGameEntry,
    readPlayerNameHint,
    applyNativeHostClasses,
    isNativeHost,
    clientKind,
    NATIVE_ENTRY_PATH,
    NATIVE_CONTRACT_VERSION,
} from "./NativeHost";
import { BRIDGE_HANDLER_NAME } from "./NativeBridge";

const PID = "1F0C2B7A-6D3E-4A21-9C55-0A1B2C3D4E5F";

function loc(pathname: string, search: string): Location {
    return { pathname, search } as Location;
}

function entryUrl(overrides: Record<string, string | null> = {}): Location {
    const params: Record<string, string> = {
        v: NATIVE_CONTRACT_VERSION,
        host: "ios",
        pid: PID,
        name: "Jeff",
        game: "ABCD",
    };
    for (const [key, value] of Object.entries(overrides)) {
        if (value === null) delete params[key];
        else params[key] = value;
    }
    return loc(NATIVE_ENTRY_PATH, `?${new URLSearchParams(params).toString()}`);
}

describe("readNativeGameEntry", () => {
    it("parses a join entry", () => {
        expect(readNativeGameEntry(entryUrl())).toEqual({
            host: "ios",
            gameCode: "ABCD",
            playerName: "Jeff",
            playerId: PID,
            isRejoin: false,
        });
    });

    it("treats new=1 as create, ignoring any game code and rejoin flag", () => {
        const entry = readNativeGameEntry(entryUrl({ new: "1", game: null, rejoin: "1" }));
        expect(entry?.gameCode).toBe("");
        expect(entry?.isRejoin).toBe(false);
    });

    it("reads the rejoin flag for an existing game", () => {
        expect(readNativeGameEntry(entryUrl({ rejoin: "1" }))?.isRejoin).toBe(true);
    });

    it("uppercases the game code", () => {
        expect(readNativeGameEntry(entryUrl({ game: "abcd" }))?.gameCode).toBe("ABCD");
    });

    it("tolerates a trailing slash on the entry path", () => {
        const url = entryUrl();
        expect(readNativeGameEntry(loc(`${NATIVE_ENTRY_PATH}/`, url.search))).not.toBeNull();
    });

    it("ignores ordinary browser loads", () => {
        expect(readNativeGameEntry(loc("/", "?join=ABCD"))).toBeNull();
    });

    it("treats identity as optional, since a deep link may not carry one", () => {
        const entry = readNativeGameEntry(entryUrl({ pid: null, name: null }));
        expect(entry).not.toBeNull();
        expect(entry?.playerId).toBeUndefined();
        expect(entry?.playerName).toBeUndefined();
        expect(entry?.gameCode).toBe("ABCD");
    });

    it.each([
        ["a missing version", { v: null }],
        ["an unknown version", { v: "99" }],
        ["a missing host", { host: null }],
        ["a host that is not a plain identifier", { host: "ios<script>" }],
        ["a missing game code with no new flag", { game: null }],
        ["a malformed game code", { game: "AB" }],
    ])("rejects %s", (_label, overrides) => {
        expect(readNativeGameEntry(entryUrl(overrides))).toBeNull();
    });
});

describe("isNativeHost", () => {
    afterEach(() => { delete (window as any).webkit; });

    it("is false in a browser or PWA", () => {
        expect(isNativeHost()).toBe(false);
    });

    it("follows the bridge, so it survives navigation away from the entry URL", () => {
        (window as any).webkit = {
            messageHandlers: { [BRIDGE_HANDLER_NAME]: { postMessage: () => {} } },
        };
        expect(isNativeHost()).toBe(true);
    });
});

describe("readPlayerNameHint", () => {
    it("reads the device name the shell suggests", () => {
        expect(readPlayerNameHint(loc("/", "?name=Jeff%27s%20iPhone"))).toBe("Jeff's iPhone");
    });

    it("is null when absent or blank", () => {
        expect(readPlayerNameHint(loc("/", ""))).toBeNull();
        expect(readPlayerNameHint(loc("/", "?name=%20%20"))).toBeNull();
    });
});

describe("applyNativeHostClasses", () => {
    it("marks the document for native and standalone styling", () => {
        applyNativeHostClasses("ios");
        expect(document.documentElement.classList.contains("native-host")).toBe(true);
        expect(document.documentElement.classList.contains("native-host-ios")).toBe(true);
        expect(document.documentElement.classList.contains("pwa-standalone")).toBe(true);
    });
});

describe("clientKind", () => {
    afterEach(() => { delete (window as any).webkit; });

    it("reports the native app when the bridge is present", () => {
        (window as any).webkit = {
            messageHandlers: { [BRIDGE_HANDLER_NAME]: { postMessage: () => {} } },
        };
        expect(clientKind()).toBe("ios-app");
    });

    it("reports a plain browser otherwise", () => {
        expect(clientKind()).toBe("browser");
    });
});
