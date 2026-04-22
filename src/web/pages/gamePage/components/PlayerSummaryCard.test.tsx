import React from "react";
import { render, screen, fireEvent } from "@testing-library/react";
import { PlayerSummaryCard } from "./PlayerSummaryCard";
import { PlayerState } from "../../../Types";

const testPlayer: PlayerState = {
    playerId: "p1",
    connectionId: "c1",
    name: "Alice",
    handCount: 3,
    isConnected: true,
    bank: [
        { id: 10, cardType: "Money", moneyValue: 5, name: "5M", isMulticolorWild: false, isWildRent: false },
        { id: 11, cardType: "Money", moneyValue: 2, name: "2M", isMulticolorWild: false, isWildRent: false },
    ],
    propertySets: [],
    unboundWilds: [],
    completedSetCount: 0,
    uniqueCompletedSetCount: 0,
};

const playerWithSets: PlayerState = {
    ...testPlayer,
    propertySets: [
        {
            setId: 1,
            color: "Brown",
            cards: [
                { id: 20, cardType: "Property", moneyValue: 1, name: "Baltic", color: "Brown", isMulticolorWild: false, isWildRent: false },
            ],
            isComplete: false,
            hasHouse: false,
            hasHotel: false,
            rent: 1,
            requiredSize: 2,
        },
    ],
};

describe("PlayerSummaryCard", () => {
    it("renders player name", () => {
        render(<PlayerSummaryCard player={testPlayer} onClick={() => {}} />);
        expect(screen.getByText("Alice")).toBeTruthy();
    });

    it("shows bank total", () => {
        const { container } = render(<PlayerSummaryCard player={testPlayer} onClick={() => {}} />);
        // Bank total is 5 + 2 = 7
        const moneyEl = container.querySelector(".playerSummary-money");
        expect(moneyEl?.textContent).toContain("7");
    });

    it("shows 'no props' when propertySets is empty", () => {
        render(<PlayerSummaryCard player={testPlayer} onClick={() => {}} />);
        expect(screen.getByText("no props")).toBeTruthy();
    });

    it("shows property set pills when sets exist", () => {
        const { container } = render(<PlayerSummaryCard player={playerWithSets} onClick={() => {}} />);
        const pills = container.querySelectorAll(".playerSummary-setpill");
        expect(pills.length).toBe(1);
        expect(pills[0].textContent).toContain("1/2");
    });

    it("adds active class when isCurrentTurn is true", () => {
        const { container } = render(<PlayerSummaryCard player={testPlayer} isCurrentTurn={true} onClick={() => {}} />);
        expect(container.querySelector(".playerSummary--active")).toBeTruthy();
    });

    it("fires onClick callback when clicked", () => {
        const handleClick = vi.fn();
        const { container } = render(<PlayerSummaryCard player={testPlayer} onClick={handleClick} />);
        fireEvent.click(container.querySelector(".playerSummary")!);
        expect(handleClick).toHaveBeenCalledTimes(1);
    });

    it("has correct aria-label", () => {
        render(<PlayerSummaryCard player={testPlayer} onClick={() => {}} />);
        expect(screen.getByRole("button", { name: /View Alice's board/ })).toBeTruthy();
    });

    it("shows typing dots when isCurrentTurn is true", () => {
        const { container } = render(<PlayerSummaryCard player={testPlayer} isCurrentTurn={true} onClick={() => {}} />);
        expect(container.querySelector(".typing-dots")).toBeTruthy();
    });

    it("does not show typing dots when isCurrentTurn is false", () => {
        const { container } = render(<PlayerSummaryCard player={testPlayer} isCurrentTurn={false} onClick={() => {}} />);
        expect(container.querySelector(".typing-dots")).toBeNull();
    });
});
