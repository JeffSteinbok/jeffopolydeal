import React from "react";
import { PlayerState, Card, PropertySetState } from "../../../Types";
import { CardComponent } from "./Card";
import { PropertyColorMap } from "../../../utilities/PropertyColors";
import IndicatorSvg from "../../../assets/Indicator.svg";
import HousePng from "../../../assets/HouseSmall.png";
import HotelPng from "../../../assets/HotelSmall.png";
import "./PlayerBoard.css";

// Color for each money denomination
const moneyColors: Record<number, string> = {
    1: "#f0ecc8",
    2: "#e8c8b0",
    3: "#d4e4bc",
    4: "#b8d4e8",
    5: "#8b7bb5",
    10: "#e8c870",
};

function BankDisplay({ bank }: { bank: Card[] }) {
    const bankTotal = bank.reduce((sum, c) => sum + c.moneyValue, 0);

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
            <div className="bank-denoms">
                {denoms.map(d => (
                    <span
                        key={d.value}
                        className="bank-pill"
                        style={{
                            backgroundColor: moneyColors[d.value] || "#4caf50",
                            color: d.value === 5 ? "#fff" : "#333",
                        }}
                        title={`${d.count}x ◆${d.value}`}
                    >
                        ◆{d.value} ×{d.count}
                    </span>
                ))}
                {denoms.length === 0 && <span className="emptyHint">Bank Empty</span>}
            </div>
        </div>
    );
}

interface PlayerBoardProps {
    player: PlayerState;
    isMe?: boolean;
    isMyTurn?: boolean;
    isCurrentTurn?: boolean;
    compact?: boolean;
    inspectMode?: boolean;
    onFlipCard?: (cardId: number) => void;
    onMoveProperty?: (cardId: number, targetSetId: number, targetColor: string | null) => void;
}

