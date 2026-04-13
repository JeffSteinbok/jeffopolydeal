import React, { useState } from "react";
import { Card } from "../../../Types";
import { CardComponent } from "./Card";
import "./ActionModal.css";

interface DiscardModalProps {
    hand: Card[];
    maxHandSize: number;
    onDiscard: (cardIds: number[]) => void;
}

export function DiscardModal({ hand, maxHandSize, onDiscard }: DiscardModalProps) {
    const [selectedCardIds, setSelectedCardIds] = useState<number[]>([]);
    const excess = hand.length - maxHandSize;
    const remaining = excess - selectedCardIds.length;

    const toggleCard = (id: number) => {
        setSelectedCardIds((prev) =>
            prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]
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
                    You have {hand.length} cards — max is {maxHandSize}.
                    Select {excess} card{excess > 1 ? "s" : ""} to discard.
                </p>
                <div className="paymentCards">
                    {hand.map((card) => (
                        <CardComponent
                            key={card.id}
                            card={card}
                            small
                            selected={selectedCardIds.includes(card.id)}
                            onClick={() => toggleCard(card.id)}
                        />
                    ))}
                </div>
                <p className="modalHint">
                    {remaining > 0
                        ? `Select ${remaining} more card${remaining > 1 ? "s" : ""}`
                        : "Ready to discard!"}
                </p>
                <div className="modalButtons">
                    <button
                        className="primary"
                        onClick={handleConfirm}
                        disabled={remaining > 0}
                    >
                        Discard {selectedCardIds.length} card{selectedCardIds.length !== 1 ? "s" : ""}
                    </button>
                </div>
            </div>
        </div>
    );
}
