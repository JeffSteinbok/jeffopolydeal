import React from "react";
import { GameAction } from "../../../Types";
import { CardComponent } from "./Card";
import "./FyiToast.css";

interface FyiToastProps {
    toasts: GameAction[];
}

export function FyiToast({ toasts }: FyiToastProps) {
    if (toasts.length === 0) return null;

    return (
        <div className="fyiToast-container">
            {toasts.map((toast) => (
                <div key={toast.id} className="fyiToast">
                    <div className="fyiToast-header">
                        <span className="fyiToast-name">{toast.playerName}</span>{" "}
                        {toast.text}
                    </div>
                    {(toast.cardPlayed || toast.sourceCards?.length || toast.targetCards?.length) && (
                        <div className="fyiToast-cards">
                            {toast.cardPlayed && (
                                <div className="fyiToast-cardGroup">
                                    <CardComponent card={toast.cardPlayed} tiny />
                                </div>
                            )}
                            {toast.sourceCards && toast.sourceCards.length > 0 && (
                                <div className="fyiToast-cardGroup">
                                    <span className="fyiToast-label">Gave:</span>
                                    <div className="fyiToast-cardRow">
                                        {toast.sourceCards.map((c) => (
                                            <CardComponent key={c.id} card={c} tiny />
                                        ))}
                                    </div>
                                </div>
                            )}
                            {toast.targetCards && toast.targetCards.length > 0 && (
                                <div className="fyiToast-cardGroup">
                                    <span className="fyiToast-label">
                                        {toast.sourceCards?.length ? "Got:" : "Paid:"}
                                    </span>
                                    <div className="fyiToast-cardRow">
                                        {toast.targetCards.map((c) => (
                                            <CardComponent key={c.id} card={c} tiny />
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
