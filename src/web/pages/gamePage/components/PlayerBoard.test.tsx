import React from "react";
import { render, screen } from "@testing-library/react";
import { PlayerBoard } from "./PlayerBoard";
import { PlayerState, Card, PropertySetState } from "../../../Types";

function makeMoneyCard(id: number, value: number): Card {
    return { id, cardType: "Money", moneyValue: value, name: `${value}M`, isMulticolorWild: false, isWildRent: false };
}

function makePropertyCard(id: number, name: string, color: string, value = 2): Card {
    return { id, cardType: "Property", moneyValue: value, name, color: color as any, isMulticolorWild: false, isWildRent: false };
}

function makeSet(overrides: Partial<PropertySetState> = {}): PropertySetState {
    return {
        setId: 1, color: "Brown", cards: [], isComplete: false,
        hasHouse: false, hasHotel: false, rent: 1, requiredSize: 2, ...overrides,
    };
}

function makePlayer(overrides: Partial<PlayerState> = {}): PlayerState {
    return {
        playerId: "p1", connectionId: "c1", name: "Alice", handCount: 3,
        isConnected: true, bank: [], propertySets: [], unboundWilds: [],
        completedSetCount: 0, uniqueCompletedSetCount: 0, ...overrides,
    };
}

describe("PlayerBoard", () => {
    it("renders player name", () => {
        render(<PlayerBoard player={makePlayer()} />);
        expect(screen.getByText("Alice")).toBeTruthy();
    });

    it("renders bank total", () => {
        const player = makePlayer({ bank: [makeMoneyCard(1, 5), makeMoneyCard(2, 3)] });
        const { container } = render(<PlayerBoard player={player} />);
        const total = container.querySelector(".playerBoard-bank-total");
        expect(total?.textContent).toContain("8");
    });

    it("renders bank pills grouped by denomination", () => {
        const player = makePlayer({ bank: [makeMoneyCard(1, 5), makeMoneyCard(2, 5), makeMoneyCard(3, 1)] });
        const { container } = render(<PlayerBoard player={player} />);
        const pills = container.querySelectorAll(".bank-pill");
        expect(pills.length).toBe(2); // ◆1 and ◆5
        expect(pills[0].textContent).toContain("◆1");
        expect(pills[1].textContent).toContain("◆5 ×2");
    });

    it("empty bank shows Bank Empty", () => {
        const player = makePlayer();
        render(<PlayerBoard player={player} />);
        expect(screen.getByText("Bank Empty")).toBeTruthy();
    });

    it("empty properties shows 'No properties'", () => {
        render(<PlayerBoard player={makePlayer()} />);
        expect(screen.getByText("No properties")).toBeTruthy();
    });

    it("renders property sets with color headers", () => {
        const card = makePropertyCard(10, "Baltic", "Brown");
        const player = makePlayer({
            propertySets: [makeSet({ setId: 1, color: "Brown", cards: [card], requiredSize: 2 })],
        });
        const { container } = render(<PlayerBoard player={player} />);
        const label = container.querySelector(".propertySet-label");
        expect(label?.textContent).toContain("1/2");
    });

    it("shows completed set indicator (checkmark)", () => {
        const cards = [makePropertyCard(10, "Baltic", "Brown"), makePropertyCard(11, "Mediterranean", "Brown")];
        const player = makePlayer({
            propertySets: [makeSet({ setId: 1, color: "Brown", cards, isComplete: true, requiredSize: 2 })],
            completedSetCount: 1,
        });
        const { container } = render(<PlayerBoard player={player} />);
        const label = container.querySelector(".propertySet-label");
        expect(label?.textContent).toContain("✓");
    });

    it("shows house on set that has one", () => {
        const cards = [makePropertyCard(10, "Baltic", "Brown"), makePropertyCard(11, "Mediterranean", "Brown")];
        const player = makePlayer({
            propertySets: [makeSet({ setId: 1, color: "Brown", cards, isComplete: true, hasHouse: true, requiredSize: 2 })],
        });
        const { container } = render(<PlayerBoard player={player} />);
        expect(container.querySelector('img[alt="House"]')).toBeTruthy();
    });

    it("shows hotel on set that has one", () => {
        const cards = [makePropertyCard(10, "Baltic", "Brown"), makePropertyCard(11, "Mediterranean", "Brown")];
        const player = makePlayer({
            propertySets: [makeSet({ setId: 1, color: "Brown", cards, isComplete: true, hasHouse: true, hasHotel: true, requiredSize: 2 })],
        });
        const { container } = render(<PlayerBoard player={player} />);
        expect(container.querySelector('img[alt="Hotel"]')).toBeTruthy();
    });

    it("shows completed set count", () => {
        const player = makePlayer({ completedSetCount: 2 });
        render(<PlayerBoard player={player} />);
        expect(screen.getByText("2/3 sets")).toBeTruthy();
    });

    it("no drag controls on opponent boards (isMe=false)", () => {
        const card = makePropertyCard(10, "Baltic", "Brown");
        const player = makePlayer({
            propertySets: [makeSet({ setId: 1, color: "Brown", cards: [card] })],
        });
        const { container } = render(<PlayerBoard player={player} isMe={false} isMyTurn={true} onFlipCard={vi.fn()} />);
        // No "New Set" drop target on opponent board
        expect(container.querySelector(".propertySet-new")).toBeNull();
    });

    it("shows New Set drop target when isMe and isMyTurn with onMoveProperty", () => {
        const card = makePropertyCard(10, "Baltic", "Brown");
        const player = makePlayer({
            propertySets: [makeSet({ setId: 1, color: "Brown", cards: [card] })],
        });
        const { container } = render(<PlayerBoard player={player} isMe={true} isMyTurn={true} onMoveProperty={vi.fn()} />);
        expect(container.querySelector(".propertySet-new")).toBeTruthy();
    });

    it("shows hand count for opponents, hand length for self", () => {
        const player = makePlayer({ handCount: 5, hand: [makeMoneyCard(1, 1), makeMoneyCard(2, 2)] });
        const { container: opponentContainer } = render(<PlayerBoard player={player} isMe={false} />);
        const opponentCards = opponentContainer.querySelector(".playerBoard-cards");
        expect(opponentCards?.textContent).toContain("5");

        const { container: myContainer } = render(<PlayerBoard player={player} isMe={true} />);
        const myCards = myContainer.querySelector(".playerBoard-cards");
        expect(myCards?.textContent).toContain("2");
    });
});
