import React from "react";
import { Card, GameState, PlayCardRequest } from "../../../Types";
import { CardComponent } from "./Card";
import "./Hand.css";

interface HandProps {
    cards: Card[];
    canPlay: boolean;
    phase: string;
    gameState: GameState;
    onPlayCard: (cardId: number, request: PlayCardRequest) => void;
    onDiscardCard: (cardId: number) => void;
}

export function Hand({ cards, canPlay, phase, gameState, onPlayCard, onDiscardCard }: HandProps) {
    const handleCardClick = (card: Card) => {
        if (!canPlay) return;

        if (phase === "Discard") {
            onDiscardCard(card.id);
            return;
        }

        // Default: play as money for action/rent cards, or as property
        // For now, simple play logic — modals for complex actions will come later
        const request: PlayCardRequest = { playAsMoney: false };

        switch (card.cardType) {
            case "Money":
                request.playAsMoney = true;
                break;
            case "Property":
                // Plays directly as property
                break;
            case "PropertyWildcard":
                request.wildcardColor = card.activeColor ?? card.color ?? undefined;
                break;
            case "Action":
                if (card.actionKind === "PassGo") {
                    // PassGo can just be played directly
                    break;
                }
                // For complex actions, bank as money for now
                // TODO: Action modals for targeting
                request.playAsMoney = true;
                break;
            case "Rent":
                // TODO: Rent targeting modal
                request.playAsMoney = true;
                break;
        }

        onPlayCard(card.id, request);
    };

    return (
        <div className="hand">
            <div className="hand-label">Your Hand ({cards.length})</div>
            <div className="hand-cards">
                {cards.map((card) => (
                    <CardComponent
                        key={card.id}
                        card={card}
                        onClick={canPlay ? () => handleCardClick(card) : undefined}
                    />
                ))}
                {cards.length === 0 && <span className="hand-empty">No cards in hand</span>}
            </div>
        </div>
    );
}
