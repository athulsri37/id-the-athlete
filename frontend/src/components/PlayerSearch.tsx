import { useMemo, useState } from "react";
import { PlayerSummary } from "../types";

interface Props {
  players: PlayerSummary[];
  guessedIds: Set<number>;
  disabled: boolean;
  onGuess: (player: PlayerSummary) => void;
}

export default function PlayerSearch({ players, guessedIds, disabled, onGuess }: Props) {
  const [query, setQuery] = useState("");
  const [open, setOpen] = useState(false);

  const matches = useMemo(() => {
    if (!query.trim()) return [];
    const q = query.toLowerCase();
    return players
      .filter((p) => !guessedIds.has(p.id) && p.name.toLowerCase().includes(q))
      .slice(0, 8);
  }, [query, players, guessedIds]);

  const pick = (player: PlayerSummary) => {
    onGuess(player);
    setQuery("");
    setOpen(false);
  };

  return (
    <div className="relative w-full max-w-md">
      <input
        type="text"
        value={query}
        disabled={disabled}
        onChange={(e) => {
          setQuery(e.target.value);
          setOpen(true);
        }}
        placeholder={disabled ? "Game over" : "Type a player name..."}
        className="w-full rounded-md border-2 border-court-green/30 bg-white px-4 py-2.5 text-sm font-medium text-court-green placeholder:text-court-green/40 focus:outline-none focus:border-ace-600 disabled:bg-slate-100 disabled:text-slate-400"
      />
      {open && matches.length > 0 && (
        <ul className="absolute z-10 mt-1 w-full rounded-md border border-court-green/20 bg-white shadow-lg overflow-hidden">
          {matches.map((p) => (
            <li key={p.id}>
              <button
                onClick={() => pick(p)}
                className="w-full text-left px-4 py-2 text-sm hover:bg-ace-500/20 text-court-green"
              >
                {p.name}
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
