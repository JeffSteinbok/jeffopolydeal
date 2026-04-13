import * as signalR from "@microsoft/signalr";
import { GameState, PlayCardRequest, ActionResponse } from "../../Types";
import { Logger } from "../../utilities/Logger";

export class GameSignalRClient {
    private connection: signalR.HubConnection;
    private onGameStateUpdated: (state: GameState) => void;

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

        this.connection.onreconnected(() => {
            Logger.log("Reconnected to SignalR hub");
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

    async createGame(): Promise<string> {
        return await this.connection.invoke<string>("CreateGame");
    }

    async joinGame(gameCode: string, playerName: string): Promise<void> {
        await this.connection.invoke("JoinGame", gameCode, playerName);
    }

    async startGame(gameCode: string): Promise<void> {
        await this.connection.invoke("StartGame", gameCode);
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

    async respondToAction(response: ActionResponse): Promise<void> {
        await this.connection.invoke("RespondToAction", response);
    }

    get connectionId(): string | null {
        return this.connection.connectionId ?? null;
    }
}
