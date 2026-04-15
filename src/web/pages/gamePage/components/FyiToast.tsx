import React, { useState, useEffect, useRef } from "react";
import { GameAction } from "../../../Types";
import { CardComponent } from "./Card";
import "./FyiToast.css";

interface FyiToastProps {
    toasts: GameAction[];
}

interface ToastEntry {
    action: GameAction;
    leaving: boolean;
}

export function FyiToast({ toasts }: FyiToastProps) {
    const [entries, setEntries] = useState<ToastEntry[]>([]);
    const prevIdsRef = useRef<Set<number>>(new Set());

    useEffect(() => {
        const currentIds = new Set(toasts.map(t => t.id));
        const prevIds = prevIdsRef.current;

        setEntries(prev => {
            // Mark removed toasts as leaving
            let updated = prev.map(e =>
                !currentIds.has(e.action.id) && !e.leaving
                    ? { ...e, leaving: true }
                    : e
            );
            // Add new toasts
            const existingIds = new Set(updated.map(e => e.action.id));
            for (const t of toasts) {
                if (!existingIds.has(t.id)) {
                    updated.push({ action: t, leaving: false });
                }
            }
            return updated;
        });

        prevIdsRef.current = currentIds;
    }, [toasts]);

    // Remove leaving toasts after animation completes
    const handleAnimationEnd = (id: number, animName: string) => {
        if (animName === "fyiToastOut") {
            setEntries(prev => prev.filter(e => e.action.id !== id));
        }
    };

    if (entries.length === 0) return null;

    return (
        <div className="fyiToast-container">
            {entries.map((entry) => (
                <div
                    key={entry.action.id}
                    className={`fyiToast${entry.leaving ? " fyiToast--leaving" : ""}`}
                    onAnimationEnd={(e) => handleAnimationEnd(entry.action.id, e.animationName)}
                >
                    <div className="fyiToast-header">
                        <span className="fyiToast-name">{entry.action.playerName}</span>{" "}
                        {entry.action.text}
                    </div>
                    {(entry.action.cardPlayed || entry.action.sourceCards?.length || entry.action.targetCards?.length) && (
                        <div className="fyiToast-cards">
                            {entry.action.cardPlayed && (
                                <div className="fyiToast-cardGroup">
                                    <CardComponent card={entry.action.cardPlayed} />
                                </div>
                            )}
                            {entry.action.sourceCards && entry.action.sourceCards.length > 0 && (
                                <div className="fyiToast-cardGroup">
                                    <span className="fyiToast-label">Gave:</span>
                                    <div className="fyiToast-cardRow">
                                        {entry.action.sourceCards.map((c) => (
                                            <CardComponent key={c.id} card={c} small />
                                        ))}
                                    </div>
                                </div>
                            )}
                            {entry.action.targetCards && entry.action.targetCards.length > 0 && (
                                <div className="fyiToast-cardGroup">
                                    <span className="fyiToast-label">
                                        {entry.action.sourceCards?.length ? "Got:" : "Paid:"}
                                    </span>
                                    <div className="fyiToast-cardRow">
                                        {entry.action.targetCards.map((c) => (
                                            <CardComponent key={c.id} card={c} small />
                                        ))}
                                    </div>
                                </div>
                            )}
                        </div>
                    )}
                </div>
            ))}
        </div>
    );
}
