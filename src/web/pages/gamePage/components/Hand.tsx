import React, { useState, useRef, useEffect } from "react";
import { Card, GameState, PlayCardRequest } from "../../../Types";
import { CardComponent } from "./Card";
import { PlayCardModal } from "./PlayCardModal";
import "./Hand.css";

interface HandProps {
    cards: Card[];
    canPlay: boolean;
    phase: string;
    gameState: GameState;
    myConnectionId: string;
    onPlayCard: (cardId: number, request: PlayCardRequest) => void;
    onDiscardCard: (cardId: number) => void;
}

export function Hand({ cards, canPlay, phase, gameState, myConnectionId, onPlayCard, onDiscardCard }: HandProps) {
    const [selectedCard, setSelectedCard] = useState<Card | null>(null);
    const [orderedCards, setOrderedCards] = useState<Card[]>(cards);
    const dragIndex = useRef<number | null>(null);
    const dragOverIndex = useRef<number | null>(null);
    const [dragOverIdx, setDragOverIdx] = useState<number | null>(null);

    // Pointer-based drag state for mobile
    const pointerDragging = useRef(false);
    const pointerStartPos = useRef<{ x: number; y: number } | null>(null);
    const dragThreshold = 8; // px before drag starts
    const draggedElement = useRef<HTMLElement | null>(null);
    const dragClone = useRef<HTMLElement | null>(null);

    // Sync orderedCards when server sends new cards (added/removed),
    // but preserve user's ordering for cards that still exist.
    useEffect(() => {
        setOrderedCards((prev) => {
            const serverIds = new Set(cards.map((c) => c.id));
            const prevIds = new Set(prev.map((c) => c.id));

            // Keep existing cards in user's order, update with fresh server data
            const kept = prev
                .filter((c) => serverIds.has(c.id))
                .map((c) => cards.find((sc) => sc.id === c.id)!);

            // Append any new cards from server
            const added = cards.filter((c) => !prevIds.has(c.id));

            return [...kept, ...added];
        });
    }, [cards]);

    const handleCardClick = (card: Card) => {
        if (!canPlay) return;
        if (pointerDragging.current) return; // Suppress click after drag

        if (phase === "Discard") {
            onDiscardCard(card.id);
            return;
        }

        // Money → auto-bank, no modal
        if (card.cardType === "Money") {
            onPlayCard(card.id, { playAsMoney: true });
            return;
        }

        // Property → auto-play as property, no modal
        if (card.cardType === "Property") {
            onPlayCard(card.id, { playAsMoney: false });
            return;
        }

        // Everything else (Action, Rent, PropertyWildcard) → show modal
        setSelectedCard(card);
    };

    // HTML5 drag handlers (desktop)
    const handleDragStart = (idx: number) => {
        dragIndex.current = idx;
    };

    const handleDragOver = (e: React.DragEvent, idx: number) => {
        e.preventDefault();
        dragOverIndex.current = idx;
        setDragOverIdx(idx);
    };

    const handleDrop = () => {
        if (dragIndex.current === null || dragOverIndex.current === null) return;
        if (dragIndex.current === dragOverIndex.current) return;

        setOrderedCards((prev) => {
            const updated = [...prev];
            const [dragged] = updated.splice(dragIndex.current!, 1);
            updated.splice(dragOverIndex.current!, 0, dragged);
            return updated;
        });

        dragIndex.current = null;
        dragOverIndex.current = null;
        setDragOverIdx(null);
    };

    const handleDragEnd = () => {
        dragIndex.current = null;
        dragOverIndex.current = null;
        setDragOverIdx(null);
    };

    // Pointer event handlers (mobile + desktop unified)
    const handlePointerDown = (e: React.PointerEvent, idx: number) => {
        if (e.pointerType === "mouse") return; // Let HTML5 drag handle mouse
        pointerStartPos.current = { x: e.clientX, y: e.clientY };
        dragIndex.current = idx;
        pointerDragging.current = false;
        draggedElement.current = e.currentTarget as HTMLElement;
        (e.currentTarget as HTMLElement).setPointerCapture(e.pointerId);
    };

    const handlePointerMove = (e: React.PointerEvent) => {
        if (e.pointerType === "mouse" || !pointerStartPos.current) return;
        const dx = e.clientX - pointerStartPos.current.x;
        const dy = e.clientY - pointerStartPos.current.y;

        if (!pointerDragging.current && (Math.abs(dx) > dragThreshold || Math.abs(dy) > dragThreshold)) {
            pointerDragging.current = true;
            // Create visual drag clone
            if (draggedElement.current && !dragClone.current) {
                const clone = draggedElement.current.cloneNode(true) as HTMLElement;
                clone.style.position = "fixed";
                clone.style.pointerEvents = "none";
                clone.style.zIndex = "10000";
                clone.style.opacity = "0.8";
                clone.style.transform = "scale(1.05)";
                document.body.appendChild(clone);
                dragClone.current = clone;
                draggedElement.current.style.opacity = "0.3";
            }
        }

        if (pointerDragging.current && dragClone.current) {
            dragClone.current.style.left = `${e.clientX - 50}px`;
            dragClone.current.style.top = `${e.clientY - 70}px`;

            // Find drop target
            if (dragClone.current) dragClone.current.style.display = "none";
            const elem = document.elementFromPoint(e.clientX, e.clientY);
            if (dragClone.current) dragClone.current.style.display = "";

            const wrapper = elem?.closest(".hand-card-wrapper");
            if (wrapper) {
                const idx = Array.from(wrapper.parentElement?.children ?? []).indexOf(wrapper);
                if (idx >= 0) {
                    dragOverIndex.current = idx;
                    setDragOverIdx(idx);
                }
            }
        }
    };

    const handlePointerUp = (e: React.PointerEvent) => {
        if (e.pointerType === "mouse") return;

        // Clean up clone
        if (dragClone.current) {
            document.body.removeChild(dragClone.current);
            dragClone.current = null;
        }
        if (draggedElement.current) {
            draggedElement.current.style.opacity = "";
        }

        if (pointerDragging.current) {
            handleDrop();
            // Prevent the upcoming click
            setTimeout(() => { pointerDragging.current = false; }, 50);
        } else {
            pointerDragging.current = false;
        }

        pointerStartPos.current = null;
        draggedElement.current = null;
        setDragOverIdx(null);
    };

    const myState = gameState.players.find(p => p.connectionId === myConnectionId);

    return (
        <div className="hand">
            <div className="hand-label">Your Hand ({orderedCards.length})</div>
            <div className="hand-cards">
                {orderedCards.map((card, idx) => (
                    <div
                        key={card.id}
                        className={`hand-card-wrapper${dragOverIdx === idx ? " drag-over" : ""}`}
                        style={{ touchAction: "none" }}
                        draggable
                        onDragStart={() => handleDragStart(idx)}
                        onDragOver={(e) => handleDragOver(e, idx)}
                        onDrop={handleDrop}
                        onDragEnd={handleDragEnd}
                        onPointerDown={(e) => handlePointerDown(e, idx)}
                        onPointerMove={handlePointerMove}
                        onPointerUp={handlePointerUp}
                    >
                        <CardComponent
                            card={card}
                            onClick={canPlay ? () => handleCardClick(card) : undefined}
                        />
                    </div>
                ))}
                {orderedCards.length === 0 && <span className="hand-empty">No cards in hand</span>}
            </div>

            {selectedCard && myState && (
                <PlayCardModal
                    card={selectedCard}
                    gameState={gameState}
                    myState={myState}
                    onPlay={(cardId, request) => {
                        onPlayCard(cardId, request);
                        setSelectedCard(null);
                    }}
                    onCancel={() => setSelectedCard(null)}
                />
            )}
        </div>
    );
}
