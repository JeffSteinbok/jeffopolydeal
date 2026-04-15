import React from "react";
import { Card as CardType, PropertyColor } from "../../../Types";
import { PropertyColorMap } from "../../../utilities/PropertyColors";
import { GameConfig } from "../../../utilities/GameConfig";
import "./Card.css";

interface CardProps {
    card: CardType;
    onClick?: () => void;
    onDoubleClick?: () => void;
    selected?: boolean;
    small?: boolean;
    tiny?: boolean;
    currentRent?: number;
}

// Background color keyed to money value (matching real cards)
const VALUE_COLORS: Record<number, string> = {
    0: "#c8c8c8",
    1: "#f0ecc8",
    2: "#e8c8b0",
    3: "#d4e4bc",
    4: "#b8d4e8",
    5: "#8b7bb5",
    10: "#e8c870",
};

function cardBg(card: CardType): string {
    if (card.cardType === "Money") return VALUE_COLORS[card.moneyValue] ?? "#a0c8a0";
    if (card.cardType === "Property") {
        const color = card.activeColor ?? card.color;
        return color ? PropertyColorMap[color].hex : "#bbb";
    }
    if (card.cardType === "PropertyWildcard") return "#fff";
    return VALUE_COLORS[card.moneyValue] ?? "#c8c8c8";
}

function borderColor(card: CardType): string {
    if (card.cardType === "Property" || card.cardType === "PropertyWildcard") {
        const color = card.activeColor ?? card.color;
        return color ? PropertyColorMap[color].hex : "#888";
    }
    return "#888";
}

export function CardComponent({ card, onClick, onDoubleClick, selected, small, tiny, currentRent }: CardProps) {
    const cls = [
        "md-card",
        tiny ? "md-card--xs" : small ? "md-card--sm" : "",
        selected ? "md-card--selected" : "",
        onClick ? "md-card--clickable" : "",
    ].filter(Boolean).join(" ");

    const bg = cardBg(card);
    const bc = borderColor(card);
    const isMoney = card.cardType === "Money";
    const isProp = card.cardType === "Property" || card.cardType === "PropertyWildcard";
    const badgeBg = isProp ? "#fff" : bg;
    const badgeLight = isProp;

    return (
        <div className={cls} onClick={onClick} onDoubleClick={onDoubleClick} style={{ backgroundColor: bg, borderColor: bc }}>
            <div className="md-card__inner">
                {renderCard(card, small || tiny, tiny, currentRent)}
            </div>
            {/* Corner badge */}
            {card.moneyValue > 0 && <Badge value={card.moneyValue} bg={badgeBg} light={badgeLight} />}
        </div>
    );
}

function renderCard(card: CardType, small?: boolean, tiny?: boolean, currentRent?: number) {
    switch (card.cardType) {
        case "Money": return <MoneyLayout card={card} />;
        case "Property":
            if (tiny && currentRent !== undefined) return <TinyPropertyLayout card={card} rent={currentRent} />;
            return <PropertyLayout card={card} small={small} />;
        case "PropertyWildcard":
            if (tiny && currentRent !== undefined) return <TinyWildcardLayout card={card} rent={currentRent} />;
            return <WildcardLayout card={card} small={small} />;
        case "Rent": return <RentLayout card={card} />;
        case "Action": return <ActionLayout card={card} />;
    }
}

/* ── Money ─────────────────────────────────────── */
function MoneyLayout({ card }: { card: CardType }) {
    return (
        <>
            <div className="md-card__body-center">
                <div className="md-money__amount">
                    <span className="md-money__sym">M</span>{card.moneyValue}
                </div>
            </div>
        </>
    );
}

/* ── Property ──────────────────────────────────── */
function PropertyLayout({ card, small }: { card: CardType; small?: boolean }) {
    const color = card.color!;
    const info = PropertyColorMap[color];
    const rents = GameConfig.rentTable[color];
    const setSize = GameConfig.setSize[color];

    return (
        <>
            <div className="md-card__name-band" style={{ color: info.textColor }}>
                {card.name}
            </div>
            <div className="md-card__body--rent">
                <RentTable rents={rents} setSize={setSize} color={info.hex} />
            </div>
        </>
    );
}

