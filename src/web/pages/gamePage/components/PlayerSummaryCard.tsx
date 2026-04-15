import React from "react";
import { PlayerState } from "../../../Types";
import { PropertyColorMap } from "../../../utilities/PropertyColors";
import "./PlayerSummaryCard.css";

interface PlayerSummaryCardProps {
    player: PlayerState;
    isCurrentTurn?: boolean;
    onClick: () => void;
}

export function PlayerSummaryCard({ player, isCurrentTurn, onClick }: PlayerSummaryCardProps) {
    const bankTotal = player.bank.reduce((sum, c) => sum + c.moneyValue, 0);

    const handleKeyDown = (e: React.KeyboardEvent) => {
        if (e.key === "Enter" || e.key === " ") {
            e.preventDefault();
            onClick();
        }
    };

    return (
        <div
            className={`playerSummary${isCurrentTurn ? " playerSummary--active" : ""}`}
            onClick={onClick}
            onKeyDown={handleKeyDown}
            role="button"
            tabIndex={0}
            aria-label={`View ${player.name}'s board${isCurrentTurn ? " (their turn)" : ""}`}
        >
            <div className="playerSummary-name">
                {isCurrentTurn && (
                    <span className="playerSummary-turn-dot" aria-label="Current turn" role="img" />
                )}
                {player.name}
            </div>
            <div className="playerSummary-row">
                <span className="playerSummary-stat playerSummary-money">💰 {bankTotal}</span>
                <span className="playerSummary-stat">🃏 {player.handCount}</span>
            </div>
            <div className="playerSummary-setpills">
                {player.propertySets.map((set, i) => (
                    <span
                        key={i}
                        className={`playerSummary-setpill${set.isComplete ? " playerSummary-setpill--complete" : ""}`}
                        style={{
                            backgroundColor: PropertyColorMap[set.color].hex,
                            color: PropertyColorMap[set.color].textColor,
                        }}
                        title={`${PropertyColorMap[set.color].name} ${set.cards.length}/${set.requiredSize}${set.hasHouse ? " 🏠" : ""}${set.hasHotel ? " 🏨" : ""}`}
                    >
                        {set.cards.length}/{set.requiredSize}
                        {set.isComplete && <span className="playerSummary-setpill-check">✓</span>}
                        {set.hasHotel ? <span className="playerSummary-setpill-icon">🏨</span>
                            : set.hasHouse ? <span className="playerSummary-setpill-icon">🏠</span>
                            : null}
                    </span>
                ))}
                {player.propertySets.length === 0 && (
                    <span className="playerSummary-no-props">no props</span>
                )}
            </div>
            <span className="playerSummary-hint">tap ›</span>
        </div>
    );
}
