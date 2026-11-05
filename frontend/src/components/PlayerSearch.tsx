import { useMemo, useState } from "react";
import { PlayerSummary } from "../types";
import { nameMatches } from "../utils/searchText";

interface Props {
  players: PlayerSummary[];
  guessedIds: Set<number>;
  disabled: boolean;
  placeholderOverride?: string;
  onGuess: (player: PlayerSummary) => void;
}

export default function PlayerSearch({ players, guessedIds, disabled, placeholderOverride, onGuess }: Props) {
  const [query, setQuery] = useState("");
  const [open, setOpen] = useState(false);

  const matches = useMemo(() => {
    if (!query.trim()) return [];
    return players
      .filter((p) => !guessedIds.has(p.id) && nameMatches(p.name, query))
      .slice(0, 8);
  }, [query, players, guessedIds]);

  const pick = (player: PlayerSummary) => {
    onGuess(player);
    setQuery("");
    setOpen(false);
  };

  return (
    // The suggestions sit above everything below them, the clue grid's pinned
    // cells included (z-30 against their z-[1]). The field's text is 16px on
    // phones: iOS Safari zooms the whole page into a smaller one when tapped.
    <div className="relative w-full max-w-md">
      <input
        type="text"
        value={query}
        disabled={disabled}
        onChange={(e) => {
          setQuery(e.target.value);
          setOpen(true);
        }}
        placeholder={disabled ? placeholderOverride ?? "Game over" : "Type a player name"}
        className="w-full rounded-md border-[3px] border-[var(--border-strong)] bg-[var(--bg-card)] px-4 py-2.5 text-base sm:text-sm font-medium text-[var(--text-primary)] placeholder:text-[var(--text-muted)] shadow-[4px_4px_0px_0px_var(--border-strong)] focus:outline-none focus:border-[var(--accent)] disabled:opacity-50"
      />
      {open && matches.length > 0 && (
        <ul className="absolute z-30 mt-1 w-full rounded-md border-[3px] border-[var(--border-strong)] bg-[var(--bg-card)] shadow-[4px_4px_0px_0px_var(--border-strong)] overflow-hidden">
          {matches.map((p) => (
            <li key={p.id}>
              <button
                onClick={() => pick(p)}
                className="w-full text-left px-4 py-2 text-sm hover:bg-[var(--bg-primary)] text-[var(--text-primary)]"
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