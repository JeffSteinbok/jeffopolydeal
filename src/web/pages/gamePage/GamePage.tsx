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
import titleImage from "../../assets/JeffopolyDeal.png";
import ShareIcon from "../../assets/Share.svg";
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
    const [showLeaveConfirm, setShowLeaveConfirm] = useState(false);
    const [toasts, setToasts] = useState<GameAction[]>([]);
    const [toastBusy, setToastBusy] = useState(false);
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

    // Show FYI toasts for new actions by other players
    useEffect(() => {
        if (!state) return;

        console.log("[Toast] State update. recentActions:", state.recentActions?.length ?? 0,
            "firstState:", firstStateRef.current,
            "actions:", JSON.stringify(state.recentActions?.map(a => ({ id: a.id, player: a.playerName, text: a.text }))));

        // On first state load, mark all existing actions as already seen
        if (firstStateRef.current) {
            state.recentActions?.forEach((a) => seenActionIdsRef.current.add(a.id));
            firstStateRef.current = false;
            return;
        }

        if (!state.recentActions || state.recentActions.length === 0) return;

        const myName = state.players.find(p => p.playerId === playerId)?.name;
        const newActions = state.recentActions.filter(
            (a) => !seenActionIdsRef.current.has(a.id) && a.playerName !== myName
        );

        console.log("[Toast] myName:", myName, "new actions:", newActions.length,
            "seen IDs:", [...seenActionIdsRef.current]);

        if (newActions.length > 0) {
            console.log("[Toast] Showing:", newActions.map(a => `${a.playerName}: ${a.text}`));
        }

        // Mark all as seen immediately (even if we stagger display)
        newActions.forEach((a) => seenActionIdsRef.current.add(a.id));
        // Also mark my own actions as seen
        state.recentActions.forEach((a) => {
            if (!seenActionIdsRef.current.has(a.id)) seenActionIdsRef.current.add(a.id);
        });

        // Stagger toasts so bot actions appear sequentially
        newActions.forEach((action, idx) => {
            const showDelay = idx * 1200;
            const showTimeoutId = setTimeout(() => {
                console.log("[Toast] Displaying toast:", action.playerName, action.text);
                setToasts((prev) => [...prev, action]);
                const hideTimeoutId = setTimeout(() => {
                    toastTimeoutsRef.current.delete(action.id);
                    setToasts((prev) => prev.filter((t) => t.id !== action.id));
                }, 2500);
                toastTimeoutsRef.current.set(action.id, hideTimeoutId);
            }, showDelay);
            toastTimeoutsRef.current.set(-action.id, showTimeoutId);
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
        setShowLeaveConfirm(true);
    };

    const handleEndGame = async () => {
        if (!window.confirm("End this game? All players will be disconnected and the game state will be cleared.")) return;
        try {
            await client?.endGame();
        } finally {
            onLeave();
        }
    };

    // Enter to draw cards when it's your turn in Draw phase
    useEffect(() => {
        if (!(state?.phase === "Draw" && isMyTurn)) return;
        const handler = (e: KeyboardEvent) => {
            if (e.key === "Enter") client?.drawCards();
        };
        document.addEventListener("keydown", handler);
        return () => document.removeEventListener("keydown", handler);
    }, [state?.phase, isMyTurn, client]);

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
        const connectedCount = state.players.filter(p => p.isConnected).length;
        // Allow any connected player to start if the original host is disconnected
        const canStart = isCreator || (state.players[0] && !state.players[0].isConnected);

        return (
            <div className="gamePage">
                <div className="lobby">
                    <img src={titleImage} alt="Jeffopoly Deal" className="lobbyTitleImage" />
                    <div className="gameCodeDisplay lobbyFadeIn">
                        Game Code: <span className="code">{state.gameCode}</span>
                        {typeof navigator.share === "function" && (
                            <button
                                className="shareButton"
                                onClick={() => navigator.share({
                                    title: "Join my Jeffopoly Deal game!",
                                    text: `Join my game with code: ${state.gameCode}`,
                                    url: `${window.location.origin}?join=${state.gameCode}`,
                                }).catch(() => {})}
                            >
                                <img src={ShareIcon} alt="" className="shareIcon" /> Share
                            </button>
                        )}
                    </div>
                    <div className="playerList">
                        <h3>Players ({connectedCount}/5)</h3>
                        {state.players.map((p) => (
                            <div key={p.playerId} className={`playerName${p.isConnected ? "" : " disconnected"}`}>
                                {p.name} {p.playerId === playerId ? "(you)" : ""}
                                {!p.isConnected && <span className="disconnectedLabel"> (reconnecting...)</span>}
                            </div>
                        ))}
                    </div>
                    {canStart && (
                        <button
                            className="primary"
                            onClick={() => client?.startGame(
                                state.gameCode,
                                minPlayers === 1,
                                Debug.isFlagSet(DebugFlags.PopulatedBoards),
                                Debug.isFlagSet(DebugFlags.PlayVsAi) || Debug.isFlagSet(DebugFlags.PopulatedBoards)
                            )}
                            disabled={connectedCount < minPlayers}
                        >
                            Start Game {connectedCount < minPlayers ? `(need ${minPlayers}+ players)` : ""}
                        </button>
                    )}
                    {!canStart && <p className="waitingText">Waiting for host to start...</p>}
                    <button className="secondary" onClick={onLeave}>Exit Game</button>
                    <div className="copyrightFooter">
                        <p>© {new Date().getFullYear()} Jeff Steinbok. All rights reserved.</p>
                        <p>Monopoly Deal is a trademark of Hasbro, Inc.</p>
                    </div>
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
        <div className={`gamePage${isLandscape ? " gamePage--landscape" : ""}${isMobile ? " gamePage--mobile" : ""}`}>
            <div className="gameHeader">
                <img src={titleImage} alt="Jeffopoly Deal" className="gameHeaderTitleImage" />
                <div className="gameHeader-right">
                    <span className="deckInfo">
                        Draw: {state.drawPileCount} | Discard: {state.discardPileCount} |
                    </span>
                    <button className="exitButton" onClick={handleExitGame}>✕</button>
                </div>
            </div>

            {/* Desktop: side-by-side layout; Mobile: stacked */}
            {isMobile ? (
                <>
                    <div className="otherPlayersArea otherPlayersArea--mobile">
                        {otherPlayers.map((p) => (
                            <PlayerSummaryCard
                                key={p.connectionId}
                                player={p}
                                isCurrentTurn={state.players[state.currentPlayerIndex]?.playerId === p.playerId}
                                onClick={() => setInspectedPlayer(p)}
                            />
                        ))}
                    </div>

                    <PlayerBoard
                        player={me}
                        isMe={true}
                        isMyTurn={isMyTurn === true && state.phase === "Play"}
                        compact={isMobile}
                        onFlipCard={(cardId) => client?.flipWildcard(cardId)}
                        onMoveProperty={(cardId, targetSetId, targetColor) => client?.moveProperty(cardId, targetSetId, targetColor)}
                    />

                    {state.phase === "Discard" && isMyTurn && me.hand && !toastBusy && (
                        <DiscardModal
                            hand={me.hand}
                            maxHandSize={7}
                            onDiscard={async (cardIds) => {
                                for (const id of cardIds) {
                                    await client?.discardCard(id);
                                }
                            }}
                            onCancel={state.playsUsed < 3 ? () => client?.cancelDiscard() : undefined}
                        />
                    )}

                    <Hand
                        cards={me.hand ?? []}
                        canPlay={isMyTurn === true && (state.phase === "Play" || state.phase === "Discard")}
                        phase={state.phase}
                        gameState={state}
                        myConnectionId={myConnectionId ?? ""}
                        smallCards={isMobile}
                        playsRemaining={3 - state.playsUsed}
                        isMyTurn={isMyTurn === true}
                        onEndTurn={() => client?.endTurn()}
                        onPlayCard={(cardId, request) => client?.playCard(cardId, request)}
                        onDiscardCard={(cardId) => client?.discardCard(cardId)}
                        onInspectPlayer={isMobile ? setInspectedPlayer : undefined}
                    />
                </>
            ) : (
                /* Desktop: opponents sidebar + main play area */
                <>
                <div className="desktopLayout">
                    <div className="opponentSidebar">
                        {otherPlayers.map((p) => (
                            <PlayerBoard key={p.connectionId} player={p} compact />
                        ))}
                    </div>

                    <div className="myArea myArea--desktop">
                        <PlayerBoard
                            player={me}
                            isMe={true}
                            isMyTurn={isMyTurn === true && state.phase === "Play"}
                            onFlipCard={(cardId) => client?.flipWildcard(cardId)}
                            onMoveProperty={(cardId, targetSetId, targetColor) => client?.moveProperty(cardId, targetSetId, targetColor)}
                        />

                        {state.phase === "Discard" && isMyTurn && me.hand && !toastBusy && (
                            <DiscardModal
                                hand={me.hand}
                                maxHandSize={7}
                                onDiscard={async (cardIds) => {
                                    for (const id of cardIds) {
                                        await client?.discardCard(id);
                                    }
                                }}
                                onCancel={state.playsUsed < 3 ? () => client?.cancelDiscard() : undefined}
                            />
                        )}

                        <Hand
                            cards={me.hand ?? []}
                            canPlay={isMyTurn === true && (state.phase === "Play" || state.phase === "Discard")}
                            phase={state.phase}
                            gameState={state}
                            myConnectionId={myConnectionId ?? ""}
                            smallCards={true}
                            playsRemaining={3 - state.playsUsed}
                            isMyTurn={isMyTurn === true}
                            onEndTurn={() => client?.endTurn()}
                            onPlayCard={(cardId, request) => client?.playCard(cardId, request)}
                            onDiscardCard={(cardId) => client?.discardCard(cardId)}
                        />
                    </div>
                </div>
                </>
            )}

            {state.phase === "Draw" && isMyTurn && !toastBusy && (
                <div className="modalOverlay" style={{ alignItems: "center" }}>
                    <div className="drawTurnPopup">
                        <h2 className="drawTurnPopup-title">It's Your Turn!</h2>
                        <button className="primary drawTurnPopup-btn" onClick={() => client?.drawCards()}>
                            Draw Cards
                        </button>
                    </div>
                </div>
            )}

            {needsResponse && state.pendingAction && !toastBusy && (
                <ActionModal
                    pendingAction={state.pendingAction}
                    myState={me}
                    paymentError={state.paymentError}
                    onRespond={(response) => client?.respondToAction(response)}
                    otherPlayers={otherPlayers}
                    onInspect={setInspectedPlayer}
                />
            )}

            <FyiToast toasts={toasts} smallCards={isMobile} onBusyChange={setToastBusy} />

            {/* Player inspect bottom sheet — z-index above ActionModal */}
            {inspectedPlayer && (
                <PlayerInspectModal
                    player={inspectedPlayer}
                    onClose={() => setInspectedPlayer(null)}
                />
            )}

            {/* Leave game confirmation dialog */}
            {showLeaveConfirm && (
                <div className="modalOverlay leaveConfirmOverlay" onClick={() => setShowLeaveConfirm(false)}>
                    <div className="leaveConfirmDialog" onClick={e => e.stopPropagation()}>
                        <h3>Leave Game?</h3>
                        <p>Are you sure you want to leave the game? This will end the game for all players.</p>
                        <div className="leaveConfirmButtons">
                            <button className="primary" onClick={() => { setShowLeaveConfirm(false); onLeave(); }}>
                                Leave Game
                            </button>
                            <button className="secondary" onClick={() => setShowLeaveConfirm(false)}>
                                Cancel
                            </button>
                        </div>
                    </div>
                </div>
            )}

            {Debug.isFlagSet(DebugFlags.ShowDeck) && client && (
                <DebugDeckViewer client={client} />
            )}
        </div>
    );
}
