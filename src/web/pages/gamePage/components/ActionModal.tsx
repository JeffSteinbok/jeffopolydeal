import React, { useState } from "react";
import { PendingAction, PlayerState, ActionResponse, Card } from "../../../Types";
import { CardComponent } from "./Card";
import "./ActionModal.css";

interface ActionModalProps {
    pendingAction: PendingAction;
    myState: PlayerState;
    onRespond: (response: ActionResponse) => void;
}

export function ActionModal({ pendingAction, myState, onRespond }: ActionModalProps) {
    const [selectedCardIds, setSelectedCardIds] = useState<number[]>([]);

    const hasJustSayNo = myState.hand?.some((c) => c.actionKind === "JustSayNo") ?? false;
    const isPayment = ["PayRent", "PayDebtCollector", "PayBirthday"].includes(pendingAction.type);
    const isStealResponse = ["RespondToSlyDeal", "RespondToForceDeal", "RespondToDealBreaker"].includes(pendingAction.type);

    const payableCards = [...myState.bank];
    myState.propertySets.forEach((set) => payableCards.push(...set.cards));

    const selectedTotal = selectedCardIds.reduce((sum, id) => {
        const card = payableCards.find((c) => c.id === id);
        return sum + (card?.moneyValue ?? 0);
    }, 0);

    const toggleCard = (id: number) => {
        setSelectedCardIds((prev) =>
            prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]
        );
    };

    const handlePay = () => {
        onRespond({ playJustSayNo: false, paymentCardIds: selectedCardIds });
    };

    const handleJustSayNo = () => {
        onRespond({ playJustSayNo: true });
    };

    const handleAccept = () => {
        // Accept the steal/swap — no payment needed
        onRespond({ playJustSayNo: false, paymentCardIds: [] });
    };

    const getTitle = (): string => {
        switch (pendingAction.type) {
            case "PayRent": return `Pay Rent: $M{pendingAction.amount}`;
            case "PayDebtCollector": return `Debt Collector: Pay $M{pendingAction.amount}`;
            case "PayBirthday": return `Happy Birthday! Pay $M{pendingAction.amount}`;
            case "RespondToSlyDeal": return "Sly Deal — Someone is stealing your property!";
            case "RespondToForceDeal": return "Force Deal — Someone wants to swap properties!";
            case "RespondToDealBreaker": return "Deal Breaker — Someone is taking your complete set!";
            case "JustSayNoChain": return "Just Say No was played! Counter it?";
            default: return "Respond";
        }
    };

    return (
        <div className="modalOverlay">
            <div className="modal">
                <h3>{getTitle()}</h3>

                {isPayment && (
                    <>
                        <p className="modalHint">Select cards to pay with (M{selectedTotal} / M{pendingAction.amount})</p>
                        <div className="paymentCards">
                            {payableCards.map((card) => (
                                <CardComponent
                                    key={card.id}
                                    card={card}
                                    small
                                    selected={selectedCardIds.includes(card.id)}
                                    onClick={() => toggleCard(card.id)}
                                />
                            ))}
                            {payableCards.length === 0 && <p>You have nothing to pay with!</p>}
                        </div>
                        <div className="modalButtons">
                            {hasJustSayNo && (
                                <button className="primary" onClick={handleJustSayNo}>Just Say No!</button>
                            )}
                            <button
                                className="secondary"
                                onClick={handlePay}
                            >
                                {payableCards.length === 0 ? "I have nothing" : `Pay $M{selectedTotal}`}
                            </button>
                        </div>
                    </>
                )}

                {isStealResponse && (
                    <div className="modalButtons">
                        {hasJustSayNo && (
                            <button className="primary" onClick={handleJustSayNo}>Just Say No!</button>
                        )}
                        <button className="secondary" onClick={handleAccept}>Accept</button>
                    </div>
                )}

                {pendingAction.type === "JustSayNoChain" && (
                    <div className="modalButtons">
                        {hasJustSayNo && (
                            <button className="primary" onClick={handleJustSayNo}>Counter with Just Say No!</button>
                        )}
                        <button className="secondary" onClick={handleAccept}>Let it go</button>
                    </div>
                )}
            </div>
        </div>
    );
}
