import React, { useState } from "react";
import { PendingAction, PlayerState, ActionResponse, Card } from "../../../Types";
import { CardComponent } from "./Card";
import "./ActionModal.css";

interface ActionModalProps {
    pendingAction: PendingAction;
    myState: PlayerState;
    paymentError?: string;
    onRespond: (response: ActionResponse) => void;
    otherPlayers?: PlayerState[];
    onInspect?: (player: PlayerState) => void;
}

export function ActionModal({ pendingAction, myState, paymentError, onRespond, otherPlayers, onInspect }: ActionModalProps) {
    const [selectedCardIds, setSelectedCardIds] = useState<number[]>([]);
    const [payHint, setPayHint] = useState(false);

    const hasJustSayNo = myState.hand?.some((c) => c.actionKind === "JustSayNo") ?? false;
    const isPayment = ["PayRent", "PayDebtCollector", "PayBirthday"].includes(pendingAction.type);
    const isStealResponse = ["RespondToSlyDeal", "RespondToForceDeal", "RespondToDealBreaker"].includes(pendingAction.type);
    const who = pendingAction.sourcePlayerName || "Someone";

    const payableCards = [...myState.bank];
    myState.propertySets.forEach((set) => payableCards.push(...set.cards));

    const selectedTotal = selectedCardIds.reduce((sum, id) => {
        const card = payableCards.find((c) => c.id === id);
        return sum + (card?.moneyValue ?? 0);
    }, 0);

    const totalAssets = payableCards.reduce((sum, c) => sum + (c.moneyValue ?? 0), 0);
    const canAfford = totalAssets >= pendingAction.amount;
    const needsMore = isPayment && canAfford && payableCards.length > 0 &&
        selectedTotal < pendingAction.amount;

    const toggleCard = (id: number) => {
        if (!canAfford) return;
        setPayHint(false);
        setSelectedCardIds((prev) =>
            prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]
        );
    };

    const handlePay = () => {
        if (needsMore) {
            setPayHint(true);
            return;
        }
        onRespond({ playJustSayNo: false, paymentCardIds: canAfford ? selectedCardIds : [] });
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
            case "PayRent": return `${who} charges you rent!`;
            case "PayDebtCollector": return `${who} plays Debt Collector!`;
            case "PayBirthday": return `It's ${who}'s Birthday!`;
            case "RespondToSlyDeal": return `${who} plays Sly Deal!`;
            case "RespondToForceDeal": return `${who} plays Forced Deal!`;
            case "RespondToDealBreaker": return `${who} plays Deal Breaker!`;
            case "JustSayNoChain": return "Just Say No was played! Counter it?";
            default: return "Respond";
        }
    };

    const getDescription = (): string | null => {
        switch (pendingAction.type) {
            case "PayRent":
                return `Pay ${pendingAction.amount} in rent.`;
            case "PayDebtCollector":
                return `Pay ${pendingAction.amount} to ${who}.`;
            case "PayBirthday":
                return `Pay ${pendingAction.amount} as a birthday gift.`;
            case "RespondToSlyDeal":
                return `${who} is stealing your "${pendingAction.targetCardName}" with a Sly Deal.`;
            case "RespondToForceDeal":
                return `${who} wants to swap your "${pendingAction.targetCardName}" for their "${pendingAction.offeredCardName}".`;
            case "RespondToDealBreaker":
                return `${who} is taking your complete ${pendingAction.targetSetColor} property set!`;
            default:
                return null;
        }
    };

    return (
        <div className="modalOverlay">
            <div className="modal">
                <h3>{getTitle()}</h3>
                {getDescription() && <p className="modalDescription">{getDescription()}</p>}

                {isPayment && (
                    <>
                        {canAfford ? (
                            <p className="modalHint">
                                Select cards to pay with (M{selectedTotal} / M{pendingAction.amount})
                            </p>
                        ) : (
                            <p className="modalHint">
                                <span className="modalWarning">You can't afford M{pendingAction.amount} — the game will take everything you have (M{totalAssets}).</span>
                            </p>
                        )}
                        {paymentError && <p className="modalError">{paymentError}</p>}
                        <div className="paymentCards">
                            {payableCards.map((card) => (
                                <CardComponent
                                    key={card.id}
                                    card={card}
                                    small
                                    selected={canAfford ? selectedCardIds.includes(card.id) : true}
                                    onClick={() => toggleCard(card.id)}
                                />
                            ))}
                            {payableCards.length === 0 && <p>You have nothing to pay with!</p>}
                        </div>
                        <div className="modalButtons">
                            <div className="payButtonWrapper">
                                <button
                                    className="primary payButton"
                                    onClick={handlePay}
                                    style={{ background: "#2e7d32", borderColor: "#1b5e20" }}
                                >
                                    {payableCards.length === 0
                                        ? "I have nothing"
                                        : canAfford
                                            ? `Pay ${selectedTotal}`
                                            : `Give Everything (${totalAssets})`}
                                </button>
                                {payHint && (
                                    <div className="payHint">
                                        Select cards above to pay at least M{pendingAction.amount}. You've selected M{selectedTotal} so far.
                                    </div>
                                )}
                            </div>
                            {hasJustSayNo && (
                                <button className="primary" onClick={handleJustSayNo}>Just Say No!</button>
                            )}
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

                {/* Inspect other players' boards during the action */}
                {onInspect && (otherPlayers?.length ?? 0) > 0 && (
                    <div className="modalInspect">
                        <div className="modalInspect-label">Inspect players:</div>
                        <div className="modalInspect-buttons">
                            {otherPlayers.map(p => (
                                <button
                                    key={p.connectionId}
                                    className="modalInspect-btn"
                                    onClick={() => onInspect(p)}
                                >
                                    👁 {p.name}
                                </button>
                            ))}
                        </div>
                    </div>
                )}
            </div>
        </div>
    );
}
