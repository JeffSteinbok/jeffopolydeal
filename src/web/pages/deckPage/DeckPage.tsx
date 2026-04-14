import React, { useEffect, useState } from "react";
import { Card } from "../../Types";
import { CardComponent } from "../gamePage/components/Card";
import "./DeckPage.css";

type ViewMode = "tiny" | "small" | "full";

interface CardGroup {
    label: string;
    cards: Card[];
}

function groupCards(cards: Card[]): CardGroup[] {
    const groups: CardGroup[] = [];
    const money = cards.filter((c) => c.cardType === "Money");
    const props = cards.filter((c) => c.cardType === "Property");
    const wilds = cards.filter((c) => c.cardType === "PropertyWildcard");
    const rents = cards.filter((c) => c.cardType === "Rent");
    const actions = cards.filter((c) => c.cardType === "Action");

    if (money.length) groups.push({ label: `Money (${money.length})`, cards: money });
    if (props.length) groups.push({ label: `Properties (${props.length})`, cards: props });
    if (wilds.length) groups.push({ label: `Property Wildcards (${wilds.length})`, cards: wilds });
    if (rents.length) groups.push({ label: `Rent (${rents.length})`, cards: rents });
    if (actions.length) groups.push({ label: `Actions (${actions.length})`, cards: actions });

    return groups;
}

export function DeckPage() {
    const [cards, setCards] = useState<Card[]>([]);
    const [loading, setLoading] = useState(true);
    const [viewMode, setViewMode] = useState<ViewMode>("small");

    useEffect(() => {
        fetch("/api/deck")
            .then((r) => r.json())
            .then((data: Card[]) => {
                setCards(data);
                setLoading(false);
            })
            .catch(() => setLoading(false));
    }, []);

    if (loading) return <div className="deckPage">Loading deck...</div>;

    const groups = groupCards(cards);

    return (
        <div className="deckPage">
            <div className="deckPage-header">
                <h1>Jeffopoly Deal — Full Deck ({cards.length} cards)</h1>
                <div className="deckPage-controls">
                    <label>View:</label>
                    {(["tiny", "small", "full"] as ViewMode[]).map((mode) => (
                        <button
                            key={mode}
                            className={viewMode === mode ? "active" : ""}
                            onClick={() => setViewMode(mode)}
                        >
                            {mode.charAt(0).toUpperCase() + mode.slice(1)}
                        </button>
                    ))}
                </div>
            </div>

            {groups.map((group) => (
                <div key={group.label} className="deckPage-group">
                    <h2>{group.label}</h2>
                    <div className={`deckPage-grid deckPage-grid--${viewMode}`}>
                        {group.cards.map((card) => (
                            <div key={card.id} className="deckPage-cardWrapper">
                                <CardComponent
                                    card={card}
                                    tiny={viewMode === "tiny"}
                                    small={viewMode === "small"}
                                />
                                <span className="deckPage-cardId">#{card.id}</span>
                            </div>
                        ))}
                    </div>
                </div>
            ))}
        </div>
    );
}
