/**
 * One-way message channel from the shared React client to a native shell.
 *
 * Deliberately semantic and small: the shell learns *what happened in the game*,
 * never how the game is rendered. Anything the shell needs to send inward
 * travels in the entry URL instead — see NativeHost.ts.
 *
 * Keep in sync with the iOS app's JeffopolyDeal/Web/GameBridge.swift
 * (repo: JeffSteinbok/jeffopolydeal-ios).
 */

/** Bumped when the envelope shape changes incompatibly. */
export const BRIDGE_VERSION = 1;

/** Handler name the shell registers on its WKUserContentController. */
export const BRIDGE_HANDLER_NAME = "jeffopoly";

/**
 * Message families. `haptic` is the only one consumed today; the others are
 * reserved so the shell's validation does not have to change when nearby
 * advertising and lifecycle signals arrive.
 */
export type BridgeMessageType = "haptic" | "gameContext" | "lifecycle";

export interface BridgeMessage {
    v: number;
    type: BridgeMessageType;
    /** Stable per-occurrence key. Repeats are dropped rather than replayed. */
    id?: string;
    payload?: Record<string, unknown>;
}

interface WebKitMessageHandler {
    postMessage(message: unknown): void;
}

function nativeHandler(): WebKitMessageHandler | null {
    const handler = (window as unknown as {
        webkit?: { messageHandlers?: Record<string, WebKitMessageHandler | undefined> };
    }).webkit?.messageHandlers?.[BRIDGE_HANDLER_NAME];
    return typeof handler?.postMessage === "function" ? handler : null;
}

export function isNativeBridgeAvailable(): boolean {
    return nativeHandler() !== null;
}

/**
 * Ids already delivered. React re-renders and repeated state hydration must not
 * replay feedback, and the shell should not have to defend against it alone.
 */
const deliveredIds = new Set<string>();
const MAX_REMEMBERED_IDS = 500;

/** Test seam; also worth calling if a client ever hosts two games in one page. */
export function resetBridgeDeduplication(): void {
    deliveredIds.clear();
}

/**
 * Posts a message when a native shell is listening. In a browser or PWA there is
 * no handler and this is a no-op, so callers never branch on the host.
 *
 * Returns whether the message was delivered — false for "no shell", "already
 * sent this id", or "the shell rejected it".
 */
export function postToNativeHost(
    type: BridgeMessageType,
    payload?: Record<string, unknown>,
    id?: string
): boolean {
    const handler = nativeHandler();
    if (!handler) return false;

    if (id !== undefined) {
        if (deliveredIds.has(id)) return false;
        // Cheap bound: a game produces far fewer events than this.
        if (deliveredIds.size >= MAX_REMEMBERED_IDS) deliveredIds.clear();
        deliveredIds.add(id);
    }

    const message: BridgeMessage = { v: BRIDGE_VERSION, type };
    if (id !== undefined) message.id = id;
    if (payload !== undefined) message.payload = payload;

    try {
        handler.postMessage(message);
        return true;
    } catch {
        // A shell that rejects a message must never take gameplay down with it.
        return false;
    }
}
