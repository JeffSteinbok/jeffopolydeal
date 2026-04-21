import React from "react";
import { PlayerState } from "../../../Types";
import { PropertyColorMap } from "../../../utilities/PropertyColors";
import IndicatorSvg from "../../../assets/Indicator.svg";
import HousePng from "../../../assets/HouseSmall.png";
import HotelPng from "../../../assets/HotelSmall.png";
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
            <div className="playerSummary-header">
                <div className="playerSummary-name">
                    {isCurrentTurn && (
                        <span className="playerSummary-turn-dot" aria-label="Current turn" role="img" />
                    )}
                    {player.name}
                    {isCurrentTurn && (
                        <span className="typing-dots"><span>.</span><span>.</span><span>.</span></span>
                    )}
                </div>
                <div className="playerSummary-stats">
                    <span className="playerSummary-stat playerSummary-money"><span className="money-diamond">◆</span>{bankTotal}</span>
                    <span className="playerSummary-stat"><img src={IndicatorSvg} alt="cards" className="playerSummary-cardIcon" /> {player.handCount}</span>
                    <span className="playerSummary-chevron" />
                </div>
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
                        title={`${PropertyColorMap[set.color].name} ${set.cards.length}/${set.requiredSize}${set.hasHouse ? " House" : ""}${set.hasHotel ? " Hotel" : ""}`}
                    >
                        {set.cards.length}/{set.requiredSize}
                        {set.isComplete && <span className="playerSummary-setpill-check">✓</span>}
                        {set.hasHotel ? <img src={HotelPng} alt="Hotel" className="playerSummary-setpill-building" />
                            : set.hasHouse ? <img src={HousePng} alt="House" className="playerSummary-setpill-building" />
                            : null}
                    </span>
                ))}
                {player.propertySets.length === 0 && (
                    <span className="playerSummary-no-props">no props</span>
                )}
            </div>
        </div>
    );
}
