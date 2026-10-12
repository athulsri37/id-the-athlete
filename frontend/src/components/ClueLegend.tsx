import { ClueExample, familyDisplay } from "../config/sportFamilies";

const STATE_CLASSES: Record<ClueExample["state"], string> = {
  match: "bg-[var(--accent)] text-[var(--on-accent)]",
  close: "bg-[var(--close-bg)] text-[var(--text-primary)]",
  miss: "bg-[var(--miss-bg)] text-[var(--text-primary)]",
};

// Icons that stand alone as a prefix (✓/✕/≈); direction arrows (▲/▼) are
// suffixed onto the numeric value instead.
const PREFIX_ICONS = new Set(["✓", "✕", "≈"]);

interface Props {
  family: string;
}

// Each sport family has its own examples, since what counts as "close"
// differs between sports (see config/sportFamilies).
export default function ClueLegend({ family }: Props) {
  const examples = familyDisplay(family).legendExamples;

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