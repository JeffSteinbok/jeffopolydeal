import React from "react";
import { render, RenderOptions } from "@testing-library/react";
import { GameConfigProvider } from "./GameConfigContext";
import { GameConfigData } from "../Types";

/** Test-only game config matching the backend defaults. */
export const testGameConfig: GameConfigData = {
    setSize: {
        Brown: 2, LightBlue: 3, Pink: 3, Orange: 3, Red: 3,
        Yellow: 3, Green: 3, DarkBlue: 2, Railroad: 4, Utility: 2,
    },
    rentTable: {
        Brown: [0, 1, 2], LightBlue: [0, 1, 2, 3], Pink: [0, 1, 2, 4],
        Orange: [0, 1, 3, 5], Red: [0, 2, 3, 6], Yellow: [0, 2, 4, 6],
        Green: [0, 2, 4, 7], DarkBlue: [0, 3, 8], Railroad: [0, 1, 2, 3, 4],
        Utility: [0, 1, 2],
    },
};

function ConfigWrapper({ children }: { children: React.ReactNode }) {
    return <GameConfigProvider config={testGameConfig}>{children}</GameConfigProvider>;
}

/** render() wrapper that provides GameConfigContext for tests.
 *  Also wraps rerender() so context is preserved on re-renders. */
export function renderWithConfig(ui: React.ReactElement, options?: Omit<RenderOptions, "wrapper">) {
    return render(ui, { wrapper: ConfigWrapper, ...options });
}
