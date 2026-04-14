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
    compact?: boolean;
    inspectMode?: boolean;
    onFlipCard?: (cardId: number) => void;
    onMoveProperty?: (cardId: number, targetSetId: number, targetColor: string | null) => void;
}

export function PlayerBoard({ player, isMe, isMyTurn, compact, inspectMode, onFlipCard, onMoveProperty }: PlayerBoardProps) {
    const canDrag = isMe && isMyTurn && !!onMoveProperty;
    const [expandedCard, setExpandedCard] = React.useState<Card | null>(null);

    // Pointer drag state for mobile
    const pointerDragCardId = React.useRef<number | null>(null);
    const pointerStartPos = React.useRef<{ x: number; y: number } | null>(null);
    const pointerDragging = React.useRef(false);
    const dragClone = React.useRef<HTMLElement | null>(null);
    const draggedElement = React.useRef<HTMLElement | null>(null);
    const dragThreshold = 8;

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

    // Pointer events for mobile property drag
    const handlePropertyPointerDown = (e: React.PointerEvent, cardId: number) => {
        if (e.pointerType === "mouse" || !canDrag) return;
        pointerDragCardId.current = cardId;
        pointerStartPos.current = { x: e.clientX, y: e.clientY };
        pointerDragging.current = false;
        draggedElement.current = e.currentTarget as HTMLElement;
        (e.currentTarget as HTMLElement).setPointerCapture(e.pointerId);
    };

    const handlePropertyPointerMove = (e: React.PointerEvent) => {
        if (e.pointerType === "mouse" || !pointerStartPos.current || !pointerDragCardId.current) return;
        const dx = e.clientX - pointerStartPos.current.x;
        const dy = e.clientY - pointerStartPos.current.y;

        if (!pointerDragging.current && (Math.abs(dx) > dragThreshold || Math.abs(dy) > dragThreshold)) {
            pointerDragging.current = true;
            if (draggedElement.current && !dragClone.current) {
                const clone = draggedElement.current.cloneNode(true) as HTMLElement;
                clone.style.position = "fixed";
                clone.style.pointerEvents = "none";
                clone.style.zIndex = "10000";
                clone.style.opacity = "0.8";
                document.body.appendChild(clone);
                dragClone.current = clone;
                draggedElement.current.style.opacity = "0.3";
            }
        }

        if (pointerDragging.current && dragClone.current) {
            dragClone.current.style.left = `${e.clientX - 40}px`;
            dragClone.current.style.top = `${e.clientY - 55}px`;
        }
    };

    const handlePropertyPointerUp = (e: React.PointerEvent) => {
        if (e.pointerType === "mouse") return;

        const cardId = pointerDragCardId.current;
        if (dragClone.current) {
            document.body.removeChild(dragClone.current);
            dragClone.current = null;
        }
        if (draggedElement.current) {
            draggedElement.current.style.opacity = "";
        }

        if (pointerDragging.current && cardId && onMoveProperty) {
            // Find drop target under pointer
            const elem = document.elementFromPoint(e.clientX, e.clientY);
            const setCol = elem?.closest(".propertySet-column");
            const newSetEl = elem?.closest(".propertySet-new");
            const unboundEl = elem?.closest(".unboundWilds");

            if (newSetEl) {
                const draggedCard = findCard(cardId);
                const color = draggedCard?.activeColor ?? draggedCard?.color ?? null;
                onMoveProperty(cardId, 0, color ?? null);
            } else if (unboundEl) {
                onMoveProperty(cardId, -1, null);
            } else if (setCol) {
                const setId = Number(setCol.getAttribute("data-set-id"));
                const color = setCol.getAttribute("data-set-color");
                if (setId && color) {
                    onMoveProperty(cardId, setId, color);
                }
            }
        }

        pointerDragCardId.current = null;
        pointerStartPos.current = null;
        pointerDragging.current = false;
        draggedElement.current = null;
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
                    <div className={`propertySets-row${inspectMode ? " propertySets-row--inspect" : ""}${compact ? " propertySets-row--compact" : ""}`}>
                        {player.propertySets.map((set) => (
                            <div
                                key={set.setId}
                                className={`propertySet-column ${canDrag ? "propertySet-column--droppable" : ""}`}
                                data-set-id={set.setId}
                                data-set-color={set.color}
                                onDragOver={canDrag ? handleDragOver : undefined}
                                onDrop={canDrag ? (e) => handleDropOnSet(e, set.setId, set.color) : undefined}
                            >
                                <div
                                    className="propertySet-label"
                                    style={{ backgroundColor: PropertyColorMap[set.color].hex, color: PropertyColorMap[set.color].textColor }}
                                >
                                    {`${PropertyColorMap[set.color].short} ${set.cards.length}/${set.requiredSize}${set.isComplete ? "✓" : ""}${set.hasHotel ? "🏨" : set.hasHouse ? "🏠" : ""} M${set.rent}`}
                                </div>
                                <div className={inspectMode ? "propertySet-stack--inspect" : "propertySet-stack"}>
                                    {set.cards.map((card, idx) => {
                                        const canFlipCard = !compact && isMe && card.cardType === "PropertyWildcard" && !card.isMulticolorWild && !!onFlipCard;
                                        return (
                                        <div
                                            key={card.id}
                                            className="propertySet-stack-item"
                                            style={{ marginTop: (!inspectMode && idx > 0) ? (compact ? -65 : -100) : 0, touchAction: canDrag ? "none" : compact ? "manipulation" : "auto" }}
                                            draggable={canDrag}
                                            onDragStart={canDrag ? (e) => handleDragStart(e, card.id) : undefined}
                                            onPointerDown={canDrag ? (e) => handlePropertyPointerDown(e, card.id) : undefined}
                                            onPointerMove={canDrag ? handlePropertyPointerMove : undefined}
                                            onPointerUp={canDrag ? handlePropertyPointerUp : undefined}
                                            onDoubleClick={compact ? () => setExpandedCard(card) : undefined}
                                        >
                                            <CardComponent
                                                card={card}
                                                small={!compact}
                                                tiny={compact}
                                                currentRent={compact ? set.rent : undefined}
                                                onDoubleClick={canFlipCard ? () => onFlipCard!(card.id) : undefined}
                                            />
                                        </div>
                                        );
                                    })}
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
                                        style={{ touchAction: canDrag ? "none" : "auto" }}
                                        draggable={canDrag}
                                        onDragStart={canDrag ? (e) => handleDragStart(e, card.id) : undefined}
                                        onPointerDown={canDrag ? (e) => handlePropertyPointerDown(e, card.id) : undefined}
                                        onPointerMove={canDrag ? handlePropertyPointerMove : undefined}
                                        onPointerUp={canDrag ? handlePropertyPointerUp : undefined}
                                    >
                                        <CardComponent card={card} small />
                                    </div>
                                ))}
                            </div>
                        </div>
                    )}
                </div>
            </div>

            {/* Card expand overlay for mobile/compact mode */}
            {expandedCard && (
                <div className="cardExpand-overlay" onClick={() => setExpandedCard(null)}>
                    <div className="cardExpand-card" onClick={(e) => e.stopPropagation()}>
                        <CardComponent card={expandedCard} />
                        {isMe && expandedCard.cardType === "PropertyWildcard" && !expandedCard.isMulticolorWild && onFlipCard && (
                            <button
                                className="secondary"
                                style={{ marginTop: 8, fontSize: "0.8rem", padding: "6px 12px" }}
                                onClick={() => { onFlipCard(expandedCard.id); setExpandedCard(null); }}
                            >
                                Flip
                            </button>
                        )}
                        <button
                            className="secondary"
                            style={{ marginTop: 8, fontSize: "0.8rem", padding: "6px 12px" }}
                            onClick={() => setExpandedCard(null)}
                        >
                            Close
                        </button>
                    </div>
                </div>
            )}
        </div>
    );
}
