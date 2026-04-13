import { PropertyColor } from "./Types";

/// Maps PropertyColor enum values to CSS-friendly display data.
export const PropertyColorMap: Record<PropertyColor, { name: string; hex: string; textColor: string }> = {
    Brown:     { name: "Brown",      hex: "#8B4513", textColor: "#fff" },
    LightBlue: { name: "Light Blue", hex: "#87CEEB", textColor: "#000" },
    Pink:      { name: "Pink",       hex: "#FF69B4", textColor: "#000" },
    Orange:    { name: "Orange",     hex: "#FF8C00", textColor: "#000" },
    Red:       { name: "Red",        hex: "#DC143C", textColor: "#fff" },
    Yellow:    { name: "Yellow",     hex: "#FFD700", textColor: "#000" },
    Green:     { name: "Green",      hex: "#228B22", textColor: "#fff" },
    DarkBlue:  { name: "Dark Blue",  hex: "#00008B", textColor: "#fff" },
    Railroad:  { name: "Railroad",   hex: "#333333", textColor: "#fff" },
    Utility:   { name: "Utility",    hex: "#90EE90", textColor: "#000" },
};
