import { useEffect, useState } from "react";
import { Sport, Difficulty } from "../types";
import ClueLegend from "../components/ClueLegend";

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
  const [showHowToPlay, setShowHowToPlay] = useState(false);

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
      <p className="text-[var(--text-secondary)] text-sm mb-2">Choose a mode to get started</p>
      <p className="text-[var(--text-muted)] text-xs mb-8">
        Tap "How to play ?" below to learn the rules
      </p>

      <div className="flex flex-wrap justify-center gap-3 max-w-lg">
        {MODES.map((m) => (
          <button
            key={m.key}
            onClick={() => onSelectMode(m.key)}
            className="btn-card px-6 py-3 rounded-md font-semibold text-lg"
          >
            {m.label}
          </button>
        ))}
      </div>

      <button
        onClick={() => setShowHowToPlay((v) => !v)}
        className="btn-card px-5 py-2 rounded-md font-semibold text-sm mt-6"
      >
        How to play ?
      </button>

      {showHowToPlay && (
        <div className="card rounded-md p-5 mt-4 w-full max-w-md text-left">
          <div className="mb-4">
            <h2 className="text-xs font-bold text-[var(--text-primary)] uppercase tracking-wide mb-1">
              Objective
            </h2>
            <p className="text-[var(--text-secondary)] text-sm">
              Guess the mystery ATP player in 8 tries using clues.
            </p>
          </div>

          <div className="mb-4">
            <h2 className="text-xs font-bold text-[var(--text-primary)] uppercase tracking-wide mb-2">
              Reading clues
            </h2>
            <ClueLegend />
          </div>

          <div>
            <h2 className="text-xs font-bold text-[var(--text-primary)] uppercase tracking-wide mb-2">
              Modes
            </h2>
            <ul className="text-[var(--text-secondary)] text-sm flex flex-col gap-1">
              <li>
                <strong className="text-[var(--text-primary)]">Daily Challenge</strong> — one shared
                puzzle per day, resets at midnight
              </li>
              <li>
                <strong className="text-[var(--text-primary)]">Easy / Medium / Hard</strong> — unlimited
                practice, different player eras
              </li>
            </ul>
          </div>
        </div>
      )}
    </div>
  );
}