import React from "react";
import { render, screen, fireEvent } from "@testing-library/react";
import { DiscardModal } from "./DiscardModal";
import { Card } from "../../../Types";

function makeCard(id: number, name = `Card ${id}`): Card {
    return { id, cardType: "Money", moneyValue: 1, name, isMulticolorWild: false, isWildRent: false };
}

const hand = [makeCard(1), makeCard(2), makeCard(3), makeCard(4), makeCard(5), makeCard(6), makeCard(7), makeCard(8), makeCard(9)];

describe("DiscardModal", () => {
    it("renders correct discard count message", () => {
        render(<DiscardModal hand={hand} maxHandSize={7} onDiscard={vi.fn()} />);
        expect(screen.getByText(/You have 9 cards/)).toBeTruthy();
        expect(screen.getByText(/Select 2 to discard/)).toBeTruthy();
    });

    it("confirm button shows correct excess count", () => {
        render(<DiscardModal hand={hand} maxHandSize={7} onDiscard={vi.fn()} />);
        expect(screen.getByRole("button", { name: /Discard 2 cards/ })).toBeTruthy();
    });

    it("confirm button is disabled until enough cards selected", () => {
        const { container } = render(<DiscardModal hand={hand} maxHandSize={7} onDiscard={vi.fn()} />);
        const btn = screen.getByRole("button", { name: /Discard 2 cards/ });
        expect(btn).toBeDisabled();

        // Select one card
        const cards = container.querySelectorAll(".md-card");
        fireEvent.click(cards[0]);
        expect(btn).toBeDisabled();

        // Select second card
        fireEvent.click(cards[1]);
        expect(btn).not.toBeDisabled();
    });

    it("card selection toggles (click to select/deselect)", () => {
        const { container } = render(<DiscardModal hand={hand} maxHandSize={7} onDiscard={vi.fn()} />);
        const cards = container.querySelectorAll(".md-card");

        fireEvent.click(cards[0]);
        expect(cards[0].classList.contains("md-card--selected")).toBe(true);

        fireEvent.click(cards[0]);
        expect(cards[0].classList.contains("md-card--selected")).toBe(false);
    });

    it("cannot select more cards than excess", () => {
        const { container } = render(<DiscardModal hand={hand} maxHandSize={7} onDiscard={vi.fn()} />);
        const cards = container.querySelectorAll(".md-card");
        // Excess is 2 — select 2 then try a 3rd
        fireEvent.click(cards[0]);
        fireEvent.click(cards[1]);
        fireEvent.click(cards[2]);
        expect(cards[2].classList.contains("md-card--selected")).toBe(false);
    });

    it("confirm button calls onDiscard with selected card IDs", () => {
        const onDiscard = vi.fn();
        const { container } = render(<DiscardModal hand={hand} maxHandSize={7} onDiscard={onDiscard} />);
        const cards = container.querySelectorAll(".md-card");

        fireEvent.click(cards[0]); // id=1
        fireEvent.click(cards[2]); // id=3
        fireEvent.click(screen.getByRole("button", { name: /Discard 2 cards/ }));

        expect(onDiscard).toHaveBeenCalledWith([1, 3]);
    });

    it("Go Back button calls onCancel", () => {
        const onCancel = vi.fn();
        render(<DiscardModal hand={hand} maxHandSize={7} onDiscard={vi.fn()} onCancel={onCancel} />);
        fireEvent.click(screen.getByText(/Go Back/));
        expect(onCancel).toHaveBeenCalledTimes(1);
    });

    it("Go Back button is not rendered when onCancel is not provided", () => {
        render(<DiscardModal hand={hand} maxHandSize={7} onDiscard={vi.fn()} />);
        expect(screen.queryByText(/Go Back/)).toBeNull();
    });

    it("uses singular 'card' when excess is 1", () => {
        const smallHand = hand.slice(0, 8);
        render(<DiscardModal hand={smallHand} maxHandSize={7} onDiscard={vi.fn()} />);
        expect(screen.getByRole("button", { name: /Discard 1 card$/ })).toBeTruthy();
    });

    it("dims unselected cards once enough are selected", () => {
        const { container } = render(<DiscardModal hand={hand} maxHandSize={7} onDiscard={vi.fn()} />);
        const cards = container.querySelectorAll(".md-card");
        fireEvent.click(cards[0]);
        fireEvent.click(cards[1]);
        // Card at index 2 should be dimmed
        expect(cards[2].classList.contains("md-card--dimmed")).toBe(true);
        // Selected cards should not be dimmed
        expect(cards[0].classList.contains("md-card--dimmed")).toBe(false);
    });
});
