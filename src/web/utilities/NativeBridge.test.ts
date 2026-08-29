import {
    postToNativeHost,
    isNativeBridgeAvailable,
    resetBridgeDeduplication,
    BRIDGE_HANDLER_NAME,
    BRIDGE_VERSION,
} from "./NativeBridge";

type Posted = { v: number; type: string; id?: string; payload?: Record<string, unknown> };

function installShell(impl?: (m: unknown) => void): Posted[] {
    const received: Posted[] = [];
    (window as any).webkit = {
        messageHandlers: {
            [BRIDGE_HANDLER_NAME]: {
                postMessage: (m: unknown) => {
                    if (impl) impl(m);
                    received.push(m as Posted);
                },
            },
        },
    };
    return received;
}

beforeEach(() => {
    delete (window as any).webkit;
    resetBridgeDeduplication();
});

describe("without a native shell", () => {
    it("reports the bridge as unavailable", () => {
        expect(isNativeBridgeAvailable()).toBe(false);
    });

    it("is a no-op rather than throwing, so browser and PWA play normally", () => {
        expect(() => postToNativeHost("haptic", { event: "cardPlayed" })).not.toThrow();
        expect(postToNativeHost("haptic", { event: "cardPlayed" })).toBe(false);
    });

    it("ignores a handler object that is not callable", () => {
        (window as any).webkit = { messageHandlers: { [BRIDGE_HANDLER_NAME]: {} } };
        expect(isNativeBridgeAvailable()).toBe(false);
        expect(postToNativeHost("haptic")).toBe(false);
    });
});

describe("with a native shell", () => {
    it("posts a versioned envelope", () => {
        const received = installShell();
        expect(postToNativeHost("haptic", { event: "turnStarted" }, "turn:1")).toBe(true);
        expect(received).toEqual([
            { v: BRIDGE_VERSION, type: "haptic", id: "turn:1", payload: { event: "turnStarted" } },
        ]);
    });

    it("omits id and payload when not supplied", () => {
        const received = installShell();
        postToNativeHost("lifecycle");
        expect(received[0]).toEqual({ v: BRIDGE_VERSION, type: "lifecycle" });
    });

    it("delivers a given id only once", () => {
        const received = installShell();
        expect(postToNativeHost("haptic", { event: "cardPlayed" }, "action:7")).toBe(true);
        expect(postToNativeHost("haptic", { event: "cardPlayed" }, "action:7")).toBe(false);
        expect(received).toHaveLength(1);
    });

    it("does not deduplicate messages sent without an id", () => {
        const received = installShell();
        postToNativeHost("haptic", { event: "selection" });
        postToNativeHost("haptic", { event: "selection" });
        expect(received).toHaveLength(2);
    });

    it("survives a shell that throws", () => {
        installShell(() => { throw new Error("shell exploded"); });
        expect(postToNativeHost("haptic", { event: "gameWon" })).toBe(false);
    });
});
