import * as React from "react";
import { createRoot } from "react-dom/client";
import { useState } from "react";
import { StartPage } from "./pages/startPage/StartPage";
import { GamePage } from "./pages/gamePage/GamePage";
import { DeckPage } from "./pages/deckPage/DeckPage";
import { ThemeProvider } from "./themes/ThemeContext";
import { Debug, DebugFlags } from "./utilities/Debug";
import {
    readNativeGameEntry,
    applyNativeHostClasses,
    publishResolvedGameCode,
    exitToNativeShell,
} from "./utilities/NativeHost";
import "./styles/global.css";
import "./themes/classic.css";
import "./themes/dark.css";

Debug.initFromUrl();

// Detect iOS standalone (PWA) mode and set a class on <html> for safe-area styling
if ((navigator as any).standalone || window.matchMedia("(display-mode: standalone)").matches) {
    document.documentElement.classList.add("pwa-standalone");
}

// A native shell (see utilities/NativeHost.ts) enters gameplay directly at /play
// and supplies the player identity itself, so there is no start page to show.
const nativeEntry = readNativeGameEntry();
if (nativeEntry) {
    applyNativeHostClasses(nativeEntry);
}

const SESSION_KEY = "jeffopolydeal_session";

interface SessionInfo {
    gameCode: string;
    playerName: string;
    playerId: string;
}

function getPlayerId(): string {
    let id = localStorage.getItem("jeffopolydeal_playerId");
    if (!id) {
        id = crypto.randomUUID();
        localStorage.setItem("jeffopolydeal_playerId", id);
    }
    return id;
}

function saveSession(info: SessionInfo) {
    localStorage.setItem(SESSION_KEY, JSON.stringify(info));
}

function loadSession(): SessionInfo | null {
    try {
        const raw = localStorage.getItem(SESSION_KEY);
        if (!raw) return null;
        return JSON.parse(raw) as SessionInfo;
    } catch {
        return null;
    }
}

function clearSession() {
    localStorage.removeItem(SESSION_KEY);
}

// eslint-disable-next-line react-refresh/only-export-components
function App() {
    const params = new URLSearchParams(window.location.search);
    const autoStart = Debug.isFlagSet(DebugFlags.SkipLobby);
    const savedSession = loadSession();

    const [gameCode, setGameCode] = useState<string | null>(
        autoStart ? "" : savedSession?.gameCode ?? null
    );
    const [playerName, setPlayerName] = useState<string>(
        autoStart ? "Player1" : savedSession?.playerName ?? ""
    );
    const [playerId] = useState<string>(getPlayerId());
    const [inGame, setInGame] = useState(autoStart || !!savedSession);
    const [isRejoin, setIsRejoin] = useState(!!savedSession && !autoStart);

    // Route to deck test page via ?page=deck
    if (params.get("page") === "deck") {
        return <DeckPage />;
    }

    // Native shells own app entry and hand off straight into gameplay.
    if (nativeEntry) {
        return (
            <GamePage
                gameCode={nativeEntry.gameCode}
                playerName={nativeEntry.playerName}
                playerId={nativeEntry.playerId}
                isRejoin={nativeEntry.isRejoin}
                onGameCodeResolved={publishResolvedGameCode}
                onLeave={exitToNativeShell}
            />
        );
    }

    const handleLeave = () => {
        clearSession();
        setInGame(false);
        setGameCode(null);
        setIsRejoin(false);
    };

    const handleJoin = (code: string, name: string) => {
        setGameCode(code);
        setPlayerName(name);
        setInGame(true);
        setIsRejoin(false);
        saveSession({ gameCode: code, playerName: name, playerId });
    };

    if (inGame) {
        return (
            <GamePage
                gameCode={gameCode ?? ""}
                playerName={playerName}
                playerId={playerId}
                isRejoin={isRejoin}
                onGameCodeResolved={(code) => {
                    setGameCode(code);
                    saveSession({ gameCode: code, playerName, playerId });
                }}
                onLeave={handleLeave}
            />
        );
    }

    return <StartPage onJoinGame={handleJoin} />;
}

const root = document.getElementById("root")!;
createRoot(root).render(
    <ThemeProvider>
        <App />
    </ThemeProvider>
);
