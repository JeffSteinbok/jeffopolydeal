import React, { useState, useMemo, useEffect, useCallback } from "react";
import { Card, PlayCardRequest, GameState, PlayerState, PropertyColor } from "../../../Types";
import { CardComponent } from "./Card";
import { PropertyColorMap } from "../../../utilities/PropertyColors";
import IndicatorSvg from "../../../assets/Indicator.svg";
import HousePng from "../../../assets/HouseSmall.png";
import HotelPng from "../../../assets/HotelSmall.png";
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

export function PlayCardModal({ card, gameState, myState, canPlay, phase: _phase, onPlay, onCancel, onInspect: _onInspect }: PlayCardModalProps) {
    const [step, setStep] = useState<Step>("choice");
    const [request, setRequest] = useState<Partial<PlayCardRequest>>({});
    const otherPlayers = gameState.players.filter(p => p.connectionId !== myState.connectionId);

    const canPlayAsMoney = card.moneyValue > 0;
    // Memoize so canUseAction (which traverses sets) is only recalculated when inputs change
    const actionPlayable = useMemo(() => canUseAction(), [card, myState, otherPlayers]); // eslint-disable-line react-hooks/exhaustive-deps

    // Keyboard: Escape to cancel; Enter for single-action modals
    const handleSingleAction = useCallback(() => {
        if (!canPlay) { onCancel(); return; }
        if (card.cardType === "Money") { onPlay(card.id, { playAsMoney: true }); return; }
        if (card.cardType === "Property") { onPlay(card.id, { playAsMoney: false }); return; }
    }, [canPlay, card, onPlay, onCancel]);

    useEffect(() => {
        const handler = (e: KeyboardEvent) => {
            if (e.key === "Escape") { onCancel(); return; }
            if (e.key === "Enter") {
                const isSingleAction = card.cardType === "Money" || card.cardType === "Property" || !canPlay;
                if (isSingleAction) handleSingleAction();
            }
        };
        document.addEventListener("keydown", handler);
        return () => document.removeEventListener("keydown", handler);
    }, [onCancel, canPlay, card, handleSingleAction]);

    // Money card — always shows modal; Bank button only available on your turn
    if (card.cardType === "Money") {
        return (
            <div className="modalOverlay" onClick={onCancel}>
                <div className="playCardModal" onClick={e => e.stopPropagation()}>
                    <div className="modalCardPreview">
                        <CardComponent card={card} />
                    </div>
                    <div className="modalButtonBar">
                        <button className="secondary" onClick={onCancel}>{canPlay ? "Cancel" : "Close"}</button>
                        {canPlay && (
                            <div className="modalButtonBar-right">
                                <button className="choiceButton choiceButton--money" onClick={() => onPlay(card.id, { playAsMoney: true })}>
                                    ◆ Bank
                                </button>
                            </div>
                        )}
                    </div>
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
                    <div className="modalButtonBar">
                        <button className="secondary" onClick={onCancel}>{canPlay ? "Cancel" : "Close"}</button>
                        {canPlay && (
                            <div className="modalButtonBar-right">
                                <button className="choiceButton choiceButton--action" onClick={() => onPlay(card.id, { playAsMoney: false })}>
                                    🏘️ Place
                                </button>
                            </div>
                        )}
                    </div>
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
                    <div className="modalButtonBar">
                        <button className="secondary" onClick={onCancel}>Close</button>
                    </div>
                </div>
            </div>
        );
    }

    // Property Wildcard — choose color
    if (card.cardType === "PropertyWildcard") {
        const colorOptions: PropertyColor[] = card.isMulticolorWild
            ? (Object.keys(PropertyColorMap) as PropertyColor[]).filter(
                  c => myState.propertySets.some(s => s.color === c)
              )
            : [card.color!, card.altColor!].filter(Boolean) as PropertyColor[];

        return (
            <div className="modalOverlay" onClick={onCancel}>
                <div className="playCardModal" onClick={e => e.stopPropagation()}>
                    <div className="modalCardPreview">
                        <CardComponent card={card} />
                    </div>
                    <h3 style={{ color: "#eee", textAlign: "right", width: "100%", fontSize: "0.85rem", margin: "0 0 -8px" }}>Place as which color?</h3>
                    <div className="modalButtonBar">
                        <button className="secondary" onClick={onCancel}>Cancel</button>
                        <div className="modalButtonBar-right">
                            {colorOptions.map(color => (
                                <button
                                    key={color}
                                    className="colorChoice colorChoice--swatch"
                                    style={{ backgroundColor: PropertyColorMap[color].hex }}
                                    onClick={() => onPlay(card.id, { playAsMoney: false, wildcardColor: color })}
                                    title={PropertyColorMap[color].name}
                                />
                            ))}
                            {card.isMulticolorWild && (
                                <button
                                    className="colorChoice"
                                    style={{ backgroundColor: "#555", color: "#eee" }}
                                    onClick={() => onPlay(card.id, { playAsMoney: false })}
                                    title="Unassigned"
                                >
                                    Unassigned
                                </button>
                            )}
                        </div>
                    </div>
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
                    <div className="modalButtonBar">
                        <button className="secondary" onClick={onCancel}>Cancel</button>
                        <div className="modalButtonBar-right">
                            <button
                                className="choiceButton choiceButton--action"
                                disabled={!actionPlayable}
                                onClick={() => handlePlayAsAction()}
                            >
                                {card.cardType === "Rent" ? "⚡ Charge Rent" : "⚡ Use Action"}
                            </button>
                            {canPlayAsMoney && (
                                <button className="choiceButton choiceButton--money" onClick={() => onPlay(card.id, { playAsMoney: true })}>
                                    <span className="money-diamond">◆</span> Bank
                                </button>
                            )}
                        </div>
                    </div>
                </div>
            </div>
        );
    }

    // Step: pick rent color
    if (step === "pickRentColor") {
        // Deduplicate colors, keeping the highest rent for each
        const bestSetByColor = new Map<string, typeof myState.propertySets[0]>();
        for (const set of myState.propertySets) {
            if (set.cards.length === 0) continue;
            if (!card.isWildRent && !card.rentColors?.includes(set.color)) continue;
            const existing = bestSetByColor.get(set.color);
            if (!existing || set.rent > existing.rent) {
                bestSetByColor.set(set.color, set);
            }
        }
        const validColors = [...bestSetByColor.keys()];

        return (
            <div className="modalOverlay" onClick={onCancel}>
                <div className="playCardModal" onClick={e => e.stopPropagation()}>
                    <h3>Choose color to charge rent for</h3>
                    <div className="colorChoices">
                        {validColors.map(color => {
                            const set = bestSetByColor.get(color);
                            return (
                                <button
                                    key={color}
                                    className="colorChoice"
                                    style={{ backgroundColor: PropertyColorMap[color].hex, color: PropertyColorMap[color].textColor }}
                                    onClick={() => {
                                        const newReq = { ...request, rentColor: color };
                                        setRequest(newReq);
                                        // Check for Double the Rent cards (need a spare play)
                                        const doubleCards = (myState.hand ?? []).filter(c => c.actionKind === "DoubleTheRent" && c.id !== card.id);
                                        if (doubleCards.length > 0 && gameState.playsUsed + 1 < 3) {
                                            setStep("pickDoubleRent");
                                        } else if (card.isWildRent) {
                                            setStep("pickTarget");
                                        } else {
                                            onPlay(card.id, { playAsMoney: false, ...newReq } as PlayCardRequest);
                                        }
                                    }}
                                >
                                    {PropertyColorMap[color].name} (◆{set?.rent})
                                </button>
                            );
                        })}
                    </div>
                    {validColors.length === 0 && <p className="modalHint">You have no matching properties!</p>}
                    <div style={{ alignSelf: "flex-start" }}>
                        <button className="secondary" onClick={onCancel} style={{ padding: "10px 18px", fontSize: "0.9rem" }}>Cancel</button>
                    </div>
                </div>
            </div>
        );
    }

    // Step: pick Double the Rent cards to stack
    if (step === "pickDoubleRent") {
        const selectedDoubles = (request.doubleRentCardIds ?? []) as number[];
        const doubleCards = (myState.hand ?? []).filter(c => c.actionKind === "DoubleTheRent" && c.id !== card.id && !selectedDoubles.includes(c.id));
        const rentSet = myState.propertySets.find(s => s.color === request.rentColor);
        const baseRent = rentSet?.rent ?? 0;
        const multiplier = Math.pow(2, selectedDoubles.length);
        const currentRent = baseRent * multiplier;
        const doubledRent = currentRent * 2;

        const finishRent = (ids: number[]) => {
            const finalReq = { ...request, doubleRentCardIds: ids.length > 0 ? ids : undefined };
            if (card.isWildRent) {
                setRequest(finalReq);
                setStep("pickTarget");
            } else {
                onPlay(card.id, { playAsMoney: false, ...finalReq } as PlayCardRequest);
            }
        };

        const handleYes = () => {
            const newDoubles = [...selectedDoubles, doubleCards[0].id];
            const playsAfter = gameState.playsUsed + 1 + newDoubles.length;
            const moreDoubles = doubleCards.length > 1 && playsAfter < 3;
            if (moreDoubles) {
                // Show dialog again for next double
                setRequest({ ...request, doubleRentCardIds: newDoubles });
            } else {
                finishRent(newDoubles);
            }
        };

        return (
            <div className="modalOverlay" onClick={onCancel}>
                <div className="playCardModal" onClick={e => e.stopPropagation()}>
                    <h3>{selectedDoubles.length > 0 ? "Double the Rent Again?" : "Double the Rent?"}</h3>
                    <p className="modalHint">
                        Charge ◆{doubledRent} instead of ◆{currentRent}?
                        <br />Uses an extra card play.
                    </p>
                    <div className="modalButtonBar">
                        <button className="secondary" onClick={onCancel}>Cancel</button>
                        <div className="modalButtonBar-right">
                            <button className="choiceButton choiceButton--action" onClick={handleYes}>
                                ⚡ Double It! (◆{doubledRent})
                            </button>
                            <button className="choiceButton choiceButton--money" onClick={() => finishRent(selectedDoubles)}>
                                No, Charge ◆{currentRent}
                            </button>
                        </div>
                    </div>
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
                        {otherPlayers.map(p => {
                            const bankTotal = p.bank.reduce((s, c) => s + c.moneyValue, 0);
                            return (
                                <button
                                    key={p.connectionId}
                                    className="targetChoice targetChoice--card"
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
                                    <span className="targetChoice-name">{p.name}</span>
                                    <span className="targetChoice-stats">
                                        <span className="targetChoice-stat" style={{ color: "#4caf50" }}>◆{bankTotal}</span>
                                        <span className="targetChoice-stat"><img src={IndicatorSvg} alt="cards" style={{ width: 12, height: "auto", verticalAlign: "middle" }} /> {p.handCount}</span>
                                        <span className="targetChoice-stat">{p.completedSetCount}/3</span>
                                    </span>
                                </button>
                            );
                        })}
                    </div>
                    <button className="secondary" onClick={onCancel}>Cancel</button>
                </div>
            </div>
        );
    }

    // Step: pick target's property (for Sly Deal / Force Deal)
    if (step === "pickTargetProperty") {
        const target = gameState.players.find(p => p.connectionId === request.targetPlayerId);
        const stealable = [
            ...(target?.propertySets.filter(s => !s.isComplete).flatMap(s => s.cards) ?? []),
            ...(target?.unboundWilds ?? []),
        ];

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
                                {s.hasHouse ? <img src={HousePng} alt="House" className="building-icon-inline" /> : ""}{s.hasHotel ? <img src={HotelPng} alt="Hotel" className="building-icon-inline" /> : ""}
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
                    <h3>Pick a Set</h3>
                    <p className="modalDescription">New rent values shown below</p>
                    <div className="targetChoices">
                        {eligibleSets.map(s => {
                            const bonus = card.actionKind === "House" ? 3 : 4;
                            const newRent = s.rent + bonus;
                            return (
                                <button
                                    key={s.color}
                                    className="colorChoice"
                                    style={{ backgroundColor: PropertyColorMap[s.color].hex, color: PropertyColorMap[s.color].textColor }}
                                    onClick={() => {
                                        onPlay(card.id, { playAsMoney: false, targetSetColor: s.color } as PlayCardRequest);
                                    }}
                                >
                                    {PropertyColorMap[s.color].name} (◆{newRent})
                                </button>
                            );
                        })}
                    </div>
                    {eligibleSets.length === 0 && <p className="modalHint">No eligible sets!</p>}
                    <div style={{ alignSelf: "flex-start" }}>
                        <button className="secondary" onClick={onCancel} style={{ padding: "10px 18px", fontSize: "0.9rem" }}>Cancel</button>
                    </div>
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
                // Any opponent has stealable (non-complete-set) properties or unbound wilds
                return otherPlayers.some(p =>
                    p.propertySets.some(s => !s.isComplete && s.cards.length > 0) ||
                    (p.unboundWilds?.length > 0)
                );
            }
            case "ForceDeal": {
                const iHaveStealable = myState.propertySets.some(s => !s.isComplete && s.cards.length > 0) || (myState.unboundWilds?.length > 0);
                const theyHaveStealable = otherPlayers.some(p =>
                    p.propertySets.some(s => !s.isComplete && s.cards.length > 0) ||
                    (p.unboundWilds?.length > 0)
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
