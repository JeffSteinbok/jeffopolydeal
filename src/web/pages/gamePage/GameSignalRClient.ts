import * as signalR from "@microsoft/signalr";
import { GameState, PlayCardRequest, ActionResponse } from "../../Types";
import { Logger } from "../../utilities/Logger";

export class GameSignalRClient {
    private connection: signalR.HubConnection;
    private onGameStateUpdated: (state: GameState) => void;
    private _gameCode: string = "";
    private _playerName: string = "";
    private _playerId: string = "";

    constructor(onGameStateUpdated: (state: GameState) => void) {
        this.onGameStateUpdated = onGameStateUpdated;

        this.connection = new signalR.HubConnectionBuilder()
            .withUrl("/hub/game")
            .withAutomaticReconnect()
            .build();

        this.connection.on("gameStateUpdated", (state: GameState) => {
            Logger.debug("Game state updated:", state);
            this.onGameStateUpdated(state);
        });

        this.connection.onreconnected(async () => {
            Logger.log("Reconnected to SignalR hub, rejoining game...");
            if (this._gameCode && this._playerName && this._playerId) {
                try {
                    const success = await this.rejoinGame(this._gameCode, this._playerName, this._playerId);
                    if (!success) {
                        Logger.warn("Failed to rejoin game after reconnect");
                    }
                } catch (err) {
                    Logger.error("Error rejoining game after reconnect:", err);
                }
            }
        });

        this.connection.onclose(() => {
            Logger.warn("SignalR connection closed");
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

    async createGame(fixedCode?: string): Promise<string> {
        return await this.connection.invoke<string>("CreateGame", fixedCode ?? null);
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

    get connectionId(): string | null {
        return this.connection.connectionId ?? null;
    }
}
