import { Debug, DebugFlags } from "./Debug";

export class Logger {
    static log(...args: any[]): void {
        console.log(...args);
    }

    static debug(...args: any[]): void {
        if (Debug.isFlagSet(DebugFlags.VerboseLogging)) {
            console.log("[DEBUG]", ...args);
        }
    }

    static error(...args: any[]): void {
        console.error(...args);
    }

    static warn(...args: any[]): void {
        console.warn(...args);
    }
}
