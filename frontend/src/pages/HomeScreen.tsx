import { useEffect } from "react";
import { Sport } from "../types";

interface Props {
  onSelectSport: (sport: Sport) => void;
}

const SPORTS: Sport[] = [{ slug: "tennis", name: "Tennis" }];

export default function HomeScreen({ onSelectSport }: Props) {
  useEffect(() => {
    document.title = "ID the Athlete";
  }, []);

  return (
    <div className="min-h-screen bg-[var(--bg-primary)] flex flex-col items-center px-4 py-10">
      <h1 className="font-heading text-5xl tracking-wide mb-1">
        <span className="text-[var(--text-primary)]">ID the </span>
        <span className="text-[var(--accent-alt)]">Athlete</span>
      </h1>
      <p className="text-[var(--text-secondary)] text-sm mb-10 max-w-md text-center">
        Somewhere in the roster is a mystery athlete. Guess players and use the
        color-coded stat clues you get back — country, style, career numbers —
        to close in on who it is before you run out of tries.
      </p>

      <div className="flex flex-col gap-3 w-full max-w-xs">
        {SPORTS.map((sport) => (
          <button
            key={sport.slug}
            onClick={() => onSelectSport(sport)}
            className="px-5 py-3 rounded-md bg-[var(--accent)] hover:bg-[var(--accent-hover)] text-[var(--on-accent)] font-semibold text-lg transition-colors"
          >
            {sport.name}
          </button>
        ))}
      </div>
    </div>
  );
}