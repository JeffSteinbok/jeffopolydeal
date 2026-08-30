import React, { useEffect, useState, useRef } from "react";
import { GameSignalRClient } from "./GameSignalRClient";
import { GameState, PlayerState, GameAction } from "../../Types";
import { Logger } from "../../utilities/Logger";
import { Debug, DebugFlags } from "../../utilities/Debug";
import { GameConfigProvider } from "../../utilities/GameConfigContext";
import { CardComponent } from "./components/Card";
import { PlayerBoard } from "./components/PlayerBoard";
import { PlayerSummaryCard } from "./components/PlayerSummaryCard";
import { PlayerInspectModal } from "./components/PlayerInspectModal";
import { Hand } from "./components/Hand";
import { ActionModal } from "./components/ActionModal";
import { DiscardModal } from "./components/DiscardModal";
import { FyiToast } from "./components/FyiToast";
import { DebugDeckViewer } from "./components/DebugDeckViewer";
import { DebugConsole } from "./components/DebugConsole";
import { copyTextToClipboard, formatGameLog, buildHangIssueUrl } from "./gameLog";
import { deriveHaptics, emitDerivedHaptics } from "../../utilities/Haptics";
import { postToNativeHost } from "../../utilities/NativeBridge";
import { onPushToken, onReturnToForeground } from "../../utilities/NativeInbound";
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
    const [showHangHelp, setShowHangHelp] = useState(false);
    const [showGameMenu, setShowGameMenu] = useState(false);
    const [toasts, setToasts] = useState<GameAction[]>([]);
    const [toastBusy, setToastBusy] = useState(false);
    const [copyLogStatus, setCopyLogStatus] = useState<"idle" | "copied" | "failed">("idle");
    const clientRef = useRef<GameSignalRClient | null>(null);
    const seenActionIdsRef = useRef<Set<number>>(new Set());
    const hapticStateRef = useRef<GameState | null>(null);
    const firstStateRef = useRef(true);
    const toastTimeoutsRef = useRef<Map<number, ReturnType<typeof setTimeout>>>(new Map());
    const copyLogTimeoutRef = useRef<ReturnType<typeof setTimeout>>();
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
                    const themeParam = new URLSearchParams(window.location.search).get("theme") || undefined;
                    const newCode = await client.createGame(useFixedCode ? "TEST" : undefined, themeParam);
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

    // Tell a native shell which game we are in and what it is doing, so it can
    // drive capabilities the web cannot — advertising this lobby to nearby
    // devices, and remembering enough to rejoin after a cold launch. Deliberately
    // not gameplay state: the shell has no business interpreting a board.
    // A native shell holds the APNs token; this client owns the hub connection.
    // Wait until we are actually in a game, since the engine keys tokens by
    // player and there is nothing to notify about before then. onPushToken
    // fires immediately if a token already arrived, so ordering does not matter.
    const hasJoined = !!state;
    useEffect(() => {
        if (!playerId || !hasJoined) return;
        return onPushToken((token) => {
            clientRef.current?.registerPushToken(playerId, token);
        });
    }, [playerId, hasJoined]);

    // Coming back to the foreground: iOS may have frozen or killed the socket
    // while suspended, and SignalR's own reconnect cannot run while it is. Check
    // and recover rather than assuming the connection survived.
    useEffect(() => onReturnToForeground(async () => {
        const client = clientRef.current;
        if (!client || client.isConnected) return;
        const reconnected = await client.ensureConnected();
        if (reconnected && gameCode && playerName && playerId) {
            await client.rejoinGame(gameCode, playerName, playerId);
        }
    }), [gameCode, playerName, playerId]);

    // Narrowed to the fields the shell cares about, so a board update does not
    // re-announce a game that has not changed.
    const contextGameCode = state?.gameCode ?? null;
    const contextPhase = state?.phase ?? null;
    const contextHostName = state?.players[0]?.name ?? null;

    useEffect(() => {
        if (!contextGameCode) return;
        postToNativeHost("gameContext", {
            gameCode: contextGameCode,
            phase: contextPhase,
            playerId,
            playerName,
            hostName: contextHostName,
        });
    }, [contextGameCode, contextPhase, contextHostName, playerId, playerName]);

    // Leaving a game means there is no game to be in. Told separately from
    // gameContext because the shell must stop advertising even though no new
    // state arrives after this point.
    useEffect(() => () => { postToNativeHost("gameContext", { gameCode: null, phase: null }); }, []);

    // Semantic haptics for a native shell. Derived from the state transition in
    // one place rather than sprinkled through handlers, so every event is
    // id-stamped and a replayed state cannot replay feedback. No-ops in a
    // browser or PWA, where no bridge is listening.
    useEffect(() => {
        if (!state) return;
        emitDerivedHaptics(deriveHaptics(hapticStateRef.current, state, playerId));
        hapticStateRef.current = state;
    }, [state, playerId]);

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
            (a) => !seenActionIdsRef.current.has(a.id)
                && a.playerName !== myName
                && !(a.targetPlayerName === myName && !a.text.startsWith("Paid"))
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
            clearTimeout(copyLogTimeoutRef.current);
        };
    }, []);

    const client = clientRef.current;
    const me = state?.players.find((p) => p.playerId === playerId);
    const myConnectionId = me?.connectionId;
    const isMyTurn = state && me && state.players[state.currentPlayerIndex]?.playerId === playerId;
    const isCreator = state && me && state.players[0]?.playerId === playerId;

    const handleExitGame = () => {
        setShowGameMenu(false);
        setShowLeaveConfirm(true);
    };

    const handleReportHang = async () => {
        setShowGameMenu(false);
        await handleCopyGameLog();
        setShowHangHelp(true);
    };

    const handleCopyGameLog = async () => {
        if (!state) return;

        try {
            await copyTextToClipboard(formatGameLog(state, playerId));
            setCopyLogStatus("copied");
        } catch (err) {
            Logger.error("Failed to copy game log:", err);
            setCopyLogStatus("failed");
        }

        clearTimeout(copyLogTimeoutRef.current);
        copyLogTimeoutRef.current = setTimeout(() => setCopyLogStatus("idle"), 2000);
    };

    const copyLogLabel = copyLogStatus === "copied"
        ? "Copied!"
        : copyLogStatus === "failed"
            ? "Copy failed"
            : "Copy log";

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
                        {canStart && state.players.length < 5 && (
                            <button
                                className="addBotButton"
                                onClick={() => client?.addBotPlayer(state.gameCode)}
                            >
                                + Add Bot Player
                            </button>
                        )}
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

    // Game Over (or debug skip-to-game-over)
    const showGameOver = state.phase === "GameOver" || Debug.isFlagSet(DebugFlags.SkipToGameOver);

    // Wrap all card-rendering views in the config provider (game over + active game)
    if (!state.gameConfig) {
        return <div className="gamePage"><div className="loading">Loading...</div></div>;
    }

    if (showGameOver) {
        const winner = state.phase === "GameOver"
            ? state.players.find(p => p.playerId === state.winnerId)
            : state.players[0];
        const winnerName = state.phase === "GameOver" ? state.winnerName : (winner?.name ?? playerName);
        const realSets = winner?.propertySets.filter(s => s.isComplete) ?? [];

        // Debug mock sets when no real complete sets exist
        const completeSets = realSets.length > 0 ? realSets : (Debug.isFlagSet(DebugFlags.SkipToGameOver) ? [
            {
                setId: 901, color: "Brown" as PropertyColor, isComplete: true, hasHouse: false, hasHotel: false, rent: 2, requiredSize: 2,
                cards: [
                    { id: 901, cardType: "Property" as CardType, moneyValue: 1, name: "Chan's Market", color: "Brown" as PropertyColor, isMulticolorWild: false, isWildRent: false },
                    { id: 902, cardType: "Property" as CardType, moneyValue: 1, name: "Wendy's", color: "Brown" as PropertyColor, isMulticolorWild: false, isWildRent: false },
                ],
            },
            {
                setId: 902, color: "Red" as PropertyColor, isComplete: true, hasHouse: false, hasHotel: false, rent: 4, requiredSize: 3,
                cards: [
                    { id: 903, cardType: "Property" as CardType, moneyValue: 3, name: "Sushi Me", color: "Red" as PropertyColor, isMulticolorWild: false, isWildRent: false },
                    { id: 904, cardType: "Property" as CardType, moneyValue: 3, name: "Din Tai Fung", color: "Red" as PropertyColor, isMulticolorWild: false, isWildRent: false },
                    { id: 905, cardType: "Property" as CardType, moneyValue: 3, name: "Prime Steakhouse", color: "Red" as PropertyColor, isMulticolorWild: false, isWildRent: false },
                ],
            },
            {
                setId: 903, color: "DarkBlue" as PropertyColor, isComplete: true, hasHouse: false, hasHotel: false, rent: 8, requiredSize: 2,
                cards: [
                    { id: 906, cardType: "Property" as CardType, moneyValue: 4, name: "False Creek", color: "DarkBlue" as PropertyColor, isMulticolorWild: false, isWildRent: false },
                    { id: 907, cardType: "Property" as CardType, moneyValue: 4, name: "Lake Sammamish", color: "DarkBlue" as PropertyColor, isMulticolorWild: false, isWildRent: false },
                ],
            },
        ] : []);
        return (
            <GameConfigProvider config={state.gameConfig}>
            <div className="gamePage">
                <div className="gameOver">
                    <img src={titleImage} alt="Jeffopoly Deal" className="gameOver-logo" />
                    <p className="winnerName">🎉 {winnerName} Wins! 🎉</p>
                    {completeSets.length > 0 && (
                        <div className="gameOver-sets">
                            {completeSets.map((set) => (
                                <div key={set.setId} className="gameOver-set">
                                    {set.cards.map((card, idx) => (
                                        <div key={card.id} className="gameOver-set-card" style={idx > 0 ? { marginTop: -95 } : undefined}>
                                            <CardComponent card={card} small />
                                        </div>
                                    ))}
                                </div>
                            ))}
                        </div>
                    )}
                    <button
                        className="secondary copyLogButton"
                        onClick={() => { void handleCopyGameLog(); }}
                        title="Copy the current game state and recent actions for a bug report"
                    >
                        {copyLogLabel}
                    </button>
                    <button className="primary" onClick={onLeave}>Play Again</button>
                </div>
            </div>
            </GameConfigProvider>
        );
    }

    // Active Game
    const otherPlayers = state.players.filter((p) => p.playerId !== playerId);
    const needsResponse = state.phase === "AwaitingResponse" &&
        state.pendingAction?.targetPlayerIds.includes(myConnectionId ?? "");

    return (
        <GameConfigProvider config={state.gameConfig}>
        <div className={`gamePage${isLandscape ? " gamePage--landscape" : ""}${isMobile ? " gamePage--mobile" : ""}`}>
            <div className="gameHeader">
                <img src={titleImage} alt="Jeffopoly Deal" className="gameHeaderTitleImage" />
                {Debug.flags !== DebugFlags.None && (
                    <DebugConsole
                        client={clientRef.current}
                        playerNames={state?.players.map(p => p.name) ?? []}
                        onShowToast={(action) => {
                            action.persistent = true;
                            setToasts((prev) => [...prev, action]);
                        }}
                    />
                )}
                <div className="gameHeader-right">
                    <span className="deckInfo">
                        Draw: {state.drawPileCount} | Discard: {state.discardPileCount} |
                    </span>
                    <div className="gameMenu">
                        <button className="gameMenuButton" onClick={() => setShowGameMenu(!showGameMenu)}>☰</button>
                        {copyLogStatus !== "idle" && (
                            <span className={`gameMenuCopyStatus ${copyLogStatus === "copied" ? "gameMenuCopyStatus--ok" : "gameMenuCopyStatus--fail"}`}>
                                {copyLogStatus === "copied" ? "✓ Log copied!" : "✗ Copy failed"}
                            </span>
                        )}
                        {showGameMenu && (
                            <>
                                <div className="gameMenuBackdrop" onClick={() => setShowGameMenu(false)} />
                                <div className="gameMenuDropdown">
                                    <button className="gameMenuItem" onClick={() => { void handleReportHang(); }}>
                                        🐛 Report Hang
                                    </button>
                                    <button className="gameMenuItem gameMenuItem--danger" onClick={handleExitGame}>
                                        Leave Game
                                    </button>
                                </div>
                            </>
                        )}
                    </div>
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
                            <PlayerBoard
                                key={p.connectionId}
                                player={p}
                                isCurrentTurn={state.players[state.currentPlayerIndex]?.playerId === p.playerId}
                                compact
                            />
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
                        <button className="drawTurnPopup-btn" onClick={() => client?.drawCards()}>
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

            <FyiToast toasts={toasts} smallCards={isMobile} myName={me?.name} onBusyChange={setToastBusy} />

            {/* Player inspect bottom sheet — z-index above ActionModal */}
            {inspectedPlayer && (
                <PlayerInspectModal
                    player={inspectedPlayer}
                    onClose={() => setInspectedPlayer(null)}
                />
            )}

            {/* Report Hang help dialog */}
            {showHangHelp && (
                <div className="modalOverlay leaveConfirmOverlay" onClick={() => setShowHangHelp(false)}>
                    <div className="leaveConfirmDialog hangHelpDialog" onClick={e => e.stopPropagation()}>
                        <h3>🐛 Report a Hang</h3>
                        <p>
                            The game log has been copied to your clipboard. Click <strong>Open GitHub Issue</strong> to
                            file a report — the log is pre-filled, so just describe what you were doing and submit.
                        </p>
                        <div className="leaveConfirmButtons">
                            <button
                                className="primary"
                                onClick={() => {
                                    window.open(
                                        buildHangIssueUrl(formatGameLog(state, playerId), state.gameCode),
                                        "_blank",
                                        "noopener,noreferrer",
                                    );
                                    setShowHangHelp(false);
                                }}
                            >
                                Open GitHub Issue
                            </button>
                            <button className="secondary" onClick={() => { void handleCopyGameLog(); }}>
                                Copy log again
                            </button>
                            <button className="secondary" onClick={() => setShowHangHelp(false)}>
                                Close
                            </button>
                        </div>
                    </div>
                </div>
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
        </GameConfigProvider>
    );
}
