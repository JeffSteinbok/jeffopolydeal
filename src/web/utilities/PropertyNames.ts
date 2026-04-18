import { PropertyColor } from "../Types";

/**
 * A property card definition: stable ID for code, display name for UI.
 */
export interface PropertyDef {
    cardId: string;
    displayName: string;
}

/**
 * Central registry of all property cards. Change displayNames to re-theme the game.
 * CardIds are stable identifiers (e.g. brown1, railroad3) — never change these.
 * Must stay in sync with backend PropertyNames.cs.
 */
export const PropertyNames: Record<PropertyColor, PropertyDef[]> = {
    // Brown
    Brown: [
        { cardId: "brown1", displayName: "Chan's Market" },
        { cardId: "brown2", displayName: "Wendy's" },
    ],

    // LightBlue
    LightBlue: [
        { cardId: "lightblue1", displayName: "Cowichan River" },
        { cardId: "lightblue2", displayName: "The Lot" },
        { cardId: "lightblue3", displayName: "Inner Harbour" },
    ],

    // Pink
    Pink: [
        { cardId: "pink1", displayName: "Carmel Drive" },
        { cardId: "pink2", displayName: "Doral Place" },
        { cardId: "pink3", displayName: "Hudson Street" },
    ],

    // Orange
    Orange: [
        { cardId: "orange1", displayName: "Duncan" },
        { cardId: "orange2", displayName: "Victoria" },
        { cardId: "orange3", displayName: "Vancouver" },
    ],

    // Red
    Red: [
        { cardId: "red1", displayName: "SushiMe" },
        { cardId: "red2", displayName: "Din Tai Fung" },
        { cardId: "red3", displayName: "Prime Steakhouse" },
    ],

    // Yellow
    Yellow: [
        { cardId: "yellow1", displayName: "Bellevue" },
        { cardId: "yellow2", displayName: "Redmond" },
        { cardId: "yellow3", displayName: "Sammamish" },
    ],

    // Green
    Green: [
        { cardId: "green1", displayName: "Woodbridge" },
        { cardId: "green2", displayName: "Timberline" },
        { cardId: "green3", displayName: "Lake House" },
    ],

    // DarkBlue
    DarkBlue: [
        { cardId: "darkblue1", displayName: "False Creek" },
        { cardId: "darkblue2", displayName: "Lake Sammamish" },
    ],

    // Railroad → Sports Stadiums
    Railroad: [
        { cardId: "railroad1", displayName: "ESP Ball Fields" },
        { cardId: "railroad2", displayName: "Folsom Field" },
        { cardId: "railroad3", displayName: "Lumen Field" },
        { cardId: "railroad4", displayName: "T-Mobile Park" },
    ],

    // Utility
    Utility: [
        { cardId: "utility1", displayName: "Safeway" },
        { cardId: "utility2", displayName: "Whole Foods" },
    ],
};

/**
 * Flat lookup: CardId → DisplayName for quick name resolution.
 */
export const DisplayNameById: Record<string, string> = Object.values(PropertyNames)
    .flat()
    .reduce((acc, def) => {
        acc[def.cardId] = def.displayName;
        return acc;
    }, {} as Record<string, string>);
