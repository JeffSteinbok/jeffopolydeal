import { Card, GameAction, GameState, PendingAction, PlayerState } from "../../Types";

function formatCardNames(cards?: Card[]): string {
    if (!cards || cards.length === 0) return "none";
    return cards.map((card) => card.name).join(", ");
}

function formatPlayerLine(player: PlayerState, currentPlayerId?: string): string {
    const labels = [
        player.playerId === currentPlayerId ? "you" : null,
        player.isConnected ? "connected" : "disconnected",
        `hand=${player.hand?.length ?? player.handCount}`,
        `bank=${player.bank.length}`,
        `sets=${player.propertySets.length}`,
        `completed=${player.completedSetCount}`,
    ].filter(Boolean);

    return `- ${player.name} (${labels.join(", ")})`;
}

function formatPendingAction(pendingAction: PendingAction): string {
    const parts = [
        pendingAction.type,
        `source=${pendingAction.sourcePlayerName}`,
        pendingAction.targetPlayerIds.length > 0 ? `targets=${pendingAction.targetPlayerIds.join(",")}` : null,
        pendingAction.amount > 0 ? `amount=◆${pendingAction.amount}` : null,
        pendingAction.targetCardName ? `targetCard=${pendingAction.targetCardName}` : null,
        pendingAction.offeredCardName ? `offeredCard=${pendingAction.offeredCardName}` : null,
        pendingAction.targetSetColor ? `targetSet=${pendingAction.targetSetColor}` : null,
    ].filter(Boolean);

    return parts.join("; ");
}

function formatAction(action: GameAction): string {
    const details = [
        action.cardPlayed ? `card=${action.cardPlayed.name}` : null,
        action.targetPlayerName ? `target=${action.targetPlayerName}` : null,
        action.sourceCards?.length ? `gave=${formatCardNames(action.sourceCards)}` : null,
        action.targetCards?.length ? `got=${formatCardNames(action.targetCards)}` : null,
    ].filter(Boolean);

    return `- [${action.id}] ${action.playerName}: ${action.text}${details.length > 0 ? ` (${details.join("; ")})` : ""}`;
}

export function formatGameLog(state: GameState, currentPlayerId?: string, generatedAt = new Date()): string {
    const currentPlayer = state.players[state.currentPlayerIndex];
    const me = currentPlayerId ? state.players.find((player) => player.playerId === currentPlayerId) : undefined;

    const sections = [
        "Jeffopoly Deal game log",
        `Generated: ${generatedAt.toISOString()}`,
        `Game code: ${state.gameCode}`,
        `Phase: ${state.phase}`,
        `Current player: ${currentPlayer?.name ?? "n/a"}`,
        `Draw pile: ${state.drawPileCount}`,
        `Discard pile: ${state.discardPileCount}`,
        state.winnerName ? `Winner: ${state.winnerName}` : null,
        state.pendingAction ? `Pending action: ${formatPendingAction(state.pendingAction)}` : null,
        "",
        "Players:",
        ...state.players.map((player) => formatPlayerLine(player, currentPlayerId)),
        me?.hand ? `Visible hand: ${formatCardNames(me.hand)}` : null,
        "",
        "Recent actions:",
        ...(state.recentActions.length > 0 ? state.recentActions.map(formatAction) : ["- none"]),
    ].filter((line): line is string => line !== null);

    return sections.join("\n");
}

export async function copyTextToClipboard(text: string): Promise<void> {
    if (typeof navigator !== "undefined" && navigator.clipboard?.writeText) {
        await navigator.clipboard.writeText(text);
        return;
    }

    if (typeof document === "undefined") {
        throw new Error("Clipboard is unavailable");
    }

    const textArea = document.createElement("textarea");
    textArea.value = text;
    textArea.setAttribute("readonly", "true");
    textArea.style.position = "fixed";
    textArea.style.opacity = "0";
    document.body.appendChild(textArea);
    textArea.select();
    textArea.setSelectionRange(0, text.length);

    // Deprecated, but still used as a last-resort fallback when the async Clipboard API is unavailable.
    const copied = document.execCommand("copy");
    document.body.removeChild(textArea);

    if (!copied) {
        throw new Error("Clipboard copy failed");
    }
}
