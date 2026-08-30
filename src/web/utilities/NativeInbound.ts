/**
 * The inbound half of the native bridge: everything a shell pushes into this
 * client, exposed as `window.jeffopolyNative`.
 *
 * Each entry is something the web genuinely cannot do for itself — discover
 * games on the local network, hold an APNs token, know that the app was
 * backgrounded, or be launched by a notification tap. Nothing here is gameplay.
 *
 * Keep in sync with the iOS app's JeffopolyDeal/Web/GameWebView.swift
 * (repo: JeffSteinbok/jeffopolydeal-ios).
 */
import { useEffect, useState } from "react";

export interface NearbyGame {
    gameCode: string;
    hostName: string;
}

/** Installed on `window` for the shell to call. */
export interface NativeInboundAPI {
    setNearbyGames(games: unknown): void;
    /** APNs device token, so the client can register it over its own hub. */
    setPushToken(token: unknown): void;
    /** "active" when the app comes to the foreground, "background" when it leaves. */
    setLifecycle(phase: unknown): void;
    /** A notification tap or deep link asking for a specific game. */
    openGame(gameCode: unknown): void;
    /** Why push is or is not working on the device, for support reporting. */
    setPushDiagnostics(status: unknown): void;
    /** The real safe-area insets, in CSS pixels, measured by the shell. */
    setSafeAreaInsets(insets: unknown): void;
}

export type LifecyclePhase = "active" | "background";

const GAME_CODE_PATTERN = /^[A-Z0-9]{4}$/;

let nearbyGames: NearbyGame[] = [];
const listeners = new Set<(games: NearbyGame[]) => void>();

let pushToken: string | null = null;
let pushDiagnostics = "none";
const pushTokenListeners = new Set<(token: string) => void>();

const foregroundListeners = new Set<() => void>();
const openGameListeners = new Set<(gameCode: string) => void>();

/**
 * Accepts only well-formed entries. The shell is trusted, but this is still a
 * `window` global that anything on the page could call.
 */
function sanitize(input: unknown): NearbyGame[] {
    if (!Array.isArray(input)) return [];
    const seen = new Set<string>();
    const games: NearbyGame[] = [];

    for (const entry of input) {
        if (typeof entry !== "object" || entry === null) continue;
        const { gameCode, hostName } = entry as Record<string, unknown>;
        if (typeof gameCode !== "string" || typeof hostName !== "string") continue;

        const code = gameCode.trim().toUpperCase();
        if (!GAME_CODE_PATTERN.test(code) || seen.has(code)) continue;

        seen.add(code);
        games.push({ gameCode: code, hostName: hostName.trim() || "Nearby Host" });
    }

    return games;
}

export function installNativeInboundAPI(): void {
    const api: NativeInboundAPI = {
        setNearbyGames(games: unknown) {
            nearbyGames = sanitize(games);
            for (const listener of listeners) listener(nearbyGames);
        },

        setPushToken(token: unknown) {
            if (typeof token !== "string") return;
            const trimmed = token.trim();
            // APNs tokens are lowercase hex; anything else is not one.
            if (!/^[0-9a-f]{8,200}$/.test(trimmed) || trimmed === pushToken) return;
            pushToken = trimmed;
            for (const listener of pushTokenListeners) listener(trimmed);
        },

        setLifecycle(phase: unknown) {
            // Only the return to the foreground is actionable: the socket may
            // have died while suspended, and nothing tells us until we look.
            if (phase !== "active") return;
            for (const listener of foregroundListeners) listener();
        },

        setPushDiagnostics(status: unknown) {
            if (typeof status !== "string") return;
            pushDiagnostics = status.trim().slice(0, 60) || "none";
        },

        setSafeAreaInsets(insets: unknown) {
            // env(safe-area-inset-*) reports 0 on first layout inside the web
            // view and only corrects later, which is too late for anything
            // sized once. The shell measures it properly, so prefer that.
            if (typeof insets !== "object" || insets === null) return;
            const { top, bottom } = insets as Record<string, unknown>;
            const set = (name: string, value: unknown) => {
                if (typeof value !== "number" || !isFinite(value) || value < 0 || value > 200) return;
                document.documentElement.style.setProperty(name, `${value}px`);
            };
            set("--native-safe-top", top);
            set("--native-safe-bottom", bottom);
        },

        openGame(gameCode: unknown) {
            if (typeof gameCode !== "string") return;
            const code = gameCode.trim().toUpperCase();
            if (!GAME_CODE_PATTERN.test(code)) return;
            for (const listener of openGameListeners) listener(code);
        },
    };
    (window as unknown as { jeffopolyNative?: NativeInboundAPI }).jeffopolyNative = api;
}

export function getPushToken(): string | null {
    return pushToken;
}

export function getPushDiagnostics(): string {
    return pushDiagnostics;
}

/** Fires immediately if a token already arrived, then on every change. */
export function onPushToken(listener: (token: string) => void): () => void {
    pushTokenListeners.add(listener);
    if (pushToken) listener(pushToken);
    return () => { pushTokenListeners.delete(listener); };
}

export function onReturnToForeground(listener: () => void): () => void {
    foregroundListeners.add(listener);
    return () => { foregroundListeners.delete(listener); };
}

export function onOpenGame(listener: (gameCode: string) => void): () => void {
    openGameListeners.add(listener);
    return () => { openGameListeners.delete(listener); };
}

/** Test seam. */
export function resetNativeInbound(): void {
    nearbyGames = [];
    listeners.clear();
    pushToken = null;
    pushDiagnostics = "none";
    pushTokenListeners.clear();
    foregroundListeners.clear();
    openGameListeners.clear();
}

export function getNearbyGames(): NearbyGame[] {
    return nearbyGames;
}

export function useNearbyGames(): NearbyGame[] {
    const [games, setGames] = useState<NearbyGame[]>(nearbyGames);
    useEffect(() => {
        const listener = (next: NearbyGame[]) => setGames(next);
        listeners.add(listener);
        setGames(nearbyGames);
        return () => { listeners.delete(listener); };
    }, []);
    return games;
}
