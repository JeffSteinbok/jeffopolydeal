import { PropertyColor } from "./Types";

/// Maps PropertyColor enum values to CSS-friendly display data.
export const PropertyColorMap: Record<PropertyColor, { name: string; hex: string; textColor: string }> = {
    Brown:     { name: "Brown",      hex: "#6d3b15", textColor: "#fff" },
    LightBlue: { name: "Light Blue", hex: "#72c5e8", textColor: "#000" },
    Pink:      { name: "Pink",       hex: "#d9308e", textColor: "#fff" },
    Orange:    { name: "Orange",     hex: "#f58220", textColor: "#000" },
    Red:       { name: "Red",        hex: "#e3242b", textColor: "#fff" },
    Yellow:    { name: "Yellow",     hex: "#feed00", textColor: "#000" },
    Green:     { name: "Green",      hex: "#1fb25a", textColor: "#fff" },
    DarkBlue:  { name: "Dark Blue",  hex: "#0055a5", textColor: "#fff" },
    Railroad:  { name: "Railroad",   hex: "#1a1a1a", textColor: "#fff" },
    Utility:   { name: "Utility",    hex: "#b5d99c", textColor: "#000" },
};
