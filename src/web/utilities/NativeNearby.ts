/**
 * Games discovered on the local network by a native shell.
 *
 * Multipeer Connectivity has no web equivalent, so this is the one part of app
 * entry the shell still owns. It pushes what it finds in here; the start page
 * renders it. Nothing else about discovery crosses the boundary.
 *
 * Keep in sync with the iOS app's JeffopolyDeal/Web/GameWebView.swift
 * (repo: JeffSteinbok/jeffopolydeal-ios).
 */
import { useEffect, useState } from "react";

export interface NearbyGame {
    gameCode: string;
    hostName: string;
}

/** The inbound half of the bridge, installed on `window` for the shell to call. */
export interface NativeInboundAPI {
    setNearbyGames(games: unknown): void;
}

const GAME_CODE_PATTERN = /^[A-Z0-9]{4}$/;

let nearbyGames: NearbyGame[] = [];
const listeners = new Set<(games: NearbyGame[]) => void>();

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
    };
    (window as unknown as { jeffopolyNative?: NativeInboundAPI }).jeffopolyNative = api;
}

/** Test seam. */
export function resetNearbyGames(): void {
    nearbyGames = [];
    listeners.clear();
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
