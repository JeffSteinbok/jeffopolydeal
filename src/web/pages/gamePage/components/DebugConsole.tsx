import React, { useState, useRef, useEffect } from "react";
import { GameSignalRClient } from "../GameSignalRClient";
import { GameAction, Card, PropertyColor } from "../../../Types";
import { PropertyColorMap } from "../../../utilities/PropertyColors";
import "./DebugConsole.css";

const COMMANDS: Record<string, string[]> = {
    "/give": ["rent", "money", "property", "house", "hotel", "passgo", "dealbreaker", "forcedeal", "slydeal", "debtcollector", "birthday", "justsayno", "doublerent", "wild"],
    "/giveto": [],
    "/bank": ["money", "rent", "property", "house", "hotel", "passgo", "dealbreaker", "forcedeal", "slydeal", "debtcollector", "birthday", "justsayno", "doublerent", "wild"],
    "/clear": ["hand", "bank"],
    "/clearto": [],
    "/myturn": [],
    "/skip": [],
    "/toast": ["banked", "placed", "rent", "rentwild", "rentpaid", "slydeal", "forcedeal", "dealbreaker", "birthday", "passgo"],
};

const TOAST_ARGS: Record<string, string[]> = {
    banked: ["1", "2", "3", "4", "5", "10"],
    placed: ["brown", "lightblue", "pink", "orange", "red", "yellow", "green", "darkblue", "railroad", "utility"],
    rent: ["brown", "lightblue", "pink", "orange", "red", "yellow", "green", "darkblue"],
    rentwild: ["brown", "lightblue", "pink", "orange", "red", "yellow", "green", "darkblue"],
    rentpaid: ["1", "2", "3", "4", "5"],
    slydeal: ["brown", "lightblue", "pink", "orange", "red", "yellow", "green", "darkblue", "railroad", "utility"],
    forcedeal: ["brown", "lightblue", "pink", "orange", "red", "yellow", "green", "darkblue", "railroad", "utility"],
    dealbreaker: ["brown", "lightblue", "pink", "orange", "red", "yellow", "green", "darkblue"],
};

const COLORS = ["brown", "lightblue", "pink", "orange", "red", "yellow", "green", "darkblue", "railroad", "utility"];

function getSuggestions(input: string, playerNames: string[]): string[] {
    const parts = input.toLowerCase().split(" ");
    const base = parts[0] || "";

    if (parts.length === 1) {
        return Object.keys(COMMANDS).filter(c => c.startsWith(base) && c !== base);
    }

    const cmd = base;
    if (!(cmd in COMMANDS)) return [];

    // giveto/clearto: part[1] is player name, part[2] is card type, part[3] is color
    if (cmd === "/giveto" || cmd === "/clearto") {
        const opts = cmd === "/giveto" ? COMMANDS["/give"] : COMMANDS["/clear"];
        if (parts.length === 2) {
            const namePart = parts[1];
            return playerNames.filter(n => n.toLowerCase().startsWith(namePart) && n.toLowerCase() !== namePart);
        }
        if (parts.length === 3) {
            const sub = parts[2];
            return opts.filter(s => s.toLowerCase().startsWith(sub) && s.toLowerCase() !== sub).map(s => s.toLowerCase());
        }
        if (cmd === "/giveto" && parts.length === 4 && ["rent", "property", "wild"].includes(parts[2])) {
            const colorPart = parts[3];
            return COLORS.filter(c => c.startsWith(colorPart) && c !== colorPart);
        }
        return [];
    }

    const subcommands = COMMANDS[cmd] || [];
    if (parts.length === 2) {
        const sub = parts[1];
        return subcommands.filter(s => s.toLowerCase().startsWith(sub) && s.toLowerCase() !== sub);
    }

    // Color arg for give/bank rent/property/wild
    if ((cmd === "/give" || cmd === "/bank") && parts.length === 3 && ["rent", "property", "wild"].includes(parts[1])) {
        const colorPart = parts[2];
        return COLORS.filter(c => c.startsWith(colorPart) && c !== colorPart);
    }

    // Toast args: toast [type] [arg] [extra]
    if (cmd === "/toast" && parts.length === 3) {
        const toastType = parts[1];
        const argPart = parts[2];
        const args = TOAST_ARGS[toastType] || [];
        return args.filter(a => a.startsWith(argPart) && a !== argPart);
    }
    if (cmd === "/toast" && parts.length === 4 && parts[1] === "rent") {
        const countPart = parts[3];
        return ["1", "2", "3", "4", "5"].filter(c => c.startsWith(countPart) && c !== countPart);
    }

    return [];
}

interface DebugConsoleProps {
    client: GameSignalRClient | null;
    playerNames: string[];
    onShowToast?: (action: GameAction) => void;
}

// Map color strings to PropertyColor enum values
const COLOR_TO_PROP: Record<string, PropertyColor> = {
    brown: "Brown", lightblue: "LightBlue", pink: "Pink", orange: "Orange",
    red: "Red", yellow: "Yellow", green: "Green", darkblue: "DarkBlue",
    railroad: "Railroad", utility: "Utility",
};

