/**
 * Entry contract between a native shell and the shared React client.
 *
 * The native app owns app entry — create/join, nearby discovery, share links,
 * notifications — and hands gameplay off by loading NATIVE_ENTRY_PATH with the
 * player's identity in the query string. Gameplay itself, including the SignalR
 * connection, stays here.
 *
 * Keep in sync with the iOS app's JeffopolyDeal/Web/GameWebURL.swift
 * (repo: JeffSteinbok/jeffopolydeal-ios).
 */

/** Path the native shell loads. Served by MapFallbackToFile in Program.cs. */
export const NATIVE_ENTRY_PATH = "/play";

/** Bumped when the query-string shape changes incompatibly. */
export const NATIVE_CONTRACT_VERSION = "1";

export interface NativeGameEntry {
    /** Which native shell we are embedded in, currently only "ios". */
    host: string;
    /** Four-character game code, or "" to ask the server for a new game. */
    gameCode: string;
    playerName: string;
    /** Stable per-install id owned by the native shell, not by localStorage. */
    playerId: string;
    isRejoin: boolean;
}

const GAME_CODE_PATTERN = /^[A-Z0-9]{4}$/;
const HOST_PATTERN = /^[a-z]{2,10}$/;

/**
 * Parses a native gameplay entry, or returns null for ordinary browser/PWA
 * loads so those keep their existing localStorage-driven behaviour.
 */
export function readNativeGameEntry(location: Location = window.location): NativeGameEntry | null {
    if (location.pathname.replace(/\/+$/, "") !== NATIVE_ENTRY_PATH) return null;

    const params = new URLSearchParams(location.search);
    if (params.get("v") !== NATIVE_CONTRACT_VERSION) return null;

    const host = (params.get("host") ?? "").trim().toLowerCase();
    const playerId = (params.get("pid") ?? "").trim();
    const playerName = (params.get("name") ?? "").trim();
    if (!HOST_PATTERN.test(host) || !playerId || !playerName) return null;

    const wantsNewGame = params.get("new") === "1";
    const gameCode = (params.get("game") ?? "").trim().toUpperCase();
    if (!wantsNewGame && !GAME_CODE_PATTERN.test(gameCode)) return null;

    return {
        host,
        gameCode: wantsNewGame ? "" : gameCode,
        playerName,
        playerId,
        isRejoin: !wantsNewGame && params.get("rejoin") === "1",
    };
}

/**
 * Marks <html> so native-only presentation can be scoped in CSS without forking
 * game logic. `pwa-standalone` comes along because the embedded surface has the
 * same no-browser-chrome, safe-area-inset layout as the installed PWA.
 */
export function applyNativeHostClasses(entry: NativeGameEntry): void {
    document.documentElement.classList.add("native-host", `native-host-${entry.host}`, "pwa-standalone");
}

/**
 * Reflects the resolved game code back into the address bar after a create. The
 * native shell observes the web view's URL to learn the code the server picked,
 * so it can persist a rejoin session without mirroring gameplay state.
 */
export function publishResolvedGameCode(code: string): void {
    const url = new URL(window.location.href);
    url.searchParams.set("game", code);
    url.searchParams.delete("new");
    window.history.replaceState(null, "", url.toString());
}

/**
 * Hands control back to the native shell, which cancels this navigation and
 * pops to its own start screen.
 */
export function exitToNativeShell(): void {
    window.location.assign("/");
}
