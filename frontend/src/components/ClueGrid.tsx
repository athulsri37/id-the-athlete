import { GuessResponse } from "../types";

interface Props {
  guesses: GuessResponse[];
  revealedCountry?: string | null;
}

const LABELS = ["Status", "Plays", "Backhand", "Country", "Slams", "Highest Rank", "Pro Yr", "Titles"];

// Responsive cell padding: generous on wide screens, compresses on narrow ones
// instead of forcing the table to overflow.
const CELL_PADDING = "[padding:clamp(14px,2vw,24px)_clamp(8px,1.8vw,22px)]";

function ResultIcon({ isMatch, direction }: { isMatch: boolean; direction: "up" | "down" | null }) {
  if (isMatch) return <span className="ml-1 text-xs">✓</span>;
  if (direction) return <span className="ml-1 text-xs">{direction === "up" ? "▲" : "▼"}</span>;
  return <span className="ml-1 text-xs">✕</span>;
}

export default function ClueGrid({ guesses, revealedCountry }: Props) {
  return (
    <div className="mt-6 max-w-[96vw] overflow-x-auto">
      <div className="min-w-[720px]">
        <div className="grid grid-cols-10 gap-1.5 mb-1.5 rounded-md bg-[var(--text-primary)] p-1">
          <div className={`text-xs font-semibold text-[var(--bg-primary)] ${CELL_PADDING}`}></div>
          <div className={`text-xs font-semibold text-[var(--bg-primary)] ${CELL_PADDING}`}>Player</div>
          {LABELS.map((l) => (
            <div key={l} className={`text-xs font-semibold text-[var(--bg-primary)] text-center ${CELL_PADDING}`}>
              {l}
            </div>
          ))}
        </div>

        {guesses.length === 0 ? (
          <p className="text-[var(--text-muted)] text-sm italic text-center py-4">
            Make your first guess to see clues appear here.
          </p>
        ) : (
          guesses.map((g, idx) => (
            <div
              key={idx}
              className={`grid grid-cols-10 gap-1.5 mb-1.5 rounded-md p-1 ${idx % 2 === 1 ? "bg-[var(--row-alt-bg)]" : ""}`}
            >
              <div className="flex items-center justify-center">
                <span className="flex items-center justify-center w-7 h-7 rounded-full bg-[var(--accent-alt)] text-[var(--on-accent-alt)] text-xs font-bold">
                  {idx + 1}
                </span>
              </div>
              <div
                className={`flex items-center rounded-full border border-[var(--border)] bg-[var(--bg-card)] text-[var(--text-primary)] text-sm font-medium truncate ${CELL_PADDING}`}
              >
                {g.guessedPlayerName}
              </div>
              {g.clues.map((c) => {
                // Once the country hint is revealed, drive that column's
                // match state off the revealed value instead of the
                // server-computed isMatch, per the hint feature's spec —
                // in practice these always agree (isMatch was already
                // computed against the same true country), this just makes
                // the dependency on the revealed value explicit.
                const isMatch =
                  c.attributeKey === "country" && revealedCountry
                    ? c.value.toLowerCase() === revealedCountry.toLowerCase()
                    : c.isMatch;

                return (
                  <div
                    key={c.attributeKey}
                    className={`flex items-center justify-center rounded-full text-sm font-semibold ${CELL_PADDING} ${
                      isMatch ? "bg-[var(--accent)] text-[var(--on-accent)]" : "bg-[var(--miss-bg)] text-[var(--text-primary)]"
                    }`}
                    title={c.label}
                  >
                    {c.value}
                    <ResultIcon isMatch={isMatch} direction={c.direction} />
                  </div>
                );
              })}
            </div>
          ))
        )}
      </div>
    </div>
  );
}