// Sample property names per color
const SAMPLE_PROPERTIES: Record<string, string[]> = {
    Brown: ["Mediterranean Avenue", "Baltic Avenue"],
    LightBlue: ["Oriental Avenue", "Vermont Avenue"],
    Pink: ["St. Charles Place", "Virginia Avenue"],
    Orange: ["St. James Place", "Tennessee Avenue"],
    Red: ["Kentucky Avenue", "Indiana Avenue"],
    Yellow: ["Atlantic Avenue", "Ventnor Avenue"],
    Green: ["Pacific Avenue", "North Carolina"],
    DarkBlue: ["Boardwalk", "Park Place"],
    Railroad: ["Reading Railroad", "B&O Railroad"],
    Utility: ["Electric Company", "Water Works"],
};

// Sample property values per color
const PROP_VALUES: Record<string, number> = {
    Brown: 1, LightBlue: 1, Pink: 2, Orange: 2, Red: 3, Yellow: 3, Green: 4, DarkBlue: 4, Railroad: 2, Utility: 2,
};

function fakeCard(name: string, cardType: string, opts: Partial<Card> = {}): Card {
    return { id: Math.random() * 100000 | 0, name, cardType: cardType as Card["cardType"], moneyValue: 0, isMulticolorWild: false, isWildRent: false, ...opts };
}

function fakePropCard(name: string, color: PropertyColor): Card {
    return fakeCard(name, "Property", { color, activeColor: color, moneyValue: PROP_VALUES[color] || 2 });
}

function buildToastAction(type: string, args: string[], playerNames: string[]): GameAction {
    const player = playerNames[0] || "Player";
    const target = playerNames[1] || "Opponent";
    const arg = args[0] || "";
    const color = (COLOR_TO_PROP[arg] || "Green") as PropertyColor;
    const colorName = PropertyColorMap[color]?.name || arg || "Green";
    const props = SAMPLE_PROPERTIES[color] || ["Property"];

    switch (type) {
        case "banked": {
            const amount = parseInt(arg) || 3;
            return {
                id: Date.now(), playerName: player, text: `Banked ◆${amount}`,
                cardPlayed: fakeCard(`◆${amount}M`, "Money", { moneyValue: amount }),
            };
        }
        case "placed": {
            return {
                id: Date.now(), playerName: player, text: `Placed ${props[0]}`,
                cardPlayed: fakePropCard(props[0], color),
            };
        }
        case "rent": {
            const cardCount = parseInt(args[1]) || 0;
            const amount = 3;
            const paymentCards: Card[] = [];
            for (let i = 0; i < cardCount; i++) {
                const val = [1, 2, 3, 4, 5][i % 5];
                paymentCards.push(fakeCard(`◆${val}M`, "Money", { moneyValue: val }));
            }
            return {
                id: Date.now(), playerName: player, text: `Charged ${colorName} rent ◆${amount}`,
                cardPlayed: fakeCard(`${colorName} Rent`, "Action", { moneyValue: 1, actionKind: "Rent", rentColors: [color] }),
                ...(paymentCards.length > 0 ? { sourceCards: paymentCards } : {}),
            };
        }
        case "rentwild": {
            const cardCount = parseInt(args[1]) || 0;
            const amount = 3;
            const paymentCards: Card[] = [];
            for (let i = 0; i < cardCount; i++) {
                const val = [1, 2, 3, 4, 5][i % 5];
                paymentCards.push(fakeCard(`◆${val}M`, "Money", { moneyValue: val }));
            }
            return {
                id: Date.now(), playerName: player, text: `Charged ${colorName} rent ◆${amount} against ${target}`,
                targetPlayerName: target,
                cardPlayed: fakeCard("Wild Rent", "Action", { moneyValue: 3, actionKind: "Rent", isWildRent: true }),
                ...(paymentCards.length > 0 ? { sourceCards: paymentCards } : {}),
            };
        }
        case "rentpaid": {
            const cardCount = parseInt(arg) || 2;
            const paymentCards: Card[] = [];
            for (let i = 0; i < cardCount; i++) {
                const val = [1, 2, 3, 4, 5][i % 5];
                paymentCards.push(fakeCard(`◆${val}M`, "Money", { moneyValue: val }));
            }
            return {
                id: Date.now(), playerName: target, text: `Paid rent of ◆${paymentCards.reduce((s, c) => s + c.moneyValue, 0)} to ${player}`,
                targetPlayerName: player,
                targetCards: paymentCards,
            };
        }
        case "slydeal": {
            return {
                id: Date.now(), playerName: player, text: `Played Sly Deal against ${target} and stole ${props[0]}`,
                targetPlayerName: target,
                cardPlayed: fakeCard("Sly Deal", "Action", { moneyValue: 3, actionKind: "SlyDeal" }),
                targetCards: [fakePropCard(props[0], color)],
            };
        }
        case "forcedeal": {
            return {
                id: Date.now(), playerName: player, text: `Played Forced Deal against ${target}`,
                targetPlayerName: target,
                cardPlayed: fakeCard("Forced Deal", "Action", { moneyValue: 3, actionKind: "ForcedDeal" }),
                sourceCards: [fakePropCard(props[0], color)],
                targetCards: [fakePropCard(props[1] || props[0], color)],
            };
        }
        case "dealbreaker": {
            return {
                id: Date.now(), playerName: player, text: `Played Deal Breaker against ${target}`,
                targetPlayerName: target,
                cardPlayed: fakeCard("Deal Breaker", "Action", { moneyValue: 5, actionKind: "DealBreaker" }),
                targetCards: (SAMPLE_PROPERTIES[color] || ["Prop1", "Prop2"]).map(
                    (n) => fakePropCard(n, color)
                ),
            };
        }
        case "birthday": {
            return {
                id: Date.now(), playerName: player, text: `Played It's My Birthday`,
                cardPlayed: fakeCard("It's My Birthday", "Action", { moneyValue: 2, actionKind: "Birthday" }),
            };
        }
        case "passgo": {
            return {
                id: Date.now(), playerName: player, text: `Played Pass Go`,
                cardPlayed: fakeCard("Pass Go", "Action", { moneyValue: 1, actionKind: "PassGo" }),
            };
        }
        default: {
            return { id: Date.now(), playerName: player, text: `${type} ${arg}`.trim() };
        }
    }
}

