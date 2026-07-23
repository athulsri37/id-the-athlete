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

// Icons that stand alone as a prefix (✓/✕/~); direction arrows (▲/▼) are
// suffixed onto the numeric value instead.
const PREFIX_ICONS = new Set(["✓", "✕", "~"]);

const EXAMPLES: ClueExample[] = [
  { pill: "Right", icon: "✓", state: "match", description: "Match" },
  { pill: "18", icon: "▲", state: "close", description: "Close — near the actual value (e.g. 18 titles when the answer is 20)" },
  { pill: "France", icon: "~", state: "close", description: "Close — a nearby country, not exact" },
  { pill: "8", icon: "▲", state: "miss", description: "No match — actual value is higher" },
  { pill: "20", icon: "▼", state: "miss", description: "No match — actual value is lower" },
  { pill: "USA", icon: "✕", state: "miss", description: "No match" },
];

export default function ClueLegend() {
  return (
    <ul className="flex flex-col gap-2">
      {EXAMPLES.map((ex) => (
        <li key={ex.description} className="flex items-center gap-3">
          <span
            className={`inline-flex items-center gap-1 px-3 py-1 rounded-full text-sm font-semibold ${STATE_CLASSES[ex.state]}`}
          >
            {PREFIX_ICONS.has(ex.icon) ? `${ex.icon} ${ex.pill}` : `${ex.pill} ${ex.icon}`}
          </span>
          <span className="text-[var(--text-secondary)] text-sm">{ex.description}</span>
        </li>
      ))}
    </ul>
  );
}