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
    <div className="min-h-screen bg-court-green flex flex-col items-center px-4 py-10">
      <button
        onClick={onBack}
        className="self-start text-sm text-court-chalk/70 underline hover:text-court-chalk mb-6"
      >
        ← Back
      </button>

      <h1 className="font-display text-5xl tracking-wide text-ace-500 mb-1">
        ID the {sport.name} Player
      </h1>
      <p className="text-court-chalk/70 text-sm mb-10">Choose a mode to get started</p>

      <div className="flex flex-col gap-3 w-full max-w-xs">
        {MODES.map((m) => (
          <button
            key={m.key}
            onClick={() => onSelectMode(m.key)}
            className="px-5 py-3 rounded-md bg-ace-500 hover:bg-ace-600 text-court-green font-semibold text-lg transition-colors"
          >
            {m.label}
          </button>
        ))}
      </div>
    </div>
  );
}