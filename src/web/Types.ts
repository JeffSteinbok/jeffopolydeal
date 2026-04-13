// Shared TypeScript types matching the C# models

export type PropertyColor =
    | "Brown" | "LightBlue" | "Pink" | "Orange" | "Red"
    | "Yellow" | "Green" | "DarkBlue" | "Railroad" | "Utility";

export type CardType = "Money" | "Property" | "PropertyWildcard" | "Rent" | "Action";

export type ActionType =
    | "PassGo" | "DebtCollector" | "ItsMyBirthday" | "SlyDeal"
    | "ForceDeal" | "DealBreaker" | "JustSayNo" | "DoubleTheRent"
    | "House" | "Hotel";

export type GamePhase = "Lobby" | "Draw" | "Play" | "AwaitingResponse" | "Discard" | "GameOver";

export type PendingActionType =
    | "PayRent" | "PayDebtCollector" | "PayBirthday"
    | "RespondToSlyDeal" | "RespondToForceDeal" | "RespondToDealBreaker"
    | "JustSayNoChain";

export interface Card {
    id: number;
    cardType: CardType;
    moneyValue: number;
    name: string;
    color?: PropertyColor;
    altColor?: PropertyColor;
    isMulticolorWild: boolean;
    rentColors?: PropertyColor[];
    isWildRent: boolean;
    actionKind?: ActionType;
    activeColor?: PropertyColor;
}

export interface PropertySetState {
    setId: number;
    color: PropertyColor;
    cards: Card[];
    isComplete: boolean;
    hasHouse: boolean;
    hasHotel: boolean;
    rent: number;
    requiredSize: number;
}

export interface PlayerState {
    connectionId: string;
    name: string;
    handCount: number;
    hand?: Card[];
    bank: Card[];
    propertySets: PropertySetState[];
    unboundWilds: Card[];
    completedSetCount: number;
    uniqueCompletedSetCount: number;
}

export interface PendingAction {
    type: PendingActionType;
    sourcePlayerId: string;
    sourcePlayerName: string;
    targetPlayerIds: string[];
    amount: number;
    targetCardId?: number;
    targetCardName?: string;
    offeredCardId?: number;
    offeredCardName?: string;
    targetSetColor?: PropertyColor;
    justSayNoResponderId?: string;
}

export interface GameState {
    phase: GamePhase;
    gameCode: string;
    players: PlayerState[];
    currentPlayerIndex: number;
    playsUsed: number;
    drawPileCount: number;
    discardPileCount: number;
    topDiscard?: Card;
    pendingAction?: PendingAction;
    winnerId?: string;
    winnerName?: string;
}

export interface PlayCardRequest {
    playAsMoney: boolean;
    wildcardColor?: PropertyColor;
    rentColor?: PropertyColor;
    targetPlayerId?: string;
    targetCardId?: number;
    offeredCardId?: number;
    targetSetColor?: PropertyColor;
    doubleRentCardIds?: number[];
}

export interface ActionResponse {
    playJustSayNo: boolean;
    paymentCardIds?: number[];
}

export interface DebugPlayerHand {
    playerName: string;
    cards: Card[];
}

export interface DebugDeckInfo {
    drawPile: Card[];
    discardPile: Card[];
    playerHands: DebugPlayerHand[];
}
