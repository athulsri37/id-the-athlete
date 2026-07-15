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
    <div className="min-h-screen bg-court-green flex flex-col items-center px-4 py-10">
      <h1 className="font-display text-5xl tracking-wide text-ace-500 mb-1">ID the Athlete</h1>
      <p className="text-court-chalk/70 text-sm mb-10 max-w-md text-center">
        Somewhere in the roster is a mystery athlete. Guess players and use the
        color-coded stat clues you get back — country, style, career numbers —
        to close in on who it is before you run out of tries.
      </p>

      <div className="flex flex-col gap-3 w-full max-w-xs">
        {SPORTS.map((sport) => (
          <button
            key={sport.slug}
            onClick={() => onSelectSport(sport)}
            className="px-5 py-3 rounded-md bg-ace-500 hover:bg-ace-600 text-court-green font-semibold text-lg transition-colors"
          >
            {sport.name}
          </button>
        ))}
      </div>
    </div>
  );
}