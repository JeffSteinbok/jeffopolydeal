import React from "react";

interface RentIconProps {
    count: number;       // 1–4
    color?: string;      // fill for the colored banner, defaults to red
    className?: string;
    style?: React.CSSProperties;
}

const stroke = "#231f20";
const sw = 0.75;
const sm = 10;  // strokeMiterlimit

export function RentIcon({ count, color = "#ed2024", className, style }: RentIconProps) {
    const c = Math.max(1, Math.min(4, count));
    return (
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 28.65 24.55" className={className} style={style}>
            {c >= 4 && <House4 color={color} />}
            {c >= 3 && <House3 color={color} />}
            {c >= 2 && <House2 color={color} />}
            <House1 color={color} />
            <text fill="#231f20" fontFamily="ArialMT, Arial" fontSize="12"
                  transform={textTransform[c - 1]}>
                {c}
            </text>
        </svg>
    );
}

const textTransform = [
    "translate(17.57 18.68)",
    "translate(17.63 18.42)",
    "translate(17.47 18.77)",
    "translate(17.48 18.11)",
];

/* House 1 — upright rectangle (always the front) */
function House1({ color }: { color: string }) {
    return (
        <g>
            <path fill={color} stroke={stroke} strokeWidth={sw} strokeMiterlimit={sm}
                  d="M28.27,5.08v2h-14.8v-2c0-1.51,1.22-2.73,2.72-2.73h9.36c1.5,0,2.72,1.22,2.72,2.73Z"/>
            <path fill="#fff" stroke={stroke} strokeWidth={sw} strokeMiterlimit={sm}
                  d="M28.27,7.08v13.55c0,1.5-1.22,2.72-2.72,2.72h-9.36c-1.5,0-2.72-1.22-2.72-2.72V7.08h14.8Z"/>
        </g>
    );
}

/* House 2 — tilted left */
function House2({ color }: { color: string }) {
    return (
        <g>
            <path fill={color} stroke={stroke} strokeWidth={sw} strokeMiterlimit={sm}
                  d="M20.87,2.37l.83,1.82-13.47,6.14-.83-1.82c-.63-1.37-.02-2.99,1.34-3.61l8.52-3.88c1.36-.62,2.98-.02,3.61,1.36Z"/>
            <path fill="#fff" stroke={stroke} strokeWidth={sw} strokeMiterlimit={sm}
                  d="M21.7,4.19l5.62,12.33c.62,1.36.02,2.98-1.35,3.6l-8.52,3.88c-1.36.62-2.98.02-3.6-1.35l-5.62-12.33,13.47-6.14Z"/>
        </g>
    );
}

/* House 3 — tilted more */
function House3({ color }: { color: string }) {
    return (
        <g>
            <path fill={color} stroke={stroke} strokeWidth={sw} strokeMiterlimit={sm}
                  d="M14.12,2.2l1.37,1.46L4.7,13.78l-1.37-1.46c-1.03-1.1-.98-2.83.12-3.85l6.83-6.4c1.09-1.03,2.82-.97,3.85.13Z"/>
            <path fill="#fff" stroke={stroke} strokeWidth={sw} strokeMiterlimit={sm}
                  d="M15.49,3.65l9.27,9.88c1.03,1.09.97,2.82-.12,3.84l-6.83,6.4c-1.09,1.03-2.82.97-3.84-.12L4.7,13.78,15.49,3.65Z"/>
        </g>
    );
}

/* House 4 — tilted furthest */
function House4({ color }: { color: string }) {
    return (
        <g>
            <path fill={color} stroke={stroke} strokeWidth={sw} strokeMiterlimit={sm}
                  d="M8.08,3.44l1.83.8-5.94,13.56-1.83-.8c-1.38-.61-2.01-2.21-1.41-3.59l3.75-8.57c.6-1.37,2.21-2,3.59-1.4Z"/>
            <path fill="#fff" stroke={stroke} strokeWidth={sw} strokeMiterlimit={sm}
                  d="M9.91,4.25l12.41,5.44c1.37.6,2,2.21,1.4,3.58l-3.75,8.57c-.6,1.37-2.21,2-3.58,1.4l-12.41-5.44,5.94-13.56Z"/>
        </g>
    );
}
