import React from "react";
import { act, render, screen, waitFor } from "@testing-library/react";
import { GamePage } from "./GamePage";
import { GameState, PlayerState } from "../../Types";
import { testGameConfig } from "../../utilities/test-helpers";

let pushState: ((state: GameState) => void) | undefined;

vi.mock("./GameSignalRClient", () => ({
    GameSignalRClient: class {
        constructor(onGameStateUpdated: (state: GameState) => void) {
            pushState = onGameStateUpdated;
        }
        start = vi.fn().mockResolvedValue(undefined);
        stop = vi.fn().mockResolvedValue(undefined);
        joinGame = vi.fn().mockResolvedValue(undefined);
        rejoinGame = vi.fn().mockResolvedValue(true);
        drawCards = vi.fn().mockResolvedValue(undefined);
        playCard = vi.fn().mockResolvedValue(undefined);
        endTurn = vi.fn().mockResolvedValue(undefined);
        discardCard = vi.fn().mockResolvedValue(undefined);
        cancelDiscard = vi.fn().mockResolvedValue(undefined);
        respondToAction = vi.fn().mockResolvedValue(undefined);
        flipWildcard = vi.fn().mockResolvedValue(undefined);
        moveProperty = vi.fn().mockResolvedValue(undefined);
    },
}));

vi.mock("./components/PlayerBoard", () => ({ PlayerBoard: () => <div data-testid="player-board" /> }));
vi.mock("./components/PlayerSummaryCard", () => ({ PlayerSummaryCard: () => <div data-testid="player-summary" /> }));
vi.mock("./components/PlayerInspectModal", () => ({ PlayerInspectModal: () => <div data-testid="player-inspect" /> }));
vi.mock("./components/Hand", () => ({ Hand: () => <div data-testid="hand" /> }));
vi.mock("./components/ActionModal", () => ({ ActionModal: () => <div data-testid="action-modal" /> }));
vi.mock("./components/DiscardModal", () => ({ DiscardModal: () => <div data-testid="discard-modal" /> }));
vi.mock("./components/FyiToast", () => ({ FyiToast: () => <div data-testid="fyi-toast" /> }));
vi.mock("./components/DebugDeckViewer", () => ({ DebugDeckViewer: () => <div data-testid="deck-viewer" /> }));
vi.mock("./components/DebugConsole", () => ({ DebugConsole: () => <div data-testid="debug-console" /> }));
vi.mock("./components/Card", () => ({ CardComponent: () => <div data-testid="card" /> }));

function makePlayer(overrides: Partial<PlayerState> = {}): PlayerState {
    return {
        playerId: "p1",
        connectionId: "c1",
        name: "Alice",
        handCount: 0,
        isConnected: true,
        bank: [],
        propertySets: [],
        unboundWilds: [],
        completedSetCount: 0,
        uniqueCompletedSetCount: 0,
        hand: [],
        ...overrides,
    };
}

function makeState(phase: GameState["phase"]): GameState {
    return {
        phase,
        gameCode: "ABCD",
        players: [makePlayer()],
        currentPlayerIndex: 0,
        playsUsed: 0,
        drawPileCount: 10,
        discardPileCount: 0,
        recentActions: [],
        winnerId: phase === "GameOver" ? "p1" : undefined,
        winnerName: phase === "GameOver" ? "Alice" : undefined,
        gameConfig: testGameConfig,
    };
}

describe("GamePage copy log button placement", () => {
    beforeEach(() => {
        pushState = undefined;
    });

    it("does not render Copy log during active gameplay", async () => {
        render(<GamePage gameCode="ABCD" playerName="Alice" playerId="p1" onLeave={vi.fn()} />);
        await waitFor(() => expect(pushState).toBeTruthy());
        act(() => pushState?.(makeState("Play")));
        await waitFor(() => {
            expect(screen.queryByRole("button", { name: "Copy log" })).toBeNull();
        });
    });

    it("renders Copy log on the game-over screen", async () => {
        render(<GamePage gameCode="ABCD" playerName="Alice" playerId="p1" onLeave={vi.fn()} />);
        await waitFor(() => expect(pushState).toBeTruthy());
        act(() => pushState?.(makeState("GameOver")));
        const button = await screen.findByRole("button", { name: "Copy log" });
        expect(button.className).toContain("copyLogButton");
        expect(button.closest(".gameOver")).toBeTruthy();
    });
});
