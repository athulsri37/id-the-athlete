import { ReactNode, useId } from "react";

// Stadium's "under the lights" scenery: a night venue seen from the stands.
// The full scenes are one 1280 x 800 picture that covers the screen (cropping
// the sky or the sides as the window's shape needs); the quieter versions
// keep to the top corners and a band along the bottom.

const LIGHT = "#f4fff8";
const NEON = "#39FF88";
const STAND = "#10161d";
const POLE = "#1f2833";
const WOOD = "#f0d9a8";
const PITCH = "#3d3524";

// SVG ids are global to the page, and React's ids contain colons, which
// url(#...) references can't use.
function useSvgIds() {
  const base = useId().replace(/:/g, "");
  return (name: string) => `${base}-${name}`;
}

function Glow({ id, opacity = 0.3 }: { id: string; opacity?: number }) {
  return (
    <radialGradient id={id}>
      <stop offset="0" stopColor={LIGHT} stopOpacity={opacity} />
      <stop offset="1" stopColor={LIGHT} stopOpacity="0" />
    </radialGradient>
  );
}

function Crowd({ id }: { id: string }) {
  return (
    <pattern id={id} width="12" height="9" patternUnits="userSpaceOnUse">
      <circle cx="3" cy="3" r="1.2" fill="#e2e8f0" fillOpacity="0.2" />
      <circle cx="9" cy="7" r="1" fill={NEON} fillOpacity="0.16" />
    </pattern>
  );
}

function NeonBlur({ id, blur }: { id: string; blur: number }) {
  return (
    <filter id={id} x="-10%" y="-10%" width="120%" height="120%">
      <feGaussianBlur stdDeviation={blur} result="b" />
      <feMerge>
        <feMergeNode in="b" />
        <feMergeNode in="SourceGraphic" />
      </feMerge>
    </filter>
  );
}

// A floodlight: a pole down to the stand and a lamp with its glow.
interface FloodlightProps {
  x: number;
  y: number; // top of the lamp
  big?: boolean;
  poleTo: number;
  glow: string;
}

function Floodlight({ x, y, big = false, poleTo, glow }: FloodlightProps) {
  const w = big ? 60 : 48;
  const h = big ? 24 : 18;
  return (
    <g>
      <circle cx={x} cy={y + h / 2} r={big ? 150 : 110} fill={`url(#${glow})`} />
      <path d={`M${x} ${y + h} V${poleTo}`} stroke={POLE} strokeWidth="5" />
      <rect x={x - w / 2} y={y} width={w} height={h} rx="3" fill="#e9fff2" />
      <path
        d={`M${x - w / 4} ${y} v${h} M${x} ${y} v${h} M${x + w / 4} ${y} v${h} M${x - w / 2} ${y + h / 2} h${w}`}
        stroke="#0a0e14"
        strokeWidth="2"
      />
    </g>
  );
}

interface StripesProps {
  vx: number;
  vy: number;
  fill: string;
  from: number; // where the stripes cross y=800, left to right
  to: number;
  step?: number;
  bottom?: number; // how far down to draw them
}

// Mown stripes running towards a vanishing point, as alternating wedges.
function Stripes({ vx, vy, fill, from, to, step = 200, bottom = 800 }: StripesProps) {
  const at = (x: number) => (vx + ((x - vx) * (bottom - vy)) / (800 - vy)).toFixed(1);
  const wedges: string[] = [];
  for (let x = from; x < to; x += step * 2) wedges.push(`${vx},${vy} ${at(x)},${bottom} ${at(x + step)},${bottom}`);
  return (
    <g fill={fill}>
      {wedges.map((points) => (
        <polygon key={points} points={points} />
      ))}
    </g>
  );
}

// Two layers, so both survive any window shape: the floodlights pinned to
// the top (scaled to fit, never cropped), and the stand and field filling
// the rest below them (cropped at the bottom or sides; the field runs on
// below the design's 800 so a tall window shows more of it rather than
// magnifying it). The split sits where
// the 1280 x 800 design has it (380/1280 of the width) unless the window is
// short and wide, when it's capped at just under half the height.
export function StadiumHome() {
  const id = useSvgIds();
  return (
    <div className="absolute inset-0 [--sky:min(29.6875vw,48vh)]" data-scene="stadium-home">
      <svg
        viewBox="0 0 1280 380"
        preserveAspectRatio="xMidYMax meet"
        className="absolute inset-x-0 top-0 h-[var(--sky)] w-full"
      >
        <defs>
          <Glow id={id("glow")} opacity={0.28} />
        </defs>
        <Floodlight x={190} y={116} big poleTo={380} glow={id("glow")} />
        <Floodlight x={1090} y={116} big poleTo={380} glow={id("glow")} />
        <Floodlight x={420} y={182} poleTo={380} glow={id("glow")} />
        <Floodlight x={860} y={182} poleTo={380} glow={id("glow")} />
      </svg>
      <svg
        viewBox="0 380 1280 1100"
        preserveAspectRatio="xMidYMin slice"
        className="absolute inset-x-0 top-[var(--sky)] h-[calc(100%-var(--sky))] w-full"
      >
        <defs>
          <Crowd id={id("crowd")} />
          <clipPath id={id("field")}>
            <rect x="0" y="470" width="1280" height="1010" />
          </clipPath>
        </defs>
        <rect x="0" y="380" width="1280" height="90" fill={STAND} />
        <rect x="0" y="380" width="1280" height="90" fill={`url(#${id("crowd")})`} />
        <path d="M0 380 H1280" stroke={POLE} strokeWidth="3" />
        <g clipPath={`url(#${id("field")})`}>
          <rect x="0" y="470" width="1280" height="1010" fill="#0d1d15" />
          <Stripes vx={640} vy={300} fill="#112a1d" from={-960} to={2240} bottom={1480} />
          <path d="M-40 520 Q640 440 1320 520" fill="none" stroke={NEON} strokeOpacity="0.55" strokeWidth="3" />
        </g>
      </svg>
    </div>
  );
}

