import { Bat, CricketBall, INK, NetBand, Racquet, Stumps, TennisBall } from "./parts";

// Retro's "kit bag" scenery. Each piece is pinned to a corner or edge of the
// screen and sized with the viewport, so the content between them stays
// clear at any width. The SVG viewBoxes are windows onto the 1280 x 800
// layouts the designs were drawn at.

const GRASS = "#7FA33A";
const PITCH = "#D9B97A";
const CREAM = "#FFFCF2";

export function RetroHome() {
  return (
    <div className="absolute inset-0" data-scene="retro-home">
      <svg viewBox="0 300 480 500" className="absolute bottom-0 left-0 w-[min(480px,36vw)]">
        <Racquet transform="translate(250 470) rotate(-28)" frame="#B5602B" />
        <TennisBall x={120} y={690} r={50} />
      </svg>
      <svg viewBox="800 200 480 600" className="absolute bottom-0 right-0 w-[min(480px,36vw)]">
        <Bat transform="translate(1050 380) rotate(24)" />
        <CricketBall x={1180} y={690} r={44} />
      </svg>
    </div>
  );
}

export function RetroTennisTour() {
  return (
    <div className="absolute inset-0" data-scene="retro-tennis-tour">
      <NetBand />
      <svg viewBox="0 280 240 280" className="absolute left-0 top-[35%] w-[min(240px,19vw)]">
        <path d="M60 300 Q130 330 170 480" fill="none" stroke={INK} strokeWidth="3" strokeDasharray="8 10" />
        <TennisBall x={180} y={520} r={30} />
      </svg>
      <svg viewBox="880 120 400 500" className="absolute right-0 top-[15%] w-[min(400px,31vw)]">
        <Racquet transform="translate(1090 270) rotate(22) scale(0.85)" frame="#3E6E9E" />
      </svg>
    </div>
  );
}

// The stumps' corner piece and the grass band share one scale: the band is
// as tall as the piece's 110 units of ground (110/400 of its width).
export function RetroCricketTour() {
  return (
    <div className="absolute inset-0" data-scene="retro-cricket-tour">
      <div className="absolute inset-x-0 bottom-0 h-[min(110px,8.525vw)] border-t-4 border-[#2B2118]" style={{ background: GRASS }} />
      <svg viewBox="0 260 340 540" className="absolute bottom-0 left-0 w-[min(340px,26.5vw)]">
        <Bat transform="translate(200 420) rotate(-14)" />
      </svg>
      <svg viewBox="880 220 400 580" className="absolute bottom-0 right-0 w-[min(400px,31vw)]">
        <polygon points="960,690 1280,690 1280,800 900,800" fill={PITCH} />
        <path d="M960 690 L900 800" stroke={INK} strokeOpacity="0.35" strokeWidth="3" />
        <path d="M880 692 H1280" stroke={INK} strokeWidth="4" />
        <path d="M1010 694 L976 800" stroke={CREAM} strokeWidth="6" />
        <path d="M1040 694 H1170" stroke={CREAM} strokeWidth="5" />
        <Stumps x={1040} y={380} />
        <CricketBall x={1104} y={270} r={44} />
      </svg>
    </div>
  );
}

// The quieter versions, for screens with more to read (a game, Past
// Challenges, How to Play) and for phones: faint, and kept to the edges.

export function RetroTennisPage() {
  return (
    <div className="absolute inset-0 opacity-40" data-scene="retro-tennis-page">
      <NetBand scale={0.6} />
      <svg viewBox="-60 -60 125 125" className="absolute right-6 top-6 w-16 md:w-20">
        <TennisBall x={0} y={0} r={40} />
      </svg>
    </div>
  );
}

export function RetroCricketPage() {
  return (
    <div className="absolute inset-0 opacity-40" data-scene="retro-cricket-page">
      <div className="absolute inset-x-0 bottom-0 h-[min(60px,7.5vw)]" style={{ background: GRASS }} />
      <svg viewBox="960 620 320 180" className="absolute bottom-0 right-0 w-[min(320px,40vw)]">
        <polygon points="1000,740 1280,740 1280,800 968,800" fill={PITCH} />
        <path d="M960 742 H1280" stroke={INK} strokeWidth="4" />
        <Stumps x={1130} y={640} scale={0.33} />
      </svg>
      <svg viewBox="-56 -56 115 115" className="absolute right-6 top-6 w-16 md:w-20">
        <CricketBall x={0} y={0} r={36} />
      </svg>
    </div>
  );
}

// Any other screen: a ball from each sport. The corner balls all sit top
// right, clear of the Back link at top left.
export function RetroPage() {
  return (
    <div className="absolute inset-0 opacity-40" data-scene="retro-page">
      <svg viewBox="-60 -60 125 125" className="absolute right-6 top-6 w-16 md:w-20">
        <TennisBall x={0} y={0} r={40} />
      </svg>
      <svg viewBox="-56 -56 115 115" className="absolute right-20 top-20 w-12 md:right-24 md:top-24 md:w-16">
        <CricketBall x={0} y={0} r={36} />
      </svg>
    </div>
  );
}
