import React from "react";
import { CardComponent } from "./Card";
import { Card } from "../../../Types";
import { renderWithConfig } from "../../../utilities/test-helpers";

const moneyCard: Card = { id: 1, cardType: "Money", moneyValue: 5, name: "5M", isMulticolorWild: false, isWildRent: false };
const propertyCard: Card = { id: 2, cardType: "Property", moneyValue: 3, name: "Baltic Avenue", color: "Brown", isMulticolorWild: false, isWildRent: false };
const actionCard: Card = { id: 3, cardType: "Action", moneyValue: 1, name: "Pass Go", actionKind: "PassGo", isMulticolorWild: false, isWildRent: false };
const rentCard: Card = { id: 4, cardType: "Rent", moneyValue: 1, name: "Rent", rentColors: ["Brown", "LightBlue"], isMulticolorWild: false, isWildRent: false };
const wildcardCard: Card = { id: 5, cardType: "PropertyWildcard", moneyValue: 0, name: "Wild", isMulticolorWild: true, isWildRent: false };

describe("CardComponent", () => {
    it("renders without crashing for Money card", () => {
        const { container } = renderWithConfig(<CardComponent card={moneyCard} />);
        expect(container.querySelector(".md-card")).toBeTruthy();
    });

    it("renders without crashing for Property card", () => {
        const { container } = renderWithConfig(<CardComponent card={propertyCard} />);
        expect(container.querySelector(".md-card")).toBeTruthy();
    });

    it("renders without crashing for Action card", () => {
        const { container } = renderWithConfig(<CardComponent card={actionCard} />);
        expect(container.querySelector(".md-card")).toBeTruthy();
    });

    it("renders without crashing for Rent card", () => {
        const { container } = renderWithConfig(<CardComponent card={rentCard} />);
        expect(container.querySelector(".md-card")).toBeTruthy();
    });

    it("renders without crashing for PropertyWildcard card", () => {
        const { container } = renderWithConfig(<CardComponent card={wildcardCard} />);
        expect(container.querySelector(".md-card")).toBeTruthy();
    });

    it("money card renders with diamond symbol and amount", () => {
        const { container } = renderWithConfig(<CardComponent card={moneyCard} />);
        expect(container.textContent).toContain("◆");
        expect(container.textContent).toContain("5");
    });

    it("property card renders the property name", () => {
        const { container } = renderWithConfig(<CardComponent card={propertyCard} />);
        expect(container.textContent).toContain("Baltic Avenue");
    });

    it("action card renders the action description", () => {
        const { container } = renderWithConfig(<CardComponent card={actionCard} />);
        expect(container.textContent).toContain("Draw 2 extra cards");
    });

    it("has clickable class when onClick is provided", () => {
        const { container } = renderWithConfig(<CardComponent card={moneyCard} onClick={() => {}} />);
        expect(container.querySelector(".md-card--clickable")).toBeTruthy();
    });

    it("has selected class when selected=true", () => {
        const { container } = renderWithConfig(<CardComponent card={moneyCard} selected={true} />);
        expect(container.querySelector(".md-card--selected")).toBeTruthy();
    });

    it("has dimmed class when dimmed=true", () => {
        const { container } = renderWithConfig(<CardComponent card={moneyCard} dimmed={true} />);
        expect(container.querySelector(".md-card--dimmed")).toBeTruthy();
    });
});
