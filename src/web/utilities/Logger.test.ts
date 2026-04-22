import { Logger } from "./Logger";
import { Debug, DebugFlags } from "./Debug";

beforeEach(() => {
    Debug.setFlags(DebugFlags.None);
});

afterEach(() => {
    vi.restoreAllMocks();
});

describe("Logger", () => {
    it("log() calls console.log", () => {
        const spy = vi.spyOn(console, "log").mockImplementation(() => {});
        Logger.log("hello");
        expect(spy).toHaveBeenCalledWith("hello");
    });

    it("error() calls console.error", () => {
        const spy = vi.spyOn(console, "error").mockImplementation(() => {});
        Logger.error("bad");
        expect(spy).toHaveBeenCalledWith("bad");
    });

    it("warn() calls console.warn", () => {
        const spy = vi.spyOn(console, "warn").mockImplementation(() => {});
        Logger.warn("careful");
        expect(spy).toHaveBeenCalledWith("careful");
    });

    it("debug() logs when VerboseLogging is set", () => {
        const spy = vi.spyOn(console, "log").mockImplementation(() => {});
        Debug.setFlags(DebugFlags.VerboseLogging);
        Logger.debug("verbose message");
        // setFlags itself logs once, then debug logs
        expect(spy).toHaveBeenCalledWith("[DEBUG]", "verbose message");
    });

    it("debug() does NOT log when VerboseLogging is not set", () => {
        const spy = vi.spyOn(console, "log").mockImplementation(() => {});
        Logger.debug("should not appear");
        expect(spy).not.toHaveBeenCalled();
    });

    it("debug() prepends [DEBUG] to output", () => {
        const spy = vi.spyOn(console, "log").mockImplementation(() => {});
        Debug.setFlags(DebugFlags.VerboseLogging);
        Logger.debug("test", 123);
        expect(spy).toHaveBeenCalledWith("[DEBUG]", "test", 123);
    });
});
