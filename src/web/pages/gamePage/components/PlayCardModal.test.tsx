import React from "react";
import { screen, fireEvent } from "@testing-library/react";
import { PlayCardModal } from "./PlayCardModal";
import { Card, GameState, PlayerState, PropertySetState } from "../../../Types";
import { renderWithConfig as render } from "../../../utilities/test-helpers";

function makeMoneyCard(id = 1, value = 5): Card {
    return { id, cardType: "Money", moneyValue: value, name: `${value}M`, isMulticolorWild: false, isWildRent: false };
}

function makePropertyCard(id = 2, name = "Baltic Avenue", color = "Brown" as any, value = 1): Card {
    return { id, cardType: "Property", moneyValue: value, name, color, isMulticolorWild: false, isWildRent: false };
}

function makeActionCard(id = 3, actionKind = "PassGo" as any, moneyValue = 1, name = "Pass Go"): Card {
    return { id, cardType: "Action", moneyValue, name, actionKind, isMulticolorWild: false, isWildRent: false };
}

function makeRentCard(id = 4, colors = ["Brown", "LightBlue"] as any[], isWildRent = false): Card {
    return { id, cardType: "Rent", moneyValue: 1, name: "Rent", rentColors: colors, isMulticolorWild: false, isWildRent };
}

function makeWildcard(id = 5, color = "Brown" as any, altColor = "LightBlue" as any): Card {
    return { id, cardType: "PropertyWildcard", moneyValue: 0, name: "Wild", color, altColor, isMulticolorWild: false, isWildRent: false };
}

function makeSet(overrides: Partial<PropertySetState> = {}): PropertySetState {
    return {
        setId: 1, color: "Brown", cards: [makePropertyCard()], isComplete: false,
        hasHouse: false, hasHotel: false, rent: 1, requiredSize: 2, ...overrides,
    };
}

function makePlayer(overrides: Partial<PlayerState> = {}): PlayerState {
    return {
        playerId: "me", connectionId: "me-conn", name: "Me", handCount: 3,
        isConnected: true, bank: [], propertySets: [], unboundWilds: [],
        completedSetCount: 0, uniqueCompletedSetCount: 0, ...overrides,
    };
}

function makeGameState(players: PlayerState[], playsUsed = 0): GameState {
    return {
        phase: "Play", gameCode: "ABC", players, currentPlayerIndex: 0,
        playsUsed, drawPileCount: 40, discardPileCount: 0, recentActions: [],
    };
}

const defaultProps = (card: Card, overrides: any = {}) => {
    const myState = overrides.myState ?? makePlayer();
    const others = overrides.otherPlayers ?? [makePlayer({ playerId: "other", connectionId: "other-conn", name: "Bob" })];
    const gameState = makeGameState([myState, ...others], overrides.playsUsed ?? 0);
    return {
        card,
        gameState,
        myState,
        canPlay: overrides.canPlay ?? true,
        phase: overrides.phase ?? "Play",
        onPlay: overrides.onPlay ?? vi.fn(),
        onCancel: overrides.onCancel ?? vi.fn(),
    };
};

