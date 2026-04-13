import React from "react";
import { PlayerState, Card } from "../../../Types";
import { CardComponent } from "./Card";
import { PropertyColorMap } from "../../../utilities/PropertyColors";
import "./PlayerBoard.css";

// Color for each money denomination
const moneyColors: Record<number, string> = {
    1: "#f0ecc8",
    2: "#a0c8a0",
    3: "#d4e4bc",
    4: "#b8d4e8",
    5: "#8b7bb5",
    10: "#e8c870",
};

function BankDisplay({ bank }: { bank: Card[] }) {
    const total = bank.reduce((sum, c) => sum + c.moneyValue, 0);

    // Group by denomination
    const denomCounts: Record<number, number> = {};
    bank.forEach(c => {
        denomCounts[c.moneyValue] = (denomCounts[c.moneyValue] || 0) + 1;
    });
    const denoms = Object.entries(denomCounts)
        .map(([val, count]) => ({ value: Number(val), count }))
        .sort((a, b) => a.value - b.value);

    return (
        <div className="bank-display">
            <div className="bank-total">💰 M{total}</div>
            <div className="bank-denoms">
                {denoms.map(d => (
                    <span
                        key={d.value}
                        className="bank-pill"
                        style={{
                            backgroundColor: moneyColors[d.value] || "#4caf50",
                            color: d.value === 5 ? "#fff" : "#333",
                        }}
                        title={`${d.count}x M${d.value}`}
                    >
                        M{d.value} ×{d.count}
                    </span>
                ))}
                {denoms.length === 0 && <span className="emptyHint">Empty</span>}
            </div>
        </div>
    );
}

interface PlayerBoardProps {
    player: PlayerState;
    isMe?: boolean;
    isMyTurn?: boolean;
    onFlipCard?: (cardId: number) => void;
    onMoveProperty?: (cardId: number, targetSetId: number, targetColor: string | null) => void;
}

export function PlayerBoard({ player, isMe, isMyTurn, onFlipCard, onMoveProperty }: PlayerBoardProps) {
    const canDrag = isMe && isMyTurn && !!onMoveProperty;

    const handleDragStart = (e: React.DragEvent, cardId: number) => {
        e.dataTransfer.setData("cardId", String(cardId));
        e.dataTransfer.effectAllowed = "move";
    };

    const handleDropOnSet = (e: React.DragEvent, setId: number, color: string) => {
        e.preventDefault();
        const cardId = Number(e.dataTransfer.getData("cardId"));
        if (cardId && onMoveProperty) {
            onMoveProperty(cardId, setId, color);
        }
    };

    const handleDropNewSet = (e: React.DragEvent) => {
        e.preventDefault();
        const cardId = Number(e.dataTransfer.getData("cardId"));
        if (cardId && onMoveProperty) {
            // targetSetId=0 means "new set", color will be determined by card
            // For multi-color wilds going to new set, we'd need a color picker
            // For now, pass null and let the server figure it out from the card
            const draggedCard = findCard(cardId);
            const color = draggedCard?.activeColor ?? draggedCard?.color ?? null;
            onMoveProperty(cardId, 0, color ?? null);
        }
    };

    const handleDropUnbound = (e: React.DragEvent) => {
        e.preventDefault();
        const cardId = Number(e.dataTransfer.getData("cardId"));
        if (cardId && onMoveProperty) {
            onMoveProperty(cardId, -1, null);
        }
    };

    const handleDragOver = (e: React.DragEvent) => {
        e.preventDefault();
        e.dataTransfer.dropEffect = "move";
    };

    const findCard = (cardId: number): Card | undefined => {
        for (const set of player.propertySets) {
            const c = set.cards.find(c => c.id === cardId);
            if (c) return c;
        }
        return player.unboundWilds?.find(c => c.id === cardId);
    };

    return (
        <div className={`playerBoard ${isMe ? "playerBoard-me" : ""}`}>
            <div className="playerBoard-header">
                <span className="playerBoard-name">{player.name}{isMe ? " (You)" : ""}</span>
                <span className="playerBoard-cards">🃏 {player.handCount}</span>
                <span className="playerBoard-sets">
                    {player.completedSetCount}/3 sets
                </span>
            </div>

            <div className="playerBoard-sections">
                {/* Bank */}
                <div className="playerBoard-bank">
                    <div className="section-label">Bank</div>
                    <BankDisplay bank={player.bank} />
                </div>

                {/* Properties */}
                <div className="playerBoard-properties">
                    <div className="section-label">Properties</div>
                    <div className="propertySets-row">
                        {player.propertySets.map((set) => (
                            <div
                                key={set.setId}
                                className={`propertySet-column ${canDrag ? "propertySet-column--droppable" : ""}`}
                                onDragOver={canDrag ? handleDragOver : undefined}
                                onDrop={canDrag ? (e) => handleDropOnSet(e, set.setId, set.color) : undefined}
                            >
                                <div
                                    className="propertySet-label"
                                    style={{ backgroundColor: PropertyColorMap[set.color].hex, color: PropertyColorMap[set.color].textColor }}
                                >
                                    {PropertyColorMap[set.color].name}
                                    {" "}({set.cards.length}/{set.requiredSize})
                                    {set.isComplete && " ✓"}
                                    {set.hasHouse && " 🏠"}
                                    {set.hasHotel && " 🏨"}
                                    {" — M" + set.rent}
                                </div>
                                <div className="propertySet-stack">
                                    {set.cards.map((card, idx) => (
                                        <div
                                            key={card.id}
                                            className="propertySet-stack-item"
                                            style={{ marginTop: idx === 0 ? 0 : -100 }}
                                            draggable={canDrag}
                                            onDragStart={canDrag ? (e) => handleDragStart(e, card.id) : undefined}
                                        >
                                            <CardComponent
                                                card={card}
                                                small
                                                onDoubleClick={
                                                    isMe && card.cardType === "PropertyWildcard" && !card.isMulticolorWild && onFlipCard
                                                        ? () => onFlipCard(card.id)
                                                        : undefined
                                                }
                                            />
                                        </div>
                                    ))}
                                </div>
                            </div>
                        ))}

                        {/* "New Set" drop target */}
                        {canDrag && (
                            <div
                                className="propertySet-column propertySet-new"
                                onDragOver={handleDragOver}
                                onDrop={handleDropNewSet}
                            >
                                <div className="propertySet-new-label">+ New Set</div>
                            </div>
                        )}

                        {player.propertySets.length === 0 && !canDrag && <span className="emptyHint">No properties</span>}
                    </div>

                    {/* Unbound wilds */}
                    {isMe && (player.unboundWilds?.length > 0 || canDrag) && (
                        <div
                            className={`unboundWilds ${canDrag ? "unboundWilds--droppable" : ""}`}
                            onDragOver={canDrag ? handleDragOver : undefined}
                            onDrop={canDrag ? handleDropUnbound : undefined}
                        >
                            <div className="section-label">Unassigned Wilds</div>
                            <div className="propertySets-row">
                                {(player.unboundWilds ?? []).map((card) => (
                                    <div
                                        key={card.id}
                                        draggable={canDrag}
                                        onDragStart={canDrag ? (e) => handleDragStart(e, card.id) : undefined}
                                    >
                                        <CardComponent card={card} small />
                                    </div>
                                ))}
                            </div>
                        </div>
                    )}
                </div>
            </div>
        </div>
    );
}
