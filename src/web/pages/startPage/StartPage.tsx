import React, { useState } from "react";
import titleImage from "../../assets/JeffopolyDeal.png";
import "./StartPage.css";

interface StartPageProps {
    onJoinGame: (gameCode: string, playerName: string) => void;
}

export function StartPage({ onJoinGame }: StartPageProps) {
    const [mode, setMode] = useState<"menu" | "create" | "join">("menu");
    const [playerName, setPlayerName] = useState("");
    const [gameCode, setGameCode] = useState("");

    const handleCreate = () => {
        if (!playerName.trim()) return;
        // Pass empty game code — GamePage will call CreateGame via SignalR
        onJoinGame("", playerName.trim());
    };

    const handleJoin = () => {
        if (!playerName.trim() || !gameCode.trim()) return;
        onJoinGame(gameCode.trim().toUpperCase(), playerName.trim());
    };

    return (
        <div className="startPage">
            <div className="startPageContent">
                <img src={titleImage} alt="Jeffopoly Deal" className="titleImage" />

                <div className="startPageForm">
                    {mode === "menu" && (
                        <div className="menuButtons">
                            <button className="primary menuButton" onClick={() => setMode("create")}>
                                Create Game
                            </button>
                            <button className="secondary menuButton" onClick={() => setMode("join")}>
                                Join Game
                            </button>
                        </div>
                    )}

                    {mode === "create" && (
                        <div className="formSection">
                            <input
                                type="text"
                                placeholder="Your Name"
                                value={playerName}
                                onChange={(e) => setPlayerName(e.target.value)}
                                onKeyDown={(e) => e.key === "Enter" && handleCreate()}
                                autoFocus
                                maxLength={20}
                                autoComplete="off"
                                data-1p-ignore
                                data-lpignore="true"
                            />
                            <button className="primary" onClick={handleCreate} disabled={!playerName.trim()}>
                                Create Game
                            </button>
                            <button className="secondary" onClick={() => setMode("menu")}>
                                Back
                            </button>
                        </div>
                    )}

                    {mode === "join" && (
                        <div className="formSection">
                            <input
                                type="text"
                                placeholder="Your Name"
                                value={playerName}
                                onChange={(e) => setPlayerName(e.target.value)}
                                autoFocus
                                maxLength={20}
                                autoComplete="off"
                                data-1p-ignore
                                data-lpignore="true"
                            />
                            <input
                                type="text"
                                placeholder="Game Code"
                                value={gameCode}
                                onChange={(e) => setGameCode(e.target.value.toUpperCase())}
                                onKeyDown={(e) => e.key === "Enter" && handleJoin()}
                                maxLength={4}
                                style={{ textTransform: "uppercase", letterSpacing: "0.2em" }}
                                autoComplete="off"
                                data-1p-ignore
                                data-lpignore="true"
                            />
                            <button className="primary" onClick={handleJoin} disabled={!playerName.trim() || !gameCode.trim()}>
                                Join Game
                            </button>
                            <button className="secondary" onClick={() => setMode("menu")}>
                                Back
                            </button>
                        </div>
                    )}
                </div>
            </div>
        </div>
    );
}