/* ── Property Wildcard ─────────────────────────── */
const RAINBOW_COLORS = [
    PropertyColorMap.Brown.hex,
    PropertyColorMap.Pink.hex,
    PropertyColorMap.Orange.hex,
    PropertyColorMap.Red.hex,
    PropertyColorMap.Yellow.hex,
    PropertyColorMap.Green.hex,
    PropertyColorMap.DarkBlue.hex,
    PropertyColorMap.LightBlue.hex,
    PropertyColorMap.Railroad.hex,
    PropertyColorMap.Utility.hex,
];

function RainbowBar() {
    return (
        <div className="md-wild__bar">
            {RAINBOW_COLORS.map((c, i) => (
                <span key={i} className="md-wild__bar-block" style={{ backgroundColor: c }} />
            ))}
        </div>
    );
}

function WildcardLayout({ card, small }: { card: CardType; small?: boolean }) {
    if (card.isMulticolorWild) {
        return (
            <>
                <RainbowBar />
                <div className="md-wild__title-box">Property Wild Card</div>
                <RainbowBar />
                <div className="md-card__body-center">
                    <div className="md-wild__placeholder">🎩</div>
                </div>
                <div className="md-wild__desc-text">
                    This card can be used as part of any property set. This card has no monetary value.
                </div>
            </>
        );
    }

    const c1 = PropertyColorMap[card.color!];
    const c2 = PropertyColorMap[card.altColor!];
    const isFlipped = card.activeColor === card.altColor;
    const activeColor = isFlipped ? card.altColor! : card.color!;
    const inactiveColor = isFlipped ? card.color! : card.altColor!;
    const activeInfo = PropertyColorMap[activeColor];
    const inactiveInfo = PropertyColorMap[inactiveColor];
    const activeRents = GameConfig.rentTable[activeColor];
    const activeSetSize = GameConfig.setSize[activeColor];

    return (
        <>
            <div className="md-wild-dual">
                {/* Active color header */}
                <div className="md-wild-dual__header" style={{ backgroundColor: activeInfo.hex }}>
                    <div className="md-wild-dual__pretitle">Property</div>
                    <div className="md-wild-dual__title">Wild Card</div>
                    <div className="md-wild-dual__subtitle">(Use card either way up.)</div>
                </div>
                {/* Rent area — only active color */}
                <div className="md-wild-dual__rent-shared">
                    <RentTable rents={activeRents} setSize={activeSetSize} color={activeInfo.hex} />
                </div>
                {/* Inactive color header (upside down) */}
                <div className="md-wild-dual__header md-wild-dual__header--bottom" style={{ backgroundColor: inactiveInfo.hex }}>
                    <div className="md-wild-dual__pretitle">Property</div>
                    <div className="md-wild-dual__title">Wild Card</div>
                    <div className="md-wild-dual__subtitle">(Use card either way up.)</div>
                </div>
            </div>
        </>
    );
}

/* ── Tiny property layouts (mobile compact view) ── */
function TinyPropertyLayout({ card, rent }: { card: CardType; rent: number }) {
    const color = card.color!;
    const info = PropertyColorMap[color];
    return (
        <>
            <div className="md-card__name-band md-card__name-band--tiny" style={{ color: info.textColor }}>{card.name}</div>
            <div className="md-card__body-center">
                <div className="md-tiny__rent"><span className="md-sym">M</span>{rent}</div>
            </div>
        </>
    );
}

function TinyWildcardLayout({ card, rent }: { card: CardType; rent: number }) {
    if (card.isMulticolorWild) {
        return (
            <>
                <RainbowBar />
                <div className="md-wild__title-box md-wild__title-box--tiny">Wild Card</div>
                <div className="md-card__body-center">
                    <div className="md-tiny__rent">🎩</div>
                </div>
            </>
        );
    }
    const c1 = PropertyColorMap[card.color!];
    const c2 = PropertyColorMap[card.altColor!];
    return (
        <>
            <div className="md-card__header">Wild Card</div>
            <div className="md-tiny__dual">
                <div className="md-tiny__dual-top" style={{ backgroundColor: c1.hex }} />
                <div className="md-tiny__rent md-tiny__rent--overlay"><span className="md-sym">M</span>{rent}</div>
                <div className="md-tiny__dual-bot" style={{ backgroundColor: c2.hex }} />
            </div>
        </>
    );
}

