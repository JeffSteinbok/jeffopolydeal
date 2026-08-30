import * as React from "react";
import { createRoot } from "react-dom/client";
import { useState } from "react";
import { StartPage } from "./pages/startPage/StartPage";
import { GamePage } from "./pages/gamePage/GamePage";
import { DeckPage } from "./pages/deckPage/DeckPage";
import { ThemeProvider } from "./themes/ThemeContext";
import { Debug, DebugFlags } from "./utilities/Debug";
import {
    isNativeHost,
    readNativeGameEntry,
    readPlayerNameHint,
    applyNativeHostClasses,
} from "./utilities/NativeHost";
import { installNativeInboundAPI } from "./utilities/NativeNearby";
import "./styles/global.css";
import "./themes/classic.css";
import "./themes/dark.css";

Debug.initFromUrl();

// Detect iOS standalone (PWA) mode and set a class on <html> for safe-area styling
if ((navigator as any).standalone || window.matchMedia("(display-mode: standalone)").matches) {
    document.documentElement.classList.add("pwa-standalone");
}

// A native shell owns app entry only where it can do something the web cannot —
// today that is local-network discovery. Everything else, start page included,
// is this client's job.
const inNativeShell = isNativeHost();
if (inNativeShell) {
    applyNativeHostClasses();
    installNativeInboundAPI();
}

// Set when the shell hands us straight into a specific game (a shared link or a
// notification tap) rather than letting the player start from the beginning.
const nativeEntry = readNativeGameEntry();

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
        autoStart ? "" : nativeEntry?.gameCode ?? savedSession?.gameCode ?? null
    );
    const [playerName, setPlayerName] = useState<string>(
        autoStart ? "Player1" : nativeEntry?.playerName ?? savedSession?.playerName ?? ""
    );
    const [playerId] = useState<string>(nativeEntry?.playerId ?? getPlayerId());
    const [inGame, setInGame] = useState(autoStart || !!nativeEntry || !!savedSession);
    const [isRejoin, setIsRejoin] = useState(
        nativeEntry ? nativeEntry.isRejoin : !!savedSession && !autoStart
    );

    // Route to deck test page via ?page=deck
    if (params.get("page") === "deck") {
        return <DeckPage />;
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

    return <StartPage onJoinGame={handleJoin} playerNameHint={readPlayerNameHint() ?? undefined} />;
}

const root = document.getElementById("root")!;
createRoot(root).render(
    <ThemeProvider>
        <App />
    </ThemeProvider>
);