// The tour scenes put the court or pitch on the right; the tour screens move
// their menu to the left to match (.tour-layout in styles/index.css).
function TourSky({ id }: { id: (name: string) => string }) {
  return (
    <>
      <rect x="460" y="220" width="820" height="70" fill={STAND} />
      <rect x="460" y="220" width="820" height="70" fill={`url(#${id("crowd")})`} />
      <Floodlight x={1130} y={98} big poleTo={220} glow={id("glow")} />
      <Floodlight x={680} y={110} poleTo={220} glow={id("glow")} />
    </>
  );
}

// A court seen from behind one baseline, in perspective towards (820, 150).
export function StadiumTennisTour() {
  const id = useSvgIds();
  return (
    <div className="absolute inset-0" data-scene="stadium-tennis-tour">
      <svg viewBox="0 0 1280 800" preserveAspectRatio="xMaxYMax slice" className="absolute inset-0 h-full w-full">
        <defs>
          <Glow id={id("glow")} />
          <Crowd id={id("crowd")} />
          <NeonBlur id={id("neon")} blur={3} />
          <pattern id={id("mesh")} width="7" height="7" patternUnits="userSpaceOnUse">
            <path d="M0 0 L7 7 M7 0 L0 7" stroke="#e2e8f0" strokeOpacity="0.35" strokeWidth="0.8" />
          </pattern>
        </defs>
        <rect x="0" y="290" width="1280" height="510" fill="#0b1724" />
        <polygon points="690,316 950,316 1300,800 340,800" fill="#0e2134" />
        <polygon points="710.3,330 929.7,330 1210,790 430,790" fill="#14406a" />
        <g fill="none" stroke={LIGHT} strokeWidth="3" filter={`url(#${id("neon")})`} strokeLinejoin="round">
          <polygon points="710.3,330 929.7,330 1210,790 430,790" />
          <path d="M737.7 330 L527.4 790 M902.3 330 L1112.6 790" />
          <path d="M721.3 365.8 H918.7 M635.9 552.6 H1004.1 M820 365.8 V552.6" />
          <path d="M820 330 v6 M820 790 v-12" />
        </g>
        <path d="M620.2 431 L620.2 398 Q820 408 1039.8 398 L1039.8 431 Z" fill={`url(#${id("mesh")})`} />
        <path d="M620.2 398 Q820 408 1039.8 398" fill="none" stroke={LIGHT} strokeWidth="4" />
        <path d="M620.2 396 V434 M1039.8 396 V434" stroke="#9aa7b4" strokeWidth="5" />
        <TourSky id={id} />
      </svg>
    </div>
  );
}

