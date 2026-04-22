import React from "react";
import { render, screen, fireEvent } from "@testing-library/react";
import { ActionModal } from "./ActionModal";
import { PendingAction, PlayerState, Card } from "../../../Types";

function makeMoneyCard(id: number, value: number): Card {
    return { id, cardType: "Money", moneyValue: value, name: `${value}M`, isMulticolorWild: false, isWildRent: false };
}

function makePropertyCard(id: number, name: string, color: string, value = 2): Card {
    return { id, cardType: "Property", moneyValue: value, name, color: color as any, isMulticolorWild: false, isWildRent: false };
}

function makePlayer(overrides: Partial<PlayerState> = {}): PlayerState {
    return {
        playerId: "me", connectionId: "me-conn", name: "Me", handCount: 3,
        isConnected: true, bank: [], propertySets: [], unboundWilds: [],
        completedSetCount: 0, uniqueCompletedSetCount: 0, ...overrides,
    };
}

function makePayRentAction(amount = 5): PendingAction {
    return {
        type: "PayRent", sourcePlayerId: "other", sourcePlayerName: "Bob",
        targetPlayerIds: ["me"], amount,
    };
}

describe("ActionModal", () => {
    it("renders payment UI for PayRent action", () => {
        const myState = makePlayer({ bank: [makeMoneyCard(1, 5)] });
        render(<ActionModal pendingAction={makePayRentAction()} myState={myState} onRespond={vi.fn()} />);
        expect(screen.getByText(/Bob charges you rent/)).toBeTruthy();
    });

    it("shows payment total and selection count", () => {
        const myState = makePlayer({ bank: [makeMoneyCard(1, 3), makeMoneyCard(2, 2)] });
        const { container } = render(<ActionModal pendingAction={makePayRentAction(5)} myState={myState} onRespond={vi.fn()} />);

        // Bank is sorted by value, so 2M is first, then 3M
        const cards = container.querySelectorAll(".md-card");
        fireEvent.click(cards[0]); // selects 2M card
        const desc = container.querySelector(".modalDescription");
        expect(desc?.textContent).toContain("◆2");
        expect(desc?.textContent).toContain("◆5");
    });

    it("Pay button disabled when not enough selected", () => {
        const myState = makePlayer({ bank: [makeMoneyCard(1, 3), makeMoneyCard(2, 4)] });
        const { container } = render(<ActionModal pendingAction={makePayRentAction(5)} myState={myState} onRespond={vi.fn()} />);

        const payBtn = screen.getByRole("button", { name: /Pay/ });
        expect(payBtn).toBeDisabled();

        // Select one card (3M — not enough)
        const cards = container.querySelectorAll(".md-card");
        fireEvent.click(cards[0]);
        expect(payBtn).toBeDisabled();
    });

    it("Pay button enabled when enough selected", () => {
        const myState = makePlayer({ bank: [makeMoneyCard(1, 3), makeMoneyCard(2, 4)] });
        const { container } = render(<ActionModal pendingAction={makePayRentAction(5)} myState={myState} onRespond={vi.fn()} />);

        const cards = container.querySelectorAll(".md-card");
        fireEvent.click(cards[0]); // 3M
        fireEvent.click(cards[1]); // 4M — total 7 >= 5
        const payBtn = screen.getByRole("button", { name: /Pay ◆7/ });
        expect(payBtn).not.toBeDisabled();
    });

    it("Pay button calls onRespond with selected card IDs", () => {
        const onRespond = vi.fn();
        const myState = makePlayer({ bank: [makeMoneyCard(1, 5)] });
        const { container } = render(<ActionModal pendingAction={makePayRentAction(5)} myState={myState} onRespond={onRespond} />);

        fireEvent.click(container.querySelectorAll(".md-card")[0]);
        fireEvent.click(screen.getByRole("button", { name: /Pay/ }));
        expect(onRespond).toHaveBeenCalledWith({ playJustSayNo: false, paymentCardIds: [1] });
    });

    it("shows Just Say No button when player has JSN card", () => {
        const jsnCard: Card = { id: 99, cardType: "Action", moneyValue: 4, name: "Just Say No", actionKind: "JustSayNo", isMulticolorWild: false, isWildRent: false };
        const myState = makePlayer({ hand: [jsnCard], bank: [makeMoneyCard(1, 5)] });
        render(<ActionModal pendingAction={makePayRentAction(5)} myState={myState} onRespond={vi.fn()} />);
        expect(screen.getByText("Just Say No!")).toBeTruthy();
    });

    it("JSN button calls onRespond with playJustSayNo=true", () => {
        const onRespond = vi.fn();
        const jsnCard: Card = { id: 99, cardType: "Action", moneyValue: 4, name: "Just Say No", actionKind: "JustSayNo", isMulticolorWild: false, isWildRent: false };
        const myState = makePlayer({ hand: [jsnCard], bank: [makeMoneyCard(1, 5)] });
        render(<ActionModal pendingAction={makePayRentAction(5)} myState={myState} onRespond={onRespond} />);
        fireEvent.click(screen.getByText("Just Say No!"));
        expect(onRespond).toHaveBeenCalledWith({ playJustSayNo: true });
    });

    it("displays error message when paymentError prop set", () => {
        const myState = makePlayer({ bank: [makeMoneyCard(1, 5)] });
        render(<ActionModal pendingAction={makePayRentAction(5)} myState={myState} paymentError="Not enough!" onRespond={vi.fn()} />);
        expect(screen.getByText("Not enough!")).toBeTruthy();
    });

    it("insolvent state: shows give everything button", () => {
        const myState = makePlayer({ bank: [makeMoneyCard(1, 2)] });
        render(<ActionModal pendingAction={makePayRentAction(10)} myState={myState} onRespond={vi.fn()} />);
        expect(screen.getByText(/Give Everything/)).toBeTruthy();
    });

    it("renders steal UI for RespondToSlyDeal", () => {
        const action: PendingAction = {
            type: "RespondToSlyDeal", sourcePlayerId: "other", sourcePlayerName: "Bob",
            targetPlayerIds: ["me"], amount: 0, targetCardName: "Boardwalk",
        };
        const myState = makePlayer();
        render(<ActionModal pendingAction={action} myState={myState} onRespond={vi.fn()} />);
        expect(screen.getByText(/Bob plays Sly Deal/)).toBeTruthy();
        expect(screen.getByText("Boardwalk")).toBeTruthy();
    });

    it("renders swap UI for RespondToForceDeal", () => {
        const action: PendingAction = {
            type: "RespondToForceDeal", sourcePlayerId: "other", sourcePlayerName: "Bob",
            targetPlayerIds: ["me"], amount: 0, targetCardName: "Park Place", targetCardId: 20,
            offeredCardName: "Baltic", offeredCardId: 21,
        };
        const targetCard = makePropertyCard(20, "Park Place", "DarkBlue");
        const myState = makePlayer({
            propertySets: [{
                setId: 1, color: "DarkBlue", cards: [targetCard], isComplete: false,
                hasHouse: false, hasHotel: false, rent: 3, requiredSize: 2,
            }],
        });
        render(<ActionModal pendingAction={action} myState={myState} onRespond={vi.fn()} />);
        expect(screen.getByText(/Bob plays Forced Deal/)).toBeTruthy();
    });

    it("nothing to pay shows 'I have nothing' button", () => {
        const myState = makePlayer();
        render(<ActionModal pendingAction={makePayRentAction(5)} myState={myState} onRespond={vi.fn()} />);
        expect(screen.getByText("I have nothing")).toBeTruthy();
    });

    it("PayDebtCollector shows correct title", () => {
        const action: PendingAction = {
            type: "PayDebtCollector", sourcePlayerId: "other", sourcePlayerName: "Carol",
            targetPlayerIds: ["me"], amount: 5,
        };
        const myState = makePlayer({ bank: [makeMoneyCard(1, 5)] });
        render(<ActionModal pendingAction={action} myState={myState} onRespond={vi.fn()} />);
        expect(screen.getByText(/Carol plays Debt Collector/)).toBeTruthy();
    });
});
