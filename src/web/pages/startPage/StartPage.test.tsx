import React from "react";
import { render, screen } from "@testing-library/react";
import { StartPage } from "./StartPage";
import { BRIDGE_HANDLER_NAME } from "../../utilities/NativeBridge";

/** Stands in for the handler the iOS shell registers on its web view. */
function installShell() {
    (window as any).webkit = {
        messageHandlers: { [BRIDGE_HANDLER_NAME]: { postMessage: () => { } } },
    };
}

beforeEach(() => {
    delete (window as any).webkit;
});

describe("the iOS download link", () => {
    it("points at the TestFlight beta in a browser", () => {
        render(<StartPage onJoinGame={() => { }} />);

        const link = screen.getByRole("link", { name: /get the ios app/i });
        expect(link).toHaveAttribute("href", "https://testflight.apple.com/join/JGt389zR");
        expect(link).toHaveTextContent(/testflight beta/i);
    });

    it("is hidden inside the iOS app, which is where it would lead", () => {
        installShell();
        render(<StartPage onJoinGame={() => { }} />);

        expect(screen.queryByRole("link", { name: /get the ios app/i })).toBeNull();
    });
});
