import React, { useState, useMemo } from "react";
import { Card, PlayCardRequest, GameState, PlayerState, PropertyColor } from "../../../Types";
import { CardComponent } from "./Card";
import { PropertyColorMap } from "../../../utilities/PropertyColors";
import "./PlayCardModal.css";

interface PlayCardModalProps {
    card: Card;
    gameState: GameState;
    myState: PlayerState;
    canPlay: boolean;
    phase: string;
    onPlay: (cardId: number, request: PlayCardRequest) => void;
    onCancel: () => void;
    onInspect?: (player: PlayerState) => void;
}

type Step = "choice" | "pickColor" | "pickTarget" | "pickTargetProperty" | "pickMyProperty" | "pickMySet" | "pickTargetSet" | "pickRentColor" | "pickDoubleRent";

export function PlayCardModal({ card, gameState, myState, canPlay, phase, onPlay, onCancel, onInspect }: PlayCardModalProps) {
    const [step, setStep] = useState<Step>("choice");
    const [request, setRequest] = useState<Partial<PlayCardRequest>>({});
    const otherPlayers = gameState.players.filter(p => p.connectionId !== myState.connectionId);

    const canPlayAsMoney = card.moneyValue > 0;
    // Memoize so canUseAction (which traverses sets) is only recalculated when inputs change
    const actionPlayable = useMemo(() => canUseAction(), [card, myState, otherPlayers]); // eslint-disable-line react-hooks/exhaustive-deps

    // Money card — always shows modal; Bank button only available on your turn
    if (card.cardType === "Money") {
        return (
            <div className="modalOverlay" onClick={onCancel}>
                <div className="playCardModal" onClick={e => e.stopPropagation()}>
                    <div className="modalCardPreview">
                        <CardComponent card={card} />
                    </div>
                    <h3>{card.name}</h3>
                    {canPlay && (
                        <div className="choiceButtons">
                            <button className="choiceButton choiceButton--money" onClick={() => onPlay(card.id, { playAsMoney: true })}>
                                💰 Bank as M{card.moneyValue}
                            </button>
                        </div>
                    )}
                    <button className="secondary" onClick={onCancel}>{canPlay ? "Cancel" : "Close"}</button>
                </div>
            </div>
        );
    }

    // Property card — Play as property or Bank for money
    if (card.cardType === "Property") {
        return (
            <div className="modalOverlay" onClick={onCancel}>
                <div className="playCardModal" onClick={e => e.stopPropagation()}>
                    <div className="modalCardPreview">
                        <CardComponent card={card} />
                    </div>
                    <h3>{card.name}</h3>
                    {canPlay && (
                        <div className="choiceButtons">
                            <button className="choiceButton choiceButton--action" onClick={() => onPlay(card.id, { playAsMoney: false })}>
                                🏘️ Play as Property
                            </button>
                        </div>
                    )}
                    <button className="secondary" onClick={onCancel}>{canPlay ? "Cancel" : "Close"}</button>
                </div>
            </div>
        );
    }

    // Not your turn — read-only card view for wildcard / action / rent
    if (!canPlay) {
        return (
            <div className="modalOverlay" onClick={onCancel}>
                <div className="playCardModal" onClick={e => e.stopPropagation()}>
                    <div className="modalCardPreview">
                        <CardComponent card={card} />
                    </div>
                    <h3>{card.name}</h3>
                    <button className="secondary" onClick={onCancel}>Close</button>
                </div>
            </div>
        );
    }

    // Property Wildcard — choose color
    if (card.cardType === "PropertyWildcard") {
        const colorOptions: PropertyColor[] = card.isMulticolorWild
            ? (Object.keys(PropertyColorMap) as PropertyColor[])
            : [card.color!, card.altColor!].filter(Boolean) as PropertyColor[];

        return (
            <div className="modalOverlay" onClick={onCancel}>
                <div className="playCardModal" onClick={e => e.stopPropagation()}>
                    <div className="modalCardPreview">
                        <CardComponent card={card} />
                    </div>
                    <h3>Play {card.name}</h3>
                    <p className="modalHint">Choose which color to play as:</p>
                    <div className="colorChoices">
                        {colorOptions.map(color => (
                            <button
                                key={color}
                                className="colorChoice"
                                style={{ backgroundColor: PropertyColorMap[color].hex, color: PropertyColorMap[color].textColor }}
                                onClick={() => onPlay(card.id, { playAsMoney: false, wildcardColor: color })}
                            >
                                {PropertyColorMap[color].name}
                            </button>
                        ))}
                    </div>
                    <button className="secondary" onClick={onCancel}>Cancel</button>
                </div>
            </div>
        );
    }

    // Action / Rent cards — multi-step flow
    // Step: choice (action vs money)
    if (step === "choice") {
        return (
            <div className="modalOverlay" onClick={onCancel}>
                <div className="playCardModal" onClick={e => e.stopPropagation()}>
                    <div className="modalCardPreview">
                        <CardComponent card={card} />
                    </div>
                    <h3>{card.name}</h3>
                    <div className="choiceButtons">
                        <button
                            className="choiceButton choiceButton--action"
                            disabled={!actionPlayable}
                            onClick={() => handlePlayAsAction()}
                        >
                            {card.cardType === "Rent" ? "⚡ Charge Rent" : "⚡ Use Action"}
                        </button>
                        {canPlayAsMoney && (
                            <button className="choiceButton choiceButton--money" onClick={() => onPlay(card.id, { playAsMoney: true })}>
                                💰 Bank as M{card.moneyValue}
                            </button>
                        )}
                    </div>
                    <button className="secondary" onClick={onCancel}>Cancel</button>
                </div>
            </div>
        );
    }

    // Step: pick rent color
    if (step === "pickRentColor") {
        const myColors = myState.propertySets
            .filter(s => s.cards.length > 0)
            .map(s => s.color);
        const validColors = card.isWildRent
            ? myColors
            : myColors.filter(c => card.rentColors?.includes(c));

        return (
            <div className="modalOverlay" onClick={onCancel}>
                <div className="playCardModal" onClick={e => e.stopPropagation()}>
                    <h3>Choose color to charge rent for</h3>
                    <div className="colorChoices">
                        {validColors.map(color => {
                            const set = myState.propertySets.find(s => s.color === color);
                            return (
                                <button
                                    key={color}
                                    className="colorChoice"
                                    style={{ backgroundColor: PropertyColorMap[color].hex, color: PropertyColorMap[color].textColor }}
                                    onClick={() => {
                                        const newReq = { ...request, rentColor: color };
                                        setRequest(newReq);
                                        // Check for Double the Rent cards
                                        const doubleCards = (myState.hand ?? []).filter(c => c.actionKind === "DoubleTheRent" && c.id !== card.id);
                                        if (doubleCards.length > 0) {
                                            setStep("pickDoubleRent");
                                        } else if (card.isWildRent) {
                                            setStep("pickTarget");
                                        } else {
                                            onPlay(card.id, { playAsMoney: false, ...newReq } as PlayCardRequest);
                                        }
                                    }}
                                >
                                    {PropertyColorMap[color].name} (M{set?.rent})
                                </button>
                            );
                        })}
                    </div>
                    {validColors.length === 0 && <p className="modalHint">You have no matching properties!</p>}
                    <button className="secondary" onClick={onCancel}>Cancel</button>
                </div>
            </div>
        );
    }

    // Step: pick Double the Rent cards to stack
    if (step === "pickDoubleRent") {
        const doubleCards = (myState.hand ?? []).filter(c => c.actionKind === "DoubleTheRent" && c.id !== card.id);
        const selectedDoubles = (request as any).doubleRentCardIds as number[] ?? [];
        const rentSet = myState.propertySets.find(s => s.color === request.rentColor);
        let baseRent = rentSet?.rent ?? 0;
        let multiplied = baseRent * Math.pow(2, selectedDoubles.length);

        const toggleDouble = (id: number) => {
            const current = selectedDoubles;
            const updated = current.includes(id) ? current.filter(x => x !== id) : [...current, id];
            setRequest({ ...request, doubleRentCardIds: updated } as any);
        };

        const finishRent = () => {
            const finalReq = { ...request, doubleRentCardIds: selectedDoubles.length > 0 ? selectedDoubles : undefined };
            if (card.isWildRent) {
                setRequest(finalReq);
                setStep("pickTarget");
            } else {
                onPlay(card.id, { playAsMoney: false, ...finalReq } as PlayCardRequest);
            }
        };

        return (
            <div className="modalOverlay" onClick={onCancel}>
                <div className="playCardModal" onClick={e => e.stopPropagation()}>
                    <h3>Double the Rent?</h3>
                    <p className="modalHint">
                        Base rent: M{baseRent} → With doubles: M{multiplied}
                        <br />Each Double the Rent counts as a card play.
                    </p>
                    <div className="cardChoices">
                        {doubleCards.map(c => (
                            <CardComponent
                                key={c.id}
                                card={c}
                                small
                                selected={selectedDoubles.includes(c.id)}
                                onClick={() => toggleDouble(c.id)}
                            />
                        ))}
                    </div>
                    <div className="choiceButtons">
                        <button className="choiceButton choiceButton--action" onClick={finishRent}>
                            {selectedDoubles.length > 0
                                ? `⚡ Charge M${multiplied} (${selectedDoubles.length}x doubled)`
                                : `⚡ Charge M${baseRent} (no double)`}
                        </button>
                    </div>
                    <button className="secondary" onClick={onCancel}>Cancel</button>
                </div>
            </div>
        );
    }

    // Step: pick target player
    if (step === "pickTarget") {
        return (
            <div className="modalOverlay" onClick={onCancel}>
                <div className="playCardModal" onClick={e => e.stopPropagation()}>
                    <h3>Choose target player</h3>
                    <div className="targetChoices">
                        {otherPlayers.map(p => (
                            <div key={p.connectionId} className="targetChoice-row">
                                <button
                                    className="targetChoice"
                                    onClick={() => {
                                        const newReq = { ...request, targetPlayerId: p.connectionId };
                                        setRequest(newReq);
                                        if (card.actionKind === "SlyDeal") {
                                            setStep("pickTargetProperty");
                                        } else if (card.actionKind === "ForceDeal") {
                                            setStep("pickTargetProperty");
                                        } else if (card.actionKind === "DealBreaker") {
                                            setStep("pickTargetSet");
                                        } else {
                                            onPlay(card.id, { playAsMoney: false, ...newReq } as PlayCardRequest);
                                        }
                                    }}
                                >
                                    {p.name} — 🃏{p.handCount} | 💰{p.bank.reduce((s, c) => s + c.moneyValue, 0)}M | {p.completedSetCount}/3 sets
                                </button>
                                {onInspect && (
                                    <button
                                        className="targetChoice-inspect"
                                        onClick={e => { e.stopPropagation(); onInspect(p); }}
                                        aria-label={`Inspect ${p.name}'s board`}
                                        title={`Inspect ${p.name}'s board`}
                                    >
                                        👁
                                    </button>
                                )}
                            </div>
                        ))}
                    </div>
                    <button className="secondary" onClick={onCancel}>Cancel</button>
                </div>
            </div>
        );
    }

    // Step: pick target's property (for Sly Deal / Force Deal)
    if (step === "pickTargetProperty") {
        const target = gameState.players.find(p => p.connectionId === request.targetPlayerId);
        const stealable = target?.propertySets
            .filter(s => !s.isComplete)
            .flatMap(s => s.cards) ?? [];

        return (
            <div className="modalOverlay" onClick={onCancel}>
                <div className="playCardModal" onClick={e => e.stopPropagation()}>
                    <h3>Pick a property to {card.actionKind === "SlyDeal" ? "steal" : "swap for"}</h3>
                    <div className="cardChoices">
                        {stealable.map(c => (
                            <CardComponent
                                key={c.id}
                                card={c}
                                small
                                onClick={() => {
                                    const newReq = { ...request, targetCardId: c.id };
                                    setRequest(newReq);
                                    if (card.actionKind === "ForceDeal") {
                                        setStep("pickMyProperty");
                                    } else {
                                        onPlay(card.id, { playAsMoney: false, ...newReq } as PlayCardRequest);
                                    }
                                }}
                            />
                        ))}
                    </div>
                    {stealable.length === 0 && <p className="modalHint">No stealable properties (complete sets are protected)</p>}
                    <button className="secondary" onClick={onCancel}>Cancel</button>
                </div>
            </div>
        );
    }

    // Step: pick my property to offer (for Force Deal)
    if (step === "pickMyProperty") {
        const myStealable = myState.propertySets
            .filter(s => !s.isComplete)
            .flatMap(s => s.cards);

        return (
            <div className="modalOverlay" onClick={onCancel}>
                <div className="playCardModal" onClick={e => e.stopPropagation()}>
                    <h3>Pick your property to offer in exchange</h3>
                    <div className="cardChoices">
                        {myStealable.map(c => (
                            <CardComponent
                                key={c.id}
                                card={c}
                                small
                                onClick={() => {
                                    const finalReq = { ...request, offeredCardId: c.id };
                                    onPlay(card.id, { playAsMoney: false, ...finalReq } as PlayCardRequest);
                                }}
                            />
                        ))}
                    </div>
                    {myStealable.length === 0 && <p className="modalHint">You have no properties to offer!</p>}
                    <button className="secondary" onClick={onCancel}>Cancel</button>
                </div>
            </div>
        );
    }

    // Step: pick target's complete set (for Deal Breaker)
    if (step === "pickTargetSet") {
        const target = gameState.players.find(p => p.connectionId === request.targetPlayerId);
        const completeSets = target?.propertySets.filter(s => s.isComplete) ?? [];

        return (
            <div className="modalOverlay" onClick={onCancel}>
                <div className="playCardModal" onClick={e => e.stopPropagation()}>
                    <h3>Pick a complete set to steal</h3>
                    <div className="targetChoices">
                        {completeSets.map(s => (
                            <button
                                key={s.color}
                                className="colorChoice"
                                style={{ backgroundColor: PropertyColorMap[s.color].hex, color: PropertyColorMap[s.color].textColor }}
                                onClick={() => {
                                    onPlay(card.id, { playAsMoney: false, ...request, targetSetColor: s.color } as PlayCardRequest);
                                }}
                            >
                                {PropertyColorMap[s.color].name} ({s.cards.length} cards)
                                {s.hasHouse ? " 🏠" : ""}{s.hasHotel ? " 🏨" : ""}
                            </button>
                        ))}
                    </div>
                    {completeSets.length === 0 && <p className="modalHint">No complete sets to steal!</p>}
                    <button className="secondary" onClick={onCancel}>Cancel</button>
                </div>
            </div>
        );
    }

    // Step: pick my complete set (for House/Hotel)
    if (step === "pickMySet") {
        const eligibleSets = myState.propertySets.filter(s => {
            if (!s.isComplete) return false;
            if (s.color === "Railroad" || s.color === "Utility") return false;
            if (card.actionKind === "House") return !s.hasHouse;
            if (card.actionKind === "Hotel") return s.hasHouse && !s.hasHotel;
            return false;
        });

        return (
            <div className="modalOverlay" onClick={onCancel}>
                <div className="playCardModal" onClick={e => e.stopPropagation()}>
                    <h3>Pick a set to add {card.actionKind === "House" ? "House" : "Hotel"} to</h3>
                    <div className="targetChoices">
                        {eligibleSets.map(s => (
                            <button
                                key={s.color}
                                className="colorChoice"
                                style={{ backgroundColor: PropertyColorMap[s.color].hex, color: PropertyColorMap[s.color].textColor }}
                                onClick={() => {
                                    onPlay(card.id, { playAsMoney: false, targetSetColor: s.color } as PlayCardRequest);
                                }}
                            >
                                {PropertyColorMap[s.color].name}
                                {s.hasHouse ? " 🏠" : ""}
                            </button>
                        ))}
                    </div>
                    {eligibleSets.length === 0 && <p className="modalHint">No eligible sets!</p>}
                    <button className="secondary" onClick={onCancel}>Cancel</button>
                </div>
            </div>
        );
    }

    return null;

    function canUseAction(): boolean {
        if (card.isPlayable !== undefined) return card.isPlayable;

        if (card.cardType === "Rent") {
            // Need properties matching the rent card colors
            const myColors = myState.propertySets.filter(s => s.cards.length > 0).map(s => s.color);
            if (card.isWildRent) return myColors.length > 0;
            return myColors.some(c => card.rentColors?.includes(c));
        }

        switch (card.actionKind) {
            case "PassGo":
                return true;
            case "ItsMyBirthday":
                return otherPlayers.length > 0;
            case "DebtCollector":
                return otherPlayers.length > 0;
            case "SlyDeal": {
                // Any opponent has stealable (non-complete-set) properties
                return otherPlayers.some(p =>
                    p.propertySets.some(s => !s.isComplete && s.cards.length > 0)
                );
            }
            case "ForceDeal": {
                const iHaveStealable = myState.propertySets.some(s => !s.isComplete && s.cards.length > 0);
                const theyHaveStealable = otherPlayers.some(p =>
                    p.propertySets.some(s => !s.isComplete && s.cards.length > 0)
                );
                return iHaveStealable && theyHaveStealable;
            }
            case "DealBreaker":
                return otherPlayers.some(p => p.propertySets.some(s => s.isComplete));
            case "House": {
                return myState.propertySets.some(s =>
                    s.isComplete && !s.hasHouse && s.color !== "Railroad" && s.color !== "Utility"
                );
            }
            case "Hotel": {
                return myState.propertySets.some(s =>
                    s.isComplete && s.hasHouse && !s.hasHotel && s.color !== "Railroad" && s.color !== "Utility"
                );
            }
            default:
                return false;
        }
    }

    // Helper to determine what step to go to after choosing "Play Action"
    function handlePlayAsAction() {
        // If the action cannot be used in the current state, keep the card in hand.
        if (!actionPlayable) {
            return;
        }

        if (card.cardType === "Rent") {
            setStep("pickRentColor");
            return;
        }

        switch (card.actionKind) {
            case "PassGo":
                onPlay(card.id, { playAsMoney: false });
                break;
            case "ItsMyBirthday":
                onPlay(card.id, { playAsMoney: false });
                break;
            case "DebtCollector":
                setStep("pickTarget");
                break;
            case "SlyDeal":
                setStep("pickTarget");
                break;
            case "ForceDeal":
                setStep("pickTarget");
                break;
            case "DealBreaker":
                setStep("pickTarget");
                break;
            case "House":
            case "Hotel":
                setStep("pickMySet");
                break;
            default:
                onPlay(card.id, { playAsMoney: true });
                break;
        }
    }
}
