import { GameState } from "../Types";
import { postToNativeHost } from "./NativeBridge";

/**
 * Semantic haptic vocabulary.
 *
 * These name moments in the game, not waveforms — the native shell owns the
 * mapping from event to feedback generator. Keep the set small: a card game
 * that buzzes constantly is worse than one that never buzzes at all, and every
 * event added here has to earn a distinguishable feel on the device.
 */
export type HapticEvent =
    | "selection"       // a card was picked up or put back down
    | "cardPlayed"      // this player committed a card
    | "paymentSettled"  // a payment this player was part of completed
    | "bigHit"          // a high-impact action landed on this player
    | "invalidMove"     // the server rejected an attempt
    | "turnStarted"     // it became this player's turn
    | "gameWon";        // this player won

/** Actions worth a heavier feel when they land on you. */
const HIGH_IMPACT_ACTIONS: ReadonlySet<string> = new Set([
    "DealBreaker", "SlyDeal", "ForceDeal", "DebtCollector",
]);

export interface DerivedHaptic {
    event: HapticEvent;
    /** Stable per-occurrence key, so a replayed state cannot replay feedback. */
    id: string;
}

export function emitHaptic(event: HapticEvent, id?: string): void {
    postToNativeHost("haptic", { event }, id);
}

export function emitDerivedHaptics(haptics: readonly DerivedHaptic[]): void {
    for (const { event, id } of haptics) emitHaptic(event, id);
}

function isMyTurn(state: GameState, myPlayerId: string): boolean {
    return state.players[state.currentPlayerIndex]?.playerId === myPlayerId;
}

function myName(state: GameState, myPlayerId: string): string | undefined {
    return state.players.find((p) => p.playerId === myPlayerId)?.name;
}

/** Highest action id seen, used as a monotonic clock for the state. */
function clock(state: GameState | null): number {
    if (!state?.recentActions?.length) return 0;
    return state.recentActions.reduce((max, a) => (a.id > max ? a.id : max), 0);
}

/**
 * Derives haptics from a game state transition.
 *
 * Pure and id-stamped: `prev` of null means "first state for this game", which
 * yields nothing so a cold launch or a rejoin does not fire a burst of feedback
 * for history the player has already lived through.
 */
export function deriveHaptics(
    prev: GameState | null,
    next: GameState,
    myPlayerId: string
): DerivedHaptic[] {
    if (!prev) return [];

    const derived: DerivedHaptic[] = [];
    const name = myName(next, myPlayerId);
    const since = clock(prev);

    // One event per new action, most specific first.
    for (const action of next.recentActions ?? []) {
        if (action.id <= since) continue;
        const id = `action:${action.id}`;

        const landedOnMe = action.targetPlayerName === name;
        const isMine = action.playerName === name;
        const kind = action.cardPlayed?.actionKind;

        if (action.text.startsWith("Paid") && (isMine || landedOnMe)) {
            derived.push({ event: "paymentSettled", id });
        } else if (landedOnMe && kind && HIGH_IMPACT_ACTIONS.has(kind)) {
            derived.push({ event: "bigHit", id });
        } else if (isMine && action.cardPlayed) {
            derived.push({ event: "cardPlayed", id });
        }
    }

    // A rejected payment is the server telling the player "not that".
    if (next.paymentError && next.paymentError !== prev.paymentError) {
        derived.push({ event: "invalidMove", id: `reject:${clock(next)}:${next.paymentError}` });
    }

    // Turn handoff, but not the lobby-to-first-turn case where the player is
    // already looking at the screen expecting it.
    const playablePhases = ["Draw", "Play", "Discard"];
    if (
        playablePhases.includes(next.phase) &&
        isMyTurn(next, myPlayerId) &&
        !(isMyTurn(prev, myPlayerId) && playablePhases.includes(prev.phase))
    ) {
        derived.push({ event: "turnStarted", id: `turn:${next.gameCode}:${clock(next)}` });
    }

    if (next.phase === "GameOver" && prev.phase !== "GameOver" && next.winnerId === myPlayerId) {
        derived.push({ event: "gameWon", id: `won:${next.gameCode}:${next.winnerId}` });
    }

    return derived;
}
