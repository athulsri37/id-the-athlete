import { AttributeDefinition, GuessResponse } from "../types";
import { CLUE_STATE_CLASSES, clueState } from "../utils/clueState";
import ResultIcon from "./ResultIcon";

interface Props {
  guesses: GuessResponse[];
  revealedCountry?: string | null;
  // This sport's attribute set, in display order -- drives both the column
  // headers and the grid's column count. Never a fixed list: Tennis has 8
  // attributes, Cricket has 9, and future sports may have a different count
  // again, so the header row can't assume any particular sport's shape.
  attributes: AttributeDefinition[];
}

// Responsive cell padding: generous on wide screens, compact on phones, where
// a clue's text wraps inside its cell instead of spilling into the next one.
const CELL_PADDING = "px-1.5 py-2 sm:[padding:clamp(14px,2vw,24px)_clamp(8px,1.8vw,22px)]";

// Column widths in rem: the guess number, the player, and each clue. The
// grid never gets narrower than these add up to. It's shown on screens
// 1024px wide and up, which fit it (phones get GuessCards instead); should a
// window ever be narrower, it scrolls sideways with the number and player
// pinned (STICKY_*) so each row stays readable.
const NUMBER_COL = 2;
const PLAYER_COL = 6.5;
const CLUE_COL = 4.75;
const GAP = 0.375; // gap-1.5
// Rows are padded 0.25rem (p-1), so the pinned cells stick where they sit.
// z-[1] lifts them over the clues scrolling beneath, and no higher: the
// player search's suggestions open over the grid.
const STICKY_NUMBER = "sticky left-1 z-[1]";
const STICKY_PLAYER = "sticky left-[2.625rem] z-[1]"; // row padding + NUMBER_COL + GAP

export default function ClueGrid({ guesses, revealedCountry, attributes }: Props) {
  // Two fixed leading columns (guess number, player name) plus one per
  // attribute. Tailwind's grid-cols-N utilities only cover a fixed set of
  // class names its scanner can see in source, which can't work for a
  // column count that varies by sport -- so this goes through an inline
  // style instead, matching what grid-cols-N generates under the hood.
  const gridStyle = {
    gridTemplateColumns: `${NUMBER_COL}rem minmax(${PLAYER_COL}rem, 1.3fr) repeat(${attributes.length}, minmax(${CLUE_COL}rem, 1fr))`,
  };
  const minWidth = `${NUMBER_COL + PLAYER_COL + attributes.length * CLUE_COL + (attributes.length + 1) * GAP + 0.5}rem`;

  return (
    <div className="mt-6 max-w-[96vw] overflow-x-auto">
      <div style={{ minWidth }}>
        <div className="grid gap-1.5 mb-1.5 rounded-md bg-[var(--text-primary)] p-1" style={gridStyle}>
          <div className={`${STICKY_NUMBER} grid-sticky-head`}></div>
          <div className={`${STICKY_PLAYER} grid-sticky-head text-xs font-semibold text-[var(--bg-primary)] ${CELL_PADDING}`}>
            Player
          </div>
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
              <div className={`${STICKY_NUMBER} ${idx % 2 === 1 ? "grid-sticky-alt" : "grid-sticky"} flex items-center justify-center`}>
                <span className="flex items-center justify-center w-7 h-7 rounded-full bg-[var(--accent-alt)] text-[var(--on-accent-alt)] text-xs font-bold">
                  {idx + 1}
                </span>
              </div>
              <div className={`${STICKY_PLAYER} ${idx % 2 === 1 ? "grid-sticky-alt" : "grid-sticky"} flex`}>
                <div
                  className={`flex flex-1 items-center rounded-2xl sm:rounded-full border border-[var(--border)] bg-[var(--bg-card)] text-[var(--text-primary)] text-xs sm:text-sm font-medium break-words leading-snug min-w-0 ${CELL_PADDING}`}
                >
                  {g.guessedPlayerName}
                </div>
              </div>
              {g.clues.map((c) => {
                const state = clueState(c, revealedCountry);
                return (
                  <div
                    key={c.attributeKey}
                    className={`flex flex-wrap items-center justify-center gap-x-1 min-w-0 rounded-2xl sm:rounded-full text-center text-xs sm:text-sm font-semibold leading-tight [overflow-wrap:anywhere] ${CELL_PADDING} ${CLUE_STATE_CLASSES[state]}`}
                    title={c.label}
                  >
                    <span>{c.value}</span>
                    <ResultIcon state={state} direction={c.direction} />
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