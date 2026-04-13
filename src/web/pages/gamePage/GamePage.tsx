import React, { useEffect, useState, useRef } from "react";
import { GameSignalRClient } from "./GameSignalRClient";
import { GameState, Card, PlayCardRequest, PlayerState } from "../../Types";
import { Logger } from "../../utilities/Logger";
import { Debug, DebugFlags } from "../../utilities/Debug";
import { CardComponent } from "./components/Card";
import { PlayerBoard } from "./components/PlayerBoard";
import { PlayerSummaryCard } from "./components/PlayerSummaryCard";
import { PlayerInspectModal } from "./components/PlayerInspectModal";
import { Hand } from "./components/Hand";
import { ActionModal } from "./components/ActionModal";
import { DiscardModal } from "./components/DiscardModal";
import { DebugDeckViewer } from "./components/DebugDeckViewer";
import "./styles/game.css";

function useIsMobile(breakpoint = 680): boolean {
    const [isMobile, setIsMobile] = useState(() => window.innerWidth <= breakpoint);
    useEffect(() => {
        let timer: ReturnType<typeof setTimeout>;
        const handler = () => {
            clearTimeout(timer);
            timer = setTimeout(() => setIsMobile(window.innerWidth <= breakpoint), 100);
        };
        window.addEventListener("resize", handler);
        return () => { window.removeEventListener("resize", handler); clearTimeout(timer); };
    }, [breakpoint]);
    return isMobile;
}

interface GamePageProps {
    gameCode: string;
    playerName: string;
    playerId: string;
    isRejoin?: boolean;
    onGameCodeResolved?: (code: string) => void;
    onLeave: () => void;
}