export function PlayerBoard({ player, isMe, isMyTurn, isCurrentTurn, compact, inspectMode, onFlipCard, onMoveProperty }: PlayerBoardProps) {
    const canDrag = isMe && isMyTurn && !!onMoveProperty;
    const [expandedSet, setExpandedSet] = React.useState<PropertySetState | null>(null);

    // ESC to close expanded set
    React.useEffect(() => {
        if (!expandedSet) return;
        const handler = (e: KeyboardEvent) => {
            if (e.key === "Escape") setExpandedSet(null);
        };
        window.addEventListener("keydown", handler);
        return () => window.removeEventListener("keydown", handler);
    }, [expandedSet]);
    const [dragOverTarget, setDragOverTarget] = React.useState<string | null>(null);

    // Pointer drag state for mobile
    const pointerDragCardId = React.useRef<number | null>(null);
    const pointerStartPos = React.useRef<{ x: number; y: number } | null>(null);
    const pointerOffset = React.useRef<{ x: number; y: number }>({ x: 0, y: 0 });
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
        setDragOverTarget(null);
        const cardId = Number(e.dataTransfer.getData("cardId"));
        if (cardId && onMoveProperty) {
            onMoveProperty(cardId, setId, color);
        }
    };

    const handleDropNewSet = (e: React.DragEvent) => {
        e.preventDefault();
        setDragOverTarget(null);
        const cardId = Number(e.dataTransfer.getData("cardId"));
        if (cardId && onMoveProperty) {
            const draggedCard = findCard(cardId);
            const color = draggedCard?.activeColor ?? draggedCard?.color ?? null;
            onMoveProperty(cardId, 0, color ?? null);
        }
    };

    const handleDropUnbound = (e: React.DragEvent) => {
        e.preventDefault();
        setDragOverTarget(null);
        const cardId = Number(e.dataTransfer.getData("cardId"));
        if (cardId && onMoveProperty) {
            onMoveProperty(cardId, -1, null);
        }
    };

    const handleDragOver = (e: React.DragEvent) => {
        e.preventDefault();
        e.dataTransfer.dropEffect = "move";
    };

    const handleDragEnter = (e: React.DragEvent, target: string) => {
        e.preventDefault();
        setDragOverTarget(target);
    };

    const handleDragLeave = (e: React.DragEvent) => {
        if (!e.currentTarget.contains(e.relatedTarget as Node)) {
            setDragOverTarget(null);
        }
    };

    // Pointer events for mobile property drag
    const handlePropertyPointerDown = (e: React.PointerEvent, cardId: number) => {
        if (e.pointerType === "mouse" || !canDrag) return;
        pointerDragCardId.current = cardId;
        pointerStartPos.current = { x: e.clientX, y: e.clientY };
        const rect = (e.currentTarget as HTMLElement).getBoundingClientRect();
        pointerOffset.current = { x: e.clientX - rect.left, y: e.clientY - rect.top };
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
            dragClone.current.style.left = `${e.clientX - pointerOffset.current.x}px`;
            dragClone.current.style.top = `${e.clientY - pointerOffset.current.y}px`;

            // Highlight drop target under pointer
            const elem = document.elementFromPoint(e.clientX, e.clientY);
            const setCol = elem?.closest(".propertySet-column");
            const newSetEl = elem?.closest(".propertySet-new");
            const unboundEl = elem?.closest(".propertySet-unbound");
            if (unboundEl) {
                setDragOverTarget("unbound");
            } else if (newSetEl) {
                setDragOverTarget("new");
            } else if (setCol) {
                const setId = setCol.getAttribute("data-set-id");
                setDragOverTarget(setId ? `set-${setId}` : null);
            } else {
                setDragOverTarget(null);
            }
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
            const unboundEl = elem?.closest(".propertySet-unbound");

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
        <div className={`playerBoard ${isMe ? "playerBoard-me" : "playerBoard--opponent"}${isCurrentTurn ? " playerBoard--currentTurn" : ""}`}>
            <div className="playerBoard-header">
                <span className="playerBoard-name">{isCurrentTurn && <span className="pulsing-dot" />}{player.name}{isCurrentTurn && <span className="typing-dots"><span>.</span><span>.</span><span>.</span></span>}</span>
                <span className="playerBoard-bank-total"><span className="money-diamond">◆</span>{player.bank.reduce((s, c) => s + c.moneyValue, 0)}</span>
                <span className="playerBoard-cards"><img src={IndicatorSvg} alt="cards" className="playerBoard-cardIcon" /> {isMe ? (player.hand?.length ?? 0) : player.handCount}</span>
            </div>

            <div className="playerBoard-sections">
                {/* Bank */}
                <div className="playerBoard-bank">
                    <BankDisplay bank={player.bank} />
                </div>

                {/* Properties */}
                <div className="playerBoard-properties">
                    <div className="section-label-row">
                        <span className="section-label">Properties</span>
                        <span className="playerBoard-sets">{player.completedSetCount}/3 sets</span>
                    </div>
                    <div className={`propertySets-row${inspectMode ? " propertySets-row--inspect" : ""}${compact ? " propertySets-row--compact" : ""}`}>
                        {player.propertySets.map((set) => (
                            <div
                                key={set.setId}
                                className={`propertySet-column ${canDrag ? "propertySet-column--droppable" : ""} ${dragOverTarget === `set-${set.setId}` ? "propertySet-column--drag-over" : ""}`}
                                data-set-id={set.setId}
                                data-set-color={set.color}
                                onDragOver={canDrag ? handleDragOver : undefined}
                                onDragEnter={canDrag ? (e) => handleDragEnter(e, `set-${set.setId}`) : undefined}
                                onDragLeave={canDrag ? handleDragLeave : undefined}
                                onDrop={canDrag ? (e) => handleDropOnSet(e, set.setId, set.color) : undefined}
                            >
                                <div
                                    className="propertySet-label"
                                    style={{ backgroundColor: PropertyColorMap[set.color].hex, color: PropertyColorMap[set.color].textColor }}
                                >
                                    {`${set.cards.length}/${set.requiredSize}${set.isComplete ? "✓" : ""}`}
                                    {set.hasHotel ? <img src={HotelPng} alt="Hotel" className="building-icon" /> : set.hasHouse ? <img src={HousePng} alt="House" className="building-icon" /> : null}
                                </div>
                                <div className="propertySet-stack">
                                    {set.cards.map((card, idx) => {
                                        const canFlipCard = !compact && isMe && card.cardType === "PropertyWildcard" && !card.isMulticolorWild && !!onFlipCard;
                                        return (
                                        <div
                                            key={card.id}
                                            className="propertySet-stack-item"
                                            style={{ marginTop: (idx > 0) ? (compact ? -65 : -100) : 0, touchAction: canDrag ? "none" : compact ? "manipulation" : "auto" }}
                                            draggable={canDrag}
                                            onDragStart={canDrag ? (e) => handleDragStart(e, card.id) : undefined}
                                            onPointerDown={canDrag ? (e) => handlePropertyPointerDown(e, card.id) : undefined}
                                            onPointerMove={canDrag ? handlePropertyPointerMove : undefined}
                                            onPointerUp={canDrag ? handlePropertyPointerUp : undefined}
                                            onClick={() => setExpandedSet(set)}
                                        >
                                            <CardComponent
                                                card={card}
                                                small={!compact}
                                                compact={compact}
                                                currentRent={set.rent}
                                                onDoubleClick={canFlipCard ? () => onFlipCard!(card.id) : undefined}
                                            />
                                        </div>
                                        );
                                    })}
                                </div>
                            </div>
                        ))}

                        {/* Unassigned wilds as a standard stack */}
                        {(isMe || inspectMode) && player.unboundWilds?.length > 0 && (
                            <div
                                className={`propertySet-column propertySet-unbound ${canDrag ? "propertySet-column--droppable" : ""} ${dragOverTarget === "unbound" ? "propertySet-column--drag-over" : ""}`}
                                onDragOver={canDrag ? handleDragOver : undefined}
                                onDragEnter={canDrag ? (e) => handleDragEnter(e, "unbound") : undefined}
                                onDragLeave={canDrag ? handleDragLeave : undefined}
                                onDrop={canDrag ? handleDropUnbound : undefined}
                            >
                                <div className="propertySet-label propertySet-label--unbound">Unassigned</div>
                                <div className="propertySet-stack">
                                    {(player.unboundWilds ?? []).map((card, idx) => (
                                        <div
                                            key={card.id}
                                            className="propertySet-stack-item"
                                            style={{ marginTop: (idx > 0) ? (compact ? -65 : -100) : 0, touchAction: canDrag ? "none" : compact ? "manipulation" : "auto" }}
                                            draggable={canDrag}
                                            onDragStart={canDrag ? (e) => handleDragStart(e, card.id) : undefined}
                                            onPointerDown={canDrag ? (e) => handlePropertyPointerDown(e, card.id) : undefined}
                                            onPointerMove={canDrag ? handlePropertyPointerMove : undefined}
                                            onPointerUp={canDrag ? handlePropertyPointerUp : undefined}
                                        >
                                            <CardComponent card={card} small={!compact} compact={compact} currentRent={compact ? 0 : undefined} />
                                        </div>
                                    ))}
                                </div>
                            </div>
                        )}

                        {/* "New Set" drop target */}
                        {canDrag && (
                            <div
                                className={`propertySet-column propertySet-new ${dragOverTarget === "new" ? "propertySet-new--drag-over" : ""}`}
                                onDragOver={handleDragOver}
                                onDragEnter={(e) => handleDragEnter(e, "new")}
                                onDragLeave={handleDragLeave}
                                onDrop={handleDropNewSet}
                            >
                                <div className="propertySet-new-label">New Set</div>
                            </div>
                        )}

                        {player.propertySets.length === 0 && !canDrag && <span className="emptyHint">No properties</span>}
                    </div>
                </div>
            </div>

            {/* Card expand overlay */}
            {expandedSet && (
                <div className="cardExpand-overlay" onClick={() => setExpandedSet(null)}>
                    <div className="cardExpand-card cardExpand-set" onClick={(e) => e.stopPropagation()}>
                        <div
                            className="cardExpand-set-header"
                            style={{ backgroundColor: PropertyColorMap[expandedSet.color].hex, color: PropertyColorMap[expandedSet.color].textColor }}
                        >
                            {`${expandedSet.cards.length}/${expandedSet.requiredSize}`}
                            {expandedSet.isComplete && " ✓"}
                            {expandedSet.hasHotel ? <img src={HotelPng} alt="Hotel" className="building-icon" /> : expandedSet.hasHouse ? <img src={HousePng} alt="House" className="building-icon" /> : null}
                        </div>
                        <div className="cardExpand-set-cards">
                            {expandedSet.cards.map((card) => (
                                <div key={card.id} className="cardExpand-set-item">
                                    <CardComponent card={card} small />
                                    {isMe && card.cardType === "PropertyWildcard" && !card.isMulticolorWild && onFlipCard && (
                                        <button
                                            className="secondary"
                                            style={{ marginTop: 4, fontSize: "0.7rem", padding: "4px 8px" }}
                                            onClick={() => { onFlipCard(card.id); setExpandedSet(null); }}
                                        >
                                            Flip
                                        </button>
                                    )}
                                </div>
                            ))}
                        </div>
                        <button
                            className="secondary"
                            style={{ marginTop: 8, fontSize: "0.8rem", padding: "6px 12px" }}
                            onClick={() => setExpandedSet(null)}
                        >
                            Close
                        </button>
                    </div>
                </div>
            )}
        </div>
    );
}
