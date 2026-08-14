import { useEffect, useState } from "react";
import { Sport } from "../types";
import { fetchDailyPuzzleDates } from "../api/client";
import { getCompletionForDate } from "../utils/dailyCompletion";
import { todayUtcDateString } from "../utils/localDate";

interface Props {
  sport: Sport;
  onSelectDate: (date: string) => void;
  onBack: () => void;
}

interface DateRow {
  date: string;
  badge: string;
  dayName: string;
  status: string;
  done: boolean;
}

function formatDateParts(dateStr: string): { badge: string; dayName: string } {
  const d = new Date(`${dateStr}T00:00:00`);
  return {
    badge: d.toLocaleDateString(undefined, { month: "short", day: "numeric" }),
    dayName: d.toLocaleDateString(undefined, { weekday: "long" }),
  };
}

// Fixed row height so the list container's height maps to a predictable
// row count (15 rows * 56px), matching the "~15 rows before scrolling" spec.
const ROW_HEIGHT_PX = 56;
const VISIBLE_ROWS = 15;

export default function PastChallenges({ sport, onSelectDate, onBack }: Props) {
  const [rows, setRows] = useState<DateRow[] | null>(null);
  const [error, setError] = useState("");

  useEffect(() => {
    document.title = `Past Challenges — ID the ${sport.name} Player | ID the Athlete`;
  }, [sport]);

  useEffect(() => {
    setRows(null);
    setError("");
    // UTC, not browser-local: DailyPuzzleGenerationService (and GameBoard's
    // undated "today" puzzle resolution) both key off DateTime.UtcNow, so
    // this exclusion has to use the same definition of "today" or it can
    // both wrongly exclude a genuine past day and wrongly admit today's
    // still-current puzzle as a normal past row.
    const today = todayUtcDateString();

    fetchDailyPuzzleDates(sport.slug)
      .then((dates) => {
        // Today has its own dedicated Daily Challenge button. Excluding it
        // here guarantees a Past Challenges play can never accidentally be
        // "today" -- which is what keeps this screen from ever being able
        // to touch the streak (see GameBoard's isPastDate guard).
        const pastDates = dates.filter((date) => date !== today);

        const built = pastDates.map((date): DateRow => {
          const { badge, dayName } = formatDateParts(date);
          const completion = getCompletionForDate(sport.slug, date);

          if (!completion) {
            return { date, badge, dayName, status: "Not yet played", done: false };
          }

          if (completion.won) {
            const n = completion.guesses.length;
            return { date, badge, dayName, status: `Won in ${n} guess${n === 1 ? "" : "es"}`, done: true };
          }

          return { date, badge, dayName, status: "Lost — 8/8 used", done: true };
        });

        setRows(built);
      })
      .catch(() => setError("Couldn't load past challenges."));
  }, [sport.slug]);

  return (
    <div className="min-h-screen bg-[var(--bg-primary)] flex flex-col items-center px-4 py-10">
      <button
        onClick={onBack}
        className="self-start text-sm text-[var(--text-secondary)] underline hover:text-[var(--text-primary)] mb-6"
      >
        ← Back
      </button>

      <h1 className="font-heading text-5xl tracking-wide mb-1">
        <span className="text-[var(--text-primary)]">Past </span>
        <span className="text-[var(--accent-alt)]">Challenges</span>
      </h1>
      <p className="text-[var(--text-secondary)] text-sm mb-8 max-w-md text-center">
        Catch up on daily puzzles you missed. These don't count toward your streak.
      </p>

      {error && <p className="text-[var(--accent-alt)] text-sm mb-4">{error}</p>}

      {rows === null && !error && <p className="text-[var(--text-muted)] text-sm">Loading…</p>}

      {rows !== null && rows.length === 0 && (
        <p className="text-[var(--text-muted)] text-sm italic">No past challenges yet — check back tomorrow.</p>
      )}

      {rows !== null && rows.length > 0 && (
        <div
          className="card rounded-md w-full max-w-md overflow-y-auto flex flex-col"
          style={{ height: `${ROW_HEIGHT_PX * VISIBLE_ROWS}px` }}
        >
          {rows.map((row, idx) => (
            <button
              key={row.date}
              onClick={() => onSelectDate(row.date)}
              style={{ height: `${ROW_HEIGHT_PX}px` }}
              className={`w-full flex-shrink-0 flex items-center gap-3 px-4 text-left border-b border-[var(--border)] last:border-b-0 hover:bg-[var(--row-alt-bg)] transition-colors ${
                idx % 2 === 1 ? "bg-[var(--row-alt-bg)]" : ""
              }`}
            >
              <span className="flex-shrink-0 text-xs font-bold text-[var(--on-accent-alt)] bg-[var(--accent-alt)] rounded-md px-2 py-1 w-16 text-center">
                {row.badge}
              </span>
              <span className="flex-1 min-w-0">
                <span className="block text-sm font-semibold text-[var(--text-primary)] truncate">{row.dayName}</span>
                <span className="block text-xs text-[var(--text-muted)] truncate">{row.status}</span>
              </span>
              <span
                className={`flex-shrink-0 text-xs font-semibold px-3 py-1.5 rounded-full whitespace-nowrap ${
                  row.done
                    ? "bg-[var(--row-alt-bg)] text-[var(--text-muted)]"
                    : "border border-[var(--border-strong)] text-[var(--text-primary)]"
                }`}
              >
                {row.done ? "✓ Done" : "Play"}
              </span>
            </button>
          ))}
        </div>
      )}
    </div>
  );
}