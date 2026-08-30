import {
    installNativeInboundAPI,
    getNearbyGames,
    resetNearbyGames,
    NativeInboundAPI,
} from "./NativeNearby";

function shell(): NativeInboundAPI {
    return (window as unknown as { jeffopolyNative: NativeInboundAPI }).jeffopolyNative;
}

beforeEach(() => {
    resetNearbyGames();
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