export function GamePage({ gameCode, playerName, playerId, isRejoin, onGameCodeResolved, onLeave }: GamePageProps) {
    const [state, setState] = useState<GameState | null>(null);
    const [error, setError] = useState<string | null>(null);
    const [inspectedPlayer, setInspectedPlayer] = useState<PlayerState | null>(null);
    const clientRef = useRef<GameSignalRClient | null>(null);
    const isMobile = useIsMobile();

    useEffect(() => {
        const client = new GameSignalRClient((newState) => {
            console.log("Game state received:", newState.phase, "players:", newState.players.length,
                "myHand:", newState.players.find(p => p.hand)?.hand?.length ?? "n/a");
            setState(newState);
        });
        clientRef.current = client;

        const connect = async () => {
            try {
                await client.start();

                if (isRejoin && gameCode) {
                    // Try to rejoin existing game
                    const success = await client.rejoinGame(gameCode, playerName, playerId);
                    if (!success) {
                        Logger.warn("Rejoin failed — game may have ended");
                        onLeave();
                        return;
                    }
                } else if (gameCode === "") {
                    // Creating a new game
                    const useFixedCode = Debug.isFlagSet(DebugFlags.FixedGameCode) || Debug.isFlagSet(DebugFlags.SkipLobby);
                    const newCode = await client.createGame(useFixedCode ? "TEST" : undefined);
                    await client.joinGame(newCode, playerName, playerId);
                    onGameCodeResolved?.(newCode);

                    // Auto-start when SkipLobby is set
                    if (Debug.isFlagSet(DebugFlags.SkipLobby)) {
                        const populate = Debug.isFlagSet(DebugFlags.PopulatedBoards);
                        await client.startGame(newCode, true, populate);
                    }
                } else {
                    await client.joinGame(gameCode, playerName, playerId);
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
    const me = state?.players.find((p) => p.playerId === playerId);
    const myConnectionId = me?.connectionId;
    const isMyTurn = state && me && state.players[state.currentPlayerIndex]?.playerId === playerId;
    const isCreator = state && me && state.players[0]?.playerId === playerId;

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

    const minPlayers = Debug.isFlagSet(DebugFlags.SkipLobby) ? 1 : 2;

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
                            onClick={() => client?.startGame(state.gameCode, minPlayers === 1)}
                            disabled={state.players.length < minPlayers}
                        >
                            Start Game {state.players.length < minPlayers ? `(need ${minPlayers}+ players)` : ""}
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
    const otherPlayers = state.players.filter((p) => p.playerId !== playerId);
    const needsResponse = state.phase === "AwaitingResponse" &&
        state.pendingAction?.targetPlayerIds.includes(myConnectionId ?? "");

    return (
        <div className="gamePage">
            <div className="gameHeader">
                <span className="gameCodeSmall">{state.gameCode}</span>
                <span className="turnInfo">
                    {isMyTurn
                        ? `Your turn — ${3 - state.playsUsed} play${3 - state.playsUsed !== 1 ? "s" : ""} left`
                        : `${state.players[state.currentPlayerIndex]?.name}'s turn`}
                </span>
                <span className="deckInfo">
                    Draw: {state.drawPileCount} | Discard: {state.discardPileCount}
                </span>
            </div>

            {/* Other players — compact summary on mobile, full boards on desktop */}
            <div className={isMobile ? "otherPlayersArea otherPlayersArea--mobile" : "otherPlayersArea"}>
                {otherPlayers.map((p) =>
                    isMobile ? (
                        <PlayerSummaryCard
                            key={p.connectionId}
                            player={p}
                            isCurrentTurn={state.players[state.currentPlayerIndex]?.playerId === p.playerId}
                            onClick={() => setInspectedPlayer(p)}
                        />
                    ) : (
                        <PlayerBoard key={p.connectionId} player={p} />
                    )
                )}
            </div>

            <div className="myArea">
                <PlayerBoard
                    player={me}
                    isMe={true}
                    isMyTurn={isMyTurn === true && state.phase === "Play"}
                    onFlipCard={(cardId) => client?.flipWildcard(cardId)}
                    onMoveProperty={(cardId, targetSetId, targetColor) => client?.moveProperty(cardId, targetSetId, targetColor)}
                />

                {state.phase === "Draw" && isMyTurn && (
                    <div className="actionBar">
                        <button className="primary" onClick={() => client?.drawCards()}>
                            Draw Cards
                        </button>
                    </div>
                )}

                {state.phase === "Play" && isMyTurn && (
                    <div className="actionBar">
                        <span className="playsRemaining">
                            {3 - state.playsUsed} play{3 - state.playsUsed !== 1 ? "s" : ""} remaining
                        </span>
                        <button className="secondary" onClick={() => client?.endTurn()}>
                            End Turn
                        </button>
                    </div>
                )}

                {state.phase === "Discard" && isMyTurn && me.hand && (
                    <DiscardModal
                        hand={me.hand}
                        maxHandSize={7}
                        onDiscard={async (cardIds) => {
                            for (const id of cardIds) {
                                await client?.discardCard(id);
                            }
                        }}
                    />
                )}

                <Hand
                    cards={me.hand ?? []}
                    canPlay={isMyTurn === true && (state.phase === "Play" || state.phase === "Discard")}
                    phase={state.phase}
                    gameState={state}
                    myConnectionId={myConnectionId ?? ""}
                    onPlayCard={(cardId, request) => client?.playCard(cardId, request)}
                    onDiscardCard={(cardId) => client?.discardCard(cardId)}
                    onInspectPlayer={isMobile ? setInspectedPlayer : undefined}
                />
            </div>

            {needsResponse && state.pendingAction && (
                <ActionModal
                    pendingAction={state.pendingAction}
                    myState={me}
                    paymentError={state.paymentError}
                    onRespond={(response) => client?.respondToAction(response)}
                    otherPlayers={otherPlayers}
                    onInspect={setInspectedPlayer}
                />
            )}

            {/* Player inspect bottom sheet — z-index above ActionModal */}
            {inspectedPlayer && (
                <PlayerInspectModal
                    player={inspectedPlayer}
                    onClose={() => setInspectedPlayer(null)}
                />
            )}

            {Debug.isFlagSet(DebugFlags.ShowDeck) && client && (
                <DebugDeckViewer client={client} />
            )}
        </div>
    );
}
