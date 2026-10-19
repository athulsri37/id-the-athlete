// The Retro theme's equipment, drawn like its buttons: thick ink outlines and
// a solid offset shadow. Each part is an SVG group placed in its scene's
// coordinates; the scenes in this folder arrange them.

export const INK = "#2B2118";
const CREAM = "#FFFCF2";
const LIME = "#C9E265";
const WOOD = "#E9C98B";
const LEATHER = "#A6322A";

interface BallProps {
  x: number;
  y: number;
  r: number;
}

export function TennisBall({ x, y, r }: BallProps) {
  return (
    <g transform={`translate(${x} ${y}) scale(${r / 50})`}>
      <circle cx="7" cy="7" r="50" fill={INK} />
      <circle r="50" fill={LIME} stroke={INK} strokeWidth="4" />
      <path d="M-34 -36 Q2 0 -34 36 M34 -36 Q-2 0 34 36" fill="none" stroke={CREAM} strokeWidth="6" strokeLinecap="round" />
    </g>
  );
}

export function CricketBall({ x, y, r }: BallProps) {
  return (
    <g transform={`translate(${x} ${y}) scale(${r / 44})`}>
      <circle cx="6" cy="6" r="44" fill={INK} />
      <circle r="44" fill={LEATHER} stroke={INK} strokeWidth="4" />
      <path d="M-14 -41 Q12 0 -14 41" fill="none" stroke={INK} strokeOpacity="0.35" strokeWidth="6" />
      <path d="M-22 -38 Q4 0 -22 38 M-6 -43 Q20 0 -6 43" fill="none" stroke={CREAM} strokeWidth="3" strokeDasharray="5 4" />
    </g>
  );
}

// The strings stop at the inside of the frame (an ellipse 82 x 112).
const STRING_RX = 82;
const STRING_RY = 112;
const STRINGS = [
  ...[-60, -40, -20, 0, 20, 40, 60].map((x) => {
    const h = STRING_RY * Math.sqrt(1 - (x / STRING_RX) ** 2);
    return `M${x} ${-h.toFixed(1)} V${h.toFixed(1)}`;
  }),
  ...[-80, -56, -32, -8, 16, 40, 64, 88].map((y) => {
    const w = STRING_RX * Math.sqrt(1 - (y / STRING_RY) ** 2);
    return `M${-w.toFixed(1)} ${y} H${w.toFixed(1)}`;
  }),
].join(" ");

function RacquetShape() {
  return (
    <>
      <ellipse rx="98" ry="128" />
      <path d="M-40 118 L-14 200 H14 L40 118 Z" />
      <rect x="-16" y="196" width="32" height="170" rx="8" />
    </>
  );
}

// Head centred on the origin, handle pointing down; place it with transform.
export function Racquet({ transform, frame }: { transform: string; frame: string }) {
  return (
    <g transform={transform}>
      <g transform="translate(7 7)" fill={INK}>
        <RacquetShape />
      </g>
      <g fill={frame} stroke={INK} strokeWidth="4">
        <RacquetShape />
      </g>
      <ellipse rx={STRING_RX} ry={STRING_RY} fill={CREAM} stroke={INK} strokeWidth="4" />
      <path d={STRINGS} stroke={INK} strokeOpacity="0.45" strokeWidth="2.5" />
      <rect x="-16" y="250" width="32" height="116" rx="8" fill={INK} />
      <path d="M-16 270 L16 260 M-16 296 L16 286 M-16 322 L16 312 M-16 348 L16 338" stroke="#E8DFC8" strokeWidth="3" />
    </g>
  );
}

function BatShape() {
  return (
    <>
      <rect x="-40" y="0" width="80" height="330" rx="16" />
      <path d="M-40 18 Q0 -26 40 18 Z" />
      <rect x="-12" y="-150" width="24" height="160" rx="8" />
    </>
  );
}

// Top of the blade at the origin, handle pointing up; place it with transform.
export function Bat({ transform }: { transform: string }) {
  return (
    <g transform={transform}>
      <g transform="translate(7 7)" fill={INK}>
        <BatShape />
      </g>
      <g fill={WOOD} stroke={INK} strokeWidth="4">
        <BatShape />
      </g>
      <rect x="-12" y="-150" width="24" height="120" rx="8" fill={LIME} stroke={INK} strokeWidth="4" />
      <path d="M-12 -130 L12 -138 M-12 -105 L12 -113 M-12 -80 L12 -88 M-12 -55 L12 -63" stroke={INK} strokeWidth="3" />
      <path d="M-22 60 V300" stroke={INK} strokeOpacity="0.25" strokeWidth="3" />
    </g>
  );
}

// Three stumps and two bails; (x, y) is the top-left of the left stump.
export function Stumps({ x, y, scale = 1 }: { x: number; y: number; scale?: number }) {
  const stumps = [0, 50, 100];
  return (
    <g transform={`translate(${x} ${y}) scale(${scale})`}>
      {stumps.map((dx) => (
        <rect key={`s${dx}`} x={dx + 9} y="9" width="24" height="306" rx="6" fill={INK} />
      ))}
      {stumps.map((dx) => (
        <rect key={dx} x={dx} y="0" width="24" height="312" rx="6" fill={WOOD} stroke={INK} strokeWidth="4" />
      ))}
      <rect x="6" y="-16" width="60" height="14" rx="7" fill={LIME} stroke={INK} strokeWidth="4" />
      <rect x="58" y="-16" width="60" height="14" rx="7" fill={LIME} stroke={INK} strokeWidth="4" />
    </g>
  );
}

// A tennis net across the bottom of the screen, standing on a clay court.
// HTML rather than SVG so it stretches to any width without distorting;
// `scale` shrinks the whole band (1 = 200px tall).
export function NetBand({ scale = 1 }: { scale?: number }) {
  const px = (n: number) => `${n * scale}px`;
  return (
    <div className="absolute inset-x-0 bottom-0" style={{ height: px(200) }}>
      <div
        className="absolute inset-x-0 bottom-0 bg-[#B5602B] border-t-4 border-[#2B2118]"
        style={{ height: px(110) }}
      >
        <div className="absolute inset-x-0 bg-[#FFFCF2]" style={{ bottom: px(34), height: px(6) }} />
      </div>
      <div className="scenery-net absolute" style={{ left: px(48), right: px(48), bottom: px(106), height: px(80) }} />
      <div
        className="absolute bg-[#FFFCF2] border-4 border-[#2B2118]"
        style={{ left: px(44), right: px(44), bottom: px(184), height: px(16) }}
      />
      <div className="absolute bg-[#2B2118]" style={{ left: px(30), bottom: px(106), width: px(20), height: px(106) }} />
      <div className="absolute bg-[#2B2118]" style={{ right: px(30), bottom: px(106), width: px(20), height: px(106) }} />
    </div>
  );
}
