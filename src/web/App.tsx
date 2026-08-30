import * as React from "react";
import { createRoot } from "react-dom/client";
import { useState, useEffect } from "react";
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
import { installNativeInboundAPI, onOpenGame } from "./utilities/NativeInbound";
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
const PLAYER_NAME_KEY = "jeffopolydeal_playerName";

/**
 * The name this player last chose. A native shell suggests the device name, but
 * that is only ever a first-run hint — once someone edits it, their choice wins.
 */
function loadPlayerName(): string | null {
    try {
        const name = localStorage.getItem(PLAYER_NAME_KEY)?.trim();
        return name ? name : null;
    } catch {
        return null;
    }
}

function savePlayerName(name: string) {
    const trimmed = name.trim().slice(0, 20);
    if (!trimmed) return;
    try {
        localStorage.setItem(PLAYER_NAME_KEY, trimmed);
    } catch {
        // A full or disabled store must not stop someone starting a game.
    }
}

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

    // A notification tap or deep link while the app is already running. The
    // player name we already know is reused, so this does not bounce them back
    // to the start page just to retype it.
    useEffect(() => onOpenGame((code) => {
        if (code === gameCode && inGame) return;
        setGameCode(code);
        setInGame(true);
        setIsRejoin(true);
        saveSession({ gameCode: code, playerName, playerId });
    }), [gameCode, inGame, playerName, playerId]);

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
        savePlayerName(name);
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

    // A remembered name beats the shell's device-name hint, which only fills in
    // for someone who has never entered one.
    const nameHint = loadPlayerName() ?? readPlayerNameHint() ?? undefined;
    return <StartPage onJoinGame={handleJoin} playerNameHint={nameHint} />;
}

const root = document.getElementById("root")!;
createRoot(root).render(
    <ThemeProvider>
        <App />
    </ThemeProvider>
);
