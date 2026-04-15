import React, { useEffect, useState, useRef } from "react";
import { GameSignalRClient } from "./GameSignalRClient";
import { GameState, Card, PlayCardRequest, PlayerState, GameAction } from "../../Types";
import { Logger } from "../../utilities/Logger";
import { Debug, DebugFlags } from "../../utilities/Debug";
import { CardComponent } from "./components/Card";
import { PlayerBoard } from "./components/PlayerBoard";
import { PlayerSummaryCard } from "./components/PlayerSummaryCard";
import { PlayerInspectModal } from "./components/PlayerInspectModal";
import { Hand } from "./components/Hand";
import { ActionModal } from "./components/ActionModal";
import { DiscardModal } from "./components/DiscardModal";
import { FyiToast } from "./components/FyiToast";
import { DebugDeckViewer } from "./components/DebugDeckViewer";
import titleImage from "../../assets/JeffopolyTitle.png";
import "./styles/game.css";

function useIsMobile(breakpoint = 680): boolean {
    // Treat landscape phone as mobile too (wide but short screen)
    const check = () => window.innerWidth <= breakpoint ||
        (window.innerWidth < 900 && window.innerHeight < 500);
    const [isMobile, setIsMobile] = useState(check);
    useEffect(() => {
        let timer: ReturnType<typeof setTimeout>;
        const handler = () => {
            clearTimeout(timer);
            timer = setTimeout(() => setIsMobile(check()), 100);
        };
        window.addEventListener("resize", handler);
        return () => { window.removeEventListener("resize", handler); clearTimeout(timer); };
    }, [breakpoint]);
    return isMobile;
}

