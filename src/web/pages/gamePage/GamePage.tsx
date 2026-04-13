import React, { useEffect, useState, useRef } from "react";
import { GameSignalRClient } from "./GameSignalRClient";
import { GameState, Card, PlayCardRequest, PlayerState } from "../../Types";
import { Logger } from "../../utilities/Logger";
import { CardComponent } from "./components/Card";
import { PlayerBoard } from "./components/PlayerBoard";
import { Hand } from "./components/Hand";
import { ActionModal } from "./components/ActionModal";
import "./styles/game.css";

interface GamePageProps {
    gameCode: string;
    playerName: string;
    onLeave: () => void;
}

export function GamePage({ gameCode, playerName, onLeave }: GamePageProps) {
    const [state, setState] = useState<GameState | null>(null);
    const [error, setError] = useState<string | null>(null);
    const clientRef = useRef<GameSignalRClient | null>(null);

    useEffect(() => {
        const client = new GameSignalRClient((newState) => {
            setState(newState);
        });
        clientRef.current = client;

        const connect = async () => {
            try {
                await client.start();

                if (gameCode === "") {
                    // Creating a new game
                    const newCode = await client.createGame();
                    await client.joinGame(newCode, playerName);
                } else {
                    await client.joinGame(gameCode, playerName);
                }
            } catch (err) {
                Logger.error("Connection error:", err);
                setError("Failed to connect to game server.");
            }
        };

        connect();

        return () => {
            client.stop();
        };
    }, []);

    const client = clientRef.current;
    const myConnectionId = client?.connectionId;
    const me = state?.players.find((p) => p.connectionId === myConnectionId);
    const isMyTurn = state && me && state.players[state.currentPlayerIndex]?.connectionId === myConnectionId;
    const isCreator = state && me && state.players[0]?.connectionId === myConnectionId;

    if (error) {
        return (
            <div className="gamePage">
                <div className="errorMessage">{error}</div>
                <button className="secondary" onClick={onLeave}>Back</button>
            </div>
        );
    }

    if (!state || !me) {
        return <div className="gamePage"><div className="loading">Connecting...</div></div>;
    }

    // Lobby
    if (state.phase === "Lobby") {
        return (
            <div className="gamePage">
                <div className="lobby">
                    <h2>Jeffopoly Deal</h2>
                    <div className="gameCodeDisplay">
                        Game Code: <span className="code">{state.gameCode}</span>
                    </div>
                    <div className="playerList">
                        <h3>Players ({state.players.length})</h3>
                        {state.players.map((p) => (
                            <div key={p.connectionId} className="playerName">
                                {p.name} {p.connectionId === myConnectionId ? "(you)" : ""}
                            </div>
                        ))}
                    </div>
                    {isCreator && (
                        <button
                            className="primary"
                            onClick={() => client?.startGame(state.gameCode)}
                            disabled={state.players.length < 2}
                        >
                            Start Game {state.players.length < 2 ? "(need 2+ players)" : ""}
                        </button>
                    )}
                    {!isCreator && <p className="waitingText">Waiting for host to start...</p>}
                </div>
            </div>
        );
    }

    // Game Over
    if (state.phase === "GameOver") {
        return (
            <div className="gamePage">
                <div className="gameOver">
                    <h2>🎉 Game Over!</h2>
                    <p className="winnerName">{state.winnerName} wins!</p>
                    <button className="primary" onClick={onLeave}>Play Again</button>
                </div>
            </div>
        );
    }

    // Active Game
    const otherPlayers = state.players.filter((p) => p.connectionId !== myConnectionId);
    const needsResponse = state.phase === "AwaitingResponse" &&
        state.pendingAction?.targetPlayerIds.includes(myConnectionId ?? "");

    return (
        <div className="gamePage">
            <div className="gameHeader">
                <span className="gameCodeSmall">{state.gameCode}</span>
                <span className="turnInfo">
                    {isMyTurn
                        ? `Your turn (${state.playsUsed}/3 plays)`
                        : `${state.players[state.currentPlayerIndex]?.name}'s turn`}
                </span>
                <span className="deckInfo">
                    Draw: {state.drawPileCount} | Discard: {state.discardPileCount}
                </span>
            </div>

            <div className="otherPlayersArea">
                {otherPlayers.map((p) => (
                    <PlayerBoard key={p.connectionId} player={p} />
                ))}
            </div>

            <div className="myArea">
                <PlayerBoard player={me} isMe={true} />

                {state.phase === "Draw" && isMyTurn && (
                    <div className="actionBar">
                        <button className="primary" onClick={() => client?.drawCards()}>
                            Draw Cards
                        </button>
                    </div>
                )}

                {state.phase === "Play" && isMyTurn && (
                    <div className="actionBar">
                        <button className="secondary" onClick={() => client?.endTurn()}>
                            End Turn
                        </button>
                    </div>
                )}

                {state.phase === "Discard" && isMyTurn && (
                    <div className="actionBar">
                        <span className="discardHint">Discard to 7 cards (you have {me.handCount})</span>
                    </div>
                )}

                <Hand
                    cards={me.hand ?? []}
                    canPlay={isMyTurn === true && (state.phase === "Play" || state.phase === "Discard")}
                    phase={state.phase}
                    gameState={state}
                    onPlayCard={(cardId, request) => client?.playCard(cardId, request)}
                    onDiscardCard={(cardId) => client?.discardCard(cardId)}
                />
            </div>

            {needsResponse && state.pendingAction && (
                <ActionModal
                    pendingAction={state.pendingAction}
                    myState={me}
                    onRespond={(response) => client?.respondToAction(response)}
                />
            )}
        </div>
    );
}
