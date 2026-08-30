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

import { isNativeBridgeAvailable } from "./NativeBridge";

/** Path the native shell loads to enter a specific game directly. */
export const NATIVE_ENTRY_PATH = "/play";

/** Bumped when the query-string shape changes incompatibly. */
export const NATIVE_CONTRACT_VERSION = "1";

/**
 * True when running inside a native shell.
 *
 * The bridge handler is the signal rather than a URL parameter, because it is
 * present on every page in the web view and so survives navigation between the
 * start page and a game. A browser or PWA never has it.
 */
export function isNativeHost(): boolean {
    return isNativeBridgeAvailable();
}

/**
 * The display name the shell suggests, from the device name. Only a hint for
 * prefilling the start page — the player can always change it.
 */
export function readPlayerNameHint(location: Location = window.location): string | null {
    const hint = new URLSearchParams(location.search).get("name")?.trim();
    return hint ? hint : null;
}

export interface NativeGameEntry {
    /** Which native shell we are embedded in, currently only "ios". */
    host: string;
    /** Four-character game code, or "" to ask the server for a new game. */
    gameCode: string;
    /** Only when the shell knows one; otherwise the client uses its own. */
    playerName?: string;
    /** Only when the shell knows one; otherwise the client uses its own. */
    playerId?: string;
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
    if (!HOST_PATTERN.test(host)) return null;

    // Identity is optional: a deep link or notification tap knows which game to
    // open but not necessarily who is opening it, and the client already has a
    // player id of its own.
    const playerId = (params.get("pid") ?? "").trim() || undefined;
    const playerName = (params.get("name") ?? "").trim() || undefined;

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
export function applyNativeHostClasses(host = "ios"): void {
    document.documentElement.classList.add("native-host", `native-host-${host}`, "pwa-standalone");
}

/**
 * Which client this is, for server-side telemetry. The iOS app and the browser
 * reach the hub through the same JavaScript now, so without this they are
 * indistinguishable once a connection is up.
 */
export function clientKind(): "ios-app" | "pwa" | "browser" {
    if (isNativeHost()) return "ios-app";

    const standalone = (navigator as unknown as { standalone?: boolean }).standalone
        || window.matchMedia?.("(display-mode: standalone)").matches;
    return standalone ? "pwa" : "browser";
}
