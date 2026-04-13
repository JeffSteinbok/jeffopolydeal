import React, { useState } from "react";
import { DebugDeckInfo } from "../../../Types";
import { CardComponent } from "./Card";
import { GameSignalRClient } from "../GameSignalRClient";
import "./DebugDeckViewer.css";

interface DebugDeckViewerProps {
    client: GameSignalRClient;
}

export function DebugDeckViewer({ client }: DebugDeckViewerProps) {
    const [deckInfo, setDeckInfo] = useState<DebugDeckInfo | null>(null);
    const [isOpen, setIsOpen] = useState(false);

    const handleOpen = async () => {
        const info = await client.getDebugDeckInfo();
        setDeckInfo(info);
        setIsOpen(true);
    };

    return (
        <>
            <button className="debugDeckButton" onClick={handleOpen} title="Debug: View Deck">
                🔍 Deck
            </button>

            {isOpen && deckInfo && (
                <div className="modalOverlay" onClick={() => setIsOpen(false)}>
                    <div className="debugDeckModal" onClick={(e) => e.stopPropagation()}>
                        <div className="debugDeckHeader">
                            <h3>🔍 Debug Deck Viewer</h3>
                            <button className="secondary" onClick={() => setIsOpen(false)}>✕</button>
                        </div>

                        <div className="debugDeckSection">
                            <h4>Draw Pile ({deckInfo.drawPile.length} cards) — next drawn is last</h4>
                            <div className="debugCardGrid">
                                {deckInfo.drawPile.map((card) => (
                                    <CardComponent key={card.id} card={card} small />
                                ))}
                                {deckInfo.drawPile.length === 0 && <span className="emptyHint">Empty</span>}
                            </div>
                        </div>

                        <div className="debugDeckSection">
                            <h4>Discard Pile ({deckInfo.discardPile.length} cards)</h4>
                            <div className="debugCardGrid">
                                {deckInfo.discardPile.map((card) => (
                                    <CardComponent key={card.id} card={card} small />
                                ))}
                                {deckInfo.discardPile.length === 0 && <span className="emptyHint">Empty</span>}
                            </div>
                        </div>

                        {deckInfo.playerHands.map((ph) => (
                            <div key={ph.playerName} className="debugDeckSection">
                                <h4>{ph.playerName}'s Hand ({ph.cards.length} cards)</h4>
                                <div className="debugCardGrid">
                                    {ph.cards.map((card) => (
                                        <CardComponent key={card.id} card={card} small />
                                    ))}
                                </div>
                            </div>
                        ))}
                    </div>
                </div>
            )}
        </>
    );
}
