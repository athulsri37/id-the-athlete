import { Difficulty } from "../types";

interface Props {
  mode: Difficulty;
  onChange: (mode: Difficulty) => void;
}

const MODES: { key: Difficulty; label: string }[] = [
  { key: "daily", label: "Daily" },
  { key: "easy", label: "Easy" },
  { key: "medium", label: "Medium" },
  { key: "hard", label: "Hard" },
];

export default function ModeSelector({ mode, onChange }: Props) {
  return (
    <div className="flex gap-2 justify-center flex-wrap">
      {MODES.map((m) => (
        <button
          key={m.key}
          onClick={() => onChange(m.key)}
          className={`px-4 py-1.5 rounded-full text-sm font-semibold transition-colors ${
            mode === m.key
              ? "bg-ace-500 text-court-green"
              : "bg-white/10 text-court-chalk hover:bg-white/20"
          }`}
        >
          {m.label}
        </button>
      ))}
    </div>
  );
}