function useIsLandscapePhone(): boolean {
    const check = () =>
        window.innerWidth < 1024 &&
        window.innerHeight < 500 &&
        window.innerWidth > window.innerHeight;
    const [isLandscapePhone, setIsLandscapePhone] = useState(check);
    useEffect(() => {
        let timer: ReturnType<typeof setTimeout>;
        const handler = () => {
            clearTimeout(timer);
            timer = setTimeout(() => setIsLandscapePhone(check()), 100);
        };
        window.addEventListener("resize", handler);
        return () => { window.removeEventListener("resize", handler); clearTimeout(timer); };
    }, []);
    return isLandscapePhone;
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
    const [toasts, setToasts] = useState<GameAction[]>([]);
    const clientRef = useRef<GameSignalRClient | null>(null);
    const seenActionIdsRef = useRef<Set<number>>(new Set());
    const firstStateRef = useRef(true);
    const toastTimeoutsRef = useRef<Map<number, ReturnType<typeof setTimeout>>>(new Map());
    const isMobile = useIsMobile();
    const isLandscape = useIsLandscapePhone();

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
                    const useFixedCode = Debug.isFlagSet(DebugFlags.FixedGameCode);
                    const newCode = await client.createGame(useFixedCode ? "TEST" : undefined);
                    await client.joinGame(newCode, playerName, playerId);
                    onGameCodeResolved?.(newCode);

                    // Auto-start when SkipLobby is set
                    if (Debug.isFlagSet(DebugFlags.SkipLobby)) {
                        const populate = Debug.isFlagSet(DebugFlags.PopulatedBoards);
                        const addBots = populate || Debug.isFlagSet(DebugFlags.PlayVsAi);
                        await client.startGame(newCode, true, populate, addBots);
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

    // Show FYI toasts for new actions when it's not my turn
    useEffect(() => {
        if (!state) return;

        // On first state load, mark all existing actions as already seen
        if (firstStateRef.current) {
            state.recentActions.forEach((a) => seenActionIdsRef.current.add(a.id));
            firstStateRef.current = false;
            return;
        }

        // Only show toasts when it's not the current player's turn
        const currentIsMyTurn = state.players[state.currentPlayerIndex]?.playerId === playerId;
        if (currentIsMyTurn) return;

        const newActions = state.recentActions.filter(
            (a) => !seenActionIdsRef.current.has(a.id)
        );

        newActions.forEach((action) => {
            seenActionIdsRef.current.add(action.id);
            setToasts((prev) => [...prev, action]);
            const timeoutId = setTimeout(() => {
                toastTimeoutsRef.current.delete(action.id);
                setToasts((prev) => prev.filter((t) => t.id !== action.id));
            }, 2000);
            toastTimeoutsRef.current.set(action.id, timeoutId);
        });
    }, [state, playerId]);

    // Clear all pending toast timeouts on unmount
    useEffect(() => {
        return () => {
            toastTimeoutsRef.current.forEach(clearTimeout);
            toastTimeoutsRef.current.clear();
        };
    }, []);

    const client = clientRef.current;
    const me = state?.players.find((p) => p.playerId === playerId);
    const myConnectionId = me?.connectionId;
    const isMyTurn = state && me && state.players[state.currentPlayerIndex]?.playerId === playerId;
    const isCreator = state && me && state.players[0]?.playerId === playerId;

    const handleExitGame = () => {
        if (window.confirm("Leave the game?")) onLeave();
    };

    const handleEndGame = async () => {
        if (!window.confirm("End this game? All players will be disconnected and the game state will be cleared.")) return;
        try {
            await client?.endGame();
        } finally {
            onLeave();
        }
    };

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

    const minPlayers = (Debug.isFlagSet(DebugFlags.SkipLobby) || Debug.isFlagSet(DebugFlags.PlayVsAi)) ? 1 : 2;

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
                            onClick={() => client?.startGame(
                                state.gameCode,
                                minPlayers === 1,
                                Debug.isFlagSet(DebugFlags.PopulatedBoards),
                                Debug.isFlagSet(DebugFlags.PlayVsAi) || Debug.isFlagSet(DebugFlags.PopulatedBoards)
                            )}
                            disabled={state.players.length < minPlayers}
                        >
                            Start Game {state.players.length < minPlayers ? `(need ${minPlayers}+ players)` : ""}
                        </button>
                    )}
                    {!isCreator && <p className="waitingText">Waiting for host to start...</p>}
                    <button className="secondary" onClick={onLeave}>Exit Game</button>
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
        <div className={`gamePage${isLandscape ? " gamePage--landscape" : ""}`}>
            <div className="gameHeader">
                <span className="gameCodeSmall">{state.gameCode}</span>
                <img src={titleImage} alt="Jeffopoly Deal" className="gameHeaderTitleImage" />
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
                    compact={isMobile}
                    onFlipCard={(cardId) => client?.flipWildcard(cardId)}
                    onMoveProperty={(cardId, targetSetId, targetColor) => client?.moveProperty(cardId, targetSetId, targetColor)}
                />

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
                    smallCards={isMobile}
                    onPlayCard={(cardId, request) => client?.playCard(cardId, request)}
                    onDiscardCard={(cardId) => client?.discardCard(cardId)}
                    onInspectPlayer={isMobile ? setInspectedPlayer : undefined}
                />

                <div className="mainControls">
                    <div className="mainControls-left">
                        <button className="endGameButton" onClick={handleEndGame}>
                            End Game
                        </button>
                        <button className="exitButton" onClick={handleExitGame}>
                            Exit
                        </button>
                    </div>
                    <div className="mainControls-right">
                        {state.phase === "Play" && isMyTurn && (
                            <span className="playsRemaining">
                                {3 - state.playsUsed} play{3 - state.playsUsed !== 1 ? "s" : ""} remaining
                            </span>
                        )}
                        {state.phase === "Draw" && isMyTurn && (
                            <button className="primary" onClick={() => client?.drawCards()}>
                                Draw Cards
                            </button>
                        )}
                        {state.phase === "Play" && isMyTurn && (
                            <button className="secondary" onClick={() => client?.endTurn()}>
                                End Turn
                            </button>
                        )}
                    </div>
                </div>
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

            <FyiToast toasts={toasts} />

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
