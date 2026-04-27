import React from "react";
import { screen, fireEvent, act } from "@testing-library/react";
import { FyiToast } from "./FyiToast";
import { GameAction } from "../../../Types";
import { renderWithConfig as render } from "../../../utilities/test-helpers";

function makeToast(id: number, text = `Action ${id}`, playerName = "Alice"): GameAction {
    return { id, playerName, text };
}

describe("FyiToast", () => {
    beforeEach(() => {
        vi.useFakeTimers();
    });
    afterEach(() => {
        vi.useRealTimers();
    });

    it("renders nothing when no toasts", () => {
        const { container } = render(<FyiToast toasts={[]} />);
        expect(container.querySelector(".fyiToast-container")).toBeNull();
    });

    it("displays toast message text", () => {
        render(<FyiToast toasts={[makeToast(1, "played a card")]} />);
        expect(screen.getByText(/played a card/)).toBeTruthy();
    });

    it("displays player name", () => {
        render(<FyiToast toasts={[makeToast(1, "drew cards", "Bob")]} />);
        expect(screen.getByText("Bob")).toBeTruthy();
    });

    it("auto-dismisses after timeout and sets leaving class", () => {
        const { container } = render(<FyiToast toasts={[makeToast(1)]} />);
        expect(container.querySelector(".fyiToast")).toBeTruthy();

        // Advance past the 3000ms auto-dismiss timer
        act(() => { vi.advanceTimersByTime(3000); });
        // Should now have leaving class
        expect(container.querySelector(".fyiToast--leaving")).toBeTruthy();
    });

    it("close button dismisses current toast", () => {
        const { container } = render(<FyiToast toasts={[makeToast(1)]} />);
        fireEvent.click(screen.getByText("✕"));
        expect(container.querySelector(".fyiToast-container")).toBeNull();
    });

    it("duplicate prevention: same toast ID not shown twice", () => {
        const toast = makeToast(1, "hello");
        const { rerender, container } = render(<FyiToast toasts={[toast]} />);
        expect(screen.getByText(/hello/)).toBeTruthy();

        // Dismiss current
        fireEvent.click(screen.getByText("✕"));
        expect(container.querySelector(".fyiToast-container")).toBeNull();

        // Re-render with same toast — should not re-appear
        rerender(<FyiToast toasts={[toast]} />);
        expect(container.querySelector(".fyiToast-container")).toBeNull();
    });

    it("queue: shows first toast, then next after dismissal", () => {
        const toasts = [makeToast(1, "first"), makeToast(2, "second")];
        const { container } = render(<FyiToast toasts={toasts} />);
        expect(screen.getByText(/first/)).toBeTruthy();

        // Dismiss first
        fireEvent.click(screen.getByText("✕"));
        // Second should now show
        expect(screen.getByText(/second/)).toBeTruthy();
    });

    it("replaces player name with 'you' when myName matches", () => {
        const toast = makeToast(1, "Alice played a card on Alice");
        render(<FyiToast toasts={[toast]} myName="Alice" />);
        expect(screen.getByText(/you played a card on/)).toBeTruthy();
    });

    it("calls onBusyChange(true) when toast appears", () => {
        const onBusyChange = vi.fn();
        render(<FyiToast toasts={[makeToast(1)]} onBusyChange={onBusyChange} />);
        expect(onBusyChange).toHaveBeenCalledWith(true);
    });

    it("calls onBusyChange(false) after toast dismissed and delay", () => {
        const onBusyChange = vi.fn();
        const { container } = render(<FyiToast toasts={[makeToast(1)]} onBusyChange={onBusyChange} />);

        fireEvent.click(screen.getByText("✕"));
        // Not called immediately due to debounce
        expect(onBusyChange).not.toHaveBeenCalledWith(false);

        act(() => { vi.advanceTimersByTime(1500); });
        expect(onBusyChange).toHaveBeenCalledWith(false);
    });

    it("shows card when cardPlayed is present", () => {
        const toast: GameAction = {
            id: 1, playerName: "Alice", text: "played Pass Go",
            cardPlayed: { id: 10, cardType: "Action", moneyValue: 1, name: "Pass Go", actionKind: "PassGo", isMulticolorWild: false, isWildRent: false },
        };
        const { container } = render(<FyiToast toasts={[toast]} />);
        expect(container.querySelector(".fyiToast-cardGroup")).toBeTruthy();
    });
});
