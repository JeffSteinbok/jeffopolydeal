import React, { useEffect, useRef } from "react";
import { PlayerState } from "../../../Types";
import { PlayerBoard } from "./PlayerBoard";
import "./PlayerInspectModal.css";

interface PlayerInspectModalProps {
    player: PlayerState;
    onClose: () => void;
}

export function PlayerInspectModal({ player, onClose }: PlayerInspectModalProps) {
    // Use a ref so the effect doesn't need to re-run when onClose identity changes
    const onCloseRef = useRef(onClose);
    onCloseRef.current = onClose;

    useEffect(() => {
        const handler = (e: KeyboardEvent) => {
            if (e.key === "Escape") onCloseRef.current();
        };
        document.addEventListener("keydown", handler);
        return () => document.removeEventListener("keydown", handler);
    }, []);

    return (
        <div
            className="inspectOverlay"
            onClick={onClose}
            role="dialog"
            aria-modal="true"
            aria-label={`${player.name}'s board`}
        >
            <div className="inspectDrawer" onClick={e => e.stopPropagation()}>
                <div className="inspectDrawer-handle" />
                <div className="inspectDrawer-header">
                    <h3 className="inspectDrawer-title">
                        {player.name}'s Board
                    </h3>
                    <button className="inspectDrawer-close" onClick={onClose} aria-label="Close">✕</button>
                </div>
                <div className="inspectDrawer-body">
                    <PlayerBoard player={player} compact inspectMode />
                </div>
            </div>
        </div>
    );
}
