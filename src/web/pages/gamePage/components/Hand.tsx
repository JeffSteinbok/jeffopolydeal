import React, { useState } from "react";
import { Card, GameState, PlayCardRequest, PlayerState } from "../../../Types";
import { CardComponent } from "./Card";
import { PlayCardModal } from "./PlayCardModal";
import "./Hand.css";

interface HandProps {
    cards: Card[];
    canPlay: boolean;
    phase: string;
    gameState: GameState;
    myConnectionId: string;
    smallCards?: boolean;
    onPlayCard: (cardId: number, request: PlayCardRequest) => void;
    onDiscardCard: (cardId: number) => void;
    onInspectPlayer?: (player: PlayerState) => void;
}

export function Hand({ cards, canPlay, phase, gameState, myConnectionId, smallCards, onPlayCard, onDiscardCard, onInspectPlayer }: HandProps) {
    const [selectedCard, setSelectedCard] = useState<Card | null>(null);

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

    const myState = gameState.players.find(p => p.connectionId === myConnectionId);

    return (
        <div className="hand">
            <div className="hand-label">Your Hand ({cards.length})</div>
            <div className={`hand-cards${!canPlay ? " hand-cards--disabled" : ""}`}>
                {cards.map((card) => (
                    <div
                        key={card.id}
                        className={`hand-card-wrapper${canPlay && card.isPlayable === false ? " hand-card-wrapper--unplayable" : ""}`}
                    >
                        <CardComponent
                            card={card}
                            small={smallCards}
                            onClick={canPlay ? () => handleCardClick(card) : undefined}
                        />
                    </div>
                ))}
                {cards.length === 0 && <span className="hand-empty">No cards in hand</span>}
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
                    onInspect={onInspectPlayer}
                />
            )}
        </div>
    );
}
