import React from "react";
import { Card as CardType, PropertyColor } from "../../../Types";
import { PropertyColorMap } from "../../../utilities/PropertyColors";
import { GameConfig } from "../../../utilities/GameConfig";
import CurrencyBackground from "../../../assets/CurrencyBackground.png";
import PassGoArrow from "../../../assets/PassGo.png";
import HouseImg from "../../../assets/House.png";
import HotelImg from "../../../assets/Hotel.png";
import BirthdayImg from "../../../assets/Birthday.png";
import { RentIcon } from "./RentIcon";
import IndicatorSvg from "../../../assets/Indicator.svg";
import "./Card.css";

/** For dark text on light backgrounds, use a subtle white shadow instead of a dark one. */
function textShadowFor(textColor: string): string {
    return textColor === "#000" ? "0 1px 2px rgba(255,255,255,0.5)" : "0 1px 2px rgba(0,0,0,0.3)";
}

interface CardProps {
    card: CardType;
    onClick?: () => void;
    onDoubleClick?: () => void;
    selected?: boolean;
    small?: boolean;
    compact?: boolean;
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

export function CardComponent({ card, onClick, onDoubleClick, selected, small, compact, currentRent }: CardProps) {
    const cls = [
        "md-card",
        compact ? "md-card--xs" : small ? "md-card--sm" : "",
        selected ? "md-card--selected" : "",
        onClick ? "md-card--clickable" : "",
    ].filter(Boolean).join(" ");

    const bg = cardBg(card);
    const bc = borderColor(card);
    const isMoney = card.cardType === "Money";
    const isRegularProp = card.cardType === "Property";
    const isPropWild = card.cardType === "PropertyWildcard";
    const isProp = isRegularProp || isPropWild;
    const badgeBg = isProp ? "#fff" : bg;
    const badgeLight = isProp;
    const badgeBorder = isRegularProp ? bc : "rgba(0,0,0,0.2)";

    const outerBg = isRegularProp ? "#fff" : bg;
    const innerBg = isRegularProp ? bg : undefined;

    return (
        <div className={cls} onClick={onClick} onDoubleClick={onDoubleClick} style={{ backgroundColor: outerBg }}>
            <div className="md-card__inner" style={innerBg ? { backgroundColor: innerBg } : undefined}>
                {renderCard(card, small || compact, compact, currentRent)}
            </div>
            {/* Corner badge */}
            {card.moneyValue > 0 && <Badge value={card.moneyValue} bg={badgeBg} light={badgeLight} compact={!!(small || compact)} borderColor={badgeBorder} />}
        </div>
    );
}

function renderCard(card: CardType, small?: boolean, compact?: boolean, currentRent?: number) {
    switch (card.cardType) {
        case "Money": return <MoneyLayout card={card} />;
        case "Property":
            if (compact && currentRent !== undefined) return <TinyPropertyLayout card={card} rent={currentRent} />;
            return <PropertyLayout card={card} small={small} />;
        case "PropertyWildcard":
            if (compact && currentRent !== undefined) return <TinyWildcardLayout card={card} rent={currentRent} />;
            return <WildcardLayout card={card} small={small} />;
        case "Rent": return <RentLayout card={card} />;
        case "Action": return <ActionLayout card={card} small={small} compact={compact} />;
    }
}

/* ── Money ─────────────────────────────────────── */
function MoneyLayout({ card }: { card: CardType }) {
    return (
        <>
            <img src={CurrencyBackground} className="md-money__watermark" alt="" />
            <div className="md-card__body-center">
                <div className="md-money__amount">
                    ◆{card.moneyValue}
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
            <div className="md-card__name-band" style={{ color: info.textColor, textShadow: textShadowFor(info.textColor) }}>
                {card.name}
            </div>
            <div className="md-card__body--rent">
                <RentTable rents={rents} setSize={setSize} color={info.hex} small={small} />
            </div>
        </>
    );
}

/* ── Property Wildcard ─────────────────────────── */
const RAINBOW_COLORS = [
    PropertyColorMap.Brown.hex,
    PropertyColorMap.LightBlue.hex,
    PropertyColorMap.Pink.hex,
    PropertyColorMap.Orange.hex,
    PropertyColorMap.Red.hex,
    PropertyColorMap.Yellow.hex,
    PropertyColorMap.Green.hex,
    PropertyColorMap.DarkBlue.hex,
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
    const inactiveRents = GameConfig.rentTable[inactiveColor];
    const inactiveSetSize = GameConfig.setSize[inactiveColor];

    return (
        <>
            <div className="md-wild-dual">
                {/* Active color header */}
                <div className="md-wild-dual__header" style={{ backgroundColor: activeInfo.hex, color: activeInfo.textColor, textShadow: textShadowFor(activeInfo.textColor) }}>
                    <div className="md-wild-dual__pretitle">Property</div>
                    <div className="md-wild-dual__title">Wild Card</div>
                    <div className="md-wild-dual__subtitle">(Use card either way up.)</div>
                </div>
                {/* Rent area: label + tables + flipped label */}
                <div className="md-wild-dual__rent-area">
                    <div className="md-wild-dual__rent-label">RENT</div>
                    <div className="md-wild-dual__rent-row">
                        <div className="md-wild-dual__rent-side md-wild-dual__rent--flipped">
                            <RentTable rents={inactiveRents} setSize={inactiveSetSize} color={inactiveInfo.hex} hideHeader isWildcard />
                        </div>
                        <div className="md-wild-dual__rent-side">
                            <RentTable rents={activeRents} setSize={activeSetSize} color={activeInfo.hex} hideHeader isWildcard />
                        </div>
                    </div>
                    <div className="md-wild-dual__rent-label md-wild-dual__rent--flipped">RENT</div>
                </div>
                {/* Inactive color header (upside down) */}
                <div className="md-wild-dual__header md-wild-dual__header--bottom" style={{ backgroundColor: inactiveInfo.hex, color: inactiveInfo.textColor, textShadow: textShadowFor(inactiveInfo.textColor) }}>
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
            <div className="md-card__name-band md-card__name-band--tiny" style={{ color: info.textColor, textShadow: textShadowFor(info.textColor) }}>{card.name}</div>
            <div className="md-card__body-center">
                <div className="md-tiny__rent" style={{ color: info.textColor, textShadow: textShadowFor(info.textColor) }}>◆{rent}</div>
            </div>
        </>
    );
}

function TinyWildcardLayout({ card, rent }: { card: CardType; rent: number }) {
    if (card.isMulticolorWild) {
        return (
            <>
                <RainbowBar />
                <div className="md-wild__title-box md-wild__title-box--tiny">PROPERTY<br/>WILD CARD</div>
                <div className="md-card__body-center">
                    <div className="md-tiny__rent md-tiny__rent--overlay">◆{rent}</div>
                </div>
            </>
        );
    }
    const isFlipped = card.activeColor === card.altColor;
    const topColor = PropertyColorMap[isFlipped ? card.altColor! : card.color!];
    const botColor = PropertyColorMap[isFlipped ? card.color! : card.altColor!];
    return (
        <>
            <div className="md-card__header md-card__header--tiny">PROPERTY<br/>WILD CARD</div>
            <div className="md-tiny__dual">
                <div className="md-tiny__dual-top" style={{ backgroundColor: topColor.hex }} />
                <div className="md-tiny__rent md-tiny__rent--overlay">◆{rent}</div>
                <div className="md-tiny__dual-bot" style={{ backgroundColor: botColor.hex }} />
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
    DebtCollector: { title: "Debt Collector",      desc: "Any player pays you 5" },
    ItsMyBirthday: { title: "It's My Birthday",    desc: "All players pay you 2" },
    SlyDeal:       { title: "Sly Deal",            desc: "Steal 1 property" },
    ForceDeal:     { title: "Forced Deal",         desc: "Swap properties with any player" },
    DealBreaker:   { title: "Deal Breaker",        desc: "Steal a complete set!" },
    JustSayNo:     { title: "Just Say No!",        desc: "Cancel any action against you" },
    DoubleTheRent: { title: "Double The Rent!",    desc: "Play with a rent card" },
    House:         { title: "House",               desc: "+3 rent on a complete set" },
    Hotel:         { title: "Hotel",               desc: "+4 rent (needs house)" },
};

function ActionLayout({ card, small, compact }: { card: CardType; small?: boolean; compact?: boolean }) {
    const meta = ACTION_META[card.actionKind ?? ""] ?? { title: card.name, desc: "" };
    const kind = card.actionKind ?? "";
    const xs = !!compact;

    let ovalContent: React.ReactNode;

    switch (kind) {
        case "PassGo":
            ovalContent = (
                <div style={{ display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center", gap: 0 }}>
                    <div style={{ fontFamily: "Inter, sans-serif", fontWeight: 900, fontSize: xs ? 5 : small ? 8 : 14, textTransform: "uppercase", letterSpacing: xs ? 0 : small ? 1 : 2, color: "#222", textAlign: "center", lineHeight: 1 }}>PASS</div>
                    <div style={{ fontFamily: "Inter, sans-serif", fontWeight: 900, fontSize: xs ? 9 : small ? 18 : 30, textTransform: "uppercase", letterSpacing: xs ? 0 : small ? 1 : 3, color: "#222", textAlign: "center", lineHeight: 1 }}>GO</div>
                    <img src={PassGoArrow} style={{ width: xs ? 18 : small ? 32 : 60, height: "auto", marginTop: xs ? 0 : small ? 1 : 2 }} alt="Go" />
                </div>
            );
            break;
        case "House":
            ovalContent = (
                <div style={{ display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center", gap: 0 }}>
                    <img src={HouseImg} style={{ width: xs ? 18 : small ? 28 : 50, height: "auto" }} alt="House" />
                    <div style={{ fontFamily: "Inter, sans-serif", fontWeight: 900, fontSize: xs ? 5 : small ? 9 : 16, textTransform: "uppercase", color: "#222", textAlign: "center", lineHeight: 1 }}>HOUSE</div>
                </div>
            );
            break;
        case "Hotel":
            ovalContent = (
                <div style={{ display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center", gap: 0 }}>
                    <img src={HotelImg} style={{ width: xs ? 18 : small ? 28 : 50, height: "auto" }} alt="Hotel" />
                    <div style={{ fontFamily: "Inter, sans-serif", fontWeight: 900, fontSize: xs ? 5 : small ? 9 : 16, textTransform: "uppercase", color: "#222", textAlign: "center", lineHeight: 1 }}>HOTEL</div>
                </div>
            );
            break;
        case "ItsMyBirthday":
            ovalContent = (
                <div style={{ display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center", gap: 0 }}>
                    <div style={{ fontFamily: "Inter, sans-serif", fontWeight: 900, fontSize: xs ? 4 : small ? 6 : 11, textTransform: "uppercase", letterSpacing: xs ? 0 : 1, color: "#222", textAlign: "center", lineHeight: 1.2 }}>IT'S MY</div>
                    <div style={{ fontFamily: "Inter, sans-serif", fontWeight: 900, fontSize: xs ? 5 : small ? 7 : 13, textTransform: "uppercase", letterSpacing: xs ? 0 : 1, color: "#222", textAlign: "center", lineHeight: 1 }}>BIRTHDAY</div>
                    <img src={BirthdayImg} style={{ width: xs ? 12 : small ? 22 : 40, height: "auto", marginTop: xs ? 0 : small ? 1 : 2 }} alt="Birthday" />
                </div>
            );
            break;
        case "DebtCollector":
            ovalContent = <span className="md-card__oval-text" style={{ fontSize: xs ? 5 : small ? 7 : 12 }}>{meta.title}</span>;
            break;
        default:
            ovalContent = <span className="md-card__oval-text" style={{ fontSize: xs ? 5 : undefined }}>{meta.title}</span>;
            break;
    }

    return (
        <>
            <div className="md-card__header">Action Card</div>
            <div className="md-card__body-center">
                <div className="md-card__oval">
                    {ovalContent}
                </div>
            </div>
            <div className="md-card__desc">{meta.desc}</div>
        </>
    );
}

/* ── Shared parts ──────────────────────────────── */
function Badge({ value, bg, light, compact, borderColor }: { value: number; bg: string; light?: boolean; compact?: boolean; borderColor?: string }) {
    const cls = `md-badge ${light ? "md-badge--light" : ""}`;
    return <div className={cls} style={{ backgroundColor: bg, borderColor }}><span className="md-badge__num">{value}</span></div>;
}

function RentTable({ rents, setSize, color, reversed, hideHeader, small, isWildcard }: { rents: number[]; setSize: number; color: string; reversed?: boolean; hideHeader?: boolean; small?: boolean; isWildcard?: boolean }) {
    return (
        <table className="md-rent-tbl">
            {!hideHeader && (
                <thead>
                    <tr><th colSpan={2} className="md-rent-tbl__hdr">RENT</th></tr>
                </thead>
            )}
            <tbody>
                {rents.slice(1).map((rent, i) => {
                    const n = i + 1;
                    const full = n === setSize;
                    const label = (small || isWildcard) ? "SET " : "FULL SET ";
                    const iconCell = (
                        <td className="md-rent-tbl__icons">
                            <RentIcon count={n} color={color} className="md-rent-tbl__rent-icon" />
                        </td>
                    );
                    const valCell = (
                        <td className="md-rent-tbl__val">
                            ◆{rent}
                        </td>
                    );
                    return (
                        <tr key={n} className={full ? "md-rent-tbl__full" : ""}>
                            {reversed ? <>{valCell}{iconCell}</> : <>{iconCell}{valCell}</>}
                        </tr>
                    );
                })}
            </tbody>
        </table>
    );
}
