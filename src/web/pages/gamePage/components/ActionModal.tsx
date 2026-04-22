import React, { useState } from "react";
import { PendingAction, PlayerState, ActionResponse, Card } from "../../../Types";
import { CardComponent } from "./Card";
import { PropertyColorMap, PropertyColorOrder } from "../../../utilities/PropertyColors";
import IndicatorSvg from "../../../assets/Indicator.svg";
import HousePng from "../../../assets/HouseSmall.png";
import HotelPng from "../../../assets/HotelSmall.png";
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

    const bankCards = [...myState.bank].sort((a, b) => a.moneyValue - b.moneyValue);
    // Group property cards by set, sorted by value within each set
    const propertySetsWithCards = myState.propertySets
        .filter(set => set.cards.length > 0)
        .map(set => ({
            ...set,
            cards: [...set.cards].sort((a, b) => a.moneyValue - b.moneyValue),
        }))
        .sort((a, b) => PropertyColorOrder.indexOf(a.color) - PropertyColorOrder.indexOf(b.color));
    const allPropertyCards: Card[] = propertySetsWithCards.flatMap(s => s.cards);
    const selectablePropertyCards = allPropertyCards.filter(c => !c.isMulticolorWild);
    const payableCards = [...bankCards, ...selectablePropertyCards];

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
    const amountMet = canAfford && selectedTotal >= pendingAction.amount;

    const toggleCard = (id: number) => {
        if (!canAfford) return;
        // Allow deselecting, but block selecting more once amount is covered
        if (!selectedCardIds.includes(id) && selectedTotal >= pendingAction.amount) return;
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
            case "PayRent": return `${who} charges you rent of ◆${pendingAction.amount}!`;
            case "PayDebtCollector": return `${who} plays Debt Collector!`;
            case "PayBirthday": return `It's ${who}'s Birthday!`;
            case "RespondToSlyDeal": return `${who} plays Sly Deal!`;
            case "RespondToForceDeal": return `${who} plays Forced Deal!`;
            case "RespondToDealBreaker": return `${who} plays Deal Breaker!`;
            case "JustSayNoChain": return hasJustSayNo ? `${who} played Just Say No! Counter it?` : `${who} played Just Say No!`;
            default: return "Respond";
        }
    };

    const getDescription = (): React.ReactNode | null => {
        switch (pendingAction.type) {
            case "PayRent":
                return `Pay ◆${pendingAction.amount} in rent.`;
            case "PayDebtCollector":
                return `Pay ◆${pendingAction.amount} to ${who}.`;
            case "PayBirthday":
                return `Pay ◆${pendingAction.amount} as a birthday gift.`;
            case "RespondToSlyDeal":
                return <>{who} stole <strong>{pendingAction.targetCardName}</strong>.</>;
            case "RespondToForceDeal":
                return <>{who} swapped your <strong>{pendingAction.targetCardName}</strong> for their <strong>{pendingAction.offeredCardName}</strong>.</>;
            case "RespondToDealBreaker":
                return <>{who} took your complete <strong>{pendingAction.targetSetColor}</strong> property set!</>;
            default:
                return null;
        }
    };

    // Find card objects for visual display in steal/swap modals
    const findCardById = (id?: number): Card | undefined => {
        if (id == null) return undefined;
        for (const set of myState.propertySets) {
            const found = set.cards.find(c => c.id === id);
            if (found) return found;
        }
        return myState.unboundWilds?.find(c => c.id === id);
    };

    const targetCard = findCardById(pendingAction.targetCardId);
    // For ForceDeal, the offered card belongs to the source player — find it from their board
    const findOfferedCard = (): Card | undefined => {
        if (pendingAction.offeredCardId == null) return undefined;
        const sourcePlayer = otherPlayers?.find(p => p.name === pendingAction.sourcePlayerName);
        if (sourcePlayer) {
            for (const set of sourcePlayer.propertySets) {
                const found = set.cards.find(c => c.id === pendingAction.offeredCardId);
                if (found) return found;
            }
            const wild = sourcePlayer.unboundWilds?.find(c => c.id === pendingAction.offeredCardId);
            if (wild) return wild;
        }
        // Fallback to a placeholder
        if (pendingAction.offeredCardName) {
            return {
                id: pendingAction.offeredCardId ?? -1,
                cardType: "Property",
                moneyValue: 0,
                name: pendingAction.offeredCardName,
                isMulticolorWild: false,
                isWildRent: false,
            };
        }
        return undefined;
    };
    const offeredCard = findOfferedCard();

    // Determine the primary action for Enter key
    const handleEnterAction = () => {
        if (isPayment) {
            if (!needsMore) handlePay();
        } else {
            handleAccept();
        }
    };

    return (
        <div className="modalOverlay" onKeyDown={(e) => { if (e.key === "Enter") handleEnterAction(); }} tabIndex={-1} ref={(el) => el?.focus()}>
            <div className="modal">
                <h3>{getTitle()}</h3>

                {isPayment ? (
                    <>
                        {canAfford ? (
                            <p className="modalDescription">
                                {getDescription()} Select cards (◆{selectedTotal} / ◆{pendingAction.amount}):
                            </p>
                        ) : (
                            <p className="modalDescription">
                                <span className="modalWarning">You can't afford ◆{pendingAction.amount}.</span>
                            </p>
                        )}
                        {paymentError && <p className="modalError">{paymentError}</p>}
                        <div className="paymentSections">
                            {bankCards.length > 0 && (
                                <div className="paymentSection">

                                    <div className="paymentCards">
                                        {bankCards.map((card) => (
                                            <CardComponent
                                                key={card.id}
                                                card={card}
                                                compact
                                                selected={canAfford ? selectedCardIds.includes(card.id) : true}
                                                dimmed={amountMet && !selectedCardIds.includes(card.id)}
                                                onClick={() => toggleCard(card.id)}
                                            />
                                        ))}
                                    </div>
                                </div>
                            )}
                            {propertySetsWithCards.length > 0 && (
                                <div className="paymentSection">

                                    <div className="paymentSetsRow">
                                        {propertySetsWithCards.map((set) => (
                                            <div key={set.setId} className="paymentSetGroup">
                                                <div
                                                    className={`paymentSetGroup-header${amountMet && !set.cards.some(c => selectedCardIds.includes(c.id)) ? " paymentSetGroup-header--dimmed" : ""}`}
                                                    style={{ backgroundColor: PropertyColorMap[set.color].hex, color: PropertyColorMap[set.color].textColor }}
                                                >
                                                    {`${set.cards.length}/${set.requiredSize}`}
                                                    {set.isComplete && "✓"}
                                                    {set.hasHouse && <img src={HousePng} alt="House" className="paymentSetGroup-building" />}
                                                    {set.hasHotel && <img src={HotelPng} alt="Hotel" className="paymentSetGroup-building" />}
                                                </div>
                                                <div className="paymentCards">
                                                    {set.cards.map((card) => {
                                                        const isFullWild = card.isMulticolorWild;
                                                        return (
                                                            <CardComponent
                                                                key={card.id}
                                                                card={card}
                                                                small
                                                                selected={!isFullWild && (canAfford ? selectedCardIds.includes(card.id) : true)}
                                                                dimmed={isFullWild || (amountMet && !selectedCardIds.includes(card.id))}
                                                                onClick={isFullWild ? undefined : () => toggleCard(card.id)}
                                                            />
                                                        );
                                                    })}
                                                </div>
                                            </div>
                                        ))}
                                    </div>
                                </div>
                            )}
                            {bankCards.length === 0 && allPropertyCards.length === 0 && <p>You have nothing to pay with!</p>}
                        </div>
                        <div className="modalButtonBar">
                            <div className="payButtonWrapper">
                                <button
                                    className="primary payButton"
                                    onClick={handlePay}
                                    disabled={needsMore}
                                >
                                    {payableCards.length === 0
                                        ? "I have nothing"
                                        : canAfford
                                            ? `Pay ◆${selectedTotal}`
                                            : `Give Everything (◆${totalAssets})`}
                                </button>
                            </div>
                            {hasJustSayNo && (
                                <button className="primary justSayNoBtn" onClick={handleJustSayNo}>Just Say No!</button>
                            )}
                        </div>
                    </>
                ) : (
                    <>
                        {getDescription() && <p className="modalDescription">{getDescription()}</p>}
                        {/* Card visuals for steal/swap */}
                        {pendingAction.type === "RespondToForceDeal" && targetCard && offeredCard && (
                            <div className="modalSwapCards">
                                <div className="modalSwapCards-side">
                                    <span className="modalSwapCards-label">Yours</span>
                                    <CardComponent card={targetCard} small />
                                </div>
                                <span className="modalSwapCards-arrow">⇄</span>
                                <div className="modalSwapCards-side">
                                    <span className="modalSwapCards-label">Theirs</span>
                                    <CardComponent card={offeredCard} small />
                                </div>
                            </div>
                        )}
                        {pendingAction.type === "RespondToSlyDeal" && targetCard && (
                            <div className="modalSwapCards" style={{ justifyContent: "flex-start" }}>
                                <CardComponent card={targetCard} small />
                            </div>
                        )}
                    </>
                )}

                {isStealResponse && (
                    <div className="modalButtonBar" style={!hasJustSayNo ? { justifyContent: "flex-end" } : undefined}>
                        <button className={hasJustSayNo ? "secondary" : "primary"} onClick={handleAccept}>
                            {hasJustSayNo ? "Accept" : "Ok"}
                        </button>
                        {hasJustSayNo && (
                            <div className="modalButtonBar-right">
                                <button className="primary justSayNoBtn" onClick={handleJustSayNo}>Just Say No!</button>
                            </div>
                        )}
                    </div>
                )}

                {pendingAction.type === "JustSayNoChain" && (
                    <div className="modalButtonBar" style={!hasJustSayNo ? { justifyContent: "flex-end" } : undefined}>
                        {hasJustSayNo && (
                            <button className="secondary" onClick={handleAccept}>Let it go</button>
                        )}
                        <div className="modalButtonBar-right">
                            {hasJustSayNo && (
                                <button className="primary justSayNoBtn" onClick={handleJustSayNo}>Counter with Just Say No!</button>
                            )}
                            {!hasJustSayNo && (
                                <button className="choiceButton choiceButton--money" style={{ width: "auto", padding: "8px 24px", fontSize: "0.9rem" }} onClick={handleAccept}>Ok</button>
                            )}
                        </div>
                    </div>
                )}

                {/* Inspect other players' boards and view own hand — only when player has choices (JSN) */}
                {hasJustSayNo && onInspect && (otherPlayers?.length ?? 0) > 0 && (
                    <div className="modalInspect">

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
                            <div className="modalInspect-showHand">
                                <button
                                    className="modalInspect-btn"
                                    onClick={() => setShowHand(prev => !prev)}
                                >
                                    <img src={IndicatorSvg} alt="Hand" style={{ width: 14, height: "auto", verticalAlign: "middle" }} />
                                    <span className="modalInspect-tooltip">Inspect your hand.</span>
                                </button>
                            </div>
                        </div>
                    </div>
                )}

                {showHand && myState.hand && myState.hand.length > 0 && (
                    <div
                        className="inspectOverlay inspectOverlay--hand"
                        onClick={() => setShowHand(false)}
                        role="dialog"
                        aria-modal="true"
                        aria-label="Your hand"
                    >
                        <div className="inspectDrawer inspectDrawer--hand" onClick={e => e.stopPropagation()}>
                            <div className="inspectDrawer-handle" />
                            <div className="inspectDrawer-header">
                                <h3 className="inspectDrawer-title">Your Hand</h3>
                                <button className="inspectDrawer-close" onClick={() => setShowHand(false)} aria-label="Close">✕</button>
                            </div>
                            <div className="inspectDrawer-body">
                                <div className="modalHand-cards">
                                    {myState.hand.map(c => (
                                        <CardComponent key={c.id} card={c} small />
                                    ))}
                                </div>
                            </div>
                        </div>
                    </div>
                )}
            </div>
        </div>
    );
}
