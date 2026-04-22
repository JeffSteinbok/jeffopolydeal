import { PropertyColorMap, PropertyColorOrder } from "./PropertyColors";

describe("PropertyColorMap", () => {
    const allColors = [
        "Brown", "LightBlue", "Pink", "Orange", "Red",
        "Yellow", "Green", "DarkBlue", "Railroad", "Utility",
    ] as const;

    it("contains all 10 colors", () => {
        for (const color of allColors) {
            expect(PropertyColorMap[color]).toBeDefined();
        }
    });

    it("every entry has name, short, hex, textColor", () => {
        for (const color of allColors) {
            const entry = PropertyColorMap[color];
            expect(entry).toHaveProperty("name");
            expect(entry).toHaveProperty("short");
            expect(entry).toHaveProperty("hex");
            expect(entry).toHaveProperty("textColor");
        }
    });

    it("hex values start with #", () => {
        for (const color of allColors) {
            expect(PropertyColorMap[color].hex).toMatch(/^#/);
        }
    });

    it("textColor is either #fff or #000", () => {
        for (const color of allColors) {
            expect(["#fff", "#000"]).toContain(PropertyColorMap[color].textColor);
        }
    });

    it("Yellow and Utility have textColor #000, all others #fff", () => {
        expect(PropertyColorMap.Yellow.textColor).toBe("#000");
        expect(PropertyColorMap.Utility.textColor).toBe("#000");
        const darkTextColors: typeof allColors[number][] = ["Yellow", "Utility"];
        for (const color of allColors) {
            if (!darkTextColors.includes(color)) {
                expect(PropertyColorMap[color].textColor).toBe("#fff");
            }
        }
    });
});

describe("PropertyColorOrder", () => {
    it("has exactly 10 entries", () => {
        expect(PropertyColorOrder).toHaveLength(10);
    });

    it("has no duplicates", () => {
        const unique = new Set(PropertyColorOrder);
        expect(unique.size).toBe(PropertyColorOrder.length);
    });

    it("every color exists in PropertyColorMap", () => {
        for (const color of PropertyColorOrder) {
            expect(PropertyColorMap[color]).toBeDefined();
        }
    });
});
