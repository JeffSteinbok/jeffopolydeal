import { formatGameLog, buildHangIssueUrl } from "./gameLog";
import { GameState } from "../../Types";

function makeState(): GameState {
    return {
        phase: "AwaitingResponse",
        gameCode: "ABCD",
        currentPlayerIndex: 1,
        playsUsed: 2,
        drawPileCount: 31,
        discardPileCount: 4,
        pendingAction: {
            type: "PayRent",
            sourcePlayerId: "p2",
            sourcePlayerName: "Bob",
            targetPlayerIds: ["p1"],
            amount: 3,
        },
        players: [
            {
                playerId: "p1",
                connectionId: "c1",
                name: "Alice",
                handCount: 3,
                isConnected: true,
                hand: [
                    { id: 1, cardType: "Action", moneyValue: 1, name: "Pass Go", actionKind: "PassGo", isMulticolorWild: false, isWildRent: false },
                    { id: 2, cardType: "Money", moneyValue: 3, name: "3M", isMulticolorWild: false, isWildRent: false },
                ],
                bank: [],
                propertySets: [],
                unboundWilds: [],
                completedSetCount: 0,
                uniqueCompletedSetCount: 0,
            },
            {
                playerId: "p2",
                connectionId: "c2",
                name: "Bob",
                handCount: 5,
                isConnected: false,
                bank: [],
                propertySets: [],
                unboundWilds: [],
                completedSetCount: 1,
                uniqueCompletedSetCount: 1,
            },
        ],
        recentActions: [
            {
                id: 9,
                playerName: "Bob",
                text: "Charged Rent ◆3",
                targetPlayerName: "Alice",
                cardPlayed: { id: 10, cardType: "Rent", moneyValue: 1, name: "Rainbow Rent", isMulticolorWild: false, isWildRent: true },
                targetCards: [
                    { id: 11, cardType: "Money", moneyValue: 2, name: "2M", isMulticolorWild: false, isWildRent: false },
                ],
            },
        ],
        gameConfig: {
            setSize: { Brown: 2, LightBlue: 3, Pink: 3, Orange: 3, Red: 3, Yellow: 3, Green: 3, DarkBlue: 2, Railroad: 4, Utility: 2 },
            rentTable: { Brown: [0, 1, 2], LightBlue: [0, 1, 2, 3], Pink: [0, 1, 2, 4], Orange: [0, 1, 3, 5], Red: [0, 2, 3, 6], Yellow: [0, 2, 4, 6], Green: [0, 2, 4, 7], DarkBlue: [0, 3, 8], Railroad: [0, 1, 2, 3, 4], Utility: [0, 1, 2] },
        },
    };
}

describe("formatGameLog", () => {
    it("includes current game context, visible hand, and recent actions", () => {
        const log = formatGameLog(makeState(), "p1", new Date("2026-05-10T12:00:00.000Z"));

        expect(log).toContain("Jeffopoly Deal game log");
        expect(log).toContain("Generated: 2026-05-10T12:00:00.000Z");
        expect(log).toContain("Game code: ABCD");
        expect(log).toContain("Phase: AwaitingResponse");
        expect(log).toContain("Current player: Bob");
        expect(log).toContain("Pending action: PayRent; source=Bob; targets=p1; amount=◆3");
        expect(log).toContain("- Alice (you, connected, hand=2, bank=0, sets=0, completed=0)");
        expect(log).toContain("- Bob (disconnected, hand=5, bank=0, sets=0, completed=1)");
        expect(log).toContain("Visible hand: Pass Go, 3M");
        expect(log).toContain("- [9] Bob: Charged Rent ◆3 (card=Rainbow Rent; target=Alice; got=2M)");
    });

    it("reports when there are no recent actions", () => {
        const state = makeState();
        state.recentActions = [];

        const log = formatGameLog(state);

        expect(log).toContain("Recent actions:");
        expect(log).toContain("- none");
    });
});

describe("buildHangIssueUrl", () => {
    it("builds a prefilled GitHub new-issue URL with the log in the body", () => {
        const url = buildHangIssueUrl("line1\nline2", "ABCD");
        const parsed = new URL(url);

        expect(parsed.origin + parsed.pathname).toBe("https://github.com/JeffSteinbok/jeffopolydeal/issues/new");
        expect(parsed.searchParams.get("title")).toBe("Game hang report (ABCD)");
        expect(parsed.searchParams.get("labels")).toBe("bug");
        const body = parsed.searchParams.get("body") ?? "";
        expect(body).toContain("## Game log");
        expect(body).toContain("line1\nline2");
    });
});
