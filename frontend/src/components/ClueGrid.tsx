import { AttributeDefinition, GuessResponse } from "../types";

interface Props {
  guesses: GuessResponse[];
  revealedCountry?: string | null;
  // This sport's attribute set, in display order -- drives both the column
  // headers and the grid's column count. Never a fixed list: Tennis has 8
  // attributes, Cricket has 9, and future sports may have a different count
  // again, so the header row can't assume any particular sport's shape.
  attributes: AttributeDefinition[];
}

// Responsive cell padding: generous on wide screens, compresses on narrow ones
// instead of forcing the table to overflow.
const CELL_PADDING = "[padding:clamp(14px,2vw,24px)_clamp(8px,1.8vw,22px)]";

function ResultIcon({
  isMatch,
  isClose,
  direction,
}: {
  isMatch: boolean;
  isClose: boolean;
  direction: "up" | "down" | null;
}) {
  if (isMatch) return <span className="ml-1 text-xs">✓</span>;
  // Direction takes priority over closeness for numeric attributes -- the
  // arrow is still the more useful signal, closeness only changes the pill
  // color. Categorical attributes (country, and Cricket's Role/Bowling
  // Style) have no direction, so they get a distinct "approximately" mark
  // instead.
  if (direction) return <span className="ml-1 text-xs">{direction === "up" ? "▲" : "▼"}</span>;
  if (isClose) return <span className="ml-1 text-xs">≈</span>;
  return <span className="ml-1 text-xs">✕</span>;
}

export default function ClueGrid({ guesses, revealedCountry, attributes }: Props) {
  // Two fixed leading columns (guess number, player name) plus one per
  // attribute. Tailwind's grid-cols-N utilities only cover a fixed set of
  // class names its scanner can see in source, which can't work for a
  // column count that varies by sport -- so this goes through an inline
  // style instead, matching what grid-cols-N generates under the hood.
  const gridStyle = { gridTemplateColumns: `repeat(${2 + attributes.length}, minmax(0, 1fr))` };

  return (
    <div className="mt-6 max-w-[96vw] overflow-x-auto">
      <div className="min-w-[720px]">
        <div className="grid gap-1.5 mb-1.5 rounded-md bg-[var(--text-primary)] p-1" style={gridStyle}>
          <div className={`text-xs font-semibold text-[var(--bg-primary)] ${CELL_PADDING}`}></div>
          <div className={`text-xs font-semibold text-[var(--bg-primary)] ${CELL_PADDING}`}>Player</div>
          {attributes.map((a) => (
            <div key={a.key} className={`text-xs font-semibold text-[var(--bg-primary)] text-center ${CELL_PADDING}`}>
              {a.label}
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
              className={`grid gap-1.5 mb-1.5 rounded-md p-1 ${idx % 2 === 1 ? "bg-[var(--row-alt-bg)]" : ""}`}
              style={gridStyle}
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
                const isClose = !isMatch && c.isClose;

                return (
                  <div
                    key={c.attributeKey}
                    className={`flex items-center justify-center rounded-full text-sm font-semibold ${CELL_PADDING} ${
                      isMatch
                        ? "bg-[var(--accent)] text-[var(--on-accent)]"
                        : isClose
                        ? "bg-[var(--close-bg)] text-[var(--text-primary)]"
                        : "bg-[var(--miss-bg)] text-[var(--text-primary)]"
                    }`}
                    title={c.label}
                  >
                    {c.value}
                    <ResultIcon isMatch={isMatch} isClose={isClose} direction={c.direction} />
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