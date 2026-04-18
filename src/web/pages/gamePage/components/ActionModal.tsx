import React, { useState } from "react";
import { PendingAction, PlayerState, ActionResponse, Card } from "../../../Types";
import { CardComponent } from "./Card";
import IndicatorSvg from "../../../assets/Indicator.svg";
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
    const [showHand, setShowHand] = useState(false);

    const hasJustSayNo = myState.hand?.some((c) => c.actionKind === "JustSayNo") ?? false;
    const isPayment = ["PayRent", "PayDebtCollector", "PayBirthday"].includes(pendingAction.type);
    const isStealResponse = ["RespondToSlyDeal", "RespondToForceDeal", "RespondToDealBreaker"].includes(pendingAction.type);
    const who = pendingAction.sourcePlayerName || "Someone";

    const payableCards = [...myState.bank];
    myState.propertySets.forEach((set) => payableCards.push(...set.cards));

    // Clear stale selections when payable cards change
    const payableIds = new Set(payableCards.map(c => c.id));
    const validSelectedIds = selectedCardIds.filter(id => payableIds.has(id));
    if (validSelectedIds.length !== selectedCardIds.length) {
        // Use a timeout to avoid setting state during render
        setTimeout(() => setSelectedCardIds(validSelectedIds), 0);
    }

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
        setSelectedCardIds((prev) =>
            prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]
        );
    };

    const handlePay = () => {
        // Filter out any stale card IDs that are no longer in payable cards
        const validIds = selectedCardIds.filter(id => payableCards.some(c => c.id === id));
        onRespond({ playJustSayNo: false, paymentCardIds: canAfford ? validIds : [] });
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
                return `Pay ◆${pendingAction.amount} in rent.`;
            case "PayDebtCollector":
                return `Pay ◆${pendingAction.amount} to ${who}.`;
            case "PayBirthday":
                return `Pay ◆${pendingAction.amount} as a birthday gift.`;
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
                                Select cards to pay (◆{selectedTotal} / ◆{pendingAction.amount}):
                            </p>
                        ) : (
                            <p className="modalHint">
                                <span className="modalWarning">You can't afford ◆{pendingAction.amount} — the game will take everything you have (◆{totalAssets}).</span>
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
                        <div className="modalButtonBar">
                            <div className="payButtonWrapper">
                                <button
                                    className="primary payButton"
                                    onClick={handlePay}
                                    disabled={needsMore}
                                    style={needsMore ? {} : { background: "#2e7d32", borderColor: "#1b5e20" }}
                                >
                                    {payableCards.length === 0
                                        ? "I have nothing"
                                        : canAfford
                                            ? `Pay ◆${selectedTotal}`
                                            : `Give Everything (◆${totalAssets})`}
                                </button>
                            </div>
                            {hasJustSayNo && (
                                <button className="primary" onClick={handleJustSayNo}>Just Say No!</button>
                            )}
                        </div>
                    </>
                )}

                {isStealResponse && (
                    <div className="modalButtonBar">
                        <button className="secondary" onClick={handleAccept}>Accept</button>
                        <div className="modalButtonBar-right">
                            {hasJustSayNo && (
                                <button className="primary" onClick={handleJustSayNo}>Just Say No!</button>
                            )}
                        </div>
                    </div>
                )}

                {pendingAction.type === "JustSayNoChain" && (
                    <div className="modalButtonBar">
                        <button className="secondary" onClick={handleAccept}>Let it go</button>
                        <div className="modalButtonBar-right">
                            {hasJustSayNo && (
                                <button className="primary" onClick={handleJustSayNo}>Counter with Just Say No!</button>
                            )}
                        </div>
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
                                    🔍 {p.name}
                                </button>
                            ))}
                            <button
                                className="modalInspect-btn"
                                onClick={() => setShowHand(prev => !prev)}
                            >
                                <img src={IndicatorSvg} alt="cards" style={{ width: 14, height: "auto", verticalAlign: "middle", marginRight: 4 }} />
                                {showHand ? "Hide Hand" : "Show Hand"}
                            </button>
                        </div>
                    </div>
                )}

                {showHand && myState.hand && myState.hand.length > 0 && (
                    <div className="modalHand">
                        <div className="modalHand-cards">
                            {myState.hand.map(c => (
                                <CardComponent key={c.id} card={c} small />
                            ))}
                        </div>
                    </div>
                )}
            </div>
        </div>
    );
}
