import {
    installNativeInboundAPI,
    getNearbyGames,
    getPushToken,
    onPushToken,
    onReturnToForeground,
    onOpenGame,
    resetNativeInbound,
    NativeInboundAPI,
} from "./NativeInbound";

function shell(): NativeInboundAPI {
    return (window as unknown as { jeffopolyNative: NativeInboundAPI }).jeffopolyNative;
}

beforeEach(() => {
    resetNativeInbound();
    installNativeInboundAPI();
});

describe("setNearbyGames", () => {
    it("accepts well-formed games", () => {
        shell().setNearbyGames([{ gameCode: "ABCD", hostName: "Jeff" }]);
        expect(getNearbyGames()).toEqual([{ gameCode: "ABCD", hostName: "Jeff" }]);
    });

    it("uppercases codes and trims host names", () => {
        shell().setNearbyGames([{ gameCode: " abcd ", hostName: "  Jeff  " }]);
        expect(getNearbyGames()).toEqual([{ gameCode: "ABCD", hostName: "Jeff" }]);
    });

    it("collapses duplicate codes, since every device in a lobby advertises", () => {
        shell().setNearbyGames([
            { gameCode: "ABCD", hostName: "Jeff" },
            { gameCode: "ABCD", hostName: "Jeff's iPad" },
        ]);
        expect(getNearbyGames()).toHaveLength(1);
    });

    it("falls back to a placeholder rather than a blank host name", () => {
        shell().setNearbyGames([{ gameCode: "ABCD", hostName: "   " }]);
        expect(getNearbyGames()[0].hostName).toBe("Nearby Host");
    });

    it("drops malformed entries without discarding the good ones", () => {
        shell().setNearbyGames([
            { gameCode: "AB", hostName: "too short" },
            { gameCode: "TOOLONG", hostName: "too long" },
            { gameCode: "AB!D", hostName: "bad chars" },
            { gameCode: 1234, hostName: "not a string" },
            { hostName: "no code" },
            null,
            "nope",
            { gameCode: "GOOD", hostName: "Jeff" },
        ]);
        expect(getNearbyGames()).toEqual([{ gameCode: "GOOD", hostName: "Jeff" }]);
    });

    it("treats anything that is not an array as no games", () => {
        shell().setNearbyGames([{ gameCode: "ABCD", hostName: "Jeff" }]);
        shell().setNearbyGames("not an array");
        expect(getNearbyGames()).toEqual([]);
    });

    it("replaces rather than accumulates, so lost peers disappear", () => {
        shell().setNearbyGames([{ gameCode: "ABCD", hostName: "Jeff" }]);
        shell().setNearbyGames([{ gameCode: "WXYZ", hostName: "Sam" }]);
        expect(getNearbyGames()).toEqual([{ gameCode: "WXYZ", hostName: "Sam" }]);
    });
});

describe("setPushToken", () => {
    it("accepts an APNs token and notifies listeners", () => {
        const seen: string[] = [];
        onPushToken((t) => seen.push(t));
        shell().setPushToken("a1b2c3d4e5f6a7b8");
        expect(getPushToken()).toBe("a1b2c3d4e5f6a7b8");
        expect(seen).toEqual(["a1b2c3d4e5f6a7b8"]);
    });

    it("replays the current token to a late subscriber", () => {
        shell().setPushToken("a1b2c3d4e5f6a7b8");
        const seen: string[] = [];
        onPushToken((t) => seen.push(t));
        expect(seen).toEqual(["a1b2c3d4e5f6a7b8"]);
    });

    it("ignores a repeat of the same token", () => {
        const seen: string[] = [];
        onPushToken((t) => seen.push(t));
        shell().setPushToken("a1b2c3d4e5f6a7b8");
        shell().setPushToken("a1b2c3d4e5f6a7b8");
        expect(seen).toHaveLength(1);
    });

    it.each([["not hex", "zzzz"], ["too short", "a1b2"], ["not a string", 12345], ["empty", "   "]])(
        "rejects %s",
        (_label, value) => {
            shell().setPushToken(value);
            expect(getPushToken()).toBeNull();
        }
    );
});

describe("setLifecycle", () => {
    it("notifies only on returning to the foreground", () => {
        let count = 0;
        onReturnToForeground(() => { count += 1; });
        shell().setLifecycle("background");
        expect(count).toBe(0);
        shell().setLifecycle("active");
        expect(count).toBe(1);
    });

    it("ignores anything that is not a known phase", () => {
        let count = 0;
        onReturnToForeground(() => { count += 1; });
        shell().setLifecycle(42);
        shell().setLifecycle("resumed");
        expect(count).toBe(0);
    });
});

describe("openGame", () => {
    it("passes a valid code through, uppercased", () => {
        const seen: string[] = [];
        onOpenGame((c) => seen.push(c));
        shell().openGame(" wxyz ");
        expect(seen).toEqual(["WXYZ"]);
    });

    it.each([["too short", "AB"], ["not a string", 1234], ["bad characters", "AB!D"]])(
        "ignores %s",
        (_label, value) => {
            const seen: string[] = [];
            onOpenGame((c) => seen.push(c));
            shell().openGame(value);
            expect(seen).toEqual([]);
        }
    );
});
