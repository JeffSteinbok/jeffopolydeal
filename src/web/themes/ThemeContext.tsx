import React, { createContext, useContext, useState, ReactNode } from "react";
import { PropertyColor } from "../Types";
import { GameConfig } from "./GameConfig";

export type ThemeName = "classic" | "dark";

interface CardIconSet {
    money: Record<number, string>;
    property: Record<string, string>;
    action: Record<string, string>;
    rent: string;
    rentWild: string;
    multicolorWild: string;
}

const defaultIcons: CardIconSet = {
    money: { 1: "💵", 2: "💵", 3: "💰", 4: "💰", 5: "💎", 10: "🏦" },
    property: {
        Brown: "🏚️", LightBlue: "🏠", Pink: "🏡", Orange: "🏘️",
        Red: "🏢", Yellow: "🏗️", Green: "🏛️", DarkBlue: "🏰",
        Railroad: "🚂", Utility: "💡",
    },
    action: {
        PassGo: "➡️", DebtCollector: "🤑", ItsMyBirthday: "🎂",
        SlyDeal: "🤫", ForceDeal: "🤝", DealBreaker: "💥",
        JustSayNo: "🚫", DoubleTheRent: "⏫", House: "🏠", Hotel: "🏨",
    },
    rent: "🏷️",
    rentWild: "🎯",
    multicolorWild: "🌈",
};

interface ThemeContextValue {
    themeName: ThemeName;
    setThemeName: (name: ThemeName) => void;
    icons: CardIconSet;
}

const ThemeContext = createContext<ThemeContextValue>({
    themeName: "classic",
    setThemeName: () => {},
    icons: defaultIcons,
});

export function ThemeProvider({ children }: { children: ReactNode }) {
    const [themeName, setThemeName] = useState<ThemeName>("classic");

    return (
        <ThemeContext.Provider value={{ themeName, setThemeName, icons: defaultIcons }}>
            <div className={`theme-${themeName}`}>
                {children}
            </div>
        </ThemeContext.Provider>
    );
}

export function useTheme() {
    return useContext(ThemeContext);
}
