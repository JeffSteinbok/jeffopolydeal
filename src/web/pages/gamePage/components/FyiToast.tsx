import React from "react";
import { GameAction } from "../../../Types";
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
                    <span className="fyiToast-name">{toast.playerName}</span>{" "}
                    {toast.text}
                </div>
            ))}
        </div>
    );
}
