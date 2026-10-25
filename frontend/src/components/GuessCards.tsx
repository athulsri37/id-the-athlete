import { useState } from "react";
import { ClueResult, GuessResponse } from "../types";
import { CLUE_STATE_CLASSES, clueState } from "../utils/clueState";
import ResultIcon from "./ResultIcon";

interface Props {
  guesses: GuessResponse[];
  revealedCountry?: string | null;
}

// The guesses on phones and portrait tablets, where ClueGrid's table is too
// wide: newest first, the latest as an open card with every clue labelled,
// earlier ones folded to their name and a coloured square per clue. Tapping
// a folded guess opens it until it's tapped again; the next guess folds them
// all again. Built from the theme's .card, so each theme styles it like its
// other cards; only the latest gets the strong border.
export default function GuessCards({ guesses, revealedCountry }: Props) {
  // The earlier guesses opened, and how many guesses there were when they
  // were: once another guess arrives this no longer applies, so all fold.
  const [opened, setOpened] = useState<{ count: number; indexes: number[] }>({ count: 0, indexes: [] });
  const openIndexes = opened.count === guesses.length ? opened.indexes : [];

  if (guesses.length === 0) {
    return (
      <p className="text-[var(--text-muted)] text-sm italic text-center py-4 mt-5">
        Make your first guess to see clues appear here.
      </p>
    );
  }

  const toggle = (index: number) =>
    setOpened({
      count: guesses.length,
      indexes: openIndexes.includes(index) ? openIndexes.filter((i) => i !== index) : [...openIndexes, index],
    });

  const latestIndex = guesses.length - 1;
  const latest = guesses[latestIndex];
  const earlier = guesses.slice(0, latestIndex).map((guess, index) => ({ guess, index })).reverse();

  return (
    <div className="mt-5 flex w-full max-w-md flex-col gap-3">
      <section
        aria-label={`Guess ${latestIndex + 1}: ${latest.guessedPlayerName}`}
        className="card flex flex-col gap-3 rounded-2xl p-3.5"
        style={{ borderColor: "var(--border-strong)" }}
      >
        <div className="flex items-center gap-2.5">
          <GuessNumber n={latestIndex + 1} />
          <span className="text-base font-semibold text-[var(--text-primary)]">{latest.guessedPlayerName}</span>
        </div>
        <ClueCells clues={latest.clues} revealedCountry={revealedCountry} />
      </section>

      {earlier.map(({ guess, index }) => {
        const label = `Guess ${index + 1}: ${guess.guessedPlayerName}`;
        return openIndexes.includes(index) ? (
          <section key={index} className="card flex flex-col gap-3 rounded-2xl p-3.5">
            <button
              onClick={() => toggle(index)}
              aria-expanded="true"
              aria-label={label}
              className="flex min-h-11 items-center gap-2.5 text-left text-[var(--text-primary)]"
            >
              <GuessNumber n={index + 1} />
              <span className="min-w-0 flex-1 text-base font-semibold">{guess.guessedPlayerName}</span>
              <Chevron open />
            </button>
            <ClueCells clues={guess.clues} revealedCountry={revealedCountry} />
          </section>
        ) : (
          <button
            key={index}
            onClick={() => toggle(index)}
            aria-expanded="false"
            aria-label={label}
            className="card flex min-h-14 w-full items-center gap-2.5 rounded-2xl px-3 py-2 text-left text-[var(--text-primary)] focus-visible:outline focus-visible:outline-2 focus-visible:outline-[var(--accent)]"
          >
            <GuessNumber n={index + 1} />
            <span className="min-w-0 flex-1 truncate text-[15px] font-semibold">{guess.guessedPlayerName}</span>
            <span aria-hidden="true" className="flex flex-none gap-[3px]">
              {guess.clues.map((c) => {
                const state = clueState(c, revealedCountry);
                return (
                  <span
                    key={c.attributeKey}
                    data-state={state}
                    className={`clue-dot h-2.5 w-2.5 rounded-sm ${CLUE_STATE_CLASSES[state]}`}
                  />
                );
              })}
            </span>
            <Chevron open={false} />
          </button>
        );
      })}
    </div>
  );
}

// Each clue under its label. Three to a row when the clues divide by three
// (Cricket's 9), otherwise four (Tennis's 8), so no row is left nearly
// empty. In a row, the labels line up at the top and the values at the
// bottom, however many lines either takes.
function ClueCells({ clues, revealedCountry }: { clues: ClueResult[]; revealedCountry?: string | null }) {
  const columns = clues.length % 3 === 0 ? 3 : 4;
  return (
    <div className="grid gap-x-1.5 gap-y-3" style={{ gridTemplateColumns: `repeat(${columns}, minmax(0, 1fr))` }}>
      {clues.map((c) => {
        const state = clueState(c, revealedCountry);
        return (
          <div key={c.attributeKey} className="flex min-w-0 flex-col gap-1">
            <span className="text-center text-[10px] font-semibold uppercase leading-tight tracking-wide text-[var(--text-secondary)]">
              {c.label}
            </span>
            <span
              className={`mt-auto flex min-h-10 flex-wrap items-center justify-center gap-x-1 rounded-lg px-1 py-1.5 text-center text-[13px] font-semibold leading-tight [overflow-wrap:anywhere] ${CLUE_STATE_CLASSES[state]}`}
            >
              <span>{c.value}</span>
              <ResultIcon state={state} direction={c.direction} />
            </span>
          </div>
        );
      })}
    </div>
  );
}

function GuessNumber({ n }: { n: number }) {
  return (
    <span className="flex h-6 w-6 flex-none items-center justify-center rounded-full bg-[var(--accent-alt)] text-xs font-bold text-[var(--on-accent-alt)]">
      {n}
    </span>
  );
}

function Chevron({ open }: { open: boolean }) {
  return (
    <svg
      aria-hidden="true"
      width="16"
      height="16"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2.4"
      strokeLinecap="round"
      strokeLinejoin="round"
      className="flex-none opacity-70"
    >
      <path d={open ? "M6 15 L12 9 L18 15" : "M6 9 L12 15 L18 9"} />
    </svg>
  );
}
