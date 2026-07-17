import { useEffect } from "react";
import { Sport, Difficulty } from "../types";

interface Props {
  sport: Sport;
  onSelectMode: (mode: Difficulty) => void;
  onBack: () => void;
}

const MODES: { key: Difficulty; label: string }[] = [
  { key: "daily", label: "Daily Challenge" },
  { key: "easy", label: "Easy" },
  { key: "medium", label: "Medium" },
  { key: "hard", label: "Hard" },
];

export default function SportHome({ sport, onSelectMode, onBack }: Props) {
  useEffect(() => {
    document.title = `ID the ${sport.name} Player | ID the Athlete`;
  }, [sport]);

  return (
    <div className="min-h-screen bg-[var(--bg-primary)] flex flex-col items-center px-4 py-10">
      <button
        onClick={onBack}
        className="self-start text-sm text-[var(--text-secondary)] underline hover:text-[var(--text-primary)] mb-6"
      >
        ← Back
      </button>

      <h1 className="font-heading text-5xl tracking-wide mb-1">
        <span className="text-[var(--text-primary)]">ID the </span>
        <span className="text-[var(--accent-alt)]">{sport.name}</span>
        <span className="text-[var(--text-primary)]"> Player</span>
      </h1>
      <p className="text-[var(--text-secondary)] text-sm mb-10">Choose a mode to get started</p>

      <div className="flex flex-col gap-3 w-full max-w-xs">
        {MODES.map((m) => (
          <button
            key={m.key}
            onClick={() => onSelectMode(m.key)}
            className="px-5 py-3 rounded-md bg-[var(--accent)] hover:bg-[var(--accent-hover)] text-[var(--on-accent)] font-semibold text-lg transition-colors"
          >
            {m.label}
          </button>
        ))}
      </div>
    </div>
  );
}