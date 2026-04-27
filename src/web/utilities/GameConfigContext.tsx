import React, { createContext, useContext } from "react";
import { GameConfigData } from "../Types";

const GameConfigContext = createContext<GameConfigData | null>(null);

export function GameConfigProvider({ config, children }: { config: GameConfigData; children: React.ReactNode }) {
    return (
        <GameConfigContext.Provider value={config}>
            {children}
        </GameConfigContext.Provider>
    );
}

export function useGameConfig(): GameConfigData {
    const ctx = useContext(GameConfigContext);
    if (!ctx) throw new Error("useGameConfig must be used within a GameConfigProvider");
    return ctx;
}
