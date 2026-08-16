interface ClueExample {
  pill: string;
  icon: string;
  state: "match" | "close" | "miss";
  description: string;
}

const STATE_CLASSES: Record<ClueExample["state"], string> = {
  match: "bg-[var(--accent)] text-[var(--on-accent)]",
  close: "bg-[var(--close-bg)] text-[var(--text-primary)]",
  miss: "bg-[var(--miss-bg)] text-[var(--text-primary)]",
};

// Icons that stand alone as a prefix (✓/✕/≈); direction arrows (▲/▼) are
// suffixed onto the numeric value instead.
const PREFIX_ICONS = new Set(["✓", "✕", "≈"]);

const TENNIS_EXAMPLES: ClueExample[] = [
  { pill: "Right", icon: "✓", state: "match", description: "Match" },
  { pill: "18", icon: "▲", state: "close", description: "Close: near the actual number (e.g. 18 when the answer is 20)" },
  { pill: "France", icon: "≈", state: "close", description: "Close: a nearby country, not exact" },
  { pill: "8", icon: "▲", state: "miss", description: "No match: actual value is higher" },
  { pill: "20", icon: "▼", state: "miss", description: "No match: actual value is lower" },
  { pill: "USA", icon: "✕", state: "miss", description: "No match" },
];

// Cricket has its own closeness mechanics (percent+floor numeric
// thresholds, Role/Bowling-Style tag matching, regional-bloc country
// grouping) that don't map onto Tennis's examples above -- e.g. Grand
// Slams and land-border adjacency mean nothing in a Cricket game, so
// showing the Tennis set there would just be confusing.
const CRICKET_EXAMPLES: ClueExample[] = [
  { pill: "12", icon: "▲", state: "close", description: "Close: near the actual wickets count (e.g. 12 when the answer is 14)" },
  { pill: "Batter", icon: "≈", state: "close", description: "Close: shares the Batting tag with the actual role (e.g. Wicketkeeper-Batter)" },
  { pill: "Right-arm Pace", icon: "≈", state: "close", description: "Close: same pace/spin type as the actual bowling style (e.g. Left-arm Pace)" },
  { pill: "Australia", icon: "≈", state: "close", description: "Close: same regional bloc as the actual country (e.g. New Zealand) -- not a shared border" },
];

interface Props {
  sportSlug?: string;
}

export default function ClueLegend({ sportSlug }: Props) {
  const examples = sportSlug?.startsWith("cricket-") ? CRICKET_EXAMPLES : TENNIS_EXAMPLES;

  return (
    <ul className="flex flex-col gap-2">
      {examples.map((ex) => (
        <li key={ex.description} className="flex items-center gap-3">
          <span
            className={`inline-flex items-center gap-1 px-3 py-1 rounded-full text-sm font-semibold flex-shrink-0 whitespace-nowrap ${STATE_CLASSES[ex.state]}`}
          >
            {PREFIX_ICONS.has(ex.icon) ? `${ex.icon} ${ex.pill}` : `${ex.pill} ${ex.icon}`}
          </span>
          <span className="text-[var(--text-secondary)] text-sm">{ex.description}</span>
        </li>
      ))}
    </ul>
  );
}