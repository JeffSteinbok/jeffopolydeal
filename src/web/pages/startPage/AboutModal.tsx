import React from "react";
import titleImage from "../../assets/JeffopolyDeal.png";
import "./AboutModal.css";

interface AboutModalProps {
    onClose: () => void;
}

const changelog = [
    { date: "05/02", notes: "Play against bot opponents from the lobby." },
    { date: "04/28", notes: "Initial release — multiplayer card game with real-time gameplay." },
];

export function AboutModal({ onClose }: AboutModalProps) {
    return (
        <div className="aboutOverlay" onClick={onClose}>
            <div className="aboutModal" onClick={(e) => e.stopPropagation()}>
                <button className="aboutCloseButton" onClick={onClose}>✕</button>
                <div className="aboutHeader">
                    <img src={titleImage} alt="Jeffopoly Deal" className="aboutLogo" />
                    <h2 className="aboutTitle">About Jeffopoly Deal</h2>
                </div>
                <p className="aboutDescription">
                    A real-time multiplayer card game inspired by Monopoly Deal, built with React, SignalR, and .NET.
                    Play with friends or challenge bot opponents!
                </p>
                <a
                    className="aboutRepoLink"
                    href="https://github.com/JeffSteinbok/jeffopolydeal"
                    target="_blank"
                    rel="noopener noreferrer"
                >
                    ⭐ View on GitHub
                </a>
                <div className="aboutCopyright">
                    <p>© {new Date().getFullYear()} Jeff Steinbok. All rights reserved.</p>
                    <p>Monopoly Deal is a trademark of Hasbro, Inc.</p>
                </div>
                <h3 className="aboutChangelogTitle">Changelog</h3>
                <div className="aboutChangelog">
                    {changelog.map((entry) => (
                        <div key={entry.date} className="aboutChangelogEntry">
                            <span className="aboutDate">{entry.date}</span>
                            <p className="aboutChangelogNotes">{entry.notes}</p>
                        </div>
                    ))}
                </div>
            </div>
        </div>
    );
}
