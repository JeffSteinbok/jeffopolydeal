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
    // Brown — Duncan, BC area / rural Vancouver Island
    Brown: [
        { cardId: "brown1", displayName: "Cowichan Station" },
        { cardId: "brown2", displayName: "Glenora General Store" },
    ],

    // LightBlue — Honeymoon Bay / Lake Cowichan area
    LightBlue: [
        { cardId: "lightblue1", displayName: "Honeymoon Bay" },
        { cardId: "lightblue2", displayName: "Lake Cowichan" },
        { cardId: "lightblue3", displayName: "Mesachie Lake" },
    ],

    // Pink — Duncan / Cowichan Valley
    Pink: [
        { cardId: "pink1", displayName: "Duncan" },
        { cardId: "pink2", displayName: "Maple Bay" },
        { cardId: "pink3", displayName: "Genoa Bay" },
    ],

    // Orange — Vancouver / Burnaby
    Orange: [
        { cardId: "orange1", displayName: "Gastown" },
        { cardId: "orange2", displayName: "Kitsilano" },
        { cardId: "orange3", displayName: "Commercial Drive" },
    ],

    // Red — East Side / Bellevue
    Red: [
        { cardId: "red1", displayName: "Bellevue Square" },
        { cardId: "red2", displayName: "Crossroads Mall" },
        { cardId: "red3", displayName: "Factoria" },
    ],

    // Yellow — Lake Sammamish / Issaquah
    Yellow: [
        { cardId: "yellow1", displayName: "Pine Lake" },
        { cardId: "yellow2", displayName: "Issaquah Highlands" },
        { cardId: "yellow3", displayName: "Sammamish Landing" },
    ],

    // Green — Seattle proper / premium areas
    Green: [
        { cardId: "green1", displayName: "Capitol Hill" },
        { cardId: "green2", displayName: "Queen Anne" },
        { cardId: "green3", displayName: "Fremont" },
    ],

    // DarkBlue — Waterfront / flagship locations
    DarkBlue: [
        { cardId: "darkblue1", displayName: "Alki Beach" },
        { cardId: "darkblue2", displayName: "Pike Place Market" },
    ],

    // Railroad → Sports Stadiums (Seattle & Vancouver)
    Railroad: [
        { cardId: "railroad1", displayName: "Lumen Field" },
        { cardId: "railroad2", displayName: "T-Mobile Park" },
        { cardId: "railroad3", displayName: "Climate Pledge Arena" },
        { cardId: "railroad4", displayName: "Rogers Arena" },
    ],

    // Utility
    Utility: [
        { cardId: "utility1", displayName: "Xfinity" },
        { cardId: "utility2", displayName: "Puget Sound Energy" },
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
