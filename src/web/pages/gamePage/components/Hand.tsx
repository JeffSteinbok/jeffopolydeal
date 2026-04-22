import React, { useState, useRef, useEffect } from "react";
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
    playsRemaining?: number;
    isMyTurn?: boolean;
    onEndTurn?: () => void;
    onPlayCard: (cardId: number, request: PlayCardRequest) => void;
    onDiscardCard: (cardId: number) => void;
    onInspectPlayer?: (player: PlayerState) => void;
}

export function Hand({ cards, canPlay, phase, gameState, myConnectionId, smallCards, playsRemaining, isMyTurn, onEndTurn, onPlayCard, onDiscardCard, onInspectPlayer }: HandProps) {
    const [selectedCard, setSelectedCard] = useState<Card | null>(null);
    const [needsOverlap, setNeedsOverlap] = useState(false);
    const [showNoPlaysPopup, setShowNoPlaysPopup] = useState(false);
    const [dismissedNoPlays, setDismissedNoPlays] = useState(false);
    const containerRef = useRef<HTMLDivElement>(null);

    // Show "no plays left" popup when playsRemaining hits 0 during Play phase
    const noPlaysLeft = isMyTurn && phase === "Play" && playsRemaining === 0;
    useEffect(() => {
        if (noPlaysLeft && !dismissedNoPlays) {
            setShowNoPlaysPopup(true);
        } else if (!noPlaysLeft) {
            setShowNoPlaysPopup(false);
            setDismissedNoPlays(false);
        }
    }, [noPlaysLeft, dismissedNoPlays]);

    // Keyboard: Enter = End Turn, Escape = Re-Arrange
    useEffect(() => {
        if (!showNoPlaysPopup) return;
        const handler = (e: KeyboardEvent) => {
            if (e.key === "Enter") { onEndTurn?.(); setShowNoPlaysPopup(false); }
            if (e.key === "Escape") { setShowNoPlaysPopup(false); setDismissedNoPlays(true); }
        };
        document.addEventListener("keydown", handler);
        return () => document.removeEventListener("keydown", handler);
    }, [showNoPlaysPopup, onEndTurn]);

    // Calculate dynamic overlap margin using ResizeObserver
    const [overlapMargin, setOverlapMargin] = useState(0);
    useEffect(() => {
        const el = containerRef.current;
        if (!el) return;

        const recalc = () => {
            if (cards.length <= 1) {
                setNeedsOverlap(false);
                setOverlapMargin(0);
                return;
            }
            const cardWidth = smallCards ? 88 : 150;
            const gap = 3; // matches CSS gap
            const padding = 24; // 12px padding on each side of .hand
            const totalWidth = cards.length * cardWidth + (cards.length - 1) * gap;
            const available = el.clientWidth - padding;
            if (totalWidth > available) {
                setNeedsOverlap(true);
                const excess = totalWidth - available;
                const margin = Math.ceil(excess / (cards.length - 1));
                const maxOverlap = Math.floor(cardWidth * 0.65);
                setOverlapMargin(Math.min(margin, maxOverlap));
            } else {
                setNeedsOverlap(false);
                setOverlapMargin(0);
            }
        };

        recalc();
        const ro = new ResizeObserver(recalc);
        ro.observe(el);
        return () => ro.disconnect();
    }, [cards.length, smallCards]);

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
            <div className="hand-header">
                <div className="hand-label">Your Hand ({cards.length})</div>
                {isMyTurn && phase === "Play" && (
                    <div className="hand-turn-controls">
                        <span className="playDots">
                            {[0, 1, 2].map(i => (
                                <span key={i} className={`playDot${i < (3 - (playsRemaining ?? 3)) ? " playDot--filled" : ""}`} />
                            ))}
                        </span>
                        <button className="endTurnButton" onClick={onEndTurn}>
                            End Turn
                        </button>
                    </div>
                )}
            </div>
            <div
                ref={containerRef}
                className={`hand-cards${!canPlay ? " hand-cards--disabled" : ""}${needsOverlap && !smallCards ? " hand-cards--overlap" : ""}`}
            >
                {cards.map((card, idx) => (
                    <div
                        key={card.id}
                        className="hand-card-wrapper"
                        style={needsOverlap && idx < cards.length - 1 ? { marginRight: `-${overlapMargin}px` } : undefined}
                    >
                        <CardComponent
                            card={card}
                            small={smallCards}
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

            {showNoPlaysPopup && (
                <div className="noPlaysOverlay">
                    <div className="noPlaysPopup">
                        <h3>No Plays Remaining</h3>
                        <div className="noPlaysPopup-buttons">
                            <button className="rearrangeButton" onClick={() => { setShowNoPlaysPopup(false); setDismissedNoPlays(true); }}>
                                Re-Arrange Cards
                            </button>
                            <button className="endTurnButton" onClick={() => { onEndTurn?.(); setShowNoPlaysPopup(false); }}>
                                End Turn
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}
