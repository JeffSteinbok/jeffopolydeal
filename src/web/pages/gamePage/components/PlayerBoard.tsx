import React from "react";
import { PlayerState } from "../../../Types";
import { CardComponent } from "./Card";
import { PropertyColorMap } from "../../../utilities/PropertyColors";
import "./PlayerBoard.css";

interface PlayerBoardProps {
    player: PlayerState;
    isMe?: boolean;
}

export function PlayerBoard({ player, isMe }: PlayerBoardProps) {
    return (
        <div className={`playerBoard ${isMe ? "playerBoard-me" : ""}`}>
            <div className="playerBoard-header">
                <span className="playerBoard-name">{player.name}{isMe ? " (You)" : ""}</span>
                <span className="playerBoard-cards">🃏 {player.handCount}</span>
                <span className="playerBoard-sets">
                    {player.completedSetCount}/3 sets
                </span>
            </div>

            <div className="playerBoard-sections">
                {/* Bank */}
                <div className="playerBoard-bank">
                    <div className="section-label">
                        Bank: {player.bank.reduce((sum, c) => sum + c.moneyValue, 0)}M
                    </div>
                    <div className="cardRow">
                        {player.bank.map((card) => (
                            <CardComponent key={card.id} card={card} small={!isMe} />
                        ))}
                        {player.bank.length === 0 && <span className="emptyHint">Empty</span>}
                    </div>
                </div>

                {/* Properties */}
                <div className="playerBoard-properties">
                    <div className="section-label">Properties</div>
                    {player.propertySets.map((set) => (
                        <div key={set.color} className="propertySet">
                            <div
                                className="propertySet-label"
                                style={{ backgroundColor: PropertyColorMap[set.color].hex, color: PropertyColorMap[set.color].textColor }}
                            >
                                {PropertyColorMap[set.color].name}
                                {" "}({set.cards.length}/{set.requiredSize})
                                {set.isComplete && " ✓"}
                                {set.hasHouse && " 🏠"}
                                {set.hasHotel && " 🏨"}
                                {" — Rent: " + set.rent + "M"}
                            </div>
                            <div className="cardRow">
                                {set.cards.map((card) => (
                                    <CardComponent key={card.id} card={card} small={!isMe} />
                                ))}
                            </div>
                        </div>
                    ))}
                    {player.propertySets.length === 0 && <span className="emptyHint">No properties</span>}
                </div>
            </div>
        </div>
    );
}
