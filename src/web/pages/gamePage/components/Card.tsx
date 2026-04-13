import React from "react";
import { Card as CardType } from "../../../Types";
import { PropertyColorMap } from "../../../utilities/PropertyColors";
import "./Card.css";

interface CardProps {
    card: CardType;
    onClick?: () => void;
    selected?: boolean;
    small?: boolean;
}

export function CardComponent({ card, onClick, selected, small }: CardProps) {
    const classNames = [
        "card",
        `card-${card.cardType.toLowerCase()}`,
        small ? "card-small" : "",
        selected ? "card-selected" : "",
        onClick ? "card-clickable" : "",
    ].filter(Boolean).join(" ");

    const bgColor = getCardColor(card);
    const textColor = getCardTextColor(card);

    return (
        <div
            className={classNames}
            onClick={onClick}
            style={{ backgroundColor: bgColor, color: textColor }}
        >
            <div className="card-value">{card.moneyValue > 0 ? `${card.moneyValue}M` : ""}</div>
            <div className="card-name">{getDisplayName(card)}</div>
            {card.cardType === "PropertyWildcard" && !card.isMulticolorWild && (
                <div className="card-alt">
                    {card.color && card.altColor
                        ? `${PropertyColorMap[card.color].name} / ${PropertyColorMap[card.altColor].name}`
                        : ""}
                </div>
            )}
            <div className="card-type">{getTypeLabel(card)}</div>
        </div>
    );
}

function getDisplayName(card: CardType): string {
    if (card.cardType === "Money") return `${card.moneyValue}M`;
    return card.name;
}

function getTypeLabel(card: CardType): string {
    switch (card.cardType) {
        case "Money": return "MONEY";
        case "Property": return "PROPERTY";
        case "PropertyWildcard": return card.isMulticolorWild ? "WILD" : "WILD";
        case "Rent": return card.isWildRent ? "RENT (ANY)" : "RENT";
        case "Action": return "ACTION";
        default: return "";
    }
}

function getCardColor(card: CardType): string {
    switch (card.cardType) {
        case "Money":
            return "#2d6a4f";
        case "Property": {
            const color = card.activeColor ?? card.color;
            return color ? PropertyColorMap[color].hex : "#555";
        }
        case "PropertyWildcard": {
            if (card.isMulticolorWild) return "linear-gradient(135deg, #ff0000, #ff8c00, #ffd700, #228b22, #00008b, #8b008b)";
            const color = card.activeColor ?? card.color;
            return color ? PropertyColorMap[color].hex : "#555";
        }
        case "Rent":
            return "#4a1a6b";
        case "Action":
            return "#b8860b";
        default:
            return "#555";
    }
}

function getCardTextColor(card: CardType): string {
    switch (card.cardType) {
        case "Money": return "#fff";
        case "Property":
        case "PropertyWildcard": {
            const color = card.activeColor ?? card.color;
            return color ? PropertyColorMap[color].textColor : "#fff";
        }
        default: return "#fff";
    }
}
