import * as React from "react";
import { createRoot } from "react-dom/client";
import { useState, useEffect } from "react";
import { StartPage } from "./pages/startPage/StartPage";
import { GamePage } from "./pages/gamePage/GamePage";
import { ThemeProvider } from "./themes/ThemeContext";
import { Debug, DebugFlags } from "./utilities/Debug";
import "./styles/global.css";
import "./themes/classic.css";
import "./themes/dark.css";

Debug.initFromUrl();

function App() {
    const autoStart = Debug.isFlagSet(DebugFlags.SkipLobby);
    const [gameCode, setGameCode] = useState<string | null>(autoStart ? "" : null);
    const [playerName, setPlayerName] = useState<string>(autoStart ? "Player1" : "");
    const [inGame, setInGame] = useState(autoStart);

    if (inGame) {
        return <GamePage gameCode={gameCode ?? ""} playerName={playerName} onLeave={() => { setInGame(false); setGameCode(null); }} />;
    }

    return <StartPage onJoinGame={(code, name) => { setGameCode(code); setPlayerName(name); setInGame(true); }} />;
}

const root = document.getElementById("root")!;
createRoot(root).render(
    <ThemeProvider>
        <App />
    </ThemeProvider>
);