/* ── Rent ──────────────────────────────────────── */
function RentLayout({ card }: { card: CardType }) {
    const segmentSize = 360 / RAINBOW_COLORS.length;
    const conicStops = RAINBOW_COLORS.map((c, i) =>
        `${c} ${i * segmentSize}deg ${(i + 1) * segmentSize}deg`
    ).join(", ");

    if (card.isWildRent) {
        return (
            <>
                <div className="md-card__header">Action Card</div>
                <div className="md-card__body-center">
                    <div className="md-rent-ring" style={{
                        background: `conic-gradient(${conicStops})`,
                    }}>
                        <div className="md-rent-ring__inner">
                            <span className="md-card__oval-text">Rent</span>
                        </div>
                    </div>
                </div>
                <div className="md-card__desc">Any color — charge 1 player</div>
            </>
        );
    }

    const colors = card.rentColors ?? [];
    const hex1 = colors[0] ? PropertyColorMap[colors[0]].hex : "#888";
    const hex2 = colors[1] ? PropertyColorMap[colors[1]].hex : "#888";

    return (
        <>
            <div className="md-card__header">Action Card</div>
            <div className="md-card__body-center">
                <div className="md-rent-ring" style={{
                    background: `linear-gradient(to bottom, ${hex1} 50%, ${hex2} 50%)`,
                }}>
                    <div className="md-rent-ring__inner">
                        <span className="md-card__oval-text">Rent</span>
                    </div>
                </div>
            </div>
            <div className="md-card__desc">Charge all players</div>
        </>
    );
}

/* ── Action ────────────────────────────────────── */
const ACTION_META: Record<string, { title: string; desc: string }> = {
    PassGo:        { title: "Pass Go",            desc: "Draw 2 extra cards" },
    DebtCollector: { title: "Debt Collector",      desc: "Any player pays you M5" },
    ItsMyBirthday: { title: "It's My Birthday",    desc: "All players pay you M2" },
    SlyDeal:       { title: "Sly Deal",            desc: "Steal 1 property" },
    ForceDeal:     { title: "Forced Deal",         desc: "Swap properties with any player" },
    DealBreaker:   { title: "Deal Breaker",        desc: "Steal a complete set!" },
    JustSayNo:     { title: "Just Say No!",        desc: "Cancel any action against you" },
    DoubleTheRent: { title: "Double The Rent!",    desc: "Play with a rent card" },
    House:         { title: "House",               desc: "+M3 rent on a complete set" },
    Hotel:         { title: "Hotel",               desc: "+M4 rent (needs house)" },
};

function ActionLayout({ card }: { card: CardType }) {
    const meta = ACTION_META[card.actionKind ?? ""] ?? { title: card.name, desc: "" };

    return (
        <>
            <div className="md-card__header">Action Card</div>
            <div className="md-card__body-center">
                <div className="md-card__oval">
                    <span className="md-card__oval-text">{meta.title}</span>
                </div>
            </div>
            <div className="md-card__desc">{meta.desc}</div>
        </>
    );
}

/* ── Shared parts ──────────────────────────────── */
function Badge({ value, bg, light }: { value: number; bg: string; light?: boolean }) {
    const cls = `md-badge ${light ? "md-badge--light" : ""}`;
    return <div className={cls} style={{ backgroundColor: bg }}><span className="md-sym">M</span>{value}</div>;
}

function RentTable({ rents, setSize, color }: { rents: number[]; setSize: number; color: string }) {
    return (
        <table className="md-rent-tbl">
            <thead>
                <tr><th colSpan={2} className="md-rent-tbl__hdr">RENT</th></tr>
            </thead>
            <tbody>
                {rents.slice(1).map((rent, i) => {
                    const n = i + 1;
                    const full = n === setSize;
                    return (
                        <tr key={n} className={full ? "md-rent-tbl__full" : ""}>
                            <td className="md-rent-tbl__icons">
                                <span className="md-rent-tbl__card-icon">{n}</span>
                            </td>
                            <td className="md-rent-tbl__val">
                                {full && <span className="md-rent-tbl__label">FULL SET </span>}
                                <span className="md-sym">M</span>{rent}
                            </td>
                        </tr>
                    );
                })}
            </tbody>
        </table>
    );
}
