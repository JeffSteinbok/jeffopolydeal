import React from "react";
import { screen, fireEvent } from "@testing-library/react";
import { Hand } from "./Hand";
import { Card, GameState, PlayerState } from "../../../Types";
import { renderWithConfig as render } from "../../../utilities/test-helpers";

// Mock ResizeObserver for jsdom
globalThis.ResizeObserver = class {
    observe() {}
    unobserve() {}
    disconnect() {}
} as any;

function makeCard(id: number, name = `Card ${id}`): Card {
    return { id, cardType: "Money", moneyValue: 1, name, isMulticolorWild: false, isWildRent: false };
}

function makePlayer(overrides: Partial<PlayerState> = {}): PlayerState {
    return {
        playerId: "me", connectionId: "me-conn", name: "Me", handCount: 3,
        isConnected: true, bank: [], propertySets: [], unboundWilds: [],
        completedSetCount: 0, uniqueCompletedSetCount: 0, ...overrides,
    };
}

function makeGameState(myState: PlayerState): GameState {
    return {
        phase: "Play", gameCode: "ABC", players: [myState],
        currentPlayerIndex: 0, playsUsed: 0, drawPileCount: 40,
        discardPileCount: 0, recentActions: [],
    };
}

function renderHand(overrides: any = {}) {
    const cards = overrides.cards ?? [makeCard(1), makeCard(2), makeCard(3)];
    const myState = makePlayer({ hand: cards });
    const gameState = makeGameState(myState);
    const props = {
        cards,
        canPlay: overrides.canPlay ?? true,
        phase: overrides.phase ?? "Play",
        gameState,
        myConnectionId: "me-conn",
        playsRemaining: overrides.playsRemaining ?? 3,
        isMyTurn: overrides.isMyTurn ?? true,
        onEndTurn: overrides.onEndTurn ?? vi.fn(),
        onPlayCard: overrides.onPlayCard ?? vi.fn(),
        onDiscardCard: overrides.onDiscardCard ?? vi.fn(),
        ...overrides,
    };
    return render(<Hand {...props} />);
}

describe("Hand", () => {
    it("renders correct number of cards", () => {
        const { container } = renderHand();
        const cards = container.querySelectorAll(".md-card");
        expect(cards.length).toBe(3);
    });

    it("shows hand count in label", () => {
        renderHand();
        expect(screen.getByText(/Your Hand \(3\)/)).toBeTruthy();
    });

    it("shows 'No cards in hand' when empty", () => {
        renderHand({ cards: [] });
        expect(screen.getByText("No cards in hand")).toBeTruthy();
    });

    it("clicking a card in Play phase opens PlayCardModal", () => {
        const { container } = renderHand({ phase: "Play" });
        const cards = container.querySelectorAll(".md-card");
        fireEvent.click(cards[0]);
        // PlayCardModal should appear — check for modal overlay
        expect(container.querySelector(".playCardModal")).toBeTruthy();
    });

    it("clicking a card in Discard phase calls onDiscardCard directly", () => {
        const onDiscardCard = vi.fn();
        const { container } = renderHand({ phase: "Discard", onDiscardCard });
        const cards = container.querySelectorAll(".md-card");
        fireEvent.click(cards[0]);
        expect(onDiscardCard).toHaveBeenCalledWith(1);
    });

    it("shows 'No Plays Remaining' popup when playsRemaining=0 and isMyTurn", () => {
        renderHand({ playsRemaining: 0, isMyTurn: true, phase: "Play" });
        expect(screen.getByText("No Plays Remaining")).toBeTruthy();
    });

    it("no plays popup not shown when not my turn", () => {
        renderHand({ playsRemaining: 0, isMyTurn: false, phase: "Play" });
        expect(screen.queryByText("No Plays Remaining")).toBeNull();
    });

    it("play counter dots rendered correctly", () => {
        const { container } = renderHand({ playsRemaining: 1, isMyTurn: true });
        const filledDots = container.querySelectorAll(".playDot--filled");
        expect(filledDots.length).toBe(2); // 3 - 1 = 2 plays used
    });

    it("End Turn button visible during play phase", () => {
        renderHand({ isMyTurn: true, phase: "Play" });
        expect(screen.getByText("End Turn")).toBeTruthy();
    });

    it("End Turn button calls onEndTurn", () => {
        const onEndTurn = vi.fn();
        renderHand({ isMyTurn: true, phase: "Play", onEndTurn });
        fireEvent.click(screen.getByText("End Turn"));
        expect(onEndTurn).toHaveBeenCalledTimes(1);
    });

    it("Re-Arrange Cards button dismisses no-plays popup", () => {
        renderHand({ playsRemaining: 0, isMyTurn: true, phase: "Play" });
        fireEvent.click(screen.getByText("Re-Arrange Cards"));
        expect(screen.queryByText("No Plays Remaining")).toBeNull();
    });
});
