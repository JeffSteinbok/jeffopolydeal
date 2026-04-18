import { PropertyColor } from "./Types";

/// Maps PropertyColor enum values to CSS-friendly display data.
export const PropertyColorMap: Record<PropertyColor, { name: string; short: string; hex: string; textColor: string }> = {
    Brown:     { name: "Brown",      short: "Brn",    hex: "#6d3b15", textColor: "#fff" },
    LightBlue: { name: "Light Blue", short: "Lt Blue", hex: "#72c5e8", textColor: "#000" },
    Pink:      { name: "Pink",       short: "Pink",   hex: "#d9308e", textColor: "#fff" },
    Orange:    { name: "Orange",     short: "Org",    hex: "#f58220", textColor: "#fff" },
    Red:       { name: "Red",        short: "Red",    hex: "#e3242b", textColor: "#fff" },
    Yellow:    { name: "Yellow",     short: "Yel",    hex: "#feed00", textColor: "#000" },
    Green:     { name: "Green",      short: "Grn",    hex: "#1fb25a", textColor: "#fff" },
    DarkBlue:  { name: "Dark Blue",  short: "Dk Blue",hex: "#0055a5", textColor: "#fff" },
    Railroad:  { name: "Railroad",   short: "Rail",   hex: "#1a1a1a", textColor: "#fff" },
    Utility:   { name: "Utility",    short: "Util",   hex: "#b5d99c", textColor: "#000" },
};
