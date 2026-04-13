import { PropertyColor } from "../Types";

/**
 * Central registry of all property names. Change these to re-theme the game.
 * Sorted by price tier (Brown = cheapest, DarkBlue = most expensive).
 * Must stay in sync with backend PropertyNames.cs.
 */
export const PropertyNames: Record<PropertyColor, string[]> = {
    // Brown — Duncan, BC area / rural Vancouver Island
    Brown: ["Cowichan Station", "Glenora General Store"],

    // LightBlue — Honeymoon Bay / Lake Cowichan area
    LightBlue: ["Honeymoon Bay", "Lake Cowichan", "Mesachie Lake"],

    // Pink — Duncan / Cowichan Valley
    Pink: ["Duncan", "Maple Bay", "Genoa Bay"],

    // Orange — Vancouver / Burnaby
    Orange: ["Gastown", "Kitsilano", "Commercial Drive"],

    // Red — East Side / Bellevue
    Red: ["Bellevue Square", "Crossroads Mall", "Factoria"],

    // Yellow — Lake Sammamish / Issaquah
    Yellow: ["Pine Lake", "Issaquah Highlands", "Sammamish Landing"],

    // Green — Seattle proper / premium areas
    Green: ["Capitol Hill", "Queen Anne", "Fremont"],

    // DarkBlue — Waterfront / flagship locations
    DarkBlue: ["Alki Beach", "Pike Place Market"],

    // Railroad → Sports Stadiums (Seattle & Vancouver)
    Railroad: ["Lumen Field", "T-Mobile Park", "Climate Pledge Arena", "Rogers Arena"],

    // Utility
    Utility: ["Xfinity", "Puget Sound Energy"],
};
