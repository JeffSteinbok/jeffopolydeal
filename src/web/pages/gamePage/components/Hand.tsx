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

    const myState = gameState.players.find(p => p.connectionId === myConnectionId);

    return (
        <div className="hand">
            <div className="hand-label">Your Hand ({orderedCards.length})</div>
            <div className="hand-cards">
                {orderedCards.map((card, idx) => (
                    <div
                        key={card.id}
                        className={`hand-card-wrapper${dragOverIdx === idx ? " drag-over" : ""}`}
                        draggable
                        onDragStart={() => handleDragStart(idx)}
                        onDragOver={(e) => handleDragOver(e, idx)}
                        onDrop={handleDrop}
                        onDragEnd={handleDragEnd}
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
