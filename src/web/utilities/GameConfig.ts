import { PropertyColor } from "../Types";

// Client-side mirror of GameConfig for UI calculations
export const GameConfig = {
    setSize: {
        Brown: 2, LightBlue: 3, Pink: 3, Orange: 3, Red: 3,
        Yellow: 3, Green: 3, DarkBlue: 2, Railroad: 4, Utility: 2,
    } as Record<PropertyColor, number>,

    rentTable: {
        Brown:     [0, 1, 2],
        LightBlue: [0, 1, 2, 3],
        Pink:      [0, 1, 2, 4],
        Orange:    [0, 1, 3, 5],
        Red:       [0, 2, 3, 6],
        Yellow:    [0, 2, 4, 6],
        Green:     [0, 2, 4, 7],
        DarkBlue:  [0, 3, 8],
        Railroad:  [0, 1, 2, 3, 4],
        Utility:   [0, 1, 2],
    } as Record<PropertyColor, number[]>,
};
