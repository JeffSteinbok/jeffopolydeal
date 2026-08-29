import { deriveHaptics } from "./Haptics";
import { GameState, GameAction, PlayerState, Card } from "../Types";

const ME = "me-id";
const THEM = "them-id";

function player(playerId: string, name: string): PlayerState {
    return {
        playerId, name, connectionId: "", handCount: 0, isConnected: true,
        bank: [], propertySets: [], unboundWilds: [],
        completedSetCount: 0, uniqueCompletedSetCount: 0,
    } as PlayerState;
}

function card(actionKind?: string): Card {
    return { id: 1, cardType: "Action", moneyValue: 0, name: "c",
        isMulticolorWild: false, isWildRent: false, actionKind } as Card;
}

function action(over: Partial<GameAction> & { id: number }): GameAction {
    return { playerName: "Them", text: "did a thing", ...over };
}

function state(over: Partial<GameState> = {}): GameState {
    return {
        phase: "Play", gameCode: "ABCD",
        players: [player(ME, "Me"), player(THEM, "Them")],
        currentPlayerIndex: 0, playsUsed: 0, drawPileCount: 10, discardPileCount: 0,
        recentActions: [], ...over,
    } as GameState;
}

describe("first state", () => {
    it("fires nothing, so a rejoin does not replay history", () => {
        const next = state({
            recentActions: [action({ id: 1, playerName: "Me", cardPlayed: card() })],
        });
        expect(deriveHaptics(null, next, ME)).toEqual([]);
    });
});

describe("action-derived events", () => {
    it("fires cardPlayed for my own card", () => {
        const prev = state();
        const next = state({ recentActions: [action({ id: 1, playerName: "Me", cardPlayed: card() })] });
        expect(deriveHaptics(prev, next, ME)).toEqual([{ event: "cardPlayed", id: "action:1" }]);
    });

    it("fires bigHit when a high-impact action lands on me", () => {
        const prev = state();
        const next = state({
            recentActions: [action({ id: 1, targetPlayerName: "Me", cardPlayed: card("DealBreaker") })],
        });
        expect(deriveHaptics(prev, next, ME)).toEqual([{ event: "bigHit", id: "action:1" }]);
    });

    it("stays quiet for a high-impact action aimed at someone else", () => {
        const prev = state();
        const next = state({
            recentActions: [action({ id: 1, targetPlayerName: "Them", cardPlayed: card("SlyDeal") })],
        });
        expect(deriveHaptics(prev, next, ME)).toEqual([]);
    });

    it("fires paymentSettled when a payment involves me", () => {
        const prev = state();
        const next = state({ recentActions: [action({ id: 1, playerName: "Me", text: "Paid 3M" })] });
        expect(deriveHaptics(prev, next, ME)).toEqual([{ event: "paymentSettled", id: "action:1" }]);
    });

    it("ignores actions already present in the previous state", () => {
        const prev = state({ recentActions: [action({ id: 1, playerName: "Me", cardPlayed: card() })] });
        const next = state({
            recentActions: [
                action({ id: 1, playerName: "Me", cardPlayed: card() }),
                action({ id: 2, playerName: "Me", cardPlayed: card() }),
            ],
        });
        expect(deriveHaptics(prev, next, ME)).toEqual([{ event: "cardPlayed", id: "action:2" }]);
    });

    it("emits at most one event per action", () => {
        const prev = state();
        const next = state({
            recentActions: [action({ id: 1, playerName: "Me", targetPlayerName: "Me", text: "Paid 5M", cardPlayed: card("DebtCollector") })],
        });
        expect(deriveHaptics(prev, next, ME)).toHaveLength(1);
    });
});

describe("turnStarted", () => {
    it("fires when the turn moves to me", () => {
        const prev = state({ currentPlayerIndex: 1 });
        const next = state({ currentPlayerIndex: 0 });
        expect(deriveHaptics(prev, next, ME)).toEqual([
            { event: "turnStarted", id: "turn:ABCD:0" },
        ]);
    });

    it("does not re-fire while the turn is still mine", () => {
        const prev = state({ currentPlayerIndex: 0 });
        const next = state({ currentPlayerIndex: 0, playsUsed: 1 });
        expect(deriveHaptics(prev, next, ME)).toEqual([]);
    });

    it("does not fire for an opponent's turn", () => {
        const prev = state({ currentPlayerIndex: 0 });
        const next = state({ currentPlayerIndex: 1 });
        expect(deriveHaptics(prev, next, ME)).toEqual([]);
    });

    it("does not fire in the lobby", () => {
        const prev = state({ phase: "Lobby", currentPlayerIndex: 1 });
        const next = state({ phase: "Lobby", currentPlayerIndex: 0 });
        expect(deriveHaptics(prev, next, ME)).toEqual([]);
    });

    it("fires across a Draw to Play phase change without repeating", () => {
        const lobby = state({ phase: "Lobby", currentPlayerIndex: 0 });
        const draw = state({ phase: "Draw", currentPlayerIndex: 0 });
        expect(deriveHaptics(lobby, draw, ME)).toHaveLength(1);
        expect(deriveHaptics(draw, state({ phase: "Play", currentPlayerIndex: 0 }), ME)).toEqual([]);
    });
});

describe("invalidMove", () => {
    it("fires on a new payment error", () => {
        const prev = state();
        const next = state({ paymentError: "Not enough money" });
        expect(deriveHaptics(prev, next, ME)).toEqual([
            { event: "invalidMove", id: "reject:0:Not enough money" },
        ]);
    });

    it("does not re-fire while the same error stands", () => {
        const prev = state({ paymentError: "Not enough money" });
        const next = state({ paymentError: "Not enough money", playsUsed: 1 });
        expect(deriveHaptics(prev, next, ME)).toEqual([]);
    });
});

describe("gameWon", () => {
    it("fires when I win", () => {
        const prev = state();
        const next = state({ phase: "GameOver", winnerId: ME });
        expect(deriveHaptics(prev, next, ME)).toEqual([
            { event: "gameWon", id: "won:ABCD:me-id" },
        ]);
    });

    it("stays quiet when someone else wins", () => {
        const prev = state();
        const next = state({ phase: "GameOver", winnerId: THEM });
        expect(deriveHaptics(prev, next, ME)).toEqual([]);
    });
});
