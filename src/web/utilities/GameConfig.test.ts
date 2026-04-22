import { GameConfig } from "./GameConfig";

const allColors = [
    "Brown", "LightBlue", "Pink", "Orange", "Red",
    "Yellow", "Green", "DarkBlue", "Railroad", "Utility",
] as const;

describe("GameConfig.setSize", () => {
    it("has entries for all 10 colors", () => {
        for (const color of allColors) {
            expect(GameConfig.setSize[color]).toBeDefined();
        }
    });

    it("Brown setSize is 2", () => {
        expect(GameConfig.setSize.Brown).toBe(2);
    });

    it("DarkBlue setSize is 2", () => {
        expect(GameConfig.setSize.DarkBlue).toBe(2);
    });

    it("Railroad setSize is 4", () => {
        expect(GameConfig.setSize.Railroad).toBe(4);
    });
});

describe("GameConfig.rentTable", () => {
    it("has entries for all 10 colors", () => {
        for (const color of allColors) {
            expect(GameConfig.rentTable[color]).toBeDefined();
        }
    });

    it("rentTable[color].length === setSize[color] + 1 for every color", () => {
        for (const color of allColors) {
            expect(GameConfig.rentTable[color].length).toBe(
                GameConfig.setSize[color] + 1
            );
        }
    });

    it("all rent values are >= 0", () => {
        for (const color of allColors) {
            for (const rent of GameConfig.rentTable[color]) {
                expect(rent).toBeGreaterThanOrEqual(0);
            }
        }
    });

    it("rent values are non-decreasing for each color", () => {
        for (const color of allColors) {
            const rents = GameConfig.rentTable[color];
            for (let i = 1; i < rents.length; i++) {
                expect(rents[i]).toBeGreaterThanOrEqual(rents[i - 1]);
            }
        }
    });

    it("DarkBlue rents are [0, 3, 8]", () => {
        expect(GameConfig.rentTable.DarkBlue).toEqual([0, 3, 8]);
    });

    it("Brown rents are [0, 1, 2]", () => {
        expect(GameConfig.rentTable.Brown).toEqual([0, 1, 2]);
    });
});
