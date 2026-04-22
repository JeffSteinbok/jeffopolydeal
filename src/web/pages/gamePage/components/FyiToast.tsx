import React, { useState, useEffect, useRef } from "react";
import { GameAction } from "../../../Types";
import { CardComponent } from "./Card";
import "./FyiToast.css";

interface FyiToastProps {
    toasts: GameAction[];
    smallCards?: boolean;
    myName?: string;
    onBusyChange?: (busy: boolean) => void;
}

export function FyiToast({ toasts, smallCards, myName, onBusyChange }: FyiToastProps) {
    const [queue, setQueue] = useState<GameAction[]>([]);
    const [current, setCurrent] = useState<GameAction | null>(null);
    const [leaving, setLeaving] = useState(false);
    const seenRef = useRef<Set<number>>(new Set());

    // Notify parent when busy state changes (debounce busy→false to avoid dialog flash)
    const busy = current !== null || queue.length > 0;
    const prevBusyRef = useRef(false);
    const busyTimerRef = useRef<ReturnType<typeof setTimeout>>();
    useEffect(() => {
        if (busy !== prevBusyRef.current) {
            clearTimeout(busyTimerRef.current);
            if (busy) {
                // Going busy: notify immediately
                prevBusyRef.current = true;
                onBusyChange?.(true);
            } else {
                // Going idle: delay longer than stagger interval so dialogs don't flash
                busyTimerRef.current = setTimeout(() => {
                    prevBusyRef.current = false;
                    onBusyChange?.(false);
                }, 1500);
            }
        }
        return () => clearTimeout(busyTimerRef.current);
    }, [busy, onBusyChange]);

    // Enqueue new toasts
    useEffect(() => {
        const newOnes: GameAction[] = [];
        for (const t of toasts) {
            if (!seenRef.current.has(t.id)) {
                seenRef.current.add(t.id);
                newOnes.push(t);
            }
        }
        if (newOnes.length > 0) {
            setQueue(prev => [...prev, ...newOnes]);
        }
    }, [toasts]);

    // Show next from queue when nothing is displayed
    useEffect(() => {
        if (current || queue.length === 0) return;
        setCurrent(queue[0]);
        setQueue(prev => prev.slice(1));
        setLeaving(false);
    }, [current, queue]);

    // Auto-dismiss after delay (skip for persistent toasts)
    useEffect(() => {
        if (!current || leaving || current.persistent) return;
        const timer = setTimeout(() => setLeaving(true), 3000);
        return () => clearTimeout(timer);
    }, [current, leaving]);

    const handleAnimationEnd = (animName: string) => {
        if (animName === "fyiToastOut") {
            setCurrent(null);
            setLeaving(false);
        }
    };

    if (!current) return null;

    return (
        <div className="fyiToast-container">
            <div
                key={current.id}
                className={`fyiToast${leaving ? " fyiToast--leaving" : ""}`}
                onAnimationEnd={(e) => handleAnimationEnd(e.animationName)}
            >
                <button className="fyiToast-close" onClick={() => { setCurrent(null); setLeaving(false); }}>✕</button>
                {current.cardPlayed && (
                    <div className="fyiToast-cardGroup">
                        <CardComponent card={current.cardPlayed} small />
                    </div>
                )}
                <div className="fyiToast-body">
                    <div className="fyiToast-header">
                        <div className="fyiToast-name">{current.playerName}</div>
                        <div className="fyiToast-text">{(() => {
                            let text = current.text;
                            const cardName = current.cardPlayed?.name;
                            const parts: React.ReactNode[] = [];

                            // Replace my name with "you" in the text
                            let targetName = current.targetPlayerName;
                            if (myName && text.includes(myName)) {
                                text = text.replace(myName, "you");
                                if (targetName === myName) targetName = "you";
                            }

                            // Bold the card name in the text
                            if (cardName && text.includes(cardName)) {
                                const idx = text.indexOf(cardName);
                                parts.push(text.slice(0, idx));
                                parts.push(<strong key="card">{cardName}</strong>);
                                text = text.slice(idx + cardName.length);
                            }

                            // Bold ◆{amount} values
                            const moneyMatch = text.match(/◆\d+/);
                            if (moneyMatch) {
                                const idx = text.indexOf(moneyMatch[0]);
                                parts.push(text.slice(0, idx));
                                parts.push(<strong key="money">{moneyMatch[0]}</strong>);
                                text = text.slice(idx + moneyMatch[0].length);
                            }

                            // Bold target player name
                            if (targetName && text.includes(targetName)) {
                                const idx = text.indexOf(targetName);
                                parts.push(text.slice(0, idx));
                                parts.push(<strong key="target">{targetName}</strong>);
                                text = text.slice(idx + targetName.length);
                            } else if (targetName && targetName !== "you") {
                                parts.push(text);
                                text = "";
                                parts.push(<> against <strong key="target">{targetName}</strong></>);
                            }

                            parts.push(text);
                            return parts.map((node, i) => <React.Fragment key={i}>{node}</React.Fragment>);
                        })()}</div>
                    </div>
                    {(current.sourceCards?.length || current.targetCards?.length) ? (
                        <div className="fyiToast-cards">
                            {current.sourceCards && current.sourceCards.length > 0 && (
                                <div className="fyiToast-cardGroup fyiToast-cardGroup--labeled">
                                    {current.targetCards && current.targetCards.length > 0 && (
                                        <span className="fyiToast-pill">Gave</span>
                                    )}
                                    <div className="fyiToast-cardRow">
                                        {current.sourceCards.map((c) => (
                                            <CardComponent key={c.id} card={c} compact />
                                        ))}
                                    </div>
                                </div>
                            )}
                            {current.sourceCards && current.sourceCards.length > 0 && current.targetCards && current.targetCards.length > 0 && (
                                <span className="fyiToast-swap">⇄</span>
                            )}
                            {current.targetCards && current.targetCards.length > 0 && (
                                <div className="fyiToast-cardGroup fyiToast-cardGroup--labeled">
                                    {current.sourceCards && current.sourceCards.length > 0 && (
                                        <span className="fyiToast-pill">Got</span>
                                    )}
                                    <div className="fyiToast-cardRow">
                                        {current.targetCards.map((c) => (
                                            <CardComponent key={c.id} card={c} compact />
                                        ))}
                                    </div>
                                </div>
                            )}
                        </div>
                    ) : null}
                </div>
            </div>
        </div>
    );
}
