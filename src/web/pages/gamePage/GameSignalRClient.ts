import * as signalR from "@microsoft/signalr";
import { GameState, PlayCardRequest, ActionResponse } from "../../Types";
import { Logger } from "../../utilities/Logger";
import { clientKind } from "../../utilities/NativeHost";

export class GameSignalRClient {
    private connection: signalR.HubConnection;
    private onGameStateUpdated: (state: GameState) => void;
    private _gameCode: string = "";
    private _playerName: string = "";
    private _playerId: string = "";

    constructor(onGameStateUpdated: (state: GameState) => void) {
        this.onGameStateUpdated = onGameStateUpdated;

        this.connection = new signalR.HubConnectionBuilder()
            // Tagged so server-side telemetry can still tell the iOS app, the
            // installed PWA, and a plain browser apart. A query parameter is the
            // only thing every SignalR transport carries; a browser cannot set
            // headers on a WebSocket handshake.
            .withUrl(`/hub/game?client=${clientKind()}`)
            .withAutomaticReconnect()
            .build();

        this.connection.on("gameStateUpdated", (state: GameState) => {
            Logger.debug("Game state updated:", state);
            this.onGameStateUpdated(state);
        });

        this.connection.onreconnected(async (connectionId) => {
            Logger.log("Reconnected to SignalR hub, new connectionId:", connectionId, "- rejoining game...");
            if (this._gameCode && this._playerName && this._playerId) {
                try {
                    const success = await this.rejoinGame(this._gameCode, this._playerName, this._playerId);
                    if (success) {
                        Logger.log("Successfully rejoined game", this._gameCode);
                    } else {
                        Logger.warn("Failed to rejoin game after reconnect — server returned false");
                    }
                } catch (err) {
                    Logger.error("Error rejoining game after reconnect:", err);
                }
            } else {
                Logger.warn("Reconnected but missing game context (gameCode/playerName/playerId)");
            }
        });

        this.connection.onreconnecting((error) => {
            Logger.warn("SignalR reconnecting...", error ? `Error: ${error.message}` : "(no error)");
        });

        this.connection.onclose((error) => {
            Logger.warn("SignalR connection closed", error ? `Error: ${error.message}` : "(clean close)");
        });
    }

    async start(): Promise<void> {
        try {
            await this.connection.start();
            Logger.log("Connected to SignalR hub");
        } catch (err) {
            Logger.error("Failed to connect to SignalR hub:", err);
            throw err;
        }
    }

    async stop(): Promise<void> {
        await this.connection.stop();
    }

    async createGame(fixedCode?: string, themeName?: string): Promise<string> {
        return await this.connection.invoke<string>("CreateGame", fixedCode ?? null, themeName ?? null);
    }

    async joinGame(gameCode: string, playerName: string, playerId: string): Promise<void> {
        this._gameCode = gameCode;
        this._playerName = playerName;
        this._playerId = playerId;
        await this.connection.invoke("JoinGame", gameCode, playerName, playerId);
    }

    async rejoinGame(gameCode: string, playerName: string, playerId: string): Promise<boolean> {
        this._gameCode = gameCode;
        this._playerName = playerName;
        this._playerId = playerId;
        return await this.connection.invoke<boolean>("RejoinGame", gameCode, playerName, playerId);
    }

    async addBotPlayer(gameCode: string): Promise<void> {
        await this.connection.invoke("AddBotPlayer", gameCode);
    }

    async startGame(
        gameCode: string,
        allowSinglePlayer: boolean = false,
        populateBoards: boolean = false,
        addBots: boolean = false): Promise<void> {
        await this.connection.invoke("StartGame", gameCode, allowSinglePlayer, populateBoards, addBots);
    }

    async drawCards(): Promise<void> {
        await this.connection.invoke("DrawCards");
    }

    async playCard(cardId: number, request: PlayCardRequest): Promise<void> {
        await this.connection.invoke("PlayCard", cardId, request);
    }

    async endTurn(): Promise<void> {
        await this.connection.invoke("EndTurn");
    }

    async discardCard(cardId: number): Promise<void> {
        await this.connection.invoke("DiscardCard", cardId);
    }

    async cancelDiscard(): Promise<void> {
        await this.connection.invoke("CancelDiscard");
    }

    async respondToAction(response: ActionResponse): Promise<void> {
        await this.connection.invoke("RespondToAction", response);
    }

    async getDebugDeckInfo(): Promise<DebugDeckInfo | null> {
        return await this.connection.invoke<DebugDeckInfo | null>("GetDebugDeckInfo");
    }

    async flipWildcard(cardId: number): Promise<void> {
        await this.connection.invoke("FlipWildcard", cardId);
    }

    async moveProperty(cardId: number, targetSetId: number, targetColor: string | null): Promise<void> {
        await this.connection.invoke("MoveProperty", cardId, targetSetId, targetColor);
    }

    async endGame(): Promise<void> {
        await this.connection.invoke("EndGame");
    }

    async debugCommand(command: string): Promise<string> {
        return await this.connection.invoke<string>("DebugCommand", command);
    }

    /**
     * Associates an APNs device token with this player so the engine can tell
     * them their turn started while the app is backgrounded. Only a native
     * shell has a token; the browser never calls this.
     */
    async registerPushToken(playerId: string, deviceToken: string): Promise<boolean> {
        if (!this.isConnected) return false;
        try {
            return await this.connection.invoke<boolean>("RegisterPushToken", playerId, deviceToken);
        } catch (err) {
            // A missed notification must never break a game in progress.
            Logger.warn("Failed to register push token:", err);
            return false;
        }
    }

    /** Diagnostic: tells the server whether this client can receive push at all. */
    async reportPushStatus(clientKind: string, nativeHost: boolean, hasToken: boolean): Promise<void> {
        if (!this.isConnected) return;
        try {
            await this.connection.invoke("ReportPushStatus", clientKind, nativeHost, hasToken);
        } catch {
            // Diagnostics must never affect play.
        }
    }

    get isConnected(): boolean {
        return this.connection.state === signalR.HubConnectionState.Connected;
    }

    /**
     * Asks the server to answer, rather than trusting our own connection state.
     * iOS freezes sockets while suspended, so a dead connection keeps reporting
     * Connected until a keepalive eventually fails — up to half a minute of a
     * game that looks alive and is not.
     */
    async isAlive(timeoutMs = 1500): Promise<boolean> {
        if (!this.isConnected) return false;
        try {
            await Promise.race([
                this.connection.invoke<boolean>("Ping"),
                new Promise((_, reject) =>
                    setTimeout(() => reject(new Error("ping timed out")), timeoutMs)),
            ]);
            return true;
        } catch {
            return false;
        }
    }

    /**
     * Forces a fresh connection. Stops first even when the old one still claims
     * to be up, since that claim is exactly what we are recovering from.
     */
    async reconnect(): Promise<boolean> {
        try {
            await this.connection.stop();
        } catch {
            // Already down; nothing to stop.
        }
        try {
            await this.connection.start();
            return true;
        } catch (err) {
            Logger.warn("Reconnect after foreground failed:", err);
            return false;
        }
    }

    get connectionId(): string | null {
        return this.connection.connectionId ?? null;
    }
}
