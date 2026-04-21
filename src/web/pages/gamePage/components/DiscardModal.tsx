import React, { useState } from "react";
import { Card } from "../../../Types";
import { CardComponent } from "./Card";
import "./ActionModal.css";

interface DiscardModalProps {
    hand: Card[];
    maxHandSize: number;
    onDiscard: (cardIds: number[]) => void;
    onCancel?: () => void;
}

export function DiscardModal({ hand, maxHandSize, onDiscard, onCancel }: DiscardModalProps) {
    const [selectedCardIds, setSelectedCardIds] = useState<number[]>([]);
    const excess = hand.length - maxHandSize;
    const remaining = excess - selectedCardIds.length;

    const toggleCard = (id: number) => {
        setSelectedCardIds((prev) =>
            prev.includes(id)
                ? prev.filter((x) => x !== id)
                : prev.length < excess ? [...prev, id] : prev
        );
    };

    const handleConfirm = () => {
        if (remaining <= 0) {
            onDiscard(selectedCardIds);
        }
    };

    return (
        <div className="modalOverlay">
            <div className="modal discardModal">
                <h3>Discard Cards</h3>
                <p className="modalDescription">
                    You have {hand.length} cards — max is {maxHandSize}. Select {excess} to discard.
                </p>
                <div className="paymentCards discardCards">
                    {hand.map((card) => {
                        const isSelected = selectedCardIds.includes(card.id);
                        const isDimmed = remaining <= 0 && !isSelected;
                        return (
                            <CardComponent
                                key={card.id}
                                card={card}
                                small
                                selected={isSelected}
                                dimmed={isDimmed}
                                onClick={() => toggleCard(card.id)}
                            />
                        );
                    })}
                </div>
                <div className="modalButtons">
                    {onCancel && (
                        <button className="secondary" onClick={onCancel}>
                            Go Back &amp; Play
                        </button>
                    )}
                    <button
                        className="primary"
                        onClick={handleConfirm}
                        disabled={remaining > 0}
                    >
                        Discard {excess} card{excess !== 1 ? "s" : ""}
                    </button>
                </div>
            </div>
        </div>
    );
}