export function DebugConsole({ client, playerNames, onShowToast }: DebugConsoleProps) {
    const [command, setCommand] = useState("");
    const [result, setResult] = useState<string | null>(null);
    const [selectedIdx, setSelectedIdx] = useState(0);
    const [showSuggestions, setShowSuggestions] = useState(true);
    const inputRef = useRef<HTMLInputElement>(null);

    const suggestions = showSuggestions && command.length > 0 ? getSuggestions(command, playerNames) : [];

    useEffect(() => { setSelectedIdx(0); }, [command]);

    const applySuggestion = (suggestion: string) => {
        const parts = command.split(" ");
        parts[parts.length - 1] = suggestion;
        setCommand(parts.join(" ") + " ");
        setShowSuggestions(true);
        inputRef.current?.focus();
    };

    const handleKeyDown = (e: React.KeyboardEvent) => {
        if (suggestions.length > 0) {
            if (e.key === "Tab" || e.key === "ArrowDown") {
                e.preventDefault();
                if (e.key === "Tab") {
                    applySuggestion(suggestions[selectedIdx]);
                } else {
                    setSelectedIdx((selectedIdx + 1) % suggestions.length);
                }
                return;
            }
            if (e.key === "ArrowUp") {
                e.preventDefault();
                setSelectedIdx((selectedIdx - 1 + suggestions.length) % suggestions.length);
                return;
            }
        }
        if (e.key === "Enter") {
            handleSubmit();
        }
        if (e.key === "Escape") {
            setShowSuggestions(false);
        }
    };

    const handleSubmit = async () => {
        if (!command.trim()) return;
        setShowSuggestions(false);
        const trimmed = command.trim();

        // Client-side commands
        if (trimmed.toLowerCase().startsWith("/toast")) {
            const parts = trimmed.toLowerCase().split(/\s+/);
            const type = parts[1] || "banked";
            const args = parts.slice(2);
            const action = buildToastAction(type, args, playerNames);
            onShowToast?.(action);
            setResult(`Toast: ${type} ${args.join(" ")}`);
            setCommand("");
            setShowSuggestions(true);
            return;
        }

        if (!client) return;
        try {
            // Strip leading "/" before sending to backend
            const backendCmd = trimmed.startsWith("/") ? trimmed.slice(1) : trimmed;
            const res = await client.debugCommand(backendCmd);
            setResult(res);
            setCommand("");
        } catch (err: unknown) {
            setResult(`Error: ${err instanceof Error ? err.message : String(err)}`);
        }
        setShowSuggestions(true);
    };

    return (
        <div className="debugConsole">
            <div className="debugConsole-inputWrap">
                <input
                    ref={inputRef}
                    className="debugConsole-input"
                    type="text"
                    placeholder="debug cmd..."
                    value={command}
                    onChange={(e) => { setCommand(e.target.value); setShowSuggestions(true); }}
                    onKeyDown={handleKeyDown}
                    autoComplete="off"
                />
                {suggestions.length > 0 && (
                    <div className="debugConsole-suggestions">
                        {suggestions.map((s, i) => (
                            <div
                                key={s}
                                className={`debugConsole-suggestion ${i === selectedIdx ? "debugConsole-suggestion--active" : ""}`}
                                onMouseDown={() => applySuggestion(s)}
                            >
                                {s}
                            </div>
                        ))}
                    </div>
                )}
            </div>
            <button className="debugConsole-btn" onClick={handleSubmit}>▶</button>
            {result && (
                <span className="debugConsole-result" title={result} onClick={() => setResult(null)}>
                    {result}
                </span>
            )}
        </div>
    );
}