// A pitch seen from behind the near stumps, towards the far ones, on a
// mown outfield; the same vanishing point as the tennis court.
export function StadiumCricketTour() {
  const id = useSvgIds();
  return (
    <div className="absolute inset-0" data-scene="stadium-cricket-tour">
      <svg viewBox="0 0 1280 800" preserveAspectRatio="xMaxYMax slice" className="absolute inset-0 h-full w-full">
        <defs>
          <Glow id={id("glow")} />
          <Crowd id={id("crowd")} />
          <NeonBlur id={id("neon")} blur={2.5} />
          <clipPath id={id("field")}>
            <rect x="0" y="290" width="1280" height="510" />
          </clipPath>
        </defs>
        <g clipPath={`url(#${id("field")})`}>
          <rect x="0" y="290" width="1280" height="510" fill="#0d1d15" />
          <Stripes vx={820} vy={150} fill="#12291c" from={-380} to={2220} />
          <path d="M380 330 Q820 296 1300 330" fill="none" stroke={NEON} strokeOpacity="0.6" strokeWidth="3" />
        </g>
        <polygon points="787.5,341.7 852.5,341.7 930.1,800 709.9,800" fill={PITCH} />
        <g fill="none" stroke={LIGHT} strokeWidth="2.5" filter={`url(#${id("neon")})`}>
          <path d="M790.7 350 H849.3 M777.5 358.6 H862.5 M789.4 358.6 L791.8 342 M850.6 358.6 L848.2 342" />
          <path d="M706.7 707.3 H933.3 M738.2 707.3 L724.6 800 M901.8 707.3 L915.4 800 M727.6 780 H912.4" />
        </g>
        <circle cx="820" cy="320" r="90" fill={`url(#${id("glow")})`} />
        <g stroke={WOOD} strokeWidth="3.5" strokeLinecap="round">
          <path d="M812 350 V323 M820 350 V323 M828 350 V323" />
          <path d="M811 320 H819 M821 320 H829" strokeWidth="2.5" />
        </g>
        <circle cx="820" cy="740" r="120" fill={`url(#${id("glow")})`} />
        <path d="M806 781 L826 790 M826 781 L846 790 M846 781 L866 790" stroke="#2B2118" strokeOpacity="0.6" strokeWidth="8" strokeLinecap="round" />
        <g stroke={WOOD} strokeWidth="7" strokeLinecap="round" filter={`url(#${id("neon")})`}>
          <path d="M800 780 V702 M820 780 V702 M840 780 V702" />
          <path d="M798 696 H818 M822 696 H842" strokeWidth="5" />
        </g>
        <TourSky id={id} />
      </svg>
    </div>
  );
}

// The quieter versions: floodlight glow in the top corners, and the sport's
// near end along the bottom (cropped to the middle on narrow screens).

function Floodlights() {
  return <div className="scenery-floodlights absolute inset-0" />;
}

function BottomBand({ children }: { children: ReactNode }) {
  return (
    <svg
      viewBox="0 680 1280 120"
      preserveAspectRatio="xMidYMax slice"
      className="absolute inset-x-0 bottom-0 h-[120px] w-full"
    >
      {children}
    </svg>
  );
}

export function StadiumTennisPage() {
  const id = useSvgIds();
  return (
    <div className="absolute inset-0" data-scene="stadium-tennis-page">
      <Floodlights />
      <BottomBand>
        <defs>
          <pattern id={id("mesh")} width="7" height="7" patternUnits="userSpaceOnUse">
            <path d="M0 0 L7 7 M7 0 L0 7" stroke="#e2e8f0" strokeOpacity="0.4" strokeWidth="0.8" />
          </pattern>
        </defs>
        <g opacity="0.5">
          <polygon points="200,700 1080,700 1280,800 0,800" fill="#14406a" />
          <path
            d="M200 700 H1080 M200 700 L0 800 M1080 700 L1280 800 M300 700 L130 800 M980 700 L1150 800 M640 700 V800"
            fill="none"
            stroke={LIGHT}
            strokeWidth="2.5"
          />
        </g>
        <g opacity="0.75">
          <rect x="96" y="722" width="1088" height="26" fill={`url(#${id("mesh")})`} />
          <path d="M96 721 H1184" stroke={LIGHT} strokeWidth="4" />
          <path d="M96 714 V750 M1184 714 V750" stroke="#9aa7b4" strokeWidth="5" />
        </g>
      </BottomBand>
    </div>
  );
}

export function StadiumCricketPage() {
  const id = useSvgIds();
  return (
    <div className="absolute inset-0" data-scene="stadium-cricket-page">
      <Floodlights />
      <BottomBand>
        <defs>
          <Glow id={id("glow")} opacity={0.22} />
        </defs>
        <g opacity="0.55">
          <rect x="0" y="700" width="1280" height="100" fill="#0d1d15" />
          <g fill="#12291c">
            <polygon points="0,700 160,700 120,800 -80,800" />
            <polygon points="320,700 480,700 460,800 260,800" />
            <polygon points="640,700 800,700 800,800 600,800" />
            <polygon points="960,700 1120,700 1140,800 940,800" />
          </g>
          <polygon points="610,700 670,700 700,800 580,800" fill={PITCH} />
          <path d="M600 735 H680 M590 770 H690" stroke={LIGHT} strokeWidth="2.5" />
        </g>
        <circle cx="640" cy="745" r="70" fill={`url(#${id("glow")})`} />
        <g opacity="0.85" stroke={WOOD} strokeLinecap="round">
          <path d="M626 770 V716 M640 770 V716 M654 770 V716" strokeWidth="5" />
          <path d="M624 711 H638 M642 711 H656" strokeWidth="3.5" />
        </g>
      </BottomBand>
    </div>
  );
}

// Any other screen: just the floodlights.
export function StadiumPage() {
  return (
    <div className="absolute inset-0" data-scene="stadium-page">
      <Floodlights />
    </div>
  );
}
