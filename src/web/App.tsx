import * as React from "react";
import { createRoot } from "react-dom/client";
import { useState } from "react";
import { StartPage } from "./pages/startPage/StartPage";
import { GamePage } from "./pages/gamePage/GamePage";
import { Debug } from "./utilities/Debug";
import "./styles/global.css";

Debug.initFromUrl();

function App() {
    const [gameCode, setGameCode] = useState<string | null>(null);
    const [playerName, setPlayerName] = useState<string>("");

    if (gameCode) {
        return <GamePage gameCode={gameCode} playerName={playerName} onLeave={() => setGameCode(null)} />;
    }

    return <StartPage onJoinGame={(code, name) => { setGameCode(code); setPlayerName(name); }} />;
}

const root = document.getElementById("root")!;
createRoot(root).render(<App />);
