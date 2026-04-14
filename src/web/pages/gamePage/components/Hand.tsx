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
        // Discard phase: direct discard on click (DiscardModal is already visible)
        if (canPlay && phase === "Discard") {
            onDiscardCard(card.id);
            return;
        }

        // All other cases: show modal with full card + contextual options
        setSelectedCard(card);
    };

    const myState = gameState.players.find(p => p.connectionId === myConnectionId);

    return (
        <div className="hand">
            <div className="hand-label">Your Hand ({cards.length})</div>
            <div className="hand-cards">
                {cards.map((card) => (
                    <div
                        key={card.id}
                        className="hand-card-wrapper"
                    >
                        <CardComponent
                            card={card}
                            tiny={smallCards}
                            onClick={() => handleCardClick(card)}
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
                    canPlay={canPlay}
                    phase={phase}
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