describe("PlayCardModal", () => {
    it("money card: shows Bank button and calls onPlay with playAsMoney", () => {
        const onPlay = vi.fn();
        render(<PlayCardModal {...defaultProps(makeMoneyCard(), { onPlay })} />);
        const bankBtn = screen.getByText(/Bank/);
        fireEvent.click(bankBtn);
        expect(onPlay).toHaveBeenCalledWith(1, { playAsMoney: true });
    });

    it("money card: Cancel button calls onCancel", () => {
        const onCancel = vi.fn();
        render(<PlayCardModal {...defaultProps(makeMoneyCard(), { onCancel })} />);
        fireEvent.click(screen.getByText("Cancel"));
        expect(onCancel).toHaveBeenCalledTimes(1);
    });

    it("money card: shows Close instead of Cancel when canPlay is false", () => {
        render(<PlayCardModal {...defaultProps(makeMoneyCard(), { canPlay: false })} />);
        expect(screen.getByText("Close")).toBeTruthy();
        expect(screen.queryByText("Cancel")).toBeNull();
    });

    it("property card: shows Place button and calls onPlay", () => {
        const onPlay = vi.fn();
        const card = makePropertyCard(2, "Baltic Avenue", "Brown");
        render(<PlayCardModal {...defaultProps(card, { onPlay })} />);
        fireEvent.click(screen.getByText(/Place/));
        expect(onPlay).toHaveBeenCalledWith(2, { playAsMoney: false });
    });

    it("wildcard: shows color picker", () => {
        const card = makeWildcard(5, "Brown", "LightBlue");
        render(<PlayCardModal {...defaultProps(card)} />);
        expect(screen.getByText(/Place as which color/)).toBeTruthy();
    });

    it("wildcard: clicking color calls onPlay with wildcardColor", () => {
        const onPlay = vi.fn();
        const card = makeWildcard(5, "Brown", "LightBlue");
        const { container } = render(<PlayCardModal {...defaultProps(card, { onPlay })} />);
        const swatches = container.querySelectorAll(".colorChoice--swatch");
        fireEvent.click(swatches[0]); // Brown
        expect(onPlay).toHaveBeenCalledWith(5, { playAsMoney: false, wildcardColor: "Brown" });
    });

    it("action card: shows Use Action and Bank buttons", () => {
        const card = makeActionCard(3, "PassGo");
        const myState = makePlayer();
        const others = [makePlayer({ playerId: "o", connectionId: "o-conn", name: "Bob" })];
        render(<PlayCardModal {...defaultProps(card, { myState, otherPlayers: others })} />);
        expect(screen.getByText(/Use Action/)).toBeTruthy();
        expect(screen.getByText(/Bank/)).toBeTruthy();
    });

    it("action PassGo: Use Action calls onPlay directly", () => {
        const onPlay = vi.fn();
        const card = makeActionCard(3, "PassGo");
        render(<PlayCardModal {...defaultProps(card, { onPlay })} />);
        fireEvent.click(screen.getByText(/Use Action/));
        expect(onPlay).toHaveBeenCalledWith(3, { playAsMoney: false });
    });

    it("canUseAction: SlyDeal disabled when no stealable properties", () => {
        const card = makeActionCard(10, "SlyDeal", 3, "Sly Deal");
        const otherPlayer = makePlayer({ playerId: "o", connectionId: "o-conn", name: "Bob", propertySets: [] });
        render(<PlayCardModal {...defaultProps(card, { otherPlayers: [otherPlayer] })} />);
        const actionBtn = screen.getByText(/Use Action/);
        expect(actionBtn).toBeDisabled();
    });

    it("canUseAction: SlyDeal enabled when opponent has non-complete set", () => {
        const card = makeActionCard(10, "SlyDeal", 3, "Sly Deal");
        const otherPlayer = makePlayer({
            playerId: "o", connectionId: "o-conn", name: "Bob",
            propertySets: [makeSet({ setId: 2, isComplete: false })],
        });
        render(<PlayCardModal {...defaultProps(card, { otherPlayers: [otherPlayer] })} />);
        expect(screen.getByText(/Use Action/)).not.toBeDisabled();
    });

    it("canUseAction: DealBreaker disabled when no complete sets to steal", () => {
        const card = makeActionCard(11, "DealBreaker", 5, "Deal Breaker");
        const otherPlayer = makePlayer({
            playerId: "o", connectionId: "o-conn", name: "Bob",
            propertySets: [makeSet({ setId: 2, isComplete: false })],
        });
        render(<PlayCardModal {...defaultProps(card, { otherPlayers: [otherPlayer] })} />);
        expect(screen.getByText(/Use Action/)).toBeDisabled();
    });

    it("canUseAction: DealBreaker enabled when opponent has complete set", () => {
        const card = makeActionCard(11, "DealBreaker", 5, "Deal Breaker");
        const otherPlayer = makePlayer({
            playerId: "o", connectionId: "o-conn", name: "Bob",
            propertySets: [makeSet({ setId: 2, isComplete: true, cards: [makePropertyCard(20), makePropertyCard(21)] })],
        });
        render(<PlayCardModal {...defaultProps(card, { otherPlayers: [otherPlayer] })} />);
        expect(screen.getByText(/Use Action/)).not.toBeDisabled();
    });

    it("Escape key calls onCancel", () => {
        const onCancel = vi.fn();
        render(<PlayCardModal {...defaultProps(makeMoneyCard(), { onCancel })} />);
        fireEvent.keyDown(document, { key: "Escape" });
        expect(onCancel).toHaveBeenCalledTimes(1);
    });

    it("rent card: shows Charge Rent button", () => {
        const card = makeRentCard(4, ["Brown", "LightBlue"]);
        const myState = makePlayer({ propertySets: [makeSet()] });
        render(<PlayCardModal {...defaultProps(card, { myState })} />);
        expect(screen.getByText(/Charge Rent/)).toBeTruthy();
    });

    it("rent card: Charge Rent goes to pickRentColor step", () => {
        const card = makeRentCard(4, ["Brown", "LightBlue"]);
        const myState = makePlayer({ propertySets: [makeSet()] });
        render(<PlayCardModal {...defaultProps(card, { myState })} />);
        fireEvent.click(screen.getByText(/Charge Rent/));
        expect(screen.getByText(/Choose color to charge rent for/)).toBeTruthy();
    });

    it("read-only view for non-canPlay wildcard/action/rent shows Close button", () => {
        const card = makeActionCard(3, "PassGo");
        render(<PlayCardModal {...defaultProps(card, { canPlay: false })} />);
        expect(screen.getByText("Close")).toBeTruthy();
        expect(screen.queryByText(/Use Action/)).toBeNull();
    });
});
