import React, { useEffect, useState } from "react";
import { Card } from "../../Types";
import { CardComponent } from "../gamePage/components/Card";
import { GameConfig } from "../../utilities/GameConfig";
import "./DeckPage.css";

type ViewMode = "tiny" | "small" | "full";

const VIEW_MODES: ViewMode[] = ["tiny", "small", "full"];

function getViewFromHash(): ViewMode {
    const hash = window.location.hash.replace("#", "");
    if (VIEW_MODES.includes(hash as ViewMode)) return hash as ViewMode;
    return "full";
}

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

function getRent(card: Card): number | undefined {
    const color = card.activeColor ?? card.color;
    if (!color) return undefined;
    const rents = GameConfig.rentTable[color];
    return rents ? rents[1] ?? 0 : undefined;
}

export function DeckPage() {
    const [cards, setCards] = useState<Card[]>([]);
    const [loading, setLoading] = useState(true);
    const [viewMode, setViewMode] = useState<ViewMode>(getViewFromHash());

    const changeView = (mode: ViewMode) => {
        setViewMode(mode);
        window.location.hash = mode;
    };

    useEffect(() => {
        const themeParam = new URLSearchParams(window.location.search).get("theme");
        const url = themeParam ? `/api/deck?theme=${encodeURIComponent(themeParam)}` : "/api/deck";
        fetch(url)
            .then((r) => r.json())
            .then((data: Card[]) => {
                setCards(data);
                setLoading(false);
            })
            .catch(() => setLoading(false));
    }, []);

    if (loading) return <div className="deckPage">Loading deck...</div>;

    const groups = groupCards(cards);
    const isTiny = viewMode === "tiny";

    return (
        <div className="deckPage">
            <div className="deckPage-header">
                <h1>Jeffopoly Deal — Full Deck ({cards.length} cards)</h1>
                <div className="deckPage-controls">
                    <label>View:</label>
                    {VIEW_MODES.map((mode) => (
                        <button
                            key={mode}
                            className={viewMode === mode ? "active" : ""}
                            onClick={() => changeView(mode)}
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
                                    compact={isTiny}
                                    small={viewMode === "small"}
                                    currentRent={isTiny ? getRent(card) : undefined}
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
